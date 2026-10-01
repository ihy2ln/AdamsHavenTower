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
                default: return "unknown";      // shrine, mystery
            }
        }

        public static TowerDungeon Build(string layoutId, TowerForestNode node, int floor, int runSeed)
        {
            var d = new TowerDungeon { theme = node.theme, poiKind = node.kind, floor = floor,
                floors = Mathf.Max(1, TowerForestLayouts.Floors(node.kind)) };
            for (int i = 0; i < d.room.Length; i++) d.room[i] = -1;
            var rng = new TowerRng(TowerForestLayouts.Hash(layoutId + ":" + node.id, floor * 131 + 7));
            int target = 6 + rng.Next(3);
            for (int attempt = 0; attempt < 200 && d.rooms.Count < target; attempt++)
            {
                int w = 3 + rng.Next(3), h = 3 + rng.Next(3);
                int x = 1 + rng.Next(Size - w - 1), y = 1 + rng.Next(Size - h - 1);
                bool clear = true;
                foreach (var other in d.rooms)
                    if (x - 1 < other.x + other.w && x + w + 1 > other.x && y - 1 < other.y + other.h && y + h + 1 > other.y)
                    { clear = false; break; }
                if (!clear) continue;
                var r = new TowerDungeonRoom { x = x, y = y, w = w, h = h };
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++) { d.cell[Index(xx, yy)] = Floor; d.room[Index(xx, yy)] = d.rooms.Count; }
                d.rooms.Add(r);
            }
            // Each room joins the nearest earlier room with an L-shaped corridor.
            for (int i = 1; i < d.rooms.Count; i++)
            {
                int best = 0, bestDist = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    int dist = Math.Abs(d.rooms[i].CenterX - d.rooms[j].CenterX) + Math.Abs(d.rooms[i].CenterY - d.rooms[j].CenterY);
                    if (dist < bestDist) { bestDist = dist; best = j; }
                }
                d.Carve(d.rooms[i].CenterX, d.rooms[i].CenterY, d.rooms[best].CenterX, d.rooms[best].CenterY, rng.Next(2) == 0);
            }
            // Corridor cells touching a room become doors.
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (d.CellAt(x, y) != Corridor) continue;
                    if (d.CellAt(x + 1, y) == Floor || d.CellAt(x - 1, y) == Floor ||
                        d.CellAt(x, y + 1) == Floor || d.CellAt(x, y - 1) == Floor) d.cell[Index(x, y)] = Door;
                }
            d.AssignRooms(new TowerRng(TowerForestLayouts.Hash(layoutId + ":" + node.id, floor * 7919 + runSeed)));
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

        private void AssignRooms(TowerRng rng)
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
                string kind = roll < 0.42f ? "enemy" : roll < 0.6f ? "treasure" : roll < 0.72f ? "rest" : roll < 0.88f ? "unknown" : "empty";
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
        private string dungeonKey = "";

        public TowerDungeon Dungeon
        {
            get
            {
                var run = Run;
                if (run == null || run.dungeonPoi.Length == 0 || RunLayout == null || RunLayout.Node(run.dungeonPoi) == null) return null;
                string key = run.layout + ":" + run.dungeonPoi + ":" + run.floor + ":" + run.seed;
                if (key != dungeonKey || dungeonCache == null)
                {
                    dungeonCache = TowerDungeon.Build(run.layout, RunLayout.Node(run.dungeonPoi), run.floor, run.seed);
                    dungeonKey = key;
                }
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

        private void RevealAround()
        {
            var run = Run; var d = Dungeon;
            var fog = run.fog.ToCharArray();
            for (int y = 0; y < TowerDungeon.Size; y++)
                for (int x = 0; x < TowerDungeon.Size; x++)
                    if (InSight(x, y)) fog[TowerDungeon.Index(x, y)] = '1';
            run.fog = new string(fog);
        }

        public string EnterPoi()
        {
            var run = Run;
            if (run == null) return "No expedition.";
            if (run.dungeonPoi.Length > 0) return "Already inside.";
            if (EventBlock() != null) return EventBlock();
            var node = RunLayout.Node(run.at);
            if (node == null || node.kind == "camp") return "Nothing to explore here. Rest or move on.";
            if (run.cleared.Contains(node.id)) return node.name + " is already cleared.";
            run.dungeonPoi = node.id;
            StartFloor(0);
            Note("The party entered " + node.name + ".");
            return null;
        }

        private void StartFloor(int floor)
        {
            var run = Run;
            run.floor = floor;
            run.fog = new string('0', TowerDungeon.Size * TowerDungeon.Size);
            run.roomsDone.Clear();
            run.roomsDone.Add(0);
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
            var run = Run; var d = Dungeon;
            if (d == null) return "Not in a dungeon.";
            if (PendingRoom >= 0 && BattleRoom(d.rooms[PendingRoom].kind)) return "Deal with this room first.";
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
            var path = new List<int>();
            for (int i = goal; i != start; i = prev[i]) path.Add(i);
            path.Reverse();
            foreach (int step in path)
            {
                run.prevX = run.px; run.prevY = run.py;
                run.px = step % TowerDungeon.Size; run.py = step / TowerDungeon.Size;
                RevealAround();
                int r = d.RoomAt(run.px, run.py);
                if (r >= 0 && !run.roomsDone.Contains(r) && (d.rooms[r].kind == "empty" || d.rooms[r].kind == "entrance"))
                    run.roomsDone.Add(r);
                if (PendingRoom >= 0) break;
            }
            return null;
        }

        public int BattleDepth(string kind)
        {
            var region = RunRegion;
            int depth = region == null ? 1 : region.depth;
            if (kind == "boss") return region == null ? 3 : region.bossDepth;
            if (kind == "elite") return Mathf.Max(3, depth + 1);
            return depth + Run.floor;
        }

        private TowerLoot RollLoot(TowerRng rng, float scale, string name)
        {
            float mul = (RunRegion == null ? 1 : RunRegion.reward) * (1 + Run.floor * 0.5f) * scale;
            var loot = new TowerLoot { name = name, gold = Mathf.RoundToInt(25 * mul * rng.Range(0.8f, 1.4f)) };
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
                    break;
                }
                case "rest":
                    if (run.firewood > 0) { run.firewood--; HealParty(50, false); Note("A warm fire: the party recovers."); }
                    else { HealParty(25, false); Note("A cold rest: the party recovers a little."); }
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
                    if (choice == "buy")
                    {
                        int gold = 0; foreach (var loot in run.haul) gold += loot.gold;
                        if (gold < 40) return "The peddler wants 40 gold from your haul.";
                        int owed = 40;
                        foreach (var loot in run.haul) { int take = Mathf.Min(owed, loot.gold); loot.gold -= take; owed -= take; }
                        run.tonics++;
                        Note("Bought a tonic.");
                        return null;          // keep trading
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

        // hp: percent per party member after the fight (same order as run.party).
        public string ResolveBattle(bool won, List<int> hp)
        {
            var run = Run; var d = Dungeon;
            int r = PendingRoom;
            if (r < 0 || !BattleRoom(d.rooms[r].kind)) return "No fight here.";
            if (hp != null && hp.Count == run.hp.Count) for (int i = 0; i < hp.Count; i++) run.hp[i] = Mathf.Clamp(hp[i], 0, 100);
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
            CompleteRoom(r);
            return null;
        }

        private void CompleteRoom(int r)
        {
            var run = Run; var d = Dungeon;
            if (!run.roomsDone.Contains(r)) run.roomsDone.Add(r);
            if (!d.rooms[r].goal) return;
            var node = RunLayout.Node(run.dungeonPoi);
            run.cleared.Add(node.id);
            run.dungeonPoi = "";
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
