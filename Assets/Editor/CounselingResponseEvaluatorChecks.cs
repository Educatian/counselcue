using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Regression fixtures for the pilot Korean micro-skill detector. Each line is a
    /// realistic counselor utterance and the move(s) an expert would accept. Add a
    /// fixture whenever reviewers report a misclassification.
    /// </summary>
    public static class CounselingResponseEvaluatorChecks
    {
        private static readonly (string utterance, CounselingMove[] accepted)[] Fixtures =
        {
            ("많이 힘드셨겠어요.", Moves(CounselingMove.Reflection)),
            // Reviewer report: "안녕하세요" was coded as advice because it contains "하세요".
            ("지혜씨 안녕하세요. 저는 상담사 최현진입니다. 만나뵙게 되어 반갑습니다.", Moves(CounselingMove.Neutral)),
            ("회사에 가는 게 정말 힘들었겠네요.", Moves(CounselingMove.Reflection)),
            ("혼자 버티느라 외로우셨겠어요.", Moves(CounselingMove.Reflection)),
            ("또 실수할까 봐 두려우셨군요.", Moves(CounselingMove.Reflection)),
            ("아내분이 많이 그리우시겠어요.", Moves(CounselingMove.Reflection)),
            ("부모님이 게으르다고 하실 때 속상했겠다.", Moves(CounselingMove.Reflection)),
            ("한국어 회의에서 소외감을 느끼셨군요.", Moves(CounselingMove.Reflection)),
            ("회사를 그만두고 싶은 마음도 드시는군요.", Moves(CounselingMove.Reflection)),
            ("그렇게 느끼실 만해요. 그럴 만하죠.", Moves(CounselingMove.Validation)),
            ("그럴 수 있어요. 천천히 말씀하셔도 괜찮아요.", Moves(CounselingMove.Validation)),
            ("그때 기분이 어떠셨어요?", Moves(CounselingMove.ReflectionAndExploration)),
            ("그 조용함이 어떻게 느껴지세요?", Moves(CounselingMove.ReflectionAndExploration)),
            ("무슨 일이 있었는지 말씀해 주시겠어요?", Moves(CounselingMove.OpenQuestion)),
            ("그 이야기를 조금 더 해 주실 수 있을까요?", Moves(CounselingMove.OpenQuestion)),
            ("편하게 말씀해 보세요.", Moves(CounselingMove.OpenQuestion, CounselingMove.Validation)),
            ("괜찮아요, 다 잘될 거예요.", Moves(CounselingMove.PrematureReassurance)),
            ("시간이 지나면 괜찮아질 거예요.", Moves(CounselingMove.PrematureReassurance)),
            ("그 정도는 누구나 겪는 일이에요.", Moves(CounselingMove.PrematureReassurance)),
            ("너무 걱정하지 마세요.", Moves(CounselingMove.PrematureReassurance, CounselingMove.Advice)),
            ("팀장님께 직접 말씀드려 보세요.", Moves(CounselingMove.Advice)),
            ("그냥 그만두는 게 좋겠어요.", Moves(CounselingMove.Advice)),
            ("운동을 해 보는 건 어떨까요?", Moves(CounselingMove.Advice)),
            ("자책하지 마세요.", Moves(CounselingMove.Advice)),
            ("팀장님과 한번 이야기해 보세요.", Moves(CounselingMove.Advice)),
            ("가족에게 솔직하게 얘기해 보세요.", Moves(CounselingMove.Advice)),
            ("저에게 편하게 이야기해 보세요.", Moves(CounselingMove.OpenQuestion, CounselingMove.Validation)),
            ("본인은 그 상황을 어떻게 보세요?", Moves(CounselingMove.OpenQuestion)),
            ("지금 돌아보시면 어떤 마음이 드세요?", Moves(CounselingMove.ReflectionAndExploration)),
            ("그때 어떤 마음이었는지 떠올려 보세요.", Moves(CounselingMove.ReflectionAndExploration)),
            ("어떻게 생각하세요?", Moves(CounselingMove.OpenQuestion)),
            ("그럴 때 보통 어떻게 하세요?", Moves(CounselingMove.OpenQuestion)),
            ("완벽하게 해야 한다고 느끼시는군요.", Moves(CounselingMove.Reflection)),
            ("이번엔 잘될 거라고 믿으셨는데 실망이 크셨겠어요.", Moves(CounselingMove.Reflection)),
            ("다들 그렇게 말하니까 더 답답하셨겠어요.", Moves(CounselingMove.Reflection)),
            ("누구나 겪는 일이라는 말을 들으면 더 서운하셨겠어요.", Moves(CounselingMove.Reflection, CounselingMove.Validation)),
            ("힘내라는 말이 오히려 부담이셨군요.", Moves(CounselingMove.Reflection)),
            ("그동안 힘내서 버텨 오셨군요.", Moves(CounselingMove.Neutral, CounselingMove.Reflection, CounselingMove.Validation)),
            ("어떤 선택을 하는 게 좋을지 막막하시겠어요.", Moves(CounselingMove.ReflectionAndExploration)),
            ("사람들 만나는 게 좋아서 시작하신 일이었군요.", Moves(CounselingMove.Neutral, CounselingMove.Reflection)),
            ("어떻게 하는 게 좋을지 고민되시는군요.", Moves(CounselingMove.ReflectionAndExploration)),
            ("퇴사는 신중하게 결정하셔야 해요.", Moves(CounselingMove.Advice)),
            ("혼자 다 감당하셔야 했군요. 많이 지치셨겠어요.", Moves(CounselingMove.Reflection)),
            ("이해가 안 되네요. 왜 그렇게 하셨어요?", Moves(CounselingMove.Neutral)),
            ("네.", Moves(CounselingMove.Neutral)),
            ("", Moves(CounselingMove.Silence))
        };

        public static int LastFailureCount { get; private set; }

        [MenuItem("Tools/CounselCue/Run Response Evaluator Checks")]
        public static void RunFromMenu()
        {
            List<string> failures = Run();
            LastFailureCount = failures.Count;
            if (failures.Count == 0) Debug.Log($"RESPONSE_EVALUATOR_CHECKS_PASS ({Fixtures.Length} fixtures)");
        }

        public static void RunFromCommandLine()
        {
            List<string> failures = Run();
            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        private static List<string> Run()
        {
            List<string> failures = new List<string>();
            for (int i = 0; i < Fixtures.Length; i++)
            {
                string utterance = Fixtures[i].utterance;
                CounselingMove[] accepted = Fixtures[i].accepted;
                CounselingMove actual = CounselingResponseEvaluator.Evaluate(utterance).Move;
                if (Array.IndexOf(accepted, actual) >= 0) continue;
                string message = $"\"{utterance}\": expected {string.Join(" or ", accepted)}, got {actual}";
                failures.Add(message);
                Debug.LogError($"RESPONSE_EVALUATOR_CHECK_FAILED {message}");
            }
            return failures;
        }

        private static CounselingMove[] Moves(params CounselingMove[] moves) => moves;
    }
}
