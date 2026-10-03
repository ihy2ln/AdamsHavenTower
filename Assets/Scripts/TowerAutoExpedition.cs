using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // An auto expedition in progress (GDD 10.3): runs on the real clock like research, offline too.
    [Serializable] public sealed class TowerAutoRun
    {
        public string region = "";
        public List<int> party = new List<int>();
        public long startUnix, endsUnix;
        public float power, danger;
    }

    // TT 10.30.0. Postings take a resident out of the Tower without the played run's `away` flag (which the
    // expedition code rewrites on every load): `posting` names where they went and `exploring` keeps every Tower
    // job, bed check and party picker from using them. Auto expeditions are the first posting; outposts the second.
    public sealed partial class TowerRules
    {
        // Rescaled from the GDD's 70..800 to the simulation's 1..10 stats: three founding heroes (all stats 3, about 13
        // power each at level 1) nearly succeed at Brook Edge; three maxed heroes (about 360) face the deepest region.
        public static readonly int[] AutoDanger = { 45, 55, 65, 80, 95, 115, 135, 160, 180, 205, 230, 260, 290 };
        public static readonly float[] AutoHours = { 0.5f, 1f, 1.5f, 2f, 3f, 4f, 6f, 8f, 9f, 10f, 11f, 12f, 12f };
        public static readonly string[] AutoTiers = { "Fail", "Partial", "Success", "Great" };
        private static readonly float[] AutoTierReward = { 0.25f, 0.6f, 1f, 1.4f };
        private static readonly float[] AutoTierInjury = { 0.7f, 0.3f, 0.1f, 0f };
        public const int AutoFood = 6, AutoWater = 6, AutoSigilsPerReturn = 2, AutoSigilsPerDay = 6;

        // ---- Postings ----------------------------------------------------------------------------------------

        public static bool IsPosted(TowerResident resident) { return resident != null && !string.IsNullOrEmpty(resident.posting); }

        // Leaves the Tower: the job slot empties (GDD 6.4), the bed stays theirs.
        private void Post(TowerResident resident, string posting)
        {
            resident.posting = posting;
            resident.postedSeconds = 0;
            resident.exploring = true;
            resident.jobRoom = 0;
            resident.duty = "";
            resident.currentRoom = resident.targetRoom = 0;
            resident.travelSeconds = resident.travelDuration = 0;
            resident.currentTask = "away";
            resident.breakSeconds = 0;
        }

        private void RecallPosting(TowerResident resident)
        {
            if (!IsPosted(resident)) return;
            resident.posting = "";
            resident.postedSeconds = 0;
            resident.exploring = false;
            var heart = State.rooms.Find(r => r.type == "heart");
            resident.currentRoom = resident.homeRoom > 0 ? resident.homeRoom : heart == null ? 0 : heart.uid;
            resident.currentTask = "idle";
        }

        public static string PostingLabel(TowerResident resident)
        {
            if (!IsPosted(resident)) return "";
            if (resident.posting == "auto") return "on an auto expedition";
            var region = Region(resident.posting.StartsWith("outpost:") ? resident.posting.Substring(8) : "");
            return "stationed at the " + (region == null ? "outpost" : region.name) + " outpost";
        }

        // ---- Auto expeditions ----------------------------------------------------------------------------------

        public TowerAutoRun AutoRun { get { return State.hasAutoRun ? State.autoRun : null; } }

        private static int RegionIndex(string id) { return Array.FindIndex(Regions, r => r.id == id); }

        public static float AutoDangerOf(string region) { int i = RegionIndex(region); return i < 0 ? 0 : AutoDanger[i]; }
        public static float AutoHoursOf(string region) { int i = RegionIndex(region); return i < 0 ? 0 : AutoHours[i]; }

        public static float HeroPower(TowerResident hero)
        {
            if (hero == null) return 0;
            int stats = hero.might + hero.sight + hero.grit + hero.charm + hero.wit + hero.grace + hero.luck;
            return stats * (1f + 0.08f * Mathf.Max(1, hero.rank)) * (0.5f + 0.5f * Mathf.Clamp(hero.level, 1, LevelCap) / (float)LevelCap);
        }

        public float PartyPower(List<int> party)
        {
            float power = 0;
            bool tank = false, support = false, controller = false;
            foreach (int id in party)
            {
                var hero = Resident(id);
                power += HeroPower(hero);
                var unit = hero == null ? null : TowerRoster.Unit(hero.unitId);
                string role = unit == null ? "" : unit.role;
                tank |= role == "Tank"; support |= role == "Support"; controller |= role == "Controller";
            }
            return power * (1f + (tank && support ? 0.10f : 0f) + (controller ? 0.05f : 0f));
        }

        public static int AutoTier(float ratio) { return ratio < 0.6f ? 0 : ratio < 0.9f ? 1 : ratio < 1.3f ? 2 : 3; }

        public string AutoPrediction(string region, List<int> party)
        {
            float danger = AutoDangerOf(region);
            return danger <= 0 || party.Count == 0 ? "" : AutoTiers[AutoTier(PartyPower(party) / danger)];
        }

        public bool CanJoinAuto(TowerResident hero)
        {
            return hero != null && hero.origin == "hero" && hero.ageStage == 0 && !hero.downed && !hero.away &&
                !hero.exploring && hero.injury < 50;
        }

        public string CanSendAuto(string region, List<int> party)
        {
            string guild = GuildRequired();
            if (guild != null) return guild;
            if (State.hasAutoRun) return "An auto expedition is already out. One party at a time.";
            if (Region(region) == null) return "Choose a region.";
            if (!RegionUnlocked(region)) return RegionLockReason(region);
            if (party == null || party.Count == 0 || party.Count > 3) return "Send one to three heroes.";
            foreach (int id in party)
            {
                var hero = Resident(id);
                if (!CanJoinAuto(hero)) return (hero == null ? "That hero" : hero.name) + " cannot go: heroes must be well and at home.";
            }
            if (State.food < AutoFood * party.Count || State.water < AutoWater * party.Count)
                return "Provisions: " + AutoFood * party.Count + " food and " + AutoWater * party.Count + " water.";
            return null;
        }

        public string SendAuto(string region, List<int> party)
        {
            string error = CanSendAuto(region, party);
            if (error != null) return error;
            State.food -= AutoFood * party.Count;
            State.water -= AutoWater * party.Count;
            long now = NowUnix();
            State.autoRun = new TowerAutoRun { region = region, party = new List<int>(party), startUnix = now,
                endsUnix = now + Mathf.RoundToInt(AutoHoursOf(region) * 3600f), power = PartyPower(party),
                danger = AutoDangerOf(region) };
            State.hasAutoRun = true;
            foreach (int id in party) Post(Resident(id), "auto");
            Note("An auto expedition set out for " + Region(region).name + " (" + AutoTiers[AutoTier(State.autoRun.power /
                State.autoRun.danger)].ToLowerInvariant() + " expected).");
            Emit("auto_depart", 0, 0, Region(region).name);
            return null;
        }

        public float AutoProgress()
        {
            var run = AutoRun;
            if (run == null || run.endsUnix <= run.startUnix) return 0;
            return Mathf.Clamp01((NowUnix() - run.startUnix) / (float)(run.endsUnix - run.startUnix));
        }

        public long AutoSecondsLeft() { var run = AutoRun; return run == null ? 0 : Math.Max(0, run.endsUnix - NowUnix()); }

        // Called from Advance like research: a party whose time is up comes home, live or offline.
        private void TickAutoExpedition()
        {
            if (State.hasAutoRun && NowUnix() >= State.autoRun.endsUnix) ResolveAuto();
        }

        public string RecallAuto()
        {
            if (!State.hasAutoRun) return "No auto expedition is out.";
            foreach (int id in State.autoRun.party) RecallPosting(Resident(id));
            State.hasAutoRun = false;
            Note("The auto expedition was recalled before it found anything.");
            return null;
        }

        private void ResolveAuto()
        {
            var run = State.autoRun;
            var region = Region(run.region);
            State.hasAutoRun = false;
            int tier = AutoTier(run.danger <= 0 ? 1 : run.power / run.danger);
            float scale = AutoTierReward[tier] * (region == null ? 1f : region.reward);
            int gold = Mathf.RoundToInt(80 * scale * (Researched("EXP-2") ? 1.2f : 1f));
            int wood = Mathf.RoundToInt(6 * scale), stone = Mathf.RoundToInt(4 * scale);
            float firewood = 12 * scale;
            int tonics = ColonyRandom01() < 0.5f * AutoTierReward[tier] ? 1 : 0;
            int celestium = RegionIndex(run.region) >= 3 ? Mathf.RoundToInt((1 + (region == null ? 1 : region.reward)) * AutoTierReward[tier]) : 0;
            State.gold += gold; State.wood += wood; State.stone += stone; State.celestium += celestium;
            State.firewood = Mathf.Min(StockCap(), State.firewood + firewood);
            State.tonics = Mathf.Min(30, State.tonics + tonics);
            int sigils = 0;
            string today = Today().ToString("yyyy-MM-dd");
            if (State.autoSigilDate != today) { State.autoSigilDate = today; State.autoSigilsToday = 0; }
            if (tier > 0 && State.autoSigilsToday + AutoSigilsPerReturn <= AutoSigilsPerDay)
            {
                sigils = GrantSigils(AutoSigilsPerReturn);
                State.autoSigilsToday += AutoSigilsPerReturn;
            }
            int hurt = 0;
            foreach (int id in run.party)
            {
                var hero = Resident(id);
                if (hero == null) continue;
                RecallPosting(hero);
                GiveXp(hero, Mathf.RoundToInt(30 * AutoTierReward[tier]));
                if (ColonyRandom01() < AutoTierInjury[tier])
                {
                    hero.injury = Mathf.Min(100, hero.injury + 35);
                    hero.hp = Mathf.Max(1, hero.hp - 30);
                    hurt++;
                }
            }
            State.autoReport = AutoTiers[tier].ToUpperInvariant() + " in " + (region == null ? run.region : region.name) + ": " +
                gold + " gold, " + Mathf.RoundToInt(firewood) + " firewood, " + wood + " wood, " + stone + " stone" +
                (celestium > 0 ? ", " + celestium + " Celestium" : "") + (tonics > 0 ? ", a Tonic" : "") +
                (sigils > 0 ? ", " + sigils + " Sigils" : "") + (hurt > 0 ? ".  " + hurt + " came back injured." : ".");
            Note("The auto expedition returned. " + State.autoReport);
            Bump("expedition");
            Emit("expedition", 0, 0, AutoTiers[tier]);
        }
    }
}
