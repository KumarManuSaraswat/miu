using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Miu
{
    public sealed class ReactionsWindow : Window
    {
        public ReactionsWindow(AppController app)
        {
            Title = "Miu reactions"; Width = 550; Height = Math.Min(760, SystemParameters.WorkArea.Height - 36);
            MinWidth = 520; MinHeight = 420; Background = Skin.Paper; Foreground = Skin.Ink;
            FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            heading.Children.Add(Skin.Text("A face for every feeling.", 24, Skin.Ink, FontWeights.SemiBold));
            var subtitle = Skin.Text("24 expressions. Click one to try it.", 12, Skin.Muted); subtitle.Margin = new Thickness(0, 7, 0, 0); heading.Children.Add(subtitle);
            var presets = Skin.Button("Companion presets", delegate { app.ShowCommunity(); }); presets.Margin = new Thickness(0, 10, 0, 0); heading.Children.Add(presets);
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            var footer = Skin.Text("Try saying “make a playful face” or “look amazed”.", 12, Skin.Muted); footer.Margin = new Thickness(0, 12, 0, 0); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var grid = new UniformGrid { Columns = 3 };
            foreach (string mood in ExpressionCatalog.ReferenceMoods)
            {
                string chosen = mood;
                var content = new StackPanel();
                content.Children.Add(new MascotFace { Width = 126, Height = 126, Mood = mood });
                var label = Skin.Text(ExpressionCatalog.Label(mood), 11, Skin.Muted); label.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(label);
                var button = Skin.Button(ExpressionCatalog.Label(mood), delegate { app.PreviewMood(chosen); });
                button.Content = content; button.Padding = new Thickness(4, 0, 4, 9); button.Margin = new Thickness(4);
                button.Background = Brushes.White; button.ToolTip = "Make a " + ExpressionCatalog.Label(mood).ToLowerInvariant() + " face"; grid.Children.Add(button);
            }
            root.Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }
    }
}
