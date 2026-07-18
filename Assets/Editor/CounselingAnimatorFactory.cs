using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    internal static class CounselingAnimatorFactory
    {
        private const string ControllerPath = "Assets/Animations/CounselingClient.controller";
        private const string UpperBodyMaskPath = "Assets/Animations/CounselingUpperBody.mask";
        private const string MotionRoot = "Assets/ThirdParty/MicrosoftRocketbox/Animations/";

        internal static RuntimeAnimatorController Create()
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
            AddState(baseMachine, "Idle", "f_sit_table_idle_neutral_01.max.fbx");
            AddState(baseMachine, "IdleVariant", "f_sit_table_idle_neutral_02.max.fbx");
            AddState(baseMachine, "Waiting", "f_sit_table_idle_waiting_01.max.fbx");
            AddState(baseMachine, "Thoughtful", "f_sit_table_gestic_thoughtful.max.fbx");
            AddState(baseMachine, "Relaxed", "f_sit_table_idle_relaxed_01.max.fbx");
            baseMachine.defaultState = FindState(baseMachine, "Idle");

            AnimatorControllerLayer[] baseLayers = controller.layers;
            baseLayers[0].name = "Seated Base";
            baseLayers[0].iKPass = true;
            controller.layers = baseLayers;

            AnimatorStateMachine upperMachine = new AnimatorStateMachine { name = "Upper Body Gestures" };
            AssetDatabase.AddObjectToAsset(upperMachine, controller);
            AnimatorState empty = upperMachine.AddState("Empty");
            AddState(upperMachine, "TalkNeutral", "f_gestic_talk_neutral_01.max.fbx");
            AddState(upperMachine, "TalkNervous", "f_gestic_talk_nervous_02.max.fbx");
            AddState(upperMachine, "TalkSad", "f_gestic_talk_sad_01.max.fbx");
            AddState(upperMachine, "TalkRelaxed", "f_gestic_talk_relaxed_01.max.fbx");
            AddState(upperMachine, "ListenAccept", "f_gestic_listen_accept_01.max.fbx");
            upperMachine.defaultState = empty;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Upper Body Gesture",
                stateMachine = upperMachine,
                avatarMask = CreateUpperBodyMask(),
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0.58f,
                iKPass = false
            });

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, string fileName)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = LoadClip(MotionRoot + fileName);
            return state;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            return machine.states.First(state => state.state.name == name).state;
        }

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "CounselingUpperBody" };
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; index++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }
    }
}
