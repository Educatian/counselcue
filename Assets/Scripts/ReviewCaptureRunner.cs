#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Editor-only Play-mode runner started by Tools → CounselCue → Review → Capture Review
    /// Screenshots. It walks the briefing (all cases, Korean and English), a live turn, and the
    /// debrief, saving Game-view captures to Screenshots/review/. It never runs in builds.
    /// </summary>
    public sealed class ReviewCaptureRunner : MonoBehaviour
    {
        public const string FlagPath = "Temp/counselcue-review-capture.flag";
        private const string OutputFolder = "Screenshots/review";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            if (!File.Exists(FlagPath)) return;
            File.Delete(FlagPath);
            GameObject runner = new GameObject("ReviewCaptureRunner");
            runner.AddComponent<ReviewCaptureRunner>();
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(OutputFolder);
            yield return new WaitForSecondsRealtime(2.5f);
            CounselingSessionOrchestrator orchestrator = FindAnyObjectByType<CounselingSessionOrchestrator>();
            int caseCount = 5;
            for (int i = 0; i < caseCount; i++)
            {
                orchestrator?.SelectCase(i);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture($"01-briefing-case{i + 1}-ko");
            }

            orchestrator?.SelectCase(1);
            Click("LanguageToggle");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("02-briefing-case2-en");
            Click("LanguageToggle");
            orchestrator?.SelectCase(0);
            yield return new WaitForSecondsRealtime(0.5f);

            Click("StartPractice");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("03-session-start-ko");
            Click("FaceObservation");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("04-face-observation");
            Click("ZoomReset");
            yield return new WaitForSecondsRealtime(0.8f);

            InputField input = FindAnyObjectByType<InputField>();
            CounselingSessionController session = FindAnyObjectByType<CounselingSessionController>();
            if (input != null && session != null)
            {
                input.text = "회사에 들어가는 순간부터 숨이 막히는 느낌이 드시는군요. 그때 어떤 생각이 가장 먼저 드세요?";
                session.Submit();
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Capture("05-after-reflection-turn-ko");
                input.text = "시간이 지나면 괜찮아질 거예요.";
                session.Submit();
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Capture("06-after-reassurance-turn-ko");
            }

            Click("EndSession");
            yield return new WaitForSecondsRealtime(0.8f);
            Click("TimelineTurn1");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("07-debrief-before-self-assessment");
            Click("AssessEffective");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("08-debrief-evidence-revealed");
            Click("LanguageToggle");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("09-debrief-en");
            Click("LanguageToggle");

            Debug.Log($"COUNSELCUE_REVIEW_CAPTURE_DONE folder={Path.GetFullPath(OutputFolder)}");
            UnityEditor.EditorApplication.isPlaying = false;
        }

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name + ".png"));
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static void Click(string buttonName)
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name != buttonName || !button.gameObject.activeInHierarchy || !button.interactable) continue;
                button.onClick.Invoke();
                return;
            }
            Debug.LogWarning($"Review capture: button {buttonName} not available.");
        }
    }
}
#endif
