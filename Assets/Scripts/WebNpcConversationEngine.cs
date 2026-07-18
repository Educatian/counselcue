using System;
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
        public string ApiBaseUrl => apiBaseUrl.TrimEnd('/');
        public bool IsAvailable => Application.platform == RuntimePlatform.WebGLPlayer || enableInEditor;

        public async Task<NpcTurnReply> RequestReplyAsync(string sessionId, int turn, string stage, string utterance, ClientRelationalState state)
        {
            if (!IsAvailable) return NpcTurnReply.Failure("웹 NPC 엔진 비활성화");
            TurnRequest payload = new TurnRequest {
                sessionId=sessionId, turn=turn, stage=stage, counselorUtterance=utterance,
                safety=state.Safety, guardedness=state.Guardedness, disclosure=state.WillingnessToDisclose
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
            public string sessionId; public int turn; public string stage; public string counselorUtterance;
            public float safety; public float guardedness; public float disclosure;
        }
        [Serializable] private sealed class TurnResponse { public string reply; public string emotion; }
    }
}
