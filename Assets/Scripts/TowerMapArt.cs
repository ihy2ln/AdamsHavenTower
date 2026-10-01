using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Art for the layered expedition map. Real art (MAP_LAYER_ART_PROMPTS.md, sliced into
    // Resources/AdamsHaven/Expedition/Overworld/) wins; anything missing is painted procedurally here so the
    // map always works and placeholders can be swapped file by file.
    public static class TowerMapArt
    {
        public const string Root = "AdamsHaven/Expedition/Overworld/";

        // Ground layers in the splat shader's order.
        public static readonly string[] Grounds = { "ground_meadow", "ground_forest_floor", "ground_deep_moss", "ground_dirt_road",
            "ground_scree", "ground_ruin_flagstone", "ground_blight", "ground_mud_marsh", "water_deep", "water_shallow" };

        public static int GroundOf(TowerTerrain t)
        {
            switch (t)
            {
                case TowerTerrain.Clearing: return 0;
                case TowerTerrain.Forest: return 1;
                case TowerTerrain.DenseForest: return 2;
                case TowerTerrain.Road: case TowerTerrain.Bridge: return 3;
                case TowerTerrain.Rock: case TowerTerrain.Hill: return 4;
                case TowerTerrain.RuinGround: return 5;
                case TowerTerrain.Blight: return 6;
                case TowerTerrain.Marsh: return 7;
                case TowerTerrain.Water: return 8;
                default: return 9;      // Ford
            }
        }

        private static readonly Dictionary<string, Texture2D> grounds = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, List<Texture2D>> stamps = new Dictionary<string, List<Texture2D>>();

        // Forget loaded and painted art (after new art is imported; statics outlive Play mode in this project).
        public static void ClearCache() { grounds.Clear(); stamps.Clear(); }

        public static Texture2D Ground(int index)
        {
            string name = Grounds[index];
            Texture2D tex;
            if (grounds.TryGetValue(name, out tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>(Root + "ground/" + name) ?? PaintGround(index);
            tex.wrapMode = TextureWrapMode.Repeat;
            grounds[name] = tex;
            return tex;
        }

        // Stamp families: canopy_oak, canopy_pine, canopy_birch, canopy_silverwood, canopy_willow, canopy_dead,
        // bush, rock, reed. Real art: every texture in Overworld/stamps/<family>/.
        public static List<Texture2D> Stamps(string family)
        {
            List<Texture2D> list;
            if (stamps.TryGetValue(family, out list) && list.Count > 0 && list[0] != null) return list;
            list = new List<Texture2D>(Resources.LoadAll<Texture2D>(Root + "stamps/" + family));
            if (list.Count == 0) for (int v = 0; v < 4; v++) list.Add(PaintStamp(family, v));
            stamps[family] = list;
            return list;
        }

        public static Texture2D Prop(string kind)
        {
            var real = Resources.Load<Texture2D>(Root + "props/poi_" + kind);
            if (real != null) return real;
            // Stand-ins from the landmark cut-outs until the POI props exist.
            string stand = kind == "camp" ? "lantern_camp" : kind == "lair" ? "battle_roots" : kind == "shrine" ? "moon_pool" :
                kind == "mystery" ? "ranger_marks" : kind == "treasure" || kind == "merchant" ? "ruined_cart" :
                kind == "elite" ? "hollow_oak" : "forest_ruin";
            return Resources.Load<Texture2D>("AdamsHaven/Expedition/Props/" + stand);
        }

        // Atlas stamps (atlas_tower_town, atlas_silverwood_gate, atlas_banner_conquered), else a place prop stand-in.
        public static Texture2D AtlasProp(string name, string fallbackKind)
        {
            return Resources.Load<Texture2D>(Root + "atlas/" + name) ?? Prop(fallbackKind);
        }

        // ---------------------------------------------------------------- procedural placeholders

        private static readonly Color[][] GroundColors =
        {
            new[] { new Color(0.36f, 0.52f, 0.28f), new Color(0.46f, 0.62f, 0.32f), new Color(0.80f, 0.82f, 0.55f) },  // meadow
            new[] { new Color(0.20f, 0.30f, 0.19f), new Color(0.30f, 0.36f, 0.20f), new Color(0.45f, 0.36f, 0.20f) },  // forest floor
            new[] { new Color(0.10f, 0.19f, 0.15f), new Color(0.15f, 0.26f, 0.19f), new Color(0.22f, 0.30f, 0.22f) },  // deep moss
            new[] { new Color(0.50f, 0.40f, 0.27f), new Color(0.60f, 0.49f, 0.33f), new Color(0.40f, 0.33f, 0.24f) },  // dirt
            new[] { new Color(0.38f, 0.40f, 0.42f), new Color(0.48f, 0.50f, 0.52f), new Color(0.30f, 0.33f, 0.30f) },  // scree
            new[] { new Color(0.50f, 0.52f, 0.52f), new Color(0.40f, 0.44f, 0.40f), new Color(0.30f, 0.40f, 0.28f) },  // ruin
            new[] { new Color(0.30f, 0.27f, 0.31f), new Color(0.38f, 0.34f, 0.36f), new Color(0.45f, 0.25f, 0.55f) },  // blight
            new[] { new Color(0.20f, 0.24f, 0.18f), new Color(0.26f, 0.32f, 0.24f), new Color(0.25f, 0.40f, 0.42f) },  // marsh
            new[] { new Color(0.08f, 0.22f, 0.32f), new Color(0.11f, 0.29f, 0.40f), new Color(0.30f, 0.50f, 0.58f) },  // deep water
            new[] { new Color(0.22f, 0.40f, 0.44f), new Color(0.32f, 0.50f, 0.50f), new Color(0.55f, 0.58f, 0.50f) },  // shallow water
        };

        private static Texture2D PaintGround(int index)
        {
            const int size = 256;
            var c = GroundColors[index];
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Periodic(u, v, 4, index) * 0.5f + Periodic(u, v, 8, index + 9) * 0.3f + Periodic(u, v, 32, index + 17) * 0.2f;
                    float fleck = Periodic(u, v, 64, index + 31);
                    var col = Color.Lerp(c[0], c[1], Mathf.SmoothStep(0.3f, 0.7f, n));
                    if (fleck > 0.78f) col = Color.Lerp(col, c[2], (fleck - 0.78f) * 3f);
                    px[y * size + x] = col;
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // Value noise that wraps every `period` lattice cells, so the texture tiles.
        private static float Periodic(float u, float v, int period, int salt)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Lattice(x0, y0, period, salt), b = Lattice(x0 + 1, y0, period, salt);
            float c = Lattice(x0, y0 + 1, period, salt), d = Lattice(x0 + 1, y0 + 1, period, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int x, int y, int period, int salt)
        {
            x = ((x % period) + period) % period; y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2246822519);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xffff) / 65535f;
        }

        private static Texture2D PaintStamp(string family, int variant)
        {
            const int w = 160, h = 192;
            var px = new Color[w * h];
            var rng = new TowerRng(TowerForestLayouts.Hash(family, variant * 97 + 1));
            Color leaf, dark, light;
            bool tree = family.StartsWith("canopy");
            switch (family)
            {
                case "canopy_pine": leaf = new Color(0.12f, 0.30f, 0.26f); break;
                case "canopy_birch": leaf = new Color(0.45f, 0.58f, 0.30f); break;
                case "canopy_silverwood": leaf = new Color(0.16f, 0.36f, 0.28f); break;
                case "canopy_willow": leaf = new Color(0.25f, 0.42f, 0.34f); break;
                case "canopy_dead": leaf = new Color(0.36f, 0.33f, 0.36f); break;
                case "rock": leaf = new Color(0.45f, 0.47f, 0.50f); break;
                case "reed": leaf = new Color(0.45f, 0.50f, 0.30f); break;
                default: leaf = new Color(0.22f, 0.40f, 0.22f); break;     // oak, bush
            }
            dark = leaf * 0.55f; dark.a = 1; light = Color.Lerp(leaf, new Color(0.95f, 0.92f, 0.7f), 0.35f);
            // Blobs: (cx, cy, r) in pixels, y up from the bottom; the trunk base is at (w/2, 10).
            var blobs = new List<Vector3>();
            float size = family == "canopy_silverwood" ? 1.25f : family == "canopy_birch" ? 0.8f : family == "bush" ? 0.45f :
                family == "rock" ? 0.5f : family == "reed" ? 0.35f : 1f;
            float baseY = tree ? 70 * size + 18 : 10 + 30 * size;
            int count = family == "rock" ? 2 : family == "reed" ? 0 : 7;
            for (int i = 0; i < count; i++)
            {
                float ang = rng.Range(0, Mathf.PI * 2), d = rng.Range(0, 26) * size;
                float r = (family == "canopy_pine" ? rng.Range(16, 24) : rng.Range(20, 32)) * size;
                blobs.Add(new Vector3(w / 2 + Mathf.Cos(ang) * d, baseY + Mathf.Sin(ang) * d * 0.8f, r));
            }
            if (family == "canopy_pine") for (int i = 0; i < 4; i++) blobs.Add(new Vector3(w / 2, baseY - 24 + i * 16, 30 - i * 6));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color col = Color.clear;
                    // Trunk.
                    if (tree && family != "canopy_dead" && Mathf.Abs(x - w / 2) < 4 * size + 1 && y >= 8 && y < baseY - 10)
                        col = family == "canopy_silverwood" || family == "canopy_birch" ? new Color(0.82f, 0.84f, 0.80f) : new Color(0.32f, 0.24f, 0.17f);
                    if (family == "canopy_dead")
                    {
                        float t = (y - 8) / (float)(h - 40);
                        float branch = Mathf.Abs(x - w / 2 - Mathf.Sin(y * 0.08f + variant) * 18 * t);
                        if (y >= 8 && y < h - 30 && (branch < 4 - 2 * t || (Mathf.Abs(Mathf.Sin(x * 0.11f + y * 0.05f)) < 0.06f && t > 0.4f)))
                            col = new Color(0.36f, 0.32f, 0.36f);
                    }
                    if (family == "reed")
                        for (int s = 0; s < 7; s++)
                        {
                            float sx = w / 2 - 24 + s * 8 + Mathf.Sin(y * 0.05f + s) * 3;
                            if (Mathf.Abs(x - sx) < 1.6f && y > 8 && y < 60 + (s * 13 + variant * 7) % 30) col = Color.Lerp(leaf, dark, (s % 3) / 3f);
                        }
                    float best = 0; Vector3 hit = Vector3.zero;
                    foreach (var b in blobs)
                    {
                        float dd = 1 - ((x - b.x) * (x - b.x) + (y - b.y) * (y - b.y) * 1.15f) / (b.z * b.z);
                        if (dd > best) { best = dd; hit = b; }
                    }
                    if (best > 0)
                    {
                        // Light from the upper left, a darker rim, and leafy noise.
                        float shade = Mathf.Clamp01(0.5f + ((x - hit.x) * -0.6f + (y - hit.y) * 0.8f) / (hit.z * 1.6f));
                        float leafy = Mathf.PerlinNoise(x * 0.25f + variant * 13, y * 0.25f);
                        var c = Color.Lerp(dark, Color.Lerp(leaf, light, shade * 0.8f), Mathf.Clamp01(shade * 0.7f + leafy * 0.5f));
                        if (best < 0.12f) c = Color.Lerp(dark * 0.8f, c, best / 0.12f);
                        c.a = Mathf.Clamp01(best * 8f);
                        col = col.a > 0 && c.a < 1 ? Color.Lerp(col, c, c.a) : c;
                        col.a = Mathf.Max(col.a, c.a);
                    }
                    px[y * w + x] = col;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px); tex.Apply();
            tex.name = family + "_" + variant;
            return tex;
        }
    }
}
