package io.github.kumarmanusaraswat.miu;

import android.app.*;
import android.content.*;
import android.graphics.*;
import android.os.Bundle;
import android.view.*;
import android.widget.*;

import java.util.*;

/** Real-device smoke tests without third-party runners or access to other apps. */
public final class MiuInstrumentation extends Instrumentation {
    private int checks;
    private MainActivity activity;
    private boolean online;
    private boolean modelChecks;

    private void check(boolean condition, String reason) {
        if (!condition) throw new AssertionError(reason);
        checks++;
    }

    @Override
    public void onCreate(Bundle args) {
        super.onCreate(args);
        online = args != null && "true".equals(args.getString("online"));
        modelChecks = args != null && "true".equals(args.getString("model"));
        start();
    }

    @Override
    public void onStart() {
        Bundle result = new Bundle();
        Prefs prefs = new Prefs(getTargetContext());
        boolean voice = prefs.voice();
        boolean assistantAutoListen = prefs.values.getBoolean("assistant_auto_listen", false);
        boolean reviewSpeech = prefs.values.getBoolean("review_uncertain_speech", false);
        try {
            prefs.values
                    .edit()
                    .putBoolean("voice", false)
                    .putBoolean("cloud", false)
                    .putBoolean("assistant_auto_listen", false)
                    .putBoolean("review_uncertain_speech", false)
                    .commit();
            activity =
                    (MainActivity)
                            startActivitySync(
                                    new Intent(getTargetContext(), MainActivity.class)
                                            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            waitForIdleSync();
            runOnMainSync(() -> {
                check(
                        findText(activity.getWindow().getDecorView(), "Hey, I’m Miu.") != null,
                        "Home title missing");
                check(activity.findViewById(R.id.message_input) != null, "Composer missing");
                check(
                        activity.findViewById(R.id.microphone).getHeight()
                                >= 48 * activity.getResources().getDisplayMetrics().density,
                        "Mic touch target too small");
                activity.submit("a what time is it");
            });
            waitForIdleSync();
            runOnMainSync(() ->
                    check(
                            findContains(activity.getWindow().getDecorView(), "It’s ") != null,
                            "Clock command went to AI"));
            runOnMainSync(() -> activity.submit("battery level"));
            waitForIdleSync();
            runOnMainSync(() ->
                    check(
                            findContains(activity.getWindow().getDecorView(), "Your battery is at")
                                    != null,
                            "Battery reply missing"));
            runOnMainSync(() -> {
                Button clear = (Button) findText(activity.getWindow().getDecorView(), "Clear");
                clear.performClick();
                activity.submit("show reactions");
            });
            waitForIdleSync();
            runOnMainSync(() -> {
                check(
                        findText(activity.getWindow().getDecorView(), "A face for every feeling.")
                                != null,
                        "Looks screen missing");
                for (String mood : Expressions.FACES) {
                    MascotView face = new MascotView(activity);
                    face.setReduced(true);
                    face.setMood(mood);
                    face.layout(0, 0, 180, 180);
                    Bitmap bitmap = Bitmap.createBitmap(180, 180, Bitmap.Config.ARGB_8888);
                    face.draw(new Canvas(bitmap));
                    check(
                            bitmap.getPixel(94, 70) != Color.TRANSPARENT,
                            "Expression did not draw: " + mood);
                    bitmap.recycle();
                }
                MascotView speaking = new MascotView(activity);
                speaking.setMood("speaking");
                speaking.layout(0, 0, 180, 180);
                Bitmap b = Bitmap.createBitmap(180, 180, Bitmap.Config.ARGB_8888);
                speaking.draw(new Canvas(b));
                check(
                        (b.getPixel(64, 95) & 0xffffff) > 0xeeeeee,
                        "Speaking eyes changed unexpectedly");
                b.recycle();
            });
            prefs.saveKey("local-instrumentation-placeholder");
            check(
                    prefs.readKey().equals("local-instrumentation-placeholder"),
                    "Keystore round-trip failed");
            check(
                    !prefs.values.getString("api_key", "").contains("placeholder"),
                    "API key was saved as plaintext");
            prefs.saveKey("");
            check(!prefs.hasKey(), "Key removal failed");
            String fixture =
                    "{\"schemaVersion\":1,\"presets\":[{\"id\":\"test\",\"name\":\"Test\",\"description\":\"Synthetic"
                        + " fixture\",\"mood\":\"content\",\"speechRate\":0,\"reducedMotion\":true}]}";
            check(Network.parseCatalog(fixture).size() == 1, "Valid preset rejected");
            for (String invalid :
                    new String[] {
                        fixture.replace("content", "execute-command"),
                        fixture.replace("\"speechRate\":0", "\"speechRate\":99"),
                        fixture.replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                        fixture.replace("\"reducedMotion\":true", "\"reducedMotion\":\"true\"")
                    }) {
                boolean rejected = false;
                try {
                    Network.parseCatalog(invalid);
                } catch (Exception expected) {
                    rejected = true;
                }
                check(rejected, "Unsafe or incompatible preset accepted");
            }
            if (online) check(Network.catalog().size() >= 1, "Public catalog returned no presets");
            runOnMainSync(() -> {
                activity.submit("settings");
            });
            waitForIdleSync();
            runOnMainSync(() ->
                    check(
                            findText(activity.getWindow().getDecorView(), "Make yourself at home.")
                                    != null,
                            "Settings screen missing"));
            runOnMainSync(() -> {
                Button talk = (Button) findText(activity.getWindow().getDecorView(), "Talk");
                talk.performClick();
                Button clear = (Button) findText(activity.getWindow().getDecorView(), "Clear");
                clear.performClick();
            });
            android.view.accessibility.AccessibilityNodeInfo entry =
                    android.view.accessibility.AccessibilityNodeInfo.obtain();
            entry.setEditable(true);
            entry.setText("Message");
            entry.setHintText("Message");
            entry.setShowingHintText(true);
            check(
                    WhatsAppScreenService.text(entry).isEmpty(),
                    "Empty editor hint was treated as a draft");
            entry.setShowingHintText(false);
            check(
                    WhatsAppScreenService.text(entry).equals("Message"),
                    "A real draft matching the hint was discarded");
            entry.setText("Keep this draft");
            check(
                    WhatsAppScreenService.text(entry).equals("Keep this draft"),
                    "Existing draft changed");
            entry.setText("Message");
            entry.setTextSelection(0, 0);
            android.view.accessibility.AccessibilityNodeInfo voiceControl =
                    android.view.accessibility.AccessibilityNodeInfo.obtain();
            voiceControl.setViewIdResourceName("com.whatsapp:id/voice_note_btn");
            voiceControl.setVisibleToUser(true);
            voiceControl.setEnabled(true);
            ArrayList<android.view.accessibility.AccessibilityNodeInfo> composerNodes =
                    new ArrayList<>(Arrays.asList(entry, voiceControl));
            check(
                    WhatsAppScreenService.composerText(entry, composerNodes).isEmpty(),
                    "WhatsApp's unflagged empty placeholder was treated as a draft");
            android.view.accessibility.AccessibilityNodeInfo sendControl =
                    android.view.accessibility.AccessibilityNodeInfo.obtain();
            sendControl.setViewIdResourceName("com.whatsapp:id/send");
            sendControl.setVisibleToUser(true);
            composerNodes.add(sendControl);
            check(
                    WhatsAppScreenService.composerText(entry, composerNodes).equals("Message"),
                    "Real placeholder-named draft with Send was discarded");
            composerNodes.remove(sendControl);
            entry.setTextSelection(7, 7);
            check(
                    WhatsAppScreenService.composerText(entry, composerNodes).equals("Message"),
                    "An editor with actual text selection was treated as empty");
            entry.setTextSelection(0, 0);
            composerNodes.remove(voiceControl);
            check(
                    WhatsAppScreenService.composerText(entry, composerNodes).equals("Message"),
                    "Unknown composer state was treated as empty");
            entry.setText("  ");
            check(
                    WhatsAppScreenService.composerText(entry, composerNodes).equals("  "),
                    "An existing whitespace draft was discarded");
            voiceControl.recycle();
            sendControl.recycle();
            entry.recycle();
            Intent assist =
                    new Intent(Intent.ACTION_ASSIST)
                            .setPackage(getTargetContext().getPackageName());
            android.content.pm.ResolveInfo resolved =
                    getTargetContext().getPackageManager().resolveActivity(assist, 0);
            check(
                    resolved != null
                            && resolved.activityInfo.name.equals(AssistantActivity.class.getName()),
                    "Android assist does not resolve to the compact surface");
            MainActivity compact =
                    (MainActivity)
                            startActivitySync(
                                    new Intent(getTargetContext(), AssistantActivity.class)
                                            .setAction(Intent.ACTION_ASSIST)
                                            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            waitForIdleSync();
            runOnMainSync(() -> {
                View view = compact.getWindow().getDecorView();
                check(
                        findText(view, "Talk") == null && findText(view, "Settings") == null,
                        "Full app navigation leaked into assistant panel");
                check(
                        !compact.findViewById(R.id.message_input).isShown(),
                        "Assistant unexpectedly opened the keyboard composer");
                check(
                        compact.findViewById(R.id.microphone).getHeight()
                                >= 48 * compact.getResources().getDisplayMetrics().density,
                        "Assistant microphone target too small");
                check(
                        (compact.getWindow().getAttributes().gravity
                                        & Gravity.VERTICAL_GRAVITY_MASK)
                                == Gravity.BOTTOM,
                        "Assistant panel is not anchored at bottom");
                compact.submit("a what time is it");
            });
            waitForIdleSync();
            runOnMainSync(() -> {
                check(
                        findContains(compact.getWindow().getDecorView(), "It’s ") != null,
                        "Compact assistant did not handle a local command");
                check(
                        findText(compact.getWindow().getDecorView(), "Open app") != null,
                        "Assistant has no explicit full-app route");
                findText(compact.getWindow().getDecorView(), "Type").performClick();
            });
            waitForIdleSync();
            runOnMainSync(() ->
                    check(
                            compact.findViewById(R.id.message_input).isShown()
                                    && compact.findViewById(R.id.message_input).hasFocus(),
                            "Assistant typing field is not ready"));
            if (android.os.Build.VERSION.SDK_INT >= 30) {
                boolean[] keyboard = {false};
                for (int attempt = 0; attempt < 30 && !keyboard[0]; attempt++) {
                    android.os.SystemClock.sleep(100);
                    runOnMainSync(() -> {
                        WindowInsets insets =
                                compact.getWindow().getDecorView().getRootWindowInsets();
                        keyboard[0] = insets != null && insets.isVisible(WindowInsets.Type.ime());
                    });
                }
                check(keyboard[0], "Assistant Type button did not open the keyboard");
            }
            runOnMainSync(() -> {
                compact.finish();
            });
            waitForIdleSync();
            for (boolean cold : new boolean[] {false, true}) {
                if (cold) {
                    runOnMainSync(() -> activity.finish());
                    waitForIdleSync();
                }
                ActivityMonitor monitor =
                        addMonitor(AssistantActivity.class.getName(), null, false);
                getTargetContext()
                        .startActivity(
                                new Intent(getTargetContext(), MainActivity.class)
                                        .setAction(Intent.ACTION_ASSIST)
                                        .addFlags(
                                                Intent.FLAG_ACTIVITY_NEW_TASK
                                                        | Intent.FLAG_ACTIVITY_CLEAR_TOP
                                                        | Intent.FLAG_ACTIVITY_SINGLE_TOP));
                Activity forwarded = waitForMonitorWithTimeout(monitor, 5000);
                removeMonitor(monitor);
                check(
                        forwarded instanceof AssistantActivity,
                        "Saved legacy assistant component did not open panel; cold=" + cold);
                runOnMainSync(forwarded::finish);
                waitForIdleSync();
            }
            if (modelChecks) runModelChecks();
            result.putString(
                    "stream",
                    "Miu device checks passed: " + checks + "; live catalog=" + online + "\n");
            finish(Activity.RESULT_OK, result);
        } catch (Throwable error) {
            result.putString(
                    "stream",
                    "Miu device check failed: "
                            + error.getClass().getSimpleName()
                            + ": "
                            + error.getMessage()
                            + "\n");
            finish(Activity.RESULT_CANCELED, result);
        } finally {
            prefs.values
                    .edit()
                    .putBoolean("voice", voice)
                    .putBoolean("assistant_auto_listen", assistantAutoListen)
                    .putBoolean("review_uncertain_speech", reviewSpeech)
                    .apply();
        }
    }

    private void runModelChecks() throws Exception {
        activity =
                (MainActivity)
                        startActivitySync(
                                new Intent(getTargetContext(), MainActivity.class)
                                        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
        waitForIdleSync();
        java.lang.reflect.Field field = MainActivity.class.getDeclaredField("localChat");
        field.setAccessible(true);
        LocalChat engine = (LocalChat) field.get(activity);
        check(engine.packaged(), "Ready-to-use build is missing its model/runtime");
        runOnMainSync(() -> activity.submit("Who built you?"));
        check(awaitText("Ancient Dragon", 5000), "Creator answer lost Miu's project lead");
        check(!engine.running(), "Creator answer should not need model inference");
        runOnMainSync(() -> activity.heard("what time is it", true));
        check(awaitText("It’s ", 5000), "Final speech still needs Send in Talk");
        Prefs speechPrefs = new Prefs(getTargetContext());
        speechPrefs.values.edit().putBoolean("review_uncertain_speech", true).commit();
        runOnMainSync(() -> activity.heard("Who built you?", true));
        runOnMainSync(() ->
                check(
                        findText(
                                        activity.getWindow().getDecorView(),
                                        "Please check what I heard, then tap Send.")
                                != null,
                        "Optional uncertain-speech review was ignored"));
        speechPrefs.values.edit().putBoolean("review_uncertain_speech", false).commit();
        runOnMainSync(() -> activity.submit("What is the capital city of France? Answer briefly."));
        check(
                awaitText("Paris", 120000),
                "Bundled model did not answer the first offline question");
        check(!engine.running(), "Model process retained after first answer");
        runOnMainSync(() -> activity.submit("Which country is that city in?"));
        check(awaitText("France", 90000), "Model follow-up lost recent conversation");
        runOnMainSync(() ->
                activity.submit("Write a very long story about a robot exploring a new planet."));
        long until = android.os.SystemClock.elapsedRealtime() + 12000;
        while (!engine.running() && android.os.SystemClock.elapsedRealtime() < until)
            Thread.sleep(100);
        check(engine.running(), "Cancellation test did not start the model process");
        runOnMainSync(() -> activity.submit("stop"));
        until = android.os.SystemClock.elapsedRealtime() + 5000;
        while (engine.running() && android.os.SystemClock.elapsedRealtime() < until)
            Thread.sleep(100);
        check(!engine.running(), "Stop left a model process running");
        runOnMainSync(() -> activity.submit("a what time is it"));
        check(awaitText("It’s ", 5000), "Clock stopped working after model cancellation");
        runOnMainSync(activity::finish);
        activity =
                (MainActivity)
                        startActivitySync(
                                new Intent(getTargetContext(), AssistantActivity.class)
                                        .setAction(Intent.ACTION_ASSIST)
                                        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
        waitForIdleSync();
        LocalChat compactEngine = (LocalChat) field.get(activity);
        runOnMainSync(() -> activity.heard("Who built you?", true));
        check(
                awaitText("Ancient Dragon", 5000),
                "Final speech still needs Send in the compact panel");
        runOnMainSync(() -> activity.submit("What is the capital city of France? Answer briefly."));
        check(awaitText("Paris", 90000), "Compact assistant did not answer using offline AI");
        check(!compactEngine.running(), "Compact assistant retained its model after replying");
        runOnMainSync(() ->
                activity.submit("Write a very long story about a robot exploring a new planet."));
        until = android.os.SystemClock.elapsedRealtime() + 12000;
        while (!compactEngine.running() && android.os.SystemClock.elapsedRealtime() < until)
            Thread.sleep(100);
        check(compactEngine.running(), "Background cancellation check never started");
        runOnMainSync(activity::finish);
        until = android.os.SystemClock.elapsedRealtime() + 5000;
        while (compactEngine.running() && android.os.SystemClock.elapsedRealtime() < until)
            Thread.sleep(100);
        check(!compactEngine.running(), "Closing the panel retained the model process");
    }

    private boolean awaitText(String text, long timeout) throws Exception {
        java.lang.reflect.Field field = MainActivity.class.getDeclaredField("conversation");
        field.setAccessible(true);
        long until = android.os.SystemClock.elapsedRealtime() + timeout;
        while (android.os.SystemClock.elapsedRealtime() < until) {
            final boolean[] found = {false};
            runOnMainSync(() -> {
                try {
                    ViewGroup messages = (ViewGroup) field.get(activity);
                    if (messages != null && messages.getChildCount() > 0) {
                        View last = messages.getChildAt(messages.getChildCount() - 1);
                        found[0] =
                                (activity.isAssistantSurface() || findText(last, "MIU") != null)
                                        && findContains(last, text) != null;
                    }
                } catch (IllegalAccessException ex) {
                    throw new AssertionError(ex);
                }
            });
            if (found[0]) return true;
            Thread.sleep(200);
        }
        return false;
    }

    private static TextView findText(View v, String text) {
        if (v instanceof TextView && ((TextView) v).getText().toString().equals(text))
            return (TextView) v;
        if (v instanceof ViewGroup)
            for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++) {
                TextView found = findText(((ViewGroup) v).getChildAt(i), text);
                if (found != null) return found;
            }
        return null;
    }

    private static TextView findContains(View v, String text) {
        if (v instanceof TextView && ((TextView) v).getText().toString().contains(text))
            return (TextView) v;
        if (v instanceof ViewGroup)
            for (int i = 0; i < ((ViewGroup) v).getChildCount(); i++) {
                TextView found = findContains(((ViewGroup) v).getChildAt(i), text);
                if (found != null) return found;
            }
        return null;
    }
}
