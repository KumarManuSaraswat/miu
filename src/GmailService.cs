using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Miu
{
    public sealed class MailMessage
    {
        public string Id = "";
        public string From = "";
        public string Subject = "";
        public string Snippet = "";
        public string Body = "";
        public DateTimeOffset ReceivedAt;
    }

    // Gmail is intentionally read-only. Message contents are returned as untrusted text.
    public sealed class GmailService : IDisposable
    {
        private const string Scope = "https://www.googleapis.com/auth/gmail.readonly";
        private const string ApiRoot = "https://gmail.googleapis.com/gmail/v1/users/me/";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";
        private const string CredentialStore = "gmail-client";
        private const string TokenStore = "gmail-token";
        private const int ResponseLimit = 4 * 1024 * 1024;
        private const int BodyLimit = 64000;
        private readonly HttpClient http;
        private readonly SemaphoreSlim authGate = new SemaphoreSlim(1, 1);
        private readonly object stateLock = new object();
        private Dictionary<string, object> credentials;
        private Dictionary<string, object> token;
        private string pendingAuthorizationUrl = "";
        private bool disposed;

        // This temporary authorization request contains state/PKCE challenge, never
        // an access token, refresh token, client secret, or PKCE verifier.
        public event EventHandler AuthorizationUrlChanged;
        public string PendingAuthorizationUrl
        {
            get { lock (stateLock) return pendingAuthorizationUrl; }
        }

        public GmailService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            http.Timeout = TimeSpan.FromSeconds(35);
            http.MaxResponseContentBufferSize = ResponseLimit;
            credentials = Load(CredentialStore);
            token = Load(TokenStore);
        }

        public bool HasCredentials
        {
            get { lock (stateLock) return !String.IsNullOrEmpty(Text(credentials, "client_id")); }
        }

        public bool IsConnected
        {
            get
            {
                lock (stateLock)
                    return HasCredentials && (!String.IsNullOrEmpty(Text(token, "refresh_token")) ||
                        (!String.IsNullOrEmpty(Text(token, "access_token")) && TokenIsFresh(token)));
            }
        }

        public void ImportCredentials(string filePath)
        {
            ThrowIfDisposed();
            if (!authGate.Wait(0)) throw new InvalidOperationException("Wait for the current Gmail connection to finish first.");
            try
            {
                FileInfo info = new FileInfo(filePath);
                if (!info.Exists || info.Length > 65536)
                    throw new InvalidOperationException("Choose the small OAuth Desktop app JSON file downloaded from Google Cloud.");
                Dictionary<string, object> root;
                try { root = Parse(File.ReadAllText(filePath, Encoding.UTF8)); }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
                        throw new InvalidOperationException("This file could not be read as Google OAuth credentials. Download a Desktop app JSON file from Google Cloud.");
                    throw;
                }
                var installed = Map(Value(root, "installed"));
                string clientId = Text(installed, "client_id");
                string secret = Text(installed, "client_secret");
                if (clientId.Length < 10 || clientId.Length > 512 ||
                    !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                    secret.Length < 1 || secret.Length > 512)
                    throw new InvalidOperationException("These are not Desktop app OAuth credentials. In Google Cloud, create an OAuth client with application type Desktop app, then download its JSON.");
                // Imported endpoints are never trusted. Only Google's fixed HTTPS endpoints are used.
                var next = new Dictionary<string, object> { { "client_id", clientId }, { "client_secret", secret } };
                lock (stateLock)
                {
                    bool changed = Text(credentials, "client_id") != clientId;
                    SecureStore.Write(CredentialStore, Stringify(next));
                    if (changed)
                    {
                        SecureStore.Delete(TokenStore);
                        token = new Dictionary<string, object>();
                    }
                    credentials = next;
                }
            }
            finally { authGate.Release(); }
        }

        public async Task<string> ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await authGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            TcpListener listener = null;
            try
            {
                var client = CopyCredentials();
                string verifier = RandomUrlString(48);
                string state = RandomUrlString(32);
                string challenge;
                using (var hash = SHA256.Create()) challenge = Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start(2);
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                string redirect = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
                string url = "https://accounts.google.com/o/oauth2/v2/auth?" + Form(new Dictionary<string, string>
                {
                    { "client_id", Text(client, "client_id") }, { "redirect_uri", redirect },
                    { "response_type", "code" }, { "scope", Scope }, { "state", state },
                    { "code_challenge", challenge }, { "code_challenge_method", "S256" },
                    { "access_type", "offline" }, { "prompt", "consent" }
                });
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(5));
                    string code;
                    try
                    {
                        SetPendingAuthorizationUrl(url);
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        code = await WaitForCodeAsync(listener, state, port, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested) throw;
                        throw new InvalidOperationException("Google sign-in timed out. Click Connect Gmail and finish signing in within five minutes.");
                    }
                    finally { SetPendingAuthorizationUrl(""); }
                    var form = new Dictionary<string, string>
                    {
                        { "client_id", Text(client, "client_id") }, { "client_secret", Text(client, "client_secret") },
                        { "code", code }, { "code_verifier", verifier }, { "redirect_uri", redirect },
                        { "grant_type", "authorization_code" }
                    };
                    var next = await RequestTokenAsync(form, cancellationToken).ConfigureAwait(false);
                    if (String.IsNullOrEmpty(Text(next, "refresh_token")))
                        throw new InvalidOperationException("Google did not grant offline Gmail access. Remove this app from your Google account connections, then reconnect and allow Gmail read access.");
                    SaveToken(next, "");
                    // A real API call confirms the Gmail API is enabled and the grant is usable.
                    var profile = await GetJsonWithTokenAsync(ApiRoot + "profile", Text(next, "access_token"), cancellationToken).ConfigureAwait(false);
                    return Text(profile, "emailAddress", "Your Gmail account");
                }
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("I couldn't reach Google. Check your internet connection and try connecting Gmail again.");
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                throw new InvalidOperationException("Google took too long to finish signing in. Please try again.");
            }
            finally
            {
                if (listener != null) listener.Stop();
                authGate.Release();
            }
        }

        private void SetPendingAuthorizationUrl(string url)
        {
            lock (stateLock)
            {
                if (pendingAuthorizationUrl == url) return;
                pendingAuthorizationUrl = url;
            }
            EventHandler changed = AuthorizationUrlChanged;
            if (changed != null) changed(this, EventArgs.Empty);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await authGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string revoke;
                lock (stateLock)
                {
                    revoke = Text(token, "refresh_token", Text(token, "access_token"));
                    SecureStore.Delete(TokenStore);
                    token = new Dictionary<string, object>();
                }
                if (String.IsNullOrEmpty(revoke)) return;
                try
                {
                    using (var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "token", revoke } }))
                    using (var response = await http.PostAsync("https://oauth2.googleapis.com/revoke", content, cancellationToken).ConfigureAwait(false))
                    {
                        // An already expired/revoked token does not need another revocation.
                        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
                            throw new HttpRequestException();
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is HttpRequestException) && !(ex is OperationCanceledException)) throw;
                    throw new InvalidOperationException("Gmail is disconnected on this device. Google could not confirm revocation; remove Miu from your Google Account > Security > Third-party connections when online.");
                }
            }
            finally { authGate.Release(); }
        }

        public async Task<List<MailMessage>> SearchAsync(string query, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            count = Math.Max(1, Math.Min(10, count));
            query = String.IsNullOrWhiteSpace(query) ? "in:inbox" : query.Trim();
            if (query.Length > 2048) throw new InvalidOperationException("That Gmail search is too long. Use a shorter sender, subject, or Gmail search expression.");
            string url = ApiRoot + "messages?maxResults=" + count.ToString(CultureInfo.InvariantCulture) + "&q=" + Uri.EscapeDataString(query);
            var listing = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
            var results = new List<MailMessage>();
            foreach (object entry in Items(Value(listing, "messages")))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string id = Text(Map(entry), "id");
                if (!ValidMessageId(id)) continue;
                var raw = await GetJsonAsync(ApiRoot + "messages/" + id + "?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Date", cancellationToken).ConfigureAwait(false);
                results.Add(ParseMessage(raw));
                if (results.Count >= count) break;
            }
            return results.OrderByDescending(m => m.ReceivedAt).ToList();
        }

        public async Task<MailMessage> ReadAsync(string id, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!ValidMessageId(id)) throw new InvalidOperationException("Choose an email from a recent Gmail search first.");
            var raw = await GetJsonAsync(ApiRoot + "messages/" + id + "?format=full", cancellationToken).ConfigureAwait(false);
            MailMessage message = ParseMessage(raw);
            var payload = Map(Value(raw, "payload"));
            string plain = await ExtractBodyAsync(payload, id, "text/plain", 0, cancellationToken).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(plain))
            {
                string html = await ExtractBodyAsync(payload, id, "text/html", 0, cancellationToken).ConfigureAwait(false);
                plain = HtmlToText(html);
            }
            message.Body = CleanText(String.IsNullOrWhiteSpace(plain) ? message.Snippet : plain, BodyLimit);
            return message;
        }

        private async Task<string> ExtractBodyAsync(Dictionary<string, object> part, string messageId, string wanted, int depth, CancellationToken cancellationToken)
        {
            if (part.Count == 0 || depth > 20 || !String.IsNullOrEmpty(Text(part, "filename"))) return "";
            string mime = Text(part, "mimeType");
            if (String.Equals(mime, wanted, StringComparison.OrdinalIgnoreCase))
            {
                var body = Map(Value(part, "body"));
                string encoded = Text(body, "data");
                if (String.IsNullOrEmpty(encoded))
                {
                    string attachment = Text(body, "attachmentId");
                    int size;
                    Int32.TryParse(Text(body, "size"), out size);
                    // Gmail can store the main text body as an attachment. Never fetch ordinary files.
                    if (attachment.Length > 0 && attachment.Length < 2048 && size <= 1024 * 1024)
                    {
                        var fetched = await GetJsonAsync(ApiRoot + "messages/" + messageId + "/attachments/" + Uri.EscapeDataString(attachment), cancellationToken).ConfigureAwait(false);
                        encoded = Text(fetched, "data");
                    }
                }
                return DecodeBody(encoded, FindHeader(part, "Content-Type"));
            }
            StringBuilder combined = new StringBuilder();
            int visited = 0;
            foreach (object child in Items(Value(part, "parts")))
            {
                if (++visited > 40 || combined.Length >= BodyLimit) break;
                string text = await ExtractBodyAsync(Map(child), messageId, wanted, depth + 1, cancellationToken).ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(text)) combined.AppendLine(text);
            }
            return Limit(combined.ToString(), BodyLimit);
        }

        private async Task<Dictionary<string, object>> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                string access = await AccessTokenAsync(false, cancellationToken).ConfigureAwait(false);
                try { return await GetJsonWithTokenAsync(url, access, cancellationToken).ConfigureAwait(false); }
                catch (UnauthorizedAccessException) { }
                access = await AccessTokenAsync(true, cancellationToken).ConfigureAwait(false);
                try { return await GetJsonWithTokenAsync(url, access, cancellationToken).ConfigureAwait(false); }
                catch (UnauthorizedAccessException)
                {
                    throw new InvalidOperationException("Google rejected the Gmail sign-in. Reconnect Gmail in Settings.");
                }
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("I couldn't reach Gmail. Check your internet connection and try again.");
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                throw new InvalidOperationException("Gmail took too long to respond. Please try again.");
            }
        }

        private async Task<Dictionary<string, object>> GetJsonWithTokenAsync(string url, string access, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(35));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
                    string body = await ReadLimitedAsync(response, timeout.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode == 429) throw new InvalidOperationException("Gmail's request limit was reached. Wait a minute before trying again.");
                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            if (body.IndexOf("accessNotConfigured", StringComparison.OrdinalIgnoreCase) >= 0 || body.IndexOf("SERVICE_DISABLED", StringComparison.OrdinalIgnoreCase) >= 0)
                                throw new InvalidOperationException("Enable the Gmail API in your Google Cloud project, wait a few minutes, then try again.");
                            if (body.IndexOf("rateLimit", StringComparison.OrdinalIgnoreCase) >= 0)
                                throw new InvalidOperationException("Gmail's request limit was reached. Wait a minute before trying again.");
                            throw new InvalidOperationException("Gmail read access was not granted or your organization blocks it. Reconnect Gmail and allow read access, or ask your account administrator.");
                        }
                        if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("That email is no longer available. Search Gmail again.");
                        throw new InvalidOperationException("Gmail returned an error (HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + "). Please try again later.");
                    }
                    return Parse(body);
                }
            }
        }

        private async Task<string> AccessTokenAsync(bool force, CancellationToken cancellationToken)
        {
            await authGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var client = CopyCredentials();
                string refresh;
                lock (stateLock)
                {
                    if (!force && TokenIsFresh(token)) return Text(token, "access_token");
                    refresh = Text(token, "refresh_token");
                }
                if (String.IsNullOrEmpty(refresh)) throw new InvalidOperationException("Connect Gmail in Settings first. Miu needs your permission to read your inbox.");
                var next = await RequestTokenAsync(new Dictionary<string, string>
                {
                    { "client_id", Text(client, "client_id") }, { "client_secret", Text(client, "client_secret") },
                    { "refresh_token", refresh }, { "grant_type", "refresh_token" }
                }, cancellationToken).ConfigureAwait(false);
                SaveToken(next, refresh);
                return Text(next, "access_token");
            }
            finally { authGate.Release(); }
        }

        private async Task<Dictionary<string, object>> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            using (var content = new FormUrlEncodedContent(form))
            using (var response = await http.PostAsync(TokenUrl, content, cancellationToken).ConfigureAwait(false))
            {
                string body = await ReadLimitedAsync(response, cancellationToken).ConfigureAwait(false);
                Dictionary<string, object> data;
                try { data = Parse(body); }
                catch (ArgumentException) { throw new InvalidOperationException("Google returned an unreadable sign-in response. Please reconnect Gmail."); }
                if (!response.IsSuccessStatusCode)
                {
                    string error = Text(data, "error");
                    if (error == "invalid_grant")
                    {
                        if (form.ContainsKey("refresh_token"))
                        {
                            lock (stateLock)
                            {
                                SecureStore.Delete(TokenStore);
                                token = new Dictionary<string, object>();
                            }
                        }
                        throw new InvalidOperationException("Your Google sign-in has expired or was revoked. Reconnect Gmail in Settings. Test-mode Google apps usually need a new sign-in after seven days.");
                    }
                    if (error == "invalid_client" || error == "unauthorized_client")
                        throw new InvalidOperationException("Google rejected the OAuth client. Import a fresh Desktop app credentials JSON from your Google Cloud project.");
                    throw new InvalidOperationException("Google couldn't finish Gmail sign-in. Check the Desktop app credentials and your Google Cloud test-user settings, then reconnect.");
                }
                if (String.IsNullOrEmpty(Text(data, "access_token"))) throw new InvalidOperationException("Google returned no access token. Please reconnect Gmail.");
                string granted = Text(data, "scope");
                if (granted.Length > 0 && !granted.Split(' ').Contains(Scope))
                    throw new InvalidOperationException("Gmail read permission was not granted. Reconnect Gmail and allow read access.");
                return data;
            }
        }

        private void SaveToken(Dictionary<string, object> next, string oldRefresh)
        {
            int expires;
            if (!Int32.TryParse(Text(next, "expires_in"), out expires)) expires = 3600;
            next["expires_utc"] = DateTime.UtcNow.AddSeconds(Math.Max(60, expires)).ToString("o", CultureInfo.InvariantCulture);
            if (String.IsNullOrEmpty(Text(next, "refresh_token"))) next["refresh_token"] = oldRefresh;
            lock (stateLock)
            {
                SecureStore.Write(TokenStore, Stringify(next));
                token = next;
            }
        }

        private Dictionary<string, object> CopyCredentials()
        {
            lock (stateLock)
            {
                if (String.IsNullOrEmpty(Text(credentials, "client_id")))
                    throw new InvalidOperationException("Import your Google Desktop app credentials JSON in Settings first. The Gmail setup guide explains the free one-time setup.");
                return new Dictionary<string, object>(credentials);
            }
        }

        private static bool TokenIsFresh(Dictionary<string, object> value)
        {
            DateTime expiry;
            return !String.IsNullOrEmpty(Text(value, "access_token")) &&
                DateTime.TryParse(Text(value, "expires_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out expiry) &&
                expiry.ToUniversalTime() > DateTime.UtcNow.AddSeconds(60);
        }

        private static async Task<string> WaitForCodeAsync(TcpListener listener, string state, int port, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(delegate { listener.Stop(); }))
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                    catch (SocketException) { cancellationToken.ThrowIfCancellationRequested(); throw; }
                    catch (ObjectDisposedException) { cancellationToken.ThrowIfCancellationRequested(); throw; }
                    using (client)
                    using (var stream = client.GetStream())
                    using (var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                        using (requestTimeout.Token.Register(delegate { client.Close(); }))
                        try
                        {
                            string headers = await ReadHeadersAsync(stream, requestTimeout.Token).ConfigureAwait(false);
                            string first = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                            string[] pieces = first.Split(' ');
                            Uri uri;
                            if (pieces.Length != 3 || pieces[0] != "GET" || !pieces[1].StartsWith("/", StringComparison.Ordinal) ||
                                !Uri.TryCreate("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + pieces[1], UriKind.Absolute, out uri) || uri.AbsolutePath != "/")
                            {
                                await ReplyAsync(stream, false, requestTimeout.Token).ConfigureAwait(false);
                                continue;
                            }
                            var query = ParseQuery(uri.Query);
                            string receivedState;
                            if (!query.TryGetValue("state", out receivedState) || !FixedEquals(state, receivedState))
                            {
                                await ReplyAsync(stream, false, requestTimeout.Token).ConfigureAwait(false);
                                continue;
                            }
                            string error;
                            if (query.TryGetValue("error", out error))
                            {
                                await ReplyAsync(stream, false, requestTimeout.Token).ConfigureAwait(false);
                                throw new InvalidOperationException(error == "access_denied" ? "Gmail sign-in was cancelled. Connect again whenever you're ready." : "Google could not authorize Gmail. Check your test-user and Desktop app settings, then reconnect.");
                            }
                            string code;
                            if (!query.TryGetValue("code", out code) || code.Length < 1 || code.Length > 4096)
                            {
                                await ReplyAsync(stream, false, requestTimeout.Token).ConfigureAwait(false);
                                continue;
                            }
                            await ReplyAsync(stream, true, requestTimeout.Token).ConfigureAwait(false);
                            return code;
                        }
                        catch (IOException) { cancellationToken.ThrowIfCancellationRequested(); }
                        catch (OperationCanceledException) { cancellationToken.ThrowIfCancellationRequested(); }
                        catch (ObjectDisposedException) { cancellationToken.ThrowIfCancellationRequested(); }
                    }
                }
            }
        }

        private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[2048];
            StringBuilder result = new StringBuilder();
            while (result.Length < 16384)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new IOException("Incomplete local callback.");
                result.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (result.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0) return result.ToString();
            }
            throw new IOException("Oversized local callback.");
        }

        private static async Task ReplyAsync(NetworkStream stream, bool success, CancellationToken cancellationToken)
        {
            string body = "<!doctype html><meta charset='utf-8'><meta name='referrer' content='no-referrer'><title>Miu Gmail connection</title><body style='font:18px system-ui;background:#111b20;color:#ddfff2;padding:60px'><h1>Miu</h1><p>" +
                (success ? "Sign-in received. Return to Miu while it finishes connecting. You may close this tab." : "This sign-in request could not be completed. Return to Miu and try connecting again.") + "</p></body>";
            byte[] data = Encoding.UTF8.GetBytes(body);
            string head = "HTTP/1.1 " + (success ? "200 OK" : "400 Bad Request") + "\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'\r\nConnection: close\r\nContent-Length: " + data.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(head);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength.GetValueOrDefault() > ResponseLimit)
                throw new InvalidOperationException("This Gmail response is too large for Miu's lightweight reader. Open the message in Gmail instead.");
            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (cancellationToken.Register(delegate { source.Dispose(); }))
            using (var memory = new MemoryStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        if (memory.Length + read > ResponseLimit) throw new InvalidOperationException("This email is too large for Miu's lightweight reader. Open it in Gmail instead.");
                        memory.Write(buffer, 0, read);
                    }
                    return Encoding.UTF8.GetString(memory.ToArray());
                }
                catch (IOException) { cancellationToken.ThrowIfCancellationRequested(); throw; }
                catch (ObjectDisposedException) { cancellationToken.ThrowIfCancellationRequested(); throw; }
            }
        }

        internal static MailMessage ParseMessage(Dictionary<string, object> raw)
        {
            var payload = Map(Value(raw, "payload"));
            var message = new MailMessage
            {
                Id = Text(raw, "id"), From = CleanText(DecodeHeader(FindHeader(payload, "From")), 512),
                Subject = CleanText(DecodeHeader(FindHeader(payload, "Subject")), 1024),
                Snippet = CleanText(WebUtility.HtmlDecode(Text(raw, "snippet")), 1200)
            };
            if (String.IsNullOrWhiteSpace(message.From)) message.From = "Unknown sender";
            if (String.IsNullOrWhiteSpace(message.Subject)) message.Subject = "(No subject)";
            long milliseconds;
            if (Int64.TryParse(Text(raw, "internalDate"), out milliseconds) && milliseconds >= 0 && milliseconds < 253402300799000L)
                message.ReceivedAt = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(milliseconds);
            else
            {
                DateTimeOffset parsed;
                if (DateTimeOffset.TryParse(FindHeader(payload, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)) message.ReceivedAt = parsed;
            }
            return message;
        }

        internal static string HtmlToText(string html)
        {
            if (String.IsNullOrWhiteSpace(html)) return "";
            html = Limit(html, BodyLimit * 3);
            try
            {
                var options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
                html = Regex.Replace(html, @"<(script|style|head)\b[^>]*>.*?</\1\s*>", " ", options, TimeSpan.FromMilliseconds(200));
                html = Regex.Replace(html, @"<!--.*?-->", " ", options, TimeSpan.FromMilliseconds(200));
                html = Regex.Replace(html, @"<(br\b[^>]*|/p|/div|/li|/tr|/h[1-6])\s*>", "\n", options, TimeSpan.FromMilliseconds(200));
                html = Regex.Replace(html, @"<[^>]*>", " ", options, TimeSpan.FromMilliseconds(200));
                return CleanText(WebUtility.HtmlDecode(html), BodyLimit);
            }
            catch (RegexMatchTimeoutException) { return "This message uses complex HTML. Open it in Gmail to read the full text."; }
        }

        internal static string DecodeBody(string encoded, string contentType)
        {
            if (String.IsNullOrEmpty(encoded)) return "";
            try
            {
                string normalized = encoded.Replace('-', '+').Replace('_', '/');
                normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
                byte[] bytes = Convert.FromBase64String(normalized);
                Encoding encoding = Encoding.UTF8;
                var charset = Regex.Match(contentType ?? "", "charset\\s*=\\s*[\"']?([^;\\s\"']+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                if (charset.Success)
                {
                    try { encoding = Encoding.GetEncoding(charset.Groups[1].Value); }
                    catch (ArgumentException) { }
                }
                return Limit(encoding.GetString(bytes), BodyLimit);
            }
            catch (FormatException) { return ""; }
        }

        private static string DecodeHeader(string header)
        {
            try
            {
                return Regex.Replace(header ?? "", @"=\?([^?]+)\?([bBqQ])\?([^?]*)\?=", delegate(Match match)
                {
                    try
                    {
                        Encoding encoding = Encoding.GetEncoding(match.Groups[1].Value);
                        string input = match.Groups[3].Value;
                        if (match.Groups[2].Value.Equals("B", StringComparison.OrdinalIgnoreCase)) return encoding.GetString(Convert.FromBase64String(input));
                        var bytes = new List<byte>();
                        for (int i = 0; i < input.Length; i++)
                        {
                            if (input[i] == '=' && i + 2 < input.Length)
                            {
                                byte parsed;
                                if (Byte.TryParse(input.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed)) { bytes.Add(parsed); i += 2; continue; }
                            }
                            bytes.Add((byte)(input[i] == '_' ? ' ' : input[i]));
                        }
                        return encoding.GetString(bytes.ToArray());
                    }
                    catch (ArgumentException) { return match.Value; }
                    catch (FormatException) { return match.Value; }
                }, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            }
            catch (RegexMatchTimeoutException) { return header; }
        }

        private static string FindHeader(Dictionary<string, object> payload, string name)
        {
            foreach (object item in Items(Value(payload, "headers")))
            {
                var header = Map(item);
                if (String.Equals(Text(header, "name"), name, StringComparison.OrdinalIgnoreCase)) return Text(header, "value");
            }
            return "";
        }

        private static string CleanText(string text, int length)
        {
            text = text ?? "";
            var output = new StringBuilder(Math.Min(text.Length, length));
            foreach (char character in text)
            {
                // Marketing preheaders use these invisible characters as padding.
                // Do not remove every Unicode Format character: ZWJ/ZWNJ and emoji
                // tag sequences carry meaning in ordinary written language.
                if (character == '\u034f' || character == '\u00ad' || character == '\u200b' ||
                    character == '\ufeff' || character == '\u2060' ||
                    (character >= '\u202a' && character <= '\u202e') ||
                    (character >= '\u2066' && character <= '\u2069')) continue;
                if (character != '\n' && character != '\t' && Char.IsControl(character)) continue;
                // Removing padding must not leave a long run of empty spoken text.
                char visible = character == '\u00a0' || character == '\u202f' ? ' ' : character;
                if ((visible == ' ' || visible == '\t') && output.Length > 0 &&
                    (output[output.Length - 1] == ' ' || output[output.Length - 1] == '\t')) continue;
                output.Append(visible);
                if (output.Length > length) break;
            }
            return Limit(output.ToString(), length).Trim();
        }

        private static bool ValidMessageId(string id)
        {
            return !String.IsNullOrEmpty(id) && id.Length <= 128 && id.All(c => Char.IsLetterOrDigit(c) || c == '_' || c == '-');
        }

        private static string Limit(string value, int length) { return value.Length <= length ? value : value.Substring(0, length) + "\n[Message shortened by Miu]"; }
        private static Dictionary<string, object> Parse(string value) { return Map(new JavaScriptSerializer { MaxJsonLength = ResponseLimit, RecursionLimit = 64 }.DeserializeObject(value)); }
        private static string Stringify(object value) { return new JavaScriptSerializer().Serialize(value); }
        private static object Value(Dictionary<string, object> map, string key) { object value; return map != null && map.TryGetValue(key, out value) ? value : null; }
        private static string Text(Dictionary<string, object> map, string key, string fallback = "") { object value = Value(map, key); return value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        private static IEnumerable<object> Items(object value) { var items = value as IEnumerable; return items == null || value is string ? Enumerable.Empty<object>() : items.Cast<object>(); }
        private static Dictionary<string, object> Load(string name)
        {
            try { string value = SecureStore.Read(name); return String.IsNullOrWhiteSpace(value) ? new Dictionary<string, object>() : Parse(value); }
            catch (CryptographicException) { return new Dictionary<string, object>(); }
            catch (ArgumentException) { return new Dictionary<string, object>(); }
            catch (IOException) { return new Dictionary<string, object>(); }
            catch (InvalidOperationException) { return new Dictionary<string, object>(); }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                int equal = part.IndexOf('=');
                if (equal < 0) continue;
                string key = Uri.UnescapeDataString(part.Substring(0, equal).Replace('+', ' '));
                string value = Uri.UnescapeDataString(part.Substring(equal + 1).Replace('+', ' '));
                if (result.ContainsKey(key)) return new Dictionary<string, string>();
                result[key] = value;
            }
            return result;
        }

        private static string Form(Dictionary<string, string> values) { return String.Join("&", values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))); }
        private static string RandomUrlString(int count) { byte[] bytes = new byte[count]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes); return Base64Url(bytes); }
        private static string Base64Url(byte[] bytes) { return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        private static bool FixedEquals(string expected, string actual)
        {
            if (actual == null || expected.Length != actual.Length) return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            return difference == 0;
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException("GmailService"); }
        public void Dispose() { disposed = true; http.Dispose(); }
    }
}
