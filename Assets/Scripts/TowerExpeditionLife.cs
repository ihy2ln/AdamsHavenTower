using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // What the guild has learned across every expedition: beasts fought, wild events met, regions walked.
    [Serializable] public sealed class TowerJournal
    {
        public List<string> beasts = new List<string>();     // BattleCatalog species ids
        public List<string> events = new List<string>();     // TowerEvents ids
        public List<string> regions = new List<string>();    // regions an expedition has set out for
    }

    // The numbers shown when a run ends, before the party is back in the Tower.
    public sealed class TowerRunSummary
    {
        public string region = "";
        public bool wiped, retreated;
        public int days, places, cleared, fights, relics, upgrades, fallen;
        public int gold, ore, essence, celestium, sigils, goldLeft;
    }

    // Expedition immersion (review step 7): the weather of each day, the run's clock, the end-of-run summary and the
    // guild journal.
    public sealed partial class TowerRules
    {
        // Weather changes the walk. Rules tests switch it off so ration and threat counts stay exact.
        public static bool Weathers = true;
        public const float RainThreat = 0.7f, RainRations = 1.2f;
        public const int FogSight = 2;

        // Each day of a run has one weather, drawn from the run seed and the day. The lakes are misty and wet,
        // the mountains foggy, the Heart sullen; the forest edge is mostly fair.
        public string Weather
        {
            get
            {
                var run = Run;
                if (!Weathers || run == null || run.mapKind != GridKind) return "clear";
                float roll = (TowerForestLayouts.Hash(run.region + ":weather:" + GameDay, run.seed) % 1000) / 1000f;
                float fog, rain;
                switch (run.biome)
                {
                    case "lakes": fog = 0.25f; rain = 0.3f; break;
                    case "mountains": fog = 0.35f; rain = 0.15f; break;
                    case "heart": fog = 0.25f; rain = 0.2f; break;
                    case "ruins": fog = 0.2f; rain = 0.2f; break;
                    default: fog = 0.15f; rain = 0.2f; break;
                }
                return roll < fog ? "fog" : roll < fog + rain ? "rain" : "clear";
            }
        }

        // Rain washes the party's tracks away (less threat) but soaks the packs (more rations); fog shortens sight.
        public float WeatherThreatScale { get { return Weather == "rain" ? RainThreat : 1f; } }
        public float WeatherRationScale { get { return Weather == "rain" ? RainRations : 1f; } }
        public int WeatherSightPenalty { get { return Weather == "fog" ? FogSight : 0; } }

        public static string WeatherName(string weather)
        { return weather == "rain" ? "Rain" : weather == "fog" ? "Fog" : "Clear skies"; }

        public static string WeatherEffect(string weather)
        {
            return weather == "rain" ? "Rain washes your tracks away (-30% threat) but soaks the packs (+20% rations)." :
                weather == "fog" ? "Fog: you see " + FogSight + " cells less." : "";
        }

        // 0 by day, 1 at night, easing through dusk and dawn.
        public static float NightLevel(float hour)
        {
            if (hour >= 7f && hour < 18f) return 0f;
            if (hour >= 18f && hour < 20f) return (hour - 18f) / 2f;
            if (hour >= 5f && hour < 7f) return 1f - (hour - 5f) / 2f;
            return 1f;
        }

        public static string PartOfDay(float hour)
        {
            if (hour >= 5f && hour < 7f) return "Dawn";
            if (hour < 12f && hour >= 7f) return "Morning";
            if (hour >= 12f && hour < 17f) return "Afternoon";
            if (hour >= 17f && hour < 20f) return "Dusk";
            return "Night";
        }

        // Days in the wild, counting the first as day 1 (run.clock is the time spent out there).
        public int DaysOut { get { var run = Run; return run == null ? 0 : 1 + Mathf.FloorToInt(run.clock / DaySeconds); } }

        // "Day 2  •  21:40  •  Night  •  Rain"
        public string ClockLine()
        {
            var run = Run;
            if (run == null) return "";
            float hour = RunHour();
            int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
            return "Day " + DaysOut + "  •  " + h.ToString("00") + ":" + m.ToString("00") + "  •  " +
                PartOfDay(hour) + "  •  " + WeatherName(Weather);
        }

        // ---------------------------------------------------------------- summary

        // Filled by EndExpedition for the end-of-run card; not saved.
        public TowerRunSummary LastSummary { get; private set; }

        private TowerRunSummary Summarize(TowerRun run, bool wiped, bool safe)
        {
            var s = new TowerRunSummary { region = Region(run.region) != null ? Region(run.region).name : run.region, wiped = wiped,
                retreated = !wiped && !safe, days = DaysOut, places = Mathf.Max(0, run.visited.Count - 1),
                cleared = run.cleared.Count, fights = run.battlesWon, relics = run.relics.Count, upgrades = run.cardLevels.Count };
            foreach (var hp in run.hp) if (hp <= 0) s.fallen++;
            return s;
        }

        private static void AddLoot(TowerRunSummary s, TowerLoot loot)
        { s.gold += loot.gold; s.ore += loot.ore; s.essence += loot.essence; s.celestium += loot.celestium; }

        // ---------------------------------------------------------------- journal

        private void NormalizeJournal()
        {
            if (State.journal == null) State.journal = new TowerJournal();
            if (State.journal.beasts == null) State.journal.beasts = new List<string>();
            if (State.journal.events == null) State.journal.events = new List<string>();
            if (State.journal.regions == null) State.journal.regions = new List<string>();
            // Saves from before the journal: the regions already conquered and the one under way count as walked.
            if (State.regionsConquered != null) foreach (var id in State.regionsConquered) RecordJournalRegion(id);
            if (State.hasRun && State.run != null && !string.IsNullOrEmpty(State.run.region)) RecordJournalRegion(State.run.region);
        }

        public TowerJournal Journal { get { NormalizeJournal(); return State.journal; } }

        public void RecordBeasts(IEnumerable<string> speciesIds)
        {
            var journal = Journal;
            foreach (var id in speciesIds) if (!string.IsNullOrEmpty(id) && !journal.beasts.Contains(id)) journal.beasts.Add(id);
        }

        private void RecordJournalEvent(string id) { var j = Journal; if (!j.events.Contains(id)) j.events.Add(id); }
        private void RecordJournalRegion(string id) { var j = State.journal; if (j != null && !j.regions.Contains(id)) j.regions.Add(id); }
    }
}
