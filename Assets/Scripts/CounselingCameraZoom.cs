using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingCameraZoom : MonoBehaviour
    {
        public const float CloseFieldOfView = 26f;
        public const float DefaultFieldOfView = 38.25f;
        public const float WideFieldOfView = 52f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text zoomLabel;

        private const float Step = 3.5f;
        private const float SmoothTime = 0.12f;
        private float targetFieldOfView = DefaultFieldOfView;
        private float zoomVelocity;

        private void Awake()
        {
            targetFieldOfView = Mathf.Clamp(targetCamera.fieldOfView, CloseFieldOfView, WideFieldOfView);
            zoomOutButton.onClick.AddListener(ZoomOut);
            zoomInButton.onClick.AddListener(ZoomIn);
            resetButton.onClick.AddListener(ResetZoom);
            UpdateLabel();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) ZoomIn();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) ZoomOut();
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) ResetZoom();

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !IsPointerOverUi())
            {
                SetTargetFieldOfView(targetFieldOfView - (wheel * Step));
            }
        }

        private void LateUpdate()
        {
            targetCamera.fieldOfView = Mathf.SmoothDamp(
                targetCamera.fieldOfView,
                targetFieldOfView,
                ref zoomVelocity,
                SmoothTime);
        }

        public void ZoomIn() => SetTargetFieldOfView(targetFieldOfView - Step);

        public void ZoomOut() => SetTargetFieldOfView(targetFieldOfView + Step);

        public void ResetZoom() => SetTargetFieldOfView(DefaultFieldOfView);

        public void SetTargetFieldOfView(float fieldOfView, bool immediate = false)
        {
            targetFieldOfView = Mathf.Clamp(fieldOfView, CloseFieldOfView, WideFieldOfView);
            if (immediate)
            {
                targetCamera.fieldOfView = targetFieldOfView;
                zoomVelocity = 0f;
            }

            UpdateLabel();
        }

        public static float ClampFieldOfView(float fieldOfView) =>
            Mathf.Clamp(fieldOfView, CloseFieldOfView, WideFieldOfView);

        private bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void UpdateLabel()
        {
            int percentage = Mathf.RoundToInt((DefaultFieldOfView / targetFieldOfView) * 100f);
            zoomLabel.text = $"{percentage}%";
            zoomInButton.interactable = targetFieldOfView > CloseFieldOfView;
            zoomOutButton.interactable = targetFieldOfView < WideFieldOfView;
        }
    }
}
