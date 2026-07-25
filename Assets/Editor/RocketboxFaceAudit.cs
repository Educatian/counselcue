using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class RocketboxFaceAudit
    {
        private const string AvatarPath =
            "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Female_Adult_05/Export/Female_Adult_05_facial.fbx";

        [MenuItem("Tools/CounselCue/Audit Rocketbox Face Rig")]
        public static void Run()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            if (prefab == null) throw new System.InvalidOperationException($"Avatar missing: {AvatarPath}");

            List<string> blendShapes = new List<string>();
            foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    blendShapes.Add($"{renderer.name}:{mesh.GetBlendShapeName(index)}");
                }
            }

            IEnumerable<string> materials = prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(material => material.name)
                .Distinct()
                .OrderBy(name => name);

            Debug.Log($"COUNSELCUE_FACE_AUDIT blendShapes={blendShapes.Count}\n" +
                      string.Join("\n", blendShapes.OrderBy(name => name)) +
                      "\nCOUNSELCUE_FACE_MATERIALS\n" + string.Join("\n", materials));
        }

        public static void RunFromCommandLine()
        {
            Run();
            EditorApplication.Exit(0);
        }
    }
}
