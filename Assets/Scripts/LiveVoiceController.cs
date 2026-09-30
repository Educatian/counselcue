using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public enum LiveVoiceState { Off, Connecting, Listening, ClientSpeaking, Error }

    /// <summary>
    /// Real-time voice sessions with Gemini Live (gemini-3.8-live). The browser side
    /// (CounselCueWebBridge.jslib) streams the microphone and plays the client's voice; this
    /// component receives transcripts and turns them into ordinary CounselCue turns (skill
    /// coding, relational model, research record, debrief). Must sit on the same GameObject
    /// as <see cref="CounselCueWebBridge"/>, which is the jslib's SendMessage target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiveVoiceController : MonoBehaviour
    {
        public const string PreferenceKey = "counselcue.conversation-mode";

        [SerializeField] private CounselingSessionController session;
        [SerializeField] private WebNpcConversationEngine npcEngine;
        [SerializeField] private ClientAvatarHost client;
        [Tooltip("Send '[상담 시스템]' relational-state notes into the live session after each coded turn (experimental).")]
        [SerializeField] private bool sendStateHints;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CounselCueWeb_LiveStart(string configJson);
        [DllImport("__Internal")] private static extern void CounselCueWeb_LiveStop();
        [DllImport("__Internal")] private static extern void CounselCueWeb_LiveMute(int muted);
        [DllImport("__Internal")] private static extern void CounselCueWeb_LiveText(string text);
        [DllImport("__Internal")] private static extern void CounselCueWeb_LiveHint(string text);
#endif

        private string lastSpeakingEmotion = "anxious";

        public static bool IsSupported => Application.platform == RuntimePlatform.WebGLPlayer;
        public bool Requested { get; private set; }
        public LiveVoiceState State { get; private set; } = LiveVoiceState.Off;
        public bool Active => State != LiveVoiceState.Off && State != LiveVoiceState.Error;
        public string LastError { get; private set; } = string.Empty;
        public event Action<LiveVoiceState> StateChanged;

        private void Awake()
        {
            Requested = IsSupported && PlayerPrefs.GetString(PreferenceKey, "text") == "live";
        }

        public void SetRequested(bool value)
        {
            Requested = value && IsSupported;
            PlayerPrefs.SetString(PreferenceKey, value ? "live" : "text");
            PlayerPrefs.Save();
        }

        public void StartSession(string sessionId, string caseId, string openingLine, ClientRelationalState state, string phase = "intake")
        {
            if (!Requested || !IsSupported) return;
            LastError = string.Empty;
            StartConfig config = new StartConfig
            {
                sessionId = sessionId,
                caseId = caseId,
                openingLine = openingLine,
                phase = phase,
                safety = state.Safety,
                guardedness = state.Guardedness,
                disclosure = state.WillingnessToDisclose,
                hints = sendStateHints
            };
            SetState(LiveVoiceState.Connecting);
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_LiveStart(JsonUtility.ToJson(config));
#endif
        }

        public void StopSession()
        {
            if (State == LiveVoiceState.Off) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_LiveStop();
#endif
            client?.StopSpeaking();
            SetState(LiveVoiceState.Off);
        }

        public void SetMuted(bool muted)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Active) CounselCueWeb_LiveMute(muted ? 1 : 0);
#endif
        }

        /// <summary>A typed counselor turn during a live session (spoken reply follows).</summary>
        public void SendText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Active && !string.IsNullOrWhiteSpace(text)) CounselCueWeb_LiveText(text.Trim());
#endif
        }

        public void SendStateHint(ClientRelationalState state, string skillCode)
        {
            if (!sendStateHints || !Active) return;
            string hint = string.Format(CultureInfo.InvariantCulture,
                "[상담 시스템] 방금 상담자 응답 코드: {0}. 현재 관계 상태 — 안전 {1:0.00}, 경계 {2:0.00}, 개방 의지 {3:0.00}. 이 메모에는 답하지 마세요.",
                skillCode, state.Safety, state.Guardedness, state.WillingnessToDisclose);
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_LiveHint(hint);
#endif
        }

        // ---- jslib callbacks (SendMessage) -------------------------------------------------

        public void OnLiveState(string value)
        {
            string state = (value ?? string.Empty).Trim();
            if (state.StartsWith("error", StringComparison.Ordinal))
            {
                LastError = state;
                client?.StopSpeaking();
                SetState(LiveVoiceState.Error);
                session?.OnLiveVoiceFailed(state);
                return;
            }
            switch (state)
            {
                case "connecting": SetState(LiveVoiceState.Connecting); break;
                case "listening":
                    client?.StopSpeaking();
                    SetState(LiveVoiceState.Listening);
                    break;
                case "speaking":
                    lastSpeakingEmotion = session == null ? "anxious" : session.CurrentClientEmotion;
                    client?.BeginExternalSpeech(lastSpeakingEmotion);
                    SetState(LiveVoiceState.ClientSpeaking);
                    break;
                case "off":
                    client?.StopSpeaking();
                    SetState(LiveVoiceState.Off);
                    break;
            }
        }

        public void OnLiveLevel(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float level))
                client?.SetSpeechLevel(level);
        }

        public void OnLivePartial(string json)
        {
            LivePartial partial = Parse<LivePartial>(json);
            if (partial == null || string.IsNullOrWhiteSpace(partial.text)) return;
            session?.ShowLivePartial(partial.role == "client", partial.text);
        }

        public void OnLiveTurn(string json)
        {
            LiveTurn turn = Parse<LiveTurn>(json);
            if (turn == null) return;
            session?.SubmitLiveTurn(turn.counselor ?? string.Empty, turn.client ?? string.Empty, turn.interrupted);
        }

        private void SetState(LiveVoiceState value)
        {
            if (State == value) return;
            State = value;
            StateChanged?.Invoke(value);
        }

        private static T Parse<T>(string json) where T : class
        {
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception) { return null; }
        }

        [Serializable] private sealed class StartConfig
        {
            public string sessionId; public string caseId; public string openingLine; public string phase;
            public float safety; public float guardedness; public float disclosure; public bool hints;
        }
        [Serializable] private sealed class LivePartial { public string role; public string text; }
        [Serializable] private sealed class LiveTurn { public string counselor; public string client; public bool interrupted; }
    }
}
