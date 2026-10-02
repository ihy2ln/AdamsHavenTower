using System;
using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// Character sheet. Press and hold a fighter's profile (party tile bottom-left, a reserve portrait, JD's plate or
// the fighter on the field) to open it. The left half is a model viewer with its own high-resolution rig: drag to
// turn (and to pan once zoomed), scroll or +/- to zoom, play any of the fighter's clips at 0.25x / 0.5x / 1x,
// pause and scrub. The right half is the sheet: rank, element, role, live stats, Celestium weapon and moves.
public sealed partial class BattleMode
{
    private const float HoldTime = 0.45f, HoldSlop = 24f;
    private static readonly float[] SheetSpeeds = { 0.25f, 0.5f, 1f };
    private static readonly Rect SheetBox = new Rect(40f, 34f, 1520f, 832f);
    private static readonly Rect SheetView = new Rect(64f, 112f, 660f, 620f);
    private const int SheetImageW = 1024, SheetImageH = 962;   // same aspect as SheetView

    private BattleUnit holdUnit, holdFocusBefore, sheetUnit;
    private float holdStart;
    private Vector2 holdFrom;
    private FieldRig sheetRig;
    private string sheetClip = "AH_battle_guard";
    // The export faces +Z away from the camera; 160 degrees is a three-quarter front view facing the enemy side.
    private const float SheetFrontYaw = 160f;
    private float sheetTime, sheetSpeed = 0.5f, sheetZoom = 1f, sheetLift, sheetYaw = SheetFrontYaw;
    private bool sheetPaused, sheetDragging, sheetScrubbing;
    private Vector2 sheetMovesScroll, sheetDragLast;

    private struct Profile { public string Title, Rank, Weapon, WeaponNote, Style; }

    private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
    {
        { "kaela", new Profile { Title = "Snow-leopard brawler", Rank = "S", Weapon = "Celestium Ice Gauntlets",
            WeaponNote = "Clawed gauntlets of icy crystal light; every strike lands cold.",
            Style = "Fist fighter: jab and cross, punch combos, a flying fist kick." } },
        { "ghislaine", new Profile { Title = "White-tiger swordswoman", Rank = "S", Weapon = "Celestium Fire Greatsword",
            WeaponNote = "A two-handed blade of burning crystal light around an ember core.",
            Style = "Tiger Cleave: low crouch, blade over the shoulder, explosive diagonal cut." } },
        { "elara", new Profile { Title = "Elf scholar-mage", Rank = "B", Weapon = "Celestium Fountain Pen",
            WeaponNote = "Writes lightning into the air; her spellbook floats at her side.",
            Style = "Ranged caster: lifts the book and looses bolts and barrages." } },
        { "helda", new Profile { Title = "Dwarf brewmaster", Rank = "C", Weapon = "Celestium Dwarf Hammer",
            WeaponNote = "A frost-crystal war hammer with a glowing orb in its head.",
            Style = "Support bruiser: overhead hammer slams and ground strikes." } },
        { "daisy", new Profile { Title = "Bonfire matriarch", Rank = "A", Weapon = "Celestium Fire Spear",
            WeaponNote = "A crystal spear wreathed in orbiting flame.",
            Style = "Lunging thrusts, spins and sweeping spear combos." } },
        { "clarity", new Profile { Title = "Bartender assassin", Rank = "B", Weapon = "Celestium Kunai",
            WeaponNote = "Twin crystal kunai held in a reverse grip.",
            Style = "Spin slashes, a radiant palm strike and a blade storm." } },
        { "jd", new Profile { Title = "Card-duelist summoner", Rank = "", Weapon = "Celestium Deck",
            WeaponNote = "Draws the party's hand and plays the support cards.",
            Style = "Off-field commander: buffs, debuffs and decrees, never a direct attack." } },
    };

    // Clip buttons in display order; a key ending in '_' matches every clip with that prefix.
    private static readonly string[,] ClipLabels =
    {
        { "AH_battle_guard", "IDLE" }, { "AH_attack_basic", "BASIC" }, { "AH_skill_", "SKILL" }, { "AH_ult_", "ULTIMATE" },
        { "AH_hit_react", "HIT" }, { "AH_knock_down", "DOWN" }, { "AH_walk", "WALK" },
        { "AH_draw_card", "DRAW" }, { "AH_place_card", "PLAY CARD" },
    };

