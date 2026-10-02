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
        [SerializeField] private LiveVoiceController liveVoice;
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
        [SerializeField] private Text briefingMetaLabel;
        [SerializeField] private RectTransform turnProgressFill;
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
        [Header("Counseling phase (접수·초기 / 목표 설정 / 중반부 / 종결)")]
        [SerializeField] private Button[] phaseButtons = new Button[0];
        [SerializeField] private Text phaseNote;
        [SerializeField] private Button[] expressivityButtons = new Button[0];
        [SerializeField] private Text expressivityNote;

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
        private CounselingPhase counselingPhase = CounselingPhase.Intake;
        // The full session under review. Replaying one scene must not throw away the other
        // turns (reviewer: replaying turn 2 erased turns 3–11), so the debrief returns here
        // after a replay with the new attempt attached to that turn.
        private readonly List<CounselingTurnSnapshot> reviewTurns = new List<CounselingTurnSnapshot>();
        private string reviewSummary = string.Empty;
        private string reviewSessionId = string.Empty;
        private TrainingMode reviewMode = TrainingMode.Practice;
        private CounselingTurnSnapshot reviewTarget;

        public CounselingPhase CounselingPhase => counselingPhase;

        /// <summary>Chooses which phase of counseling to practise; applies to every case.</summary>
        public void SetCounselingPhase(CounselingPhase value)
        {
            if (phase != TrainingSessionPhase.Briefing) return;
            counselingPhase = value;
            if (caseDefinition != null)
            {
                caseDefinition.SetPhase(value);
                sessionController.SetCaseDefinition(caseDefinition);
                sessionController.PrepareBriefing();
            }
            ConfigureBriefing();
        }

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
            for (int i = 0; phaseButtons != null && i < phaseButtons.Length && i < CounselingPhaseLibrary.All.Length; i++)
            {
                CounselingPhase option = CounselingPhaseLibrary.All[i];
                if (phaseButtons[i] != null) phaseButtons[i].onClick.AddListener(() => SetCounselingPhase(option));
            }
            for (int i = 0; expressivityButtons != null && i < expressivityButtons.Length && i < 3; i++)
            {
                Expressivity option = (Expressivity)i;
                if (expressivityButtons[i] != null) expressivityButtons[i].onClick.AddListener(() => ExpressionSettings.Level = option);
            }
            ExpressionSettings.Changed += RefreshExpressivityButtons;
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

        public void BeginSceneReplay(CounselingTurnSnapshot source)
        {
            reviewTarget = source;
            BeginSession(TrainingMode.SceneReplay, -1, source);
        }

        public void PauseSession()
        {
            if (phase != TrainingSessionPhase.Active) return;
            canceledSubmissionOnPause = sessionController.CancelPendingSubmission();
            phase = TrainingSessionPhase.Paused;
            pauseOverlay.SetActive(true);
            liveVoice?.SetMuted(true);
            sessionController.SetInteractionEnabled(false);
        }

        public void ResumeSession()
        {
            if (phase != TrainingSessionPhase.Paused) return;
            phase = TrainingSessionPhase.Active;
            pauseOverlay.SetActive(false);
            liveVoice?.SetMuted(false);
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
            selected.SetPhase(counselingPhase);
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
                // A scene picked from the preserved review comes from that full session.
                bool fromReview = reviewTurns.Contains(source);
                List<CounselingTurnSnapshot> sourceTurns = fromReview ? reviewTurns : turns;
                int firstSessionTurn = int.MaxValue;
                for (int i = 0; i < sourceTurns.Count; i++)
                {
                    if (sourceTurns[i] != null) firstSessionTurn = Mathf.Min(firstSessionTurn, sourceTurns[i].turn);
                }
                for (int i = 0; !fromReview && i < sessionPrefix.Count; i++)
                {
                    CounselingTurnSnapshot earlier = sessionPrefix[i];
                    if (earlier != null && earlier.turn < source.turn && earlier.turn < firstSessionTurn) priorTurns.Add(earlier);
                }
                for (int i = 0; i < sourceTurns.Count; i++)
                {
                    if (sourceTurns[i] != null && sourceTurns[i].turn < source.turn) priorTurns.Add(sourceTurns[i]);
                }
            }
            sessionPrefix.Clear();
            sessionPrefix.AddRange(priorTurns);
            mode = selectedMode;
            selectedFocusIndex = focusIndex;
            replaySource = source;
            phase = TrainingSessionPhase.Active;
            stage = CounselingStage.Rapport;
            latestState = source == null ? (caseDefinition != null ? caseDefinition.StartingState : ClientRelationalState.Initial) : source.stateBefore;
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
            // Live voice runs full and focused sessions; a scene replay stays in text so the
            // earlier exchanges it rebuilds are exactly what the client remembers.
            liveVoice?.StopSession();
            if (source == null && liveVoice != null && liveVoice.Requested && caseDefinition != null)
            {
                string personaKey = string.IsNullOrWhiteSpace(caseDefinition.PersonaPromptKey) ? caseDefinition.CaseId : caseDefinition.PersonaPromptKey;
                liveVoice.StartSession(sessionController.SessionId, personaKey,
                    caseDefinition.InitialClientLine, caseDefinition.StartingState, caseDefinition.PhaseKey);
            }
            UpdateHud();
        }

        private void ShowBriefing()
        {
            liveVoice?.StopSession();
            phase = TrainingSessionPhase.Briefing;
            activeControlCard.SetActive(false);
            pauseOverlay.SetActive(false);
            debriefOverlay.SetActive(false);
            briefingOverlay.SetActive(true);
            sessionController.PrepareBriefing();
        }

        private void FinishSession(bool timedOut)
        {
            liveVoice?.StopSession();
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
            WriteSummary(timedOut);
            if (mode == TrainingMode.SceneReplay && reviewTarget != null && reviewTurns.Count > 0)
            {
                if (turns.Count > 0)
                {
                    CounselingTurnSnapshot attempt = turns[0];
                    reviewTarget.retryUtterance = attempt.counselorUtterance;
                    reviewTarget.retryReply = attempt.clientReply;
                    reviewTarget.retrySkill = attempt.skill;
                    reviewTarget.retryQuality = attempt.quality;
                }
                debriefTitle.text = "장면 재연습 결과";
                reflectionController.Present(caseDefinition, reviewMode, reviewTurns, reviewSummary, reviewSessionId, reviewTurns.IndexOf(reviewTarget));
                return;
            }
            reviewTurns.Clear();
            reviewTurns.AddRange(turns);
            reviewSummary = report;
            reviewSessionId = sessionController.SessionId;
            reviewMode = mode;
            reviewTarget = null;
            reflectionController.Present(caseDefinition, mode, turns, report, sessionController.SessionId);
        }

        private void ConfigureBriefing()
        {
            if (caseDefinition == null) return;
            string name = caseDefinition.LocalizedName(useEnglish);
            string profile = caseDefinition.LocalizedProfile(useEnglish);
            if (briefingMetaLabel != null)
            {
                briefingCaseLabel.text = caseDefinition.LocalizedTitle(useEnglish);
                briefingMetaLabel.text = $"{name}  ·  {profile}";
            }
            else
            {
                briefingCaseLabel.text = $"{caseDefinition.LocalizedTitle(useEnglish)} · {name}, {profile}";
            }
            if (clientNameLabel != null) clientNameLabel.text = useEnglish ? $"CLIENT  ·  {name}, {profile}" : $"내담자  ·  {name}, {profile}";
            StringBuilder body = new StringBuilder();
            body.AppendLine(Heading((useEnglish ? "SITUATION  ·  " : "상황  ·  ") + caseDefinition.LocalizedSessionLabel(useEnglish)));
            body.AppendLine(caseDefinition.LocalizedSituation(useEnglish));
            body.AppendLine();
            body.AppendLine(Heading(useEnglish ? "SESSION GOALS" : "이번 세션의 목표"));
            string[] objectives = caseDefinition.LocalizedObjectives(useEnglish) ?? Array.Empty<string>();
            for (int i = 0; i < objectives.Length; i++)
            {
                body.AppendLine($"{i + 1}. {objectives[i]}");
            }
            briefingBodyLabel.text = body.ToString();
            ApplyBriefingPortrait();
            RefreshPhaseButtons();
            RefreshExpressivityButtons();
            Button[] focusButtons = { focusOneButton, focusTwoButton, focusThreeButton };
            for (int i = 0; i < focusButtons.Length; i++)
            {
                bool available = caseDefinition.FocusSkills != null && i < caseDefinition.FocusSkills.Length;
                focusButtons[i].gameObject.SetActive(available);
                if (available) focusButtons[i].GetComponentInChildren<Text>().text = FocusButtonLabel(caseDefinition.FocusSkills[i]);
            }
        }

        private void OnDestroy() => ExpressionSettings.Changed -= RefreshExpressivityButtons;

        /// <summary>How strongly the AI client shows emotion (voice and face); fixed by study URL parameters.</summary>
        private void RefreshExpressivityButtons()
        {
            if (expressivityButtons == null) return;
            Expressivity level = ExpressionSettings.Level;
            bool locked = ExpressionSettings.LockedByStudy;
            for (int i = 0; i < expressivityButtons.Length && i < 3; i++)
            {
                if (expressivityButtons[i] == null) continue;
                Expressivity option = (Expressivity)i;
                UiTheme.SetChoice(expressivityButtons[i], option == level, false);
                expressivityButtons[i].interactable = !locked || option == level;
                Text label = expressivityButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.text = useEnglish ? ExpressionSettings.LabelEn(option) : ExpressionSettings.LabelKo(option);
            }
            if (expressivityNote != null)
            {
                string note = useEnglish ? ExpressionSettings.NoteEn(level) : ExpressionSettings.NoteKo(level);
                expressivityNote.text = locked ? (useEnglish ? "Fixed by the study settings · " : "연구 설정으로 고정됨 · ") + note : note;
            }
        }

        private void RefreshPhaseButtons()
        {
            if (phaseButtons == null) return;
            for (int i = 0; i < phaseButtons.Length && i < CounselingPhaseLibrary.All.Length; i++)
            {
                if (phaseButtons[i] == null) continue;
                CounselingPhase option = CounselingPhaseLibrary.All[i];
                UiTheme.SetChoice(phaseButtons[i], option == counselingPhase, false);
                Text label = phaseButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.text = useEnglish ? CounselingPhaseLibrary.LabelEn(option) : CounselingPhaseLibrary.LabelKo(option);
            }
            if (phaseNote != null)
                phaseNote.text = useEnglish ? CounselingPhaseLibrary.DescriptionEn(counselingPhase) : CounselingPhaseLibrary.DescriptionKo(counselingPhase);
        }

        private static string Heading(string value) =>
            $"<size=13>{UiTheme.Colorize(value, UiTheme.CeladonDeepHex)}</size>";

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
                UiTheme.SetChoice(caseButtons[i], i == selectedIndex, true);
                if (label == null || item == null) continue;
                string profile = item.LocalizedProfile(useEnglish) ?? string.Empty;
                int separator = profile.IndexOf(" · ", StringComparison.Ordinal);
                string age = separator > 0 ? profile.Substring(0, separator) : profile;
                label.text = $"{item.LocalizedTitle(useEnglish)}\n<size=13>{item.LocalizedName(useEnglish)} · {age}</size>";
            }
        }

        private void UpdateHud()
        {
            displayedSecond = Mathf.CeilToInt(remainingSeconds);
            timerLabel.text = $"{displayedSecond / 60:00}:{displayedSecond % 60:00}";
            timerLabel.color = displayedSecond <= 60 ? UiTheme.TimerWarning : UiTheme.Amber;
            if (turnProgressFill != null)
            {
                Vector2 max = turnProgressFill.anchorMax;
                max.x = targetTurns <= 0 ? 0f : Mathf.Clamp01((float)turns.Count / targetTurns);
                turnProgressFill.anchorMax = max;
                turnProgressFill.gameObject.SetActive(turns.Count > 0);
            }
            string focus = SelectedFocus == null ? string.Empty : $" · {SelectedFocus.label}";
            string sessionPhase = caseDefinition == null || caseDefinition.Phase == CounselingPhase.Intake
                ? string.Empty
                : $" · {CounselingPhaseLibrary.LabelKo(caseDefinition.Phase)}";
            stageLabel.text = $"{CounselingSessionFlow.ModeLabel(mode)}{focus}{sessionPhase} · {CurrentStageLabel} · {turns.Count}/{targetTurns}턴";
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
                PatternSummary() +
                "장면을 선택하고 먼저 자신의 판단을 남긴 뒤, 시스템 근거와 비교해 보세요.";
        }

        private string PatternSummary()
        {
            List<string> codes = new List<string>();
            List<int> qualities = new List<int>();
            foreach (CounselingTurnSnapshot turn in turns)
            {
                if (turn == null) continue;
                codes.Add(turn.skillCode);
                qualities.Add(turn.quality);
            }
            string summary = ResponsePatternProfile.Build(codes, qualities).ToKoreanSummary();
            return summary.Length == 0 ? string.Empty : $"<color=#9FD0BA>{summary}</color>\n";
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
                sessionPhase = caseDefinition == null ? "intake" : caseDefinition.PhaseKey,
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
            public string sessionPhase;
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
