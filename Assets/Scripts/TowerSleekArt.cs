using System.Collections.Generic;
using UnityEngine;

// Loads the sliced UI control sprites under Resources/AdamsHaven/TowerPresentation/UI, sliced by
// UiSliced/slice_sheets.py. Two packs: Sleek/ (sleek_manifest.json, the line icons) and Wuwa/ (wuwa_manifest.json,
// the angular sci-fantasy controls from UiPrompts/BUTTON_PROMPTS_WUWA_FANTASY.md); a Wuwa sheet replaces a Sleek
// sheet of the same name. Each state PNG sits on a shared canvas with a glow margin; Get() crops to the solid body
// (from the manifest) so the art matches the control's rect, and caches the sprite.
public static class TowerSleekArt
{
    private const string Base = "AdamsHaven/TowerPresentation/UI/";
    private static readonly string[] Packs = { "Sleek", "Wuwa" };     // later packs win

    [System.Serializable] private class Entry { public string sheet, state; public int[] canvas, body, pivot; [System.NonSerialized] public string root; }
    [System.Serializable] private class EntryList { public Entry[] items; }

    private static Dictionary<string, Entry> entries;
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static bool loaded, available;

    // False when no art or manifest is present; callers then fall back to the procedural skin.
    public static bool Available { get { Load(); return available; } }

    public static bool Has(string sheet, string state) { Load(); return entries.ContainsKey(sheet + "/" + state); }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        entries = new Dictionary<string, Entry>();
        foreach (var pack in Packs)
        {
            var text = Resources.Load<TextAsset>(Base + pack + "/" + pack.ToLowerInvariant() + "_manifest");
            if (text == null) continue;
            var list = JsonUtility.FromJson<EntryList>("{\"items\":" + text.text + "}");
            if (list == null || list.items == null) continue;
            foreach (var e in list.items)
                if (e != null && e.body != null && e.pivot != null) { e.root = Base + pack + "/"; entries[e.sheet + "/" + e.state] = e; }
        }
        available = entries.Count > 0;
    }

    // Body size in source pixels (the art's solid part, without glow), or zero when missing.
    public static Vector2 BodySize(string sheet, string state)
    {
        Load();
        Entry e;
        return entries.TryGetValue(sheet + "/" + state, out e) ? new Vector2(e.body[0], e.body[1]) : Vector2.zero;
    }

    // Border is (left, bottom, right, top) in pixels of the cropped body. Returns null if the state is missing.
    public static Sprite Get(string sheet, string state, Vector4 border) { return Make(sheet, state, border, false); }

    // The whole canvas including glow (for additive flares and halos).
    public static Sprite GetCanvas(string sheet, string state) { return Make(sheet, state, Vector4.zero, true); }

    private static Sprite Make(string sheet, string state, Vector4 border, bool canvas)
    {
        Load();
        string key = sheet + "/" + state + "/" + border + (canvas ? "/canvas" : "");
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite)) return sprite;
        Entry e;
        if (!entries.TryGetValue(sheet + "/" + state, out e)) return null;
        var texture = Resources.Load<Texture2D>(e.root + sheet + "/" + sheet + "__" + state);
        if (texture == null) return null;
        Rect rect;
        if (canvas) rect = new Rect(0, 0, texture.width, texture.height);
        else
        {
            int bw = e.body[0], bh = e.body[1];
            int x0 = Mathf.Clamp(e.pivot[0] - bw / 2, 0, texture.width - 1);
            int yTop = e.pivot[1] - bh / 2;
            int y0 = Mathf.Clamp(texture.height - (yTop + bh), 0, texture.height - 1);
            rect = new Rect(x0, y0, Mathf.Min(bw, texture.width - x0), Mathf.Min(bh, texture.height - y0));
        }
        sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        sprite.name = sheet + "__" + state;
        cache[key] = sprite;
        return sprite;
    }
}
