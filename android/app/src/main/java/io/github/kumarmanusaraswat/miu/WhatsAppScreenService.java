package io.github.kumarmanusaraswat.miu;

import android.accessibilityservice.AccessibilityService;
import android.app.KeyguardManager;
import android.content.Intent;
import android.os.*;
import android.view.accessibility.*;

import java.util.*;

/**
 * User-directed visible-chat reading and a single confirmed send. No chat monitoring or AI actions.
 */
public final class WhatsAppScreenService extends AccessibilityService {
    private final Handler handler = new Handler(Looper.getMainLooper());
    private WhatsAppBridge.Request current;
    private boolean scheduled, filled, attempted;
    private final Runnable step =
            () -> {
                scheduled = false;
                try {
                    advance();
                } catch (RuntimeException unavailable) {
                    finish(
                            attempted
                                    ? "WhatsApp’s screen changed during Send. Check the chat before"
                                            + " retrying; I won’t retry automatically."
                                    : filled
                                            ? "WhatsApp’s screen changed. A draft may remain;"
                                                    + " please review it there. I stopped without"
                                                    + " retrying."
                                            : "WhatsApp’s screen isn’t available. Open the intended"
                                                    + " chat and ask again. Nothing was sent.",
                            false,
                            null,
                            null);
                }
            };

    @Override
    protected void onServiceConnected() {
        WhatsAppBridge.service = this;
    }

    @Override
    public void onDestroy() {
        cancel();
        if (WhatsAppBridge.service == this) WhatsAppBridge.service = null;
        super.onDestroy();
    }

    @Override
    public void onInterrupt() {
        WhatsAppBridge.clear();
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        if (current != null
                && "com.whatsapp"
                        .contentEquals(
                                event.getPackageName() == null ? "" : event.getPackageName()))
            schedule(500);
    }

    void begin() {
        cancel();
        current = WhatsAppBridge.request;
        filled = false;
        attempted = false;
        schedule(700);
    }

    void cancel() {
        handler.removeCallbacksAndMessages(null);
        scheduled = false;
        current = null;
    }

    private void schedule(long delay) {
        if (current != null && !scheduled) {
            scheduled = true;
            handler.postDelayed(step, delay);
        }
    }

    private boolean locked() {
        return ((KeyguardManager) getSystemService(KEYGUARD_SERVICE)).isKeyguardLocked();
    }

