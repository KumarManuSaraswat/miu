package io.github.kumarmanusaraswat.miu;

import static org.junit.Assert.*;

import org.junit.Test;

import java.util.*;

public class EnglishLanguagesTest {
    @Test
    public void usesAnInstalledEnglishVariant() {
        assertEquals("en-IN", EnglishLanguages.choose(Arrays.asList("hi-IN", "en-IN"), "en-US"));
        assertEquals("en-IN", EnglishLanguages.choose(Arrays.asList("en-US", "en-IN"), "en-IN"));
        assertEquals("en-US", EnglishLanguages.choose(Arrays.asList("en_GB", "en_US"), "en-AU"));
        assertNull(EnglishLanguages.choose(Arrays.asList("hi-IN", "de-DE"), "en-US"));
    }
}
