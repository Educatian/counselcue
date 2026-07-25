using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class ClientAvatarHost : MonoBehaviour
    {
        [SerializeField] private Transform lookTarget;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private AvatarPresentationDefinition initialPresentation;
        [SerializeField] private ClientProfileDefinition initialProfile;

        private ClientAvatarController activeClient;
        private string activePresentationId = string.Empty;

        public event Action ActiveAvatarChanged;

        public string GazeStateLabel => activeClient == null ? "Unavailable" : activeClient.GazeStateLabel;
        public float GazeContactWeight => activeClient == null ? 0f : activeClient.GazeContactWeight;
        public int FacialBlendShapeCount => activeClient == null ? 0 : activeClient.FacialBlendShapeCount;
        public int FacialSemanticChannelCount => activeClient == null ? 0 : activeClient.FacialSemanticChannelCount;
        public int SuppressedCombinedShapeCount => activeClient == null ? 0 : activeClient.SuppressedCombinedShapeCount;
        public string ActiveFacialCue => activeClient == null ? "Unavailable" : activeClient.ActiveFacialCue;

        private void Awake()
        {
            if (activeClient == null && initialPresentation != null)
            {
                ApplyPresentation(initialPresentation, initialProfile);
            }
        }

        public void Configure(
            Transform configuredLookTarget,
            RuntimeAnimatorController configuredController,
            AvatarPresentationDefinition presentation,
            ClientProfileDefinition profile)
        {
            lookTarget = configuredLookTarget;
            animatorController = configuredController;
            initialPresentation = presentation;
            initialProfile = profile;
        }

        public void ApplyCase(CounselingCaseDefinition definition)
        {
            if (definition == null) return;
            ApplyPresentation(definition.AvatarPresentation, definition.ProfileDefinition);
        }

        public void ApplyPresentation(AvatarPresentationDefinition presentation, ClientProfileDefinition profile)
        {
            if (presentation == null || presentation.AvatarPrefab == null) return;
            if (activeClient != null && activePresentationId == presentation.PresentationId)
            {
                activeClient.Configure(lookTarget, profile, presentation);
                ActiveAvatarChanged?.Invoke();
                return;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            GameObject avatar = Instantiate(presentation.AvatarPrefab, transform);
            avatar.name = $"ClientAvatar_{presentation.PresentationId}";
            avatar.transform.localPosition = presentation.LocalPosition;
            avatar.transform.localRotation = Quaternion.Euler(presentation.LocalEulerAngles);
            avatar.transform.localScale = presentation.LocalScale;
            Animator animator = avatar.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            activeClient = avatar.AddComponent<ClientAvatarController>();
            activeClient.Configure(lookTarget, profile, presentation);
            activePresentationId = presentation.PresentationId;
            ActiveAvatarChanged?.Invoke();
        }

        public bool TryGetObservationAnchors(out Vector3 bodyAnchor, out Vector3 faceAnchor)
        {
            if (activeClient != null) return activeClient.TryGetObservationAnchors(out bodyAnchor, out faceAnchor);
            bodyAnchor = transform.position + (Vector3.up * 1.25f);
            faceAnchor = transform.position + (Vector3.up * 1.58f);
            return false;
        }

        public void SetAffect(ClientAffect value, bool immediate = false) => activeClient?.SetAffect(value, immediate);
        public void SetRelationalState(ClientRelationalState state) => activeClient?.SetRelationalState(state);
        public void Speak(string text, string emotion) => activeClient?.Speak(text, emotion);
        public void BeginSpeaking(string text, string emotion) => activeClient?.BeginSpeaking(text, emotion);
        public void StopSpeaking() => activeClient?.StopSpeaking();
        public void CycleDebugGaze() => activeClient?.CycleDebugGaze();
    }
}
