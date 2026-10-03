using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // One link of the run map's web: two places and the trail between them.
    public sealed class TowerMapEdge
    {
        public string a, b, key;                     // key = TowerRules.RoadKey(a, b)
        public List<Vector2Int> path;                // cells from a to b, both ends included
        public float length;                         // terrain cost of the trail (worn road not counted)
        public string Other(string id) { return id == a ? b : id == b ? a : null; }

        // The trail walked from one end to the other.
        public List<Vector2Int> From(string id)
        {
            if (id == a) return path;
            var back = new List<Vector2Int>(path);
            back.Reverse();
            return back;
        }
    }

    // The run map as a Path of Exile 2-style Atlas web: the places are nodes, each linked to its natural neighbours by a
    // Gabriel graph (a and b link when no other place lies inside the circle with a-b as its diameter). That graph is
    // planar and always connected, and it is a pure function of the map, so a saved run rebuilds the same web.
    // After the forest shifts, the links among the places that stayed put are carried over as they were (`links`),
    // so a shift never cuts a known place off; the moved places link in by the Gabriel rule again.
    // The camp never links straight to the lair while another way round exists.
    public sealed class TowerMapWeb
    {
        public readonly List<TowerMapEdge> edges = new List<TowerMapEdge>();
        public readonly Dictionary<string, int> fromCamp = new Dictionary<string, int>();   // hops from the camp
        private readonly Dictionary<string, List<TowerMapEdge>> byNode = new Dictionary<string, List<TowerMapEdge>>();
        private static readonly List<TowerMapEdge> None = new List<TowerMapEdge>();

        public List<TowerMapEdge> EdgesOf(string id)
        {
            List<TowerMapEdge> list;
            return id != null && byNode.TryGetValue(id, out list) ? list : None;
        }

        public TowerMapEdge Edge(string a, string b)
        {
            var list = EdgesOf(a);
            for (int i = 0; i < list.Count; i++) if (list[i].Other(a) == b) return list[i];
            return null;
        }

        public bool Linked(string a, string b) { return Edge(a, b) != null; }

        public int CampHops(string id) { int h; return fromCamp.TryGetValue(id, out h) ? h : -1; }

        // Hops from a place to every place it can reach.
        public Dictionary<string, int> Hops(string from)
        {
            var hops = new Dictionary<string, int> { { from, 0 } };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (var e in EdgesOf(at))
                {
                    string next = e.Other(at);
                    if (hops.ContainsKey(next)) continue;
                    hops[next] = hops[at] + 1;
                    queue.Enqueue(next);
                }
            }
            return hops;
        }

        // A stable fingerprint of the links and their trails (tests: same map, same web).
        public string Signature()
        {
            var keys = new List<string>();
            foreach (var e in edges) keys.Add(e.key + "#" + e.path.Count);
            keys.Sort(string.CompareOrdinal);
            return string.Join(";", keys.ToArray());
        }

        // anchors: the places that stayed put through a shift; links: the edge keys they had (carried over exactly).
        public static TowerMapWeb Build(TowerOverworld map, List<TowerOverworldPoi> anchors = null, List<string> links = null)
        {
            var web = new TowerMapWeb();
            var pois = map.pois;
            int n = pois.Count;
            var anchored = new HashSet<string>();
            if (anchors != null) foreach (var a in anchors) anchored.Add(a.id);
            var kept = new HashSet<string>();
            if (links != null) foreach (var k in links) kept.Add(k);
            int camp = pois.FindIndex(p => p.kind == "camp"), lair = pois.FindIndex(p => p.kind == "lair");

            var pairs = new List<Vector2Int>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    string key = TowerRules.RoadKey(pois[i].id, pois[j].id);
                    bool link;
                    if (kept.Contains(key)) link = true;
                    else if (kept.Count > 0 && anchored.Contains(pois[i].id) && anchored.Contains(pois[j].id)) link = false;
                    else link = Gabriel(pois, i, j);
                    if (link) pairs.Add(new Vector2Int(i, j));
                }
            // No shortcut from the camp to the lair while the web holds together without it.
            if (camp >= 0 && lair >= 0)
            {
                var shortcut = new Vector2Int(Mathf.Min(camp, lair), Mathf.Max(camp, lair));
                if (pairs.Contains(shortcut) && !kept.Contains(TowerRules.RoadKey(pois[camp].id, pois[lair].id)))
                {
                    pairs.Remove(shortcut);
                    if (Components(n, pairs) > 1) pairs.Add(shortcut);
                }
            }
            // Trails for every link. Long ones (hull links bridging empty map edges: a few per map, 3x the usual trail) go
            // while the web holds together without them, so no single trip eats half the threat meter.
            var trails = new List<TowerMapEdge>();
            var trailPairs = new List<Vector2Int>();
            foreach (var pair in pairs)
            {
                var edge = Trail(map, pair.x, pair.y);
                if (edge != null) { trails.Add(edge); trailPairs.Add(pair); }
            }
            var order = new List<int>();
            for (int i = 0; i < trails.Count; i++) order.Add(i);
            order.Sort((p, q) => trails[q].path.Count != trails[p].path.Count ? trails[q].path.Count.CompareTo(trails[p].path.Count) :
                string.CompareOrdinal(trails[p].key, trails[q].key));
            var dropped = new HashSet<int>();
            foreach (int i in order)
            {
                if (trails[i].path.Count <= LongTrail || kept.Contains(trails[i].key)) continue;
                var without = new List<Vector2Int>();
                for (int k = 0; k < trails.Count; k++) if (k != i && !dropped.Contains(k)) without.Add(trailPairs[k]);
                if (Components(n, without) == 1) dropped.Add(i);
            }
            var linked = new List<Vector2Int>();
            for (int i = 0; i < trails.Count; i++)
                if (!dropped.Contains(i)) { web.Register(trails[i]); linked.Add(trailPairs[i]); }
            // Join anything left apart (a carried-over web plus places that just moved): shortest gap first.
            for (int guard = 0; guard < n && Components(n, linked) > 1; guard++)
            {
                var group = Groups(n, linked);
                var gaps = new List<Vector2Int>();
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (group[i] != group[j]) gaps.Add(new Vector2Int(i, j));
                gaps.Sort((p, q) => Dist2(pois, p) != Dist2(pois, q) ? Dist2(pois, p).CompareTo(Dist2(pois, q)) :
                    p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
                bool joined = false;
                foreach (var gap in gaps) if (web.Add(map, gap.x, gap.y)) { linked.Add(gap); joined = true; break; }
                if (!joined) break;
            }
            if (camp >= 0) foreach (var pair in web.Hops(pois[camp].id)) web.fromCamp[pair.Key] = pair.Value;
            return web;
        }

        // Trails longer than this many cells are dropped where the web holds together without them.
        public const int LongTrail = 36;

        private static TowerMapEdge Trail(TowerOverworld map, int i, int j)
        {
            var a = map.pois[i]; var b = map.pois[j];
            var path = map.FindPath(new Vector2Int(a.x, a.y), new Vector2Int(b.x, b.y));
            if (path == null || path.Count < 2) return null;
            return new TowerMapEdge { a = a.id, b = b.id, key = TowerRules.RoadKey(a.id, b.id), path = path, length = map.PathCost(path) };
        }

        private bool Add(TowerOverworld map, int i, int j)
        {
            var edge = Trail(map, i, j);
            if (edge == null) return false;
            Register(edge);
            return true;
        }

        private void Register(TowerMapEdge edge)
        {
            edges.Add(edge);
            List<TowerMapEdge> list;
            if (!byNode.TryGetValue(edge.a, out list)) byNode[edge.a] = list = new List<TowerMapEdge>();
            list.Add(edge);
            if (!byNode.TryGetValue(edge.b, out list)) byNode[edge.b] = list = new List<TowerMapEdge>();
            list.Add(edge);
        }

        // No other place strictly inside the circle on a-b as diameter (exact on the integer grid).
        private static bool Gabriel(List<TowerOverworldPoi> p, int i, int j)
        {
            for (int k = 0; k < p.Count; k++)
            {
                if (k == i || k == j) continue;
                long dot = (long)(p[i].x - p[k].x) * (p[j].x - p[k].x) + (long)(p[i].y - p[k].y) * (p[j].y - p[k].y);
                if (dot < 0) return false;
            }
            return true;
        }

        private static int Dist2(List<TowerOverworldPoi> p, Vector2Int pair)
        {
            int dx = p[pair.x].x - p[pair.y].x, dy = p[pair.x].y - p[pair.y].y;
            return dx * dx + dy * dy;
        }

        private static int[] Groups(int n, List<Vector2Int> pairs)
        {
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            System.Func<int, int> find = null;
            find = x => parent[x] == x ? x : (parent[x] = find(parent[x]));
            foreach (var pair in pairs) parent[find(pair.x)] = find(pair.y);
            var group = new int[n];
            for (int i = 0; i < n; i++) group[i] = find(i);
            return group;
        }

        private static int Components(int n, List<Vector2Int> pairs)
        {
            var group = Groups(n, pairs);
            var roots = new HashSet<int>(group);
            return roots.Count;
        }
    }
}
