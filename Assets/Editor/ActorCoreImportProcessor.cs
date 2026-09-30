using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Import rules for Reallusion ActorCore actors unpacked by Tools/art/import_actorcore.py into
    /// Assets/ThirdParty/ActorCore/&lt;actor-id&gt;/. The CC_Base rig maps to a Humanoid avatar, so the
    /// Rocketbox seated/listening clips retarget onto it; the facial blendshapes (Brow_Drop_L,
    /// V_Open, ...) are translated by <see cref="FacialRigSemanticAdapter.ToSemantic"/>.
    /// </summary>
    public sealed class ActorCoreImportProcessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/ActorCore/";

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes = true;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            switch (file)
            {
                case "Albedo":
                    importer.maxTextureSize = 2048;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = true;
                    break;
                case "Normal":
                    importer.maxTextureSize = 2048;
                    importer.textureType = TextureImporterType.NormalMap;
                    break;
                case "MetallicSmoothness":
                case "Occlusion":
                    importer.maxTextureSize = 1024;
                    importer.sRGBTexture = false;
                    break;
                default:
                    // Textures Unity extracts from the FBX itself are not used (see ActorCoreLibrary).
                    importer.maxTextureSize = 512;
                    break;
            }
        }
    }

    public static class ActorCoreLibrary
    {
        private const string SourceMaterial = "Character_Pbr";

        public static string ModelPath(string actorId) => $"{ActorCoreImportProcessor.Root}{actorId}/{actorId}.fbx";

        /// <summary>
        /// Loads an unpacked ActorCore actor, building its Standard material from the unpacked maps
        /// and remapping the FBX material onto it. Returns null when the actor is not installed
        /// (the content is licensed per purchaser and is not in the repository).
        /// </summary>
        public static GameObject Load(string actorId)
        {
            string modelPath = ModelPath(actorId);
            if (!File.Exists(modelPath)) return null;
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return null;

            Material material = EnsureMaterial(actorId);
            if (material != null)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterial);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out Object mapped) || mapped != material)
                {
                    importer.AddRemap(id, material);
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }

        private static Material EnsureMaterial(string actorId)
        {
            string folder = $"{ActorCoreImportProcessor.Root}{actorId}";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Albedo.png");
            if (albedo == null) return null;
            string path = $"{folder}/M_{actorId}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = $"M_{actorId}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_MainTex", albedo);
            material.SetColor("_Color", Color.white);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Normal.png");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.85f);
            SetKeyword(material, "_NORMALMAP", normal != null);

            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/MetallicSmoothness.png");
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_GlossMapScale", 0.72f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            SetKeyword(material, "_METALLICGLOSSMAP", metallic != null);
            if (metallic == null)
            {
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Glossiness", 0.35f);
            }

            Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Occlusion.png");
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 0.8f);

            // Hair cards and lashes live in the same atlas: alpha-test them, keep the rest opaque.
            bool cutout = HasAlpha(albedo);
            material.SetFloat("_Mode", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.38f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            SetKeyword(material, "_ALPHABLEND_ON", false);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
            material.renderQueue = cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool HasAlpha(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null && importer.DoesSourceTextureHaveAlpha() && AlphaVaries(path);
        }

        private static bool AlphaVaries(string path)
        {
            // The unpacker writes RGBA for every actor; only a real opacity map has transparent texels.
            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!probe.LoadImage(File.ReadAllBytes(path))) return false;
                Color32[] pixels = probe.GetPixels32();
                for (int i = 0; i < pixels.Length; i += 7)
                {
                    if (pixels[i].a < 200) return true;
                }
                return false;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }
    }
}
