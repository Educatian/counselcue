using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Briefing-card controls for research data: an opt-in consent toggle for local JSONL
    /// logging (off by default), an optional learner code, an export for the instructor
    /// dashboard, and a button that deletes every local record file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResearchDataControls : MonoBehaviour
    {
        [SerializeField] private Toggle consentToggle;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private InputField learnerCodeInput;
        [SerializeField] private Text statusLabel;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CounselCueWeb_Download(string fileName, string text);
#endif

        private void Awake()
        {
            if (consentToggle != null)
            {
                consentToggle.SetIsOnWithoutNotify(LocalJsonlLog.ConsentGranted);
                consentToggle.onValueChanged.AddListener(OnConsentChanged);
            }
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteLocalRecords);
            if (exportButton != null) exportButton.onClick.AddListener(ExportRecords);
            if (learnerCodeInput != null)
            {
                learnerCodeInput.SetTextWithoutNotify(LocalJsonlLog.LearnerCode);
                learnerCodeInput.onEndEdit.AddListener(value => LocalJsonlLog.LearnerCode = value);
            }
            ShowStoredState();
        }

        private void OnConsentChanged(bool granted)
        {
            LocalJsonlLog.ConsentGranted = granted;
            SetStatus(granted
                ? "연구용 로컬 기록 사용 · 응답 텍스트와 파생 신호만 이 기기에 저장됩니다."
                : "연구용 로컬 기록 꺼짐 · 새 기록을 남기지 않습니다.");
        }

        private void ExportRecords()
        {
            if (learnerCodeInput != null) LocalJsonlLog.LearnerCode = learnerCodeInput.text;
            string bundle = LocalJsonlLog.BuildExportBundle();
            if (bundle == null)
            {
                SetStatus("내보낼 기록이 없습니다. 기록에 동의한 뒤 연습하면 기록이 쌓입니다.");
                return;
            }
            string fileName = LocalJsonlLog.ExportFileName();
#if UNITY_WEBGL && !UNITY_EDITOR
            CounselCueWeb_Download(fileName, bundle);
            SetStatus($"기록 파일을 내려받았습니다 · {fileName} · 강사에게 전달하세요.");
#else
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "exports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                File.WriteAllText(path, bundle);
                SetStatus($"기록 파일을 저장했습니다 · {fileName} · 강사에게 전달하세요.");
                Debug.Log($"COUNSELCUE_EXPORT_WRITTEN {path}");
                if (!Application.isBatchMode) Application.OpenURL("file://" + folder.Replace('\\', '/'));
            }
            catch (Exception exception)
            {
                SetStatus("기록 파일을 저장하지 못했습니다. 저장 공간과 권한을 확인해 주세요.");
                Debug.LogWarning($"CounselCue export failed: {exception.Message}");
            }
#endif
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
