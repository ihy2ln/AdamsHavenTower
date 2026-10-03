using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A staffed colony in a conquered region (TT 10.30.0). Not the run-map "outposts" of BATTLE_MODE_GDD 13: this one
    // persists, is run from the Tower, and feeds it by caravan.
    [Serializable] public sealed class TowerOutpost
    {
        public string region = "";
        public int level = 1;
        public List<int> staff = new List<int>();
        public float gold, food, water, firewood, wood, stone, ore, essence, tonics, celestium;   // waiting for a caravan
        public float condition = 100;
        public float threat;
        public float caravanSeconds, raidSeconds;
        public string report = "";
    }

    public sealed class TowerOutpostSpec
    {
        public readonly string region, primary, secondary, stat, name;
        public TowerOutpostSpec(string region, string name, string primary, string secondary, string stat)
        { this.region = region; this.name = name; this.primary = primary; this.secondary = secondary; this.stat = stat; }
    }

    public sealed partial class TowerRules
    {
        public static readonly TowerOutpostSpec[] OutpostSpecs = {
            new TowerOutpostSpec("silverbrook_edge", "Brookside Farm", "food", "wood", "grace"),
            new TowerOutpostSpec("rootside_camp", "Rootside Logging Camp", "firewood", "wood", "might"),
            new TowerOutpostSpec("shallow_ford", "Fordwatch Mill", "water", "food", "sight"),
            new TowerOutpostSpec("moon_shrine", "Moonlit Sanctum", "essence", "tonics", "wit"),
            new TowerOutpostSpec("old_bridge", "Bridge Tollhouse", "stone", "gold", "grit"),
            new TowerOutpostSpec("sunken_marsh", "Marsh Apothecary", "tonics", "food", "wit"),
            new TowerOutpostSpec("watchpost_ruin", "Watchpost Mine", "ore", "gold", "might"),
            new TowerOutpostSpec("silverwood_gate", "Gatekeep Dig", "celestium", "ore", "grit"),
            new TowerOutpostSpec("silverwood_d1", "Woodcutters' Hamlet", "wood", "firewood", "might"),
            new TowerOutpostSpec("silverwood_d2", "Lakeside Weir", "water", "essence", "sight"),
            new TowerOutpostSpec("silverwood_d3", "Crystal Delve", "ore", "celestium", "grit"),
            new TowerOutpostSpec("silverwood_d4", "Rootcity Bazaar", "gold", "essence", "luck"),
            new TowerOutpostSpec("silverwood_d5", "Heartwood Grove", "celestium", "essence", "wit")
        };

        private static readonly int[] OutpostCaps = { 1, 1, 2, 2, 3, 4, 5, 6, 8 };
        public static int MaxOutposts(int heartRank) { return OutpostCaps[Mathf.Clamp(heartRank, 1, TowerTiers.MaxRank) - 1]; }
        public const float CaravanSeconds = 360f, OutpostRaidCheck = 360f, HomesickAfter = 3 * DaySeconds;

        // A day's output per staff member of average skill, before the region's reward and the outpost's rank.
        public static float OutpostBase(string resource)
        {
            switch (resource)
            {
                case "food": case "water": case "firewood": return 40;
                case "wood": return 6;
                case "stone": return 4;
                case "ore": return 3;
                case "essence": return 2;
                case "gold": return 60;
                case "tonics": return 0.5f;
                case "celestium": return 0.4f;
                default: return 0;
            }
        }

        public static TowerOutpostSpec OutpostSpec(string region)
        {
            foreach (var spec in OutpostSpecs) if (spec.region == region) return spec;
            return null;
        }

        public TowerOutpost Outpost(string region) { return State.outposts.Find(o => o.region == region); }

        public static int OutpostSlots(int level) { return 2 * (level <= 3 ? 1 : level <= 5 ? 2 : 3); }

        // Conquered regions with no outpost yet.
        public List<string> OutpostSites()
        {
            var sites = new List<string>();
            foreach (string region in State.regionsConquered)
                if (Outpost(region) == null && OutpostSpec(region) != null) sites.Add(region);
            return sites;
        }

        public int FoundCost(string region) { var r = Region(region); return Mathf.RoundToInt(250 * (r == null ? 1 : r.reward)); }
        public const int FoundWood = 15, FoundStone = 15;

        public string FoundOutpost(string region)
        {
            string guild = GuildRequired();
            if (guild != null) return guild;
            if (OutpostSpec(region) == null) return "Unknown region.";
            if (!State.regionsConquered.Contains(region)) return "Conquer " + Region(region).name + " first: clear its lair.";
            if (Outpost(region) != null) return "There is already an outpost there.";
            int cap = MaxOutposts(State.heartRank);
            if (State.outposts.Count >= cap) return "The Heart can hold " + cap + " outpost" + (cap == 1 ? "" : "s") + " at this rank.";
            if (State.gold < FoundCost(region) || State.wood < FoundWood || State.stone < FoundStone)
                return "Founding needs " + FoundCost(region) + " gold, " + FoundWood + " wood and " + FoundStone + " stone.";
            State.gold -= FoundCost(region); State.wood -= FoundWood; State.stone -= FoundStone;
            State.outposts.Add(new TowerOutpost { region = region });
            Note("Founded the " + OutpostSpec(region).name + " in " + Region(region).name + ". Station residents to work it.");
            Bump("outpost");
            Emit("outpost", 0, 0, OutpostSpec(region).name);
            return null;
        }

        public string AbandonOutpost(string region)
        {
            var outpost = Outpost(region);
            if (outpost == null) return "No outpost there.";
            foreach (int id in outpost.staff.ToArray()) RecallPosting(Resident(id));
            State.outposts.Remove(outpost);
            Note("The " + OutpostSpec(region).name + " was abandoned; its people walked home.");
            return null;
        }

        public bool CanStation(TowerResident resident)
        {
            return resident != null && resident.ageStage == 0 && resident.origin != "body" && !resident.downed &&
                !resident.away && !resident.exploring && resident.injury < 50;
        }

        public string Station(int residentId, string region)
        {
            var outpost = Outpost(region);
            var resident = Resident(residentId);
            if (outpost == null) return "Found an outpost there first.";
            if (!CanStation(resident)) return (resident == null ? "That resident" : resident.name) + " cannot travel now.";
            if (outpost.staff.Count >= OutpostSlots(outpost.level)) return "The outpost is fully staffed. Upgrade it for more places.";
            outpost.staff.Add(residentId);
            Post(resident, "outpost:" + region);
            Note(resident.name + " set out to live at the " + OutpostSpec(region).name + ".");
            return null;
        }

        public string Unstation(int residentId)
        {
            var resident = Resident(residentId);
            if (resident == null || !IsPosted(resident) || !resident.posting.StartsWith("outpost:")) return "Not stationed anywhere.";
            var outpost = Outpost(resident.posting.Substring(8));
            if (outpost != null) outpost.staff.Remove(residentId);
            RecallPosting(resident);
            Note(resident.name + " came home from the outpost.");
            return null;
        }

        public int OutpostUpgradeGold(TowerOutpost outpost)
        {
            int bays = OutpostSlots(outpost.level + 1) / 2;
            return (outpost.level == 1 ? 200 : 600 * (outpost.level - 1)) * bays;
        }

        public int OutpostUpgradeMaterial(TowerOutpost outpost) { return outpost.level * (OutpostSlots(outpost.level + 1) / 2) * 2; }

        public string UpgradeOutpost(string region)
        {
            var outpost = Outpost(region);
            if (outpost == null) return "No outpost there.";
            if (outpost.level >= TowerTiers.MaxRank) return "The outpost is already rank SSR.";
            if (outpost.level >= RankCap()) return "Raise the Celestium Heart to upgrade the outpost past rank " + TowerTiers.Tier(outpost.level) + ".";
            int gold = OutpostUpgradeGold(outpost), material = OutpostUpgradeMaterial(outpost);
            if (State.gold < gold || State.wood < material || State.stone < material)
                return "The upgrade needs " + gold + " gold and " + material + " wood and stone.";
            State.gold -= gold; State.wood -= material; State.stone -= material;
            outpost.level++;
            Note("The " + OutpostSpec(region).name + " rose to rank " + TowerTiers.Tier(outpost.level) + ".");
            Emit("upgrade", 0, 0, outpost.level.ToString());
            return null;
        }

        // Skill of the staff in the outpost's key stat: 1.0 for one average (5) worker, like a room's work rate.
        public float StaffFactor(TowerOutpost outpost)
        {
            var spec = OutpostSpec(outpost.region);
            float total = 0;
            foreach (int id in outpost.staff)
            {
                var r = Resident(id);
                if (r != null) total += (0.35f + 0.19f * r.Stat(spec.stat)) / 1.3f;
            }
            return total;
        }

        // What the outpost makes in one game day, per resource.
        public float OutpostDaily(TowerOutpost outpost, string resource)
        {
            var spec = OutpostSpec(outpost.region);
            var region = Region(outpost.region);
            if (spec == null || region == null) return 0;
            float share = resource == spec.primary ? 1f : resource == spec.secondary ? 0.5f : 0f;
            return share * OutpostBase(resource) * region.reward * (1 + 0.5f * (outpost.level - 1)) *
                StaffFactor(outpost) * Mathf.Clamp01(outpost.condition / 100f);
        }

        public float OutpostDefence(TowerOutpost outpost)
        {
            float defence = outpost.level * 2;
            foreach (int id in outpost.staff)
            {
                var r = Resident(id);
                if (r != null) defence += r.might * 0.3f + r.weapon * 2f + (r.origin == "hero" ? 4f : 0f);
            }
            return defence;
        }

        public float AmbushChance(TowerOutpost outpost)
        { return Mathf.Clamp(0.05f + outpost.threat / 200f - OutpostDefence(outpost) / 100f, 0.02f, 0.4f); }

        private static readonly string[] OutpostGoods = { "gold", "food", "water", "firewood", "wood", "stone", "ore", "essence", "tonics", "celestium" };

        private static float Goods(TowerOutpost o, string resource)
        {
            switch (resource)
            {
                case "gold": return o.gold; case "food": return o.food; case "water": return o.water;
                case "firewood": return o.firewood; case "wood": return o.wood; case "stone": return o.stone;
                case "ore": return o.ore; case "essence": return o.essence; case "tonics": return o.tonics;
                default: return o.celestium;
            }
        }

        private static void SetGoods(TowerOutpost o, string resource, float value)
        {
            switch (resource)
            {
                case "gold": o.gold = value; break; case "food": o.food = value; break; case "water": o.water = value; break;
                case "firewood": o.firewood = value; break; case "wood": o.wood = value; break; case "stone": o.stone = value; break;
                case "ore": o.ore = value; break; case "essence": o.essence = value; break; case "tonics": o.tonics = value; break;
                default: o.celestium = value; break;
            }
        }

        public float OutpostCargo(TowerOutpost outpost, string resource) { return Goods(outpost, resource); }

        private void TickOutposts(float dt, bool live)
        {
            if (State.introPhase != "complete") return;
            foreach (string region in State.regionsConquered)
                if (!State.outpostSitesSeen.Contains(region) && OutpostSpec(region) != null)
                {
                    State.outpostSitesSeen.Add(region);
                    Note("A site for an outpost has opened in " + Region(region).name + ". Found it from the Guild.");
                    Emit("outpost_site", 0, 0, Region(region).name);
                }
            foreach (var outpost in State.outposts)
            {
                outpost.staff.RemoveAll(id => Resident(id) == null);
                var region = Region(outpost.region);
                if (region == null) continue;
                foreach (string resource in OutpostGoods)
                {
                    float daily = OutpostDaily(outpost, resource);
                    if (daily > 0) SetGoods(outpost, resource, Goods(outpost, resource) + daily * dt / DaySeconds);
                }
                foreach (int id in outpost.staff) TendStaff(Resident(id), dt);
                if (outpost.staff.Count > 0) outpost.condition = Mathf.Min(100, outpost.condition + 10f * dt / DaySeconds);
                outpost.threat = Mathf.Min(100, outpost.threat + (6f + 2f * region.reward) * dt / DaySeconds);
                outpost.caravanSeconds += dt;
                if (outpost.caravanSeconds >= CaravanSeconds) { outpost.caravanSeconds = 0; Caravan(outpost); }
                outpost.raidSeconds += dt;
                if (outpost.raidSeconds >= OutpostRaidCheck)
                {
                    outpost.raidSeconds = 0;
                    if (outpost.threat >= 40 && ColonyRandom01() < outpost.threat / 200f) OutpostRaid(outpost, live);
                }
            }
        }

        // The outpost feeds its people; morale rides on pride at first, then homesickness after three days away.
        private void TendStaff(TowerResident resident, float dt)
        {
            if (resident == null) return;
            resident.postedSeconds += dt;
            resident.hunger = Mathf.Max(resident.hunger, 70);
            resident.thirst = Mathf.Max(resident.thirst, 70);
            resident.rest = Mathf.Max(resident.rest, 70);
            if (resident.injury > 0 && resident.injury < 50) resident.injury = Mathf.Max(0, resident.injury - 0.012f * dt);
            float target = resident.postedSeconds < HomesickAfter ? 56 : 42;
            resident.happiness = Mathf.MoveTowards(resident.happiness, target, 0.09f * dt);
        }

        public string SendCaravan(string region)
        {
            var outpost = Outpost(region);
            if (outpost == null) return "No outpost there.";
            outpost.caravanSeconds = 0;
            return Caravan(outpost) ? null : "The outpost has nothing to send yet.";
        }

        // Whole units go home; fractions wait for the next caravan. An ambush on the road costs part of the cargo.
        private bool Caravan(TowerOutpost outpost)
        {
            var cargo = new Dictionary<string, int>();
            foreach (string resource in OutpostGoods)
            {
                int amount = Mathf.FloorToInt(Goods(outpost, resource));
                if (amount <= 0) continue;
                cargo[resource] = amount;
                SetGoods(outpost, resource, Goods(outpost, resource) - amount);
            }
            if (cargo.Count == 0) return false;
            string name = OutpostSpec(outpost.region).name;
            bool ambushed = ColonyRandom01() < AmbushChance(outpost);
            float kept = ambushed ? 1f - (0.3f + 0.3f * ColonyRandom01()) : 1f;
            string text = "";
            foreach (var pair in cargo)
            {
                int amount = Mathf.FloorToInt(pair.Value * kept);
                if (amount <= 0) continue;
                Deliver(pair.Key, amount);
                text += (text.Length > 0 ? ", " : "") + amount + " " + pair.Key;
            }
            outpost.report = (ambushed ? "Ambushed on the road; the rest arrived: " : "Last caravan: ") + (text.Length > 0 ? text : "nothing") + ".";
            Note((ambushed ? "Bandits struck the caravan from the " : "A caravan arrived from the ") + name +
                (text.Length > 0 ? ": " + text + "." : " with nothing left."));
            Bump("caravan");
            Emit("caravan_home", 0, 0, name);
            return true;
        }

        private void Deliver(string resource, int amount)
        {
            switch (resource)
            {
                case "gold": State.gold += amount; break;
                case "food": State.food = Mathf.Min(StockCap(), State.food + amount); break;
                case "water": State.water = Mathf.Min(StockCap(), State.water + amount); break;
                case "firewood": State.firewood = Mathf.Min(StockCap(), State.firewood + amount); break;
                case "wood": State.wood += amount; break;
                case "stone": State.stone += amount; break;
                case "ore": State.ore += amount; break;
                case "essence": State.essence += amount; break;
                case "tonics": State.tonics = Mathf.Min(30, State.tonics + amount); break;
                case "celestium": State.celestium += amount; break;
            }
        }

        // Forest raiders test the outpost. Its staff and rank hold the walls; a lost raid costs stock and condition.
        // Offline (catch-up) raids can still cost goods, but never wound anyone.
        private void OutpostRaid(TowerOutpost outpost, bool live)
        {
            var region = Region(outpost.region);
            float attack = outpost.threat * (0.3f + 0.1f * region.reward);
            string name = OutpostSpec(outpost.region).name;
            if (OutpostDefence(outpost) >= attack)
            {
                outpost.threat = Mathf.Max(0, outpost.threat - 25);
                outpost.report = "Raiders were driven off.";
                Note("Raiders struck the " + name + " and were driven off.");
                foreach (int id in outpost.staff) GiveXp(Resident(id), 10);
                return;
            }
            foreach (string resource in OutpostGoods) SetGoods(outpost, resource, Goods(outpost, resource) * 0.6f);
            outpost.condition = Mathf.Max(0, outpost.condition - 20);
            outpost.threat = Mathf.Max(0, outpost.threat - 10);
            if (live)
                foreach (int id in outpost.staff)
                {
                    var r = Resident(id);
                    if (r != null) r.injury = Mathf.Min(45, r.injury + 20);   // hurt, never downed: they hold on until recalled
                }
            outpost.report = "Raiders broke in and looted the stores.";
            Bump("outpost_raid");
            Note("Raiders broke into the " + name + " and looted its stores." + (live ? " Its people are hurt." : ""));
            Emit("outpost_raid", 0, 0, name);
        }

        private void AddOutpostThoughts(TowerResident resident, List<TowerThought> list)
        {
            if (!IsPosted(resident) || !resident.posting.StartsWith("outpost:")) return;
            list.Add(resident.postedSeconds < HomesickAfter ? new TowerThought("Frontier pride", 6) :
                new TowerThought("Homesick", -8));
        }
    }
}
