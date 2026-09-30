using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class CounselingReflectionController : MonoBehaviour
    {
        [SerializeField] private CounselingSessionOrchestrator orchestrator;
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Text sceneDetailLabel;
        [SerializeField] private Text assessmentStatusLabel;
        [SerializeField] private Button effectiveButton;
        [SerializeField] private Button retryNeededButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button[] timelineButtons;

        private string sessionId = string.Empty;
        private readonly List<CounselingTurnSnapshot> turns = new List<CounselingTurnSnapshot>();
        private CounselingCaseDefinition caseDefinition;
        private TrainingMode mode;
        private string fullSummary;
        private int selectedIndex = -1;

        private void Awake()
        {
            effectiveButton.onClick.AddListener(() => SaveAssessment("잘된 장면"));
            retryNeededButton.onClick.AddListener(() => SaveAssessment("다시 연습 필요"));
            replayButton.onClick.AddListener(ReplaySelectedScene);
            for (int i = 0; i < timelineButtons.Length; i++)
            {
                int captured = i;
                timelineButtons[i].onClick.AddListener(() => SelectTurn(captured));
            }
        }

        public void Present(
            CounselingCaseDefinition selectedCase,
            TrainingMode trainingMode,
            IReadOnlyList<CounselingTurnSnapshot> sessionTurns,
            string summary,
            string sessionIdentifier = "",
            int selectTurnIndex = 0)
        {
            sessionId = sessionIdentifier ?? string.Empty;
            caseDefinition = selectedCase;
            mode = trainingMode;
            fullSummary = summary;
            turns.Clear();
            for (int i = 0; i < sessionTurns.Count; i++) turns.Add(sessionTurns[i]);
            selectedIndex = turns.Count > 0 ? Mathf.Clamp(selectTurnIndex, 0, turns.Count - 1) : -1;
            RefreshTimeline();
            RefreshSelection();
            Canvas.ForceUpdateCanvases();
        }

        private void RefreshTimeline()
        {
            for (int i = 0; i < timelineButtons.Length; i++)
            {
                bool visible = i < turns.Count;
                timelineButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                bool assessed = !string.IsNullOrWhiteSpace(turns[i].selfAssessment);
                UiTheme.SetChoice(timelineButtons[i], i == selectedIndex, false, assessed);
                Text label = timelineButtons[i].GetComponentInChildren<Text>();
                string retried = turns[i].retryQuality >= 0 ? " ↻" : string.Empty;
                label.text = assessed
                    ? $"{turns[i].turn} · {turns[i].skill}{retried}"
                    : $"{turns[i].turn}턴{retried}";
            }
        }

        private void SelectTurn(int index)
        {
            if (index < 0 || index >= turns.Count) return;
            selectedIndex = index;
            RefreshTimeline();
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            bool hasSelection = selectedIndex >= 0 && selectedIndex < turns.Count;
            effectiveButton.interactable = hasSelection;
            retryNeededButton.interactable = hasSelection;
            if (!hasSelection)
            {
                replayButton.interactable = false;
                summaryLabel.text = "연습이 종료되었습니다.\n장면을 선택하고 먼저 자신의 판단을 남겨주세요.";
                sceneDetailLabel.text = "완료된 상담자 응답이 없습니다. 브리핑으로 돌아가 새 연습을 시작하세요.";
                assessmentStatusLabel.text = "자기평가할 장면 없음";
                return;
            }

            CounselingTurnSnapshot turn = turns[selectedIndex];
            bool isAssessed = !string.IsNullOrWhiteSpace(turn.selfAssessment);
            bool allTurnsAssessed = true;
            for (int i = 0; i < turns.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(turns[i].selfAssessment)) continue;
                allTurnsAssessed = false;
                break;
            }
            replayButton.interactable = isAssessed;
            summaryLabel.text = allTurnsAssessed
                ? fullSummary
                : "연습이 종료되었습니다.\n장면을 선택하고 먼저 자신의 판단을 남겨주세요.";
            string sceneHeading = isAssessed
                ? $"{turn.turn}턴 · {turn.stage} · {turn.skill} · {turn.alignment}"
                : $"{turn.turn}턴 · 장면 기록";
            string evidence = isAssessed
                ? $"시스템 근거  {turn.coachingFeedback}" + CodingLine(turn)
                : "시스템 근거  자기평가 후 공개됩니다.";
            // Line prefixes stay plain ("상담자  ") so the English phrasebook can translate
            // them; only whole lines are wrapped in rich-text colour.
            // The exchange reads in conversational order: the client line the counselor answered,
            // the counselor's response, then the client's reaction (reviewer: pairs were unclear).
            string before = string.IsNullOrWhiteSpace(turn.clientPrompt)
                ? string.Empty
                : UiTheme.Colorize($"내담자(앞)  {turn.clientPrompt}", UiTheme.InkMutedHex) + "\n";
            sceneDetailLabel.text =
                $"<size=14>{UiTheme.Colorize(sceneHeading, UiTheme.CeladonDeepHex)}</size>\n\n" +
                before +
                $"상담자  {turn.counselorUtterance}\n내담자(반응)  {turn.clientReply}\n\n" +
                (isAssessed ? evidence + HintLines(turn) : UiTheme.Colorize(evidence, UiTheme.InkMutedHex)) +
                RetryLines(turn);
            // The chosen judgment reads as "recorded" (tonal), leaving solid green for the next action.
            UiTheme.SetChoice(effectiveButton, false, false, turn.selfAssessment == "잘된 장면");
            UiTheme.SetChoice(retryNeededButton, false, false, turn.selfAssessment == "다시 연습 필요");
            assessmentStatusLabel.text = isAssessed
                ? $"나의 판단 · {turn.selfAssessment}"
                : "먼저 이 장면에 대한 자신의 판단을 선택하세요.";
        }

        /// <summary>
        /// Content hints for replaying the scene: which cues in the client's line could be
        /// followed, and one alternative response (from the AI coder), or a rule-based hint.
        /// </summary>
        private static string HintLines(CounselingTurnSnapshot turn)
        {
            string text = string.Empty;
            if (turn.focusOptions != null && turn.focusOptions.Length > 0)
                text += $"\n주목할 단서  {string.Join(" · ", turn.focusOptions)}";
            if (!string.IsNullOrWhiteSpace(turn.alternativeResponse))
                text += $"\n다른 반응 예  “{turn.alternativeResponse}”";
            else if (turn.quality < 3)
                text += $"\n다시 연습 힌트  {RetryHint(turn.skillCode)}";
            return text;
        }

        private static string RetryLines(CounselingTurnSnapshot turn)
        {
            if (turn.retryQuality < 0) return string.Empty;
            return "\n\n" + UiTheme.Colorize($"재연습  원래 {turn.quality}/3 → 재시도 {turn.retryQuality}/3 · {turn.retrySkill}", UiTheme.CeladonDeepHex) +
                   $"\n상담자  {turn.retryUtterance}\n내담자(반응)  {turn.retryReply}";
        }

        private static string RetryHint(string code)
        {
            switch (code)
            {
                case CounselingCodebook.Advice:
                case CounselingCodebook.PrematureReassurance:
                    return "해결책이나 안심보다 먼저, 내담자가 말한 감정 한 가지를 짚어 반영해 보세요.";
                case CounselingCodebook.ClosedQuestion:
                case CounselingCodebook.WhyQuestion:
                    return "예·아니오나 이유를 묻기보다 '어떤 순간에…', '그때 어떠셨는지…'처럼 경험을 열어 보세요.";
                case CounselingCodebook.Reflection:
                    return "반영 뒤에 짧은 개방형 질문을 이어 내담자가 더 말할 공간을 만들어 보세요.";
                case CounselingCodebook.OpenQuestion:
                    return "질문 앞에 방금 들은 감정이나 의미를 한 문장으로 먼저 반영해 보세요.";
                case CounselingCodebook.Validation:
                    return "공감에 그치지 말고 내담자가 쓴 표현을 살려 구체적인 감정·상황을 반영해 보세요.";
                default:
                    return "내담자 진술 속 감정·생각·행동·관계 단서 중 하나를 골라 그 부분에 반응해 보세요.";
            }
        }

        private static string CodingLine(CounselingTurnSnapshot turn)
        {
            if (string.IsNullOrWhiteSpace(turn.skillRationale)) return string.Empty;
            string source = turn.codingSource == "llm" ? "AI 코딩" : "규칙 코딩";
            return $"\n코딩 근거  {turn.skillRationale} ({source})";
        }

        private void SaveAssessment(string assessment)
        {
            if (selectedIndex < 0 || selectedIndex >= turns.Count) return;
            CounselingTurnSnapshot turn = turns[selectedIndex];
            turn.selfAssessment = assessment;
            CounselingSelfAssessmentRecord record = new CounselingSelfAssessmentRecord
            {
                sessionId = sessionId,
                timestampUtc = DateTime.UtcNow.ToString("O"),
                caseId = caseDefinition == null ? "unknown" : caseDefinition.CaseId,
                trainingMode = mode.ToString(),
                sourceTurn = turn.turn,
                selfAssessment = assessment,
                skill = turn.skill,
                skillCode = turn.skillCode,
                codingSource = turn.codingSource,
                quality = turn.quality
            };
            LocalJsonlLog.Append("counseling-self-assessments.jsonl", record);
            RefreshTimeline();
            RefreshSelection();
        }

        private void ReplaySelectedScene()
        {
            if (selectedIndex < 0 || selectedIndex >= turns.Count) return;
            orchestrator.BeginSceneReplay(turns[selectedIndex]);
        }
    }
}
