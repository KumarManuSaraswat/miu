using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Miu
{
    public static class AppPaths
    {
        public static string DataDirectory
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MIU_DATA_DIR");
                if (String.IsNullOrWhiteSpace(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Miu");
                Directory.CreateDirectory(path);
                return path;
            }
        }
    }

    public static class SecureStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Miu.Desktop.v1");
        private static readonly object Sync = new object();
        private static string FileName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
                throw new ArgumentException("Invalid secret name.");
            return Path.Combine(AppPaths.DataDirectory, name + ".protected");
        }
        public static string Read(string name)
        {
            lock (Sync)
            {
                string path = FileName(name);
                if (!File.Exists(path)) return null;
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser)); }
                catch (CryptographicException) { throw new InvalidOperationException("Saved credentials cannot be unlocked by this Windows account. Reconnect in Settings."); }
            }
        }
        public static void Write(string name, string value)
        {
            lock (Sync)
            {
                string path = FileName(name);
                byte[] data = Encoding.UTF8.GetBytes(value ?? "");
                try
                {
                    File.WriteAllBytes(path + ".tmp", ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser));
                    if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
                }
                finally { Array.Clear(data, 0, data.Length); }
            }
        }
        public static void Delete(string name) { lock (Sync) { string path = FileName(name); if (File.Exists(path)) File.Delete(path); } }
    }

    public static class Json
    {
        public static Dictionary<string, object> Parse(string json) { return Map(new JavaScriptSerializer().DeserializeObject(json)); }
        public static string Stringify(object value) { return new JavaScriptSerializer().Serialize(value); }
        public static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        public static string Text(Dictionary<string, object> value, string key, string fallback = "")
        {
            object item;
            return value != null && value.TryGetValue(key, out item) && item != null ? Convert.ToString(item) : fallback;
        }
        public static IEnumerable<object> Items(object value)
        {
            var list = value as IEnumerable;
            if (list != null && !(value is string)) foreach (object item in list) yield return item;
        }
    }

    public class AppSettings
    {
        public const string DefaultGeminiModel = "gemini-3.5-flash-lite";
        public bool VoiceEnabled = true;
        public bool NaturalVoiceEnabled = true;
        public bool CloudEnabled = false;
        public bool LocalAiEnabled = true;
        public bool ReducedMotion = false;
        public bool AlwaysOnTop = true;
        public bool AutoListen = false;
        public string GeminiModel = DefaultGeminiModel;
        public string CatalogUrl = "";
        public string PreferredMood = "idle";
        public int SpeechRate = 0;
        public double MascotLeft = -1;
        public double MascotTop = -1;
        public string GeminiApiKey
        {
            get { return SecureStore.Read("gemini-key") ?? ""; }
            set { if (String.IsNullOrWhiteSpace(value)) SecureStore.Delete("gemini-key"); else SecureStore.Write("gemini-key", value.Trim()); }
        }
        public static AppSettings Load()
        {
            string file = Path.Combine(AppPaths.DataDirectory, "settings.json");
            if (!File.Exists(file)) return new AppSettings();
            try
            {
                var data = Json.Parse(File.ReadAllText(file));
                var result = new AppSettings();
                result.VoiceEnabled = Json.Text(data, "voice", "True") == "True";
                result.NaturalVoiceEnabled = Json.Text(data, "naturalVoice", "True") == "True";
                result.CloudEnabled = Json.Text(data, "cloud", "False") == "True";
                result.LocalAiEnabled = Json.Text(data, "localAi", "True") == "True";
                result.ReducedMotion = Json.Text(data, "reducedMotion", "False") == "True";
                result.AlwaysOnTop = Json.Text(data, "alwaysOnTop", "True") == "True";
                result.GeminiModel = MigrateGeminiModel(Json.Text(data, "model", result.GeminiModel));
                Uri catalog;
                if (CommunityCatalog.TryEndpoint(Json.Text(data, "catalogUrl"), out catalog)) result.CatalogUrl = catalog.AbsoluteUri;
                string mood = Json.Text(data, "preferredMood", "idle");
                if (ExpressionCatalog.IsSupported(mood)) result.PreferredMood = mood;
                int rate; if (Int32.TryParse(Json.Text(data, "speechRate"), out rate)) result.SpeechRate = Math.Max(-5, Math.Min(5, rate));
                double x, y;
                if (Double.TryParse(Json.Text(data, "left"), out x) && !Double.IsNaN(x) && !Double.IsInfinity(x)) result.MascotLeft = x;
                if (Double.TryParse(Json.Text(data, "top"), out y) && !Double.IsNaN(y) && !Double.IsInfinity(y)) result.MascotTop = y;
                return result;
            }
            catch { return new AppSettings(); }
        }
        internal static string MigrateGeminiModel(string value)
        {
            // The former default is restricted to prior active users. Preserve
            // every other explicit model choice when loading older settings.
            string model = (value ?? "").Trim();
            if (model.Length == 0 || String.Equals(model, "gemini-2.5-flash-lite", StringComparison.OrdinalIgnoreCase))
                return DefaultGeminiModel;
            return model;
        }
        public void Save()
        {
            var data = new Dictionary<string, object> { { "voice", VoiceEnabled }, { "naturalVoice", NaturalVoiceEnabled }, { "cloud", CloudEnabled }, { "reducedMotion", ReducedMotion },
                { "alwaysOnTop", AlwaysOnTop }, { "model", GeminiModel }, { "speechRate", SpeechRate }, { "left", MascotLeft }, { "top", MascotTop },
                { "catalogUrl", CatalogUrl }, { "preferredMood", PreferredMood }, { "localAi", LocalAiEnabled } };
            string file = Path.Combine(AppPaths.DataDirectory, "settings.json");
            File.WriteAllText(file + ".tmp", Json.Stringify(data));
            if (File.Exists(file)) File.Replace(file + ".tmp", file, null); else File.Move(file + ".tmp", file);
        }
    }
}
