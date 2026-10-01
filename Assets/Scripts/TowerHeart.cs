using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One unit revealed by a Heart summon.
    public sealed class TowerSummon
    {
        public string name, unitId, kind;   // kind: "hero" or "resident"
        public int rank;                    // 1 (F) to 9 (SSR)
        public bool fused;                  // a duplicate hero that raised the one already in the Tower
    }

    // The Celestium Heart's own systems (TOWER_MODE_GDD section 8): rank upgrades, warning stages,
    // the hard-fail legacy run, and summoning heroes and residents with Sigils.
    public sealed partial class TowerRules
    {
        // ---------------------------------------------------------------- rank

        private static readonly float[] HeartHp = { 1200, 1800, 2600, 3400, 4300, 5300, 6400, 7600, 9000 };
        public static float HeartMaxHp(int rank) { return HeartHp[Mathf.Clamp(rank, 1, HeartHp.Length) - 1]; }

        // Celestium and gold to raise the Heart one rank (8.3). First-pass numbers for the 60-90 day pacing.
        private static readonly int[] HeartCelestium = { 8, 20, 45, 90, 160, 280, 450, 700 };
        public int HeartUpgradeCelestium()
        { return State.heartRank >= TowerTiers.MaxRank ? 0 : HeartCelestium[State.heartRank - 1]; }
        public int HeartUpgradeGold() { return State.heartRank >= TowerTiers.MaxRank ? 0 : 500 * State.heartRank; }

        public string UpgradeHeart()
        {
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (State.heartRank >= TowerTiers.MaxRank) return "The Heart has reached rank SSR.";
            int celestium = HeartUpgradeCelestium(), gold = HeartUpgradeGold();
            if (State.celestium < celestium || State.gold < gold)
                return "Raising the Heart to rank " + TowerTiers.Tier(State.heartRank + 1) + " needs " +
                    celestium + " Celestium and " + gold + " gold.";
            State.celestium -= celestium; State.gold -= gold;
            State.heartRank++;
            State.heartHp = HeartMaxHp(State.heartRank);
            Note("The Celestium Heart rose to rank " + TowerTiers.Tier(State.heartRank) + ". Buildings may now reach rank " +
                TowerTiers.Tier(RankCap()) + ".");
            Bump("heart");
            Emit("heart", 0, 0, State.heartRank.ToString());
            return null;
        }

        // ---------------------------------------------------------------- warning stages (8.5)

        public string HeartStage()
        {
            float share = State.heartHp / HeartMaxHp(State.heartRank);
            return share > 0.6f ? "stable" : share > 0.25f ? "strained" : "critical";
        }

        private void CheckHeartStage()
        {
            string stage = HeartStage();
            if (stage == State.heartStage) return;
            bool worse = stage == "critical" || stage == "strained" && State.heartStage == "stable";
            State.heartStage = stage;
            if (!worse) { Note("The Celestium Heart steadies (" + stage + ")."); return; }
            Note(stage == "critical" ?
                "The Celestium Heart is CRITICAL. If it falls, this run ends. Guard it now." :
                "The Celestium Heart is strained. Drive the danger out before it fails.");
            Emit("heart_" + stage, Room0("heart"), 0, stage);
        }

        private int Room0(string type) { var room = State.rooms.Find(r => r.type == type); return room == null ? 0 : room.uid; }

        // ---------------------------------------------------------------- hard fail and legacy (8.5)

        // A fallen Heart ends the run. Heroes, Sigils and the summon pity carry over; the tower resets.
        public static TowerState LegacyRun(TowerState fallen)
        {
            var state = TowerMilestones.Create(1);
            state.slot = fallen.slot;
            state.runs = fallen.runs + 1;
            state.sigils = fallen.sigils;
            state.summonPity = fallen.summonPity;
            state.freeSummonUsed = fallen.freeSummonUsed;
            state.legacyHeroes = new List<TowerResident>();
            var seen = new HashSet<string>();
            var heroes = new List<TowerResident>(fallen.residents);
            if (fallen.legacyHeroes != null) heroes.AddRange(fallen.legacyHeroes);
            foreach (var hero in heroes)
            {
                if (hero.origin != "hero" || string.IsNullOrEmpty(hero.unitId) || seen.Contains(hero.unitId)) continue;
                seen.Add(hero.unitId);
                state.legacyHeroes.Add(new TowerResident { name = hero.name, unitId = hero.unitId, origin = "hero",
                    rank = hero.rank, level = hero.level, xp = hero.xp, trait = hero.trait,
                    might = hero.might, sight = hero.sight, grit = hero.grit, charm = hero.charm,
                    wit = hero.wit, grace = hero.grace, luck = hero.luck, weapon = hero.weapon, tool = hero.tool });
            }
            state.log.Add("The Heart fell and pulled its heroes back. A new run begins at Silverbrook Edge" +
                (state.legacyHeroes.Count > 0 ? " with " + state.legacyHeroes.Count + " heroes waiting to return." : "."));
            return state;
        }

        // Called when the new run's Tower opens: legacy heroes walk back in through the Gate.
        private void ReturnLegacyHeroes()
        {
            if (State.legacyHeroes == null || State.legacyHeroes.Count == 0) return;
            foreach (var hero in State.legacyHeroes)
            {
                var owned = State.residents.Find(r => r.origin == "hero" && r.unitId == hero.unitId);
                if (owned != null)
                {
                    owned.rank = Mathf.Max(owned.rank, hero.rank);
                    owned.level = Mathf.Max(owned.level, hero.level);
                    continue;
                }
                var back = AddResident(hero.unitId, hero.name, "hero", hero.level);
                back.rank = hero.rank; back.xp = hero.xp; back.trait = hero.trait;
                back.might = hero.might; back.sight = hero.sight; back.grit = hero.grit; back.charm = hero.charm;
                back.wit = hero.wit; back.grace = hero.grace; back.luck = hero.luck;
                back.weapon = hero.weapon; back.tool = hero.tool;
            }
            Note(State.legacyHeroes.Count + " heroes returned to the new Tower.");
            State.legacyHeroes.Clear();
        }

        // ---------------------------------------------------------------- summoning (8.1)

        public static readonly string[] SummonHeroIds = { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "amara" };
        public static readonly string[] SummonHeroNames = { "Kaela", "Ghislaine", "Elara", "Helda", "Daisy", "Clarity", "Amara" };
        // Percent chance of each rank F..SSR (draft rates from 8.1; the rates screen shows these).
        public static readonly float[] SummonRates = { 13.5f, 15f, 18f, 18f, 14f, 10f, 7f, 3f, 1.5f };
        public const int SoftPityFrom = 50, HardPity = 60, TenPullCost = 10;
        public const float HeroShare = 0.6f;

        public List<TowerSummon> LastSummon { get; private set; }

        public bool FreeSummonReady { get { return State.introPhase == "complete" && !State.freeSummonUsed; } }

        // Chance of SSR on the next pull, with soft pity ramping from pull 50 to a guarantee at 60.
        public float SsrChance()
        {
            int next = State.summonPity + 1;
            if (next >= HardPity) return 100f;
            return SummonRates[8] + Mathf.Max(0, next - SoftPityFrom + 1) * 6f;
        }

        public string Summon(int count)
        {
            if (State.introPhase != "complete") return "The Heart must be awake and the Tower founded first.";
            if (count != 1 && count != 10) return "Summon one or ten.";
            bool free = count == 1 && FreeSummonReady;
            int cost = free ? 0 : count == 10 ? TenPullCost : 1;
            if (State.sigils < cost) return "Summoning needs " + cost + " Sigil" + (cost > 1 ? "s" : "") + ".";
            State.sigils -= cost;
            LastSummon = new List<TowerSummon>();
            for (int i = 0; i < count; i++)
            {
                // The tutorial summon is a guaranteed hero of rank B or better.
                int rank = free ? Mathf.Max(5, RollRank()) : RollRank();
                bool hero = free || Random01() < HeroShare;
                LastSummon.Add(hero ? SummonHero(rank) : SummonResident(rank));
            }
            if (free) State.freeSummonUsed = true;
            Bump("summon");
            var best = LastSummon[0];
            foreach (var pull in LastSummon) if (pull.rank > best.rank) best = pull;
            Note((count == 10 ? "Ten summons" : "A summon") + " answered the Heart; best: " + best.name +
                " (" + TowerTiers.Tier(best.rank) + ").");
            Emit("summon", Room0("heart"), 0, TowerTiers.Tier(best.rank));
            return null;
        }

        private int RollRank()
        {
            float ssr = SsrChance();
            if (Random01() * 100f < ssr) { State.summonPity = 0; return 9; }
            State.summonPity++;
            // Below SSR, use the base ladder without the SSR slice.
            float total = 0;
            for (int i = 0; i < 8; i++) total += SummonRates[i];
            float roll = Random01() * total;
            for (int i = 0; i < 8; i++)
            {
                roll -= SummonRates[i];
                if (roll < 0) return i + 1;
            }
            return 8;
        }

        private TowerSummon SummonHero(int rank)
        {
            int pick = Mathf.Min(SummonHeroIds.Length - 1, (int)(Random01() * SummonHeroIds.Length));
            string unitId = SummonHeroIds[pick], name = SummonHeroNames[pick];
            var owned = State.residents.Find(r => r.origin == "hero" && r.unitId == unitId);
            if (owned != null)
            {
                // Duplicates fuse: a higher pull lifts the hero to that rank, otherwise the hero gains a level.
                if (rank > owned.rank) { RaiseStats(owned, rank - owned.rank); owned.rank = rank; }
                else owned.level++;
                return new TowerSummon { name = name, unitId = unitId, kind = "hero", rank = rank, fused = true };
            }
            var hero = AddResident(unitId, name, "hero", 1);
            hero.rank = rank;
            RaiseStats(hero, rank - 1);
            return new TowerSummon { name = name, unitId = unitId, kind = "hero", rank = rank };
        }

        private TowerSummon SummonResident(int rank)
        {
            string[] trades = { "Miner", "Builder", "Cook", "Scout", "Herald", "Scholar", "Trader" };
            string name = trades[(int)(Random01() * trades.Length) % trades.Length] + " " + State.nextResidentId;
            var resident = AddResident("", name, "villager", 1);
            resident.rank = rank;
            RaiseStats(resident, Mathf.CeilToInt(rank / 2f));
            return new TowerSummon { name = name, kind = "resident", rank = rank };
        }

        // Each step adds one point to a stat, cycling from a random start so ranks read in the stat block.
        private void RaiseStats(TowerResident resident, int steps)
        {
            string[] keys = { "might", "sight", "grit", "charm", "wit", "grace", "luck" };
            int start = (int)(Random01() * keys.Length);
            for (int i = 0; i < steps; i++)
                switch (keys[(start + i) % keys.Length])
                {
                    case "might": resident.might = Mathf.Min(10, resident.might + 1); break;
                    case "sight": resident.sight = Mathf.Min(10, resident.sight + 1); break;
                    case "grit": resident.grit = Mathf.Min(10, resident.grit + 1); break;
                    case "charm": resident.charm = Mathf.Min(10, resident.charm + 1); break;
                    case "wit": resident.wit = Mathf.Min(10, resident.wit + 1); break;
                    case "grace": resident.grace = Mathf.Min(10, resident.grace + 1); break;
                    default: resident.luck = Mathf.Min(10, resident.luck + 1); break;
                }
        }
    }
}