    // ---- press and hold ----------------------------------------------------------------------

    private void BeginHold(BattleUnit unit)
    {
        if (unit == null || sheetUnit != null) return;
        holdUnit = unit; holdStart = Time.unscaledTime; holdFrom = mouse; holdFocusBefore = focusUnit;
    }

    private void TrackHold(Event e)
    {
        if (holdUnit == null) return;
        if (e.type == EventType.MouseUp || showLog || confirmWithdraw || battle.Finished
            || (mouse - holdFrom).sqrMagnitude > HoldSlop * HoldSlop) { holdUnit = null; return; }
        if (Time.unscaledTime - holdStart < HoldTime) return;
        BattleUnit unit = holdUnit;
        holdUnit = null;
        focusUnit = holdFocusBefore;   // the tap that started the hold toggled selection; undo it
        OpenSheet(unit);
    }

    private void DrawHoldRing(BattleUnit unit, Rect r)
    {
        if (holdUnit != unit || Event.current.type != EventType.Repaint) return;
        float k = Mathf.Clamp01((Time.unscaledTime - holdStart - 0.08f) / (HoldTime - 0.08f));
        if (k <= 0f) return;
        Fill(new Rect(r.x, r.yMax - r.height * k, r.width, r.height * k), BattleGui.Alpha(Ice, .32f));
        Outline(r, BattleGui.Alpha(Ice, .45f + .55f * k), 2.5f, 10f);
    }

    // ---- open / close / tick -----------------------------------------------------------------

    private void OpenSheet(BattleUnit unit)
    {
        CloseSheet();
        sheetUnit = unit;
        sheetTime = 0f; sheetPaused = false; sheetZoom = 1f; sheetLift = 0f; sheetYaw = SheetFrontYaw;
        sheetSpeed = SheetSpeeds[1];
        sheetMovesScroll = Vector2.zero;
        sheetRig = CreateRig(unit, "Sheet rig: ", new Vector3(2000f, -1000f, 0f), SheetImageW, SheetImageH, sheetYaw);
        sheetClip = "AH_battle_guard";
        if (sheetRig != null) { FrameSheetCamera(); SampleSheet(); }
    }

    private void CloseSheet()
    {
        if (sheetRig != null) ReleaseRig(sheetRig);
        sheetRig = null; sheetUnit = null; holdUnit = null;
        sheetDragging = false; sheetScrubbing = false;
    }

    private void FrameSheetCamera()
    {
        if (sheetRig == null) return;
        // At 1x the view spans -0.3 .. 3.1 units: the 2-unit body plus room for weapons raised overhead.
        // Zoom out (0.6x) for jumps such as Helda's ultimate; drag up / down pans at any zoom.
        sheetZoom = Mathf.Clamp(sheetZoom, 0.6f, 4f);
        float half = 1.7f / sheetZoom;
        sheetLift = Mathf.Clamp(sheetLift, -1.6f, 1.8f);
        sheetRig.Camera.orthographicSize = half;
        sheetRig.Camera.transform.localPosition = new Vector3(0f, 1.4f + sheetLift, -6f);
    }

    // Non-looping moves hold their last frame briefly before repeating, so the follow-through can be read.
    private float SheetLoopLength(AnimationClip clip)
    {
        bool loops = sheetClip == "AH_battle_guard" || sheetClip == "AH_walk";
        return clip.length + (loops ? 0f : 0.6f);
    }

    private void SampleSheet()
    {
        AnimationClip clip;
        if (sheetRig == null || !sheetRig.Clips.TryGetValue(sheetClip, out clip)) return;
        float t = Mathf.Repeat(sheetTime, Mathf.Max(.01f, SheetLoopLength(clip)));
        clip.SampleAnimation(sheetRig.Model, Mathf.Min(t, clip.length - .001f));
        if (Mathf.Abs(sheetRig.Yaw - sheetYaw) > .01f) sheetRig.TurnTo(sheetYaw);
    }

