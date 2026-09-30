using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselCueSprintChecks
    {
        [MenuItem("Tools/CounselCue/Run Sprint 1-3 Checks")]
        public static void RunFromMenu() => Run(false);

        public static void RunFromCommandLine() => Run(true);

        private static void Run(bool exit)
        {
            try
            {
                CaseCatalog catalog = CounselingContentFactory.CreateOrUpdate();
                Require(catalog != null && catalog.Count == 5, "CaseCatalog must contain exactly five pilot cases.");
                Require(catalog.Cases.Select(c => c.CaseId).Distinct().Count() == 5, "Case IDs must be unique.");
                Require(catalog.Cases.Select(c => c.ProfileDefinition.ProfileId).Distinct().Count() == 5, "Client profiles must be unique.");
                Require(catalog.Cases.Select(c => c.AvatarPresentation.PresentationId).Distinct().Count() == 5, "Avatar presentations must be unique.");

                foreach (CounselingCaseDefinition item in catalog.Cases)
                {
                    Require(item.AvatarPresentation.AvatarPrefab != null, $"{item.CaseId}: avatar missing.");
                    Require(item.FocusSkills.Length == 3, $"{item.CaseId}: expected three focused-practice skills.");
                    Require(!string.IsNullOrWhiteSpace(item.GetReply(5, true)), $"{item.CaseId}: disclosure trajectory missing.");
                    Require(!string.IsNullOrWhiteSpace(item.PersonaPromptKey), $"{item.CaseId}: persona prompt key missing.");
                }

                GameObject facePrefab = catalog.DefaultCase.AvatarPresentation.AvatarPrefab;
                int blendShapeCount = 0;
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SkinnedMeshRenderer renderer in facePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null) continue;
                    blendShapeCount += renderer.sharedMesh.blendShapeCount;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++) names.Add(renderer.sharedMesh.GetBlendShapeName(i));
                }
                HashSet<string> semanticNames = new HashSet<string>(
                    names.Select(FacialRigSemanticAdapter.ToSemantic).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
                bool actorCore = CounselingContentFactory.UsingActorCore;
                Require(blendShapeCount >= (actorCore ? 50 : 150),
                    $"{(actorCore ? "ActorCore" : "Rocketbox")} face expected >={(actorCore ? 50 : 150)} blendshapes, found {blendShapeCount}.");
                Require(semanticNames.Contains("AU_45_Blink") ||
                        (semanticNames.Contains("AU_43_L_EyeClosed") && semanticNames.Contains("AU_43_R_EyeClosed")), "Blink AU missing.");
                Require(semanticNames.Any(n => n.StartsWith("AU_04_")), "Brow lowerer AU missing.");
                Require(semanticNames.Count(n => n.StartsWith("AA_VI_")) >= (actorCore ? 6 : 15),
                    $"Too few visemes mapped ({semanticNames.Count(n => n.StartsWith("AA_VI_"))}).");
                foreach (CounselingCaseDefinition item in catalog.Cases)
                {
                    int channels = 0;
                    foreach (SkinnedMeshRenderer renderer in item.AvatarPresentation.AvatarPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer.sharedMesh == null) continue;
                        for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                            if (FacialRigSemanticAdapter.ToSemantic(renderer.sharedMesh.GetBlendShapeName(i)).Length > 0) channels++;
                    }
                    Require(channels >= 20, $"{item.CaseId}: only {channels} facial channels map to AU/viseme semantics.");
                }
                Require(KoreanVisemePlanner.Build("상담을 시작해요").Any(v => v != "AA_VI_00_Sil"), "Korean viseme plan is silent.");
                HashSet<string> shadowed = FacialRigSemanticAdapter.FindCombinedShapesShadowedByLaterals(
                    new[] { "AU_12_LipCornerPuller", "AU_12_L_LipCornerPuller", "AU_12_R_LipCornerPuller" });
                Require(shadowed.Contains("AU_12_LipCornerPuller"), "Combined/lateral AU duplicate detection failed.");
                Require(FacialRigSemanticAdapter.ToSemantic("Brow_Drop_L") == "AU_04_L_BrowLowerer", "ActorCore brow mapping failed.");
                Require(FacialRigSemanticAdapter.ToSemantic("A14_Eye_Blink_Left").Length == 0, "ARKit duplicates must stay unmapped.");
                Require(FacialMorphDynamics.BlinkWeight(0.065f) > 0.95f, "Blink must contain a short natural hold.");
                float morphVelocity = 0f;
                float morphStep = FacialMorphDynamics.Step(0f, 100f, ref morphVelocity, 0.16f, 170f, 1f / 60f);
                Require(morphStep > 0f && morphStep < 10f, "Facial morph dynamics must prevent one-frame popping.");
                Require(Enum.GetValues(typeof(ClientGazeState)).Length >= 5, "Five observable gaze states required.");
                Require(CounselingCameraZoom.CloseFieldOfView <= 24f, "Face observation preset must be close enough.");

                CounselingRoomBuilder.Build();
                Require(GameObject.Find("CounselorEyeContactTarget") != null, "Independent eye contact target missing.");
                Require(GameObject.Find("FaceObservation") != null, "Face observation zoom control missing.");
                Require(Resources.FindObjectsOfTypeAll<GameObject>().Any(item => item.name == "FaceDebugPanel"), "Face diagnostics panel missing.");
                Require(UnityEngine.Object.FindObjectsByType<ClientAvatarHost>(FindObjectsSortMode.None).Length == 1, "Client avatar host missing.");
                Require(UnityEngine.Object.FindObjectsByType<ClientMicroMotionController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length >= 1, "Micro motion controller missing.");
                Require(GameObject.Find("SelectCase5") != null, "Five-case selection UI missing.");
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"COUNSELCUE_SPRINT_1_3_CHECKS_PASS cases=5 gazeStates=5 rig={(actorCore ? "actorcore" : "rocketbox")} blendshapes={blendShapeCount}");
                if (exit) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("COUNSELCUE_SPRINT_1_3_CHECKS_FAIL");
                if (exit) EditorApplication.Exit(1); else throw;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
