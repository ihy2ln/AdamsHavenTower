using UnityEngine;

namespace AdamsHaven.Tower
{
    // The Silverbrook Adventure Guild is where expeditions are chosen. Its level unlocks deeper battle destinations.
    public sealed class TowerExpeditionTier
    {
        public readonly string name, blurb;
        public readonly int depth, reward, minLevel;
        public TowerExpeditionTier(string name, string blurb, int depth, int reward, int minLevel)
        { this.name = name; this.blurb = blurb; this.depth = depth; this.reward = reward; this.minLevel = minLevel; }
    }

    public sealed partial class TowerRules
    {
        // Rewards mirror BattleMode: depth >= 8 pays 300, >= 3 pays 160, otherwise 80.
        public static readonly TowerExpeditionTier[] ExpeditionTiers =
        {
            new TowerExpeditionTier("OUTSKIRTS", "Forest edge skirmish", 1, 80, 1),
            new TowerExpeditionTier("DEEP WOODS", "Elite patrols", 4, 160, 2),
            new TowerExpeditionTier("BOSS LAIR", "Heartwood throne", 8, 300, 3),
        };

        public TowerRoom GuildHall()
        {
            TowerRoom best = null;
            foreach (var room in State.rooms)
                if (room.type == "guild_hall" && (best == null || room.level > best.level)) best = room;
            return best;
        }

        public int GuildLevel { get { var hall = GuildHall(); return hall == null ? 0 : hall.level; } }

        // Null when the guild can send residents out or open the expedition board.
        public string GuildRequired()
        {
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (GuildHall() == null) return "Build the Silverbrook Adventure Guild to send expeditions.";
            return null;
        }

        public string CanLaunchBattle(int tier)
        {
            string error = GuildRequired();
            if (error != null) return error;
            if (tier < 0 || tier >= ExpeditionTiers.Length) return "Unknown expedition.";
            if (GuildLevel < ExpeditionTiers[tier].minLevel)
                return "Upgrade the Guild to level " + ExpeditionTiers[tier].minLevel + ".";
            return null;
        }
    }
}
