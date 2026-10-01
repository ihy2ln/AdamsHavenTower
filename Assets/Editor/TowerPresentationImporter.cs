using UnityEditor;
using UnityEngine;

public sealed class TowerPresentationImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath.Contains("/AdamsHaven/Expedition/"))
        {
            var expedition = (TextureImporter)assetImporter;
            expedition.textureType = TextureImporterType.Default;
            expedition.alphaIsTransparency = true;
            expedition.mipmapEnabled = false;
            expedition.wrapMode = assetPath.Contains("/Dungeon/Silverbrook/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            expedition.filterMode = assetPath.Contains("/Icons/") ? FilterMode.Point : FilterMode.Bilinear;
            expedition.maxTextureSize = 2048;
            if (assetPath.Contains("/Dungeon/AnimeV2/"))
            {
                expedition.maxTextureSize = 4096;
                expedition.mipmapEnabled = true;
                expedition.wrapMode = TextureWrapMode.Clamp;
                expedition.textureCompression = TextureImporterCompression.CompressedHQ;
                expedition.anisoLevel = 4;
            }
            expedition.npotScale = TextureImporterNPOTScale.None;
            expedition.textureCompression = assetPath.Contains("/Maps/") || assetPath.Contains("/Rooms/") || assetPath.Contains("/Dungeon/AnimeV2/")
                ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            return;
        }
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
        // Angular sci-fantasy controls are drawn 4-7x below source size: mipmaps keep the fine trim from shimmering.
        if (assetPath.Contains("/UI/Wuwa/"))
        {
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipMapsPreserveCoverage = false;
        }
        if (assetPath.Contains("/Scenery/"))
        {
            importer.mipmapEnabled = true;
            importer.maxTextureSize = assetPath.Contains("world_extended") ? 16384 : 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
