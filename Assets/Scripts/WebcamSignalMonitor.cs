using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class WebcamSignalMonitor : MonoBehaviour
    {
        [SerializeField] private RawImage preview;
        [SerializeField] private Text statusLabel;
        [SerializeField, Min(0.05f)] private float sampleInterval = 0.2f;
        [SerializeField, Range(4, 20)] private int sampleGrid = 8;

        private WebCamTexture cameraTexture;
        private Color32[] pixels;
        private float[] previousSamples;
        private float nextSampleTime;

        public float SignalQuality { get; private set; }
        public float Movement { get; private set; }
        public bool IsActive => cameraTexture != null && cameraTexture.isPlaying;

        private void Start()
        {
            if (FacialActionUnitMonitor.IsRequestedFromCommandLine())
            {
                SetStatus("AU 브리지 사용 ·\n원시 영상 미저장");
                return;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                SetStatus("웹캠 없음 · 언어 신호만 사용");
                return;
            }

            cameraTexture = new WebCamTexture(devices[0].name, 640, 360, 15);
            cameraTexture.Play();
            if (preview != null)
            {
                preview.texture = cameraTexture;
                preview.gameObject.SetActive(true);
            }

            SetStatus("웹캠 보정 중 · 로컬 처리");
        }

        private void Update()
        {
            if (!IsActive || !cameraTexture.didUpdateThisFrame || Time.unscaledTime < nextSampleTime)
            {
                return;
            }

            nextSampleTime = Time.unscaledTime + sampleInterval;
            AnalyzeFrame();
        }

        private void OnDestroy()
        {
            if (IsActive)
            {
                cameraTexture.Stop();
            }
        }

        private void AnalyzeFrame()
        {
            int width = cameraTexture.width;
            int height = cameraTexture.height;
            if (width < 32 || height < 32)
            {
                return;
            }

            int required = width * height;
            if (pixels == null || pixels.Length != required)
            {
                pixels = new Color32[required];
                previousSamples = new float[sampleGrid * sampleGrid];
            }

            cameraTexture.GetPixels32(pixels);
            float luminanceSum = 0f;
            float motionSum = 0f;
            int index = 0;
            for (int row = 0; row < sampleGrid; row++)
            {
                int y = Mathf.Clamp((row * 2 + 1) * height / (sampleGrid * 2), 0, height - 1);
                for (int column = 0; column < sampleGrid; column++)
                {
                    int x = Mathf.Clamp((column * 2 + 1) * width / (sampleGrid * 2), 0, width - 1);
                    Color32 color = pixels[y * width + x];
                    float luminance = (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) / 255f;
                    luminanceSum += luminance;
                    motionSum += Mathf.Abs(luminance - previousSamples[index]);
                    previousSamples[index++] = luminance;
                }
            }

            float meanLuminance = luminanceSum / index;
            float illumination = 1f - Mathf.Clamp01(Mathf.Abs(meanLuminance - 0.48f) / 0.48f);
            float movement = Mathf.Clamp01((motionSum / index) * 7f);
            SignalQuality = Mathf.Lerp(SignalQuality, illumination, 0.24f);
            Movement = Mathf.Lerp(Movement, movement, 0.22f);
            SetStatus(SignalQuality < 0.35f
                ? "영상 신호 낮음 · 조명을 확인하세요"
                : $"웹캠 신호 양호 · 안정성 {Mathf.RoundToInt((1f - Movement) * 100f)}%");
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
            {
                statusLabel.text = value;
            }
        }
    }
}
