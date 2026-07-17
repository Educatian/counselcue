using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingSessionController : MonoBehaviour
    {
        private const string InitialClientLine = "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.\n제가 너무 약한 사람인가 싶기도 하고요.";

        [SerializeField] private ClientAvatarController client;
        [SerializeField] private WebcamSignalMonitor webcam;
        [SerializeField] private FacialActionUnitMonitor actionUnits;
        [SerializeField] private GptRealtimeConversationEngine realtimeEngine;
        [SerializeField] private CounselingSessionOrchestrator sessionOrchestrator;
        [SerializeField] private CounselingCaseDefinition caseDefinition;
        [SerializeField] private InputField counselorInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text clientLine;
        [SerializeField] private Text sessionStatus;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private Text allianceLabel;

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

        public bool IsSubmitting => isSubmitting;

        private void Awake()
        {
            sendButton.onClick.AddListener(Submit);
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
            relationalState = ClientRelationalState.Initial;
            turn = 0;
            isSubmitting = false;
            conversationEngine = "local";
            counselorInput.text = string.Empty;
            SetClientLine(InitialLine);
            client.SetAffect(ClientAffect.Anxious, true);
            feedbackLabel.text = sessionOrchestrator.ShowLiveCoaching
                ? "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요."
                : "평가 모드 · 세션 종료 후 전달 피드백을 확인합니다.";
            UpdateLabels();
            SetInteractionEnabled(true);
        }

        public void BeginReplaySession(CounselingTurnSnapshot source)
        {
            InvalidateSession();
            sessionId = Guid.NewGuid().ToString("N");
            relationalState = source.stateBefore;
            turn = Mathf.Max(0, source.turn - 1);
            isSubmitting = false;
            conversationEngine = "local";
            counselorInput.text = string.Empty;
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

        public async void Submit()
        {
            if (isSubmitting || sessionOrchestrator == null || !sessionOrchestrator.CanSubmit) return;
            string utterance = counselorInput.text.Trim();
            if (utterance.Length == 0) return;

            int expectedSession = sessionGeneration;
            int expectedSubmission = ++submissionGeneration;
            isSubmitting = true;
            sendButton.interactable = false;
            try
            {
                ResponseAssessment assessment = CounselingResponseEvaluator.Evaluate(utterance);
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
                string[] replies = supportive ? supportiveReplies : guardedReplies;
                int proposedTurn = turn + 1;
                string clientPrompt = clientLine.text;
                string reply = caseDefinition != null
                    ? caseDefinition.GetReply(proposedTurn - 1, supportive)
                    : replies[Mathf.Min(proposedTurn - 1, replies.Length - 1)];
                string selectedEngine = "local";
                if (realtimeEngine != null && realtimeEngine.IsRequested)
                {
                    feedbackLabel.text = "GPT 내담자 연결 중…";
                    RealtimeReply realtimeReply = await realtimeEngine.RequestReplyAsync(utterance);
                    if (realtimeReply.Succeeded)
                    {
                        reply = realtimeReply.Text;
                        selectedEngine = "gpt-realtime-2.1";
                    }
                }

                if (!IsCurrentSubmission(expectedSession, expectedSubmission)) return;

                relationalState = relationalResult.State;
                turn = proposedTurn;
                conversationEngine = selectedEngine;
                SetClientLine(reply);
                client.SetAffect(supportive ? ClientAffect.Relieved : ClientAffect.Guarded);
                client.Speak(reply);
                string engineLabel = conversationEngine == "local" ? "로컬 사례" : "GPT Realtime";
                feedbackLabel.text = sessionOrchestrator.ShowLiveCoaching
                    ? $"{engineLabel} · <color=#F8C77A><b>{AlignmentLabel(relationalResult.Alignment)}</b></color> · <b>{assessment.Skill}</b> · {relationalResult.CoachingFeedback}{sessionOrchestrator.CurrentFocusPrompt}"
                    : "평가 모드 · 세션 종료 후 전달 피드백을 확인합니다.";
                WriteRecord(utterance, reply, assessment, observation, relationalResult);
                sessionOrchestrator.RecordTurn(new CounselingTurnSnapshot
                {
                    turn = turn,
                    stage = sessionOrchestrator.CurrentStageLabel,
                    counselorUtterance = utterance,
                    clientPrompt = clientPrompt,
                    clientReply = reply,
                    skill = assessment.Skill,
                    quality = assessment.Quality,
                    alignment = AlignmentLabel(relationalResult.Alignment),
                    coachingFeedback = relationalResult.CoachingFeedback,
                    stateBefore = previousState,
                    stateAfter = relationalResult.State
                }, assessment, relationalResult);
                counselorInput.text = string.Empty;
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
                }
            }
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
            sessionGeneration++;
            CancelPendingSubmission();
        }

        private void SetClientLine(string value) => clientLine.text = value;

        private string InitialLine => caseDefinition == null || string.IsNullOrWhiteSpace(caseDefinition.InitialClientLine)
            ? InitialClientLine
            : caseDefinition.InitialClientLine;

        private void UpdateLabels()
        {
            string stageLabel = sessionOrchestrator == null ? "초기면담" : sessionOrchestrator.CurrentStageLabel;
            string caseTitle = caseDefinition == null ? "불안 사례" : caseDefinition.CaseTitle;
            sessionStatus.text = $"{caseTitle} · {stageLabel} · {turn + 1}번째 교환";
            allianceLabel.text = $"안전 {Percent(relationalState.Safety)} · 경계 {Percent(relationalState.Guardedness)} · 공개 {Percent(relationalState.WillingnessToDisclose)}";
        }

        private void WriteRecord(
            string counselorUtterance,
            string reply,
            ResponseAssessment assessment,
            DeliveryObservation observation,
            RelationalTurnResult relationalResult)
        {
            CounselingSessionRecord record = new CounselingSessionRecord
            {
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
                conversationEngine = conversationEngine
            };
            File.AppendAllText(Path.Combine(Application.persistentDataPath, "counseling-sessions.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine);
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
                    return "비언어 근거 없음";
            }
        }
    }
}
