using System;
using System.Collections.Generic;
using UnityEngine;

// Pre-rendered anime fighter clips (Tools/produce_fighter_clips.py: Qwen Image Edit keys -> MiniMax H3 -> BiRefNet).
// Resources/AdamsHaven/BattleClips/<model id>/clips.json lists every action's frames on the atlas pages p0..pN, each
// with its foot anchor, so a frame is drawn straight onto the field at the unit's foot: no camera, no RenderTexture.
// Action names reuse the rigs' AH_* clip names, so the battle drives a clip fighter like any other field rig.
[Serializable]
public sealed class BattleClipManifest
{
    public string unit;
    public int version, fps = 12, ppu = 160;      // ppu: atlas pixels per body unit (the body is 2 units tall)
    public string[] pages;
    public BattleClipAction[] actions;
    public BattleClipCard[] cards;
    public BattleClipCine cine;
    public BattleClipHi[] hi;                     // hi-res pages of the staged actions (drawn large by BattleStage)
}

[Serializable]
public sealed class BattleClipAction
{
    public string name;
    public bool loop, hold, attack;
    public int contact = -1, release = -1;       // frame indices: the hit lands / a projectile leaves the hand
    public int[] frames;                          // 7 ints a frame: page, x, y, w, h (page pixels, top-left), footX, footY
    public float[] tips;                          // per frame: the muzzle (hand / weapon tip) in body units from the foot
    public int Count { get { return frames == null ? 0 : frames.Length / 7; } }
}

// The same frames packed again at a higher ppu (own pages per action, loaded only while the action is staged).
[Serializable]
public sealed class BattleClipHi { public string name; public float ppu; public string[] pages; public int[] frames; }

[Serializable] public sealed class BattleClipCard { public string card, action; }
[Serializable] public sealed class BattleClipCine { public string file; public float footX, footY, ppu; }

public sealed class BattleClipSet
{
    public const string Root = "AdamsHaven/BattleClips/";
    public const string Guard = "AH_battle_guard", Basic = "AH_attack_basic", Hit = "AH_hit_react", Block = "AH_block",
        Down = "AH_knock_down", Victory = "AH_victory", Support = "AH_support", UltFinish = "AH_ult_finish", SkillPrefix = "AH_skill_";
    // A unit only switches to clips once it can show every everyday beat; until then its rig stays.
    public static readonly string[] Required = { Guard, Basic, Hit, Block, Down, Victory };
    // Contact lead on the battle clock, so a clip's own anticipation sets when the hit lands (BattleFx.ScheduleGroup).
    public const float MinLead = .2f, MaxLead = .8f;

    public readonly string Id;
    public readonly BattleClipManifest Manifest;
    readonly Dictionary<string, BattleClipAction> actions = new Dictionary<string, BattleClipAction>();
    readonly Dictionary<string, string> cards = new Dictionary<string, string>();
    Texture2D[] pages;
    Texture2D hi;

    BattleClipSet(string id, BattleClipManifest manifest)
    {
        Id = id; Manifest = manifest;
        if (manifest.actions != null)
            foreach (var a in manifest.actions) if (a != null && !string.IsNullOrEmpty(a.name) && a.Count > 0) actions[a.name] = a;
        if (manifest.cards != null)
            foreach (var c in manifest.cards) if (c != null && !string.IsNullOrEmpty(c.card)) cards[c.card] = c.action;
    }

    public static BattleClipSet Parse(string id, string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var manifest = JsonUtility.FromJson<BattleClipManifest>(json);
        if (manifest == null || manifest.fps <= 0 || manifest.ppu <= 0) return null;
        return new BattleClipSet(id, manifest);
    }

