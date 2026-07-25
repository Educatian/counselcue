using System.Collections;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum ClientAffect
    {
        Guarded,
        Anxious,
        Relieved,
        Thoughtful
    }

    [DisallowMultipleComponent]
    public sealed class ClientAvatarController : MonoBehaviour
    {
        private const int GestureLayer = 1;
        private const float GestureLayerWeight = 0.42f;
        private const float GestureFadeInSpeed = 0.72f;
        private const float GestureFadeOutSpeed = 0.52f;

        [SerializeField] private Animator animator;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private ClientProfileDefinition clientProfile;
        [SerializeField] private AvatarPresentationDefinition avatarPresentation;

        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private Coroutine speechRoutine;
        private float gestureLayerWeight;
        private float gestureLayerTarget;
        private ClientGazeController gazeController;
        private ClientFacialExpressionDriver facialDriver;
        private ClientRenderingController renderingController;

        public string GazeStateLabel => gazeController == null ? "Unavailable" : gazeController.State.ToString();
        public float GazeContactWeight => gazeController == null ? 0f : gazeController.ContactWeight;
        public int FacialBlendShapeCount => facialDriver == null ? 0 : facialDriver.BlendShapeCount;
        public int FacialSemanticChannelCount => facialDriver == null ? 0 : facialDriver.SemanticChannelCount;
        public int SuppressedCombinedShapeCount => facialDriver == null ? 0 : facialDriver.SuppressedCombinedShapeCount;
        public string ActiveFacialCue => facialDriver == null ? "Unavailable" : facialDriver.ActiveCueSummary;
        public void CycleDebugGaze() => gazeController?.CycleDebugState();

        public void Configure(
            Transform configuredLookTarget,
            ClientProfileDefinition configuredProfile,
            AvatarPresentationDefinition configuredPresentation)
        {
            lookTarget = configuredLookTarget;
            clientProfile = configuredProfile;
            avatarPresentation = configuredPresentation;
            animator ??= GetComponentInChildren<Animator>(true);
            InitializeDrivers();
        }

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>();
            InitializeDrivers();
            SetAffect(ClientAffect.Anxious, true);
        }

        private void InitializeDrivers()
        {
            if (animator != null)
            {
                gazeController = animator.GetComponent<ClientGazeController>() ?? animator.gameObject.AddComponent<ClientGazeController>();
                facialDriver = animator.GetComponent<ClientFacialExpressionDriver>() ?? animator.gameObject.AddComponent<ClientFacialExpressionDriver>();
                if (animator.GetComponent<ClientMicroMotionController>() == null) animator.gameObject.AddComponent<ClientMicroMotionController>();
                gazeController.Initialize(lookTarget, clientProfile, avatarPresentation);
                facialDriver.Initialize(avatarPresentation);
            }
            renderingController = GetComponent<ClientRenderingController>() ?? gameObject.AddComponent<ClientRenderingController>();
            renderingController.ApplyReadableFaceMaterials();
            if (animator != null && animator.layerCount > GestureLayer)
            {
                animator.SetLayerWeight(GestureLayer, 0f);
            }

        }

        private void Update()
        {
            gazeController?.SetContext(affect, relationalState, speechRoutine != null);
            facialDriver?.SetContext(affect, relationalState);
            UpdateGestureLayer();
        }

        private void OnDisable()
        {
            if (speechRoutine != null) StopCoroutine(speechRoutine);
            speechRoutine = null;
            facialDriver?.EndSpeech();
            gestureLayerTarget = 0f;
            gestureLayerWeight = 0f;
            if (animator != null && animator.layerCount > GestureLayer)
            {
                animator.SetLayerWeight(GestureLayer, 0f);
            }
        }

        public void SetAffect(ClientAffect value, bool immediate = false)
        {
            affect = value;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                string state = value switch
                {
                    ClientAffect.Relieved => "Relaxed",
                    ClientAffect.Thoughtful => "Thoughtful",
                    ClientAffect.Guarded => "Waiting",
                    _ => "Idle"
                };
                animator.CrossFadeInFixedTime(state, immediate ? 0f : 0.7f, 0);
            }

            facialDriver?.SetContext(affect, relationalState);
        }

        public void SetRelationalState(ClientRelationalState state)
        {
            relationalState = state;
            facialDriver?.SetContext(affect, relationalState);
        }

        public static ClientAffect AffectForEmotion(string emotion)
        {
            return (emotion ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "guarded" => ClientAffect.Guarded,
                "relieved" => ClientAffect.Relieved,
                "thoughtful" => ClientAffect.Thoughtful,
                _ => ClientAffect.Anxious
            };
        }

        public static string GestureStateFor(string emotion, int variant)
        {
            string normalized = (emotion ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "relieved" => "TalkRelaxed",
                "guarded" => variant % 2 == 0 ? "TalkSad" : "TalkNeutral",
                "thoughtful" => "TalkNeutral",
                _ => variant % 3 == 0 ? "TalkNeutral" : variant % 3 == 1 ? "TalkNervousSoft" : "TalkNervous"
            };
        }

        public void Speak(string text) => Speak(text, affect.ToString());

        public void Speak(string text, string emotion)
        {
            StopSpeaking();
            float duration = Mathf.Clamp((text ?? string.Empty).Length * 0.055f, 1.2f, 8f);
            facialDriver?.BeginSpeech(text, duration);
            speechRoutine = StartCoroutine(SpeechRoutine(duration, emotion, (text ?? string.Empty).Length));
        }

        public void BeginSpeaking(string text, string emotion)
        {
            StopSpeaking();
            facialDriver?.BeginSpeech(text, 60f);
            speechRoutine = StartCoroutine(SpeechRoutine(60f, emotion, (text ?? string.Empty).Length));
        }

        public void StopSpeaking()
        {
            if (speechRoutine != null) StopCoroutine(speechRoutine);
            speechRoutine = null;
            facialDriver?.EndSpeech();
            gestureLayerTarget = 0f;
        }

        private IEnumerator SpeechRoutine(float duration, string emotion, int textLength)
        {
            float elapsed = 0f;
            float nextGesture = Random.Range(0.55f, 0.95f);
            float gestureEnd = float.PositiveInfinity;
            bool useGesture = textLength >= 18 && (duration > 10f || Random.value < 0.72f);
            bool gestureActive = false;
            int gestureVariant = Random.Range(0, 12);
            gestureLayerTarget = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (useGesture && !gestureActive && elapsed >= nextGesture && duration - elapsed >= 1.3f &&
                    animator != null && animator.layerCount > GestureLayer)
                {
                    string gestureState = GestureStateFor(emotion, gestureVariant++);
                    animator.CrossFadeInFixedTime(
                        gestureState,
                        0.65f,
                        GestureLayer,
                        Random.Range(0.05f, 0.18f));
                    gestureLayerTarget = GestureLayerWeight;
                    gestureActive = true;
                    gestureEnd = elapsed + Random.Range(2.4f, 3.4f);
                }

                if (gestureActive && elapsed >= gestureEnd)
                {
                    gestureActive = false;
                    gestureLayerTarget = 0f;
                    nextGesture = elapsed + Random.Range(1.6f, 2.8f);
                }

                yield return null;
            }

            speechRoutine = null;
            facialDriver?.EndSpeech();
            gestureLayerTarget = 0f;
        }

        private void UpdateGestureLayer()
        {
            if (animator == null || animator.layerCount <= GestureLayer) return;

            float speed = gestureLayerTarget > gestureLayerWeight
                ? GestureFadeInSpeed
                : GestureFadeOutSpeed;
            gestureLayerWeight = Mathf.MoveTowards(gestureLayerWeight, gestureLayerTarget, speed * Time.deltaTime);
            animator.SetLayerWeight(GestureLayer, gestureLayerWeight);
        }

    }
}
