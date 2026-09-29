using UnityEditor;
using UnityEngine;

public sealed class TowerPresentationImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/AdamsHaven/TowerPresentation/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        if (assetPath.Contains("/Scenery/"))
        {
            importer.mipmapEnabled = true;
            importer.maxTextureSize = assetPath.Contains("world_extended") ? 16384 : 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
