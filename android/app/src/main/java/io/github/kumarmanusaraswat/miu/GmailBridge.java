package io.github.kumarmanusaraswat.miu;

/** Process memory only. Never persisted, logged, included in intents, or passed to AI. */
final class GmailBridge {
    enum Mode {
        OPEN_MESSAGE,
        LATEST,
        SEARCH
    }

    static final class Request {
        final Mode mode;
        final String query;
        final long started = android.os.SystemClock.elapsedRealtime();

        Request(Mode m, String q) {
            mode = m;
            query = q;
        }
    }

    static final class Result {
        final String text;
        final boolean email;

        Result(String t, boolean e) {
            text = t;
            email = e;
        }
    }

    static Request request;
    static Result result;
    static GmailScreenService service;

    static void clear() {
        request = null;
        result = null;
        if (service != null) service.cancel();
    }

    static boolean ready() {
        return service != null;
    }

    static void start(Mode mode, String query) {
        clear();
        request = new Request(mode, query);
        if (service != null) service.begin();
    }

    static Result take() {
        Result r = result;
        result = null;
        return r;
    }
}
