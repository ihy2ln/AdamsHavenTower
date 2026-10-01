using System.Collections.Generic;
using UnityEngine;

// Top-down dungeon tiles for each expedition theme, painted at runtime so every theme has a full set
// (floor, corridor, wall per neighbour mask, door) until hand-made tile sheets replace them.
// If Resources/AdamsHaven/Expedition/Tiles/<theme>_<part>.png exists it is used instead.
public static class TowerDungeonTiles
{
    public const int Px = 64;
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private sealed class Look
    {
        public Color floorA, floorB, gap, wallA, wallB, face, accent;
        public string floor, wall;
    }

    private static Look For(string theme)
    {
        switch (theme)
        {
            case "cave": return new Look { floorA = C(0x5a4632), floorB = C(0x6e583f), gap = C(0x3a2c20), wallA = C(0x2b2320), wallB = C(0x3d322b),
                face = C(0x6b5a4a), accent = C(0x7a5a34), floor = "earth", wall = "roots" };
            case "marsh": return new Look { floorA = C(0x6d5536), floorB = C(0x80653f), gap = C(0x1f3a3a), wallA = C(0x1d3b36), wallB = C(0x2a5146),
                face = C(0x4e7a58), accent = C(0x8fae5a), floor = "boards", wall = "reeds" };
            case "crystal": return new Look { floorA = C(0x2f3542), floorB = C(0x3a4252), gap = C(0x1a1e27), wallA = C(0x1b1f2b), wallB = C(0x262c3c),
                face = C(0x46506a), accent = C(0x6fe3ff), floor = "slate", wall = "crystals" };
            case "briar": return new Look { floorA = C(0x7a6444), floorB = C(0x8c7550), gap = C(0x4a3b28), wallA = C(0x1e2c1c), wallB = C(0x2c3f27),
                face = C(0x4f6a3b), accent = C(0x9a3b3b), floor = "dirt", wall = "thorns" };
            case "mine": return new Look { floorA = C(0x3b4455), floorB = C(0x485267), gap = C(0x1b2029), wallA = C(0x262c38), wallB = C(0x353d4d),
                face = C(0x5d6a82), accent = C(0xe8a64a), floor = "slate", wall = "masonry" };
            case "blight": return new Look { floorA = C(0x4a4658), floorB = C(0x5a5570), gap = C(0x201a2c), wallA = C(0x1f1a28), wallB = C(0x2e2640),
                face = C(0x5a4a78), accent = C(0xa24bff), floor = "flag", wall = "thorns" };
            case "heartwood": return new Look { floorA = C(0x6f7a6a), floorB = C(0x80907a), gap = C(0x2f3a2c), wallA = C(0x3a4a3a), wallB = C(0x4d6048),
                face = C(0x9aa88a), accent = C(0xf0d070), floor = "flag", wall = "roots" };
            case "keep": return new Look { floorA = C(0x7b5a3a), floorB = C(0x8e6a45), gap = C(0x3e2a1b), wallA = C(0x3a3634), wallB = C(0x4d4845),
                face = C(0x8a837b), accent = C(0x5b3d24), floor = "timber", wall = "masonry" };
            default: return new Look { floorA = C(0x6f7266), floorB = C(0x80847a), gap = C(0x3b3d37), wallA = C(0x34372f), wallB = C(0x454a3e),
                face = C(0x8a8e80), accent = C(0x5f7d3a), floor = "flag", wall = "bricks" };   // ruin
        }
    }

