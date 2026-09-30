using System;
using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// Each real 3D rig is rendered against alpha so it can share the existing IMGUI stage/HUD.
// Clips are sampled on the battle clock: speed changes and impact pauses affect bones too.
public sealed partial class BattleMode
{
    sealed class FieldRig
    {
        public GameObject Stage, Model;
        public Camera Camera;
        public RenderTexture Image;
        public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        public string Action = "AH_battle_guard";
        public float Started, Rate = 1f, Offset;
        public bool Hold;   // knocked down: stay on the last frame instead of returning to guard
        public readonly List<Material> Materials = new List<Material>();
    }
    readonly Dictionary<BattleUnit, FieldRig> fieldRigs = new Dictionary<BattleUnit, FieldRig>();

    // Units with an anime-toon rig (Meshy battle-outfit model + retargeted CZN-style clips, marked by
    // AH_hit_react) and JD's card duelist render in 3D; everyone else uses the painted BattleChibi loops.
    // The older civilian-outfit 3D drafts are skipped. Set true to force every 3D rig back on.
    static readonly bool UseOld3DRigs = false;
    static bool IsAnimeRig(FieldRig rig, BattleUnit unit) { return unit.Id == "jd" || rig.Clips.ContainsKey("AH_hit_react"); }

    // Move sets per unit: clip, start offset into the clip (skips long wind-ups) and first contact time.
    struct Move { public string Clip; public float Start, Impact; public Move(string c, float s, float i) { Clip = c; Start = s; Impact = i; } }
    static readonly Dictionary<string, Move[]> MoveSets = new Dictionary<string, Move[]>
    {
        // basic, skill, ultimate
        { "kaela", new[] { new Move("AH_attack_cross_slash", 0, .52f), new Move("AH_attack_rising_strike", 0, .66f), new Move("AH_flying_kick", 1.7f, 2.54f) } },
    };
    const int AnimeColumns = 6, AnimeRows = 4, AnimeFrames = 24;
    const float AnimeFps = 12f;
    readonly Dictionary<string, Texture2D> animeClips = new Dictionary<string, Texture2D>();

    // Atlas from Tools/build_battle_chibi_atlases.py: 6x4 cells, frame 0 top-left, feet on the cell bottom.
    Texture2D AnimeClip(string id, string clip)
    {
        string key = id + "/" + clip;
        Texture2D tex;
        if (!animeClips.TryGetValue(key, out tex))
        {
            tex = Resources.Load<Texture2D>("AdamsHaven/BattleChibi/" + key);
            if (tex) { tex.filterMode = FilterMode.Trilinear; tex.wrapMode = TextureWrapMode.Clamp; }
            animeClips[key] = tex;
        }
        return tex;
    }
    static Cutout AnimeCell(Texture2D tex, int frame)
    {
        frame = ((frame % AnimeFrames) + AnimeFrames) % AnimeFrames;
        float w = 1f / AnimeColumns, h = 1f / AnimeRows;
        int col = frame % AnimeColumns, row = frame / AnimeColumns;
        return new Cutout { Tex = tex, Uv = new Rect(col * w, 1f - (row + 1) * h, w, h), Aspect = (float)AnimeRows / AnimeColumns };
    }
    // Idle breathing loop; the walk cycle plays while a melee attacker dashes in and back.
    Cutout AnimeFrame(BattleUnit u, UnitVis v, Cutout fallback)
    {
        float age = fx - v.LungeStart;
        bool dashing = !v.Ranged && v.LungeTo != Vector2.zero && age >= 0f && age < v.LungeDur;
        Texture2D tex = dashing ? AnimeClip(u.Id, "walk_in_place") : null;
        if (!tex) tex = AnimeClip(u.Id, "idle");
        if (!tex) return fallback;
        int phase = (u.Id.GetHashCode() & 0x7fffffff) % AnimeFrames;
        return AnimeCell(tex, Mathf.FloorToInt(fx * AnimeFps) + phase);
    }

