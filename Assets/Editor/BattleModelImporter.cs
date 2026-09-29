using UnityEditor;
using UnityEngine;

// Keep the shared Blender rig intact; these authored clips are not humanoid retargets.
public sealed class BattleModelImporter : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/BattleModels/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!assetPath.Contains("/BattleModels/")) return;
        int start = clip.name.LastIndexOf("AH_");
        if (start >= 0) clip.name = clip.name.Substring(start);
        clip.wrapMode = clip.name.Contains("guard") ? WrapMode.Loop : WrapMode.ClampForever;
    }
}
