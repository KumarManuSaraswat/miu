using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Miu
{
    internal static class Skin
    {
        // Mint is retained as an API name for existing controls; the palette is monochrome.
        public static readonly Brush Ink = B("#24262B"), Muted = B("#777B84"), Paper = B("#FAFAFB"), Mint = B("#E7E9ED"), Line = B("#E6E7EB");
        public static Brush B(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        public static TextBlock Text(string text, double size, Brush color = null, FontWeight? weight = null)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = color ?? Ink, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        }
        public static Button Button(string label, RoutedEventHandler handler, string accessible = null)
        {
            var button = new Button { Content = label };
            if (handler != null) button.Click += handler;
            AutomationProperties.SetName(button, accessible ?? label);
            return button;
        }
        public static Button Icon(string glyph, string label, RoutedEventHandler handler)
        {
            var button = Button(glyph, handler, label);
            button.FontFamily = new FontFamily("Segoe MDL2 Assets");
            button.FontSize = 16; button.Width = 36; button.Height = 36; button.Padding = new Thickness(0);
            button.Background = Brushes.Transparent; button.ToolTip = label;
            return button;
        }
        public static Border Card(UIElement child, string color = "#FFFFFF", double radius = 16)
        {
            return new Border { Child = child, Background = B(color), CornerRadius = new CornerRadius(radius), Padding = new Thickness(16), BorderBrush = Line, BorderThickness = new Thickness(1) };
        }
    }
}
