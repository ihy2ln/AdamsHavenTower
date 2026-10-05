using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Two Gates, one at each end of the ground floor, with the Heart shaft as close to the middle as the wings allow
    // (TOWER_MODE_GDD 4, revised 2026-10-01). A floor's west/east counts only founded building cells; on the ground
    // floor each Gate stands one cell beyond them and steps outward whenever its side is extended.
    public sealed partial class TowerRules
    {
        public const int LayoutVersion = 1;

        public List<TowerRoom> Gates() { return State.rooms.FindAll(r => r.type == "gate"); }
        public bool HasGate() { return State.rooms.Exists(r => r.type == "gate"); }

        public TowerRoom GateOn(int side)
        { return State.rooms.Find(r => r.type == "gate" && r.floor == 0 && (side < 0 ? r.x < CoreX : r.x > CoreX)); }

        // Cells a floor spans on screen, Gates included (the founded cells plus a Gate cell at each ground-floor end).
        // A foundation under construction on the ground floor already pushes its Gate one cell out (TT 10.4.1).
        public int DrawnWest(TowerFloor floor) { return floor.west + (floor.number == 0 && GateOn(-1) != null ? 1 + PendingWing(-1) : 0); }
        public int DrawnEast(TowerFloor floor) { return floor.east + (floor.number == 0 && GateOn(1) != null ? 1 + PendingWing(1) : 0); }
        private int PendingWing(int side) { return WingWork(0, side) != null ? 1 : 0; }

        // Puts each Gate just outside its side's founded cells, creating the west Gate once the Tower is founded.
        public void PlaceGates()
        {
            var ground = Floor(0);
            if (ground == null) return;
            // Player-built Gates (owner, 2026-10-05): in play the player builds each Gate; existing ones still step out.
            PlaceGate(ground, 1, !PlayerGates);
            PlaceGate(ground, -1, State.introPhase == "complete" && !PlayerGates);
        }

        // ---- Player-built Gates (TT 10.5.0b): the Gate is the end of the ground floor, placed from BUILD ----------

        // Play only, like one world: EditMode suites keep the automatic Gates they were written for.
        public static bool PlayerGates { get { return OneWorld; } }

        // The cell a Gate takes at that end: one beyond the founded cells (and any wing under construction).
        public int GateEndX(int side)
        {
            var ground = Floor(0);
            if (ground == null) return side < 0 ? CoreX - 1 : CoreX + 1;
            return side < 0 ? CoreX - ground.west - 1 - PendingWing(-1) : CoreX + ground.east + 1 + PendingWing(1);
        }

        // The first Gate is free (the founding); the second costs gold.
        public int GateCost() { return HasGate() ? 80 : 0; }

        // A Gate is offered in BUILD while an end of the ground floor has none.
        public bool GateBuildable()
        {
            return (State.introPhase == "gate" || State.introPhase == "complete") && Floor(0) != null &&
                (GateOn(-1) == null || GateOn(1) == null);
        }

        // Tap anywhere on a side of the ground floor, from its last founded cell outward: the Gate takes that end.
        public string BuildGate(int floor, int x)
        {
            if (State.introPhase == "dormant") return "Awaken the Heart first.";
            if (floor != 0 || Floor(0) == null) return "A Gate stands at an end of the ground floor.";
            if (x == CoreX) return "Tap an end of the ground floor, left or right of the Heart.";
            int side = x < CoreX ? -1 : 1;
            int end = GateEndX(side);
            if (GateOn(side) != null) return "That end already has its Gate.";
            if (side < 0 ? x > end + 1 : x < end - 1) return "Tap the very end of the ground floor: the Gate closes it.";
            if (RoomAt(0, end) != null || WorkRoomAt(0, end) != null) return "Something already stands at that end.";
            int cost = GateCost();
            if (State.gold < cost) return "A second Gate costs " + cost + " gold.";
            State.gold -= cost;
            var gate = AddRoom("gate", 0, end);
            gate.flip = false;
            State.layoutVersion = LayoutVersion;
            TouchLayout();
            Emit("build", gate.uid, 0, "gate");
            if (State.introPhase == "gate")
            {
                State.introPhase = "shack";
                Note("The Celestium Gate closes the " + (side < 0 ? "west" : "east") + " end of the ground floor.");
            }
            else Note("A second Gate closes the " + (side < 0 ? "west" : "east") + " end of the ground floor.");
            return null;
        }

        private void PlaceGate(TowerFloor ground, int side, bool create)
        {
            var gate = GateOn(side);
            if (gate == null && !create) return;
            int x = side < 0 ? CoreX - ground.west - 1 - PendingWing(-1) : CoreX + ground.east + 1 + PendingWing(1);
            var there = RoomAt(0, x);
            if (there != null && there != gate) return;   // a room already stands there: leave the Gate where it is
            if (gate == null) gate = AddRoom("gate", 0, x);
            else gate.x = x;
            gate.flip = false;
        }

        // Older saves: the Gate's own cell counted in the ground floor's east wing, and there was no west Gate.
        private void MigrateLayout()
        {
            if (State.layoutVersion >= LayoutVersion) return;
            var ground = Floor(0);
            var east = GateOn(1);
            if (ground != null && east != null && east.x == CoreX + ground.east && ground.east > 0) ground.east--;
            foreach (var floor in State.floors)
                if (floor.number != 0 && !State.rooms.Exists(r => r.floor == floor.number && r.x > CoreX)) floor.east = 0;
            State.layoutVersion = LayoutVersion;
            if (State.introPhase == "complete") PlaceGates();
        }

        // Guards stand at both Gates.
        public int GuardCount()
        {
            int count = 0;
            foreach (var r in State.residents)
            {
                if (r.downed || r.exploring || r.away || r.jobRoom == 0) continue;
                var job = Room(r.jobRoom);
                if (job != null && job.type == "gate") count++;
            }
            return count;
        }

        public int GuardSlots()
        {
            int slots = 0;
            foreach (var gate in Gates()) slots += Capacity(gate);
            return slots;
        }
    }
}
