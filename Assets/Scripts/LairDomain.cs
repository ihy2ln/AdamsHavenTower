using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Dungeon Mode state (TOWER_MODE_GDD 20). A Tower save carries an empty one; schema stays 2.
    [Serializable] public sealed class TowerLairState
    {
        public int heartFloor;              // signed: +N the Heart crowns the summit, -N it sleeps in the depths, 0 dormant
        public int livingFloors = 1;        // floors next to the Heart where the residents live
        public float notoriety;             // 0..100: how hated the dungeon is; high draws the elite team
        public float fame = 10;             // 0..100: how tempting it is; high draws more and bigger parties
        public float nextPartySeconds = 240;
        public int nextPartyId = 1, nextMonsterId = 1;
        public int random;                  // the dungeon's own xorshift stream, so Tower sequences never shift
        public int monsterPity;             // Monster banner pulls since the last A or better
        public float sigilProgress;         // kills pay Sigils in fractions
        public int offlineArrivals;         // parties that arrived during one catch-up
        public List<LairMonster> monsters = new List<LairMonster>();
        public List<LairParty> parties = new List<LairParty>();
        public List<LairReport> reports = new List<LairReport>();   // newest first, at most LairBalance.Reports
        public List<LairNamed> named = new List<LairNamed>();       // adventurers with a name to remember (slice 2)
        public List<LairVault> vaults = new List<LairVault>();      // gold sitting in each Bait Vault
        public int kills, captures, escapes, breaches, wipes, relocations;
    }

    [Serializable] public sealed class LairMonster
    {
        public int id;
        public string formId = "", family = "", name = "", element = "";
        public int rank = 1, level = 1, xp;
        public float hp = 1;                // fraction of full health
        public float woundSeconds;          // out of action while > 0
        public int lairRoom;                // the Monster Lair it lives in; 0 = it guards the Heart
        public int kills;
    }

    [Serializable] public sealed class LairAdventurer
    {
        public string name = "", cls = "", element = "", status = "ok";   // ok, dead, captured
        public int rank = 1, level = 1;
        public float power, hp, maxHp;
        public string heroId = "";          // a roster hero leading an elite team
        public bool named;
        public bool Alive { get { return status == "ok"; } }
    }

    [Serializable] public sealed class LairParty
    {
        public int id;
        public string name = "", kind = "normal";   // normal or elite
        public List<LairAdventurer> members = new List<LairAdventurer>();
        public int floor, x, fromFloor, fromX;      // where it stands, and where it came from (for the view)
        public float stepSeconds, stepTotal, moveSeconds;
        public List<int> visited = new List<int>(); // rooms whose encounter already fired
        public int loot, kills, captured, ransom, goldLost;
        public float heartDamage, notorietyDelta, fameDelta;
        public bool sated;
        public List<string> lines = new List<string>();
    }

    [Serializable] public sealed class LairReport
    {
        public int day;
        public float clock;
        public string partyName = "", kind = "normal", outcome = "";   // wiped, fled or breached
        public int size, avgRank, kills, captured, goldGained, goldLost;
        public float heartDamage, notorietyDelta, fameDelta;
        public List<string> lines = new List<string>();
    }

    [Serializable] public sealed class LairNamed
    {
        public string unitId = "", name = "", lastOutcome = "";
        public int rank, visits, kills;
        public float grudge;
    }

    [Serializable] public sealed class LairVault
    {
        public int room;
        public float gold;
        public float carry;                 // refill owed from the treasury, paid in whole coins
    }

    // Dungeon rooms. Kept out of TowerCatalog.All (build menus, checkpoints and tests enumerate it);
    // TowerCatalog.Get falls back here, so labels and panels still find them.
    public static class LairCatalog
    {
        public sealed class Spec
        {
            public readonly string id, role, art;   // role: trap, snare, lair, lure; art: the Tower room it borrows
            public readonly bool singleBay;
            public Spec(string id, string role, string art, bool singleBay)
            { this.id = id; this.role = role; this.art = art; this.singleBay = singleBay; }
        }

        public static readonly TowerRoomDef[] All = {
            new TowerRoomDef("spike_hall", "Spike Trap Hall", "lair", 3, 120, "grit"),
            new TowerRoomDef("snare_pit", "Snare Pit", "lair", 1, 90, "grit"),
            new TowerRoomDef("monster_lair", "Monster Lair", "lair", 3, 150, "grit"),
            new TowerRoomDef("bait_vault", "Bait Vault", "lair", 1, 100, "grit"),
        };

        private static readonly Spec[] Specs = {
            new Spec("spike_hall", "trap", "forge", false),
            new Spec("snare_pit", "snare", "warehouse", true),
            new Spec("monster_lair", "lair", "barn", false),
            new Spec("bait_vault", "lure", "market", true),
        };

        // Placeholder art is tinted blood-red until the dungeon gets its own pictures.
        public static readonly Color Tint = new Color(0.95f, 0.55f, 0.50f);

        public static TowerRoomDef Get(string id)
        {
            foreach (var def in All) if (def.id == id) return def;
            return null;
        }

        public static Spec SpecOf(string id)
        {
            foreach (var spec in Specs) if (spec.id == id) return spec;
            return null;
        }

        public static bool Is(string type) { return SpecOf(type) != null; }
        public static bool SingleBay(string type) { var spec = SpecOf(type); return spec != null && spec.singleBay; }
        public static string Role(string type) { var spec = SpecOf(type); return spec == null ? "" : spec.role; }
        // The Tower room whose art a dungeon room borrows; any other type is returned unchanged.
        public static string ArtType(string type) { var spec = SpecOf(type); return spec == null ? type : spec.art; }
    }

    // Every Dungeon Mode number in one place (GDD 20.4).
    public static class LairBalance
    {
        public const int Reports = 20;
        public const float FirstParty = 240f;
        public const float NotorietyDecayPerMinute = 1f, FameDriftPerMinute = 0.2f, FameRest = 10f;
        public const float EliteNotoriety = 75f;   // TowerRules.SiegeThreat: notoriety mirrors threat
        public const int OfflineArrivalCap = 8;
        public const float OfflineHeartFloor = 0.3f;

        // Movement (seconds)
        public const float CellSeconds = 2f, StairSeconds = 3.5f, EncounterSeconds = 4f, VaultSeconds = 6f;

        // Monsters
        public static readonly int[] MonsterCaps = { 4, 6, 8, 11, 14, 18, 22, 26, 30 };
        public const int MonsterPullCost = 5, MonsterTenCost = 50, MonsterPityAt = 20, MonsterFloorRank = 6;
        public const int DuplicateLevels = 2, MonsterLevelCap = 50, XpPerWin = 10, XpPerLevel = 40;
        public const float MonsterFood = 0.004f, MonsterRegen = 0.004f, HungryPower = 0.7f;
        public const float WoundAt = 0.05f, WoundSeconds = 300f, GuardPower = 0.5f;

        // Dungeon floors
        public static readonly int[] DigCaps = { 2, 3, 4, 6, 8, 10, 13, 16, 20 };
        public static readonly int[] LivingCaps = { 1, 1, 2, 2, 2, 3, 3, 3, 3 };
        public const int DigBase = 6, DigPerFloor = 3, LivingBase = 10, LivingPerFloor = 8;

        // Arrivals
        public const float ArrivalBase = 300f, ArrivalMin = 90f, ArrivalMax = 480f;

        // Encounters
        public const float SpikeChance = 0.6f, SpikeChancePerLevel = 0.04f, RogueTrapPenalty = 0.10f;
        public const float SpikeDamage = 14f, SpikeDamagePerLevel = 0.35f, SpikeWear = 6f, TrapDisabledBelow = 20f;
        public const float DisarmChance = 0.3f, DisarmWear = 25f;
        public const float SnareChance = 0.35f, SnareChancePerLevel = 0.05f;
        public const int RansomPerRank = 30;
        public const float RansomPerLevel = 0.15f;
        public const int VaultPerLevel = 80;
        public const float VaultRefill = 0.5f, VaultReserve = 100f;
        public static readonly float[] PartyLoss = { 0.10f, 0.25f, 0.50f, 0.85f };     // by AutoTier of defenders / party
        public static readonly float[] DefenderLoss = { 0.70f, 0.45f, 0.20f, 0.05f };
        public const float BreachPerRank = 25f, EliteBreach = 2.5f;
        public const float FleeHp = 0.35f, SatedFleeHp = 0.6f;
        public const float ClericHeal = 0.15f, ClericPower = 1.1f;
    }
}
