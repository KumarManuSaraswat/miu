using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Miu
{
    public sealed class ChatWindow : Window
    {
        private readonly AppController app;
        private readonly StackPanel messages;
        private readonly ScrollViewer scroll;
        private readonly TextBox input;
        private readonly TextBlock hint, stateText, usage, engineLabel;
        private readonly Button talk, mute, send;
        private readonly DispatcherTimer metrics = new DispatcherTimer();
        private readonly Border welcome;
        private readonly StackPanel microphoneFeedback;
        private readonly ProgressBar microphoneLevel;
        private readonly TextBlock heardText;
        private bool isListening;
        private bool hasMessage;
        public ChatWindow(AppController controller)
        {
            app = controller;
            Title = "Miu — your little desktop companion"; Width = 440; Height = 668;
            MinWidth = 390; MinHeight = 520; MaxHeight = Math.Max(520, SystemParameters.WorkArea.Height - 28);
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip; Background = Skin.Paper;
            AllowsTransparency = true; Background = Brushes.Transparent;
            FontFamily = new FontFamily("Segoe UI"); Foreground = Skin.Ink; ShowInTaskbar = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
            var outer = new Border { Background = Skin.Paper, CornerRadius = new CornerRadius(22), BorderBrush = Skin.B("#DADDE2"), BorderThickness = new Thickness(1), ClipToBounds = true };
            var layout = new Grid(); outer.Child = layout; Content = outer;
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(22, 16, 16, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal };
            brand.Children.Add(new MascotFace { Width = 48, Height = 48, Margin = new Thickness(-6, 0, 7, 0) });
            var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(Skin.Text("miu", 24, Skin.Ink, FontWeights.Bold));
            brandText.Children.Add(Skin.Text("A little friend. A little help.", 11, Skin.Muted)); brand.Children.Add(brandText); header.Children.Add(brand);
            brand.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.ClickCount == 1) try { DragMove(); } catch { } };
            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            controls.Children.Add(Skin.Icon("\uE899", "Explore Miu’s reactions", delegate { app.ShowReactions(); }));
            controls.Children.Add(Skin.Icon("\uE713", "Settings", delegate { app.ShowSettings(); }));
            controls.Children.Add(Skin.Icon("\uE711", "Hide chat (Miu stays on your desktop)", delegate { Hide(); }));
            Grid.SetColumn(controls, 1); header.Children.Add(controls); layout.Children.Add(header);

            messages = new StackPanel { Margin = new Thickness(22, 6, 22, 10) };
            scroll = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
            welcome = MakeWelcome(); messages.Children.Add(welcome);

            var bottom = new StackPanel { Margin = new Thickness(20, 6, 20, 18) };
            var stateRow = new Grid { Margin = new Thickness(3, 0, 3, 10) };
            stateRow.ColumnDefinitions.Add(new ColumnDefinition()); stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stateText = Skin.Text("●  Ready when you are", 11, Skin.Muted); stateText.TextWrapping = TextWrapping.NoWrap; stateText.TextTrimming = TextTrimming.CharacterEllipsis; stateText.Margin = new Thickness(0, 0, 8, 0); stateRow.Children.Add(stateText);
            usage = Skin.Text("", 10, Skin.Muted); usage.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(usage, 1); stateRow.Children.Add(usage); bottom.Children.Add(stateRow);
            microphoneFeedback = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 4, 9) };
            microphoneLevel = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4, Foreground = Skin.Ink, Background = Skin.Line, BorderThickness = new Thickness(0) };
            AutomationProperties.SetName(microphoneLevel, "Microphone input level");
            heardText = Skin.Text("Speak when the microphone is ready.", 11, Skin.Muted); heardText.Margin = new Thickness(0, 5, 0, 0);
            microphoneFeedback.Children.Add(microphoneLevel); microphoneFeedback.Children.Add(heardText); bottom.Children.Add(microphoneFeedback);
            var compose = new Grid();
            compose.ColumnDefinitions.Add(new ColumnDefinition()); compose.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputBorder = new Border { CornerRadius = new CornerRadius(14), BorderBrush = Skin.Line, BorderThickness = new Thickness(1), Background = Brushes.White, Padding = new Thickness(2), Margin = new Thickness(0, 0, 8, 0) };
            var inputGrid = new Grid(); inputGrid.ColumnDefinitions.Add(new ColumnDefinition()); inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            input = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 44, MaxLength = 4000, Padding = new Thickness(12, 11, 3, 10), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(input, "Message Miu");
            hint = Skin.Text("Ask Miu something…", 13, Skin.Muted); hint.Margin = new Thickness(12, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center; hint.IsHitTestVisible = false;
            inputGrid.Children.Add(input); inputGrid.Children.Add(hint);
            input.TextChanged += delegate { hint.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            input.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { Submit(); e.Handled = true; } };
            send = Skin.Icon("\uE72A", "Send message", delegate { Submit(); }); send.Margin = new Thickness(0, 4, 4, 4); Grid.SetColumn(send, 1); inputGrid.Children.Add(send);
            inputBorder.Child = inputGrid; compose.Children.Add(inputBorder);
            talk = Skin.Icon("\uE720", "Talk to Miu — Ctrl+Alt+Space", delegate { app.ToggleListening(); });
            talk.Width = 50; talk.Height = 50; talk.Background = Skin.Mint; Grid.SetColumn(talk, 1); compose.Children.Add(talk); bottom.Children.Add(compose);
            var footer = new Grid { Margin = new Thickness(2, 9, 0, 0) };
            engineLabel = Skin.Text("ON DEVICE  /  NO SUBSCRIPTION", 9, Skin.Muted); engineLabel.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(engineLabel);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            mute = Skin.Icon("\uE767", "Toggle spoken replies", delegate { app.Settings.VoiceEnabled = !app.Settings.VoiceEnabled; app.ApplySettings(); }); mute.Width = 28; mute.Height = 24; mute.FontSize = 13; buttons.Children.Add(mute);
            var clear = Skin.Icon("\uE894", "Clear conversation", delegate { ClearMessages(); app.ClearConversation(); }); clear.Width = 28; clear.Height = 24; clear.FontSize = 12; buttons.Children.Add(clear); footer.Children.Add(buttons); bottom.Children.Add(footer);
            Grid.SetRow(bottom, 2); layout.Children.Add(bottom);
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) { if (!app.IsExiting) { e.Cancel = true; Hide(); } };
            IsVisibleChanged += delegate { if (IsVisible) { UpdateMetrics(); metrics.Start(); } else metrics.Stop(); };
            metrics.Interval = TimeSpan.FromSeconds(8); metrics.Tick += delegate { UpdateMetrics(); };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { app.CancelCurrent(); e.Handled = true; } };
            UpdateSettings();
        }
        private Border MakeWelcome()
        {
            var body = new StackPanel();
            var hero = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(134) });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border { Child = Skin.Text("YOUR DESKTOP SIDEKICK", 9, Skin.B("#626773"), FontWeights.SemiBold), Background = Skin.B("#ECEEF1"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 13) };
            copy.Children.Add(badge); copy.Children.Add(Skin.Text("Hey, I’m Miu.", 29, Skin.Ink, FontWeights.SemiBold));
            var intro = Skin.Text("Small companion.\nHere to make your day easier.", 12, Skin.Muted); intro.LineHeight = 19; intro.Margin = new Thickness(0, 8, 0, 0); copy.Children.Add(intro); hero.Children.Add(copy);
            var face = new MascotFace { Width = 132, Height = 140, Mood = "idle" }; Grid.SetColumn(face, 1); hero.Children.Add(face); body.Children.Add(hero);
            body.Children.Add(Skin.Text("What shall we do?", 14, Skin.Ink, FontWeights.SemiBold));
            body.Children.Add(QuickAction("\uE715", "Catch up on email", "Ask about your latest Gmail messages", "Check my latest email"));
            body.Children.Add(QuickAction("\uE770", "A hand with the little things", "Open apps, take a note, check the time", "What can you do?"));
            body.Children.Add(QuickAction("\uE8F2", "Let’s have a little chat", "Say hello, or tell me about your day", "Hello Miu"));
            var tip = new Border { Background = Skin.B("#F0F1F3"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 15, 0, 2) };
            tip.Child = Skin.Text("Tap the mic or press Ctrl + Alt + Space.\nI only listen when you ask me to.", 11, Skin.Muted); body.Children.Add(tip);
            return new Border { Child = body };
        }
        private Button QuickAction(string icon, string title, string description, string command)
        {
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            var symbol = Skin.Text(icon, 19, Skin.B("#666C77")); symbol.FontFamily = new FontFamily("Segoe MDL2 Assets"); symbol.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(symbol);
            var text = new StackPanel(); text.Children.Add(Skin.Text(title, 13, Skin.Ink, FontWeights.SemiBold)); var sub = Skin.Text(description, 11, Skin.Muted); sub.Margin = new Thickness(0, 3, 0, 0); text.Children.Add(sub); Grid.SetColumn(text, 1); grid.Children.Add(text);
            var arrow = Skin.Text("›", 20, Skin.Muted); arrow.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(arrow, 2); grid.Children.Add(arrow);
            var button = Skin.Button(title, delegate { app.Submit(command); }); button.Content = grid; button.Background = Brushes.White; button.BorderBrush = Skin.Line; button.BorderThickness = new Thickness(1); button.Margin = new Thickness(0, 9, 0, 0); button.Padding = new Thickness(12, 11, 10, 11); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }
        private void Submit() { string value = input.Text.Trim(); if (value.Length == 0 || app.Busy) return; input.Clear(); app.Submit(value); }
        public void AddMessage(string text, bool user, bool email = false)
        {
            if (!hasMessage) { messages.Children.Remove(welcome); hasMessage = true; }
            var stack = new StackPanel();
            var label = Skin.Text(user ? "YOU" : email ? "MIU  ·  GMAIL" : "MIU", 9, Skin.Muted, FontWeights.SemiBold); label.Margin = new Thickness(2, 0, 0, 5); stack.Children.Add(label);
            var content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), FontSize = 13, Foreground = Skin.Ink, IsTabStop = false };
            var bubble = new Border { Child = content, Background = user ? Skin.B("#ECEEF2") : Brushes.White, BorderBrush = Skin.Line, BorderThickness = new Thickness(user ? 0 : 1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12, 14, 12) }; stack.Children.Add(bubble);
            stack.Margin = new Thickness(user ? 36 : 0, 5, user ? 0 : 12, 12); messages.Children.Add(stack);
            while (messages.Children.Count > 40) messages.Children.RemoveAt(0);
            Dispatcher.BeginInvoke(new Action(delegate { scroll.ScrollToEnd(); }));
        }
        public void ClearMessages() { messages.Children.Clear(); hasMessage = false; messages.Children.Add(welcome); }
        public void SetStatus(string text) { stateText.Text = "●  " + text; stateText.ToolTip = text; }
        public void SetBusy(bool busy) { send.IsEnabled = !busy; talk.ToolTip = busy ? "Stop current response" : "Talk to Miu — Ctrl+Alt+Space"; }
        public void SetListening(bool listening)
        {
            isListening = listening; talk.Content = listening ? "\uE71A" : "\uE720"; talk.Background = listening ? Skin.B("#CDD2DA") : Skin.Mint;
            microphoneFeedback.Visibility = listening ? Visibility.Visible : Visibility.Collapsed;
            microphoneLevel.Value = 0; heardText.Text = "Speak now. The bar moves when your microphone hears sound.";
        }
        public void SetAudioLevel(int level) { if (isListening) microphoneLevel.Value = Math.Max(0, Math.Min(100, level)); }
        public void SetInputDevice(string device) { microphoneLevel.ToolTip = String.IsNullOrWhiteSpace(device) ? "Windows default microphone" : device; }
        public void SetPartialRecognition(string text)
        {
            if (!isListening || String.IsNullOrWhiteSpace(text)) return;
            heardText.Text = "Hearing: " + (text.Length > 100 ? text.Substring(0, 100) + "…" : text);
        }
        public void ReviewRecognition(string text)
        {
            if (!String.IsNullOrWhiteSpace(input.Text))
            {
                AddMessage("I was not sure I heard you correctly: “" + text + "”. Your typed message is still in the box.", false);
                SetStatus("I kept your draft. Check what I heard above."); return;
            }
            input.Text = text; input.CaretIndex = input.Text.Length; input.Focus();
            SetStatus("Check what I heard, then press Enter to send.");
        }
        public void FocusInput() { input.Focus(); }
        public void UpdateSettings()
        {
            mute.Content = app.Settings.VoiceEnabled ? "\uE767" : "\uE74F";
            mute.ToolTip = app.Settings.VoiceEnabled ? "Spoken replies on — click to mute" : "Spoken replies off — click to unmute";
            engineLabel.Text = app.Settings.CloudEnabled ? "GEMINI CHAT  /  LOCAL DEVICE COMMANDS" : "ON DEVICE  /  NO SUBSCRIPTION";
        }
        private void UpdateMetrics()
        {
            using (var process = Process.GetCurrentProcess()) usage.Text = Math.Round(process.WorkingSet64 / 1048576.0) + " MB app";
        }
        public void PlaceNearMascot()
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left + 8, Math.Min(app.Mascot.Left - Width - 14, area.Right - Width - 8));
            Top = Math.Max(area.Top + 8, Math.Min(app.Mascot.Top + app.Mascot.Height - Height, area.Bottom - Height - 8));
        }
    }
}
