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
