using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.Video;
using static BattleGui;

// The MEDIA LIBRARY screen: import pictures, videos, sounds and 3D models into the game's slots (MediaLibrary).
//   MOVES     every fighter card, ultimate and awakening: card art, field effect, cinematic, sound
//   FIGHTERS  the party and JD: field model (own, another fighter's, an imported picture or .glb), portrait, card art
//   MONSTERS  every bestiary form: field picture
//   MAPS      battle stages: take the game's out, add your own with a theme
//   SOUNDS    every game sound;  MUSIC  battle, atlas and dungeon loops
// Opened from the battle top bar, the character sheet and the expedition screens. IMGUI on the 1600x900 canvas,
// drawn above everything (GUI.depth) and swallowing input while open.
public sealed class MediaPanel : MonoBehaviour
{
    private const float VW = 1600f, VH = 900f;
    private static readonly Color Gold = new Color(.97f, .82f, .48f), Ice = new Color(.60f, .86f, 1f), Red = new Color(1f, .55f, .50f),
        Mint = new Color(.45f, 1f, .78f), Dim = new Color(.70f, .76f, .85f);
    private static readonly string[] Tabs = { "MOVES", "FIGHTERS", "MONSTERS", "MAPS", "SOUNDS", "MUSIC" };
    public static readonly string[] GameSounds = { "swing", "hit", "crit", "heal", "status", "down", "ult", "card", "victory", "defeat",
        "Sfx/card_draw", "Sfx/card_play", "Sfx/shield_up", "Sfx/shield_block", "Sfx/break", "Sfx/turn_start", "Sfx/enemy_turn",
        "Sfx/buff", "Sfx/debuff", "Sfx/ult_ready", "Sfx/decree", "Sfx/epiphany", "Sfx/enemy_claw", "Sfx/enemy_bite", "Sfx/enemy_magic", "Sfx/enemy_slam",
        "Sfx/hit_fire", "Sfx/hit_water", "Sfx/hit_wind", "Sfx/hit_earth", "Sfx/hit_lightning", "Sfx/hit_light", "Sfx/hit_dark", "Sfx/hit_neutral",
        "Sfx/atlas_reveal", "Sfx/atlas_travel", "Sfx/atlas_complete", "Sfx/tower_activate", "Sfx/room_reveal", "Sfx/trap_spring",
        "Sfx/treasure_open", "Sfx/stairs_descend", "door", "chime", "reward", "click", "confirm", "error", "step", "shift" };
    private static readonly string[] MusicSlots = { "battle", "atlas", "dungeon" };

    public static bool IsOpen { get { return instance != null && instance.open; } }
    private static MediaPanel instance;
    private Action onClosed;
    private bool open;
    private int tab;
    private string selected = "";
    private float listScroll;
    private bool dragging; private float dragStartY, dragStartScroll; private bool dragMoved;
    private Vector2 mouse;
    private string message = ""; private float messageAt = -9f;
    private readonly MediaBrowser browser = new MediaBrowser();
    private bool browsing;
    private readonly Dictionary<string, Texture2D> builtInArt = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, bool> builtInExists = new Dictionary<string, bool>();
    // Video preview.
    private VideoPlayer player; private RenderTexture playerTexture; private string playingKey = "";

    // Opens the library (on a tab, with an item selected: "card id", "unit id", ...). onClosed runs when it closes.
    public static void Show(string tabName = null, string select = null, Action onClosed = null)
    {
        if (instance == null)
        {
            var host = new GameObject("Media Library Panel");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MediaPanel>();
        }
        instance.open = true;
        instance.onClosed = onClosed;
        if (tabName != null) { int t = Array.IndexOf(Tabs, tabName.ToUpperInvariant()); if (t >= 0) instance.tab = t; }
        if (select != null) instance.selected = select;
        instance.listScroll = 0f;
        instance.browsing = false;
        MediaLibrary.PreloadAll();
    }

    public static void Hide()
    {
        if (instance == null || !instance.open) return;
        instance.StopVideo();
        instance.open = false;
        instance.browsing = false;
        var done = instance.onClosed; instance.onClosed = null;
        done?.Invoke();
    }

    private void Say(string text) { message = text; messageAt = Time.unscaledTime; }

    private void OnDestroy() { StopVideo(); if (instance == this) instance = null; }

