using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Analytic two-bone IK on raw transforms (upper → lower → end). Unlike Mecanim IK it is not
    /// clamped by humanoid muscle limits, so limbs can cross the midline (crossed legs, folded
    /// arms). The pole sets the plane the middle joint bends toward.
    /// </summary>
    public static class HumanTwoBoneIk
    {
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0.001f) return;
            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            float upperLength = Vector3.Distance(a, b);
            float lowerLength = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            float reach = Mathf.Clamp(toTarget.magnitude, 0.02f, (upperLength + lowerLength) * 0.999f);
            Vector3 direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (c - a).normalized;
            Vector3 bend = (pole - a) - Vector3.Dot(pole - a, direction) * direction;
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(direction, Vector3.right);
            bend.Normalize();
            float cosA = Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * upperLength * reach), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 middle = a + direction * (upperLength * cosA) + bend * (upperLength * sinA);
            Vector3 endTarget = a + direction * reach;

            Quaternion upperStart = upper.rotation;
            Quaternion lowerStart = lower.rotation;
            upper.rotation = Quaternion.FromToRotation(b - a, middle - a) * upperStart;
            Vector3 middleNow = lower.position;
            lower.rotation = Quaternion.FromToRotation(end.position - middleNow, endTarget - middleNow) * lower.rotation;
            if (weight < 0.999f)
            {
                upper.rotation = Quaternion.Slerp(upperStart, upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(lowerStart, lower.rotation, weight);
            }
        }
    }
}
