using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingLanguageToggle : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private CounselingSessionOrchestrator orchestrator;

        /// <summary>Raised after the UI language changes (true = English).</summary>
        public static event System.Action<bool> LanguageChanged;

        private readonly Dictionary<Text, string> koreanByText = new Dictionary<Text, string>();
        private readonly Dictionary<Text, string> koreanByDynamicText = new Dictionary<Text, string>();
        private readonly List<Text> dynamicTexts = new List<Text>();
        private readonly Dictionary<Text, string> lastTranslation = new Dictionary<Text, string>();
        private bool useEnglish;

        private static readonly HashSet<string> DynamicKeys = new HashSet<string>
        {
            "WebcamStatus", "AuStatus", "SessionStatus", "StageLabel", "Alliance", "Feedback", "DataStatus",
            "DebriefTitle", "DebriefSummary", "SceneDetail", "AssessmentStatus"
        };

        private static readonly Dictionary<string, string> EnglishByKey = new Dictionary<string, string>
        {
            { "SessionEyebrow", "COUNSELING PRACTICE  ·  1:1 INTAKE" },
            { "Privacy", "No video saved · on-device processing" },
            { "ZoomEyebrow", "OBSERVATION ZOOM" },
            { "Placeholder", "Enter your counseling response…" },
            { "PauseSession", "Pause" },
            { "EndSession", "End" },
            { "ZoomReset", "Reset" },
            { "SendButton", "Respond" },
            { "BriefingTitle", "Choose today's practice path" },
            { "FullSessionLabel", "FULL SESSION · 15 MIN / TARGET 10 TURNS" },
            { "BriefingPortraitCaption", "AI-generated case illustration" },
            { "ConsentLabel", "I agree to local research logging (text and derived signals on this device; no video)" },
            { "DeleteLocalData", "Delete local records" },
            { "StartPractice", "Start coached practice" },
            { "StartEvaluation", "Start assessment mode" },
            { "FocusedLabel", "MICRO-SKILL PRACTICE · 3 MIN / TARGET 3 TURNS" },
            { "PrivacyLine", "Webcam video is not saved · Practice duration is a pilot setting for user research." },
            { "PauseTitle", "Session paused" },
            { "PauseBody", "The timer and counseling input are paused.\nContinue from the same scene when you are ready." },
            { "ResumeSession", "Continue" },
            { "PauseEndSession", "End session" },
            { "TimelineHeading", "SCENE TIMELINE · SELECT A SCENE" },
            { "AssessEffective", "Effective scene" },
            { "AssessRetry", "Needs another try" },
            { "ReplaySelected", "Practice this scene again" },
            { "ReturnToBriefing", "Practice paths" },
            { "DebriefDisclaimer", "※ Compare system evidence only after self-assessment. Training feedback, not a clinical evaluation.\nIf practice left you uneasy, take a break and talk with your supervisor or instructor." }
        };

        private void Awake()
        {
            toggleButton.onClick.AddListener(ToggleLanguage);
            Canvas canvas = toggleButton.GetComponentInParent<Canvas>();
            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            foreach (Text text in texts)
            {
                string key = GetKey(text);
                if (EnglishByKey.ContainsKey(key)) koreanByText[text] = text.text;
                if (!DynamicKeys.Contains(key) && !key.StartsWith("TimelineTurn")) continue;
                dynamicTexts.Add(text);
                koreanByDynamicText[text] = text.text;
            }
            RefreshToggleLabel();
        }

        private void LateUpdate()
        {
            foreach (Text text in dynamicTexts)
            {
                if (!useEnglish)
                {
                    koreanByDynamicText[text] = text.text;
                    continue;
                }

                // A new source is anything other than our own last output. Utterances inside a
                // translated scene stay Korean, so "contains Korean" alone would mistake our
                // output for a new source and later restore half-English text.
                if (!lastTranslation.TryGetValue(text, out string previous) || text.text != previous)
                    koreanByDynamicText[text] = text.text;
                string translated = TranslateDynamic(GetKey(text), koreanByDynamicText[text]);
                text.text = translated;
                lastTranslation[text] = translated;
            }
        }

        private void ToggleLanguage()
        {
            useEnglish = !useEnglish;
            foreach (KeyValuePair<Text, string> entry in koreanByText)
            {
                string key = GetKey(entry.Key);
                entry.Key.text = useEnglish ? EnglishByKey[key] : entry.Value;
            }
            if (!useEnglish)
            {
                foreach (KeyValuePair<Text, string> entry in koreanByDynamicText) entry.Key.text = entry.Value;
            }
            lastTranslation.Clear();
            // Case title, client name, briefing body, case and focus buttons depend on the
            // selected case, so the orchestrator renders them instead of a fixed string table.
            if (orchestrator != null) orchestrator.SetEnglish(useEnglish);
            LanguageChanged?.Invoke(useEnglish);
            RefreshToggleLabel();
        }

        private void RefreshToggleLabel()
        {
            Text label = toggleButton.GetComponentInChildren<Text>();
            label.text = useEnglish ? "UI: KO" : "UI: EN";
        }

        private static string GetKey(Text text)
        {
            return text.name == "Label" && text.transform.parent != null
                ? text.transform.parent.name
                : text.name;
        }

        private static bool ContainsKorean(string value)
        {
            return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "[가-힣]");
        }

        private string TranslateDynamic(string key, string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            CounselingCaseDefinition activeCase = orchestrator == null ? null : orchestrator.ActiveCase;
            if (activeCase != null && !string.IsNullOrEmpty(activeCase.CaseTitle))
                source = source.Replace(activeCase.CaseTitle, activeCase.LocalizedTitle(true));
            if (key == "SceneDetail") return UiPhrasebook.TranslateScene(source);
            if (key == "DebriefTitle" || key == "DebriefSummary" || key == "AssessmentStatus" || key.StartsWith("TimelineTurn"))
                return UiPhrasebook.Translate(source);
            if (key == "WebcamStatus")
            {
                if (source == "웹캠 준비 중") return "Webcam starting";
                if (source == "영상 신호 낮음 · 조명을 확인하세요") return "Video signal low · Check lighting";
                return source.Replace("웹캠 신호 양호 · 안정성", "Webcam signal good · Stability");
            }
            if (key == "AuStatus")
            {
                if (source == "AU 분석 대기 · 선택 기능") return "AU analysis idle · Optional";
                if (source == "얼굴을 찾는 중 · 정면을 봐주세요") return "Finding face · Look toward the camera";
                return source.Replace("중립 보정", "Neutral calibration").Replace("표정을 편안하게", "Relax your expression");
            }
            if (key == "DataStatus")
            {
                string data = source
                    .Replace("연구용 로컬 기록 사용", "Local research logging on")
                    .Replace("연구용 로컬 기록 꺼짐", "Local research logging off")
                    .Replace("응답 텍스트와 파생 신호만 이 기기에 저장됩니다.", "only response text and derived signals are stored on this device.")
                    .Replace("새 기록을 남기지 않습니다.", "no new records are written.")
                    .Replace("일부 로컬 기록을 삭제하지 못했습니다. 다시 시도해 주세요.", "Some local records could not be deleted. Please try again.")
                    .Replace("삭제할 로컬 기록이 없습니다.", "There are no local records to delete.");
                data = Regex.Replace(data, "이 기기에 기록 파일 (\\d+)개", "$1 record file(s) on this device");
                return Regex.Replace(data, "로컬 기록 파일 (\\d+)개를 삭제했습니다\\.", "Deleted $1 local record file(s).");
            }
            if (key == "Alliance")
            {
                return source.Replace("안전", "Safety").Replace("경계", "Guarded").Replace("공개", "Disclosure");
            }
            if (key == "Feedback")
            {
                if (source == "세션을 시작하면 상담자의 언어 기술과 비언어 전달을 함께 관찰합니다.")
                    return "Start a session to observe counseling language and embodied delivery together.";
                if (source == "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요.")
                    return "Reflect emotion and leave space for the client to elaborate.";
                if (source == "평가 모드 · 세션 종료 후 전달 피드백을 확인합니다.")
                    return "Assessment · Delivery feedback appears after the session.";
                if (source == "GPT 내담자 연결 중…") return "Connecting to the GPT client…";
                if (source == "응답 처리 중 문제가 발생했습니다. 다시 시도해 주세요.") return "The response could not be processed. Please try again.";
                return UiPhrasebook.Translate(source);
            }

            string translated = source
                .Replace("직장 불안", "Workplace anxiety")
                .Replace("연습 모드", "Practice")
                .Replace("평가 모드", "Assessment")
                .Replace("집중연습", "Focused practice")
                .Replace("장면 재연습", "Scene replay")
                .Replace("관계 형성", "Rapport")
                .Replace("초기 탐색", "Initial exploration")
                .Replace("감정 심화", "Emotional deepening")
                .Replace("핵심 탐색", "Core exploration")
                .Replace("정리", "Consolidation")
                .Replace("종결", "Closing")
                .Replace("감정 반영", "Emotion reflection")
                .Replace("개방형 질문", "Open questions")
                .Replace("전달 정합", "Delivery alignment")
                .Replace(" 연습 ·", " practice ·")
                .Replace("3분", "3 min");
            translated = Regex.Replace(translated, "(\\d+)번째 교환", "Turn $1");
            return Regex.Replace(translated, "(\\d+)/(\\d+)턴", "$1/$2 turns");
        }
    }
}
