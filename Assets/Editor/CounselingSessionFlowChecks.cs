using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselingSessionFlowChecks
    {
        [MenuItem("Tools/Affect Counsel/Run Session Flow Checks")]
        public static void RunFromMenu() => Run();

        public static void RunFromCommandLine()
        {
            bool passed = Run();
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static bool Run()
        {
            bool passed = true;
            passed &= ExpectStage(0, 0.25f, CounselingStage.Rapport);
            passed &= ExpectStage(4, 0.25f, CounselingStage.InitialExploration);
            passed &= ExpectStage(4, 0.40f, CounselingStage.EmotionalDeepening);
            passed &= ExpectStage(6, 0.55f, CounselingStage.CoreExploration);
            passed &= ExpectStage(8, 0.55f, CounselingStage.CoreExploration);
            passed &= ExpectStage(8, 0.70f, CounselingStage.Consolidation);
            passed &= ExpectStage(10, 0.70f, CounselingStage.Closing);
            passed &= Expect(CounselingSessionFlow.StageLabel(CounselingStage.Rapport) == "관계 형성", "rapport label");
            passed &= Expect(CounselingSessionFlow.ModeLabel(TrainingMode.Evaluation) == "평가 모드", "evaluation label");
            passed &= Expect(CounselingSessionFlow.ModeLabel(TrainingMode.FocusedPractice) == "집중연습", "focused practice label");
            passed &= Expect(CounselingSessionFlow.ModeLabel(TrainingMode.SceneReplay) == "장면 재연습", "scene replay label");
            CounselingCaseDefinition definition = AssetDatabase.LoadAssetAtPath<CounselingCaseDefinition>(
                "Assets/Data/Cases/WorkplaceAnxietyCase.asset");
            passed &= Expect(definition != null, "workplace anxiety case asset");
            if (definition != null)
            {
                passed &= Expect(definition.LearningObjectives.Length == 3, "three learning objectives");
                passed &= Expect(definition.FocusSkills.Length == 3, "three focus skills");
                passed &= Expect(definition.FullSessionSeconds == 900, "full session duration");
                passed &= Expect(definition.FocusedPracticeSeconds == 180, "focused practice duration");
                passed &= Expect(definition.FocusedTargetTurns == 3, "focused practice target turns");
                passed &= Expect(!string.IsNullOrWhiteSpace(definition.GetReply(0, true)), "supportive disclosure reply");
                passed &= Expect(!string.IsNullOrWhiteSpace(definition.GetReply(0, false)), "guarded disclosure reply");
            }
            passed &= Expect(CounselingSubmissionGuard.CanCommit(2, 2, 5, 5, true), "current active submission commits");
            passed &= Expect(!CounselingSubmissionGuard.CanCommit(2, 2, 5, 5, false), "paused submission is rejected");
            passed &= Expect(!CounselingSubmissionGuard.CanCommit(2, 2, 5, 6, true), "canceled submission stays rejected after resume");
            passed &= Expect(!CounselingSubmissionGuard.CanCommit(2, 3, 5, 5, true), "stale session submission is rejected after restart");

            Debug.Log(passed ? "SESSION_FLOW_CHECKS_PASS" : "SESSION_FLOW_CHECKS_FAIL");
            return passed;
        }

        private static bool ExpectStage(int turns, float disclosure, CounselingStage expected)
        {
            CounselingStage actual = CounselingSessionFlow.DetermineStage(turns, disclosure);
            return Expect(actual == expected, $"turns={turns}, disclosure={disclosure:0.00}: expected {expected}, got {actual}");
        }

        private static bool Expect(bool condition, string message)
        {
            if (!condition) Debug.LogError($"SESSION_FLOW_CHECK_FAILED {message}");
            return condition;
        }
    }
}
