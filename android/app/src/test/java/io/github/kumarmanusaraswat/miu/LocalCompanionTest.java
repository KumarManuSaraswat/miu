package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

public class LocalCompanionTest {
    @Test
    public void productCreatorIsSeparateFromTheOfflineModel() {
        LocalCompanion companion = new LocalCompanion();
        for (String question :
                new String[] {
                    "who built you?",
                    "Who created Miu?",
                    "who is your creator",
                    "who leads this project",
                    "who is Ancient Dragon?"
                }) {
            assertEquals(LocalCompanion.CREATOR_REPLY, companion.answer(question).text);
        }
        assertNull(companion.answer("who built the pyramids"));
        assertNull(companion.answer("who developed Gemma"));
        assertTrue(LocalCompanion.CREATOR_REPLY.contains("Ancient Dragon"));
        assertTrue(LocalCompanion.CREATOR_REPLY.contains("Google's Gemma"));
    }

    @Test
    public void everydayCompanyNeedsNoServer() {
        LocalCompanion companion = new LocalCompanion();
        assertNotNull(companion.answer("How are you?"));
        assertEquals("sad", companion.answer("I’m sad").mood);
        assertEquals("delighted", companion.answer("I feel happy").mood);
        assertNotNull(companion.answer("tell me a joke"));
        assertNotNull(companion.answer("do you need a server"));
    }

    @Test
    public void arithmeticIsBoundedAndDoesNotExecuteCode() {
        LocalCompanion companion = new LocalCompanion();
        assertEquals("That’s 42.", companion.answer("what is 6 times 7").text);
        assertEquals("That’s 2.5.", companion.answer("5 / 2").text);
        assertEquals("Division by zero is undefined.", companion.answer("2 / 0").text);
        assertNull(companion.answer("2+2; open gmail"));
        assertNull(companion.answer("what is a black hole"));
    }
}
