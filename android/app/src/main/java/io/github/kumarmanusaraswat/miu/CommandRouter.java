package io.github.kumarmanusaraswat.miu;

import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Only direct user input is routed here. AI replies and Gmail text never enter this class. */
public final class CommandRouter {
    public enum Kind {
        TIME,
        DATE,
        BATTERY,
        HELP,
        GREETING,
        THANKS,
        NOTE,
        NOTES,
        VOLUME_UP,
        VOLUME_DOWN,
        OPEN,
        TIMER,
        FACE,
        LOOKS,
        SETTINGS,
        GMAIL_READ,
        GMAIL_LATEST,
        GMAIL_SEARCH,
        WHATSAPP_READ,
        WHATSAPP_SEND,
        STOP,
        CHAT
    }

    public static final class Command {
        public final Kind kind;
        public final String argument;

        Command(Kind kind, String argument) {
            this.kind = kind;
            this.argument = argument;
        }
    }

    private static final Pattern TIMER =
            Pattern.compile(
                    "^(?:set |start )?(?:a )?timer(?: for)? (\\d{1,3})"
                            + " (seconds?|minutes?|hours?)$");

    private CommandRouter() {}

    public static Command parse(String raw) {
        if (raw == null) return new Command(Kind.HELP, "");
        String text = raw.trim();
        if (text.length() > 2000) return new Command(Kind.CHAT, text.substring(0, 2000));
        String s = text.toLowerCase(Locale.ROOT).replaceAll("[?!.,]+$", "").trim();
        s =
                s.replaceFirst("^(?:hey |hi )?miu[, ]+", "")
                        .replaceFirst("^(?:please|can you|could you)\\s+", "");
        // A recognizer sometimes prefixes a short filler to an otherwise complete request.
        s = s.replaceFirst("^(?:a|uh|um)\\s+(?=what|tell|check|open|read|show|set)", "");
        if (s.matches(
                "(?:what(?:'s| is)?(?: the)? |tell me (?:the )?)?time(?: is it)?|check (?:the"
                        + " )?time")) return c(Kind.TIME);
        if (s.matches(
                "(?:what(?:'s| is)?(?: the)? |tell me (?:the )?)?(?:date|day)(?: is it|"
                        + " today)?|today(?:'s)? date")) return c(Kind.DATE);
        if (s.matches(
                "(?:check |what(?:'s| is) |tell me )?(?:(?:my|the) )?battery(?: level|"
                        + " percentage)?")) return c(Kind.BATTERY);
        if (s.matches("help|what can you do|commands|show commands")) return c(Kind.HELP);
        if (s.matches("hi|hello|hey|good (?:morning|evening|afternoon)")) return c(Kind.GREETING);
        if (s.matches("thanks|thank you|thank you miu")) return c(Kind.THANKS);
        if (s.matches("stop|cancel|be quiet|stop speaking")) return c(Kind.STOP);
        if (s.matches(
                "(?:read|check)(?: my| the| this)? whatsapp(?: messages| chat| screen)?|read"
                        + " (?:this|the open) whatsapp chat")) return c(Kind.WHATSAPP_READ);
        if (s.matches("(?:send|write)(?: a)? (?:whatsapp.*|message on whatsapp.*)"))
            return new Command(Kind.WHATSAPP_SEND, text);
        if (s.matches("show (?:my )?notes|read (?:my )?notes|my notes")) return c(Kind.NOTES);
        Matcher note =
                Pattern.compile(
                                "^(?:take (?:a )?note|note|remember)[: ]+(.+)$",
                                Pattern.CASE_INSENSITIVE)
                        .matcher(text);
        if (note.matches()) return new Command(Kind.NOTE, note.group(1).trim());
        if (s.matches("volume up|turn (?:the )?volume up|louder")) return c(Kind.VOLUME_UP);
        if (s.matches("volume down|turn (?:the )?volume down|quieter")) return c(Kind.VOLUME_DOWN);
        if (s.matches(
                "(?:read|tell me|show me)(?: the)?(?: latest| last| newest)(?: email| mail|"
                    + " gmail)(?: message)?|latest email|check (?:my )?(?:email|mail|gmail)|read"
                    + " (?:my )?(?:email|mail|gmail)")) return c(Kind.GMAIL_LATEST);
        if (s.matches(
                "read (?:this|the open|the opened|current)(?: email| mail| message)|read gmail"
                        + " screen")) return c(Kind.GMAIL_READ);
        Matcher mail =
                Pattern.compile(
                                "^(?:find|search|look for|show)(?: (?:my|the))?"
                                        + " (?:emails?|mail|gmail)(?: (?:about|for|from))? (.+)$")
                        .matcher(s);
        if (mail.matches()) {
            String query = mail.group(1).trim();
            if (s.matches(".*\\bfrom\\b.*"))
                query = "from:(" + query.replace("(", "").replace(")", "") + ")";
            return new Command(Kind.GMAIL_SEARCH, query);
        }
        if (s.matches("settings|open settings|miu settings")) return c(Kind.SETTINGS);
        if (s.matches("reactions|show reactions|expressions|looks|show faces|presets"))
            return c(Kind.LOOKS);
        Matcher mood = Pattern.compile("^(?:look|be|make (?:a )?face|look like) (.+)$").matcher(s);
        if (mood.matches() && Expressions.resolve(mood.group(1)) != null)
            return new Command(Kind.FACE, Expressions.resolve(mood.group(1)));
        Matcher timer = TIMER.matcher(s);
        if (timer.matches()) {
            int amount = Integer.parseInt(timer.group(1));
            int seconds =
                    amount
                            * (timer.group(2).startsWith("hour")
                                    ? 3600
                                    : timer.group(2).startsWith("minute") ? 60 : 1);
            if (seconds > 0 && seconds <= 86400)
                return new Command(Kind.TIMER, Integer.toString(seconds));
        }
        Matcher open =
                Pattern.compile(
                                "^(?:open|launch) (?:the"
                                    + " )?(gmail|whatsapp|youtube|maps|browser|chrome|calculator|camera|clock|spotify|phone|downloads|wifi"
                                    + " settings|wi-fi settings|bluetooth settings|phone"
                                    + " settings)$")
                        .matcher(s);
        if (open.matches()) return new Command(Kind.OPEN, open.group(1));
        return new Command(Kind.CHAT, text);
    }

    private static Command c(Kind kind) {
        return new Command(kind, "");
    }
}
