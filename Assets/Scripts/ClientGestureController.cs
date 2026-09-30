using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Procedural, research-informed seated gestures for the client (see Docs/GESTURES.md).
    ///
    /// The Rocketbox base clips were recorded sitting at a table, so on an armchair the hands
    /// float in the air. This controller places both hands with humanoid IK on targets computed
    /// every frame from the avatar's own skeleton (thighs, knees, lap, chest, chin, neck, the
    /// chair's armrests), so poses fit any body — adult, teen or older client, Rocketbox or
    /// ActorCore — and moves between them with minimum-jerk arcs and Kendon's gesture phases
    /// (preparation → stroke → hold → retraction). On top it layers trunk lean, head nods and
    /// tilts, shoulder shrugs and finger curl.
    ///
    /// What happens when is driven by the client's affect, the relational state and a
    /// per-client <see cref="ClientGestureStyle"/>: anxious clients produce more self-adaptors
    /// (hand rubbing, neck touch, thigh rubbing) and fewer illustrators; guarded clients close
    /// their posture and lean back; safety opens the posture and brings forward lean and palm-up
    /// illustrators; grief slows and shrinks everything.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ClientGestureController : MonoBehaviour
    {
        private enum Anchor
        {
            ThighSame, ThighOther, Lap, Knees, RibsSame, RibsOther, Chest, Chin, Mouth, Cheek, Forehead, Ear, Neck, Armrest, GestureSpace,
            Frozen // an in-flight pose captured relative to the hips when a motion is interrupted
        }

        /// <summary>
        /// A hand target in the hand's own body frame: x points outward on the hand's side
        /// (right for the right hand, left for the left hand), y up, z forward. Offsets are metres
        /// for an average adult and scale with <see cref="Animator.humanScale"/>.
        /// </summary>
        private struct HandSpec
        {
            public Anchor anchor;
            public float t;
            public Vector3 offset;
            public Vector3 finger;
            public Vector3 palm;
            public float curl;
            public Vector3 elbow;
            public bool elbowOnArmrest;
            public bool contact;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        private sealed class Hand
        {
            public bool right;
            public AvatarIKGoal goal;
            public AvatarIKHint hint;
            public Transform bone;
            public Transform upper;
            public Transform lower;
            public HandSpec from;
            public HandSpec to;
            public float progress = 1f;
            public float duration = 1f;
            public Vector3 oscillation;      // body-frame amplitude (m)
            public float oscillationHz;
            public bool oscillateAlongThigh;
            public float twistAmplitude;     // degrees about the palm normal (wringing)
            public float beat;               // 0..1 down-stroke envelope for beat gestures
            public float curl;
            public float fidget;
            public Vector3 fingerLocal = Vector3.forward;
            public Vector3 palmLocal = Vector3.down;
            public bool calibrated;
        }

        private Animator animator;
        private readonly Hand left = new Hand { right = false, goal = AvatarIKGoal.LeftHand, hint = AvatarIKHint.LeftElbow };
        private readonly Hand right = new Hand { right = true, goal = AvatarIKGoal.RightHand, hint = AvatarIKHint.RightElbow };
        private Transform hips, spine, chest, neck, head, leftEye, rightEye;
        private Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg, leftShoulder, rightShoulder, leftUpperArm, rightUpperArm;
        private readonly List<Bounds> armrests = new List<Bounds>();
        private HumanPoseHandler poseHandler;
        private HumanPose humanPose;
        private readonly List<int>[] fingerMuscles = { new List<int>(), new List<int>() };
        private readonly List<int>[] thumbMuscles = { new List<int>(), new List<int>() };

        private ClientGestureStyle style = new ClientGestureStyle();
        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relational = ClientRelationalState.Initial;
        private RestPose rest = RestPose.HandsClasped;
        private bool speaking;
        private float speechLevel;
        private bool externalSpeech;
        private Coroutine gesture;
        private Coroutine speechGestures;
        private float ikWeight;
        private float nextPostureShift;
        private float nextAdaptor;

        // Trunk and head channels (degrees), smoothed toward targets.
        private float lean, leanVelocity, leanTarget, gestureLean;
        private float sideLean, sideLeanVelocity, sideLeanTarget;
        private float headTilt, headTiltVelocity, headTiltTarget;
        private float headPitch, headPitchVelocity, gestureHeadDown;
        private float shrug, shrugVelocity, shrugTarget;
        private float nodTime = -1f, nodDuration, nodAmplitude;
        private int nodCount;
        private float shakeTime = -1f, shakeAmplitude;

        public RestPose CurrentRest => rest;
        public bool IsCalibrated => left.calibrated && right.calibrated;
        public string ActiveGesture { get; private set; } = "rest";

        private float Scale => animator != null && animator.isHuman ? Mathf.Max(0.5f, animator.humanScale) : 1f;

        public void Initialize(ClientProfileDefinition profile)
        {
            style = ClientGestureStyle.For(profile == null ? null : profile.ProfileId);
            if (animator == null) Setup();
            ChooseRest(true);
        }

        private void Awake()
        {
            if (animator == null) Setup();
        }

        private void Setup()
        {
            animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null) return;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            left.bone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            right.bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            left.upper = leftUpperArm;
            right.upper = rightUpperArm;
            left.lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            right.lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                string key = names[i].Replace(" ", string.Empty).Replace(".", string.Empty).ToLowerInvariant();
                if (!key.Contains("stretched")) continue;
                int side = key.StartsWith("left") ? 0 : key.StartsWith("right") ? 1 : -1;
                if (side < 0) continue;
                if (key.Contains("thumb")) thumbMuscles[side].Add(i);
                else if (key.Contains("index") || key.Contains("middle") || key.Contains("ring") || key.Contains("little")) fingerMuscles[side].Add(i);
            }

            FindArmrests();
            nextPostureShift = Random.Range(16f, 30f);
            nextAdaptor = Random.Range(5f, 10f);
        }

        private void FindArmrests()
        {
            armrests.Clear();
            GameObject chair = GameObject.Find("ClientChair");
            if (chair == null) return;
            foreach (Renderer renderer in chair.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                if (name.Contains("arm")) armrests.Add(renderer.bounds);
            }
        }

        // ---- public signals ------------------------------------------------------------------

        public void SetContext(ClientAffect value, ClientRelationalState state)
        {
            bool changed = value != affect;
            affect = value;
            relational = state;
            if (changed && gesture == null && Random.value < 0.65f) ChooseRest(false);
        }

        public void BeginSpeech(string text, float duration, bool external)
        {
            speaking = true;
            externalSpeech = external;
            if (speechGestures != null) StopCoroutine(speechGestures);
            speechGestures = StartCoroutine(SpeechGestures(text ?? string.Empty, duration));
        }

        public void SetSpeechLevel(float level) => speechLevel = Mathf.Clamp01(level);

        public void EndSpeech()
        {
            speaking = false;
            externalSpeech = false;
            speechLevel = 0f;
            if (speechGestures != null) StopCoroutine(speechGestures);
            speechGestures = null;
        }

        /// <summary>Listening backchannel after the counselor finishes a turn.</summary>
        public void Acknowledge()
        {
            float willingness = style.NodTendency * Mathf.Lerp(0.55f, 1.1f, relational.Safety) * (1f - relational.Guardedness * 0.5f);
            if (Random.value < willingness) Nod(Random.value < 0.4f ? 2 : 1, Random.Range(5f, 8.5f) * Mathf.Lerp(0.8f, 1.1f, relational.Safety));
            else gestureHeadDown = Mathf.Max(gestureHeadDown, 2.5f);
        }

        public void Nod(int count, float amplitude)
        {
            nodTime = 0f;
            nodCount = Mathf.Max(1, count);
            nodDuration = 0.4f * style.Tempo;
            nodAmplitude = amplitude;
        }

        public void ShakeHead(float amplitude)
        {
            shakeTime = 0f;
            shakeAmplitude = amplitude;
        }

        /// <summary>Review/debug: hold a rest pose (or run an adaptor) regardless of the scheduler.</summary>
        public void DebugRest(RestPose pose)
        {
            StopGesture();
            ApplyRest(pose, 0.6f);
            nextPostureShift = 999f;
            nextAdaptor = 999f;
        }

        public void DebugAdaptor(AdaptorKind kind, float hold)
        {
            StopGesture();
            gesture = StartCoroutine(Adaptor(kind, hold));
            nextPostureShift = 999f;
            nextAdaptor = 999f;
        }

        public void DebugIllustrator(int kind)
        {
            StopGesture();
            gesture = StartCoroutine(kind == 0 ? Beat(4) : kind == 1 ? OpenPalms(false) : OpenPalms(true));
            nextPostureShift = 999f;
            nextAdaptor = 999f;
        }

        public void ResumeScheduler()
        {
            nextPostureShift = Random.Range(8f, 16f);
            nextAdaptor = Random.Range(4f, 8f);
        }

        // ---- scheduler -----------------------------------------------------------------------

        private void Update()
        {
            if (animator == null || !animator.isHuman) return;
            float dt = Time.deltaTime;
            ikWeight = Mathf.MoveTowards(ikWeight, IsCalibrated ? 1f : 0f, dt * 1.6f);

            nextPostureShift -= dt;
            nextAdaptor -= dt;
            if (gesture == null && nextPostureShift <= 0f)
            {
                ChooseRest(false);
                nextPostureShift = Random.Range(20f, 42f) * style.Tempo;
            }
            if (gesture == null && nextAdaptor <= 0f)
            {
                if (!speaking || Random.value < 0.3f) RunAdaptor();
                nextAdaptor = NextInterval(AdaptorRate(), 6f);
            }

            AdvanceHand(left, dt);
            AdvanceHand(right, dt);
            UpdateCurl(left, dt);
            UpdateCurl(right, dt);

            float tempo = style.Tempo;
            float immediacy = relational.Safety * 4f - relational.Guardedness * 4.5f;
            float affectLean = affect == ClientAffect.Relieved ? 1.5f : affect == ClientAffect.Guarded ? -2f : 0f;
            leanTarget = RestLean(rest) + style.Slump * 0.6f + immediacy + affectLean + gestureLean;
            lean = Mathf.SmoothDamp(lean, leanTarget, ref leanVelocity, 0.85f * tempo);
            sideLean = Mathf.SmoothDamp(sideLean, sideLeanTarget, ref sideLeanVelocity, 0.8f * tempo);
            headTilt = Mathf.SmoothDamp(headTilt, headTiltTarget, ref headTiltVelocity, 0.7f * tempo);
            float headDown = style.HeadDown + (affect == ClientAffect.Guarded ? 3f : 0f) + relational.Guardedness * 2f + gestureHeadDown;
            headPitch = Mathf.SmoothDamp(headPitch, headDown, ref headPitchVelocity, 0.6f * tempo);
            gestureHeadDown = Mathf.MoveTowards(gestureHeadDown, 0f, dt * 1.2f);
            shrug = Mathf.SmoothDamp(shrug, shrugTarget, ref shrugVelocity, 0.16f);
        }

        private float AdaptorRate()
        {
            float affectGain = affect switch
            {
                ClientAffect.Anxious => 1.6f,
                ClientAffect.Guarded => 0.8f,
                ClientAffect.Relieved => 0.55f,
                _ => 0.9f
            };
            return Mathf.Max(0.2f, style.AdaptorsPerMinute * affectGain * (1f + relational.Guardedness * 0.5f - relational.Safety * 0.35f));
        }

        private float IllustratorRate()
        {
            float affectGain = affect switch
            {
                ClientAffect.Relieved => 1.4f,
                ClientAffect.Anxious => 0.7f,
                ClientAffect.Guarded => 0.45f,
                _ => 0.8f
            };
            return Mathf.Max(0.5f, style.IllustratorsPerMinute * affectGain * (0.6f + relational.Safety * 0.8f) * (1f - relational.Guardedness * 0.4f));
        }

        private static float NextInterval(float perMinute, float minimum)
        {
            // Poisson process: exponential waiting times look less mechanical than a fixed beat.
            float mean = 60f / Mathf.Max(0.05f, perMinute);
            return Mathf.Max(minimum, -Mathf.Log(Mathf.Max(0.02f, Random.value)) * mean);
        }

        private void ChooseRest(bool immediate)
        {
            ClientGestureStyle.RestWeight[] options;
            if (affect == ClientAffect.Thoughtful) options = style.ThoughtfulRests;
            else if (affect == ClientAffect.Guarded || relational.Guardedness > 0.6f) options = style.ClosedRests;
            else if (affect == ClientAffect.Relieved || relational.Safety > 0.6f) options = style.OpenRests;
            else options = Random.value < relational.Guardedness ? style.ClosedRests : style.OpenRests;
            RestPose next = style.Pick(options);
            if (speaking && (next == RestPose.CheekOnHand || next == RestPose.ChinOnFist)) next = style.Pick(style.OpenRests);
            ApplyRest(next, immediate ? 0f : Random.Range(1.1f, 1.6f) * style.Tempo);
        }

        private void RunAdaptor()
        {
            List<AdaptorKind> options = new List<AdaptorKind>();
            foreach (AdaptorKind kind in style.Adaptors)
            {
                if (kind == AdaptorKind.HairTuck && !style.TucksHair) continue;
                options.Add(kind);
            }
            if (options.Count == 0) return;
            AdaptorKind chosen = options[Random.Range(0, options.Count)];
            if (affect == ClientAffect.Relieved && chosen == AdaptorKind.Wring) chosen = AdaptorKind.HandRub;
            gesture = StartCoroutine(Adaptor(chosen, -1f));
        }

        private void StopGesture()
        {
            if (gesture != null) StopCoroutine(gesture);
            gesture = null;
            ClearMotion(left);
            ClearMotion(right);
            gestureLean = 0f;
            shrugTarget = 0f;
            ActiveGesture = "rest";
        }

        // ---- rest poses ----------------------------------------------------------------------

        private static HandSpec Spec(Anchor anchor, float t, Vector3 offset, Vector3 finger, Vector3 palm, float curl, Vector3 elbow, bool contact = true)
        {
            return new HandSpec { anchor = anchor, t = t, offset = offset, finger = finger, palm = palm, curl = curl, elbow = elbow, contact = contact };
        }

        private static readonly Vector3 LowElbow = new Vector3(0.17f, -0.25f, -0.02f);
        private static readonly Vector3 FrontElbow = new Vector3(0.18f, -0.18f, 0.12f);

        private static HandSpec ThighRest(float t = 0.6f) =>
            Spec(Anchor.ThighSame, t, Vector3.zero, new Vector3(0.12f, -0.32f, 1f), new Vector3(0f, -1f, 0.3f), 0.34f, LowElbow);

        private static HandSpec KneeRest() =>
            Spec(Anchor.ThighSame, 0.9f, new Vector3(0.012f, 0f, 0f), new Vector3(0.08f, -0.5f, 1f), new Vector3(0f, -1f, 0.15f), 0.4f, LowElbow);

        private static HandSpec LapRest() =>
            Spec(Anchor.Lap, 0f, new Vector3(0.035f, 0f, 0.035f), new Vector3(-0.6f, -0.1f, 0.8f), Vector3.down, 0.38f, LowElbow);

        // Hands stacked on the lap, both palms down, fingers following the forearms inward —
        // the reserved "손을 모은" posture common among Korean adults in formal conversation.
        private static HandSpec ClaspUnder(Anchor anchor = Anchor.Lap) =>
            Spec(anchor, 0f, new Vector3(0.025f, 0.0f, 0.0f), new Vector3(-0.7f, -0.2f, 0.7f), new Vector3(0f, -1f, 0.1f), 0.4f, LowElbow);

        private static HandSpec ClaspOver(Anchor anchor = Anchor.Lap) =>
            Spec(anchor, 0f, new Vector3(-0.015f, 0.03f, 0.02f), new Vector3(-0.75f, -0.15f, 0.65f), new Vector3(0f, -1f, 0.1f), 0.35f, LowElbow);

        private static HandSpec ArmrestRest() =>
            Spec(Anchor.Armrest, 0f, Vector3.zero, new Vector3(0f, -0.25f, 1f), Vector3.down, 0.35f, new Vector3(0.16f, -0.24f, -0.1f));

        private void ApplyRest(RestPose pose, float duration)
        {
            rest = pose;
            bool rightDominant = Random.value < 0.72f;
            sideLeanTarget = 0f;
            headTiltTarget = 0f;
            ActiveGesture = "rest:" + pose;
            switch (pose)
            {
                case RestPose.HandsOnThighs:
                    MoveHand(right, ThighRest(Random.Range(0.52f, 0.66f)), duration);
                    MoveHand(left, ThighRest(Random.Range(0.52f, 0.66f)), duration);
                    break;
                case RestPose.HandsOnKnees:
                    MoveHand(right, KneeRest(), duration);
                    MoveHand(left, KneeRest(), duration);
                    break;
                case RestPose.HandsClasped:
                    MoveHand(rightDominant ? right : left, ClaspOver(), duration);
                    MoveHand(rightDominant ? left : right, ClaspUnder(), duration);
                    break;
                case RestPose.ForearmsCrossedLow:
                    MoveHand(right, Spec(Anchor.ThighOther, 0.36f, new Vector3(0f, 0.055f, 0f), new Vector3(-0.55f, -0.1f, 0.8f), Vector3.down, 0.4f, LowElbow), duration);
                    MoveHand(left, Spec(Anchor.ThighOther, 0.46f, new Vector3(0f, 0.015f, 0f), new Vector3(-0.55f, -0.15f, 0.8f), Vector3.down, 0.4f, LowElbow), duration);
                    break;
                case RestPose.ArmsFolded:
                    // Right hand tucked under the left upper arm, left hand over the right forearm.
                    MoveHand(right, Spec(Anchor.RibsOther, 0f, new Vector3(0.0f, -0.01f, 0.03f), new Vector3(-0.75f, 0f, -0.35f), new Vector3(0.5f, 0f, -0.85f), 0.45f, FrontElbow), duration);
                    MoveHand(left, Spec(Anchor.RibsOther, 0f, new Vector3(0.02f, -0.07f, 0.1f), new Vector3(-1f, -0.15f, 0.2f), Vector3.down, 0.4f, FrontElbow), duration);
                    break;
                case RestPose.HandOnArmrest:
                    MoveHand(rightDominant ? right : left, ArmrestRest(), duration);
                    MoveHand(rightDominant ? left : right, ThighRest(0.55f), duration);
                    break;
                case RestPose.CheekOnHand:
                {
                    Hand support = rightDominant ? right : left;
                    Hand other = rightDominant ? left : right;
                    // Jaw and cheek rest in the palm heel, fingers along the cheek toward the temple;
                    // the other forearm lies across the stomach under the elbow (no reachable armrest
                    // on a wide sofa), and the head tilts into the hand.
                    HandSpec cheek = Spec(Anchor.Cheek, 0f, new Vector3(0.012f, -0.058f, 0.02f), new Vector3(-0.1f, 1f, -0.15f), new Vector3(-1f, 0.1f, 0.25f), 0.45f, new Vector3(0.02f, -0.34f, 0.22f));
                    cheek.elbowOnArmrest = false;
                    MoveHand(support, cheek, duration * 1.1f);
                    MoveHand(other, Spec(Anchor.RibsOther, 0f, new Vector3(0.0f, -0.1f, 0.08f), new Vector3(-1f, 0f, 0.15f), Vector3.up, 0.5f, FrontElbow), duration);
                    float sign = support.right ? 1f : -1f;
                    sideLeanTarget = 4f * sign;
                    headTiltTarget = 10f * sign;
                    break;
                }
                case RestPose.ChinOnFist:
                {
                    Hand thinker = rightDominant ? right : left;
                    Hand support = rightDominant ? left : right;
                    MoveHand(thinker, Spec(Anchor.Chin, 0f, new Vector3(0.0f, -0.03f, 0.02f), new Vector3(-0.1f, 0.75f, 0.65f), new Vector3(-0.2f, 0.25f, -1f), 0.85f, new Vector3(0.06f, -0.26f, 0.16f)), duration * 1.1f);
                    MoveHand(support, Spec(Anchor.RibsOther, 0f, new Vector3(0.0f, -0.07f, 0.1f), new Vector3(-1f, 0f, 0.15f), Vector3.up, 0.5f, FrontElbow), duration);
                    break;
                }
                case RestPose.LeanInClasped:
                    MoveHand(rightDominant ? right : left, ClaspOver(Anchor.Knees), duration);
                    MoveHand(rightDominant ? left : right, ClaspUnder(Anchor.Knees), duration);
                    break;
            }
        }

        private static float RestLean(RestPose pose) => pose switch
        {
            RestPose.LeanInClasped => 15f,
            RestPose.ChinOnFist => 4f,
            RestPose.CheekOnHand => 2f,
            RestPose.HandsOnKnees => 3f,
            RestPose.ArmsFolded => -3f,
            RestPose.ForearmsCrossedLow => -1.5f,
            _ => 0f
        };

        private bool IsPaired(RestPose pose) =>
            pose == RestPose.HandsClasped || pose == RestPose.ForearmsCrossedLow || pose == RestPose.ArmsFolded ||
            pose == RestPose.ChinOnFist || pose == RestPose.LeanInClasped || pose == RestPose.CheekOnHand;

        /// <summary>The rest target for one hand, used to return after a gesture.</summary>
        private HandSpec RestFor(Hand hand, bool otherHandBusy)
        {
            if (otherHandBusy && IsPaired(rest))
                return rest == RestPose.LeanInClasped ? KneeRest() : LapRest();
            switch (rest)
            {
                case RestPose.HandsOnThighs: return ThighRest();
                case RestPose.HandsOnKnees: return KneeRest();
                case RestPose.HandOnArmrest: return hand.right ? ArmrestRest() : ThighRest(0.55f);
                default:
                    return hand.to.anchor == Anchor.Lap || hand.to.anchor == Anchor.ThighSame ? hand.to : LapRest();
            }
        }

        // ---- gestures ------------------------------------------------------------------------

        private IEnumerator Adaptor(AdaptorKind kind, float forcedHold)
        {
            ActiveGesture = "adaptor:" + kind;
            float tempo = style.Tempo;
            float amp = style.Amplitude;
            float Hold(float min, float max) => forcedHold > 0f ? forcedHold : Random.Range(min, max);
            Hand main = Random.value < 0.7f ? right : left;
            Hand other = main == right ? left : right;
            HandSpec mainRest = main.to;
            HandSpec otherRest = other.to;
            bool freedRest = IsPaired(rest);

            switch (kind)
            {
                case AdaptorKind.HandRub:
                case AdaptorKind.Wring:
                {
                    float move = 0.55f * tempo;
                    MoveHand(right, Spec(Anchor.Lap, 0f, new Vector3(0.03f, 0.032f, 0.03f), new Vector3(-1f, 0f, 0.5f), new Vector3(-0.4f, -1f, 0f), 0.35f, LowElbow), move);
                    MoveHand(left, Spec(Anchor.Lap, 0f, new Vector3(0.03f, 0f, 0.03f), new Vector3(-1f, 0f, 0.5f), new Vector3(-0.4f, 1f, 0f), 0.4f, LowElbow), move);
                    yield return new WaitForSeconds(move);
                    if (kind == AdaptorKind.HandRub)
                    {
                        Oscillate(right, new Vector3(0.024f, 0f, 0.012f) * amp, 1.7f);
                        Oscillate(left, new Vector3(-0.006f, 0f, -0.004f) * amp, 1.7f);
                    }
                    else
                    {
                        right.twistAmplitude = 20f * amp; right.oscillationHz = 0.85f;
                        left.twistAmplitude = -16f * amp; left.oscillationHz = 0.85f;
                    }
                    left.fidget = right.fidget = 1f;
                    yield return new WaitForSeconds(Hold(2.2f, 4f));
                    ClearMotion(left); ClearMotion(right);
                    ApplyRest(rest, 0.7f * tempo);
                    yield return new WaitForSeconds(0.7f * tempo);
                    break;
                }
                case AdaptorKind.ThighRub:
                {
                    float move = 0.45f * tempo;
                    MoveHand(main, ThighRest(0.5f), move);
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    yield return new WaitForSeconds(move);
                    main.oscillateAlongThigh = true;
                    Oscillate(main, new Vector3(0f, 0f, 0.055f) * amp, 1.15f);
                    yield return new WaitForSeconds(Hold(1.8f, 3.2f));
                    ClearMotion(main);
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.6f * tempo);
                    break;
                }
                case AdaptorKind.NeckTouch:
                {
                    float move = 0.7f * tempo;
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    MoveHand(main, Spec(Anchor.Neck, 0f, new Vector3(0.02f, -0.01f, 0.0f), new Vector3(-0.25f, 0.3f, -0.9f), new Vector3(-1f, 0f, 0.3f), 0.25f, new Vector3(0.12f, -0.3f, 0.2f)), move);
                    gestureHeadDown = 4f;
                    yield return new WaitForSeconds(move);
                    Oscillate(main, new Vector3(0f, 0.012f, 0f), 1.3f);
                    yield return new WaitForSeconds(Hold(1.4f, 2.6f));
                    ClearMotion(main);
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.75f * tempo);
                    break;
                }
                case AdaptorKind.FaceTouch:
                {
                    float move = 0.75f * tempo;
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    MoveHand(main, Spec(Anchor.Cheek, 0f, new Vector3(0.005f, -0.02f, 0.015f), new Vector3(-0.2f, 1f, 0.15f), new Vector3(-1f, 0f, 0.2f), 0.42f, new Vector3(0.05f, -0.3f, 0.24f)), move);
                    yield return new WaitForSeconds(move);
                    Oscillate(main, new Vector3(0f, 0.008f, 0.004f), 0.9f);
                    yield return new WaitForSeconds(Hold(1.2f, 2.2f));
                    ClearMotion(main);
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.8f * tempo);
                    break;
                }
                case AdaptorKind.ForeheadRub:
                {
                    float move = 0.65f * tempo;
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    MoveHand(main, Spec(Anchor.Forehead, 0f, new Vector3(0.02f, 0.035f, 0.02f), new Vector3(-0.9f, 0.45f, 0f), new Vector3(-0.2f, -0.25f, -1f), 0.35f, new Vector3(0.05f, -0.3f, 0.24f)), move);
                    gestureHeadDown = 9f;
                    gestureLean = 4f;
                    yield return new WaitForSeconds(move);
                    Oscillate(main, new Vector3(0.028f, 0f, 0f) * amp, 1.05f);
                    yield return new WaitForSeconds(Hold(1.5f, 2.6f));
                    ClearMotion(main);
                    gestureLean = 0f;
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.7f * tempo);
                    break;
                }
                case AdaptorKind.HandToChest:
                {
                    float move = 0.85f * tempo;
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    MoveHand(main, Spec(Anchor.Chest, 0f, new Vector3(-0.02f, 0f, 0f), new Vector3(-0.6f, 0.7f, 0f), Vector3.back, 0.22f, new Vector3(0.08f, -0.28f, 0.2f)), move);
                    gestureHeadDown = 6f;
                    yield return new WaitForSeconds(move);
                    yield return new WaitForSeconds(Hold(2.2f, 3.6f));
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.9f * tempo);
                    break;
                }
                case AdaptorKind.HairTuck:
                {
                    float move = 0.5f * tempo;
                    if (freedRest) MoveHand(other, RestFor(other, true), move);
                    HandSpec front = Spec(Anchor.Ear, 0f, new Vector3(0.0f, -0.01f, 0.06f), new Vector3(0f, 0.25f, -1f), Vector3.left, 0.35f, new Vector3(0.15f, -0.3f, 0.14f));
                    HandSpec behind = front;
                    behind.offset = new Vector3(0.005f, 0.0f, -0.01f);
                    MoveHand(main, front, move);
                    yield return new WaitForSeconds(move);
                    MoveHand(main, behind, 0.45f * tempo);
                    yield return new WaitForSeconds(0.45f * tempo + (forcedHold > 0f ? forcedHold : 0.15f));
                    yield return Return(main, other, mainRest, otherRest, freedRest, 0.6f * tempo);
                    break;
                }
                case AdaptorKind.SmallShift:
                {
                    // Irregular, plan-less repositioning — typical of depressed affect.
                    HandSpec shifted = main.to;
                    shifted.t = Mathf.Clamp(shifted.t + Random.Range(-0.08f, 0.08f), 0.3f, 0.95f);
                    shifted.offset += new Vector3(Random.Range(-0.02f, 0.02f), 0f, Random.Range(-0.02f, 0.02f));
                    MoveHand(main, shifted, 0.8f * tempo);
                    gestureLean = Random.Range(-1.5f, 1.5f);
                    yield return new WaitForSeconds(0.8f * tempo + (forcedHold > 0f ? forcedHold : 0f));
                    gestureLean = 0f;
                    break;
                }
            }
            ActiveGesture = "rest:" + rest;
            gesture = null;
        }

        private IEnumerator Return(Hand main, Hand other, HandSpec mainRest, HandSpec otherRest, bool freedRest, float duration)
        {
            MoveHand(main, mainRest, duration);
            if (freedRest) MoveHand(other, otherRest, duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator SpeechGestures(string text, float duration)
        {
            float elapsed = 0f;
            string trimmed = text.Trim();
            // Utterance-level cues first (Korean and English).
            bool unsure = ContainsAny(trimmed, "모르겠", "글쎄", "잘 몰", "어떻게 해야", "I don't know", "not sure");
            bool yes = StartsWithAny(trimmed, "네", "예", "맞아", "그렇", "응", "Yes", "Yeah", "Right");
            bool no = StartsWithAny(trimmed, "아니", "아뇨", "No", "Not really");
            bool self = StartsWithAny(trimmed, "저는", "제가", "저도", "저 ", "I ", "I'm", "My ");
            bool laugh = ContainsAny(trimmed, "ㅎㅎ", "하하", "웃기", "웃음", "haha");

            if (yes) Nod(Random.value < 0.5f ? 2 : 1, 6f);
            if (no) ShakeHead(5f);

            if (gesture == null && laugh && style.CoversMouthWhenSmiling)
                gesture = StartCoroutine(MouthCover());
            else if (gesture == null && unsure && Random.value < 0.8f)
            {
                yield return new WaitForSeconds(Random.Range(0.25f, 0.7f));
                if (gesture == null) gesture = StartCoroutine(OpenPalms(true));
            }
            else if (gesture == null && self && Random.value < 0.4f)
            {
                yield return new WaitForSeconds(Random.Range(0.1f, 0.35f));
                if (gesture == null) gesture = StartCoroutine(SelfPoint());
            }
            if ((rest == RestPose.CheekOnHand || rest == RestPose.ChinOnFist) && gesture == null && Random.value < 0.6f)
                ApplyRest(style.Pick(style.OpenRests), 1.1f * style.Tempo);

            float next = Random.Range(0.6f, 1.6f);
            while (speaking && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                next -= Time.deltaTime;
                if (next <= 0f)
                {
                    if (gesture == null && duration - elapsed > 1.4f)
                    {
                        float roll = Random.value;
                        bool open = affect == ClientAffect.Relieved || relational.Safety > 0.55f;
                        gesture = StartCoroutine(roll < (open ? 0.3f : 0.15f) ? OpenPalms(false) : Beat(Random.Range(2, 5)));
                    }
                    next = NextInterval(IllustratorRate(), 2.2f);
                }
                yield return null;
            }
            speechGestures = null;
        }

        private IEnumerator Beat(int beats)
        {
            ActiveGesture = "illustrator:beat";
            float tempo = style.Tempo;
            Hand main = Random.value < 0.72f ? right : left;
            Hand other = main == right ? left : right;
            HandSpec mainRest = main.to;
            HandSpec otherRest = other.to;
            bool freedRest = IsPaired(rest);
            float amp = style.Amplitude * Mathf.Lerp(0.8f, 1.15f, relational.Safety);
            float move = 0.42f * tempo;
            HandSpec space = Spec(Anchor.GestureSpace, 0f, new Vector3(0.07f * amp, 0f, 0.01f), new Vector3(-0.25f, 0.1f, 1f), new Vector3(-0.45f, 0.85f, 0.1f), 0.3f, new Vector3(0.14f, -0.26f, 0.02f), false);
            if (freedRest) MoveHand(other, RestFor(other, true), move);
            MoveHand(main, space, move);
            yield return new WaitForSeconds(move);
            for (int i = 0; i < beats; i++)
            {
                // Stroke: a quick downward accent, then a slower recovery (McNeill beats).
                float stroke = Random.Range(0.14f, 0.2f) * tempo;
                float recover = Random.Range(0.22f, 0.4f) * tempo;
                float t = 0f;
                float depth = amp * (externalSpeech ? Mathf.Lerp(0.6f, 1.2f, speechLevel) : 1f);
                while (t < stroke) { t += Time.deltaTime; main.beat = depth * Mathf.SmoothStep(0f, 1f, t / stroke); yield return null; }
                t = 0f;
                while (t < recover) { t += Time.deltaTime; main.beat = depth * (1f - Mathf.SmoothStep(0f, 1f, t / recover)); yield return null; }
                main.beat = 0f;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.25f) * tempo);
            }
            yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
            yield return Return(main, other, mainRest, otherRest, freedRest, 0.55f * tempo);
            ActiveGesture = "rest:" + rest;
            gesture = null;
        }

        private IEnumerator OpenPalms(bool shrugToo)
        {
            ActiveGesture = shrugToo ? "illustrator:shrug" : "illustrator:open-palms";
            float tempo = style.Tempo;
            HandSpec rightRest = right.to;
            HandSpec leftRest = left.to;
            float amp = style.Amplitude;
            HandSpec open = Spec(Anchor.GestureSpace, 0f, new Vector3(0.13f * amp, shrugToo ? 0.02f : 0f, 0.02f), new Vector3(0.25f, 0.05f, 1f), new Vector3(-0.25f, 1f, 0f), 0.15f, new Vector3(0.16f, -0.26f, 0.0f), false);
            float move = 0.45f * tempo;
            MoveHand(right, open, move);
            MoveHand(left, open, move * Random.Range(0.95f, 1.1f));
            if (shrugToo)
            {
                shrugTarget = 1f;
                headTiltTarget = Random.value < 0.5f ? 6f : -6f;
            }
            yield return new WaitForSeconds(move + Random.Range(0.5f, 0.9f) * tempo);
            shrugTarget = 0f;
            headTiltTarget = 0f;
            MoveHand(right, rightRest, 0.6f * tempo);
            MoveHand(left, leftRest, 0.65f * tempo);
            yield return new WaitForSeconds(0.65f * tempo);
            ActiveGesture = "rest:" + rest;
            gesture = null;
        }

        private IEnumerator SelfPoint()
        {
            ActiveGesture = "illustrator:self";
            float tempo = style.Tempo;
            Hand main = right;
            HandSpec mainRest = main.to;
            HandSpec otherRest = left.to;
            bool freedRest = IsPaired(rest);
            if (freedRest) MoveHand(left, RestFor(left, true), 0.4f * tempo);
            MoveHand(main, Spec(Anchor.Chest, 0f, new Vector3(-0.03f, -0.02f, 0.005f), new Vector3(-0.6f, 0.65f, 0.1f), Vector3.back, 0.3f, new Vector3(0.08f, -0.28f, 0.2f)), 0.4f * tempo);
            yield return new WaitForSeconds(0.4f * tempo + Random.Range(0.35f, 0.6f));
            yield return Return(main, left, mainRest, otherRest, freedRest, 0.5f * tempo);
            ActiveGesture = "rest:" + rest;
            gesture = null;
        }

        private IEnumerator MouthCover()
        {
            // A common Korean (especially female) politeness gesture when laughing or smiling.
            ActiveGesture = "illustrator:mouth-cover";
            float tempo = style.Tempo;
            Hand main = right;
            HandSpec mainRest = main.to;
            HandSpec otherRest = left.to;
            bool freedRest = IsPaired(rest);
            if (freedRest) MoveHand(left, RestFor(left, true), 0.4f * tempo);
            MoveHand(main, Spec(Anchor.Mouth, 0f, new Vector3(-0.005f, -0.005f, 0.012f), new Vector3(-0.45f, 0.9f, 0f), Vector3.back, 0.25f, new Vector3(0.05f, -0.3f, 0.24f)), 0.38f * tempo);
            gestureHeadDown = 5f;
            yield return new WaitForSeconds(0.38f * tempo + Random.Range(0.7f, 1.2f));
            yield return Return(main, left, mainRest, otherRest, freedRest, 0.5f * tempo);
            ActiveGesture = "rest:" + rest;
            gesture = null;
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            foreach (string token in tokens) if (text.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool StartsWithAny(string text, params string[] tokens)
        {
            foreach (string token in tokens) if (text.StartsWith(token, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---- hand motion ---------------------------------------------------------------------

        private void MoveHand(Hand hand, HandSpec target, float duration)
        {
            if (duration <= 0f || !hand.calibrated)
            {
                hand.from = target;
                hand.to = target;
                hand.progress = 1f;
                hand.curl = target.curl;
                return;
            }
            // Start from wherever the hand currently is along its path.
            if (hand.progress < 1f && hips != null)
            {
                Pose current = BasePose(hand);
                hand.from = new HandSpec
                {
                    anchor = Anchor.Frozen,
                    localPosition = hips.InverseTransformPoint(current.position),
                    localRotation = Quaternion.Inverse(hips.rotation) * current.rotation,
                    curl = hand.curl,
                    elbow = hand.to.elbow,
                    elbowOnArmrest = hand.to.elbowOnArmrest
                };
            }
            else hand.from = hand.to;
            hand.to = target;
            hand.progress = 0f;
            hand.duration = duration;
        }

        private static void Oscillate(Hand hand, Vector3 amplitude, float hz)
        {
            hand.oscillation = amplitude;
            hand.oscillationHz = hz;
        }

        private static void ClearMotion(Hand hand)
        {
            hand.oscillation = Vector3.zero;
            hand.oscillationHz = 0f;
            hand.oscillateAlongThigh = false;
            hand.twistAmplitude = 0f;
            hand.beat = 0f;
            hand.fidget = 0f;
        }

        private static void AdvanceHand(Hand hand, float dt)
        {
            if (hand.progress < 1f) hand.progress = Mathf.Min(1f, hand.progress + dt / Mathf.Max(0.05f, hand.duration));
        }

        private void UpdateCurl(Hand hand, float dt)
        {
            float target = Mathf.Lerp(hand.from.curl, hand.to.curl, MinimumJerk(hand.progress));
            hand.curl = Mathf.MoveTowards(hand.curl, target, dt * 2.5f);
        }

        private static float MinimumJerk(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (10f + t * (-15f + 6f * t));
        }

        // ---- IK ------------------------------------------------------------------------------

        // Body frame from the pelvis, not the root transform: imported rigs may carry an axis
        // conversion on the root, so transform.right/up are not reliably the character's.
        private Vector3 bodyRight = Vector3.right, bodyUp = Vector3.up, bodyForward = Vector3.forward;
        private Vector3 Forward => bodyForward;
        private Vector3 Up => bodyUp;
        private Vector3 Right => bodyRight;

        private void UpdateBodyFrame()
        {
            if (leftUpperLeg == null || rightUpperLeg == null) return;
            Vector3 hipLine = rightUpperLeg.position - leftUpperLeg.position;
            if (hipLine.sqrMagnitude < 1e-6f) return;
            bodyRight = hipLine.normalized;
            bodyUp = Vector3.ProjectOnPlane(Vector3.up, bodyRight).normalized;
            bodyForward = Vector3.Cross(bodyRight, bodyUp);
        }

        private Vector3 BodyVector(Vector3 v, float side) => Right * (v.x * side) + Up * v.y + Forward * v.z;

        private Quaternion LeanRotation()
        {
            return Quaternion.AngleAxis(lean, Right) * Quaternion.AngleAxis(-sideLean, Forward);
        }

        private Pose BasePose(Hand hand)
        {
            float s = MinimumJerk(hand.progress);
            Pose a = SpecPose(hand, hand.from);
            Pose b = SpecPose(hand, hand.to);
            Vector3 position = Vector3.Lerp(a.position, b.position, s);
            // Lift the hand through the middle of a transition so it arcs rather than drags.
            float distance = Vector3.Distance(a.position, b.position);
            position += Up * (Mathf.Sin(Mathf.PI * s) * Mathf.Min(0.06f, distance * 0.35f));
            return new Pose(position, Quaternion.Slerp(a.rotation, b.rotation, s));
        }

        private Pose Evaluate(Hand hand)
        {
            Pose basePose = BasePose(hand);
            Vector3 position = basePose.position;
            Quaternion rotation = basePose.rotation;

            float side = hand.right ? 1f : -1f;
            float time = Time.time;
            if (hand.oscillationHz > 0f)
            {
                float wave = Mathf.Sin(time * hand.oscillationHz * Mathf.PI * 2f);
                if (hand.oscillateAlongThigh)
                {
                    Transform upper = hand.right ? rightUpperLeg : leftUpperLeg;
                    Transform lower = hand.right ? rightLowerLeg : leftLowerLeg;
                    Vector3 along = upper != null && lower != null ? (lower.position - upper.position).normalized : Forward;
                    position += along * (hand.oscillation.z * wave * Scale);
                }
                else
                {
                    position += BodyVector(hand.oscillation, side) * (wave * Scale);
                }
                if (Mathf.Abs(hand.twistAmplitude) > 0.01f)
                {
                    Vector3 palm = rotation * hand.palmLocal;
                    rotation = Quaternion.AngleAxis(hand.twistAmplitude * wave, palm) * rotation;
                }
            }
            if (hand.beat > 0f) position -= Up * (0.035f * hand.beat * Scale);
            // Physiological tremor-level drift so a resting hand is never frozen.
            position += BodyVector(new Vector3(
                Mathf.PerlinNoise(time * 0.31f, side * 3.1f) - 0.5f,
                Mathf.PerlinNoise(time * 0.27f, side * 5.7f) - 0.5f,
                Mathf.PerlinNoise(time * 0.23f, side * 8.3f) - 0.5f), side) * (0.006f * Scale);
            return new Pose(position, rotation);
        }

        private Pose SpecPose(Hand hand, HandSpec spec)
        {
            if (spec.anchor == Anchor.Frozen)
                return new Pose(hips.TransformPoint(spec.localPosition), hips.rotation * spec.localRotation);
            float side = hand.right ? 1f : -1f;
            float scale = Scale;
            Vector3 contact = AnchorPosition(spec.anchor, spec.t, hand.right) + BodyVector(spec.offset, side) * scale;
            Vector3 finger = BodyVector(spec.finger, side).normalized;
            Vector3 palm = BodyVector(spec.palm, side).normalized;
            // Palm-normal "palm" points out of the palm; the IK goal is the wrist, which sits
            // behind the palm centre and off the contact surface.
            Vector3 wrist = spec.contact
                ? contact - finger * (0.07f * scale) - palm * (0.028f * scale)
                : contact;
            Quaternion bone = Quaternion.LookRotation(finger, -palm) * Quaternion.Inverse(Quaternion.LookRotation(hand.fingerLocal, -hand.palmLocal));
            return new Pose(wrist, bone);
        }

        private Vector3 ElbowHint(Hand hand)
        {
            float side = hand.right ? 1f : -1f;
            Transform shoulder = hand.right ? rightUpperArm : leftUpperArm;
            Vector3 origin = shoulder != null ? shoulder.position : hips.position + Up * 0.45f;
            HandSpec spec = hand.progress < 0.5f ? hand.from : hand.to;
            if (spec.elbowOnArmrest && TryArmrest(hand.right, out Vector3 armrestTop))
                return armrestTop - Forward * (0.18f * Scale) + Up * (0.03f * Scale);
            return origin + BodyVector(spec.elbow, side) * Scale;
        }

        private Vector3 AnchorPosition(Anchor anchor, float t, bool rightHand)
        {
            float scale = Scale;
            Transform sameUpper = rightHand ? rightUpperLeg : leftUpperLeg;
            Transform sameLower = rightHand ? rightLowerLeg : leftLowerLeg;
            Transform otherUpper = rightHand ? leftUpperLeg : rightUpperLeg;
            Transform otherLower = rightHand ? leftLowerLeg : rightLowerLeg;
            float side = rightHand ? 1f : -1f;
            switch (anchor)
            {
                case Anchor.ThighSame: return Thigh(sameUpper, sameLower, t);
                case Anchor.ThighOther: return Thigh(otherUpper, otherLower, t);
                case Anchor.Lap: return Vector3.Lerp(Thigh(leftUpperLeg, leftLowerLeg, 0.55f), Thigh(rightUpperLeg, rightLowerLeg, 0.55f), 0.5f) + Up * (0.02f * scale);
                case Anchor.Knees: return Vector3.Lerp(Thigh(leftUpperLeg, leftLowerLeg, 0.92f), Thigh(rightUpperLeg, rightLowerLeg, 0.92f), 0.5f) + Forward * (0.05f * scale) + Up * (0.04f * scale);
                case Anchor.RibsSame:
                case Anchor.RibsOther:
                {
                    float ribSide = anchor == Anchor.RibsSame ? side : -side;
                    Vector3 torso = neck != null ? Vector3.Lerp(chest.position, neck.position, 0.2f) : chest.position;
                    return torso + Right * (0.1f * ribSide * scale) + Forward * (0.12f * scale);
                }
                case Anchor.Chest:
                {
                    Vector3 upperChest = neck != null ? Vector3.Lerp(chest.position, neck.position, 0.45f) : chest.position + Up * (0.1f * scale);
                    return upperChest + Forward * (0.12f * scale) + Right * (0.02f * side * scale);
                }
                case Anchor.Neck:
                    return (neck != null ? neck.position : head.position) + Right * (0.045f * side * scale) + Up * (0.03f * scale) - Forward * (0.01f * scale);
                case Anchor.Armrest:
                    return TryArmrest(rightHand, out Vector3 top) ? top : Thigh(sameUpper, sameLower, 0.55f);
                case Anchor.GestureSpace:
                    return Vector3.Lerp(Thigh(leftUpperLeg, leftLowerLeg, 0.5f), Thigh(rightUpperLeg, rightLowerLeg, 0.5f), 0.5f)
                           + Up * (0.14f * scale) + Forward * (0.08f * scale);
            }

            // Face anchors follow the head's own frame so the hand tracks head turns.
            FaceFrame(out Vector3 eyes, out Vector3 faceRight, out Vector3 faceUp, out Vector3 faceForward);
            switch (anchor)
            {
                case Anchor.Chin: return eyes - faceUp * (0.115f * scale) + faceForward * (0.03f * scale);
                case Anchor.Mouth: return eyes - faceUp * (0.07f * scale) + faceForward * (0.06f * scale);
                case Anchor.Cheek: return eyes + faceRight * (0.06f * side * scale) - faceUp * (0.06f * scale) + faceForward * (0.015f * scale);
                case Anchor.Forehead: return eyes + faceUp * (0.045f * scale) + faceForward * (0.07f * scale);
                case Anchor.Ear: return eyes + faceRight * (0.08f * side * scale) - faceForward * (0.075f * scale);
            }
            return hips.position;
        }

        private Vector3 Thigh(Transform upper, Transform lower, float t)
        {
            if (upper == null || lower == null) return hips.position + Forward * 0.25f;
            float length = Vector3.Distance(upper.position, lower.position);
            return Vector3.Lerp(upper.position, lower.position, t) + Up * (length * 0.17f);
        }

        private void FaceFrame(out Vector3 eyes, out Vector3 faceRight, out Vector3 faceUp, out Vector3 faceForward)
        {
            if (leftEye != null && rightEye != null)
            {
                eyes = Vector3.Lerp(leftEye.position, rightEye.position, 0.5f);
                faceRight = (rightEye.position - leftEye.position).normalized;
            }
            else
            {
                eyes = head.position + Up * (0.1f * Scale) + Forward * (0.08f * Scale);
                faceRight = head.right;
            }
            Vector3 neckPoint = neck != null ? neck.position : head.position - Up * 0.1f;
            faceUp = Vector3.ProjectOnPlane(eyes - neckPoint, faceRight).normalized;
            faceForward = Vector3.Cross(faceRight, faceUp);
            if (Vector3.Dot(faceForward, Forward) < 0f) faceForward = -faceForward;
        }

        private bool TryArmrest(bool rightHand, out Vector3 top)
        {
            top = Vector3.zero;
            if (armrests.Count == 0) return false;
            Transform shoulder = rightHand ? rightUpperArm : leftUpperArm;
            if (shoulder == null) return false;
            float best = float.MaxValue;
            Bounds chosen = armrests[0];
            foreach (Bounds bounds in armrests)
            {
                float distance = Vector3.Distance(bounds.center, shoulder.position);
                if (distance < best) { best = distance; chosen = bounds; }
            }
            Vector3 probe = hips.position + Forward * (0.2f * Scale);
            Vector3 clamped = chosen.ClosestPoint(new Vector3(probe.x, chosen.max.y, probe.z));
            top = new Vector3(clamped.x, chosen.max.y + 0.012f, clamped.z);
            // Rest the hand on the inner half of the armrest.
            Vector3 inward = Vector3.ProjectOnPlane(hips.position - top, Vector3.up);
            top += inward.normalized * Mathf.Min(chosen.extents.x, chosen.extents.z) * 0.25f;
            // A wide sofa arm can be out of reach; then the pose falls back to the thigh.
            Hand hand = rightHand ? right : left;
            if (hand.lower != null && hand.bone != null)
            {
                float arm = Vector3.Distance(shoulder.position, hand.lower.position) + Vector3.Distance(hand.lower.position, hand.bone.position);
                if (Vector3.Distance(shoulder.position, top) > arm * 0.8f) return false;
            }
            return true;
        }

        // ---- post-IK layers --------------------------------------------------------------------

        private void LateUpdate()
        {
            if (animator == null || !animator.isHuman || hips == null) return;
            UpdateBodyFrame();
            Calibrate(left);
            Calibrate(right);
            ApplyFingers();

            if (spine != null) spine.rotation = LeanRotation() * spine.rotation;

            if (rightShoulder != null) rightShoulder.rotation = Quaternion.AngleAxis(shrug * 9f, Forward) * rightShoulder.rotation;
            if (leftShoulder != null) leftShoulder.rotation = Quaternion.AngleAxis(-shrug * 9f, Forward) * leftShoulder.rotation;

            float nod = 0f;
            if (nodTime >= 0f)
            {
                nodTime += Time.deltaTime;
                float phase = nodTime / nodDuration;
                if (phase >= nodCount) nodTime = -1f;
                else nod = nodAmplitude * Mathf.Sin(Mathf.PI * Mathf.Repeat(phase, 1f)) * (phase < 1f ? 1f : 0.7f);
            }
            float shake = 0f;
            if (shakeTime >= 0f)
            {
                shakeTime += Time.deltaTime;
                if (shakeTime > 0.9f) shakeTime = -1f;
                else shake = shakeAmplitude * Mathf.Sin(shakeTime / 0.9f * Mathf.PI * 3f) * (1f - shakeTime / 0.9f);
            }
            Transform headRoot = neck != null ? neck : head;
            if (headRoot != null)
            {
                headRoot.rotation = Quaternion.AngleAxis(headPitch + nod, Right) *
                                    Quaternion.AngleAxis(shake, Up) *
                                    Quaternion.AngleAxis(-headTilt, Forward) * headRoot.rotation;
            }

            // Arms last, so every anchor (thigh, chin, chest) is read from this frame's final pose.
            SolveArm(left);
            SolveArm(right);
        }

        /// <summary>
        /// Analytic two-bone IK on the humanoid arm bones. Unlike Mecanim's hand IK it is not
        /// clamped by muscle limits, so hands can cross the midline (clasped hands, folded arms,
        /// holding the other elbow) and the elbow follows the pose's own pole target.
        /// </summary>
        private void SolveArm(Hand hand)
        {
            if (!hand.calibrated || ikWeight <= 0.001f || hand.upper == null || hand.lower == null || hand.bone == null) return;
            Pose pose = Evaluate(hand);
            Vector3 target = pose.position;
            Vector3 a = hand.upper.position;
            Vector3 b = hand.lower.position;
            Vector3 c = hand.bone.position;
            float upperLength = Vector3.Distance(a, b);
            float lowerLength = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            float reach = Mathf.Clamp(toTarget.magnitude, 0.05f, (upperLength + lowerLength) * 0.999f);
            Vector3 direction = toTarget.normalized;
            Vector3 pole = ElbowHint(hand) - a;
            Vector3 bend = pole - Vector3.Dot(pole, direction) * direction;
            if (bend.sqrMagnitude < 1e-6f) bend = -Up;
            bend.Normalize();
            float cosA = Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * upperLength * reach), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = a + direction * (upperLength * cosA) + bend * (upperLength * sinA);
            Vector3 wrist = a + direction * reach;

            Quaternion upperStart = hand.upper.rotation;
            Quaternion lowerStart = hand.lower.rotation;
            Quaternion handStart = hand.bone.rotation;
            hand.upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upperStart;
            Vector3 elbowNow = hand.lower.position;
            hand.lower.rotation = Quaternion.FromToRotation(hand.bone.position - elbowNow, wrist - elbowNow) * hand.lower.rotation;

            // Keep the wrist within a comfortable range: a real wrist rarely rests more than
            // ~30° off the forearm line, and over-bent wrists read as broken.
            Vector3 forearmAxis = (hand.bone.position - hand.lower.position).normalized;
            Quaternion desired = LimitWrist(pose.rotation, hand, forearmAxis);

            // Share the wrist's twist with the forearm so the skin does not candy-wrap at the wrist.
            Quaternion delta = desired * Quaternion.Inverse(hand.bone.rotation);
            Vector3 deltaAxis = new Vector3(delta.x, delta.y, delta.z);
            Vector3 projected = Vector3.Project(deltaAxis, forearmAxis);
            Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, delta.w);
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w > 1e-8f)
            {
                twist = Normalize(twist);
                hand.lower.rotation = Quaternion.Slerp(Quaternion.identity, twist, 0.5f) * hand.lower.rotation;
            }
            hand.bone.rotation = desired;

            if (ikWeight < 0.999f)
            {
                Quaternion upperSolved = hand.upper.rotation;
                Quaternion lowerSolved = hand.lower.rotation;
                Quaternion handSolved = hand.bone.rotation;
                hand.upper.rotation = Quaternion.Slerp(upperStart, upperSolved, ikWeight);
                hand.lower.rotation = Quaternion.Slerp(lowerStart, lowerSolved, ikWeight);
                hand.bone.rotation = Quaternion.Slerp(handStart, handSolved, ikWeight);
            }
        }

        private const float MaxWristBend = 28f;

        private static Quaternion LimitWrist(Quaternion rotation, Hand hand, Vector3 forearmAxis)
        {
            Vector3 finger = rotation * hand.fingerLocal;
            float angle = Vector3.Angle(forearmAxis, finger);
            if (angle <= MaxWristBend) return rotation;
            Vector3 axis = Vector3.Cross(finger, forearmAxis);
            if (axis.sqrMagnitude < 1e-8f) return rotation;
            return Quaternion.AngleAxis(angle - MaxWristBend, axis.normalized) * rotation;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        private void Calibrate(Hand hand)
        {
            if (hand.calibrated || hand.bone == null) return;
            Transform index = animator.GetBoneTransform(hand.right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform middle = animator.GetBoneTransform(hand.right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform little = animator.GetBoneTransform(hand.right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal) ??
                               animator.GetBoneTransform(hand.right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal);
            Vector3 finger = middle != null ? (middle.position - hand.bone.position).normalized : hand.bone.rotation * Vector3.right;
            Vector3 thumbSide = index != null && little != null ? Vector3.ProjectOnPlane(index.position - little.position, finger).normalized : Vector3.up;
            // Palm normal from handedness: right hand = finger × thumb side, left = thumb side × finger.
            Vector3 palm = (hand.right ? Vector3.Cross(finger, thumbSide) : Vector3.Cross(thumbSide, finger)).normalized;
            Quaternion inverse = Quaternion.Inverse(hand.bone.rotation);
            hand.fingerLocal = inverse * finger;
            hand.palmLocal = inverse * palm;
            hand.calibrated = true;
            // Enter the chosen rest pose now that targets can be computed.
            hand.from = hand.to;
            hand.progress = 1f;
        }

        private void ApplyFingers()
        {
            if (poseHandler == null) return;
            poseHandler.GetHumanPose(ref humanPose);
            float time = Time.time;
            for (int side = 0; side < 2; side++)
            {
                Hand hand = side == 0 ? left : right;
                float curl = hand.curl;
                float fidget = hand.fidget > 0f || affect == ClientAffect.Anxious
                    ? Mathf.Sin(time * 5.2f + side) * (hand.fidget > 0f ? 0.12f : 0.05f)
                    : 0f;
                List<int> fingers = fingerMuscles[side];
                for (int i = 0; i < fingers.Count; i++)
                {
                    // Distal joints curl a little more than proximal ones; the little finger most.
                    float joint = (i % 3) * 0.08f;
                    float finger = (i / 3) * 0.05f;
                    float value = Mathf.Lerp(0.75f, -0.85f, Mathf.Clamp01(curl + joint + finger)) + (i < 3 ? fidget : 0f);
                    humanPose.muscles[fingers[i]] = Mathf.Lerp(humanPose.muscles[fingers[i]], value, ikWeight);
                }
                foreach (int thumb in thumbMuscles[side])
                    humanPose.muscles[thumb] = Mathf.Lerp(humanPose.muscles[thumb], Mathf.Lerp(0.45f, -0.35f, curl) + fidget, ikWeight);
            }
            // SetHumanPose re-derives the hips from the body pose and can spin the avatar about
            // its root; only the finger muscles changed, so keep the hips and root exactly as the
            // animation and IK left them.
            Transform root = animator.transform;
            Vector3 rootPosition = root.localPosition;
            Quaternion rootRotation = root.localRotation;
            Vector3 hipsPosition = hips.position;
            Quaternion hipsRotation = hips.rotation;
            poseHandler.SetHumanPose(ref humanPose);
            root.localPosition = rootPosition;
            root.localRotation = rootRotation;
            hips.SetPositionAndRotation(hipsPosition, hipsRotation);
        }

        private void OnDestroy()
        {
            poseHandler?.Dispose();
        }
    }
}
