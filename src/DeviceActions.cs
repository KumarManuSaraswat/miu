using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Miu
{
    // Only fixed, documented actions can reach the Windows shell. User text is
    // never a command line, executable path, URL scheme, or process argument.
    public static class DeviceActions
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);

        public static bool IsAllowedApp(string app)
        {
            switch (app)
            {
                case "calculator": case "notepad": case "explorer": case "downloads":
                case "documents": case "browser": case "gmail": case "windows_settings":
                case "notes": return true;
                default: return false;
            }
        }

        public static string OpenApp(string app)
        {
            if (!IsAllowedApp(app)) throw new InvalidOperationException("That application is not in Miu's supported app list.");
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string target;
            string friendly;
            switch (app)
            {
                case "calculator": target = Path.Combine(windows, "System32", "calc.exe"); friendly = "Calculator"; break;
                case "notepad": target = Path.Combine(windows, "System32", "notepad.exe"); friendly = "Notepad"; break;
                case "explorer": target = Path.Combine(windows, "explorer.exe"); friendly = "File Explorer"; break;
                case "downloads": target = DownloadsFolder(); friendly = "Downloads"; break;
                case "documents": target = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); friendly = "Documents"; break;
                case "browser": target = "https://www.google.com/"; friendly = "your browser"; break;
                case "gmail": target = "https://mail.google.com/mail/u/0/#inbox"; friendly = "Gmail in your browser"; break;
                case "windows_settings": target = "ms-settings:"; friendly = "Windows Settings"; break;
                case "notes": target = NotesDirectory; Directory.CreateDirectory(target); friendly = "your Miu notes"; break;
                default: throw new InvalidOperationException("Unsupported application.");
            }
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return "Opened " + friendly + ".";
        }

        public static string SearchWeb(string query)
        {
            if (String.IsNullOrWhiteSpace(query)) return "Tell me what to search for, like: search the web for easy pasta recipes.";
            if (query.Length > 1500) return "That search is a little long. Please use fewer than 1,500 characters.";
            string url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return "Opened a web search for " + query + ".";
        }

        public static string ChangeVolume(string action)
        {
            byte key;
            switch (action)
            {
                case "up": key = 0xAF; break;
                case "down": key = 0xAE; break;
                case "toggle_mute": key = 0xAD; break;
                default: throw new InvalidOperationException("Unsupported volume action.");
            }
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 2, UIntPtr.Zero);
            return action == "toggle_mute" ? "Toggled your computer's sound mute." : "Turned the volume " + action + " one step.";
        }

        public static string BatteryStatus()
        {
            PowerStatus power = SystemInformation.PowerStatus;
            if (power.BatteryChargeStatus == BatteryChargeStatus.Unknown ||
                (power.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0)
                return "Windows doesn't report a battery on this device.";
            float fraction = power.BatteryLifePercent;
            if (fraction < 0 || fraction > 1) return "Windows can't read the battery level right now.";
            string result = "Your battery is at " + Math.Round(fraction * 100).ToString(CultureInfo.InvariantCulture) + " percent";
            result += power.PowerLineStatus == PowerLineStatus.Online ? ", and the device is plugged in." : ".";
            return result;
        }

        public static string SaveNote(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return "What should I write down? Try: take a note buy milk tomorrow.";
            if (text.Length > 12000) return "Please keep each note under 12,000 characters.";
            Directory.CreateDirectory(NotesDirectory);
            // A generated filename means note contents cannot choose or escape a path.
            string name = "Note-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt";
            string path = Path.Combine(NotesDirectory, name);
            using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (StreamWriter writer = new StreamWriter(file, new UTF8Encoding(false)))
                writer.WriteLine(text.Trim());
            return "Saved your note. Say 'open notes' to see it.";
        }

        public static string NotesDirectory { get { return Path.Combine(AppPaths.DataDirectory, "Notes"); } }

        private static string DownloadsFolder()
        {
            Guid folder = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            IntPtr value;
            int result = SHGetKnownFolderPath(ref folder, 0, IntPtr.Zero, out value);
            if (result == 0)
            {
                try { return Marshal.PtrToStringUni(value); }
                finally { Marshal.FreeCoTaskMem(value); }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
