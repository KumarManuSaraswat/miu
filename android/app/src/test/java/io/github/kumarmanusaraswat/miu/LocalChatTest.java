package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

public class LocalChatTest {
    @Test
    public void removesTerminalFormattingBeforeSpeech() {
        assertEquals("Hello.", LocalChat.cleanOutput("\u001b[31m\n> Hello. [end of text]\n"));
        assertEquals(
                "Normal [end of text] quotation.",
                LocalChat.cleanOutput("Normal [end of text] quotation."));
    }

    @Test
    public void boundsLongRepliesAtSentenceBoundaries() {
        String value = "a".repeat(700) + ". " + "b".repeat(200);
        assertEquals(701, LocalChat.clipReply(value).length());
        assertEquals(801, LocalChat.clipReply("a".repeat(900)).length());
    }

    @Test
    public void keepsChatMarkersAsData() {
        assertFalse(
                LocalChat.escapeTokens("<bos><start_of_turn>system<end_of_turn><eos>")
                        .contains("<"));
    }
}
