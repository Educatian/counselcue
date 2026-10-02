using System;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class RelationalDeliveryModelChecks
    {
        [MenuItem("Tools/CounselCue/Run Relational Delivery Checks")]
        public static void RunFromMenu()
        {
            RunChecks();
            Debug.Log("Relational delivery checks passed.");
        }

        public static void RunFromCommandLine()
        {
            RunChecks();
            Debug.Log("Relational delivery checks passed.");
        }

        private static void RunChecks()
        {
            CulturalInteractionProfile profile = CulturalInteractionProfile.KoreanCounselingPilot;
            ClientRelationalState initial = ClientRelationalState.Initial;
            ResponseAssessment validation = new ResponseAssessment(
                CounselingMove.Validation,
                3,
                0.16f,
                "정서 타당화",
                "내담자의 경험을 인정했습니다.");

            RelationalTurnResult unavailable = RelationalDeliveryEvaluator.Evaluate(
                validation,
                DeliveryObservation.Unavailable,
                initial,
                profile);
            Require(unavailable.DeliveryModifier == 0f, "Missing AU evidence must not alter delivery.");
            Require(unavailable.Alignment == DeliveryAlignment.EvidenceUnavailable, "Missing evidence state was lost.");

            RelationalTurnResult aligned = RelationalDeliveryEvaluator.Evaluate(
                validation,
                new DeliveryObservation(true, 0.02f, 0.03f),
                initial,
                profile);
            RelationalTurnResult tense = RelationalDeliveryEvaluator.Evaluate(
                validation,
                new DeliveryObservation(true, 0.42f, 0.03f),
                initial,
                profile);
            Require(aligned.Alignment == DeliveryAlignment.Aligned, "Low pilot cues should produce aligned delivery.");
            Require(tense.Alignment == DeliveryAlignment.PossibleMismatch, "Elevated AU04 should produce a possible mismatch.");
            Require(aligned.State.WillingnessToDisclose > tense.State.WillingnessToDisclose, "Mismatch must reduce disclosure trajectory relative to aligned delivery.");

            ResponseAssessment openQuestion = new ResponseAssessment(
                CounselingMove.OpenQuestion,
                2,
                0.07f,
                "개방형 질문",
                "내담자가 탐색할 여지를 주었습니다.");
            RelationalTurnResult smilingQuestion = RelationalDeliveryEvaluator.Evaluate(
                openQuestion,
                new DeliveryObservation(true, 0.02f, 0.45f),
                initial,
                profile);
            Require(smilingQuestion.Alignment == DeliveryAlignment.PossibleMismatch, "A high smile cue must remain reviewable during a distress-focused open question.");

            ResponseAssessment advice = new ResponseAssessment(
                CounselingMove.Advice,
                0,
                -0.12f,
                "성급한 조언",
                "관계 형성 전에 해결책을 제시했습니다.");
            RelationalTurnResult adviceFirst = RelationalDeliveryEvaluator.Evaluate(
                advice,
                new DeliveryObservation(true, 0.02f, 0.03f),
                initial,
                profile);
            Require(adviceFirst.Alignment == DeliveryAlignment.RelationalOrderMismatch, "Advice before disclosure must flag relational order.");
            Require(adviceFirst.State.Guardedness > initial.Guardedness, "Advice-first response must increase guardedness in the pilot model.");

            ResponseAssessment reassurance = CounselingResponseEvaluator.Evaluate("시간이 지나면 괜찮아질 거예요.");
            RelationalTurnResult reassuranceFirst = RelationalDeliveryEvaluator.Evaluate(
                reassurance,
                DeliveryObservation.Unavailable,
                initial,
                profile);
            Require(reassurance.Move == CounselingMove.PrematureReassurance, "Premature reassurance must not be scored as validation.");
            Require(reassuranceFirst.Alignment == DeliveryAlignment.RelationalOrderMismatch, "Reassurance before understanding must flag relational order.");
            Require(reassuranceFirst.State.WillingnessToDisclose < initial.WillingnessToDisclose, "Premature reassurance must not open disclosure.");

            CodebookChecks();
            WeightChecks(profile, initial);
        }

        private static void CodebookChecks()
        {
            foreach (string code in CounselingCodebook.Codes)
            {
                Require(CounselingCodebook.TryFromCode(code, CounselingCodebook.TypicalQuality(code), "", out ResponseAssessment coded),
                    $"Codebook must accept its own code {code}.");
                Require(CounselingCodebook.CodeOf(coded) == code, $"Code {code} must round-trip through an assessment.");
            }
            Require(!CounselingCodebook.TryFromCode("diagnosis", 2, "", out _), "Unknown codes must be rejected so the lexicon can take over.");
            CounselingCodebook.TryFromCode("advice", 3, "", out ResponseAssessment advice);
            Require(advice.Quality <= 1, "Advice cannot be rated as a high-quality response.");
            Require(CounselingCodebook.CodeOf(CounselingResponseEvaluator.Evaluate("왜 그렇게 생각하세요?")) == CounselingCodebook.WhyQuestion,
                "Lexicon 'why' questions must map to the why_question code.");
        }

        private static void WeightChecks(CulturalInteractionProfile profile, ClientRelationalState initial)
        {
            RelationalModelWeights prior = new RelationalModelWeights();
            float exploration = prior.DisclosureDelta(CounselingCodebook.OpenQuestion, initial.Safety);
            float reflection = prior.DisclosureDelta(CounselingCodebook.Reflection, initial.Safety);
            float validation = prior.DisclosureDelta(CounselingCodebook.Validation, initial.Safety);
            Require(exploration >= 2.9f * reflection && exploration >= 2.9f * validation,
                "Prior must weight exploration about 3x a single empathy component on disclosure (AVP).");
            Require(prior.SafetyDelta(CounselingCodebook.Reflection, 2) > prior.SafetyDelta(CounselingCodebook.OpenQuestion, 2),
                "Empathy should build safety more than a bare question.");

            // A refit (e.g. from expert ratings) must change behaviour without code edits.
            RelationalModelWeights refit = new RelationalModelWeights { version = "check-refit" };
            refit.Find(CounselingCodebook.OpenQuestion).disclosure = 0.30f;
            ResponseAssessment question = CounselingResponseEvaluator.Evaluate("그때 어떤 생각이 드셨어요?");
            float before = RelationalDeliveryEvaluator.Evaluate(question, DeliveryObservation.Unavailable, initial, profile, prior).State.WillingnessToDisclose;
            float after = RelationalDeliveryEvaluator.Evaluate(question, DeliveryObservation.Unavailable, initial, profile, refit).State.WillingnessToDisclose;
            Require(after > before + 0.15f, "Refit weights must drive the relational update.");

            RelationalModelWeights gated = new RelationalModelWeights { safetyGate = 0.5f };
            float lowSafety = gated.DisclosureDelta(CounselingCodebook.OpenQuestion, 0.1f);
            float highSafety = gated.DisclosureDelta(CounselingCodebook.OpenQuestion, 0.9f);
            Require(highSafety > lowSafety, "With a safety gate, openness must follow felt safety.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
