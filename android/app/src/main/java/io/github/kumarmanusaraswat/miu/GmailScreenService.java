package io.github.kumarmanusaraswat.miu;

import android.accessibilityservice.AccessibilityService;
import android.app.KeyguardManager;
import android.content.*;
import android.os.*;
import android.view.accessibility.*;

import java.util.*;

/** A bounded, user-started Gmail-only screen reader. No network, storage, or continuous capture. */
public final class GmailScreenService extends AccessibilityService {
    private static final String GMAIL = "com.google.android.gm";
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final LinkedHashSet<String> chunks = new LinkedHashSet<>();
    private int stage, pages, attempts, bodyTextCount;
    private boolean scheduled;
    private GmailBridge.Request current;
    private final Runnable step =
            () -> {
                scheduled = false;
                try {
                    advance();
                } catch (RuntimeException unavailable) {
                    finish(
                            "Gmail’s screen changed before I could finish. Open the message and ask"
                                    + " again.",
                            false);
                }
            };

    @Override
    protected void onServiceConnected() {
        GmailBridge.service = this;
    }

    @Override
    public void onDestroy() {
        cancel();
        if (GmailBridge.service == this) GmailBridge.service = null;
        super.onDestroy();
    }

    @Override
    public void onInterrupt() {
        GmailBridge.clear();
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        // The service is registered only for Gmail. Ignore every event while idle.
        if (current != null
                && GMAIL.contentEquals(
                        event.getPackageName() == null ? "" : event.getPackageName()))
            schedule(500);
    }

    void begin() {
        cancel();
        current = GmailBridge.request;
        stage = 0;
        pages = 0;
        attempts = 0;
        bodyTextCount = 0;
        schedule(700);
    }

