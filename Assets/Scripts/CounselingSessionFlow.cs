namespace AdieLab.AffectCounsel
{
    public enum TrainingMode
    {
        Practice,
        Evaluation
    }

    public enum TrainingSessionPhase
    {
        Briefing,
        Active,
        Paused,
        Debrief
    }

    public enum CounselingStage
    {
        Rapport,
        InitialExploration,
        EmotionalDeepening,
        CoreExploration,
        Consolidation,
        Closing
    }

    public static class CounselingSessionFlow
    {
        public const float StartingDurationSeconds = 900f;
        public const int StartingTargetTurns = 10;

        public static CounselingStage DetermineStage(int completedTurns, float willingnessToDisclose)
        {
            CounselingStage turnStage;
            if (completedTurns < 2) turnStage = CounselingStage.Rapport;
            else if (completedTurns < 4) turnStage = CounselingStage.InitialExploration;
            else if (completedTurns < 6) turnStage = CounselingStage.EmotionalDeepening;
            else if (completedTurns < 8) turnStage = CounselingStage.CoreExploration;
            else if (completedTurns < 10) turnStage = CounselingStage.Consolidation;
            else turnStage = CounselingStage.Closing;

            if (turnStage > CounselingStage.InitialExploration && willingnessToDisclose < 0.30f)
            {
                return CounselingStage.InitialExploration;
            }

            if (turnStage > CounselingStage.EmotionalDeepening && willingnessToDisclose < 0.45f)
            {
                return CounselingStage.EmotionalDeepening;
            }

            if (turnStage > CounselingStage.CoreExploration && willingnessToDisclose < 0.60f)
            {
                return CounselingStage.CoreExploration;
            }

            return turnStage;
        }

        public static string StageLabel(CounselingStage stage)
        {
            switch (stage)
            {
                case CounselingStage.Rapport:
                    return "관계 형성";
                case CounselingStage.InitialExploration:
                    return "초기 탐색";
                case CounselingStage.EmotionalDeepening:
                    return "감정 심화";
                case CounselingStage.CoreExploration:
                    return "핵심 탐색";
                case CounselingStage.Consolidation:
                    return "정리";
                default:
                    return "종결";
            }
        }

        public static string ModeLabel(TrainingMode mode) =>
            mode == TrainingMode.Practice ? "연습 모드" : "평가 모드";
    }

    public static class CounselingSubmissionGuard
    {
        public static bool CanCommit(
            int expectedSession,
            int currentSession,
            int expectedSubmission,
            int currentSubmission,
            bool sessionActive) =>
            sessionActive &&
            expectedSession == currentSession &&
            expectedSubmission == currentSubmission;
    }
}
