using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    public sealed class TowerDungeonRoom
    {
        public int x, y, w, h;
        public string kind = "empty";
        public bool goal;
        public int CenterX { get { return x + w / 2; } }
        public int CenterY { get { return y + h / 2; } }
    }

    // A 20x20 dungeon grid, D&D style. The layout is fixed by (map, POI, floor);
    // what waits in each room is rolled from the run's own seed.
    public sealed class TowerDungeon
    {
        public const int Size = 20;
        public const int Rock = 0, Floor = 1, Corridor = 2, Door = 3;
        public readonly int[] cell = new int[Size * Size];
        public readonly int[] room = new int[Size * Size];
        public readonly List<TowerDungeonRoom> rooms = new List<TowerDungeonRoom>();
        public string theme, poiKind;
        public int floor, floors;

        public static int Index(int x, int y) { return y * Size + x; }
        public static bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < Size && y < Size; }
        public int CellAt(int x, int y) { return Inside(x, y) ? cell[Index(x, y)] : Rock; }
        public int RoomAt(int x, int y) { return Inside(x, y) ? room[Index(x, y)] : -1; }
        public bool Walkable(int x, int y) { return CellAt(x, y) != Rock; }

        public static string GoalKind(string poiKind)
        {
            switch (poiKind)
            {
                case "combat": return "enemy";
                case "elite": case "landmark": return "elite";
                case "lair": return "boss";
                case "treasure": return "treasure";
                case "merchant": return "merchant";
                case "shrine": return "shrine";
                default: return "unknown";      // mystery
            }
        }

        // Version 1 keeps the plus-shaped layouts of older saves; version 2 draws a shape per visit from the run seed;
        // version 3 (BM 10.3.0) lets the "at least one fight" rule use the first room too, so small vaults always hold
        // a fight (about a fifth of them had none). A floor keeps the version it was entered with.
        // (Version 0, the scattered-rooms generator, is gone: NormalizeExpeditions walks such a save out of its dungeon.)
        public const int LayoutVersion = 3;
        public static TowerDungeon Build(string layoutId, TowerForestNode node, int floor, int runSeed, int version = 1)
        {
            return BuildBranches(layoutId, node, floor, runSeed, version >= 2, version >= 3 ? 1 : 2);
        }

        // Room layouts on the 3x3 sector grid, entrance first. Version 2 dungeons pick one per visit from the run's seed
        // (version 1 kept the plus shape and a seed without the run, so a place looked the same every expedition).
        private static readonly int[][] Shapes =
        {
            new[] { 3, 4, 1, 5, 7 },                 // plus: a junction with three arms (corners added as detours)
            new[] { 6, 3, 0, 1, 4, 7, 8, 5, 2 },     // serpent: one long winding way
            new[] { 3, 0, 1, 2, 5, 8, 7, 6 },        // ring: a loop around a solid core
            new[] { 3, 4, 1, 2, 7, 8, 5 },           // fork: two branches that meet again at the far side
        };

        private static TowerDungeon BuildBranches(string layoutId, TowerForestNode node, int floor, int runSeed, bool shaped = false, int firstFight = 2)
        {
            var d = new TowerDungeon { theme = node.theme, poiKind = node.kind, floor = floor,
                floors = Mathf.Max(1, TowerForestLayouts.Floors(node.kind)) };
            for (int i = 0; i < d.room.Length; i++) d.room[i] = -1;
            var rng = new TowerRng(TowerForestLayouts.Hash(layoutId + ":" + node.id + ":branches", floor * 131 + 7 + (shaped ? runSeed : 0)));
            List<int> slots;
            int shape = 0;
            if (shaped && (node.kind == "merchant" || node.kind == "shrine")) slots = new List<int> { 3, 4 };   // walk straight in
            else if (shaped && node.kind == "treasure") slots = new List<int> { 3, 4, 5, rng.Next(2) == 0 ? 1 : 7 };
            else
            {
                shape = shaped ? rng.Next(Shapes.Length) : 0;
                slots = new List<int>(Shapes[shape]);
            }
            if (shape == 0 && slots.Count == 5)
            {
                // A central junction with three guaranteed arms; optional corner chambers are detours.
                var corners = new List<int> { 0, 2, 6, 8 };
                int target = 6 + rng.Next(3);
                while (slots.Count < target) { int pick = rng.Next(corners.Count); slots.Add(corners[pick]); corners.RemoveAt(pick); }
            }
            foreach (int slot in slots)
            {
                var r = new TowerDungeonRoom { x = 1 + slot % 3 * 6, y = 1 + slot / 3 * 6,
                    w = 3 + rng.Next(2), h = 3 + rng.Next(2) };
                for (int y = r.y; y < r.y + r.h; y++)
                    for (int x = r.x; x < r.x + r.w; x++) { d.cell[Index(x, y)] = Floor; d.room[Index(x, y)] = d.rooms.Count; }
                d.rooms.Add(r);
            }
            // Connect only adjacent sectors, preserving readable physical forks and leaf rooms.
            var connections = new int[slots.Count];
            for (int i = 1; i < slots.Count; i++)
            {
                var candidates = new List<int>();
                for (int j = 0; j < i; j++)
                    if (Math.Abs(slots[i] % 3 - slots[j] % 3) + Math.Abs(slots[i] / 3 - slots[j] / 3) == 1) candidates.Add(j);
                // Serpent and ring follow their order exactly; the others pick any earlier neighbour.
                int parent = shape == 1 || shape == 2 ? (candidates.Contains(i - 1) ? i - 1 : candidates[0]) : candidates[rng.Next(candidates.Count)];
                connections[parent]++; connections[i]++;
                var a = d.rooms[parent]; var b = d.rooms[i];
                d.Carve(a.CenterX, a.CenterY, b.CenterX, b.CenterY, slots[parent] / 3 == slots[i] / 3);
            }
            if (shape == 2 && slots.Count > 2)
            {
                // Close the ring: the last room joins the entrance again.
                var a = d.rooms[slots.Count - 1]; var b = d.rooms[0];
                d.Carve(a.CenterX, a.CenterY, b.CenterX, b.CenterY, false);
                connections[slots.Count - 1]++; connections[0]++;
            }
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (d.CellAt(x, y) == Corridor && (d.CellAt(x + 1, y) == Floor || d.CellAt(x - 1, y) == Floor ||
                    d.CellAt(x, y + 1) == Floor || d.CellAt(x, y - 1) == Floor)) d.cell[Index(x, y)] = Door;
            d.AssignRooms(new TowerRng(TowerForestLayouts.Hash(layoutId + ":" + node.id, floor * 7919 + runSeed)), shaped);
            if (d.rooms.Count <= 2) return d;   // merchant / shrine: entrance and the goal only
            // At least one non-goal branch rewards exploration.
            for (int i = d.rooms.Count - 1; i > 1; i--)
                if (connections[i] == 1 && !d.rooms[i].goal && d.rooms[i].kind != "stairs" && d.rooms[i].kind != "elite")
                { d.rooms[i].kind = "treasure"; break; }
            if (!d.rooms.Exists(r => r.kind == "enemy" || r.kind == "elite" || r.kind == "boss"))
                for (int i = firstFight; i < d.rooms.Count; i++)
                    if (!d.rooms[i].goal && d.rooms[i].kind != "stairs" && d.rooms[i].kind != "treasure") { d.rooms[i].kind = "enemy"; break; }
            // Version 3: when every other room is treasure, the one nearest the door becomes its guard.
            if (firstFight <= 1 && !d.rooms.Exists(r => r.kind == "enemy" || r.kind == "elite" || r.kind == "boss"))
                for (int i = 1; i < d.rooms.Count; i++)
                    if (!d.rooms[i].goal && d.rooms[i].kind != "stairs") { d.rooms[i].kind = "enemy"; break; }
            return d;
        }

        private void Carve(int x0, int y0, int x1, int y1, bool horizontalFirst)
        {
            int x = x0, y = y0;
            while (x != x1 || y != y1)
            {
                if (horizontalFirst ? x != x1 : y == y1) x += Math.Sign(x1 - x); else y += Math.Sign(y1 - y);
                if (cell[Index(x, y)] == Rock) cell[Index(x, y)] = Corridor;
            }
        }

        // Rooms linked by a passage (corridors may cross a third room: then each half links its own pair), and the
        // passage cells leading out of each room. Built once per dungeon.
        private List<int>[] roomLinks, roomPassages;

        public List<int> Links(int r) { BuildRoomGraph(); return roomLinks[r]; }
        public List<int> Passages(int r) { BuildRoomGraph(); return roomPassages[r]; }

        private void BuildRoomGraph()
        {
            if (roomLinks != null) return;
            roomLinks = new List<int>[rooms.Count];
            roomPassages = new List<int>[rooms.Count];
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            var seen = new bool[Size * Size];
            var queue = new Queue<int>();
            for (int r = 0; r < rooms.Count; r++)
            {
                roomLinks[r] = new List<int>(); roomPassages[r] = new List<int>();
                Array.Clear(seen, 0, seen.Length);
                var room = rooms[r];
                for (int y = room.y; y < room.y + room.h; y++)
                    for (int x = room.x; x < room.x + room.w; x++)
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + dx[k], ny = y + dy[k];
                            if (!Walkable(nx, ny)) continue;
                            int q = RoomAt(nx, ny);
                            if (q >= 0) { if (q != r && !roomLinks[r].Contains(q)) roomLinks[r].Add(q); continue; }
                            int i = Index(nx, ny);
                            if (!seen[i]) { seen[i] = true; queue.Enqueue(i); }
                        }
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), x = i % Size, y = i / Size;
                    roomPassages[r].Add(i);
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + dx[k], ny = y + dy[k];
                        if (!Walkable(nx, ny)) continue;
                        int q = RoomAt(nx, ny);
                        if (q >= 0) { if (q != r && !roomLinks[r].Contains(q)) roomLinks[r].Add(q); continue; }
                        int n = Index(nx, ny);
                        if (!seen[n]) { seen[n] = true; queue.Enqueue(n); }
                    }
                }
            }
        }

        public int[] Distances(int sx, int sy)
        {
            var dist = new int[Size * Size];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            dist[Index(sx, sy)] = 0; queue.Enqueue(Index(sx, sy));
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % Size, y = i / Size;
                foreach (var n in new[] { new Vector2Int(x + 1, y), new Vector2Int(x - 1, y), new Vector2Int(x, y + 1), new Vector2Int(x, y - 1) })
                {
                    if (!Walkable(n.x, n.y) || dist[Index(n.x, n.y)] >= 0) continue;
                    dist[Index(n.x, n.y)] = dist[i] + 1; queue.Enqueue(Index(n.x, n.y));
                }
            }
            return dist;
        }

        // Version 2 dungeons also hold traps, skill checks, lore and a sealed door (at most one).
        private void AssignRooms(TowerRng rng, bool rich = false)
        {
            if (rooms.Count == 0) return;
            rooms[0].kind = "entrance";
            var dist = Distances(rooms[0].CenterX, rooms[0].CenterY);
            int goal = rooms.Count - 1, far = -1;
            for (int i = 1; i < rooms.Count; i++)
            {
                int dd = dist[Index(rooms[i].CenterX, rooms[i].CenterY)];
                if (dd > far) { far = dd; goal = i; }
            }
            bool last = floor >= floors - 1;
            rooms[goal].kind = last ? GoalKind(poiKind) : "stairs";
            rooms[goal].goal = last;
            bool needElite = !last && (poiKind == "elite" || poiKind == "lair");
            bool anyEnemy = false;
            for (int i = 1; i < rooms.Count; i++)
            {
                if (i == goal) continue;
                float roll = rng.Value();
                string kind = !rich ? (roll < 0.42f ? "enemy" : roll < 0.6f ? "treasure" : roll < 0.72f ? "rest" : roll < 0.88f ? "unknown" : "empty")
                    : roll < 0.36f ? "enemy" : roll < 0.48f ? "treasure" : roll < 0.57f ? "rest" : roll < 0.65f ? "unknown" :
                      roll < 0.74f ? "trap" : roll < 0.83f ? "skillcheck" : roll < 0.91f ? "story" : roll < 0.95f ? "keygate" : "empty";
                if (kind == "keygate" && rooms.Exists(r => r.kind == "keygate")) kind = "treasure";
                if (needElite && kind == "enemy") { kind = "elite"; needElite = false; }
                if (kind == "enemy") anyEnemy = true;
                rooms[i].kind = kind;
            }
            if (!anyEnemy && rooms.Count > 2)
                for (int i = 1; i < rooms.Count; i++) if (i != goal && rooms[i].kind != "elite") { rooms[i].kind = "enemy"; break; }
        }
    }

    public sealed partial class TowerRules
    {
        private TowerDungeon dungeonCache;
        private TowerRun dkRun;
        private string dkLayout, dkPoi;
        private int dkFloor, dkSeed, dkVersion, dkTablets, dkShift;

        // The dungeon the party is in (cached; compared field by field, since the board asks for it per cell).
        // On the grid its place's mods may turn quiet rooms into treasure or mystery rooms (TowerAtlasNodes).
        public TowerDungeon Dungeon
        {
            get
            {
                var run = Run;
                if (run == null || run.dungeonPoi.Length == 0) return null;
                int tablets = run.tablets == null ? 0 : run.tablets.Count;
                if (dungeonCache != null && dkRun == run && dkPoi == run.dungeonPoi && dkLayout == run.layout && dkFloor == run.floor &&
                    dkSeed == run.seed && dkVersion == run.dungeonLayoutVersion && dkTablets == tablets && dkShift == run.shift)
                    return dungeonCache;
                var layout = RunLayout;
                var node = layout == null ? null : layout.Node(run.dungeonPoi);
                if (node == null) return null;
                dungeonCache = TowerDungeon.Build(run.layout, node, run.floor, run.seed, run.dungeonLayoutVersion);
                if (GridRun) ApplyRoomMods(dungeonCache, NodeTotals(run.dungeonPoi));
                dkRun = run; dkPoi = run.dungeonPoi; dkLayout = run.layout; dkFloor = run.floor; dkSeed = run.seed;
                dkVersion = run.dungeonLayoutVersion; dkTablets = tablets; dkShift = run.shift;
                return dungeonCache;
            }
        }

        public bool Revealed(int x, int y)
        {
            var run = Run;
            return run != null && TowerDungeon.Inside(x, y) && run.fog.Length == TowerDungeon.Size * TowerDungeon.Size &&
                run.fog[TowerDungeon.Index(x, y)] != '0';
        }

        // Lit cells are what the party sees right now; revealed-but-unlit cells are drawn dimmed.
        public bool InSight(int x, int y)
        {
            var run = Run; var d = Dungeon;
            if (d == null) return false;
            int here = d.RoomAt(run.px, run.py);
            if (here >= 0)
            {
                var r = d.rooms[here];
                if (x >= r.x - 1 && x <= r.x + r.w && y >= r.y - 1 && y <= r.y + r.h) return true;
            }
            return Math.Max(Math.Abs(x - run.px), Math.Abs(y - run.py)) <= 2;
        }

        // Fog by room: standing in a room shows the room, every passage out of it, and the rooms those passages reach
        // (their contents included), so the party always knows what waits next door. Between rooms only the cells
        // right around the party are lifted (old saves, or a party stopped in a passage).
        private void RevealAround()
        {
            var run = Run; var d = Dungeon;
            if (d == null) return;
            int n = TowerDungeon.Size * TowerDungeon.Size;
            if (run.fog.Length != n) run.fog = new string('0', n);
            var fog = run.fog.ToCharArray();
            int here = d.RoomAt(run.px, run.py);
            if (here >= 0)
            {
                MarkRoom(fog, d.rooms[here]);
                foreach (int i in d.Passages(here))
                    for (int y = i / TowerDungeon.Size - 1; y <= i / TowerDungeon.Size + 1; y++)
                        for (int x = i % TowerDungeon.Size - 1; x <= i % TowerDungeon.Size + 1; x++)
                            if (TowerDungeon.Inside(x, y)) fog[TowerDungeon.Index(x, y)] = '1';
                foreach (int q in d.Links(here)) MarkRoom(fog, d.rooms[q]);
            }
            else
                for (int y = run.py - 1; y <= run.py + 1; y++)
                    for (int x = run.px - 1; x <= run.px + 1; x++)
                        if (TowerDungeon.Inside(x, y)) fog[TowerDungeon.Index(x, y)] = '1';
            var next = new string(fog);
            if (next != run.fog) run.fog = next;
        }

        private static void MarkRoom(char[] fog, TowerDungeonRoom room)
        {
            for (int y = room.y - 1; y <= room.y + room.h; y++)
                for (int x = room.x - 1; x <= room.x + room.w; x++)
                    if (TowerDungeon.Inside(x, y)) fog[TowerDungeon.Index(x, y)] = '1';
        }

        // How bright each cell is drawn (0..1): 0 unknown, 1 in sight, 0.8 a known room with something left in it,
        // 0.6 a known passage, 0.4 a finished room. Fills `into` (Size * Size) when given.
        public float[] DungeonLight(float[] into = null)
        {
            int n = TowerDungeon.Size * TowerDungeon.Size;
            var light = into != null && into.Length == n ? into : new float[n];
            var run = Run; var d = Dungeon;
            if (d == null) { Array.Clear(light, 0, n); return light; }
            int here = d.RoomAt(run.px, run.py);
            var lit = here >= 0 ? d.rooms[here] : null;
            bool fogged = run.fog.Length != n;
            for (int i = 0; i < n; i++)
            {
                int x = i % TowerDungeon.Size, y = i / TowerDungeon.Size;
                if (fogged || run.fog[i] == '0') { light[i] = 0; continue; }
                bool sight = (lit != null && x >= lit.x - 1 && x <= lit.x + lit.w && y >= lit.y - 1 && y <= lit.y + lit.h) ||
                    Math.Max(Math.Abs(x - run.px), Math.Abs(y - run.py)) <= 2;
                if (sight) { light[i] = 1; continue; }
                int r = d.RoomAt(x, y);
                light[i] = r < 0 ? 0.6f : run.roomsDone.Contains(r) ? 0.4f : 0.8f;
            }
            return light;
        }

        public string EnterPoi()
        {
            var run = Run;
            if (run == null) return "No expedition.";
            if (run.dungeonPoi.Length > 0) return "Already inside.";
            if (EventBlock() != null) return EventBlock();
            var node = RunLayout.Node(run.at);
            if (node == null || node.kind == "camp") return "Nothing to explore here. Rest or move on.";
            if (node.kind == "tower") return "The tower has no depths: its view is the prize.";
            if (run.cleared.Contains(node.id)) return node.name + " is already cleared.";
            run.dungeonPoi = node.id;
            StartFloor(0);
            Note("The party entered " + node.name + ".");
            return null;
        }

        private void StartFloor(int floor)
        {
            var run = Run;
            run.dungeonLayoutVersion = TowerDungeon.LayoutVersion;
            run.floor = floor;
            run.fog = new string('0', TowerDungeon.Size * TowerDungeon.Size);
            run.roomsDone.Clear();
            run.roomsDone.Add(0);
            // Rooms finished on an earlier visit stay finished: re-entering never re-rolls loot or rests.
            if (run.roomsCleared == null) run.roomsCleared = new List<string>();
            string prefix = run.dungeonPoi + ":" + floor + ":";
            foreach (var key in run.roomsCleared)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) run.roomsDone.Add(int.Parse(key.Substring(prefix.Length)));
            var d = Dungeon;
            run.px = run.prevX = d.rooms[0].CenterX;
            run.py = run.prevY = d.rooms[0].CenterY;
            RevealAround();
        }

        public int PendingRoom
        {
            get
            {
                var run = Run; var d = Dungeon;
                if (d == null) return -1;
                int r = d.RoomAt(run.px, run.py);
                if (r < 0 || run.roomsDone.Contains(r)) return -1;
                return d.rooms[r].kind == "empty" || d.rooms[r].kind == "entrance" ? -1 : r;
            }
        }

        public static bool BattleRoom(string kind) { return kind == "enemy" || kind == "elite" || kind == "boss"; }

        // Walks along explored cells toward the target and stops at the first room with something in it.
        public string DungeonMove(int tx, int ty)
        {
            List<int> path;
            string error = DungeonRoute(tx, ty, out path);
            if (error != null) return error;
            foreach (int step in path)
            {
                error = DungeonAdvance(step % TowerDungeon.Size, step / TowerDungeon.Size);
                if (error != null) return error;
                if (PendingRoom >= 0) break;
            }
            return null;
        }

        // Walking distances from the party over known ground (-1 where the party cannot see a way).
        private int[] KnownDistances()
        {
            var run = Run; var d = Dungeon;
            var dist = new int[TowerDungeon.Size * TowerDungeon.Size];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<int>();
            int start = TowerDungeon.Index(run.px, run.py);
            dist[start] = 0; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % TowerDungeon.Size, y = i / TowerDungeon.Size;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!d.Walkable(nx, ny) || !Revealed(nx, ny)) continue;
                    int n = TowerDungeon.Index(nx, ny);
                    if (dist[n] >= 0) continue;
                    dist[n] = dist[i] + 1; queue.Enqueue(n);
                }
            }
            return dist;
        }

        // AUTO: the nearest known room still to explore (not finished, not the one the party stands in), by walking
        // distance. The goal or the stairs come last, once nothing else is left. skip: rooms the player turned down
        // for now (a rest, the stairs). -1 when there is nothing left to walk to.
        public int DungeonAutoTarget(ICollection<int> skip = null)
        {
            var run = Run; var d = Dungeon;
            if (d == null) return -1;
            var dist = KnownDistances();
            int here = d.RoomAt(run.px, run.py), best = -1, bestD = int.MaxValue, end = -1, endD = int.MaxValue;
            for (int i = 0; i < d.rooms.Count; i++)
            {
                if (i == here || run.roomsDone.Contains(i) || (skip != null && skip.Contains(i))) continue;
                var room = d.rooms[i];
                int c = TowerDungeon.Index(room.CenterX, room.CenterY);
                if (!Revealed(room.CenterX, room.CenterY) || dist[c] < 0) continue;
                if (room.goal || room.kind == "stairs") { if (dist[c] < endD) { endD = dist[c]; end = i; } }
                else if (dist[c] < bestD) { bestD = dist[c]; best = i; }
            }
            return best >= 0 ? best : end;
        }

        // GO TO GOAL: the goal room (or the stairs down) once it has been seen, else -1.
        public int DungeonGoalRoom
        {
            get
            {
                var d = Dungeon;
                if (d == null) return -1;
                for (int i = 0; i < d.rooms.Count; i++)
                    if ((d.rooms[i].goal || d.rooms[i].kind == "stairs") && Revealed(d.rooms[i].CenterX, d.rooms[i].CenterY)) return i;
                return -1;
            }
        }

        // One AUTO step: walk toward the nearest room still to explore, stopping at the first room with something in it.
        public string DungeonExplore(ICollection<int> skip = null)
        {
            int target = DungeonAutoTarget(skip);
            if (target < 0) return "Nothing left to explore here.";
            var room = Dungeon.rooms[target];
            return DungeonMove(room.CenterX, room.CenterY);
        }

        public string DungeonRoute(int tx, int ty, out List<int> path)
        {
            path = new List<int>();
            var run = Run; var d = Dungeon;
            if (d == null) return "Not in a dungeon.";
            if (PendingRoom >= 0 && BattleRoom(d.rooms[PendingRoom].kind)) return "Deal with this room first.";
            if (RewardPending) return "Choose your reward first.";
            if (!d.Walkable(tx, ty) || !Revealed(tx, ty)) return "You cannot see a way there.";
            if (tx == run.px && ty == run.py) return null;
            var prev = new int[TowerDungeon.Size * TowerDungeon.Size];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var queue = new Queue<int>();
            int start = TowerDungeon.Index(run.px, run.py), goal = TowerDungeon.Index(tx, ty);
            prev[start] = -1; queue.Enqueue(start);
            while (queue.Count > 0 && prev[goal] == -2)
            {
                int i = queue.Dequeue(), x = i % TowerDungeon.Size, y = i / TowerDungeon.Size;
                int[] nx = { x + 1, x - 1, x, x }, ny = { y, y, y + 1, y - 1 };
                for (int k = 0; k < 4; k++)
                {
                    if (!d.Walkable(nx[k], ny[k]) || !Revealed(nx[k], ny[k])) continue;
                    int n = TowerDungeon.Index(nx[k], ny[k]);
                    if (prev[n] != -2) continue;
                    prev[n] = i; queue.Enqueue(n);
                }
            }
            if (prev[goal] == -2) return "No known path.";
            for (int i = goal; i != start; i = prev[i]) path.Add(i);
            path.Reverse();
            return null;
        }

        public string DungeonAdvance(int x, int y)
        {
            var run = Run; var d = Dungeon;
            if (d == null) return "Not in a dungeon.";
            if (PendingRoom >= 0 && BattleRoom(d.rooms[PendingRoom].kind)) return "Deal with this room first.";
            if (Math.Abs(x - run.px) + Math.Abs(y - run.py) != 1) return "Move one step at a time.";
            if (!d.Walkable(x, y) || !Revealed(x, y)) return "You cannot see a way there.";
            run.prevX = run.px; run.prevY = run.py;
            run.px = x; run.py = y;
            RevealAround();
            int r = d.RoomAt(x, y);
            if (r >= 0 && !run.roomsDone.Contains(r) && (d.rooms[r].kind == "empty" || d.rooms[r].kind == "entrance")) run.roomsDone.Add(r);
            return null;
        }

        // Danger sets the numbers only; whether a fight is a pack, an elite or the lair boss comes from the room
        // (BattleCatalog.Build), so a deep region no longer turns every room into the boss line-up.
        public int BattleDepth(string kind)
        {
            var region = RunRegion;
            int depth = region == null ? 1 : region.depth;
            if (kind == "boss") return region == null ? 3 : region.bossDepth;
            // A place far out on the web (a high tier) fights a little harder (TowerAtlasNodes.NodeDepthBonus).
            depth += NodeDepthBonus(Run.dungeonPoi);
            if (kind == "elite") return depth + Run.floor + 1;
            return depth + Run.floor;
        }

        // The fight waiting in the current dungeon room: the dungeon's theme picks the species, the region its lair
        // boss. Seeded by run, place, floor and room, so leaving and coming back meets the same fight.
        public BattleEncounterSpec RoomEncounter(string kind)
        {
            var run = Run; var d = Dungeon;
            var spec = new BattleEncounterSpec { Depth = BattleDepth(kind), Kind = kind == "enemy" ? "normal" : kind,
                Theme = d != null ? d.theme : "", Region = run.region,
                Seed = (int)(TowerForestLayouts.Hash(run.dungeonPoi + ":" + run.floor + ":" + PendingRoom, run.seed) & 0x7fffffff) | 1 };
            // The place's map mods (Brutal, Resilient, Teeming, Fortified, Hasted...) shape every fight in it.
            if (GridRun) ApplyMods(spec, NodeTotals(run.dungeonPoi));
            return spec;
        }

        private TowerLoot RollLoot(TowerRng rng, float scale, string name)
        {
            float mul = (RunRegion == null ? 1 : RunRegion.reward) * (1 + Run.floor * 0.5f) * scale;
            if (HasRelic("lucky_coin")) mul *= 1.25f;
            // Inside a grid place: its tier and mods pay (more loot; Hasted pays more gold).
            float goldMul = 1f;
            if (GridRun && Run.dungeonPoi.Length > 0) { mul *= NodeLootScale(Run.dungeonPoi); goldMul = NodeTotals(Run.dungeonPoi).gold; }
            var loot = new TowerLoot { name = name, gold = Mathf.RoundToInt(25 * mul * goldMul * rng.Range(0.8f, 1.4f)) };
            if (rng.Value() < 0.3f * scale) loot.ore = 1 + rng.Next(2);
            if (rng.Value() < 0.15f * scale) loot.essence = 1;
            if (rng.Value() < 0.1f * scale) loot.tonics = 1;
            if (scale >= 2) loot.celestium = 2 + rng.Next(3);
            return loot;
        }

        private TowerRng RoomRng(int roomIndex)
        { var run = Run; return new TowerRng(TowerForestLayouts.Hash(run.layout + run.dungeonPoi, run.seed + run.floor * 97 + roomIndex * 13)); }

        // choice: treasure/rest "take"; unknown "investigate" or "ignore"; merchant "buy" or "leave"; stairs "descend".
        public string ResolveRoom(string choice)
        {
            var run = Run; var d = Dungeon;
            int r = PendingRoom;
            if (r < 0) return "Nothing to do here.";
            var room = d.rooms[r];
            var rng = RoomRng(r);
            switch (room.kind)
            {
                case "treasure":
                {
                    var loot = RollLoot(rng, room.goal ? 3 : 1, room.goal ? "Vault hoard" : "Treasure");
                    run.haul.Add(loot);
                    Note("Found " + LootLine(loot) + ".");
                    if (room.goal) { CompleteRoom(r); if (Run != null) OfferRewards("treasure", RoomRng(r + 2000)); return null; }
                    break;
                }
                case "rest":
                    if (run.firewood > 0) { run.firewood--; HealParty(50, false); Note("A warm fire: the party recovers."); }
                    else { HealParty(25, false); Note("A cold rest: the party recovers a little."); }
                    EaseStress(25);
                    break;
                case "unknown":
                    if (choice == "investigate")
                    {
                        float roll = rng.Value();
                        if (roll < 0.55f) { var loot = RollLoot(rng, room.goal ? 2 : 1, "Hidden cache"); run.haul.Add(loot); Note("A hidden cache: " + LootLine(loot) + "."); }
                        else if (roll < 0.8f) { HealParty(30, false); Note("A blessing: the party feels restored."); }
                        else { for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - 15); Note("A trap! Everyone is hurt."); }
                    }
                    break;
                case "merchant":
                    if (choice.StartsWith("buy", StringComparison.Ordinal))
                    {
                        string item = choice == "buy" ? "tonic" : choice.Substring(4);
                        int price = MerchantPrice(item);
                        if (price <= 0) return "The peddler does not sell that.";
                        if (HaulGold < price) return "The peddler wants " + price + " gold from your haul.";
                        if (item == "relic" && UnownedRelics().Count == 0) return "You already carry every relic the peddler has.";
                        SpendHaulGold(price);
                        if (item == "tonic") run.tonics++;
                        else if (item == "rations") run.rations += 2;
                        else if (item == "firewood") run.firewood++;
                        else { var relics = UnownedRelics(); var relic = relics[rng.Next(relics.Count)]; run.relics.Add(relic.id); Note("Bought the " + relic.name + "."); return null; }
                        Note("Bought " + (item == "rations" ? "two rations" : "a " + item) + ".");
                        return null;          // keep trading
                    }
                    break;
                case "shrine":
                    if (choice == "offer")
                    {
                        // Blood for a blessing: every fighter gives 15% health for a relic.
                        var relics = UnownedRelics();
                        if (relics.Count == 0) { HealParty(25, false); EaseStress(30); Note("The shrine has nothing left to give; it soothes you instead."); break; }
                        for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - 15);
                        var relic = relics[rng.Next(relics.Count)];
                        NormalizeRewards(run);
                        run.relics.Add(relic.id);
                        Note("The shrine takes its due and grants the " + relic.name + ".");
                    }
                    else if (choice == "pray") { HealParty(25, false); EaseStress(30); Note("A quiet prayer: wounds close and nerves settle."); }
                    break;
                case "trap":
                    if (choice == "disarm" && rng.Value() < RoomChance("trap"))
                    {
                        var loot = RollLoot(rng, .6f, "Trap salvage");
                        run.haul.Add(loot);
                        Note("The trap is disarmed and stripped: " + LootLine(loot) + ".");
                    }
                    else
                    {
                        int hurt = choice == "disarm" ? 12 : 8;
                        for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - hurt);
                        Note(choice == "disarm" ? "The trap springs! Everyone is hurt." : "The party dashes through the trap and takes a few cuts.");
                    }
                    break;
                case "skillcheck":
                    if (choice == "attempt")
                    {
                        if (rng.Value() < RoomChance("skillcheck"))
                        {
                            var loot = RollLoot(rng, 1.5f, "Hard-won find");
                            run.haul.Add(loot);
                            AwardExpeditionXp(10);
                            Note("The way is forced open: " + LootLine(loot) + ".");
                        }
                        else
                        {
                            for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - 10);
                            Note("It gives way badly. Everyone is bruised.");
                        }
                    }
                    break;
                case "story":
                    if (choice == "read")
                    {
                        AwardExpeditionXp(12);
                        LowerThreat(5);
                        NormalizeRewards(run);
                        if (!run.flags.Contains("lore_" + d.theme)) run.flags.Add("lore_" + d.theme);
                        Note("The party learns something of this place.");
                    }
                    break;
                case "keygate":
                    if (choice == "force")
                    {
                        if (rng.Value() < RoomChance("keygate"))
                        {
                            var loot = RollLoot(rng, 2.2f, "Sealed vault");
                            run.haul.Add(loot);
                            Note("The seal breaks. Behind it: " + LootLine(loot) + ".");
                        }
                        else
                        {
                            for (int i = 0; i < run.hp.Count; i++) if (run.hp[i] > 0) run.hp[i] = Mathf.Max(1, run.hp[i] - 15);
                            Note("The seal lashes back. The door stays shut.");
                        }
                    }
                    break;
                case "stairs":
                    if (choice != "descend") return null;
                    StartFloor(run.floor + 1);
                    Note("The party descends to floor " + (run.floor + 1) + ".");
                    return null;
                default:
                    return "Fight this room.";
            }
            CompleteRoom(r);
            return null;
        }

        // Odds for the trait-checked rooms, with the trait that helps (shown on the buttons).
        public static string RoomTrait(string kind)
        {
            return kind == "trap" ? "Careful" : kind == "skillcheck" ? "Stout" : kind == "keygate" ? "Curious" : "";
        }

        public float RoomChance(string kind)
        {
            float chance = kind == "trap" ? .5f : kind == "skillcheck" ? .5f : kind == "keygate" ? .45f : 1f;
            string trait = RoomTrait(kind);
            if (trait.Length > 0 && PartyHasTrait(trait)) chance += .3f;
            return Mathf.Clamp01(chance);
        }

        public static int MerchantPrice(string item)
        {
            return item == "tonic" ? 40 : item == "rations" ? 30 : item == "firewood" ? 20 : item == "relic" ? 160 : 0;
        }

        public int HaulGold { get { int gold = 0; var run = Run; if (run != null) foreach (var loot in run.haul) gold += loot.gold; return gold; } }

        private void SpendHaulGold(int amount)
        {
            foreach (var loot in Run.haul) { int take = Mathf.Min(amount, loot.gold); loot.gold -= take; amount -= take; }
        }

        private List<TowerRelicDef> UnownedRelics()
        {
            var list = new List<TowerRelicDef>();
            foreach (var r in Relics) if (!HasRelic(r.id)) list.Add(r);
            return list;
        }

        // hp: percent per party member after the fight (same order as run.party).
        public string ResolveBattle(bool won, List<int> hp)
        {
            var run = Run; var d = Dungeon;
            int r = PendingRoom;
            if (r < 0 || !BattleRoom(d.rooms[r].kind)) return "No fight here.";
            if (hp != null && hp.Count == run.hp.Count) for (int i = 0; i < hp.Count; i++) run.hp[i] = Mathf.Clamp(hp[i], 0, 100);
            PassBattleTime();
            if (!PartyAlive) return EndExpedition(true);
            if (!won)
            {
                // Withdrawing: the party backs out the way it came.
                run.px = run.prevX; run.py = run.prevY;
                Note("The party withdrew from the fight.");
                return null;
            }
            string kind = d.rooms[r].kind;
            float scale = kind == "boss" ? 4 : kind == "elite" ? 2 : 1;
            var loot = RollLoot(RoomRng(r), scale, kind == "boss" ? "Boss spoils" : kind == "elite" ? "Elite spoils" : "Spoils");
            run.haul.Add(loot);
            run.battlesWon++;
            RaiseThreat(ThreatBattle);
            AwardExpeditionXp(kind == "boss" ? 70 : kind == "elite" ? 35 : 15);
            Bump("expedition_win");
            var rewardRng = RoomRng(r + 1000);
            string poi = run.dungeonPoi;
            CompleteRoom(r);
            if (Run != null) AfterWin(kind, rewardRng, poi);
            return null;
        }

        private void CompleteRoom(int r)
        {
            var run = Run; var d = Dungeon;
            if (!run.roomsDone.Contains(r)) run.roomsDone.Add(r);
            if (run.roomsCleared == null) run.roomsCleared = new List<string>();
            string key = run.dungeonPoi + ":" + run.floor + ":" + r;
            if (!run.roomsCleared.Contains(key)) run.roomsCleared.Add(key);
            if (!d.rooms[r].goal) return;
            var node = RunLayout.Node(run.dungeonPoi);
            run.cleared.Add(node.id);
            run.dungeonPoi = "";
            // On the grid a cleared place is a completed Atlas node: the places linked to it come out of the fog.
            MarkNodeDone(node.id);
            LowerThreat(ThreatPoiCleared);
            Note(node.name + " is cleared.");
            if (node.kind == "lair") ConquerRegion(run.region);
        }

        public string LeaveDungeon()
        {
            var run = Run;
            if (run == null || run.dungeonPoi.Length == 0) return "Not in a dungeon.";
            if (PendingRoom >= 0 && BattleRoom(Dungeon.rooms[PendingRoom].kind)) return "You cannot flee mid-fight.";
            run.dungeonPoi = "";
            Note("The party climbed back out to the forest.");
            return null;
        }
    }
}