    private void OnGUI()
    {
        if (!open) return;
        GUI.depth = -100;
        BattleGui.Build();
        Event e = Event.current;
        float scale = Mathf.Min(Screen.width / VW, Screen.height / VH);
        Vector2 offset = new Vector2((Screen.width - VW * scale) * .5f, (Screen.height - VH * scale) * .5f);
        mouse = (e.mousePosition - offset) / scale;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { if (browsing) browsing = false; else Hide(); e.Use(); GUI.matrix = old; return; }
        Fill(new Rect(-VW, -VH, VW * 3f, VH * 3f), new Color(0, 0, .02f, .82f));
        Rect box = new Rect(40f, 28f, 1520f, 844f);
        Round(box, new Color(.02f, .04f, .07f, .985f), 18f);
        Outline(box, Alpha(Gold, .8f), 2f, 18f);
        Text(new Rect(box.x + 28f, box.y + 12f, 600f, 44f), "MEDIA LIBRARY", 30, Gold, TextAnchor.MiddleLeft, true, false, 2f);
        Text(new Rect(box.x + 30f, box.y + 54f, 1100f, 22f), "Add your own art, effects, cinematics, sounds, models and stages. Files are copied onto this device; CLEAR returns a slot to the game's own.", 13, Dim, TextAnchor.MiddleLeft);
        if (Btn(new Rect(box.xMax - 160f, box.y + 16f, 136f, 42f), "CLOSE", true, Red)) { Hide(); GUI.matrix = old; return; }
        for (int i = 0; i < Tabs.Length; i++)
            if (Btn(new Rect(box.x + 28f + i * 160f, box.y + 88f, 150f, 40f), Tabs[i], true, Ice, tab == i, 15)) { tab = i; selected = ""; listScroll = 0f; StopVideo(); }
        Rect body = new Rect(box.x + 28f, box.y + 144f, box.width - 56f, box.height - 168f);
        switch (tab)
        {
            case 0: DrawMoves(body); break;
            case 1: DrawFighters(body); break;
            case 2: DrawMonsters(body); break;
            case 3: DrawMaps(body); break;
            case 4: DrawSounds(body); break;
            case 5: DrawMusic(body); break;
        }
        float age = Time.unscaledTime - messageAt;
        if (age < 4f && message.Length > 0)
        {
            float a = age > 3.2f ? (4f - age) / .8f : 1f;
            Rect r = new Rect(VW * .5f - 420f, box.yMax - 46f, 840f, 34f);
            Round(r, new Color(.03f, .05f, .09f, .95f * a), 17f);
            Outline(r, Alpha(Ice, .7f * a), 1.5f, 17f);
            Text(r, message, 14, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
        }
        DrawVideo();
        if (browsing) browser.Draw(new Rect(200f, 70f, 1200f, 760f), mouse);
        // Nothing underneath gets the pointer while the library is up.
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel) e.Use();
        GUI.matrix = old;
    }

    private bool Btn(Rect r, string label, bool enabled, Color accent, bool active = false, int size = 13)
    {
        if (browsing) { CommandSurface(r, accent, enabled, false, active); Text(r, label, size, enabled ? Color.white : new Color(.5f, .54f, .6f), TextAnchor.MiddleCenter, true); return false; }
        return MediaBrowser.Button(r, label, enabled, accent, mouse, active, size);
    }

    // ---- shared list (left column) -------------------------------------------------------------------------

    private struct Row { public string Id, Label, Note; public bool Header; public Color Accent; }

