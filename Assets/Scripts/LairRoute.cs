using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // How adventurers walk to the Heart (GDD 20.5). A cell graph of its own: the raiders' room graph (RaidNeighbours)
    // links every pair of shaft-side rooms on neighbouring floors and would let a party ride the shaft past every trap.
    // In a dungeon the shaft is sealed below the living floors; stairs alternate ends, so a party zig-zags across each
    // dungeon floor from end to end. A breadth-first distance field from the Heart cell steers every step.
    public sealed partial class TowerRules
    {
        private Dictionary<long, int> routeField;
        private string routeSignature;

        private static long CellKey(int floor, int x) { return ((long)(floor + 64) << 16) | (uint)(x + 64); }

        // Walkable cells of a floor: both wings, the shaft cell and (on the entrance) the Gates.
        public int RouteWest(int floor) { var f = Floor(floor); return f == null ? CoreX : CoreX - DrawnWest(f); }
        public int RouteEast(int floor) { var f = Floor(floor); return f == null ? CoreX : CoreX + DrawnEast(f); }

        private bool Walkable(int floor, int x) { return Floor(floor) != null && x >= RouteWest(floor) && x <= RouteEast(floor); }

        // The stair from `floor` to the next floor toward the Heart: the shaft between living floors, otherwise the east
        // end on even floors and the west end on odd ones, at the outermost cell both floors have founded.
        public int StairX(int floor)
        {
            int next = floor + LairDir;
            string a = FloorKind(floor), b = FloorKind(next);
            bool shaft = (a == "living" || a == "heart") && (b == "living" || b == "heart");
            var f = Floor(floor); var g = Floor(next);
            if (shaft || f == null || g == null) return CoreX;
            bool east = Mathf.Abs(floor) % 2 == 0;
            int reach = east ? Mathf.Min(f.east, g.east) : Mathf.Min(f.west, g.west);
            return reach <= 0 ? CoreX : east ? CoreX + reach : CoreX - reach;
        }

        private string RouteSignature()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(LayoutStamp).Append('|').Append(State.lair.heartFloor).Append('|').Append(State.lair.livingFloors);
            foreach (var f in State.floors) sb.Append('|').Append(f.number).Append(',').Append(DrawnWest(f)).Append(',').Append(DrawnEast(f));
            return sb.ToString();
        }

        private Dictionary<long, int> RouteField()
        {
            string signature = RouteSignature();
            if (routeField != null && signature == routeSignature) return routeField;
            routeSignature = signature;
            routeField = new Dictionary<long, int>();
            if (!LairFounded) return routeField;
            var queue = new Queue<Vector2Int>();
            var start = new Vector2Int(HeartFloor, CoreX);
            routeField[CellKey(start.x, start.y)] = 0;
            queue.Enqueue(start);
            var next = new List<Vector2Int>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                int d = routeField[CellKey(cell.x, cell.y)];
                RouteNeighbours(cell.x, cell.y, next);
                foreach (var n in next)
                {
                    long key = CellKey(n.x, n.y);
                    if (routeField.ContainsKey(key)) continue;
                    routeField[key] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return routeField;
        }

        private void RouteNeighbours(int floor, int x, List<Vector2Int> into)
        {
            into.Clear();
            if (Walkable(floor, x - 1)) into.Add(new Vector2Int(floor, x - 1));
            if (Walkable(floor, x + 1)) into.Add(new Vector2Int(floor, x + 1));
            int dir = LairDir;
            // Up toward the Heart from this floor, and back down from the floor before it.
            if (floor != HeartFloor && StairX(floor) == x && Walkable(floor + dir, x)) into.Add(new Vector2Int(floor + dir, x));
            int previous = floor - dir;
            if (floor != 0 && Floor(previous) != null && StairX(previous) == x && Walkable(previous, x))
                into.Add(new Vector2Int(previous, x));
        }

        // Dungeon floors and the entrance have no shaft landing: only the living floors and the Heart share it.
        public bool ShaftSealed(int floor)
        {
            if (!LairFounded) return false;
            string kind = FloorKind(floor);
            return kind != "living" && kind != "heart";
        }

        // Steps left from this cell to the Heart; -1 when no path exists.
        public int RouteDistance(int floor, int x)
        {
            int d;
            return RouteField().TryGetValue(CellKey(floor, x), out d) ? d : -1;
        }

        // The next cell toward the Heart (the cell itself when it is the Heart or cut off).
        public Vector2Int RouteNext(int floor, int x)
        {
            int d = RouteDistance(floor, x);
            if (d <= 0) return new Vector2Int(floor, x);
            var options = new List<Vector2Int>();
            RouteNeighbours(floor, x, options);
            foreach (var n in options) if (RouteDistance(n.x, n.y) == d - 1) return n;
            return new Vector2Int(floor, x);
        }

        // The Gate farthest from the Heart, so a party crosses the whole entrance hall.
        public Vector2Int RouteEntry()
        {
            var gates = Gates();
            Vector2Int best = new Vector2Int(0, CoreX);
            int far = -1;
            foreach (var gate in gates)
            {
                int d = RouteDistance(0, gate.x);
                if (d > far) { far = d; best = new Vector2Int(0, gate.x); }
            }
            if (far < 0)
            {
                int west = RouteDistance(0, RouteWest(0)), east = RouteDistance(0, RouteEast(0));
                best = new Vector2Int(0, west >= east ? RouteWest(0) : RouteEast(0));
            }
            return best;
        }

        // The whole path from the entry to the Heart (tests and the view use it).
        public List<Vector2Int> RoutePath()
        {
            var path = new List<Vector2Int>();
            var cell = RouteEntry();
            path.Add(cell);
            for (int guard = 0; guard < 2000; guard++)
            {
                var next = RouteNext(cell.x, cell.y);
                if (next == cell) break;
                path.Add(next);
                cell = next;
            }
            return path;
        }
    }
}
