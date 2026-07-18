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
        private const float GestureLayerWeight = 0.58f;

        [SerializeField] private Animator animator;
        [SerializeField] private Transform lookTarget;
        [SerializeField, Min(1f)] private float blendSpeed = 95f;

        private SkinnedMeshRenderer[] renderers;
        private ClientAffect affect = ClientAffect.Anxious;
        private Coroutine speechRoutine;
        private float speakingWeight;
        private float blinkWeight;
        private float nextBlink;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            nextBlink = Random.Range(1.8f, 4.2f);
            if (animator != null && animator.layerCount > GestureLayer)
            {
                animator.SetLayerWeight(GestureLayer, GestureLayerWeight);
            }

            SetAffect(ClientAffect.Anxious, true);
        }

        private void Update()
        {
            nextBlink -= Time.deltaTime;
            if (nextBlink <= 0f)
            {
                StartCoroutine(Blink());
                nextBlink = Random.Range(2.4f, 5.2f);
            }

            ApplyFace();
        }

        private void OnDisable()
        {
            if (speechRoutine != null) StopCoroutine(speechRoutine);
            speechRoutine = null;
            speakingWeight = 0f;
            blinkWeight = 0f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || lookTarget == null) return;

            float gaze = affect == ClientAffect.Guarded ? 0.28f : 0.72f;
            animator.SetLookAtWeight(gaze, 0.12f, 0.76f, 0.62f, 0.42f);
            animator.SetLookAtPosition(lookTarget.position);
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
                animator.CrossFadeInFixedTime(state, immediate ? 0f : 0.45f, 0);
            }

            if (immediate) ApplyFace(true);
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
                _ => variant % 3 == 0 ? "TalkNeutral" : "TalkNervous"
            };
        }

        public void Speak(string text) => Speak(text, affect.ToString());

        public void Speak(string text, string emotion)
        {
            StopSpeaking();
            float duration = Mathf.Clamp((text ?? string.Empty).Length * 0.055f, 1.2f, 8f);
            speechRoutine = StartCoroutine(SpeechRoutine(duration, emotion, (text ?? string.Empty).Length));
        }

        public void BeginSpeaking(string text, string emotion)
        {
            StopSpeaking();
            speechRoutine = StartCoroutine(SpeechRoutine(60f, emotion, (text ?? string.Empty).Length));
        }

        public void StopSpeaking()
        {
            if (speechRoutine != null) StopCoroutine(speechRoutine);
            speechRoutine = null;
            speakingWeight = 0f;
            if (animator != null && animator.layerCount > GestureLayer)
            {
                animator.CrossFadeInFixedTime("Empty", 0.4f, GestureLayer);
            }
        }

        private IEnumerator SpeechRoutine(float duration, string emotion, int textLength)
        {
            float elapsed = 0f;
            float gestureStart = Random.Range(0.35f, 0.75f);
            float gestureEnd = Mathf.Min(duration - 0.2f, gestureStart + Random.Range(1.8f, 3.2f));
            bool useGesture = textLength >= 18 && Random.value < 0.72f;
            bool gestureStarted = false;
            bool gestureFinished = false;
            string gestureState = GestureStateFor(emotion, Random.Range(0, 6));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float noise = Mathf.PerlinNoise(Time.unscaledTime * 5.4f, 0.31f);
                float mouthTarget = Mathf.Clamp01((noise - 0.32f) * 1.55f);
                speakingWeight = Mathf.MoveTowards(speakingWeight, mouthTarget, Time.deltaTime * 4.8f);

                if (useGesture && !gestureStarted && elapsed >= gestureStart &&
                    animator != null && animator.layerCount > GestureLayer)
                {
                    gestureStarted = true;
                    animator.CrossFadeInFixedTime(gestureState, 0.38f, GestureLayer);
                }

                if (gestureStarted && !gestureFinished && elapsed >= gestureEnd)
                {
                    gestureFinished = true;
                    animator.CrossFadeInFixedTime("Empty", 0.5f, GestureLayer);
                }

                yield return null;
            }

            speechRoutine = null;
            speakingWeight = 0f;
            if (animator != null && animator.layerCount > GestureLayer)
            {
                animator.CrossFadeInFixedTime("Empty", 0.4f, GestureLayer);
            }
        }

        private IEnumerator Blink()
        {
            float elapsed = 0f;
            while (elapsed < 0.16f)
            {
                elapsed += Time.deltaTime;
                blinkWeight = Mathf.Sin(elapsed / 0.16f * Mathf.PI);
                yield return null;
            }

            blinkWeight = 0f;
        }

        private void ApplyFace(bool immediate = false)
        {
            if (renderers == null) return;

            float browUp = affect == ClientAffect.Anxious ? 32f : affect == ClientAffect.Guarded ? 16f : 8f;
            float browDown = affect == ClientAffect.Guarded ? 28f : 4f;
            float smile = affect == ClientAffect.Relieved ? 16f : 0f;
            float mouthOpen = speakingWeight * 30f;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    string name = mesh.GetBlendShapeName(shapeIndex).ToLowerInvariant();
                    float target = 0f;
                    if (Matches(name, "browinnerup", "innerbrowraiser", "au1")) target = browUp;
                    else if (Matches(name, "browdown", "browlower", "au4")) target = browDown;
                    else if (Matches(name, "mouthsmile", "lipcornerpull", "au12")) target = smile;
                    else if (Matches(name, "jawopen", "mouthopen", "au26")) target = mouthOpen;
                    else if (Matches(name, "eyeblink", "blinktop", "au45")) target = blinkWeight * 100f;
                    float current = renderer.GetBlendShapeWeight(shapeIndex);
                    renderer.SetBlendShapeWeight(
                        shapeIndex,
                        immediate ? target : Mathf.MoveTowards(current, target, blendSpeed * Time.deltaTime));
                }
            }
        }

        private static bool Matches(string source, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (source.Contains(tokens[i])) return true;
            }

            return false;
        }
    }
}
