using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AdieLab.AffectCounsel
{
    public readonly struct NpcTurnReply
    {
        private NpcTurnReply(bool ok, string text, string emotion, string error)
        { Succeeded = ok; Text = text; Emotion = emotion; Error = error; }
        public bool Succeeded { get; }
        public string Text { get; }
        public string Emotion { get; }
        public string Error { get; }
        public static NpcTurnReply Success(string text, string emotion) => new NpcTurnReply(true, text, emotion, "");
        public static NpcTurnReply Failure(string error) => new NpcTurnReply(false, "", "", error);
    }

    [DisallowMultipleComponent]
    public sealed class WebNpcConversationEngine : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "https://counselcue-api.jewoong-moon.workers.dev";
        [SerializeField, Range(5f, 45f)] private float timeoutSeconds = 25f;
        [SerializeField] private bool enableInEditor;
        [SerializeField] private string activeCaseId = "workplace-anxiety-01";
        [SerializeField, Range(0, 12)] private int historyTurns = 8;

        // The persona server is stateless, so the recent dialogue is resent each turn.
        // Without it the LLM client cannot remember what it already disclosed.
        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private string openingLine = string.Empty;

        public string ApiBaseUrl => apiBaseUrl.TrimEnd('/');
        public string ActiveCaseId => activeCaseId;
        public bool IsAvailable => Application.platform == RuntimePlatform.WebGLPlayer || enableInEditor;

        public void ConfigureCase(CounselingCaseDefinition definition)
        {
            if (definition == null) activeCaseId = "workplace-anxiety-01";
            else activeCaseId = string.IsNullOrWhiteSpace(definition.PersonaPromptKey)
                ? definition.CaseId
                : definition.PersonaPromptKey.Trim();
        }

        public void ResetConversation(string clientOpeningLine, IReadOnlyList<CounselingTurnSnapshot> priorTurns = null)
        {
            history.Clear();
            openingLine = clientOpeningLine ?? string.Empty;
            if (priorTurns == null) return;
            for (int i = 0; i < priorTurns.Count; i++)
            {
                CounselingTurnSnapshot snapshot = priorTurns[i];
                if (snapshot != null) RecordExchange(snapshot.counselorUtterance, snapshot.clientReply);
            }
        }

        public void RecordExchange(string counselorUtterance, string clientReply)
        {
            if (string.IsNullOrWhiteSpace(counselorUtterance) && string.IsNullOrWhiteSpace(clientReply)) return;
            history.Add(new HistoryEntry { counselor = counselorUtterance ?? string.Empty, client = clientReply ?? string.Empty });
            int overflow = history.Count - Mathf.Max(0, historyTurns);
            if (overflow > 0) history.RemoveRange(0, overflow);
        }

        public async Task<NpcTurnReply> RequestReplyAsync(string sessionId, int turn, string stage, string utterance, ClientRelationalState state)
        {
            if (!IsAvailable) return NpcTurnReply.Failure("웹 NPC 엔진 비활성화");
            TurnRequest payload = new TurnRequest {
                sessionId=sessionId, caseId=activeCaseId, turn=turn, stage=stage, counselorUtterance=utterance,
                safety=state.Safety, guardedness=state.Guardedness, disclosure=state.WillingnessToDisclose,
                openingLine=openingLine, history=history.ToArray()
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using UnityWebRequest request = new UnityWebRequest(ApiBaseUrl + "/turn", UnityWebRequest.kHttpVerbPOST) {
                uploadHandler=new UploadHandlerRaw(bytes), downloadHandler=new DownloadHandlerBuffer(),
                timeout=Mathf.CeilToInt(timeoutSeconds)
            };
            request.SetRequestHeader("Content-Type", "application/json");
            UnityWebRequestAsyncOperation operation=request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
                return NpcTurnReply.Failure($"NPC API {request.responseCode}: {request.error}");
            TurnResponse response=JsonUtility.FromJson<TurnResponse>(request.downloadHandler.text);
            return response == null || string.IsNullOrWhiteSpace(response.reply)
                ? NpcTurnReply.Failure("NPC 응답이 비어 있습니다.")
                : NpcTurnReply.Success(response.reply.Trim(), NormalizeEmotion(response.emotion));
        }

        private static string NormalizeEmotion(string value)
        {
            string result=(value ?? "").Trim().ToLowerInvariant();
            return result=="guarded" || result=="anxious" || result=="relieved" || result=="thoughtful" ? result : "anxious";
        }

        [Serializable] private sealed class TurnRequest {
            public string sessionId; public string caseId; public int turn; public string stage; public string counselorUtterance;
            public float safety; public float guardedness; public float disclosure;
            public string openingLine; public HistoryEntry[] history;
        }
        [Serializable] private sealed class HistoryEntry { public string counselor; public string client; }
        [Serializable] private sealed class TurnResponse { public string reply; public string emotion; }
    }
}
