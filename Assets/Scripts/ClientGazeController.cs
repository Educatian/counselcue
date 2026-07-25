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
            smoothedTarget = Vector3.SmoothDamp(smoothedTarget, desired, ref targetVelocity, 0.22f);
            currentWeight = Mathf.MoveTowards(currentWeight, ResolveLookWeight(), Time.deltaTime * 1.8f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || counselorEyeAnchor == null) return;
            animator.SetLookAtWeight(currentWeight, 0.04f, 0.62f, 0.86f, 0.58f);
            animator.SetLookAtPosition(smoothedTarget);
        }

        private ClientGazeState ChooseNextState()
        {
            float guardedness = relationalState.Guardedness;
            if (state == ClientGazeState.BriefAvert || state == ClientGazeState.DownwardReflection ||
                state == ClientGazeState.RecallSearch)
            {
                return ClientGazeState.Reengage;
            }

            if (state == ClientGazeState.Reengage) return ClientGazeState.CounselorContact;
            if (affect == ClientAffect.Thoughtful || (speaking && Random.value < 0.34f))
            {
                return Random.value < 0.68f ? ClientGazeState.DownwardReflection : ClientGazeState.RecallSearch;
            }

            float avertChance = Mathf.Lerp(0.20f, 0.62f, guardedness);
            return Random.value < avertChance ? ClientGazeState.BriefAvert : ClientGazeState.CounselorContact;
        }

        private void Enter(ClientGazeState next)
        {
            state = next;
            stateRemaining = next switch
            {
                ClientGazeState.CounselorContact => Random.Range(1.4f, 3.2f),
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
                ClientGazeState.BriefAvert => basePosition + right * (Mathf.Sin(Time.time * 0.73f) >= 0f ? 0.42f : -0.42f) - up * 0.10f,
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
