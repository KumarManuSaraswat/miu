using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Miu
{
    public sealed class SettingsWindow : Window
    {
        private readonly AppController app;
        private readonly TextBlock gmailStatus, voiceStatus, notice;
        private readonly CheckBox voice, naturalVoice, cloud, localAi, motion, onTop, freeTier;
        private readonly PasswordBox key;
        private readonly TextBox model, googleSignInLink;
        private readonly Slider rate;
        private readonly Button connect, disconnect, import, reopenSignIn, cancelSignIn;
        private readonly StackPanel signInRecovery;
        private readonly Expander googleSignIn;
        private CancellationTokenSource accountOperation;
        private bool connectingGmail, isClosed;
        public SettingsWindow(AppController controller)
        {
            app = controller; Title = "Miu settings"; Width = 478; Height = Math.Min(710, SystemParameters.WorkArea.Height - 32);
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Skin.Paper; Foreground = Skin.Ink;
            FontFamily = new FontFamily("Segoe UI"); ResizeMode = ResizeMode.CanResize; MinWidth = 430; MinHeight = 450;
            var root = new DockPanel(); Content = root;
            var footer = new StackPanel { Margin = new Thickness(24, 12, 24, 18) };
            notice = Skin.Text("", 11, Skin.Muted); notice.Margin = new Thickness(0, 0, 0, 8); footer.Children.Add(notice);
            var save = Skin.Button("Save settings", delegate { Save(); }); save.Background = Skin.Mint; footer.Children.Add(save); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var panel = new StackPanel { Margin = new Thickness(24, 18, 24, 8) };
            root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            panel.Children.Add(Skin.Text("Make Miu yours.", 26, Skin.Ink, FontWeights.SemiBold));
            var subtitle = Skin.Text("Free essentials. Optional connections.", 13, Skin.Muted); subtitle.Margin = new Thickness(0, 6, 0, 20); panel.Children.Add(subtitle);
            Section(panel, "Voice & desktop");
            voice = Check(panel, "Speak replies", app.Settings.VoiceEnabled);
            naturalVoice = Check(panel, "Use the natural English voice (offline)", app.Settings.NaturalVoiceEnabled);
            panel.Children.Add(Skin.Text(app.Speech.HasNaturalVoice ? "A softer voice, generated on this PC. It unloads after each reply." : "Windows voice is the fallback until the natural voice is installed.", 11, Skin.Muted));
            onTop = Check(panel, "Keep the little mascot on top of other windows", app.Settings.AlwaysOnTop);
            motion = Check(panel, "Reduce animation", app.Settings.ReducedMotion);
            panel.Children.Add(Skin.Text("Speaking speed", 12, Skin.Muted));
            rate = new Slider { Minimum = -5, Maximum = 5, Value = app.Settings.SpeechRate, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 10) }; panel.Children.Add(rate);
            AutomationProperties.SetName(rate, "Speaking speed");
            var testVoice = Skin.Button("Save & try voice", delegate { if (Save()) app.Speech.Speak("Hi, I'm Miu. I'm here to help. What would you like to do today?"); }); testVoice.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(testVoice);
            voiceStatus = Skin.Text(app.Speech.DescribeVoices() + "\n" + app.Speech.DescribeRecognizers(), 11, Skin.Muted); panel.Children.Add(voiceStatus);
            var fallback = Skin.Text("Tap the mic for one turn. Ctrl+Alt+Space works anywhere. You can also click the message box and use Windows voice typing (Win+H).", 11, Skin.Muted); fallback.Margin = new Thickness(0, 9, 0, 7); panel.Children.Add(fallback);
            var microphoneSettings = Skin.Button("Choose or test my microphone", delegate { OpenUrl("ms-settings:sound"); }); microphoneSettings.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(microphoneSettings);
            var expressionRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 14) };
            foreach (string mood in new[] { "happy", "curious", "excited", "love", "sad", "sleepy", "angry", "confused" })
            {
                string chosen = mood;
                var button = Skin.Button(mood, delegate { app.PreviewMood(chosen); });
                button.Content = new MascotFace { Width = 42, Height = 45, Mood = mood }; button.Padding = new Thickness(2); button.Margin = new Thickness(0, 0, 4, 4); button.ToolTip = mood; expressionRow.Children.Add(button);
                AutomationProperties.SetName(button, "Preview " + mood + " face");
            }
            panel.Children.Add(expressionRow);
            var reactions = Skin.Button("Explore all 24 reactions", delegate { app.ShowReactions(); }); reactions.Margin = new Thickness(0, 0, 0, 18); panel.Children.Add(reactions);
            var presets = Skin.Button("Choose a companion preset", delegate { app.ShowCommunity(); }); presets.Margin = new Thickness(0, 0, 0, 18); panel.Children.Add(presets);
            Section(panel, "Gmail");
            gmailStatus = Skin.Text("", 12, Skin.Muted); gmailStatus.Margin = new Thickness(0, 3, 0, 10); panel.Children.Add(gmailStatus);
            panel.Children.Add(Skin.Text("A free, one-time Google setup is needed to let your own app read your inbox. Miu never sends or deletes emails.", 11, Skin.Muted));
            var accountButtons = new WrapPanel { Margin = new Thickness(0, 10, 0, 6) };
            import = Skin.Button("1  Import Google JSON", delegate { Import(); }); import.Margin = new Thickness(0, 0, 6, 6); accountButtons.Children.Add(import);
            connect = Skin.Button("2  Connect Gmail", async delegate { await Connect(); }); connect.Margin = new Thickness(0, 0, 0, 6); accountButtons.Children.Add(connect); panel.Children.Add(accountButtons);
            var accountOther = new WrapPanel();
            var guide = Skin.Button("Setup guide", delegate { OpenGuide("GMAIL_SETUP.md"); }); guide.Margin = new Thickness(0, 0, 6, 0); accountOther.Children.Add(guide);
            disconnect = Skin.Button("Disconnect", async delegate { await Disconnect(); }); accountOther.Children.Add(disconnect); panel.Children.Add(accountOther);
            signInRecovery = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
            var recoveryButtons = new WrapPanel();
            reopenSignIn = Skin.Button("Open sign-in again", delegate { ReopenSignIn(); }); reopenSignIn.Margin = new Thickness(0, 0, 6, 6); recoveryButtons.Children.Add(reopenSignIn);
            cancelSignIn = Skin.Button("Cancel sign-in", delegate { CancelSignIn(); }); cancelSignIn.Margin = new Thickness(0, 0, 0, 6); recoveryButtons.Children.Add(cancelSignIn); signInRecovery.Children.Add(recoveryButtons);
            var linkPanel = new StackPanel();
            linkPanel.Children.Add(Skin.Text("Copy this link into another browser if needed. Keep Miu open while you sign in.", 11, Skin.Muted));
            googleSignInLink = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 84, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 0), FontSize = 11 };
            linkPanel.Children.Add(googleSignInLink);
            AutomationProperties.SetName(googleSignInLink, "Google sign-in link");
            googleSignIn = new Expander { Header = "Google sign-in link", Content = linkPanel, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            signInRecovery.Children.Add(googleSignIn); panel.Children.Add(signInRecovery);
            var privacy = Skin.Text("Mail is fetched only when asked and is never passed to the chat model. Sign-in tokens are encrypted for your Windows account.", 11, Skin.Muted); privacy.Margin = new Thickness(0, 9, 0, 20); panel.Children.Add(privacy);
            Section(panel, "Offline AI");
            localAi = Check(panel, "Use the included Gemma 3 model", app.Settings.LocalAiEnabled);
            panel.Children.Add(Skin.Text("Chat runs on this laptop with no account or API key. The model uses about 1 GB of memory while answering, then unloads. Replies can be mistaken; check important facts.", 12, Skin.Muted));
            var modelTerms = Skin.Button("Model terms and credits", delegate { OpenGuide("MODEL_TERMS.md"); }); modelTerms.Margin = new Thickness(0, 8, 0, 18); panel.Children.Add(modelTerms);
            Section(panel, "Cloud conversation · optional");
            panel.Children.Add(Skin.Text("Used only when you turn offline AI off. Connecting Gemini sends your chat text to Google; email results stay local.", 12, Skin.Muted));
            cloud = Check(panel, "Enable Gemini conversation", app.Settings.CloudEnabled);
            panel.Children.Add(Skin.Text("Gemini API key", 11, Skin.Muted));
            key = new PasswordBox { Password = app.Settings.GeminiApiKey, Padding = new Thickness(10), Margin = new Thickness(0, 5, 0, 9), BorderBrush = Skin.Line, BorderThickness = new Thickness(1) }; panel.Children.Add(key);
            AutomationProperties.SetName(key, "Gemini API key");
            panel.Children.Add(Skin.Text("Model", 11, Skin.Muted));
            string displayedModel = String.Equals(app.Settings.GeminiModel, "gemini-2.5-flash-lite", StringComparison.OrdinalIgnoreCase) ? "gemini-3.5-flash-lite" : app.Settings.GeminiModel;
            model = new TextBox { Text = displayedModel, ToolTip = "For example: gemini-3.5-flash-lite", Margin = new Thickness(0, 5, 0, 6) }; panel.Children.Add(model);
            AutomationProperties.SetName(model, "Gemini model");
            freeTier = Check(panel, "I am using a free-tier project without paid billing", app.Settings.CloudEnabled);
            panel.Children.Add(Skin.Text("Free-tier quotas and regional availability apply. Google may use free-tier chat content to improve its products. Miu cannot verify your project's billing status. Keep billing disabled to avoid charges.", 11, Skin.Muted));
            var keyLink = Skin.Button("Get a free API key ↗", delegate { OpenUrl("https://aistudio.google.com/api-keys"); }); keyLink.Margin = new Thickness(0, 10, 0, 18); panel.Children.Add(keyLink);
            Section(panel, "Your space");
            panel.Children.Add(Skin.Text("No telemetry. No always-on microphone. Chat is kept in memory for this session. Notes and encrypted credentials stay in your Windows profile.", 11, Skin.Muted));
            var notes = Skin.Button("Open notes folder", delegate { try { string folder = Path.Combine(AppPaths.DataDirectory, "Notes"); Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); } catch { notice.Text = "The notes folder could not be opened. Please try again."; } }); notes.Margin = new Thickness(0, 10, 0, 8); panel.Children.Add(notes);
            var help = Skin.Button("Command guide", delegate { OpenGuide("COMMANDS.md"); }); panel.Children.Add(help);
            var about = new StackPanel { Margin = new Thickness(0, 20, 0, 8) };
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            about.Children.Add(Skin.Text("Miu " + version + " · Windows preview", 13, Skin.Ink, FontWeights.SemiBold));
            about.Children.Add(Skin.Text("Open source under the MIT license. Optional voice components have their own licenses.", 11, Skin.Muted));
            var project = Skin.Button("Project & support ↗", delegate { OpenUrl("https://github.com/KumarManuSaraswat/miu"); }); project.Margin = new Thickness(0, 8, 0, 0); about.Children.Add(project);
            panel.Children.Add(about);
            app.Gmail.AuthorizationUrlChanged += AuthorizationUrlChanged;
            Closed += delegate
            {
                isClosed = true;
                app.Gmail.AuthorizationUrlChanged -= AuthorizationUrlChanged;
                googleSignInLink.Clear();
                if (accountOperation != null) accountOperation.Cancel();
            };
            UpdateAccount();
        }
        private static void Section(StackPanel parent, string title) { var t = Skin.Text(title, 15, Skin.Ink, FontWeights.SemiBold); t.Margin = new Thickness(0, 0, 0, 8); parent.Children.Add(t); }
        private static CheckBox Check(StackPanel parent, string text, bool value) { var c = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = value }; parent.Children.Add(c); return c; }
        public void RefreshAppearanceSettings() { rate.Value = app.Settings.SpeechRate; motion.IsChecked = app.Settings.ReducedMotion; }
        private bool Save()
        {
            if (cloud.IsChecked == true && (String.IsNullOrWhiteSpace(key.Password) || freeTier.IsChecked != true)) { notice.Text = "Add your free-tier key and confirm that paid billing is disabled, or leave Gemini off."; return false; }
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Text.Trim(), "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,100}$")) { notice.Text = "Enter a model name such as gemini-3.5-flash-lite."; return false; }
            try
            {
                app.Settings.GeminiApiKey = key.Password;
                app.Settings.VoiceEnabled = voice.IsChecked == true; app.Settings.CloudEnabled = cloud.IsChecked == true;
                app.Settings.LocalAiEnabled = localAi.IsChecked == true;
                app.Settings.NaturalVoiceEnabled = naturalVoice.IsChecked == true;
                app.Settings.ReducedMotion = motion.IsChecked == true; app.Settings.AlwaysOnTop = onTop.IsChecked == true;
                app.Settings.SpeechRate = (int)rate.Value; app.Settings.GeminiModel = model.Text.Trim();
                if (!app.ApplySettings()) { notice.Text = "These settings are active for this session, but could not be saved. Check that your Windows profile is writable."; return false; }
                notice.Text = "Saved. Miu is ready."; return true;
            }
            catch { notice.Text = "Settings could not be saved. Check that your Windows profile is writable."; return false; }
        }
        private void Import()
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Google desktop credentials (*.json)|*.json", Title = "Choose your Google Desktop OAuth JSON" };
            if (picker.ShowDialog(this) != true) return;
            try { app.Gmail.ImportCredentials(picker.FileName); notice.Text = "Credentials imported. Now connect Gmail."; UpdateAccount(); }
            catch (Exception e) { notice.Text = e.Message; }
        }
        private async System.Threading.Tasks.Task Connect()
        {
            if (accountOperation != null) return;
            accountOperation = new CancellationTokenSource(); connectingGmail = true; SetAccountBusy(true); notice.Text = "Finish signing in in your browser. You can reopen the link or cancel below Gmail.";
            try { string address = await app.Gmail.ConnectAsync(accountOperation.Token); notice.Text = "Connected: " + address; }
            catch (OperationCanceledException) { notice.Text = "Sign-in cancelled."; }
            catch (Exception e) { notice.Text = e.Message; }
            finally { accountOperation.Dispose(); accountOperation = null; connectingGmail = false; SetAccountBusy(false); UpdateAccount(); }
        }
        private async System.Threading.Tasks.Task Disconnect()
        {
            if (accountOperation != null) return;
            accountOperation = new CancellationTokenSource(); SetAccountBusy(true);
            try { await app.Gmail.DisconnectAsync(accountOperation.Token); app.ClearConversation(); notice.Text = "Gmail disconnected."; }
            catch (Exception e) { notice.Text = e.Message; }
            finally { accountOperation.Dispose(); accountOperation = null; SetAccountBusy(false); UpdateAccount(); }
        }
        private void SetAccountBusy(bool busy) { connect.IsEnabled = import.IsEnabled = disconnect.IsEnabled = !busy; UpdateSignInRecovery(); }
        private void AuthorizationUrlChanged(object sender, EventArgs args)
        {
            if (isClosed || Dispatcher.HasShutdownStarted) return;
            if (Dispatcher.CheckAccess()) UpdateSignInRecovery();
            else
            {
                try { Dispatcher.BeginInvoke(new Action(UpdateSignInRecovery)); }
                catch (InvalidOperationException) { }
            }
        }
        private void UpdateSignInRecovery()
        {
            if (isClosed) return;
            bool pending = connectingGmail && accountOperation != null;
            string url = pending ? app.Gmail.PendingAuthorizationUrl : "";
            bool hasLink = !String.IsNullOrEmpty(url);
            signInRecovery.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
            googleSignIn.Visibility = hasLink ? Visibility.Visible : Visibility.Collapsed;
            if (hasLink && googleSignInLink.Text != url) googleSignIn.IsExpanded = true;
            googleSignInLink.Text = url;
            bool canCancel = pending && !accountOperation.IsCancellationRequested;
            cancelSignIn.IsEnabled = canCancel;
            reopenSignIn.IsEnabled = hasLink && canCancel;
        }
        private void CancelSignIn()
        {
            if (!connectingGmail || accountOperation == null) return;
            notice.Text = "Cancelling Google sign-in...";
            accountOperation.Cancel();
            UpdateSignInRecovery();
        }
        private void ReopenSignIn()
        {
            if (!connectingGmail || accountOperation == null || accountOperation.IsCancellationRequested) return;
            string url = app.Gmail.PendingAuthorizationUrl;
            if (String.IsNullOrEmpty(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); notice.Text = "Sign-in opened again. You can also copy the Google sign-in link into another browser."; }
            catch { googleSignIn.IsExpanded = true; notice.Text = "The browser could not open. Copy the Google sign-in link into your browser."; }
        }
        private void UpdateAccount()
        {
            gmailStatus.Text = app.Gmail.IsConnected ? "●  Connected · read-only access" : app.Gmail.HasCredentials ? "○  Credentials ready · sign in to connect" : "○  Not connected yet";
            connect.IsEnabled = app.Gmail.HasCredentials && accountOperation == null; disconnect.IsEnabled = app.Gmail.IsConnected && accountOperation == null;
        }
        private void OpenGuide(string name)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", name);
            try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = false }); }
            catch { notice.Text = "Open docs/" + name + " in the Miu folder."; }
        }
        private void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { notice.Text = "Open " + url + " in your browser."; } }
    }
}