    private static Color C(int hex) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1); }

    private static float Noise(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // Smooth value noise over an 8px lattice for soft painted variation.
    private static float Soft(int x, int y, int seed)
    {
        float fx = x / 8f, fy = y / 8f;
        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
        float tx = fx - ix, ty = fy - iy;
        float a = Mathf.Lerp(Noise(ix, iy, seed), Noise(ix + 1, iy, seed), tx);
        float b = Mathf.Lerp(Noise(ix, iy + 1, seed), Noise(ix + 1, iy + 1, seed), tx);
        return Mathf.Lerp(a, b, ty);
    }

    private static Sprite Make(string key, Color[] pixels)
    {
        var tex = new Texture2D(Px, Px, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, Px, Px), new Vector2(0.5f, 0.5f), Px);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }

    private static Sprite Override(string key)
    {
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite)) return sprite;
        var tex = Resources.Load<Texture2D>("AdamsHaven/Expedition/Tiles/" + key);
        if (tex == null) return null;
        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        cache[key] = sprite;
        return sprite;
    }

    public static Sprite Floor(string theme, int variant, bool corridor)
    {
        string key = theme + (corridor ? "_corridor_" : "_floor_") + variant;
        Sprite sprite = Override(key);
        if (sprite != null) return sprite;
        var look = For(theme);
        var px = new Color[Px * Px];
        int seed = variant * 31 + (corridor ? 7 : 0) + theme.Length * 101;
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                float n = Soft(x, y, seed) * 0.7f + Noise(x, y, seed) * 0.3f;
                Color c = Color.Lerp(look.floorA, look.floorB, n);
                switch (look.floor)
                {
                    case "flag":
                    {
                        int row = y / 21, off = row % 2 == 0 ? 0 : 16;
                        int sx = ((x + off) % 64) / 32;
                        bool mortar = y % 21 < 2 || (x + off) % 32 < 2;
                        float stone = Noise(sx + row * 5, row, seed);
                        c = Color.Lerp(c, look.floorB * (0.85f + stone * 0.3f), 0.5f);
                        if (Soft(x + 40, y, seed + 3) > 0.72f) c = Color.Lerp(c, look.accent, 0.45f);   // moss
                        if (mortar) c = look.gap;
                        break;
                    }
                    case "earth":
                        if (Noise(x / 3, y / 3, seed + 9) > 0.93f) c = Color.Lerp(c, look.gap, 0.7f);    // pebbles
                        break;
                    case "boards":
                        if (y % 16 < 2) c = look.gap;
                        else c *= 0.9f + 0.2f * Noise(0, y / 16 + x / 48, seed);
                        break;
                    case "slate":
                        if (Mathf.Abs(Mathf.Sin((x + y * 0.6f + seed) * 0.21f) + Soft(x, y, seed + 5) - 0.9f) < 0.05f)
                            c = Color.Lerp(c, look.accent, 0.55f);                                   // crystal veins
                        break;
                    case "dirt":
                        if (Noise(x / 2, y / 2, seed + 4) > 0.95f) c = Color.Lerp(c, look.accent, 0.5f); // fallen berries
                        break;
                    case "timber":
                        if (x % 16 < 2) c = look.gap;
                        else c *= 0.88f + 0.24f * Noise(x / 16, 0, seed);
                        break;
                }
                if (corridor) c *= 0.82f;
                c.a = 1;
                px[y * Px + x] = c;
            }
        return Make(key, px);
    }

    // mask: 1 = open floor above, 2 = right, 4 = below, 8 = left.
    public static Sprite Wall(string theme, int mask)
    {
        string key = theme + "_wall_" + mask;
        Sprite sprite = Override(key);
        if (sprite != null) return sprite;
        var look = For(theme);
        var px = new Color[Px * Px];
        int seed = theme.Length * 57 + 3;
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                float n = Soft(x, y, seed);
                Color c = Color.Lerp(look.wallA, look.wallB, n);
                switch (look.wall)
                {
                    case "bricks":
                    case "masonry":
                    {
                        int h = look.wall == "bricks" ? 12 : 16;
                        int row = y / h, off = row % 2 == 0 ? 0 : h;
                        if (y % h < 2 || (x + off) % (h * 2) < 2) c *= 0.6f;
                        break;
                    }
                    case "roots":
                        if (Mathf.Abs(Mathf.Sin(x * 0.18f + Soft(x, y, seed + 2) * 5f) * 20 + 32 - y) < 1.6f) c = look.accent;
                        break;
                    case "reeds":
                        if (x % 7 == 0 && Noise(x, 0, seed) > 0.4f && y > Noise(x, 1, seed) * 30) c = Color.Lerp(c, look.accent, 0.7f);
                        break;
                    case "crystals":
                    {
                        float cx = 20 + 24 * Noise(1, 1, seed), dx = Mathf.Abs(x - cx);
                        if (dx < (Px - y) * 0.18f && y > 18) c = Color.Lerp(look.accent, Color.white, (Px - y) / 90f);
                        break;
                    }
                    case "thorns":
                        if (Noise(x / 4, y / 4, seed + 7) > 0.86f) c = Color.Lerp(c, look.accent, 0.6f);
                        break;
                }
                // Faces that meet open floor get a lit edge and a dark lip; Unity textures run bottom-up.
                int top = Px - 1 - y;
                float edge = 0;
                if ((mask & 1) != 0 && top < 8) edge = Mathf.Max(edge, 1 - top / 8f);
                if ((mask & 4) != 0 && y < 8) edge = Mathf.Max(edge, 1 - y / 8f);
                if ((mask & 2) != 0 && Px - 1 - x < 8) edge = Mathf.Max(edge, 1 - (Px - 1 - x) / 8f);
                if ((mask & 8) != 0 && x < 8) edge = Mathf.Max(edge, 1 - x / 8f);
                c = Color.Lerp(c, look.face, edge * 0.8f);
                bool lip = ((mask & 1) != 0 && top < 2) || ((mask & 4) != 0 && y < 2) ||
                    ((mask & 2) != 0 && Px - 1 - x < 2) || ((mask & 8) != 0 && x < 2);
                if (lip) c = Color.black;
                c.a = 1;
                px[y * Px + x] = c;
            }
        return Make(key, px);
    }

    public static Sprite Door(string theme, bool vertical)
    {
        string key = theme + "_door_" + (vertical ? "v" : "h");
        Sprite sprite = Override(key);
        if (sprite != null) return sprite;
        var look = For(theme);
        var floor = Floor(theme, 0, true).texture.GetPixels();
        var px = new Color[Px * Px];
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                Color c = floor[y * Px + x];
                int a = vertical ? x : y, b = vertical ? y : x;
                if (a > 26 && a < 38 && b > 4 && b < 60)
                {
                    c = Color.Lerp(C(0x6b4526), C(0x8a5c34), Noise(b / 6, 0, 3));      // oak door leaf
                    if (b % 12 < 2 || a == 27 || a == 37) c = C(0x2a1a10);
                    if (b > 28 && b < 34 && a > 30 && a < 34) c = C(0xd4b060);         // brass pull
                }
                px[y * Px + x] = c;
            }
        return Make(key, px);
    }

    public static Color ThemeTint(string theme) { return For(theme).face; }
}