    void BuildFieldRigs()
    {
        ReleaseFieldRigs();
        AnimationSignal -= AnimateFieldRig;
        AnimationSignal += AnimateFieldRig;
        foreach (var u in battle.Allies) AddFieldRig(u);
        foreach (var u in battle.Reserves) AddFieldRig(u);
        AddFieldRig(battle.Summoner);
    }
    void AddFieldRig(BattleUnit unit)
    {
        string path = "AdamsHaven/BattleModels/" + unit.Id + "/model";
        var prefab = Resources.Load<GameObject>(path);
        if (!prefab) return;
        var rig = new FieldRig();
        rig.Stage = new GameObject("Battle rig: " + unit.Id);
        rig.Stage.transform.SetParent(transform, false);
        rig.Stage.transform.position = new Vector3(1000 + fieldRigs.Count * 20, -1000, 0);
        var pivot = new GameObject("Model scale and facing").transform;
        pivot.SetParent(rig.Stage.transform, false);
        rig.Model = Instantiate(prefab, pivot);
        foreach (var a in rig.Model.GetComponentsInChildren<Animation>()) a.enabled = false;
        foreach (var a in rig.Model.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var clip in Resources.LoadAll<AnimationClip>(path))
        {
            if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
            int start = clip.name.LastIndexOf("AH_", StringComparison.Ordinal);
            if (start >= 0) rig.Clips[clip.name.Substring(start)] = clip;
        }
        if (!UseOld3DRigs && !IsAnimeRig(rig, unit)) { Destroy(rig.Stage); return; }
        AnimationClip idle;
        if (rig.Clips.TryGetValue("AH_battle_guard", out idle)) idle.SampleAnimation(rig.Model, 0);
        foreach (var helper in rig.Model.GetComponentsInChildren<MeshRenderer>())
            if (helper.name == "Icosphere") helper.gameObject.SetActive(false);
        var renderers = rig.Model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(rig.Stage); return; }
        // Renderer.bounds can still describe the bind pose immediately after sampling.
        var bounds = new Bounds();
        bool hasPoint = false;
        foreach (var r in renderers)
        {
            if (r.name.StartsWith("Deck", StringComparison.Ordinal) || r.name.StartsWith("PlayingCard", StringComparison.Ordinal)) continue;
            Mesh mesh = null;
            bool baked = r is SkinnedMeshRenderer;
            if (r is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh, true); }
            else { var filter = r.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
            if (!mesh) continue;
            foreach (var vertex in mesh.vertices)
            {
                var point = r.transform.TransformPoint(vertex);
                if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; }
                else bounds.Encapsulate(point);
            }
            if (baked) Destroy(mesh);
        }
        float scale = 2f / Mathf.Max(.01f, bounds.size.y);
        pivot.localScale = Vector3.one * scale;
        pivot.position += new Vector3(-bounds.center.x + rig.Stage.transform.position.x, -bounds.min.y + rig.Stage.transform.position.y, -bounds.center.z + rig.Stage.transform.position.z) * scale;
        // Export faces -Z; show a three-quarter view toward the enemy line.
        pivot.RotateAround(rig.Stage.transform.position, Vector3.up, 155f);
        foreach (var r in renderers)
        {
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            var mats = r.materials;
            foreach (var mat in mats)
            {
                var tex = mat.mainTexture;
                var color = mat.color;
                var toon = Shader.Find("AdamsHaven/AnimeToon");
                if (toon)
                {
                    // Anime cel shading + ink outline; scene lighting is intentionally ignored.
                    mat.shader = toon;
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", color);
                    rig.Materials.Add(mat);
                    continue;
                }
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", .12f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Cull", 0f);
                mat.SetFloat("_ReceiveShadows", 0f);
                mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                rig.Materials.Add(mat);
            }
        }
        var cameraObject = new GameObject("Portrait camera");
        cameraObject.transform.SetParent(rig.Stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 1, -6);
        rig.Camera = cameraObject.AddComponent<Camera>();
        rig.Camera.orthographic = true;
        rig.Camera.orthographicSize = 1.5f;
        rig.Camera.nearClipPlane = .1f; rig.Camera.farClipPlane = 12;
        rig.Camera.clearFlags = CameraClearFlags.SolidColor;
        rig.Camera.backgroundColor = Color.clear;
        rig.Camera.allowHDR = false;
        rig.Image = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rig.Image.Create(); rig.Camera.targetTexture = rig.Image;
        var lightObject = new GameObject("Portrait light");
        lightObject.transform.SetParent(rig.Stage.transform, false);
        lightObject.transform.localPosition = new Vector3(-2, 3, -3);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.range = 9; light.intensity = 35;
        fieldRigs.Add(unit, rig);
    }
    void PlayRig(FieldRig rig, string clip, float start, float rate, bool hold = false)
    {
        if (!rig.Clips.ContainsKey(clip)) return;
        rig.Action = clip; rig.Started = fx; rig.Offset = start; rig.Rate = rate; rig.Hold = hold;
    }
    void AnimateFieldRig(BattleAnimationSignal signal)
    {
        FieldRig hurt;
        if (signal.Target != null && fieldRigs.TryGetValue(signal.Target, out hurt) && !hurt.Hold)
        {
            // Victim reactions: a flinch on damage, a held knockdown when the unit goes down.
            if (signal.Phase == BattleAnimationPhase.Hit) PlayRig(hurt, "AH_hit_react", .35f, 1.6f);
            else if (signal.Phase == BattleAnimationPhase.Down) PlayRig(hurt, "AH_knock_down", .2f, 1.3f, true);
        }
        if (signal.Actor == null) return;
        FieldRig rig;
        if (!fieldRigs.TryGetValue(signal.Actor, out rig) || rig.Hold) return;
        if (signal.Actor.Id == "jd")
        {
            string gesture = signal.Phase == BattleAnimationPhase.DrawCards ? "AH_draw_card"
                : signal.Phase == BattleAnimationPhase.PlayCard ? "AH_place_card" : null;
            if (gesture != null && rig.Clips.ContainsKey(gesture))
            {
                rig.Action = gesture; rig.Started = fx; rig.Rate = 2.5f;
            }
            return;
        }
        if (signal.Phase != BattleAnimationPhase.Action) return;
        var c = signal.Card;
        float want = Mathf.Max(.15f, V(signal.Actor).Impact);
        Move[] set;
        if (c != null && c.Power > 0 && MoveSets.TryGetValue(signal.Actor.Id, out set))
        {
            var m = c.Kind == BattleCardKind.Ultimate ? set[2] : c.Id != null && c.Id.StartsWith("basic_") ? set[0] : set[1];
            if (rig.Clips.ContainsKey(m.Clip)) { PlayRig(rig, m.Clip, m.Start, (m.Impact - m.Start) / want); return; }
        }
        string name; float impact;
        if (c == null || c.Power <= 0) { name = "AH_attack_guard_pulse"; impact = .65f; }
        else if (c.Kind == BattleCardKind.Ultimate) { name = "AH_attack_overhead_burst"; impact = .91f; }
        else if (c.Magic) { name = "AH_attack_cast_release"; impact = .78f; }
        else if (signal.Actor.Role == BattleRole.Ranger) { name = "AH_attack_aimed_shot"; impact = .64f; }
        else if (signal.Actor.Id == "kaela") { name = "AH_attack_rising_strike"; impact = .66f; }
        else { name = "AH_attack_cross_slash"; impact = .52f; }
        PlayRig(rig, name, 0, impact / want);
    }
    void TickFieldRigs()
    {
        foreach (var entry in fieldRigs)
        {
            var rig = entry.Value;
            bool visible = slots.ContainsKey(entry.Key);
            rig.Stage.SetActive(visible);
            if (!visible) continue;
            AnimationClip clip;
            if (!rig.Clips.TryGetValue(rig.Action, out clip)) continue;
            float time = rig.Offset + (fx - rig.Started) * rig.Rate;
            if (rig.Hold) time = Mathf.Min(time, clip.length - .01f);
            else if (rig.Action != "AH_battle_guard" && time >= clip.length)
            {
                rig.Action = "AH_battle_guard"; rig.Started = fx; rig.Rate = 1; rig.Offset = 0;
                if (!rig.Clips.TryGetValue(rig.Action, out clip)) continue;
                time = 0;
            }
            clip.SampleAnimation(rig.Model, Mathf.Repeat(time, Mathf.Max(.01f, clip.length)));
        }
    }
    bool DrawFieldRig(BattleUnit unit, Vector2 foot, float height)
    {
        FieldRig rig;
        if (!fieldRigs.TryGetValue(unit, out rig)) return false;
        float size = height * 1.5f;
        GUI.DrawTexture(new Rect(foot.x - size / 2, foot.y - height * 1.25f, size, size), rig.Image, ScaleMode.StretchToFill, true);
        return true;
    }
    void ReleaseFieldRigs()
    {
        foreach (var rig in fieldRigs.Values)
        {
            if (rig.Camera) rig.Camera.targetTexture = null;
            if (rig.Image) { rig.Image.Release(); Destroy(rig.Image); }
            foreach (var material in rig.Materials) if (material) Destroy(material);
            if (rig.Stage) Destroy(rig.Stage);
        }
        fieldRigs.Clear();
    }
