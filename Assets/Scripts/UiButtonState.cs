using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>Fades a button's label with its interactable state (Unity only tints the surface).</summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonState : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private float disabledAlpha = 0.4f;

        private bool initialized;
        private bool lastInteractable;

        private void LateUpdate()
        {
            if (button == null || label == null) return;
            bool interactable = button.IsInteractable();
            if (initialized && interactable == lastInteractable) return;
            initialized = true;
            lastInteractable = interactable;
            Color color = label.color;
            color.a = interactable ? 1f : disabledAlpha;
            label.color = color;
        }
    }
}
