using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Visual tokens for the CounselCue interface: hanji paper, ink glass, celadon (청자)
    /// actions, lamp amber for time and highlights, and a vermilion seal accent. The scene
    /// builder and the runtime controllers share these so selection states match the build.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.118f, 0.129f, 0.122f, 1f);
        public static readonly Color InkMuted = new Color(0.345f, 0.360f, 0.340f, 1f);
        public static readonly Color Paper = new Color(0.965f, 0.945f, 0.905f, 1f);
        public static readonly Color PaperDeep = new Color(0.918f, 0.890f, 0.840f, 1f);
        public static readonly Color Glass = new Color(0.063f, 0.071f, 0.067f, 0.86f);
        public static readonly Color GlassDeep = new Color(0.086f, 0.094f, 0.090f, 1f);
        public static readonly Color OnDark = new Color(0.953f, 0.941f, 0.910f, 1f);
        public static readonly Color OnDarkMuted = new Color(0.780f, 0.788f, 0.752f, 1f);
        public static readonly Color Celadon = new Color(0.498f, 0.722f, 0.627f, 1f);
        public static readonly Color CeladonDeep = new Color(0.173f, 0.388f, 0.322f, 1f);
        public static readonly Color CeladonSoft = new Color(0.824f, 0.894f, 0.859f, 1f);
        public static readonly Color Amber = new Color(0.937f, 0.745f, 0.455f, 1f);
        public static readonly Color Clay = new Color(0.855f, 0.560f, 0.435f, 1f);
        public static readonly Color Vermilion = new Color(0.722f, 0.290f, 0.220f, 1f);
        public static readonly Color TimerWarning = new Color(0.965f, 0.560f, 0.470f, 1f);

        public const string CeladonDeepHex = "#2C6352";
        public const string CeladonHex = "#7FB8A0";
        public const string InkMutedHex = "#585C57";

        /// <summary>Selected / unselected look for chips such as case and scene pickers.</summary>
        public static void SetChoice(Button button, bool selected, bool onDark, bool completed = false)
        {
            if (button == null) return;
            Image surface = button.targetGraphic as Image;
            Color fill;
            Color text;
            if (selected)
            {
                fill = CeladonDeep;
                text = Paper;
            }
            else if (completed)
            {
                fill = onDark ? new Color(0.498f, 0.722f, 0.627f, 0.22f) : CeladonSoft;
                text = onDark ? OnDark : CeladonDeep;
            }
            else
            {
                fill = onDark ? new Color(1f, 1f, 1f, 0.05f) : PaperDeep;
                text = onDark ? OnDarkMuted : Ink;
            }

            if (surface != null) surface.color = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = selected ? new Color(1.08f, 1.08f, 1.08f, 1f) : new Color(1.06f, 1.06f, 1.06f, onDark ? 2.4f : 1f);
            button.colors = colors;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = text;
        }

        public static string Colorize(string value, string hex) => $"<color={hex}>{value}</color>";
    }
}
