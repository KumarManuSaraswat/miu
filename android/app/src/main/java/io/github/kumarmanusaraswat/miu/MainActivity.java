package io.github.kumarmanusaraswat.miu;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.*;
import android.provider.Settings;
import android.text.InputType;
import android.view.*;
import android.view.inputmethod.InputMethodManager;
import android.widget.*;

import java.util.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity implements SpeechController.Listener {
    static final int INK = 0xff292934, MUTED = 0xff767684, ACCENT = 0xff746d9d, BG = 0xfff8f7fb;
    private Prefs prefs;
    private SpeechController speech;
    private final LocalCompanion companion = new LocalCompanion();
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private LinearLayout root, body, conversation, navigation;
    private View heroContainer;
    private TextView status;
    private EditText input;
    private MascotView mascot;
    private ScrollView chatScroll;
    private final ArrayList<Message> messages = new ArrayList<>();
    private String screen = "Talk";
    private boolean active, busy;
    private int networkGeneration;

    private static final class Message {
        final String who, text;
        final boolean sensitive;

        Message(String w, String t, boolean s) {
            who = w;
            text = t;
            sensitive = s;
        }
    }

    @Override
    public void onCreate(Bundle state) {
        super.onCreate(state);
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
        }
        if (Build.VERSION.SDK_INT >= 29) getWindow().setNavigationBarContrastEnforced(false);
        prefs = new Prefs(this);
        speech = new SpeechController(this, prefs, this);
        build();
        if (Build.VERSION.SDK_INT >= 30)
            root.post(() -> {
                WindowInsetsController bars = getWindow().getInsetsController();
                if (bars != null)
                    bars.setSystemBarsAppearance(
                            WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                                    | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS,
                            WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                                    | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS);
            });
    }

    int dp(float n) {
        return (int) (getResources().getDisplayMetrics().density * n + .5f);
    }

    private GradientDrawable shape(int color, int stroke, float radius) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(color);
        d.setCornerRadius(dp(radius));
        if (stroke != 0) d.setStroke(dp(1), stroke);
        return d;
    }

    private LinearLayout column() {
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        return l;
    }

    private TextView text(String value, float size, int color) {
        TextView t = new TextView(this);
        t.setText(value);
        t.setTextSize(size);
        t.setTextColor(color);
        t.setLineSpacing(dp(3), 1);
        return t;
    }

    private Button button(String label, Runnable action) {
        Button b = new Button(this);
        b.setText(label);
        b.setTextSize(14);
        b.setAllCaps(false);
        b.setTextColor(INK);
        b.setMinHeight(dp(48));
        b.setMinimumHeight(dp(48));
        b.setPadding(dp(14), dp(9), dp(14), dp(9));
        b.setBackground(shape(0xffeeedf4, 0, 16));
        b.setStateListAnimator(null);
        b.setElevation(0);
        b.setOnClickListener(v -> action.run());
        return b;
    }

    private void space(LinearLayout l, int size) {
        View v = new View(this);
        l.addView(v, new LinearLayout.LayoutParams(1, dp(size)));
    }

    private void build() {
        root = column();
        root.setBackgroundColor(BG);
        root.setPadding(dp(22), 0, dp(22), 0);
        root.setFitsSystemWindows(false);
        root.setOnApplyWindowInsetsListener((v, insets) -> {
            int top, bottom, left, right;
            if (Build.VERSION.SDK_INT >= 30) {
                android.graphics.Insets bars =
                        insets.getInsets(
                                WindowInsets.Type.systemBars()
                                        | WindowInsets.Type.displayCutout()
                                        | WindowInsets.Type.ime());
                top = bars.top;
                bottom = bars.bottom;
                left = bars.left;
                right = bars.right;
            } else {
                top = insets.getSystemWindowInsetTop();
                bottom = insets.getSystemWindowInsetBottom();
                left = insets.getSystemWindowInsetLeft();
                right = insets.getSystemWindowInsetRight();
            }
            root.setPadding(dp(22) + left, top, dp(22) + right, bottom);
            if (heroContainer != null && Build.VERSION.SDK_INT >= 30) {
                boolean compact =
                        insets.isVisible(WindowInsets.Type.ime())
                                || getResources().getConfiguration().orientation
                                        == android.content.res.Configuration.ORIENTATION_LANDSCAPE;
                heroContainer.setVisibility(compact ? View.GONE : View.VISIBLE);
            }
            return insets;
        });
        setContentView(root);
        LinearLayout header = new LinearLayout(this);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setPadding(0, dp(16), 0, dp(10));
        LinearLayout brand = column();
        TextView title = text("miu", 32, INK);
        title.setTypeface(Typeface.create("sans-serif-rounded", Typeface.BOLD));
        brand.addView(title);
        brand.addView(text("A little company. A little help.", 12, MUTED));
        header.addView(brand, new LinearLayout.LayoutParams(0, -2, 1));
        Button clear =
                button(
                        "Clear",
                        () -> {
                            speech.stop();
                            networkGeneration++;
                            busy = false;
                            GmailBridge.clear();
                            WhatsAppBridge.clear();
                            messages.clear();
                            updateSecure();
                            show("Talk");
                        });
        clear.setContentDescription("Clear conversation and any Gmail text from memory");
        header.addView(clear);
        root.addView(header);
        body = column();
        root.addView(body, new LinearLayout.LayoutParams(-1, 0, 1));
        LinearLayout nav = new LinearLayout(this);
        navigation = nav;
        nav.setPadding(0, dp(10), 0, dp(10));
        for (String tab : new String[] {"Talk", "Looks", "Settings"}) {
            Button b = button(tab, () -> show(tab));
            b.setTag(tab);
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, dp(50), 1);
            p.setMargins(dp(3), 0, dp(3), 0);
            nav.addView(b, p);
        }
        root.addView(nav);
        show("Talk");
    }

    private void show(String destination) {
        screen = destination;
        body.removeAllViews();
        mascot = null;
        status = null;
        input = null;
        conversation = null;
        chatScroll = null;
        heroContainer = null;
        if (navigation != null)
            for (int i = 0; i < navigation.getChildCount(); i++) {
                Button item = (Button) navigation.getChildAt(i);
                boolean selected = destination.equals(item.getTag());
                item.setSelected(selected);
                item.setTextColor(selected ? ACCENT : MUTED);
                item.setTypeface(null, selected ? Typeface.BOLD : Typeface.NORMAL);
                item.setBackground(shape(selected ? 0xffe9e4f3 : Color.TRANSPARENT, 0, 16));
            }
        if (destination.equals("Talk")) talk();
        else if (destination.equals("Looks")) looks();
        else settings();
    }

    private void talk() {
        LinearLayout hero = new LinearLayout(this);
        heroContainer = hero;
        if (getResources().getConfiguration().orientation
                == android.content.res.Configuration.ORIENTATION_LANDSCAPE)
            hero.setVisibility(View.GONE);
        hero.setGravity(Gravity.CENTER_VERTICAL);
        mascot = new MascotView(this);
        mascot.setReduced(prefs.reduced());
        mascot.setMood(prefs.mood());
        hero.addView(mascot, new LinearLayout.LayoutParams(dp(122), dp(128)));
        LinearLayout greeting = column();
        TextView heading = text(messages.isEmpty() ? "Hello, you." : "I’m right here.", 24, INK);
        heading.setTypeface(null, Typeface.BOLD);
        greeting.addView(heading);
        space(greeting, 6);
        status = text("Ready when you are", 13, MUTED);
        status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        greeting.addView(status);
        hero.addView(greeting, new LinearLayout.LayoutParams(0, -2, 1));
        body.addView(hero);
        chatScroll = new ScrollView(this);
        chatScroll.setFillViewport(true);
        conversation = column();
        conversation.setPadding(0, dp(6), 0, dp(12));
        chatScroll.addView(conversation);
        body.addView(chatScroll, new LinearLayout.LayoutParams(-1, 0, 1));
        renderMessages();
        LinearLayout actions = new LinearLayout(this);
        actions.setGravity(Gravity.CENTER_VERTICAL);
        Button stop =
                button(
                        "Stop",
                        () -> {
                            speech.stop();
                            networkGeneration++;
                            busy = false;
                            GmailBridge.clear();
                            WhatsAppBridge.clear();
                            state("Stopped · ready when you are", prefs.mood());
                        });
        actions.addView(stop);
        TextView hint = text("Tap the mic, then speak", 12, MUTED);
        hint.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        actions.addView(hint, new LinearLayout.LayoutParams(0, -1, 1));
        body.addView(actions);
        space(body, 8);
        LinearLayout composer = new LinearLayout(this);
        composer.setGravity(Gravity.CENTER_VERTICAL);
        composer.setPadding(dp(6), dp(4), dp(5), dp(4));
        composer.setBackground(shape(Color.WHITE, 0xffe5e3ed, 22));
        input = new EditText(this);
        input.setId(R.id.message_input);
        input.setSingleLine(true);
        input.setTextSize(16);
        input.setTextColor(INK);
        input.setHintTextColor(MUTED);
        input.setHint("Ask Miu something…");
        input.setBackgroundColor(Color.TRANSPARENT);
        input.setPadding(dp(12), dp(10), dp(6), dp(10));
        input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        input.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_SEND);
        input.setSaveEnabled(false);
        input.setFilters(
                new android.text.InputFilter[] {new android.text.InputFilter.LengthFilter(2000)});
        input.setOnEditorActionListener((v, action, event) -> {
            if (action == android.view.inputmethod.EditorInfo.IME_ACTION_SEND) {
                sendInput();
                return true;
            }
            return false;
        });
        composer.addView(input, new LinearLayout.LayoutParams(0, dp(52), 1));
        Button send = button("↑", this::sendInput);
        send.setTextSize(24);
        send.setContentDescription("Send message");
        composer.addView(send, new LinearLayout.LayoutParams(dp(48), dp(48)));
        Button mic = button("Mic", this::microphone);
        mic.setId(R.id.microphone);
        mic.setTextColor(Color.WHITE);
        mic.setBackground(shape(ACCENT, 0, 17));
        mic.setContentDescription("Start or stop listening");
        LinearLayout.LayoutParams mp = new LinearLayout.LayoutParams(dp(60), dp(48));
        mp.setMargins(dp(5), 0, 0, 0);
        composer.addView(mic, mp);
        body.addView(composer);
        TextView local =
                text(
                        "LOCAL COMPANION  ·  "
                                + (prefs.values.getBoolean("cloud", false)
                                        ? "AI CHAT ENABLED"
                                        : "NO ACCOUNT NEEDED"),
                        10,
                        MUTED);
        local.setPadding(dp(3), dp(10), 0, 0);
        body.addView(local);
    }

    private void renderMessages() {
        if (conversation == null) return;
        conversation.removeAllViews();
        if (messages.isEmpty()) {
            TextView prompt = text("Small things, made easier.", 19, INK);
            prompt.setTypeface(null, Typeface.BOLD);
            conversation.addView(prompt);
            space(conversation, 7);
            conversation.addView(text("Speak naturally, or start with one of these.", 14, MUTED));
            space(conversation, 18);
            for (String sample :
                    new String[] {
                        "What time is it?", "Read the latest email", "What can you do?"
                    }) {
                Button b = button(sample, () -> submit(sample));
                LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, -2);
                p.bottomMargin = dp(9);
                b.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
                conversation.addView(b, p);
            }
            return;
        }
        for (Message m : messages) {
            LinearLayout bubble = column();
            bubble.setPadding(dp(16), dp(12), dp(16), dp(14));
            bubble.setBackground(
                    shape(
                            m.who.equals("YOU") ? 0xffedeaf3 : Color.WHITE,
                            m.who.equals("YOU") ? 0 : 0xffe5e3ed,
                            20));
            TextView label = text(m.who + (m.sensitive ? " · PHONE ONLY" : ""), 10, MUTED);
            bubble.addView(label);
            space(bubble, 4);
            TextView content = text(m.text, 16, INK);
            content.setTextIsSelectable(!m.sensitive);
            bubble.addView(content);
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, -2);
            p.bottomMargin = dp(12);
            p.leftMargin = m.who.equals("YOU") ? dp(22) : 0;
            p.rightMargin = m.who.equals("YOU") ? 0 : dp(12);
            conversation.addView(bubble, p);
        }
        chatScroll.post(() -> {
            if (chatScroll != null) chatScroll.fullScroll(View.FOCUS_DOWN);
        });
    }

    private void add(String who, String value, boolean sensitive) {
        messages.add(new Message(who, value, sensitive));
        while (messages.size() > 24) messages.remove(0);
        updateSecure();
        renderMessages();
    }

    private void updateSecure() {
        boolean sensitive = false;
        for (Message m : messages) sensitive |= m.sensitive;
        if (sensitive) getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
        else getWindow().clearFlags(WindowManager.LayoutParams.FLAG_SECURE);
    }

    private void reply(String value, boolean sensitive) {
        add("MIU", value, sensitive);
        state("Ready when you are", prefs.mood());
        if (active) speech.speak(value);
    }

    private void sendInput() {
        if (input == null) return;
        String value = input.getText().toString().trim();
        if (value.isEmpty()) return;
        input.setText("");
        ((InputMethodManager) getSystemService(INPUT_METHOD_SERVICE))
                .hideSoftInputFromWindow(input.getWindowToken(), 0);
        submit(value);
    }

    void submit(String value) {
        if (value == null || value.trim().isEmpty()) return;
        if (!screen.equals("Talk")) show("Talk");
        speech.stop();
        networkGeneration++;
        busy = false;
        GmailBridge.clear();
        WhatsAppBridge.clear();
        add("YOU", value, false);
        CommandRouter.Command c = CommandRouter.parse(value);
        if (c.kind == CommandRouter.Kind.WHATSAPP_READ
                || c.kind == CommandRouter.Kind.WHATSAPP_SEND) {
            whatsapp(c);
            return;
        }
        if (c.kind == CommandRouter.Kind.STOP) {
            GmailBridge.clear();
            state("Stopped", prefs.mood());
            return;
        }
        if (c.kind == CommandRouter.Kind.GMAIL_READ
                || c.kind == CommandRouter.Kind.GMAIL_LATEST
                || c.kind == CommandRouter.Kind.GMAIL_SEARCH) {
            gmail(c);
            return;
        }
        if (c.kind == CommandRouter.Kind.SETTINGS) {
            show("Settings");
            return;
        }
        if (c.kind == CommandRouter.Kind.LOOKS) {
            show("Looks");
            return;
        }
        if (c.kind == CommandRouter.Kind.FACE) {
            prefs.values.edit().putString("mood", c.argument).apply();
            reply("How’s this? " + Expressions.label(c.argument) + ".", false);
            return;
        }
        String local = PhoneActions.execute(this, c, prefs);
        if (local != null) {
            reply(local, c.kind == CommandRouter.Kind.NOTES);
            return;
        }
        LocalCompanion.Reply company = companion.answer(c.argument);
        if (company != null) {
            reply(company.text, false);
            if (mascot != null) mascot.setMood(company.mood);
            return;
        }
        if (!prefs.values.getBoolean("cloud", false) || !prefs.hasKey()) {
            reply(LocalCompanion.fallback(), false);
            return;
        }
        int generation = ++networkGeneration;
        busy = true;
        state("Thinking…", "thinking");
        worker.execute(() -> {
            String answer;
            try {
                answer = Network.chat(c.argument, prefs);
            } catch (Exception ex) {
                answer =
                        ex instanceof java.io.IOException
                                ? ex.getMessage()
                                : "I couldn’t get a reply. Check the key, model, and connection in"
                                        + " Settings.";
            }
            final String result = answer;
            runOnUiThread(() -> {
                if (isDestroyed() || generation != networkGeneration) return;
                busy = false;
                reply(result, false);
            });
        });
    }

    private void microphone() {
        if (speech.isListening()) {
            speech.stop();
            state("Listening stopped", prefs.mood());
            return;
        }
        if (!speech.available()) {
            dialog(
                    "Speech isn’t installed",
                    "Install or enable an Android speech recognition service and its English"
                            + " language pack. You can always type to Miu.",
                    "Open voice settings",
                    () -> open(new Intent(Settings.ACTION_VOICE_INPUT_SETTINGS)));
            return;
        }
        if (!speech.onDevice() && !prefs.values.getBoolean("system_speech_consent", false)) {
            dialog(
                    "Use Android’s speech service?",
                    "This phone hasn’t exposed an on-device recognizer. Its speech provider may"
                            + " process microphone audio online, even when offline recognition is"
                            + " requested. Miu records only after you tap Mic and keeps no audio.",
                    "Use speech service",
                    () -> {
                        prefs.values.edit().putBoolean("system_speech_consent", true).apply();
                        microphone();
                    });
            return;
        }
        if (checkSelfPermission(Manifest.permission.RECORD_AUDIO)
                != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[] {Manifest.permission.RECORD_AUDIO}, 11);
            return;
        }
        networkGeneration++;
        busy = false;
        speech.listen();
    }

    @Override
    public void onRequestPermissionsResult(int request, String[] permissions, int[] grants) {
        super.onRequestPermissionsResult(request, permissions, grants);
        if (grants.length == 0 || grants[0] != PackageManager.PERMISSION_GRANTED) {
            state("Permission was not enabled. You can still type to Miu.", "idle");
            return;
        }
        if (request == 11)
            new Handler(Looper.getMainLooper())
                    .postDelayed(
                            () -> {
                                if (active) microphone();
                            },
                            250);
        if (request == 12) startFloating();
    }

    @Override
    public void state(String message, String mood) {
        if (status != null) status.setText(message);
        if (mascot != null) mascot.setMood(mood.equals("idle") ? prefs.mood() : mood);
    }

    @Override
    public void heard(String value, boolean uncertain) {
        if (!active) return;
        if (uncertain) {
            if (input != null) input.setText(value);
            state("Please check what I heard, then tap Send.", prefs.mood());
        } else submit(value);
    }

    private void gmail(CommandRouter.Command c) {
        if (!prefs.values.getBoolean("gmail_consent", false) || !GmailBridge.ready()) {
            gmailDisclosure();
            return;
        }
        Intent intent = getPackageManager().getLaunchIntentForPackage("com.google.android.gm");
        if (intent == null) {
            reply("Gmail isn’t installed on this phone.", false);
            return;
        }
        GmailBridge.Mode mode =
                c.kind == CommandRouter.Kind.GMAIL_READ
                        ? GmailBridge.Mode.OPEN_MESSAGE
                        : c.kind == CommandRouter.Kind.GMAIL_LATEST
                                ? GmailBridge.Mode.LATEST
                                : GmailBridge.Mode.SEARCH;
        GmailBridge.start(mode, c.argument);
        state("Opening Gmail for this request…", "thinking");
        try {
            startActivity(intent);
        } catch (RuntimeException ex) {
            GmailBridge.clear();
            reply("I couldn’t open Gmail. Try opening it yourself first.", false);
        }
    }

    private void gmailDisclosure() {
        dialog(
                "Read Gmail on this phone",
                "Miu can read the accessible body of an opened Gmail message and try to search or"
                    + " open the latest email, only after you ask. Opening an email may mark it as"
                    + " read.\n\n"
                    + "It cannot send, delete, archive, or read Gmail’s private storage. Hidden or"
                    + " collapsed content may be missing. Email text stays in memory on this phone"
                    + " and never goes to AI or the preset catalog.\n\n"
                    + "Enable “Miu · Read Gmail when asked” in Android Accessibility. This optional"
                    + " access can be disabled there anytime.",
                "Open Accessibility",
                () -> {
                    prefs.values.edit().putBoolean("gmail_consent", true).apply();
                    open(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS));
                });
    }

    private ScrollView scroller() {
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        body.addView(scroll, new LinearLayout.LayoutParams(-1, -1));
        return scroll;
    }

    private void looks() {
        ScrollView scroll = scroller();
        LinearLayout content = column();
        content.setPadding(0, dp(14), 0, dp(24));
        scroll.addView(content);
        TextView title = text("A face for every feeling.", 25, INK);
        title.setTypeface(null, Typeface.BOLD);
        content.addView(title);
        space(content, 6);
        content.addView(
                text(
                        "Choose Miu’s resting expression. Speaking stays soft and simple.",
                        14,
                        MUTED));
        space(content, 20);
        for (int row = 0; row < Expressions.FACES.length; row += 3) {
            LinearLayout line = new LinearLayout(this);
            for (int col = 0; col < 3 && row + col < Expressions.FACES.length; col++) {
                String mood = Expressions.FACES[row + col];
                LinearLayout tile = column();
                tile.setGravity(Gravity.CENTER);
                tile.setPadding(dp(4), dp(5), dp(4), dp(12));
                tile.setBackground(
                        shape(mood.equals(prefs.mood()) ? 0xffe8e3f4 : 0xfff0eef5, 0, 18));
                MascotView face = new MascotView(this);
                face.setReduced(prefs.reduced() || !mood.equals(prefs.mood()));
                face.setMood(mood);
                face.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
                tile.addView(face, new LinearLayout.LayoutParams(-1, dp(96)));
                TextView label = text(Expressions.label(mood), 11, INK);
                label.setGravity(Gravity.CENTER);
                tile.addView(label);
                tile.setContentDescription("Choose " + Expressions.label(mood) + " expression");
                tile.setFocusable(true);
                tile.setOnClickListener(v -> {
                    prefs.values.edit().putString("mood", mood).apply();
                    show("Looks");
                });
                LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, -2, 1);
                p.setMargins(dp(3), 0, dp(3), dp(8));
                line.addView(tile, p);
            }
            content.addView(line);
        }
        space(content, 16);
        content.addView(text("Community presets", 21, INK));
        space(content, 6);
        content.addView(
                text(
                        "Curated looks from our free Neon-backed catalog. Loading shares a normal"
                                + " web request, never your conversations or email.",
                        14,
                        MUTED));
        space(content, 12);
        Button load = button("Load community presets", () -> {});
        content.addView(load);
        load.setOnClickListener(v -> {
            load.setEnabled(false);
            load.setText("Loading…");
            worker.execute(() -> {
                try {
                    List<Network.Preset> presets = Network.catalog();
                    runOnUiThread(() -> {
                        if (isDestroyed()) return;
                        load.setText("Refresh community presets");
                        load.setEnabled(true);
                        LinearLayout previous = content.findViewWithTag("catalog");
                        if (previous != null) content.removeView(previous);
                        LinearLayout list = column();
                        list.setTag("catalog");
                        space(list, 12);
                        for (Network.Preset preset : presets) {
                            Button choice =
                                    button(
                                            preset.name + " · " + preset.description,
                                            () -> {
                                                prefs.values
                                                        .edit()
                                                        .putString("mood", preset.mood)
                                                        .putInt("rate", preset.rate)
                                                        .putBoolean("reduced", preset.reduced)
                                                        .apply();
                                                Toast.makeText(
                                                                this,
                                                                preset.name + " applied",
                                                                Toast.LENGTH_SHORT)
                                                        .show();
                                                show("Talk");
                                            });
                            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(-1, -2);
                            p.bottomMargin = dp(8);
                            list.addView(choice, p);
                        }
                        content.addView(list);
                    });
                } catch (Exception ex) {
                    runOnUiThread(() -> {
                        load.setText("Try loading again");
                        load.setEnabled(true);
                        Toast.makeText(
                                        this,
                                        "Catalog unavailable. All built-in faces still work.",
                                        Toast.LENGTH_LONG)
                                .show();
                    });
                }
            });
        });
    }

    private void settings() {
        ScrollView scroll = scroller();
        LinearLayout c = column();
        c.setPadding(0, dp(14), 0, dp(24));
        scroll.addView(c);
        TextView title = text("Make yourself at home.", 25, INK);
        title.setTypeface(null, Typeface.BOLD);
        c.addView(title);
        space(c, 6);
        c.addView(text("Useful on its own. Extra access is always your choice.", 14, MUTED));
        section(c, "Voice & motion");
        toggle(
                c,
                "Speak replies",
                prefs.voice(),
                v -> prefs.values.edit().putBoolean("voice", v).apply());
        toggle(
                c,
                "Reduce motion",
                prefs.reduced(),
                v -> prefs.values.edit().putBoolean("reduced", v).apply());
        c.addView(
                text(
                        speech.onDevice()
                                ? "Recognition: on-device English, when its language pack is"
                                        + " available."
                                : "Recognition: Android speech provider. It may require a"
                                        + " connection.",
                        13,
                        MUTED));
        space(c, 10);
        TextView languageStatus = text("Offline English setup", 13, MUTED);
        c.addView(languageStatus);
        c.addView(
                button(
                        "Check offline English",
                        () -> speech.inspectLanguages(languageStatus::setText)));
        space(c, 8);
        c.addView(
                button(
                        "Download offline English",
                        () ->
                                dialog(
                                        "Install an offline English pack?",
                                        "Android’s speech provider will download its English"
                                            + " recognition model. Use Wi-Fi if you want to avoid"
                                            + " mobile data usage. Download size and availability"
                                            + " depend on the provider. No Miu server, subscription"
                                            + " or API key is involved.",
                                        "Download",
                                        () ->
                                                speech.downloadOfflineEnglish(
                                                        languageStatus::setText))));
        space(c, 8);
        c.addView(
                button(
                        "Voice & language settings",
                        () -> open(new Intent("com.android.settings.TTS_SETTINGS"))));
        space(c, 8);
        c.addView(
                button(
                        "Speech recognition settings",
                        () -> open(new Intent(Settings.ACTION_VOICE_INPUT_SETTINGS))));
        section(c, "Gmail on your phone");
        c.addView(
                text(
                        GmailBridge.ready()
                                ? "Access enabled · idle until you ask"
                                : "Access off · optional",
                        14,
                        ACCENT));
        space(c, 7);
        c.addView(
                text(
                        "Read an opened email’s accessible body, or ask Miu to find a message."
                            + " Gmail’s interface and hidden content can limit what is available.",
                        14,
                        MUTED));
        space(c, 10);
        c.addView(button("Manage Gmail access", this::gmailDisclosure));
        section(c, "WhatsApp on your phone");
        c.addView(
                text(
                        WhatsAppBridge.ready()
                                ? "Access enabled · idle until you ask"
                                : "Access off · optional",
                        14,
                        ACCENT));
        space(c, 7);
        c.addView(
                text(
                        "Read the visible messages in an open chat, or send your own message after"
                                + " reviewing the chat name and text. No chat monitoring or"
                                + " AI-generated sends.",
                        14,
                        MUTED));
        space(c, 10);
        c.addView(button("Manage WhatsApp access", this::whatsappDisclosure));
        section(c, "Keep Miu nearby");
        c.addView(
                text(
                        "A movable mascot over other apps. No background listening. Long-press it"
                            + " or use its notification to hide it. You can also add Miu to Quick"
                            + " Settings.",
                        14,
                        MUTED));
        space(c, 10);
        c.addView(
                button(
                        FloatingService.running ? "Hide floating Miu" : "Show floating Miu",
                        () -> {
                            if (FloatingService.running) {
                                stopService(new Intent(this, FloatingService.class));
                                show("Settings");
                                return;
                            }
                            if (!Settings.canDrawOverlays(this)) {
                                dialog(
                                        "Let Miu float nearby",
                                        "Enable “Display over other apps” for Miu. Then return here"
                                            + " and tap Show floating Miu. This permission is only"
                                            + " for the small movable mascot.",
                                        "Open overlay settings",
                                        () ->
                                                open(
                                                        new Intent(
                                                                Settings
                                                                        .ACTION_MANAGE_OVERLAY_PERMISSION,
                                                                Uri.parse(
                                                                        "package:"
                                                                                + getPackageName()))));
                                return;
                            }
                            if (Build.VERSION.SDK_INT >= 33
                                    && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                                            != PackageManager.PERMISSION_GRANTED) {
                                requestPermissions(
                                        new String[] {Manifest.permission.POST_NOTIFICATIONS}, 12);
                                return;
                            }
                            startFloating();
                        }));
        section(c, "Optional AI conversation");
        c.addView(
                text(
                        "Local commands, voice and Gmail reading need no AI key. For open-ended"
                            + " chat, use your own Gemini key. Only the message you submit is sent"
                            + " to Google; Gmail results, notes and chat history are excluded. Free"
                            + " quotas and availability depend on your account. Keep billing"
                            + " disabled if you want a zero-cost setup.",
                        14,
                        MUTED));
        space(c, 10);
        EditText key = new EditText(this);
        key.setHint(
                prefs.hasKey() ? "Key saved securely · enter to replace" : "Your Gemini API key");
        key.setTextSize(14);
        key.setSingleLine(true);
        key.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        key.setSaveEnabled(false);
        key.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO);
        c.addView(key);
        EditText model = new EditText(this);
        model.setText(prefs.model());
        model.setTextSize(14);
        model.setSingleLine(true);
        model.setHint("Gemini model name");
        model.setContentDescription("Gemini model name");
        c.addView(model);
        space(c, 8);
        c.addView(
                button(
                        "Save AI settings",
                        () -> {
                            String name = model.getText().toString().trim();
                            if (!name.matches("gemini-[a-z0-9.-]{1,80}")) {
                                model.setError("Enter a valid Gemini model name");
                                return;
                            }
                            try {
                                if (!key.getText().toString().trim().isEmpty())
                                    prefs.saveKey(key.getText().toString());
                                prefs.values.edit().putString("model", name).apply();
                                key.setText("");
                                Toast.makeText(this, "Saved on this phone", Toast.LENGTH_SHORT)
                                        .show();
                                show("Settings");
                            } catch (Exception ex) {
                                Toast.makeText(
                                                this,
                                                "Couldn’t save the key securely. It was not"
                                                        + " enabled.",
                                                Toast.LENGTH_LONG)
                                        .show();
                            }
                        }));
        toggle(
                c,
                "Enable AI chat",
                prefs.values.getBoolean("cloud", false),
                value -> {
                    if (value && !prefs.hasKey()) {
                        Toast.makeText(this, "Save your own API key first", Toast.LENGTH_LONG)
                                .show();
                        show("Settings");
                        return;
                    }
                    prefs.values.edit().putBoolean("cloud", value).apply();
                });
        c.addView(
                button(
                        "Remove saved AI key",
                        () -> {
                            try {
                                prefs.saveKey("");
                                show("Settings");
                            } catch (Exception ignored) {
                                Toast.makeText(
                                                this,
                                                "Couldn’t remove the key. Try again.",
                                                Toast.LENGTH_LONG)
                                        .show();
                            }
                        }));
        section(c, "Your data, your phone");
        c.addView(
                text(
                        "No account is required. No analytics or audio recordings. Conversation and"
                            + " Gmail text are held only in memory. Notes stay in app storage; your"
                            + " AI key is encrypted with Android Keystore. Android backup is"
                            + " disabled. Clear removes the current conversation. Uninstalling"
                            + " removes app data.",
                        14,
                        MUTED));
        space(c, 10);
        c.addView(
                button(
                        "Delete saved notes",
                        () ->
                                dialog(
                                        "Delete all notes?",
                                        "This removes Miu’s locally saved notes from this phone.",
                                        "Delete notes",
                                        () -> {
                                            prefs.values.edit().remove("notes").apply();
                                            Toast.makeText(
                                                            this,
                                                            "Notes deleted",
                                                            Toast.LENGTH_SHORT)
                                                    .show();
                                        })));
        space(c, 8);
        c.addView(
                button(
                        "Project, source & privacy",
                        () ->
                                open(
                                        new Intent(
                                                Intent.ACTION_VIEW,
                                                Uri.parse(
                                                        "https://github.com/KumarManuSaraswat/miu/blob/main/android/README.md")))));
        space(c, 16);
        c.addView(
                text(
                        "Miu for Android · "
                                + BuildConfig.VERSION_NAME
                                + "\nOpen source · MIT license",
                        12,
                        MUTED));
    }

    private interface Toggle {
        void changed(boolean value);
    }

    private void toggle(LinearLayout c, String label, boolean value, Toggle action) {
        Switch s = new Switch(this);
        s.setText(label);
        s.setTextColor(INK);
        s.setTextSize(15);
        s.setMinHeight(dp(56));
        s.setChecked(value);
        s.setOnCheckedChangeListener((view, checked) -> action.changed(checked));
        c.addView(s, new LinearLayout.LayoutParams(-1, -2));
    }

    private void section(LinearLayout c, String name) {
        space(c, 26);
        TextView t = text(name, 20, INK);
        t.setTypeface(null, Typeface.BOLD);
        c.addView(t);
        space(c, 10);
    }

    private void startFloating() {
        try {
            startForegroundService(new Intent(this, FloatingService.class));
            Toast.makeText(this, "Miu is nearby. Long-press it to hide.", Toast.LENGTH_LONG).show();
            new Handler(Looper.getMainLooper())
                    .postDelayed(
                            () -> {
                                if (active && screen.equals("Settings")) show("Settings");
                            },
                            300);
        } catch (RuntimeException ex) {
            Toast.makeText(
                            this,
                            "Android couldn’t start the companion. Check overlay and notification"
                                    + " permissions.",
                            Toast.LENGTH_LONG)
                    .show();
        }
    }

    private void dialog(String title, String message, String positive, Runnable action) {
        new AlertDialog.Builder(this)
                .setTitle(title)
                .setMessage(message)
                .setPositiveButton(positive, (d, w) -> action.run())
                .setNegativeButton("Not now", null)
                .show();
    }

    private void open(Intent intent) {
        try {
            startActivity(intent);
        } catch (ActivityNotFoundException | SecurityException ex) {
            Toast.makeText(
                            this,
                            "That settings page isn’t available on this phone.",
                            Toast.LENGTH_LONG)
                    .show();
        }
    }

    private void whatsappDisclosure() {
        dialog(
                "WhatsApp, only when you ask",
                "Miu can read text visible in your open WhatsApp chat. Opening a chat may mark"
                    + " messages read. It cannot read hidden messages, images or voice notes.\n\n"
                    + "For sending, Miu shows the displayed chat name and your exact message here"
                    + " first. After you tap Send, it checks that chat and text again, then taps"
                    + " WhatsApp’s Send once. It won’t overwrite an existing draft, retry Send, or"
                    + " act on received messages or AI replies. Delivery depends on WhatsApp and"
                    + " its connection.\n\n"
                    + "Chat text stays in phone memory, never AI or Neon. Enable “Miu · WhatsApp"
                    + " when asked” in Accessibility. Disable it there anytime.",
                "Open Accessibility",
                () -> {
                    prefs.values.edit().putBoolean("whatsapp_consent", true).apply();
                    open(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS));
                });
    }

    private void whatsapp(CommandRouter.Command c) {
        WhatsAppDraft draft =
                c.kind == CommandRouter.Kind.WHATSAPP_SEND ? WhatsAppDraft.parse(c.argument) : null;
        if (c.kind == CommandRouter.Kind.WHATSAPP_SEND && draft == null) {
            reply(
                    "Open the intended chat in WhatsApp first. Then say “send WhatsApp message"
                            + " saying I’ll be late”, or type “send WhatsApp to +countrycodeNUMBER:"
                            + " your message”. I’ll show the chat name and text for review before"
                            + " sending.",
                    false);
            return;
        }
        if (!prefs.values.getBoolean("whatsapp_consent", false) || !WhatsAppBridge.ready()) {
            whatsappDisclosure();
            return;
        }
        if (getPackageManager().getLaunchIntentForPackage("com.whatsapp") == null) {
            reply(
                    "WhatsApp isn’t installed. This integration currently supports the standard"
                            + " WhatsApp app.",
                    false);
            return;
        }
        WhatsAppBridge.start(
                draft == null ? WhatsAppBridge.Mode.READ : WhatsAppBridge.Mode.PREVIEW,
                draft,
                null);
        launchWhatsApp(draft);
    }

    private void launchWhatsApp(WhatsAppDraft draft) {
        Intent intent;
        if (draft != null && !draft.number.isEmpty())
            intent =
                    new Intent(Intent.ACTION_VIEW, Uri.parse("https://wa.me/" + draft.number))
                            .setPackage("com.whatsapp");
        else intent = getPackageManager().getLaunchIntentForPackage("com.whatsapp");
        try {
            if (intent == null) throw new ActivityNotFoundException();
            startActivity(intent);
        } catch (RuntimeException ex) {
            WhatsAppBridge.clear();
            reply(
                    "I couldn’t open that chat. Open it in WhatsApp yourself, then ask again.",
                    false);
        }
    }

    private void reviewWhatsApp(WhatsAppBridge.Result result) {
        add("MIU", "Ready to review a message for “" + result.recipient + "”.", true);
        String details =
                "Chat shown in WhatsApp: "
                        + result.recipient
                        + (result.draft.number.isEmpty()
                                ? ""
                                : "\nRequested number: +" + result.draft.number)
                        + "\n\nMessage:\n"
                        + result.draft.text
                        + "\n\n"
                        + "Check that this is the intended chat. Send will hand this message to"
                        + " WhatsApp once.";
        new AlertDialog.Builder(this)
                .setTitle("Send this WhatsApp message?")
                .setMessage(details)
                .setPositiveButton(
                        "Send message",
                        (d, w) -> {
                            if (android.os.SystemClock.elapsedRealtime() - result.created > 60000) {
                                reply(
                                        "That review expired. Ask again so I can recheck the chat.",
                                        false);
                                return;
                            }
                            WhatsAppBridge.start(
                                    WhatsAppBridge.Mode.SEND, result.draft, result.recipient);
                            launchWhatsApp(result.draft);
                        })
                .setNegativeButton(
                        "Cancel",
                        (d, w) -> {
                            WhatsAppBridge.clear();
                            state("Message canceled. Nothing was sent.", prefs.mood());
                        })
                .setOnCancelListener(d -> WhatsAppBridge.clear())
                .show();
    }

    @Override
    protected void onResume() {
        super.onResume();
        active = true;
        speech.setActive(true);
        if (screen.equals("Settings")) show("Settings");
        GmailBridge.Result result = GmailBridge.take();
        if (result != null) {
            show("Talk");
            reply(result.text, result.email);
        }
        WhatsAppBridge.Result whatsapp = WhatsAppBridge.take();
        if (whatsapp != null) {
            show("Talk");
            if (whatsapp.draft != null) reviewWhatsApp(whatsapp);
            else reply(whatsapp.text, whatsapp.sensitive);
        }
    }

    @Override
    protected void onPause() {
        active = false;
        speech.setActive(false);
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        networkGeneration++;
        speech.destroy();
        worker.shutdownNow();
        messages.clear();
        super.onDestroy();
    }
}
