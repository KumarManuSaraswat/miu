package io.github.kumarmanusaraswat.miu;

import android.app.Activity;
import android.content.Intent;
import android.os.*;
import android.speech.*;
import android.speech.tts.*;

import java.util.*;

/** A recognizer exists only during an explicitly started listening session. */
final class SpeechController implements RecognitionListener {
    interface Listener {
        void state(String message, String mood);

        void heard(String text, boolean uncertain);
    }

    private final Activity activity;
    private final Listener listener;
    private final Prefs prefs;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private SpeechRecognizer recognizer;
    private TextToSpeech tts;
    private boolean ready, listening, active = true;
    private int generation, engineGeneration;
    private String pendingText;
    private final Runnable timeout = this::onTimeout;

    private void onTimeout() {
        cancelListening();
        listener.state("Listening stopped. Tap the microphone to try again.", "idle");
    }

    SpeechController(Activity a, Prefs p, Listener l) {
        activity = a;
        prefs = p;
        listener = l;
    }

    private void prepareVoice() {
        int engine = ++engineGeneration;
        tts =
                new TextToSpeech(
                        activity,
                        status -> {
                            if (engine != engineGeneration || tts == null || !active) return;
                            if (status != TextToSpeech.SUCCESS) {
                                listener.state(
                                        "The voice service could not start. Your reply is shown"
                                                + " below.",
                                        "idle");
                                releaseVoice();
                                return;
                            }
                            tts.setLanguage(Locale.ENGLISH);
                            Set<Voice> voices = tts.getVoices();
                            Voice best = null;
                            if (voices != null)
                                for (Voice v : voices) {
                                    if (v.getLocale().getLanguage().equals("en")
                                            && !v.isNetworkConnectionRequired()
                                            && (v.getFeatures() == null
                                                    || !v.getFeatures()
                                                            .contains(
                                                                    TextToSpeech.Engine
                                                                            .KEY_FEATURE_NOT_INSTALLED))
                                            && (best == null || v.getQuality() > best.getQuality()))
                                        best = v;
                                }
                            // Never silently send private text to a network voice.
                            ready = best != null && tts.setVoice(best) == TextToSpeech.SUCCESS;
                            if (!ready) {
                                pendingText = null;
                                listener.state(
                                        "Install an English offline voice in Android’s"
                                                + " text-to-speech settings.",
                                        "idle");
                                releaseVoice();
                                return;
                            }
                            tts.setOnUtteranceProgressListener(
                                    new UtteranceProgressListener() {
                                        public void onStart(String id) {
                                            postState(
                                                    id, "Speaking · tap Stop to pause", "speaking");
                                        }

                                        public void onDone(String id) {
                                            if (id.endsWith("/end")) finishSpeech(id, false);
                                        }

                                        public void onError(String id) {
                                            finishSpeech(id, true);
                                        }
                                    });
                            playPending();
                        });
    }

    private void postState(String id, String message, String mood) {
        handler.post(() -> {
            if (active && id.startsWith(generation + "/")) listener.state(message, mood);
        });
    }

    private void finishSpeech(String id, boolean error) {
        handler.post(() -> {
            if (!active || !id.startsWith(generation + "/")) return;
            stop();
            releaseVoice();
            listener.state(
                    error
                            ? "Voice is unavailable. Your reply is shown below."
                            : "Ready when you are",
                    "idle");
        });
    }

    boolean onDevice() {
        return Build.VERSION.SDK_INT >= 31
                && SpeechRecognizer.isOnDeviceRecognitionAvailable(activity);
    }

    boolean available() {
        return onDevice() || SpeechRecognizer.isRecognitionAvailable(activity);
    }

    boolean isListening() {
        return listening;
    }

    boolean voiceReady() {
        return ready;
    }

    void setActive(boolean value) {
        active = value;
        if (!value) {
            stop();
            releaseVoice();
        }
    }