    void cancel() {
        handler.removeCallbacksAndMessages(null);
        scheduled = false;
        current = null;
        chunks.clear();
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
        if (current == null || current != GmailBridge.request) {
            cancel();
            return;
        }
        if (locked()) {
            finish(
                    "Unlock your phone and ask me again. I don’t read Gmail while the phone is"
                            + " locked.",
                    false);
            return;
        }
        if (SystemClock.elapsedRealtime() - current.started > 30000) {
            finish(
                    "Gmail’s controls weren’t available. Open the email in Gmail yourself, return"
                            + " to Miu, and ask “read this email”. No preview was read as a full"
                            + " message.",
                    false);
            return;
        }
        AccessibilityNodeInfo root = getRootInActiveWindow();
        if (root == null
                || !GMAIL.contentEquals(
                        root.getPackageName() == null ? "" : root.getPackageName())) {
            schedule(800);
            return;
        }
        ArrayList<AccessibilityNodeInfo> nodes = new ArrayList<>();
        collect(root, nodes, 0);
        AccessibilityNodeInfo body = findBody(nodes);
        if (current.mode == GmailBridge.Mode.OPEN_MESSAGE || stage >= 3) {
            if (body == null) {
                if (++attempts > 10)
                    finish(
                            "Open a message in Gmail first, then return to Miu and ask “read this"
                                    + " email”. I can read its accessible body, not Gmail’s private"
                                    + " storage.",
                            false);
                else schedule(750);
                return;
            }
            readPage(nodes, body);
            return;
        }
        if (stage == 0) {
            AccessibilityNodeInfo search =
                    find(
                            nodes,
                            n ->
                                    id(n).endsWith("/open_search")
                                            || id(n).endsWith("/search")
                                            || id(n).endsWith("/search_action_bar")
                                            || label(n).equalsIgnoreCase("Search mail")
                                            || label(n).equalsIgnoreCase("Search in mail"));
            if (search != null && click(search)) {
                stage = 1;
                attempts = 0;
                schedule(650);
                return;
            }
            // A message screen may hide search behind the Inbox. Only navigate back, never a mail
            // action.
            if (body != null && attempts++ < 2) {
                performGlobalAction(GLOBAL_ACTION_BACK);
                schedule(800);
                return;
            }
            if (++attempts > 8) {
                finish(
                        "I couldn’t find Gmail’s search control. Open the message you want, then"
                                + " ask “read this email”.",
                        false);
                return;
            }
            schedule(700);
            return;
        }
        if (stage == 1) {
            AccessibilityNodeInfo input = find(nodes, n -> n.isEditable() && n.isVisibleToUser());
            if (input == null) {
                if (++attempts > 8)
                    finish(
                            "Gmail didn’t expose a search field. Search in Gmail yourself, open the"
                                    + " email, then ask me to read it.",
                            false);
                else schedule(650);
                return;
            }
            Bundle args = new Bundle();
            args.putCharSequence(
                    AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE,
                    current.mode == GmailBridge.Mode.LATEST ? "in:inbox" : current.query);
            if (!input.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args)) {
                finish(
                        "I couldn’t type into Gmail’s search field. Please open the message"
                                + " yourself.",
                        false);
                return;
            }
            boolean submitted =
                    Build.VERSION.SDK_INT >= 30
                            && input.performAction(
                                    AccessibilityNodeInfo.AccessibilityAction.ACTION_IME_ENTER
                                            .getId());
            if (!submitted) {
                finish(
                        "Your Gmail version doesn’t support submitting searches through"
                            + " accessibility. Tap Search in Gmail, open a result, then ask “read"
                            + " this email”.",
                        false);
                return;
            }
            stage = 2;
            attempts = 0;
            schedule(1400);
            return;
        }
        if (stage == 2) {
            if (find(
                            nodes,
                            n ->
                                    label(n).equalsIgnoreCase("No results")
                                            || label(n).equalsIgnoreCase("No results found")
                                            || label(n).equalsIgnoreCase("No matches")
                                            || label(n).equalsIgnoreCase("No conversations found")
                                            || label(n).equalsIgnoreCase("No conversations found."))
                    != null) {
                finish("Gmail found no matching messages.", false);
                return;
            }
            // Only known conversation rows can be opened. Never click arbitrary links, buttons, or
            // text.
            AccessibilityNodeInfo row =
                    find(
                            nodes,
                            n ->
                                    (id(n).endsWith("/conversation_list_item")
                                                    || id(n).endsWith(
                                                                    "/viewified_conversation_item_view")
                                                    || id(n).endsWith(
                                                                    "/conversation_list_item_view")
                                                    || id(n).endsWith(
                                                                    "/conversation_list_item_layout"))
                                            && n.isVisibleToUser());
            if (row == null) {
                AccessibilityNodeInfo list =
                        find(nodes, n -> id(n).endsWith("/conversation_list_view"));
                if (list != null)
                    for (int i = 0; i < list.getChildCount(); i++) {
                        AccessibilityNodeInfo child = list.getChild(i);
                        if (child != null
                                && child.isClickable()
                                && child.isVisibleToUser()
                                && child.getChildCount() > 0) {
                            row = child;
                            break;
                        }
                    }
            }
            if (row != null && click(row)) {
                stage = 3;
                attempts = 0;
                schedule(1200);
                return;
            }
            if (++attempts > 8) {
                finish(
                        "Your search is open in Gmail. Tap the message you want, then return to Miu"
                                + " and say “read this email”.",
                        false);
                return;
            }
            schedule(700);
        }
    }

    private void readPage(List<AccessibilityNodeInfo> nodes, AccessibilityNodeInfo body) {
        ArrayList<AccessibilityNodeInfo> bodyNodes = new ArrayList<>();
        collect(body, bodyNodes, 0);
        if (pages == 0) {
            for (AccessibilityNodeInfo n : nodes) {
                String id = id(n);
                if (id.endsWith("/subject_and_folder_view")
                        || id.endsWith("/subject")
                        || id.endsWith("/sender_name")
                        || id.endsWith("/sender")) {
                    String text = text(n);
                    if (!text.isEmpty()) chunks.add(text);
                }
            }
        }
        for (AccessibilityNodeInfo n : bodyNodes) {
            String t = text(n);
            if (!t.isEmpty()) {
                bodyTextCount++;
                chunks.add(t.substring(0, Math.min(12000, t.length())));
            }
        }
        pages++;
        int length = chunks.stream().mapToInt(String::length).sum();
        AccessibilityNodeInfo scroll = find(bodyNodes, AccessibilityNodeInfo::isScrollable);
        if (scroll == null) {
            AccessibilityNodeInfo parent = body.getParent();
            for (int i = 0; i < 4 && parent != null; i++, parent = parent.getParent())
                if (parent.isScrollable()) {
                    scroll = parent;
                    break;
                }
        }
        if (pages < 6
                && length < 12000
                && scroll != null
                && scroll.performAction(AccessibilityNodeInfo.ACTION_SCROLL_FORWARD)) {
            schedule(650);
            return;
        }
        if (chunks.isEmpty() || bodyTextCount == 0) {
            finish(
                    "Gmail hasn’t exposed readable message text. Expand the message or open its"
                            + " plain-text content, then try again.",
                    false);
            return;
        }
        String joined = String.join("\n\n", chunks);
        boolean truncated = joined.length() > 12000 || pages >= 6;
        String result =
                "From the opened Gmail message:\n\n"
                        + joined.substring(0, Math.min(12000, joined.length()))
                        + "\n\n"
                        + (truncated
                                ? "Reading limit reached. More may remain in Gmail."
                                : "This is Gmail’s accessible message text. Collapsed, image-only,"
                                        + " or hidden content may be missing.");
        finish(result, true);
    }

    private void finish(String text, boolean email) {
        boolean assistant = current != null && current.assistant;
        GmailBridge.request = null;
        GmailBridge.result = new GmailBridge.Result(text, email);
        cancel();
        if (!locked()) {
            Intent intent =
                    new Intent(this, assistant ? AssistantActivity.class : MainActivity.class)
                            .addFlags(
                                    Intent.FLAG_ACTIVITY_NEW_TASK
                                            | Intent.FLAG_ACTIVITY_SINGLE_TOP
                                            | Intent.FLAG_ACTIVITY_CLEAR_TOP);
            try {
                startActivity(intent);
            } catch (RuntimeException ignored) {
                /* Result remains in memory until Miu is opened. */
            }
        }
    }

    private interface Match {
        boolean test(AccessibilityNodeInfo n);
    }

    private static AccessibilityNodeInfo find(List<AccessibilityNodeInfo> nodes, Match predicate) {
        for (AccessibilityNodeInfo n : nodes) if (predicate.test(n)) return n;
        return null;
    }

    private static String id(AccessibilityNodeInfo n) {
        return n.getViewIdResourceName() == null ? "" : n.getViewIdResourceName();
    }

    private static String label(AccessibilityNodeInfo n) {
        CharSequence s = n.getContentDescription();
        return s == null ? text(n) : s.toString().trim();
    }

    private static String text(AccessibilityNodeInfo n) {
        CharSequence t = n.getText();
        return t == null ? "" : t.toString().trim();
    }

    private static AccessibilityNodeInfo findBody(List<AccessibilityNodeInfo> nodes) {
        return find(
                nodes,
                n ->
                        "android.webkit.WebView"
                                        .contentEquals(
                                                n.getClassName() == null ? "" : n.getClassName())
                                || id(n).endsWith("/message_body"));
    }

    private static void collect(
            AccessibilityNodeInfo node, List<AccessibilityNodeInfo> result, int depth) {
        if (node == null || depth > 40 || result.size() >= 1800) return;
        result.add(node);
        for (int i = 0; i < node.getChildCount() && result.size() < 1800; i++)
            collect(node.getChild(i), result, depth + 1);
    }

    private static boolean click(AccessibilityNodeInfo n) {
        for (int i = 0; i < 4 && n != null; i++, n = n.getParent())
            if (n.isClickable() && n.isEnabled())
                return n.performAction(AccessibilityNodeInfo.ACTION_CLICK);
        return false;
    }
}
