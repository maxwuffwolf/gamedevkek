using UnityEngine;

namespace Game.Core
{
    public static class ColorUtil
    {
        /// <summary>"#RRGGBB" или "#RRGGBBAA" → Color. При ошибке возвращает fallback.</summary>
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        public static Color Parse(string hex) => Parse(hex, Color.magenta);

        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static string Colorize(string text, Color c) => $"<color={ToHex(c)}>{text}</color>";
    }
}
