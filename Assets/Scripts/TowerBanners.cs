using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One summon banner of the Heart hub (TOWER_MODE_GDD 8.1).
    public sealed class TowerBannerDef
    {
        public readonly string id, name, pool;   // pool: "mixed" (heroes and residents), "heroes" or "residents"
        public readonly int pullCost, tenCost;
        public TowerBannerDef(string id, string name, string pool, int pullCost, int tenCost)
        { this.id = id; this.name = name; this.pool = pool; this.pullCost = pullCost; this.tenCost = tenCost; }
    }

    // Summon banners (GDD 8.1, TT 10.3.1). Standard is the original Summon(count) and keeps its 60/40 hero/resident mix.
    // Featured and Pick-Your-Hero draw heroes only and share Standard's SSR pity (summonPity); half of their SS/SSR
    // pulls are the featured or chosen hero, and after a miss the next one at that rank is guaranteed. The Resident
    // banner draws the 24 residents with SSR at 1% and its own A-or-better guarantee every 20 pulls.
    public sealed partial class TowerRules
    {
        public static readonly TowerBannerDef[] Banners = {
            new TowerBannerDef("standard", "Standard", "mixed", PullCost, TenPullCost),
            new TowerBannerDef("featured", "Featured", "heroes", 10, 100),
            new TowerBannerDef("pick", "Pick-Your-Hero", "heroes", 20, 200),
            new TowerBannerDef("resident", "Resident", "residents", 5, 50),
        };

        public const float FeaturedShare = 0.5f;     // share of SS/SSR pulls that are the featured or chosen hero
        public const float ResidentSsrRate = 1f;     // Resident banner SSR chance, percent, no pity ramp
        public const int ResidentPityAt = 20;        // a Resident pull this many since the last A+ is A or better
        public const int ResidentFloorRank = 6;      // A

        public static TowerBannerDef Banner(string id)
        {
            foreach (var def in Banners) if (def.id == id) return def;
            return null;
        }

        public TowerBannerDef CurrentBanner { get { return Banner(State.summonBanner) ?? Banners[0]; } }

        public void SetSummonBanner(string id) { if (Banner(id) != null) State.summonBanner = id; }

        public static int BannerCost(TowerBannerDef def, int count) { return count == 10 ? def.tenCost : def.pullCost; }

        // ---------------------------------------------------------------- Featured rotation (weekly, local calendar)

        private static readonly DateTime WeekEpoch = new DateTime(2024, 1, 1);   // a Monday

        // Weeks since the epoch; a new week starts each Monday by the local calendar (TowerRules.Today, as the daily board).
        public static int WeekIndex(DateTime date)
        { return (int)Math.Floor((date.Date - WeekEpoch).TotalDays / 7.0); }

        // The first day of the next rotation (the Monday that ends this week's Featured pair).
        public static DateTime FeaturedEnds() { return WeekEpoch.AddDays(7.0 * (WeekIndex(Today()) + 1)); }

        // This week's featured hero of `rank` (9 SSR or 8 SS); null without roster data. The SS hero steps through its
        // pool at a different stride, so the pair does not repeat in lockstep.
        public static RosterUnit FeaturedHero(int rank)
        {
            var pool = TowerRoster.OfRank(rank, true);
            if (pool.Count == 0) return null;
            int week = WeekIndex(Today());
            int step = rank == 9 ? week : week * 3 + 1;
            return pool[((step % pool.Count) + pool.Count) % pool.Count];
        }

        // ---------------------------------------------------------------- Pick-Your-Hero target

        // Every hero the Pick banner can aim at: the SSR heroes, then the SS heroes.
        public static List<RosterUnit> PickTargets()
        {
            var list = new List<RosterUnit>(TowerRoster.OfRank(9, true));
            list.AddRange(TowerRoster.OfRank(8, true));
            return list;
        }

        public RosterUnit PickTarget()
        {
            var unit = TowerRoster.Unit(State.pickTarget);
            if (unit != null && unit.IsHero && unit.rank >= 8) return unit;
            var all = PickTargets();
            return all.Count > 0 ? all[0] : null;
        }

        // The target may change at any time; a pending guarantee (pickMissed) carries over to the new one.
        public string SetPickTarget(string unitId)
        {
            var unit = TowerRoster.Unit(unitId);
            if (unit == null || !unit.IsHero || unit.rank < 8) return "Pick an SS or SSR hero.";
            State.pickTarget = unit.id;
            return null;
        }

        public void CyclePickTarget(int step)
        {
            var all = PickTargets();
            if (all.Count == 0) return;
            var current = PickTarget();
            int index = Mathf.Max(0, all.FindIndex(u => current != null && u.id == current.id));
            State.pickTarget = all[((index + step) % all.Count + all.Count) % all.Count].id;
        }

        // Pulls left until the Resident banner's A-or-better guarantee (1 = the next pull).
        public int ResidentPullsToGuarantee() { return Mathf.Max(1, ResidentPityAt - State.residentPity); }

        // Every load: older saves have none of these fields.
        private void NormalizeBanners()
        {
            if (Banner(State.summonBanner) == null) State.summonBanner = "standard";
            if (State.pickTarget == null) State.pickTarget = "";
            if (State.pickTarget.Length > 0)
            {
                var unit = TowerRoster.Unit(State.pickTarget);
                if (unit == null || !unit.IsHero || unit.rank < 8) State.pickTarget = "";
            }
            if (State.pickTarget.Length == 0) { var first = PickTarget(); if (first != null) State.pickTarget = first.id; }
            State.residentPity = Mathf.Clamp(State.residentPity, 0, ResidentPityAt - 1);
        }

        // ---------------------------------------------------------------- pulling

        // Summons on a banner. Standard is exactly Summon(count), the free first summon included; the other banners
        // always cost Sigils.
        public string Summon(int count, string bannerId)
        {
            var def = Banner(bannerId);
            if (def == null) return "Unknown banner.";
            if (def.id == "standard") return Summon(count);
            if (State.introPhase != "complete") return "The Heart must be awake and the Tower founded first.";
            if (count != 1 && count != 10) return "Summon one or ten.";
            int cost = BannerCost(def, count);
            if (State.sigils < cost) return "The " + def.name + " banner needs " + cost + " Sigils.";
            State.sigils -= cost;
            LastSummon = new List<TowerSummon>();
            bool residents = def.pool == "residents";
            var ranks = new int[count];
            int best = 0;
            for (int i = 0; i < count; i++) { ranks[i] = residents ? RollResidentRank() : RollRank(); best = Mathf.Max(best, ranks[i]); }
            if (count == 10 && best < TenPullFloor) ranks[count - 1] = TenPullFloor;
            for (int i = 0; i < count; i++)
                LastSummon.Add(residents ? SummonResident(ranks[i]) : SummonBannerHero(def, ranks[i]));
            Bump("summon");
            var top = LastSummon[0];
            foreach (var pull in LastSummon) if (pull.rank > top.rank) top = pull;
            Note((count == 10 ? "Ten summons" : "A summon") + " on the " + def.name + " banner answered the Heart; best: " +
                top.name + " (" + TowerTiers.Tier(top.rank) + ").");
            Emit("summon", Room0("heart"), 0, TowerTiers.Tier(top.rank));
            return null;
        }

        // Featured and Pick: an SS/SSR pull at the target's rank is the target half the time, or always after a miss.
        // Pick aims at one hero, so only pulls of that hero's rank roll for it.
        private TowerSummon SummonBannerHero(TowerBannerDef def, int rank)
        {
            var pool = TowerRoster.OfRank(rank, true);
            if (pool.Count == 0) return SummonHero(rank);
            bool featured = def.id == "featured";
            RosterUnit target = null;
            if (rank >= 8)
            {
                target = featured ? FeaturedHero(rank) : PickTarget();
                if (target != null && target.rank != rank) target = null;
            }
            RosterUnit unit;
            if (target == null) unit = pool[PickIndex(pool.Count)];
            else
            {
                bool guaranteed = featured ? State.featuredMissed : State.pickMissed;
                bool hit = guaranteed || Random01() < FeaturedShare;
                if (!hit)
                {
                    var others = pool.FindAll(u => u.id != target.id);
                    hit = others.Count == 0;
                    unit = hit ? target : others[PickIndex(others.Count)];
                }
                else unit = target;
                if (featured) State.featuredMissed = !hit; else State.pickMissed = !hit;
            }
            return GrantHero(unit, unit.id, unit.name, rank);
        }

        // Resident ladder: SSR fixed at 1% with no ramp; the other ranks keep the Standard weights, scaled to the
        // remaining 99%. The 20th pull since the last A+ is A or better (A, S, SS, SSR by their weights).
        private int RollResidentRank()
        {
            int rank;
            if (State.residentPity + 1 >= ResidentPityAt)
            {
                float total = SummonRates[5] + SummonRates[6] + SummonRates[7] + ResidentSsrRate;
                float roll = Random01() * total;
                rank = 9;
                for (int i = 5; i < 8; i++) { roll -= SummonRates[i]; if (roll < 0) { rank = i + 1; break; } }
            }
            else
            {
                float roll = Random01() * 100f;
                if (roll < ResidentSsrRate) rank = 9;
                else
                {
                    float below = 0;
                    for (int i = 0; i < 8; i++) below += SummonRates[i];
                    roll = (roll - ResidentSsrRate) / (100f - ResidentSsrRate) * below;
                    rank = 8;
                    for (int i = 0; i < 8; i++) { roll -= SummonRates[i]; if (roll < 0) { rank = i + 1; break; } }
                }
            }
            State.residentPity = rank >= ResidentFloorRank ? 0 : State.residentPity + 1;
            return rank;
        }

        // Per-rank percent chances shown on a banner's rates screen (index 0 = F).
        public static float[] BannerRates(TowerBannerDef def)
        {
            var rates = (float[])SummonRates.Clone();
            if (def != null && def.pool == "residents")
            {
                float below = 0;
                for (int i = 0; i < 8; i++) below += SummonRates[i];
                for (int i = 0; i < 8; i++) rates[i] = SummonRates[i] / below * (100f - ResidentSsrRate);
                rates[8] = ResidentSsrRate;
            }
            return rates;
        }
    }
}
