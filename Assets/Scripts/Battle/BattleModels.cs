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
        public Transform Pivot;
        public float Yaw;   // degrees the pivot is turned about the stage's vertical axis
        public Camera Camera;
        public RenderTexture Image;
        public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        public string Action = "AH_battle_guard";
        public float Started, Rate = 1f, Offset;
        public bool Hold;   // knocked down: stay on the last frame instead of returning to guard
        public bool Flat;   // a 2D anime cutout rig: drawn facing the enemy line, it cannot turn
        // Pre-rendered anime clips (BattleClips.cs): no model, camera or image; frames drawn from the atlas at the foot.
        public BattleClipSet Flip;
        // A model imported in the media library: no clips, so the battle animates it procedurally (idle sway, a
        // lunge lean when it acts, a recoil when hit, a fall when it goes down).
        public bool Imported;
        public float KickStart = -9f, HurtStart = -9f;
        public string MoveSet = "";
        public readonly List<Material> Materials = new List<Material>();

        public void TurnTo(float yaw)
        {
            if (!Flat) Pivot.RotateAround(Stage.transform.position, Vector3.up, yaw - Yaw);
            Yaw = yaw;
        }
    }
    readonly Dictionary<BattleUnit, FieldRig> fieldRigs = new Dictionary<BattleUnit, FieldRig>();

    // Units with an anime-toon rig (Meshy battle-outfit model + retargeted CZN-style clips, marked by
    // AH_hit_react) and JD's card duelist render in 3D; everyone else uses the painted BattleChibi loops.
    // The older civilian-outfit 3D drafts are skipped. Set true to force every 3D rig back on.
    static readonly bool UseOld3DRigs = false;
    // Field camera: the 2-unit-tall model plus room for weapons raised overhead (Ghislaine's cleave reaches ~3 units).
    const float FieldCamCenter = 1.2f, FieldCamHalf = 1.75f;
    const int FieldImage = 600;
    static bool IsAnimeRig(FieldRig rig, BattleUnit unit) { return unit.Id == "jd" || rig.Clips.ContainsKey("AH_hit_react"); }

    // Move sets per unit: clip, start offset into the clip (skips long wind-ups) and first contact time.
    struct Move { public string Clip; public float Start, Impact; public Move(string c, float s, float i) { Clip = c; Start = s; Impact = i; } }
    static readonly Dictionary<string, Move[]> MoveSets = new Dictionary<string, Move[]>
    {
        // basic, skill, ultimate
        // Full-body Meshy rigs + Tools/build_battle_rig.py: AH_attack_basic is the authored CZN-timed basic
        // (contact time from Tools/battle_rigs/<id>.json); skill/ultimate use each fighter's own style clips.
        { "kaela", new[] { new Move("AH_attack_basic", 0, .34f), new Move("AH_skill_punch_combo", .9f, 1.33f), new Move("AH_ult_flying_fist_kick", 1.7f, 2.23f) } },
        { "ghislaine", new[] { new Move("AH_attack_basic", 0, .36f), new Move("AH_attack_basic", 0, .36f), new Move("AH_attack_basic", 0, .36f) } },
        { "elara", new[] { new Move("AH_attack_basic", 0, .5f), new Move("AH_skill_cast", .9f, 1.57f), new Move("AH_ult_charged_cast", 1.2f, 1.8f) } },
        { "helda", new[] { new Move("AH_attack_basic", 0, .48f), new Move("AH_skill_ground_slam", .8f, 1.4f), new Move("AH_ult_axe_chop", 2.2f, 2.9f) } },
        { "daisy", new[] { new Move("AH_attack_basic", 0, .34f), new Move("AH_skill_weapon_combo", 0, .5f), new Move("AH_skill_weapon_combo", 0, .5f) } },
        { "clarity", new[] { new Move("AH_attack_basic", 0, .36f), new Move("AH_skill_radiant_palm", 2f, 2.63f), new Move("AH_ult_blade_spin", 1.3f, 1.9f) } },
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
        if (FieldPicture(u) != null) return fallback;
        string model = ModelId(u);
        Texture2D tex = dashing ? AnimeClip(model, "walk_in_place") : null;
        if (!tex) tex = AnimeClip(model, "idle");
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
        // Export faces -Z; the field shows a three-quarter view toward the enemy line.
        var rig = CreateRig(unit, "Battle rig: ", new Vector3(1000 + fieldRigs.Count * 20, -1000, 0), FieldImage, FieldImage, 155f);
        if (rig != null) fieldRigs.Add(unit, rig);
    }

    // One offscreen rig: the unit's model scaled to 2 units tall with its feet on the stage, toon materials,
    // and a transparent orthographic camera rendering into its own image. Null when the unit has no usable model.
    FieldRig CreateRig(BattleUnit unit, string label, Vector3 at, int width, int height, float yaw)
    {
        // Media library: an imported picture means no rig at all (the field draws the picture); an imported .glb/.gltf
        // becomes the rig; otherwise the unit's own model or the one it borrows (model swap).
        if (FieldPicture(unit) != null) return null;
        string loadError;
        GameObject imported = MediaLibrary.ModelFor("unit." + unit.Id + ".model", out loadError);
        if (loadError != null) Debug.LogWarning("Imported model for " + unit.Id + " not used: " + loadError);
        string model = ModelId(unit);
        if (imported == null && PreferClips)
        {
            var clips = CreateClipRig(unit, model);
            if (clips != null) return clips;
        }
        if (imported == null && Prefer2DRigs)
        {
            var flat = CreateFlatRig(unit, model, label, at, width, height);
            if (flat != null) return flat;
        }
        string path = "AdamsHaven/BattleModels/" + model + "/model";
        var prefab = imported == null ? Resources.Load<GameObject>(path) : null;
        if (!prefab && imported == null) return null;
        var rig = new FieldRig { Imported = imported != null, MoveSet = model };
        rig.Stage = new GameObject(label + unit.Id);
        rig.Stage.transform.SetParent(transform, false);
        rig.Stage.transform.position = at;
        var pivot = new GameObject("Model scale and facing").transform;
        pivot.SetParent(rig.Stage.transform, false);
        rig.Pivot = pivot;
        if (imported != null)
        {
            // glTF characters face the other way from the project's battle prefabs: turn them once inside a holder
            // (the holder is what the procedural motion moves).
            var holder = new GameObject("Imported model");
            holder.transform.SetParent(pivot, false);
            imported.transform.SetParent(holder.transform, false);
            imported.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rig.Model = holder;
        }
        else rig.Model = Instantiate(prefab, pivot);
        foreach (var a in rig.Model.GetComponentsInChildren<Animation>()) a.enabled = false;
        foreach (var a in rig.Model.GetComponentsInChildren<Animator>()) a.enabled = false;
        if (!rig.Imported)
            foreach (var clip in Resources.LoadAll<AnimationClip>(path))
            {
                if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                int start = clip.name.LastIndexOf("AH_", StringComparison.Ordinal);
                if (start >= 0) rig.Clips[clip.name.Substring(start)] = clip;
            }
        if (!rig.Imported && !UseOld3DRigs && !IsAnimeRig(rig, model == unit.Id ? unit : new BattleUnit { Id = model })) { Destroy(rig.Stage); return null; }
        AnimationClip idle;
        if (rig.Clips.TryGetValue("AH_battle_guard", out idle)) idle.SampleAnimation(rig.Model, 0);
        foreach (var helper in rig.Model.GetComponentsInChildren<MeshRenderer>())
            if (helper.name == "Icosphere") helper.gameObject.SetActive(false);
        var renderers = rig.Model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(rig.Stage); return null; }
        // Renderer.bounds can still describe the bind pose immediately after sampling.
        var bounds = new Bounds();
        bool hasPoint = false;
        foreach (var r in renderers)
        {
            if (r.name.StartsWith("Deck", StringComparison.Ordinal) || r.name.StartsWith("PlayingCard", StringComparison.Ordinal)
                || r.name.StartsWith("Weapon_", StringComparison.Ordinal)) continue;  // held props must not shrink the body
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
        // Model space and world space differ by the stage offset; the bounds were measured in world space.
        pivot.position += new Vector3(-bounds.center.x + at.x, -bounds.min.y + at.y, -bounds.center.z + at.z) * scale;
        rig.TurnTo(yaw);
        foreach (var r in renderers)
        {
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            bool weapon = r.name.StartsWith("Weapon_", StringComparison.Ordinal);
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
                    if (weapon)
                    {
                        // Celestium weapons are crystal light: no cel shadow, brighter, a strong rim and a fine line.
                        mat.SetColor("_BaseColor", new Color(color.r * 1.25f, color.g * 1.25f, color.b * 1.25f, 1f));
                        mat.SetColor("_ShadeColor", new Color(.92f, .92f, .95f, 1f));
                        mat.SetColor("_RimColor", new Color(1f, .97f, .9f, 1f));
                        mat.SetFloat("_RimStrength", .6f);
                        mat.SetFloat("_RimPower", 3f);
                        mat.SetFloat("_Saturation", 1.2f);
                        mat.SetFloat("_OutlineWidth", .0015f);
                    }
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
        AddRigCamera(rig, width, height);
        var lightObject = new GameObject("Portrait light");
        lightObject.transform.SetParent(rig.Stage.transform, false);
        lightObject.transform.localPosition = new Vector3(-2, 3, -3);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.range = 9; light.intensity = 35;
        return rig;
    }

    // 2D anime cutout rigs (Assets/Editor/Battle2DRigBuilder.cs, Tools/build_2d_rig.py): sprites on a bone hierarchy,
    // two units tall with the feet on the origin, facing the enemy line, with the same AH_* clip names as the 3D rigs.
    // The 2D/3D choice is remembered (the character sheet has the switch); units without a 2D rig stay 3D.
    public const string Rig2DKey = "AdamsHaven.Battle.Rig2D";
    public static bool Prefer2DRigs
    {
        get { return PlayerPrefs.GetInt(Rig2DKey, 1) == 1; }
        set { PlayerPrefs.SetInt(Rig2DKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    static string Rig2DFolder(string id) { return "AdamsHaven/BattleRigs2D/" + id; }
    public static bool Has2DRig(string id) { return Resources.Load<GameObject>(Rig2DFolder(id) + "/model") != null; }
    public static bool Has3DRig(string id) { return Resources.Load<GameObject>("AdamsHaven/BattleModels/" + id + "/model") != null; }

    FieldRig CreateFlatRig(BattleUnit unit, string model, string label, Vector3 at, int width, int height)
    {
        string folder = Rig2DFolder(model);
        var prefab = Resources.Load<GameObject>(folder + "/model");
        if (!prefab) return null;
        var rig = new FieldRig { Flat = true, MoveSet = model };
        rig.Stage = new GameObject(label + unit.Id + " (2D)");
        rig.Stage.transform.SetParent(transform, false);
        rig.Stage.transform.position = at;
        rig.Pivot = new GameObject("Model scale and facing").transform;
        rig.Pivot.SetParent(rig.Stage.transform, false);
        rig.Model = Instantiate(prefab, rig.Pivot);
        foreach (var clip in Resources.LoadAll<AnimationClip>(folder))
            if (clip.name.StartsWith("AH_", StringComparison.Ordinal)) rig.Clips[clip.name] = clip;
        AnimationClip idle;
        if (rig.Clips.TryGetValue("AH_battle_guard", out idle)) idle.SampleAnimation(rig.Model, 0);
        // Painted detail survives better with a sharper target than the toon 3D models need.
        AddRigCamera(rig, Mathf.RoundToInt(width * 1.5f), Mathf.RoundToInt(height * 1.5f));
        return rig;
    }

    static void AddRigCamera(FieldRig rig, int width, int height)
    {
        var cameraObject = new GameObject("Portrait camera");
        cameraObject.transform.SetParent(rig.Stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, FieldCamCenter, -6);
        rig.Camera = cameraObject.AddComponent<Camera>();
        rig.Camera.orthographic = true;
        rig.Camera.orthographicSize = FieldCamHalf;
        rig.Camera.nearClipPlane = .1f; rig.Camera.farClipPlane = 12;
        rig.Camera.clearFlags = CameraClearFlags.SolidColor;
        rig.Camera.backgroundColor = Color.clear;
        rig.Camera.allowHDR = false;
        rig.Image = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rig.Image.Create(); rig.Camera.targetTexture = rig.Image;
    }

    static void ReleaseRig(FieldRig rig)
    {
        if (rig.Camera) rig.Camera.targetTexture = null;
        if (rig.Image) { rig.Image.Release(); Destroy(rig.Image); }
        foreach (var material in rig.Materials) if (material) Destroy(material);
        if (rig.Stage) Destroy(rig.Stage);
    }
    void PlayRig(FieldRig rig, string clip, float start, float rate, bool hold = false)
    {
        if (rig.Flip != null ? !rig.Flip.Has(clip) : !rig.Clips.ContainsKey(clip)) return;
        rig.Action = clip; rig.Started = fx; rig.Offset = start; rig.Rate = rate; rig.Hold = hold;
    }
    void AnimateFieldRig(BattleAnimationSignal signal)
    {
        FieldRig hurt;
        if (signal.Target != null && fieldRigs.TryGetValue(signal.Target, out hurt) && !hurt.Hold)
        {
            if (hurt.Imported)
            {
                if (signal.Phase == BattleAnimationPhase.Hit) hurt.HurtStart = fx;
                else if (signal.Phase == BattleAnimationPhase.Down) { hurt.Hold = true; hurt.HurtStart = fx; }
            }
            // Victim reactions: a flinch on damage, a held knockdown when the unit goes down.
            if (hurt.Flip != null) ClipReaction(hurt, signal);
            else if (signal.Phase == BattleAnimationPhase.Hit) PlayRig(hurt, "AH_hit_react", .35f, 1.6f);
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
        if (rig.Imported) { rig.KickStart = fx; return; }
        if (rig.Flip != null) { ClipAction(rig, signal); return; }
        var c = signal.Card;
        float want = Mathf.Max(.15f, V(signal.Actor).Impact);
        Move[] set;
        if (c != null && c.Power > 0 && MoveSets.TryGetValue(rig.MoveSet.Length > 0 ? rig.MoveSet : signal.Actor.Id, out set))
        {
            var m = c.Kind == BattleCardKind.Ultimate ? set[2] : c.Id != null && c.Id.StartsWith("basic_") ? set[0] : set[1];
            if (rig.Clips.ContainsKey(m.Clip)) { PlayRig(rig, m.Clip, m.Start, (m.Impact - m.Start) / want); return; }
        }
        string name; float impact;
        if (c == null || c.Power <= 0) { name = "AH_attack_guard_pulse"; impact = .65f; }
        else if (c.Kind == BattleCardKind.Ultimate) { name = "AH_attack_overhead_burst"; impact = .91f; }
        else if (c.Magic) { name = "AH_attack_cast_release"; impact = .78f; }
        else if (signal.Actor.Role == BattleRole.Ranger) { name = "AH_attack_aimed_shot"; impact = .64f; }
        else if (rig.MoveSet == "kaela") { name = "AH_attack_rising_strike"; impact = .66f; }
        else { name = "AH_attack_cross_slash"; impact = .52f; }
        PlayRig(rig, name, 0, impact / want);
    }
    void TickFieldRigs()
    {
        foreach (var entry in fieldRigs)
        {
            var rig = entry.Value;
            bool visible = slots.ContainsKey(entry.Key);
            if (rig.Stage) rig.Stage.SetActive(visible);
            if (!visible) continue;
            if (rig.Flip != null) { TickClipRig(rig); continue; }
            if (rig.Imported) { PoseImported(rig, entry.Key); continue; }
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
    // Procedural motion for an imported model (it has no clips): breathe and sway, lean into an attack, recoil from a
    // hit, topple when it goes down. All on the battle clock.
    void PoseImported(FieldRig rig, BattleUnit unit)
    {
        float t = fx + (unit.Id.GetHashCode() & 0xff) * .01f;
        float kick = fx - rig.KickStart, hurt = fx - rig.HurtStart;
        float lean = kick >= 0f && kick < .6f ? Mathf.Sin(kick / .6f * Mathf.PI) * 16f : 0f;
        float recoil = hurt >= 0f && hurt < .35f ? Mathf.Sin(hurt / .35f * Mathf.PI) * -10f : 0f;
        float fall = rig.Hold ? Mathf.Clamp01(hurt / .5f) * 80f : 0f;
        float bob = Mathf.Sin(t * 2.1f) * .025f;
        var tr = rig.Model.transform;
        tr.localRotation = Quaternion.Euler(lean + recoil + fall, Mathf.Sin(t * .7f) * 6f, Mathf.Sin(t * 1.3f) * 2f);
        tr.localPosition = new Vector3(0f, bob / Mathf.Max(.01f, rig.Pivot.localScale.y), 0f);
    }

    bool DrawFieldRig(BattleUnit unit, Vector2 foot, float height)
    {
        FieldRig rig;
        if (!fieldRigs.TryGetValue(unit, out rig)) return false;
        if (rig.Flip != null) return DrawClipRig(rig, foot, height);
        // 1 model unit = height / 2 pixels; the image spans the camera's 2 * FieldCamHalf units.
        float size = height * FieldCamHalf;
        GUI.DrawTexture(new Rect(foot.x - size / 2, foot.y - height * (FieldCamCenter + FieldCamHalf) / 2f, size, size), rig.Image, ScaleMode.StretchToFill, true);
        return true;
    }
    void ReleaseFieldRigs()
    {
        foreach (var rig in fieldRigs.Values) ReleaseRig(rig);
        fieldRigs.Clear();
        CloseSheet();
    }
#if UNITY_EDITOR
    public void DebugRigDraw()
    {
        Signal(BattleAnimationPhase.DrawCards, battle.Summoner, null, null);
        fx += .6f; TickFieldRigs();
        foreach (var rig in fieldRigs.Values) if (rig.Stage) rig.Stage.SetActive(true);
    }
    public void DebugRigAttack()
    {
        foreach (var unit in fieldRigs.Keys)
            Signal(unit.Id == "jd" ? BattleAnimationPhase.PlayCard : BattleAnimationPhase.Action, unit, null, BattleCatalog.Basic(unit));
        fx += .3f;
        TickFieldRigs();
        foreach (var rig in fieldRigs.Values) if (rig.Stage) rig.Stage.SetActive(true);
    }
    // Writes each rig's current render target to <dir>/rig-<id>-<clip>.png.
    public void DebugRigSnapshot(string dir)
    {
        foreach (var entry in fieldRigs)
        {
            var img = entry.Value.Image;
            if (img == null) continue;          // clip fighters have no render target
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
            var renderer = entry.Value.Model ? entry.Value.Model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            report.AppendLine(entry.Key.Id + ": " + entry.Value.Clips.Count + " clips; size=" + (renderer ? renderer.bounds.size.ToString() : "no mesh"));
        }
        foreach (var rig in fieldRigs.Values) if (rig.Stage) rig.Stage.SetActive(true);
        return report.ToString();
    }
#endif
    void OnDisable() { foreach (var r in fieldRigs.Values) if (r.Stage) r.Stage.SetActive(false); }
    void OnDestroy() { AnimationSignal -= AnimateFieldRig; MediaLibrary.Changed -= OnMediaChanged; ReleaseFieldRigs(); }
}
