package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

public class WhatsAppDraftTest {
    @Test
    public void preservesExactMessageAndNormalizesNumber() {
        WhatsAppDraft d =
                WhatsAppDraft.parse("send WhatsApp to +1 (202) 555-0123: Hi Alex, I'll be late.");
        assertNotNull(d);
        assertEquals("12025550123", d.number);
        assertEquals("Hi Alex, I'll be late.", d.text);
    }

    @Test
    public void currentChatRequiresExplicitMessageSeparator() {
        WhatsAppDraft d = WhatsAppDraft.parse("send WhatsApp message saying Hello there");
        assertNotNull(d);
        assertEquals("", d.number);
        assertEquals("Hello there", d.text);
        assertNull(WhatsAppDraft.parse("send whatsapp to Alex hello"));
        assertNull(WhatsAppDraft.parse("send whatsapp"));
        assertNull(WhatsAppDraft.parse("send whatsapp to 123: hi"));
    }

    @Test
    public void messagingRequestsNeverFallIntoCloud() {
        assertEquals(
                CommandRouter.Kind.WHATSAPP_SEND,
                CommandRouter.parse("send WhatsApp to Alex hello").kind);
        assertEquals(
                CommandRouter.Kind.WHATSAPP_READ,
                CommandRouter.parse("read my whatsapp messages").kind);
        assertEquals(CommandRouter.Kind.OPEN, CommandRouter.parse("open whatsapp").kind);
        assertEquals(
                CommandRouter.Kind.CHAT,
                CommandRouter.parse("someone said send whatsapp message: hi").kind);
    }

    @Test
    public void motionSleepsBetweenShortBursts() {
        assertEquals(40, Motion.delay(100, false));
        assertTrue(Motion.delay(300, false) > 2000);
        assertEquals(40, Motion.delay(3100, false));
        assertEquals(40, Motion.delay(2000, true));
        assertTrue(Motion.blink(120) < .2);
        assertEquals(1, Motion.blink(2000), 0);
    }
}