#if UNITY_EDITOR
    public void DebugRigDraw()
    {
        Signal(BattleAnimationPhase.DrawCards, battle.Summoner, null, null);
        fx += .6f; TickFieldRigs();
        foreach (var rig in fieldRigs.Values) rig.Stage.SetActive(true);
    }
    public void DebugRigAttack()
    {
        foreach (var unit in fieldRigs.Keys)
            Signal(unit.Id == "jd" ? BattleAnimationPhase.PlayCard : BattleAnimationPhase.Action, unit, null, BattleCatalog.Basic(unit));
        fx += .3f;
        TickFieldRigs();
        foreach (var rig in fieldRigs.Values) rig.Stage.SetActive(true);
    }
    // Writes each rig's current render target to <dir>/rig-<id>-<clip>.png.
    public void DebugRigSnapshot(string dir)
    {
        foreach (var entry in fieldRigs)
        {
            var img = entry.Value.Image;
            var previous = RenderTexture.active;
            RenderTexture.active = img;
            var tex = new Texture2D(img.width, img.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, img.width, img.height), 0, 0); tex.Apply();
            RenderTexture.active = previous;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "rig-" + entry.Key.Id + "-" + entry.Value.Action + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
    public string DebugRigReport()
    {
        LayoutField(); TickFieldRigs();
        var report = new System.Text.StringBuilder();
        foreach (var entry in fieldRigs)
        {
            var renderer = entry.Value.Model.GetComponentInChildren<SkinnedMeshRenderer>();
            report.AppendLine(entry.Key.Id + ": " + entry.Value.Clips.Count + " clips; size=" + (renderer ? renderer.bounds.size.ToString() : "no mesh"));
        }
        foreach (var rig in fieldRigs.Values) rig.Stage.SetActive(true);
        return report.ToString();
    }
#endif
    void OnDisable() { foreach (var r in fieldRigs.Values) if (r.Stage) r.Stage.SetActive(false); }
    void OnDestroy() { AnimationSignal -= AnimateFieldRig; ReleaseFieldRigs(); }
}
