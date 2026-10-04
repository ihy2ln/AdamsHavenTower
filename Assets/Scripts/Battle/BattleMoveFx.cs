using System;
using System.Collections.Generic;
using UnityEngine;

// Per-card effect layers (Resources/AdamsHaven/Fx/Moves/moves.json, from Tools/produce_move_fx.py + Tools/move_specs.py).
// A party card's action is drawn from up to three transparent layers over the live field, whether or not the battle
// stages it with the camera (BattleStage.cs):
//   charge - at the fighter's muzzle (clips.json tips) from the start of the action until the release
//   travel - from the muzzle to each target: a stretched beam strip, or a projectile sprite
//   impact - on each target, once on their centroid, on the actor or on the allies it helps
// The wording of each layer comes from the old skill-insert videos, so the battle shows what the cinematic showed.
[Serializable]
public sealed class MoveFile { public int version; public MoveDef[] moves; }

[Serializable]
public sealed class MoveDef
{
    public string card, unit;
    public bool stage;          // a skill the battle stages with the camera (ultimates and awakenings use video)
    public float[] tint;
    public MoveLayer charge, travel, impact;
    public Color Tint { get { return tint != null && tint.Length >= 3 ? new Color(tint[0], tint[1], tint[2], 1f) : Color.white; } }
}

[Serializable]
public sealed class MoveLayer
{
    public string sheet, kind, at;
    public float size, life, lift, width, grow, arc, stagger;
    public int count, frames, cols, rows;
    public bool each, video, hold, under, bottom;
    public float[] offset;
    // JsonUtility creates every nested object even when the JSON leaves it out: a layer exists when it names its art.
    public bool Present { get { return !string.IsNullOrEmpty(sheet); } }
    public int Cols { get { return cols > 0 ? cols : 4; } }
    public int Rows { get { return rows > 0 ? rows : 4; } }
    public int Frames { get { return frames > 0 ? frames : Cols * Rows; } }
}

public sealed partial class BattleMode
{
    const string MovesPath = "AdamsHaven/Fx/Moves/";
    static Dictionary<string, MoveDef> moveDefs;

    static Dictionary<string, MoveDef> MoveDefs
    {
        get
        {
            if (moveDefs != null) return moveDefs;
            moveDefs = new Dictionary<string, MoveDef>();
            var text = Resources.Load<TextAsset>(MovesPath + "moves");
            if (text == null) return moveDefs;
            var file = JsonUtility.FromJson<MoveFile>(text.text);
            if (file != null && file.moves != null)
                foreach (var m in file.moves) if (m != null && !string.IsNullOrEmpty(m.card)) moveDefs[m.card] = m;
            return moveDefs;
        }
    }

    // A party card's layers: its own, else the fighter's default (so no party card falls back to the generic orb).
    public static MoveDef MoveDefFor(BattleCard card, BattleUnit actor)
    {
        if (card == null || actor == null || actor.Enemy) return null;
        MoveDef def;
        if (card.Id != null && MoveDefs.TryGetValue(card.Id, out def)) return def;
        // A combo borrows its performer's own move until it has layers of its own (BattleCombos.LookFor).
        string look = BattleCombos.IsCombo(card) ? BattleCombos.LookFor(card) : null;
        if (look != null && MoveDefs.TryGetValue(look, out def)) return def;
        return MoveDefs.TryGetValue("default_" + actor.Id, out def) ? def : null;
    }

    // The card's own colour (measured from its effect art), else its element's.
    Color CardColor(BattleCard card, BattleUnit actor)
    {
        if (card == null) return Color.white;
        var def = MoveDefFor(card, actor);
        return def != null && def.tint != null && def.tint.Length >= 3 ? def.Tint : ElementColor(card.Element);
    }

    bool HasImpactLayer(BattleCard card, BattleUnit actor)
    {
        var def = MoveDefFor(card, actor);
        return def != null && def.impact != null && def.impact.Present;
    }

    // Effect art, cached for the battle (released with the other move effects).
    readonly Dictionary<string, Texture2D> layerSheets = new Dictionary<string, Texture2D>();
    Texture2D LayerSheet(string name)
    {
        Texture2D t;
        if (string.IsNullOrEmpty(name)) return null;
        if (!layerSheets.TryGetValue(name, out t))
        {
            t = Resources.Load<Texture2D>(MovesPath + name);
            if (t) { t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; }
            layerSheets[name] = t;
        }
        return t;
    }
    void ReleaseLayerSheets()
    {
        foreach (var t in layerSheets.Values) if (t) Resources.UnloadAsset(t);
        layerSheets.Clear();
    }

    // Who the layers land on: the group's fact targets (each once, in order), else the actor.
    static List<BattleUnit> LayerTargets(List<BattleFact> group, BattleUnit actor)
    {
        var list = new List<BattleUnit>();
        foreach (var f in group) if (f.Target != null && !list.Contains(f.Target)) list.Add(f.Target);
        if (list.Count == 0 && actor != null) list.Add(actor);
        return list;
    }

