using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    [Serializable] public sealed class RosterStats { public int might, sight, grit, charm, wit, grace, luck; }

    // One hero or resident from the character prompt pack (CharacterPrompts/), exported to
    // Resources/AdamsHaven/Roster/tower_roster.json by CharacterPrompts/_source/export_game_roster.py.
    [Serializable] public sealed class RosterUnit
    {
        public string code, id, kind, name, epithet, race, gender, size, body, rankName;
        public string element, cls, role, primary, secondary, room;
        public string personality, quote, origin;
        public string weapon, weaponDesc, passive, ability1, ability2, ability3, ultimate;
        public string perk, tool, toolDesc;
        public int rank, age, height, chest, waist, hips, weight, levelCap;
        public string[] palette;
        public RosterStats stats;

        public bool IsHero { get { return kind == "hero"; } }
    }

    [Serializable] internal sealed class RosterFile { public RosterUnit[] units; }

    // Data side of the roster: summon pools by rank, names, element/class/role, abilities and portrait paths.
    // Everything degrades gracefully: without the JSON the summon code falls back to the original seven heroes.
    public static class TowerRoster
    {
        private const string File = "AdamsHaven/Roster/tower_roster";
        private const string ArtRoot = "AdamsHaven/Roster/Art/";

        private static List<RosterUnit> units;
        private static Dictionary<string, RosterUnit> byId;

        public static bool Available { get { Load(); return units.Count > 0; } }
        public static IList<RosterUnit> All { get { Load(); return units; } }

        private static void Load()
        {
            if (units != null) return;
            units = new List<RosterUnit>();
            byId = new Dictionary<string, RosterUnit>();
            var text = Resources.Load<TextAsset>(File);
            if (text == null) return;
            var file = JsonUtility.FromJson<RosterFile>(text.text);
            if (file == null || file.units == null) return;
            foreach (var unit in file.units)
                if (unit != null && !string.IsNullOrEmpty(unit.id) && unit.stats != null && !byId.ContainsKey(unit.id))
                { units.Add(unit); byId[unit.id] = unit; }
        }

        public static RosterUnit Unit(string id)
        {
            Load();
            RosterUnit unit;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out unit) ? unit : null;
        }

        // Heroes or residents of one summon rank (1 = F .. 9 = SSR).
        public static List<RosterUnit> OfRank(int rank, bool heroes)
        {
            Load();
            return units.FindAll(u => u.rank == rank && u.IsHero == heroes);
        }

        // Roster stats run on the GDD 6.7 band scale (2..70). The simulation uses 1..10 per stat, so the band is mapped
        // linearly onto 3..10 (the villager baseline is 3). The full values stay available on the unit for later use.
        public static int GameStat(int rosterStat)
        {
            return Mathf.Clamp(Mathf.RoundToInt(3f + (rosterStat - 2f) / 68f * 7f), 1, 10);
        }

        public static void ApplyStats(TowerResident resident, RosterUnit unit)
        {
            if (resident == null || unit == null) return;
            resident.might = GameStat(unit.stats.might); resident.sight = GameStat(unit.stats.sight);
            resident.grit = GameStat(unit.stats.grit); resident.charm = GameStat(unit.stats.charm);
            resident.wit = GameStat(unit.stats.wit); resident.grace = GameStat(unit.stats.grace);
            resident.luck = GameStat(unit.stats.luck);
        }

        // Short label for panels: "B · Fire Striker" for heroes, "A · Merchant" style key stat for residents.
        public static string Tag(TowerResident resident)
        {
            var unit = resident == null ? null : Unit(resident.unitId);
            if (unit == null) return "";
            return unit.IsHero ? "  [" + unit.rankName + " " + unit.element + " " + unit.role + "]" :
                "  [" + unit.rankName + " " + unit.epithet.Replace("the ", "") + "]";
        }

        // Where a portrait for this unit goes (Resources path, no extension), named as in the prompt pack.
        public static string ArtPath(RosterUnit unit, string name) { return ArtRoot + unit.id + "/" + name; }
        public static Texture2D Portrait(string unitId, string name)
        {
            var unit = Unit(unitId);
            return unit == null ? null : Resources.Load<Texture2D>(ArtPath(unit, name));
        }
    }
}
