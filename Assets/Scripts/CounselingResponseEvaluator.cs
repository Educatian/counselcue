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
        Neutral,
        PrematureReassurance
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

    /// <summary>
    /// Pilot lexical detector for Korean counseling micro-skills. Terms are matched as
    /// stems, so each entry lists the conjugated forms learners actually type
    /// (e.g. 외롭다 → 외로우셨겠어요, 힘들다 → 힘들었겠네요). This is a prototype
    /// rule set for expert review, not a validated classifier.
    /// </summary>
    public static class CounselingResponseEvaluator
    {
        // Feeling words, including ㅂ-irregular and ㄹ-stem conjugations.
        private static readonly string[] ReflectionTerms =
        {
            "느껴", "느끼", "느낌", "마음", "감정", "기분",
            "불안", "걱정", "긴장", "초조", "조마조마",
            "힘드", "힘들", "힘든", "벅차", "버거", "지치", "지쳤", "지친",
            "두렵", "두려", "무섭", "무서", "겁이", "겁나",
            "외롭", "외로", "쓸쓸", "허전", "그립", "그리우", "그리워",
            "답답", "막막", "막히", "숨이",
            "속상", "서운", "억울", "슬프", "슬퍼", "슬픈", "우울",
            "부담", "압박", "창피", "부끄", "수치", "화가", "화나", "좌절", "혼란", "위축", "소외"
        };

        // Legitimising or permission-giving responses. Bare "이해" is avoided because
        // "이해가 안 돼요" is the opposite of validation.
        private static readonly string[] ValidationTerms =
        {
            "그럴 수", "그럴 만", "그러실 만", "그렇게 느끼실 만", "그럴 법", "충분히 그럴",
            "이해가 돼", "이해돼", "이해가 됩", "이해됩", "이해할 수 있", "이해해요", "이해합니다",
            "자연스러", "당연하", "당연한", "당연해",
            "괜찮", "천천히", "말하지 않아도", "말씀하지 않으셔도", "편하게 말씀", "함께", "듣고", "들어 보", "들어볼"
        };

        private static readonly string[] AdviceTerms =
        {
            "해야", "하세요", "보세요", "보시면", "보시는 게", "보시는 건", "지 마세요", "지 마요", "하면 돼", "하면 되", "잊어", "생각하지 마",
            "긍정적", "운동해", "게 좋겠", "게 좋아", "게 좋을", "편이 좋", "는 게 어때", "는 건 어때", "는 게 어떨", "는 건 어떨"
        };

        // Invitations to keep talking look like imperatives ("말씀해 보세요") but are
        // exploration, so they are removed before advice matching.
        private static readonly string[] InvitationTerms =
        {
            "말씀해 보", "말씀해보", "말씀하세요", "말씀하셔도", "이야기해 보", "이야기해보", "이야기하셔도",
            "얘기해 보", "얘기해보", "말해 보", "말해보", "들려주"
        };

        // Premature reassurance and minimising: comforting in intent, but they close
        // exploration before the client feels understood.
        private static readonly string[] ReassuranceTerms =
        {
            "괜찮아질", "잘될 거", "잘 될 거", "잘될 겁", "잘 될 겁", "좋아질 거", "좋아질 겁", "나아질 거", "나아질 겁",
            "다 지나갈", "지나갈 거", "금방 나아", "걱정하지 마", "걱정 마", "걱정 안 하셔도", "걱정 안 해도", "걱정하실 필요",
            "별거 아니", "별것 아니", "별일 아니", "누구나 겪", "누구나 그래", "누구나 그렇", "다들 그래", "다들 그렇",
            "힘내", "기운 내", "기운내"
        };

        private static readonly string[] OpenQuestionTerms =
        {
            "어떤", "어떻게", "어떠", "어땠", "무엇", "무슨", "뭐가", "뭘", "뭔가요", "언제", "어디서",
            "들려주", "말씀해 주", "말씀해주", "이야기해 주", "이야기해주", "얘기해 주", "얘기해주", "더 말해 줄", "더 말해줄",
            "설명해 주", "설명해주", "더 해 주", "더 해주", "더 듣고 싶"
        };

        private static readonly string[] NegatedUnderstandingTerms =
        {
            "이해가 안", "이해 안", "이해가 잘 안", "이해할 수 없", "이해가 되지 않"
        };

        public static ResponseAssessment Evaluate(string utterance)
        {
            string normalized = (utterance ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new ResponseAssessment(CounselingMove.Silence, 0, -0.03f, "침묵", "응답이 입력되지 않았습니다.");
            }

            bool reassurance = ContainsAny(normalized, ReassuranceTerms);
            bool invitation = ContainsAny(normalized, InvitationTerms);
            bool advice = ContainsAny(RemoveAll(normalized, InvitationTerms), AdviceTerms);
            bool negatedUnderstanding = ContainsAny(normalized, NegatedUnderstandingTerms);
            bool reflection = ContainsAny(normalized, ReflectionTerms);
            bool validation = !negatedUnderstanding && ContainsAny(normalized, ValidationTerms);
            bool openQuestion = invitation || ContainsAny(normalized, OpenQuestionTerms);
            bool whyQuestion = IsWhyQuestion(normalized);

            if (reassurance)
            {
                return new ResponseAssessment(
                    CounselingMove.PrematureReassurance,
                    0,
                    -0.08f,
                    "성급한 안심",
                    "위로의 의도는 좋지만, 내담자가 이해받았다고 느끼기 전에 안심시키면 탐색이 닫힐 수 있습니다.");
            }

            if (advice)
            {
                return new ResponseAssessment(CounselingMove.Advice, 0, -0.12f, "성급한 조언", "충분한 탐색 전에 해결책이 제시되어 내담자가 평가받는다고 느낄 수 있습니다.");
            }

            if (whyQuestion && !reflection)
            {
                return new ResponseAssessment(CounselingMove.Neutral, 1, 0f, "'왜' 질문", "'왜' 질문은 이유를 해명하라는 요구로 들릴 수 있습니다. '무엇이', '어떻게'로 바꿔 보세요.");
            }

            if (reflection || validation)
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

            if (openQuestion)
            {
                return new ResponseAssessment(CounselingMove.OpenQuestion, 2, 0.07f, "개방형 질문", "탐색할 여지를 주었습니다. 질문 전에 감정을 한 번 반영하면 더 안정적입니다.");
            }

            return new ResponseAssessment(CounselingMove.Neutral, 1, 0.01f, "중립 반응", "대화는 이어지지만 감정과 의미를 더 구체적으로 반영할 필요가 있습니다.");
        }

        private static bool IsWhyQuestion(string source) =>
            source.StartsWith("왜", StringComparison.Ordinal) ||
            source.Contains(" 왜 ", StringComparison.Ordinal) ||
            source.Contains("왜 그", StringComparison.Ordinal) ||
            source.Contains("왜요", StringComparison.Ordinal);

        private static string RemoveAll(string source, string[] terms)
        {
            string result = source;
            for (int i = 0; i < terms.Length; i++) result = result.Replace(terms[i], " ");
            return result;
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