    private void advance() {
        if (current == null || current != WhatsAppBridge.request) {
            cancel();
            return;
        }
        if (locked()) {
            finish("Unlock your phone and ask again. Nothing was sent.", false, null, null);
            return;
        }
        if (SystemClock.elapsedRealtime() - current.started > 18000) {
            finish(
                    "I couldn’t verify an open WhatsApp chat. Open the intended chat yourself,"
                            + " return to Miu and try again. Nothing was sent.",
                    false,
                    null,
                    null);
            return;
        }
        AccessibilityNodeInfo root = getRootInActiveWindow();
        if (root == null
                || !"com.whatsapp"
                        .contentEquals(
                                root.getPackageName() == null ? "" : root.getPackageName())) {
            schedule(700);
            return;
        }
        ArrayList<AccessibilityNodeInfo> nodes = new ArrayList<>();
        collect(root, nodes, 0);
        AccessibilityNodeInfo title = find(nodes, "conversation_contact_name"),
                entry = find(nodes, "entry");
        if (title == null) title = find(nodes, "conversation_title");
        if (title == null || text(title).isEmpty() || entry == null || !entry.isEditable()) {
            schedule(650);
            return;
        }
        String recipient = text(title);
        if (current.mode == WhatsAppBridge.Mode.READ) {
            ArrayList<String> messages = new ArrayList<>();
            for (AccessibilityNodeInfo n : nodes) {
                String id = id(n);
                if (n.isVisibleToUser()
                        && (id.equals("com.whatsapp:id/message_text")
                                || id.equals("com.whatsapp:id/caption"))) {
                    String t = text(n);
                    if (!t.isEmpty()) messages.add(t);
                }
            }
            if (messages.isEmpty()) {
                finish(
                        "The open chat has no accessible text messages on screen. Images, voice"
                                + " notes and hidden messages aren’t read.",
                        false,
                        null,
                        null);
                return;
            }
            String content = String.join("\n\n", messages);
            String bounded = content.substring(0, Math.min(8000, content.length()));
            finish(
                    "Visible messages in “"
                            + recipient
                            + "”:\n\n"
                            + bounded
                            + "\n\n"
                            + "Only text currently exposed on this chat screen is included. Sender"
                            + " and delivery labels may be unavailable.",
                    true,
                    null,
                    null);
            return;
        }
        if (current.mode == WhatsAppBridge.Mode.PREVIEW) {
            if (!text(entry).isEmpty()) {
                finish(
                        "This chat already has a draft. Send or clear it yourself first; I won’t"
                                + " overwrite it.",
                        false,
                        null,
                        null);
                return;
            }
            finish("Review before sending", true, recipient, current.draft);
            return;
        }
        // Confirmation binds the exact displayed chat title and message. Never retry a Send click.
        if (!recipient.equals(current.recipient)) {
            finish(
                    "The open chat changed. Nothing was sent. Ask again from the intended chat.",
                    false,
                    null,
                    null);
            return;
        }
        if (!filled) {
            if (!text(entry).isEmpty()) {
                finish(
                        "A draft appeared in the chat. I left it untouched and did not send"
                                + " anything.",
                        false,
                        null,
                        null);
                return;
            }
            Bundle args = new Bundle();
            args.putCharSequence(
                    AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE,
                    current.draft.text);
            if (!entry.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args)) {
                finish("WhatsApp didn’t accept the text. Nothing was sent.", false, null, null);
                return;
            }
            filled = true;
            schedule(600);
            return;
        }
        if (!text(entry).equals(current.draft.text)) {
            finish(
                    "The draft changed before sending. I stopped; please review it in WhatsApp.",
                    false,
                    null,
                    null);
            return;
        }
        AccessibilityNodeInfo send = find(nodes, "send");
        if (send == null || !send.isClickable() || !send.isEnabled() || !send.isVisibleToUser()) {
            finish(
                    "The message is drafted in WhatsApp. Its Send control wasn’t available; review"
                            + " and send it there yourself.",
                    false,
                    null,
                    null);
            return;
        }
        if (attempted) {
            cancel();
            return;
        }
        attempted = true;
        boolean clicked = send.performAction(AccessibilityNodeInfo.ACTION_CLICK);
        finish(
                clicked
                        ? "Handed your message to WhatsApp in “"
                                + recipient
                                + "”. Check the chat’s delivery indicator; WhatsApp needs a"
                                + " connection to deliver it."
                        : "WhatsApp didn’t confirm the Send action. Check the chat before trying"
                                + " again so you don’t send a duplicate.",
                true,
                null,
                null);
    }

    private void finish(String text, boolean sensitive, String recipient, WhatsAppDraft draft) {
        WhatsAppBridge.request = null;
        WhatsAppBridge.result = new WhatsAppBridge.Result(text, sensitive, recipient, draft);
        cancel();
        if (!locked())
            try {
                startActivity(
                        new Intent(this, MainActivity.class)
                                .addFlags(
                                        Intent.FLAG_ACTIVITY_NEW_TASK
                                                | Intent.FLAG_ACTIVITY_SINGLE_TOP
                                                | Intent.FLAG_ACTIVITY_CLEAR_TOP));
            } catch (RuntimeException ignored) {
            }
    }

    private static String text(AccessibilityNodeInfo n) {
        return n.getText() == null ? "" : n.getText().toString().trim();
    }

    private static String id(AccessibilityNodeInfo n) {
        return n.getViewIdResourceName() == null ? "" : n.getViewIdResourceName();
    }

    private static AccessibilityNodeInfo find(List<AccessibilityNodeInfo> nodes, String suffix) {
        for (AccessibilityNodeInfo n : nodes)
            if (id(n).equals("com.whatsapp:id/" + suffix) && n.isVisibleToUser()) return n;
        return null;
    }

    private static void collect(
            AccessibilityNodeInfo n, List<AccessibilityNodeInfo> nodes, int depth) {
        if (n == null || depth > 35 || nodes.size() > 1200) return;
        nodes.add(n);
        for (int i = 0; i < n.getChildCount() && nodes.size() < 1200; i++)
            collect(n.getChild(i), nodes, depth + 1);
    }
}
