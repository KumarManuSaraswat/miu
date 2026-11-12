using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Miu
{
    public sealed class CommunityWindow : Window
    {
        private readonly AppController app;
        private readonly CommunityCatalog catalog = new CommunityCatalog();
        private readonly TextBox address;
        private readonly TextBlock status;
        private readonly StackPanel cards;
        private readonly Button load;
        private CancellationTokenSource pending;
        private bool closed;
        public CommunityWindow(AppController controller)
        {
            app = controller; Title = "Miu presets"; Width = 560; Height = Math.Min(720, SystemParameters.WorkArea.Height - 36);
            MinWidth = 540; MinHeight = 440; Background = Skin.Paper; Foreground = Skin.Ink;
            FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Background = Skin.Paper, Margin = new Thickness(24) }; Content = root;
            var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            header.Children.Add(Skin.Text("A little more you.", 25, Skin.Ink, FontWeights.SemiBold));
            var intro = Skin.Text("Presets change Miu’s face, motion, and speaking speed. Pick one whenever you like.", 12, Skin.Muted); intro.Margin = new Thickness(0, 8, 0, 14); header.Children.Add(intro);
            var remote = new StackPanel();
            remote.Children.Add(Skin.Text("Enter a community catalog address provided by a maintainer. Loading it shares your IP address with that service; it sends no chat, audio, email, or account details.", 11, Skin.Muted));
            address = new TextBox { Text = app.Settings.CatalogUrl, Margin = new Thickness(0, 8, 0, 8), MaxLength = 2048, ToolTip = "https://your-catalog.example/v1/presets" }; remote.Children.Add(address);
            AutomationProperties.SetName(address, "Community catalog HTTPS address");
            load = Skin.Button("Load community presets", async delegate { await LoadCatalog(); }); remote.Children.Add(load);
            header.Children.Add(new Expander { Header = "Community catalog · optional", Content = remote, Margin = new Thickness(0, 0, 0, 12) });
            status = Skin.Text("Built-in presets · available offline", 11, Skin.Muted); status.Margin = new Thickness(0, 0, 0, 12); header.Children.Add(status);
            cards = new StackPanel(); root.Children.Add(new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            ShowPresets(CommunityCatalog.BuiltInPresets());
            Closed += delegate { closed = true; if (pending != null) pending.Cancel(); catalog.Dispose(); };
        }
        private async System.Threading.Tasks.Task LoadCatalog()
        {
            if (pending != null) return;
            Uri endpoint;
            if (!CommunityCatalog.TryEndpoint(address.Text, out endpoint)) { status.Text = "Use an HTTPS address ending in /v1/presets. Never paste a database password or connection string here."; return; }
            pending = new CancellationTokenSource(); load.IsEnabled = false; address.IsEnabled = false; status.Text = "Loading community presets…";
            try
            {
                var presets = await catalog.FetchAsync(endpoint.AbsoluteUri, pending.Token);
                if (closed) return;
                string old = app.Settings.CatalogUrl; app.Settings.CatalogUrl = endpoint.AbsoluteUri;
                try { app.Settings.Save(); } catch { app.Settings.CatalogUrl = old; throw; }
                ShowPresets(presets);
                status.Text = presets.Length == 0 ? "This catalog has no published presets yet." : "Community presets · " + endpoint.Host;
            }
            catch (OperationCanceledException) { if (!closed) status.Text = "The catalog took too long. Your current presets are still available."; }
            catch (Exception) { if (!closed) status.Text = "The catalog could not be loaded or saved. Check its address and try again. Your current presets are still available."; }
            finally { pending.Dispose(); pending = null; if (!closed) { load.IsEnabled = true; address.IsEnabled = true; } }
        }
        private void ShowPresets(CommunityPreset[] presets)
        {
            cards.Children.Clear();
            foreach (CommunityPreset preset in presets)
            {
                CommunityPreset selected = preset;
                var card = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                var heading = new StackPanel { Orientation = Orientation.Horizontal };
                heading.Children.Add(new MascotFace { Width = 65, Height = 65, Mood = preset.Mood });
                var text = new StackPanel { Margin = new Thickness(12, 4, 0, 0), Width = 340 };
                text.Children.Add(Skin.Text(preset.Name, 16, Skin.Ink, FontWeights.SemiBold));
                text.Children.Add(Skin.Text(preset.Description, 12, Skin.Muted)); heading.Children.Add(text); card.Children.Add(heading);
                card.Children.Add(Skin.Text("Voice speed " + preset.SpeechRate + " · " + (preset.ReducedMotion ? "Reduced motion" : "Animated"), 11, Skin.Muted));
                var apply = Skin.Button("Apply " + preset.Name, delegate
                {
                    try { app.ApplyPreset(selected); status.Text = selected.Name + " applied. Your accounts and chat settings stay the same."; }
                    catch { status.Text = "This preset could not be saved. Your previous settings are still active."; }
                }); apply.Margin = new Thickness(0, 8, 0, 0); card.Children.Add(apply);
                cards.Children.Add(new Border { Child = card, Padding = new Thickness(12), BorderBrush = Skin.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 10) });
            }
            var restore = Skin.Button("Show built-in presets", delegate { ShowPresets(CommunityCatalog.BuiltInPresets()); status.Text = "Built-in presets · available offline"; }); cards.Children.Add(restore);
        }
    }
}
