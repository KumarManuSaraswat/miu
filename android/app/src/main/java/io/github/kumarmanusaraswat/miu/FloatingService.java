package io.github.kumarmanusaraswat.miu;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.graphics.PixelFormat;
import android.os.*;
import android.provider.Settings;
import android.view.*;

/** User-enabled companion only: no mic, networking, screen access, or boot auto-start. */
public final class FloatingService extends Service {
    static boolean running;
    private WindowManager windows;
    private MascotView face;
    private WindowManager.LayoutParams params;
    private boolean registered;
    private Prefs preferences;
    private final android.content.SharedPreferences.OnSharedPreferenceChangeListener appearance =
            (p, key) -> {
                if (face != null && ("mood".equals(key) || "reduced".equals(key))) {
                    face.setMood(preferences.mood());
                    face.setReduced(preferences.reduced());
                }
            };
    private final BroadcastReceiver screen =
            new BroadcastReceiver() {
                public void onReceive(Context c, Intent i) {
                    if (face != null)
                        face.setVisibility(
                                Intent.ACTION_SCREEN_OFF.equals(i.getAction())
                                                || ((KeyguardManager)
                                                                getSystemService(KEYGUARD_SERVICE))
                                                        .isKeyguardLocked()
                                        ? View.GONE
                                        : View.VISIBLE);
                }
            };

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && "STOP".equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (!Settings.canDrawOverlays(this)) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (face != null) return START_NOT_STICKY;
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(
                new NotificationChannel(
                        "companion", "Floating companion", NotificationManager.IMPORTANCE_LOW));
        PendingIntent open =
                PendingIntent.getActivity(
                        this,
                        1,
                        new Intent(this, AssistantActivity.class).setAction(Intent.ACTION_ASSIST),
                        PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        PendingIntent stop =
                PendingIntent.getService(
                        this,
                        2,
                        new Intent(this, FloatingService.class).setAction("STOP"),
                        PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        Notification notification =
                new Notification.Builder(this, "companion")
                        .setSmallIcon(io.github.kumarmanusaraswat.miu.R.drawable.ic_notification)
                        .setContentTitle("Miu is here")
                        .setContentText("Tap the mascot to talk · long-press to hide")
                        .setContentIntent(open)
                        .setOngoing(true)
                        .addAction(new Notification.Action.Builder(null, "Hide Miu", stop).build())
                        .build();
        if (Build.VERSION.SDK_INT >= 34)
            startForeground(7, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(7, notification);
        windows = (WindowManager) getSystemService(WINDOW_SERVICE);
        face = new MascotView(this);
        Prefs prefs = new Prefs(this);
        preferences = prefs;
        prefs.values.registerOnSharedPreferenceChangeListener(appearance);
        face.setMood(prefs.mood());
        face.setReduced(prefs.reduced());
        face.setContentDescription("Open Miu. Drag to move. Long-press to hide.");
        int size = (int) (88 * getResources().getDisplayMetrics().density);
        params =
                new WindowManager.LayoutParams(
                        size,
                        size,
                        WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                        WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                                | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
                        PixelFormat.TRANSLUCENT);
        params.gravity = Gravity.TOP | Gravity.START;
        params.x = 16;
        params.y = 220;
        face.setOnClickListener(v ->
                startActivity(
                        new Intent(this, AssistantActivity.class)
                                .setAction(Intent.ACTION_ASSIST)
                                .addFlags(
                                        Intent.FLAG_ACTIVITY_NEW_TASK
                                                | Intent.FLAG_ACTIVITY_SINGLE_TOP)));
        face.setOnLongClickListener(v -> {
            stopSelf();
            return true;
        });
        face.setOnTouchListener(
                new View.OnTouchListener() {
                    float x, y;
                    int px, py;
                    boolean moved;
                    final Handler h = new Handler(Looper.getMainLooper());
                    final Runnable hold =
                            () -> {
                                moved = true;
                                face.performLongClick();
                            };

                    public boolean onTouch(View v, android.view.MotionEvent e) {
                        switch (e.getActionMasked()) {
                            case MotionEvent.ACTION_DOWN:
                                x = e.getRawX();
                                y = e.getRawY();
                                px = params.x;
                                py = params.y;
                                moved = false;
                                h.postDelayed(hold, 600);
                                return true;
                            case MotionEvent.ACTION_MOVE:
                                float dx = e.getRawX() - x, dy = e.getRawY() - y;
                                if (Math.abs(dx) + Math.abs(dy) > 12) {
                                    moved = true;
                                    h.removeCallbacks(hold);
                                    params.x =
                                            Math.max(
                                                    0,
                                                    Math.min(
                                                            getResources()
                                                                            .getDisplayMetrics()
                                                                            .widthPixels
                                                                    - size,
                                                            px + (int) dx));
                                    params.y =
                                            Math.max(
                                                    0,
                                                    Math.min(
                                                            getResources()
                                                                            .getDisplayMetrics()
                                                                            .heightPixels
                                                                    - size,
                                                            py + (int) dy));
                                    if (face != null) windows.updateViewLayout(face, params);
                                }
                                return true;
                            case MotionEvent.ACTION_UP:
                                h.removeCallbacks(hold);
                                if (!moved) v.performClick();
                                return true;
                            case MotionEvent.ACTION_CANCEL:
                                h.removeCallbacks(hold);
                                return true;
                            default:
                                return false;
                        }
                    }
                });
        try {
            windows.addView(face, params);
            running = true;
        } catch (RuntimeException ex) {
            face = null;
            stopSelf();
            return START_NOT_STICKY;
        }
        IntentFilter filter = new IntentFilter();
        filter.addAction(Intent.ACTION_SCREEN_OFF);
        filter.addAction(Intent.ACTION_SCREEN_ON);
        filter.addAction(Intent.ACTION_USER_PRESENT);
        if (Build.VERSION.SDK_INT >= 33)
            registerReceiver(screen, filter, Context.RECEIVER_NOT_EXPORTED);
        else registerReceiver(screen, filter);
        registered = true;
        if (((KeyguardManager) getSystemService(KEYGUARD_SERVICE)).isKeyguardLocked())
            face.setVisibility(View.GONE);
        return START_NOT_STICKY;
    }

    @Override
    public void onDestroy() {
        running = false;
        if (preferences != null)
            preferences.values.unregisterOnSharedPreferenceChangeListener(appearance);
        if (registered) unregisterReceiver(screen);
        if (face != null) {
            windows.removeView(face);
            face = null;
        }
        stopForeground(STOP_FOREGROUND_REMOVE);
        super.onDestroy();
    }
}
