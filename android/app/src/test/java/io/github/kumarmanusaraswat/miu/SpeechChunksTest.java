package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

import java.util.*;

public class SpeechChunksTest {
    @Test
    public void longReadoutsAreNotSilentlyCutOff() {
        String text = "An email with several paragraphs and a final sentence.";
        List<String> chunks = SpeechChunks.split(text, 16);
        assertEquals(text, String.join(" ", chunks));
        for (String part : chunks) assertTrue(part.length() <= 16);
    }

    @Test
    public void emojiPairsRemainIntact() {
        List<String> chunks = SpeechChunks.split("123\ud83d\ude0a456", 4);
        assertEquals("123\ud83d\ude0a456", String.join("", chunks));
        for (String chunk : chunks) {
            assertFalse(Character.isHighSurrogate(chunk.charAt(chunk.length() - 1)));
            assertFalse(Character.isLowSurrogate(chunk.charAt(0)));
        }
    }
}
