using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One task on today's daily board (TowerRules.DailyPool), measured from the counter value when the board was dealt.
    [Serializable] public sealed class TowerDaily
    {
        public string id;
        public int baseline;
        public bool claimed;
    }

    public sealed class TowerDailyDef
    {
        public readonly string id, title, counter;   // counter "" = done as soon as the board is dealt
        public readonly int target;
        public TowerDailyDef(string id, string title, string counter, int target)
        { this.id = id; this.title = title; this.counter = counter; this.target = target; }
    }

    // Sigil income (TOWER_MODE_GDD 8.1): about 30 free Sigils per real day, from four sources.
    //   goals        2 to 4 each (TowerGoalDef.sigils), about 8 a day
    //   daily board  4 tasks x 3 plus a 4 bonus = 16 a day, dealt each local calendar day
    //   expeditions  a successful return pays 2 + 1.5 x region reward (first 2 returns a day), +10 per new conquest
    //   Heart        a lump sum on every rank-up, 250 in all, about 3 a day over the 80-day climb
    public sealed partial class TowerRules
    {
        // The calendar the daily board follows. Tests swap it for a fixed date.
        public static Func<DateTime> Today = () => DateTime.Now.Date;

        public const int DailyTaskSigils = 3, DailyBonusSigils = 4, DailyPicked = 3;
        public const int ExpeditionSigilReturns = 2, ConquestSigils = 10;

        // Sigils granted on reaching each Heart rank, E (index 0) to SSR.
        private static readonly int[] HeartRankSigils = { 10, 15, 20, 25, 30, 40, 50, 60 };
        public static int HeartRankUpSigils(int newRank)
        { return newRank < 2 || newRank > TowerTiers.MaxRank ? 0 : HeartRankSigils[newRank - 2]; }

        // "Visit the Tower" is always on the board; three more are picked from the rest by the date.
        public static readonly TowerDailyDef DailyCheckIn = new TowerDailyDef("checkin", "Visit the Tower", "", 1);
        public static readonly TowerDailyDef[] DailyPool = {
            new TowerDailyDef("harvest", "Collect 8 harvests", "collect", 8),
            new TowerDailyDef("bigharvest", "Collect 16 harvests", "collect", 16),
            new TowerDailyDef("rush", "Rush a room successfully", "rush_ok", 1),
            new TowerDailyDef("build", "Start a new room", "build", 1),
            new TowerDailyDef("levelup", "Level up a resident", "level_up", 1),
            new TowerDailyDef("expedition", "Return from an expedition", "expedition", 1),
        };

        public static TowerDailyDef DailyDef(string id)
        {
            if (id == DailyCheckIn.id) return DailyCheckIn;
            foreach (var def in DailyPool) if (def.id == id) return def;
            return null;
        }

        public static string DateKey(DateTime date) { return date.ToString("yyyy-MM-dd"); }

        // Deals a fresh board when the calendar day changed. Unclaimed tasks from an earlier day are lost.
        public void RefreshDaily()
        {
            if (State.daily == null) State.daily = new List<TowerDaily>();
            if (State.introPhase != "complete") return;
            DateTime today = Today();
            string key = DateKey(today);
            if (State.dailyDate == key && State.daily.Count > 0) return;
            State.dailyDate = key;
            State.dailyBonusClaimed = false;
            State.dailyExpeditionSigils = 0;
            State.daily.Clear();
            State.daily.Add(new TowerDaily { id = DailyCheckIn.id });
            var pool = new List<TowerDailyDef>();
            foreach (var def in DailyPool)
            {
                if (def.id == "expedition" && GuildRequired() != null) continue;   // no Guild yet, no expeditions
                pool.Add(def);
            }
            // The same date always deals the same board; harvest and big harvest never share one.
            int seed = today.Year * 372 + today.Month * 31 + today.Day;
            while (State.daily.Count < 1 + DailyPicked && pool.Count > 0)
            {
                seed = seed * 1103515245 + 12345;
                var def = pool[(int)((uint)seed >> 8) % pool.Count];
                pool.Remove(def);
                if (def.counter == "collect") pool.RemoveAll(d => d.counter == "collect");
                State.daily.Add(new TowerDaily { id = def.id, baseline = Counter(def.counter) });
            }
        }

        public int DailyProgress(TowerDaily task)
        {
            var def = DailyDef(task.id);
            if (def == null) return 0;
            if (def.counter.Length == 0) return def.target;
            return Mathf.Min(def.target, Counter(def.counter) - task.baseline);
        }

        public bool DailyComplete(TowerDaily task)
        {
            var def = DailyDef(task.id);
            return def != null && DailyProgress(task) >= def.target;
        }

        public int DailyClaimable()
        {
            int n = 0;
            if (State.introPhase == "complete" && State.daily != null)
                foreach (var task in State.daily) if (!task.claimed && DailyComplete(task)) n++;
            return n;
        }

        public string ClaimDaily(int index)
        {
            RefreshDaily();
            if (index < 0 || index >= State.daily.Count) return "No such task.";
            var task = State.daily[index];
            if (task.claimed) return "Already claimed.";
            if (!DailyComplete(task)) return "That task is not finished.";
            task.claimed = true;
            int sigils = DailyTaskSigils;
            bool all = State.daily.TrueForAll(t => t.claimed);
            if (all && !State.dailyBonusClaimed) { State.dailyBonusClaimed = true; sigils += DailyBonusSigils; }
            State.sigils += sigils;
            Note("Daily task done: " + DailyDef(task.id).title + ". +" + sigils + " Sigils" +
                (all ? " (the whole board is clear)." : "."));
            Emit("reward", 0, 0, sigils + " Sigils");
            return null;
        }

        // ---------------------------------------------------------------- expeditions

        // A return that won at least one fight pays Sigils, for the first two returns of the day.
        public int ExpeditionReturnSigils(TowerRun run)
        {
            var region = Region(run.region);
            if (region == null || run.battlesWon <= 0 && run.cleared.Count == 0) return 0;
            RefreshDaily();
            if (State.dailyExpeditionSigils >= ExpeditionSigilReturns) return 0;
            return Mathf.RoundToInt(2 + 1.5f * region.reward);
        }

        private int PayExpeditionSigils(TowerRun run)
        {
            int sigils = ExpeditionReturnSigils(run);
            if (sigils <= 0) return 0;
            State.dailyExpeditionSigils++;
            State.sigils += sigils;
            return sigils;
        }
    }
}
