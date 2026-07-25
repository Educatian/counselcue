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

        public string GazeStateLabel => activeClient == null ? "Unavailable" : activeClient.GazeStateLabel;
        public float GazeContactWeight => activeClient == null ? 0f : activeClient.GazeContactWeight;
        public int FacialBlendShapeCount => activeClient == null ? 0 : activeClient.FacialBlendShapeCount;
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
        }

        public void SetAffect(ClientAffect value, bool immediate = false) => activeClient?.SetAffect(value, immediate);
        public void SetRelationalState(ClientRelationalState state) => activeClient?.SetRelationalState(state);
        public void Speak(string text, string emotion) => activeClient?.Speak(text, emotion);
        public void BeginSpeaking(string text, string emotion) => activeClient?.BeginSpeaking(text, emotion);
        public void StopSpeaking() => activeClient?.StopSpeaking();
        public void CycleDebugGaze() => activeClient?.CycleDebugGaze();
    }
}
