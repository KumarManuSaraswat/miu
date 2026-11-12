using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Miu
{
    public static class CommunityCatalogTests
    {
        public static string LastReport { get; private set; }
        private const string Preset = "{\"id\":\"focus\",\"name\":\"Focus\",\"description\":\"A calm companion.\",\"mood\":\"content\",\"speechRate\":-1,\"reducedMotion\":true}";
        private static string Envelope(string rows) { return "{\"schemaVersion\":1,\"presets\":[" + rows + "]}"; }
        public static int Run()
        {
            var failures = new List<string>(); int count = 0;
            Action<bool, string> check = delegate(bool ok, string name) { count++; if (!ok) failures.Add(name); };
            Action<string, string> reject = delegate(string body, string name) {
                bool rejected = false; try { CommunityCatalog.Parse(body); } catch (InvalidOperationException) { rejected = true; }
                check(rejected, name);
            };
            Uri uri;
            check(CommunityCatalog.TryEndpoint("https://catalog.example/v1/presets", out uri), "Valid public HTTPS catalog");
            foreach (string bad in new[] { "", "http://catalog.example/v1/presets", "https://user:password@catalog.example/v1/presets", "https://catalog.example/v1/presets?key=secret", "https://catalog.example/v1/presets#secret", "https://127.0.0.1/v1/presets", "https://localhost/v1/presets", "https://catalog.example:444/v1/presets", "postgresql://user:password@db.example/db", "https://catalog.example/other" })
                check(!CommunityCatalog.TryEndpoint(bad, out uri), "Reject unsafe endpoint: " + bad.Split('?')[0]);
            var valid = CommunityCatalog.Parse(Envelope(Preset));
            check(valid.Length == 1 && valid[0].Mood == "content" && valid[0].SpeechRate == -1 && valid[0].ReducedMotion, "Valid preset is decoded");
            check(CommunityCatalog.Parse(Envelope("")).Length == 0, "Empty catalog is valid");
            reject(Envelope(Preset).Replace("schemaVersion\":1", "schemaVersion\":2"), "Unknown schema");
            reject(Envelope(Preset).Replace("schemaVersion\":1", "schemaVersion\":\"1\""), "Schema must be integer");
            reject(Envelope(Preset.Replace("content", "execute")), "Unknown mood");
            reject(Envelope(Preset.Replace("\"speechRate\":-1", "\"speechRate\":10")), "Unsafe rate");
            reject(Envelope(Preset.Replace("\"speechRate\":-1", "\"speechRate\":1.5")), "Rate must be integer");
            reject(Envelope(Preset.Replace("\"reducedMotion\":true", "\"reducedMotion\":\"true\"")), "Motion must be boolean");
            reject(Envelope(Preset.Replace("\"id\":\"focus\"", "\"id\":\"../focus\"")), "Invalid ID");
            reject(Envelope(Preset + "," + Preset), "Duplicate IDs");
            reject(Envelope(Preset.Replace("A calm companion.", "<script>")), "No HTML");
            reject(Envelope(Preset.Replace("A calm companion.", "https://example.com")), "No remote links");
            reject(Envelope(Preset.Replace("Focus", "\\u202eFocus")), "No display direction override");
            reject(Envelope(Preset.Replace("Focus", "\\nFocus")), "No control characters");
            reject(Envelope(Preset.Replace("Focus", new String('x', 81))), "Bounded name");
            reject(Envelope(Preset.Replace("A calm companion.", new String('x', 241))), "Bounded description");
            reject(Envelope(Preset.Replace("\"mood\"", "\"action\"")), "Cannot supply executable actions");
            reject(Envelope(Preset.Replace("\"reducedMotion\":true", "\"reducedMotion\":true,\"cloud\":true")), "Cannot change cloud preferences");
            reject(Envelope(Preset).Replace("\"presets\"", "\"actions\""), "Unknown envelope fields");
            reject(new String('x', 65537), "Response size limit");
            reject("{invalid", "Malformed JSON");
            using (var handler = new FakeHandler(Envelope(Preset), HttpStatusCode.OK, "application/json"))
            using (var client = new CommunityCatalog(handler))
            {
                var fetched = client.FetchAsync("https://catalog.example/v1/presets", CancellationToken.None).GetAwaiter().GetResult();
                check(fetched.Length == 1 && handler.SafeRequest, "Fetch sends only a public GET with no body or authorization");
            }
            foreach (var response in new[] { new FakeHandler(Envelope(Preset), HttpStatusCode.Found, "application/json"), new FakeHandler("private error", HttpStatusCode.InternalServerError, "application/json"), new FakeHandler(Envelope(Preset), HttpStatusCode.OK, "text/html"), new FakeHandler(new String('x', 65537), HttpStatusCode.OK, "application/json") })
            using (var client = new CommunityCatalog(response))
            {
                bool rejected = false;
                try { client.FetchAsync("https://catalog.example/v1/presets", CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
                check(rejected, "Reject redirects, server errors, wrong content type, and oversized HTTP data");
            }
            LastReport = failures.Count == 0 ? "PASS: " + count + " offline community catalog checks." : "FAIL: " + String.Join("; ", failures);
            Console.WriteLine(LastReport); return failures.Count == 0 ? 0 : 1;
        }
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly string body, media; private readonly HttpStatusCode status;
            public bool SafeRequest;
            public FakeHandler(string body, HttpStatusCode status, string media) { this.body = body; this.status = status; this.media = media; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                SafeRequest = request.Method == HttpMethod.Get && request.Content == null && request.Headers.Authorization == null && request.RequestUri.Query.Length == 0;
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, media) });
            }
        }
    }
}
