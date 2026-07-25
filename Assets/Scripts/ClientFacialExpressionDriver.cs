using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class ClientFacialExpressionDriver : MonoBehaviour
    {
        private sealed class Binding
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public string key;
            public float current;
            public float velocity;
            public bool active = true;
        }

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly Dictionary<string, List<Binding>> byKey = new Dictionary<string, List<Binding>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> visemeWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private float expressionIntensity = 0.72f;
        private float blinkElapsed = -1f;
        private float nextBlink;
        private bool speaking;
        private string[] visemes = Array.Empty<string>();
        private float speechElapsed;
        private float speechDuration = 1f;
        private string activeViseme = "AA_VI_00_Sil";
        private float speechOpenness;
        private float asymmetry;
        private float asymmetryTarget;
        private float nextAsymmetryShift;

        public int BlendShapeCount => bindings.Count;
        public int SemanticChannelCount => byKey.Count;
        public int SuppressedCombinedShapeCount { get; private set; }
        public string ActiveCueSummary => $"{affect} · {activeViseme.Replace("AA_VI_", string.Empty)} · HF morph";

        public void Initialize(AvatarPresentationDefinition presentation)
        {
            expressionIntensity = presentation == null ? 0.72f : presentation.ExpressionIntensity;
            bindings.Clear();
            byKey.Clear();
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    string key = FacialRigSemanticAdapter.Normalize(mesh.GetBlendShapeName(index));
                    if (!key.StartsWith("AU_", StringComparison.OrdinalIgnoreCase) &&
                        !key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase)) continue;
                    Binding binding = new Binding { renderer = renderer, index = index, key = key };
                    bindings.Add(binding);
                    if (!byKey.TryGetValue(key, out List<Binding> list))
                    {
                        list = new List<Binding>();
                        byKey.Add(key, list);
                    }
                    list.Add(binding);
                }
            }

            HashSet<string> shadowed = FacialRigSemanticAdapter.FindCombinedShapesShadowedByLaterals(byKey.Keys);
            if (byKey.ContainsKey("AU_43_L_EyeClosed") && byKey.ContainsKey("AU_43_R_EyeClosed"))
            {
                shadowed.Add("AU_45_Blink");
            }
            SuppressedCombinedShapeCount = 0;
            foreach (Binding binding in bindings)
            {
                if (!shadowed.Contains(binding.key)) continue;
                binding.active = false;
                binding.renderer.SetBlendShapeWeight(binding.index, 0f);
                SuppressedCombinedShapeCount++;
            }
            nextBlink = UnityEngine.Random.Range(2.2f, 4.8f);
            nextAsymmetryShift = UnityEngine.Random.Range(3.5f, 7f);
        }

        public void SetContext(ClientAffect clientAffect, ClientRelationalState state)
        {
            affect = clientAffect;
            relationalState = state;
        }

        public void BeginSpeech(string text, float duration)
        {
            visemes = KoreanVisemePlanner.Build(text);
            speechElapsed = 0f;
            speechDuration = Mathf.Max(0.5f, duration);
            speaking = true;
        }

        public void EndSpeech()
        {
            speaking = false;
            activeViseme = "AA_VI_00_Sil";
        }

        private void Update()
        {
            if (bindings.Count == 0) return;
            UpdateBlink();
            UpdateSpeech();
            UpdateAsymmetry();

            float guarded = relationalState.Guardedness;
            float safety = relationalState.Safety;
            float micro = Mathf.PerlinNoise(Time.time * 0.24f, 0.37f) - 0.5f;
            foreach (Binding binding in bindings)
            {
                if (!binding.active) continue;
                float target = ResolveTarget(binding.key, guarded, safety, micro) * expressionIntensity;
                target *= ResolveSideMultiplier(binding.key);
                bool isViseme = binding.key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase);
                float smoothTime = isViseme ? 0.065f : IsBlink(binding.key) ? 0.035f : 0.16f;
                float maxSpeed = isViseme ? 430f : IsBlink(binding.key) ? 1200f : 170f;
                binding.current = FacialMorphDynamics.Step(binding.current, target, ref binding.velocity, smoothTime, maxSpeed, Time.deltaTime);
                binding.renderer.SetBlendShapeWeight(binding.index, binding.current);
            }
        }

        private void UpdateBlink()
        {
            if (blinkElapsed >= 0f)
            {
                blinkElapsed += Time.deltaTime;
                if (blinkElapsed >= 0.175f)
                {
                    blinkElapsed = -1f;
                    nextBlink = UnityEngine.Random.Range(2.5f, 5.4f);
                }
                return;
            }
            nextBlink -= Time.deltaTime;
            if (nextBlink <= 0f) blinkElapsed = 0f;
        }

        private void UpdateSpeech()
        {
            if (!speaking || visemes.Length == 0)
            {
                activeViseme = "AA_VI_00_Sil";
                visemeWeights.Clear();
                visemeWeights[activeViseme] = 1f;
                speechOpenness = 0f;
                return;
            }
            speechElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(speechElapsed / speechDuration);
            float position = progress * Mathf.Max(1, visemes.Length - 1);
            int left = Mathf.Min(visemes.Length - 1, Mathf.FloorToInt(position));
            int right = Mathf.Min(visemes.Length - 1, left + 1);
            float blend = FacialMorphDynamics.Ease(position - left);
            visemeWeights.Clear();
            AddVisemeWeight(visemes[left], 1f - blend);
            AddVisemeWeight(visemes[right], blend);
            activeViseme = blend < 0.5f ? visemes[left] : visemes[right];
            speechOpenness = Mathf.Lerp(VisemeOpenness(visemes[left]), VisemeOpenness(visemes[right]), blend);
        }

        private float ResolveTarget(string key, float guarded, float safety, float micro)
        {
            if (key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase))
            {
                if (!speaking) return key.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase) ? 8f : 0f;
                return visemeWeights.TryGetValue(key, out float weight) ? weight * 34f : 0f;
            }

            if (IsBlink(key)) return FacialMorphDynamics.BlinkWeight(blinkElapsed) * 88f;
            if (key.Contains("AU_04_")) return (affect == ClientAffect.Guarded ? 17f : affect == ClientAffect.Anxious ? 8f : 2f) + guarded * 5f;
            if (key.Contains("AU_01_")) return (affect == ClientAffect.Anxious ? 10f : affect == ClientAffect.Thoughtful ? 6f : 2f) * (affect == ClientAffect.Guarded ? 0.4f : 1f);
            if (key.Contains("AU_05_")) return affect == ClientAffect.Anxious ? 6f : 1f;
            if (key.Contains("AU_06_")) return affect == ClientAffect.Relieved ? 8f + safety * 4f : 0f;
            if (key.Contains("AU_07_")) return affect == ClientAffect.Guarded ? 10f + guarded * 5f : 2f;
            if (key.Contains("AU_12_")) return affect == ClientAffect.Relieved ? 12f + safety * 5f : 0f;
            if (key.Contains("AU_14_")) return affect == ClientAffect.Thoughtful ? 5f : 0f;
            if (key.Contains("AU_15_")) return affect == ClientAffect.Guarded ? 7f : 0f;
            if (key.Contains("AU_17_")) return affect == ClientAffect.Anxious ? 5f : 1f;
            if (key.Contains("AU_23_") || key.Contains("AU_24_")) return (affect == ClientAffect.Guarded ? 8f + guarded * 5f : Mathf.Max(0f, micro * 2f)) * (speaking ? 0.22f : 1f);
            if (key.Contains("AU_25_")) return speaking ? 3f + speechOpenness * 6f : 0f;
            if (key.Contains("AU_26_")) return speaking ? speechOpenness * 9f : 0f;
            if (key.Contains("AU_41_")) return affect == ClientAffect.Thoughtful ? 7f : 0f;
            return 0f;
        }

        private void UpdateAsymmetry()
        {
            nextAsymmetryShift -= Time.deltaTime;
            if (nextAsymmetryShift <= 0f)
            {
                asymmetryTarget = UnityEngine.Random.Range(-0.055f, 0.055f);
                nextAsymmetryShift = UnityEngine.Random.Range(3.5f, 7f);
            }
            asymmetry = Mathf.Lerp(asymmetry, asymmetryTarget, 1f - Mathf.Exp(-Time.deltaTime * 0.8f));
        }

        private float ResolveSideMultiplier(string key)
        {
            if (FacialRigSemanticAdapter.IsLeft(key)) return 1f + asymmetry;
            if (FacialRigSemanticAdapter.IsRight(key)) return 1f - asymmetry;
            return 1f;
        }

        private void AddVisemeWeight(string key, float amount)
        {
            if (visemeWeights.TryGetValue(key, out float current)) visemeWeights[key] = current + amount;
            else visemeWeights[key] = amount;
        }

        private static float VisemeOpenness(string key)
        {
            if (key.EndsWith("10_aa", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (key.EndsWith("11_E", StringComparison.OrdinalIgnoreCase) || key.EndsWith("13_O", StringComparison.OrdinalIgnoreCase)) return 0.75f;
            if (key.EndsWith("12_I", StringComparison.OrdinalIgnoreCase) || key.EndsWith("14_U", StringComparison.OrdinalIgnoreCase)) return 0.55f;
            if (key.EndsWith("04_DD", StringComparison.OrdinalIgnoreCase) || key.EndsWith("05_KK", StringComparison.OrdinalIgnoreCase)) return 0.42f;
            return key.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase) ? 0f : 0.25f;
        }

        private static bool IsBlink(string key)
        {
            return key.Contains("AU_45_Blink") || key.Contains("AU_43_");
        }
    }
}
