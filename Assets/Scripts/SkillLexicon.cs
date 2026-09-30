using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Term lists used by <see cref="CounselingResponseEvaluator"/>. Serialized with JsonUtility so
    /// counseling experts can revise the pilot rules without touching code.
    /// </summary>
    [Serializable]
    public sealed class SkillLexicon
    {
        public const string ResourcePath = "CounselCue/skill-lexicon";

        public string version = "ko-pilot-2";
        public string[] reflection;
        public string[] validation;
        public string[] advice;
        public string[] invitation;
        public string[] thirdPartyMarkers;
        public string[] counselorAddress;
        public string[] nonDirective;
        public string[] reassurance;
        public string[] quotative;
        public string[] openQuestion;
        public string[] negatedUnderstanding;

        /// <summary>Loads Resources/CounselCue/skill-lexicon.json over the defaults, list by list.</summary>
        public static SkillLexicon LoadOrDefault(SkillLexicon defaults)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return defaults;
                SkillLexicon overrides = JsonUtility.FromJson<SkillLexicon>(asset.text);
                return overrides == null ? defaults : defaults.MergedWith(overrides);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"CounselCue skill lexicon override ignored: {exception.Message}");
                return defaults;
            }
        }

        public SkillLexicon MergedWith(SkillLexicon overrides)
        {
            return new SkillLexicon
            {
                version = string.IsNullOrWhiteSpace(overrides.version) ? version : overrides.version,
                reflection = Pick(overrides.reflection, reflection),
                validation = Pick(overrides.validation, validation),
                advice = Pick(overrides.advice, advice),
                invitation = Pick(overrides.invitation, invitation),
                thirdPartyMarkers = Pick(overrides.thirdPartyMarkers, thirdPartyMarkers),
                counselorAddress = Pick(overrides.counselorAddress, counselorAddress),
                nonDirective = Pick(overrides.nonDirective, nonDirective),
                reassurance = Pick(overrides.reassurance, reassurance),
                quotative = Pick(overrides.quotative, quotative),
                openQuestion = Pick(overrides.openQuestion, openQuestion),
                negatedUnderstanding = Pick(overrides.negatedUnderstanding, negatedUnderstanding)
            };
        }

        private static string[] Pick(string[] candidate, string[] fallback) =>
            candidate != null && candidate.Length > 0 ? candidate : fallback;
    }
}
