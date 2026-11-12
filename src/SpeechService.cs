using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Miu
{
    /// <summary>
    /// On-demand local speech. No audio device or speech model is held while idle.
    /// Events can arrive on a background thread; UI subscribers must dispatch them.
    /// StartListeningAsync completes when initialization finishes, not when speech ends.
    /// </summary>
    public sealed class SpeechService : IDisposable
    {
        private readonly object stateGate = new object();
        private readonly object queueGate = new object();
        private Task work = Task.FromResult(0);
        private Task startingTask = Task.FromResult(0);
        private SpeechRecognitionEngine recognizer;
        private SpeechSynthesizer synthesizer;
        private volatile NaturalSpeechSession naturalSpeech;
        private OfflineSession offlineSession;
        private Timer listeningLimit;
        private int listenVersion;
        private int speakVersion;
        private bool listening;
        private bool starting;
        private bool speaking;
        private bool disposed;
        private int rate;
        private bool useNaturalVoice = true;
        private int lastInputPeak;
        private string lastInputDevice = "";
        private string sapiDraft = "";

        public event Action<string> Recognized;
        // Drafts and uncertain results are display-only; subscribers must never auto-submit them.
        public event Action<string> PartialRecognized;
        public event Action<string> ReviewRequested;
        public event Action<string> StatusChanged;
        public event Action<string> Error;
        public event Action<int> AudioLevelChanged;
        public event Action<bool> ListeningChanged;
        public event Action<bool> SpeakingChanged;

        public bool IsListening { get { lock (stateGate) { return listening || starting; } } }
        public bool IsSpeaking { get { lock (stateGate) { return speaking; } } }
        public int LastInputPeak { get { lock (stateGate) { return lastInputPeak; } } }
        public string LastInputDevice { get { lock (stateGate) { return lastInputDevice; } } }
        public bool UseNaturalVoice
        {
            get { lock (stateGate) { return useNaturalVoice; } }
            set { lock (stateGate) { useNaturalVoice = value; } }
        }
        public bool HasNaturalVoice { get { return PiperSpeech.IsAvailable; } }
        public bool HasOfflineRecognizer
        {
            get
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                return File.Exists(Path.Combine(directory, "Miu.Voice.exe"))
                    && File.Exists(Path.Combine(directory, "voice", "libvosk.dll"))
                    && File.Exists(Path.Combine(directory, "voice", "model", "am", "final.mdl"));
            }
        }

        public int Rate
        {
            get { lock (stateGate) { return rate; } }
            set { lock (stateGate) { rate = Math.Max(-10, Math.Min(10, value)); } }
        }

        public Task StartListeningAsync()
        {
            int version;
            lock (stateGate)
            {
                if (disposed) return Task.FromResult(0);
                if (starting || listening) return startingTask;
                starting = true;
                lastInputPeak = 0;
                lastInputDevice = "";
                sapiDraft = "";
                version = ++listenVersion;
                ++speakVersion;
            }
            Notify(StatusChanged, "Starting microphone...");
            Task task = Enqueue(delegate { StartRecognitionCore(version); });
            lock (stateGate) { startingTask = task; }
            return task;
        }

        public void StopListening()
        {
            lock (stateGate)
            {
                ++listenVersion;
                starting = false;
            }
            Enqueue(delegate
            {
                StopRecognitionCore();
                Notify(StatusChanged, "Microphone off");
            });
        }

        public void Speak(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            // Bound voice output; the full answer remains available in the chat panel.
            if (text.Length > 6000) text = text.Substring(0, 6000) + ". The rest is in the chat.";
            int version;
            lock (stateGate)
            {
                if (disposed) return;
                version = ++speakVersion;
                ++listenVersion;
                starting = false;
            }
            string spokenText = text;
            Enqueue(delegate { StartSynthesisCore(spokenText, version); });
        }

        public void StopSpeaking()
        {
            lock (stateGate) { ++speakVersion; }
            NaturalSpeechSession natural = naturalSpeech;
            if (natural != null) natural.Cancel();
            Enqueue(delegate { StopSynthesisCore(); });
        }

        public string DescribeVoices()
        {
            string natural = HasNaturalVoice
                ? "Natural English voice (Piper) is installed; loads only for each reply. Windows fallback: "
                : "Natural voice needs the free setup-natural-voice.ps1 download. Windows voices: ";
            try
            {
                using (SpeechSynthesizer probe = new SpeechSynthesizer())
                {
                    string[] voices = probe.GetInstalledVoices()
                        .Where(delegate(InstalledVoice voice) { return voice.Enabled; })
                        .Select(delegate(InstalledVoice voice)
                        {
                            return voice.VoiceInfo.Name + " (" + voice.VoiceInfo.Culture.Name + ")";
                        }).ToArray();
                    return natural + (voices.Length == 0 ? "No Windows desktop voices installed." : String.Join(", ", voices));
                }
            }
            catch { return natural + "Windows speech output is unavailable. Replies still appear in chat."; }
        }

        public string DescribeRecognizers()
        {
            try
            {
                string[] engines = SpeechRecognitionEngine.InstalledRecognizers()
                    .Select(delegate(RecognizerInfo info) { return info.Name + " (" + info.Culture.Name + ")"; })
                    .ToArray();
                if (HasOfflineRecognizer)
                    return "Offline English voice (Vosk) is preferred; loads only while listening."
                        + (engines.Length > 0 ? " Windows fallback: " + String.Join(", ", engines) : String.Empty);
                return engines.Length == 0
                    ? "Offline voice needs setup. Run scripts\\setup-voice.ps1; or use Win+H in the message box."
                    : String.Join(", ", engines);
            }
            catch
            {
                return HasOfflineRecognizer ? "Offline English voice (Vosk) is preferred; loads only while listening."
                    : "Windows desktop recognition is unavailable. Use Win+H in the message box.";
            }
        }

        // A continuation queue serializes all native speech calls without an idle worker thread.
        // In particular, microphone disposal completes before any response audio begins.
        private Task Enqueue(Action action)
        {
            lock (queueGate)
            {
                work = work.ContinueWith(delegate(Task previous)
                {
                    try { action(); }
                    catch { Notify(Error, "Windows speech could not complete that request. You can still use the message box."); }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return work;
            }
        }

        private bool WantsListening(int version)
        {
            lock (stateGate) { return !disposed && listenVersion == version; }
        }

        private bool WantsSpeaking(int version)
        {
            lock (stateGate) { return !disposed && speakVersion == version; }
        }

        private void StartRecognitionCore(int version)
        {
            StopRecognitionCore();
            StopSynthesisCore();
            if (!WantsListening(version)) return;
            if (HasOfflineRecognizer) StartOfflineRecognitionCore(version);
            else StartWindowsRecognitionCore(version);
        }

        private void StartWindowsRecognitionCore(int version)
        {
            StopRecognitionCore();
            StopSynthesisCore();
            if (!WantsListening(version)) return;

            SpeechRecognitionEngine engine = null;
            try
            {
                RecognizerInfo chosen = SpeechRecognitionEngine.InstalledRecognizers()
                    .Where(delegate(RecognizerInfo info) { return info.Culture.TwoLetterISOLanguageName == "en"; })
                    .OrderByDescending(delegate(RecognizerInfo info) { return info.Culture.Name == CultureInfo.CurrentUICulture.Name; })
                    .FirstOrDefault();
                if (chosen == null)
                {
                    FinishStarting(version);
                    Notify(Error, "Offline voice needs a one-time free download. Run scripts\\setup-voice.ps1 in the Miu folder. Until then, click the message box, press Win+H, speak, then press Enter.");
                    return;
                }

                engine = new SpeechRecognitionEngine(chosen);
                engine.InitialSilenceTimeout = TimeSpan.FromSeconds(12);
                engine.EndSilenceTimeout = TimeSpan.FromSeconds(2.4);
                engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(2.4);
                engine.BabbleTimeout = TimeSpan.FromSeconds(15);
                engine.MaxAlternates = 1;

                bool dictationAvailable = true;
                try { engine.LoadGrammar(new DictationGrammar()); }
                catch (InvalidOperationException) { dictationAvailable = false; }
                catch (System.Runtime.InteropServices.COMException) { dictationAvailable = false; }

                Choices commands = new Choices(new string[] {
                    "hello", "hello Miu", "hi Miu", "how are you", "help", "what can you do",
                    "check my email", "check email", "check Gmail", "latest email", "read my latest email",
                    "read my email", "read the first email", "unread emails", "show unread emails",
                    "open Gmail", "open browser", "open calculator", "open notepad", "open settings",
                    "open downloads", "open documents", "open file explorer", "what time is it",
                    "what is the date", "what day is it", "volume up", "volume down", "mute", "unmute",
                    "stop", "stop talking", "thank you", "thanks", "good morning", "good night"
                });
                GrammarBuilder commandBuilder = new GrammarBuilder();
                commandBuilder.Culture = chosen.Culture;
                commandBuilder.Append(commands);
                Grammar commandGrammar = new Grammar(commandBuilder);
                commandGrammar.Name = "Miu commands";
                commandGrammar.Priority = 1;
                engine.LoadGrammar(commandGrammar);

                SpeechRecognitionEngine captured = engine;
                engine.AudioLevelUpdated += delegate(object sender, AudioLevelUpdatedEventArgs args)
                {
                    if (!WantsListening(version)) return;
                    lock (stateGate) { lastInputPeak = Math.Max(lastInputPeak, args.AudioLevel); }
                    Notify(AudioLevelChanged, args.AudioLevel);
                };
                engine.SpeechHypothesized += delegate(object sender, SpeechHypothesizedEventArgs args)
                {
                    if (!WantsListening(version) || args.Result == null) return;
                    string draft = LimitTranscript(args.Result.Text);
                    lock (stateGate) { sapiDraft = draft; }
                    Notify(PartialRecognized, draft);
                };
                engine.SpeechRecognitionRejected += delegate(object sender, SpeechRecognitionRejectedEventArgs args)
                {
                    if (!WantsListening(version) || args.Result == null || String.IsNullOrWhiteSpace(args.Result.Text)) return;
                    lock (stateGate) { sapiDraft = LimitTranscript(args.Result.Text); }
                };
                engine.RecognizeCompleted += delegate(object sender, RecognizeCompletedEventArgs args)
                {
                    Enqueue(delegate { CompleteRecognitionCore(captured, version, args); });
                };

                if (!WantsListening(version)) { engine.Dispose(); return; }
                recognizer = engine;
                engine.SetInputToDefaultAudioDevice();
                if (!WantsListening(version)) { StopRecognitionCore(); return; }
                engine.RecognizeAsync(RecognizeMode.Single);
                lock (stateGate) { starting = false; listening = true; lastInputDevice = "Windows default microphone (SAPI fallback)"; }
                Notify(ListeningChanged, true);
                Notify(StatusChanged, dictationAvailable ? "Listening with Windows fallback... speak, then pause" : "Listening for a basic command...");
                listeningLimit = new Timer(delegate
                {
                    Enqueue(delegate
                    {
                        if (recognizer != captured || !WantsListening(version)) return;
                        StopRecognitionCore();
                        Notify(StatusChanged, "Listening timed out. Tap the microphone to try again.");
                    });
                }, null, 35000, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                if (recognizer == engine) StopRecognitionCore();
                else if (engine != null) { try { engine.Dispose(); } catch { } }
                FinishStarting(version);
                if (!WantsListening(version)) return;
                string hint = ex is UnauthorizedAccessException
                    ? "Allow microphone access for desktop apps in Windows Settings > Privacy & security > Microphone."
                    : "Check your default microphone and desktop microphone permission in Windows Settings. You can also use Win+H in the message box.";
                Notify(Error, "I could not start the microphone. " + hint);
            }
        }

        private void FinishStarting(int version)
        {
            bool changed = false;
            lock (stateGate)
            {
                if (listenVersion == version)
                {
                    changed = starting || listening;
                    starting = false;
                    listening = false;
                }
            }
            if (changed) Notify(ListeningChanged, false);
        }

        private void CompleteRecognitionCore(SpeechRecognitionEngine engine, int version, RecognizeCompletedEventArgs args)
        {
            if (recognizer != engine) return;
            bool wanted = WantsListening(version);
            StopRecognitionCore();
            if (!wanted || args.Cancelled) return;
            if (args.Error != null)
            {
                Notify(Error, "The microphone stopped unexpectedly. Check your input device, or use Win+H in the message box.");
                return;
            }
            if (args.InitialSilenceTimeout)
            {
                Notify(StatusChanged, LastInputPeak == 0 ? "No microphone signal reached Miu. Check the input device, mute switch, and microphone permission." : "I heard audio but no clear words. Move closer to the microphone and try again.");
                return;
            }
            if (args.Result == null || String.IsNullOrWhiteSpace(args.Result.Text) || args.Result.Confidence < 0.55f)
            {
                string draft;
                lock (stateGate) { draft = args.Result == null || String.IsNullOrWhiteSpace(args.Result.Text) ? sapiDraft : args.Result.Text; }
                if (!String.IsNullOrWhiteSpace(draft))
                {
                    Notify(StatusChanged, "Please check what I heard, then press Send. Nothing has run yet.");
                    Notify(ReviewRequested, LimitTranscript(draft));
                }
                else Notify(StatusChanged, LastInputPeak == 0 ? "No microphone signal reached Miu. Check the input device and mute switch." : "Audio reached Miu, but I could not make out words. Try speaking closer or type your message.");
                return;
            }
            Notify(StatusChanged, "Microphone off");
            Notify(Recognized, args.Result.Text.Trim());
        }

        private void StopRecognitionCore()
        {
            if (listeningLimit != null) { listeningLimit.Dispose(); listeningLimit = null; }
            OfflineSession offline = offlineSession;
            offlineSession = null;
            if (offline != null)
            {
                try
                {
                    if (!offline.Process.HasExited)
                    {
                        offline.Process.Kill();
                        offline.Process.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                // The reader task disposes the process after stdout is drained.
            }
            SpeechRecognitionEngine engine = recognizer;
            recognizer = null;
            if (engine != null)
            {
                try { engine.RecognizeAsyncCancel(); } catch { }
                try { engine.SetInputToNull(); } catch { }
                try { engine.Dispose(); } catch { }
            }
            bool changed;
            lock (stateGate) { changed = listening; listening = false; }
            if (changed) Notify(ListeningChanged, false);
            Notify(AudioLevelChanged, 0);
        }

        private void StartOfflineRecognitionCore(int version)
        {
            if (!WantsListening(version)) return;
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Miu.Voice.exe"),
                Arguments = "--parent " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture),
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            OfflineSession session = new OfflineSession { Process = process, Version = version };
            try
            {
                process.ErrorDataReceived += delegate { /* Native diagnostics never contain user-facing replies. */ };
                if (!process.Start()) throw new InvalidOperationException("Offline voice could not start.");
                process.BeginErrorReadLine();
                offlineSession = session;
                Notify(StatusChanged, "Loading offline voice... microphone off");
                listeningLimit = new Timer(delegate
                {
                    Enqueue(delegate
                    {
                        if (offlineSession != session || !WantsListening(version)) return;
                        StopRecognitionCore();
                        FinishStarting(version);
                        if (session.EverListened) Notify(StatusChanged, "Listening timed out. Tap the microphone to try again.");
                        else if (!TryWindowsFallback(version)) Notify(Error, "Offline voice took too long to start. Tap the microphone to try again.");
                    });
                }, null, 60000, Timeout.Infinite);
                Task.Run(delegate
                {
                    try
                    {
                        string line;
                        while ((line = process.StandardOutput.ReadLine()) != null)
                        {
                            string capturedLine = line;
                            Enqueue(delegate { HandleOfflineLine(session, capturedLine); });
                        }
                        process.WaitForExit();
                        session.ExitCode = process.ExitCode;
                    }
                    catch { session.ExitCode = 2; }
                    finally
                    {
                        // Completion is queued after all stdout messages, preserving protocol order.
                        Enqueue(delegate { CompleteOfflineRecognitionCore(session); });
                    }
                });
            }
            catch
            {
                offlineSession = null;
                try { if (!process.HasExited) process.Kill(); } catch { }
                process.Dispose();
                FinishStarting(version);
                if (!TryWindowsFallback(version)) Notify(Error, "Offline voice could not start. Run scripts\\setup-voice.ps1 to check its installation.");
            }
        }

        private void HandleOfflineLine(OfflineSession session, string line)
        {
            if (offlineSession != session || !WantsListening(session.Version)) return;
            int divider = line.IndexOf('|');
            if (divider < 0) return;
            string kind = line.Substring(0, divider);
            string value = line.Substring(divider + 1);
            if (kind == "LISTENING")
            {
                bool active = value == "1";
                if (active) { session.EverListened = true; if (listeningLimit != null) listeningLimit.Change(45000, Timeout.Infinite); }
                lock (stateGate) { starting = false; listening = active; }
                Notify(ListeningChanged, active);
                Notify(StatusChanged, active ? "Listening offline... speak, then pause" : "Microphone off");
            }
            else if (kind == "LEVEL")
            {
                int level;
                if (Int32.TryParse(value, out level))
                {
                    level = Math.Max(0, Math.Min(100, level));
                    lock (stateGate) { lastInputPeak = Math.Max(lastInputPeak, level); }
                    Notify(AudioLevelChanged, level);
                }
            }
            else if (kind == "RESULT" || kind == "ERROR" || kind == "EMPTY" || kind == "REVIEW" || kind == "PARTIAL" || kind == "DEVICE")
            {
                string decoded;
                try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
                catch (FormatException) { return; }
                if (kind == "RESULT") session.Text = LimitTranscript(decoded);
                else if (kind == "ERROR") session.Error = decoded;
                else if (kind == "REVIEW") session.ReviewText = LimitTranscript(decoded);
                else if (kind == "PARTIAL") Notify(PartialRecognized, LimitTranscript(decoded));
                else if (kind == "DEVICE") { lock (stateGate) { lastInputDevice = decoded; } }
                else session.EmptyMessage = decoded;
            }
        }

        private void CompleteOfflineRecognitionCore(OfflineSession session)
        {
            bool current = offlineSession == session && WantsListening(session.Version);
            if (offlineSession == session)
            {
                offlineSession = null;
                StopRecognitionCore();
                FinishStarting(session.Version);
            }
            session.Process.Dispose();
            if (!current) return;
            if (!session.EverListened && (!String.IsNullOrWhiteSpace(session.Error) || session.ExitCode != 0) && TryWindowsFallback(session.Version)) return;
            if (!String.IsNullOrWhiteSpace(session.Error)) Notify(Error, session.Error);
            else if (session.ExitCode != 0) Notify(Error, "Offline voice stopped unexpectedly. Check microphone access in Windows Settings and try again.");
            else if (!String.IsNullOrWhiteSpace(session.ReviewText))
            {
                Notify(StatusChanged, "Please check what I heard, then press Send. Nothing has run yet.");
                Notify(ReviewRequested, session.ReviewText);
            }
            else if (!String.IsNullOrWhiteSpace(session.Text))
            {
                Notify(StatusChanged, "Microphone off");
                Notify(Recognized, session.Text);
            }
            else Notify(StatusChanged, session.EmptyMessage ?? "I did not catch that clearly. Tap the microphone to try again.");
        }

        private bool TryWindowsFallback(int version)
        {
            if (!WantsListening(version)) return false;
            try
            {
                if (!SpeechRecognitionEngine.InstalledRecognizers().Any(delegate(RecognizerInfo item) { return item.Culture.TwoLetterISOLanguageName == "en"; })) return false;
                lock (stateGate) { starting = true; sapiDraft = ""; }
                Notify(StatusChanged, "Offline recognizer could not load. Trying Windows speech...");
                StartWindowsRecognitionCore(version);
                return true;
            }
            catch { return false; }
        }

        private static string LimitTranscript(string value)
        {
            string text = (value ?? "").Trim();
            return text.Length > 4000 ? text.Substring(0, 4000) : text;
        }

        private sealed class OfflineSession
        {
            public Process Process;
            public int Version;
            public int ExitCode;
            public string Text;
            public string ReviewText;
            public string Error;
            public string EmptyMessage;
            public bool EverListened;
        }

        private void StartSynthesisCore(string text, int version)
        {
            StopRecognitionCore();
            StopSynthesisCore();
            if (!WantsSpeaking(version)) return;
            if (UseNaturalVoice && HasNaturalVoice)
            {
                var session = new NaturalSpeechSession { Version = version };
                naturalSpeech = session;
                int speechRate = Rate;
                lock (stateGate) { speaking = true; }
                Notify(SpeakingChanged, true);
                // Generation/playback never occupy the native-operation queue. Stop and
                // microphone requests can cancel this utterance without waiting for it.
                Task.Run(async delegate
                {
                    bool failed = false;
                    try
                    {
                        if (!WantsSpeaking(version)) session.Cancel();
                        session.Cancellation.Token.ThrowIfCancellationRequested();
                        await PiperSpeech.SpeakAsync(text, speechRate, session.Cancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                    catch { failed = true; }
                    await Enqueue(delegate { CompleteNaturalSynthesisCore(session, text, failed); }).ConfigureAwait(false);
                });
                return;
            }
            StartWindowsSynthesisCore(text, version);
        }

        private void CompleteNaturalSynthesisCore(NaturalSpeechSession session, string text, bool failed)
        {
            bool current = naturalSpeech == session;
            bool wanted = WantsSpeaking(session.Version);
            bool cancelled = session.Cancellation.IsCancellationRequested;
            if (current)
            {
                naturalSpeech = null;
                StopSynthesisCore();
            }
            session.Cancellation.Dispose();
            if (current && wanted && failed && !cancelled)
            {
                Notify(StatusChanged, "Natural voice is unavailable. Using the Windows voice for this reply.");
                StartWindowsSynthesisCore(text, session.Version);
            }
        }

        private sealed class NaturalSpeechSession
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public int Version;
            public void Cancel()
            {
                try { Cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
                catch (AggregateException) { }
            }
        }

        private void StartWindowsSynthesisCore(string text, int version)
        {
            if (!WantsSpeaking(version)) return;
            SpeechSynthesizer voice = null;
            try
            {
                voice = new SpeechSynthesizer();
                InstalledVoice preferred = voice.GetInstalledVoices()
                    .Where(delegate(InstalledVoice candidate) { return candidate.Enabled; })
                    .OrderByDescending(delegate(InstalledVoice candidate) { return candidate.VoiceInfo.Culture.TwoLetterISOLanguageName == "en"; })
                    .ThenByDescending(delegate(InstalledVoice candidate) { return candidate.VoiceInfo.Gender == VoiceGender.Female; })
                    .FirstOrDefault();
                if (preferred == null)
                {
                    voice.Dispose();
                    Notify(Error, "No Windows desktop voice is installed. Add a voice in Windows Settings > Time & language > Speech. Your reply is in the chat.");
                    return;
                }
                voice.SelectVoice(preferred.VoiceInfo.Name);
                voice.Rate = Rate;
                voice.SetOutputToDefaultAudioDevice();
                SpeechSynthesizer captured = voice;
                voice.SpeakCompleted += delegate(object sender, SpeakCompletedEventArgs args)
                {
                    Enqueue(delegate
                    {
                        if (synthesizer != captured) return;
                        bool wanted = WantsSpeaking(version);
                        StopSynthesisCore();
                        if (wanted && args.Error != null) Notify(Error, "Windows could not play that reply. It is still available in the chat.");
                    });
                };
                if (!WantsSpeaking(version)) { voice.Dispose(); return; }
                synthesizer = voice;
                lock (stateGate) { speaking = true; }
                Notify(SpeakingChanged, true);
                voice.SpeakAsync(text);
            }
            catch
            {
                if (synthesizer == voice) StopSynthesisCore();
                else if (voice != null) { try { voice.Dispose(); } catch { } }
                if (WantsSpeaking(version)) Notify(Error, "Windows speech output is unavailable. Check your speakers; the reply is in the chat.");
            }
        }

        private void StopSynthesisCore()
        {
            NaturalSpeechSession natural = naturalSpeech;
            naturalSpeech = null;
            if (natural != null) natural.Cancel();
            SpeechSynthesizer voice = synthesizer;
            synthesizer = null;
            if (voice != null)
            {
                try { voice.SpeakAsyncCancelAll(); } catch { }
                try { voice.Dispose(); } catch { }
            }
            bool changed;
            lock (stateGate) { changed = speaking; speaking = false; }
            if (changed) Notify(SpeakingChanged, false);
        }

        private static void Notify<T>(Action<T> handler, T value)
        {
            if (handler == null) return;
            foreach (Action<T> subscriber in handler.GetInvocationList())
            {
                // UI shutdown must not tear down a native speech callback thread.
                try { subscriber(value); } catch { }
            }
        }

        public void Dispose()
        {
            lock (stateGate)
            {
                if (disposed) return;
                disposed = true;
                ++listenVersion;
                ++speakVersion;
                starting = false;
            }
            // Request immediate playback/process cancellation even if Windows closes
            // before the final queued cleanup has a chance to run.
            NaturalSpeechSession natural = naturalSpeech;
            if (natural != null) natural.Cancel();
            Enqueue(delegate
            {
                StopRecognitionCore();
                StopSynthesisCore();
            });
        }
    }
}
