using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Null-safe helpers for Unity objects. C#'s ?? and ??= skip Unity's overloaded == operator, so in
    /// the Editor a missing component ("fake null") is treated as present and the fallback never runs.
    /// </summary>
    public static class UnityObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent(out T existing) ? existing : gameObject.AddComponent<T>();
        }

        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.GetOrAddComponent<T>();
        }
    }
}
