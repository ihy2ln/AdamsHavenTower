using UnityEditor;
using UnityEngine;

// Tower Mode 3D chibis (Tools/build_tower_rig.py): Meshy auto-rig + Blender-authored clips, sampled as legacy clips
// like the battle rigs. Clip names arrive as "Armature|AH_task_forge"; keep the AH_* part.
public sealed class TowerChibi3DImporter : AssetPostprocessor
{
    const string Folder = "/TowerChibi3D/";

    void OnPreprocessModel()
    {
        if (!assetPath.Contains(Folder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importBlendShapes = false;
        importer.importCameras = importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.resampleCurves = false;
    }

    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!assetPath.Contains(Folder)) return;
        int start = clip.name.LastIndexOf("AH_");
        if (start >= 0) clip.name = clip.name.Substring(start);
        clip.wrapMode = clip.name == "AH_knocked_down" ? WrapMode.ClampForever : WrapMode.Loop;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
