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
            Require(Mathf.Approximately(CounselingCameraZoom.ObservationWeight(CounselingCameraZoom.DefaultFieldOfView), 0f), "Default view must use body framing.");
            Require(Mathf.Approximately(CounselingCameraZoom.ObservationWeight(CounselingCameraZoom.CloseFieldOfView), 1f), "Close view must use face framing.");
            float midpoint = Mathf.Lerp(CounselingCameraZoom.DefaultFieldOfView, CounselingCameraZoom.CloseFieldOfView, 0.5f);
            Require(Mathf.Abs(CounselingCameraZoom.ObservationWeight(midpoint) - 0.5f) < 0.001f, "Intermediate zoom framing must interpolate smoothly.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
