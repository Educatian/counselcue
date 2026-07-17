using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingSessionOrchestrator : MonoBehaviour
    {
        [SerializeField] private CounselingSessionController sessionController;
        [SerializeField] private GameObject activeControlCard;
        [SerializeField] private GameObject briefingOverlay;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private GameObject debriefOverlay;
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text debriefTitle;
        [SerializeField] private Text debriefReport;
        [SerializeField] private Button practiceStartButton;
        [SerializeField] private Button evaluationStartButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button endButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseEndButton;
        [SerializeField] private Button returnButton;

        private TrainingSessionPhase phase = TrainingSessionPhase.Briefing;
        private TrainingMode mode = TrainingMode.Practice;
        private CounselingStage stage = CounselingStage.Rapport;
        private ClientRelationalState latestState = ClientRelationalState.Initial;
        private float sessionDurationSeconds = CounselingSessionFlow.StartingDurationSeconds;
        private float remainingSeconds = CounselingSessionFlow.StartingDurationSeconds;
        private int displayedSecond = -1;
        private int turnCount;
        private int alignedCount;
        private int mismatchCount;
        private int qualityTotal;
        private bool canceledSubmissionOnPause;

        public bool CanSubmit => phase == TrainingSessionPhase.Active;
        public bool ShowLiveCoaching => mode == TrainingMode.Practice;
        public bool IsActive => phase == TrainingSessionPhase.Active;
        public TrainingMode Mode => mode;
        public TrainingSessionPhase Phase => phase;
        public float ElapsedSeconds => sessionDurationSeconds - remainingSeconds;
        public string CurrentStageLabel => CounselingSessionFlow.StageLabel(stage);

        private void Awake()
        {
            practiceStartButton.onClick.AddListener(BeginPracticeSession);
            evaluationStartButton.onClick.AddListener(BeginEvaluationSession);
            pauseButton.onClick.AddListener(PauseSession);
            endButton.onClick.AddListener(EndSession);
            resumeButton.onClick.AddListener(ResumeSession);
            pauseEndButton.onClick.AddListener(EndSession);
            returnButton.onClick.AddListener(ReturnToBriefing);
        }

        private void Start()
        {
            ShowBriefing();
        }

        private void Update()
        {
            if (phase != TrainingSessionPhase.Active) return;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.unscaledDeltaTime);
            int currentSecond = Mathf.CeilToInt(remainingSeconds);
            if (currentSecond != displayedSecond) UpdateHud();
            if (remainingSeconds <= 0f) FinishSession(true);
        }

        public void RecordTurn(ResponseAssessment assessment, RelationalTurnResult result)
        {
            if (phase != TrainingSessionPhase.Active) return;
            turnCount++;
            qualityTotal += assessment.Quality;
            latestState = result.State;
            if (result.Alignment == DeliveryAlignment.Aligned) alignedCount++;
            if (result.Alignment == DeliveryAlignment.PossibleMismatch ||
                result.Alignment == DeliveryAlignment.RelationalOrderMismatch) mismatchCount++;
            stage = CounselingSessionFlow.DetermineStage(turnCount, latestState.WillingnessToDisclose);
            UpdateHud();
        }

        public void BeginPracticeSession() => BeginSession(TrainingMode.Practice);

        public void BeginEvaluationSession() => BeginSession(TrainingMode.Evaluation);

        public void PauseSession()
        {
            if (phase != TrainingSessionPhase.Active) return;
            canceledSubmissionOnPause = sessionController.CancelPendingSubmission();
            phase = TrainingSessionPhase.Paused;
            pauseOverlay.SetActive(true);
            sessionController.SetInteractionEnabled(false);
        }

        public void ResumeSession()
        {
            if (phase != TrainingSessionPhase.Paused) return;
            phase = TrainingSessionPhase.Active;
            pauseOverlay.SetActive(false);
            sessionController.SetInteractionEnabled(true);
            if (canceledSubmissionOnPause) sessionController.ShowCanceledSubmissionMessage();
            canceledSubmissionOnPause = false;
        }

        public void EndSession()
        {
            if (phase != TrainingSessionPhase.Active && phase != TrainingSessionPhase.Paused) return;
            sessionController.CancelPendingSubmission();
            FinishSession(false);
        }

        public void ReturnToBriefing()
        {
            ShowBriefing();
        }

        private void BeginSession(TrainingMode selectedMode)
        {
            mode = selectedMode;
            phase = TrainingSessionPhase.Active;
            stage = CounselingStage.Rapport;
            latestState = ClientRelationalState.Initial;
            sessionDurationSeconds = ResolveStartingDuration();
            remainingSeconds = sessionDurationSeconds;
            displayedSecond = -1;
            turnCount = 0;
            alignedCount = 0;
            mismatchCount = 0;
            qualityTotal = 0;
            canceledSubmissionOnPause = false;
            briefingOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(false);
            activeControlCard.SetActive(true);
            sessionController.BeginNewSession();
            UpdateHud();
        }

        private void ShowBriefing()
        {
            phase = TrainingSessionPhase.Briefing;
            activeControlCard.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(false);
            briefingOverlay.SetActive(true);
            sessionController.PrepareBriefing();
        }

        private void FinishSession(bool timedOut)
        {
            sessionController.CancelPendingSubmission();
            phase = TrainingSessionPhase.Debrief;
            sessionController.SetInteractionEnabled(false);
            activeControlCard.SetActive(false);
            briefingOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(true);
            debriefTitle.text = timedOut ? "시간이 종료되었습니다" : "첫 회기 디브리핑";
            debriefReport.text = BuildDebriefReport();
            WriteSummary(timedOut);
        }

        private void UpdateHud()
        {
            displayedSecond = Mathf.CeilToInt(remainingSeconds);
            int minutes = displayedSecond / 60;
            int seconds = displayedSecond % 60;
            timerLabel.text = $"{minutes:00}:{seconds:00}";
            stageLabel.text = $"{CounselingSessionFlow.ModeLabel(mode)} · {CurrentStageLabel} · {turnCount}/{CounselingSessionFlow.StartingTargetTurns}턴";
        }

        private string BuildDebriefReport()
        {
            float averageQuality = turnCount == 0 ? 0f : (float)qualityTotal / turnCount;
            string completion = turnCount >= CounselingSessionFlow.StartingTargetTurns ? "목표 턴 완료" : "조기 종료";
            string strength = alignedCount > mismatchCount
                ? "언어 기술과 전달 단서가 조화를 이룬 순간이 더 많았습니다."
                : "세션 구조를 유지하며 내담자의 반응을 계속 관찰했습니다.";
            string nextStep = mismatchCount > 0
                ? "불일치가 나타난 장면에서 표정에 힘을 빼고 응답 공간을 확보해 보세요."
                : "다음 세션에서는 감정 반영 뒤 의미 탐색으로 더 깊게 이어가 보세요.";
            return
                $"{CounselingSessionFlow.ModeLabel(mode)}  ·  {completion}\n" +
                $"진행 시간  {FormatElapsed()}   |   상담자 응답  {turnCount}턴   |   최종 단계  {CurrentStageLabel}\n\n" +
                $"관계 궤적   안전 {Percent(latestState.Safety)}   ·   경계 {Percent(latestState.Guardedness)}   ·   공개 {Percent(latestState.WillingnessToDisclose)}\n" +
                $"전달 정합   {alignedCount}회   ·   불일치 가능성 {mismatchCount}회   ·   언어기술 평균 {averageQuality:0.0}/3\n\n" +
                $"잘된 점\n{strength}\n\n다음 연습\n{nextStep}\n\n" +
                "※ 이 결과는 훈련용 시뮬레이션 피드백이며 상담역량 판정이나 임상평가가 아닙니다.";
        }

        private string FormatElapsed()
        {
            int elapsed = Mathf.RoundToInt(ElapsedSeconds);
            return $"{elapsed / 60:00}:{elapsed % 60:00}";
        }

        private void WriteSummary(bool timedOut)
        {
            TrainingSessionSummaryRecord record = new TrainingSessionSummaryRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                trainingMode = mode.ToString(),
                timedOut = timedOut,
                elapsedSeconds = ElapsedSeconds,
                turnCount = turnCount,
                finalStage = stage.ToString(),
                alignedCount = alignedCount,
                mismatchCount = mismatchCount,
                relationalSafety = latestState.Safety,
                guardedness = latestState.Guardedness,
                willingnessToDisclose = latestState.WillingnessToDisclose
            };
            File.AppendAllText(Path.Combine(Application.persistentDataPath, "counseling-session-summaries.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine);
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        private static float ResolveStartingDuration()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith("--session-test-duration=", StringComparison.Ordinal)) continue;
                if (float.TryParse(argument.Substring("--session-test-duration=".Length), out float seconds))
                {
                    return Mathf.Clamp(seconds, 0.25f, CounselingSessionFlow.StartingDurationSeconds);
                }
            }

            return CounselingSessionFlow.StartingDurationSeconds;
        }

        [Serializable]
        private sealed class TrainingSessionSummaryRecord
        {
            public string timestampUtc;
            public string trainingMode;
            public bool timedOut;
            public float elapsedSeconds;
            public int turnCount;
            public string finalStage;
            public int alignedCount;
            public int mismatchCount;
            public float relationalSafety;
            public float guardedness;
            public float willingnessToDisclose;
        }
    }
}
