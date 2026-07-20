using System;
using System.IO;
using System.Linq;
using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselingAnimationChecks
    {
        private const string ControllerPath = "Assets/Animations/CounselingClient.controller";

        [MenuItem("Tools/CounselCue/Run Animation Checks")]
        public static void RunFromMenu()
        {
            RunChecks();
            Debug.Log("ANIMATION_CHECKS_PASS");
        }

        public static void RunFromCommandLine()
        {
            RunChecks();
            Debug.Log("ANIMATION_CHECKS_PASS");
        }

        private static void RunChecks()
        {
            CounselingAnimatorFactory.Create();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Require(controller != null, "Counseling animator controller is missing.");
            Require(controller.layers.Length == 2, "Animator must separate seated base and upper-body gestures.");

            AnimatorControllerLayer upper = controller.layers[1];
            Require(upper.avatarMask != null, "Upper-body gesture layer requires an AvatarMask.");
            Require(upper.blendingMode == AnimatorLayerBlendingMode.Override, "Gesture layer must use bounded Override blending.");
            Require(Mathf.Approximately(upper.defaultWeight, 0f), "Gesture layer must start neutral and blend in at runtime.");

            string[] stateNames = upper.stateMachine.states.Select(state => state.state.name).ToArray();
            foreach (string required in new[] { "Empty", "TalkNeutral", "TalkNervous", "TalkNervousSoft", "TalkSad", "TalkRelaxed", "ListenAccept" })
            {
                Require(stateNames.Contains(required), $"Missing upper-body state: {required}");
            }

            Require(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), "Torso must be included.");
            Require(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), "Left arm must be included.");
            Require(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), "Right arm must be included.");
            Require(!upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), "Left leg must stay seated.");
            Require(!upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), "Right leg must stay seated.");
            Require(!upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), "Root motion must stay on the seated base.");

            Require(typeof(CounselCueWebBridge).GetMethod("OnWebVoiceStarted") != null, "Web voice start callback is missing.");
            Require(typeof(CounselCueWebBridge).GetMethod("OnWebVoiceEnded") != null, "Web voice end callback is missing.");
            Require(ClientAvatarController.AffectForEmotion("thoughtful") == ClientAffect.Thoughtful, "Thoughtful affect mapping failed.");
            Require(ClientAvatarController.GestureStateFor("relieved", 0) == "TalkRelaxed", "Relieved gesture mapping failed.");
            Require(ClientAvatarController.GestureStateFor("guarded", 0) == "TalkSad", "Guarded gesture mapping failed.");
            Require(ClientAvatarController.GestureStateFor("anxious", 1) == "TalkNervousSoft", "Anxious gesture variation mapping failed.");
            string avatarSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/ClientAvatarController.cs"));
            Require(avatarSource.Contains("gestureLayerTarget"), "Gesture layer requires a runtime weight envelope.");
            Require(!avatarSource.Contains("CrossFadeInFixedTime(\"Empty\""), "Gesture exit must fade layer weight instead of snapping to Empty.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
