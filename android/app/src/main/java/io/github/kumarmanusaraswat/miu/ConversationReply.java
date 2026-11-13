package io.github.kumarmanusaraswat.miu;

import java.util.Locale;
import java.util.regex.Pattern;

/** Presentation metadata only. Model reactions never enter the device command router. */
final class ConversationReply {
    final String text, mood;

    ConversationReply(String text, String mood) {
        this.text = text;
        this.mood = mood;
    }

    // Text cues only: no audio sentiment, saved mood profile, or extra model inference.
    static ConversationReply fromText(String message, String answer) {
        String user = normalize(message), reply = normalize(answer), mood = "content";
        if (has(
                        user,
                        "\\b(?:i(?:'m| am| feel| have been feeling|'ve been feeling)(?:"
                            + " feeling)?(?: really| very| so| a bit| pretty)?"
                            + " (?:sad|lonely|upset|unhappy|scared|afraid|anxious|stressed|overwhelmed|frustrated|angry|heartbroken|not"
                            + " (?:happy|okay|ok))|i (?:lost my|failed my|miss my)|my"
                            + " .{0,30}(?:died|passed away))\\b")
                || has(
                        reply,
                        "\\b(?:sorry (?:to hear|for your loss|you're going through)|that sounds"
                            + " (?:really |very |so"
                            + " )?(?:hard|painful|difficult|frustrating|upsetting)|understandable"
                            + " to feel (?:sad|lonely|upset))\\b"))
            mood = "sad";
        else if (has(
                user,
                "\\b(?:i(?:'m| am| feel)(?: really| very| so)?"
                    + " (?:tired|sleepy|exhausted)|(?:heading|going|off) to bed|good ?night)\\b"))
            mood = "sleepy";
        else if (has(
                user,
                "\\b(?:you(?:'re| are)(?: such a| so| really| very)?"
                    + " (?:sweet|cute|kind|helpful|lovely)|i (?:love|like|appreciate) you)\\b"))
            mood = "blushing";
        else if (has(
                        user,
                        "\\b(?:i(?:'m| am| feel)(?: really| very| so)?"
                            + " (?:happy|excited|proud|thrilled)|i (?:finally"
                            + " )?(?:passed|graduated|got promoted)|i won (?:a|the|my))\\b")
                || has(
                        reply,
                        "\\b(?:congratulations|congrats|well done|that's"
                            + " (?:fantastic|wonderful|great|amazing) news|happy for you)\\b"))
            mood = "delighted";
        else if (has(
                user,
                "\\b(?:(?:tell|give) me .{0,24}joke|make me laugh|something funny|(?:a|another)"
                    + " joke|haha+|hehe+)\\b"))
            mood = "playful";
        else if (has(user, "^(?:(?:wow|whoa)[!, .]+|that's (?:amazing|incredible|astonishing)\\b)"))
            mood = "amazed";
        else if (has(
                user,
                "^(?:(?:hey|hi|hello|thanks|thank you)\\b|good (?:morning|afternoon|evening)\\b)"))
            mood = "cheerful";
        else if (has(
                        user,
                        "\\b(?:why|how|what|explain|teach me|tell me about|help me understand)\\b")
                || user.endsWith("?")) mood = "curious";
        return new ConversationReply(answer, mood);
    }

    private static boolean has(String value, String pattern) {
        return Pattern.compile(pattern).matcher(value).find();
    }

    private static String normalize(String value) {
        return value == null ? "" : value.trim().toLowerCase(Locale.ROOT).replace('’', '\'');
    }
}
