using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Miu
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            if (args.Length > 0 && args[0] == "--self-test")
            {
                int result = EngineTests.Run();
                int gmailResult = GmailTests.Run();
                int catalogResult = CommunityCatalogTests.Run();
                int gazeResult = MascotGazeTests.Run();
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-results.txt"), EngineTests.LastReport + "\r\n" + GmailTests.LastReport + "\r\n" + CommunityCatalogTests.LastReport + "\r\n" + MascotGazeTests.LastReport);
                return result == 0 && gmailResult == 0 && catalogResult == 0 && gazeResult == 0 ? 0 : 1;
            }
            if (args.Length > 0 && args[0] == "--diagnostics")
            {
                using (var speech = new SpeechService())
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.txt"), "Miu desktop diagnostics\r\n" + Environment.OSVersion + "\r\n" + speech.DescribeVoices() + "\r\n" + speech.DescribeRecognizers());
                return 0;
            }
            if (args.Length > 1 && args[0] == "--render-preview")
            {
                var preview = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                using (Stream theme = Assembly.GetExecutingAssembly().GetManifestResourceStream("Miu.Theme.xaml"))
                    preview.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme));
                using (var controller = new AppController(true))
                {
                    Render(controller.Chat, args[1]);
                    RenderExpressions(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "miu-reactions.png"));
                    Render(new CommunityWindow(controller), Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "miu-presets.png"));
                }
                preview.Shutdown();
                return 0;
            }
            bool ownsMutex;
            using (var singleton = new Mutex(true, "Local\\Miu.Desktop.Companion.v1", out ownsMutex))
            {
                if (!ownsMutex) { MessageBox.Show("Miu is already running. Click the little mascot or the Miu icon in the system tray.", "Miu"); return 0; }
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                using (Stream theme = Assembly.GetExecutingAssembly().GetManifestResourceStream("Miu.Theme.xaml"))
                    application.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme));
                application.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    e.Handled = true;
                    MessageBox.Show("Miu ran into a problem. Please restart the app. Your account credentials are kept in your Windows profile.", "Miu", MessageBoxButton.OK, MessageBoxImage.Information);
                };
                AppController controller = null;
                try
                {
                    controller = new AppController();
                    application.Run();
                    return 0;
                }
                catch (Exception e)
                {
                    string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt");
                    try { File.WriteAllText(log, e.ToString()); } catch { }
                    MessageBox.Show("Miu could not start. Details are in startup-error.txt beside Miu.exe.\n\n" + e.Message, "Miu");
                    return 1;
                }
                finally { if (controller != null && !controller.IsExiting) controller.Dispose(); singleton.ReleaseMutex(); }
            }
        }
        private static void Render(Window window, string path)
        {
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(window.Width, window.Height));
            content.Arrange(new Rect(0, 0, window.Width, window.Height));
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.Width * 2, (int)window.Height * 2, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.GetFullPath(path))) encoder.Save(file);
        }
        private static void RenderExpressions(string path)
        {
            var surface = new System.Windows.Controls.Grid { Width = 660, Height = 1440, Background = Skin.B("#17181D") };
            for (int i = 0; i < 3; i++) surface.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            for (int i = 0; i < 8; i++) surface.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
            var moods = ExpressionCatalog.ReferenceMoods;
            for (int i = 0; i < moods.Length; i++)
            {
                var item = new System.Windows.Controls.StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                item.Children.Add(new MascotFace { Width = 152, Height = 152, Mood = moods[i], Frame = 2, AudioLevel = 35 });
                var label = Skin.Text(ExpressionCatalog.Label(moods[i]), 11, Skin.B("#A9ABB5")); label.HorizontalAlignment = HorizontalAlignment.Center; item.Children.Add(label);
                System.Windows.Controls.Grid.SetRow(item, i / 3); System.Windows.Controls.Grid.SetColumn(item, i % 3); surface.Children.Add(item);
            }
            surface.Measure(new Size(660, 1440)); surface.Arrange(new Rect(0, 0, 660, 1440)); surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(990, 2160, 144, 144, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }
}
