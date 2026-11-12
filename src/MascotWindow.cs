using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Miu
{
    public sealed class MascotWindow : Window
    {
        public readonly MascotFace Face;
        private readonly AppController app;
        private readonly TextBlock status;
        private readonly DispatcherTimer blink = new DispatcherTimer(), unblink = new DispatcherTimer(), motion = new DispatcherTimer();
        private Point down;
        private bool dragged;
        public MascotWindow(AppController controller)
        {
            app = controller;
            Title = "Miu companion"; Width = 170; Height = 192; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = app.Settings.AlwaysOnTop;
            var root = new Grid { Background = Brushes.Transparent };
            Face = new MascotFace { Width = 164, Height = 164, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand };
            root.Children.Add(Face);
            status = Skin.Text("Click to chat", 11, Skin.Ink);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            var label = new Border { Child = status, Background = Skin.Paper, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 2), IsHitTestVisible = false };
            root.Children.Add(label); Content = root;
            Face.ToolTip = "Click to chat • Double-click to talk • Drag to move • Right-click for more";
            Face.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                down = e.GetPosition(this); dragged = false;
                if (e.ClickCount == 2) { app.ToggleListening(); e.Handled = true; return; }
                Face.CaptureMouse();
            };
            Face.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (!Face.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
                Point now = e.GetPosition(this);
                if (Math.Abs(now.X - down.X) + Math.Abs(now.Y - down.Y) > 6)
                {
                    dragged = true; Face.ReleaseMouseCapture();
                    try { DragMove(); } catch (InvalidOperationException) { }
                    KeepOnScreen(); app.Settings.MascotLeft = Left; app.Settings.MascotTop = Top; app.SaveSettings();
                }
            };
            Face.MouseLeftButtonUp += delegate
            {
                if (!Face.IsMouseCaptured) return;
                Face.ReleaseMouseCapture(); if (!dragged) app.ShowPanel();
            };
            var menu = new ContextMenu();
            AddMenu(menu, "Talk to Miu     Ctrl+Alt+Space", delegate { app.ToggleListening(); });
            AddMenu(menu, "Open chat", delegate { app.ShowPanel(); });
            AddMenu(menu, "Reactions", delegate { app.ShowReactions(); });
            AddMenu(menu, "Settings", delegate { app.ShowSettings(); });
            AddMenu(menu, "Sleep / wake", delegate { app.SetSleeping(!app.Sleeping); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "Quit Miu", delegate { app.Exit(); });
            Face.ContextMenu = menu;
            Loaded += delegate
            {
                Rect area = SystemParameters.WorkArea;
                Left = app.Settings.MascotLeft < 0 ? area.Right - Width - 24 : app.Settings.MascotLeft;
                Top = app.Settings.MascotTop < 0 ? area.Bottom - Height - 16 : app.Settings.MascotTop;
                KeepOnScreen();
            };
            blink.Interval = TimeSpan.FromSeconds(4.5);
            blink.Tick += delegate { if (!app.Settings.ReducedMotion && !app.Sleeping) { Face.Blink = true; Face.InvalidateVisual(); unblink.Start(); } };
            unblink.Interval = TimeSpan.FromMilliseconds(140);
            unblink.Tick += delegate { unblink.Stop(); Face.Blink = false; Face.InvalidateVisual(); };
            motion.Interval = TimeSpan.FromMilliseconds(150);
            motion.Tick += delegate { Face.Frame++; Face.InvalidateVisual(); };
            blink.Start();
            Closed += delegate { blink.Stop(); unblink.Stop(); motion.Stop(); };
        }
        private static void AddMenu(ContextMenu menu, string text, Action action) { var item = new MenuItem { Header = text }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        private void KeepOnScreen()
        {
            // Keep a recovered position reachable even when the previous monitor has been removed.
            Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
        }
        public void SetMood(string mood, string caption)
        {
            Face.Mood = mood; status.Text = caption; Face.InvalidateVisual();
            if ((Face.IsTalking || mood == "speaking") && !app.Settings.ReducedMotion) motion.Start(); else motion.Stop();
            if (mood == "sleepy" || app.Settings.ReducedMotion) blink.Stop(); else if (!blink.IsEnabled) blink.Start();
        }
        public void SetTalking(bool talking)
        {
            Face.IsTalking = talking;
            if (talking && !app.Settings.ReducedMotion) motion.Start(); else motion.Stop();
            Face.InvalidateVisual();
        }
    }
}