    // The card's layers on the battle timeline: charge at a0, travel from the release, impact at contact.
    void ScheduleMoveLayers(MoveDef def, BattleUnit actor, BattleCard card, List<BattleFact> group, float a0, float release,
        float contact, bool ranged)
    {
        var targets = LayerTargets(group, actor);
        foreach (var layer in new[] { def.charge, def.travel, def.impact })      // load now, not mid-action
            if (layer != null && layer.Present && !layer.video) LayerSheet(layer.sheet);
        if (def.charge != null && def.charge.Present)
        {
            float until = Mathf.Max(.15f, release - a0 + .06f);
            At(a0, () => SpawnCharge(def.charge, actor, until));
        }
        if (def.travel != null && def.travel.Present)
        {
            // Ranged: leaves on the release frame. Melee travel (a trail) rides the dash, the last .12 s before contact.
            float from = ranged ? release : Mathf.Max(a0, contact - .12f);
            float flight = Mathf.Max(.06f, contact - from);
            int count = Mathf.Max(1, def.travel.count);
            foreach (var target in targets)
            {
                if (target == actor && targets.Count == 1 && !ranged) continue;
                for (int k = 0; k < count; k++)
                {
                    BattleUnit to = target;
                    At(from + k * def.travel.stagger, () => SpawnTravel(def.travel, actor, to, flight));
                }
            }
        }
        if (def.impact != null && def.impact.Present)
            At(contact, () => SpawnImpact(def.impact, actor, card, targets));
    }

    void SpawnCharge(MoveLayer layer, BattleUnit actor, float life)
    {
        var sheet = LayerSheet(layer.sheet);
        if (sheet == null || actor == null || !slots.ContainsKey(actor)) return;
        Vector2 offset = layer.offset != null && layer.offset.Length >= 2 ? new Vector2(layer.offset[0], layer.offset[1]) : Vector2.zero;
        effects.Add(new Effect { Kind = 13, Unit = actor, Pos = offset, Born = fx, Life = layer.hold ? life : Mathf.Max(.3f, layer.life),
            Size = layer.size > 0 ? layer.size : 200f, Color = Color.white, Sheet = sheet, Cols = layer.Cols, Rows = layer.Rows,
            Frames = layer.Frames });
    }

    void SpawnTravel(MoveLayer layer, BattleUnit actor, BattleUnit target, float flight)
    {
        var sheet = LayerSheet(layer.sheet);
        if (sheet == null || actor == null || !slots.ContainsKey(actor)) return;
        bool beam = layer.kind == "beam";
        effects.Add(new Effect { Kind = beam ? (byte)14 : (byte)15, Unit = actor, Target = target, Pos = Muzzle(actor), Born = fx,
            Flight = flight, Life = flight + (beam ? Mathf.Max(.12f, layer.life) : .05f),
            Size = layer.width > 0 ? layer.width : beam ? 70f : 140f, Grow = layer.arc, Color = Color.white, Sheet = sheet,
            Cols = layer.Cols, Rows = layer.Rows, Frames = layer.Frames });
    }

    void SpawnImpact(MoveLayer layer, BattleUnit actor, BattleCard card, List<BattleUnit> targets)
    {
        // An effect imported in the media library still replaces the card's own.
        if (card != null && SpawnImportedFx(card, actor, targets.Count > 0 ? targets[0] : actor)) return;
        var on = new List<BattleUnit>();
        if (layer.at == "actor") on.Add(actor);
        else on.AddRange(targets);
        on.RemoveAll(u => u == null || !slots.ContainsKey(u));
        if (on.Count == 0) return;
        float life = layer.life > 0f ? layer.life : .6f;
        if (layer.video)
        {
            var clip = MoveVideo(layer.sheet);
            if (clip != null) { SlotInfo s = Slot(on[0]); PlayFxVideo(clip, s.Foot + new Vector2(0f, -s.H * layer.lift), layer.size); }
            return;
        }
        var sheet = LayerSheet(layer.sheet);
        if (sheet == null) return;
        float cellAspect = (sheet.height / (float)layer.Rows) / Mathf.Max(1f, sheet.width / (float)layer.Cols);
        if (!layer.each)
        {
            Vector2 sum = Vector2.zero, foot = Vector2.zero;
            foreach (var u in on) { sum += Slot(u).Foot; }
            foot = sum / on.Count;
            float h = Slot(on[0]).H;
            AddImpact(layer, sheet, foot, h, cellAspect, life);
            return;
        }
        foreach (var u in on) { SlotInfo s = Slot(u); AddImpact(layer, sheet, s.Foot, s.H, cellAspect, life); }
    }

    void AddImpact(MoveLayer layer, Texture2D sheet, Vector2 foot, float unitH, float cellAspect, float life)
    {
        float size = layer.size > 0 ? layer.size : 380f, h = size * cellAspect;
        Vector2 pos = layer.bottom ? new Vector2(foot.x, foot.y - h * .5f + 12f - unitH * layer.lift) : foot + new Vector2(0f, -unitH * layer.lift);
        effects.Add(new Effect { Kind = 11, Pos = pos, Born = fx, Life = life, Size = size, Color = Color.white, Sheet = sheet,
            Cols = layer.Cols, Rows = layer.Rows, Frames = layer.Frames, Under = layer.under });
    }

