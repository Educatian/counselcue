using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingSessionController : MonoBehaviour
    {
        [SerializeField] private ClientAvatarController client;
        [SerializeField] private WebcamSignalMonitor webcam;
        [SerializeField] private FacialActionUnitMonitor actionUnits;
        [SerializeField] private GptRealtimeConversationEngine realtimeEngine;
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
            "회사에 들어가는 순간부터 가슴이 답답해져요. 특히 팀장님과 이야기할 때 더 심해지고요."
        };

        private readonly string[] guardedReplies =
        {
            "글쎄요… 그냥 제가 알아서 해야 하는 문제 같기도 해요.",
            "그렇게 간단히 해결될 문제였으면 이미 했을 것 같아요.",
            "무슨 말을 해야 할지 잘 모르겠어요."
        };

        private string sessionId;
        private float alliance = 0.38f;
        private int turn;
        private bool isSubmitting;
        private string conversationEngine = "local";

        private void Awake()
        {
            sessionId = Guid.NewGuid().ToString("N");
            sendButton.onClick.AddListener(Submit);
            counselorInput.onEndEdit.AddListener(value =>
            {
                if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrWhiteSpace(value)) Submit();
            });
        }

        private void Start()
        {
            SetClientLine("요즘 회사에 가려고 하면 숨이 막히는 것 같아요.\n제가 너무 약한 사람인가 싶기도 하고요.");
            client.SetAffect(ClientAffect.Anxious, true);
            UpdateLabels();
        }

        public async void Submit()
        {
            if (isSubmitting) return;
            string utterance = counselorInput.text.Trim();
            if (utterance.Length == 0) return;

            isSubmitting = true;
            sendButton.interactable = false;
            ResponseAssessment assessment = CounselingResponseEvaluator.Evaluate(utterance);
            float deliveryModifier = 0f;
            alliance = Mathf.Clamp01(alliance + assessment.TrustDelta + deliveryModifier);
            turn++;

            bool supportive = assessment.Quality >= 2;
            string[] replies = supportive ? supportiveReplies : guardedReplies;
            string reply = replies[Mathf.Min(turn - 1, replies.Length - 1)];
            conversationEngine = "local";
            if (realtimeEngine != null && realtimeEngine.IsRequested)
            {
                feedbackLabel.text = "GPT 내담자 연결 중…";
                RealtimeReply realtimeReply = await realtimeEngine.RequestReplyAsync(utterance);
                if (realtimeReply.Succeeded)
                {
                    reply = realtimeReply.Text;
                    conversationEngine = "gpt-realtime-2.1";
                }
            }

            SetClientLine(reply);
            client.SetAffect(supportive ? ClientAffect.Relieved : ClientAffect.Guarded);
            client.Speak(reply);
            string engineLabel = conversationEngine == "local" ? "로컬 사례" : "GPT Realtime";
            feedbackLabel.text = $"{engineLabel} · {assessment.Skill} · {assessment.Rationale}";
            WriteRecord(utterance, reply, assessment, deliveryModifier);
            counselorInput.text = string.Empty;
            counselorInput.ActivateInputField();
            UpdateLabels();
            sendButton.interactable = true;
            isSubmitting = false;
        }

        private void SetClientLine(string value) => clientLine.text = value;

        private void UpdateLabels()
        {
            sessionStatus.text = $"불안 사례 · 초기면담 · {turn + 1}번째 교환";
            allianceLabel.text = $"관계 안전감  {Mathf.RoundToInt(alliance * 100f)}";
        }

        private void WriteRecord(string counselorUtterance, string reply, ResponseAssessment assessment, float deliveryModifier)
        {
            SessionRecord record = new SessionRecord
            {
                sessionId = sessionId,
                timestampUtc = DateTime.UtcNow.ToString("O"),
                turn = turn,
                counselorUtterance = counselorUtterance,
                clientReply = reply,
                skill = assessment.Skill,
                quality = assessment.Quality,
                alliance = alliance,
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
                deliveryModifier = deliveryModifier,
                conversationEngine = conversationEngine
            };
            File.AppendAllText(Path.Combine(Application.persistentDataPath, "counseling-sessions.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine);
        }

        [Serializable]
        private sealed class SessionRecord
        {
            public string sessionId;
            public string timestampUtc;
            public int turn;
            public string counselorUtterance;
            public string clientReply;
            public string skill;
            public int quality;
            public float alliance;
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
}
