using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Miu
{
    public sealed class AppController : IDisposable
    {
        public readonly AppSettings Settings;
        public readonly SpeechService Speech;
        public readonly GmailService Gmail;
        public readonly MascotWindow Mascot;
        public readonly ChatWindow Chat;
        private readonly AssistantEngine engine;
        private readonly Forms.NotifyIcon tray;
        private readonly Hotkey hotkey;
        private SettingsWindow settingsWindow;
        private ReactionsWindow reactionsWindow;
        private CommunityWindow communityWindow;
        private CancellationTokenSource pending;
        private string responseMood = "idle";
        private bool voiceError;
        public bool Busy { get { return pending != null; } }
        public bool Sleeping { get; private set; }
        public bool IsExiting { get; private set; }
        public AppController(bool preview = false)
        {
            Settings = AppSettings.Load(); Gmail = new GmailService(); Speech = new SpeechService(); Speech.Rate = Settings.SpeechRate; Speech.UseNaturalVoice = Settings.NaturalVoiceEnabled;
            engine = new AssistantEngine(Gmail, Settings);
            Mascot = new MascotWindow(this); Chat = new ChatWindow(this);
            responseMood = Settings.PreferredMood; Mascot.SetMood(responseMood, "Click to chat");
            if (preview) return;
            Speech.Recognized += delegate(string text) { UI(delegate { Submit(text); }); };
            Speech.PartialRecognized += delegate(string text) { UI(delegate { if (Speech.IsListening) Chat.SetPartialRecognition(text); }); };
            Speech.ReviewRequested += delegate(string text) { UI(delegate { voiceError = true; ShowPanel(); Chat.ReviewRecognition(text); Mascot.SetMood("curious", "Check what I heard"); }); };
            Speech.Error += delegate(string text) { UI(delegate { voiceError = true; ShowPanel(); Chat.AddMessage(text, false); Chat.SetStatus("Voice needs attention · typing works"); Mascot.SetMood("confused", "Try typing"); }); };
            Speech.StatusChanged += delegate(string text) { UI(delegate { if (!Busy && !Sleeping) Chat.SetStatus(text); }); };
            Speech.AudioLevelChanged += delegate(int level) { UI(delegate { Chat.SetAudioLevel(level); Mascot.Face.AudioLevel = level; if (Speech.IsListening) Mascot.Face.InvalidateVisual(); }); };
            Speech.ListeningChanged += delegate(bool listening) { UI(delegate { Chat.SetListening(listening); if (listening) { Chat.SetInputDevice(Speech.LastInputDevice); Mascot.SetMood("listening", "I’m listening"); Chat.SetStatus("Listening… speak, then pause"); } else if (!Busy && !Speech.IsSpeaking && !voiceError) SetReady(); }); };
            Speech.SpeakingChanged += delegate(bool speaking) { UI(delegate { Mascot.SetTalking(speaking); if (speaking) { Mascot.SetMood(responseMood, "Talking to you"); Chat.SetStatus("Speaking… tap mic to interrupt"); } else if (!Busy && !Speech.IsListening && !voiceError) { Mascot.SetMood(Sleeping ? "sleepy" : responseMood, Sleeping ? "Resting" : "Click to chat"); Chat.SetStatus(Sleeping ? "Resting · microphone off" : "Ready when you are"); } }); };
            Mascot.Show(); hotkey = new Hotkey(Mascot, ToggleListening);
            tray = new Forms.NotifyIcon { Text = "Miu — Ctrl+Alt+Space to talk", Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application, Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Miu", null, delegate { UI(ShowPanel); });
            menu.Items.Add("Talk / stop", null, delegate { UI(ToggleListening); });
            menu.Items.Add("Sleep / wake", null, delegate { UI(delegate { SetSleeping(!Sleeping); }); });
            menu.Items.Add("Settings", null, delegate { UI(ShowSettings); });
            menu.Items.Add("Reactions", null, delegate { UI(ShowReactions); });
            menu.Items.Add("Presets", null, delegate { UI(ShowCommunity); });
            menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Quit Miu", null, delegate { UI(Exit); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { UI(ShowPanel); };
            ShowPanel(); if (!hotkey.Registered) Chat.AddMessage("Ctrl+Alt+Space is being used by another app. You can still click my microphone button to talk.", false);
        }
        private void UI(Action action)
        {
            if (Application.Current == null || IsExiting) return;
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.HasShutdownStarted) return;
            if (dispatcher.CheckAccess()) action(); else dispatcher.BeginInvoke(new Action(delegate { if (!IsExiting) action(); }));
        }
        public void ShowPanel() { if (IsExiting) return; if (Sleeping) { Sleeping = false; responseMood = "happy"; SetReady(); } if (!Chat.IsVisible) { Chat.PlaceNearMascot(); Chat.Show(); } Chat.Activate(); Chat.FocusInput(); }
        public void ShowSettings()
        {
            Speech.StopListening(); Speech.StopSpeaking();
            if (settingsWindow != null) { settingsWindow.Activate(); return; }
            settingsWindow = new SettingsWindow(this) { Owner = Chat.IsVisible ? (Window)Chat : Mascot };
            settingsWindow.Closed += delegate { settingsWindow = null; }; settingsWindow.Show();
        }
        public void ShowReactions()
        {
            if (reactionsWindow != null) { reactionsWindow.Activate(); return; }
            reactionsWindow = new ReactionsWindow(this) { Owner = Chat.IsVisible ? (Window)Chat : Mascot };
            reactionsWindow.Closed += delegate { reactionsWindow = null; }; reactionsWindow.Show();
        }
        public void ShowCommunity()
        {
            if (communityWindow != null) { communityWindow.Activate(); return; }
            communityWindow = new CommunityWindow(this) { Owner = Chat.IsVisible ? (Window)Chat : Mascot };
            communityWindow.Closed += delegate { communityWindow = null; }; communityWindow.Show();
        }
        public void ApplyPreset(CommunityPreset preset)
        {
            if (preset == null || !ExpressionCatalog.IsSupported(preset.Mood) || preset.SpeechRate < -5 || preset.SpeechRate > 5) throw new ArgumentException("Unsupported preset.");
            int previousRate = Settings.SpeechRate; bool previousMotion = Settings.ReducedMotion; string previousMood = Settings.PreferredMood;
            Settings.SpeechRate = preset.SpeechRate; Settings.ReducedMotion = preset.ReducedMotion; Settings.PreferredMood = preset.Mood;
            try { Settings.Save(); }
            catch { Settings.SpeechRate = previousRate; Settings.ReducedMotion = previousMotion; Settings.PreferredMood = previousMood; throw; }
            Speech.Rate = Settings.SpeechRate; PreviewMood(preset.Mood);
            if (settingsWindow != null) settingsWindow.RefreshAppearanceSettings();
        }
        public async void Submit(string text)
        {
            if (String.IsNullOrWhiteSpace(text) || Busy || IsExiting) return;
            Sleeping = false; voiceError = false; Speech.StopListening(); Speech.StopSpeaking(); ShowPanel();
            Chat.AddMessage(text, true); pending = new CancellationTokenSource(); var operation = pending;
            Chat.SetBusy(true); Chat.SetStatus("Thinking… Esc to cancel"); Mascot.SetMood("thinking", "One little moment");
            try
            {
                AssistantReply reply = await engine.HandleAsync(text, operation.Token);
                if (operation.IsCancellationRequested || IsExiting) return;
                responseMood = reply.Mood ?? "happy"; Chat.AddMessage(reply.Text, false, reply.IsEmail); Mascot.SetMood(responseMood, "Click to chat");
                switch (reply.Action ?? "")
                {
                    case "settings": case "connect_gmail": ShowSettings(); break;
                    case "reactions": ShowReactions(); break;
                    case "sleep": SetSleeping(true); break;
                    case "wake": SetSleeping(false); break;
                    case "mute": Settings.VoiceEnabled = false; ApplySettings(); break;
                    case "unmute": Settings.VoiceEnabled = true; ApplySettings(); break;
                    case "clear": engine.ClearConversation(); Chat.ClearMessages(); Chat.AddMessage(reply.Text, false); PreviewMood(Settings.PreferredMood); break;
                }
                if (Settings.VoiceEnabled && !Sleeping) Speech.Speak(reply.Text);
            }
            catch (OperationCanceledException) { if (!IsExiting) Chat.SetStatus("Cancelled · ready when you are"); }
            catch (Exception e) { if (!IsExiting) { Chat.AddMessage("I couldn’t finish that. " + FriendlyError(e), false); Mascot.SetMood("confused", "Try again"); } }
            finally
            {
                if (pending == operation) pending = null; operation.Dispose(); Chat.SetBusy(false);
                if (!IsExiting && !Speech.IsSpeaking && !Speech.IsListening && !voiceError) SetReady();
            }
        }
        private static string FriendlyError(Exception e)
        {
            if (e is System.Net.Http.HttpRequestException) return "Please check your internet connection and try again.";
            if (e is UnauthorizedAccessException || e is System.IO.IOException) return "I couldn’t access my local files. Check that your profile is writable.";
            if (e is InvalidOperationException || e is ArgumentException) return e.Message;
            return "Please try again, or check the connection in Settings.";
        }
        public async void ToggleListening()
        {
            if (IsExiting) return;
            ShowPanel();
            if (Busy || Speech.IsListening) { CancelCurrent(); return; }
            // A mic press during a reply interrupts speech and starts the new turn.
            // StartListeningAsync releases speech output before opening input.
            Sleeping = false; voiceError = false;
            try { await Speech.StartListeningAsync(); }
            catch (Exception e) { Chat.AddMessage("Voice is not ready. " + FriendlyError(e), false); }
        }
        public void CancelCurrent() { if (pending != null) pending.Cancel(); Speech.StopListening(); Speech.StopSpeaking(); Chat.SetListening(false); SetReady(); }
        public void SetSleeping(bool sleeping) { Sleeping = sleeping; if (sleeping) { if (pending != null) pending.Cancel(); Speech.StopListening(); Speech.StopSpeaking(); Chat.Hide(); } SetReady(); }
        private void SetReady() { Mascot.SetMood(Sleeping ? "sleepy" : responseMood, Sleeping ? "Resting" : "Click to chat"); Chat.SetStatus(Sleeping ? "Resting · microphone off" : "Ready when you are"); }
        public void PreviewMood(string mood) { responseMood = mood; Sleeping = false; Mascot.SetMood(mood, ExpressionCatalog.Label(mood)); if (!Busy && !Speech.IsSpeaking && !Speech.IsListening) Chat.SetStatus("Ready when you are"); }
        public void ClearConversation() { if (Busy) CancelCurrent(); engine.ClearConversation(); Speech.StopSpeaking(); Chat.ClearMessages(); responseMood = Settings.PreferredMood; SetReady(); }
        public bool ApplySettings() { Speech.Rate = Settings.SpeechRate; Speech.UseNaturalVoice = Settings.NaturalVoiceEnabled; if (!Settings.VoiceEnabled) Speech.StopSpeaking(); Mascot.Topmost = Settings.AlwaysOnTop; SetReady(); Chat.UpdateSettings(); return SaveSettings(); }
        public bool SaveSettings() { try { Settings.Save(); return true; } catch { Chat.SetStatus("Settings could not be saved"); return false; } }
        public void Exit() { if (IsExiting) return; IsExiting = true; SaveSettings(); Dispose(); Application.Current.Shutdown(); }
        public void Dispose() { if (pending != null) pending.Cancel(); Speech.Dispose(); Gmail.Dispose(); engine.Dispose(); if (hotkey != null) hotkey.Dispose(); if (tray != null) { tray.Visible = false; tray.Dispose(); } }
    }
}
