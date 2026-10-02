using System;

namespace AdieLab.AffectCounsel
{
    [Serializable]
    public sealed class CounselingTurnSnapshot
    {
        public int turn;
        public string stage;
        public string counselorUtterance;
        public string clientPrompt;
        public string clientReply;
        public string skill;
        public string skillCode;
        public string codingSource;
        public string skillRationale;
        public int quality;
        public string alignment;
        public string coachingFeedback;
        public ClientRelationalState stateBefore;
        public ClientRelationalState stateAfter;
        public string selfAssessment;
        /// <summary>Cues in the client's line the counselor could have followed (LLM coder).</summary>
        public string[] focusOptions = Array.Empty<string>();
        /// <summary>An example alternative response (LLM coder); empty when the response was strong.</summary>
        public string alternativeResponse = string.Empty;
        /// <summary>Latest scene-replay attempt of this turn (empty/-1 when not replayed).</summary>
        public string retryUtterance = string.Empty;
        public string retryReply = string.Empty;
        public string retrySkill = string.Empty;
        public int retryQuality = -1;
    }

    [Serializable]
    public sealed class CounselingSelfAssessmentRecord
    {
        public int schemaVersion = ResearchRecord.SchemaVersion;
        public string sessionId;
        public string timestampUtc;
        public string caseId;
        public string trainingMode;
        public int sourceTurn;
        public string selfAssessment;
        public string skill;
        public string skillCode;
        public string codingSource;
        public int quality;
    }
}
