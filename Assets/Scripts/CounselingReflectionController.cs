using System;
using System.Collections.Generic;
using System.IO;
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
            string summary)
        {
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
                Text label = timelineButtons[i].GetComponentInChildren<Text>();
                label.text = string.IsNullOrWhiteSpace(turns[i].selfAssessment)
                    ? $"{turns[i].turn}턴"
                    : $"{turns[i].turn} · {turns[i].skill}";
            }
        }

        private void SelectTurn(int index)
        {
            if (index < 0 || index >= turns.Count) return;
            selectedIndex = index;
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
                ? $"시스템 근거  {turn.coachingFeedback}"
                : "시스템 근거  자기평가 후 공개됩니다.";
            sceneDetailLabel.text =
                sceneHeading + "\n" +
                $"상담자  {turn.counselorUtterance}\n내담자  {turn.clientReply}\n" +
                evidence;
            assessmentStatusLabel.text = isAssessed
                ? $"나의 판단 · {turn.selfAssessment}"
                : "먼저 이 장면에 대한 자신의 판단을 선택하세요.";
        }

        private void SaveAssessment(string assessment)
        {
            if (selectedIndex < 0 || selectedIndex >= turns.Count) return;
            CounselingTurnSnapshot turn = turns[selectedIndex];
            turn.selfAssessment = assessment;
            CounselingSelfAssessmentRecord record = new CounselingSelfAssessmentRecord
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                caseId = caseDefinition == null ? "unknown" : caseDefinition.CaseId,
                trainingMode = mode.ToString(),
                sourceTurn = turn.turn,
                selfAssessment = assessment,
                skill = turn.skill,
                quality = turn.quality
            };
            File.AppendAllText(
                Path.Combine(Application.persistentDataPath, "counseling-self-assessments.jsonl"),
                JsonUtility.ToJson(record) + Environment.NewLine);
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
