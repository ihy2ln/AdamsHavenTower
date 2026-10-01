using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Guild expeditions: region map -> plan -> forest map (1 of 15 layouts) -> themed dungeon crawls.
    // Rules only; TowerExpeditionUi draws it and BattleMode fights the battles.

    public sealed class TowerRegionDef
    {
        public readonly string id, name, blurb;
        public readonly float x, y;           // normalized position on Expedition/Maps/region (y from the top)
        public readonly string[] requires;
        public readonly int depth, bossDepth;
        public readonly float reward;
        public TowerRegionDef(string id, string name, string blurb, float x, float y, int depth, int bossDepth,
            float reward, params string[] requires)
        {
            this.id = id; this.name = name; this.blurb = blurb; this.x = x; this.y = y; this.depth = depth;
            this.bossDepth = bossDepth; this.reward = reward; this.requires = requires;
        }
    }

    [Serializable] public sealed class TowerLoot
    {
        public string name = "";
        public int gold, ore, essence, celestium, tonics;
    }

    [Serializable] public sealed class TowerRun
    {
        public string region = "", layout = "";
        public int seed;
        public List<string> party = new List<string>();     // first three fight, the rest are reserves
        public List<int> hp = new List<int>();              // percent, parallel to party
        public int rations, tonics, firewood;
        public List<string> visited = new List<string>();
        public List<string> cleared = new List<string>();
        public string at = "";
        public List<TowerLoot> haul = new List<TowerLoot>();
        public List<TowerLoot> pocket = new List<TowerLoot>();
        public int battlesWon;
        // Dungeon crawl; empty dungeonPoi means the party is on the forest map.
        public string dungeonPoi = "";
        public int floor, px, py, prevX, prevY;
        // Missing in older saves: zero keeps the active legacy floor intact.
        public int dungeonLayoutVersion;
        public string fog = "";
        public List<int> roomsDone = new List<int>();
        // Forest fog, roads and threat (TowerThreat.cs). Older saves load these empty; NormalizeExpeditions refills the fog.
        public List<string> revealed = new List<string>();
        public List<string> road = new List<string>();      // walked trails, TowerRules.RoadKey
        public int threat, roadSteps;
        // Traversal events (TowerEvents.cs): the open event card, its last result, what was seen and remembered.
        public string eventId = "", eventResult = "";
        public List<string> eventsSeen = new List<string>();
        public List<string> flags = new List<string>();
        public int steps, ambushDepth;                     // ambushDepth > 0: a fight waits on the forest map
        public bool lastStepEvent;
        public List<TowerTrail> trails = new List<TowerTrail>();   // the routes actually walked on the map
        // Layered grid map (TowerOverworldRules.cs). "" = a painted-plate run from before the grid map.
        // The grid is regenerated from biome + gridSeed + shift; only the party cell, fog and worn road are saved.
        public string mapKind = "", biome = "", gridFog = "", gridRoad = "";
        public int gridVersion, gridSeed, shift, cx, cy, targetX = -1, targetY = -1;
        public float travelCarry, threatCarry;
    }

    // One walked route between two forest places, as points on the painted map (normalised, y from the top).
    [Serializable] public sealed class TowerTrail
    {
        public string a = "", b = "";
        public List<Vector2> pts = new List<Vector2>();
    }

    public sealed partial class TowerRules
    {
        public const int PocketSlots = 2, RationFood = 5, RationWater = 5, FirewoodBundle = 10;
        public static readonly string[] Fighters = { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity" };

        public static readonly TowerRegionDef[] Regions =
        {
            new TowerRegionDef("silverbrook_edge", "Brook Edge", "Trails just past the village fences.", 0.30f, 0.62f, 1, 3, 1f),
            new TowerRegionDef("rootside_camp", "Rootside", "An abandoned ranger camp in the pines.", 0.19f, 0.34f, 2, 3, 1.2f, "silverbrook_edge"),
            new TowerRegionDef("shallow_ford", "Ford", "Stepping stones over the Silverbrook.", 0.47f, 0.69f, 2, 4, 1.3f, "silverbrook_edge"),
            new TowerRegionDef("moon_shrine", "Moon Shrine", "A ruined shrine that hums at night.", 0.31f, 0.11f, 3, 5, 1.6f, "rootside_camp"),
            new TowerRegionDef("old_bridge", "Old Bridge", "The stone bridge and the isle beyond.", 0.52f, 0.45f, 3, 5, 1.7f, "shallow_ford"),
            new TowerRegionDef("sunken_marsh", "Marsh", "Drowned groves and a sunken cache.", 0.80f, 0.76f, 4, 6, 2f, "shallow_ford"),
            new TowerRegionDef("watchpost_ruin", "Watchpost", "Raiders hold the old watch tower.", 0.64f, 0.18f, 5, 7, 2.4f, "old_bridge"),
            new TowerRegionDef("silverwood_gate", "Wood Gate", "The statue gate into the deep wood.", 0.86f, 0.37f, 7, 8, 3f,
                "watchpost_ruin", "sunken_marsh"),
            // Beyond the Gate: five Silverwood depths, each fought on one of its three painted maps.
            new TowerRegionDef("silverwood_d1", "Wood Edge", "Woodcutters' hamlets and fallen oaks past the Gate.", 0.88f, 0.09f, 8, 9, 3.4f, "silverwood_gate"),
            new TowerRegionDef("silverwood_d2", "Lakes", "Still lakes, braided rivers and drowned groves.", 0.88f, 0.22f, 9, 10, 3.8f, "silverwood_d1"),
            new TowerRegionDef("silverwood_d3", "Mountains", "Cliff ridges, old mines and crystal caverns.", 0.90f, 0.54f, 10, 11, 4.2f, "silverwood_d2"),
            new TowerRegionDef("silverwood_d4", "Ruins", "A sunken city and blighted citadel under the roots.", 0.90f, 0.67f, 11, 12, 4.6f, "silverwood_d3"),
            new TowerRegionDef("silverwood_d5", "Heart", "The world tree, the hollow gate and the blight.", 0.90f, 0.90f, 12, 13, 5.2f, "silverwood_d4"),
        };

        public static TowerRegionDef Region(string id)
        {
            foreach (var r in Regions) if (r.id == id) return r;
            return null;
        }

        // 1 to 5 for the Silverwood depth regions, 0 for the Silverbrook ones.
        public static int SilverwoodDepth(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || !regionId.StartsWith("silverwood_d") || regionId.Length != 13) return 0;
            int depth = regionId[12] - '0';
            return depth >= 1 && depth <= 5 ? depth : 0;
        }

        public TowerRun Run { get { return State.hasRun ? State.run : null; } }
        public TowerRegionDef RunRegion { get { return State.hasRun ? Region(State.run.region) : null; } }
        public TowerForestLayout RunLayout
        { get { return !State.hasRun ? null : State.run.mapKind == GridKind ? GridLayout : TowerForestLayouts.Get(State.run.layout); } }

        public bool RegionUnlocked(string id) { return State.regionsUnlocked.Contains(id); }
        public bool RegionConquered(string id) { return State.regionsConquered.Contains(id); }

        private void NormalizeExpeditions()
        {
            if (State.regionsUnlocked == null) State.regionsUnlocked = new List<string>();
            if (State.regionsConquered == null) State.regionsConquered = new List<string>();
            if (State.run == null) { State.run = new TowerRun(); State.hasRun = false; }
            if (!State.regionsUnlocked.Contains(Regions[0].id)) State.regionsUnlocked.Add(Regions[0].id);
            if (State.hasRun && RunLayout == null) State.hasRun = false;
            if (State.hasRun && State.run.dungeonPoi.Length > 0 && RunLayout.Node(State.run.dungeonPoi) == null)
                State.run.dungeonPoi = "";
            if (State.hasRun && State.run.revealed.Count == 0) RebuildFog();
            if (State.hasRun && State.run.eventId.Length > 0 && TowerEvents.Get(State.run.eventId) == null) State.run.eventId = "";
        }

        // ---------------------------------------------------------------- plan

        public int MaxRations { get { return Mathf.FloorToInt(Mathf.Min(State.food / RationFood, State.water / RationWater)); } }
        public int MaxFirewood { get { return Mathf.FloorToInt(State.firewood / FirewoodBundle); } }

        // Gear forged in the Tower (weapon and tool levels) goes with the hero of the same name.
        public TowerResident HeroResident(string unitId)
        { return State.residents.Find(r => r.unitId == unitId && r.origin == "hero"); }
        public float GearAttack(string unitId) { var h = HeroResident(unitId); return h == null ? 0 : 0.08f * h.weapon; }
        public float GearHp(string unitId) { var h = HeroResident(unitId); return h == null ? 0 : 0.06f * h.tool; }

        public string CanStartExpedition(string region, List<string> party, int rations, int tonics, int firewood)
        {
            string error = GuildRequired();
            if (error != null) return error;
            if (State.hasRun) return "An expedition is already under way.";
            if (Region(region) == null) return "Unknown region.";
            if (!RegionUnlocked(region)) return "Conquer the neighbouring regions first.";
            if (party == null || party.Count == 0) return "Pick at least one fighter.";
            if (party.Count > 6) return "At most six fighters.";
            foreach (var id in party)
            {
                if (Array.IndexOf(Fighters, id) < 0) return "Unknown fighter.";
                if (party.FindAll(p => p == id).Count > 1) return "Each fighter can go once.";
                var hero = HeroResident(id);
                if (hero != null && (hero.downed || hero.exploring || hero.injury >= 50)) return hero.name + " cannot travel now.";
            }
            if (rations < 0 || tonics < 0 || firewood < 0) return "Invalid provisions.";
            if (rations > MaxRations) return "Not enough food and water for " + rations + " rations.";
            if (tonics > State.tonics) return "Not enough tonics.";
            if (firewood > MaxFirewood) return "Not enough firewood.";
            return null;
        }

        // layoutId picks the map. Null or "" (or a map outside the region's pool) keeps the random shift.
        public string StartExpedition(string region, List<string> party, int rations, int tonics, int firewood, string layoutId = null)
        {
            string error = CanStartExpedition(region, party, rations, tonics, firewood);
            if (error != null) return error;
            State.food -= rations * RationFood;
            State.water -= rations * RationWater;
            State.tonics -= tonics;
            State.firewood -= firewood * FirewoodBundle;
            // The forest shifts: a random layout, never the same one twice in a row.
            int silverwood = SilverwoodDepth(region);
            var ids = silverwood > 0 ? TowerForestLayouts.SilverwoodDepthIds(silverwood) : TowerForestLayouts.Ids;
            if (silverwood > 0 && (ids.Length == 0 || TowerForestLayouts.Get(ids[0]) == null)) ids = TowerForestLayouts.Ids;
            int pick = Mathf.Min(ids.Length - 1, Mathf.FloorToInt(Random01() * ids.Length));
            if (ids[pick] == State.lastLayout) pick = (pick + 1 + Mathf.FloorToInt(Random01() * (ids.Length - 1))) % ids.Length;
            int chosen = string.IsNullOrEmpty(layoutId) ? -1 : Array.IndexOf(ids, layoutId);
            if (chosen >= 0) pick = chosen;
            var layout = TowerForestLayouts.Get(ids[pick]);
            var run = new TowerRun { region = region, layout = layout.id, seed = State.randomState, rations = rations,
                tonics = tonics, firewood = firewood, at = layout.entrance };
            foreach (var id in party) { run.party.Add(id); run.hp.Add(100); }
            run.visited.Add(layout.entrance);
            State.run = run;
            State.hasRun = true;
            State.lastLayout = layout.id;
            if (GridMaps) StartGridRun(run, region);
            RevealFrom(run.at);
            Note("Expedition set out for " + Region(region).name + " (" + RunLayout.name + ").");
            return null;
        }

        // ---------------------------------------------------------------- forest map

        public bool NodeVisible(string nodeId)
        {
            var run = Run;
            return run != null && run.revealed.Contains(nodeId);
        }

        public string ForestMove(string nodeId)
        {
            var run = Run; var layout = RunLayout;
            if (run == null) return "No expedition.";
            if (run.dungeonPoi.Length > 0) return "Leave the dungeon first.";
            if (layout.Node(nodeId) == null) return "Unknown place.";
            if (GridRun)
            {
                // On the grid any seen place can be walked to; an event on the way may stop the party short.
                var poi = Overworld.Poi(nodeId);
                return GridMove(poi.x, poi.y).error;
            }
            if (!layout.Linked(run.at, nodeId)) return "No trail leads there from here.";
            string block = EventBlock();
            if (block != null) return block;
            string from = run.at;
            bool road = OnRoad(from, nodeId);
            run.at = nodeId;
            if (!run.visited.Contains(nodeId)) run.visited.Add(nodeId);
            RevealFrom(nodeId);
            if (WalkTrail(from, nodeId))
            {
                if (run.rations > 0) run.rations--;
                else
                {
                    for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - 10);
                    Note("No rations left: the party grows weak.");
                }
            }
            RollTraversalEvent(nodeId, road);
            return null;
        }

        public string CampRest()
        {
            var run = Run;
            if (run == null) return "No expedition.";
            var node = RunLayout.Node(run.at);
            if (node == null || node.kind != "camp") return "Rest at a camp.";
            if (EventBlock() != null) return EventBlock();
            if (run.firewood <= 0) return "No firewood to make camp.";
            run.firewood--;
            HealParty(35, false);
            LowerThreat(ThreatCampRest);
            Note("The party rested by the fire.");
            return null;
        }

        public string UseTonic(string unitId)
        {
            var run = Run;
            if (run == null) return "No expedition.";
            if (run.tonics <= 0) return "No tonics left.";
            int i = run.party.IndexOf(unitId);
            if (i < 0) return "Not in the party.";
            if (run.hp[i] >= 100) return "Already at full health.";
            run.tonics--;
            run.hp[i] = run.hp[i] <= 0 ? 50 : Mathf.Min(100, run.hp[i] + 40);
            return null;
        }

        private void HealParty(int amount, bool revive)
        {
            var run = Run;
            for (int i = 0; i < run.hp.Count; i++)
                if (run.hp[i] > 0 || revive) run.hp[i] = Mathf.Min(100, Mathf.Max(run.hp[i], 0) + amount);
        }

        public bool PartyAlive
        {
            get { var run = Run; if (run == null) return false; foreach (var h in run.hp) if (h > 0) return true; return false; }
        }

        // ---------------------------------------------------------------- loot

        public string PocketLoot(int index)
        {
            var run = Run;
            if (run == null || index < 0 || index >= run.haul.Count) return "Nothing to pocket.";
            if (run.pocket.Count >= PocketSlots) return "The Safe Pocket is full.";
            run.pocket.Add(run.haul[index]);
            run.haul.RemoveAt(index);
            return null;
        }

        public string UnpocketLoot(int index)
        {
            var run = Run;
            if (run == null || index < 0 || index >= run.pocket.Count) return "Nothing there.";
            run.haul.Add(run.pocket[index]);
            run.pocket.RemoveAt(index);
            return null;
        }

        public static string LootLine(TowerLoot loot)
        {
            var parts = new List<string>();
            if (loot.gold > 0) parts.Add(loot.gold + "g");
            if (loot.ore > 0) parts.Add(loot.ore + " ore");
            if (loot.essence > 0) parts.Add(loot.essence + " essence");
            if (loot.celestium > 0) parts.Add(loot.celestium + " Celestium");
            if (loot.tonics > 0) parts.Add(loot.tonics + " tonic");
            return loot.name + " (" + string.Join(", ", parts.ToArray()) + ")";
        }

        private void Bank(TowerLoot loot)
        {
            State.gold += loot.gold; State.ore += loot.ore; State.essence += loot.essence;
            State.celestium += loot.celestium; State.tonics += loot.tonics;
        }

        // Leaving (wiped == false) banks the haul and returns unused provisions.
        // A wiped party keeps only the Safe Pocket; experience was already earned.
        public string EndExpedition(bool wiped)
        {
            var run = Run;
            if (run == null) return "No expedition.";
            int banked = 0;
            foreach (var loot in run.pocket) { Bank(loot); banked += loot.gold; }
            if (!wiped)
            {
                foreach (var loot in run.haul) { Bank(loot); banked += loot.gold; }
                State.food += run.rations * RationFood;
                State.water += run.rations * RationWater;
                State.tonics += run.tonics;
                State.firewood += run.firewood * FirewoodBundle;
            }
            // Heroes come home carrying their wounds.
            for (int i = 0; i < run.party.Count; i++)
            {
                var hero = HeroResident(run.party[i]);
                if (hero != null && run.hp[i] < 50) hero.injury = Mathf.Max(hero.injury, 50 - run.hp[i] / 2f);
            }
            State.hasRun = false;
            Bump("expedition");
            Note(wiped ? "The expedition party fell. Only the Safe Pocket came home." :
                "The expedition returned: " + banked + " gold banked.");
            return null;
        }

        private void ConquerRegion(string id)
        {
            if (!State.regionsConquered.Contains(id)) State.regionsConquered.Add(id);
            foreach (var region in Regions)
            {
                if (State.regionsUnlocked.Contains(region.id)) continue;
                bool open = true;
                foreach (var need in region.requires) if (!State.regionsConquered.Contains(need)) open = false;
                if (open) { State.regionsUnlocked.Add(region.id); Note(region.name + " can now be explored."); }
            }
            Note(Region(id).name + " is conquered.");
        }

        // Experience always survives: it is granted to the heroes the moment a fight is won.
        private void AwardExpeditionXp(int amount)
        {
            foreach (var id in Run.party)
            {
                var hero = HeroResident(id);
                if (hero != null) GiveXp(hero, amount);
            }
        }
    }
}
