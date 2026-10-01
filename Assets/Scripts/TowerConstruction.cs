using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Rooms, wings and floors are not instant: each is a job that counts down in game seconds
    // (also while the Tower is closed, through CatchUp) and only then changes the layout.
    [Serializable] public sealed class TowerWork
    {
        public string kind;      // "room", "wing" or "floor"
        public int floor;
        public int x;            // room: first cell
        public int side;         // wing: -1 west, +1 east
        public string type;      // room: catalogue id
        public float remaining, total;

        public float Progress { get { return total <= 0 ? 1 : Mathf.Clamp01(1 - remaining / total); } }
    }

    public sealed partial class TowerRules
    {
        // Tests and validation runs build instantly; the game never sets this.
        public static bool InstantConstruction;

        private bool Timed { get { return !InstantConstruction && State.introPhase == "complete"; } }

        // The first lessons are shortened so the opening does not stall.
        private float EarlyScale { get { return State.tutorialStep < 7 ? 0.5f : 1f; } }

        public float RoomBuildSeconds(string type)
        {
            var def = TowerCatalog.Get(type);
            return def == null ? 0 : (15 + 10 * def.width + def.cost / 20f) * EarlyScale;
        }

        public float WingBuildSeconds(int number, int side)
        { return (20 + 3 * WingCellsOf(number, side)) * EarlyScale; }

        public float FloorBuildSeconds(int number) { return (45 + 3 * Math.Abs(number)) * EarlyScale; }

        public TowerWork WorkRoomAt(int floor, int x)
        {
            foreach (var work in State.works)
            {
                if (work.kind != "room" || work.floor != floor) continue;
                var def = TowerCatalog.Get(work.type);
                if (def != null && x >= work.x && x < work.x + TowerTiers.Bays(work.type, 1)) return work;
            }
            return null;
        }

        public TowerWork WingWork(int floor, int side)
        {
            side = side < 0 ? -1 : 1;
            return State.works.Find(w => w.kind == "wing" && w.floor == floor && w.side == side);
        }

        public TowerWork FloorWork(int number)
        { return State.works.Find(w => w.kind == "floor" && w.floor == number); }

        public static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        private void StartWork(string kind, int floor, int x, int side, string type, float seconds)
        {
            State.works.Add(new TowerWork { kind = kind, floor = floor, x = x, side = side,
                type = type, remaining = seconds, total = seconds });
        }

        private void TickConstruction(float dt)
        {
            for (int i = State.works.Count - 1; i >= 0; i--)
            {
                var work = State.works[i];
                work.remaining -= dt;
                if (work.remaining > 0) continue;
                State.works.RemoveAt(i);
                Complete(work);
            }
        }

        private void Complete(TowerWork work)
        {
            if (work.kind == "room")
            {
                var built = AddRoom(work.type, work.floor, work.x);
                if (built == null) return;
                Bump("build");
                Emit("build", built.uid, 0, work.type);
                if (State.introPhase == "complete" && State.tutorialStep == 0 && work.type == "kitchen")
                    State.tutorialStep = 1;
                Note("Finished building " + TowerCatalog.Get(work.type).displayName + " on floor " + work.floor + ".");
            }
            else if (work.kind == "wing")
            {
                var floor = Floor(work.floor);
                if (floor == null) return;
                if (work.side < 0) floor.west++; else floor.east++;
                Note("Finished the " + (work.side < 0 ? "west" : "east") + " foundation of floor " + work.floor + ".");
            }
            else if (work.kind == "floor")
            {
                if (Floor(work.floor) != null) return;
                State.floors.Add(new TowerFloor { number = work.floor });
                Note("Floor " + work.floor + " is open.");
            }
        }
    }
}
