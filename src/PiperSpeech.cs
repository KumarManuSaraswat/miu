using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Miu
{
    // A separate native process owns the neural model only during synthesis.
    // Text, stderr, raw PCM and the WAV wrapper are never written to disk.
    internal static class PiperSpeech
    {
        private const int SampleRate = 22050;
        private const int MaximumPcmBytes = 16 * 1024 * 1024;
        private const int MaximumTextLength = 6500;
        private static readonly SemaphoreSlim VoiceGate = new SemaphoreSlim(1, 1);
        private static string VoiceDirectory { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "natural-voice"); } }
        private static string EnginePath { get { return Path.Combine(VoiceDirectory, "piper", "piper.exe"); } }
        private static string ModelPath { get { return Path.Combine(VoiceDirectory, "en_US-ljspeech-medium.onnx"); } }

        public static bool IsAvailable
        {
            get
            {
                return File.Exists(EnginePath) && File.Exists(ModelPath) && File.Exists(ModelPath + ".json") &&
                    Directory.Exists(Path.Combine(VoiceDirectory, "piper", "espeak-ng-data"));
            }
        }

        public static async Task SpeakAsync(string text, int rate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(text)) return;
            if (!IsAvailable) throw new InvalidOperationException("The natural offline voice is not installed.");
            if (text.Length > MaximumTextLength) throw new InvalidOperationException("This reply is too long for the natural voice.");
            await VoiceGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                // Native process waits and audio playback never block the UI or
                // retain a worker thread when there is no utterance in progress.
                await Task.Run(delegate
                {
                    using (var wave = new MemoryStream(65536))
                    {
                        try
                        {
                            Synthesize(text, rate, wave, token);
                            token.ThrowIfCancellationRequested();
                            Play(wave, token);
                        }
                        finally
                        {
                            // Clear our managed audio buffer after SoundPlayer is disposed.
                            byte[] buffer = wave.GetBuffer();
                            Array.Clear(buffer, 0, buffer.Length);
                        }
                    }
                }, token).ConfigureAwait(false);
            }
            finally { VoiceGate.Release(); }
        }

        private static void Synthesize(string text, int rate, MemoryStream wave, CancellationToken token)
        {
            double scale = Math.Max(0.55, Math.Min(1.8, Math.Pow(1.12, -Math.Max(-10, Math.Min(10, rate)))));
            var start = new ProcessStartInfo
            {
                FileName = EnginePath,
                WorkingDirectory = Path.GetDirectoryName(EnginePath),
                Arguments = "--model " + Quote(ModelPath) + " --config " + Quote(ModelPath + ".json") +
                    " --output_raw --quiet --length_scale " + scale.ToString("0.###", CultureInfo.InvariantCulture) +
                    " --sentence_silence 0.12",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            wave.Write(new byte[44], 0, 44);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var process = new Process { StartInfo = start })
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                bool started = false;
                try
                {
                    token.ThrowIfCancellationRequested();
                    started = process.Start();
                    if (!started) throw new InvalidOperationException("Natural voice could not start.");
                    try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                    using (timeout.Token.Register(delegate { TryTerminate(process); }))
                    {
                        // Drain both pipes concurrently before supplying text. No shell
                        // command or command-line argument ever contains spoken content.
                        Task output = Task.Run(delegate { ReadPcm(process, wave, timeout.Token); });
                        Task errors = Task.Run(delegate { DiscardErrors(process, timeout.Token); });
                        Task input = Task.Run(delegate { SendText(process, text, timeout.Token); });
                        Task complete = Task.WhenAll(output, errors, input);
                        // Observe faults even if timeout/termination prevents orderly IO completion.
                        complete.ContinueWith(delegate(Task failed) { var observed = failed.Exception; },
                            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

                        if (!process.WaitForExit(60000))
                        {
                            TryTerminate(process);
                            throw new TimeoutException("Natural voice took too long to generate this reply.");
                        }
                        if (!complete.Wait(5000)) throw new TimeoutException("Natural voice audio did not finish in time.");
                        token.ThrowIfCancellationRequested();
                        if (timeout.IsCancellationRequested) throw new TimeoutException("Natural voice generation timed out.");
                        if (process.ExitCode != 0) throw new InvalidOperationException("Natural voice generation failed.");
                    }
                }
                catch
                {
                    token.ThrowIfCancellationRequested();
                    if (timeout.IsCancellationRequested) throw new TimeoutException("Natural voice generation timed out.");
                    // Never surface native stderr: a phonemizer error can quote input text.
                    throw new InvalidOperationException("Natural voice could not generate this reply. Windows voice can be used instead.");
                }
                finally
                {
                    if (started)
                    {
                        TryTerminate(process);
                        ClosePipes(process);
                        try { process.WaitForExit(2000); }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                    }
                }
            }
            // The model process and its streams are gone before playback starts.
            int pcmLength = checked((int)wave.Length - 44);
            if (pcmLength < 2 || (pcmLength & 1) != 0)
                throw new InvalidOperationException("Natural voice returned invalid audio.");
            wave.Position = 0;
            using (var header = new BinaryWriter(wave, Encoding.ASCII, true))
            {
                header.Write(Encoding.ASCII.GetBytes("RIFF")); header.Write(pcmLength + 36);
                header.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); header.Write(16);
                header.Write((short)1); header.Write((short)1); header.Write(SampleRate);
                header.Write(SampleRate * 2); header.Write((short)2); header.Write((short)16);
                header.Write(Encoding.ASCII.GetBytes("data")); header.Write(pcmLength);
                header.Flush();
            }
            wave.Position = 0;
        }

        private static void ReadPcm(Process process, MemoryStream wave, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                Stream output = process.StandardOutput.BaseStream;
                int read;
                while ((read = output.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (wave.Length - 44 + read > MaximumPcmBytes)
                        throw new InvalidOperationException("Natural voice exceeded the audio size limit.");
                    int required = checked((int)wave.Length + read);
                    if (required > wave.Capacity)
                        wave.Capacity = Math.Min(MaximumPcmBytes + 44, Math.Max(required, wave.Capacity * 2));
                    wave.Write(buffer, 0, read);
                }
            }
            catch { TryTerminate(process); throw; }
            finally { Array.Clear(buffer, 0, buffer.Length); }
        }

        private static void DiscardErrors(Process process, CancellationToken token)
        {
            byte[] buffer = new byte[1024];
            try
            {
                Stream error = process.StandardError.BaseStream;
                while (error.Read(buffer, 0, buffer.Length) > 0) token.ThrowIfCancellationRequested();
            }
            catch { TryTerminate(process); throw; }
            finally { Array.Clear(buffer, 0, buffer.Length); }
        }

        private static void SendText(Process process, string text, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                // A single input line avoids the native CLI switching stdin mode
                // between paragraphs. Explicit UTF8 avoids Windows code-page loss.
                string line = text.Replace('\r', ' ').Replace('\n', ' ').Replace("\0", "");
                using (var input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                {
                    input.WriteLine(line);
                    input.Flush();
                }
            }
            catch { TryTerminate(process); throw; }
        }

        private static void Play(MemoryStream wave, CancellationToken token)
        {
            using (var player = new SoundPlayer(wave))
            {
                var playbackGate = new object();
                player.LoadTimeout = 5000;
                player.Load();
                using (token.Register(delegate
                {
                    // Cancel() synchronously stops sound before the microphone can
                    // start. The lock also closes the cancel-before-Play race.
                    lock (playbackGate) { try { player.Stop(); } catch (InvalidOperationException) { } }
                }))
                {
                    try
                    {
                        lock (playbackGate)
                        {
                            token.ThrowIfCancellationRequested();
                            player.Play();
                        }
                        int duration = checked((int)Math.Ceiling((wave.Length - 44) * 1000.0 / (SampleRate * 2))) + 150;
                        if (token.WaitHandle.WaitOne(duration)) token.ThrowIfCancellationRequested();
                    }
                    finally { lock (playbackGate) { player.Stop(); } }
                }
            }
        }

        private static string Quote(string path) { return "\"" + path + "\""; }
        private static void TryTerminate(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        private static void ClosePipes(Process process)
        {
            try { process.StandardInput.Close(); } catch (Exception) { }
            try { process.StandardOutput.Close(); } catch (Exception) { }
            try { process.StandardError.Close(); } catch (Exception) { }
        }
    }
}