    private void TickSheet(float dt)
    {
        if (sheetRig == null) return;
        if (!sheetPaused) sheetTime += dt * sheetSpeed;
        SampleSheet();
    }

    private void PlaySheetClip(string clip)
    {
        sheetClip = clip; sheetTime = 0f; sheetPaused = false;
        SampleSheet();
    }

    private List<KeyValuePair<string, string>> SheetClips()
    {
        var list = new List<KeyValuePair<string, string>>();
        if (sheetRig == null) return list;
        var names = new List<string>(sheetRig.Clips.Keys);
        names.Sort(StringComparer.Ordinal);
        var taken = new HashSet<string>();
        for (int i = 0; i < ClipLabels.GetLength(0); i++)
        {
            string key = ClipLabels[i, 0];
            bool prefix = key.EndsWith("_", StringComparison.Ordinal);
            foreach (string name in names)
                if (!taken.Contains(name) && (prefix ? name.StartsWith(key, StringComparison.Ordinal) : name == key))
                { list.Add(new KeyValuePair<string, string>(name, ClipLabels[i, 1])); taken.Add(name); }
        }
        foreach (string name in names)
            if (!taken.Contains(name)) list.Add(new KeyValuePair<string, string>(name, Pretty(name).ToUpperInvariant()));
        return list;
    }

