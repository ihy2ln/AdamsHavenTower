using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One of the fifteen predetermined forest maps. Nodes are POIs, objectives, landmarks and the camp.
    [Serializable] public sealed class TowerForestNode
    {
        public string id, kind, name, theme, prop = "";
        public float x, y;                      // normalized on the backdrop, y from the top
        public List<string> links = new List<string>();
    }

    [Serializable] public sealed class TowerForestLayout
    {
        public string id, name, backdrop, theme, entrance;
        public bool mirror;                     // stand-in plates are shown flipped until their own painting exists
        public List<TowerForestNode> nodes = new List<TowerForestNode>();

        public TowerForestNode Node(string nodeId) { return nodes.Find(n => n.id == nodeId); }
        public bool Linked(string a, string b)
        {
            var na = Node(a); var nb = Node(b);
            return na != null && nb != null && a != b && (na.links.Contains(b) || nb.links.Contains(a));
        }
    }

    [Serializable] internal sealed class TowerForestLayoutFile { public List<TowerForestLayout> layouts = new List<TowerForestLayout>(); }

    public static class TowerForestLayouts
    {
        // Twelve painted maps from the old expedition set plus three new plates.
        public static readonly string[] Ids = {
            "briar_tangle", "caravan_break", "crystal_blight", "fallen_giant", "fern_marsh", "fog_hollow",
            "lantern_ruins", "moonwell_basin", "pine_crossing", "river_braid", "watchpost_ridge", "wolf_range",
            "silverfall_glen", "thornwood_gate", "ashen_barrow" };
        private static readonly string[] Themes = {
            "briar", "keep", "crystal", "cave", "marsh", "ruin", "ruin", "crystal", "briar", "marsh", "keep", "cave",
            "marsh", "briar", "ruin" };

        public static readonly string[] Props = {
            "battle_roots", "briar_bridge", "fallen_giant", "forest_ruin", "hollow_oak", "lantern_camp", "moon_pool",
            "ranger_marks", "ruined_cart" };
        private static readonly string[] PropNames = {
            "Battle Roots", "Briar Bridge", "Fallen Giant", "Forest Ruin", "Hollow Oak", "Lantern Camp", "Moon Pool",
            "Ranger Marks", "Ruined Cart" };
        private static readonly string[] PropThemes = { "cave", "briar", "cave", "ruin", "cave", "keep", "crystal", "briar", "keep" };

        // Dungeon floors per node kind.
        public static int Floors(string kind)
        {
            switch (kind)
            {
                case "lair": return 3;
                case "elite": case "landmark": return 2;
                case "camp": return 0;
                default: return 1;
            }
        }

        private static readonly Dictionary<string, TowerForestLayout> cache = new Dictionary<string, TowerForestLayout>();

        public static TowerForestLayout Get(string id)
        {
            if (string.IsNullOrEmpty(id) || Array.IndexOf(Ids, id) < 0) return null;
            if (cache.Count == 0) Load();
            TowerForestLayout layout;
            if (!cache.TryGetValue(id, out layout)) { layout = Generate(id); cache[id] = layout; }
            return layout;
        }

        public static void ClearCache() { cache.Clear(); }

        // Hand-placed layouts override the generator, one entry per map.
        private static void Load()
        {
            var text = Resources.Load<TextAsset>("AdamsHaven/Expedition/forest_layouts");
            if (text == null) return;
            var file = JsonUtility.FromJson<TowerForestLayoutFile>(text.text);
            if (file == null || file.layouts == null) return;
            foreach (var layout in file.layouts)
                if (layout != null && Array.IndexOf(Ids, layout.id) >= 0 && layout.nodes.Count > 0) cache[layout.id] = layout;
        }

        public static string Pretty(string id)
        {
            var words = id.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        public static uint Hash(string text, int salt = 0)
        {
            uint h = 2166136261u ^ (uint)salt;
            foreach (char c in text) { h ^= c; h *= 16777619u; }
            return h == 0 ? 1u : h;
        }

        // Deterministic generator: the seed is the map id, so a map is identical every time it is drawn.
        private static TowerForestLayout Generate(string id)
        {
            var rng = new TowerRng(Hash(id));
            string theme = Themes[Array.IndexOf(Ids, id)];
            var layout = new TowerForestLayout { id = id, name = Pretty(id), backdrop = "AdamsHaven/Expedition/Maps/" + id,
                theme = theme, entrance = "camp" };
            string standIn = id == "silverfall_glen" ? "river_braid" : id == "thornwood_gate" ? "briar_tangle" :
                id == "ashen_barrow" ? "lantern_ruins" : null;
            if (standIn != null && Resources.Load<Texture2D>(layout.backdrop) == null)
            { layout.backdrop = "AdamsHaven/Expedition/Maps/" + standIn; layout.mirror = true; }
            int[] perColumn = { 1, 3, 3, 3, 2, 1 };
            var columns = new List<List<TowerForestNode>>();
            var middle = new List<TowerForestNode>();
            for (int c = 0; c < perColumn.Length; c++)
            {
                var column = new List<TowerForestNode>();
                for (int i = 0; i < perColumn[c]; i++)
                {
                    float y = perColumn[c] == 1 ? 0.52f : Mathf.Lerp(0.2f, 0.82f, i / (float)(perColumn[c] - 1));
                    var node = new TowerForestNode { id = "n" + c + i, theme = theme,
                        x = 0.08f + c * 0.168f + rng.Range(-0.03f, 0.03f), y = y + rng.Range(-0.06f, 0.06f) };
                    column.Add(node);
                    if (c > 0 && c < perColumn.Length - 1) middle.Add(node);
                }
                columns.Add(column);
            }
            var camp = columns[0][0];
            camp.id = "camp"; camp.kind = "camp"; camp.name = "Expedition Camp";
            var lair = columns[perColumn.Length - 1][0];
            lair.kind = "lair"; lair.name = LairNames[rng.Next(LairNames.Length)];

            // Eleven middle slots: three landmarks and eight points of interest.
            var kinds = new List<string> { "combat", "combat", "combat", "elite", "treasure", "shrine", "mystery", "merchant",
                "landmark", "landmark", "landmark" };
            for (int i = kinds.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = kinds[i]; kinds[i] = kinds[j]; kinds[j] = t; }
            // Keep the elite away from the first column.
            int elite = kinds.IndexOf("elite");
            if (elite < 3) { int swap = 6 + rng.Next(5); var t = kinds[swap]; kinds[swap] = "elite"; kinds[elite] = t; }
            var props = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            for (int i = props.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = props[i]; props[i] = props[j]; props[j] = t; }
            int landmark = 0;
            for (int i = 0; i < middle.Count; i++)
            {
                var node = middle[i];
                node.kind = kinds[i];
                if (node.kind == "landmark")
                {
                    int p = props[landmark++];
                    node.prop = Props[p]; node.name = PropNames[p]; node.theme = PropThemes[p];
                }
                else node.name = NameFor(node.kind, rng);
            }

            // Trails run left to right; every node can be reached.
            for (int c = 0; c < columns.Count - 1; c++)
            {
                var from = columns[c]; var to = columns[c + 1];
                for (int i = 0; i < from.Count; i++)
                {
                    int j = Mathf.RoundToInt(i * (to.Count - 1) / (float)Mathf.Max(1, from.Count - 1));
                    Link(from[i], to[j]);
                    if (to.Count > 1 && rng.Next(2) == 0) Link(from[i], to[Mathf.Clamp(j + (rng.Next(2) == 0 ? -1 : 1), 0, to.Count - 1)]);
                }
                for (int j = 0; j < to.Count; j++)
                {
                    bool reached = false;
                    foreach (var n in from) if (n.links.Contains(to[j].id)) reached = true;
                    if (!reached) Link(from[Mathf.RoundToInt(j * (from.Count - 1) / (float)Mathf.Max(1, to.Count - 1))], to[j]);
                }
            }
            foreach (var column in columns) layout.nodes.AddRange(column);
            return layout;
        }

        private static void Link(TowerForestNode a, TowerForestNode b) { if (!a.links.Contains(b.id)) a.links.Add(b.id); }

        private static readonly string[] LairNames = { "Thornmother's Den", "The Rootbound Throne", "Wolf King's Barrow",
            "The Hollow Crown", "Moonscar Lair", "The Briar Heart" };

        private static string NameFor(string kind, TowerRng rng)
        {
            string[] pool;
            switch (kind)
            {
                case "combat": pool = new[] { "Bandit Trail", "Wolf Den", "Goblin Warren", "Spider Hollow", "Boar Thicket", "Raider Outpost" }; break;
                case "elite": pool = new[] { "Warden's Circle", "Thornseal Vault", "Old Guard Post", "Iron Maw Grotto" }; break;
                case "treasure": pool = new[] { "Sunken Cache", "Smuggler's Hoard", "Workshop Cache", "Fallen Armory" }; break;
                case "shrine": pool = new[] { "Moss Shrine", "Moonstone Altar", "Weeping Idol" }; break;
                case "mystery": pool = new[] { "Whispering Stones", "Strange Lights", "Hidden Root Door" }; break;
                case "merchant": pool = new[] { "Wandering Peddler", "Caravan Stop", "Tinker's Wagon" }; break;
                default: pool = new[] { "Clearing" }; break;
            }
            return pool[rng.Next(pool.Length)];
        }
    }

    // Small deterministic RNG so generated maps and dungeons do not touch the Tower's own random stream.
    public sealed class TowerRng
    {
        private uint state;
        public TowerRng(uint seed) { state = seed == 0 ? 1u : seed; }
        public uint NextUInt() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public int Next(int max) { return max <= 0 ? 0 : (int)(NextUInt() % (uint)max); }
        public float Value() { return (NextUInt() & 0xffffff) / 16777216f; }
        public float Range(float a, float b) { return a + (b - a) * Value(); }
    }
}