    // Draws a scrolling list; returns the id tapped this frame (or null).
    private string List(Rect r, List<Row> rows)
    {
        Event e = Event.current;
        Round(r, new Color(.03f, .05f, .09f), 12f);
        const float h = 40f;
        float content = rows.Count * h + 8f, max = Mathf.Max(0f, content - r.height);
        if (!browsing)
        {
            if (e.type == EventType.ScrollWheel && r.Contains(mouse)) { listScroll = Mathf.Clamp(listScroll + e.delta.y * 30f, 0f, max); }
            if (e.type == EventType.MouseDown && r.Contains(mouse)) { dragging = true; dragMoved = false; dragStartY = mouse.y; dragStartScroll = listScroll; }
            if (e.type == EventType.MouseDrag && dragging) { if (Mathf.Abs(mouse.y - dragStartY) > 8f) dragMoved = true; listScroll = Mathf.Clamp(dragStartScroll - (mouse.y - dragStartY), 0f, max); }
        }
        bool tap = !browsing && e.type == EventType.MouseUp && dragging && !dragMoved && r.Contains(mouse);
        if (e.type == EventType.MouseUp) dragging = false;
        listScroll = Mathf.Clamp(listScroll, 0f, max);
        string picked = null;
        GUI.BeginGroup(r);
        Vector2 local = mouse - r.position;
        for (int i = 0; i < rows.Count; i++)
        {
            float y = 4f + i * h - listScroll;
            if (y < -h || y > r.height) continue;
            Row row = rows[i];
            Rect rr = new Rect(6f, y, r.width - 12f, h - 4f);
            if (row.Header) { Text(new Rect(rr.x + 8f, rr.y + 6f, rr.width, rr.height), row.Label, 13, Gold, TextAnchor.MiddleLeft, true); continue; }
            bool sel = row.Id == selected, hover = rr.Contains(local);
            Round(rr, sel ? new Color(.12f, .20f, .30f) : hover ? new Color(.08f, .13f, .19f) : new Color(.05f, .08f, .12f), 8f);
            if (sel) Outline(rr, Alpha(row.Accent, .9f), 1.5f, 8f);
            Text(new Rect(rr.x + 12f, rr.y, rr.width - 90f, rr.height), row.Label, 14, Color.white, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(row.Note)) Text(new Rect(rr.xMax - 90f, rr.y, 80f, rr.height), row.Note, 11, Mint, TextAnchor.MiddleRight, true);
            if (tap && hover) picked = row.Id;
        }
        GUI.EndGroup();
        if (picked != null) { selected = picked; StopVideo(); }
        return picked;
    }

    // ---- slots ---------------------------------------------------------------------------------------------

    // One importable slot: title, what is in it now, a thumbnail for pictures, IMPORT / PLAY / CLEAR.
    private void Slot(Rect r, string title, string key, string builtInNote, Texture2D builtInImage = null, string[] kinds = null)
    {
        var entry = MediaLibrary.Get(key);
        Round(r, new Color(.04f, .07f, .11f), 12f);
        Outline(r, Alpha(entry != null ? Mint : Ice, entry != null ? .7f : .3f), 1.4f, 12f);
        Text(new Rect(r.x + 16f, r.y + 8f, 400f, 26f), title, 16, Gold, TextAnchor.MiddleLeft, true);
        string now = entry != null ? "IMPORTED: " + entry.source + (entry.kind == MediaLibrary.Video && entry.length > 0 ? "  (" + entry.length.ToString("0.0") + " s)" : "")
            : builtInNote;
        Text(new Rect(r.x + 16f, r.y + 36f, r.width - 200f, 40f), now, 13, entry != null ? Mint : Dim, TextAnchor.UpperLeft, false, true);
        Texture2D thumb = entry != null && entry.kind == MediaLibrary.Image ? MediaLibrary.ImageFor(key) : builtInImage;
        if (thumb != null)
        {
            Rect t = new Rect(r.xMax - 150f, r.y + 8f, 136f, r.height - 16f);
            Fill(t, new Color(.02f, .03f, .05f));
            GUI.DrawTexture(t, thumb, ScaleMode.ScaleToFit, true);
        }
        kinds = kinds ?? MediaLibrary.Accepts(key);
        float bx = r.x + 16f, by = r.yMax - 44f;
        if (Btn(new Rect(bx, by, 120f, 34f), "IMPORT", true, Ice)) Pick(title, key, kinds);
        bx += 128f;
        bool playable = entry != null && (entry.kind == MediaLibrary.Audio || entry.kind == MediaLibrary.Video);
        if (Btn(new Rect(bx, by, 100f, 34f), playingKey == key ? "STOP" : "PLAY", playable, Mint)) Preview(key, entry);
        bx += 108f;
        if (Btn(new Rect(bx, by, 100f, 34f), "CLEAR", entry != null, Red)) { if (playingKey == key) StopVideo(); MediaLibrary.Clear(key); Say(title + " is back to the game's own."); }
    }

