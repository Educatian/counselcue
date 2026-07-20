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
        [SerializeField] private ClientAvatarController client;
        [SerializeField] private RectTransform unityInputCard;
        [SerializeField] private RectTransform unityInputAccent;
        [SerializeField] private GameObject unityInputField;
        [SerializeField] private GameObject unitySendButton;
        [SerializeField] private Text unityFeedbackLabel;

        private bool lastEnabled;
        private string lastFeedbackText = string.Empty;
        private string pendingSpeechText = string.Empty;
        private string pendingSpeechEmotion = "anxious";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CounselCueWeb_Initialize(string objectName, string apiBaseUrl);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetEnabled(int enabled);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetText(string value);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetFeedback(string value);
        [DllImport("__Internal")] private static extern void CounselCueWeb_Speak(string text, string emotion);
#endif
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
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

        private void Update()
        {
            SyncFeedback();
            bool enabled = orchestrator != null && orchestrator.CanSubmit && session != null && !session.IsSubmitting;
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

        public void SpeakClient(string text, string emotion)
        {
            pendingSpeechText = text ?? string.Empty;
            pendingSpeechEmotion = emotion ?? "anxious";
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_Speak(pendingSpeechText, pendingSpeechEmotion);
#else
            client?.Speak(pendingSpeechText, pendingSpeechEmotion);
#endif
        }

        public void OnWebVoiceStarted(string unused)
        {
            client?.BeginSpeaking(pendingSpeechText, pendingSpeechEmotion);
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
