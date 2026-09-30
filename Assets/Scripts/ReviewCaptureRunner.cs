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
            // A clean room plate (no HUD) for the web loading screen and social card.
            Canvas[] hudCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in hudCanvases) canvas.enabled = false;
            yield return Capture("00-room-clean");
            // Client line-up: every case's avatar in the chair, plus a face close-up.
            Camera main = Camera.main;
            ClientAvatarHost host = FindAnyObjectByType<ClientAvatarHost>();
            CounselingCameraZoom zoom = FindAnyObjectByType<CounselingCameraZoom>();
            for (int i = 0; i < 5 && orchestrator != null && main != null && host != null; i++)
            {
                orchestrator.SelectCase(i);
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Capture($"12-client-case{i + 1}");
                if (!host.TryGetObservationAnchors(out _, out Vector3 face)) continue;
                Vector3 position = main.transform.position;
                Quaternion rotation = main.transform.rotation;
                float fov = main.fieldOfView;
                if (zoom != null) zoom.enabled = false;
                main.transform.rotation = Quaternion.LookRotation(face - position);
                main.fieldOfView = CounselingCameraZoom.CloseFieldOfView * 0.6f;
                yield return Capture($"13-face-case{i + 1}");
                main.transform.SetPositionAndRotation(position, rotation);
                main.fieldOfView = fov;
                if (zoom != null) zoom.enabled = true;
            }
            foreach (Canvas canvas in hudCanvases) canvas.enabled = true;
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
            Click("PauseSession");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("10-pause-ko");
            Click("ResumeSession");
            yield return new WaitForSecondsRealtime(0.4f);
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
                // A simulated Gemini Live exchange (the editor has no browser audio): the same
                // pipeline codes the transcript, updates the relational model and records it.
                session.SubmitLiveTurn("그 순간 어떤 느낌이 드셨는지 조금 더 들려주시겠어요?", "…음, 사실 회의실에 들어가기 전부터 손이 떨려요.", false);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("11-live-turn-simulated-ko");
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

        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        /// <summary>
        /// Renders the camera and the HUD into a 1920×1080 target so review shots show the
        /// real 16:9 layout regardless of the editor's Game view size.
        /// </summary>
        private static IEnumerator Capture(string name)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, name + ".png"));
                yield return new WaitForSecondsRealtime(0.4f);
                yield break;
            }

            RenderTexture target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            System.Collections.Generic.List<Canvas> switched = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.05f;
                switched.Add(canvas);
            }
            camera.targetTexture = target;
            // Let CanvasScaler adopt the new pixel size, then re-rasterize text at that scale
            // (otherwise glyphs baked at the small Game-view scale are stretched and blurry).
            yield return null;
            foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), image.EncodeToPNG());

            camera.targetTexture = null;
            foreach (Canvas canvas in switched)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
            target.Release();
            Destroy(target);
            Destroy(image);
            yield return new WaitForSecondsRealtime(0.2f);
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
