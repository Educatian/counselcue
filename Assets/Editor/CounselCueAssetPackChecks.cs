using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselCueAssetPackChecks
    {
        private const string ModelRoot = "Assets/Models/CounselCue";
        private const string FbxPath = ModelRoot + "/CounselCueRoomAssetPack.fbx";
        private const string GlbPath = ModelRoot + "/CounselCueRoomAssetPack.glb";
        private const string BlendPath = "Tools/Blender/out/CounselCueRoomAssetPack.blend";
        private const string BriefPath = "Docs/CounselCueAssetUpgradeBrief.md";

        [MenuItem("Tools/CounselCue/Run Asset Pack Checks")]
        public static void Run()
        {
            Require(File.Exists(BriefPath), "Asset upgrade brief is missing.");
            Require(File.Exists(BlendPath), "Blender source file is missing.");
            Require(File.Exists(FbxPath), "FBX asset pack is missing.");
            Require(File.Exists(GlbPath), "GLB asset pack is missing.");

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Require(fbx != null, "Unity could not import the CounselCue FBX asset pack.");

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            int meshCount = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Mesh) meshCount++;
            }

            Require(meshCount >= 20, $"Expected at least 20 meshes in asset pack, found {meshCount}.");
            Debug.Log($"COUNSELCUE_ASSET_PACK_PASS meshes={meshCount} fbx={FbxPath}");
        }

        public static void RunFromCommandLine()
        {
            AssetDatabase.Refresh();
            Run();
            EditorApplication.Exit(0);
        }

        public static void RunSceneIntegrationFromCommandLine()
        {
            AssetDatabase.Refresh();
            CounselingRoomBuilder.Build();
            Run();

            GameObject premiumRoot = GameObject.Find("CounselCue_PremiumAssets");
            Require(premiumRoot != null, "Premium room asset root is missing from the generated scene.");
            Require(GameObject.Find("RoundOakTable_Blender") != null, "Blender table was not placed.");
            Require(GameObject.Find("LinenTissueBox_Blender") != null, "Blender tissue box was not placed.");
            Require(GameObject.Find("CeladonTeaCup_Blender") != null, "Blender tea cup was not placed.");
            Require(GameObject.Find("HanjiFloorLamp_Blender") != null, "Blender floor lamp was not placed.");
            Require(GameObject.Find("HanjiArtwork_Blender") != null, "Blender artwork was not placed.");
            Require(GameObject.Find("AcousticRibPanel_Blender") != null, "Blender acoustic panel was not placed.");

            int rendererCount = premiumRoot.GetComponentsInChildren<Renderer>(true).Length;
            Require(rendererCount >= 50, $"Expected at least 50 integrated premium renderers, found {rendererCount}.");
            Debug.Log($"COUNSELCUE_ROOM_INTEGRATION_PASS renderers={rendererCount}");
            EditorApplication.Exit(0);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
