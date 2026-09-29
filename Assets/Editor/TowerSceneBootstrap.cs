using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerSceneBootstrap
{
    [MenuItem("Adams Haven/Tower/Create Tower Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Adams Haven Tower");
        root.AddComponent<AdamsHavenPrototype>();
        var camera = new GameObject("Tower Camera");
        camera.tag = "MainCamera";
        camera.AddComponent<Camera>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/AdamsHavenTower.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/AdamsHavenTower.unity", true)
        };
        AssetDatabase.SaveAssets();
        TowerPortValidation.Run();
        Debug.Log("Tower scene created with the Tower-only controller.");
    }
}
