using System;

namespace AdieLab.AffectCounsel
{
    [Serializable]
    internal sealed class CounselingSessionRecord
    {
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
    }
}