    private static string Pretty(string clip)
    {
        string s = clip.StartsWith("AH_", StringComparison.Ordinal) ? clip.Substring(3) : clip;
        foreach (string lead in new[] { "attack_", "skill_", "ult_" })
            if (s.StartsWith(lead, StringComparison.Ordinal) && s.Length > lead.Length) { s = s.Substring(lead.Length); break; }
        s = s.Replace('_', ' ');
        return s.Length == 0 ? clip : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // ---- drawing -----------------------------------------------------------------------------

    private void DrawCharacterSheet()
    {
        BattleUnit u = sheetUnit;
        if (u == null) return;
        modalDrawing = true;
        Color accent = u == battle.Summoner ? Gold : ElementColor(u.Element);
        Fill(new Rect(-VW, -VH, VW * 3f, VH * 3f), new Color(0f, 0f, .02f, .80f));
        Round(SheetBox, new Color(.02f, .035f, .06f, .97f), 22f);
        Outline(SheetBox, BattleGui.Alpha(accent, .85f), 2.2f, 22f);
        if (MiniButton(new Rect(SheetBox.xMax - 66f, SheetBox.y + 16f, 50f, 50f), "X", true, false, EnemyRed, -1f, 20))
        { CloseSheet(); modalDrawing = false; return; }
        // 2D anime art or the 3D model, for fighters that have both (the choice applies to the whole field).
        if (Has2DRig(u.Id) && Has3DRig(u.Id)
            && MiniButton(new Rect(SheetView.xMax - 170f, SheetView.yMax - 52f, 158f, 40f), Prefer2DRigs ? "SHOW 3D MODEL" : "SHOW 2D ART", true, false, Ice, -1f, 12))
        {
            Prefer2DRigs = !Prefer2DRigs;
            BuildFieldRigs();
            OpenSheet(u);
            used = true;
            modalDrawing = false;
            return;
        }
        DrawSheetViewer(u, accent);
        DrawSheetInfo(u, accent);
        if (sheetUnit == null) { modalDrawing = false; return; }
        if (rightPressed || (pressed && !used && !SheetBox.Contains(mouse))) { CloseSheet(); used = true; }
        else if (pressed && !used) used = true;   // the sheet swallows every other press
        modalDrawing = false;
    }

    private void DrawSheetViewer(BattleUnit u, Color accent)
    {
        Event e = Event.current;
        bool paint = e.type == EventType.Repaint;
        Rect view = SheetView;
        Text(new Rect(view.x + 4f, SheetBox.y + 18f, 520f, 30f), "CHARACTER SHEET", 15, BattleGui.Alpha(accent, .9f), TextAnchor.MiddleLeft, true);
        Text(new Rect(view.x + 4f, SheetBox.y + 44f, 640f, 26f), sheetRig != null && sheetRig.Flat ? "2D anime art  -  drag up / down to move, scroll or + / - to zoom"
            : "Drag sideways to turn, up / down to move  -  scroll or + / - to zoom", 12, new Color(.68f, .76f, .86f));
        Round(view, new Color(.03f, .05f, .085f, 1f), 16f);
        if (paint)
        {
            DrawGlow(new Rect(view.x + 40f, view.y + 40f, view.width - 80f, view.height - 120f), BattleGui.Alpha(accent, .10f));
            DrawGlow(new Rect(view.x + 120f, view.yMax - 120f, view.width - 240f, 90f), BattleGui.Alpha(accent, .28f));
            if (sheetRig != null && sheetRig.Image != null) GUI.DrawTexture(view, sheetRig.Image, ScaleMode.ScaleAndCrop, true);
            else
            {
                Texture2D art = Art("FullCards/" + u.Id);
                if (art != null) GUI.DrawTexture(new Rect(view.x + 20f, view.y + 20f, view.width - 40f, view.height - 40f), art, ScaleMode.ScaleToFit, true);
                else DrawPortrait(new Rect(view.x + 130f, view.y + 110f, 400f, 400f), Spr(u.Art), 0.9f);
                Text(new Rect(view.x, view.yMax - 40f, view.width, 30f), "No 3D model for this character yet", 14, new Color(.7f, .78f, .88f), TextAnchor.MiddleCenter);
            }
        }
        Outline(view, BattleGui.Alpha(accent, .5f), 1.6f, 16f);
        if (sheetRig == null) return;

        AnimationClip clip;
        sheetRig.Clips.TryGetValue(sheetClip, out clip);
        if (clip != null)
        {
            Text(new Rect(view.x + 18f, view.y + 12f, 400f, 28f), Pretty(sheetClip), 20, Color.white, TextAnchor.MiddleLeft, true, false, 1.5f);
            Text(new Rect(view.xMax - 220f, view.y + 12f, 200f, 28f), SpeedLabel2(sheetSpeed) + (sheetPaused ? "  -  PAUSED" : ""), 14, Gold, TextAnchor.MiddleRight, true, false, 1f);
        }

        // Pointer: drag turns the model (and pans vertically once zoomed in); the wheel zooms.
        if (e.type == EventType.MouseDown && e.button == 0 && view.Contains(mouse) && !used)
        { sheetDragging = true; sheetDragLast = mouse; used = true; }
        else if (e.type == EventType.MouseDrag && sheetDragging)
        {
            // Deltas come from the virtual-canvas pointer, so turning speed is the same on every screen size.
            Vector2 d = mouse - sheetDragLast;
            sheetDragLast = mouse;
            sheetYaw = Mathf.Repeat(sheetYaw - d.x * 0.5f, 360f);
            sheetLift += d.y * (3.4f / sheetZoom) / view.height;
            FrameSheetCamera();
            e.Use();
        }
        else if (e.type == EventType.MouseUp) { sheetDragging = false; sheetScrubbing = false; }
        else if (e.type == EventType.ScrollWheel && view.Contains(mouse))
        { sheetZoom *= 1f - e.delta.y * 0.06f; FrameSheetCamera(); e.Use(); }

        // Row 1: one button per clip.
        var clips = SheetClips();
        float gap = 6f, y = view.yMax + 12f;
        float w = Mathf.Min(110f, (view.width - gap * (clips.Count - 1)) / Mathf.Max(1, clips.Count));
        for (int i = 0; i < clips.Count; i++)
            if (MiniButton(new Rect(view.x + i * (w + gap), y, w, 40f), clips[i].Value, true, clips[i].Key == sheetClip, accent, -1f, clips[i].Value.Length > 7 ? 10 : 12))
                PlaySheetClip(clips[i].Key);

        // Row 2: play / pause, scrub bar, speed, zoom.
        y += 50f;
        float x = view.x;
        if (MiniButton(new Rect(x, y, 64f, 40f), sheetPaused ? "PLAY" : "PAUSE", clip != null, sheetPaused, Gold, -1f, 11)) sheetPaused = !sheetPaused;
        x += 72f;
        Rect scrub = new Rect(x, y + 13f, 236f, 14f);
        if (clip != null)
        {
            float len = SheetLoopLength(clip);
            float now = Mathf.Min(Mathf.Repeat(sheetTime, Mathf.Max(.01f, len)), clip.length);
            Bar(scrub, now / Mathf.Max(.01f, clip.length), 0f, accent, Color.clear);
            Disc(new Vector2(scrub.x + scrub.width * Mathf.Clamp01(now / Mathf.Max(.01f, clip.length)), scrub.center.y), 9f, Color.white);
            Text(new Rect(scrub.x, y - 8f, scrub.width, 16f), now.ToString("0.00") + " / " + clip.length.ToString("0.00") + " s", 10, new Color(.7f, .78f, .88f), TextAnchor.MiddleCenter);
            Rect grab = new Rect(scrub.x - 8f, y, scrub.width + 16f, 40f);
            if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(mouse) && !used) { sheetScrubbing = true; used = true; }
            if (sheetScrubbing && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                sheetPaused = true;
                sheetTime = Mathf.Clamp01((mouse.x - scrub.x) / scrub.width) * (clip.length - .001f);
                SampleSheet();
                if (e.type == EventType.MouseDrag) e.Use();
            }
        }
        x += 248f;
        for (int i = 0; i < SheetSpeeds.Length; i++)
            if (MiniButton(new Rect(x + i * 60f, y, 56f, 40f), SpeedLabel2(SheetSpeeds[i]), true, Mathf.Approximately(sheetSpeed, SheetSpeeds[i]), Gold, -1f, 12))
            { sheetSpeed = SheetSpeeds[i]; sheetPaused = false; }
        x += 186f;
        if (MiniButton(new Rect(x, y, 42f, 40f), "-", sheetZoom > 0.601f, false, Ice, -1f, 20)) { sheetZoom /= 1.35f; FrameSheetCamera(); }
        if (MiniButton(new Rect(x + 46f, y, 42f, 40f), "+", sheetZoom < 3.99f, false, Ice, -1f, 20)) { sheetZoom *= 1.35f; FrameSheetCamera(); }
        if (MiniButton(new Rect(view.xMax - 64f, y, 64f, 40f), "RESET", true, false, Ice, -1f, 10))
        { sheetZoom = 1f; sheetLift = 0f; sheetYaw = SheetFrontYaw; FrameSheetCamera(); SampleSheet(); }
    }

