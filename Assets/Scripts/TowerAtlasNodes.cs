using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A map mod (Path of Exile 2 waystone style): each one makes a place harder and pays for it.
    public sealed class TowerMapMod
    {
        public string id, name, danger, reward;
        public float offense = 1f, vitality = 1f, loot = 1f, gold = 1f;
        public int extraFoes, treasureRooms, mysteryRooms;
        public string affix = "";
        public bool relic;
        public string Line { get { return name + ": " + danger + "; " + reward; } }
    }

    // What all of a place's mods add up to.
    public sealed class TowerModTotals
    {
        public float offense = 1f, vitality = 1f, loot = 1f, gold = 1f;
        public int extraFoes, treasureRooms, mysteryRooms;
        public string affix = "";
        public bool relic;
    }

    // The run map's places as Atlas nodes (BATTLE_MODE_GDD.md section 4): completed, open (linked to a completed place),
    // scouted (seen from afar, not yet reachable), the lair's beacon, or hidden. Each dungeon place has a tier
    // (region danger + hops from the camp, the lair highest) and 0-3 seeded mods; towers reveal a wide circle and add a
    // tablet's mod to every place in it.
    public sealed partial class TowerRules
    {
        public const int NodeHidden = 0, NodeBeacon = 1, NodeScouted = 2, NodeOpen = 3, NodeDone = 4;
        public const int MaxTier = 16, TowerRadius = 13, NodeReveal = 2, TabletChoices = 3;

        public static readonly TowerMapMod[] MapMods =
        {
            new TowerMapMod { id = "brutal", name = "Brutal", danger = "monsters deal 25% more damage", reward = "+30% loot", offense = 1.25f, loot = 1.3f },
            new TowerMapMod { id = "resilient", name = "Resilient", danger = "monsters have 30% more health", reward = "+25% loot", vitality = 1.3f, loot = 1.25f },
            new TowerMapMod { id = "teeming", name = "Teeming", danger = "one more enemy in every fight", reward = "+25% loot", extraFoes = 1, loot = 1.25f },
            new TowerMapMod { id = "fortified", name = "Fortified", danger = "one foe in every fight is Shielded", reward = "a relic always on offer", affix = "Shielded", relic = true },
            new TowerMapMod { id = "hasted", name = "Hasted", danger = "one foe in every fight is Hasted", reward = "+20% gold", affix = "Hasted", gold = 1.2f },
            new TowerMapMod { id = "rich", name = "Rich", danger = "monsters have 10% more health", reward = "an extra treasure room", vitality = 1.1f, treasureRooms = 1 },
            new TowerMapMod { id = "haunted", name = "Haunted", danger = "two rooms turn strange", reward = "+15% loot", mysteryRooms = 2, loot = 1.15f },
        };

        public static TowerMapMod MapMod(string id)
        {
            foreach (var m in MapMods) if (m.id == id) return m;
            return null;
        }

        // Places with a dungeon to run carry tiers and mods.
        public static bool ModdedKind(string kind)
        { return kind == "combat" || kind == "elite" || kind == "mystery" || kind == "treasure" || kind == "lair"; }

        // Merchants, shrines and towers have nothing to clear: reaching them completes them.
        public static bool VisitKind(string kind) { return kind == "merchant" || kind == "shrine" || kind == "tower"; }

        public static string Roman(int n)
        {
            if (n <= 0) return "";
            string[] tens = { "", "X", "XX" }, ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            return tens[Mathf.Min(2, n / 10)] + ones[n % 10];
        }

        // Waystone colour bands: I-V white (0), VI-X yellow (1), XI and up red (2).
        public static int TierBand(int tier) { return tier <= 5 ? 0 : tier <= 10 ? 1 : 2; }

        // ---------------------------------------------------------------- node states

        public bool NodeComplete(string id)
        {
            var run = Run; var map = Overworld;
            if (run == null || map == null || string.IsNullOrEmpty(id)) return false;
            var p = map.Poi(id);
            return p != null && (p.kind == "camp" || run.cleared.Contains(id) || (run.nodesDone != null && run.nodesDone.Contains(id)));
        }

        public int NodeState(string id)
        {
            var run = Run; var map = Overworld; var web = Web;
            if (map == null) return NodeHidden;
            var p = map.Poi(id);
            if (p == null) return NodeHidden;
            if (NodeComplete(id)) return NodeDone;
            foreach (var e in web.EdgesOf(id)) if (NodeComplete(e.Other(id))) return NodeOpen;
            if (run.revealed.Contains(id)) return NodeScouted;
            return p.kind == "lair" ? NodeBeacon : NodeHidden;
        }

        // Tier: the region's danger plus one per hop from the camp; the lair one above the highest.
        public int NodeTier(string id)
        {
            var map = Overworld; var web = Web;
            if (map == null) return 0;
            var p = map.Poi(id);
            if (p == null || p.kind == "camp") return 0;
            int depth = RunRegion == null ? 1 : RunRegion.depth;
            if (p.kind != "lair") return TierAt(depth, PlaceHops(id));
            int top = depth;
            foreach (var q in map.pois) if (q.kind != "lair" && q.kind != "camp") top = Mathf.Max(top, TierAt(depth, PlaceHops(q.id)));
            return Mathf.Min(MaxTier, top + 1);
        }

        // Hops from the camp as they were when the place appeared (recorded at the start, after each shift, on migration).
        private int PlaceHops(string id)
        {
            var run = Run;
            if (run.nodeHops != null)
            {
                string prefix = id + "=";
                foreach (var entry in run.nodeHops)
                    if (entry.StartsWith(prefix, System.StringComparison.Ordinal)) { int h; if (int.TryParse(entry.Substring(prefix.Length), out h)) return h; }
            }
            return Mathf.Max(1, Web.CampHops(id));
        }

        private void RecordHops()
        {
            var run = Run; var map = Overworld; var web = Web;
            if (map == null) return;
            if (run.nodeHops == null) run.nodeHops = new List<string>();
            run.nodeHops.RemoveAll(e => { int cut = e.IndexOf('='); return cut <= 0 || map.Poi(e.Substring(0, cut)) == null; });
            var have = new HashSet<string>();
            foreach (var e in run.nodeHops) have.Add(e.Substring(0, e.IndexOf('=')));
            foreach (var p in map.pois) if (!have.Contains(p.id)) run.nodeHops.Add(p.id + "=" + Mathf.Max(1, web.CampHops(p.id)));
        }

        private static int TierAt(int depth, int hops) { return Mathf.Clamp(depth + Mathf.Max(1, hops) - 1, 1, MaxTier); }

        // Tiers above the region's own danger: +10% loot each, and +1 battle danger per two (at most +2).
        private int TierStep(string id)
        {
            if (!GridRun || string.IsNullOrEmpty(id)) return 0;
            int depth = RunRegion == null ? 1 : RunRegion.depth;
            return Mathf.Max(0, NodeTier(id) - depth);
        }

        public int NodeDepthBonus(string id) { return Mathf.Min(2, TierStep(id) / 2); }

        // ---------------------------------------------------------------- mods

        // The place's own seeded mods (more the farther from camp: up to one at a hop, two at two hops, three beyond;
        // the lair at most one), then one per tablet of a tower whose circle covers it.
        public List<string> NodeModIds(string id)
        {
            var list = new List<string>();
            var run = Run; var map = Overworld; var web = Web;
            if (map == null || string.IsNullOrEmpty(id)) return list;
            var p = map.Poi(id);
            if (p == null || !ModdedKind(p.kind)) return list;
            int hops = PlaceHops(id);
            int most = p.kind == "lair" ? 1 : Mathf.Min(3, hops);
            var rng = new TowerRng(TowerForestLayouts.Hash("mods:" + p.id + ":" + p.x + "," + p.y, run.seed));
            int count = rng.Next(most + 1);
            var pool = new List<TowerMapMod>(MapMods);
            for (int k = 0; k < count && pool.Count > 0; k++)
            {
                int pick = rng.Next(pool.Count);
                list.Add(pool[pick].id);
                pool.RemoveAt(pick);
            }
            if (run.tablets != null)
                foreach (var t in run.tablets)
                {
                    int cut = t.IndexOf(':');
                    if (cut <= 0) continue;
                    var tower = map.Poi(t.Substring(0, cut));
                    if (tower != null && InTowerRange(tower, p) && MapMod(t.Substring(cut + 1)) != null) list.Add(t.Substring(cut + 1));
                }
            return list;
        }

        public List<TowerMapMod> NodeMods(string id)
        {
            var mods = new List<TowerMapMod>();
            foreach (var m in NodeModIds(id)) mods.Add(MapMod(m));
            return mods;
        }

        public static TowerModTotals Totals(List<string> ids)
        {
            var t = new TowerModTotals();
            if (ids == null) return t;
            foreach (var id in ids)
            {
                var m = MapMod(id);
                if (m == null) continue;
                t.offense *= m.offense; t.vitality *= m.vitality; t.loot *= m.loot; t.gold *= m.gold;
                t.extraFoes += m.extraFoes; t.treasureRooms += m.treasureRooms; t.mysteryRooms += m.mysteryRooms;
                t.relic |= m.relic;
                // One forced affix per fight; Shielded (Fortified) wins over Hasted.
                if (m.affix.Length > 0 && (t.affix.Length == 0 || m.affix == "Shielded")) t.affix = m.affix;
            }
            t.extraFoes = Mathf.Min(2, t.extraFoes);
            return t;
        }

        public TowerModTotals NodeTotals(string id) { return GridRun ? Totals(NodeModIds(id)) : new TowerModTotals(); }

        // Loot multiplier of a place: its mods times +10% per tier above the region's danger.
        public float NodeLootScale(string id) { return GridRun ? NodeTotals(id).loot * (1f + 0.1f * TierStep(id)) : 1f; }

        // The fight in a dungeon room carries its place's mods (BattleEncounterSpec: Offense, Vitality, ExtraFoes, Affix).
        public static void ApplyMods(BattleEncounterSpec spec, TowerModTotals m)
        {
            spec.Offense = m.offense; spec.Vitality = m.vitality; spec.ExtraFoes = m.extraFoes; spec.Affix = m.affix;
        }

        // Rich and Haunted change the room mix: quiet rooms turn into treasure or mystery rooms. The entrance, the goal,
        // the stairs, elites and bosses never change, and at least one fight stays.
        public static void ApplyRoomMods(TowerDungeon d, TowerModTotals m)
        {
            int treasure = m.treasureRooms, mystery = m.mysteryRooms;
            if (d.rooms.Count <= 2) return;
            for (int pass = 0; pass < 2 && (treasure > 0 || mystery > 0); pass++)
                for (int i = d.rooms.Count - 1; i >= 1 && (treasure > 0 || mystery > 0); i--)
                {
                    var r = d.rooms[i];
                    if (r.goal || r.kind == "stairs" || r.kind == "elite" || r.kind == "boss" || r.kind == "entrance") continue;
                    bool quiet = r.kind == "empty" || r.kind == "rest" || r.kind == "story" || r.kind == "trap" || r.kind == "skillcheck";
                    // Second pass: a spare pack room may turn too, while another fight remains.
                    bool spare = pass == 1 && r.kind == "enemy" && d.rooms.FindAll(x => BattleRoom(x.kind)).Count > 1;
                    if (treasure > 0 && (quiet || spare || r.kind == "unknown")) { r.kind = "treasure"; treasure--; }
                    else if (mystery > 0 && (quiet || spare)) { r.kind = "unknown"; mystery--; }
                }
        }

        // ---------------------------------------------------------------- knowing places

        // A place becomes known: shown on the map with a little cleared ground around it.
        private void ScoutNode(string id, char[] fog)
        {
            var run = Run; var p = Overworld.Poi(id);
            if (p == null) return;
            if (!run.revealed.Contains(id)) run.revealed.Add(id);
            RevealInto(fog, p.x, p.y, NodeReveal);
        }

        // Completing a place shows every place linked to it and the trails between.
        private void RevealNeighbours(string id, char[] fog)
        {
            foreach (var e in Web.EdgesOf(id))
            {
                ScoutNode(e.Other(id), fog);
                foreach (var c in e.path) RevealInto(fog, c.x, c.y, 1);
            }
        }

        // Re-applies what completed places reveal (after a start, a shift or a migration).
        private void SyncAtlas()
        {
            var run = Run; var map = Overworld;
            if (map == null) return;
            if (run.nodesDone == null) run.nodesDone = new List<string>();
            var fog = FogBuffer();
            foreach (var p in map.pois) if (NodeComplete(p.id)) { ScoutNode(p.id, fog); RevealNeighbours(p.id, fog); }
            CommitFog(fog);
        }

        // Completes a place (a cleared dungeon, a merchant or shrine reached, a tower climbed) and reveals its links.
        private void MarkNodeDone(string id)
        {
            var run = Run;
            if (!GridRun || Overworld.Poi(id) == null) return;
            if (run.nodesDone == null) run.nodesDone = new List<string>();
            if (!run.nodesDone.Contains(id)) run.nodesDone.Add(id);
            var fog = FogBuffer();
            ScoutNode(id, fog);
            RevealNeighbours(id, fog);
            CommitFog(fog);
        }

        // Reaching a place. Merchants and shrines complete on arrival; a tower is climbed. True when a tower was climbed.
        private bool ArriveAt(TowerOverworldPoi poi)
        {
            if (poi == null || NodeComplete(poi.id) || !VisitKind(poi.kind)) return false;
            MarkNodeDone(poi.id);
            if (poi.kind != "tower") return false;
            ClimbTower(poi);
            return true;
        }

        // ---------------------------------------------------------------- towers and tablets

        public static bool InTowerRange(TowerOverworldPoi tower, TowerOverworldPoi p)
        { return (p.x - tower.x) * (p.x - tower.x) + (p.y - tower.y) * (p.y - tower.y) <= TowerRadius * TowerRadius; }

        // From the top every place in the circle is seen, and the party picks one of three tablets for it.
        private void ClimbTower(TowerOverworldPoi tower)
        {
            var run = Run; var map = Overworld;
            var fog = FogBuffer();
            RevealInto(fog, tower.x, tower.y, TowerRadius);
            foreach (var p in map.pois) if (InTowerRange(tower, p)) ScoutNode(p.id, fog);
            CommitFog(fog);
            var rng = new TowerRng(TowerForestLayouts.Hash("tablets:" + tower.id, run.seed + run.shift * 31));
            var pool = new List<TowerMapMod>(MapMods);
            run.tabletOffer = new List<string>();
            for (int k = 0; k < TabletChoices && pool.Count > 0; k++)
            {
                int pick = rng.Next(pool.Count);
                run.tabletOffer.Add(tower.id + ":" + pool[pick].id);
                pool.RemoveAt(pick);
            }
            Note("From the top of " + tower.name + " the party maps the woods for " + TowerRadius + " cells around. Choose a tablet to set.");
        }

        public bool TabletPending { get { var run = Run; return run != null && run.tabletOffer != null && run.tabletOffer.Count > 0; } }

        // The mod an offered tablet ("tower:mod") adds.
        public static TowerMapMod TabletMod(string offer)
        {
            int cut = offer == null ? -1 : offer.IndexOf(':');
            return cut <= 0 ? null : MapMod(offer.Substring(cut + 1));
        }

        // Places an offered tablet would change: every place in the circle with a dungeon still to clear.
        public int TabletReach(string offer)
        {
            var map = Overworld;
            int cut = offer == null ? -1 : offer.IndexOf(':');
            var tower = cut <= 0 || map == null ? null : map.Poi(offer.Substring(0, cut));
            if (tower == null) return 0;
            int n = 0;
            foreach (var p in map.pois) if (ModdedKind(p.kind) && !NodeComplete(p.id) && InTowerRange(tower, p)) n++;
            return n;
        }

        public string ChooseTablet(int index)
        {
            var run = Run;
            if (!TabletPending) return "No tablet to choose.";
            if (index < 0 || index >= run.tabletOffer.Count) return "Pick one of the tablets.";
            string offer = run.tabletOffer[index];
            run.tabletOffer = new List<string>();
            if (run.tablets == null) run.tablets = new List<string>();
            run.tablets.Add(offer);
            var mod = TabletMod(offer);
            Note("A " + mod.name + " tablet is set: places around the tower turn " + mod.name.ToLowerInvariant() + " (" + mod.danger + "; " + mod.reward + ").");
            return null;
        }
    }
}
