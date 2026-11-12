using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Miu
{
    // Built as a separate x64 executable. The process owns the model and microphone;
    // exiting (or cancelling this process) returns all recognition memory to Windows.
    public static class VoskWorker
    {
        private static readonly Regex TextPattern = new Regex("\\\"text\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.Compiled);
        private static readonly Regex PartialPattern = new Regex("\\\"partial\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.Compiled);
        private static readonly Regex ConfidencePattern = new Regex("\\\"conf\\\"\\s*:\\s*([0-9.]+)", RegexOptions.Compiled);

        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            IntPtr model = IntPtr.Zero;
            IntPtr recognition = IntPtr.Zero;
            Process parent = null;
            Timer parentWatch = null;
            int shuttingDown = 0;
            try
            {
                if (args.Length == 1 && args[0] == "--self-test") return RunOfflineTests();
                if (args.Length == 1 && args[0] == "--devices") { ListDevices(); return 0; }
                if (args.Length >= 1 && args.Length <= 2 && args[0] == "--probe")
                {
                    int seconds = 6;
                    if (args.Length == 2 && (!Int32.TryParse(args[1], out seconds) || seconds < 1 || seconds > 10))
                        throw new InvalidOperationException("A microphone probe must be between 1 and 10 seconds.");
                    Probe(seconds);
                    return 0;
                }
                bool parentMode = args.Length == 2 && args[0] == "--parent";
                bool waveMode = args.Length == 2 && (args[0] == "--wav" || args[0] == "--replay");
                bool checkMode = args.Length == 1 && args[0] == "--check";
                if (!parentMode && !waveMode && !checkMode)
                {
                    Write("HELP", "Launch voice from Miu. Diagnostics: --devices, --probe [1-10 seconds], --check, --self-test, --wav FILE, --replay FILE.");
                    return args.Length == 0 ? 0 : 64;
                }
                if (args.Length == 2 && args[0] == "--parent")
                {
                    int parentId;
                    if (!Int32.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out parentId)) return 3;
                    try { parent = Process.GetProcessById(parentId); if (parent.HasExited) return 3; }
                    catch (ArgumentException) { return 3; }
                    Process watchedParent = parent;
                    parentWatch = new Timer(delegate
                    {
                        if (Interlocked.CompareExchange(ref shuttingDown, 0, 0) != 0) return;
                        try { if (watchedParent.HasExited) Environment.Exit(3); }
                        catch { if (Interlocked.CompareExchange(ref shuttingDown, 0, 0) == 0) Environment.Exit(3); }
                    }, null, 500, 500);
                }
                string voiceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice");
                string modelPath = Path.Combine(voiceDirectory, "model");
                if (!File.Exists(Path.Combine(voiceDirectory, "libvosk.dll")) || !Directory.Exists(Path.Combine(modelPath, "am")))
                    throw new InvalidOperationException("Offline voice is not installed. Run scripts\\setup-voice.ps1 once, then try again.");
                Native.SetDllDirectory(voiceDirectory);
                Native.vosk_set_log_level(-1);
                Write("STATUS", "loading");
                byte[] pathBytes = Encoding.UTF8.GetBytes(modelPath + "\0");
                model = Native.vosk_model_new(pathBytes);
                if (model == IntPtr.Zero) throw new InvalidOperationException("The offline voice model could not load. Run setup-voice.ps1 again to repair it.");
                recognition = Native.vosk_recognizer_new(model, 16000.0f);
                if (recognition == IntPtr.Zero) throw new InvalidOperationException("The offline speech recognizer could not start.");
                Native.vosk_recognizer_set_words(recognition, 1);

                if (args.Length > 0 && args[0] == "--check")
                {
                    Write("CHECK", "ok");
                    return 0;
                }
                // Useful deterministic integration check: PCM WAV is consumed without any mic.
                if (waveMode)
                {
                    CaptureResult replay = RecognizeWaveFile(recognition, args[1], args[0] == "--replay");
                    EmitResult(replay);
                    return 0;
                }

                CaptureResult result;
                using (MicrophoneCapture microphone = new MicrophoneCapture())
                {
                    WriteText("DEVICE", microphone.DeviceName);
                    microphone.Start();
                    Write("LISTENING", "1");
                    result = Capture(recognition, microphone);
                }
                // This line is emitted only after waveIn has closed the microphone.
                Write("LISTENING", "0");
                EmitResult(result);
                return 0;
            }
            catch (DllNotFoundException)
            {
                WriteText("ERROR", "Offline voice files are missing. Run scripts\\setup-voice.ps1 to install them.");
                return 2;
            }
            catch (BadImageFormatException)
            {
                WriteText("ERROR", "Offline voice requires the x64 build of Miu and 64-bit Windows.");
                return 2;
            }
            catch (Exception ex)
            {
                WriteText("ERROR", ex is InvalidOperationException ? ex.Message : "Offline voice could not start. Check your microphone and Windows desktop microphone permission.");
                return 2;
            }
            finally
            {
                Interlocked.Exchange(ref shuttingDown, 1);
                if (parentWatch != null) parentWatch.Dispose();
                if (parent != null) parent.Dispose();
                if (recognition != IntPtr.Zero) Native.vosk_recognizer_free(recognition);
                if (model != IntPtr.Zero) Native.vosk_model_free(model);
            }
        }

        private static CaptureResult Capture(IntPtr recognition, MicrophoneCapture microphone)
        {
            Stopwatch clock = Stopwatch.StartNew();
            var state = new CaptureState();
            bool timedOut = false;
            while (clock.ElapsedMilliseconds < 40000 && state.AudioMilliseconds < 35000)
            {
                foreach (byte[] buffer in microphone.ReadAvailable())
                {
                    ProcessAudio(recognition, buffer, state, true);
                }
                if (!state.HasText && clock.ElapsedMilliseconds >= 12000) break;
                if (state.ShouldFinish) break;
                timedOut = clock.ElapsedMilliseconds >= 40000 || state.AudioMilliseconds >= 35000;
            }
            timedOut = timedOut || clock.ElapsedMilliseconds >= 40000 || state.AudioMilliseconds >= 35000;
            return state.Finish(ParseTranscript(ReadUtf8(Native.vosk_recognizer_final_result(recognition))), timedOut);
        }

        private static void ProcessAudio(IntPtr recognition, byte[] buffer, CaptureState state, bool emit)
        {
            double energy = RootMeanSquare(buffer);
            state.ObserveAudio(buffer.Length, energy);
            if (emit) Write("LEVEL", LevelPercent(energy).ToString(CultureInfo.InvariantCulture));
            int accepted = Native.vosk_recognizer_accept_waveform(recognition, buffer, buffer.Length);
            if (accepted < 0) throw new InvalidOperationException("Offline recognition stopped. Please try again.");
            bool changed;
            if (accepted == 1) changed = state.AddFinal(ParseTranscript(ReadUtf8(Native.vosk_recognizer_result(recognition))));
            else changed = state.UpdatePartial(ParsePartial(ReadUtf8(Native.vosk_recognizer_partial_result(recognition))));
            if (changed && emit) WriteText("PARTIAL", state.DisplayText);
        }

        private static CaptureResult RecognizeWaveFile(IntPtr recognition, string path, bool useLiveEndpoint)
        {
            // Only uncompressed mono 16-bit 16 kHz WAV is accepted; no media decoder is bundled.
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidOperationException("Test audio must be a PCM WAV file.");
                reader.ReadUInt32();
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidOperationException("Test audio must be a PCM WAV file.");
                bool formatValid = false;
                var state = new CaptureState();
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    uint size = reader.ReadUInt32();
                    long end = reader.BaseStream.Position + size;
                    if (end > reader.BaseStream.Length) throw new InvalidOperationException("The WAV file is truncated.");
                    if (chunk == "fmt ")
                    {
                        if (size < 16) throw new InvalidOperationException("The WAV format is invalid.");
                        ushort format = reader.ReadUInt16();
                        ushort channels = reader.ReadUInt16();
                        uint samples = reader.ReadUInt32();
                        reader.ReadUInt32(); reader.ReadUInt16();
                        ushort bits = reader.ReadUInt16();
                        formatValid = format == 1 && channels == 1 && samples == 16000 && bits == 16;
                    }
                    else if (chunk == "data")
                    {
                        if (!formatValid) throw new InvalidOperationException("Test audio must be mono, 16 kHz, 16-bit PCM WAV.");
                        while (reader.BaseStream.Position < end)
                        {
                            byte[] bytes = reader.ReadBytes((int)Math.Min(3200, end - reader.BaseStream.Position));
                            ProcessAudio(recognition, bytes, state, false);
                            if (useLiveEndpoint && (state.ShouldFinish || state.AudioMilliseconds >= 35000))
                                return state.Finish(ParseTranscript(ReadUtf8(Native.vosk_recognizer_final_result(recognition))), state.AudioMilliseconds >= 35000);
                        }
                    }
                    reader.BaseStream.Position = end + (size % 2);
                }
                if (!formatValid) throw new InvalidOperationException("Test audio must contain a supported WAV format.");
                return state.Finish(ParseTranscript(ReadUtf8(Native.vosk_recognizer_final_result(recognition))), false);
            }
        }

        private static double RootMeanSquare(byte[] bytes)
        {
            if (bytes.Length < 2) return 0;
            double sum = 0;
            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                short sample = (short)(bytes[i] | (bytes[i + 1] << 8));
                sum += (double)sample * sample;
            }
            return Math.Sqrt(sum / (bytes.Length / 2));
        }

        private static TranscriptPart ParseTranscript(string json)
        {
            var result = new TranscriptPart();
            Match textMatch = TextPattern.Match(json ?? String.Empty);
            if (!textMatch.Success) return result;
            result.Text = DecodeText(textMatch.Groups[1].Value);
            MatchCollection scores = ConfidencePattern.Matches(json);
            foreach (Match score in scores)
            {
                double value;
                if (!Double.TryParse(score.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) || value < 0 || value > 1) continue;
                result.ScoreSum += value; result.ScoreCount++; result.MinimumScore = Math.Min(result.MinimumScore, value);
            }
            return result;
        }

        private static string ParsePartial(string json)
        {
            Match match = PartialPattern.Match(json ?? String.Empty);
            return match.Success ? DecodeText(match.Groups[1].Value) : "";
        }

        private static string DecodeText(string text)
        {
            try
            {
                string result = Regex.Unescape(text).Trim();
                result = Regex.Replace(result, @"[\x00-\x1F]", " ");
                return result.Length > 4000 ? result.Substring(0, 4000) : result;
            }
            catch (ArgumentException) { return ""; }
        }

        private static int LevelPercent(double rms)
        {
            if (rms <= 0) return 0;
            // A logarithmic meter shows normal/quiet speech instead of rounding
            // nearly all microphone signals to zero on a linear full-scale meter.
            double db = 20.0 * Math.Log10(rms / 32768.0);
            return Math.Max(0, Math.Min(100, (int)Math.Round((db + 65.0) * 100.0 / 65.0)));
        }

        private static void EmitResult(CaptureResult result)
        {
            Write("METRICS", "peak=" + result.Peak.ToString(CultureInfo.InvariantCulture) + ";maxRms=" + result.MaxRms.ToString("F1", CultureInfo.InvariantCulture));
            if (String.IsNullOrWhiteSpace(result.Text)) WriteText("EMPTY", result.Message);
            else WriteText(result.Review ? "REVIEW" : "RESULT", result.Text);
        }

        private sealed class TranscriptPart
        {
            public string Text = "";
            public double ScoreSum;
            public int ScoreCount;
            public double MinimumScore = 1;
        }

        private sealed class CaptureResult
        {
            public string Text = "";
            public string Message = "";
            public bool Review;
            public int Peak;
            public double MaxRms;
        }

        private sealed class CaptureState
        {
            private readonly List<string> segments = new List<string>();
            private string partial = "", unfinishedPartial = "";
            private double lastSignal, lastText, noiseFloor = Double.MaxValue;
            private double scoreSum, minimumScore = 1;
            private int scoreCount, buffers;
            private bool missingScores;
            public double AudioMilliseconds;
            public double MaxRms;
            public bool HasText { get { return segments.Count > 0 || partial.Length > 0 || unfinishedPartial.Length > 0; } }
            public bool ShouldFinish { get { return HasText && AudioMilliseconds - Math.Max(lastSignal, lastText) >= 2400; } }
            public string DisplayText { get { return Join(String.Join(" ", segments), partial); } }

            public void ObserveAudio(int bytes, double rms)
            {
                if (bytes <= 0) return;
                buffers++; AudioMilliseconds += bytes / 32.0; MaxRms = Math.Max(MaxRms, rms);
                if (AudioMilliseconds <= 600) noiseFloor = Math.Min(noiseFloor, rms);
                double baseline = noiseFloor == Double.MaxValue ? 40 : noiseFloor;
                double threshold = Math.Max(60, Math.Min(2500, baseline * 2.8 + 20));
                if (rms > threshold) lastSignal = AudioMilliseconds;
            }

            public bool UpdatePartial(string value)
            {
                value = value ?? "";
                if (value == partial) return false;
                partial = value;
                if (value.Length > 0) { unfinishedPartial = value; lastText = AudioMilliseconds; }
                return true;
            }

            public bool AddFinal(TranscriptPart part)
            {
                if (part == null || String.IsNullOrWhiteSpace(part.Text)) return false;
                segments.Add(part.Text);
                scoreSum += part.ScoreSum; scoreCount += part.ScoreCount;
                minimumScore = Math.Min(minimumScore, part.MinimumScore);
                if (part.ScoreCount == 0) missingScores = true;
                partial = ""; unfinishedPartial = "";
                if (lastText == 0) lastText = AudioMilliseconds;
                return true;
            }

            public CaptureResult Finish(TranscriptPart final, bool timedOut)
            {
                AddFinal(final);
                string text = Join(String.Join(" ", segments), unfinishedPartial);
                var result = new CaptureResult { Text = text, Peak = LevelPercent(MaxRms), MaxRms = MaxRms };
                result.Review = timedOut || unfinishedPartial.Length > 0 || missingScores || scoreCount == 0 || scoreSum / Math.Max(1, scoreCount) < 0.68 || minimumScore < 0.30;
                if (text.Length == 0)
                    result.Message = buffers == 0 ? "Windows opened the microphone but returned no audio. Check your input device and desktop microphone permission."
                        : MaxRms < 18 ? "The microphone signal is silent. Check its mute switch and choose the correct Windows input device."
                        : MaxRms < 80 ? "Your microphone is very quiet. Move closer or raise its input volume, then try again."
                        : "Audio reached Miu, but I could not make out words. Speak clearly after Listening appears, then pause.";
                return result;
            }

            private static string Join(string left, string right)
            {
                string text = (left + " " + right).Trim();
                return text.Length > 4000 ? text.Substring(0, 4000) : text;
            }
        }

        private static void ListDevices()
        {
            uint count = Native.waveInGetNumDevs();
            Write("DEVICES", count.ToString(CultureInfo.InvariantCulture));
            for (uint i = 0; i < count; i++) Write("DEVICE", i.ToString(CultureInfo.InvariantCulture) + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(DeviceName(i))));
        }

        private static string DeviceName(uint device)
        {
            WaveInCaps info;
            return Native.waveInGetDevCaps(new UIntPtr(device), out info, (uint)Marshal.SizeOf(typeof(WaveInCaps))) == 0
                ? info.Name + " (input " + device.ToString(CultureInfo.InvariantCulture) + ")" : "Default Windows microphone";
        }

        private static void Probe(int seconds)
        {
            int buffers = 0; long bytes = 0; double maximum = 0;
            var elapsed = Stopwatch.StartNew();
            using (var microphone = new MicrophoneCapture())
            {
                WriteText("DEVICE", microphone.DeviceName);
                microphone.Start(); Write("LISTENING", "1");
                elapsed.Restart();
                while (elapsed.ElapsedMilliseconds < seconds * 1000)
                    foreach (byte[] buffer in microphone.ReadAvailable())
                    {
                        buffers++; bytes += buffer.Length;
                        double rms = RootMeanSquare(buffer); maximum = Math.Max(maximum, rms);
                        Write("LEVEL", LevelPercent(rms).ToString(CultureInfo.InvariantCulture));
                    }
            }
            Write("LISTENING", "0");
            Write("PROBE", "buffers=" + buffers.ToString(CultureInfo.InvariantCulture) + ";bytes=" + bytes.ToString(CultureInfo.InvariantCulture) + ";peak=" + LevelPercent(maximum).ToString(CultureInfo.InvariantCulture) + ";maxRms=" + maximum.ToString("F1", CultureInfo.InvariantCulture) + ";milliseconds=" + elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
        }

        private static int[] ReadyIndices(int next, bool[] done)
        {
            var result = new List<int>();
            for (int i = 0; i < done.Length; i++)
            {
                int index = (next + i) % done.Length;
                if (!done[index]) break;
                result.Add(index);
            }
            return result.ToArray();
        }

        private static int RunOfflineTests()
        {
            int checks = 0, failures = 0;
            Action<bool, string> check = delegate(bool condition, string name) { checks++; if (!condition) { failures++; Write("FAIL", name); } };
            check(String.Join(",", ReadyIndices(3, new[] { true, false, false, true })) == "3,0", "Wrapped completed buffers retain FIFO order");
            check(String.Join(",", ReadyIndices(2, new[] { true, true, true, true })) == "2,3,0,1", "A full queue starts at the oldest buffer");
            check(ReadyIndices(2, new[] { false, false, false, true }).Length == 0, "Later buffers wait for the oldest buffer");
            check(LevelPercent(0) == 0 && LevelPercent(80) > 0 && LevelPercent(2000) > LevelPercent(80), "Meter distinguishes silence, quiet speech and loud speech");
            check(ParsePartial("{\"partial\":\"open calculator\"}") == "open calculator", "Partial transcripts are decoded");
            var quiet = new CaptureState();
            quiet.ObserveAudio(3200, 35); quiet.UpdatePartial("open");
            for (int i = 0; i < 20; i++) quiet.ObserveAudio(3200, 35);
            check(!quiet.ShouldFinish, "Quiet speech gets a full pause allowance");
            quiet.UpdatePartial("open calculator"); quiet.ObserveAudio(3200, 35);
            check(!quiet.ShouldFinish, "New decoded words keep quiet speech alive");
            for (int i = 0; i < 25; i++) quiet.ObserveAudio(3200, 5);
            check(quiet.ShouldFinish, "Silence after decoded speech ends the turn");
            CaptureResult partial = quiet.Finish(new TranscriptPart(), false);
            check(partial.Text == "open calculator" && partial.Review, "Partial-only output requires review and is never executable");
            var low = new CaptureState(); low.ObserveAudio(3200, 800);
            CaptureResult uncertain = low.Finish(ParseTranscript("{\"result\":[{\"conf\":0.42}],\"text\":\"open calculator\"}"), false);
            check(uncertain.Text == "open calculator" && uncertain.Review, "Low-confidence words are preserved for manual review");
            var certain = new CaptureState(); certain.ObserveAudio(3200, 800);
            certain.AddFinal(ParseTranscript("{\"result\":[{\"conf\":0.98}],\"text\":\"open\"}"));
            CaptureResult combined = certain.Finish(ParseTranscript("{\"result\":[{\"conf\":0.95}],\"text\":\"calculator\"}"), false);
            check(combined.Text == "open calculator" && !combined.Review, "Final segments combine in order with good confidence");
            var truncated = new CaptureState(); truncated.ObserveAudio(3200, 800);
            check(truncated.Finish(ParseTranscript("{\"result\":[{\"conf\":1.0}],\"text\":\"open calculator\"}"), true).Review, "A time-limited transcript requires review even at high confidence");
            var unknown = new CaptureState(); unknown.ObserveAudio(3200, 800);
            check(unknown.Finish(ParseTranscript("{\"text\":\"open calculator\"}"), false).Review, "Unknown confidence cannot auto-submit");
            check(new CaptureState().Finish(new TranscriptPart(), false).Message.Contains("returned no audio"), "Missing capture buffers are distinguished");
            var silent = new CaptureState(); silent.ObserveAudio(3200, 0);
            check(silent.Finish(new TranscriptPart(), false).Message.Contains("silent"), "Silent microphone is identified");
            var weak = new CaptureState(); weak.ObserveAudio(3200, 40);
            check(weak.Finish(new TranscriptPart(), false).Message.Contains("very quiet"), "Weak signal is distinguished from missing words");
            var noise = new CaptureState(); noise.ObserveAudio(3200, 600);
            check(noise.Finish(new TranscriptPart(), false).Message.Contains("Audio reached Miu"), "Audible unrecognized input receives useful guidance");
            Write("CHECK", (failures == 0 ? "PASS: " : "FAIL: ") + checks + " offline speech checks; " + failures + " failures.");
            return failures == 0 ? 0 : 1;
        }

        private static string ReadUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return String.Empty;
            int length = 0;
            while (length < 1024 * 1024 && Marshal.ReadByte(pointer, length) != 0) ++length;
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void Write(string kind, string value)
        {
            Console.WriteLine(kind + "|" + value);
            Console.Out.Flush();
        }

        private static void WriteText(string kind, string value)
        {
            Write(kind, Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? String.Empty)));
        }

        private sealed class MicrophoneCapture : IDisposable
        {
            private const uint Done = 1;
            private readonly AutoResetEvent ready = new AutoResetEvent(false);
            private readonly List<Buffer> buffers = new List<Buffer>();
            private IntPtr handle;
            private bool disposed;
            private int nextRead;
            public string DeviceName { get; private set; }
            private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(WaveHeader));

            public MicrophoneCapture()
            {
                try
                {
                    if (Native.waveInGetNumDevs() == 0) throw new InvalidOperationException("No microphone was found. Connect a microphone and select it as the default Windows input device.");
                    WaveFormat format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSecond = 16000, AverageBytesPerSecond = 32000, BlockAlign = 2, BitsPerSample = 16, ExtraSize = 0 };
                    Check(Native.waveInOpen(out handle, UInt32.MaxValue, ref format, ready.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x00050000), "open the microphone");
                    uint device;
                    DeviceName = Native.waveInGetID(handle, out device) == 0 && device != UInt32.MaxValue ? VoskWorker.DeviceName(device) : "Default Windows microphone";
                    for (int i = 0; i < 10; ++i)
                    {
                        Buffer buffer = new Buffer();
                        buffers.Add(buffer);
                        buffer.Data = Marshal.AllocHGlobal(3200);
                        buffer.Header = Marshal.AllocHGlobal((int)HeaderSize);
                        WaveHeader header = new WaveHeader { Data = buffer.Data, BufferLength = 3200 };
                        Marshal.StructureToPtr(header, buffer.Header, false);
                        Check(Native.waveInPrepareHeader(handle, buffer.Header, HeaderSize), "prepare microphone input");
                        buffer.Prepared = true;
                        Check(Native.waveInAddBuffer(handle, buffer.Header, HeaderSize), "queue microphone input");
                    }
                }
                catch { Dispose(); throw; }
            }

            public void Start() { Check(Native.waveInStart(handle), "start recording"); }

            public IList<byte[]> ReadAvailable()
            {
                ready.WaitOne(150);
                List<byte[]> data = new List<byte[]>();
                bool[] done = new bool[buffers.Count];
                for (int i = 0; i < buffers.Count; i++)
                    done[i] = ((((WaveHeader)Marshal.PtrToStructure(buffers[i].Header, typeof(WaveHeader))).Flags & Done) != 0);
                foreach (int index in ReadyIndices(nextRead, done))
                {
                    Buffer buffer = buffers[index];
                    WaveHeader header = (WaveHeader)Marshal.PtrToStructure(buffer.Header, typeof(WaveHeader));
                    if ((header.Flags & Done) == 0) continue;
                    int count = (int)Math.Min(header.BytesRecorded, header.BufferLength);
                    if (count > 0)
                    {
                        byte[] bytes = new byte[count];
                        Marshal.Copy(header.Data, bytes, 0, count);
                        data.Add(bytes);
                    }
                    header.BytesRecorded = 0;
                    Marshal.StructureToPtr(header, buffer.Header, false);
                    Check(Native.waveInAddBuffer(handle, buffer.Header, HeaderSize), "continue recording");
                    nextRead = (index + 1) % buffers.Count;
                }
                return data;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (handle != IntPtr.Zero)
                {
                    Native.waveInStop(handle);
                    Native.waveInReset(handle);
                    foreach (Buffer buffer in buffers)
                        if (buffer.Prepared) Native.waveInUnprepareHeader(handle, buffer.Header, HeaderSize);
                    Native.waveInClose(handle);
                    handle = IntPtr.Zero;
                }
                foreach (Buffer buffer in buffers)
                {
                    if (buffer.Header != IntPtr.Zero) Marshal.FreeHGlobal(buffer.Header);
                    if (buffer.Data != IntPtr.Zero) Marshal.FreeHGlobal(buffer.Data);
                }
                buffers.Clear();
                ready.Dispose();
            }

            private static void Check(uint result, string action)
            {
                if (result != 0) throw new InvalidOperationException("Could not " + action + ". Check the default input device and allow desktop microphone access in Windows Settings (audio error " + result + ").");
            }

            private sealed class Buffer { public IntPtr Header; public IntPtr Data; public bool Prepared; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormat
        {
            public ushort FormatTag; public ushort Channels; public uint SamplesPerSecond;
            public uint AverageBytesPerSecond; public ushort BlockAlign; public ushort BitsPerSample; public ushort ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WaveInCaps
        {
            public ushort Manufacturer, Product;
            public uint DriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Formats;
            public ushort Channels, Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data; public uint BufferLength; public uint BytesRecorded; public IntPtr User;
            public uint Flags; public uint Loops; public IntPtr Next; public IntPtr Reserved;
        }

        private static class Native
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetDllDirectory(string path);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_set_log_level(int level);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_model_new(byte[] path);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_model_free(IntPtr model);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_new(IntPtr model, float sampleRate);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_recognizer_free(IntPtr recognizer);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_recognizer_set_words(IntPtr recognizer, int enabled);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int vosk_recognizer_accept_waveform(IntPtr recognizer, byte[] data, int length);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);
            [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_final_result(IntPtr recognizer);
            [DllImport("winmm.dll")] public static extern uint waveInGetNumDevs();
            [DllImport("winmm.dll", EntryPoint = "waveInGetDevCapsW", CharSet = CharSet.Unicode)] public static extern uint waveInGetDevCaps(UIntPtr device, out WaveInCaps info, uint size);
            [DllImport("winmm.dll")] public static extern uint waveInGetID(IntPtr handle, out uint device);
            [DllImport("winmm.dll")] public static extern uint waveInOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
            [DllImport("winmm.dll")] public static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
            [DllImport("winmm.dll")] public static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
            [DllImport("winmm.dll")] public static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
            [DllImport("winmm.dll")] public static extern uint waveInStart(IntPtr handle);
            [DllImport("winmm.dll")] public static extern uint waveInStop(IntPtr handle);
            [DllImport("winmm.dll")] public static extern uint waveInReset(IntPtr handle);
            [DllImport("winmm.dll")] public static extern uint waveInClose(IntPtr handle);
        }
    }
}