    private static string SpeedLabel2(float value)
    {
        return value.ToString(value < 1f ? "0.##" : "0", System.Globalization.CultureInfo.InvariantCulture) + "x";
    }

    private void DrawSheetInfo(BattleUnit u, Color accent)
    {
        bool commander = u == battle.Summoner;
        Profile p;
        Profiles.TryGetValue(u.Id, out p);
        float x = 760f, w = SheetBox.xMax - 24f - x - 70f;
        float y = SheetBox.y + 22f;

        Text(new Rect(x, y, w, 50f), u.Name.ToUpperInvariant(), 40, Color.white, TextAnchor.MiddleLeft, true, false, 2f);
        string line = (string.IsNullOrEmpty(p.Title) ? "" : p.Title + "   -   ")
            + (commander ? "Summoner" : u.Element + "  " + u.Role);
        Text(new Rect(x, y + 50f, w, 26f), line, 18, BattleGui.Alpha(accent, 1f), TextAnchor.MiddleLeft, true);
        if (!string.IsNullOrEmpty(p.Rank))
        {
            Vector2 badge = new Vector2(SheetBox.xMax - 130f, y + 40f);
            Disc(badge, 34f, new Color(.05f, .07f, .12f));
            Ring(badge, 34f, 3f, Gold);
            Text(new Rect(badge.x - 34f, badge.y - 22f, 68f, 40f), p.Rank, 32, Gold, TextAnchor.MiddleCenter, true, false, 1.5f);
            Text(new Rect(badge.x - 40f, badge.y + 37f, 80f, 16f), "RANK", 10, new Color(.75f, .8f, .9f), TextAnchor.MiddleCenter, true);
        }

        // Stats: live values for this battle.
        y += 92f;
        var stats = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("HP", u.Hp + " / " + u.MaxHp),
            new KeyValuePair<string, string>("ATTACK", Mathf.RoundToInt(u.Attack).ToString()),
            new KeyValuePair<string, string>("MAGIC", Mathf.RoundToInt(u.Magic).ToString()),
            new KeyValuePair<string, string>("DEFENSE", Mathf.RoundToInt(u.Defense).ToString()),
            new KeyValuePair<string, string>("RESIST", Mathf.RoundToInt(u.Resistance).ToString()),
        };
        if (commander)
        {
            stats.Add(new KeyValuePair<string, string>("SP", battle.Sp + " / " + BattleState.SpMax));
            stats.Add(new KeyValuePair<string, string>("CP", battle.Cp + " / 2"));
        }
        else
        {
            stats.Add(new KeyValuePair<string, string>("SPEED", Mathf.RoundToInt(u.Speed).ToString()));
            stats.Add(new KeyValuePair<string, string>("CRIT", Mathf.RoundToInt(u.CritRate * 100f) + "%"));
            stats.Add(new KeyValuePair<string, string>("CRIT DMG", Mathf.RoundToInt(u.CritDamage * 100f) + "%"));
            stats.Add(new KeyValuePair<string, string>("AP / EP", u.Ap + "/" + u.MaxAp + "  " + u.Ep + "/" + u.MaxEp));
            stats.Add(new KeyValuePair<string, string>("ULTIMATE", u.Ultimate + "%"));
        }
        float cw = (SheetBox.xMax - 24f - x - 4f * 8f) / 5f;
        for (int i = 0; i < stats.Count; i++)
        {
            Rect cell = new Rect(x + (i % 5) * (cw + 8f), y + (i / 5) * 62f, cw, 54f);
            Round(cell, new Color(.04f, .065f, .10f, 1f), 10f);
            Outline(cell, BattleGui.Alpha(accent, .35f), 1.2f, 10f);
            Text(new Rect(cell.x + 10f, cell.y + 4f, cell.width - 20f, 18f), stats[i].Key, 11, new Color(.66f, .74f, .86f), TextAnchor.MiddleLeft, true);
            Text(new Rect(cell.x + 10f, cell.y + 22f, cell.width - 20f, 28f), stats[i].Value, 19, Color.white, TextAnchor.MiddleLeft, true);
        }
        y += stats.Count > 5 ? 128f : 66f;

