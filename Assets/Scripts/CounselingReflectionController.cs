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
            string sessionIdentifier = "")
        {
            sessionId = sessionIdentifier ?? string.Empty;
            caseDefinition = selectedCase;
            mode = trainingMode;
            fullSummary = summary;
            turns.Clear();
            for (int i = 0; i < sessionTurns.Count; i++) turns.Add(sessionTurns[i]);
            selectedIndex = turns.Count > 0 ? 0 : -1;
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
                label.text = assessed
                    ? $"{turns[i].turn} · {turns[i].skill}"
                    : $"{turns[i].turn}턴";
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
            sceneDetailLabel.text =
                $"<size=14>{UiTheme.Colorize(sceneHeading, UiTheme.CeladonDeepHex)}</size>\n\n" +
                $"상담자  {turn.counselorUtterance}\n내담자  {turn.clientReply}\n\n" +
                (isAssessed ? evidence : UiTheme.Colorize(evidence, UiTheme.InkMutedHex));
            // The chosen judgment reads as "recorded" (tonal), leaving solid green for the next action.
            UiTheme.SetChoice(effectiveButton, false, false, turn.selfAssessment == "잘된 장면");
            UiTheme.SetChoice(retryNeededButton, false, false, turn.selfAssessment == "다시 연습 필요");
            assessmentStatusLabel.text = isAssessed
                ? $"나의 판단 · {turn.selfAssessment}"
                : "먼저 이 장면에 대한 자신의 판단을 선택하세요.";
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
