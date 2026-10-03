using UnityEditor;
using UnityEngine;

// Codex card art and silhouettes (Resources/AdamsHaven/Bestiary/Cards, from Tools/sync_codex_cards.py): kept at its own size (no
// power-of-two stretch), clamped, and mipmapped because the grid draws the cards at a fraction of their size.
// AndroidTextureCompression runs after this and gives Android its ASTC format.
public sealed class CodexCardImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/AdamsHaven/Bestiary/Cards/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = assetPath.EndsWith(".png");   // the silhouette masks
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
