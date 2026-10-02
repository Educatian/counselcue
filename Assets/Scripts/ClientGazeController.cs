using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum ClientGazeState
    {
        CounselorContact,
        BriefAvert,
        DownwardReflection,
        RecallSearch,
        Reengage
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ClientGazeController : MonoBehaviour
    {
        [SerializeField] private Transform counselorEyeAnchor;
        [SerializeField] private ClientGazeState state = ClientGazeState.Reengage;

        private Animator animator;
        private ClientAffect affect = ClientAffect.Anxious;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private float baselineComfort = 0.58f;
        private float presentationIntensity = 0.68f;
        private float stateRemaining;
        private bool speaking;
        private Vector3 smoothedTarget;
        private Vector3 targetVelocity;
        private float currentWeight;

        public ClientGazeState State => state;
        public float ContactWeight => currentWeight;
        /// <summary>Approximate vertical eye angle (positive up), for lid-gaze coupling.</summary>
        public float GazePitchDegrees { get; private set; }
        /// <summary>Raised whenever the gaze moves to a new state (used for gaze-evoked blinks).</summary>
        public event System.Action<ClientGazeState> GazeShifted;

        public void CycleDebugState()
        {
            int next = ((int)state + 1) % System.Enum.GetValues(typeof(ClientGazeState)).Length;
            Enter((ClientGazeState)next);
            stateRemaining = 3f;
        }

        public void Initialize(
            Transform eyeAnchor,
            ClientProfileDefinition profile,
            AvatarPresentationDefinition presentation)
        {
            animator = GetComponent<Animator>();
            counselorEyeAnchor = eyeAnchor;
            baselineComfort = profile == null ? 0.58f : profile.BaselineGazeComfort;
            presentationIntensity = presentation == null ? 0.68f : presentation.GazeIntensity;
            smoothedTarget = eyeAnchor == null ? transform.position + transform.forward : eyeAnchor.position;
            Enter(ClientGazeState.Reengage);
        }

        public void SetContext(ClientAffect clientAffect, ClientRelationalState stateValue, bool isSpeaking)
        {
            affect = clientAffect;
            relationalState = stateValue;
            speaking = isSpeaking;
        }

        private void Update()
        {
            if (animator == null || counselorEyeAnchor == null) return;
            stateRemaining -= Time.deltaTime;
            if (stateRemaining <= 0f) Enter(ChooseNextState());

            Vector3 desired = ResolveTargetPosition();
            smoothedTarget = Vector3.SmoothDamp(smoothedTarget, desired, ref targetVelocity, 0.32f);
            currentWeight = Mathf.MoveTowards(currentWeight, ResolveLookWeight(), Time.deltaTime * 1.8f);
            Transform headBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (headBone != null)
            {
                Vector3 toTarget = smoothedTarget - headBone.position;
                Vector3 toCounselor = counselorEyeAnchor.position - headBone.position;
                float pitch = Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(toCounselor.normalized.y, -1f, 1f));
                GazePitchDegrees = pitch * Mathf.Rad2Deg * Mathf.Clamp01(currentWeight * 1.25f);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || counselorEyeAnchor == null) return;
            animator.SetLookAtWeight(currentWeight, 0.03f, 0.42f, 0.9f, 0.55f);
            animator.SetLookAtPosition(smoothedTarget);
        }

        private float avertSide = 1f;

        private ClientGazeState ChooseNextState()
        {
            float guardedness = relationalState.Guardedness;
            if (state == ClientGazeState.BriefAvert || state == ClientGazeState.DownwardReflection ||
                state == ClientGazeState.RecallSearch)
            {
                return ClientGazeState.Reengage;
            }

            if (state == ClientGazeState.Reengage) return ClientGazeState.CounselorContact;
            if (affect == ClientAffect.Thoughtful || (speaking && Random.value < 0.18f))
            {
                return Random.value < 0.68f ? ClientGazeState.DownwardReflection : ClientGazeState.RecallSearch;
            }

            float avertChance = Mathf.Lerp(0.20f, 0.62f, guardedness);
            return Random.value < avertChance ? ClientGazeState.BriefAvert : ClientGazeState.CounselorContact;
        }

        private void Enter(ClientGazeState next)
        {
            state = next;
            GazeShifted?.Invoke(next);
            // Keep one side per aversion instead of swinging left and right.
            if (next == ClientGazeState.BriefAvert) avertSide = Random.value < 0.5f ? -1f : 1f;
            stateRemaining = next switch
            {
                ClientGazeState.CounselorContact => speaking ? Random.Range(3.2f, 6f) : Random.Range(2.4f, 4.8f),
                ClientGazeState.BriefAvert => Random.Range(0.55f, 1.25f),
                ClientGazeState.DownwardReflection => Random.Range(0.8f, 1.7f),
                ClientGazeState.RecallSearch => Random.Range(0.65f, 1.35f),
                _ => Random.Range(0.45f, 0.9f)
            };
        }

        private Vector3 ResolveTargetPosition()
        {
            Vector3 basePosition = counselorEyeAnchor.position;
            Vector3 right = counselorEyeAnchor.right;
            Vector3 up = counselorEyeAnchor.up;
            return state switch
            {
                ClientGazeState.BriefAvert => basePosition + right * (0.3f * avertSide) - up * 0.12f,
                ClientGazeState.DownwardReflection => basePosition - up * 0.46f + right * 0.10f,
                ClientGazeState.RecallSearch => basePosition + up * 0.28f - right * 0.28f,
                ClientGazeState.Reengage => basePosition - up * 0.06f,
                _ => basePosition
            };
        }

        private float ResolveLookWeight()
        {
            float relationship = Mathf.Lerp(0.72f, 1.08f, relationalState.Safety);
            float baseWeight = baselineComfort * presentationIntensity * relationship;
            float stateMultiplier = state switch
            {
                ClientGazeState.CounselorContact => 1f,
                ClientGazeState.Reengage => 0.86f,
                ClientGazeState.BriefAvert => 0.46f,
                ClientGazeState.DownwardReflection => 0.54f,
                _ => 0.62f
            };
            return Mathf.Clamp(baseWeight * stateMultiplier, 0.22f, 0.82f);
        }
    }
}
