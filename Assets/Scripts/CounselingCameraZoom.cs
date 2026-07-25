using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingCameraZoom : MonoBehaviour
    {
        public const float CloseFieldOfView = 23.5f;
        public const float DefaultFieldOfView = 38.25f;
        public const float WideFieldOfView = 52f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private ClientAvatarHost clientAvatar;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button faceObservationButton;
        [SerializeField] private Text zoomLabel;

        private const float Step = 3.5f;
        private const float SmoothTime = 0.12f;
        private float targetFieldOfView = DefaultFieldOfView;
        private float zoomVelocity;
        private Quaternion defaultRotation;
        private Quaternion bodyObservationRotation;
        private Quaternion faceObservationRotation;
        private bool hasObservationFrame;
        private int framingRefreshFrames;

        private void Awake()
        {
            defaultRotation = targetCamera.transform.rotation;
            bodyObservationRotation = defaultRotation;
            faceObservationRotation = defaultRotation;
            targetFieldOfView = Mathf.Clamp(targetCamera.fieldOfView, CloseFieldOfView, WideFieldOfView);
            zoomOutButton.onClick.AddListener(ZoomOut);
            zoomInButton.onClick.AddListener(ZoomIn);
            resetButton.onClick.AddListener(ResetZoom);
            faceObservationButton?.onClick.AddListener(ToggleFaceObservation);
            UpdateLabel();
        }

        private void OnEnable()
        {
            if (clientAvatar != null) clientAvatar.ActiveAvatarChanged += HandleAvatarChanged;
        }

        private void Start()
        {
            framingRefreshFrames = 90;
            RefreshObservationFrame(true);
        }

        private void OnDisable()
        {
            if (clientAvatar != null) clientAvatar.ActiveAvatarChanged -= HandleAvatarChanged;
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
            if (framingRefreshFrames > 0)
            {
                RefreshObservationFrame(false);
                framingRefreshFrames--;
            }

            targetCamera.fieldOfView = Mathf.SmoothDamp(
                targetCamera.fieldOfView,
                targetFieldOfView,
                ref zoomVelocity,
                SmoothTime);

            if (hasObservationFrame)
            {
                float observationWeight = ObservationWeight(targetFieldOfView);
                Quaternion desired = Quaternion.Slerp(bodyObservationRotation, faceObservationRotation, observationWeight);
                float rotationBlend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
                targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, desired, rotationBlend);
            }
        }

        public void ZoomIn()
        {
            RefreshObservationFrame();
            SetTargetFieldOfView(targetFieldOfView - Step);
        }

        public void ZoomOut()
        {
            RefreshObservationFrame();
            SetTargetFieldOfView(targetFieldOfView + Step);
        }

        public void ResetZoom()
        {
            SetTargetFieldOfView(DefaultFieldOfView);
            RefreshObservationFrame();
        }

        public void ToggleFaceObservation()
        {
            RefreshObservationFrame();
            bool isClose = targetFieldOfView <= CloseFieldOfView + 0.2f;
            SetTargetFieldOfView(isClose ? DefaultFieldOfView : CloseFieldOfView);
        }

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

        public static float ObservationWeight(float fieldOfView) =>
            Mathf.InverseLerp(DefaultFieldOfView, CloseFieldOfView, ClampFieldOfView(fieldOfView));

        public void RefreshObservationFrame() => RefreshObservationFrame(false);

        private void HandleAvatarChanged()
        {
            framingRefreshFrames = 90;
            RefreshObservationFrame(false);
        }

        private void RefreshObservationFrame(bool immediate)
        {
            if (targetCamera == null || clientAvatar == null ||
                !clientAvatar.TryGetObservationAnchors(out Vector3 bodyAnchor, out Vector3 faceAnchor))
            {
                hasObservationFrame = false;
                bodyObservationRotation = defaultRotation;
                faceObservationRotation = defaultRotation;
                return;
            }

            Vector3 cameraPosition = targetCamera.transform.position;
            bodyObservationRotation = LookRotationOrDefault(bodyAnchor - cameraPosition, defaultRotation);
            faceObservationRotation = LookRotationOrDefault(faceAnchor - cameraPosition, bodyObservationRotation);
            hasObservationFrame = true;
            if (immediate)
            {
                targetCamera.transform.rotation = Quaternion.Slerp(
                    bodyObservationRotation,
                    faceObservationRotation,
                    ObservationWeight(targetFieldOfView));
            }
        }

        private static Quaternion LookRotationOrDefault(Vector3 direction, Quaternion fallback) =>
            direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;

        private bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void UpdateLabel()
        {
            int percentage = Mathf.RoundToInt((DefaultFieldOfView / targetFieldOfView) * 100f);
            zoomLabel.text = $"{percentage}%";
            zoomInButton.interactable = targetFieldOfView > CloseFieldOfView;
            zoomOutButton.interactable = targetFieldOfView < WideFieldOfView;
            if (faceObservationButton != null)
            {
                Text label = faceObservationButton.GetComponentInChildren<Text>();
                if (label != null) label.text = targetFieldOfView <= CloseFieldOfView + 0.2f ? "전체 보기" : "얼굴 관찰";
            }
        }
    }
}
