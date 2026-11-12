using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Miu
{
    // The 24 reference expressions are ordered left-to-right, top-to-bottom.
    // Renderer, Settings, commands and cloud validation share these stable IDs.
    public static class ExpressionCatalog
    {
        private static readonly string[] referenceMoods = new[]
        {
            "content", "kiss", "crying",
            "neutral", "playful", "wailing",
            "mischievous", "smug_kiss", "sassy",
            "wide_eyed", "surprised_kiss", "shocked",
            "joyful", "sad", "annoyed",
            "scrunched", "dizzy", "blushing",
            "cheerful", "drooling", "delighted",
            "skeptical", "furious", "amazed"
        };

        private static readonly string[] legacyMoods = new[]
        {
            "idle", "happy", "curious", "excited", "love", "sleepy",
            "angry", "confused", "thinking", "listening", "speaking", "wink", "surprised"
        };

        private static readonly string[] allMoods = BuildAllMoods();
        private static readonly HashSet<string> supported = new HashSet<string>(allMoods, StringComparer.Ordinal);

        // Copies prevent consumers from changing the shared allowlist by accident.
        public static string[] ReferenceMoods { get { return (string[])referenceMoods.Clone(); } }
        public static string[] AllMoods { get { return (string[])allMoods.Clone(); } }

        public static bool IsSupported(string mood)
        {
            string canonical;
            return TryResolve(mood, out canonical);
        }

        // Labels and spoken forms such as "wide eyed" resolve to renderer IDs.
        // No arbitrary input becomes a renderer path or executable instruction.
        public static bool TryResolve(string value, out string mood)
        {
            mood = "";
            if (String.IsNullOrWhiteSpace(value) || value.Length > 80) return false;
            string candidate = Regex.Replace(value.Trim().ToLowerInvariant(), @"[\s-]+", "_");
            if (!supported.Contains(candidate)) return false;
            mood = candidate;
            return true;
        }

        public static string Label(string mood)
        {
            string canonical;
            if (!TryResolve(mood, out canonical)) return "Expression";
            switch (canonical)
            {
                case "wide_eyed": return "Wide-eyed";
                case "smug_kiss": return "Smug kiss";
                case "surprised_kiss": return "Surprised kiss";
                default: return Char.ToUpperInvariant(canonical[0]) + canonical.Substring(1);
            }
        }

        private static string[] BuildAllMoods()
        {
            var values = new List<string>();
            foreach (string mood in legacyMoods) if (!values.Contains(mood)) values.Add(mood);
            foreach (string mood in referenceMoods) if (!values.Contains(mood)) values.Add(mood);
            return values.ToArray();
        }
    }
}
