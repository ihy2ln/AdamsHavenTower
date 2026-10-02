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

        public static TowerDungeon Build(string layoutId, TowerForestNode node, int floor, int runSeed, int version = 1)
        {
            if (version > 0) return BuildBranches(layoutId, node, floor, runSeed, version >= 2);
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

        // Room layouts on the 3x3 sector grid, entrance first. Version 2 dungeons pick one per visit from the run's seed
        // (version 1 kept the plus shape and a seed without the run, so a place looked the same every expedition).
        private static readonly int[][] Shapes =
        {
            new[] { 3, 4, 1, 5, 7 },                 // plus: a junction with three arms (corners added as detours)
            new[] { 6, 3, 0, 1, 4, 7, 8, 5, 2 },     // serpent: one long winding way
            new[] { 3, 0, 1, 2, 5, 8, 7, 6 },        // ring: a loop around a solid core
            new[] { 3, 4, 1, 2, 7, 8, 5 },           // fork: two branches that meet again at the far side
        };

        private static TowerDungeon BuildBranches(string layoutId, TowerForestNode node, int floor, int runSeed, bool shaped = false)
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
                for (int i = 2; i < d.rooms.Count; i++)
                    if (!d.rooms[i].goal && d.rooms[i].kind != "stairs" && d.rooms[i].kind != "treasure") { d.rooms[i].kind = "enemy"; break; }
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
        private string dungeonKey = "";

        public TowerDungeon Dungeon
        {
            get
            {
                var run = Run;
                if (run == null || run.dungeonPoi.Length == 0 || RunLayout == null || RunLayout.Node(run.dungeonPoi) == null) return null;
                string key = run.layout + ":" + run.dungeonPoi + ":" + run.floor + ":" + run.seed + ":" + run.dungeonLayoutVersion;
                if (key != dungeonKey || dungeonCache == null)
                {
                    dungeonCache = TowerDungeon.Build(run.layout, RunLayout.Node(run.dungeonPoi), run.floor, run.seed, run.dungeonLayoutVersion);
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
            run.dungeonLayoutVersion = 2;
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
            if (kind == "elite") return depth + Run.floor + 1;
            return depth + Run.floor;
        }

        // The fight waiting in the current dungeon room: the dungeon's theme picks the species, the region its lair
        // boss. Seeded by run, place, floor and room, so leaving and coming back meets the same fight.
        public BattleEncounterSpec RoomEncounter(string kind)
        {
            var run = Run; var d = Dungeon;
            return new BattleEncounterSpec { Depth = BattleDepth(kind), Kind = kind == "enemy" ? "normal" : kind,
                Theme = d != null ? d.theme : "", Region = run.region,
                Seed = (int)(TowerForestLayouts.Hash(run.dungeonPoi + ":" + run.floor + ":" + PendingRoom, run.seed) & 0x7fffffff) | 1 };
        }

        private TowerLoot RollLoot(TowerRng rng, float scale, string name)
        {
            float mul = (RunRegion == null ? 1 : RunRegion.reward) * (1 + Run.floor * 0.5f) * scale;
            if (HasRelic("lucky_coin")) mul *= 1.25f;
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
            CompleteRoom(r);
            if (Run != null) AfterWin(kind, rewardRng);
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