    // One set per model id for the whole battle (field and character sheet share it); Unload() at battle exit.
    static readonly Dictionary<string, BattleClipSet> cache = new Dictionary<string, BattleClipSet>();
    public static BattleClipSet Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        BattleClipSet set;
        if (cache.TryGetValue(id, out set)) return set;
        var text = Resources.Load<TextAsset>(Root + id + "/clips");
        set = text != null ? Parse(id, text.text) : null;
        if (text != null) Resources.UnloadAsset(text);
#if UNITY_EDITOR
        if (set == null) return null;    // in the editor a set can be generated mid-session: don't remember its absence
#endif
        cache[id] = set;
        return set;
    }
    public static bool Exists(string id) { var set = Load(id); return set != null && set.IsComplete; }
    public static void UnloadAll()
    {
        foreach (var set in cache.Values) if (set != null) set.Release();
        cache.Clear();
    }
    void Release()
    {
        if (pages != null) foreach (var p in pages) if (p) Resources.UnloadAsset(p);
        if (hi) Resources.UnloadAsset(hi);
        pages = null; hi = null;
        foreach (var name in new List<string>(hiPages.Keys)) UnloadHi(name);
    }

    // ---- hi-res pages for staged actions (async, so starting a staged skill never hitches) ----
    readonly Dictionary<string, ResourceRequest[]> hiPages = new Dictionary<string, ResourceRequest[]>();
    BattleClipHi HiOf(string action)
    {
        if (Manifest.hi != null)
            foreach (var h in Manifest.hi) if (h != null && h.name == action && h.pages != null && h.pages.Length > 0) return h;
        return null;
    }
    public void LoadHi(string action)
    {
        var h = HiOf(action);
        if (h == null || hiPages.ContainsKey(action)) return;
        var reqs = new ResourceRequest[h.pages.Length];
        for (int i = 0; i < reqs.Length; i++) reqs[i] = Resources.LoadAsync<Texture2D>(Root + Id + "/" + h.pages[i]);
        hiPages[action] = reqs;
    }
    public bool HiReady(string action)
    {
        ResourceRequest[] reqs;
        if (action == null || !hiPages.TryGetValue(action, out reqs)) return false;
        foreach (var r in reqs) if (r == null || !r.isDone || !(r.asset as Texture2D)) return false;
        return true;
    }
    public void UnloadHi(string action)
    {
        ResourceRequest[] reqs;
        if (action == null || !hiPages.TryGetValue(action, out reqs)) return;
        foreach (var r in reqs) if (r != null && r.isDone && r.asset) Resources.UnloadAsset(r.asset);
        hiPages.Remove(action);
    }

    // The muzzle at a frame, in body units from the foot (x toward the enemy, y up); false when the clip has none.
    public bool TipAt(string action, int frame, out Vector2 tip)
    {
        tip = Vector2.zero;
        var a = Get(action);
        if (a == null || a.tips == null || frame < 0 || 2 * frame + 1 >= a.tips.Length) return false;
        tip = new Vector2(a.tips[2 * frame], a.tips[2 * frame + 1]);
        return true;
    }
    // Display seconds to the pose an action is handed over on: the release frame if it throws, else the contact.
    public float KeyTime(string action)
    {
        var a = Get(action);
        if (a == null) return 0f;
        int key = a.release >= 0 ? a.release : a.contact;
        return key < 0 ? 0f : key / (float)Manifest.fps;
    }

    public bool Has(string action) { return action != null && actions.ContainsKey(action); }
    public IEnumerable<string> Actions { get { return actions.Keys; } }
    public bool IsComplete
    {
        get
        {
            foreach (var name in Required) if (!Has(name)) return false;
            foreach (var name in actions.Keys) if (name.StartsWith(SkillPrefix, StringComparison.Ordinal)) return true;
            return false;
        }
    }
    public BattleClipAction Get(string action) { BattleClipAction a; return action != null && actions.TryGetValue(action, out a) ? a : null; }
    // Clip time is display time (1x battle speed), so painted frames play at their authored rate.
    public float Length(string action) { var a = Get(action); return a == null ? 0f : a.Count / (float)Manifest.fps; }
    public bool Loops(string action) { var a = Get(action); return a != null && a.loop; }
    public bool Holds(string action) { var a = Get(action); return a != null && a.hold; }

    // Seconds (display time) from the start of the clip to its contact frame, or -1.
    public float ContactTime(string action)
    {
        var a = Get(action);
        return a == null || a.contact < 0 ? -1f : a.contact / (float)Manifest.fps;
    }
    public float ReleaseTime(string action)
    {
        var a = Get(action);
        return a == null || a.release < 0 ? -1f : a.release / (float)Manifest.fps;
    }
    // Battle seconds from the start of the action to the hit, clamped; `fallback` when the clip has no contact frame.
    public static float Lead(float contactSeconds, float speedBase, float fallback)
    {
        if (contactSeconds < 0f) return fallback;
        return Mathf.Clamp(contactSeconds * speedBase, MinLead, MaxLead);
    }

    // Card -> action. The manifest's card map first (a damaging card mapped to a support action, like Kaela's
    // IceCounter retaliation reusing mv_whiteout_counter, falls through), then generic rules.
    public string ActionFor(BattleCard card)
    {
        if (card == null) return Has(Support) ? Support : null;
        string mapped;
        if (card.Id != null && cards.TryGetValue(card.Id, out mapped) && Has(mapped) && !(card.Power > 0 && mapped == Support))
            return mapped;
        // A combo plays its performer's own move (BattleCombos.LookFor); a joint ultimate plays the lead's finisher.
        string look = BattleCombos.IsCombo(card) ? BattleCombos.LookFor(card) : null;
        if (look != null && cards.TryGetValue(look, out mapped) && Has(mapped)) return mapped;
        if (card.Kind == BattleCardKind.Ultimate) return Has(UltFinish) ? UltFinish : card.Power > 0 ? FirstSkill() : Has(Support) ? Support : null;
        if (card.Kind == BattleCardKind.Awakening) return Has(Support) ? Support : null;
        if (card.Id != null && card.Id.StartsWith("basic_", StringComparison.Ordinal)) return Basic;
        if (card.Id != null && card.Id.StartsWith("guard_", StringComparison.Ordinal)) return Has(Block) ? Block : Has(Support) ? Support : null;
        if (card.Power > 0) return FirstSkill();
        return Has(Support) ? Support : null;
    }
    string FirstSkill()
    {
        if (Manifest.actions != null)
            foreach (var a in Manifest.actions) if (a != null && a.name != null && a.name.StartsWith(SkillPrefix, StringComparison.Ordinal) && a.Count > 0) return a.name;
        return Basic;
    }

    // Frame index at `time` display seconds into the action: looped, held on the last frame, or clamped.
    public int FrameAt(string action, float time)
    {
        var a = Get(action);
        if (a == null) return -1;
        int n = a.Count;
        int f = Mathf.FloorToInt(Mathf.Max(0f, time) * Manifest.fps);
        return a.loop ? ((f % n) + n) % n : Mathf.Min(f, n - 1);
    }

    Texture2D Page(int index)
    {
        if (pages == null)
        {
            int n = Manifest.pages == null ? 0 : Manifest.pages.Length;
            pages = new Texture2D[n];
            for (int i = 0; i < n; i++)
            {
                pages[i] = Resources.Load<Texture2D>(Root + Id + "/" + Manifest.pages[i]);
                if (pages[i]) { pages[i].wrapMode = TextureWrapMode.Clamp; pages[i].filterMode = FilterMode.Bilinear; }
            }
        }
        return index >= 0 && index < pages.Length ? pages[index] : null;
    }
    public Texture2D HighGuard
    {
        get
        {
            if (!hi && Manifest.cine != null && !string.IsNullOrEmpty(Manifest.cine.file))
                hi = Resources.Load<Texture2D>(Root + Id + "/" + Manifest.cine.file);
            return hi;
        }
    }

    // Draws one frame with its foot on `foot` (GUI space, y down); unitPx = screen pixels per body unit.
    // GUI.color and GUI.matrix still apply, so tint, fades, hurt flashes and lean work as for any field image.
    public bool Draw(string action, int frame, Vector2 foot, float unitPx)
    {
        var a = Get(action);
        if (a == null || frame < 0 || frame >= a.Count) return false;
        int k = frame * 7;
        int[] frames = a.frames;
        float ppu = Manifest.ppu;
        Texture2D page;
        var hi = HiReady(action) ? HiOf(action) : null;
        if (hi != null && hi.frames != null && hi.frames.Length >= (frame + 1) * 7)
        {
            frames = hi.frames; ppu = hi.ppu;
            page = hiPages[action][hi.frames[k]].asset as Texture2D;
        }
        else page = Page(a.frames[k]);
        if (!page) return false;
        float x = frames[k + 1], y = frames[k + 2], w = frames[k + 3], h = frames[k + 4];
        float s = unitPx / ppu;
        var dst = new Rect(foot.x - frames[k + 5] * s, foot.y - frames[k + 6] * s, w * s, h * s);
        var uv = new Rect(x / page.width, 1f - (y + h) / page.height, w / page.width, h / page.height);
        GUI.DrawTextureWithTexCoords(dst, page, uv, true);
        return true;
    }
    public bool DrawHighGuard(Vector2 foot, float unitPx)
    {
        var tex = HighGuard;
        var c = Manifest.cine;
        if (!tex || c == null || c.ppu <= 0f) return false;
        float s = unitPx / c.ppu;
        GUI.DrawTexture(new Rect(foot.x - c.footX * s, foot.y - c.footY * s, tex.width * s, tex.height * s), tex, ScaleMode.StretchToFill, true);
        return true;
    }
}