        // Condition: stress and statuses.
        string condition = commander ? "" : "Stress " + u.Stress + "%" + (u.CollapseRounds > 0 ? "  (BREAKDOWN " + u.CollapseRounds + "T)" : "");
        for (int i = 0; i < u.Statuses.Count; i++)
            condition += (condition.Length > 0 ? "   -   " : "") + (IsBuff(u.Statuses[i].Name) ? "+ " : "- ") + StatusLabel(u.Statuses[i].Name) + " " + u.Statuses[i].Turns + "T";
        if (!u.Alive) condition = "DOWN   -   " + condition;
        if (condition.Length > 0) Text(new Rect(x, y, SheetBox.xMax - 24f - x, 22f), condition, 14, new Color(.85f, .88f, .95f), TextAnchor.MiddleLeft);
        y += 30f;

        // Celestium weapon.
        if (!string.IsNullOrEmpty(p.Weapon))
        {
            Rect box = new Rect(x, y, SheetBox.xMax - 24f - x, 100f);
            Round(box, new Color(.04f, .06f, .10f, 1f), 12f);
            Outline(box, BattleGui.Alpha(Gold, .45f), 1.4f, 12f);
            Text(new Rect(box.x + 14f, box.y + 6f, 300f, 18f), "WEAPON", 11, new Color(.66f, .74f, .86f), TextAnchor.MiddleLeft, true);
            Text(new Rect(box.x + 14f, box.y + 24f, box.width - 28f, 26f), p.Weapon, 20, Gold, TextAnchor.MiddleLeft, true);
            Text(new Rect(box.x + 14f, box.y + 52f, box.width - 28f, 42f), p.WeaponNote + " " + p.Style, 13, new Color(.86f, .9f, .96f), TextAnchor.UpperLeft, false, true);
            y += 108f;
        }