    private void Pick(string title, string key, string[] kinds)
    {
        browsing = true;
        browser.Open("IMPORT  -  " + title, MediaLibrary.Extensions(kinds), path =>
        {
            browsing = false;
            string error = MediaLibrary.Import(key, path);
            if (error != null) { Say(error); return; }
            var entry = MediaLibrary.Get(key);
            if (entry != null && entry.kind == MediaLibrary.Video) Preview(key, entry);      // measures its length
            Say("Imported " + System.IO.Path.GetFileName(path) + ".");
            TowerAudio.Play("confirm", .7f);
        }, () => browsing = false);
    }

    private void Preview(string key, MediaLibrary.Entry entry)
    {
        if (entry == null) return;
        if (playingKey == key) { StopVideo(); return; }
        if (entry.kind == MediaLibrary.Audio)
        {
            var clip = MediaLibrary.AudioFor(key);
            if (clip == null) { Say("Loading the sound, tap PLAY again in a moment."); return; }
            TowerAudio.PlayClip(clip, 1f);
            return;
        }
        if (entry.kind != MediaLibrary.Video) return;
        StopVideo();
        var host = new GameObject("Media preview");
        host.transform.SetParent(transform, false);
        player = host.AddComponent<VideoPlayer>();
        player.playOnAwake = false; player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.source = VideoSource.Url;
        player.url = MediaLibrary.VideoUrl(key);
        string k = key;
        player.prepareCompleted += p =>
        {
            if (p.width > 0)
            {
                playerTexture = new RenderTexture((int)p.width, (int)p.height, 0);
                p.targetTexture = playerTexture;
            }
            MediaLibrary.SetVideoInfo(k, (float)p.length, (int)p.width, (int)p.height);
            p.Play();
        };
        player.errorReceived += (p, msg) => Say("This video cannot be played here: " + msg);
        player.Prepare();
        playingKey = key;
    }

    private void StopVideo()
    {
        if (player != null) { Destroy(player.gameObject); player = null; }
        if (playerTexture != null) { playerTexture.Release(); Destroy(playerTexture); playerTexture = null; }
        playingKey = "";
    }

