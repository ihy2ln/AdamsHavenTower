using UnityEditor;
using UnityEngine;

// Pre-rendered fighter clips (Tools/produce_fighter_clips.py): atlas pages of painted frames plus the high-resolution
// guard used by the match cut. Painted line art, so no mips (they are drawn near 1:1) and high-quality compression;
// pages are packed at most 2048 px because AndroidTextureCompression caps every Resources texture there.
public sealed class BattleClipImporter : AssetPostprocessor
{
    public const string Folder = "/Resources/AdamsHaven/BattleClips/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
    }
}