        // Moves and ultimates.
        var moves = new List<BattleCard>();
        if (commander) { moves.AddRange(BattleCatalog.SummonerKit()); moves.AddRange(BattleCatalog.SummonerUltimates()); }
        else { moves.Add(BattleCatalog.Basic(u)); moves.AddRange(BattleCatalog.FighterKit(u)); moves.AddRange(BattleCatalog.Ultimates(u)); }
        Text(new Rect(x, y, 300f, 22f), "MOVES", 13, BattleGui.Alpha(accent, .95f), TextAnchor.MiddleLeft, true);
        y += 26f;
        Rect area = new Rect(x, y, SheetBox.xMax - 24f - x, SheetBox.yMax - 18f - y);
        const float rowH = 44f;
        sheetMovesScroll = GUI.BeginScrollView(area, sheetMovesScroll, new Rect(0f, 0f, area.width - 18f, moves.Count * rowH));
        for (int i = 0; i < moves.Count; i++)
        {
            BattleCard card = moves[i];
            bool ult = card.Kind == BattleCardKind.Ultimate;
            Rect row = new Rect(0f, i * rowH, area.width - 22f, rowH - 4f);
            Round(row, ult ? new Color(.10f, .08f, .03f, 1f) : new Color(.035f, .055f, .09f, 1f), 9f);
            Text(new Rect(row.x + 12f, row.y + 3f, 330f, 20f), card.Name, 15, ult ? Gold : Color.white, TextAnchor.MiddleLeft, true);
            Text(new Rect(row.xMax - 230f, row.y + 3f, 220f, 20f), MoveCost(card), 12, ult ? Gold : Ice, TextAnchor.MiddleRight, true);
            Text(new Rect(row.x + 12f, row.y + 22f, row.width - 24f, 18f), MoveEffect(card), 12, new Color(.78f, .84f, .92f), TextAnchor.MiddleLeft);
        }
        GUI.EndScrollView();
    }

    private static string MoveCost(BattleCard card)
    {
        if (card.Kind == BattleCardKind.Ultimate) return "ULTIMATE  -  " + BattleState.UltimateSpCost + " SP";
        string cost = "";
        if (card.Ap > 0) cost += card.Ap + " AP";
        if (card.Ep > 0) cost += (cost.Length > 0 ? "  " : "") + card.Ep + " EP";
        return cost.Length > 0 ? cost : "FREE";
    }

    private static string MoveEffect(BattleCard card)
    {
        string target;
        switch (card.Target)
        {
            case BattleTarget.Self: target = "Self"; break;
            case BattleTarget.Enemy: target = "One enemy"; break;
            case BattleTarget.AllEnemies: target = "All enemies"; break;
            case BattleTarget.Ally: target = "One ally"; break;
            case BattleTarget.AllAllies: target = "All allies"; break;
            default: target = "No target"; break;
        }
        string text = Description(card).Replace("Choose a target.", "").Trim();
        if (card.Magic) text = "Magic. " + text;
        return target + ".  " + text;
    }

#if UNITY_EDITOR
    // Test hooks: open a sheet without a real press-and-hold, pick a clip and time, and read the viewer image.
    public bool DebugOpenSheet(string unitId)
    {
        if (battle == null) return false;
        BattleUnit unit = unitId == "jd" ? battle.Summoner : battle.Allies.Find(a => a.Id == unitId) ?? battle.Reserves.Find(a => a.Id == unitId);
        if (unit == null) return false;
        OpenSheet(unit);
        return true;
    }

    public void DebugSheetPose(string clip, float time, float yaw, float zoom)
    {
        if (sheetRig == null) return;
        if (sheetRig.Clips.ContainsKey(clip)) sheetClip = clip;
        sheetTime = time; sheetPaused = true; sheetYaw = yaw; sheetZoom = zoom;
        FrameSheetCamera(); SampleSheet();
    }

    public void DebugCloseSheet() { CloseSheet(); }
    public bool DebugSheetOpen { get { return sheetUnit != null; } }
    public RenderTexture DebugSheetImage { get { return sheetRig != null ? sheetRig.Image : null; } }
    public void DebugBeginHold(string unitId)
    {
        BattleUnit unit = battle.Allies.Find(a => a.Id == unitId);
        if (unit != null) BeginHold(unit);
    }
    public float DebugSpeed { get { return speed; } }
#endif
}
