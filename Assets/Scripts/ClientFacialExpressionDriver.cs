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
        }

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly Dictionary<string, List<Binding>> byKey = new Dictionary<string, List<Binding>>(StringComparer.OrdinalIgnoreCase);
        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private float expressionIntensity = 0.72f;
        private float blinkPhase = -1f;
        private float nextBlink;
        private bool speaking;
        private string[] visemes = Array.Empty<string>();
        private float speechElapsed;
        private float speechDuration = 1f;
        private string activeViseme = "AA_VI_00_Sil";

        public int BlendShapeCount => bindings.Count;
        public string ActiveCueSummary => $"{affect} · {activeViseme.Replace("AA_VI_", string.Empty)}";

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
                    string fullName = mesh.GetBlendShapeName(index);
                    string key = Normalize(fullName);
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
            nextBlink = UnityEngine.Random.Range(2.2f, 4.8f);
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

            float guarded = relationalState.Guardedness;
            float safety = relationalState.Safety;
            float micro = Mathf.PerlinNoise(Time.time * 0.24f, 0.37f) - 0.5f;
            foreach (Binding binding in bindings)
            {
                float target = ResolveTarget(binding.key, guarded, safety, micro) * expressionIntensity;
                float speed = binding.key.StartsWith("AA_VI_") ? 150f : 72f;
                binding.current = Mathf.MoveTowards(binding.current, target, speed * Time.deltaTime);
                binding.renderer.SetBlendShapeWeight(binding.index, binding.current);
            }
        }

        private void UpdateBlink()
        {
            if (blinkPhase >= 0f)
            {
                blinkPhase += Time.deltaTime / 0.16f;
                if (blinkPhase >= 1f)
                {
                    blinkPhase = -1f;
                    nextBlink = UnityEngine.Random.Range(2.5f, 5.4f);
                }
                return;
            }
            nextBlink -= Time.deltaTime;
            if (nextBlink <= 0f) blinkPhase = 0f;
        }

        private void UpdateSpeech()
        {
            if (!speaking || visemes.Length == 0)
            {
                activeViseme = "AA_VI_00_Sil";
                return;
            }
            speechElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(speechElapsed / speechDuration);
            int index = Mathf.Min(visemes.Length - 1, Mathf.FloorToInt(progress * visemes.Length));
            activeViseme = visemes[index];
        }

        private float ResolveTarget(string key, float guarded, float safety, float micro)
        {
            if (key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase))
            {
                if (!speaking) return key.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase) ? 8f : 0f;
                return string.Equals(key, activeViseme, StringComparison.OrdinalIgnoreCase) ? 38f : 0f;
            }

            if (key.Contains("AU_45_Blink") || key.Contains("AU_43_"))
                return blinkPhase < 0f ? 0f : Mathf.Sin(blinkPhase * Mathf.PI) * 92f;
            if (key.Contains("AU_04_")) return (affect == ClientAffect.Guarded ? 20f : affect == ClientAffect.Anxious ? 9f : 3f) + guarded * 7f;
            if (key.Contains("AU_01_")) return affect == ClientAffect.Anxious ? 12f : affect == ClientAffect.Thoughtful ? 7f : 3f;
            if (key.Contains("AU_05_")) return affect == ClientAffect.Anxious ? 6f : 1f;
            if (key.Contains("AU_06_")) return affect == ClientAffect.Relieved ? 8f + safety * 4f : 0f;
            if (key.Contains("AU_07_")) return affect == ClientAffect.Guarded ? 10f + guarded * 5f : 2f;
            if (key.Contains("AU_12_")) return affect == ClientAffect.Relieved ? 12f + safety * 5f : 0f;
            if (key.Contains("AU_14_")) return affect == ClientAffect.Thoughtful ? 5f : 0f;
            if (key.Contains("AU_15_")) return affect == ClientAffect.Guarded ? 7f : 0f;
            if (key.Contains("AU_17_")) return affect == ClientAffect.Anxious ? 5f : 1f;
            if (key.Contains("AU_23_") || key.Contains("AU_24_")) return affect == ClientAffect.Guarded ? 8f + guarded * 5f : Mathf.Max(0f, micro * 2f);
            if (key.Contains("AU_25_") || key.Contains("AU_26_")) return speaking ? 7f : 0f;
            if (key.Contains("AU_41_")) return affect == ClientAffect.Thoughtful ? 7f : 0f;
            return 0f;
        }

        private static string Normalize(string name)
        {
            int separator = name.LastIndexOf('.');
            return separator >= 0 ? name.Substring(separator + 1) : name;
        }
    }
}
