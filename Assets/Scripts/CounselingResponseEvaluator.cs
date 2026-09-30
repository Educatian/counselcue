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
        private static readonly string[] DefaultReflectionTerms =
        {
            "느껴", "느끼", "느낌", "마음", "감정", "기분",
            "불안", "걱정", "긴장", "초조", "조마조마",
            "힘드", "힘들", "힘든", "벅차", "버거", "지치", "지쳤", "지친",
            "두렵", "두려", "무섭", "무서", "겁이", "겁나",
            "외롭", "외로", "쓸쓸", "허전", "그립", "그리우", "그리워",
            "답답", "막막", "막히", "숨이",
            "속상", "서운", "억울", "슬프", "슬퍼", "슬픈", "우울",
            "부담", "압박", "창피", "부끄", "수치", "화가", "화나", "좌절", "혼란", "위축", "소외",
            "실망", "허탈", "후회", "죄책", "미안", "원망", "섭섭", "아쉬", "고민", "마음이 아프", "마음 아프"
        };

        // Legitimising or permission-giving responses. Bare "이해" is avoided because
        // "이해가 안 돼요" is the opposite of validation.
        private static readonly string[] DefaultValidationTerms =
        {
            "그럴 수", "그럴 만", "그러실 만", "그렇게 느끼실 만", "그럴 법", "충분히 그럴",
            "이해가 돼", "이해돼", "이해가 됩", "이해됩", "이해할 수 있", "이해해요", "이해합니다",
            "자연스러", "당연하", "당연한", "당연해",
            "괜찮", "천천히", "말하지 않아도", "말씀하지 않으셔도", "편하게 말씀", "함께", "듣고", "애쓰", "애써"
        };

        // Directives and prescriptions. Matched only after invitations, introspective
        // prompts and questions are removed (see Neutralize), so "말씀해 보세요",
        // "떠올려 보세요" and "어떻게 생각하세요?" are not treated as advice.
        private static readonly string[] DefaultAdviceTerms =
        {
            "해야", "셔야 해", "셔야 합", "셔야 돼", "셔야 됩", "하세요", "해 보세요", "해보세요", "드려 보세요", "드려보세요", "가 보세요", "가보세요",
            "만나 보세요", "만나보세요", "바꿔 보세요", "바꿔보세요", "써 보세요", "써보세요", "찾아 보세요", "찾아보세요",
            "쉬어 보세요", "쉬어보세요", "지 마세요", "지 마요", "하면 돼", "하면 되", "잊어", "생각하지 마",
            "긍정적", "운동해", "게 좋겠", "게 좋아요", "게 좋습니다", "게 좋을 거", "게 좋을 것", "편이 좋",
            "는 게 어때", "는 건 어때", "는 게 어떨", "는 건 어떨"
        };

        // Invitations to keep talking with the counselor, and introspective prompts.
        // They look like imperatives but are exploration.
        private static readonly string[] DefaultInvitationTerms =
        {
            "말씀해 보", "말씀해보", "말씀하세요", "말씀하셔도", "이야기해 보", "이야기해보", "이야기하셔도",
            "얘기해 보", "얘기해보", "말해 보", "말해보", "들려주",
            "떠올려 보", "떠올려보", "돌아보", "돌이켜 보", "돌이켜보", "생각해 보", "생각해보", "느껴 보", "느껴보",
            "살펴보", "살펴 보"
        };

        // A talk invitation aimed at someone else ("팀장님께 이야기해 보세요") is advice.
        // The counselor as addressee ("저에게") is removed before this check.
        private static readonly string[] DefaultThirdPartyMarkers =
        {
            "께 ", "께서", "에게", "한테", "랑 ", "과 이야기", "와 이야기", "과 얘기", "와 얘기", "과 한번", "와 한번"
        };

        private static readonly string[] DefaultCounselorAddressTerms = { "저에게", "저한테", "제게", "여기서", "여기에서" };

        // Question and reported-speech forms that contain advice stems but are not advice.
        private static readonly string[] DefaultNonDirectiveForms =
        {
            "생각하세요", "생각하시", "느끼세요", "하세요?", "하세요 ?", "하시나요", "하셨어요", "하셨나요",
            "해야 한다고", "해야 한다는", "해야 된다고", "해야 된다는", "해야 하나", "해야 할지"
        };

        // Premature reassurance and minimising: comforting in intent, but they close
        // exploration before the client feels understood. A match followed by a
        // quotative ("잘될 거라고 믿으셨는데", "누구나 겪는 일이라는 말") reflects what
        // the client or others said, and is not counted.
        private static readonly string[] DefaultReassuranceTerms =
        {
            "괜찮아질", "잘될 거", "잘 될 거", "잘될 겁", "잘 될 겁", "좋아질 거", "좋아질 겁", "나아질 거", "나아질 겁",
            "다 지나갈", "지나갈 거", "금방 나아", "걱정하지 마", "걱정 마", "걱정 안 하셔도", "걱정 안 해도", "걱정하실 필요",
            "별거 아니", "별것 아니", "별일 아니", "누구나 겪", "누구나 그래", "누구나 그렇", "다들 그래", "다들 그렇",
            "힘내세요", "힘내요", "힘 내세요", "힘 내요", "힘내!", "기운 내세요", "기운내세요", "기운 내요"
        };

        private static readonly string[] DefaultQuotativeMarkers =
        {
            "라고", "라는", "다고", "다는", "는 말", "말하", "말을", "말씀", "생각하", "믿으", "희망", "기대"
        };

        private static readonly string[] DefaultOpenQuestionTerms =
        {
            "어떤", "어떻게", "어떠", "어땠", "무엇", "무슨 일", "무슨 생각", "무슨 마음", "무슨 느낌", "무슨 의미",
            "뭐가", "뭘", "뭔가요", "언제", "어디서",
            "들려주", "말씀해 주", "말씀해주", "이야기해 주", "이야기해주", "얘기해 주", "얘기해주", "더 말해 줄", "더 말해줄",
            "설명해 주", "설명해주", "더 해 주", "더 해주", "더 듣고 싶"
        };

        private static readonly string[] DefaultNegatedUnderstandingTerms =
        {
            "이해가 안", "이해 안", "이해가 잘 안", "이해할 수 없", "이해가 되지 않"
        };


        private static SkillLexicon active;

        /// <summary>
        /// The term lists in use. Experts can override any list by placing
        /// Resources/CounselCue/skill-lexicon.json (see Tools → CounselCue → Export Skill Lexicon JSON);
        /// lists missing or empty in the file keep the built-in defaults below.
        /// </summary>
        public static SkillLexicon Active
        {
            get
            {
                if (active == null) active = SkillLexicon.LoadOrDefault(Defaults());
                return active;
            }
        }

        /// <summary>Replaces the active lexicon (tests, expert tools). Pass null to reload.</summary>
        public static void UseLexicon(SkillLexicon lexicon) => active = lexicon;

        public static SkillLexicon Defaults() => new SkillLexicon
        {
            reflection = (string[])DefaultReflectionTerms.Clone(),
            validation = (string[])DefaultValidationTerms.Clone(),
            advice = (string[])DefaultAdviceTerms.Clone(),
            invitation = (string[])DefaultInvitationTerms.Clone(),
            thirdPartyMarkers = (string[])DefaultThirdPartyMarkers.Clone(),
            counselorAddress = (string[])DefaultCounselorAddressTerms.Clone(),
            nonDirective = (string[])DefaultNonDirectiveForms.Clone(),
            reassurance = (string[])DefaultReassuranceTerms.Clone(),
            quotative = (string[])DefaultQuotativeMarkers.Clone(),
            openQuestion = (string[])DefaultOpenQuestionTerms.Clone(),
            negatedUnderstanding = (string[])DefaultNegatedUnderstandingTerms.Clone()
        };

        private static string[] ReflectionTerms => Active.reflection;
        private static string[] ValidationTerms => Active.validation;
        private static string[] AdviceTerms => Active.advice;
        private static string[] InvitationTerms => Active.invitation;
        private static string[] ThirdPartyMarkers => Active.thirdPartyMarkers;
        private static string[] CounselorAddressTerms => Active.counselorAddress;
        private static string[] NonDirectiveForms => Active.nonDirective;
        private static string[] ReassuranceTerms => Active.reassurance;
        private static string[] QuotativeMarkers => Active.quotative;
        private static string[] OpenQuestionTerms => Active.openQuestion;
        private static string[] NegatedUnderstandingTerms => Active.negatedUnderstanding;

        public static ResponseAssessment Evaluate(string utterance)
        {
            string normalized = (utterance ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new ResponseAssessment(CounselingMove.Silence, 0, -0.03f, "침묵", "응답이 입력되지 않았습니다.");
            }

            bool reassurance = ContainsUnquoted(normalized, ReassuranceTerms);
            string addressed = RemoveAll(normalized, CounselorAddressTerms);
            bool thirdParty = ContainsAny(addressed, ThirdPartyMarkers);
            bool invitation = !thirdParty && ContainsAny(normalized, InvitationTerms);
            string directiveText = RemoveAll(normalized, NonDirectiveForms);
            if (!thirdParty) directiveText = RemoveAll(directiveText, InvitationTerms);
            bool advice = ContainsAny(directiveText, AdviceTerms) ||
                          (thirdParty && ContainsAny(normalized, InvitationTerms));
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

        private static bool ContainsUnquoted(string source, string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                int index = source.IndexOf(terms[i], StringComparison.Ordinal);
                while (index >= 0)
                {
                    int after = index + terms[i].Length;
                    string tail = source.Substring(after, Math.Min(10, source.Length - after));
                    if (!ContainsAny(tail, QuotativeMarkers)) return true;
                    index = source.IndexOf(terms[i], after, StringComparison.Ordinal);
                }
            }

            return false;
        }

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
