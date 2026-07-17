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
