using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Research-grounded facial behaviour for a seated client (see Docs/FACIAL_BEHAVIOR.md).
    ///
    /// Layers, each driven on FACS action units through <see cref="FacialRigSemanticAdapter"/>:
    /// 1. Jaw: the humanoid jaw is restored to its bind pose every frame (Mecanim writes a
    ///    half-open jaw when clips carry no jaw curve) and opened only for speech.
    /// 2. Affect display: low-intensity AU prototypes per affect (anxious worry brow AU1+4 with lip
    ///    stretch AU20 and lip press AU24; guarded AU4+7 with lip press; thoughtful AU4 with AU14/17;
    ///    relieved Duchenne AU6+12), plus a case-level sadness tone (AU1+4+15+17, heavy lids).
    ///    Intensity drifts in slow episodes so the face is never a frozen mask.
    /// 3. Blinks: spontaneous rate by context (about 15/min listening, 24/min speaking, more when
    ///    anxious), gamma-distributed intervals, partial and double blinks, and blinks evoked by gaze
    ///    shifts, by pauses in speech and, as listener feedback, at the end of the counselor's turn.
    /// 4. Conversational signals: brief brow raises (AU1+2) at phrase onsets and brief brow
    ///    lowering (AU4) at word-search pauses while the client speaks.
    /// 5. Lid–gaze coupling: the upper lids follow vertical gaze.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
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

        // Jaw rotation (degrees) for a fully open "aa"; relaxed rest leaves the teeth just apart.
        private const float SpeechJawDegrees = 9f;
        private const float RestJawDegrees = 0.6f;

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly Dictionary<string, List<Binding>> byKey = new Dictionary<string, List<Binding>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> visemeWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private float expressionIntensity = 0.72f;
        private float sadnessTone;
        // Plan intensity (0..1) mapped to a display gain; 0.5 leaves the base profiles as tuned.
        private float affectGain = 1f;
        private float affectGainTarget = 1f;
        private float restingLid;
        private bool speaking;
        private string[] visemes = Array.Empty<string>();
        private bool externalDriven;
        private float externalLevel;
        private float speechElapsed;
        private float speechDuration = 1f;
        private string activeViseme = "AA_VI_00_Sil";
        private string previousViseme = "AA_VI_00_Sil";
        private float speechOpenness;
        private float asymmetry;
        private float asymmetryTarget;
        private float nextAsymmetryShift;

        // Blink state.
        private float blinkElapsed = -1f;
        private float blinkDuration = 0.2f;
        private float blinkDepth = 1f;
        private float nextBlink;
        private float sinceBlink = 99f;
        private float pendingBlinkDelay = -1f;
        private float pendingBlinkDuration;
        private bool doubleBlinkQueued;

        // Slow intensity episodes and brief conversational pulses.
        private float episodeGain = 1f;
        private float episodeTarget = 1f;
        private float nextEpisode;
        private float browPulseElapsed = -1f;
        private float browPulseStrength;
        private float browPulseDelay = -1f;
        private float thinkPulseElapsed = -1f;
        private float lastBrowPulse = -99f;

        // Jaw and gaze.
        private Animator animator;
        private Transform jaw;
        private Transform head;
        private Quaternion jawBindRelativeToHead = Quaternion.identity;
        private bool jawControllable;
        private float jawDegrees;
        private float jawVelocity;
        private ClientGazeController gaze;
        private ClientGazeState lastGazeState;

        public int BlendShapeCount => bindings.Count;
        public int SemanticChannelCount => byKey.Count;
        public int SuppressedCombinedShapeCount { get; private set; }
        public float JawDegrees => jawDegrees;
        public string ActiveCueSummary => $"{affect} · {activeViseme.Replace("AA_VI_", string.Empty)} · FACS";

        public void Initialize(AvatarPresentationDefinition presentation, ClientProfileDefinition profile = null)
        {
            expressionIntensity = presentation == null ? 0.72f : presentation.ExpressionIntensity;
            sadnessTone = FacialTone.SadnessFor(profile);
            restingLid = FacialTone.RestingLidFor(presentation);
            bindings.Clear();
            byKey.Clear();
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    string key = FacialRigSemanticAdapter.ToSemantic(mesh.GetBlendShapeName(index));
                    if (key.Length == 0) continue;
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

            InitializeJaw();
            if (gaze != null) gaze.GazeShifted -= OnGazeShifted;
            gaze = GetComponent<ClientGazeController>();
            if (gaze != null && isActiveAndEnabled) gaze.GazeShifted += OnGazeShifted;
            nextBlink = SampleBlinkInterval();
            nextAsymmetryShift = UnityEngine.Random.Range(3.5f, 7f);
            nextEpisode = UnityEngine.Random.Range(2f, 5f);
        }

        private void OnEnable()
        {
            CounselorBodyController.Submitted += OnCounselorTurnEnded;
            if (gaze != null) { gaze.GazeShifted -= OnGazeShifted; gaze.GazeShifted += OnGazeShifted; }
        }

        private void OnDisable()
        {
            CounselorBodyController.Submitted -= OnCounselorTurnEnded;
            if (gaze != null) gaze.GazeShifted -= OnGazeShifted;
        }

        public void SetIntensity(float intensity)
        {
            affectGainTarget = Mathf.Lerp(0.55f, 1.45f, Mathf.Clamp01(intensity));
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
            // Speakers mark the start of a turn with a brow flash more often than not.
            TriggerBrowPulse(UnityEngine.Random.Range(0.55f, 0.9f), UnityEngine.Random.Range(0.05f, 0.25f));
        }

        /// <summary>Audio-driven speech: the mouth follows <see cref="SetExternalLevel"/>.</summary>
        public void BeginExternalSpeech()
        {
            visemes = Array.Empty<string>();
            externalLevel = 0f;
            externalDriven = true;
            speaking = true;
        }

        public void SetExternalLevel(float level) => externalLevel = Mathf.Clamp01(level);

        public void EndSpeech()
        {
            externalDriven = false;
            externalLevel = 0f;
            speaking = false;
            activeViseme = "AA_VI_00_Sil";
            // Turn-final blink as the floor is handed back.
            QueueBlink(UnityEngine.Random.Range(0.08f, 0.3f), 0.22f, 0.55f);
        }

        private void OnCounselorTurnEnded()
        {
            // Listener feedback: a slightly longer blink and an attentive brow lift once the
            // counselor finishes (Hömke, Holler & Levinson, 2017).
            QueueBlink(UnityEngine.Random.Range(0.2f, 0.5f), 0.32f, 0.7f);
            TriggerBrowPulse(UnityEngine.Random.Range(0.35f, 0.6f), UnityEngine.Random.Range(0.15f, 0.4f));
        }

        private void OnGazeShifted(ClientGazeState next)
        {
            // Large gaze shifts are often accompanied by a blink (Evinger et al., 1994).
            bool large = next != lastGazeState &&
                         (next == ClientGazeState.BriefAvert || next == ClientGazeState.DownwardReflection ||
                          next == ClientGazeState.RecallSearch || lastGazeState != ClientGazeState.CounselorContact);
            lastGazeState = next;
            if (large) QueueBlink(0f, 0.2f, 0.45f);
        }

        private void Update()
        {
            if (bindings.Count == 0) return;
            float dt = Time.deltaTime;
            UpdateBlink(dt);
            UpdateSpeech(dt);
            UpdateAsymmetry(dt);
            UpdateEpisodes(dt);
            affectGain = Mathf.MoveTowards(affectGain, affectGainTarget, dt * 0.8f);
            UpdatePulses(dt);

            float guarded = relationalState.Guardedness;
            float safety = relationalState.Safety;
            float gazePitch = gaze != null ? gaze.GazePitchDegrees : 0f;
            foreach (Binding binding in bindings)
            {
                if (!binding.active) continue;
                bool isViseme = binding.key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase);
                bool isBlink = IsBlink(binding.key);
                float target = ResolveTarget(binding.key, guarded, safety, gazePitch);
                if (!isViseme && !isBlink) target *= expressionIntensity * episodeGain * affectGain;
                target *= ResolveSideMultiplier(binding.key);
                // Expressions rise faster than they fade (Krumhuber et al., 2013; Cohn & Schmidt, 2004).
                float smoothTime = isViseme ? 0.065f : isBlink ? 0.03f : target > binding.current ? 0.14f : 0.34f;
                float maxSpeed = isViseme ? 430f : isBlink ? 1400f : 170f;
                binding.current = FacialMorphDynamics.Step(binding.current, target, ref binding.velocity, smoothTime, maxSpeed, dt);
                binding.renderer.SetBlendShapeWeight(binding.index, binding.current);
            }
        }

        private void LateUpdate()
        {
            // Runs after the Animator, the gaze IK and the gesture pose (execution order 900), which
            // all rewrite the humanoid jaw muscle; put the jaw back to its closed bind pose.
            if (!jawControllable || jaw == null || head == null) return;
            float target = speaking ? Mathf.Lerp(RestJawDegrees, SpeechJawDegrees, speechOpenness) : RestJawDegrees;
            jawDegrees = Mathf.SmoothDamp(jawDegrees, target, ref jawVelocity, speaking ? 0.055f : 0.12f, Mathf.Infinity, Time.deltaTime);
            jaw.rotation = head.rotation * jawBindRelativeToHead * Quaternion.Euler(0f, 0f, -jawDegrees);
        }

        private void InitializeJaw()
        {
            jawControllable = false;
            animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;
            jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            // The opening axis below is the Character Creator / ActorCore jaw (local -Z opens).
            if (jaw == null || head == null || !jaw.name.StartsWith("CC_Base", StringComparison.Ordinal)) return;
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                int j = Array.IndexOf(renderer.bones, jaw);
                int h = Array.IndexOf(renderer.bones, head);
                if (j < 0 || h < 0 || j >= mesh.bindposes.Length || h >= mesh.bindposes.Length) continue;
                Quaternion jawBind = (renderer.transform.localToWorldMatrix * mesh.bindposes[j].inverse).rotation;
                Quaternion headBind = (renderer.transform.localToWorldMatrix * mesh.bindposes[h].inverse).rotation;
                jawBindRelativeToHead = Quaternion.Inverse(headBind) * jawBind;
                jawControllable = true;
                jawDegrees = RestJawDegrees;
                return;
            }
        }

        // ---- Blinks -------------------------------------------------------------------------

        private float BlinkRatePerMinute()
        {
            // Spontaneous blink rate: about 17/min at rest and 26/min in conversation (Bentivoglio
            // et al., 1997); listening sits between. Anxiety raises it (Harrigan & O'Connell, 1996).
            float rate = speaking ? 24f : 15f;
            rate *= affect switch
            {
                ClientAffect.Anxious => 1.35f,
                ClientAffect.Guarded => 1.1f,
                ClientAffect.Thoughtful => 0.95f,
                _ => 0.95f
            };
            return rate * (1f + 0.15f * relationalState.Guardedness);
        }

        private float SampleBlinkInterval()
        {
            // Gamma(k = 2) around the mean interval: irregular, but never machine-gun short.
            float mean = 60f / Mathf.Max(4f, BlinkRatePerMinute());
            float u1 = Mathf.Max(1e-4f, UnityEngine.Random.value);
            float u2 = Mathf.Max(1e-4f, UnityEngine.Random.value);
            float sample = -(mean / 2f) * Mathf.Log(u1 * u2);
            return Mathf.Clamp(sample, 0.7f, 11f);
        }

        private void QueueBlink(float delay, float duration, float probability)
        {
            if (UnityEngine.Random.value > probability) return;
            if (blinkElapsed >= 0f || sinceBlink < 0.6f) return;
            if (pendingBlinkDelay >= 0f && pendingBlinkDelay <= delay) return;
            pendingBlinkDelay = delay;
            pendingBlinkDuration = duration;
        }

        private void StartBlink(float duration, float depth)
        {
            blinkElapsed = 0f;
            blinkDuration = duration;
            blinkDepth = depth;
            sinceBlink = 0f;
            nextBlink = SampleBlinkInterval();
        }

        private void UpdateBlink(float dt)
        {
            sinceBlink += dt;
            if (blinkElapsed >= 0f)
            {
                blinkElapsed += dt;
                if (blinkElapsed >= blinkDuration)
                {
                    blinkElapsed = -1f;
                    if (doubleBlinkQueued) { doubleBlinkQueued = false; pendingBlinkDelay = 0.09f; pendingBlinkDuration = 0.17f; }
                }
                return;
            }
            if (pendingBlinkDelay >= 0f)
            {
                pendingBlinkDelay -= dt;
                if (pendingBlinkDelay < 0f) StartBlink(pendingBlinkDuration, 1f);
                return;
            }
            nextBlink -= dt;
            if (nextBlink > 0f) return;
            float roll = UnityEngine.Random.value;
            // Most spontaneous blinks are complete; some are partial, a few come in pairs.
            if (roll < 0.15f) StartBlink(UnityEngine.Random.Range(0.13f, 0.17f), UnityEngine.Random.Range(0.45f, 0.7f));
            else
            {
                StartBlink(UnityEngine.Random.Range(0.17f, 0.24f), 1f);
                doubleBlinkQueued = roll > 0.92f;
            }
        }

        // ---- Speech -------------------------------------------------------------------------

        private void UpdateSpeech(float dt)
        {
            if (speaking && externalDriven)
            {
                // No text timing in a live stream: open the mouth with the audio level and
                // drift between vowel shapes so it does not look like a single flapping pose.
                float drift = Mathf.PerlinNoise(Time.time * 5.5f, 0.71f);
                string vowel = drift < 0.3f ? "AA_VI_10_aa" : drift < 0.5f ? "AA_VI_11_E" : drift < 0.72f ? "AA_VI_13_O" : "AA_VI_12_I";
                float level = Mathf.SmoothStep(0f, 1f, externalLevel);
                visemeWeights.Clear();
                visemeWeights[vowel] = level;
                visemeWeights["AA_VI_00_Sil"] = 1f - level;
                activeViseme = level > 0.2f ? vowel : "AA_VI_00_Sil";
                speechOpenness = level * VisemeOpenness(vowel);
                return;
            }
            if (!speaking || visemes.Length == 0)
            {
                activeViseme = "AA_VI_00_Sil";
                visemeWeights.Clear();
                visemeWeights[activeViseme] = 1f;
                speechOpenness = 0f;
                previousViseme = activeViseme;
                return;
            }
            speechElapsed += dt;
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

            bool silent = activeViseme.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase);
            bool wasSilent = previousViseme.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase);
            if (silent && !wasSilent)
            {
                // Blinks cluster at speech pauses; some pauses show a brief word-search frown.
                QueueBlink(UnityEngine.Random.Range(0f, 0.08f), 0.19f, 0.3f);
                if (UnityEngine.Random.value < 0.14f) thinkPulseElapsed = 0f;
            }
            else if (!silent && wasSilent && Time.time - lastBrowPulse > 1.4f && UnityEngine.Random.value < 0.22f)
            {
                // Phrase-initial emphasis: eyebrow flash (Ekman, 1979; Cavé et al., 1996).
                TriggerBrowPulse(UnityEngine.Random.Range(0.45f, 0.85f), 0f);
            }
            previousViseme = activeViseme;
        }

        // ---- Expression targets -----------------------------------------------------------------

        private float ResolveTarget(string key, float guarded, float safety, float gazePitch)
        {
            if (key.StartsWith("AA_VI_", StringComparison.OrdinalIgnoreCase))
            {
                if (!speaking) return key.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase) ? 8f : 0f;
                return visemeWeights.TryGetValue(key, out float weight) ? weight * 34f : 0f;
            }

            if (IsBlink(key))
            {
                float blink = FacialMorphDynamics.BlinkWeight(blinkElapsed, blinkDuration) * blinkDepth * 90f;
                // Heavy lids with sadness, and lids that follow the eyes down.
                // A relaxed upper lid rests on the top of the iris; scanned faces often sit too wide.
                float droop = restingLid + sadnessTone * 12f + Mathf.Clamp01(-gazePitch / 22f) * 10f;
                return Mathf.Max(blink, droop);
            }

            if (key.StartsWith("EYE_", StringComparison.OrdinalIgnoreCase))
            {
                if (key.EndsWith("LookDown", StringComparison.OrdinalIgnoreCase)) return Mathf.Clamp01(-gazePitch / 25f) * 55f;
                if (key.EndsWith("LookUp", StringComparison.OrdinalIgnoreCase)) return Mathf.Clamp01(gazePitch / 20f) * 40f;
                return 0f;
            }

            float brow = BrowPulse();
            float think = ThinkPulse();
            // Relational safety relaxes the guarded components; guardedness tightens them.
            float tension = Mathf.Lerp(1.15f, 0.7f, safety) * (1f + 0.35f * guarded);
            float lipsFree = speaking ? 0.2f : 1f;
            float s = sadnessTone;

            if (key.Contains("AU_01_")) return AffectValue(13f, 2f, 4f, 3f) + 10f * s + 16f * brow;
            if (key.Contains("AU_02_")) return AffectValue(3f, 0f, 1f, 1f) + 14f * brow;
            if (key.Contains("AU_04_")) return AffectValue(4f, 10f, 6f, 0f) * tension + 5f * s + 10f * think + 4f * guarded;
            if (key.Contains("AU_05_")) return AffectValue(3f, 0f, 0f, 0f);
            if (key.Contains("AU_06_")) return affect == ClientAffect.Relieved ? (5f + safety * 5f) * (1f - 0.5f * s) : 0f;
            if (key.Contains("AU_07_")) return AffectValue(2f, 9f, 4f, 1f) * tension + 3f * think + restingLid * 0.35f;
            if (key.Contains("AU_12_")) return (affect == ClientAffect.Relieved ? 10f + safety * 8f : 3f + safety * 6f) * (1f - 0.5f * s) * (speaking ? 0.6f : 1f);
            if (key.Contains("AU_14_"))
            {
                // A unilateral dimpler reads as suppressed doubt; keep it to one side when guarded.
                float dimpler = AffectValue(0f, 5f, 4f, 0f);
                return affect == ClientAffect.Guarded && FacialRigSemanticAdapter.IsRight(key) ? dimpler * 0.2f : dimpler;
            }
            if (key.Contains("AU_15_")) return AffectValue(0f, 2f, 0f, 0f) + 8f * s;
            if (key.Contains("AU_17_")) return (1f + AffectValue(3f, 5f, 4f, 0f) + 5f * s) * (speaking ? 0.4f : 1f);
            if (key.Contains("AU_20_")) return AffectValue(3f, 0f, 0f, 0f) * (speaking ? 0.5f : 1f);
            if (key.Contains("AU_23_")) return AffectValue(0f, 5f, 0f, 0f) * tension * lipsFree;
            if (key.Contains("AU_24_")) return AffectValue(3f, 7f, 2f, 0f) * tension * lipsFree;
            if (key.Contains("AU_25_")) return speaking ? 3f + speechOpenness * 6f : affect == ClientAffect.Relieved ? 2f : 0f;
            if (key.Contains("AU_26_")) return speaking ? speechOpenness * 9f : 0f;
            if (key.Contains("AU_41_")) return affect == ClientAffect.Thoughtful ? 7f : 0f;
            return 0f;
        }

        private float AffectValue(float anxious, float guarded, float thoughtful, float relieved)
        {
            return affect switch
            {
                ClientAffect.Anxious => anxious,
                ClientAffect.Guarded => guarded,
                ClientAffect.Thoughtful => thoughtful,
                _ => relieved
            };
        }

        // ---- Pulses, episodes and asymmetry ----------------------------------------------------------

        private void TriggerBrowPulse(float strength, float delay)
        {
            browPulseStrength = strength;
            browPulseDelay = delay;
            lastBrowPulse = Time.time;
        }

        private void UpdatePulses(float dt)
        {
            if (browPulseDelay >= 0f)
            {
                browPulseDelay -= dt;
                if (browPulseDelay < 0f) browPulseElapsed = 0f;
            }
            if (browPulseElapsed >= 0f)
            {
                browPulseElapsed += dt;
                if (browPulseElapsed > 0.75f) browPulseElapsed = -1f;
            }
            if (thinkPulseElapsed >= 0f)
            {
                thinkPulseElapsed += dt;
                if (thinkPulseElapsed > 1.1f) thinkPulseElapsed = -1f;
            }
        }

        private float BrowPulse() => browPulseElapsed < 0f ? 0f : Envelope(browPulseElapsed, 0.12f, 0.18f, 0.45f) * browPulseStrength;
        private float ThinkPulse() => thinkPulseElapsed < 0f ? 0f : Envelope(thinkPulseElapsed, 0.2f, 0.4f, 0.5f);

        private static float Envelope(float t, float onset, float apex, float offset)
        {
            if (t < onset) return FacialMorphDynamics.Ease(t / onset);
            if (t < onset + apex) return 1f;
            return 1f - FacialMorphDynamics.Ease((t - onset - apex) / offset);
        }

        private void UpdateEpisodes(float dt)
        {
            nextEpisode -= dt;
            if (nextEpisode <= 0f)
            {
                episodeTarget = UnityEngine.Random.Range(0.7f, 1.15f);
                nextEpisode = UnityEngine.Random.Range(4f, 9f);
            }
            episodeGain = Mathf.Lerp(episodeGain, episodeTarget, 1f - Mathf.Exp(-dt * 0.9f));
        }

        private void UpdateAsymmetry(float dt)
        {
            nextAsymmetryShift -= dt;
            if (nextAsymmetryShift <= 0f)
            {
                asymmetryTarget = UnityEngine.Random.Range(-0.08f, 0.08f);
                nextAsymmetryShift = UnityEngine.Random.Range(3.5f, 7f);
            }
            asymmetry = Mathf.Lerp(asymmetry, asymmetryTarget, 1f - Mathf.Exp(-dt * 0.8f));
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
            // Bilabials close the lips completely.
            if (key.EndsWith("01_PP", StringComparison.OrdinalIgnoreCase)) return 0f;
            return key.EndsWith("00_Sil", StringComparison.OrdinalIgnoreCase) ? 0f : 0.25f;
        }

        private static bool IsBlink(string key)
        {
            return key.Contains("AU_45_Blink") || key.Contains("AU_43_");
        }
    }

    /// <summary>Case-level facial tone derived from the client profile.</summary>
    public static class FacialTone
    {
        /// <summary>Upper-lid closure (blendshape weight) for a relaxed resting eye.</summary>
        public static float RestingLidFor(AvatarPresentationDefinition presentation)
        {
            string id = presentation == null ? string.Empty : presentation.PresentationId;
            // This actor's neutral eye opening shows sclera above and below the iris.
            if (id.Contains("international-belonging")) return 17f;
            return 7f;
        }

        public static float SadnessFor(ClientProfileDefinition profile)
        {
            if (profile == null) return 0.1f;
            string id = (profile.ProfileId + " " + profile.CounselingDomain).ToLowerInvariant();
            if (id.Contains("bereave") || id.Contains("사별") || id.Contains("애도")) return 0.6f;
            if (id.Contains("adolescent") || id.Contains("청소년")) return 0.2f;
            return 0.1f;
        }
    }
}
