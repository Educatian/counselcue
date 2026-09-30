using System;

namespace AdieLab.AffectCounsel
{
    [Serializable]
    internal sealed class CounselingSessionRecord
    {
        public int schemaVersion = ResearchRecord.SchemaVersion;
        public string appVersion;
        public string caseId;
        public string lexiconVersion;
        public string sessionId;
        public string timestampUtc;
        public string trainingMode;
        public string sessionStage;
        public float sessionElapsedSeconds;
        public int turn;
        public string counselorUtterance;
        public string clientReply;
        public string skill;
        public string counselingMove;
        public int quality;
        public float alliance;
        public string deliveryAlignment;
        public bool deliveryEvidenceAvailable;
        public float relationalSafety;
        public float guardedness;
        public float willingnessToDisclose;
        public string culturalProfileId;
        public string deliveryFeedback;
        public float webcamSignalQuality;
        public float webcamMovement;
        public string auSource;
        public bool auTracking;
        public bool auCalibrated;
        public float au01;
        public float au02;
        public float au04;
        public float au06;
        public float au07;
        public float au12;
        public float au14;
        public float au15;
        public float au17;
        public float au23;
        public float au25;
        public float au26;
        public float au45;
        public float deliveryModifier;
        public string conversationEngine;

        // Schema v3: skill coding provenance and the model that produced the state change.
        public string skillCode;
        public string codebookVersion;
        public string codingSource;
        public string codingModel;
        public float codingConfidence;
        public string codingRationale;
        public string codingEvidence;
        public string lexiconCode;
        public int lexiconQuality;
        public string relationalModelVersion;
        public float safetyBefore;
        public float guardednessBefore;
        public float disclosureBefore;
        /// <summary>Gemini Live only: the counselor spoke over the client's reply (barge-in).</summary>
        public bool liveInterrupted;
        /// <summary>Counseling phase practised: intake, goal_setting, middle, termination.</summary>
        public string sessionPhase = "intake";
    }
}
