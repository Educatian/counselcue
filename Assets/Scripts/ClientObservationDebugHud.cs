using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class ClientObservationDebugHud : MonoBehaviour
    {
        [SerializeField] private ClientAvatarHost client;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text diagnosticsLabel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button cycleGazeButton;

        private void Awake()
        {
            toggleButton?.onClick.AddListener(Toggle);
            cycleGazeButton?.onClick.AddListener(() => client?.CycleDebugGaze());
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7)) Toggle();
            if (panel == null || !panel.activeSelf || diagnosticsLabel == null || client == null) return;
            diagnosticsLabel.text =
                $"시선 상태  {client.GazeStateLabel}\n" +
                $"LookAt 가중치  {client.GazeContactWeight:0.00}\n" +
                $"얼굴 blendshape  {client.FacialBlendShapeCount}\n" +
                $"활성 단서  {client.ActiveFacialCue}\n" +
                "F7: 닫기 · 시선 전환 버튼으로 상태 확인";
        }

        public void Toggle()
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }
    }
}
