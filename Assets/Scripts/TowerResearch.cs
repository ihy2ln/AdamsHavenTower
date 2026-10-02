using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    public sealed class TowerResearchDef
    {
        public readonly string id, branch, name, effect;
        public readonly int index;          // 1..8 inside its branch
        public readonly string[] unlocks;   // Construction nodes: the buildings whose blueprints they grant
        public TowerResearchDef(string branch, int index, string name, string effect, params string[] unlocks)
        {
            this.branch = branch; this.index = index; this.name = name; this.effect = effect; this.unlocks = unlocks;
            id = branch + "-" + index;
        }
        // Nodes 1-2 are tier 1, 3-4 tier 2, 5-6 tier 3, 7 tier 4, 8 tier 5 (GDD 8.2).
        public int Tier { get { return index <= 2 ? 1 : index <= 4 ? 2 : index <= 6 ? 3 : index == 7 ? 4 : 5; } }
    }

    // The Heart's research tree (TOWER_MODE_GDD 8.2): 5 branches x 8 nodes, one at a time, on a real-time timer that
    // keeps running while the game is closed. Nodes cost Celestium (plus gold from tier 3) and can be rushed with Tonics.
    // A node needs the previous node of its branch and a Heart rank whose research tier reaches it.
    public sealed partial class TowerRules
    {
        public const int ResearchVersion = 1;
        public static readonly string[] Branches = { "CON", "SET", "PRO", "DEF", "EXP" };
        public static readonly string[] BranchNames = { "Construction", "Settlement", "Production", "Defence", "Exploration" };

        // Wall-clock seconds; tests swap it for a fixed clock.
        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static readonly int[] TierCelestium = { 10, 30, 90, 270, 800 };
        private static readonly int[] TierGold = { 0, 0, 500, 3000, 15000 };
        private static readonly int[] TierSeconds = { 600, 3600, 3 * 3600, 8 * 3600, 16 * 3600 };
        private static readonly int[] HeartResearchTier = { 1, 1, 2, 2, 3, 3, 4, 4, 5 };   // Heart F..SSR (8.4)
        public const int RushSecondsPerTonic = 1800;

        public static readonly TowerResearchDef[] ResearchTree =
        {
            new TowerResearchDef("CON", 1, "Farmstead plans", "Unlocks the Farmstead.", "farmstead"),
            new TowerResearchDef("CON", 2, "Market and Cottage", "Unlocks the Argent Market and the Cottage.", "market", "cottage"),
            new TowerResearchDef("CON", 3, "Stores and tavern", "Unlocks the Barn, Grain Silo and The Frosted Mug.", "barn", "silo", "frosted_mug"),
            new TowerResearchDef("CON", 4, "Stone and storage", "Unlocks the Stone Quarry and the Warehouse.", "quarry", "warehouse"),
            new TowerResearchDef("CON", 5, "Training halls", "Unlocks The Forge and the Deck Hall.", "forge", "deck_hall"),
            new TowerResearchDef("CON", 6, "Fine homes", "Unlocks the Hearth Nursery, Terrace Row and Ashgrove Manor.", "nursery", "terrace_row", "manor"),
            new TowerResearchDef("CON", 7, "Celestium fittings", "Buildings may be upgraded to rank SSR."),
            new TowerResearchDef("CON", 8, "Master builders", "Room upgrades cost 10% less gold."),

            new TowerResearchDef("SET", 1, "Shared meals", "Residents feel better: +3 mood."),
            new TowerResearchDef("SET", 2, "Cozy lighting", "Residents feel better: +3 mood."),
            new TowerResearchDef("SET", 3, "Better beds", "Every home holds about 10% more people (at least +1 bed)."),
            new TowerResearchDef("SET", 4, "Apprenticeships", "Residents earn 25% more experience."),
            new TowerResearchDef("SET", 5, "Nursery births", "Families have children 25% sooner."),
            new TowerResearchDef("SET", 6, "Festival", "A harvest festival every second day lifts every resident's mood."),
            new TowerResearchDef("SET", 7, "Hero housing", "Heroes recover health 50% faster."),
            new TowerResearchDef("SET", 8, "Steward Mk II", "The Steward also places idle residents by their best stat."),

            new TowerResearchDef("PRO", 1, "Crop rotation", "+5% Food from every collect."),
            new TowerResearchDef("PRO", 2, "Sharper tools", "+3% yield from every room."),
            new TowerResearchDef("PRO", 3, "Seasoned timber", "+5% Firewood from every collect."),
            new TowerResearchDef("PRO", 4, "Deep wells", "+5% Water from every collect."),
            new TowerResearchDef("PRO", 5, "Pack storage", "+15% storage caps."),
            new TowerResearchDef("PRO", 6, "Trade ledgers", "+8% Gold from every collect."),
            new TowerResearchDef("PRO", 7, "Cycle shortening", "Production cycles run 8% shorter."),
            new TowerResearchDef("PRO", 8, "Celestium sieve", "+1 Celestium from every Quarry collect."),

            new TowerResearchDef("DEF", 1, "Watchfires", "Fires do 15% less damage."),
            new TowerResearchDef("DEF", 2, "Pest bait", "Pests are 20% weaker."),
            new TowerResearchDef("DEF", 3, "Gate guard post", "A third guard can hold the Gate."),
            new TowerResearchDef("DEF", 4, "Field medicine", "Carers heal 25% faster."),
            new TowerResearchDef("DEF", 5, "Wardstones", "The Heart's warning stages come later (strained below 50%, critical below 20%)."),
            new TowerResearchDef("DEF", 6, "Tonic still", "+1 Tonic from every Frosted Mug collect."),
            new TowerResearchDef("DEF", 7, "Hero bulwark", "Heroes fight incidents 25% harder."),
            new TowerResearchDef("DEF", 8, "Celestial aegis", "Raiders hurt the Heart 30% less, and the Heart slowly heals when its chamber is calm."),

            new TowerResearchDef("EXP", 1, "Guild charts", "Opens Rootside and the Ford (regions 2-3)."),
            new TowerResearchDef("EXP", 2, "Pack mules", "Expeditions bring home 20% more gold."),
            new TowerResearchDef("EXP", 3, "Sigil focus", "+10% Sigils from every source."),
            new TowerResearchDef("EXP", 4, "Shrine and bridge maps", "Opens the Moon Shrine and the Old Bridge (regions 4-5)."),
            new TowerResearchDef("EXP", 5, "Earlier soft pity", "Soft pity starts at pull 45 instead of 50."),
            new TowerResearchDef("EXP", 6, "Marsh and watchpost maps", "Opens the Marsh and the Watchpost (regions 6-7)."),
            new TowerResearchDef("EXP", 7, "Fusion boon", "Duplicate summons add one extra level."),
            new TowerResearchDef("EXP", 8, "Silverwood key", "Opens the Wood Gate (region 8) and raises SSR odds by 0.5%."),
        };

        // Which research node opens each region (regions not listed need no research).
        private static readonly Dictionary<string, string> RegionResearch = new Dictionary<string, string>
        {
            { "rootside_camp", "EXP-1" }, { "shallow_ford", "EXP-1" },
            { "moon_shrine", "EXP-4" }, { "old_bridge", "EXP-4" },
            { "sunken_marsh", "EXP-6" }, { "watchpost_ruin", "EXP-6" },
            { "silverwood_gate", "EXP-8" },
        };

        // Buildings known from the start, before any Construction research (GDD 8.2).
        public static readonly string[] StartingBlueprints = { "house", "kitchen", "well", "lumber_mill", "guild_hall" };

        public static TowerResearchDef ResearchDef(string id)
        {
            foreach (var def in ResearchTree) if (def.id == id) return def;
            return null;
        }

        // The Construction node that unlocks a building, or null for a starting building.
        public static TowerResearchDef UnlockNode(string buildingType)
        {
            foreach (var def in ResearchTree)
                if (def.unlocks != null && Array.IndexOf(def.unlocks, buildingType) >= 0) return def;
            return null;
        }

        public static int ResearchCelestium(TowerResearchDef def) { return TierCelestium[def.Tier - 1]; }
        public static int ResearchGold(TowerResearchDef def) { return TierGold[def.Tier - 1]; }
        public static int ResearchSeconds(TowerResearchDef def) { return TierSeconds[def.Tier - 1]; }
        public static int ResearchTierFor(int heartRank) { return HeartResearchTier[Mathf.Clamp(heartRank, 1, 9) - 1]; }
        public int ResearchTierCap() { return ResearchTierFor(State.heartRank); }

        public bool Researched(string id) { return State.research != null && State.research.Contains(id); }
        public bool Researching { get { return !string.IsNullOrEmpty(State.researching); } }

        public long ResearchRemaining()
        { return Researching ? Math.Max(0, State.researchEndsUnix - NowUnix()) : 0; }

        public float ResearchProgress()
        {
            var def = ResearchDef(State.researching);
            if (def == null) return 0;
            return Mathf.Clamp01(1f - ResearchRemaining() / (float)ResearchSeconds(def));
        }

        public int RushTonicCost()
        { return Researching ? Mathf.Max(1, Mathf.CeilToInt(ResearchRemaining() / (float)RushSecondsPerTonic)) : 0; }

        // Why a node cannot start now, or null when it can.
        public string CanResearch(string id)
        {
            var def = ResearchDef(id);
            if (def == null) return "Unknown research.";
            if (State.introPhase != "complete") return "Found the Tower first.";
            if (Researched(id)) return def.name + " is already known.";
            if (Researching) return "The Heart is already studying " + ResearchDef(State.researching).name + ".";
            if (def.index > 1 && !Researched(def.branch + "-" + (def.index - 1)))
                return "Research " + ResearchDef(def.branch + "-" + (def.index - 1)).name + " first.";
            if (def.Tier > ResearchTierCap())
            {
                int rank = State.heartRank;
                while (rank < TowerTiers.MaxRank && ResearchTierFor(rank) < def.Tier) rank++;
                return "Raise the Heart to rank " + TowerTiers.Tier(rank) + " to study tier " + def.Tier + " research.";
            }
            if (State.celestium < ResearchCelestium(def) || State.gold < ResearchGold(def))
                return def.name + " needs " + ResearchCelestium(def) + " Celestium" +
                    (ResearchGold(def) > 0 ? " and " + Compact(ResearchGold(def)) + " gold." : ".");
            return null;
        }

        public string StartResearch(string id)
        {
            string error = CanResearch(id);
            if (error != null) return error;
            var def = ResearchDef(id);
            State.celestium -= ResearchCelestium(def);
            State.gold -= ResearchGold(def);
            State.researching = id;
            State.researchEndsUnix = NowUnix() + ResearchSeconds(def);
            Note("The Heart began studying " + def.name + ".");
            Emit("research_start", Room0("heart"), 0, def.name);
            return null;
        }

        public string RushResearch()
        {
            if (!Researching) return "Nothing is being researched.";
            int cost = RushTonicCost();
            if (State.tonics < cost) return "Rushing needs " + cost + " Tonic" + (cost > 1 ? "s" : "") + ".";
            State.tonics -= cost;
            State.researchEndsUnix = NowUnix();
            TickResearch();
            return null;
        }

        // Called on every Advance (and so on load, through the offline catch-up).
        private void TickResearch()
        {
            if (!Researching || NowUnix() < State.researchEndsUnix) return;
            CompleteResearch(State.researching);
            State.researching = "";
            State.researchEndsUnix = 0;
        }

        private void CompleteResearch(string id)
        {
            var def = ResearchDef(id);
            if (def == null || Researched(id)) return;
            State.research.Add(id);
            if (def.unlocks != null)
                foreach (string type in def.unlocks)
                    if (!State.blueprints.Contains(type)) State.blueprints.Add(type);
            Note("Research complete: " + def.name + ". " + def.effect);
            Bump("research");
            Emit("research", Room0("heart"), 0, def.name);
        }

        // Older saves bought buildings one by one and opened regions by conquest alone. Give them the nodes that
        // match what they already have (and each node's predecessors), so nothing they own becomes locked.
        private void MigrateResearch()
        {
            if (State.research == null) State.research = new List<string>();
            if (State.researchVersion >= ResearchVersion) return;
            State.researchVersion = ResearchVersion;
            if (State.introPhase != "complete") return;
            foreach (var def in ResearchTree)
            {
                bool owned = false;
                if (def.unlocks != null)
                    foreach (string type in def.unlocks)
                        if (State.blueprints.Contains(type) || State.rooms.Exists(r => r.type == type)) owned = true;
                foreach (var pair in RegionResearch)
                    if (pair.Value == def.id && State.regionsUnlocked.Contains(pair.Key)) owned = true;
                if (!owned) continue;
                for (int i = 1; i <= def.index; i++)
                {
                    string need = def.branch + "-" + i;
                    if (!Researched(need)) CompleteResearchQuietly(need);
                }
            }
        }

        private void CompleteResearchQuietly(string id)
        {
            var def = ResearchDef(id);
            State.research.Add(id);
            if (def.unlocks != null)
                foreach (string type in def.unlocks)
                    if (!State.blueprints.Contains(type)) State.blueprints.Add(type);
        }

        // Checkpoint saves (TowerMilestones): everything the checkpoint's Heart rank could have studied.
        public void GrantResearchUpToTier(int tier)
        {
            if (State.research == null) State.research = new List<string>();
            foreach (var def in ResearchTree)
                if (def.Tier <= tier && !Researched(def.id)) CompleteResearchQuietly(def.id);
            State.researchVersion = ResearchVersion;
        }

        // ---------------------------------------------------------------- effects read by the other systems

        public bool RegionResearched(string regionId)
        {
            string node;
            return !RegionResearch.TryGetValue(regionId, out node) || Researched(node);
        }

        public static string RegionResearchNode(string regionId)
        {
            string node;
            return RegionResearch.TryGetValue(regionId, out node) ? node : null;
        }

        // Yield multiplier for a collect of this resource (PRO branch).
        public float YieldBonus(string produces)
        {
            float bonus = 1f;
            if (Researched("PRO-2")) bonus += 0.03f;
            switch (produces)
            {
                case "food": if (Researched("PRO-1")) bonus += 0.05f; break;
                case "firewood": if (Researched("PRO-3")) bonus += 0.05f; break;
                case "water": if (Researched("PRO-4")) bonus += 0.05f; break;
                case "gold": if (Researched("PRO-6")) bonus += 0.08f; break;
            }
            return bonus;
        }

        public float CycleSpeed() { return Researched("PRO-7") ? 1f / 0.92f : 1f; }
        public float StockCapBonus() { return Researched("PRO-5") ? 1.15f : 1f; }
        public float XpBonus() { return Researched("SET-4") ? 1.25f : 1f; }
        public float CareBonus() { return Researched("DEF-4") ? 1.25f : 1f; }
        public float FireDamageScale() { return Researched("DEF-1") ? 0.85f : 1f; }
        public int SoftPityStart() { return Researched("EXP-5") ? 45 : SoftPityFrom; }
        public float SsrRateBonus() { return Researched("EXP-8") ? 0.5f : 0f; }

        // Every Sigil grant goes through here so Sigil focus (EXP-3) can add 10%; fractions carry to the next grant.
        private int GrantSigils(int amount)
        {
            if (amount <= 0) return 0;
            float total = amount * (Researched("EXP-3") ? 1.1f : 1f) + State.sigilCarry;
            int whole = Mathf.FloorToInt(total + 0.0001f);
            State.sigilCarry = total - whole;
            State.sigils += whole;
            return whole;
        }
    }
}
