package io.github.kumarmanusaraswat.miu;

import java.util.regex.*;

/** Parses only a direct user's explicit messaging command. Never called on screen or model text. */
final class WhatsAppDraft {
    final String number, text;

    private WhatsAppDraft(String number, String text) {
        this.number = number;
        this.text = text;
    }

    static WhatsAppDraft parse(String raw) {
        if (raw == null || raw.length() > 2000) return null;
        String s =
                raw.trim()
                        .replaceFirst("(?i)^(?:hey |hi )?miu[, ]+", "")
                        .replaceFirst("(?i)^(?:please|can you|could you)\\s+", "");
        Matcher addressed =
                Pattern.compile(
                                "(?is)^(?:send|write)(?: a)? whatsapp(?: message)? to"
                                        + " (\\+?[1-9][0-9 ()-]{5,24})\\s*(?::|saying)\\s*(.+)$")
                        .matcher(s);
        if (addressed.matches()) {
            String number = addressed.group(1).replaceAll("[^0-9]", "");
            String text = addressed.group(2).trim();
            if (!number.matches("[1-9][0-9]{6,14}") || text.isEmpty() || text.length() > 1200)
                return null;
            return new WhatsAppDraft(number, text);
        }
        Matcher current =
                Pattern.compile(
                                "(?is)^(?:send|write)(?: a)? (?:whatsapp(?: message)?|message on"
                                        + " whatsapp)\\s*(?::|saying)\\s*(.+)$")
                        .matcher(s);
        if (current.matches()) {
            String text = current.group(1).trim();
            if (text.isEmpty() || text.length() > 1200) return null;
            return new WhatsAppDraft("", text);
        }
        return null;
    }
}
