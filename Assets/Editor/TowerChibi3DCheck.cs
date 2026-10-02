using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders every Tower 3D chibi doing each room's work loop without entering Play mode:
// Unity.exe -batchmode -projectPath . -executeMethod TowerChibi3DCheck.Run -logFile <log> -quit
// Writes TowerChibi3DCheck/<id>.png (one frame per task, AnimeToon, three-quarter view, springs stepped for 0.6 s
// before each shot so tails / jiggle show their lag) and report.txt (clips, props, spring bones).
public static class TowerChibi3DCheck
{
    static readonly (string clip, string label)[] Shots =
    {
        ("AH_idle", "idle"), ("AH_walk", "walk"), ("AH_task_forge", "forge"), ("AH_task_kitchen", "kitchen"),
        ("AH_task_well", "well"), ("AH_task_farm", "farm"), ("AH_task_market", "market"), ("AH_task_warehouse", "warehouse"),
        ("AH_task_lumber", "lumber"), ("AH_task_quarry", "quarry"), ("AH_task_tavern", "tavern"), ("AH_task_heart", "heart"),
        ("AH_task_guard", "guard"), ("AH_task_train", "train"), ("AH_sit", "sit"), ("AH_sleep", "sleep"), ("AH_knocked_down", "down"),
    };
    const int Cell = 384;

    [MenuItem("Adams Haven/Tower Chibi 3D Check")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        string outDir = Path.Combine(Application.dataPath, "../TowerChibi3DCheck");
        Directory.CreateDirectory(outDir);
        var report = new StringBuilder();
        foreach (var id in TowerChibi3D.Ids) Render(id, outDir, report);
        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
        Debug.Log("TowerChibi3DCheck\n" + report);
    }

    static void Render(string id, string outDir, StringBuilder report)
    {
        var stage = new GameObject("check " + id);
        var chibi = TowerChibi3D.Spawn(id, stage.transform, 2f);
        if (!chibi) { report.AppendLine(id + ": missing model"); Object.DestroyImmediate(stage); return; }
        chibi.enabled = false;  // stepped by hand below
        chibi.Springs.enabled = false;
        stage.transform.rotation = Quaternion.Euler(0, 25, 0);
        report.AppendLine(id + ": height " + chibi.Height.ToString("0.00") + ", " + chibi.Clips.Count + " clips (" +
                          string.Join(", ", chibi.Clips.Keys.OrderBy(k => k)) + ")");
        report.AppendLine("   springs: tail " + chibi.Springs.HasTail + ", chains " + chibi.Springs.ChainCount + ", jiggle bones " + chibi.Springs.JiggleCount);
        var missing = Shots.Where(s => !chibi.Clips.ContainsKey(s.clip)).Select(s => s.clip).ToArray();
        if (missing.Length > 0) report.AppendLine("   MISSING clips: " + string.Join(", ", missing));

        var camGo = new GameObject("cam");
        camGo.transform.position = new Vector3(0, 1.05f, -6);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.15f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.86f, .84f, .8f, 1);
        cam.nearClipPlane = .1f; cam.farClipPlane = 20;
        var rt = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var sheet = new Texture2D(Cell * Shots.Length, Cell, TextureFormat.RGB24, false);
        // skinning refreshes once per editor frame, so bake each pose into static proxies for rendering
        var skins = chibi.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var proxies = skins.Select(s =>
        {
            var go = new GameObject("proxy " + s.name);
            go.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            go.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials;
            return go;
        }).ToArray();
        for (int i = 0; i < Shots.Length; i++)
        {
            if (!chibi.Clips.ContainsKey(Shots[i].clip)) continue;
            chibi.Play(Shots[i].clip, true);
            var clip = chibi.Clips[Shots[i].clip];
            float at = Shots[i].clip == "AH_knocked_down" ? clip.length : clip.length * .45f;
            for (float t = Mathf.Max(0, at - .6f); t <= at; t += 1f / 30f) { chibi.Sample(t); chibi.Springs.Step(1f / 30f); }
            for (int k = 0; k < skins.Length; k++)
            {
                proxies[k].SetActive(skins[k].gameObject.activeInHierarchy);
                skins[k].BakeMesh(proxies[k].GetComponent<MeshFilter>().sharedMesh, true);
                proxies[k].transform.SetPositionAndRotation(skins[k].transform.position, skins[k].transform.rotation);
            }
            foreach (var s in skins) s.enabled = false;
            cam.Render();
            foreach (var s in skins) s.enabled = true;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, Cell, Cell), Cell * i, 0);
            RenderTexture.active = null;
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(outDir, id + ".png"), sheet.EncodeToPNG());
        foreach (var p in proxies) { Object.DestroyImmediate(p.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(p); }
        cam.targetTexture = null; rt.Release();
        Object.DestroyImmediate(sheet); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(stage); Object.DestroyImmediate(camGo);
    }

    // Logs a spring chain's joint positions at the sampled pose and after a second of simulation.
    public static void DebugSprings()
    {
        AssetDatabase.Refresh();
        var stage = new GameObject("debug");
        var chibi = TowerChibi3D.Spawn("kaela", stage.transform, 2f);
        chibi.enabled = false; chibi.Springs.enabled = false;
        var sb = new StringBuilder("SPRINGDEBUG\n");
        var bones = chibi.Model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("BraidL") || t.name == "Head").ToArray();
        foreach (var clip in new[] { "AH_idle", "AH_task_forge" })
        {
            chibi.Play(clip, true);
            chibi.Sample(.5f);
            sb.AppendLine(clip + " sampled: " + string.Join(" | ", bones.Select(b => b.name + " " + b.position.ToString("F3") + " up " + b.up.ToString("F2"))));
            for (int k = 0; k < 30; k++) { chibi.Sample(.5f); chibi.Springs.Step(1f / 30f); }
            sb.AppendLine(clip + " sprung:  " + string.Join(" | ", bones.Select(b => b.name + " " + b.position.ToString("F3") + " up " + b.up.ToString("F2"))));
        }
        Debug.Log(sb.ToString());
        Object.DestroyImmediate(stage);
    }

    // A scene that shows all eight cycling through the room loops in Play mode.
    [MenuItem("Adams Haven/Open Tower Chibi 3D Showcase")]
    public static void OpenShowcase()
    {
        const string path = "Assets/Scenes/TowerChibi3DShowcase.unity";
        if (System.IO.File.Exists(path) && !Application.isBatchMode) { EditorSceneManager.OpenScene(path); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(0, 1.1f, -9);
        cam.orthographic = true; cam.orthographicSize = 2.6f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .19f, .24f);
        new GameObject("Tower chibi 3D showcase").AddComponent<TowerChibi3DShowcase>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, path);
    }
}
