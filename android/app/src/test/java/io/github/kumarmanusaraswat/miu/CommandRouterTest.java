package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

public class CommandRouterTest {
    @Test
    public void understandsItsNameWithoutChangingMessageBodies() {
        assertEquals(CommandRouter.Kind.GREETING, CommandRouter.parse("Hi Miu!").kind);
        assertEquals(CommandRouter.Kind.GREETING, CommandRouter.parse("Hello, Miu").kind);
        assertEquals(
                CommandRouter.Kind.TIME, CommandRouter.parse("Hey Miu, what time is it?").kind);
        assertEquals("Hello, Miu!", CommandRouter.parse("Miu, note Hello, Miu!").argument);
        assertEquals(
                "send WhatsApp message saying Hi Miu!",
                CommandRouter.parse("Miu, send WhatsApp message saying Hi Miu!").argument);
    }

    private void kind(CommandRouter.Kind expected, String... examples) {
        for (String text : examples) assertEquals(text, expected, CommandRouter.parse(text).kind);
    }

    @Test
    public void voiceFillersStillReachLocalClock() {
        kind(
                CommandRouter.Kind.TIME,
                "what time is it",
                "a what time is it",
                "Hey Miu, what time is it?",
                "uh what is the time",
                "please tell me the time",
                "check the time");
    }

    @Test
    public void incompleteOrQuotedInputDoesNotRunActions() {
        kind(
                CommandRouter.Kind.CHAT,
                "is it",
                "what time is it and delete my files",
                "open terminal",
                "run rm -rf /",
                "someone said open calculator",
                "{\"command\":\"open gmail\"}",
                "how do I open gmail",
                "open gmail and send a message");
    }

    @Test
    public void localEmailRoutesStayOutOfCloud() {
        kind(
                CommandRouter.Kind.GMAIL_LATEST,
                "read the latest email",
                "tell me the latest email",
                "check my gmail",
                "read my email");
        kind(
                CommandRouter.Kind.GMAIL_READ,
                "read this email",
                "read the open message",
                "read gmail screen");
        assertEquals("from:(alex)", CommandRouter.parse("find email from Alex").argument);
        assertEquals("receipt", CommandRouter.parse("search email about receipt").argument);
    }

    @Test
    public void timerBoundsAndUnits() {
        assertEquals("300", CommandRouter.parse("set a timer for 5 minutes").argument);
        assertEquals("3600", CommandRouter.parse("timer 1 hour").argument);
        kind(
                CommandRouter.Kind.CHAT,
                "timer 0 seconds",
                "timer 999 hours",
                "timer -1 minutes",
                "timer 1 second and call someone");
    }

    @Test
    public void notesRetainOriginalText() {
        CommandRouter.Command c = CommandRouter.parse("note Buy Milk at 6 PM");
        assertEquals(CommandRouter.Kind.NOTE, c.kind);
        assertEquals("Buy Milk at 6 PM", c.argument);
    }

    @Test
    public void knownActionsAreAllowlisted() {
        kind(CommandRouter.Kind.OPEN, "open calculator", "open phone settings", "launch gmail");
        kind(
                CommandRouter.Kind.CHAT,
                "open /sdcard/secrets",
                "open javascript:alert(1)",
                "launch arbitrary.package");
    }

    @Test
    public void expressionsAreValidated() {
        assertEquals(24, Expressions.FACES.length);
        assertEquals(
                24, new java.util.HashSet<>(java.util.Arrays.asList(Expressions.FACES)).size());
        assertEquals("wide_eyed", Expressions.resolve("Wide-eyed"));
        assertNull(Expressions.resolve("<script>"));
        assertNull(Expressions.resolve(null));
        assertEquals("cheerful", Expressions.face("speaking"));
        assertEquals("idle", Expressions.face(null));
    }

    @Test
    public void emptyAndHugeInputAreBounded() {
        assertEquals(CommandRouter.Kind.HELP, CommandRouter.parse(null).kind);
        StringBuilder large = new StringBuilder();
        for (int i = 0; i < 5000; i++) large.append('x');
        assertEquals(2000, CommandRouter.parse(large.toString()).argument.length());
    }
}
