using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Batch check for the full-body battle rigs (Tools/build_battle_rig.py) without entering Play mode:
// Unity.exe -batchmode -projectPath . -executeMethod BattleRigBatchCheck.Run -logFile <log> -quit
// Imports, lists each model's AH_* clips and renders guard / basic-contact frames the way BattleModels.cs
// frames a field rig (2 units tall, turned 155 degrees, ortho size 1.5, AnimeToon shader) to BattleRigCheck/.
public static class BattleRigBatchCheck
{
    static readonly string[] Ids = { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "celestium-normal", "celestium-muscle", "celestium-short" };

    [MenuItem("Adams Haven/Battle Rig Batch Check")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        string outDir = Path.Combine(Application.dataPath, "../BattleRigCheck");
        Directory.CreateDirectory(outDir);
        var report = new StringBuilder();
        foreach (var id in Ids)
        {
            string path = "Assets/Resources/AdamsHaven/BattleModels/" + id + "/model.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) { report.AppendLine(id + ": missing"); continue; }
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name.Substring(c.name.LastIndexOf("AH_") < 0 ? 0 : c.name.LastIndexOf("AH_")), c => c);
            report.AppendLine(id + ": " + string.Join(", ", clips.Select(kv => kv.Key + " " + kv.Value.length.ToString("0.00") + "s")));
            if (!clips.ContainsKey("AH_attack_basic")) continue;
            var provenance = Path.Combine(Path.GetDirectoryName(path), "provenance.json");
            float contact = .3f;
            if (File.Exists(provenance))
            {
                var text = File.ReadAllText(provenance);
                int k = text.IndexOf("\"AH_attack_basic\":");
                if (k > 0) float.TryParse(text.Substring(k + 18).Split(',', '}')[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out contact);
            }
            Render(id, prefab, clips, outDir, contact, report);
        }
        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
        Debug.Log("BattleRigBatchCheck\n" + report);
    }

    static void Render(string id, GameObject prefab, System.Collections.Generic.Dictionary<string, AnimationClip> clips, string outDir, float contact, StringBuilder report)
    {
        var stage = new GameObject("check " + id);
        var pivot = new GameObject("pivot").transform;
        pivot.SetParent(stage.transform, false);
        var model = Object.Instantiate(prefab, pivot);
        foreach (var a in model.GetComponentsInChildren<Animation>()) a.enabled = false;
        clips["AH_battle_guard"].SampleAnimation(model, 0);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(); bool any = false;
        foreach (var r in renderers)
        {
            if (r.name.StartsWith("Weapon_")) continue;
            var skin = r as SkinnedMeshRenderer;
            if (!skin) continue;
            var mesh = new Mesh(); skin.BakeMesh(mesh, true);
            foreach (var v in mesh.vertices) { var p = r.transform.TransformPoint(v); if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p); }
            Object.DestroyImmediate(mesh);
        }
        float scale = 2f / Mathf.Max(.01f, bounds.size.y);
        pivot.localScale = Vector3.one * scale;
        pivot.position = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
        pivot.RotateAround(Vector3.zero, Vector3.up, 155f);
        report.AppendLine("   height " + bounds.size.y.ToString("0.00") + " renderers " + string.Join(", ", renderers.Select(r => r.name)));
        var toon = Shader.Find("AdamsHaven/AnimeToon");
        foreach (var r in renderers)
        {
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            var mats = r.sharedMaterials.Select(m => new Material(m)).ToArray();
            foreach (var m in mats) if (toon) { var tex = m.mainTexture; var col = m.color; m.shader = toon; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col); }
            r.sharedMaterials = mats;
        }
        var camGo = new GameObject("cam");
        camGo.transform.position = new Vector3(0, 1, -6);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.5f; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.18f, .2f, .24f, 1); cam.nearClipPlane = .1f; cam.farClipPlane = 12;
        var light = new GameObject("light").AddComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40, -30, 0);
        var rt = new RenderTexture(512, 512, 24);
        cam.targetTexture = rt;
        float len = clips["AH_attack_basic"].length;
        var shots = new (string, string, float)[9];
        shots[0] = ("guard", "AH_battle_guard", 0f);
        for (int s = 0; s < 8; s++) shots[s + 1] = ("t" + s, "AH_attack_basic", len * s / 7f);
        report.AppendLine("   basic " + len.ToString("0.00") + "s contact " + contact.ToString("0.00") + "s (strip: guard + 8 evenly spaced frames)");
        var sheet = new Texture2D(512 * shots.Length, 512, TextureFormat.RGB24, false);
        // Skinning only refreshes once per editor frame, so bake each sampled pose into static proxies.
        var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        var proxies = skins.Select(smr =>
        {
            var go = new GameObject("proxy " + smr.name);
            go.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            return go;
        }).ToArray();
        foreach (var smr in skins) smr.enabled = false;
        for (int i = 0; i < shots.Length; i++)
        {
            clips[shots[i].Item2].SampleAnimation(model, shots[i].Item3);
            for (int k = 0; k < skins.Length; k++)
            {
                skins[k].BakeMesh(proxies[k].GetComponent<MeshFilter>().sharedMesh, true);
                proxies[k].transform.SetPositionAndRotation(skins[k].transform.position, skins[k].transform.rotation);
            }
            cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, 512, 512), 512 * i, 0);
            RenderTexture.active = null;
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(outDir, id + ".png"), sheet.EncodeToPNG());
        foreach (var p in proxies) { Object.DestroyImmediate(p.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(p); }
        cam.targetTexture = null; rt.Release();
        Object.DestroyImmediate(sheet); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(stage); Object.DestroyImmediate(camGo); Object.DestroyImmediate(light.gameObject);
    }
}
