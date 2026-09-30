using System;
using System.Collections.Generic;
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
        [SerializeField] private CaseCatalog caseCatalog;
        [SerializeField] private ClientAvatarHost clientAvatar;
        [SerializeField] private Button[] caseButtons = Array.Empty<Button>();
        [SerializeField] private GameObject activeControlCard;
        [SerializeField] private GameObject briefingOverlay;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private GameObject debriefOverlay;
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text briefingCaseLabel;
        [SerializeField] private Text briefingBodyLabel;
        [SerializeField] private Text clientNameLabel;
        [SerializeField] private Image briefingPortrait;
        [SerializeField] private Text briefingPortraitCaption;
        [SerializeField] private float briefingBodyWidthWithPortrait = 680f;
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
        // Exchanges that preceded the current session when it is a scene replay, so a
        // replay of a replay still carries the original earlier conversation.
        private readonly List<CounselingTurnSnapshot> sessionPrefix = new List<CounselingTurnSnapshot>();
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
        private bool useEnglish;
        private int selectedCaseIndex;

        public bool CanSubmit => phase == TrainingSessionPhase.Active;
        public bool ShowLiveCoaching => mode != TrainingMode.Evaluation;
        public TrainingMode Mode => mode;
        public TrainingSessionPhase Phase => phase;
        public float ElapsedSeconds => sessionDurationSeconds - remainingSeconds;
        public string CurrentStageLabel => CounselingSessionFlow.StageLabel(stage);
        public CounselingCaseDefinition ActiveCase => caseDefinition;

        /// <summary>Re-renders case-dependent briefing text in the chosen UI language.</summary>
        public void SetEnglish(bool value)
        {
            useEnglish = value;
            ConfigureBriefing();
            if (caseCatalog != null) UpdateCaseButtons(selectedCaseIndex);
        }
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
            for (int i = 0; i < caseButtons.Length; i++)
            {
                int selectedIndex = i;
                caseButtons[i].onClick.AddListener(() => SelectCase(selectedIndex));
            }
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
            if (caseCatalog != null && caseCatalog.DefaultCase != null) SelectCase(0);
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

        public void SelectCase(int index)
        {
            if (phase != TrainingSessionPhase.Briefing || caseCatalog == null) return;
            CounselingCaseDefinition selected = caseCatalog.GetCase(index);
            if (selected == null) return;
            caseDefinition = selected;
            sessionController.SetCaseDefinition(selected);
            clientAvatar?.ApplyCase(selected);
            selectedCaseIndex = index;
            // Refresh the client's speech card too, so the line behind the briefing belongs
            // to the newly selected case rather than the previous one.
            sessionController.PrepareBriefing();
            ConfigureBriefing();
            UpdateCaseButtons(index);
        }

        private void BeginSession(TrainingMode selectedMode, int focusIndex, CounselingTurnSnapshot source)
        {
            List<CounselingTurnSnapshot> priorTurns = new List<CounselingTurnSnapshot>();
            if (source != null)
            {
                // Keep the exchanges that led up to the replayed scene so the AI client
                // remembers them; the list is cleared for the new session below.
                int firstSessionTurn = int.MaxValue;
                for (int i = 0; i < turns.Count; i++)
                {
                    if (turns[i] != null) firstSessionTurn = Mathf.Min(firstSessionTurn, turns[i].turn);
                }
                for (int i = 0; i < sessionPrefix.Count; i++)
                {
                    CounselingTurnSnapshot earlier = sessionPrefix[i];
                    if (earlier != null && earlier.turn < source.turn && earlier.turn < firstSessionTurn) priorTurns.Add(earlier);
                }
                for (int i = 0; i < turns.Count; i++)
                {
                    if (turns[i] != null && turns[i].turn < source.turn) priorTurns.Add(turns[i]);
                }
            }
            sessionPrefix.Clear();
            sessionPrefix.AddRange(priorTurns);
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
            else sessionController.BeginReplaySession(source, priorTurns);
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
            reflectionController.Present(caseDefinition, mode, turns, report, sessionController.SessionId);
            WriteSummary(timedOut);
        }

        private void ConfigureBriefing()
        {
            if (caseDefinition == null) return;
            string name = caseDefinition.LocalizedName(useEnglish);
            string profile = caseDefinition.LocalizedProfile(useEnglish);
            briefingCaseLabel.text = $"{caseDefinition.LocalizedTitle(useEnglish)} · {name}, {profile}";
            if (clientNameLabel != null) clientNameLabel.text = useEnglish ? $"CLIENT  ·  {name}, {profile}" : $"내담자  ·  {name}, {profile}";
            StringBuilder body = new StringBuilder();
            body.AppendLine(useEnglish ? "Situation" : "상황");
            body.AppendLine(caseDefinition.LocalizedConcern(useEnglish));
            body.AppendLine();
            body.AppendLine(useEnglish ? "Session goals" : "이번 세션의 목표");
            string[] objectives = caseDefinition.LocalizedObjectives(useEnglish) ?? Array.Empty<string>();
            for (int i = 0; i < objectives.Length; i++)
            {
                body.AppendLine($"{i + 1}. {objectives[i]}");
            }
            briefingBodyLabel.text = body.ToString();
            ApplyBriefingPortrait();
            Button[] focusButtons = { focusOneButton, focusTwoButton, focusThreeButton };
            for (int i = 0; i < focusButtons.Length; i++)
            {
                bool available = caseDefinition.FocusSkills != null && i < caseDefinition.FocusSkills.Length;
                focusButtons[i].gameObject.SetActive(available);
                if (available) focusButtons[i].GetComponentInChildren<Text>().text = FocusButtonLabel(caseDefinition.FocusSkills[i]);
            }
        }

        private string FocusButtonLabel(CounselingFocusSkill skill)
        {
            int minutes = Mathf.Max(1, Mathf.RoundToInt(caseDefinition.FocusedPracticeSeconds / 60f));
            if (!useEnglish) return $"{skill.label} 연습 · {minutes}분";
            string label;
            switch (skill.id)
            {
                case "emotion-reflection": label = "Emotion reflection"; break;
                case "open-question": label = "Open questions"; break;
                case "delivery-alignment": label = "Delivery alignment"; break;
                default: label = skill.label; break;
            }
            return $"{label} · {minutes} min";
        }

        private float briefingBodyFullWidth = -1f;

        private void ApplyBriefingPortrait()
        {
            if (briefingPortrait == null) return;
            Sprite portrait = caseDefinition == null ? null : caseDefinition.BriefingPortrait;
            bool visible = portrait != null;
            briefingPortrait.sprite = portrait;
            briefingPortrait.preserveAspect = true;
            briefingPortrait.gameObject.SetActive(visible);
            if (briefingPortraitCaption != null) briefingPortraitCaption.gameObject.SetActive(visible);

            // Narrow the case description only while an illustration is shown.
            RectTransform bodyRect = briefingBodyLabel.rectTransform;
            if (briefingBodyFullWidth < 0f) briefingBodyFullWidth = bodyRect.sizeDelta.x;
            float width = visible ? Mathf.Min(briefingBodyFullWidth, briefingBodyWidthWithPortrait) : briefingBodyFullWidth;
            bodyRect.sizeDelta = new Vector2(width, bodyRect.sizeDelta.y);
        }

        private void UpdateCaseButtons(int selectedIndex)
        {
            for (int i = 0; i < caseButtons.Length; i++)
            {
                if (caseButtons[i] == null) continue;
                Text label = caseButtons[i].GetComponentInChildren<Text>();
                CounselingCaseDefinition item = caseCatalog.GetCase(i);
                if (label != null && item != null) label.text = item.LocalizedTitle(useEnglish);
                ColorBlock colors = caseButtons[i].colors;
                colors.normalColor = i == selectedIndex ? new Color(0.20f, 0.48f, 0.37f, 1f) : Color.white;
                colors.highlightedColor = i == selectedIndex ? new Color(0.26f, 0.56f, 0.44f, 1f) : new Color(0.90f, 0.94f, 0.91f, 1f);
                caseButtons[i].colors = colors;
                if (label != null) label.color = i == selectedIndex ? Color.white : new Color(0.105f, 0.13f, 0.12f, 1f);
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
                appVersion = Application.version,
                sessionId = sessionController.SessionId,
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
            LocalJsonlLog.Append("counseling-session-summaries.jsonl", record);
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        [Serializable]
        private sealed class TrainingSessionSummaryRecord
        {
            public int schemaVersion = ResearchRecord.SchemaVersion;
            public string appVersion;
            public string sessionId;
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