    // ---- drawing -------------------------------------------------------------------------------

    static Rect SheetUv(Texture2D sheet, int cols, int rows, int frame)
    {
        int c = frame % cols, r = frame / cols;
        return new Rect(c / (float)cols, 1f - (r + 1) / (float)rows, 1f / cols, 1f / rows);
    }

    // Kind 13: a sheet riding the fighter's muzzle (the charge glow follows the hand as the clip moves).
    void DrawCharge(Effect e, float t)
    {
        if (e.Unit == null || !slots.ContainsKey(e.Unit)) return;
        SlotInfo s = Slot(e.Unit);
        Vector2 at = Muzzle(e.Unit) + new Vector2(e.Pos.x * (e.Unit.Enemy ? -1f : 1f), -e.Pos.y) * s.H * .5f;
        int frame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(t * 1.4f, 1f) * (e.Frames - 1)), 0, e.Frames - 1);
        float cellW = e.Sheet.width / (float)e.Cols, cellH = e.Sheet.height / (float)e.Rows;
        float w = e.Size, h = w * cellH / cellW;
        Color was = GUI.color;
        GUI.color = new Color(1, 1, 1, t > .85f ? (1f - t) / .15f : Mathf.Clamp01(t * 8f));
        GUI.DrawTextureWithTexCoords(new Rect(at.x - w / 2, at.y - h / 2, w, h), e.Sheet, SheetUv(e.Sheet, e.Cols, e.Rows, frame), true);
        GUI.color = was;
    }

    // Kinds 14 / 15: from the release point to the target. A beam grows along its path and thins after contact;
    // a bolt flies (optionally arced) facing its direction of travel. Pointing left mirrors instead of turning upside down.
    void DrawTravel(Effect e)
    {
        Vector2 from = e.Pos;
        Vector2 to = e.Target != null && slots.ContainsKey(e.Target) ? Chest(e.Target) : from + new Vector2(e.Unit != null && e.Unit.Enemy ? -420f : 420f, 0f);
        float age = fx - e.Born;
        float k = Mathf.Clamp01(age / Mathf.Max(.01f, e.Flight));
        Color was = GUI.color;
        Matrix4x4 keep = GUI.matrix;
        if (e.Kind == 14)
        {
            Vector2 d = to - from;
            bool left = d.x < 0f;
            if (left) { GUI.matrix = GUI.matrix * Matrix4x4.TRS(from, Quaternion.identity, new Vector3(-1f, 1f, 1f)) * Matrix4x4.TRS(-from, Quaternion.identity, Vector3.one); d.x = -d.x; }
            BattleGui.RotateAround(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
            float len = d.magnitude * BattleGui.EaseOut(k) + 20f;
            float fade = age > e.Flight ? Mathf.Clamp01(1f - (age - e.Flight) / Mathf.Max(.05f, e.Life - e.Flight)) : 1f;
            float th = e.Size * (.55f + .45f * fade);
            int row = Mathf.FloorToInt(fx * 24f) % Mathf.Max(1, e.Rows);
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(age * 14f) * fade);
            // The strip was rendered edge to edge, so stretching it along the path keeps it continuous.
            GUI.DrawTextureWithTexCoords(new Rect(from.x, from.y - th / 2, len, th), e.Sheet,
                new Rect(0f, 1f - (row + 1) / (float)e.Rows, 1f, 1f / e.Rows), true);
        }
        else
        {
            float arc = e.Grow;
            Vector2 p = Vector2.Lerp(from, to, k) + new Vector2(0f, -Mathf.Sin(k * Mathf.PI) * arc);
            Vector2 ahead = Vector2.Lerp(from, to, Mathf.Min(1f, k + .02f)) + new Vector2(0f, -Mathf.Sin(Mathf.Min(1f, k + .02f) * Mathf.PI) * arc);
            Vector2 d = ahead - p;
            if (d.sqrMagnitude < 1e-4f) d = to - from;
            bool left = d.x < 0f;
            if (left) { GUI.matrix = GUI.matrix * Matrix4x4.TRS(p, Quaternion.identity, new Vector3(-1f, 1f, 1f)) * Matrix4x4.TRS(-p, Quaternion.identity, Vector3.one); d.x = -d.x; }
            BattleGui.RotateAround(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, p);
            int frame = Mathf.FloorToInt(fx * 30f) % Mathf.Max(1, e.Frames);
            float cellW = e.Sheet.width / (float)e.Cols, cellH = e.Sheet.height / (float)e.Rows;
            float w = e.Size, h = w * cellH / cellW;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(age * 12f) * (k >= 1f ? 0f : 1f));
            GUI.DrawTextureWithTexCoords(new Rect(p.x - w * .6f, p.y - h / 2, w, h), e.Sheet, SheetUv(e.Sheet, e.Cols, e.Rows, frame), true);
        }
        GUI.matrix = keep;
        GUI.color = was;
    }
}
