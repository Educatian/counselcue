using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Briefing chips for the conversation mode: typed text (AI voice replies) or a real-time
    /// Gemini Live voice session. Choosing live is the learner's informed choice to stream the
    /// microphone to Google; the note says so before any session starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConversationModeSelector : MonoBehaviour
    {
        public const string TextNote = "텍스트로 응답하고, 내담자는 AI 음성으로 답합니다.";
        public const string LiveNote = "마이크 음성이 Google Gemini로 실시간 전송됩니다 · 원음은 저장하지 않습니다.";
        public const string UnsupportedNote = "실시간 음성은 웹 버전(Chrome·Edge)에서 사용할 수 있습니다.";

        [SerializeField] private LiveVoiceController liveVoice;
        [SerializeField] private Button textButton;
        [SerializeField] private Button liveButton;
        [SerializeField] private Text note;

        private bool showUnsupported;

        private void Start()
        {
            textButton.onClick.AddListener(() => Select(false));
            liveButton.onClick.AddListener(() => Select(true));
            Refresh();
        }

        public void Select(bool live)
        {
            showUnsupported = live && !LiveVoiceController.IsSupported;
            if (liveVoice != null) liveVoice.SetRequested(live && LiveVoiceController.IsSupported);
            Refresh();
        }

        private void Refresh()
        {
            bool live = liveVoice != null && liveVoice.Requested;
            UiTheme.SetChoice(textButton, !live, false);
            UiTheme.SetChoice(liveButton, live, false);
            if (note != null)
            {
                note.text = showUnsupported ? UnsupportedNote : live ? LiveNote : TextNote;
                note.color = live ? UiTheme.CeladonDeep : UiTheme.InkMuted;
            }
        }
    }
}
