using System.Collections;
using System.Globalization;
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
            bool autoplayAdvice = false;
            float captureDelay = 2f;
            int zoomButtonClicks = 0;
            string sessionState = "active";
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
                else if (arguments[i] == "--autoplay-advice")
                {
                    autoplay = true;
                    autoplayAdvice = true;
                }
                else if (arguments[i].StartsWith("--capture-delay=", System.StringComparison.Ordinal) &&
                         float.TryParse(arguments[i].Substring("--capture-delay=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedDelay))
                {
                    captureDelay = Mathf.Max(0f, parsedDelay);
                }
                else if (arguments[i] == "--zoom=close")
                {
                    zoomButtonClicks = 4;
                }
                else if (arguments[i] == "--zoom=wide")
                {
                    zoomButtonClicks = -4;
                }
                else if (arguments[i].StartsWith("--session=", System.StringComparison.Ordinal))
                {
                    sessionState = arguments[i].Substring("--session=".Length).ToLowerInvariant();
                }
            }

            if (string.IsNullOrWhiteSpace(capturePath))
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(captureDelay);
            if (sessionState != "briefing")
            {
                InvokeButton(sessionState.StartsWith("evaluation", System.StringComparison.Ordinal) ? "StartEvaluation" : "StartPractice");
                yield return new WaitForSecondsRealtime(0.3f);
            }

            if (zoomButtonClicks != 0)
            {
                string targetName = zoomButtonClicks > 0 ? "ZoomIn" : "ZoomOut";
                for (int click = 0; click < Mathf.Abs(zoomButtonClicks); click++) InvokeButton(targetName);

                yield return new WaitForSecondsRealtime(0.6f);
            }

            if (sessionState.StartsWith("cancel-", System.StringComparison.Ordinal))
            {
                StartSubmission();
                yield return new WaitForSecondsRealtime(0.15f);
                if (sessionState == "cancel-resume")
                {
                    InvokeButton("PauseSession");
                    InvokeButton("ResumeSession");
                }
                else if (sessionState == "cancel-end")
                {
                    InvokeButton("EndSession");
                }
                else if (sessionState == "cancel-restart")
                {
                    InvokeButton("EndSession");
                    InvokeButton("ReturnToBriefing");
                    InvokeButton("StartPractice");
                }

                yield return new WaitForSecondsRealtime(2.3f);
            }
            else if (autoplay)
            {
                InputField input = FindAnyObjectByType<InputField>();
                CounselingSessionController session = FindAnyObjectByType<CounselingSessionController>();
                if (input != null && session != null)
                {
                    input.text = autoplayAdvice
                        ? "그런 생각은 잊고 매일 운동하면서 긍정적으로 생각하세요."
                        : "회사에 들어가는 순간부터 긴장되고 숨이 막히는 느낌이 드시는군요. 그때 가장 먼저 떠오르는 생각은 무엇인가요?";
                    session.Submit();
                    yield return new WaitForSecondsRealtime(1f);
                }
            }

            if (sessionState == "paused")
            {
                InvokeButton("PauseSession");
                yield return new WaitForSecondsRealtime(0.3f);
            }
            else if (sessionState == "debrief" || sessionState == "evaluation-debrief")
            {
                InvokeButton("PauseSession");
                yield return new WaitForSecondsRealtime(0.15f);
                InvokeButton("ResumeSession");
                yield return new WaitForSecondsRealtime(0.15f);
                InvokeButton("EndSession");
                yield return new WaitForSecondsRealtime(0.3f);
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

        private static bool InvokeButton(string buttonName)
        {
            Button[] buttons = FindObjectsByType<Button>();
            foreach (Button button in buttons)
            {
                if (button.name != buttonName || !button.gameObject.activeInHierarchy) continue;
                button.onClick.Invoke();
                return true;
            }

            return false;
        }

        private static void StartSubmission()
        {
            InputField input = FindAnyObjectByType<InputField>();
            CounselingSessionController session = FindAnyObjectByType<CounselingSessionController>();
            if (input == null || session == null) return;
            input.text = "회사에 들어가는 순간부터 긴장되고 숨이 막히는 느낌이 드시는군요. 그때 가장 먼저 떠오르는 생각은 무엇인가요?";
            session.Submit();
        }
    }
}
