package io.github.kumarmanusaraswat.miu;

import android.content.Context;
import android.graphics.*;
import android.view.View;

import java.util.HashMap;
import java.util.regex.*;

/**
 * Small vector companion. Idle motion is scheduled in short bursts, never a continuous frame loop.
 */
public final class MascotView extends View {
    private final Paint paint = new Paint(3);
    private final LinearGradient bodyGradient =
            new LinearGradient(25, 45, 162, 158, 0xff535560, 0xff25262e, Shader.TileMode.CLAMP);
    private final HashMap<String, Path> paths = new HashMap<>();
    private String mood = "idle";
    private boolean reduced;
    private boolean talking;
    private int frame;
    private long origin = android.os.SystemClock.uptimeMillis(), reactionStart = origin;
    private final Runnable animate =
            new Runnable() {
                public void run() {
                    if (!canMove()) return;
                    frame++;
                    invalidate();
                    long now = android.os.SystemClock.uptimeMillis();
                    postDelayed(
                            this,
                            Motion.delay(now - origin, animated() || now - reactionStart < 1400));
                }
            };

    public MascotView(Context context) {
        super(context);
        setContentDescription("Miu, your little companion");
        setLayerType(View.LAYER_TYPE_SOFTWARE, null);
    }

    public void setMood(String value) {
        String resolved = Expressions.resolve(value);
        String next = resolved == null ? "idle" : resolved;
        if (mood.equals(next)) return;
        mood = next;
        reactionStart = android.os.SystemClock.uptimeMillis();
        setContentDescription("Miu: " + Expressions.label(mood));
        removeCallbacks(animate);
        invalidate();
        if (canMove()) postDelayed(animate, 40);
    }

    public String getMood() {
        return mood;
    }

    public void setTalking(boolean value) {
        if (talking == value) return;
        talking = value;
        scheduleMotion();
        invalidate();
    }

    public boolean isTalking() {
        return talking;
    }

    @Override
    public boolean performClick() {
        return super.performClick();
    }

    public void setReduced(boolean value) {
        reduced = value;
        scheduleMotion();
        invalidate();
    }

    private boolean animated() {
        return talking
                || mood.equals("speaking")
                || mood.equals("listening")
                || mood.equals("thinking");
    }

    private boolean canMove() {
        return !reduced
                && isAttachedToWindow()
                && isShown()
                && getWindowVisibility() == VISIBLE
                && android.animation.ValueAnimator.areAnimatorsEnabled();
    }

    private void scheduleMotion() {
        if (animate == null) return;
        removeCallbacks(animate);
        if (canMove()) postDelayed(animate, 40);
    }

