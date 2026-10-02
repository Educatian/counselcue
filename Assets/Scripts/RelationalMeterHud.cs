using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Three slim meters for the client's relational state (safety, guardedness, willingness
    /// to disclose). Fills ease toward new values so a turn's effect is visible, not abrupt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RelationalMeterHud : MonoBehaviour
    {
        [SerializeField] private RectTransform[] fills = new RectTransform[3];
        [SerializeField] private Text[] values = new Text[3];
        [SerializeField] private float easeSeconds = 0.6f;

        private readonly float[] target = { 0.38f, 0.62f, 0.25f };
        private readonly float[] shown = { 0.38f, 0.62f, 0.25f };

        public void Show(float safety, float guardedness, float disclosure)
        {
            target[0] = Mathf.Clamp01(safety);
            target[1] = Mathf.Clamp01(guardedness);
            target[2] = Mathf.Clamp01(disclosure);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null) values[i].text = Mathf.RoundToInt(target[i] * 100f).ToString();
            }
        }

        private void Update()
        {
            float step = easeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / easeSeconds;
            for (int i = 0; i < fills.Length && i < target.Length; i++)
            {
                shown[i] = Mathf.MoveTowards(shown[i], target[i], step);
                if (fills[i] == null) continue;
                Vector2 max = fills[i].anchorMax;
                max.x = Mathf.Max(0.02f, shown[i]);
                fills[i].anchorMax = max;
            }
        }
    }
}
