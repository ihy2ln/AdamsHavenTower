using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Dungeon Mode save slots (AdamsHavenDungeon/): 1 the player's own dungeon, 2-4 pinned test checkpoints,
    // anything else a dormant dungeon. TowerMilestones.Create and EnsureSlots hand over to this in Dungeon Mode.
    public static class LairMilestones
    {
        public const int Slots = 4;
        public static readonly string[] Labels = { "Dormant Dungeon", "Summit Dungeon", "Depths Dungeon", "Elite Incoming" };

        public static string Caption(int slot)
        {
            return slot >= 1 && slot <= Slots ? "SLOT " + slot + "  /  " + Labels[slot - 1] : "SLOT " + slot + "  /  Dormant Dungeon";
        }

        public static TowerState Create(int slot)
        {
            var rules = TowerRules.NewLair(slot);
            var state = rules.State;
            state.generator = TowerMilestones.Version;
            if (slot < 2 || slot > Slots) return state;
            state.pinned = true;
            state.label = Labels[slot - 1];
            rules.AwakenHeart();
            rules.LairFound(slot != 3);
            rules.ChooseStarter("kaela");
            state.sigils = 200;
            state.food = state.water = state.firewood = 120;
            if (slot == 4)
            {
                state.heartRank = 4;
                state.heartHp = TowerRules.HeartMaxHp(4);
                state.gold = 20000; state.celestium = 400; state.sigils = 500;
                state.wood = state.stone = state.ore = state.essence = 500;
                rules.DevLairMonsters(8);
                var lair = state.rooms.Find(r => r.type == "monster_lair");
                if (lair != null) foreach (var m in state.lair.monsters) if (rules.LairCapacity(lair) > rules.MonstersIn(lair.uid).Count) m.lairRoom = lair.uid;
                state.lair.notoriety = 80;
                state.threat = 80;
                state.siegeCooldown = 0;
                state.eventCooldown = 20;
                state.lair.fame = 30;
            }
            state.log.Clear();
            rules.Note(state.label + ": a Dungeon Mode checkpoint.");
            state.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return state;
        }

        public static void EnsureSlots()
        {
            for (int slot = 1; slot <= Slots; slot++)
                if (TowerSaveFiles.Load(slot) == null) TowerSaveFiles.Save(Create(slot));
        }
    }
}
