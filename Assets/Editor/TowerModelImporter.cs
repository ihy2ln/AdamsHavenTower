using System.IO;
using UnityEditor;
using UnityEngine;

// Barn tier models (Tools/barn_pipeline.py) arrive as barn_<tier>.fbx + barn_<tier>.png. This importer sets them up
// and builds one prefab (unlit, baked-painting material) per tier that TowerArtDirector loads from Resources.
public sealed class TowerModelImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/AdamsHaven/TowerModels/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importCameras = importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool touched = false;
        foreach (string path in imported)
            if (path.StartsWith(Folder) && (path.EndsWith(".fbx") || path.EndsWith(".png"))) touched = true;
        if (touched) EditorApplication.delayCall += RebuildPrefabs;
    }

    [MenuItem("Tools/Tower/Rebuild model prefabs")]
    public static void RebuildPrefabs()
    {
        foreach (string dir in Directory.GetDirectories(Folder))
        {
            string folder = dir.Replace('\\', '/');
            foreach (string fbxFile in Directory.GetFiles(folder, "*.fbx"))
            {
                string fbxPath = fbxFile.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(fbxPath);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + ".png");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (texture == null || model == null) continue;

                string matPath = folder + "/" + name + ".mat";
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, matPath);
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", 2);
                EditorUtility.SetDirty(material);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + name + ".prefab");
                Object.DestroyImmediate(instance);
            }
        }
        AssetDatabase.SaveAssets();
    }
}
