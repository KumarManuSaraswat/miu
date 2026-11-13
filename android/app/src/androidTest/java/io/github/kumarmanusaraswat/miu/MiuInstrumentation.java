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

    private void check(boolean condition, String reason) {
        if (!condition) throw new AssertionError(reason);
        checks++;
    }

    @Override
    public void onCreate(Bundle args) {
        super.onCreate(args);
        online = args != null && "true".equals(args.getString("online"));
        start();
    }

    @Override
    public void onStart() {
        Bundle result = new Bundle();
        Prefs prefs = new Prefs(getTargetContext());
        boolean voice = prefs.voice();
        try {
            prefs.values.edit().putBoolean("voice", false).putBoolean("cloud", false).commit();
            activity =
                    (MainActivity)
                            startActivitySync(
                                    new Intent(getTargetContext(), MainActivity.class)
                                            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            waitForIdleSync();
            runOnMainSync(() -> {
                check(
                        findText(activity.getWindow().getDecorView(), "Hello, you.") != null,
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
            prefs.values.edit().putBoolean("voice", voice).apply();
        }
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
