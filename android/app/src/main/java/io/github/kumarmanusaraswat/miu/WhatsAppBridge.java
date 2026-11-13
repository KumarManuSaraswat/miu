package io.github.kumarmanusaraswat.miu;

/**
 * Private process memory. No message body, recipient or approval is accepted from external intents.
 */
final class WhatsAppBridge {
    enum Mode {
        READ,
        PREVIEW,
        SEND
    }

    static final class Request {
        final Mode mode;
        final WhatsAppDraft draft;
        final String recipient;
        final boolean assistant;
        final long started = android.os.SystemClock.elapsedRealtime();

        Request(Mode mode, WhatsAppDraft draft, String recipient, boolean assistant) {
            this.mode = mode;
            this.draft = draft;
            this.recipient = recipient;
            this.assistant = assistant;
        }
    }

    static final class Result {
        final String text, recipient;
        final WhatsAppDraft draft;
        final boolean sensitive;
        final long created = android.os.SystemClock.elapsedRealtime();

        Result(String text, boolean sensitive, String recipient, WhatsAppDraft draft) {
            this.text = text;
            this.sensitive = sensitive;
            this.recipient = recipient;
            this.draft = draft;
        }
    }

    static Request request;
    static Result result;
    static WhatsAppScreenService service;

    static boolean ready() {
        return service != null;
    }

    static void clear() {
        request = null;
        result = null;
        if (service != null) service.cancel();
    }

    static void start(Mode mode, WhatsAppDraft draft, String recipient, boolean assistant) {
        clear();
        request = new Request(mode, draft, recipient, assistant);
        if (service != null) service.begin();
    }

    static Result take() {
        Result r = result;
        result = null;
        return r != null && android.os.SystemClock.elapsedRealtime() - r.created < 60000 ? r : null;
    }
}
