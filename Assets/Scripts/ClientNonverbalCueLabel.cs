using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// A quiet subtitle-style note of the client's salient nonverbal behavior ("손을 비비며 ·
    /// 시선을 아래로"). Reviewer feedback: trainees could not tell which client nonverbal cues were
    /// there to reflect. The note changes only when the behavior changes and fades in and out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientNonverbalCueLabel : MonoBehaviour
    {
        [SerializeField] private ClientAvatarHost client;
        [SerializeField] private Text label;
        [SerializeField] private float holdSeconds = 0.6f;

        private string shown = string.Empty;
        private string pending = string.Empty;
        private float pendingSince;
        private bool english;

        public void Configure(ClientAvatarHost host, Text cueLabel)
        {
            client = host;
            label = cueLabel;
        }

        private void OnEnable() => CounselingLanguageToggle.LanguageChanged += OnLanguage;
        private void OnDisable() => CounselingLanguageToggle.LanguageChanged -= OnLanguage;
        private void OnLanguage(bool value) { english = value; shown = null; }

        private void Update()
        {
            if (client == null || label == null) return;
            string next = Describe();
            if (next != pending)
            {
                pending = next;
                pendingSince = Time.unscaledTime;
            }
            // Wait briefly so a passing gesture does not flicker the note.
            if (pending != shown && Time.unscaledTime - pendingSince >= holdSeconds)
            {
                shown = pending;
                label.text = shown;
            }
            Color color = label.color;
            color.a = Mathf.MoveTowards(color.a, string.IsNullOrEmpty(shown) ? 0f : 1f, Time.unscaledDeltaTime * 3f);
            label.color = color;
        }

        private string Describe()
        {
            ClientGestureController gestures = client.Gestures;
            string gesture = gestures == null ? string.Empty : Gesture(gestures.ActiveGesture);
            string gaze = Gaze(client.GazeStateLabel);
            if (gesture.Length > 0 && gaze.Length > 0) return $"{gesture} · {gaze}";
            return gesture.Length > 0 ? gesture : gaze;
        }

        private string Gesture(string key)
        {
            switch (key)
            {
                case "rest:HandsClasped": return english ? "hands folded" : "손을 모으고 있음";
                case "rest:ArmsFolded": return english ? "arms folded" : "팔짱을 낌";
                case "rest:ForearmsCrossedLow": return english ? "arms held close" : "팔을 모아 몸을 감쌈";
                case "rest:CheekOnHand": return english ? "cheek resting on a hand" : "턱을 괴고 있음";
                case "rest:ChinOnFist": return english ? "chin on fist" : "주먹을 턱에 댐";
                case "rest:LeanInClasped": return english ? "leaning forward" : "몸을 앞으로 기울임";
                case "rest:HandsOnKnees": return english ? "hands on knees" : "무릎에 손을 올림";
                case "adaptor:HandRub": return english ? "rubbing hands" : "손을 비빔";
                case "adaptor:Wring": return english ? "wringing hands" : "손을 쥐었다 폈다 함";
                case "adaptor:ThighRub": return english ? "rubbing a thigh" : "허벅지를 문지름";
                case "adaptor:NeckTouch": return english ? "touching the neck" : "목을 만짐";
                case "adaptor:FaceTouch": return english ? "touching the face" : "얼굴을 만짐";
                case "adaptor:ForeheadRub": return english ? "rubbing the forehead" : "이마를 짚음";
                case "adaptor:HandToChest": return english ? "hand on chest" : "가슴에 손을 얹음";
                case "adaptor:HairTuck": return english ? "touching hair" : "머리카락을 넘김";
                case "illustrator:shrug": return english ? "shrugging" : "어깨를 으쓱함";
                case "illustrator:self": return english ? "pointing to self" : "자신을 가리킴";
                case "illustrator:mouth-cover": return english ? "covering the mouth" : "입을 가리고 웃음";
                default: return string.Empty;
            }
        }

        private string Gaze(string state)
        {
            switch (state)
            {
                case nameof(ClientGazeState.BriefAvert): return english ? "looking away" : "시선을 피함";
                case nameof(ClientGazeState.DownwardReflection): return english ? "looking down" : "시선을 아래로";
                case nameof(ClientGazeState.RecallSearch): return english ? "searching for words" : "생각을 더듬음";
                default: return string.Empty;
            }
        }
    }
}
