using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Briefing-card controls for research data: an opt-in consent toggle for local JSONL
    /// logging (off by default) and a button that deletes every local record file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResearchDataControls : MonoBehaviour
    {
        [SerializeField] private Toggle consentToggle;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Text statusLabel;

        private void Awake()
        {
            if (consentToggle != null)
            {
                consentToggle.SetIsOnWithoutNotify(LocalJsonlLog.ConsentGranted);
                consentToggle.onValueChanged.AddListener(OnConsentChanged);
            }
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteLocalRecords);
            ShowStoredState();
        }

        private void OnConsentChanged(bool granted)
        {
            LocalJsonlLog.ConsentGranted = granted;
            SetStatus(granted
                ? "연구용 로컬 기록 사용 · 응답 텍스트와 파생 신호만 이 기기에 저장됩니다."
                : "연구용 로컬 기록 꺼짐 · 새 기록을 남기지 않습니다.");
        }

        private void DeleteLocalRecords()
        {
            int deleted = LocalJsonlLog.DeleteAll();
            if (deleted < 0) SetStatus("일부 로컬 기록을 삭제하지 못했습니다. 다시 시도해 주세요.");
            else if (deleted == 0) SetStatus("삭제할 로컬 기록이 없습니다.");
            else SetStatus($"로컬 기록 파일 {deleted}개를 삭제했습니다.");
        }

        private void ShowStoredState()
        {
            int files = LocalJsonlLog.CountFiles();
            string consent = LocalJsonlLog.ConsentGranted ? "연구용 로컬 기록 사용" : "연구용 로컬 기록 꺼짐";
            SetStatus(files > 0 ? $"{consent} · 이 기기에 기록 파일 {files}개" : consent);
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null) statusLabel.text = value;
        }
    }
}
