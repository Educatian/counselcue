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
    }
}
