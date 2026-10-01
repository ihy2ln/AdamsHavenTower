using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // What TowerMapView draws: a grid map plus what is known about it. One source per screen
    // (an expedition's forest, the region Atlas), so both share the same layered renderer.
    public interface ITowerMapSource
    {
        TowerOverworld Map { get; }
        string Key { get; }                 // a new key rebuilds the scene
        string Family { get; }              // same family, new key = the forest shifted (crossfade)
        bool Seen(int x, int y);
        float Road(int x, int y);           // drawn road strength, 0..1
        bool Known(TowerOverworldPoi p);    // learned of: glows through the fog
        bool Cleared(TowerOverworldPoi p);
        Texture2D Prop(TowerOverworldPoi p, out float width);
        Vector2Int Party { get; }
        List<string> PartyIds { get; }
        float StampScale { get; }           // canopy scale (the Atlas is drawn smaller)
    }

    // The current expedition's forest.
    public sealed class TowerRunMapSource : ITowerMapSource
    {
        private readonly TowerRules rules;
        public TowerRunMapSource(TowerRules rules) { this.rules = rules; }
        private TowerRun Run { get { return rules.Run; } }

        public TowerOverworld Map { get { return rules.Overworld; } }
        public string Family { get { return "run:" + Run.biome + ":" + Run.gridSeed; } }
        public string Key { get { return Family + ":" + Run.shift; } }
        public bool Seen(int x, int y) { return rules.CellSeen(x, y); }
        public float Road(int x, int y) { return RoadValue(rules.RoadStrength(x, y)); }
        public bool Known(TowerOverworldPoi p) { return rules.NodeVisible(p.id); }
        public bool Cleared(TowerOverworldPoi p) { return Run.cleared.Contains(p.id); }
        public Vector2Int Party { get { return new Vector2Int(Run.cx, Run.cy); } }
        public List<string> PartyIds { get { return LivingParty(Run); } }
        public float StampScale { get { return 1f; } }

        public Texture2D Prop(TowerOverworldPoi p, out float width)
        {
            width = p.kind == "camp" || p.kind == "lair" ? 2.6f : 2.2f;
            return TowerMapArt.Prop(p.kind);
        }

        public static float RoadValue(int strength)
        {
            if (strength <= 0) return 0;
            return strength >= TowerRules.RoadWalked ? 0.55f + 0.45f * (strength - TowerRules.RoadWalked) / (TowerRules.RoadMax - TowerRules.RoadWalked) :
                0.25f * strength / TowerRules.RoadWalked;
        }

        public static List<string> LivingParty(TowerRun run)
        {
            var ids = new List<string>();
            for (int i = 0; i < run.party.Count && ids.Count < 3; i++) if (run.hp[i] > 0) ids.Add(run.party[i]);
            return ids;
        }
    }

    // The Atlas: every region on one painted map. Reached regions are clear, the next ones glow through
    // the fog, roads join what the guild has opened, conquered regions fly a banner.
    public sealed class TowerAtlasSource : ITowerMapSource
    {
        private readonly TowerRules rules;
        private readonly float[] roads;
        private readonly bool[] seen;
        private readonly string key;

        public TowerAtlasSource(TowerRules rules)
        {
            this.rules = rules;
            var map = Map;
            var sb = new System.Text.StringBuilder("atlas:");
            foreach (var r in TowerRules.Regions) sb.Append(rules.RegionConquered(r.id) ? 'C' : rules.RegionUnlocked(r.id) ? 'U' : '-');
            key = sb.ToString();
            seen = new bool[map.cells.Length];
            roads = new float[map.cells.Length];
            Reveal(map.Poi(TowerAtlas.HomeId), 7);
            foreach (var r in TowerRules.Regions)
                if (rules.RegionUnlocked(r.id)) Reveal(map.Poi(r.id), rules.RegionConquered(r.id) ? 6 : 4);
            // Roads from home to the first region and along every opened link.
            foreach (var r in TowerRules.Regions)
            {
                if (!rules.RegionUnlocked(r.id)) continue;
                if (r.requires.Length == 0) Lay(TowerAtlas.HomeId, r.id, rules.RegionConquered(r.id) ? 1f : 0.55f);
                foreach (var need in r.requires)
                    if (rules.RegionUnlocked(need)) Lay(need, r.id, rules.RegionConquered(r.id) && rules.RegionConquered(need) ? 1f : 0.55f);
            }
        }

        private void Reveal(TowerOverworldPoi p, int radius)
        {
            if (p == null) return;
            var map = Map;
            for (int y = p.y - radius; y <= p.y + radius; y++)
                for (int x = p.x - radius; x <= p.x + radius; x++)
                    if (map.Inside(x, y) && (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= radius * radius + radius) seen[map.Index(x, y)] = true;
        }

        private void Lay(string from, string to, float strength)
        {
            var map = Map; var a = map.Poi(from); var b = map.Poi(to);
            if (a == null || b == null) return;
            var path = map.FindPath(new Vector2Int(a.x, a.y), new Vector2Int(b.x, b.y));
            if (path == null) return;
            foreach (var c in path)
            {
                int i = map.Index(c.x, c.y);
                roads[i] = Mathf.Max(roads[i], strength);
                seen[i] = true;
            }
        }

        public TowerOverworld Map { get { return TowerAtlas.Map; } }
        public string Key { get { return key; } }
        public string Family { get { return "atlas"; } }
        public bool Seen(int x, int y) { return seen[Map.Index(x, y)]; }
        public float Road(int x, int y) { return roads[Map.Index(x, y)]; }
        public bool Cleared(TowerOverworldPoi p) { return rules.RegionConquered(p.id); }
        public float StampScale { get { return 0.8f; } }

        // The next regions out (one requirement met) are rumoured: they glow through the fog.
        public bool Known(TowerOverworldPoi p)
        {
            var r = TowerRules.Region(p.id);
            if (r == null) return true;
            if (rules.RegionUnlocked(r.id)) return true;
            foreach (var need in r.requires) if (rules.RegionUnlocked(need)) return true;
            return false;
        }

        public Vector2Int Party
        {
            get
            {
                var run = rules.Run;
                var p = Map.Poi(run != null ? run.region : TowerAtlas.HomeId) ?? Map.Poi(TowerAtlas.HomeId);
                return new Vector2Int(p.x, Mathf.Min(Map.height - 1, p.y + 1));
            }
        }

        public List<string> PartyIds
        {
            get
            {
                var run = rules.Run;
                if (run != null) return TowerRunMapSource.LivingParty(run);
                var ids = new List<string>();
                foreach (var id in TowerRules.Fighters) if (ids.Count < 3 && rules.HeroResident(id) != null) ids.Add(id);
                if (ids.Count == 0) ids.Add(TowerRules.Fighters[0]);
                return ids;
            }
        }

        public Texture2D Prop(TowerOverworldPoi p, out float width)
        {
            width = 2.8f;
            if (p.id == TowerAtlas.HomeId) { width = 3.6f; return TowerMapArt.AtlasProp("atlas_tower_town", "camp"); }
            if (p.id == "silverwood_gate") { width = 3.4f; return TowerMapArt.AtlasProp("atlas_silverwood_gate", "elite"); }
            if (rules.RegionConquered(p.id)) return TowerMapArt.AtlasProp("atlas_banner_conquered", "shrine");
            return TowerMapArt.Prop(rules.RegionUnlocked(p.id) ? "lair" : "mystery");
        }
    }

    // The Atlas map itself: fixed for every save, built from the region list (positions from TowerRules.Regions).
    public static class TowerAtlas
    {
        public const string HomeId = "adams_haven";
        public const int Width = 56, Height = 34;
        public const float HomeX = 0.12f, HomeY = 0.66f;

        private static TowerOverworld map;

        public static TowerOverworld Map
        {
            get
            {
                if (map == null) map = Generate();
                return map;
            }
        }

        private static TowerOverworld Generate()
        {
            // Start from a forest-edge forest; the deep wood darkens toward the Silverwood in the east.
            var m = TowerOverworldGen.GenerateBase("edge", 0xA71A5u, Width, Height);
            var rng = new TowerRng(0x5EEDu);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var t = m.At(x, y);
                    if (t == TowerTerrain.Forest && x > Width * 0.72f && rng.Value() < 0.55f) m.Set(x, y, TowerTerrain.DenseForest);
                    if (t == TowerTerrain.DenseForest && x < Width * 0.3f && rng.Value() < 0.6f) m.Set(x, y, TowerTerrain.Forest);
                }
            // The Silverbrook runs south through the fords and under the old bridge.
            float bx = Width * 0.5f;
            for (int y = 0; y < Height; y++)
            {
                bx += (Mathf.PerlinNoise(y * 0.15f, 3.3f) - 0.5f) * 1.6f;
                for (int w = 0; w < 2; w++) m.Set(Mathf.RoundToInt(bx) + w, y, TowerTerrain.Water);
            }
            // Places: home, then every region where the old region map put it.
            m.pois.Add(new TowerOverworldPoi { id = HomeId, kind = "camp", name = "Adams Haven", theme = "keep",
                x = Mathf.RoundToInt(HomeX * (Width - 1)), y = Mathf.RoundToInt(HomeY * (Height - 1)) });
            foreach (var r in TowerRules.Regions)
            {
                int depth = TowerRules.SilverwoodDepth(r.id);
                m.pois.Add(new TowerOverworldPoi { id = r.id, kind = "region", name = r.name,
                    theme = depth > 0 ? TowerOverworldGen.Biomes[depth - 1].themes[0] : "ruin",
                    x = Mathf.Clamp(Mathf.RoundToInt(r.x * (Width - 1)), 2, Width - 3), y = Mathf.Clamp(Mathf.RoundToInt(r.y * (Height - 1)), 2, Height - 3) });
            }
            foreach (var p in m.pois)
            {
                if (p.id == "sunken_marsh") TowerOverworldGen.Paint(m, p.x, p.y, 4, TowerTerrain.Marsh, false);
                TowerOverworldGen.Paint(m, p.x, p.y, p.id == HomeId ? 3 : 2, TowerTerrain.Clearing, true);
            }
            var home = m.Poi(HomeId);
            foreach (var p in m.pois) TowerOverworldGen.EnsureReachable(m, new Vector2Int(home.x, home.y), new Vector2Int(p.x, p.y));
            m.entranceX = home.x; m.entranceY = home.y;
            return m;
        }

        // Region nearest a cell (within a few cells), for taps on the Atlas.
        public static TowerOverworldPoi RegionNear(int x, int y, int within = 3)
        {
            TowerOverworldPoi best = null; int bd = within * within + 1;
            foreach (var p in Map.pois)
            {
                int d = (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }
    }
}
