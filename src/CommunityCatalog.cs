using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Miu
{
    public sealed class CommunityPreset
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Mood { get; private set; }
        public int SpeechRate { get; private set; }
        public bool ReducedMotion { get; private set; }
        internal CommunityPreset(string id, string name, string description, string mood, int speechRate, bool reducedMotion)
        { Id = id; Name = name; Description = description; Mood = mood; SpeechRate = speechRate; ReducedMotion = reducedMotion; }
    }

    // Public data only. No database connection string or user data is sent by this client.
    public sealed class CommunityCatalog : IDisposable
    {
        private const int MaximumBytes = 64 * 1024;
        private readonly HttpClient http;
        public CommunityCatalog() : this(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false }) { }
        internal CommunityCatalog(HttpMessageHandler handler)
        { http = new HttpClient(handler); http.Timeout = TimeSpan.FromSeconds(12); }

        public static CommunityPreset[] BuiltInPresets()
        {
            return new[] {
                new CommunityPreset("focus", "Focus", "A calm face, less movement, and a slightly slower voice.", "content", -1, true),
                new CommunityPreset("cheerful", "Cheerful", "A friendly smile and a lively speaking pace.", "cheerful", 1, false),
                new CommunityPreset("gentle", "Gentle", "A quiet expression and an unhurried voice.", "content", -2, true)
            };
        }

        public static bool TryEndpoint(string value, out Uri endpoint)
        {
            endpoint = null;
            Uri parsed;
            if (String.IsNullOrWhiteSpace(value) || value.Length > 2048 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out parsed)) return false;
            if (parsed.Scheme != Uri.UriSchemeHttps || parsed.Port != 443 || parsed.HostNameType != UriHostNameType.Dns ||
                parsed.IsLoopback || parsed.Host.IndexOf('.') < 0 || parsed.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                parsed.UserInfo.Length != 0 || parsed.Query.Length != 0 || parsed.Fragment.Length != 0 || parsed.AbsolutePath != "/v1/presets") return false;
            endpoint = parsed; return true;
        }

        public async Task<CommunityPreset[]> FetchAsync(string address, CancellationToken cancellation)
        {
            Uri endpoint;
            if (!TryEndpoint(address, out endpoint)) throw new InvalidOperationException("Enter an HTTPS catalog address ending in /v1/presets, without keys or query parameters.");
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(12));
                using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint))
                {
                    request.Headers.Accept.ParseAdd("application/json");
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The catalog is unavailable. Built-in presets still work.");
                        if (response.Content.Headers.ContentType == null || response.Content.Headers.ContentType.MediaType != "application/json") throw InvalidCatalog();
                        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaximumBytes) throw InvalidCatalog();
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            byte[] chunk = new byte[4096];
                            int read;
                            while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false)) > 0)
                            {
                                if (buffer.Length + read > MaximumBytes) throw InvalidCatalog();
                                buffer.Write(chunk, 0, read);
                            }
                            try { return Parse(new UTF8Encoding(false, true).GetString(buffer.ToArray())); }
                            catch (DecoderFallbackException) { throw InvalidCatalog(); }
                        }
                    }
                }
            }
        }

        internal static CommunityPreset[] Parse(string json)
        {
            if (String.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw InvalidCatalog();
            try
            {
                var root = new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 8 }.DeserializeObject(json) as Dictionary<string, object>;
                RequireFields(root, "schemaVersion", "presets");
                if (!(root["schemaVersion"] is int) || (int)root["schemaVersion"] != 1) throw InvalidCatalog();
                var rows = root["presets"] as object[];
                if (rows == null || rows.Length > 64) throw InvalidCatalog();
                var result = new List<CommunityPreset>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (object item in rows)
                {
                    var row = item as Dictionary<string, object>;
                    RequireFields(row, "id", "name", "description", "mood", "speechRate", "reducedMotion");
                    string id = DisplayText(row["id"], 64), name = DisplayText(row["name"], 80), description = DisplayText(row["description"], 240), mood = DisplayText(row["mood"], 40);
                    if (!Regex.IsMatch(id, @"^[a-z0-9][a-z0-9-]{0,63}$") || !ids.Add(id) || !ExpressionCatalog.IsSupported(mood)) throw InvalidCatalog();
                    if (!(row["speechRate"] is int) || (int)row["speechRate"] < -5 || (int)row["speechRate"] > 5 || !(row["reducedMotion"] is bool)) throw InvalidCatalog();
                    result.Add(new CommunityPreset(id, name, description, mood, (int)row["speechRate"], (bool)row["reducedMotion"]));
                }
                return result.ToArray();
            }
            catch (InvalidOperationException) { throw; }
            catch (ArgumentException) { throw InvalidCatalog(); }
            catch (FormatException) { throw InvalidCatalog(); }
        }
        private static void RequireFields(Dictionary<string, object> value, params string[] fields)
        {
            if (value == null || value.Count != fields.Length) throw InvalidCatalog();
            foreach (string field in fields) if (!value.ContainsKey(field)) throw InvalidCatalog();
        }
        private static string DisplayText(object value, int maximum)
        {
            string text = value as string;
            if (String.IsNullOrWhiteSpace(text) || text.Length > maximum || text != text.Trim() || text.IndexOfAny(new[] { '<', '>' }) >= 0 ||
                Regex.IsMatch(text, @"(?i)(?:https?://|www\.)")) throw InvalidCatalog();
            foreach (char c in text) if (Char.IsControl(c) || Char.GetUnicodeCategory(c) == UnicodeCategory.Format) throw InvalidCatalog();
            return text;
        }
        private static InvalidOperationException InvalidCatalog() { return new InvalidOperationException("This catalog has an unsupported format. No settings were changed."); }
        public void Dispose() { http.Dispose(); }
    }
}
