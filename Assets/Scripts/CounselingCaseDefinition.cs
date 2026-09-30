using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [Serializable]
    public sealed class CounselingDisclosureStep
    {
        [TextArea] public string supportiveReply;
        [TextArea] public string guardedReply;
    }

    [Serializable]
    public sealed class CounselingFocusSkill
    {
        public string id;
        public string label;
        [TextArea] public string objective;
        [TextArea] public string coachingPrompt;
    }

    [Serializable]
    public sealed class CaseEnglishText
    {
        public string title;
        public string clientName;
        public string clientProfile;
        [TextArea] public string presentingConcern;
        public string[] learningObjectives = Array.Empty<string>();
    }

    [CreateAssetMenu(fileName = "CounselingCase", menuName = "CounselCue/Counseling Case")]
    public sealed class CounselingCaseDefinition : ScriptableObject
    {
        [SerializeField] private string caseId = "workplace-anxiety-01";
        [SerializeField] private string caseTitle = "직장 불안";
        [SerializeField] private string clientName = "김지혜";
        [SerializeField] private string clientProfile = "32세 · 초기면담";
        [SerializeField, TextArea] private string presentingConcern;
        [SerializeField, TextArea] private string initialClientLine;
        [SerializeField] private float fullSessionSeconds = 900f;
        [SerializeField] private float focusedPracticeSeconds = 180f;
        [SerializeField] private int focusedTargetTurns = 3;
        [SerializeField] private string[] learningObjectives = Array.Empty<string>();
        [SerializeField] private CounselingDisclosureStep[] disclosureLadder = Array.Empty<CounselingDisclosureStep>();
        [SerializeField] private CounselingFocusSkill[] focusSkills = Array.Empty<CounselingFocusSkill>();
        [SerializeField] private ClientProfileDefinition clientProfileDefinition;
        [SerializeField] private AvatarPresentationDefinition avatarPresentation;
        [SerializeField] private string counselingDomain = "직업·성인상담";
        [SerializeField] private string difficultyLabel = "기초";
        [SerializeField, TextArea] private string personaPromptKey = "workplace-anxiety-01";
        [Tooltip("Optional AI-generated case illustration shown on the briefing card (Assets/Art/Higgsfield/Portraits/{caseId}).")]
        [SerializeField] private Sprite briefingPortrait;
        [SerializeField] private CaseEnglishText english = new CaseEnglishText();

        public string CaseId => caseId;
        public string CaseTitle => caseTitle;
        public string ClientName => clientName;
        public string ClientProfile => clientProfile;
        public string PresentingConcern => presentingConcern;
        public string InitialClientLine => initialClientLine;
        public float FullSessionSeconds => fullSessionSeconds;
        public float FocusedPracticeSeconds => focusedPracticeSeconds;
        public int FocusedTargetTurns => focusedTargetTurns;
        public string[] LearningObjectives => learningObjectives;
        public CounselingFocusSkill[] FocusSkills => focusSkills;
        public ClientProfileDefinition ProfileDefinition => clientProfileDefinition;
        public AvatarPresentationDefinition AvatarPresentation => avatarPresentation;
        public string CounselingDomain => counselingDomain;
        public string DifficultyLabel => difficultyLabel;
        public string PersonaPromptKey => personaPromptKey;
        public Sprite BriefingPortrait => briefingPortrait;

        // English UI text; each falls back to the Korean source when a translation is missing.
        public string LocalizedTitle(bool useEnglish) => Pick(useEnglish, english?.title, caseTitle);
        public string LocalizedName(bool useEnglish) => Pick(useEnglish, english?.clientName, clientName);
        public string LocalizedProfile(bool useEnglish) => Pick(useEnglish, english?.clientProfile, clientProfile);
        public string LocalizedConcern(bool useEnglish) => Pick(useEnglish, english?.presentingConcern, presentingConcern);
        public string[] LocalizedObjectives(bool useEnglish) =>
            useEnglish && english?.learningObjectives != null && english.learningObjectives.Length > 0
                ? english.learningObjectives
                : learningObjectives;

        private static string Pick(bool useEnglish, string translated, string source) =>
            useEnglish && !string.IsNullOrWhiteSpace(translated) ? translated : source;

        public string GetReply(int turnIndex, bool supportive)
        {
            if (disclosureLadder == null || disclosureLadder.Length == 0) return initialClientLine;
            CounselingDisclosureStep step = disclosureLadder[Mathf.Clamp(turnIndex, 0, disclosureLadder.Length - 1)];
            return supportive ? step.supportiveReply : step.guardedReply;
        }

        public void Configure(
            string configuredCaseId,
            string configuredTitle,
            string configuredClientName,
            string configuredClientProfile,
            string configuredConcern,
            string configuredInitialLine,
            float configuredFullSeconds,
            float configuredFocusedSeconds,
            int configuredFocusedTurns,
            string[] configuredObjectives,
            CounselingDisclosureStep[] configuredLadder,
            CounselingFocusSkill[] configuredFocusSkills)
        {
            caseId = configuredCaseId;
            caseTitle = configuredTitle;
            clientName = configuredClientName;
            clientProfile = configuredClientProfile;
            presentingConcern = configuredConcern;
            initialClientLine = configuredInitialLine;
            fullSessionSeconds = configuredFullSeconds;
            focusedPracticeSeconds = configuredFocusedSeconds;
            focusedTargetTurns = configuredFocusedTurns;
            learningObjectives = configuredObjectives;
            disclosureLadder = configuredLadder;
            focusSkills = configuredFocusSkills;
        }

        public void ConfigureEnglish(CaseEnglishText text)
        {
            english = text ?? new CaseEnglishText();
        }

        public void ConfigurePortrait(Sprite portrait)
        {
            briefingPortrait = portrait;
        }

        public void ConfigurePresentation(
            ClientProfileDefinition configuredProfile,
            AvatarPresentationDefinition configuredPresentation,
            string configuredDomain,
            string configuredDifficulty,
            string configuredPersonaPromptKey)
        {
            clientProfileDefinition = configuredProfile;
            avatarPresentation = configuredPresentation;
            counselingDomain = configuredDomain;
            difficultyLabel = configuredDifficulty;
            personaPromptKey = configuredPersonaPromptKey;
        }
    }
}
