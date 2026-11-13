package io.github.kumarmanusaraswat.miu;

import java.util.*;

/** Splits a bounded local readout at word boundaries without cutting a UTF-16 surrogate pair. */
final class SpeechChunks {
    static List<String> split(String text, int limit) {
        if (limit < 2) throw new IllegalArgumentException("Chunk limit too small");
        ArrayList<String> chunks = new ArrayList<>();
        int start = 0;
        while (start < text.length()) {
            int end = Math.min(text.length(), start + limit);
            if (end < text.length()) {
                int space = text.lastIndexOf(' ', end);
                if (space > start) end = space;
                if (Character.isHighSurrogate(text.charAt(end - 1))) end--;
            }
            String part = text.substring(start, end).trim();
            if (!part.isEmpty()) chunks.add(part);
            start = end;
            while (start < text.length() && Character.isWhitespace(text.charAt(start))) start++;
        }
        return chunks;
    }
}
