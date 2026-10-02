using UnityEngine;

namespace AdamsHaven.Tower
{
    // The Silverbrook Adventure Guild is where expeditions are chosen; it must stand before any party sets out.
    public sealed partial class TowerRules
    {
        public TowerRoom GuildHall()
        {
            TowerRoom best = null;
            foreach (var room in State.rooms)
                if (room.type == "guild_hall" && (best == null || room.level > best.level)) best = room;
            return best;
        }

        public int GuildLevel { get { var hall = GuildHall(); return hall == null ? 0 : hall.level; } }

        // Null when the guild can send residents out or open the expedition board.
        // Set by the top-right test button so expeditions open without a Guild Hall.
        public static bool DebugIgnoreGuild;

        public string GuildRequired()
        {
            if (DebugIgnoreGuild) return null;
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (GuildHall() == null) return "Build the Silverbrook Adventure Guild to send expeditions.";
            return null;
        }
    }
}
