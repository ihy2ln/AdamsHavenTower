using UnityEditor;
using UnityEngine;

// The generated Battle Mode art (Tools/GenerateBattleFx.py) is soft gradients and thin lines, so keep it
// uncompressed, unmipped and clamped instead of letting the default importer band it.
public sealed class BattleFxImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/AdamsHaven/Fx/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        // Per-card effect layers (Fx/Moves, Tools/produce_move_fx.py) are many and large: compressed, 2048 max.
        bool moves = path.Contains("/Fx/Moves/");
        importer.textureCompression = moves ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = moves ? 2048 : 4096;
    }
}
