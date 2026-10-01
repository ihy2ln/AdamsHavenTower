using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Seeded generator for the layered expedition map. Deterministic: only TowerRng and Mathf.PerlinNoise
    // with seed-derived offsets, never UnityEngine.Random. Bump TowerOverworld.Version when the output changes.
    public static class TowerOverworldGen
    {
        public sealed class Biome
        {
            public string id;
            public float dense = 0.6f;      // noise threshold for deep forest (lower = more of it)
            public float hills, rocks, ruins, blight, marsh;
            public int rivers = 1, riverWidth = 1, lakes;
            public string[] themes = { "ruin" };
        }

        public static readonly Biome[] Biomes =
        {
            new Biome { id = "edge", dense = 0.60f, hills = 0.05f, rocks = 0.05f, rivers = 1, themes = new[] { "ruin", "cave", "briar", "keep" } },
            new Biome { id = "lakes", dense = 0.66f, marsh = 0.18f, rivers = 2, riverWidth = 2, lakes = 1, themes = new[] { "marsh", "cave", "ruin" } },
            new Biome { id = "mountains", dense = 0.68f, hills = 0.30f, rocks = 0.16f, rivers = 1, themes = new[] { "mine", "cave", "crystal", "keep" } },
            new Biome { id = "ruins", dense = 0.62f, ruins = 0.20f, rocks = 0.08f, rivers = 1, themes = new[] { "ruin", "keep", "crystal", "blight" } },
            new Biome { id = "heart", dense = 0.55f, blight = 0.18f, rocks = 0.05f, rivers = 1, themes = new[] { "heartwood", "blight", "crystal" } },
        };

        public static Biome BiomeFor(string id) { foreach (var b in Biomes) if (b.id == id) return b; return Biomes[0]; }

        // Silverwood depth 1..5 -> its biome; Silverbrook regions use the forest edge.
        public static string BiomeForRegion(string regionId)
        {
            int depth = TowerRules.SilverwoodDepth(regionId);
            return depth > 0 ? Biomes[depth - 1].id : "edge";
        }

        // How many places of each kind a map holds (camp and lair always one each).
        private static readonly string[] Kinds = { "combat", "combat", "combat", "combat", "elite", "elite", "mystery", "mystery", "treasure", "shrine", "merchant" };

        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { "combat", new[] { "Raiders", "Wolf Den", "Bandit Camp", "Ambush Hollow", "Boar Thicket", "Goblin Ditch", "Poachers" } },
            { "elite", new[] { "Watch Tower", "Warband", "Old Fort", "Bone Ridge" } },
            { "mystery", new[] { "Lights", "Standing Stones", "Whispering Pool", "Strange Tracks" } },
            { "treasure", new[] { "Root Cache", "Sunken Chest", "Lost Cart" } },
            { "shrine", new[] { "Moon Shrine", "Old Altar", "Silver Spring" } },
            { "merchant", new[] { "Wandering Trader", "Peddler's Wagon" } },
            { "camp", new[] { "Camp" } },
            { "lair", new[] { "The Lair" } },
        };

        public static TowerOverworld Generate(string biomeId, uint seed, int width = TowerOverworld.Width, int height = TowerOverworld.Height)
        {
            var biome = BiomeFor(biomeId);
            var map = new TowerOverworld(width, height, biome.id, seed);
            var rng = new TowerRng(seed * 2654435761u + 7u);
            float ox = rng.Range(0, 1000), oy = rng.Range(0, 1000);

            // 1. Forest everywhere, deep where the noise is high; hills, rocks, ruin ground, blight and marsh in patches.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float n = Fbm(ox + x * 0.09f, oy + y * 0.09f);
                    var t = n > biome.dense ? TowerTerrain.DenseForest : TowerTerrain.Forest;
                    if (Patch(ox + 300, oy, x, y, 0.11f, biome.hills)) t = TowerTerrain.Hill;
                    if (Patch(ox + 600, oy + 50, x, y, 0.08f, biome.ruins)) t = TowerTerrain.RuinGround;
                    if (Patch(ox + 900, oy + 90, x, y, 0.10f, biome.marsh)) t = TowerTerrain.Marsh;
                    if (Patch(ox + 1200, oy + 30, x, y, 0.09f, biome.blight)) t = TowerTerrain.Blight;
                    if (Patch(ox + 1500, oy + 70, x, y, 0.16f, biome.rocks)) t = TowerTerrain.Rock;
                    map.Set(x, y, t);
                }

            // 2. Water: lakes, then rivers meandering across the map.
            for (int l = 0; l < biome.lakes; l++)
            {
                int cx = width / 2 + rng.Next(width / 3) - width / 6, cy = height / 2 + rng.Next(height / 4) - height / 8;
                float rx = rng.Range(6, 10), ry = rng.Range(4, 7);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float d = Mathf.Pow((x - cx) / rx, 2) + Mathf.Pow((y - cy) / ry, 2);
                        if (d + (Mathf.PerlinNoise(ox + x * 0.2f, oy + y * 0.2f) - 0.5f) * 0.6f < 1f) map.Set(x, y, TowerTerrain.Water);
                    }
            }
            for (int r = 0; r < biome.rivers; r++) River(map, rng, biome.riverWidth, ox + r * 50, oy);

            // 3. Places: entrance at the bottom, camp close to it, the rest spread out, lair the farthest from camp.
            map.entranceX = width / 2 + rng.Next(width / 3) - width / 6;
            map.entranceY = height - 2;
            var spots = new List<Vector2Int>();
            var camp = new Vector2Int(Mathf.Clamp(map.entranceX + rng.Next(9) - 4, 4, width - 5), height - 7);
            spots.Add(camp);
            for (int tries = 0; tries < 4000 && spots.Count < Kinds.Length + 2; tries++)
            {
                var p = new Vector2Int(3 + rng.Next(width - 6), 3 + rng.Next(height - 10));
                bool ok = map.Walkable(p.x, p.y);
                foreach (var s in spots) if ((s - p).sqrMagnitude < 9 * 9) { ok = false; break; }
                if (ok) spots.Add(p);
            }
            // Farthest from camp becomes the lair.
            int lairAt = 1;
            for (int i = 2; i < spots.Count; i++) if ((spots[i] - camp).sqrMagnitude > (spots[lairAt] - camp).sqrMagnitude) lairAt = i;
            var order = new List<string> { "camp" };
            var kinds = new List<string>(Kinds);
            for (int i = 1; i < spots.Count; i++)
            {
                if (i == lairAt) { order.Add("lair"); continue; }
                if (kinds.Count == 0) { order.Add("combat"); continue; }
                int k = rng.Next(kinds.Count);
                order.Add(kinds[k]); kinds.RemoveAt(k);
            }
            var used = new HashSet<string>();
            for (int i = 0; i < spots.Count; i++)
            {
                string kind = order[i];
                var poi = new TowerOverworldPoi { id = kind == "camp" ? "camp" : "p" + i, kind = kind, x = spots[i].x, y = spots[i].y,
                    theme = biome.themes[rng.Next(biome.themes.Length)], name = PickName(kind, rng, used) };
                map.pois.Add(poi);
                // Every place sits in its own glade.
                Glade(map, poi.x, poi.y, kind == "lair" || kind == "camp" ? 3 : 2, rng, ox, oy);
            }

            // 4. The way in: a road from the map edge to the camp.
            Carve(map, new Vector2Int(map.entranceX, height - 1), camp, TowerTerrain.Road);
            Glade(map, map.entranceX, height - 1, 1, rng, ox, oy);

            // 5. Every place must be reachable: where water or rock is in the way, open a ford or a pass.
            foreach (var poi in map.pois)
                if (map.FindPath(camp, new Vector2Int(poi.x, poi.y)) == null) Connect(map, camp, new Vector2Int(poi.x, poi.y));
            return map;
        }

        private static string PickName(string kind, TowerRng rng, HashSet<string> used)
        {
            var list = Names[kind];
            for (int i = 0; i < list.Length; i++)
            {
                string name = list[(rng.Next(list.Length) + i) % list.Length];
                if (used.Add(name)) return name;
            }
            return list[0];
        }

        private static float Fbm(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 0.6f + Mathf.PerlinNoise(x * 2.1f + 17, y * 2.1f + 5) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 3, y * 4.3f + 29) * 0.1f;
        }

        // True on roughly `amount` of the map, in soft blobs.
        private static bool Patch(float ox, float oy, int x, int y, float scale, float amount)
        {
            if (amount <= 0) return false;
            return Mathf.PerlinNoise(ox + x * scale, oy + y * scale) > 1f - amount * 1.6f;
        }

        private static void River(TowerOverworld map, TowerRng rng, int width, float ox, float oy)
        {
            bool vertical = rng.Value() < 0.5f;
            int len = vertical ? map.height : map.width, span = vertical ? map.width : map.height;
            float pos = span * rng.Range(0.3f, 0.7f);
            for (int i = 0; i < len; i++)
            {
                pos += (Mathf.PerlinNoise(ox + i * 0.12f, oy + 400) - 0.5f) * 2.2f;
                pos = Mathf.Clamp(pos, 4, span - 5);
                for (int w = 0; w < width; w++)
                {
                    int p = Mathf.RoundToInt(pos) + w;
                    if (vertical) map.Set(p, i, TowerTerrain.Water); else map.Set(i, p, TowerTerrain.Water);
                }
            }
        }

        private static void Glade(TowerOverworld map, int cx, int cy, int radius, TowerRng rng, float ox, float oy)
        {
            for (int y = cy - radius - 1; y <= cy + radius + 1; y++)
                for (int x = cx - radius - 1; x <= cx + radius + 1; x++)
                {
                    if (!map.Inside(x, y)) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + (Mathf.PerlinNoise(ox + x * 0.5f, oy + y * 0.5f) - 0.5f) * 1.2f;
                    if (d > radius + 0.5f) continue;
                    var t = map.At(x, y);
                    if (t == TowerTerrain.Road) continue;
                    map.Set(x, y, t == TowerTerrain.Water ? TowerTerrain.Ford : TowerTerrain.Clearing);
                }
        }

        // Lay terrain along the cheapest route, turning water into fords and rock into hill passes.
        private static void Carve(TowerOverworld map, Vector2Int a, Vector2Int b, TowerTerrain lay)
        {
            var path = map.FindPath(a, b, null, Crossing(map));
            if (path == null) return;
            for (int i = 0; i < path.Count; i++)
            {
                Open(map, path[i], lay);
                // A diagonal step may not squeeze between two blocked corners: open one of them too.
                if (i > 0 && path[i].x != path[i - 1].x && path[i].y != path[i - 1].y)
                {
                    var side = new Vector2Int(path[i].x, path[i - 1].y);
                    if (!map.Walkable(side.x, side.y) || lay == TowerTerrain.Road) Open(map, side, lay);
                }
            }
        }

        private static void Open(TowerOverworld map, Vector2Int c, TowerTerrain lay)
        {
            var t = map.At(c.x, c.y);
            if (t == TowerTerrain.Water) map.Set(c.x, c.y, lay == TowerTerrain.Road ? TowerTerrain.Bridge : TowerTerrain.Ford);
            else if (lay == TowerTerrain.Road) map.Set(c.x, c.y, TowerTerrain.Road);
            else if (t == TowerTerrain.Rock) map.Set(c.x, c.y, TowerTerrain.Hill);
        }

        private static void Connect(TowerOverworld map, Vector2Int a, Vector2Int b) { Carve(map, a, b, TowerTerrain.Ford); }

        // Cost where blocked ground is crossable but expensive, so a crossing is opened only where needed.
        private static System.Func<int, float> Crossing(TowerOverworld map)
        {
            return i =>
            {
                float c = TowerOverworld.TerrainCost(map.cells[i]);
                return float.IsInfinity(c) ? 15f : c;
            };
        }
    }
}
