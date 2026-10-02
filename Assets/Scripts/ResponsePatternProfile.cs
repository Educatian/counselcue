using System;
using System.Collections.Generic;
using System.Text;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Which kinds of response a counselor keeps choosing across a session — not only whether
    /// each single response was appropriate. Reviewer feedback: repeated choices show the skills
    /// a trainee relies on, the ones they rarely use and possible weak areas.
    /// </summary>
    public sealed class ResponsePatternProfile
    {
        /// <summary>Exploratory and empathic skills a first-stage session is expected to use.</summary>
        public static readonly string[] CoreSkills =
        {
            CounselingCodebook.Reflection, CounselingCodebook.ReflectionExploration,
            CounselingCodebook.Validation, CounselingCodebook.OpenQuestion
        };

        /// <summary>Responses that tend to close exploration when they dominate.</summary>
        public static readonly string[] ClosingMoves =
        {
            CounselingCodebook.ClosedQuestion, CounselingCodebook.WhyQuestion,
            CounselingCodebook.Advice, CounselingCodebook.PrematureReassurance
        };

        public int Total { get; private set; }
        public float AverageQuality { get; private set; }
        public IReadOnlyList<KeyValuePair<string, int>> Counts => counts;
        public IReadOnlyList<string> Underused => underused;
        /// <summary>Longest run of the same code (e.g. three closed questions in a row).</summary>
        public string LongestStreakCode { get; private set; } = string.Empty;
        public int LongestStreak { get; private set; }
        public float ClosingShare { get; private set; }

        private readonly List<KeyValuePair<string, int>> counts = new List<KeyValuePair<string, int>>();
        private readonly List<string> underused = new List<string>();

        public static ResponsePatternProfile Build(IReadOnlyList<string> codes, IReadOnlyList<int> qualities = null)
        {
            ResponsePatternProfile profile = new ResponsePatternProfile();
            if (codes == null) return profile;
            Dictionary<string, int> tally = new Dictionary<string, int>();
            string previous = null;
            int run = 0;
            int closing = 0;
            int qualitySum = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                string code = CounselingCodebook.IsKnown(codes[i]) ? codes[i] : CounselingCodebook.Neutral;
                tally[code] = tally.TryGetValue(code, out int n) ? n + 1 : 1;
                run = code == previous ? run + 1 : 1;
                previous = code;
                if (run > profile.LongestStreak)
                {
                    profile.LongestStreak = run;
                    profile.LongestStreakCode = code;
                }
                if (Array.IndexOf(ClosingMoves, code) >= 0) closing++;
                if (qualities != null && i < qualities.Count) qualitySum += qualities[i];
            }
            profile.Total = codes.Count;
            profile.AverageQuality = codes.Count == 0 ? 0f : (float)qualitySum / codes.Count;
            profile.ClosingShare = codes.Count == 0 ? 0f : (float)closing / codes.Count;
            foreach (KeyValuePair<string, int> pair in tally) profile.counts.Add(pair);
            profile.counts.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : Array.IndexOf(CounselingCodebook.Codes, a.Key).CompareTo(Array.IndexOf(CounselingCodebook.Codes, b.Key)));
            foreach (string skill in CoreSkills)
            {
                if (!tally.ContainsKey(skill)) profile.underused.Add(skill);
            }
            return profile;
        }

        /// <summary>Compact Korean summary for the debrief rail (a few short lines).</summary>
        public string ToKoreanSummary(int maxCodes = 4)
        {
            if (Total == 0) return string.Empty;
            StringBuilder text = new StringBuilder();
            text.Append("반응 유형  ");
            for (int i = 0; i < counts.Count && i < maxCodes; i++)
            {
                if (i > 0) text.Append(" · ");
                text.Append(CounselingCodebook.SkillLabel(counts[i].Key)).Append(' ').Append(counts[i].Value);
            }
            if (counts.Count > maxCodes) text.Append(" 외");
            text.Append('\n');
            if (underused.Count > 0)
            {
                text.Append("덜 쓴 기술  ");
                for (int i = 0; i < underused.Count; i++)
                {
                    if (i > 0) text.Append(" · ");
                    text.Append(CounselingCodebook.SkillLabel(underused[i]));
                }
                text.Append('\n');
            }
            if (LongestStreak >= 3)
                text.Append($"반복 패턴  {CounselingCodebook.SkillLabel(LongestStreakCode)} {LongestStreak}회 연속\n");
            if (ClosingShare >= 0.3f)
                text.Append($"닫는 반응 비율 {Math.Round(ClosingShare * 100f)}% · 탐색을 여는 반응을 늘려 보세요\n");
            return text.ToString().TrimEnd('\n');
        }
    }
}