    void listen() {
        stop();
        if (!active) return;
        try {
            recognizer =
                    onDevice()
                            ? SpeechRecognizer.createOnDeviceSpeechRecognizer(activity)
                            : SpeechRecognizer.createSpeechRecognizer(activity);
            // A late callback from a destroyed turn must never control a newer turn.
            SpeechRecognizer session = recognizer;
            recognizer.setRecognitionListener(
                    new RecognitionListener() {
                        private boolean current() {
                            return active && listening && recognizer == session;
                        }

                        public void onReadyForSpeech(Bundle b) {
                            if (current()) SpeechController.this.onReadyForSpeech(b);
                        }

                        public void onBeginningOfSpeech() {}

                        public void onRmsChanged(float rms) {}

                        public void onBufferReceived(byte[] b) {}

                        public void onEndOfSpeech() {
                            if (current()) SpeechController.this.onEndOfSpeech();
                        }

                        public void onError(int error) {
                            if (current()) SpeechController.this.onError(error);
                        }

                        public void onResults(Bundle b) {
                            if (current()) SpeechController.this.onResults(b);
                        }

                        public void onPartialResults(Bundle b) {}

                        public void onEvent(int type, Bundle b) {}
                    });
            listening = true;
            handler.postDelayed(timeout, 25000);
            if (onDevice() && Build.VERSION.SDK_INT >= 33) {
                listener.state("Checking offline English…", "thinking");
                session.checkRecognitionSupport(
                        recognitionIntent(language()),
                        activity.getMainExecutor(),
                        new RecognitionSupportCallback() {
                            public void onSupportResult(RecognitionSupport support) {
                                if (recognizer != session || !active || !listening) return;
                                String installed =
                                        EnglishLanguages.choose(
                                                support.getInstalledOnDeviceLanguages(),
                                                language());
                                if (installed == null) {
                                    String supported =
                                            EnglishLanguages.choose(
                                                    support.getSupportedOnDeviceLanguages(),
                                                    language());
                                    if (supported != null)
                                        prefs.values
                                                .edit()
                                                .putString("recognition_language", supported)
                                                .apply();
                                    cancelListening();
                                    listener.state(
                                            "Offline English needs a download. Open Settings →"
                                                    + " Download offline English.",
                                            "idle");
                                    return;
                                }
                                prefs.values
                                        .edit()
                                        .putString("recognition_language", installed)
                                        .apply();
                                startRecognition(session, installed);
                            }

                            public void onError(int error) {
                                if (recognizer == session && active && listening)
                                    startRecognition(session, language());
                            }
                        });
            } else startRecognition(session, language());
        } catch (RuntimeException ex) {
            cancelListening();
            listener.state(
                    "Speech recognition could not start. Check your phone’s speech services.",
                    "sad");
        }
    }

    private String language() {
        return prefs.values.getString("recognition_language", EnglishLanguages.preferred());
    }