    @Override
    protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        origin = android.os.SystemClock.uptimeMillis();
        reactionStart = origin;
        scheduleMotion();
    }

    @Override
    protected void onWindowVisibilityChanged(int visibility) {
        super.onWindowVisibilityChanged(visibility);
        scheduleMotion();
    }

    @Override
    protected void onVisibilityChanged(View changed, int visibility) {
        super.onVisibilityChanged(changed, visibility);
        scheduleMotion();
    }

    @Override
    protected void onDetachedFromWindow() {
        removeCallbacks(animate);
        super.onDetachedFromWindow();
    }

    private void style(int color, float width) {
        paint.setShader(null);
        paint.setColor(color);
        paint.setStrokeWidth(width);
        paint.setStyle(width == 0 ? Paint.Style.FILL : Paint.Style.STROKE);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setStrokeJoin(Paint.Join.ROUND);
    }

    private void round(Canvas c, float l, float t, float r, float b, float radius, int color) {
        style(color, 0);
        c.drawRoundRect(l, t, r, b, radius, radius, paint);
    }

    private void eye(Canvas c, float x, float y, float w, float h) {
        round(c, x - w / 2, y - h / 2, x + w / 2, y + h / 2, w / 2, 0xfff5f5fa);
    }

    private void dot(Canvas c, float x, float y, float radius) {
        style(0xfff5f5fa, 0);
        c.drawCircle(x, y, radius, paint);
    }

    private void rings(Canvas c, float radius) {
        style(0xfff5f5fa, 3.3f);
        c.drawCircle(64, 95, radius, paint);
        c.drawCircle(124, 95, radius, paint);
    }

    private Path path(String data) {
        Path known = paths.get(data);
        if (known != null) return known;
        Matcher m = Pattern.compile("[MLCQZ]|-?\\d+(?:\\.\\d+)?").matcher(data);
        java.util.ArrayList<String> tokens = new java.util.ArrayList<>();
        while (m.find()) tokens.add(m.group());
        Path p = new Path();
        int i = 0;
        while (i < tokens.size()) {
            String op = tokens.get(i++);
            int count = op.equals("C") ? 6 : op.equals("Q") ? 4 : op.equals("Z") ? 0 : 2;
            float[] v = new float[count];
            for (int j = 0; j < count; j++) v[j] = Float.parseFloat(tokens.get(i++));
            switch (op) {
                case "M":
                    p.moveTo(v[0], v[1]);
                    break;
                case "L":
                    p.lineTo(v[0], v[1]);
                    break;
                case "C":
                    p.cubicTo(v[0], v[1], v[2], v[3], v[4], v[5]);
                    break;
                case "Q":
                    p.quadTo(v[0], v[1], v[2], v[3]);
                    break;
                case "Z":
                    p.close();
                    break;
                default:
                    throw new IllegalArgumentException("Unsupported path");
            }
        }
        paths.put(data, p);
        return p;
    }

    private void ink(Canvas c, String data) {
        style(0xfff5f5fa, 3.3f);
        c.drawPath(path(data), paint);
    }

    private void fill(Canvas c, String data) {
        style(0xfff5f5fa, 0);
        c.drawPath(path(data), paint);
    }

    @Override
    protected void onDraw(Canvas c) {
        super.onDraw(c);
        float s = Math.min(getWidth() / 180f, getHeight() / 180f);
        c.save();
        c.translate((getWidth() - 180 * s) / 2, (getHeight() - 180 * s) / 2);
        c.scale(s, s);
        long now = android.os.SystemClock.uptimeMillis(), elapsed = now - origin;
        boolean moving = !reduced && android.animation.ValueAnimator.areAnimatorsEnabled();
        float reaction =
                moving
                        ? (float) Math.sin(Math.PI * Math.min(1, (now - reactionStart) / 1400.0))
                        : 0;
        float lift = moving ? Motion.bob(elapsed) : 0;
        boolean lively =
                mood.equals("delighted")
                        || mood.equals("joyful")
                        || mood.equals("playful")
                        || mood.equals("excited");
        c.translate(0, lift - (lively ? 6 : 2) * reaction);
        c.rotate(
                reaction
                        * (mood.equals("sad")
                                ? 5
                                : mood.equals("blushing")
                                        ? -6
                                        : mood.equals("skeptical") ? 6 : 2.5f),
                94,
                130);
        c.scale(1 + .025f * reaction, 1 - .03f * reaction, 94, 145);
        if (moving && (mood.equals("scrunched") || mood.equals("annoyed")))
            c.rotate((float) Math.sin((now - reactionStart) / 85.0) * 3 * reaction, 94, 110);
        style(0x160d0e14, 0);
        c.drawOval(43, 160, 149, 170, paint);
        style(0xff989aa6, 12);
        c.drawPath(path("M 25,49 C 29,28 42,17 60,14"), paint);
        style(0x33ffffff, 2);
        c.drawPath(path("M 23,47 C 28,27 42,17 58,15"), paint);
        style(0xff33343d, 0);
        paint.setShader(bodyGradient);
        c.drawRoundRect(26, 45, 162, 158, 36, 36, paint);
        style(0xff60626d, .8f);
        c.drawRoundRect(26, 45, 162, 158, 36, 36, paint);
        style(0x20ffffff, .7f);
        c.drawRoundRect(29, 48, 159, 155, 33, 33, paint);
        round(c, 34, 53, 154, 148, 29, 0xff0c0d12);
        style(0xff50525a, .8f);
        c.drawRoundRect(34, 53, 154, 148, 29, 29, paint);
        style(0x18ffffff, 2);
        c.drawPath(path("M 49,65 C 66,57 117,57 138,64"), paint);
        style(0xff858795, 0);
        c.drawCircle(94, 153, 1.4f, paint);
        String face = Expressions.face(mood);
        c.save();
        c.translate(moving ? Motion.glance(elapsed) : 0, 0);
        c.save();
        c.scale(
                1 + (lively ? .1f : .04f) * reaction,
                (moving ? Motion.blink(elapsed) : 1) * (1 + .14f * reaction),
                94,
                95);
        eyes(c, face);
        c.restore();
        c.save();
        if (moving && (mood.equals("crying") || mood.equals("shocked")))
            c.translate(0, 4 * reaction);
        accents(c, face);
        c.restore();
        boolean speaking = talking || mood.equals("speaking");
        if (speaking
                && (face.equals("cheerful")
                        || face.equals("idle")
                        || face.equals("content")
                        || face.equals("neutral"))) {
            float h = moving ? 2 + (float) Math.abs(Math.sin(now / 110.0)) * 4.5f : 3;
            eye(c, 94, 117, 12, h);
        } else {
            c.save();
            c.scale(1 + .12f * reaction, 1 + .16f * reaction, 94, 117);
            if (speaking && moving)
                c.scale(1, .75f + .25f * (float) Math.sin(now / 110.0), 94, 117);
            mouth(c, face);
            c.restore();
        }
        if (mood.equals("listening")) {
            for (int i = 0; i < 5; i++) {
                float h = reduced ? 4 : 3 + (float) Math.abs(Math.sin((frame + i) * .8)) * 8;
                style(0xfff5f5fa, 2.7f);
                c.drawLine(82 + i * 6, 123 - h / 2, 82 + i * 6, 123 + h / 2, paint);
            }
        }
        c.restore();
        c.restore();
    }

    private void eyes(Canvas c, String e) {
        switch (e) {
            case "idle":
            case "listening":
                eye(c, 64, 95, 12, 25);
                eye(c, 124, 95, 12, 25);
                break;
            case "cheerful":
                eye(c, 64, 95, 10, 19);
                eye(c, 124, 95, 10, 19);
                break;
            case "content":
            case "kiss":
            case "crying":
                ink(c, "M 52,93 L 76,93 M 112,93 L 136,93");
                break;
            case "neutral":
                dot(c, 64, 96, 10);
                dot(c, 124, 96, 10);
                break;
            case "playful":
                fill(c, "M 52,98 C 52,80 76,80 76,98 Z M 112,98 C 112,80 136,80 136,98 Z");
                break;
            case "wailing":
                ink(
                        c,
                        "M 57,82 C 42,93 53,109 67,104 C 73,102 77,99 77,95 M 131,82 C 146,93"
                                + " 135,109 121,104 C 115,102 111,99 111,95");
                break;
            case "mischievous":
            case "smug_kiss":
                fill(
                        c,
                        "M 52,84 L 78,104 C 57,117 45,99 52,84 Z M 136,84 L 110,104 C 131,117"
                                + " 143,99 136,84 Z");
                break;
            case "sassy":
                ink(
                        c,
                        "M 51,93 L 77,93 C 75,113 54,113 51,93 Z M 111,93 L 137,93 C 135,113"
                                + " 114,113 111,93 Z");
                break;
            case "wide_eyed":
            case "surprised_kiss":
            case "shocked":
                rings(c, 11.8f);
                break;
            case "joyful":
                rings(c, 6.5f);
                break;
            case "sad":
                dot(c, 64, 102, 5.5f);
                dot(c, 124, 102, 5.5f);
                break;
            case "annoyed":
                ink(
                        c,
                        "M 51,93 L 77,93 Q 74,108 54,101 Z M 111,93 L 137,93 Q 134,108 114,101 Z M"
                                + " 64,94 L 64,101 M 124,94 L 124,101");
                break;
            case "scrunched":
                ink(
                        c,
                        "M 54,88 L 74,97 L 54,106 M 52,96 L 70,97 M 134,88 L 114,97 L 134,106 M"
                                + " 136,96 L 118,97");
                break;
            case "dizzy":
                ink(
                        c,
                        "M 72,101 C 61,109 49,101 53,91 C 57,81 72,85 72,94 C 73,101 59,102 60,95 C"
                                + " 61,91 67,94 65,97 M 132,101 C 121,109 109,101 113,91 C 117,81"
                                + " 132,85 132,94 C 133,101 119,102 120,95 C 121,91 127,94 125,97");
                break;
            case "blushing":
                ink(
                        c,
                        "M 53,91 L 76,91 L 55,97 L 75,97 L 54,103 L 75,103 M 113,91 L 136,91 L"
                                + " 115,97 L 135,97 L 114,103 L 135,103");
                break;
            case "drooling":
                ink(
                        c,
                        "M 51,87 L 78,87 M 58,88 L 58,108 M 70,88 L 70,108 M 110,87 L 137,87 M"
                                + " 118,88 L 118,108 M 130,88 L 130,108");
                break;
            case "delighted":
            case "amazed":
                dot(c, 64, 94, 7.2f);
                dot(c, 124, 94, 7.2f);
                break;
            case "skeptical":
                ink(
                        c,
                        "M 52,96 L 77,96 Q 74,111 56,103 Z M 111,96 L 136,96 Q 132,110 115,104 Z M"
                                + " 64,97 L 64,102 M 124,97 L 124,102");
                break;
            case "furious":
                ink(
                        c,
                        "M 51,84 L 78,104 C 56,111 49,102 51,84 Z M 137,84 L 110,104 C 132,111"
                                + " 139,102 137,84 Z");
                break;
            case "thinking":
                eye(c, 65, 94, 11, 22);
                eye(c, 124, 98, 11, 13);
                break;
            case "wink":
                dot(c, 64, 95, 7);
                ink(c, "M 116,88 L 128,96 L 116,103");
                break;
            default:
                eye(c, 64, 95, 12, 25);
                eye(c, 124, 95, 12, 25);
        }
    }

    private void accents(Canvas c, String e) {
        switch (e) {
            case "crying":
            case "shocked":
                ink(
                        c,
                        "M 59,112 C 58,119 51,126 55,131 C 59,137 66,133 64,127 Z M 129,112 C"
                                + " 128,119 121,126 125,131 C 129,137 136,133 134,127 Z");
                break;
            case "sad":
                ink(c, "M 54,82 C 55,89 62,93 73,91 M 115,91 C 126,93 133,89 134,82");
                break;
            case "annoyed":
                ink(
                        c,
                        "M 135,72 Q 136,79 142,79 M 148,72 Q 147,79 142,79 M 135,86 Q 136,80 142,80"
                                + " M 148,86 Q 147,80 142,80");
                break;
            case "skeptical":
                ink(c, "M 56,85 L 72,85 M 116,85 L 132,85");
                break;
        }
    }

    private void mouth(Canvas c, String e) {
        switch (e) {
            case "kiss":
            case "smug_kiss":
            case "surprised_kiss":
                ink(c, "M 89,105 C 100,99 105,112 95,114 C 105,116 101,131 90,125");
                break;
            case "crying":
                ink(c, "M 83,109 C 84,92 104,92 105,109 Z");
                break;
            case "neutral":
                ink(c, "M 82,110 L 106,110");
                break;
            case "playful":
                ink(
                        c,
                        "M 79,108 Q 86,108 86,115 M 109,108 Q 102,108 102,115 M 84,115 L 104,115 M"
                                + " 88,115 L 88,127 C 89,137 100,137 100,127 L 100,115 M 94,116 L"
                                + " 94,128");
                break;
            case "wailing":
                ink(
                        c,
                        "M 78,117 C 71,106 85,102 94,103 C 104,102 117,109 110,119 C 105,126"
                                + " 100,114 94,120 C 88,115 83,125 78,117 Z");
                break;
            case "sassy":
                ink(
                        c,
                        "M 81,115 Q 87,121 94,115 Q 101,121 107,115 M 89,120 C 88,135 101,135"
                                + " 100,120");
                break;
            case "shocked":
                ink(c, "M 84,127 C 84,101 104,101 104,127 Z");
                break;
            case "joyful":
                ink(
                        c,
                        "M 82,108 Q 86,117 94,116 Q 103,117 106,108 M 86,114 L 86,123 C 87,134"
                                + " 101,134 102,123 L 102,114");
                break;
            case "sad":
                ink(c, "M 84,126 C 85,112 103,112 104,126");
                break;
            case "annoyed":
                style(0xfff5f5fa, 3.3f);
                c.drawRoundRect(80, 115, 108, 125, 5, 5, paint);
                break;
            case "scrunched":
                ink(c, "M 83,112 Q 90,118 83,124 M 105,112 Q 98,118 105,124 M 88,118 L 100,118");
                break;
            case "dizzy":
                ink(c, "M 79,122 L 83,113 L 87,122 L 91,113 L 95,122 L 99,113 L 103,122 L 107,113");
                break;
            case "blushing":
                ink(c, "M 82,124 Q 93,130 102,120 M 102,113 Q 98,123 110,122");
                break;
            case "cheerful":
                ink(c, "M 86,116 Q 94,122 102,116");
                break;
            case "drooling":
                ink(
                        c,
                        "M 82,107 C 72,109 74,120 83,120 L 93,120 L 93,129 C 94,135 102,135 103,128"
                                + " L 103,121 C 116,117 113,106 105,106 Z");
                break;
            case "delighted":
                ink(c, "M 82,111 Q 88,116 94,112 Q 100,116 106,111 C 109,126 79,130 82,111 Z");
                break;
            case "skeptical":
            case "thinking":
                ink(c, "M 85,127 L 103,120");
                break;
            case "furious":
                ink(c, "M 94,111 L 101,129 L 87,129 Z");
                break;
            case "amazed":
                ink(c, "M 80,129 L 80,109 L 87,118 L 94,108 L 101,118 L 108,109 L 108,129 Z");
                break;
            case "wink":
                ink(c, "M 82,116 Q 94,128 106,116");
                break;
            case "listening":
                break;
            default:
                ink(c, "M 81,115 Q 87,123 94,115 Q 101,123 107,115");
        }
    }
}
