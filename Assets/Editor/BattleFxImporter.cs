using UnityEditor;
using UnityEngine;

// The generated Battle Mode art (Tools/GenerateBattleFx.py) is soft gradients and thin lines, so keep it
// uncompressed, unmipped and clamped instead of letting the default importer band it.
public sealed class BattleFxImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/AdamsHaven/Fx/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
    }
}