// Field side of the clip fighters: a FieldRig whose Flip is set has no model, camera or render target. Its frames are
// drawn straight from the atlas at the unit's foot, and its clip time runs in display seconds (1x battle speed).
public sealed partial class BattleMode
{
    // Clips are preferred over the 2D/3D rigs once a unit's set is complete; the switch is for comparing them while
    // the rigs are still the fallback (the character sheet shows it).
    public const string ClipsKey = "AdamsHaven.Battle.Clips";
    public static bool PreferClips
    {
        get { return PlayerPrefs.GetInt(ClipsKey, 1) == 1; }
        set { PlayerPrefs.SetInt(ClipsKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool HasClips(string id) { return BattleClipSet.Exists(id); }

    // The battle clock runs at SpeedBase at "1x"; painted frames play at their authored rate, so scale it back up.
    const float ClipRate = 1f / SpeedBase;
    bool victoryPlayed;

    FieldRig CreateClipRig(BattleUnit unit, string model)
    {
        var set = BattleClipSet.Load(model);
        if (set == null || !set.IsComplete) return null;
        var rig = new FieldRig { Flip = set, Flat = true, MoveSet = model };
        StartClip(rig, BattleClipSet.Guard, false);
        rig.Started -= (unit.Id.GetHashCode() & 0xff) * .013f;    // idle loops out of step with each other
        return rig;
    }

    FieldRig ClipRig(BattleUnit unit)
    {
        FieldRig rig;
        return unit != null && fieldRigs.TryGetValue(unit, out rig) && rig.Flip != null ? rig : null;
    }

    void StartClip(FieldRig rig, string action, bool hold)
    {
        rig.Action = action; rig.Started = fx; rig.Offset = 0f; rig.Rate = ClipRate; rig.Hold = hold;
    }

    // Victim: a flinch, a block when a shield took the whole hit, a held knockdown.
    void ClipReaction(FieldRig rig, BattleAnimationSignal signal)
    {
        if (signal.Phase == BattleAnimationPhase.Hit)
        {
            bool blocked = signal.Fact != null && signal.Fact.Kind == "blocked" && rig.Flip.Has(BattleClipSet.Block);
            StartClip(rig, blocked ? BattleClipSet.Block : BattleClipSet.Hit, false);
        }
        else if (signal.Phase == BattleAnimationPhase.Down) StartClip(rig, BattleClipSet.Down, true);
    }

    // Actor: the card's action, timed so its contact frame lands on the hit BattleFx scheduled from the same clip
    // (ImpactLead). When that lead was clamped the clip stretches to keep the contact on the hit.
    void ClipAction(FieldRig rig, BattleAnimationSignal signal)
    {
        string action = rig.Flip.ActionFor(signal.Card);
        if (action == null) return;
        StartClip(rig, action, false);
        UnitVis v = V(signal.Actor);
        if (v.ClipFrom >= 0f)
        {
            // Handed over from a cinematic: start on the pose it ended on, held until the cut lands (ClipStart).
            rig.Offset = rig.Flip.KeyTime(action); rig.Started = v.ClipStart; v.ClipFrom = -1f;
            return;
        }
        float contact = rig.Flip.ContactTime(action), want = v.Impact;
        if (contact > 0f && want > .01f) rig.Rate = contact / want;
    }

    // Back to the guard loop once a one-shot action ends; held actions (knockdown, victory) stay on their last frame.
    void TickClipRig(FieldRig rig)
    {
        if (rig.Hold || rig.Action == BattleClipSet.Guard || rig.Flip.Loops(rig.Action)) return;
        if (rig.Offset + Mathf.Max(0f, fx - rig.Started) * rig.Rate >= rig.Flip.Length(rig.Action)) StartClip(rig, BattleClipSet.Guard, false);
    }

    // `back` draws the frame from that many battle seconds ago (after-images while lunging).
    bool DrawClipRig(FieldRig rig, Vector2 foot, float height, float back = 0f)
    {
        return rig.Flip.Draw(rig.Action, ClipFrame(rig, back), foot, height / 2f);
    }

    // Elapsed clip time never goes negative, so a clip can wait on its key frame until a later start.
    int ClipFrame(FieldRig rig, float back = 0f)
    {
        float t = rig.Offset + Mathf.Max(0f, fx - back - rig.Started) * rig.Rate;
        return rig.Flip.FrameAt(rig.Action, t);
    }

    // Where an effect leaves a fighter: the clip's muzzle at the current frame, else in front of the chest.
    Vector2 Muzzle(BattleUnit unit)
    {
        SlotInfo s = Slot(unit);
        Vector2 off; float lean;
        LungeOffset(V(unit), fx, unit.Enemy, out off, out lean);
        var rig = ClipRig(unit);
        Vector2 tip;
        if (rig != null && rig.Flip.TipAt(rig.Action, ClipFrame(rig), out tip))
            return s.Foot + off + new Vector2((unit.Enemy ? -1f : 1f) * tip.x, -tip.y) * (s.H * .5f);   // tips: x toward the foe
        return Chest(unit) + off + new Vector2((unit.Enemy ? -1f : 1f) * s.W * .3f, 0f);
    }

    // Battle seconds from the start of an action to its hit: a clip fighter's contact frame, else the fixed beats.
    float ImpactLead(BattleUnit actor, BattleCard card, bool offense, bool ranged)
    {
        float fallback = offense ? (ranged ? .50f : .30f) : .34f;
        FieldRig rig;
        if (actor == null || card == null || !fieldRigs.TryGetValue(actor, out rig) || rig.Flip == null) return fallback;
        return BattleClipSet.Lead(rig.Flip.ContactTime(rig.Flip.ActionFor(card)), SpeedBase, fallback);
    }
    // When a ranged clip lets go of its projectile (battle seconds after the action starts), or the old .16.
    float ReleaseLead(BattleUnit actor, BattleCard card, float impact)
    {
        FieldRig rig;
        if (actor == null || card == null || !fieldRigs.TryGetValue(actor, out rig) || rig.Flip == null) return .16f;
        float release = rig.Flip.ReleaseTime(rig.Flip.ActionFor(card));
        float contact = rig.Flip.ContactTime(rig.Flip.ActionFor(card));
        if (release < 0f || contact <= 0f) return Mathf.Min(.16f, impact * .5f);
        return Mathf.Clamp(impact * release / contact, 0f, impact - .05f);
    }

    // A finished battle: the winning side's standing units celebrate before the result panel (Busy holds it back
    // until queueEnd): the fighters after a win, the monsters (BattleEnemyClips.cs) after a loss.
    void ScheduleVictory()
    {
        if (victoryPlayed || battle == null || !battle.Finished) return;
        victoryPlayed = true;
        bool winners = battle.Victory;
        bool any = false;
        foreach (var entry in fieldRigs)
            if (entry.Value.Flip != null && entry.Key.Alive && slots.ContainsKey(entry.Key) && entry.Key.Enemy != winners) any = true;
        if (!any) return;
        float t = Mathf.Max(queueEnd, fx) + .15f;
        At(t, () =>
        {
            foreach (var entry in fieldRigs)
                if (entry.Value.Flip != null && entry.Key.Alive && entry.Key.Enemy != winners && entry.Value.Flip.Has(BattleClipSet.Victory))
                    StartClip(entry.Value, BattleClipSet.Victory, true);
        });
        queueEnd = t + .8f;      // battle seconds: about 1.6 s at 1x, the clip plus a beat on the held pose
    }

    // Character sheet: the selected action with the framing the sheet camera uses for rigs (1.7 units half-height at
    // 1x zoom, centred 1.4 units up, dragged up / down by sheetLift).
    void DrawClipSheet(Rect view)
    {
        var set = sheetRig.Flip;
        string action = set.Has(sheetClip) ? sheetClip : BattleClipSet.Guard;
        float len = set.Length(action);
        float t = Mathf.Min(Mathf.Repeat(sheetTime, Mathf.Max(.01f, SheetLoopLength(len))), len - .001f);
        float unitPx = view.height / (2f * 1.7f / Mathf.Clamp(sheetZoom, .6f, 4f));
        GUI.BeginGroup(view);
        set.Draw(action, set.FrameAt(action, t), new Vector2(view.width * .5f, view.height * .5f + (1.4f + sheetLift) * unitPx), unitPx);
        GUI.EndGroup();
    }
}
