using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    public static class WebGlHudLayout
    {

        public static void ApplyBrowserInputLayout(
            RectTransform inputCard,
            RectTransform inputAccent,
            GameObject inputField,
            GameObject sendButton)
        {
            if (inputCard == null || inputAccent == null || inputField == null || sendButton == null)
            {
                Debug.LogWarning("CounselCue WebGL HUD references are incomplete; keeping the Unity input controls visible.");
                return;
            }

            inputField.SetActive(false);
            sendButton.SetActive(false);
            inputCard.gameObject.SetActive(false);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CounselCueWebBridge : MonoBehaviour
    {
        [SerializeField] private CounselingSessionController session;
        [SerializeField] private CounselingSessionOrchestrator orchestrator;
        [SerializeField] private WebNpcConversationEngine npcEngine;
        [SerializeField] private ClientAvatarHost client;
        [SerializeField] private RectTransform unityInputCard;
        [SerializeField] private RectTransform unityInputAccent;
        [SerializeField] private GameObject unityInputField;
        [SerializeField] private GameObject unitySendButton;
        [SerializeField] private Text unityFeedbackLabel;

        private bool lastEnabled;
        private LiveVoiceController liveVoice;

        private void Awake() => liveVoice = GetComponent<LiveVoiceController>();
        private string lastFeedbackText = string.Empty;
        private string pendingSpeechText = string.Empty;
        private string pendingSpeechEmotion = "anxious";
        private float pendingSpeechIntensity = 0.5f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CounselCueWeb_Initialize(string objectName, string apiBaseUrl);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetEnabled(int enabled);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetText(string value);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetFeedback(string value);
        [DllImport("__Internal")] private static extern void CounselCueWeb_Speak(string text, string emotion);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SpeakPlan(string payloadJson);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetCase(string caseId);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetLanguage(int isEnglish);
#endif
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ReleaseBrowserKeyboard();
            WebGlHudLayout.ApplyBrowserInputLayout(
                unityInputCard,
                unityInputAccent,
                unityInputField,
                unitySendButton);
            CounselCueWeb_Initialize(gameObject.name, npcEngine == null ? "" : npcEngine.ApiBaseUrl);
            SyncFeedback();
            CounselCueWeb_SetEnabled(0);
#endif
        }

        /// <summary>
        /// Lets the browser input bar receive Korean IME, spacing and punctuation keys
        /// (WebGLInput.captureAllKeyboardInput = false). Set through reflection because the Unity
        /// 6.3 Web platform does not expose WebGLInput to player scripts at compile time.
        /// </summary>
        private static void ReleaseBrowserKeyboard()
        {
            System.Type input = System.Type.GetType("UnityEngine.WebGLInput, UnityEngine.WebGLModule")
                ?? System.Type.GetType("UnityEngine.WebGLInput, UnityEngine.CoreModule")
                ?? System.Type.GetType("UnityEngine.WebGLInput, UnityEngine");
            System.Reflection.PropertyInfo capture = input?.GetProperty("captureAllKeyboardInput");
            if (capture != null && capture.CanWrite) capture.SetValue(null, false);
            else Debug.Log("CounselCue: WebGLInput unavailable; the browser input bar stops key events itself.");
        }

        private void OnEnable() => CounselingLanguageToggle.LanguageChanged += OnLanguageChanged;

        private void OnDisable() => CounselingLanguageToggle.LanguageChanged -= OnLanguageChanged;

        // Keeps the browser-side input bar and tour in the same language as the Unity UI.
        private void OnLanguageChanged(bool useEnglish)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_SetLanguage(useEnglish ? 1 : 0);
#endif
        }

        private void Update()
        {
            SyncFeedback();
            // In a live voice session the dock stays up while a turn is being coded, so the
            // learner can keep talking, typing or muting.
            bool live = liveVoice != null && liveVoice.Active;
            bool enabled = orchestrator != null && orchestrator.CanSubmit && session != null && (live || !session.IsSubmitting);
            if (enabled == lastEnabled) return;
            lastEnabled = enabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_SetEnabled(enabled ? 1 : 0);
#endif
        }

        private void SyncFeedback()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string value = unityFeedbackLabel == null ? string.Empty : unityFeedbackLabel.text;
            if (value == lastFeedbackText) return;
            lastFeedbackText = value;
            CounselCueWeb_SetFeedback(value);
#endif
        }

        public void OnWebTextChanged(string value) => session?.SetCounselorInput(value ?? "");

        public void OnWebTextSubmitted(string value)
        {
            if (session == null) return;
            session.SetCounselorInput(value ?? "");
            session.Submit();
        }

        public void ClearInput()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_SetText("");
#endif
        }

        public void SpeakClient(string text, string emotion, AffectPlan plan = null)
        {
            pendingSpeechText = text ?? string.Empty;
            pendingSpeechEmotion = emotion ?? "anxious";
            pendingSpeechIntensity = plan != null ? plan.intensity : 0.5f;
#if UNITY_WEBGL && !UNITY_EDITOR
            // The server picks a case-specific voice so each client sounds their age and gender.
            CounselCueWeb_SetCase(npcEngine == null ? "" : npcEngine.ActiveCaseId);
            if (plan == null) CounselCueWeb_Speak(pendingSpeechText, pendingSpeechEmotion);
            else
            {
                // The server re-derives the voice style from this plan with the same policy.
                CounselCueWeb_SpeakPlan(JsonUtility.ToJson(new VoicePayload
                {
                    text = pendingSpeechText,
                    spoken = plan.spoken,
                    emotion = plan.affect,
                    intensity = plan.intensity,
                    delivery = plan.delivery,
                    expression = ExpressionSettings.ToControls(),
                }));
            }
#else
            client?.Speak(pendingSpeechText, pendingSpeechEmotion);
#endif
        }

        public void OnWebVoiceStarted(string unused)
        {
            client?.SetAffectIntensity(pendingSpeechIntensity);
            client?.BeginSpeaking(pendingSpeechText, pendingSpeechEmotion);
        }

        [System.Serializable]
        private sealed class VoicePayload
        {
            public string text; public string spoken; public string emotion; public float intensity;
            public string delivery; public ExpressionControls expression;
        }

        public void OnWebVoiceEnded(string unused)
        {
            client?.StopSpeaking();
        }

        public void OnWebVoiceFailed(string unused)
        {
            client?.Speak(pendingSpeechText, pendingSpeechEmotion);
        }
    }
}
