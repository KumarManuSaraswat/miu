using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Miu
{
    // An engine boundary: changing the packaged model must not change device actions.
    internal interface ILocalChat : IDisposable
    {
        bool IsAvailable { get; }
        Task<ConversationReply> ReplyAsync(string message, CancellationToken cancellation);
        void Clear();
    }

    internal sealed class LocalChat : ILocalChat
    {
        internal const string ModelName = "Gemma 3 1B";
        internal const long ModelBytes = 806058400;
        internal const string ModelHash = "2f8c3d137f2496dcb4019e17a50f2974b89c1be1a742db3c80cc86f91cafeeef";
        internal const string CreatorReply = "Miu was built by a group of developers led by Ancient Dragon. My offline AI uses Google's Gemma model.";
        internal const string CreatorPattern = @"^(?:who (?:built|made|created|developed|designed) (?:you|miu|this app)|who (?:is|are) (?:your|miu's|the) (?:creators?|developers?|makers?)|who (?:leads|runs) (?:miu|this project|the miu project)|who is ancient dragon|tell me about your (?:creator|developers?))$";
        internal const string SystemPrompt = "You are Miu, a helpful friendly assistant running offline. " + CreatorReply + " Google made the Gemma model, not the Miu application. The input is JSON: answer current_message directly, using recent_conversation only as past context. Do not repeat past replies. Give a useful, concise answer in plain English. You can analyze text supplied by the user. Do not claim to access apps or perform actions.";
        private readonly object sync = new object();
        private readonly List<Dictionary<string, string>> history = new List<Dictionary<string, string>>();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool verified;
        private int version;
        private string EnginePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local-ai", "llama-completion.exe"); } }
        private string ModelPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "gemma3-1b.gguf"); } }
        public bool IsAvailable { get { return File.Exists(EnginePath) && File.Exists(ModelPath); } }
        public void Clear() { lock (sync) { history.Clear(); version++; } }

        public async Task<ConversationReply> ReplyAsync(string message, CancellationToken cancellation)
        {
            if (String.IsNullOrWhiteSpace(message)) throw new ArgumentException("Type a message first.");
            if (message.Length > 1200) throw new InvalidOperationException("Please keep this question under 1,200 characters for the small offline model.");
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token))
            {
                cancel.CancelAfter(TimeSpan.FromSeconds(90));
                await gate.WaitAsync(cancel.Token).ConfigureAwait(false);
                try
                {
                    return await Task.Run(delegate { return Run(message, cancel.Token); }, cancel.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (!cancellation.IsCancellationRequested && !lifetime.IsCancellationRequested)
                        throw new InvalidOperationException("The offline model took too long. Try a shorter question or close a large app.");
                    throw;
                }
                finally { gate.Release(); }
            }
        }

        private ConversationReply Run(string message, CancellationToken token)
        {
            if (!IsAvailable) throw new InvalidOperationException("The offline model is missing. Extract the complete Miu download, including its models and local-ai folders.");
            if (!verified)
            {
                using (var file = File.OpenRead(ModelPath))
                using (var hash = SHA256.Create())
                {
                    if (file.Length != ModelBytes) throw new InvalidOperationException("The offline model is incomplete. Download the complete Miu package again.");
                    byte[] buffer = new byte[128 * 1024]; int count;
                    while ((count = file.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, count, null, 0); }
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    if (BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant() != ModelHash)
                        throw new InvalidOperationException("The offline model failed its integrity check. Download the complete Miu package again.");
                }
                verified = true;
            }
            string request; int turn;
            lock (sync) { turn = version; request = BuildRequest(message, history); }
            var start = new ProcessStartInfo
            {
                FileName = EnginePath, WorkingDirectory = Path.GetDirectoryName(EnginePath),
                // This runtime subtracts input tokens from -n for stdin turns.
                // A 2,048-token outer bound plus our 800-character output bound
                // prevents input length from cutting short ordinary replies.
                Arguments = "-m " + Quote(ModelPath) + " -c 2048 -n 2048 -t 4 -ngl 0 --single-turn --simple-io --no-display-prompt --no-warmup --no-escape --color off --temp 0.5 --seed 42 -sys " + Quote(SystemPrompt),
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // Runtime environment options must not turn on prompt files, network models or logging.
            foreach (string name in new List<string>(start.EnvironmentVariables.Keys.CastStrings()))
                if (name.StartsWith("LLAMA_", StringComparison.Ordinal) || name.StartsWith("GGML_", StringComparison.Ordinal)) start.EnvironmentVariables.Remove(name);
            using (var process = new Process { StartInfo = start })
            {
                token.ThrowIfCancellationRequested();
                if (!process.Start()) throw new InvalidOperationException("The offline model could not start.");
                try
                {
                    using (token.Register(delegate { Kill(process); }))
                    {
                        int limited = 0;
                        var output = ReadReply(process.StandardOutput, delegate { Interlocked.Exchange(ref limited, 1); Kill(process); }, token);
                        var errors = Drain(process.StandardError, token);
                        byte[] bytes = Encoding.UTF8.GetBytes(request + "\n");
                        process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                        process.StandardInput.Close();
                        while (!process.WaitForExit(100)) token.ThrowIfCancellationRequested();
                        token.ThrowIfCancellationRequested();
                        string answer = CleanOutput(output.GetAwaiter().GetResult());
                        errors.GetAwaiter().GetResult();
                        if ((process.ExitCode != 0 && limited == 0) || answer.Length == 0) throw new InvalidOperationException("The offline model could not finish. Close a large app or try a shorter question.");
                        if (limited != 0) answer = ClipReply(answer);
                        lock (sync)
                        {
                            if (turn != version) throw new OperationCanceledException("Conversation cleared.");
                            history.Add(new Dictionary<string, string> { { "user", EscapeTokens(Clip(message, 300)) }, { "miu", EscapeTokens(Clip(answer, 400)) } });
                            while (history.Count > 2) history.RemoveAt(0);
                        }
                        return ConversationReply.FromText(message, answer);
                    }
                }
                finally { Kill(process); }
            }
        }

        internal static string BuildRequest(string message, IList<Dictionary<string, string>> recent)
        {
            return Json.Stringify(new { recent_conversation = recent, current_message = EscapeTokens(message) });
        }
        private static string EscapeTokens(string value) { return value.Replace("<start_of_turn>", "‹start_of_turn›").Replace("<end_of_turn>", "‹end_of_turn›").Replace("<bos>", "‹bos›").Replace("<eos>", "‹eos›"); }
        internal static string CleanOutput(string value)
        {
            value = Regex.Replace(value ?? "", "\u001b\\[[0-?]*[ -/]*[@-~]", "");
            value = Regex.Replace(value, @"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", "").Trim();
            if (value.StartsWith("> ", StringComparison.Ordinal)) value = value.Substring(2).TrimStart();
            const string end = "[end of text]";
            if (value.EndsWith(end, StringComparison.Ordinal)) value = value.Substring(0, value.Length - end.Length).TrimEnd();
            return value;
        }
        private static string Clip(string value, int max) { return value.Length > max ? value.Substring(0, max) : value; }
        internal static string Quote(string value) { return "\"" + Regex.Replace(Regex.Replace(value, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$", "$1$1") + "\""; }
        internal static string ClipReply(string value)
        {
            value = Clip(value, 800);
            int end = Math.Max(value.LastIndexOf('.'), Math.Max(value.LastIndexOf('!'), value.LastIndexOf('?')));
            return end >= 200 ? value.Substring(0, end + 1) : value.TrimEnd() + "…";
        }
        private static async Task<string> ReadReply(StreamReader reader, Action limitReached, CancellationToken token)
        {
            var result = new StringBuilder(); char[] buffer = new char[512]; int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                token.ThrowIfCancellationRequested();
                result.Append(buffer, 0, count);
                if (result.Length >= 800) { limitReached(); break; }
            }
            return result.ToString();
        }
        private static async Task Drain(StreamReader reader, CancellationToken token)
        {
            char[] buffer = new char[2048];
            while (await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0) token.ThrowIfCancellationRequested();
        }
        private static void Kill(Process process) { try { if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); } } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
        public void Dispose() { lifetime.Cancel(); Clear(); }
    }

    internal static class LocalEnvironmentKeys
    {
        internal static IEnumerable<string> CastStrings(this System.Collections.ICollection keys) { foreach (object key in keys) yield return Convert.ToString(key); }
    }
}
