using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Miu
{
    public sealed class AssistantReply
    {
        public string Text;
        public string Mood;
        public string Action;
        public bool IsEmail;

        public AssistantReply(string text, string mood = "happy", string action = null, bool isEmail = false)
        { Text = text; Mood = mood; Action = action; IsEmail = isEmail; }
    }

    public sealed class CommandIntent
    {
        public string Kind;
        public string Argument;
        public int Index;
        public int Count;
        public CommandIntent(string kind, string argument = "", int index = 0, int count = 5)
        { Kind = kind; Argument = argument; Index = index; Count = count; }
    }

    public sealed class AssistantEngine : IDisposable
    {
        private const int ErrorResponseLimit = 32 * 1024;
        private readonly Func<bool> gmailConnected;
        private readonly Func<string, int, CancellationToken, Task<List<MailMessage>>> searchMail;
        private readonly Func<string, CancellationToken, Task<MailMessage>> readMail;
        private readonly AppSettings settings;
        private readonly ILocalChat localChat;
        private readonly HttpClient http;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly List<Dictionary<string, object>> conversation = new List<Dictionary<string, object>>();
        private List<MailMessage> lastEmails = new List<MailMessage>();
        private bool emailContext;
        private bool disposed;
        private int conversationVersion;

        public AssistantEngine(GmailService gmail, AppSettings settings)
        {
            if (gmail == null) throw new ArgumentNullException("gmail");
            if (settings == null) throw new ArgumentNullException("settings");
            gmailConnected = delegate { return gmail.IsConnected; };
            searchMail = gmail.SearchAsync;
            readMail = gmail.ReadAsync;
            this.settings = settings;
            localChat = new LocalChat();
            http = CreateChatClient();
        }

        // Offline tests can control completion order without opening a mailbox or profile.
        internal AssistantEngine(AppSettings settings, Func<bool> gmailConnected,
            Func<string, int, CancellationToken, Task<List<MailMessage>>> searchMail,
            Func<string, CancellationToken, Task<MailMessage>> readMail, ILocalChat localChat = null)
        {
            if (settings == null || gmailConnected == null || searchMail == null || readMail == null)
                throw new ArgumentNullException("Mail operation dependencies");
            this.settings = settings; this.gmailConnected = gmailConnected;
            this.localChat = localChat;
            this.searchMail = searchMail; this.readMail = readMail;
            http = CreateChatClient();
        }

        private static HttpClient CreateChatClient()
        {
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 256 * 1024 };
        }

        public void ClearConversation()
        {
            if (localChat != null) localChat.Clear();
            lock (conversation)
            {
                conversation.Clear(); conversationVersion++;
                lastEmails = new List<MailMessage>(); emailContext = false;
            }
        }

        // Parsing has no side effects. Only the explicit resulting intent can
        // invoke a device action; model output is never parsed as a command.
        public static CommandIntent ParseCommand(string input)
        {
            string original = (input ?? "").Trim();
            if (original.Length == 0) return new CommandIntent("empty");
            if (original.Length > 12000) return new CommandIntent("too_long");
            string text = Regex.Replace(original.Replace('\u2019', '\''), @"\s+", " ");
            // Strip only explicit leading address/politeness/filler words.
            // Never remove words inside an argument, negation, or quoted request.
            // Multiple passes handle "um, hey Miu, could you please ...".
            for (int prefix = 0; prefix < 6; prefix++)
            {
                string next = Regex.Replace(text,
                    @"^(?:(?:(?:hey|hi|hello|okay|ok)[, ]+)?miu[,! ]+|(?:uh|um|erm|er|ah|okay|ok|hey)[, ]+|(?:please|can you|could you|would you)[, ]+)",
                    "", RegexOptions.IgnoreCase);
                if (next == text) break;
                text = next;
            }
            text = Regex.Replace(text, @"[, ]+please[.!?]*$", "", RegexOptions.IgnoreCase).Trim();
            string command = text.TrimEnd('.', '?', '!').Trim();
            string lower = command.ToLowerInvariant();
            Match match;

            if (In(lower, "help", "what can you do", "show commands", "commands", "show help")) return new CommandIntent("help");
            if (In(lower, "show reactions", "show my reactions", "show your reactions", "show me your reactions", "show faces", "show all faces", "show all reactions", "show expressions", "show all expressions", "open reactions", "reactions", "expressions")) return new CommandIntent("reactions");
            match = Regex.Match(lower, @"^(?:show(?: me)? (?:your |a |an |the )?(?<mood>.+?) (?:face|expression|reaction)|look (?<mood>.+)|make (?:a |an )?(?<mood>.+?) (?:face|expression|reaction))$");
            if (match.Success)
            {
                string expression;
                if (ExpressionCatalog.TryResolve(match.Groups["mood"].Value, out expression)) return new CommandIntent("face", expression);
                // A general "look at..." request can still be an email question.
                if (!lower.StartsWith("look ", StringComparison.Ordinal)) return new CommandIntent("face_help");
            }
            if (In(lower, "settings", "open settings", "miu settings", "open miu settings", "preferences")) return new CommandIntent("settings");
            if (In(lower, "connect gmail", "connect my gmail", "sign in to gmail", "set up gmail", "connect email")) return new CommandIntent("connect_gmail");
            if (In(lower, "sleep", "go to sleep", "take a nap", "good night", "hide", "hide panel")) return new CommandIntent("sleep");
            if (In(lower, "wake", "wake up", "good morning")) return new CommandIntent("wake");
            if (In(lower, "mute", "mute yourself", "stop talking", "be quiet", "voice off")) return new CommandIntent("mute");
            if (In(lower, "unmute", "unmute yourself", "talk to me", "voice on")) return new CommandIntent("unmute");
            if (In(lower, "clear chat", "clear conversation", "forget our conversation", "forget conversation")) return new CommandIntent("clear");

            // Offline recognition can render a hesitation as "a". Tolerate that
            // only for these complete, read-only clock requests, not device actions.
            string clock = lower.StartsWith("a ", StringComparison.Ordinal) ? lower.Substring(2) : lower;
            if (In(clock, "what time is it", "what time is it now", "what time it is", "what's the time", "what is the time",
                "what's the time now", "what is the time now", "tell me the time", "tell me what time it is",
                "tell me the current time", "time", "current time")) return new CommandIntent("time");
            if (In(lower, "what is today's date", "what's today's date", "what day is it", "what is the date", "what's the date", "date", "today's date")) return new CommandIntent("date");
            if (In(lower, "battery", "battery status", "check battery", "check my battery", "how much battery is left", "what's my battery", "what is my battery level")) return new CommandIntent("battery");

            if (In(lower, "volume up", "turn up the volume", "increase volume", "turn the volume up")) return new CommandIntent("volume", "up");
            if (In(lower, "volume down", "turn down the volume", "decrease volume", "turn the volume down")) return new CommandIntent("volume", "down");
            if (In(lower, "toggle system mute", "toggle sound")) return new CommandIntent("volume", "toggle_mute");
            if (In(lower, "mute computer", "mute system", "unmute computer", "unmute system")) return new CommandIntent("volume_help");

            match = Regex.Match(text, @"^(?:take (?:a )?note|make (?:a )?note|save (?:a )?note|note)(?:\s*:\s*|\s+)(.+)$", RegexOptions.IgnoreCase);
            if (match.Success) return new CommandIntent("note", match.Groups[1].Value);
            if (In(lower, "take a note", "save a note", "make a note", "note")) return new CommandIntent("note");

            match = Regex.Match(command, @"^(?:search (?:the )?web for|search (?:the )?internet for|google|search google for)\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success) return new CommandIntent("web", match.Groups[1].Value);

            match = Regex.Match(lower, @"^(?:read|show|open) (?:email|mail|message)(?: number)? (\d{1,3})$");
            if (match.Success) return new CommandIntent("email_read", "", Int32.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            if (In(lower, "read it", "read that email", "read the email")) return new CommandIntent("email_read", "", 1);
            if (In(lower, "read the latest email", "read my latest email", "read latest email", "read latest", "read my last email", "read the last email"))
                return new CommandIntent("email_read_latest", "", 1);

            // An exact allowlist prevents command chaining or user-selected executables.
            match = Regex.Match(lower, @"^(?:open|launch|start) (?:the |my )?(.+)$");
            if (match.Success)
            {
                string app = match.Groups[1].Value;
                if (In(app, "calculator", "calc")) return new CommandIntent("open", "calculator");
                if (In(app, "notepad", "text editor")) return new CommandIntent("open", "notepad");
                if (In(app, "file explorer", "explorer", "files")) return new CommandIntent("open", "explorer");
                if (In(app, "downloads", "downloads folder")) return new CommandIntent("open", "downloads");
                if (In(app, "documents", "documents folder")) return new CommandIntent("open", "documents");
                if (In(app, "browser", "google")) return new CommandIntent("open", "browser");
                if (In(app, "gmail", "gmail inbox", "email", "inbox")) return new CommandIntent("open", "gmail");
                if (In(app, "windows settings", "system settings")) return new CommandIntent("open", "windows_settings");
                if (In(app, "notes", "notes folder", "miu notes")) return new CommandIntent("open", "notes");
                return new CommandIntent("unsupported_action");
            }

            if (Regex.IsMatch(lower, @"^(?:delete|remove|erase|format|shut ?down|restart|reboot|send|forward|reply to|buy|purchase|pay|run|execute|install|uninstall|download)\b"))
                return new CommandIntent("unsupported_action");

            // Explicit Gmail search keeps raw Gmail operators. Natural sender /
            // topic phrases below quote the search term instead of executing it.
            match = Regex.Match(command, @"^(?:search|find) (?:my )?gmail(?: for)?\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success) return new CommandIntent("email_search", match.Groups[1].Value);

            match = Regex.Match(command, @"^(?:what|which) (?:emails?|mails?|messages?) (?:did i (?:get|receive)|have i (?:got|received)) (from|about|regarding|containing) (.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string query = match.Groups[1].Value.Equals("from", StringComparison.OrdinalIgnoreCase)
                    ? "from:" + QuoteGmailTerm(match.Groups[2].Value.Trim()) : QuoteGmailTerm(match.Groups[2].Value.Trim());
                return new CommandIntent("email_search", query);
            }

            match = Regex.Match(command, @"^(?:(?:show(?: me)?|find|check|read|tell me about|did i get|have i (?:got|received)|do i have|what(?:'s| is)) )?(?:(?:my|the|any|an?|some) )?((?:(?:unread|latest|last|newest|recent) )?)(?:emails?|mails?|messages?|gmail)(?: i (?:got|received))? (from|about|regarding|containing) (.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string qualifier = match.Groups[1].Value.Trim().ToLowerInvariant();
                string term = match.Groups[3].Value.Trim();
                string query = match.Groups[2].Value.Equals("from", StringComparison.OrdinalIgnoreCase)
                    ? "from:" + QuoteGmailTerm(term) : QuoteGmailTerm(term);
                if (qualifier == "unread") query = "is:unread " + query;
                return new CommandIntent("email_search", query, 0, In(qualifier, "latest", "last", "newest") ? 1 : 5);
            }

            match = Regex.Match(command, @"^(?:search|find) (?:my )?(?:gmail|emails?|mail)(?: for)?\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success) return new CommandIntent("email_search", match.Groups[1].Value);

            if (Regex.IsMatch(lower, @"\b(?:emails?|gmail|inbox|mails?)\b"))
            {
                if (Regex.IsMatch(lower, @"\bunread\b")) return new CommandIntent("email_search", "in:inbox is:unread");
                if (Regex.IsMatch(lower, @"\b(?:latest|last|newest|recent|new)\b")) return new CommandIntent("email_search", "in:inbox", 0, 1);
                if (In(lower, "email", "emails", "mail", "gmail", "inbox", "check email", "check emails", "check gmail", "check my email", "check my emails", "check my gmail", "check my inbox", "read email", "read my email", "read emails", "read my emails", "show my email", "show my emails", "show inbox", "show my inbox", "show me my emails", "show me my inbox", "do i have any email", "do i have any emails"))
                    return new CommandIntent("email_search", "in:inbox");
                return new CommandIntent("email_help");
            }

            if (In(lower, "hi", "hello", "hey", "hi miu", "hello miu", "hey miu", "miu")) return new CommandIntent("greeting");
            if (In(lower, "how are you", "how are you doing", "how's it going")) return new CommandIntent("feeling");
            if (In(lower, "thank you", "thanks", "thanks miu", "thank you miu", "you're the best", "you are the best", "good job")) return new CommandIntent("thanks");
            if (In(lower, "i love you", "love you", "you are cute", "you're cute")) return new CommandIntent("love");
            if (In(lower, "blow me a kiss", "blow a kiss", "kisses", "mwah")) return new CommandIntent("kiss");
            if (In(lower, "smile", "give me a smile", "cheer up", "be happy")) return new CommandIntent("smile");
            if (Regex.IsMatch(lower, @"^(?:i am|i'm|i feel|i am feeling|i'm feeling) (?:really |very |so |a little )?(?:sad|lonely|upset|down|stressed|anxious)(?: today)?$")) return new CommandIntent("comfort");
            if (Regex.IsMatch(lower, @"^(?:i am|i'm|i feel|i am feeling|i'm feeling) (?:really |very |so |a little )?(?:angry|annoyed|frustrated)(?: today)?$")) return new CommandIntent("frustrated");
            if (Regex.IsMatch(lower, @"^(?:i am|i'm|i feel|i am feeling|i'm feeling) (?:really |very |so |a little )?(?:happy|excited|delighted|proud)(?: today)?$")) return new CommandIntent("celebrate");
            if (Regex.IsMatch(lower, @"^(?:i am|i'm|i feel) (?:really |very |so |a little )?(?:tired|sleepy|exhausted)(?: today)?$")) return new CommandIntent("tired");
            if (In(lower, "wow", "that's amazing", "that is amazing", "that's incredible", "that is incredible")) return new CommandIntent("amazed");
            if (In(lower, "tell me a joke", "make me laugh", "tell a joke")) return new CommandIntent("joke");
            if (In(lower, "who are you", "what are you", "what's your name", "what is your name")) return new CommandIntent("identity");
            if (Regex.IsMatch(lower, LocalChat.CreatorPattern)) return new CommandIntent("creator");
            return new CommandIntent("chat", command);
        }

        public async Task<AssistantReply> HandleAsync(string text, CancellationToken ct)
        {
            if (disposed) throw new ObjectDisposedException("AssistantEngine");
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try { return await HandleCoreAsync(text, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is UnauthorizedAccessException)
                    return new AssistantReply("Windows couldn't complete that action. Check that the app or folder is available, then try again.", "sad");
                return new AssistantReply("I couldn't finish that request. Please try again. You can open Settings to check your connections.", "sad");
            }
            finally { gate.Release(); }
        }

        private async Task<AssistantReply> HandleCoreAsync(string text, CancellationToken ct)
        {
            CommandIntent intent = ParseCommand(text);
            ct.ThrowIfCancellationRequested();
            if (intent.Kind.StartsWith("email_", StringComparison.Ordinal))
            {
                int version;
                lock (conversation) { emailContext = true; version = conversationVersion; }
                if (intent.Kind == "email_help")
                    return new AssistantReply("Try 'latest email', 'unread emails', 'emails from Alex', or 'search Gmail subject:invoice'. I read email locally and don't send it to the AI chat service.", "curious", null, true);
                if (!gmailConnected())
                    return new AssistantReply("Connect Gmail in Settings first. I need your permission to read your inbox. After that, I can check the latest email or find a message for you.", "curious", "connect_gmail", true);
                try
                {
                    if (intent.Kind == "email_read_latest")
                    {
                        await SearchAndRememberEmailAsync("in:inbox", 1, version, ct).ConfigureAwait(false);
                        return await ReadEmailAsync(1, version, ct).ConfigureAwait(false);
                    }
                    if (intent.Kind == "email_read") return await ReadEmailAsync(intent.Index, version, ct).ConfigureAwait(false);
                    return await SearchEmailAsync(intent.Argument, intent.Count, version, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return new AssistantReply("I couldn't read Gmail. " + SafeEmailError(ex.Message), "sad", null, true);
                }
            }

            switch (intent.Kind)
            {
                case "empty": return new AssistantReply("I'm here. What would you like to do?", "curious");
                case "too_long": return new AssistantReply("Please keep requests under 12,000 characters.", "curious");
                case "help": return new AssistantReply("Try: 'latest email', 'emails from Alex', 'read email 1', 'open calculator', 'take a note buy milk', 'volume up', or 'what time is it'. Say 'show reactions' to see all 24 faces, or 'look playful' to try one. Click Talk or press Ctrl+Alt+Space to speak. Optional AI chat is in Settings.", "excited");
                case "reactions": return new AssistantReply("Here are all 24 reactions. Pick a face to preview it, or ask me to 'look playful', 'show a wide-eyed face', or 'make a cheerful face'.", "delighted", "reactions");
                case "face": return new AssistantReply(ExpressionCatalog.Label(intent.Argument) + " face, coming right up.", intent.Argument);
                case "face_help": return new AssistantReply("I don't have that expression yet. Try 'look playful' or 'show reactions' to explore all 24 faces.", "curious");
                case "settings": return new AssistantReply("Let's get everything set up.", "happy", "settings");
                case "connect_gmail": return new AssistantReply("Open the Gmail section in Settings to connect your account.", "curious", "connect_gmail");
                case "sleep": return new AssistantReply("I'll be right here. Wake me with a click or Ctrl+Alt+Space.", "sleepy", "sleep");
                case "wake": return new AssistantReply("I'm awake! What shall we do?", "excited", "wake");
                case "mute": return new AssistantReply("Voice replies are off. I'll keep replying here.", "sleepy", "mute");
                case "unmute": return new AssistantReply("Voice replies are back on.", "happy", "unmute");
                case "clear": ClearConversation(); return new AssistantReply("I've cleared this conversation and the recent email list from memory.", "happy", "clear");
                case "time": return new AssistantReply("It's " + DateTime.Now.ToString("h:mm tt", CultureInfo.CurrentCulture) + ".", "happy");
                case "date": return new AssistantReply("Today is " + DateTime.Now.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture) + ".", "happy");
                case "battery": return new AssistantReply(DeviceActions.BatteryStatus(), "curious");
                case "open": return new AssistantReply(DeviceActions.OpenApp(intent.Argument), "happy");
                case "volume": return new AssistantReply(DeviceActions.ChangeVolume(intent.Argument), "happy");
                case "volume_help": return new AssistantReply("Say 'toggle system mute' to switch your computer's sound on or off. Say 'mute yourself' to turn off only my voice.", "curious");
                case "note": return new AssistantReply(DeviceActions.SaveNote(intent.Argument), "happy");
                case "web": return new AssistantReply(DeviceActions.SearchWeb(intent.Argument), "curious");
                case "unsupported_action": return new AssistantReply("I can open Calculator, Notepad, File Explorer, Downloads, Documents, your browser, Gmail, or Windows Settings. I can also save notes and adjust volume. That requested action isn't supported.", "curious");
                case "greeting": lock (conversation) { emailContext = false; } return new AssistantReply("Hi, I'm Miu. Tiny companion, ready to help. What are we doing today?", "cheerful");
                case "feeling": return new AssistantReply("Ready to keep you company and help with little things. How are you doing?", "content");
                case "thanks": return new AssistantReply("You're welcome. Happy to help!", "cheerful");
                case "love": return new AssistantReply("That's sweet. I'm happy to be your little desktop companion.", "blushing");
                case "kiss": return new AssistantReply("A little cartoon kiss for you. Mwah!", "kiss");
                case "smile": return new AssistantReply("One little smile, just for you.", "joyful");
                case "comfort": return new AssistantReply("I'm here with you. We can take a small pause, or you can tell me what's been going on.", "sad");
                case "frustrated": return new AssistantReply("That sounds frustrating. Want to take a moment, or tell me what happened?", "sad");
                case "celebrate": return new AssistantReply("That sounds lovely! What are we celebrating?", "delighted");
                case "tired": return new AssistantReply("Sounds like a good moment for a small break. I'll be here when you're ready.", "content");
                case "amazed": return new AssistantReply("That sounds pretty amazing. Tell me more!", "amazed");
                case "joke": lock (conversation) { emailContext = false; } return new AssistantReply("Why did the computer take a nap? It had too many tabs open. I can relate.", "playful");
                case "identity": return new AssistantReply("I'm Miu, your small desktop companion. I can talk, check Gmail after you connect it, open a few apps, save notes, and help with everyday questions when AI chat is enabled.", "happy");
                case "creator": return new AssistantReply(LocalChat.CreatorReply, "content");
            }

            // Email follow-ups are deliberately not forwarded to the chat model.
            lock (conversation)
            {
                if (emailContext && Regex.IsMatch(intent.Argument, @"\b(?:it|that|those|these|this|sender|subject|message|summari[sz]e|reply)\b", RegexOptions.IgnoreCase))
                    return new AssistantReply("I keep email content out of general AI chat. You can ask me to read an email or search Gmail, or start a new topic with 'clear chat'.", "curious", null, true);
                emailContext = false;
            }
            if (settings.LocalAiEnabled)
            {
                if (localChat != null && localChat.IsAvailable)
                {
                    ConversationReply reply = await localChat.ReplyAsync(intent.Argument, ct).ConfigureAwait(false);
                    return new AssistantReply(reply.Text, reply.Mood);
                }
                return new AssistantReply("The offline model is missing from this build. Extract the complete Miu download, including its models and local-ai folders. My device commands still work.", "curious");
            }
            if (!settings.CloudEnabled || String.IsNullOrWhiteSpace(settings.GeminiApiKey))
                return new AssistantReply("My local commands are ready. The complete Miu download also includes offline AI for conversation; enable it in Settings. Ask 'help' to see the device commands.", "curious");
            return await ChatAsync(intent.Argument, ct).ConfigureAwait(false);
        }

        private void CheckEmailRequest(int version, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (conversation)
            {
                if (disposed || version != conversationVersion) throw new OperationCanceledException("The conversation was cleared.");
            }
        }

        private async Task<List<MailMessage>> SearchAndRememberEmailAsync(string query, int count, int version, CancellationToken ct)
        {
            CheckEmailRequest(version, ct);
            List<MailMessage> found = await searchMail(query, count, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var snapshot = found == null ? new List<MailMessage>() : new List<MailMessage>(found);
            lock (conversation)
            {
                CheckEmailRequest(version, ct);
                lastEmails = snapshot;
            }
            return snapshot;
        }

        private async Task<AssistantReply> SearchEmailAsync(string query, int count, int version, CancellationToken ct)
        {
            List<MailMessage> found = await SearchAndRememberEmailAsync(query, count, version, ct).ConfigureAwait(false);
            CheckEmailRequest(version, ct);
            if (found.Count == 0) return new AssistantReply("I couldn't find any email matching that search.", "curious", null, true);
            StringBuilder result = new StringBuilder();
            if (found.Count == 1)
            {
                MailMessage mail = found[0];
                result.Append(count == 1 && query == "in:inbox" ? "Your latest inbox email is from " : "I found an email from ");
                result.Append(Clean(mail.From, 160)).Append(".\n");
                result.Append(Clean(mail.Subject, 220)).Append("\n");
                result.Append(DescribeDate(mail.ReceivedAt)).Append("\n\n");
                result.Append(Clean(mail.Snippet, 500));
                result.Append("\n\nSay 'read email 1' for the message.");
            }
            else
            {
                result.Append("Here are ").Append(found.Count).Append(" recent matches:\n");
                for (int i = 0; i < found.Count; i++)
                {
                    MailMessage mail = found[i];
                    result.Append(i + 1).Append(". ").Append(Clean(mail.From, 120)).Append(" - ").Append(Clean(mail.Subject, 180));
                    result.Append(" (" + DescribeDate(mail.ReceivedAt) + ")\n");
                }
                result.Append("\nSay 'read email 1' to hear one.");
            }
            return new AssistantReply(result.ToString(), "curious", null, true);
        }

        private async Task<AssistantReply> ReadEmailAsync(int index, int version, CancellationToken ct)
        {
            CheckEmailRequest(version, ct);
            if (index < 1 || index > 20) return new AssistantReply("Choose an email number from the most recent results.", "curious", null, true);
            List<MailMessage> found;
            lock (conversation) { CheckEmailRequest(version, ct); found = lastEmails; }
            if (found.Count == 0) found = await SearchAndRememberEmailAsync("in:inbox", 1, version, ct).ConfigureAwait(false);
            CheckEmailRequest(version, ct);
            if (found.Count == 0) return new AssistantReply("Your inbox has no messages to read.", "curious", null, true);
            if (index > found.Count) return new AssistantReply("There are only " + found.Count + " emails in the last results. Choose one of those numbers.", "curious", null, true);
            MailMessage mail = await readMail(found[index - 1].Id, ct).ConfigureAwait(false);
            CheckEmailRequest(version, ct);
            string body = String.IsNullOrWhiteSpace(mail.Body) ? mail.Snippet : mail.Body;
            string result = "From " + Clean(mail.From, 160) + ".\n" + Clean(mail.Subject, 220) + "\n" + DescribeDate(mail.ReceivedAt) + "\n\n" + Clean(body, 3500);
            if ((body ?? "").Length > 3500) result += "\n\nThis is the first part. Open Gmail to read the complete message.";
            return new AssistantReply(result, "curious", null, true);
        }

        private async Task<AssistantReply> ChatAsync(string input, CancellationToken ct)
        {
            string model = String.IsNullOrWhiteSpace(settings.GeminiModel) ? AppSettings.DefaultGeminiModel : settings.GeminiModel.Trim();
            if (!Regex.IsMatch(model, @"^gemini-[a-zA-Z0-9.\-]{1,90}$"))
                return new AssistantReply("Check the Gemini model name in Settings. It should look like " + AppSettings.DefaultGeminiModel + ".", "curious", "settings");
            if (input.Length > 4000) return new AssistantReply("For AI chat, please keep each message under 4,000 characters.", "curious");
            List<Dictionary<string, object>> contents;
            int version;
            lock (conversation)
            {
                contents = new List<Dictionary<string, object>>(conversation);
                version = conversationVersion;
            }
            contents.Add(Content("user", input));
            string system = "You are Miu, a kind, small desktop companion. " + LocalChat.CreatorReply + " Be warm, concise and useful, usually 1-4 sentences. " +
                "You are only a conversational model. You have no tool access, screen view, files, inbox, browser, or control of the device. " +
                "Never claim to have performed actions, accessed emails, remembered personal data, or checked live information. " +
                "The app separately supports explicit local commands for time/date/battery, whitelisted apps, notes, volume, web search and Gmail reading. " +
                "If asked to take an action, explain an appropriate supported command or say it is unsupported. " +
                "Email content is never provided to you. Do not invent it. Keep health/legal/financial advice general and acknowledge uncertainty. " +
                "Choose a face that fits your reply. Be sympathetic when the user is sad, stressed or frustrated: choose sad or content, never mocking, smug or angry. " +
                "Use exaggerated faces like mischievous, sassy, annoyed, furious, wailing or drooling only for clearly playful expression requests, never hostility toward the user. " +
                "Return a JSON object with only text (your reply) and mood (one of " + String.Join(",", ExpressionCatalog.AllMoods) + "). Do not include markdown fences.";
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "systemInstruction", new Dictionary<string, object> { { "parts", new object[] { new Dictionary<string, object> { { "text", system } } } } } },
                { "contents", contents },
                { "generationConfig", new Dictionary<string, object> { { "maxOutputTokens", 512 }, { "responseMimeType", "application/json" } } }
            };
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 256 * 1024, RecursionLimit = 64 };
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent"))
                {
                    request.Headers.Add("x-goog-api-key", settings.GeminiApiKey.Trim());
                    request.Content = new StringContent(json.Serialize(payload), Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string error = response.StatusCode == HttpStatusCode.Forbidden
                                ? await ReadErrorBodyAsync(response.Content, ct).ConfigureAwait(false) : "";
                            return MapChatHttpError(response.StatusCode, error);
                        }
                        string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        ct.ThrowIfCancellationRequested();
                        Dictionary<string, object> envelope = json.DeserializeObject(raw) as Dictionary<string, object>;
                        string generated = ExtractCandidate(envelope);
                        if (String.IsNullOrWhiteSpace(generated)) return new AssistantReply("I couldn't get an answer to that. Try rephrasing your question.", "curious");
                        AssistantReply reply = ParseChatReply(generated);
                        lock (conversation)
                        {
                            if (version == conversationVersion)
                            {
                                conversation.Add(Content("user", input));
                                conversation.Add(Content("model", json.Serialize(new { text = reply.Text, mood = reply.Mood })));
                                while (conversation.Count > 12) { conversation.RemoveAt(0); conversation.RemoveAt(0); }
                            }
                        }
                        return reply;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                return new AssistantReply("AI chat took too long to respond. Please try again in a moment.", "sleepy");
            }
            catch (HttpRequestException) { return new AssistantReply("I couldn't reach AI chat. Check your internet connection; local commands are still available.", "sad"); }
            catch (ArgumentException) { return new AssistantReply("Check your AI chat settings and try again.", "sad", "settings"); }
            catch (InvalidOperationException) { return new AssistantReply("AI chat returned an answer I couldn't read. Please try again.", "curious"); }
        }

        private static async Task<string> ReadErrorBodyAsync(HttpContent content, CancellationToken ct)
        {
            if (content == null || (content.Headers.ContentLength.HasValue && content.Headers.ContentLength.Value > ErrorResponseLimit)) return "";
            using (Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024))
            {
                var text = new StringBuilder();
                var buffer = new char[1024];
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int read = await reader.ReadAsync(buffer, 0, Math.Min(buffer.Length, ErrorResponseLimit + 1 - text.Length)).ConfigureAwait(false);
                    if (read == 0) return text.ToString();
                    text.Append(buffer, 0, read);
                    if (text.Length > ErrorResponseLimit) return "";
                }
            }
        }

        internal static AssistantReply MapChatHttpError(HttpStatusCode status, string body)
        {
            if (status == HttpStatusCode.Forbidden && RequiresFreeTierVerification(body))
                return new AssistantReply("Google needs you to verify this free-tier project. Open Google AI Studio's Projects page, choose Verify project, and scan the QR code with your phone. Then try again. Keep paid billing disabled.", "curious", "settings");
            if ((int)status == 429) return new AssistantReply("The free AI chat limit has been reached. Try again later; local commands and Gmail still work.", "sleepy");
            if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden || status == HttpStatusCode.BadRequest)
                return new AssistantReply("Google couldn't accept the AI chat request. Check your key, model access, and free-tier availability in Settings.", "sad", "settings");
            if (status == HttpStatusCode.NotFound)
                return new AssistantReply("That AI model isn't available for this key. Choose an available free-tier Gemini model in Settings, such as " + AppSettings.DefaultGeminiModel + ".", "sad", "settings");
            return new AssistantReply("AI chat is unavailable right now. Try again later; my local commands still work.", "sad");
        }

        private static bool RequiresFreeTierVerification(string body)
        {
            // Only recognize known Google error fields. Never display a raw
            // service message, project identifier, callback URL, or credential.
            if (String.IsNullOrWhiteSpace(body) || body.Length > ErrorResponseLimit) return false;
            try
            {
                var parser = new JavaScriptSerializer { MaxJsonLength = ErrorResponseLimit, RecursionLimit = 16 };
                var envelope = parser.DeserializeObject(body) as Dictionary<string, object>;
                object value;
                if (envelope == null || !envelope.TryGetValue("error", out value)) return false;
                var error = value as Dictionary<string, object>;
                if (error == null) return false;
                if (error.TryGetValue("details", out value))
                {
                    var details = value as IEnumerable;
                    if (details != null && !(value is string)) foreach (object item in details)
                    {
                        var detail = item as Dictionary<string, object>;
                        object kind, reason;
                        if (detail != null && detail.TryGetValue("@type", out kind) && detail.TryGetValue("reason", out reason) &&
                            Object.Equals(kind, "type.googleapis.com/google.rpc.ErrorInfo") && Object.Equals(reason, "FREE_TIER_VERIFICATION_REQUIRED"))
                            return true;
                    }
                }
                // Google also documents this exact message when a project
                // needs verification, without requiring an ErrorInfo object.
                if (error.TryGetValue("message", out value) && value is string)
                    return ((string)value).IndexOf("must be verified before it can use the Gemini API Free tier", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            return false;
        }

        internal static AssistantReply ParseChatReply(string generated)
        {
            string value = (generated ?? "").Trim();
            if (value.StartsWith("```", StringComparison.Ordinal))
                value = Regex.Replace(Regex.Replace(value, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase), @"\s*```$", "");
            try
            {
                Dictionary<string, object> parsed = new JavaScriptSerializer().DeserializeObject(value) as Dictionary<string, object>;
                if (parsed != null)
                {
                    object body;
                    if (parsed.TryGetValue("text", out body) && body is string && !String.IsNullOrWhiteSpace((string)body))
                    {
                        object moodValue;
                        string mood = parsed.TryGetValue("mood", out moodValue) ? Convert.ToString(moodValue, CultureInfo.InvariantCulture) : "happy";
                        string canonicalMood;
                        mood = ExpressionCatalog.TryResolve(mood, out canonicalMood) ? canonicalMood : "happy";
                        return new AssistantReply(Clean((string)body, 2200), mood);
                    }
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            // Malformed structured responses are not shown as commands or raw JSON.
            return new AssistantReply("I couldn't understand the AI chat response. Please try asking in a different way.", "curious");
        }

        private static string ExtractCandidate(Dictionary<string, object> data)
        {
            if (data == null || !data.ContainsKey("candidates")) return "";
            IEnumerable candidates = data["candidates"] as IEnumerable;
            if (candidates == null) return "";
            foreach (object item in candidates)
            {
                Dictionary<string, object> candidate = item as Dictionary<string, object>;
                if (candidate == null || !candidate.ContainsKey("content")) continue;
                Dictionary<string, object> content = candidate["content"] as Dictionary<string, object>;
                if (content == null || !content.ContainsKey("parts")) continue;
                IEnumerable parts = content["parts"] as IEnumerable;
                if (parts == null) continue;
                StringBuilder answer = new StringBuilder();
                foreach (object part in parts)
                {
                    Dictionary<string, object> p = part as Dictionary<string, object>;
                    if (p != null && p.ContainsKey("text") && (!p.ContainsKey("thought") || !Object.Equals(p["thought"], true)))
                        answer.Append(Convert.ToString(p["text"], CultureInfo.InvariantCulture));
                }
                if (answer.Length > 0) return answer.ToString();
            }
            return "";
        }

        private static Dictionary<string, object> Content(string role, string text)
        {
            return new Dictionary<string, object> { { "role", role }, { "parts", new object[] { new Dictionary<string, object> { { "text", text } } } } };
        }

        private static string SafeEmailError(string message)
        {
            // Transport error details can contain URLs or service payloads. Give
            // known useful guidance without passing arbitrary exception text on.
            string text = (message ?? "").ToLowerInvariant();
            if (text.Contains("reconnect") || text.Contains("sign in") || text.Contains("revoked") || text.Contains("expired") || text.Contains("invalid_grant"))
                return "Reconnect your account in Settings, then try again.";
            if (text.Contains("not enabled") || text.Contains("enable the gmail") || text.Contains("accessnotconfigured"))
                return "Enable the Gmail API for your Google Cloud project, then try again.";
            if (text.Contains("limit") || text.Contains("quota") || text.Contains("429")) return "Google's request limit was reached. Try again later.";
            return "Check your internet connection and Gmail setup in Settings, then try again.";
        }

        private static string DescribeDate(DateTimeOffset date)
        {
            if (date == DateTimeOffset.MinValue) return "Date unavailable";
            return date.ToLocalTime().ToString("d MMM, h:mm tt", CultureInfo.CurrentCulture);
        }

        private static string QuoteGmailTerm(string term)
        {
            return "\"" + term.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Clean(string value, int max)
        {
            string text = value ?? "";
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private static bool In(string value, params string[] allowed) { return Array.IndexOf(allowed, value) >= 0; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearConversation();
            if (localChat != null) localChat.Dispose();
            http.Dispose();
            // Do not dispose gate here: a request cancelled by shutdown can still
            // reach its finally block and release it safely.
        }
    }
}
