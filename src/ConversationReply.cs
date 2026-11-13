using System;
using System.Text.RegularExpressions;

namespace Miu
{
    // Presentation metadata only. A conversational reaction cannot become a device action.
    internal sealed class ConversationReply
    {
        internal readonly string Text, Mood;
        internal ConversationReply(string text, string mood) { Text = text; Mood = mood; }

        // Small, deterministic presentation rules. They use text, not microphone tone,
        // and never alter the answer, save a mood profile, or run another model.
        internal static ConversationReply FromText(string message, string answer)
        {
            string user = Normalize(message), reply = Normalize(answer);
            string mood = "content";
            if (Has(user, @"\b(?:i(?:'m| am| feel| have been feeling|'ve been feeling)(?: feeling)?(?: really| very| so| a bit| pretty)? (?:sad|lonely|upset|unhappy|scared|afraid|anxious|stressed|overwhelmed|frustrated|angry|heartbroken|not (?:happy|okay|ok))|i (?:lost my|failed my|miss my)|my .{0,30}(?:died|passed away))\b") ||
                Has(reply, @"\b(?:sorry (?:to hear|for your loss|you're going through)|that sounds (?:really |very |so )?(?:hard|painful|difficult|frustrating|upsetting)|understandable to feel (?:sad|lonely|upset))\b")) mood = "sad";
            else if (Has(user, @"\b(?:i(?:'m| am| feel)(?: really| very| so)? (?:tired|sleepy|exhausted)|(?:heading|going|off) to bed|good ?night)\b")) mood = "sleepy";
            else if (Has(user, @"\b(?:you(?:'re| are)(?: such a| so| really| very)? (?:sweet|cute|kind|helpful|lovely)|i (?:love|like|appreciate) you)\b")) mood = "blushing";
            else if (Has(user, @"\b(?:i(?:'m| am| feel)(?: really| very| so)? (?:happy|excited|proud|thrilled)|i (?:finally )?(?:passed|graduated|got promoted)|i won (?:a|the|my))\b") ||
                Has(reply, @"\b(?:congratulations|congrats|well done|that's (?:fantastic|wonderful|great|amazing) news|happy for you)\b")) mood = "delighted";
            else if (Has(user, @"\b(?:(?:tell|give) me .{0,24}joke|make me laugh|something funny|(?:a|another) joke|haha+|hehe+)\b")) mood = "playful";
            else if (Has(user, @"^(?:(?:wow|whoa)[!, .]+|that's (?:amazing|incredible|astonishing)\b)")) mood = "amazed";
            else if (Has(user, @"^(?:(?:hey|hi|hello|thanks|thank you)\b|good (?:morning|afternoon|evening)\b)")) mood = "cheerful";
            else if (Has(user, @"\b(?:why|how|what|explain|teach me|tell me about|help me understand)\b") || user.EndsWith("?", StringComparison.Ordinal)) mood = "curious";
            return new ConversationReply(answer, mood);
        }

        private static bool Has(string value, string pattern) { return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant); }
        private static string Normalize(string value) { return (value ?? "").Trim().ToLowerInvariant().Replace('’', '\''); }
    }
}
