package io.github.kumarmanusaraswat.miu;

import android.app.Activity;
import android.content.*;
import android.media.AudioManager;
import android.os.*;
import android.provider.*;

import java.text.DateFormat;
import java.util.Date;

final class PhoneActions {
    static String execute(Activity a, CommandRouter.Command command, Prefs prefs) {
        switch (command.kind) {
            case TIME:
                return "It’s "
                        + DateFormat.getTimeInstance(DateFormat.SHORT).format(new Date())
                        + ".";
            case DATE:
                return "Today is "
                        + DateFormat.getDateInstance(DateFormat.FULL).format(new Date())
                        + ".";
            case BATTERY:
                BatteryManager battery =
                        (BatteryManager) a.getSystemService(Context.BATTERY_SERVICE);
                int level = battery.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY);
                return level >= 0 && level <= 100
                        ? "Your battery is at " + level + " percent."
                        : "This phone hasn’t reported its battery level.";
            case GREETING:
                return "Hi, I’m Miu. What can I help you with?";
            case THANKS:
                return "You’re welcome. I’m right here.";
            case HELP:
                return "Try “what time is it”, “battery level”, “open calculator”, “set a timer for"
                        + " 5 minutes”, or “note buy milk”.\n\n"
                        + "For Gmail: “read this email”, “read the latest email”, or “find email"
                        + " from Alex”. Enable Gmail access in Settings first.\n\n"
                        + "For WhatsApp: open a chat, then ask “read WhatsApp messages” or “send"
                        + " WhatsApp message saying hello”. Sending always shows the chat and"
                        + " text for review.\n\n"
                        + "You can also ask “how are you”, “tell me a joke”, or “what is 6 times"
                        + " 7”. Explore my 24 reactions in Looks. These everyday skills work"
                        + " locally.";
            case NOTE:
                String existing = prefs.values.getString("notes", "");
                String updated = command.argument + "\n" + existing;
                prefs.values
                        .edit()
                        .putString("notes", updated.substring(0, Math.min(12000, updated.length())))
                        .apply();
                return "Saved that note on this phone.";
            case NOTES:
                String notes = prefs.values.getString("notes", "");
                return notes.isEmpty() ? "No notes yet. Try “note buy milk”." : notes;
            case VOLUME_UP:
            case VOLUME_DOWN:
                ((AudioManager) a.getSystemService(Context.AUDIO_SERVICE))
                        .adjustStreamVolume(
                                AudioManager.STREAM_MUSIC,
                                command.kind == CommandRouter.Kind.VOLUME_UP
                                        ? AudioManager.ADJUST_RAISE
                                        : AudioManager.ADJUST_LOWER,
                                AudioManager.FLAG_SHOW_UI);
                return "Media volume adjusted.";
            case TIMER:
                return launch(
                        a,
                        new Intent(AlarmClock.ACTION_SET_TIMER)
                                .putExtra(
                                        AlarmClock.EXTRA_LENGTH, Integer.parseInt(command.argument))
                                .putExtra(AlarmClock.EXTRA_MESSAGE, "Miu timer")
                                .putExtra(AlarmClock.EXTRA_SKIP_UI, false),
                        "Confirm the timer in your Clock app.");
            case OPEN:
                return open(a, command.argument);
            default:
                return null;
        }
    }

    private static String open(Activity a, String name) {
        Intent intent = null;
        String[] packages = null;
        switch (name) {
            case "gmail":
                packages = new String[] {"com.google.android.gm"};
                break;
            case "whatsapp":
                packages = new String[] {"com.whatsapp"};
                break;
            case "youtube":
                packages = new String[] {"com.google.android.youtube"};
                break;
            case "maps":
                packages = new String[] {"com.google.android.apps.maps"};
                break;
            case "spotify":
                packages = new String[] {"com.spotify.music"};
                break;
            case "calculator":
                packages =
                        new String[] {
                            "com.google.android.calculator",
                            "com.miui.calculator",
                            "com.sec.android.app.popupcalculator",
                            "com.android.calculator2"
                        };
                break;
            case "browser":
            case "chrome":
                intent =
                        new Intent(
                                Intent.ACTION_VIEW,
                                android.net.Uri.parse("https://www.google.com"));
                break;
            case "camera":
                intent = new Intent(MediaStore.INTENT_ACTION_STILL_IMAGE_CAMERA);
                break;
            case "clock":
                intent = new Intent(AlarmClock.ACTION_SHOW_ALARMS);
                break;
            case "phone":
                intent = new Intent(Intent.ACTION_DIAL);
                break;
            case "downloads":
                intent = new Intent(android.app.DownloadManager.ACTION_VIEW_DOWNLOADS);
                break;
            case "wifi settings":
            case "wi-fi settings":
                intent = new Intent(Settings.ACTION_WIFI_SETTINGS);
                break;
            case "bluetooth settings":
                intent = new Intent(Settings.ACTION_BLUETOOTH_SETTINGS);
                break;
            case "phone settings":
                intent = new Intent(Settings.ACTION_SETTINGS);
                break;
        }
        if (packages != null)
            for (String p : packages) {
                intent = a.getPackageManager().getLaunchIntentForPackage(p);
                if (intent != null) break;
            }
        if (intent == null) return "I couldn’t find " + name + " on this phone.";
        return launch(a, intent, "Opened " + name + ".");
    }

    static String launch(Activity a, Intent intent, String message) {
        try {
            a.startActivity(intent);
            return message;
        } catch (ActivityNotFoundException | SecurityException ex) {
            return "No available app could handle that action on this phone.";
        }
    }
}
