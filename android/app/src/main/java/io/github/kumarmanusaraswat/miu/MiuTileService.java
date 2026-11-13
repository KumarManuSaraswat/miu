package io.github.kumarmanusaraswat.miu;

import android.app.PendingIntent;
import android.content.Intent;
import android.os.Build;
import android.service.quicksettings.TileService;

public final class MiuTileService extends TileService {
    @android.annotation.SuppressLint(
            "StartActivityAndCollapseDeprecated") // The PendingIntent overload exists only on API
    // 34+.
    @Override
    public void onClick() {
        super.onClick();
        unlockAndRun(() -> {
            Intent intent =
                    new Intent(this, AssistantActivity.class)
                            .setAction(Intent.ACTION_ASSIST)
                            .addFlags(
                                    Intent.FLAG_ACTIVITY_NEW_TASK
                                            | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            if (Build.VERSION.SDK_INT >= 34)
                startActivityAndCollapse(
                        PendingIntent.getActivity(
                                this,
                                3,
                                intent,
                                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT));
            else startActivityAndCollapse(intent);
        });
    }
}
