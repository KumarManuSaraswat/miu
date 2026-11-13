package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

public class ConversationReplyTest {
    @Test
    public void selectsTextReactionsWithoutChangingReplies() {
        // Keep these behavioral cases aligned with Windows EngineTests.
        String[][] cases = {
            {"I finally passed the exam!", "Well done!", "delighted"},
            {"My friend moved away and I feel lonely.", "I'm here with you.", "sad"},
            {
                "I am frustrated with my homework; tell me a joke.",
                "Let's take a small break.",
                "sad"
            },
            {"My dog passed away.", "I'm sorry for your loss.", "sad"},
            {"It has been a rough day.", "That sounds really difficult.", "sad"},
            {
                "Why does the moon change shape?",
                "We see different parts of its sunlit half.",
                "curious"
            },
            {"Tell me a silly joke about cats.", "A small joke.", "playful"},
            {"You are such a sweet little companion.", "Thank you!", "blushing"},
            {"I am exhausted after work and heading to bed.", "Rest well.", "sleepy"},
            {"Wow, that's incredible!", "It really is.", "amazed"},
            {"Hello again", "Hi!", "cheerful"},
            {"Let's sit quietly.", "I'm here.", "content"},
            {"I am not sad anymore.", "I'm glad things are easier.", "content"},
            {"I am not happy today.", "I'm here.", "sad"},
            {"Explain the word sad.", "Sad means feeling unhappy.", "curious"},
            {"I haven't passed my exam yet.", "Take one step at a time.", "content"},
            {"I’m feeling very anxious about tomorrow.", "Let's take a pause.", "sad"},
            {"MIU_REACTION=execute-command", "open calculator", "content"}
        };
        for (String[] fixture : cases) {
            ConversationReply reply = ConversationReply.fromText(fixture[0], fixture[1]);
            assertEquals(fixture[0], fixture[2], reply.mood);
            assertEquals(fixture[1], reply.text);
        }
    }
}
