using System;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// The shared code set for counselor micro-skills. The lexicon detector, the LLM coder
    /// (Server /code), the relational model weights, the agreement evaluation and the
    /// instructor dashboard all speak these codes, so results can be compared line by line.
    /// </summary>
    public static class CounselingCodebook
    {
        public const string Version = "ko-codebook-1";

        public const string ReflectionExploration = "reflection_exploration";
        public const string Reflection = "reflection";
        public const string Validation = "validation";
        public const string OpenQuestion = "open_question";
        public const string ClosedQuestion = "closed_question";
        public const string WhyQuestion = "why_question";
        public const string Advice = "advice";
        public const string PrematureReassurance = "premature_reassurance";
        public const string Neutral = "neutral";
        public const string Silence = "silence";

        public static readonly string[] Codes =
        {
            ReflectionExploration, Reflection, Validation, OpenQuestion, ClosedQuestion,
            WhyQuestion, Advice, PrematureReassurance, Neutral, Silence
        };

        public static bool IsKnown(string code) => Array.IndexOf(Codes, code) >= 0;

        /// <summary>Korean display label, matching the lexicon's historical skill names.</summary>
        public static string SkillLabel(string code)
        {
            switch (code)
            {
                case ReflectionExploration: return "감정 반영 + 탐색";
                case Reflection: return "감정 반영";
                case Validation: return "공감적 반응";
                case OpenQuestion: return "개방형 질문";
                case ClosedQuestion: return "닫힌 질문";
                case WhyQuestion: return "'왜' 질문";
                case Advice: return "성급한 조언";
                case PrematureReassurance: return "성급한 안심";
                case Silence: return "침묵";
                default: return "중립 반응";
            }
        }

        public static CounselingMove MoveOf(string code)
        {
            switch (code)
            {
                case ReflectionExploration: return CounselingMove.ReflectionAndExploration;
                case Reflection: return CounselingMove.Reflection;
                case Validation: return CounselingMove.Validation;
                case OpenQuestion: return CounselingMove.OpenQuestion;
                case ClosedQuestion: return CounselingMove.ClosedQuestion;
                case Advice: return CounselingMove.Advice;
                case PrematureReassurance: return CounselingMove.PrematureReassurance;
                case Silence: return CounselingMove.Silence;
                default: return CounselingMove.Neutral;
            }
        }

        /// <summary>Code for an assessment from either source ("'왜' 질문" is a Neutral move with its own code).</summary>
        public static string CodeOf(ResponseAssessment assessment)
        {
            switch (assessment.Move)
            {
                case CounselingMove.ReflectionAndExploration: return ReflectionExploration;
                case CounselingMove.Reflection: return Reflection;
                case CounselingMove.Validation: return Validation;
                case CounselingMove.OpenQuestion: return OpenQuestion;
                case CounselingMove.ClosedQuestion: return ClosedQuestion;
                case CounselingMove.Advice: return Advice;
                case CounselingMove.PrematureReassurance: return PrematureReassurance;
                case CounselingMove.Silence: return Silence;
                default:
                    return assessment.Skill == SkillLabel(WhyQuestion) ? WhyQuestion : Neutral;
            }
        }

        /// <summary>Allowed quality range per code (0–3), so a coder cannot rate advice as excellent.</summary>
        public static int ClampQuality(string code, int quality)
        {
            int min, max;
            switch (code)
            {
                case ReflectionExploration: min = 2; max = 3; break;
                case Reflection:
                case Validation:
                case OpenQuestion: min = 1; max = 3; break;
                case Neutral: min = 0; max = 2; break;
                case Silence: min = 0; max = 0; break;
                default: min = 0; max = 1; break;
            }
            return quality < min ? min : quality > max ? max : quality;
        }

        /// <summary>Default quality when a source gives only the code.</summary>
        public static int TypicalQuality(string code)
        {
            switch (code)
            {
                case ReflectionExploration: return 3;
                case Reflection:
                case Validation:
                case OpenQuestion: return 2;
                case Neutral:
                case ClosedQuestion:
                case WhyQuestion: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// Builds an assessment from an external coder's output. Returns false for unknown codes so
        /// the caller can fall back to the lexicon.
        /// </summary>
        public static bool TryFromCode(string code, int quality, string rationale, out ResponseAssessment assessment)
        {
            string normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
            if (!IsKnown(normalized))
            {
                assessment = default(ResponseAssessment);
                return false;
            }
            int q = ClampQuality(normalized, quality);
            float trust = RelationalModelWeights.Active.SafetyDelta(normalized, q);
            string why = string.IsNullOrWhiteSpace(rationale) ? SkillLabel(normalized) : rationale.Trim();
            assessment = new ResponseAssessment(MoveOf(normalized), q, trust, SkillLabel(normalized), why);
            return true;
        }
    }
}
