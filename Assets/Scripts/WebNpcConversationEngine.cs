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
        private NpcTurnReply(bool ok, string text, string emotion, string error, AffectPlan plan = null)
        { Succeeded = ok; Text = text; Emotion = emotion; Error = error; Plan = plan; }
        public bool Succeeded { get; }
        public string Text { get; }
        public string Emotion { get; }
        /// <summary>The server expression policy's plan for this reply; null from older servers.</summary>
        public AffectPlan Plan { get; }
        public string Error { get; }
        public static NpcTurnReply Success(string text, string emotion, AffectPlan plan = null) => new NpcTurnReply(true, text, emotion, "", plan);
        public static NpcTurnReply Failure(string error) => new NpcTurnReply(false, "", "", error);
    }

    /// <summary>Result of the server-side LLM skill coder (Server /code).</summary>
    public readonly struct SkillCodingReply
    {
        public SkillCodingReply(bool ok, string code, int quality, string rationale, string evidence, float confidence, string model, string error,
            string[] focusOptions = null, string alternative = "")
        {
            Succeeded = ok; Code = code; Quality = quality; Rationale = rationale;
            Evidence = evidence; Confidence = confidence; Model = model; Error = error;
            FocusOptions = focusOptions ?? Array.Empty<string>(); Alternative = alternative ?? "";
        }
        /// <summary>Cues in the client's line a counselor could respond to ("감정: 억울함").</summary>
        public string[] FocusOptions { get; }
        /// <summary>One exemplary alternative response, empty when the response was already strong.</summary>
        public string Alternative { get; }
        public bool Succeeded { get; }
        public string Code { get; }
        public int Quality { get; }
        public string Rationale { get; }
        public string Evidence { get; }
        public float Confidence { get; }
        public string Model { get; }
        public string Error { get; }
        public static SkillCodingReply Failure(string error) => new SkillCodingReply(false, "", 0, "", "", 0f, "", error);
    }

    [DisallowMultipleComponent]
    public sealed class WebNpcConversationEngine : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "https://counselcue-api.jewoong-moon.workers.dev";
        [SerializeField, Range(5f, 45f)] private float timeoutSeconds = 25f;
        [SerializeField] private bool enableInEditor;
        [SerializeField] private string activeCaseId = "workplace-anxiety-01";
        [SerializeField, Range(0, 12)] private int historyTurns = 8;
        [Header("LLM skill coder (lexicon is the fallback)")]
        [SerializeField] private bool useLlmCoder = true;
        [SerializeField, Range(3f, 20f)] private float coderTimeoutSeconds = 8f;
        [SerializeField, Range(0f, 1f)] private float minCoderConfidence = 0.5f;

        // The persona server is stateless, so the recent dialogue is resent each turn.
        // Without it the LLM client cannot remember what it already disclosed.
        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private string openingLine = string.Empty;
        private string phaseKey = "intake";

        /// <summary>Counseling phase sent to the server ("intake", "goal_setting", "middle", "termination").</summary>
        public string PhaseKey { get => phaseKey; set => phaseKey = string.IsNullOrWhiteSpace(value) ? "intake" : value; }

        public string ApiBaseUrl => apiBaseUrl.TrimEnd('/');
        public string ActiveCaseId => activeCaseId;
        public bool IsAvailable => Application.platform == RuntimePlatform.WebGLPlayer || enableInEditor;
        public bool CoderEnabled => IsAvailable && useLlmCoder;
        public float MinCoderConfidence => minCoderConfidence;

        /// <summary>
        /// Asks the server's LLM coder to code the counselor turn with the shared codebook.
        /// Callers fall back to the lexicon on any failure or low confidence.
        /// </summary>
        /// <summary>Jev real-time analysis (worker /analyze); fast, typed, with calibrated confidence.</summary>
        public async Task<JevAnalysis> RequestAnalysisAsync(string sessionId, string utterance, string clientLine, string clientReply = null)
        {
            if (!IsAvailable || AnalysisSettings.Mode == AnalysisMode.Off) return JevAnalysis.Failure("analysis off");
            AnalyzeRequest payload = new AnalyzeRequest {
                sessionId=sessionId, caseId=activeCaseId, phase=phaseKey, counselorUtterance=Clip(utterance, 800),
                clientLine=Clip(clientLine, 600), clientReply=Clip(clientReply, 400)
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using UnityWebRequest request = new UnityWebRequest(ApiBaseUrl + "/analyze", UnityWebRequest.kHttpVerbPOST) {
                uploadHandler=new UploadHandlerRaw(bytes), downloadHandler=new DownloadHandlerBuffer(), timeout=6
            };
            request.SetRequestHeader("Content-Type", "application/json");
            UnityWebRequestAsyncOperation operation=request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
                return JevAnalysis.Failure($"analysis {request.responseCode}: {request.error}");
            JevAnalysis result = JsonUtility.FromJson<JevAnalysis>(request.downloadHandler.text);
            if (result == null || !CounselingCodebook.IsKnown(result.code)) return JevAnalysis.Failure("analysis empty");
            result.succeeded = true;
            return result;
        }

        public async Task<SkillCodingReply> RequestCodingAsync(string sessionId, int turn, string stage, string utterance, string clientLine)
        {
            if (!CoderEnabled) return SkillCodingReply.Failure("coder disabled");
            CodeRequest payload = new CodeRequest {
                sessionId=sessionId, caseId=activeCaseId, turn=turn, stage=stage, phase=phaseKey,
                counselorUtterance=Clip(utterance, 800), clientLine=Clip(clientLine, 400)
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using UnityWebRequest request = new UnityWebRequest(ApiBaseUrl + "/code", UnityWebRequest.kHttpVerbPOST) {
                uploadHandler=new UploadHandlerRaw(bytes), downloadHandler=new DownloadHandlerBuffer(),
                timeout=Mathf.CeilToInt(coderTimeoutSeconds)
            };
            request.SetRequestHeader("Content-Type", "application/json");
            UnityWebRequestAsyncOperation operation=request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
                return SkillCodingReply.Failure($"coder {request.responseCode}: {request.error}");
            CodeResponse response = JsonUtility.FromJson<CodeResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.code)) return SkillCodingReply.Failure("coder empty");
            return new SkillCodingReply(true, response.code, response.quality, response.rationale ?? "", response.evidence ?? "",
                response.confidence, response.model ?? "", "", response.focus_options, response.alternative ?? "");
        }

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
            openingLine = Clip(clientOpeningLine, 400);
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
            history.Add(new HistoryEntry { counselor = Clip(counselorUtterance, 400), client = Clip(clientReply, 400) });
            int overflow = history.Count - Mathf.Max(0, historyTurns);
            if (overflow > 0) history.RemoveRange(0, overflow);
        }

        public async Task<NpcTurnReply> RequestReplyAsync(string sessionId, int turn, string stage, string utterance, ClientRelationalState state)
        {
            if (!IsAvailable) return NpcTurnReply.Failure("웹 NPC 엔진 비활성화");
            TurnRequest payload = new TurnRequest {
                sessionId=sessionId, caseId=activeCaseId, turn=turn, stage=stage, phase=phaseKey, counselorUtterance=Clip(utterance, 800),
                safety=state.Safety, guardedness=state.Guardedness, disclosure=state.WillingnessToDisclose,
                openingLine=openingLine, history=history.ToArray(), expression=ExpressionSettings.ToControls()
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
                : NpcTurnReply.Success(response.reply.Trim(), NormalizeEmotion(response.emotion),
                    response.plan != null && !string.IsNullOrEmpty(response.plan.policy) ? response.plan : null);
        }

        // Mirrors the worker's per-field limits so the request body stays well under its cap.
        private static string Clip(string value, int maxLength)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }

        private static string NormalizeEmotion(string value)
        {
            string result=(value ?? "").Trim().ToLowerInvariant();
            return result=="guarded" || result=="anxious" || result=="relieved" || result=="thoughtful" ? result : "anxious";
        }

        [Serializable] private sealed class TurnRequest {
            public string sessionId; public string caseId; public int turn; public string stage; public string phase; public string counselorUtterance;
            public float safety; public float guardedness; public float disclosure;
            public string openingLine; public HistoryEntry[] history; public ExpressionControls expression;
        }
        [Serializable] private sealed class HistoryEntry { public string counselor; public string client; }
        [Serializable] private sealed class TurnResponse { public string reply; public string emotion; public float intensity; public AffectPlan plan; }
        [Serializable] private sealed class AnalyzeRequest {
            public string sessionId; public string caseId; public string phase;
            public string counselorUtterance; public string clientLine; public string clientReply;
        }
        [Serializable] private sealed class CodeRequest {
            public string sessionId; public string caseId; public int turn; public string stage; public string phase;
            public string counselorUtterance; public string clientLine;
        }
        [Serializable] private sealed class CodeResponse {
            public string code; public int quality; public string rationale; public string evidence;
            public float confidence; public string model;
            public string[] focus_options; public string alternative;
        }
    }
}