    private void DrawVideo()
    {
        if (player == null || playerTexture == null || browsing) return;
        Rect r = new Rect(VW - 560f, VH - 360f, 480f, 290f);
        Round(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(0, 0, 0, .95f), 10f);
        GUI.DrawTexture(r, playerTexture, ScaleMode.ScaleToFit, false);
        Outline(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), Alpha(Mint, .8f), 1.5f, 10f);
    }

    private Texture2D BuiltInArt(string path)
    {
        Texture2D t;
        if (!builtInArt.TryGetValue(path, out t)) { t = Resources.Load<Texture2D>("AdamsHaven/" + path); builtInArt[path] = t; }
        return t;
    }

    private bool BuiltInExists<T>(string path) where T : UnityEngine.Object
    {
        bool b;
        string key = typeof(T).Name + ":" + path;
        if (!builtInExists.TryGetValue(key, out b)) { b = Resources.Load<T>("AdamsHaven/" + path) != null; builtInExists[key] = b; }
        return b;
    }

    // ---- MOVES ---------------------------------------------------------------------------------------------

    private static List<BattleCard> MovesOf(BattleUnit unit)
    {
        var list = new List<BattleCard> { BattleCatalog.Basic(unit), BattleCatalog.Guard(unit) };
        list.AddRange(BattleCatalog.FighterKit(unit));
        list.AddRange(BattleCatalog.Ultimates(unit));
        var aw = BattleCatalog.Awakening(unit);
        if (aw != null) list.Add(aw);
        return list;
    }

    private static string Marks(string id)
    {
        int n = 0;
        foreach (var s in new[] { ".art", ".fx", ".cine", ".sfx" }) if (MediaLibrary.Has("card." + id + s)) n++;
        return n > 0 ? n + " SET" : "";
    }

    private void DrawMoves(Rect body)
    {
        var rows = new List<Row>();
        BattleCard chosen = null;
        foreach (var unit in BattleCatalog.Party())
        {
            rows.Add(new Row { Header = true, Label = unit.Name.ToUpperInvariant() });
            foreach (var c in MovesOf(unit))
            {
                rows.Add(new Row { Id = c.Id, Label = c.Name, Note = Marks(c.Id), Accent = Ice });
                if (c.Id == selected) chosen = c;
            }
        }
        rows.Add(new Row { Header = true, Label = "JD  -  SUMMONER" });
        var jd = new List<BattleCard>(BattleCatalog.SummonerKit());
        jd.AddRange(BattleCatalog.SummonerUltimates());
        foreach (var c in jd) { rows.Add(new Row { Id = c.Id, Label = c.Name, Note = Marks(c.Id), Accent = Gold }); if (c.Id == selected) chosen = c; }
        List(new Rect(body.x, body.y, 400f, body.height), rows);
        Rect d = new Rect(body.x + 420f, body.y, body.width - 420f, body.height);
        if (chosen == null) { Text(new Rect(d.x + 20f, d.y + 10f, d.width - 40f, 60f), "Choose a move on the left.", 18, Dim, TextAnchor.UpperLeft); return; }
        string owner = chosen.Owner ?? "";
        Text(new Rect(d.x, d.y, d.width - 200f, 36f), chosen.Name.ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleLeft, true);
        Text(new Rect(d.x, d.y + 34f, d.width - 200f, 22f), owner.ToUpperInvariant() + "  -  " + chosen.Kind + "  -  " + chosen.Id, 13, Dim, TextAnchor.MiddleLeft);
        float y = d.y + 66f, h = 140f, w = (d.width - 16f) / 2f;
        Slot(new Rect(d.x, y, w, h), "CARD ART", "card." + chosen.Id + ".art",
            BuiltInExists<Texture2D>("Cards/" + chosen.Id) ? "Game art: painted card." : "Game art: the fighter's card portrait.",
            BuiltInArt("Cards/" + chosen.Id) ?? BuiltInArt("FullCards/" + owner));
        Slot(new Rect(d.x + w + 16f, y, w, h), "FIELD EFFECT", "card." + chosen.Id + ".fx",
            BuiltInExists<Texture2D>("Fx/Moves/" + chosen.Id) || BuiltInExists<VideoClip>("Fx/Moves/" + chosen.Id) ? "Game effect: painted for this move." :
            "Game effect: element slash and burst. A picture pops at the hit; a video plays over the target (black is see-through).");
        y += h + 14f;
        Slot(new Rect(d.x, y, w, h), "CINEMATIC", "card." + chosen.Id + ".cine",
            BuiltInExists<VideoClip>("UltCutIns/" + chosen.Id) ? "Game cinematic: rendered clip." : chosen.Kind == BattleCardKind.Ultimate || chosen.Kind == BattleCardKind.Awakening
            ? "Game cinematic: the card slide-in." : "None: a video here makes this move cinematic (CINE setting).");
        Slot(new Rect(d.x + w + 16f, y, w, h), "SOUND", "card." + chosen.Id + ".sfx",
            BuiltInExists<AudioClip>("Audio/Moves/" + chosen.Id) ? "Game sound: recorded for this move." : "Game sound: the shared swing and hit.");
        y += h + 14f;
        Text(new Rect(d.x, y, d.width, 60f), "Videos: .mp4 (H.264) plays everywhere; .webm on Android and Windows. Sounds: .ogg, .wav or .mp3. Pictures: .png (transparent is best) or .jpg.",
            12, Dim, TextAnchor.UpperLeft, false, true);
    }

    // ---- FIGHTERS ------------------------------------------------------------------------------------------

    private static readonly string[] ModelIds = { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "jd" };

    private void DrawFighters(Rect body)
    {
        var rows = new List<Row>();
        var units = BattleCatalog.Party();
        units.Add(BattleCatalog.JD());
        BattleUnit chosen = null;
        foreach (var u in units)
        {
            int n = 0;
            foreach (var s in new[] { ".model", ".portrait", ".card" }) if (MediaLibrary.Has("unit." + u.Id + s)) n++;
            string borrowed = MediaLibrary.SwapFor(u.Id);
            rows.Add(new Row { Id = u.Id, Label = u.Name, Note = n > 0 ? n + " SET" : borrowed.Length > 0 ? "SWAPPED" : "", Accent = Ice });
            if (u.Id == selected) chosen = u;
        }
        List(new Rect(body.x, body.y, 400f, body.height), rows);
        Rect d = new Rect(body.x + 420f, body.y, body.width - 420f, body.height);
        if (chosen == null) { Text(new Rect(d.x + 20f, d.y + 10f, d.width - 40f, 60f), "Choose a fighter on the left.", 18, Dim, TextAnchor.UpperLeft); return; }
        Text(new Rect(d.x, d.y, d.width, 36f), chosen.Name.ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleLeft, true);
        // Field model: own, another's, or imported.
        Rect model = new Rect(d.x, d.y + 46f, d.width, 196f);
        Round(model, new Color(.04f, .07f, .11f), 12f);
        Outline(model, Alpha(Ice, .3f), 1.4f, 12f);
        Text(new Rect(model.x + 16f, model.y + 8f, 600f, 26f), "FIELD MODEL", 16, Gold, TextAnchor.MiddleLeft, true);
        string swap = MediaLibrary.SwapFor(chosen.Id);
        var imported = MediaLibrary.Get("unit." + chosen.Id + ".model");
        string now = imported != null ? (imported.kind == MediaLibrary.Model ? "Imported 3D model: " : "Imported picture: ") + imported.source
            : swap.Length > 0 ? "Uses " + Pretty(swap) + "'s model." : "Own model (" + (BattleMode.Has2DRig(chosen.Id) && BattleMode.Prefer2DRigs ? "2D anime rig" :
              BattleMode.Has3DRig(chosen.Id) ? "3D rig" : "painted chibi") + ").";
        Text(new Rect(model.x + 16f, model.y + 36f, model.width - 32f, 22f), now, 13, imported != null || swap.Length > 0 ? Mint : Dim, TextAnchor.MiddleLeft);
        Text(new Rect(model.x + 16f, model.y + 62f, 400f, 22f), "Borrow a model the game has:", 13, Dim, TextAnchor.MiddleLeft);
        float bx = model.x + 16f;
        foreach (string id in ModelIds)
        {
            bool has = BattleMode.Has3DRig(id) || BattleMode.Has2DRig(id) || BuiltInExists<Texture2D>("BattleChibi/" + id + "/idle");
            bool isOwn = id == chosen.Id;
            bool active = isOwn ? swap.Length == 0 : swap == id;
            if (Btn(new Rect(bx, model.y + 88f, 120f, 36f), isOwn ? "OWN" : Pretty(id).ToUpperInvariant(), has || isOwn, Ice, active && imported == null, 12))
            { MediaLibrary.SetSwap(chosen.Id, isOwn ? "" : id); Say(isOwn ? chosen.Name + " uses their own model." : chosen.Name + " now uses " + Pretty(id) + "'s model."); }
            bx += 126f;
        }
        if (Btn(new Rect(model.x + 16f, model.yMax - 50f, 260f, 38f), "IMPORT PICTURE OR .GLB", true, Ice)) Pick("FIELD MODEL", "unit." + chosen.Id + ".model", new[] { MediaLibrary.Image, MediaLibrary.Model });
        if (Btn(new Rect(model.x + 284f, model.yMax - 50f, 170f, 38f), "CLEAR IMPORT", imported != null, Red)) { MediaLibrary.Clear("unit." + chosen.Id + ".model"); Say("Field model import cleared."); }
        Text(new Rect(model.x + 470f, model.yMax - 56f, model.width - 490f, 50f), "Pictures stand on the field as cutouts (transparent .png). 3D models: .glb or .gltf, shown in their rest pose and animated by the battle.", 12, Dim, TextAnchor.MiddleLeft, false, true);
        float w = (d.width - 16f) / 2f, y = model.yMax + 14f;
        Slot(new Rect(d.x, y, w, 150f), "PORTRAIT", "unit." + chosen.Id + ".portrait", "Game art: the chibi head crop.", BuiltInArt("Chibi/" + chosen.Id));
        Slot(new Rect(d.x + w + 16f, y, w, 150f), "CARD ART", "unit." + chosen.Id + ".card", "Game art: full card illustration.", BuiltInArt("FullCards/" + chosen.Id));
    }

    private static string Pretty(string id) { return id.Length == 0 ? id : id == "jd" ? "JD" : char.ToUpperInvariant(id[0]) + id.Substring(1); }

    // ---- MONSTERS ------------------------------------------------------------------------------------------

    private void DrawMonsters(Rect body)
    {
        var rows = new List<Row>();
        string name = null;
        foreach (string id in BattleCatalog.SpeciesIds)
        {
            string label = BattleCatalog.SpeciesName(id);
            rows.Add(new Row { Id = id, Label = label, Note = MediaLibrary.Has("unit." + id + ".model") ? "SET" : "", Accent = Red });
            if (id == selected) name = label;
        }
        List(new Rect(body.x, body.y, 400f, body.height), rows);
        Rect d = new Rect(body.x + 420f, body.y, body.width - 420f, body.height);
        if (name == null) { Text(new Rect(d.x + 20f, d.y + 10f, d.width - 40f, 60f), "Choose a monster on the left.", 18, Dim, TextAnchor.UpperLeft); return; }
        Text(new Rect(d.x, d.y, d.width, 36f), name.ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleLeft, true);
        Text(new Rect(d.x, d.y + 34f, d.width, 22f), BattleCatalog.SpeciesInfo(selected), 13, Dim, TextAnchor.MiddleLeft);
        Slot(new Rect(d.x, d.y + 70f, d.width, 220f), "FIELD PICTURE", "unit." + selected + ".model", "Game art: painted cutout.",
            BuiltInArt("FieldModels/" + selected), new[] { MediaLibrary.Image });
    }

    // ---- MAPS ----------------------------------------------------------------------------------------------

    private void DrawMaps(Rect body)
    {
        Text(new Rect(body.x, body.y, body.width - 300f, 26f), "BATTLE STAGES  -  take the game's out, or add your own (16:9 pictures, 1600 px wide or more).", 15, Gold, TextAnchor.MiddleLeft, true);
        if (Btn(new Rect(body.xMax - 260f, body.y - 6f, 260f, 40f), "ADD A STAGE", true, Mint))
        {
            browsing = true;
            browser.Open("ADD A BATTLE STAGE", MediaLibrary.ImageExt, path =>
            {
                browsing = false;
                string error = MediaLibrary.AddStage(path, "any");
                Say(error ?? "Stage added. Tap its THEME to choose where it appears.");
            }, () => browsing = false);
        }
        const float cw = 280f, ch = 230f, gap = 14f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((body.width + gap) / (cw + gap)));
        var cards = new List<object>();
        foreach (var b in MediaLibrary.BuiltInStages) cards.Add(b);
        foreach (var s in MediaLibrary.CustomStages) cards.Add(s);
        Rect area = new Rect(body.x, body.y + 40f, body.width, body.height - 40f);
        Event e = Event.current;
        int rowsCount = (cards.Count + perRow - 1) / perRow;
        float max = Mathf.Max(0f, rowsCount * (ch + gap) - area.height);
        if (!browsing && e.type == EventType.ScrollWheel && area.Contains(mouse)) listScroll = Mathf.Clamp(listScroll + e.delta.y * 30f, 0f, max);
        if (!browsing && e.type == EventType.MouseDown && area.Contains(mouse)) { dragging = true; dragStartY = mouse.y; dragStartScroll = listScroll; }
        if (!browsing && e.type == EventType.MouseDrag && dragging) listScroll = Mathf.Clamp(dragStartScroll - (mouse.y - dragStartY), 0f, max);
        if (e.type == EventType.MouseUp) dragging = false;
        listScroll = Mathf.Clamp(listScroll, 0f, max);
        GUI.BeginGroup(area);
        Vector2 saved = mouse;
        mouse -= area.position;
        for (int i = 0; i < cards.Count; i++)
        {
            Rect c = new Rect((i % perRow) * (cw + gap), (i / perRow) * (ch + gap) - listScroll, cw, ch);
            if (c.yMax < 0 || c.y > area.height) continue;
            Round(c, new Color(.04f, .07f, .11f), 12f);
            Rect pic = new Rect(c.x + 10f, c.y + 10f, c.width - 20f, 120f);
            Fill(pic, new Color(.02f, .03f, .05f));
            if (cards[i] is MediaLibrary.BuiltInStage)
            {
                var b = (MediaLibrary.BuiltInStage)cards[i];
                bool hidden = MediaLibrary.StageHidden(b.Id);
                var tex = BuiltInArt(b.Resource);
                if (tex != null) { Color was = GUI.color; GUI.color = hidden ? new Color(.4f, .4f, .45f) : Color.white; GUI.DrawTexture(pic, tex, ScaleMode.ScaleAndCrop); GUI.color = was; }
                Outline(c, Alpha(hidden ? Red : Ice, .45f), 1.4f, 12f);
                Text(new Rect(c.x + 12f, c.y + 134f, c.width - 24f, 24f), b.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
                Text(new Rect(c.x + 12f, c.y + 156f, c.width - 24f, 20f), "GAME STAGE  -  " + b.Theme.ToUpperInvariant(), 11, Dim, TextAnchor.MiddleLeft, true);
                if (Btn(new Rect(c.x + 12f, c.yMax - 46f, c.width - 24f, 36f), hidden ? "TAKEN OUT  -  PUT BACK" : "IN USE  -  TAKE OUT", true, hidden ? Red : Mint))
                { MediaLibrary.SetStageHidden(b.Id, !hidden); Say(b.Name + (hidden ? " is back in the rotation." : " is taken out.")); }
            }
            else
            {
                var s = (MediaLibrary.Stage)cards[i];
                var tex = MediaLibrary.StageImage(s);
                if (tex != null) { Color was = GUI.color; GUI.color = s.on ? Color.white : new Color(.4f, .4f, .45f); GUI.DrawTexture(pic, tex, ScaleMode.ScaleAndCrop); GUI.color = was; }
                Outline(c, Alpha(s.on ? Mint : Red, .6f), 1.4f, 12f);
                Text(new Rect(c.x + 12f, c.y + 134f, c.width - 24f, 24f), s.name, 15, Color.white, TextAnchor.MiddleLeft, true);
                float bw = (c.width - 36f) / 3f;
                if (Btn(new Rect(c.x + 12f, c.yMax - 86f, c.width - 24f, 34f), "THEME: " + s.theme.ToUpperInvariant(), true, Ice, false, 12))
                { s.theme = MediaLibrary.Themes[(Array.IndexOf(MediaLibrary.Themes, s.theme) + 1) % MediaLibrary.Themes.Length]; MediaLibrary.UpdateStage(s); }
                if (Btn(new Rect(c.x + 12f, c.yMax - 46f, bw, 36f), s.on ? "ON" : "OFF", true, s.on ? Mint : Red, s.on, 12)) { s.on = !s.on; MediaLibrary.UpdateStage(s); }
                if (Btn(new Rect(c.x + 18f + bw, c.yMax - 46f, bw * 2f, 36f), "REMOVE", true, Red, false, 12)) { MediaLibrary.RemoveStage(s.id); Say("Stage removed."); }
            }
        }
        mouse = saved;
        GUI.EndGroup();
    }

    // ---- SOUNDS and MUSIC ----------------------------------------------------------------------------------

    private void DrawSounds(Rect body)
    {
        var rows = new List<Row>();
        foreach (string id in GameSounds)
            rows.Add(new Row { Id = id, Label = SoundName(id), Note = MediaLibrary.Has("sfx." + id) ? "SET" : "", Accent = Ice });
        List(new Rect(body.x, body.y, 400f, body.height), rows);
        Rect d = new Rect(body.x + 420f, body.y, body.width - 420f, body.height);
        if (Array.IndexOf(GameSounds, selected) < 0) { Text(new Rect(d.x + 20f, d.y + 10f, d.width - 40f, 60f), "Choose a sound on the left.", 18, Dim, TextAnchor.UpperLeft); return; }
        Text(new Rect(d.x, d.y, d.width, 36f), SoundName(selected).ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleLeft, true);
        bool recorded = BuiltInExists<AudioClip>("Audio/" + selected);
        Slot(new Rect(d.x, d.y + 50f, d.width, 150f), "SOUND", "sfx." + selected, recorded ? "Game sound: recorded clip." : "Game sound: synthesised.");
        if (Btn(new Rect(d.x, d.y + 214f, 220f, 38f), "PLAY GAME SOUND", true, Ice)) TowerAudio.Play(selected);
    }

    private static string SoundName(string id)
    {
        string s = id.StartsWith("Sfx/", StringComparison.Ordinal) ? id.Substring(4) : id;
        return s.Replace('_', ' ');
    }

    private void DrawMusic(Rect body)
    {
        Text(new Rect(body.x, body.y, body.width, 26f), "MUSIC  -  loops while you are in each place. Your own tracks: .ogg, .mp3 or .wav.", 15, Gold, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < MusicSlots.Length; i++)
            Slot(new Rect(body.x, body.y + 40f + i * 164f, body.width, 150f), MusicSlots[i].ToUpperInvariant() + " MUSIC", "music." + MusicSlots[i],
                "None: the place's ambience plays alone.");
    }
}