    private Intent recognitionIntent(String language) {
        return new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                .putExtra(
                        RecognizerIntent.EXTRA_LANGUAGE_MODEL,
                        RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                .putExtra(RecognizerIntent.EXTRA_LANGUAGE, language)
                .putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true)
                .putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3)
                .putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, true);
    }

    private void startRecognition(SpeechRecognizer session, String language) {
        if (session != recognizer || !active || !listening) return;
        try {
            listener.state("Starting microphone · " + language, "thinking");
            session.startListening(recognitionIntent(language));
        } catch (RuntimeException failure) {
            cancelListening();
            listener.state(
                    "Speech could not start. Check the offline language setup in Settings.",
                    "idle");
        }
    }

    void inspectLanguages(java.util.function.Consumer<String> report) {
        try {
            inspectLanguagesSafely(report);
        } catch (RuntimeException unavailable) {
            cancelListening();
            report.accept(
                    "The speech provider could not check its languages. Open Android’s speech"
                            + " recognition settings.");
        }
    }

    private void inspectLanguagesSafely(java.util.function.Consumer<String> report) {
        stop();
        if (!onDevice() || Build.VERSION.SDK_INT < 33) {
            report.accept(
                    "This phone cannot list on-device language packs. Use Android’s speech"
                            + " recognition settings.");
            return;
        }
        recognizer = SpeechRecognizer.createOnDeviceSpeechRecognizer(activity);
        SpeechRecognizer session = recognizer;
        session.checkRecognitionSupport(
                recognitionIntent(language()),
                activity.getMainExecutor(),
                new RecognitionSupportCallback() {
                    public void onSupportResult(RecognitionSupport support) {
                        if (recognizer != session || !active) return;
                        String installed =
                                EnglishLanguages.choose(
                                        support.getInstalledOnDeviceLanguages(), language());
                        String supported =
                                EnglishLanguages.choose(
                                        support.getSupportedOnDeviceLanguages(), language());
                        if (installed != null)
                            prefs.values
                                    .edit()
                                    .putString("recognition_language", installed)
                                    .apply();
                        else if (supported != null)
                            prefs.values
                                    .edit()
                                    .putString("recognition_language", supported)
                                    .apply();
                        cancelListening();
                        report.accept(
                                "Installed on device: "
                                        + String.join(", ", support.getInstalledOnDeviceLanguages())
                                        + "\n"
                                        + (installed == null
                                                ? "No installed English pack. Download offline"
                                                        + " English below."
                                                : "Miu will use " + installed + " locally.")
                                        + "\nPending downloads: "
                                        + String.join(", ", support.getPendingOnDeviceLanguages()));
                    }

                    public void onError(int error) {
                        if (recognizer != session || !active) return;
                        cancelListening();
                        report.accept(
                                "The speech provider couldn’t list its languages (code "
                                        + error
                                        + "). Try Download offline English or open Android’s speech"
                                        + " settings.");
                    }
                });
        handler.postDelayed(
                () -> {
                    if (recognizer == session) {
                        cancelListening();
                        report.accept(
                                "The language check timed out. Try Android’s speech recognition"
                                        + " settings.");
                    }
                },
                15000);
    }

    void downloadOfflineEnglish(java.util.function.Consumer<String> report) {
        try {
            downloadOfflineEnglishSafely(report);
        } catch (RuntimeException unavailable) {
            cancelListening();
            report.accept(
                    "The speech provider could not request a download. Open Android’s speech"
                            + " recognition settings.");
        }
    }

    private void downloadOfflineEnglishSafely(java.util.function.Consumer<String> report) {
        stop();
        if (!onDevice() || Build.VERSION.SDK_INT < 33) {
            report.accept(
                    "This phone requires language downloads through Android’s speech settings.");
            return;
        }
        recognizer = SpeechRecognizer.createOnDeviceSpeechRecognizer(activity);
        SpeechRecognizer session = recognizer;
        report.accept(
                "Requesting the " + language() + " offline pack from Android’s speech provider…");
        if (Build.VERSION.SDK_INT >= 34)
            session.triggerModelDownload(
                    recognitionIntent(language()),
                    activity.getMainExecutor(),
                    new ModelDownloadListener() {
                        private boolean current() {
                            return recognizer == session && active;
                        }

                        public void onProgress(int progress) {
                            if (current())
                                report.accept(
                                        "Downloading offline English: "
                                                + progress
                                                + "%. Keep Miu open.");
                        }

                        public void onSuccess() {
                            if (current()) {
                                cancelListening();
                                report.accept(
                                        "Offline English is ready. Return to Talk and tap Mic.");
                            }
                        }

                        public void onScheduled() {
                            if (current()) {
                                cancelListening();
                                report.accept(
                                        "Android scheduled the language download. Connect to Wi-Fi,"
                                            + " then use Check offline English to see when it is"
                                            + " installed.");
                            }
                        }

                        public void onError(int error) {
                            if (current()) {
                                cancelListening();
                                report.accept(
                                        "Android couldn’t download the language pack (code "
                                                + error
                                                + "). Check your connection and download English"
                                                + " through Speech recognition settings.");
                            }
                        }
                    });
        else {
            session.triggerModelDownload(recognitionIntent(language()));
            report.accept(
                    "Download requested from Android. Use Check offline English after the provider"
                            + " finishes.");
        }
        handler.postDelayed(
                () -> {
                    if (recognizer == session) {
                        cancelListening();
                        report.accept(
                                "Download status is no longer available. Android may continue its"
                                        + " download; use Check offline English.");
                    }
                },
                120000);
    }

    void speak(String text) {
        stop();
        if (!active || !prefs.voice()) return;
        int limit = 16000;
        pendingText =
                text.length() > limit ? text.substring(0, limit) + ". More is shown in Miu." : text;
        if (tts == null) {
            listener.state("Preparing offline voice…", "thinking");
            prepareVoice();
        } else if (ready) playPending();
        else
            listener.state(
                    "Install an English offline voice in Android’s text-to-speech settings.",
                    "idle");
    }

    private void playPending() {
        if (!active || !ready || tts == null || pendingText == null) return;
        String spoken = pendingText;
        pendingText = null;
        tts.setSpeechRate(prefs.rate());
        List<String> chunks =
                SpeechChunks.split(
                        spoken, Math.min(3000, TextToSpeech.getMaxSpeechInputLength() - 1));
        for (int i = 0; i < chunks.size(); i++) {
            String id = generation + (i == chunks.size() - 1 ? "/end" : "/part-" + i);
            if (tts.speak(
                            chunks.get(i),
                            i == 0 ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD,
                            null,
                            id)
                    == TextToSpeech.ERROR) {
                stop();
                releaseVoice();
                listener.state("Voice is unavailable. Your reply is shown below.", "idle");
                break;
            }
        }
    }

    void stop() {
        generation++;
        pendingText = null;
        cancelListening();
        releaseVoice();
    }

    void cancelListening() {
        handler.removeCallbacks(timeout);
        listening = false;
        if (recognizer != null) {
            recognizer.cancel();
            recognizer.destroy();
            recognizer = null;
        }
    }

    void destroy() {
        active = false;
        stop();
        handler.removeCallbacksAndMessages(null);
        releaseVoice();
    }

    private void releaseVoice() {
        engineGeneration++;
        ready = false;
        pendingText = null;
        if (tts != null) {
            tts.shutdown();
            tts = null;
        }
    }

    public void onReadyForSpeech(Bundle p) {
        if (listening)
            listener.state(
                    onDevice()
                            ? "Listening on device · speak now"
                            : "Listening · Android speech service",
                    "listening");
    }

    public void onBeginningOfSpeech() {}

    public void onRmsChanged(float rms) {}

    public void onBufferReceived(byte[] data) {}

    public void onEndOfSpeech() {
        if (listening) listener.state("Understanding…", "thinking");
    }

    public void onError(int error) {
        if (!listening) return;
        cancelListening();
        String message;
        switch (error) {
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                message = "Microphone permission is off. Enable it in Miu’s app permissions.";
                break;
            case SpeechRecognizer.ERROR_AUDIO:
                message = "Microphone is busy. Close the other recording app and try again.";
                break;
            case SpeechRecognizer.ERROR_NETWORK:
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                message =
                        "The speech provider needs a connection. Download its offline English pack"
                                + " or type here.";
                break;
            case SpeechRecognizer.ERROR_LANGUAGE_UNAVAILABLE:
            case SpeechRecognizer.ERROR_LANGUAGE_NOT_SUPPORTED:
                message =
                        "Offline English is unavailable. Open Miu Settings → Download offline"
                                + " English.";
                break;
            case SpeechRecognizer.ERROR_NO_MATCH:
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                message = "I didn’t catch that. Tap the mic and speak after Listening appears.";
                break;
            default:
                message =
                        "Speech service is not ready. Try again in a moment, or type your message.";
        }
        listener.state(message, "idle");
    }

    public void onResults(Bundle results) {
        if (!listening) return;
        ArrayList<String> words = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
        float[] confidence = results.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES);
        cancelListening();
        if (words == null
                || words.isEmpty()
                || words.get(0) == null
                || words.get(0).trim().isEmpty()) {
            listener.state("No speech recognized. Try again or type below.", "idle");
            return;
        }
        boolean uncertain =
                confidence != null
                        && confidence.length > 0
                        && confidence[0] >= 0
                        && confidence[0] < .55f;
        listener.heard(words.get(0), uncertain);
    }

    public void onPartialResults(Bundle partial) {}

    public void onEvent(int type, Bundle params) {}
}
