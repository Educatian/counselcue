using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum DeliveryAlignment
    {
        Aligned,
        PossibleMismatch,
        RelationalOrderMismatch,
        EvidenceUnavailable
    }

    public readonly struct DeliveryObservation
    {
        public DeliveryObservation(bool isAvailable, float browTension, float smileActivation)
        {
            IsAvailable = isAvailable;
            BrowTension = browTension;
            SmileActivation = smileActivation;
        }

        public static DeliveryObservation Unavailable => new DeliveryObservation(false, 0f, 0f);
        public bool IsAvailable { get; }
        public float BrowTension { get; }
        public float SmileActivation { get; }
    }

    public readonly struct ClientRelationalState
    {
        public ClientRelationalState(float safety, float guardedness, float willingnessToDisclose)
        {
            Safety = Mathf.Clamp01(safety);
            Guardedness = Mathf.Clamp01(guardedness);
            WillingnessToDisclose = Mathf.Clamp01(willingnessToDisclose);
        }

        public static ClientRelationalState Initial => new ClientRelationalState(0.38f, 0.62f, 0.25f);
        public float Safety { get; }
        public float Guardedness { get; }
        public float WillingnessToDisclose { get; }
    }

    public readonly struct CulturalInteractionProfile
    {
        public CulturalInteractionProfile(
            string id,
            float browTensionThreshold,
            float distressSmileThreshold,
            float adviceGuardednessThreshold)
        {
            Id = id;
            BrowTensionThreshold = browTensionThreshold;
            DistressSmileThreshold = distressSmileThreshold;
            AdviceGuardednessThreshold = adviceGuardednessThreshold;
        }

        public static CulturalInteractionProfile KoreanCounselingPilot =>
            new CulturalInteractionProfile("ko-counseling-pilot-v1", 0.18f, 0.28f, 0.50f);

        public string Id { get; }
        public float BrowTensionThreshold { get; }
        public float DistressSmileThreshold { get; }
        public float AdviceGuardednessThreshold { get; }
    }

    public readonly struct RelationalTurnResult
    {
        public RelationalTurnResult(
            ClientRelationalState state,
            DeliveryAlignment alignment,
            float deliveryModifier,
            string coachingFeedback)
        {
            State = state;
            Alignment = alignment;
            DeliveryModifier = deliveryModifier;
            CoachingFeedback = coachingFeedback;
        }

        public ClientRelationalState State { get; }
        public DeliveryAlignment Alignment { get; }
        public float DeliveryModifier { get; }
        public string CoachingFeedback { get; }
    }

    public static class RelationalDeliveryEvaluator
    {
        public static RelationalTurnResult Evaluate(
            ResponseAssessment response,
            DeliveryObservation delivery,
            ClientRelationalState current,
            CulturalInteractionProfile profile)
        {
            DeliveryAlignment alignment;
            float modifier;
            string coaching;

            if (response.Move == CounselingMove.Advice && current.Guardedness >= profile.AdviceGuardednessThreshold)
            {
                alignment = DeliveryAlignment.RelationalOrderMismatch;
                modifier = -0.05f;
                coaching = "관계가 아직 경계된 상태입니다. 해결책보다 감정 반영과 탐색을 먼저 시도해 보세요.";
            }
            else if (!delivery.IsAvailable)
            {
                alignment = DeliveryAlignment.EvidenceUnavailable;
                modifier = 0f;
                coaching = "비언어 근거가 없어 언어 기술만 반영했습니다.";
            }
            else if (IsDeliverySensitive(response.Move) && delivery.BrowTension >= profile.BrowTensionThreshold)
            {
                alignment = DeliveryAlignment.PossibleMismatch;
                modifier = -0.04f;
                coaching = "공감 문장은 적절했지만 이마 긴장 단서가 함께 관찰됐습니다. 표정에 힘을 빼고 한 박자 쉬어 보세요.";
            }
            else if (IsDeliverySensitive(response.Move) && delivery.SmileActivation >= profile.DistressSmileThreshold)
            {
                alignment = DeliveryAlignment.PossibleMismatch;
                modifier = -0.03f;
                coaching = "고통을 다루는 순간 미소 단서가 함께 관찰됐습니다. 맥락에 맞는 따뜻한 중립 표정을 점검해 보세요.";
            }
            else
            {
                alignment = DeliveryAlignment.Aligned;
                modifier = IsDeliverySensitive(response.Move) ? 0.02f : 0f;
                coaching = IsDeliverySensitive(response.Move)
                    ? "언어 기술과 현재 관찰된 얼굴 전달 단서가 조화를 이룹니다."
                    : "현재 관찰된 전달 단서와 뚜렷한 충돌이 없습니다.";
            }

            float verbalEffect = response.TrustDelta;
            float safety = current.Safety + verbalEffect + modifier;
            float guardedness = current.Guardedness - (verbalEffect * 0.65f) - modifier;
            float disclosureEffect = DisclosureEffect(response.Move, response.Quality);
            float disclosure = current.WillingnessToDisclose + disclosureEffect + (modifier * 0.8f);
            ClientRelationalState next = new ClientRelationalState(safety, guardedness, disclosure);
            return new RelationalTurnResult(next, alignment, modifier, coaching);
        }

        private static bool IsDeliverySensitive(CounselingMove move) =>
            move == CounselingMove.Reflection ||
            move == CounselingMove.Validation ||
            move == CounselingMove.ReflectionAndExploration ||
            move == CounselingMove.OpenQuestion;

        private static float DisclosureEffect(CounselingMove move, int quality)
        {
            if (move == CounselingMove.Advice) return -0.09f;
            if (move == CounselingMove.ReflectionAndExploration) return 0.14f;
            if (move == CounselingMove.Reflection || move == CounselingMove.Validation) return 0.10f;
            if (move == CounselingMove.OpenQuestion) return 0.06f;
            return quality > 0 ? 0.01f : -0.03f;
        }
    }
}
