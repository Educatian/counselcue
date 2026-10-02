using UnityEditor;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Web-sized, crunched room and UI textures so the WebGL download (and the Cloudflare
    /// Pages upload, capped at 25 MiB) stays small. Room surfaces are seen from a few metres
    /// away; 1024 px keeps their grain.
    /// </summary>
    public sealed class WebTextureImportProcessor : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            bool room = assetPath.StartsWith("Assets/Art/Textures/");
            bool ui = assetPath.StartsWith("Assets/Art/UI/");
            if (!room && !ui) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = ui ? 1024 : 1024;
            if (assetPath.EndsWith("ui_vignette.png")) importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 60;
        }
    }
}
