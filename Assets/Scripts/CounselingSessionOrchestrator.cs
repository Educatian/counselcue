using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingSessionOrchestrator : MonoBehaviour
    {
        [SerializeField] private CounselingSessionController sessionController;
        [SerializeField] private CounselingReflectionController reflectionController;
        [SerializeField] private CounselingCaseDefinition caseDefinition;
        [SerializeField] private GameObject activeControlCard;
        [SerializeField] private GameObject briefingOverlay;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private GameObject debriefOverlay;
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text briefingCaseLabel;
        [SerializeField] private Text briefingBodyLabel;
        [SerializeField] private Text debriefTitle;
        [SerializeField] private Button practiceStartButton;
        [SerializeField] private Button evaluationStartButton;
        [SerializeField] private Button focusOneButton;
        [SerializeField] private Button focusTwoButton;
        [SerializeField] private Button focusThreeButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button endButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseEndButton;
        [SerializeField] private Button returnButton;

        private readonly List<CounselingTurnSnapshot> turns = new List<CounselingTurnSnapshot>();
        private TrainingSessionPhase phase = TrainingSessionPhase.Briefing;
        private TrainingMode mode = TrainingMode.Practice;
        private CounselingStage stage = CounselingStage.Rapport;
        private ClientRelationalState latestState = ClientRelationalState.Initial;
        private CounselingTurnSnapshot replaySource;
        private float sessionDurationSeconds = CounselingSessionFlow.StartingDurationSeconds;
        private float remainingSeconds = CounselingSessionFlow.StartingDurationSeconds;
        private int displayedSecond = -1;
        private int targetTurns = CounselingSessionFlow.StartingTargetTurns;
        private int selectedFocusIndex = -1;
        private int alignedCount;
        private int mismatchCount;
        private int qualityTotal;
        private bool canceledSubmissionOnPause;

        public bool CanSubmit => phase == TrainingSessionPhase.Active;
        public bool ShowLiveCoaching => mode != TrainingMode.Evaluation;
        public TrainingMode Mode => mode;
        public TrainingSessionPhase Phase => phase;
        public float ElapsedSeconds => sessionDurationSeconds - remainingSeconds;
        public string CurrentStageLabel => CounselingSessionFlow.StageLabel(stage);
        public string CurrentFocusPrompt
        {
            get
            {
                CounselingFocusSkill focus = SelectedFocus;
                return focus == null ? string.Empty : $" · 집중목표: {focus.coachingPrompt}";
            }
        }

        private CounselingFocusSkill SelectedFocus
        {
            get
            {
                if (caseDefinition == null || caseDefinition.FocusSkills == null) return null;
                return selectedFocusIndex >= 0 && selectedFocusIndex < caseDefinition.FocusSkills.Length
                    ? caseDefinition.FocusSkills[selectedFocusIndex]
                    : null;
            }
        }

        private void Awake()
        {
            practiceStartButton.onClick.AddListener(BeginPracticeSession);
            evaluationStartButton.onClick.AddListener(BeginEvaluationSession);
            focusOneButton.onClick.AddListener(() => BeginFocusedPractice(0));
            focusTwoButton.onClick.AddListener(() => BeginFocusedPractice(1));
            focusThreeButton.onClick.AddListener(() => BeginFocusedPractice(2));
            pauseButton.onClick.AddListener(PauseSession);
            endButton.onClick.AddListener(EndSession);
            resumeButton.onClick.AddListener(ResumeSession);
            pauseEndButton.onClick.AddListener(EndSession);
            returnButton.onClick.AddListener(ReturnToBriefing);
        }

        private void Start()
        {
            ConfigureBriefing();
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

        public void RecordTurn(
            CounselingTurnSnapshot snapshot,
            ResponseAssessment assessment,
            RelationalTurnResult result)
        {
            if (phase != TrainingSessionPhase.Active) return;
            turns.Add(snapshot);
            qualityTotal += assessment.Quality;
            latestState = result.State;
            if (result.Alignment == DeliveryAlignment.Aligned) alignedCount++;
            if (result.Alignment == DeliveryAlignment.PossibleMismatch ||
                result.Alignment == DeliveryAlignment.RelationalOrderMismatch) mismatchCount++;
            stage = CounselingSessionFlow.DetermineStage(turns.Count, latestState.WillingnessToDisclose);
            UpdateHud();
            if ((mode == TrainingMode.SceneReplay || mode == TrainingMode.FocusedPractice) && turns.Count >= targetTurns)
            {
                FinishSession(false);
            }
        }

        public void BeginPracticeSession() => BeginSession(TrainingMode.Practice, -1, null);

        public void BeginEvaluationSession() => BeginSession(TrainingMode.Evaluation, -1, null);

        public void BeginFocusedPractice(int focusIndex) => BeginSession(TrainingMode.FocusedPractice, focusIndex, null);

        public void BeginSceneReplay(CounselingTurnSnapshot source) => BeginSession(TrainingMode.SceneReplay, -1, source);

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

        public void ReturnToBriefing() => ShowBriefing();

        private void BeginSession(TrainingMode selectedMode, int focusIndex, CounselingTurnSnapshot source)
        {
            mode = selectedMode;
            selectedFocusIndex = focusIndex;
            replaySource = source;
            phase = TrainingSessionPhase.Active;
            stage = CounselingStage.Rapport;
            latestState = source == null ? ClientRelationalState.Initial : source.stateBefore;
            targetTurns = ResolveTargetTurns();
            sessionDurationSeconds = ResolveStartingDuration();
            remainingSeconds = sessionDurationSeconds;
            displayedSecond = -1;
            turns.Clear();
            alignedCount = 0;
            mismatchCount = 0;
            qualityTotal = 0;
            canceledSubmissionOnPause = false;
            briefingOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(false);
            activeControlCard.SetActive(true);
            if (source == null) sessionController.BeginNewSession();
            else sessionController.BeginReplaySession(source);
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
            if (phase == TrainingSessionPhase.Debrief) return;
            sessionController.CancelPendingSubmission();
            phase = TrainingSessionPhase.Debrief;
            sessionController.SetInteractionEnabled(false);
            activeControlCard.SetActive(false);
            briefingOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(true);
            debriefTitle.text = timedOut
                ? "시간이 종료되었습니다"
                : mode == TrainingMode.SceneReplay ? "장면 재연습 결과" : "세션 성찰 및 재연습";
            string report = BuildDebriefReport();
            reflectionController.Present(caseDefinition, mode, turns, report);
            WriteSummary(timedOut);
        }

        private void ConfigureBriefing()
        {
            if (caseDefinition == null) return;
            briefingCaseLabel.text = $"{caseDefinition.CaseTitle} · {caseDefinition.ClientName}, {caseDefinition.ClientProfile}";
            StringBuilder body = new StringBuilder();
            body.AppendLine("상황");
            body.AppendLine(caseDefinition.PresentingConcern);
            body.AppendLine();
            body.AppendLine("이번 세션의 목표");
            for (int i = 0; i < caseDefinition.LearningObjectives.Length; i++)
            {
                body.AppendLine($"{i + 1}. {caseDefinition.LearningObjectives[i]}");
            }
            briefingBodyLabel.text = body.ToString();
            Button[] focusButtons = { focusOneButton, focusTwoButton, focusThreeButton };
            for (int i = 0; i < focusButtons.Length; i++)
            {
                bool available = caseDefinition.FocusSkills != null && i < caseDefinition.FocusSkills.Length;
                focusButtons[i].gameObject.SetActive(available);
                if (available) focusButtons[i].GetComponentInChildren<Text>().text = $"{caseDefinition.FocusSkills[i].label} 연습 · 3분";
            }
        }

        private void UpdateHud()
        {
            displayedSecond = Mathf.CeilToInt(remainingSeconds);
            timerLabel.text = $"{displayedSecond / 60:00}:{displayedSecond % 60:00}";
            string focus = SelectedFocus == null ? string.Empty : $" · {SelectedFocus.label}";
            stageLabel.text = $"{CounselingSessionFlow.ModeLabel(mode)}{focus} · {CurrentStageLabel} · {turns.Count}/{targetTurns}턴";
        }

        private string BuildDebriefReport()
        {
            float averageQuality = turns.Count == 0 ? 0f : (float)qualityTotal / turns.Count;
            string focus = SelectedFocus == null ? string.Empty : $" · {SelectedFocus.label}";
            string replay = replaySource == null || turns.Count == 0
                ? string.Empty
                : $" · 원래 {replaySource.quality}/3 → 재시도 {turns[0].quality}/3";
            return
                $"{CounselingSessionFlow.ModeLabel(mode)}{focus} · {FormatElapsed()} · {turns.Count}턴{replay}\n" +
                $"관계 궤적  안전 {Percent(latestState.Safety)} · 경계 {Percent(latestState.Guardedness)} · 공개 {Percent(latestState.WillingnessToDisclose)}\n" +
                $"전달 정합 {alignedCount}회 · 불일치 가능성 {mismatchCount}회 · 언어기술 평균 {averageQuality:0.0}/3\n" +
                "장면을 선택하고 먼저 자신의 판단을 남긴 뒤, 시스템 근거와 비교해 보세요.";
        }

        private string FormatElapsed()
        {
            int elapsed = Mathf.RoundToInt(ElapsedSeconds);
            return $"{elapsed / 60:00}:{elapsed % 60:00}";
        }

        private int ResolveTargetTurns()
        {
            if (mode == TrainingMode.SceneReplay) return 1;
            if (mode == TrainingMode.FocusedPractice && caseDefinition != null) return caseDefinition.FocusedTargetTurns;
            return CounselingSessionFlow.StartingTargetTurns;
        }

        private float ResolveStartingDuration()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith("--session-test-duration=", StringComparison.Ordinal)) continue;
                if (float.TryParse(argument.Substring("--session-test-duration=".Length), out float seconds)) return Mathf.Max(0.25f, seconds);
            }
            if (caseDefinition == null) return CounselingSessionFlow.StartingDurationSeconds;
            return mode == TrainingMode.FocusedPractice || mode == TrainingMode.SceneReplay
                ? caseDefinition.FocusedPracticeSeconds
                : caseDefinition.FullSessionSeconds;
        }

        private void WriteSummary(bool timedOut)
        {
            TrainingSessionSummaryRecord record = new TrainingSessionSummaryRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                caseId = caseDefinition == null ? "unknown" : caseDefinition.CaseId,
                trainingMode = mode.ToString(),
                focusSkill = SelectedFocus == null ? string.Empty : SelectedFocus.id,
                replaySourceTurn = replaySource == null ? 0 : replaySource.turn,
                timedOut = timedOut,
                elapsedSeconds = ElapsedSeconds,
                turnCount = turns.Count,
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

        [Serializable]
        private sealed class TrainingSessionSummaryRecord
        {
            public string timestampUtc;
            public string caseId;
            public string trainingMode;
            public string focusSkill;
            public int replaySourceTurn;
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
