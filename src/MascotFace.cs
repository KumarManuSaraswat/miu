using System;
using System.Windows;
using System.Windows.Media;

namespace Miu
{
    // A soft, code-drawn companion. Cached body and frozen brushes keep idle rendering inexpensive.
    public class MascotFace : FrameworkElement
    {
        public string Mood = "idle";
        public bool Blink;
        public bool IsTalking;
        public int Frame;
        public int AudioLevel;
        public bool ShowBody = true;
        private static SolidColorBrush Brush(string hex) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze(); return brush; }
        private static readonly Brush White = Brush("#F5F5FA"), Dim = Brush("#858995");
        private static readonly DrawingGroup Body = MakeBody();
        private static Pen Line(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
        private static LinearGradientBrush Gradient(string top, string bottom)
        {
            var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), new Point(0.2, 0), new Point(0.8, 1)); brush.Freeze(); return brush;
        }
        private static void Path(DrawingContext dc, string data, Brush fill, Pen pen) { dc.DrawGeometry(fill, pen, Geometry.Parse(data)); }
        private static DrawingGroup MakeBody()
        {
            var group = new DrawingGroup();
            using (DrawingContext dc = group.Open())
            {
                for (int i = 6; i >= 0; i--)
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(3 + i), 13, 14, 20)), null, new Point(96, 165), 45 + i * 2, 3 + i * 0.65);
                Path(dc, "M 25,49 C 29,28 42,17 60,14", null, Line(Gradient("#B1B3BD", "#6B6E79"), 12));
                Path(dc, "M 23,47 C 28,27 42,17 58,15", null, Line(Brush("#28FFFFFF"), 2));
                dc.DrawRoundedRectangle(Gradient("#4A4C55", "#222329"), Line(Brush("#555861"), 0.8), new Rect(26, 45, 136, 113), 36, 36);
                dc.DrawRoundedRectangle(null, Line(Brush("#20FFFFFF"), 0.7), new Rect(29, 48, 130, 107), 33, 33);
                dc.DrawRoundedRectangle(Gradient("#111216", "#07080B"), Line(Brush("#50525A"), 0.8), new Rect(34, 53, 120, 95), 29, 29);
                Path(dc, "M 49,65 C 66,57 117,57 138,64", null, Line(Brush("#16FFFFFF"), 2));
                dc.DrawEllipse(Brush("#777A86"), null, new Point(94, 153), 1.4, 1.4);
            }
            group.Freeze(); return group;
        }
        private static readonly Pen FaceLine = Line(White, 3.3);
        private static readonly Pen FineLine = Line(White, 2.7);
        private static void Eye(DrawingContext dc, double x, double y, double width, double height)
        {
            dc.DrawRoundedRectangle(White, null, new Rect(x - width / 2, y - height / 2, width, height), width / 2, width / 2);
        }
        private static void Ink(DrawingContext dc, string data) { Path(dc, data, null, FaceLine); }
        private static void Fine(DrawingContext dc, string data) { Path(dc, data, null, FineLine); }
        private static void Dot(DrawingContext dc, double x, double y, double radius) { dc.DrawEllipse(White, null, new Point(x, y), radius, radius); }
        private static void Rings(DrawingContext dc, double radius)
        {
            dc.DrawEllipse(null, FaceLine, new Point(64, 95), radius, radius + 0.4);
            dc.DrawEllipse(null, FaceLine, new Point(124, 95), radius, radius + 0.4);
        }
        private static string Expression(string mood)
        {
            switch (mood)
            {
                case "sleepy": return "content";
                case "happy": return "cheerful";
                case "excited": return "delighted";
                case "love": return "kiss";
                case "angry": return "furious";
                case "confused": return "dizzy";
                case "curious": return "wide_eyed";
                case "surprised": return "shocked";
                case "speaking": return "cheerful";
                default: return mood;
            }
        }
        private static bool CanBlink(string expression)
        {
            switch (expression)
            {
                case "idle": case "cheerful": case "content": case "neutral": case "wide_eyed": case "surprised_kiss":
                case "joyful": case "sad": case "delighted": case "amazed":
                case "listening": case "thinking": case "wink": return true;
                default: return false;
            }
        }
        private static void DrawEyes(DrawingContext dc, string expression, bool blink)
        {
            if (blink)
            {
                Ink(dc, "M 53,95 L 75,95 M 113,95 L 135,95");
                return;
            }
            switch (expression)
            {
                case "idle":
                    Eye(dc, 64, 95, 12, 25); Eye(dc, 124, 95, 12, 25);
                    break;
                case "content": case "kiss": case "crying":
                    Ink(dc, "M 52,93 L 76,93 M 112,93 L 136,93");
                    break;
                case "neutral":
                    Dot(dc, 64, 96, 10); Dot(dc, 124, 96, 10);
                    break;
                case "playful":
                    Path(dc, "M 52,98 C 52,80 76,80 76,98 Z M 112,98 C 112,80 136,80 136,98 Z", White, null);
                    break;
                case "wailing":
                    Ink(dc, "M 57,82 C 42,93 53,109 67,104 C 73,102 77,99 77,95 M 131,82 C 146,93 135,109 121,104 C 115,102 111,99 111,95");
                    break;
                case "mischievous": case "smug_kiss":
                    Path(dc, "M 52,84 L 78,104 C 57,117 45,99 52,84 Z M 136,84 L 110,104 C 131,117 143,99 136,84 Z", White, null);
                    break;
                case "sassy":
                    Ink(dc, "M 51,93 L 77,93 C 75,113 54,113 51,93 Z M 111,93 L 137,93 C 135,113 114,113 111,93 Z");
                    break;
                case "wide_eyed": case "surprised_kiss": case "shocked":
                    Rings(dc, 11.8);
                    break;
                case "joyful":
                    Rings(dc, 6.5);
                    break;
                case "sad":
                    Dot(dc, 64, 102, 5.5); Dot(dc, 124, 102, 5.5);
                    break;
                case "annoyed":
                    Ink(dc, "M 51,93 L 77,93 Q 74,108 54,101 Z M 111,93 L 137,93 Q 134,108 114,101 Z");
                    Fine(dc, "M 62,94 L 62,101 M 66,94 L 65,102 M 122,94 L 122,101 M 126,94 L 125,102");
                    break;
                case "scrunched":
                    Ink(dc, "M 54,88 L 74,97 L 54,106 M 52,96 L 70,97 M 134,88 L 114,97 L 134,106 M 136,96 L 118,97");
                    break;
                case "dizzy":
                    Ink(dc, "M 72,101 C 61,109 49,101 53,91 C 57,81 72,85 72,94 C 73,101 59,102 60,95 C 61,91 67,94 65,97 M 132,101 C 121,109 109,101 113,91 C 117,81 132,85 132,94 C 133,101 119,102 120,95 C 121,91 127,94 125,97");
                    break;
                case "blushing":
                    Ink(dc, "M 53,91 L 76,91 L 55,97 L 75,97 L 54,103 L 75,103 M 113,91 L 136,91 L 115,97 L 135,97 L 114,103 L 135,103");
                    break;
                case "cheerful":
                    Eye(dc, 64, 95, 10, 19); Eye(dc, 124, 95, 10, 19);
                    break;
                case "drooling":
                    Ink(dc, "M 51,87 L 78,87 M 58,88 L 58,108 M 70,88 L 70,108 M 110,87 L 137,87 M 118,88 L 118,108 M 130,88 L 130,108");
                    break;
                case "delighted": case "amazed":
                    Dot(dc, 64, 94, 7.2); Dot(dc, 124, 94, 7.2);
                    break;
                case "skeptical":
                    Ink(dc, "M 52,96 L 77,96 Q 74,111 56,103 Z M 111,96 L 136,96 Q 132,110 115,104 Z");
                    Fine(dc, "M 64,97 L 64,102 M 124,97 L 124,102");
                    break;
                case "furious":
                    Ink(dc, "M 51,84 L 78,104 C 56,111 49,102 51,84 Z M 137,84 L 110,104 C 132,111 139,102 137,84 Z");
                    break;
                case "listening":
                    Eye(dc, 64, 94, 12, 24); Eye(dc, 124, 94, 12, 24);
                    break;
                case "thinking":
                    Eye(dc, 65, 94, 11, 22); Eye(dc, 124, 98, 11, 13);
                    break;
                case "wink":
                    Dot(dc, 64, 95, 7); Ink(dc, "M 116,88 L 128,96 L 116,103");
                    break;
                default:
                    Ink(dc, "M 52,93 L 76,93 M 112,93 L 136,93");
                    break;
            }
        }
        private static void DrawAccents(DrawingContext dc, string expression)
        {
            switch (expression)
            {
                case "crying": case "shocked":
                    Ink(dc, "M 59,112 C 58,119 51,126 55,131 C 59,137 66,133 64,127 Z M 129,112 C 128,119 121,126 125,131 C 129,137 136,133 134,127 Z");
                    break;
                case "sad":
                    Ink(dc, "M 54,82 C 55,89 62,93 73,91 M 115,91 C 126,93 133,89 134,82");
                    break;
                case "annoyed":
                    Fine(dc, "M 135,72 Q 136,79 142,79 M 148,72 Q 147,79 142,79 M 135,86 Q 136,80 142,80 M 148,86 Q 147,80 142,80");
                    break;
                case "skeptical":
                    Ink(dc, "M 56,85 L 72,85 M 116,85 L 132,85");
                    break;
                case "thinking":
                    Fine(dc, "M 115,84 L 130,81");
                    break;
            }
        }
        private static void DrawMouth(DrawingContext dc, string expression)
        {
            switch (expression)
            {
                case "content": case "mischievous": case "wide_eyed":
                    Ink(dc, "M 81,115 Q 87,123 94,115 Q 101,123 107,115");
                    break;
                case "kiss": case "smug_kiss": case "surprised_kiss":
                    Ink(dc, "M 89,105 C 100,99 105,112 95,114 C 105,116 101,131 90,125");
                    break;
                case "crying":
                    Ink(dc, "M 83,109 C 84,92 104,92 105,109 Z");
                    break;
                case "neutral":
                    Ink(dc, "M 82,110 L 106,110");
                    break;
                case "playful":
                    Ink(dc, "M 79,108 Q 86,108 86,115 M 109,108 Q 102,108 102,115 M 84,115 L 104,115 M 88,115 L 88,127 C 89,137 100,137 100,127 L 100,115 M 94,116 L 94,128");
                    break;
                case "wailing":
                    Ink(dc, "M 78,117 C 71,106 85,102 94,103 C 104,102 117,109 110,119 C 105,126 100,114 94,120 C 88,115 83,125 78,117 Z");
                    break;
                case "sassy":
                    Ink(dc, "M 81,115 Q 87,121 94,115 Q 101,121 107,115 M 89,120 C 88,135 101,135 100,120");
                    break;
                case "shocked":
                    Ink(dc, "M 84,127 C 84,101 104,101 104,127 Z");
                    break;
                case "joyful":
                    Ink(dc, "M 82,108 Q 86,117 94,116 Q 103,117 106,108 M 86,114 L 86,123 C 87,134 101,134 102,123 L 102,114");
                    break;
                case "sad":
                    Ink(dc, "M 84,126 C 85,112 103,112 104,126");
                    break;
                case "annoyed":
                    dc.DrawRoundedRectangle(null, FaceLine, new Rect(80, 115, 28, 10), 5, 5);
                    break;
                case "scrunched":
                    Ink(dc, "M 83,112 Q 90,118 83,124 M 105,112 Q 98,118 105,124 M 88,118 L 100,118");
                    break;
                case "dizzy":
                    Fine(dc, "M 79,122 L 83,113 L 87,122 L 91,113 L 95,122 L 99,113 L 103,122 L 107,113");
                    break;
                case "blushing":
                    Ink(dc, "M 82,124 Q 93,130 102,120 M 102,113 Q 98,123 110,122");
                    break;
                case "cheerful":
                    Fine(dc, "M 86,116 Q 94,122 102,116");
                    break;
                case "drooling":
                    Ink(dc, "M 82,107 C 72,109 74,120 83,120 L 93,120 L 93,129 C 94,135 102,135 103,128 L 103,121 C 116,117 113,106 105,106 Z");
                    break;
                case "delighted":
                    Ink(dc, "M 82,111 Q 88,116 94,112 Q 100,116 106,111 C 109,126 79,130 82,111 Z");
                    break;
                case "skeptical":
                    Ink(dc, "M 85,127 L 103,120");
                    break;
                case "furious":
                    Ink(dc, "M 94,111 L 101,129 L 87,129 Z");
                    break;
                case "amazed":
                    Ink(dc, "M 80,129 L 80,109 L 87,118 L 94,108 L 101,118 L 108,109 L 108,129 Z");
                    break;
                case "thinking":
                    Fine(dc, "M 85,123 L 103,120");
                    break;
                case "wink":
                    Ink(dc, "M 82,116 Q 94,128 106,116");
                    break;
                case "listening": break;
                default:
                    Ink(dc, "M 81,115 Q 87,123 94,115 Q 101,123 107,115");
                    break;
            }
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double scale = Math.Min(ActualWidth / 180, ActualHeight / 180);
            if (scale <= 0) return;
            dc.PushTransform(new TranslateTransform((ActualWidth - 180 * scale) / 2, (ActualHeight - 180 * scale) / 2));
            dc.PushTransform(new ScaleTransform(scale, scale));
            if (ShowBody) dc.DrawDrawing(Body);
            else dc.DrawRoundedRectangle(Brush("#101115"), null, new Rect(26, 45, 136, 113), 36, 36);
            string mood = Mood ?? "idle";
            string expression = Expression(mood);
            DrawEyes(dc, expression, (Blink && CanBlink(expression)) || mood == "sleepy");
            DrawAccents(dc, expression);
            bool talking = IsTalking || mood == "speaking";
            int phase = Math.Abs(Frame % 4);
            if (talking && (expression == "cheerful" || expression == "idle" || expression == "content" || expression == "neutral"))
            {
                double height = phase == 2 ? 6.0 : (phase == 1 ? 4.0 : (phase == 3 ? 3.0 : 2.2));
                double width = phase == 2 ? 13.0 : 12.0;
                dc.DrawRoundedRectangle(White, null, new Rect(94 - width / 2, 117 - height / 2, width, height), height / 2, height / 2);
            }
            else
            {
                if (talking) dc.PushTransform(new ScaleTransform(1, 0.68 + phase * 0.16, 94, 116));
                DrawMouth(dc, expression);
                if (talking) dc.Pop();
            }
            if (expression == "listening")
            {
                for (int i = 0; i < 5; i++)
                {
                    double height = 2 + AudioLevel / 13.0 * (i == 2 ? 1 : 0.6);
                    dc.DrawLine(FineLine, new Point(82 + i * 6, 123 - height / 2), new Point(82 + i * 6, 123 + height / 2));
                }
            }
            if (mood == "sleepy")
                Path(dc, "M 139,25 L 148,25 L 139,34 L 148,34 M 153,12 L 165,12 L 153,23 L 165,23", null, Line(Dim, 2.3));
            dc.Pop(); dc.Pop();
        }
    }
}
