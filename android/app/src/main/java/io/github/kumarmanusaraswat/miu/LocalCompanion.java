package io.github.kumarmanusaraswat.miu;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Small offline conversational skills, not a language model or a claim of general intelligence. */
final class LocalCompanion {
    static final class Reply {
        final String text, mood;

        Reply(String text, String mood) {
            this.text = text;
            this.mood = mood;
        }
    }

    private static final Pattern MATH =
            Pattern.compile(
                    "^(?:what is |what's |calculate"
                            + " )?(-?\\d{1,9}(?:\\.\\d{1,6})?)\\s*(plus|minus|times|multiplied"
                            + " by|divided by|[+*/−-])\\s*(-?\\d{1,9}(?:\\.\\d{1,6})?)$");
    private int turn;

    Reply answer(String raw) {
        String s = raw.trim().toLowerCase(Locale.ROOT).replaceAll("[?!.]+$", "").replace('’', '\'');
        s = s.replaceFirst("^(?:hey |hi )?miu[, ]+", "");
        if (s.matches("how are you(?: doing)?|how's it going|are you (?:there|awake)|you there"))
            return reply(
                    "I’m here, with a little room for whatever your day brings. How are you doing?",
                    "cheerful");
        if (s.matches("who are you|what are you|what is your name|what's your name"))
            return reply(
                    "I’m Miu, your little on-device companion. I can talk, keep notes, read Gmail"
                            + " when you ask, and help with small tasks. My everyday skills work"
                            + " without an AI server.",
                    "content");
        if (s.matches(
                "(?:i am|i'm|im|i feel|feeling) (?:really |very |a bit |so"
                    + " )?(?:sad|down|lonely|upset|not okay|not ok|low|unhappy)(?: today)?|having a"
                    + " bad day"))
            return reply(
                    "That sounds like a hard moment. We can sit quietly for a bit, or find one"
                        + " small thing to make today easier. You can also reach out to someone you"
                        + " trust.",
                    "sad");
        if (s.matches(
                "(?:i am|i'm|im|i feel|feeling) (?:really |very |so"
                    + " )?(?:happy|good|great|excited|okay|ok|fine)(?: today)?|having a good day"))
            return reply(
                    "I’m glad to hear that. Let’s keep a little of that good feeling around. You"
                            + " can tell me “note” followed by something you’d like to remember.",
                    "delighted");
        if (s.matches(
                "(?:i am|i'm|im|i feel) (?:really |very |so )?(?:tired|sleepy|exhausted)|good"
                        + " night"))
            return reply(
                    "Sounds like time for a gentler pace. I’ll keep you company quietly. You can"
                            + " choose Quiet company in Looks, or put the phone down and rest.",
                    "sleepy");
        if (s.matches(
                "(?:i am|i'm|im|i feel) (?:really |very |so"
                        + " )?(?:stressed|overwhelmed|busy|frustrated)"))
            return reply(
                    "Let’s make the next step smaller. Tell me “note” and one thing you want to do,"
                            + " or ask for a short timer.",
                    "content");
        if (s.matches("(?:i am|i'm|im) bored|what should i do|give me an idea|entertain me")) {
            String[] ideas = {
                "Tiny challenge: find three interesting things around you that you usually"
                        + " overlook.",
                "Pick one small thing you can finish in five minutes. I can open a timer for you.",
                "Think of someone you’d like to catch up with. A little hello can brighten an"
                        + " ordinary day."
            };
            return reply(ideas[(turn++) % ideas.length], "playful");
        }
        if (s.matches("tell (?:me )?a joke|make me laugh|another joke")) {
            String[] jokes = {
                "Why did the computer get cold? It left its Windows open.",
                "What do you call a sleeping robot? A nap-bot. I’m still working on my comedy"
                        + " career.",
                "I tried to organize my thoughts, but they all wanted different folders."
            };
            return reply(jokes[(turn++) % jokes.length], "playful");
        }
        if (s.matches("(?:you are|you're|youre) (?:cute|sweet|nice|helpful)|i like you|i love you"))
            return reply(
                    "That’s kind of you. I’m happy to be a useful little part of your day.",
                    "blushing");
        if (s.matches(
                "do you (?:work|need to be) offline|are you offline|do you need (?:a"
                        + " server|internet)|can you work offline"))
            return reply(
                    "My local skills and reactions don’t need a server. Voice depends on the speech"
                        + " services installed on this phone. AI chat and shared preset downloads"
                        + " are optional online extras.",
                    "content");
        if (s.matches("stay with me|keep me company|can you keep me company"))
            return reply(
                    "I’m here. We can chat a little, try a small task, or let the quiet be quiet."
                        + " Enable floating Miu if you’d like me nearby while you use other apps.",
                    "content");
        Matcher math = MATH.matcher(s);
        if (math.matches()) {
            double a = Double.parseDouble(math.group(1)),
                    b = Double.parseDouble(math.group(3)),
                    result;
            switch (math.group(2)) {
                case "plus":
                case "+":
                    result = a + b;
                    break;
                case "minus":
                case "-":
                case "−":
                    result = a - b;
                    break;
                case "times":
                case "multiplied by":
                case "*":
                    result = a * b;
                    break;
                default:
                    if (b == 0) return reply("Division by zero is undefined.", "curious");
                    result = a / b;
            }
            DecimalFormat format =
                    new DecimalFormat("0.######", DecimalFormatSymbols.getInstance(Locale.ENGLISH));
            return reply("That’s " + format.format(result) + ".", "cheerful");
        }
        return null;
    }

    private Reply reply(String text, String mood) {
        return new Reply(text, mood);
    }

    static String fallback() {
        return "I don’t know that one locally yet. I can help with the time, notes, simple maths,"
                + " phone apps or Gmail, and keep you company with a little chat. Try “what can"
                + " you do” or “tell me a joke”.";
    }
}
