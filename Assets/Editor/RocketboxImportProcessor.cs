using UnityEditor;

namespace AdieLab.AffectCounsel.Editor
{
    public sealed class RocketboxImportProcessor : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/MicrosoftRocketbox/";

        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = assetPath.ToLowerInvariant().Contains("idle") || assetPath.ToLowerInvariant().Contains("waiting");
                clips[i].loopPose = clips[i].loopTime;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }
            if (clips.Length > 0) importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            string lower = assetPath.ToLowerInvariant();
            importer.maxTextureSize = lower.Contains("head_color") ? 2048 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (lower.Contains("normal")) importer.textureType = TextureImporterType.NormalMap;
        }

        private void OnPostprocessMeshHierarchy(UnityEngine.GameObject gameObject)
        {
            if (!assetPath.StartsWith(Root)) return;
            string lower = gameObject.name.ToLowerInvariant();
            if (lower.Contains("poly") && !lower.Contains("hipoly")) gameObject.SetActive(false);
        }
    }
}
