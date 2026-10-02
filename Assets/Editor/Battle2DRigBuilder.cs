using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds a 2D anime cutout rig for Battle Mode from Tools/build_2d_rig.py output:
//   Assets/Resources/AdamsHaven/BattleRigs2D/<id>/rig.json + parts/*.png  ->  model.prefab + AH_*.anim
// The prefab is a bone hierarchy (empty transforms at the joints) with one SpriteRenderer per part, two units tall with
// the feet on the origin, facing right. BattleMode.CreateRig loads it like a 3D model and samples the clips by name,
// so the clip names and contact times match the 3D rigs (BattleModels.MoveSets): basic 0.34 s, skill 1.33 s, ult 2.23 s.
public static class Battle2DRigBuilder
{
    public const string Root = "Assets/Resources/AdamsHaven/BattleRigs2D/";

    [Serializable] class Bone { public string name = "", parent = ""; public float[] pivot, tip; }
    [Serializable] class Part { public string name = "", bone = "", file = ""; public int x, y, w, h, order; }
    [Serializable] class RigFile { public string id = ""; public float ppu; public int[] image; public float[] feet; public Bone[] bones; public Part[] parts; }

    [MenuItem("Adams Haven/Battle/Build 2D Rigs")]
    public static void BuildAll()
    {
        foreach (var dir in Directory.GetDirectories(Root))
            if (File.Exists(Path.Combine(dir, "rig.json"))) Build(Path.GetFileName(dir));
    }

    public static string Build(string id)
    {
        string folder = Root + id + "/";
        var rig = JsonUtility.FromJson<RigFile>(File.ReadAllText(folder + "rig.json"));
        var bones = new Dictionary<string, Bone>();
        foreach (var b in rig.bones) bones[b.name] = b;
        float ppu = rig.ppu;
        Vector2 feet = new Vector2(rig.feet[0], rig.feet[1]);
        // Canvas pixels (y down) to rig units (y up, feet at the origin).
        Func<float[], Vector2> unit = p => new Vector2((p[0] - feet.x) / ppu, (feet.y - p[1]) / ppu);

        // Parts become sprites pivoted on their bone's joint, so rotating the bone turns the part about the joint.
        foreach (var part in rig.parts)
        {
            string path = folder + part.file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var pivotPx = bones[part.bone].pivot;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2((pivotPx[0] - part.x) / part.w, 1f - (pivotPx[1] - part.y) / part.h);
            settings.spritePixelsPerUnit = ppu;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = true;          // the field draws the rig small: mips keep the lineart clean
            settings.filterMode = FilterMode.Trilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.npotScale = TextureImporterNPOTScale.None;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        var model = new GameObject(id + " 2D rig");
        var nodes = new Dictionary<string, Transform>();
        restPositions.Clear();
        // Bones in file order: parents come first.
        foreach (var b in rig.bones)
        {
            var t = new GameObject(b.name).transform;
            var parent = b.parent.Length > 0 ? nodes[b.parent] : model.transform;
            t.SetParent(parent, false);
            Vector2 at = unit(b.pivot) - (b.parent.Length > 0 ? unit(bones[b.parent].pivot) : Vector2.zero);
            t.localPosition = new Vector3(at.x, at.y, 0);
            restPositions[b.name] = at;
            nodes[b.name] = t;
        }
        foreach (var part in rig.parts)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + part.file);
            var go = new GameObject("Part " + part.name);
            go.transform.SetParent(nodes[part.bone], false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = part.order;
        }
        string prefabPath = folder + "model.prefab";
        PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
        UnityEngine.Object.DestroyImmediate(model);

        // Rest direction of each bone (pivot -> tip), degrees, 0 = facing direction (right), 90 = up.
        var rest = new Dictionary<string, float>();
        foreach (var b in rig.bones)
        {
            if (b.tip == null || b.tip.Length < 2) { rest[b.name] = 0; continue; }
            Vector2 d = unit(b.tip) - unit(b.pivot);
            rest[b.name] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }
        var paths = new Dictionary<string, string>();
        foreach (var b in rig.bones) paths[b.name] = b.parent.Length > 0 ? paths[b.parent] + "/" + b.name : b.name;

        int clips = 0;
        foreach (var spec in Battle2DMoves.Clips())
        {
            var clip = BuildClip(spec, rig.bones, rest, paths);
            string clipPath = folder + spec.Name + ".anim";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            clips++;
        }
        AssetDatabase.SaveAssets();
        return id + ": " + rig.parts.Length + " parts, " + rig.bones.Length + " bones, " + clips + " clips";
    }

