using System.Runtime.InteropServices;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselCueWebBridge : MonoBehaviour
    {
        [SerializeField] private CounselingSessionController session;
        [SerializeField] private CounselingSessionOrchestrator orchestrator;
        [SerializeField] private WebNpcConversationEngine npcEngine;
        private bool lastEnabled;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CounselCueWeb_Initialize(string objectName, string apiBaseUrl);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetEnabled(int enabled);
        [DllImport("__Internal")] private static extern void CounselCueWeb_SetText(string value);
        [DllImport("__Internal")] private static extern void CounselCueWeb_Speak(string text, string emotion);
#endif
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
            CounselCueWeb_Initialize(gameObject.name, npcEngine == null ? "" : npcEngine.ApiBaseUrl);
            CounselCueWeb_SetEnabled(0);
#endif
        }
        private void Update()
        {
            bool enabled=orchestrator != null && orchestrator.CanSubmit && session != null && !session.IsSubmitting;
            if (enabled==lastEnabled) return;
            lastEnabled=enabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_SetEnabled(enabled ? 1 : 0);
#endif
        }
        public void OnWebTextChanged(string value) { session?.SetCounselorInput(value ?? ""); }
        public void OnWebTextSubmitted(string value) {
            if (session==null) return;
            session.SetCounselorInput(value ?? "");
            session.Submit();
        }
        public void ClearInput() {
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_SetText("");
#endif
        }
        public void SpeakClient(string text, string emotion) {
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_Speak(text ?? "", emotion ?? "anxious");
#endif
        }
    }
}
