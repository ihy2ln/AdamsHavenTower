using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Walkable mask for one painted forest map: white = open, grey = slow, black = blocked.
    // Used to walk the party across the painting along believable ground (A* over the mask grid).
    // Coordinates are normalised on the plate, y from the top, like forest nodes.
    public sealed class TowerMapMask
    {
        public const string Folder = "AdamsHaven/Expedition/Anchors/";
        public const float OpenCost = 1f, SlowCost = 2.5f;

        public readonly int width, height;
        private readonly byte[] cells;     // 0 blocked, 1 slow, 2 open

        private TowerMapMask(int w, int h, byte[] c) { width = w; height = h; cells = c; }

        // Built from raw 0..255 values (tests and tools).
        public static TowerMapMask FromValues(int w, int h, byte[] values)
        {
            var c = new byte[w * h];
            for (int i = 0; i < c.Length; i++) c[i] = values[i] >= 192 ? (byte)2 : values[i] >= 64 ? (byte)1 : (byte)0;
            return new TowerMapMask(w, h, c);
        }

        // Null when the layout has no (readable) mask: callers fall back to a plain transition.
        public static TowerMapMask Load(string layoutId)
        {
            if (string.IsNullOrEmpty(layoutId)) return null;
            var tex = Resources.Load<Texture2D>(Folder + layoutId + "_mask");
            if (tex == null) return null;
            Color32[] pixels;
            try { pixels = tex.GetPixels32(); }
            catch (UnityException) { return null; }
            var c = new byte[tex.width * tex.height];
            // Texture rows run bottom to top; the mask is addressed from the top.
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    byte v = pixels[(tex.height - 1 - y) * tex.width + x].r;
                    c[y * tex.width + x] = v >= 192 ? (byte)2 : v >= 64 ? (byte)1 : (byte)0;
                }
            return new TowerMapMask(tex.width, tex.height, c);
        }

        private int Index(int x, int y) { return y * width + x; }
        private bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }
        private int CellX(float nx) { return Mathf.Clamp(Mathf.FloorToInt(nx * width), 0, width - 1); }
        private int CellY(float ny) { return Mathf.Clamp(Mathf.FloorToInt(ny * height), 0, height - 1); }

        public bool Walkable(float nx, float ny) { return cells[Index(CellX(nx), CellY(ny))] > 0; }

        public float Cost(float nx, float ny)
        {
            byte c = cells[Index(CellX(nx), CellY(ny))];
            return c == 2 ? OpenCost : c == 1 ? SlowCost : float.PositiveInfinity;
        }

        // Nearest walkable cell to a point (the node may sit on a prop drawn over rock).
        private bool Snap(int x, int y, out int sx, out int sy)
        {
            for (int r = 0; r < Mathf.Max(width, height); r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int nx = x + dx, ny = y + dy;
                        if (Inside(nx, ny) && cells[Index(nx, ny)] > 0) { sx = nx; sy = ny; return true; }
                    }
            sx = sy = 0;
            return false;
        }

        // Path from a to b as a smoothed polyline in normalised coordinates, or null when no route exists.
        public List<Vector2> FindPath(Vector2 a, Vector2 b)
        {
            int sx, sy, gx, gy;
            if (!Snap(CellX(a.x), CellY(a.y), out sx, out sy) || !Snap(CellX(b.x), CellY(b.y), out gx, out gy)) return null;
            int n = width * height;
            var g = new float[n]; var from = new int[n]; var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; from[i] = -1; }
            int start = Index(sx, sy), goal = Index(gx, gy);
            g[start] = 0;
            var open = new List<int> { start };
            var f = new float[n];
            f[start] = Heuristic(sx, sy, gx, gy);
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (f[open[i]] < f[open[best]]) best = i;
                int cur = open[best]; open.RemoveAt(best);
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
                        byte cell = cells[ni];
                        if (cell == 0 || closed[ni]) continue;
                        // No cutting corners between two blocked neighbours.
                        if (dx != 0 && dy != 0 && (cells[Index(cx + dx, cy)] == 0 || cells[Index(cx, cy + dy)] == 0)) continue;
                        float step = (dx != 0 && dy != 0 ? 1.4142f : 1f) * (cell == 2 ? OpenCost : SlowCost);
                        float ng = g[cur] + step;
                        if (ng < g[ni])
                        {
                            g[ni] = ng; from[ni] = cur; f[ni] = ng + Heuristic(nx, ny, gx, gy);
                            open.Add(ni);
                        }
                    }
            }
            if (from[goal] < 0 && goal != start) return null;
            var cellsPath = new List<Vector2Int>();
            for (int at = goal; at >= 0; at = from[at]) { cellsPath.Add(new Vector2Int(at % width, at / width)); if (at == start) break; }
            cellsPath.Reverse();
            return Smooth(cellsPath, a, b);
        }

        private static float Heuristic(int x, int y, int gx, int gy)
        {
            float dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);    // octile distance, open-ground cost
        }

        // String-pull the cell path with line-of-sight, then round the corners (Chaikin).
        private List<Vector2> Smooth(List<Vector2Int> path, Vector2 a, Vector2 b)
        {
            var points = new List<Vector2> { a };
            int anchor = 0;
            while (anchor < path.Count - 1)
            {
                int far = anchor + 1;
                for (int i = path.Count - 1; i > anchor + 1; i--)
                    if (LineClear(path[anchor], path[i])) { far = i; break; }
                points.Add(Center(path[far]));
                anchor = far;
            }
            points.Add(b);
            for (int pass = 0; pass < 2 && points.Count > 2; pass++)
            {
                var next = new List<Vector2> { points[0] };
                for (int i = 0; i < points.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
                }
                next.Add(points[points.Count - 1]);
                points = next;
            }
            return points;
        }

        private Vector2 Center(Vector2Int c) { return new Vector2((c.x + 0.5f) / width, (c.y + 0.5f) / height); }

        private bool LineClear(Vector2Int a, Vector2Int b)
        {
            int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 2;
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0 : i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                if (cells[Index(x, y)] == 0) return false;
            }
            return true;
        }

        public static float Length(List<Vector2> path, float aspect)
        {
            float total = 0;
            for (int i = 1; i < path.Count; i++)
                total += new Vector2((path[i].x - path[i - 1].x) * aspect, path[i].y - path[i - 1].y).magnitude;
            return total;
        }
    }
}
