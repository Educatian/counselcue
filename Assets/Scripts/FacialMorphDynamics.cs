using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public static class FacialMorphDynamics
    {
        public static float Step(float current, float target, ref float velocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            if (deltaTime <= 0f) return current;
            return Mathf.Clamp(
                Mathf.SmoothDamp(current, target, ref velocity, Mathf.Max(0.025f, smoothTime), maxSpeed, deltaTime),
                0f,
                100f);
        }

        public static float BlinkWeight(float elapsed)
        {
            const float close = 0.055f;
            const float hold = 0.025f;
            const float open = 0.095f;
            if (elapsed < 0f || elapsed >= close + hold + open) return 0f;
            if (elapsed < close) return Ease(elapsed / close);
            if (elapsed < close + hold) return 1f;
            return 1f - Ease((elapsed - close - hold) / open);
        }

        public static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
