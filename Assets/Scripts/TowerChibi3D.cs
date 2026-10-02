using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// A Tower Mode 3D chibi: the Meshy-rigged model from Resources/AdamsHaven/TowerChibi3D/<id>/model (built by
// Tools/build_tower_rig.py), anime-toon materials, the AH_* task clips sampled on its own clock, the held tool /
// station props each task needs, and TowerChibiSprings for tail + jiggle.
// Fallout Shelter style: a resident assigned to a room plays that room's work loop (RoomClips).
public sealed class TowerChibi3D : MonoBehaviour
{
    public static readonly string[] Ids =
        { "ghislaine", "kaela", "helda", "elara", "clarity", "celestium-med", "celestium-muscle", "celestium-short" };

    // room type (TowerDomain.TowerRoomDef id) -> work loop
    public static readonly Dictionary<string, string> RoomClips = new Dictionary<string, string>
    {
        { "gate", "AH_task_guard" }, { "heart", "AH_task_heart" },
        { "house", "AH_sit" }, { "cottage", "AH_sit" }, { "nursery", "AH_chat" }, { "terrace_row", "AH_sit" }, { "manor", "AH_chat" },
        { "kitchen", "AH_task_kitchen" }, { "farmstead", "AH_task_farm" }, { "well", "AH_task_well" },
        { "lumber_mill", "AH_task_lumber" }, { "quarry", "AH_task_quarry" },
        { "barn", "AH_task_haul" }, { "silo", "AH_task_warehouse" }, { "warehouse", "AH_task_warehouse" },
        { "frosted_mug", "AH_task_tavern" }, { "guild_hall", "AH_chat" }, { "forge", "AH_task_forge" },
        { "deck_hall", "AH_task_train" }, { "market", "AH_task_market" },
    };

    // which Prop_* (held) / Set_* (station) objects show for a clip; everything else stays hidden
    public static readonly Dictionary<string, string[]> ClipProps = new Dictionary<string, string[]>
    {
        { "AH_task_forge", new[] { "Prop_Hammer", "Set_Anvil" } }, { "AH_task_kitchen", new[] { "Prop_Ladle", "Set_Pot" } },
        { "AH_task_well", new[] { "Set_Rope" } }, { "AH_task_lumber", new[] { "Prop_Axe" } },
        { "AH_task_quarry", new[] { "Prop_Pickaxe" } }, { "AH_task_tavern", new[] { "Prop_Mug" } },
        { "AH_sit", new[] { "Set_Chair" } }, { "AH_sleep", new[] { "Set_Bed" } },
    };

    public string Id;
    public string Clip { get; private set; } = "AH_idle";
    public float Speed = 1f;
    public GameObject Model;
    public TowerChibiSprings Springs;
    public float Height;
    public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
    readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>();
    readonly List<Material> materials = new List<Material>();
    float time;

    public static string ResourcePath(string id) { return "AdamsHaven/TowerChibi3D/" + id + "/model"; }

    // Builds the chibi under parent with its feet at the parent origin, facing the parent's -Z (toward a camera at -Z).
    public static TowerChibi3D Spawn(string id, Transform parent, float targetHeight = 0f)
    {
        var prefab = Resources.Load<GameObject>(ResourcePath(id));
        if (!prefab) return null;
        var go = new GameObject("Tower chibi " + id);
        go.transform.SetParent(parent, false);
        var chibi = go.AddComponent<TowerChibi3D>();
        chibi.Id = id;
        chibi.Model = Instantiate(prefab, go.transform);
        chibi.Model.name = "model";
        chibi.Model.transform.localRotation = Quaternion.Euler(0, 180, 0) * chibi.Model.transform.localRotation;  // FBX faces +Z
        foreach (var a in chibi.Model.GetComponentsInChildren<Animation>()) a.enabled = false;
        foreach (var clip in Resources.LoadAll<AnimationClip>(ResourcePath(id)))
        {
            if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
            int start = clip.name.LastIndexOf("AH_", StringComparison.Ordinal);
            if (start >= 0) chibi.Clips[clip.name.Substring(start)] = clip;
        }
        foreach (var t in chibi.Model.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Prop_", StringComparison.Ordinal) || t.name.StartsWith("Set_", StringComparison.Ordinal))
                chibi.props[t.name] = t.gameObject;
        chibi.ApplyToon();
        chibi.Sample(0f);
        chibi.Height = chibi.MeasureHeight();
        if (targetHeight > 0f && chibi.Height > 0f)
        {
            chibi.Model.transform.localScale *= targetHeight / chibi.Height;
            chibi.Height = targetHeight;
        }
        chibi.Springs = chibi.Model.AddComponent<TowerChibiSprings>();
        chibi.Springs.Setup(chibi.Height, JiggleStrength(id));
        chibi.Play("AH_idle", true);
        return chibi;
    }

    // provenance.json carries the per-character jiggle strength (robots are subtler)
    static float JiggleStrength(string id)
    {
        var text = Resources.Load<TextAsset>("AdamsHaven/TowerChibi3D/" + id + "/provenance");
        if (!text) return 1f;
        var m = Regex.Match(text.text, "\"jiggle_strength\"\\s*:\\s*([0-9.]+)");
        return m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 1f;
    }

    public void PlayRoom(string roomType)
    {
        Play(roomType != null && RoomClips.TryGetValue(roomType, out var c) ? c : "AH_idle");
    }

    public void Play(string clip, bool restart = false)
    {
        if (!Clips.ContainsKey(clip)) clip = "AH_idle";
        if (clip == Clip && !restart) return;
        Clip = clip; time = 0f;
        ClipProps.TryGetValue(clip, out var shown);
        foreach (var p in props) p.Value.SetActive(shown != null && Array.IndexOf(shown, p.Key) >= 0);
        Springs?.ResetState();
    }

    public void Sample(float t)
    {
        if (Clips.TryGetValue(Clip, out var clip))
        {
            float len = Mathf.Max(.01f, clip.length);
            clip.SampleAnimation(Model, clip.wrapMode == WrapMode.ClampForever ? Mathf.Min(t, len - .001f) : Mathf.Repeat(t, len));
        }
    }

    public float ClipTime => time;

    void Update()
    {
        time += Time.deltaTime * Speed;
        Sample(time);
    }

    float MeasureHeight()
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var r in Model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (r.name.StartsWith("Prop_", StringComparison.Ordinal) || r.name.StartsWith("Set_", StringComparison.Ordinal)) continue;
            var mesh = new Mesh();
            r.BakeMesh(mesh, true);
            foreach (var v in mesh.vertices) { float y = r.transform.TransformPoint(v).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            DestroyImmediate(mesh);
        }
        return hi > lo ? hi - lo : 0f;
    }

    void ApplyToon()
    {
        var toon = Shader.Find("AdamsHaven/AnimeToon");
        foreach (var r in Model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!toon) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                var m = new Material(toon);
                var tex = src ? src.mainTexture : null;
                m.SetTexture("_BaseMap", tex ? tex : Texture2D.whiteTexture);
                m.SetColor("_BaseColor", src && !tex ? src.color : Color.white);
                // The width is a fraction of the ortho view height (~1 px at 1080p). The Tower view is ~25 units tall
                // and a chibi only ~1.2, so anything heavier swallows the whole figure in ink.
                m.SetFloat("_OutlineWidth", .001f);
                materials.Add(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    void OnDestroy()
    {
        foreach (var m in materials)
            if (m) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
    }
}
