using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Grows the client's subtitle card upward to fit longer, multi-sentence statements instead
    /// of cutting them off (reviewer: long replies were truncated on screen), and shrinks it back
    /// for short lines so the client's body stays visible.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SubtitleAutoSize : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private Text line;
        [SerializeField] private float topPadding = 38f;
        [SerializeField] private float bottomPadding = 12f;
        [SerializeField] private float minLineHeight = 52f;
        [SerializeField] private float maxLineHeight = 150f;
        [SerializeField] private int preferredFontSize = 20;

        private string measured;
        private float measuredWidth;

        public void Configure(RectTransform speechCard, Text clientLine)
        {
            card = speechCard;
            line = clientLine;
        }

        private void LateUpdate()
        {
            if (card == null || line == null) return;
            float width = line.rectTransform.rect.width;
            if (line.text == measured && Mathf.Approximately(width, measuredWidth)) return;
            measured = line.text;
            measuredWidth = width;

            TextGenerationSettings settings = line.GetGenerationSettings(new Vector2(width, 0f));
            settings.resizeTextForBestFit = false;
            settings.fontSize = preferredFontSize;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float scale = line.pixelsPerUnit <= 0f ? 1f : line.pixelsPerUnit;
            float needed = line.cachedTextGeneratorForLayout.GetPreferredHeight(line.text ?? string.Empty, settings) / scale;
            float height = Mathf.Clamp(needed + 6f, minLineHeight, maxLineHeight);

            // Best fit only when even the tallest card cannot hold the line at the preferred size.
            line.resizeTextForBestFit = needed + 6f > maxLineHeight;
            line.resizeTextMaxSize = preferredFontSize;
            if (!line.resizeTextForBestFit) line.fontSize = preferredFontSize;

            Vector2 lineSize = line.rectTransform.sizeDelta;
            line.rectTransform.sizeDelta = new Vector2(lineSize.x, height);
            Vector2 cardSize = card.sizeDelta;
            card.sizeDelta = new Vector2(cardSize.x, topPadding + height + bottomPadding);
        }
    }
}
