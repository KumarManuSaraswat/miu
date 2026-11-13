package io.github.kumarmanusaraswat.miu;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

public final class Expressions {
    public static final String[] FACES = {
        "content",
        "kiss",
        "crying",
        "neutral",
        "playful",
        "wailing",
        "mischievous",
        "smug_kiss",
        "sassy",
        "wide_eyed",
        "surprised_kiss",
        "shocked",
        "joyful",
        "sad",
        "annoyed",
        "scrunched",
        "dizzy",
        "blushing",
        "cheerful",
        "drooling",
        "delighted",
        "skeptical",
        "furious",
        "amazed"
    };
    private static final Set<String> SUPPORTED;

    static {
        HashSet<String> all = new HashSet<>(Arrays.asList(FACES));
        all.addAll(
                Arrays.asList(
                        "idle",
                        "happy",
                        "curious",
                        "excited",
                        "love",
                        "sleepy",
                        "angry",
                        "confused",
                        "thinking",
                        "listening",
                        "speaking",
                        "wink",
                        "surprised"));
        SUPPORTED = Collections.unmodifiableSet(all);
    }

    private Expressions() {}

    public static String resolve(String value) {
        if (value == null || value.length() > 80) return null;
        String id = value.trim().toLowerCase(Locale.ROOT).replaceAll("[\\s-]+", "_");
        return SUPPORTED.contains(id) ? id : null;
    }

    public static String label(String value) {
        String id = resolve(value);
        if (id == null) return "Expression";
        return Character.toUpperCase(id.charAt(0)) + id.substring(1).replace('_', ' ');
    }

    public static String face(String id) {
        if (id == null) return "idle";
        switch (id) {
            case "sleepy":
                return "content";
            case "happy":
            case "speaking":
                return "cheerful";
            case "excited":
                return "delighted";
            case "love":
                return "kiss";
            case "angry":
                return "furious";
            case "confused":
                return "dizzy";
            case "curious":
                return "wide_eyed";
            case "surprised":
                return "shocked";
            default:
                return resolve(id) == null ? "idle" : id;
        }
    }
}
