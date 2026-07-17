using System;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselingCameraZoomChecks
    {
        [MenuItem("Tools/CounselCue/Run Camera Zoom Checks")]
        public static void RunFromMenu()
        {
            RunChecks();
            Debug.Log("Camera zoom checks passed.");
        }

        public static void RunFromCommandLine()
        {
            RunChecks();
            Debug.Log("Camera zoom checks passed.");
        }

        private static void RunChecks()
        {
            Require(CounselingCameraZoom.ClampFieldOfView(10f) == CounselingCameraZoom.CloseFieldOfView, "Close zoom limit failed.");
            Require(CounselingCameraZoom.ClampFieldOfView(70f) == CounselingCameraZoom.WideFieldOfView, "Wide zoom limit failed.");
            Require(CounselingCameraZoom.ClampFieldOfView(CounselingCameraZoom.DefaultFieldOfView) == CounselingCameraZoom.DefaultFieldOfView, "Default zoom changed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
