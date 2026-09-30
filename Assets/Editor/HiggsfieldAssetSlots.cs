using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Drop-in slots for images generated with Higgsfield (see Docs/HIGGSFIELD_ASSET_PACK.md).
    /// Every slot is optional: when a file is missing the builder keeps the current look,
    /// so the project always builds. Re-run "Tools → CounselCue → Build Korean Counseling Room"
    /// after adding or replacing a file.
    /// </summary>
    public static class HiggsfieldAssetSlots
    {
        public const string Root = "Assets/Art/Higgsfield";
        public const string PortraitFolder = Root + "/Portraits";
        public const string RoomFolder = Root + "/Room";
        public const string WindowViewBaseName = "window_view";
        public const string WallArtworkBaseName = "wall_artwork";

        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>Briefing illustration for a case: Portraits/{caseId}.png|.jpg</summary>
        public static Sprite LoadPortrait(string caseId)
        {
            string path = Find(PortraitFolder, caseId);
            if (path == null) return null;
            EnsureSpriteImport(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Texture2D LoadWindowView() => LoadTexture(RoomFolder, WindowViewBaseName);

        public static Texture2D LoadWallArtwork() => LoadTexture(RoomFolder, WallArtworkBaseName);

        private static Texture2D LoadTexture(string folder, string baseName)
        {
            string path = Find(folder, baseName);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string Find(string folder, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return null;
            for (int i = 0; i < Extensions.Length; i++)
            {
                string path = $"{folder}/{baseName}{Extensions[i]}";
                if (!File.Exists(path)) continue;
                // A file copied in while the editor was busy may not be imported yet.
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return path;
            }
            return null;
        }

        private static void EnsureSpriteImport(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }

    /// <summary>Imports new files in the Higgsfield folders with sensible WebGL-friendly settings.</summary>
    public sealed class HiggsfieldAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(HiggsfieldAssetSlots.Root + "/", System.StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            if (path.StartsWith(HiggsfieldAssetSlots.PortraitFolder + "/", System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
            }
            else if (path.StartsWith(HiggsfieldAssetSlots.RoomFolder + "/", System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
            }
        }
    }
}
