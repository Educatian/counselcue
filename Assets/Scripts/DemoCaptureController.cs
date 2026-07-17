using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    public sealed class DemoCaptureController : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            string capturePath = null;
            bool autoplay = false;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].StartsWith("--capture=", System.StringComparison.Ordinal))
                {
                    capturePath = arguments[i].Substring("--capture=".Length).Trim('"');
                }
                else if (arguments[i] == "--autoplay")
                {
                    autoplay = true;
                }
            }

            if (string.IsNullOrWhiteSpace(capturePath))
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(2f);
            if (autoplay)
            {
                InputField input = FindAnyObjectByType<InputField>();
                CounselingSessionController session = FindAnyObjectByType<CounselingSessionController>();
                if (input != null && session != null)
                {
                    input.text = "회사에 들어가는 순간부터 긴장되고 숨이 막히는 느낌이 드시는군요. 그때 가장 먼저 떠오르는 생각은 무엇인가요?";
                    session.Submit();
                    yield return new WaitForSecondsRealtime(1f);
                }
            }

            MaskWebcamPreview();
            string directory = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(capturePath);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!File.Exists(capturePath) && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        private static void MaskWebcamPreview()
        {
            RawImage[] rawImages = FindObjectsByType<RawImage>();
            foreach (RawImage rawImage in rawImages)
            {
                if (rawImage.name != "WebcamPreview") continue;
                rawImage.texture = null;
                rawImage.color = new Color(0.08f, 0.12f, 0.11f, 1f);
            }
        }
    }
}
