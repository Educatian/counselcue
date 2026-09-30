using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// One-click review helpers: run every in-editor check without quitting the editor, and
    /// capture a fixed set of review screenshots in Play mode.
    /// </summary>
    public static class CounselCueReviewTools
    {
        private const string ScenePath = "Assets/Scenes/KoreanCounselingRoom.unity";

        [MenuItem("Tools/CounselCue/Review/Run All Checks")]
        public static void RunAllChecks()
        {
            List<string> failed = new List<string>();
            int passed = 0;
            void Check(string name, Action run)
            {
                try { run(); passed++; }
                catch (Exception exception) { failed.Add(name); Debug.LogException(exception); }
            }

            Check("Relational delivery", RelationalDeliveryModelChecks.RunFromMenu);
            Check("Response evaluator", () =>
            {
                CounselingResponseEvaluatorChecks.RunFromMenu();
                if (CounselingResponseEvaluatorChecks.LastFailureCount > 0) throw new InvalidOperationException("Response evaluator fixtures failed.");
            });
            Check("Camera zoom", CounselingCameraZoomChecks.RunFromMenu);
            Check("Animation", CounselingAnimationChecks.RunFromMenu);
            Check("Asset pack", CounselCueAssetPackChecks.Run);
            Check("Session flow", CounselingSessionFlowChecks.RunFromMenu);
            // Builds and saves the scene, so it runs last.
            Check("Sprint 1-3 + scene build", CounselCueSprintChecks.RunFromMenu);

            string summary = $"COUNSELCUE_ALL_CHECKS pass={passed} fail={failed.Count}" + (failed.Count > 0 ? " failed=" + string.Join(", ", failed) : string.Empty);
            if (failed.Count == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        [MenuItem("Tools/CounselCue/Review/Capture Review Screenshots")]
        public static void CaptureReviewScreenshots()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ReviewCaptureRunner.FlagPath));
            File.WriteAllText(ReviewCaptureRunner.FlagPath, DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }
    }
}
