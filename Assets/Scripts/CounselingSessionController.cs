using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingSessionController : MonoBehaviour
    {
        private const string InitialClientLine = "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.\n제가 너무 약한 사람인가 싶기도 하고요.";

        [SerializeField] private ClientAvatarHost client;
        [SerializeField] private WebcamSignalMonitor webcam;
        [SerializeField] private FacialActionUnitMonitor actionUnits;
        [SerializeField] private GptRealtimeConversationEngine realtimeEngine;
        [SerializeField] private WebNpcConversationEngine webNpcEngine;
        [SerializeField] private CounselCueWebBridge webBridge;
        [SerializeField] private CounselingSessionOrchestrator sessionOrchestrator;
        [SerializeField] private CounselingCaseDefinition caseDefinition;
        [SerializeField] private InputField counselorInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text clientLine;
        [SerializeField] private Text sessionStatus;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private Text allianceLabel;
        [SerializeField] private RelationalMeterHud relationalMeters;
        [SerializeField] private LiveVoiceController liveVoice;

        private readonly string[] supportiveReplies =
        {
            "제가 요즘 계속 긴장한 채로 지냈던 것 같아요. 누군가에게 말하니 조금 정리가 되는 느낌이에요.",
            "그 말을 들으니 제가 너무 예민한 사람은 아닌 것 같아서 조금 안심돼요.",
            "회사에 들어가는 순간부터 가슴이 답답해져요. 특히 팀장님과 이야기할 때 더 심해지고요.",
            "지난주 회의에서 팀장님이 사람들 앞에서 제 실수를 지적했어요. 그 뒤로 시선이 신경 쓰여요.",
            "또 틀리면 어쩌나 싶어서 작은 일도 계속 확인해요. 결국 제가 부족한 탓 같고요.",
            "요즘은 출근 전부터 퇴사해야 하나 생각해요. 그렇지만 그만두는 것도 겁이 나요.",
            "가족에게는 걱정시킬까 봐 말하지 못했어요. 혼자 버티는 게 점점 힘들어요.",
            "제가 원하는 건 당장 답을 정하는 것보다 안전하게 일할 수 있다는 느낌인 것 같아요.",
            "지금 정리해 주신 내용을 들으니 제가 무엇 때문에 힘든지 조금 더 선명해졌어요.",
            "다음에는 불안이 올라오는 순간을 더 살펴보고, 제가 할 수 있는 작은 선택도 찾아보고 싶어요."
        };

        private readonly string[] guardedReplies =
        {
            "글쎄요… 그냥 제가 알아서 해야 하는 문제 같기도 해요.",
            "그렇게 간단히 해결될 문제였으면 이미 했을 것 같아요.",
            "무슨 말을 해야 할지 잘 모르겠어요.",
            "그 얘기는 아직 자세히 하고 싶지 않아요.",
            "결국 제가 잘못한 것 같아서 말해도 달라질 게 있나 싶어요.",
            "퇴사 얘기까지는 하고 싶지 않아요. 너무 앞서가는 것 같아요.",
            "가족에게는 말하고 싶지 않아요. 걱정만 더할 테니까요.",
            "제가 무엇을 원하는지는 잘 모르겠어요. 그냥 덜 힘들었으면 좋겠어요.",
            "정리가 됐는지는 모르겠어요. 아직 조금 부담스러워요.",
            "오늘은 여기까지만 이야기하고 싶어요."
        };

        private string sessionId;
        private ClientRelationalState relationalState = ClientRelationalState.Initial;
        private readonly CulturalInteractionProfile culturalProfile = CulturalInteractionProfile.KoreanCounselingPilot;
        private int turn;
        private bool isSubmitting;
        private string conversationEngine = "local";
        private int sessionGeneration;
        private int submissionGeneration;
        private string committedClientLine = string.Empty;
        private string currentEmotion = "anxious";
        // Expression pipeline: the latest plan (text mode) or affect report (live mode).
        private float currentIntensity = 0.5f;
        private string currentExpressionPolicy = string.Empty;
        private string liveAffect = string.Empty;
        private JevAnalysis lastAnalysis = JevAnalysis.Failure("none");
        private float liveIntensity = -1f;
        private readonly Queue<PendingLiveTurn> pendingLiveTurns = new Queue<PendingLiveTurn>();

        public bool IsSubmitting => isSubmitting;
        public string SessionId => sessionId;

        public void SetCaseDefinition(CounselingCaseDefinition definition)
        {
            caseDefinition = definition;
            webNpcEngine?.ConfigureCase(definition);
            client?.ApplyCase(definition);
            UpdateLabels();
        }

        public void SetCounselorInput(string value)
        {
            CounselorBodyController.NotifyTyping();
            if (counselorInput != null) counselorInput.text = value ?? string.Empty;
        }

        private void Awake()
        {
            sendButton.onClick.AddListener(Submit);
            // Editor/desktop typing (the WebGL dock reports through SetCounselorInput).
            counselorInput.onValueChanged.AddListener(value => { if (!string.IsNullOrEmpty(value)) CounselorBodyController.NotifyTyping(); });
            counselorInput.onEndEdit.AddListener(value =>
            {
                if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrWhiteSpace(value)) Submit();
            });
        }

        public void PrepareBriefing()
        {
            InvalidateSession();
            SetInteractionEnabled(false);
            SetClientLine(InitialLine);
            client.SetAffect(ClientAffect.Anxious, true);
            feedbackLabel.text = "세션을 시작하면 상담자의 언어 기술과 비언어 전달을 함께 관찰합니다.";
            UpdateLabels();
        }

        public void BeginNewSession()
        {
            InvalidateSession();
            sessionId = Guid.NewGuid().ToString("N");
            relationalState = caseDefinition != null ? caseDefinition.StartingState : ClientRelationalState.Initial;
            if (webNpcEngine != null) webNpcEngine.PhaseKey = caseDefinition != null ? caseDefinition.PhaseKey : "intake";
            client.SetRelationalState(relationalState);
            turn = 0;
            isSubmitting = false;
            conversationEngine = "local";
            counselorInput.text = string.Empty;
            webNpcEngine?.ResetConversation(InitialLine);
            SetClientLine(InitialLine);
            client.SetAffect(ClientAffect.Anxious, true);
            feedbackLabel.text = sessionOrchestrator.ShowLiveCoaching
                ? "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요."
                : "평가 모드 · 세션 종료 후 전달 피드백을 확인합니다.";
            UpdateLabels();
            SetInteractionEnabled(true);
        }

        public void BeginReplaySession(CounselingTurnSnapshot source, IReadOnlyList<CounselingTurnSnapshot> priorTurns = null)
        {
            InvalidateSession();
            sessionId = Guid.NewGuid().ToString("N");
            relationalState = source.stateBefore;
            if (webNpcEngine != null) webNpcEngine.PhaseKey = caseDefinition != null ? caseDefinition.PhaseKey : "intake";
            turn = Mathf.Max(0, source.turn - 1);
            isSubmitting = false;
            conversationEngine = "local";
            counselorInput.text = string.Empty;
            webNpcEngine?.ResetConversation(InitialLine, priorTurns);
            SetClientLine(string.IsNullOrWhiteSpace(source.clientPrompt) ? InitialLine : source.clientPrompt);
            client.SetAffect(relationalState.Guardedness > 0.65f ? ClientAffect.Guarded : ClientAffect.Anxious, true);
            feedbackLabel.text = $"선택 장면 재연습 · 원래 응답: {source.skill} · 다른 전달을 시도해 보세요.";
            UpdateLabels();
            SetInteractionEnabled(true);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            counselorInput.interactable = enabled;
            sendButton.interactable = enabled && !isSubmitting;
            if (enabled) counselorInput.ActivateInputField();
        }

        public bool CancelPendingSubmission()
        {
            bool hadPendingSubmission = isSubmitting;
            submissionGeneration++;
            realtimeEngine?.CancelPendingRequest();
            isSubmitting = false;
            return hadPendingSubmission;
        }

        public void ShowCanceledSubmissionMessage()
        {
            feedbackLabel.text = "이전 응답 요청이 취소되었습니다. 내용을 확인한 뒤 다시 보내세요.";
        }

        public void Submit()
        {
            CounselorBodyController.NotifySubmitted();
            if (sessionOrchestrator == null || !sessionOrchestrator.CanSubmit) return;
            string utterance = counselorInput.text.Trim();
            if (utterance.Length == 0) return;
            if (liveVoice != null && liveVoice.Active)
            {
                // Typed turn inside a live voice session: the client answers by voice and the
                // exchange comes back through SubmitLiveTurn with both transcripts.
                liveVoice.SendText(utterance);
                counselorInput.text = string.Empty;
                webBridge?.ClearInput();
                return;
            }
            if (isSubmitting) return;
            ProcessTurnAsync(utterance, null, false);
        }

        /// <summary>A completed exchange from a Gemini Live voice session (already spoken aloud).</summary>
        public void SubmitLiveTurn(string counselorText, string clientText, bool interrupted)
        {
            if (sessionOrchestrator == null || !sessionOrchestrator.CanSubmit) return;
            string counselor = (counselorText ?? string.Empty).Trim();
            string reply = (clientText ?? string.Empty).Trim();
            if (counselor.Length == 0)
            {
                // The client spoke without a counselor turn (e.g. after a long silence): show it,
                // but there is nothing to code.
                if (reply.Length > 0) SetClientLine(reply);
                return;
            }
            pendingLiveTurns.Enqueue(new PendingLiveTurn { counselor = counselor, client = reply, interrupted = interrupted });
            PumpLiveTurns();
        }

        public void ShowLivePartial(bool isClient, string text)
        {
            if (sessionOrchestrator == null || !sessionOrchestrator.CanSubmit) return;
            if (isClient) clientLine.text = text;
            else feedbackLabel.text = $"<color=#9FD0BA>듣는 중</color> · {text}";
        }

        public void OnLiveVoiceFailed(string reason)
        {
            string message = reason == "error:mic"
                ? "마이크 권한이 없어 텍스트 대화로 계속합니다."
                : reason == "error:unsupported"
                    ? "이 브라우저는 실시간 음성을 지원하지 않아 텍스트 대화로 계속합니다."
                    : "실시간 음성 연결이 끊겨 텍스트 대화로 전환했습니다.";
            feedbackLabel.text = message;
            SetInteractionEnabled(sessionOrchestrator != null && sessionOrchestrator.CanSubmit);
        }

        /// <summary>Emotion used for the avatar while a live reply plays.</summary>
        public string CurrentClientEmotion => currentEmotion;
        public float CurrentClientIntensity => currentIntensity;

        /// <summary>Gemini Live reported the client's feeling (set_client_affect); the face follows at once.</summary>
        public void ApplyLiveAffect(string affect, float intensity)
        {
            liveAffect = (affect ?? string.Empty).Trim().ToLowerInvariant();
            liveIntensity = Mathf.Clamp01(intensity);
            currentEmotion = liveAffect.Length > 0 ? liveAffect : currentEmotion;
            currentIntensity = liveIntensity;
            client?.SetAffect(ClientAvatarController.AffectForEmotion(currentEmotion));
            client?.SetAffectIntensity(currentIntensity);
        }

        private void PumpLiveTurns()
        {
            if (isSubmitting || pendingLiveTurns.Count == 0) return;
            PendingLiveTurn next = pendingLiveTurns.Dequeue();
            ProcessTurnAsync(next.counselor, next.client, next.interrupted);
        }

        private async void ProcessTurnAsync(string utterance, string liveReply, bool liveInterrupted)
        {
            bool isLive = liveReply != null;
            int expectedSession = sessionGeneration;
            int expectedSubmission = ++submissionGeneration;
            isSubmitting = true;
            sendButton.interactable = false;
            client?.Acknowledge();
            try
            {
                string clientPrompt = committedClientLine;
                ResponseAssessment lexiconAssessment = CounselingResponseEvaluator.Evaluate(utterance);
                ResponseAssessment assessment = lexiconAssessment;
                SkillCodingReply coding = SkillCodingReply.Failure("not requested");
                string codingSource = "lexicon";
                // Jev (fast, typed) and the LLM coder (slower, explains) run side by side.
                Task<SkillCodingReply> codingTask = webNpcEngine != null && webNpcEngine.CoderEnabled
                    ? webNpcEngine.RequestCodingAsync(sessionId, turn + 1, sessionOrchestrator.CurrentStageLabel, utterance, clientPrompt)
                    : null;
                Task<JevAnalysis> analysisTask = webNpcEngine != null && AnalysisSettings.Mode != AnalysisMode.Off
                    ? webNpcEngine.RequestAnalysisAsync(sessionId, utterance, clientPrompt, liveReply)
                    : null;
                lastAnalysis = JevAnalysis.Failure("not requested");
                if (analysisTask != null)
                {
                    if (!isLive && codingTask == null) feedbackLabel.text = "응답을 분석하는 중…";
                    lastAnalysis = await analysisTask;
                    if (!IsCurrentSubmission(expectedSession, expectedSubmission)) return;
                    if (lastAnalysis.succeeded && sessionOrchestrator.ShowLiveCoaching)
                        feedbackLabel.text = $"<color=#9FD0BA>실시간 분석</color> · {CounselingCodebook.SkillLabel(lastAnalysis.code)} · 확신도 {lastAnalysis.confidence:P0} · 정밀 분석 중…";
                    // Live mode without an affect report: let Jev's reading of the reply drive the face.
                    if (isLive && lastAnalysis.succeeded && liveAffect.Length == 0 && !string.IsNullOrEmpty(lastAnalysis.clientAffect))
                        ApplyLiveAffect(lastAnalysis.clientAffect, lastAnalysis.clientIntensity);
                }
                if (codingTask != null)
                {
                    if (!isLive && !lastAnalysis.succeeded) feedbackLabel.text = "응답을 분석하는 중…";
                    coding = await codingTask;
                    if (!IsCurrentSubmission(expectedSession, expectedSubmission)) return;
                    // The LLM coder leads when it answers confidently with a known code;
                    // otherwise the lexicon keeps the session going offline or on errors.
                    if (coding.Succeeded && coding.Confidence >= webNpcEngine.MinCoderConfidence &&
                        CounselingCodebook.TryFromCode(coding.Code, coding.Quality, coding.Rationale, out ResponseAssessment coded))
                    {
                        assessment = coded;
                        codingSource = "llm";
                    }
                }
                // Jev-first mode: a confident Jev code decides the turn; the LLM's explanation is
                // kept when it agrees, otherwise the rationale says the code came from Jev.
                if (AnalysisSettings.Mode == AnalysisMode.JevFirst && lastAnalysis.succeeded &&
                    lastAnalysis.confidence >= AnalysisSettings.MinConfidence)
                {
                    bool agrees = coding.Succeeded && coding.Code == lastAnalysis.code;
                    string rationale = agrees ? coding.Rationale
                        : $"실시간 분석(Jev)이 '{CounselingCodebook.SkillLabel(lastAnalysis.code)}'로 판정했습니다 (확신도 {lastAnalysis.confidence:P0}).";
                    if (CounselingCodebook.TryFromCode(lastAnalysis.code, lastAnalysis.quality, rationale, out ResponseAssessment jevCoded))
                    {
                        assessment = jevCoded;
                        codingSource = "jev";
                    }
                }
                ClientRelationalState previousState = relationalState;
                DeliveryObservation observation = actionUnits.IsTracking && actionUnits.IsCalibrated
                    ? new DeliveryObservation(true, actionUnits.Au04, actionUnits.Au12)
                    : DeliveryObservation.Unavailable;
                RelationalTurnResult relationalResult = RelationalDeliveryEvaluator.Evaluate(
                    assessment,
                    observation,
                    previousState,
                    culturalProfile);

                bool supportive = assessment.Quality >= 2 &&
                                  relationalResult.State.WillingnessToDisclose >= previousState.WillingnessToDisclose;
                int proposedTurn = turn + 1;
                string reply;
                string selectedEngine;
                string replyEmotion;
                float replyIntensity = 0.5f;
                AffectPlan replyPlan = null;
                if (isLive)
                {
                    reply = liveReply;
                    selectedEngine = "gemini-live";
                    // Prefer the persona's own report from the live session over the state heuristic.
                    replyEmotion = liveAffect.Length > 0 ? liveAffect : EmotionForState(relationalResult.State, previousState);
                    if (liveIntensity >= 0f) replyIntensity = liveIntensity;
                }
                else
                {
                    string[] replies = supportive ? supportiveReplies : guardedReplies;
                    reply = caseDefinition != null
                        ? caseDefinition.GetReply(proposedTurn - 1, supportive)
                        : replies[Mathf.Min(proposedTurn - 1, replies.Length - 1)];
                    selectedEngine = "local";
                    replyEmotion = supportive ? "relieved" : "guarded";
                    if (webNpcEngine != null && webNpcEngine.IsAvailable)
                    {
                        feedbackLabel.text = "AI 내담자 응답 생성 중…";
                        NpcTurnReply npcReply = await webNpcEngine.RequestReplyAsync(
                            sessionId, proposedTurn, sessionOrchestrator.CurrentStageLabel, utterance, relationalResult.State);
                        if (npcReply.Succeeded)
                        {
                            reply = npcReply.Text;
                            replyEmotion = npcReply.Emotion;
                            replyPlan = npcReply.Plan;
                            if (replyPlan != null) replyIntensity = replyPlan.intensity;
                            selectedEngine = "persona-llm";
                        }
                    }
                    else if (realtimeEngine != null && realtimeEngine.IsRequested)
                    {
                        feedbackLabel.text = "GPT 내담자 연결 중…";
                        RealtimeReply realtimeReply = await realtimeEngine.RequestReplyAsync(utterance);
                        if (realtimeReply.Succeeded)
                        {
                            reply = realtimeReply.Text;
                            selectedEngine = "gpt-realtime-2.1";
                        }
                    }
                }

                if (!IsCurrentSubmission(expectedSession, expectedSubmission)) return;

                relationalState = relationalResult.State;
                client.SetRelationalState(relationalState);
                turn = proposedTurn;
                conversationEngine = selectedEngine;
                currentEmotion = replyEmotion;
                currentIntensity = replyIntensity;
                currentExpressionPolicy = replyPlan != null ? replyPlan.policy : isLive && liveIntensity >= 0f ? "live-report" : string.Empty;
                if (reply.Length > 0) SetClientLine(reply);
                client.SetAffect(ClientAvatarController.AffectForEmotion(replyEmotion));
                client.SetAffectIntensity(replyIntensity);
                webNpcEngine?.RecordExchange(utterance, reply);
                if (!isLive)
                {
                    if (webBridge != null) webBridge.SpeakClient(reply, replyEmotion, replyPlan);
                    else client.Speak(reply, replyEmotion);
                }
                else
                {
                    liveVoice?.SendStateHint(relationalState, CounselingCodebook.CodeOf(assessment));
                }
                string engineLabel = conversationEngine == "local" ? "로컬 사례" :
                    conversationEngine == "persona-llm" ? "AI 페르소나 + 감정 음성" :
                    conversationEngine == "gemini-live" ? "Gemini Live 음성" : "GPT Realtime";
                feedbackLabel.text = sessionOrchestrator.ShowLiveCoaching
                    ? $"{engineLabel} · <color=#EFBE74>{AlignmentLabel(relationalResult.Alignment)}</color> · <color=#9FD0BA>{assessment.Skill}</color> · {relationalResult.CoachingFeedback}{sessionOrchestrator.CurrentFocusPrompt}"
                    : "평가 모드 · 세션 종료 후 전달 피드백을 확인합니다.";
                WriteRecord(utterance, reply, assessment, observation, relationalResult,
                    lexiconAssessment, coding, codingSource, previousState, liveInterrupted);
                sessionOrchestrator.RecordTurn(new CounselingTurnSnapshot
                {
                    turn = turn,
                    stage = sessionOrchestrator.CurrentStageLabel,
                    counselorUtterance = utterance,
                    clientPrompt = clientPrompt,
                    clientReply = reply,
                    skill = assessment.Skill,
                    skillCode = CounselingCodebook.CodeOf(assessment),
                    codingSource = codingSource,
                    skillRationale = assessment.Rationale,
                    quality = assessment.Quality,
                    alignment = AlignmentLabel(relationalResult.Alignment),
                    coachingFeedback = relationalResult.CoachingFeedback,
                    stateBefore = previousState,
                    stateAfter = relationalResult.State,
                    focusOptions = coding.Succeeded ? coding.FocusOptions : Array.Empty<string>(),
                    alternativeResponse = coding.Succeeded && codingSource == "llm" ? coding.Alternative : string.Empty
                }, assessment, relationalResult);
                if (!isLive)
                {
                    counselorInput.text = string.Empty;
                    webBridge?.ClearInput();
                }
                UpdateLabels();
            }
            catch (Exception exception)
            {
                if (IsCurrentSubmission(expectedSession, expectedSubmission))
                {
                    feedbackLabel.text = "응답 처리 중 문제가 발생했습니다. 다시 시도해 주세요.";
                    Debug.LogException(exception);
                }
            }
            finally
            {
                if (expectedSession == sessionGeneration && expectedSubmission == submissionGeneration)
                {
                    isSubmitting = false;
                    SetInteractionEnabled(sessionOrchestrator.CanSubmit);
                    PumpLiveTurns();
                }
            }
        }

        private static string EmotionForState(ClientRelationalState next, ClientRelationalState previous)
        {
            if (next.Guardedness >= 0.66f || next.WillingnessToDisclose < previous.WillingnessToDisclose - 0.02f) return "guarded";
            if (next.Safety >= 0.55f && next.WillingnessToDisclose > previous.WillingnessToDisclose) return "relieved";
            if (next.WillingnessToDisclose > previous.WillingnessToDisclose) return "thoughtful";
            return "anxious";
        }

        private sealed class PendingLiveTurn
        {
            public string counselor;
            public string client;
            public bool interrupted;
        }

        private bool IsCurrentSubmission(int expectedSession, int expectedSubmission) =>
            CounselingSubmissionGuard.CanCommit(
                expectedSession,
                sessionGeneration,
                expectedSubmission,
                submissionGeneration,
                sessionOrchestrator.CanSubmit);

        private void InvalidateSession()
        {
            pendingLiveTurns.Clear();
            sessionGeneration++;
            CancelPendingSubmission();
        }

        private void SetClientLine(string value)
        {
            committedClientLine = value ?? string.Empty;
            clientLine.text = committedClientLine;
        }

        private string InitialLine => caseDefinition == null || string.IsNullOrWhiteSpace(caseDefinition.InitialClientLine)
            ? InitialClientLine
            : caseDefinition.InitialClientLine;

        private void UpdateLabels()
        {
            string stageLabel = sessionOrchestrator == null ? "초기면담" : sessionOrchestrator.CurrentStageLabel;
            string caseTitle = caseDefinition == null ? "불안 사례" : caseDefinition.CaseTitle;
            sessionStatus.text = $"{caseTitle} · {stageLabel} · {turn + 1}번째 교환";
            if (allianceLabel != null)
                allianceLabel.text = $"안전 {Percent(relationalState.Safety)} · 경계 {Percent(relationalState.Guardedness)} · 공개 {Percent(relationalState.WillingnessToDisclose)}";
            if (relationalMeters != null) relationalMeters.Show(relationalState.Safety, relationalState.Guardedness, relationalState.WillingnessToDisclose);
        }

        private void WriteRecord(
            string counselorUtterance,
            string reply,
            ResponseAssessment assessment,
            DeliveryObservation observation,
            RelationalTurnResult relationalResult,
            ResponseAssessment lexiconAssessment,
            SkillCodingReply coding,
            string codingSource,
            ClientRelationalState stateBefore,
            bool liveInterrupted)
        {
            CounselingSessionRecord record = new CounselingSessionRecord
            {
                appVersion = Application.version,
                caseId = caseDefinition == null ? "unknown" : caseDefinition.CaseId,
                lexiconVersion = CounselingResponseEvaluator.Active.version,
                sessionId = sessionId,
                timestampUtc = DateTime.UtcNow.ToString("O"),
                turn = turn,
                trainingMode = sessionOrchestrator.Mode.ToString(),
                sessionStage = sessionOrchestrator.CurrentStageLabel,
                sessionElapsedSeconds = sessionOrchestrator.ElapsedSeconds,
                counselorUtterance = counselorUtterance,
                clientReply = reply,
                skill = assessment.Skill,
                counselingMove = assessment.Move.ToString(),
                quality = assessment.Quality,
                alliance = relationalState.Safety,
                deliveryAlignment = relationalResult.Alignment.ToString(),
                deliveryEvidenceAvailable = observation.IsAvailable,
                relationalSafety = relationalState.Safety,
                guardedness = relationalState.Guardedness,
                willingnessToDisclose = relationalState.WillingnessToDisclose,
                culturalProfileId = culturalProfile.Id,
                deliveryFeedback = relationalResult.CoachingFeedback,
                webcamSignalQuality = webcam.SignalQuality,
                webcamMovement = webcam.Movement,
                auSource = actionUnits.Source,
                auTracking = actionUnits.IsTracking,
                auCalibrated = actionUnits.IsCalibrated,
                au01 = actionUnits.Au01,
                au02 = actionUnits.Au02,
                au04 = actionUnits.Au04,
                au06 = actionUnits.Au06,
                au07 = actionUnits.Au07,
                au12 = actionUnits.Au12,
                au14 = actionUnits.Au14,
                au15 = actionUnits.Au15,
                au17 = actionUnits.Au17,
                au23 = actionUnits.Au23,
                au25 = actionUnits.Au25,
                au26 = actionUnits.Au26,
                au45 = actionUnits.Au45,
                deliveryModifier = relationalResult.DeliveryModifier,
                conversationEngine = conversationEngine,
                sessionPhase = caseDefinition != null ? caseDefinition.PhaseKey : "intake",
                clientAffect = currentEmotion,
                clientAffectIntensity = currentIntensity,
                expressionPolicy = currentExpressionPolicy,
                expressivity = ExpressionSettings.ToControls().expressivity,
                analysisMode = AnalysisSettings.Key(AnalysisSettings.Mode),
                jevCode = lastAnalysis.succeeded ? lastAnalysis.code : string.Empty,
                jevConfidence = lastAnalysis.succeeded ? lastAnalysis.confidence : 0f,
                jevQuality = lastAnalysis.succeeded ? lastAnalysis.quality : -1,
                jevAttendsToFeeling = lastAnalysis.succeeded ? lastAnalysis.attendsToFeeling : 0f,
                jevLatencyMs = lastAnalysis.succeeded ? lastAnalysis.latencyMs : 0,
                jevModel = lastAnalysis.succeeded ? lastAnalysis.model : string.Empty,
                jevClientAffect = lastAnalysis.succeeded ? lastAnalysis.clientAffect : string.Empty,
                skillCode = CounselingCodebook.CodeOf(assessment),
                codebookVersion = CounselingCodebook.Version,
                codingSource = codingSource,
                codingModel = coding.Succeeded ? coding.Model : string.Empty,
                codingConfidence = coding.Succeeded ? coding.Confidence : 0f,
                codingRationale = assessment.Rationale,
                codingEvidence = coding.Succeeded ? coding.Evidence : string.Empty,
                lexiconCode = CounselingCodebook.CodeOf(lexiconAssessment),
                lexiconQuality = lexiconAssessment.Quality,
                relationalModelVersion = RelationalModelWeights.Active.version,
                safetyBefore = stateBefore.Safety,
                guardednessBefore = stateBefore.Guardedness,
                disclosureBefore = stateBefore.WillingnessToDisclose,
                liveInterrupted = liveInterrupted
            };
            LocalJsonlLog.Append("counseling-sessions.jsonl", record);
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        private static string AlignmentLabel(DeliveryAlignment alignment)
        {
            switch (alignment)
            {
                case DeliveryAlignment.Aligned:
                    return "전달 정합";
                case DeliveryAlignment.PossibleMismatch:
                    return "전달 불일치 가능성";
                case DeliveryAlignment.RelationalOrderMismatch:
                    return "관계 순서 불일치";
                default:
                    return "표정 분석 없음";
            }
        }
    }
}
