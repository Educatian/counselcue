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

        private readonly Dictionary<Text, string> koreanByText = new Dictionary<Text, string>();
        private readonly Dictionary<Text, string> koreanByDynamicText = new Dictionary<Text, string>();
        private readonly List<Text> dynamicTexts = new List<Text>();
        private bool useEnglish;

        private static readonly HashSet<string> DynamicKeys = new HashSet<string>
        {
            "WebcamStatus", "AuStatus", "SessionStatus", "StageLabel", "Alliance", "Feedback"
        };

        private static readonly Dictionary<string, string> EnglishByKey = new Dictionary<string, string>
        {
            { "SessionEyebrow", "COUNSELING PRACTICE  ·  1:1 INTAKE" },
            { "Privacy", "No video saved · on-device processing" },
            { "ZoomEyebrow", "OBSERVATION ZOOM" },
            { "ClientName", "CLIENT  ·  JIHYE KIM, 32" },
            { "Placeholder", "Enter your counseling response…" },
            { "PauseSession", "Pause" },
            { "EndSession", "End" },
            { "ZoomReset", "Reset" },
            { "SendButton", "Respond" },
            { "BriefingTitle", "Choose today's practice path" },
            { "BriefingCase", "Workplace anxiety · Jihye Kim, 32 · Intake" },
            { "BriefingBody", "Situation\nShe feels short of breath before work and worries that she may be weak.\n\nSession goals\n1. Build relational safety and explain the counseling structure.\n2. Explore experience with reflections and open questions.\n3. Protect response space without rushing to solutions.\n\n15 min · target 10 turns · webcam video not saved" },
            { "FullSessionLabel", "FULL SESSION · 15 MIN / TARGET 10 TURNS" },
            { "StartPractice", "Start coached practice" },
            { "StartEvaluation", "Start assessment mode" },
            { "FocusedLabel", "MICRO-SKILL PRACTICE · 3 MIN / TARGET 3 TURNS" },
            { "StartFocusOne", "Emotion reflection · 3 min" },
            { "StartFocusTwo", "Open questions · 3 min" },
            { "StartFocusThree", "Delivery alignment · 3 min" },
            { "PrivacyLine", "Webcam video is not saved · Practice duration is a pilot setting for user research." },
            { "PauseTitle", "Session paused" },
            { "PauseBody", "The timer and counseling input are paused.\nContinue from the same scene when you are ready." },
            { "ResumeSession", "Continue" },
            { "PauseEndSession", "End session" },
            { "DebriefTitle", "Reflect and retry" },
            { "TimelineHeading", "SCENE TIMELINE · SELECT A SCENE" },
            { "SceneDetail", "Select a scene to review the counselor response and system evidence." },
            { "AssessmentStatus", "Choose your own judgment first." },
            { "AssessEffective", "Effective scene" },
            { "AssessRetry", "Needs another try" },
            { "ReplaySelected", "Practice this scene again" },
            { "ReturnToBriefing", "Practice paths" },
            { "DebriefDisclaimer", "※ Compare system evidence only after self-assessment. Training feedback, not a clinical evaluation." }
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
                if (!DynamicKeys.Contains(key)) continue;
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

                if (ContainsKorean(text.text)) koreanByDynamicText[text] = text.text;
                text.text = TranslateDynamic(GetKey(text), koreanByDynamicText[text]);
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

        private static string TranslateDynamic(string key, string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
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
                .Replace("전달 정합", "Delivery alignment");
            translated = Regex.Replace(translated, "(\\d+)번째 교환", "Turn $1");
            return Regex.Replace(translated, "(\\d+)/(\\d+)턴", "$1/$2 turns");
        }
    }
}
