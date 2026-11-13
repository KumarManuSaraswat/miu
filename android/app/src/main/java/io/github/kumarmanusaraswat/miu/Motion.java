package io.github.kumarmanusaraswat.miu;

/** Sparse idle bursts: sleep between blinks, glances and little bobs; 25 fps only during motion. */
final class Motion {
    static final long CYCLE = 8500;

    static long phase(long elapsed) {
        return Math.floorMod(elapsed, CYCLE);
    }

    static long delay(long elapsed, boolean active) {
        if (active) return 40;
        long p = phase(elapsed);
        if (p < 240 || p >= 3000 && p < 3900 || p >= 6500 && p < 7300) return 40;
        return p < 3000 ? 3000 - p : p < 6500 ? 6500 - p : CYCLE - p;
    }

    static float blink(long elapsed) {
        long p = phase(elapsed);
        return p < 240 ? (float) (1 - .9 * Math.sin(Math.PI * p / 240.0)) : 1;
    }

    static float glance(long elapsed) {
        long p = phase(elapsed);
        return p >= 3000 && p < 3900 ? (float) (3 * Math.sin(Math.PI * (p - 3000) / 900.0)) : 0;
    }

    static float bob(long elapsed) {
        long p = phase(elapsed);
        return p >= 6500 && p < 7300 ? (float) (-2 * Math.sin(Math.PI * (p - 6500) / 800.0)) : 0;
    }
}
