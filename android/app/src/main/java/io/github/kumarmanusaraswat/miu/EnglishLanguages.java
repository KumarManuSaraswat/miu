package io.github.kumarmanusaraswat.miu;

import java.util.*;

final class EnglishLanguages {
    static String preferred() {
        Locale locale = Locale.getDefault();
        return locale.getLanguage().equals("en") ? locale.toLanguageTag() : "en-US";
    }

    static String choose(List<String> available, String preferred) {
        String first = null, us = null;
        for (String tag : available) {
            Locale locale = Locale.forLanguageTag(tag.replace('_', '-'));
            if (!locale.getLanguage().equals("en")) continue;
            String canonical = locale.toLanguageTag();
            if (canonical.equalsIgnoreCase(preferred)) return canonical;
            if (first == null) first = canonical;
            if (canonical.equalsIgnoreCase("en-US")) us = canonical;
        }
        return us != null ? us : first;
    }
}
