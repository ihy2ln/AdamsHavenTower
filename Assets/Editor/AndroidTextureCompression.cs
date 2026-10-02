using UnityEditor;
using UnityEngine;

// Mobile size pass for the Android build only (the editor and Windows keep the importers' own settings).
// The art importers keep painted rooms, cards and UI uncompressed and allow 4096 px backgrounds, which made the APK
// pass 1 GB. This runs after them and gives Android:
//   - a 2048 px cap (1024 for 3D model textures): more than a phone screen shows. The wide world panorama keeps its size.
//   - ASTC 6x6 for big painted backgrounds (longest side over 1024 px, not UI), ASTC 4x4 for UI and small art.
public sealed class AndroidTextureCompression : AssetPostprocessor
{
    public const string Platform = "Android";
    public const int MaxSize = 2048, ModelMaxSize = 1024, BigArt = 1024;

    public override int GetPostprocessOrder() { return 100; }   // after TowerPresentationImporter and friends

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/")) return;
        var importer = (TextureImporter)assetImporter;
        if (importer.textureType == TextureImporterType.NormalMap) return;   // normal maps keep Unity's own encoding
        int width, height;
        importer.GetSourceTextureWidthAndHeight(out width, out height);
        bool panorama = assetPath.Contains("world_extended");
        bool model = assetPath.Contains("Models/");
        bool ui = assetPath.Contains("/UI/") || assetPath.Contains("/Cards/") || assetPath.Contains("/icons");
        int cap = panorama ? importer.maxTextureSize : model ? ModelMaxSize : MaxSize;
        int shown = Mathf.Min(Mathf.Max(width, height), cap);

        var android = importer.GetPlatformTextureSettings(Platform);
        android.overridden = true;
        android.maxTextureSize = Mathf.Min(importer.maxTextureSize, cap);
        android.format = !ui && shown > BigArt ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
        android.compressionQuality = 100;
        importer.SetPlatformTextureSettings(android);
    }
}
