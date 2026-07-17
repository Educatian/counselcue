using System.Collections;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum ClientAffect
    {
        Guarded,
        Anxious,
        Relieved
    }

    [DisallowMultipleComponent]
    public sealed class ClientAvatarController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform lookTarget;
        [SerializeField, Min(1f)] private float blendSpeed = 95f;

        private SkinnedMeshRenderer[] renderers;
        private ClientAffect affect = ClientAffect.Anxious;
        private float speakingWeight;
        private float blinkWeight;
        private float nextBlink;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            nextBlink = Random.Range(1.8f, 4.2f);
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

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || lookTarget == null)
            {
                return;
            }

            float gaze = affect == ClientAffect.Guarded ? 0.28f : 0.78f;
            animator.SetLookAtWeight(gaze, 0.16f, 0.82f, 0.70f, 0.48f);
            animator.SetLookAtPosition(lookTarget.position);
        }

        public void SetAffect(ClientAffect value, bool immediate = false)
        {
            affect = value;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                string state = value == ClientAffect.Relieved ? "Thoughtful" : value == ClientAffect.Guarded ? "Waiting" : "Idle";
                animator.CrossFadeInFixedTime(state, immediate ? 0f : 0.35f);
            }

            if (immediate)
            {
                ApplyFace(true);
            }
        }

        public void Speak(string text)
        {
            StopAllCoroutines();
            StartCoroutine(SpeechRoutine(Mathf.Clamp(text.Length * 0.055f, 1.2f, 5.5f)));
        }

        private IEnumerator SpeechRoutine(float duration)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.CrossFadeInFixedTime("Talk", 0.22f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                speakingWeight = Mathf.Abs(Mathf.Sin(elapsed * 11.4f));
                yield return null;
            }

            speakingWeight = 0f;
            SetAffect(affect);
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
            if (renderers == null)
            {
                return;
            }

            float browUp = affect == ClientAffect.Anxious ? 42f : affect == ClientAffect.Guarded ? 18f : 10f;
            float browDown = affect == ClientAffect.Guarded ? 38f : 5f;
            float smile = affect == ClientAffect.Relieved ? 22f : 0f;
            float mouthOpen = speakingWeight * 48f;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

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
                    renderer.SetBlendShapeWeight(shapeIndex, immediate ? target : Mathf.MoveTowards(current, target, blendSpeed * Time.deltaTime));
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
