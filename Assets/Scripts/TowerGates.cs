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
        public int DrawnWest(TowerFloor floor) { return floor.west + (floor.number == 0 && GateOn(-1) != null ? 1 : 0); }
        public int DrawnEast(TowerFloor floor) { return floor.east + (floor.number == 0 && GateOn(1) != null ? 1 : 0); }

        // Puts each Gate just outside its side's founded cells, creating the west Gate once the Tower is founded.
        public void PlaceGates()
        {
            var ground = Floor(0);
            if (ground == null) return;
            PlaceGate(ground, 1, true);
            PlaceGate(ground, -1, State.introPhase == "complete");
        }

        private void PlaceGate(TowerFloor ground, int side, bool create)
        {
            var gate = GateOn(side);
            if (gate == null && !create) return;
            int x = side < 0 ? CoreX - ground.west - 1 : CoreX + ground.east + 1;
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
