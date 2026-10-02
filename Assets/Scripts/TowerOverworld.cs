using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // The layered expedition map (plan: hidden square rules grid, painted layers on top).
    // The grid is never saved: it is regenerated from biome + seed + shift, and a run stores only what changed.
    public enum TowerTerrain : byte { DenseForest, Forest, Clearing, Road, Water, Ford, Bridge, Rock, Hill, RuinGround, Blight, Marsh }

    [System.Serializable] public sealed class TowerOverworldPoi
    {
        public string id = "", kind = "", name = "", theme = "";
        public int x, y;
    }

    public sealed class TowerOverworld
    {
        public const int Version = 3, Width = 64, Height = 40;   // 3: biome place mixes (TowerOverworldGen.KindsFor)
        public const float Blocked = float.PositiveInfinity;

        public readonly int width, height;
        public readonly string biome;
        public readonly uint seed;
        public readonly TowerTerrain[] cells;
        public readonly List<TowerOverworldPoi> pois = new List<TowerOverworldPoi>();
        public int entranceX, entranceY;

        public TowerOverworld(int w, int h, string biome, uint seed)
        {
            width = w; height = h; this.biome = biome; this.seed = seed;
            cells = new TowerTerrain[w * h];
        }

        public int Index(int x, int y) { return y * width + x; }
        public bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }
        public TowerTerrain At(int x, int y) { return cells[Index(x, y)]; }
        public void Set(int x, int y, TowerTerrain t) { if (Inside(x, y)) cells[Index(x, y)] = t; }

        public TowerOverworldPoi Poi(string id) { return pois.Find(p => p.id == id); }
        public TowerOverworldPoi PoiAt(int x, int y) { return pois.Find(p => p.x == x && p.y == y); }
        public TowerOverworldPoi Camp { get { return pois.Find(p => p.kind == "camp"); } }
        public TowerOverworldPoi Lair { get { return pois.Find(p => p.kind == "lair"); } }

        // Movement cost of stepping onto a cell. Roads and clearings are easy, deep forest is slow, water and rock block.
        public static float TerrainCost(TowerTerrain t)
        {
            switch (t)
            {
                case TowerTerrain.Road: case TowerTerrain.Clearing: case TowerTerrain.Bridge: return 1f;
                case TowerTerrain.RuinGround: return 1.2f;
                case TowerTerrain.Forest: return 2f;
                case TowerTerrain.Marsh: case TowerTerrain.Blight: return 2.5f;
                case TowerTerrain.Hill: case TowerTerrain.Ford: return 3f;
                case TowerTerrain.DenseForest: return 4f;
                default: return Blocked;
            }
        }

        public bool Walkable(int x, int y) { return Inside(x, y) && !float.IsInfinity(TerrainCost(At(x, y))); }

        // isRoad marks cells the party has worn into road; they cost as little as a real road.
        public float Cost(int x, int y, System.Func<int, bool> isRoad = null)
        {
            float c = TerrainCost(At(x, y));
            if (isRoad != null && !float.IsInfinity(c) && isRoad(Index(x, y))) c = 1f;
            return c;
        }

        // Cheapest 8-way route from a to b (inclusive), or null. Diagonals may not cut past a blocked corner.
        public List<Vector2Int> FindPath(Vector2Int a, Vector2Int b, System.Func<int, bool> isRoad = null, System.Func<int, float> costOverride = null)
        {
            if (!Inside(a.x, a.y) || !Inside(b.x, b.y)) return null;
            System.Func<int, float> cost = costOverride ?? (i => Cost(i % width, i / width, isRoad));
            if (float.IsInfinity(cost(Index(b.x, b.y)))) return null;
            int n = width * height, start = Index(a.x, a.y), goal = Index(b.x, b.y);
            var g = new float[n]; var from = new int[n]; var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; from[i] = -1; }
            g[start] = 0;
            var open = new MinHeap(n);
            open.Push(start, Heuristic(a.x, a.y, b.x, b.y));
            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (cur == goal) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                int cx = cur % width, cy = cur / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (!Inside(nx, ny)) continue;
                        int ni = Index(nx, ny);
                        if (closed[ni]) continue;
                        float step = cost(ni);
                        if (float.IsInfinity(step)) continue;
                        if (dx != 0 && dy != 0 && (float.IsInfinity(cost(Index(cx + dx, cy))) || float.IsInfinity(cost(Index(cx, cy + dy))))) continue;
                        float ng = g[cur] + step * (dx != 0 && dy != 0 ? 1.4142f : 1f);
                        if (ng < g[ni]) { g[ni] = ng; from[ni] = cur; open.Push(ni, ng + Heuristic(nx, ny, b.x, b.y)); }
                    }
            }
            if (start != goal && from[goal] < 0) return null;
            var path = new List<Vector2Int>();
            for (int at = goal; at >= 0; at = at == start ? -1 : from[at]) path.Add(new Vector2Int(at % width, at / width));
            path.Reverse();
            return path;
        }

        // Total cost of walking a path (the first cell is where the party already stands).
        public float PathCost(List<Vector2Int> path, System.Func<int, bool> isRoad = null)
        {
            float total = 0;
            for (int i = 1; i < path.Count; i++)
            {
                bool diagonal = path[i].x != path[i - 1].x && path[i].y != path[i - 1].y;
                total += Cost(path[i].x, path[i].y, isRoad) * (diagonal ? 1.4142f : 1f);
            }
            return total;
        }

        private static float Heuristic(int x, int y, int gx, int gy)
        {
            float dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
        }

        // Stable fingerprint of the generated grid (tests: same seed, same map).
        public uint Hash()
        {
            uint h = 2166136261u;
            foreach (var c in cells) { h ^= (uint)c; h *= 16777619u; }
            foreach (var p in pois) { h ^= (uint)(p.x * 131 + p.y); h *= 16777619u; }
            return h;
        }

        private sealed class MinHeap
        {
            private readonly int[] items; private readonly float[] keys; public int Count;
            public MinHeap(int capacity) { items = new int[capacity * 8 + 8]; keys = new float[capacity * 8 + 8]; }
            public void Push(int item, float key)
            {
                int i = Count++;
                items[i] = item; keys[i] = key;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p); i = p;
                }
            }
            public int Pop()
            {
                int top = items[0];
                Count--;
                items[0] = items[Count]; keys[0] = keys[Count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < Count && keys[l] < keys[m]) m = l;
                    if (r < Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
                return top;
            }
            private void Swap(int a, int b)
            {
                int t = items[a]; items[a] = items[b]; items[b] = t;
                float k = keys[a]; keys[a] = keys[b]; keys[b] = k;
            }
        }
    }
}
