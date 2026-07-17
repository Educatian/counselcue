using System;

namespace AdieLab.AffectCounsel
{
    public enum CounselingMove
    {
        Silence,
        Reflection,
        Validation,
        ReflectionAndExploration,
        OpenQuestion,
        Advice,
        Neutral
    }

    public readonly struct ResponseAssessment
    {
        public ResponseAssessment(CounselingMove move, int quality, float trustDelta, string skill, string rationale)
        {
            Move = move;
            Quality = quality;
            TrustDelta = trustDelta;
            Skill = skill;
            Rationale = rationale;
        }

        public CounselingMove Move { get; }
        public int Quality { get; }
        public float TrustDelta { get; }
        public string Skill { get; }
        public string Rationale { get; }
    }

    public static class CounselingResponseEvaluator
    {
        private static readonly string[] ReflectionTerms =
        {
            "느껴", "마음", "불안", "걱정", "힘드", "벅차", "두렵", "외롭", "답답"
        };

        private static readonly string[] ValidationTerms =
        {
            "그럴 수", "이해", "괜찮", "천천히", "말하지 않아도", "함께", "듣고"
        };

        private static readonly string[] AdviceTerms =
        {
            "해야", "하세요", "하면 돼", "잊어", "생각하지 마", "긍정적", "운동해"
        };

        public static ResponseAssessment Evaluate(string utterance)
        {
            string normalized = (utterance ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new ResponseAssessment(CounselingMove.Silence, 0, -0.03f, "침묵", "응답이 입력되지 않았습니다.");
            }

            bool reflection = ContainsAny(normalized, ReflectionTerms);
            bool validation = ContainsAny(normalized, ValidationTerms);
            bool advice = ContainsAny(normalized, AdviceTerms);
            bool openQuestion = normalized.Contains("어떤", StringComparison.Ordinal) ||
                                normalized.Contains("어떻게", StringComparison.Ordinal) ||
                                normalized.Contains("무엇", StringComparison.Ordinal) ||
                                normalized.Contains("들려주", StringComparison.Ordinal);

            if ((reflection || validation) && !advice)
            {
                string skill = reflection && openQuestion ? "감정 반영 + 탐색" : "공감적 반응";
                CounselingMove move = reflection && openQuestion
                    ? CounselingMove.ReflectionAndExploration
                    : validation
                        ? CounselingMove.Validation
                        : CounselingMove.Reflection;
                return new ResponseAssessment(
                    move,
                    reflection && validation ? 3 : 2,
                    reflection && validation ? 0.16f : 0.10f,
                    skill,
                    "내담자의 경험을 먼저 인정해 안전한 자기개방을 도왔습니다.");
            }

            if (openQuestion && !advice)
            {
                return new ResponseAssessment(CounselingMove.OpenQuestion, 2, 0.07f, "개방형 질문", "탐색할 여지를 주었습니다. 질문 전에 감정을 한 번 반영하면 더 안정적입니다.");
            }

            if (advice)
            {
                return new ResponseAssessment(CounselingMove.Advice, 0, -0.12f, "성급한 조언", "충분한 탐색 전에 해결책이 제시되어 내담자가 평가받는다고 느낄 수 있습니다.");
            }

            return new ResponseAssessment(CounselingMove.Neutral, 1, 0.01f, "중립 반응", "대화는 이어지지만 감정과 의미를 더 구체적으로 반영할 필요가 있습니다.");
        }

        private static bool ContainsAny(string source, string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (source.Contains(terms[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