    // Keys hold absolute aims (limbs) and relative turns/offsets (body); every key is solved top-down into local angles.
    static AnimationClip BuildClip(Battle2DMoves.ClipSpec spec, Bone[] bones, Dictionary<string, float> rest, Dictionary<string, string> paths)
    {
        var clip = new AnimationClip { name = spec.Name, frameRate = 60 };
        var rot = new Dictionary<string, AnimationCurve>();
        var px = new Dictionary<string, AnimationCurve>();
        var py = new Dictionary<string, AnimationCurve>();
        var last = new Dictionary<string, float>();
        foreach (var b in bones) { rot[b.name] = new AnimationCurve(); px[b.name] = new AnimationCurve(); py[b.name] = new AnimationCurve(); }
        foreach (var key in spec.Keys)
        {
            var world = new Dictionary<string, float>();     // accumulated turn of each bone from its rest
            foreach (var b in bones)
            {
                float parentTurn = b.parent.Length > 0 ? world[b.parent] : 0f;
                float local;
                float aim;
                if (key.Pose.Aim.TryGetValue(b.name, out aim)) local = Mathf.DeltaAngle(rest[b.name] + parentTurn, aim);
                else if (key.Pose.Turn.TryGetValue(b.name, out local)) { }
                else local = 0f;
                // Keep the shortest way round from the previous key so curves never spin.
                float prev;
                if (last.TryGetValue(b.name, out prev)) local = prev + Mathf.DeltaAngle(prev, local);
                last[b.name] = local;
                world[b.name] = parentTurn + local;
                rot[b.name].AddKey(new Keyframe(key.Time, local));
                Vector2 off;
                key.Pose.Move.TryGetValue(b.name, out off);
                px[b.name].AddKey(new Keyframe(key.Time, off.x));
                py[b.name].AddKey(new Keyframe(key.Time, off.y));
            }
        }
        // Every clip keys every bone (angle and position): sampling a clip must fully replace the previous pose.
        foreach (var b in bones)
        {
            Smooth(rot[b.name]);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[b.name], typeof(Transform), "localEulerAnglesRaw.z"), rot[b.name]);
            Vector2 restPos;
            restPositions.TryGetValue(b.name, out restPos);
            var cx = Offset(px[b.name], restPos.x); var cy = Offset(py[b.name], restPos.y);
            Smooth(cx); Smooth(cy);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[b.name], typeof(Transform), "m_LocalPosition.x"), cx);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[b.name], typeof(Transform), "m_LocalPosition.y"), cy);
        }
        if (spec.Loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
        return clip;
    }

    // Rest position of each bone (rig units, local to its parent), filled by Build; position curves add offsets to it.
    static readonly Dictionary<string, Vector2> restPositions = new Dictionary<string, Vector2>();

    // Contact sheet of poses for checking a rig: frames are "AH_clip@seconds", rendered offscreen in edit mode.
    public static string RenderPoses(string id, string[] frames, string outPath, int cellW = 420, int cellH = 480, float half = 1.4f, int cols = 4)
    {
        string folder = Root + id + "/";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "model.prefab");
        var stage = new GameObject("2D rig preview");
        stage.transform.position = new Vector3(5000, 5000, 0);
        var model = (GameObject)UnityEngine.Object.Instantiate(prefab, stage.transform);
        var camGo = new GameObject("Preview camera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.localPosition = new Vector3(0.15f, 1.2f, -6);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = half;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.24f, 0.26f, 0.31f, 1);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 12;
        var rt = new RenderTexture(cellW, cellH, 24);
        cam.targetTexture = rt;
        int rows = (frames.Length + cols - 1) / cols;
        var sheet = new Texture2D(cellW * cols, cellH * rows, TextureFormat.RGB24, false);
        var fill = new Color[sheet.width * sheet.height];
        for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.15f, 0.16f, 0.19f);
        sheet.SetPixels(fill);
        var tmp = new Texture2D(cellW, cellH, TextureFormat.RGB24, false);
        for (int i = 0; i < frames.Length; i++)
        {
            var at = frames[i].Split('@');
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + at[0] + ".anim");
            if (clip == null) continue;
            clip.SampleAnimation(model, float.Parse(at[1], System.Globalization.CultureInfo.InvariantCulture));
            cam.Render();
            RenderTexture.active = rt; tmp.ReadPixels(new Rect(0, 0, cellW, cellH), 0, 0); tmp.Apply(); RenderTexture.active = null;
            sheet.SetPixels((i % cols) * cellW, (rows - 1 - i / cols) * cellH, cellW, cellH, tmp.GetPixels());
        }
        sheet.Apply();
        File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        cam.targetTexture = null; rt.Release();
        UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(tmp);
        return outPath;
    }

    // Numbered PNG frames of clips played back to back ("AH_clip@from-to"), for preview videos.
    public static int RenderSequence(string id, string[] segments, string outDir, int fps = 30, int w = 720, int h = 720, float half = 1.45f)
    {
        string folder = Root + id + "/";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "model.prefab");
        var stage = new GameObject("2D rig sequence");
        stage.transform.position = new Vector3(6000, 5000, 0);
        var model = (GameObject)UnityEngine.Object.Instantiate(prefab, stage.transform);
        var camGo = new GameObject("Sequence camera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.localPosition = new Vector3(0.25f, 1.2f, -6);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = half;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.14f, 0.19f, 1);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 12;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGB24, false);
        Directory.CreateDirectory(outDir);
        int n = 0;
        foreach (var seg in segments)
        {
            var at = seg.Split('@');
            var span = at[1].Split('-');
            float a = float.Parse(span[0], System.Globalization.CultureInfo.InvariantCulture);
            float b = float.Parse(span[1], System.Globalization.CultureInfo.InvariantCulture);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + at[0] + ".anim");
            if (clip == null) continue;
            for (float t = a; t <= b + 1e-4f; t += 1f / fps)
            {
                clip.SampleAnimation(model, Mathf.Min(t, clip.length));
                cam.Render();
                RenderTexture.active = rt; tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); tmp.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, "f_" + (n++).ToString("0000") + ".png"), tmp.EncodeToPNG());
            }
        }
        cam.targetTexture = null; rt.Release();
        UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tmp);
        return n;
    }

    static AnimationCurve Offset(AnimationCurve c, float by)
    {
        var o = new AnimationCurve();
        foreach (var k in c.keys) o.AddKey(new Keyframe(k.time, k.value + by));
        return o;
    }

    static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }
}
