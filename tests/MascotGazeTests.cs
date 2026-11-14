using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Miu
{
    public static class MascotGazeTests
    {
        public static string LastReport { get; private set; }
        public static int Run()
        {
            int count = 0;
            var failures = new List<string>();
            Action<bool, string> check = delegate(bool ok, string description) { count++; if (!ok) failures.Add(description); };
            var size = new Size(180, 180);
            check(MascotGaze.Target(new Point(94, 95), size) == new Point(), "Looking at the eyes centers the gaze");
            check(MascotGaze.Target(new Point(-1000, 95), size).X == -6, "Cursor on a left-hand monitor looks left");
            check(MascotGaze.Target(new Point(2000, 95), size).X == 6, "Distant cursor stops at the right eye limit");
            check(MascotGaze.Target(new Point(94, -1000), size).Y == -4, "Cursor above looks up");
            check(MascotGaze.Target(new Point(94, 2000), size).Y == 4, "Cursor below looks down");
            bool bounded = true;
            foreach (double x in new double[] { -100000, -500, 0, 94, 180, 500, 100000 })
                foreach (double y in new double[] { -100000, -500, 0, 95, 180, 500, 100000 })
                {
                    Point offset = MascotGaze.Target(new Point(x, y), size);
                    bounded &= offset.X * offset.X / 36 + offset.Y * offset.Y / 16 <= 1.000001;
                }
            check(bounded, "All desktop directions remain within the face's safe ellipse");
            Point normal = MascotGaze.Target(new Point(130, 120), size);
            Point scaled = MascotGaze.Target(new Point(260, 240), new Size(360, 360));
            Point letterboxed = MascotGaze.Target(new Point(220, 120), new Size(360, 180));
            check((normal - scaled).Length < 0.00001, "Gaze is invariant under display scaling");
            check((normal - letterboxed).Length < 0.00001, "Gaze accounts for centered non-square face bounds");
            check(MascotGaze.Target(new Point(10, 20), new Size()) == new Point(), "An unmeasured face stays centered");
            check(MascotGaze.Target(new Point(Double.NaN, 20), size) == new Point(), "Invalid pointer coordinates stay centered");
            Point current = new Point();
            Point target = new Point(-6, 4);
            bool smooth = true;
            for (int i = 0; i < 30; i++)
            {
                Point next = MascotGaze.Step(current, target);
                smooth &= (target - next).Length <= (target - current).Length && next.X >= -6 && next.Y <= 4;
                current = next;
            }
            check(smooth && current == target, "Gaze eases without overshoot and settles exactly for idle polling");
            var face = new MascotFace();
            check(face.SetGaze(target) && !face.SetGaze(target), "Stationary eyes do not request duplicate redraws");
            foreach (string mood in new string[] { "idle", "cheerful", "wide_eyed", "listening" })
                foreach (bool talking in new bool[] { false, true })
                {
                    byte[] centered = Render(mood, talking, false, new Point());
                    byte[] looking = Render(mood, talking, false, new Point(6, -4));
                    bool eyesChanged = false, outsideUnchanged = true;
                    for (int y = 0; y < 180; y++)
                        for (int x = 0; x < 180; x++)
                            for (int channel = 0; channel < 4; channel++)
                            {
                                int index = (y * 180 + x) * 4 + channel;
                                if (centered[index] == looking[index]) continue;
                                if (x >= 43 && x <= 145 && y >= 76 && y <= 109) eyesChanged = true;
                                else outsideUnchanged = false;
                            }
                    check(eyesChanged && outsideUnchanged, mood + ": only eyes move; body, mouth and audio bars stay intact (talking=" + talking + ")");
                }
            byte[] open = Render("idle", false, false, target), blink = Render("idle", false, true, target);
            bool blinkChanged = false;
            for (int i = 0; i < open.Length; i++) if (open[i] != blink[i]) { blinkChanged = true; break; }
            check(blinkChanged, "Blinking still renders independently of gaze");
            LastReport = "Mascot gaze checks: " + count + ", failures: " + failures.Count + (failures.Count == 0 ? "" : "\r\n" + String.Join("\r\n", failures));
            return failures.Count == 0 ? 0 : 1;
        }
        private static byte[] Render(string mood, bool talking, bool blink, Point gaze)
        {
            var face = new MascotFace { Width = 180, Height = 180, Mood = mood, IsTalking = talking, Blink = blink, Frame = 2, AudioLevel = 35 };
            face.SetGaze(gaze); face.Measure(new Size(180, 180)); face.Arrange(new Rect(0, 0, 180, 180)); face.UpdateLayout();
            var bitmap = new RenderTargetBitmap(180, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(face);
            var pixels = new byte[180 * 180 * 4]; bitmap.CopyPixels(pixels, 180 * 4, 0); return pixels;
        }
    }
}
