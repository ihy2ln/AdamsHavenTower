using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A planned trip across the run map's web (TowerMapWeb): the places it passes, the trail cells and what it costs.
    public sealed class TowerAtlasRoute
    {
        public string target = "";
        public List<string> hops = new List<string>();          // places from the party's to the target, both included
        public List<Vector2Int> path = new List<Vector2Int>();  // trail cells, the party's cell first
        public float cost, rations, hours;
        public int threat, roadCells, trailCells;
        public string error;
        public bool Here { get { return error == null && hops.Count == 1; } }
    }

    // Result of travelling: the cells the party actually crossed, and whether something stopped them.
    public sealed class TowerGridStep
    {
        public List<Vector2Int> walked = new List<Vector2Int>();
        public bool halted, shifted, towerClimbed;
        public TowerOverworldPoi arrived;
        public string error;
    }

    // Expedition rules on the layered grid map (TowerOverworld). The map is played like the Path of Exile 2 Atlas:
    // the places are nodes of a web (TowerMapWeb) and the party travels node to node along its trails, never cell by cell.
    // Only places linked to a completed one (or the camp) can be reached; completing a place reveals its neighbours
    // (TowerAtlasNodes.cs). Every trail cell still costs time, rations and threat, and is worn toward road (cheaper the
    // next time). Events roll once per trail travelled. A full threat meter makes the forest stir: loose road wears down
    // and places nobody has seen yet move. Plate runs (mapKind "") keep the old node rules for the rules tests.
    public sealed partial class TowerRules
    {
        // New expeditions use the grid map. Rules tests switch it off to walk the plate graphs.
        public static bool GridMaps = true;

        public const string GridKind = "grid";
        public const float RationCost = 20f;            // cost units per ration (about one old trail hop)
        public const float ThreatPerTrailCell = 0.8f, ThreatPerRoadCell = 0.3f, ThreatPerNightCell = 0.4f;
        public const int RoadWalked = 3, RoadMax = 9, RoadWornOnStir = 3, GridSight = 5;
        // 1: the run map is a web of places (old grid runs are migrated on load: MigrateWebRun).
        public const int WebVersion = 1;
        // Time in the wild (Tower clock seconds; DaySeconds = one day): each cost unit walked is 8 minutes,
        // a fight half an hour, a camp rest sleeps until dawn at night or three hours by day.
        public const float TravelSecondsPerCost = DaySeconds / 24f * (8f / 60f), BattleSeconds = DaySeconds / 48f;

        public bool GridRun { get { var run = Run; return run != null && run.mapKind == GridKind; } }

        // The current run's grid map and web, regenerated from its seed (cached; compared field by field so the
        // thousands of cell lookups a frame does not build a key string each time).
        private TowerOverworld overworldCache;
        private TowerMapWeb webCache;
        private TowerForestLayout overworldLayout;
        private TowerRun owRun;
        private string owBiome;
        private int owSeed, owShift, owVersion;

        public TowerOverworld Overworld
        {
            get
            {
                var run = Run;
                if (run == null || run.mapKind != GridKind) return null;
                if (overworldCache == null || owRun != run || owBiome != run.biome || owSeed != run.gridSeed || owShift != run.shift ||
                    owVersion != run.gridVersion)
                {
                    overworldCache = TowerOverworldGen.Generate(run.biome, (uint)run.gridSeed, run.shift, run.anchors, version: run.gridVersion);
                    webCache = TowerMapWeb.Build(overworldCache, run.anchors, run.webLinks);
                    overworldLayout = LayoutFromOverworld(overworldCache, run.layout, webCache);
                    owRun = run; owBiome = run.biome; owSeed = run.gridSeed; owShift = run.shift; owVersion = run.gridVersion;
                }
                return overworldCache;
            }
        }

        // The web of places over the current grid map (null on plate runs).
        public TowerMapWeb Web { get { return Overworld == null ? null : webCache; } }

        // The grid's places seen as forest nodes, so dungeons, events and the run panel work unchanged.
        // Their links are the web's, so scouting events and the rest-before-lair rule follow the same trails.
        private static TowerForestLayout LayoutFromOverworld(TowerOverworld map, string id, TowerMapWeb web)
        {
            var layout = new TowerForestLayout { id = id, name = TowerForestLayouts.Pretty(map.biome == "edge" ? "forest_edge" : map.biome),
                backdrop = "", theme = map.pois.Count > 0 ? map.pois[0].theme : "ruin", entrance = "camp" };
            foreach (var p in map.pois)
            {
                var node = new TowerForestNode { id = p.id, kind = p.kind, name = p.name, theme = p.theme,
                    x = (p.x + 0.5f) / map.width, y = (p.y + 0.5f) / map.height };
                foreach (var e in web.EdgesOf(p.id)) node.links.Add(e.Other(p.id));
                layout.nodes.Add(node);
            }
            return layout;
        }

        private TowerForestLayout GridLayout { get { return Overworld == null ? null : overworldLayout; } }

        // ---------------------------------------------------------------- setup

        private void StartGridRun(TowerRun run, string region)
        {
            run.mapKind = GridKind;
            run.gridVersion = TowerOverworld.Version;
            run.webVersion = WebVersion;
            run.biome = TowerOverworldGen.BiomeForRegion(region);
            run.gridSeed = (int)(TowerForestLayouts.Hash(region + ":grid", run.seed) & 0x7fffffff);
            run.shift = 0;
            run.layout = "grid_" + run.biome;
            run.webLinks = new List<string>();
            run.tablets = new List<string>();
            run.tabletOffer = new List<string>();
            var map = Overworld;
            var camp = map.Camp;
            run.cx = camp.x; run.cy = camp.y;
            run.at = camp.id;
            run.gridFog = new string('0', map.width * map.height);
            run.gridRoad = new string('0', map.width * map.height);
            run.targetX = run.targetY = -1;
            run.day = GameDay;
            run.nodesDone = new List<string> { camp.id };
            run.nodeHops = new List<string>();
            RecordHops();
            SyncAtlas();
        }

        // A run saved on a painted plate (from before the grid map) moves onto the grid: the party makes a fresh camp in the
        // same region and keeps its health, stress, supplies, haul, relics and upgrades. Places cleared on the plate are lost
        // with it. True when the run changed and needs saving.
        public bool MigratePlateRun()
        {
            var run = Run;
            if (!GridMaps || run == null || run.mapKind == GridKind) return false;
            run.dungeonPoi = ""; run.fog = ""; run.roomsDone.Clear(); run.roomsCleared.Clear();
            run.eventId = ""; run.eventResult = ""; run.ambushDepth = 0; run.lastStepEvent = false;
            run.visited.Clear(); run.cleared.Clear(); run.revealed.Clear(); run.road.Clear(); run.trails.Clear();
            run.anchors.Clear();
            run.roadSteps = 0; run.threat = 0;
            StartGridRun(run, run.region);
            run.visited.Add(run.at);
            RevealFrom(run.at);
            Note("The forest shifted while the guild was away. The party made a fresh camp.");
            return true;
        }

        // A grid run saved before the web (free cell-by-cell walking) keeps its map, haul and progress: every place it
        // cleared or stood on counts as completed, and a party caught between places walks back to the camp.
        public bool MigrateWebRun()
        {
            var run = Run;
            if (run == null || run.mapKind != GridKind || run.webVersion >= WebVersion) return false;
            if (run.nodesDone == null) run.nodesDone = new List<string>();
            if (run.webLinks == null) run.webLinks = new List<string>();
            if (run.tablets == null) run.tablets = new List<string>();
            if (run.tabletOffer == null) run.tabletOffer = new List<string>();
            var map = Overworld;
            if (map == null || map.Camp == null) return false;
            var here = map.PoiAt(run.cx, run.cy);
            if (run.dungeonPoi.Length == 0 && here == null)
            {
                run.cx = map.Camp.x; run.cy = map.Camp.y; run.at = map.Camp.id;
            }
            else if (here != null) run.at = here.id;
            var done = new List<string> { map.Camp.id };
            done.AddRange(run.cleared); done.AddRange(run.visited);
            if (run.at.Length > 0) done.Add(run.at);
            if (run.dungeonPoi.Length > 0) done.Add(run.dungeonPoi);
            // The old run walked freely, so it passed the places between: complete the fewest-hop way from the camp to each
            // place it knew, so the party can always travel home over completed places.
            var hops = Web.Hops(map.Camp.id);
            foreach (var id in done.ToArray())
            {
                string at = id;
                for (int guard = 0; guard < 64 && at != null && hops.ContainsKey(at) && hops[at] > 1; guard++)
                {
                    string back = null;
                    foreach (var e in Web.EdgesOf(at)) { int h; if (hops.TryGetValue(e.Other(at), out h) && h == hops[at] - 1) { back = e.Other(at); break; } }
                    if (back != null) done.Add(back);
                    at = back;
                }
            }
            foreach (var id in done) if (map.Poi(id) != null && !run.nodesDone.Contains(id)) run.nodesDone.Add(id);
            run.targetX = run.targetY = -1;
            run.webVersion = WebVersion;
            RecordHops();
            SyncAtlas();
            return true;
        }

        // ---------------------------------------------------------------- the forest shifts

        public int GameDay { get { return Mathf.FloorToInt((6f + RunClock / DaySeconds * 24f) / 24f); } }

        // The Tower stands still while an expedition is open, so the run keeps its own time on top of the Tower clock.
        public float RunClock { get { var run = Run; return State.clock + (run != null ? run.clock : 0f); } }
        public float RunHour() { return (6f + RunClock / DaySeconds * 24f) % 24f; }

        private void PassRestTime()
        {
            var run = Run;
            if (run == null) return;
            float hour = RunHour();
            float hours = IsDaylight(hour) ? 3f : (hour >= 6f ? 30f - hour : 6f - hour);   // night: sleep until 06:00
            run.clock += hours * DaySeconds / 24f;
        }

        private void PassBattleTime() { var run = Run; if (run != null) run.clock += BattleSeconds; }

        // The woods rearrange around the party. Full (threat filled): every place nobody has seen moves, the woods regrow
        // and the fog closes in again away from what is known. Small (a new day): one unseen place moves.
        // The camp, the lair, completed, reachable and scouted places, and the party's own spot never move, and the
        // links among them are carried over, so nothing known is cut off.
        public bool ShiftForest(bool small)
        {
            var run = Run; var map = Overworld; var web = Web;
            if (map == null) return false;
            var keep = new List<TowerOverworldPoi>();
            var loose = new List<TowerOverworldPoi>();
            foreach (var p in map.pois)
            {
                bool fixedPlace = p.kind == "camp" || p.kind == "lair" || NodeState(p.id) >= NodeScouted || run.visited.Contains(p.id) ||
                    run.cleared.Contains(p.id) || p.id == run.at || p.id == run.dungeonPoi;
                (fixedPlace ? keep : loose).Add(p);
            }
            if (loose.Count == 0) return false;
            if (small)
            {
                // Move one place the party has not seen yet if there is one, else any unexplored place.
                var pick = loose.Find(p => !CellSeen(p.x, p.y)) ?? loose[EventRng("dayshift").Next(loose.Count)];
                foreach (var p in loose) if (p != pick) keep.Add(p);
            }
            var kept = new HashSet<string>();
            foreach (var p in keep) kept.Add(p.id);
            run.webLinks = new List<string>();
            foreach (var e in web.edges) if (kept.Contains(e.a) && kept.Contains(e.b)) run.webLinks.Add(e.key);
            run.anchors = new List<TowerOverworldPoi>();
            foreach (var p in keep)
                run.anchors.Add(new TowerOverworldPoi { id = p.id, kind = p.kind, name = p.name, theme = p.theme, x = p.x, y = p.y });
            run.shift++;
            run.targetX = run.targetY = -1;
            var moved = Overworld;     // regenerated with the new shift
            if (!small)
            {
                // The fog closes in again except around the party, the anchored places and well-worn road.
                var fog = new string('0', moved.cells.Length).ToCharArray();
                RevealInto(fog, run.cx, run.cy, GridSightRadius);
                foreach (var p in run.anchors) RevealInto(fog, p.x, p.y, NodeReveal);
                for (int y = 0; y < moved.height; y++)
                    for (int x = 0; x < moved.width; x++)
                        if (RoadStrength(x, y) >= RoadWalked) RevealInto(fog, x, y, 1);
                CommitFog(fog);
            }
            run.revealed.RemoveAll(id => moved.Poi(id) == null);
            foreach (var p in moved.pois) if (CellSeen(p.x, p.y) && !run.revealed.Contains(p.id)) run.revealed.Add(p.id);
            RecordHops();
            SyncAtlas();
            var here = moved.PoiAt(run.cx, run.cy);
            run.at = here != null ? here.id : "";
            Note(small ? "Overnight the trees have moved: one of the paths ahead is not where it was." :
                "The forest shifts! Trees turn and close in; places nobody has seen are somewhere else now.");
            return true;
        }

        // A new day while out in the wild nudges the forest once.
        private void CheckDayRoll()
        {
            var run = Run;
            int today = GameDay;
            if (today <= run.day) return;
            run.day = today;
            ShiftForest(true);
        }

        // ---------------------------------------------------------------- cells

        public bool CellSeen(int x, int y)
        {
            var run = Run; var map = Overworld;
            return map != null && map.Inside(x, y) && run.gridFog.Length == map.cells.Length && run.gridFog[map.Index(x, y)] != '0';
        }

        public int RoadStrength(int x, int y)
        {
            var run = Run; var map = Overworld;
            if (map == null || !map.Inside(x, y) || run.gridRoad.Length != map.cells.Length) return 0;
            return run.gridRoad[map.Index(x, y)] - '0';
        }

        // Laid road on the map, or ground worn into road by walking.
        public bool CellIsRoad(int x, int y)
        {
            var map = Overworld;
            return map != null && map.Inside(x, y) && (map.At(x, y) == TowerTerrain.Road || map.At(x, y) == TowerTerrain.Bridge || RoadStrength(x, y) >= RoadWalked);
        }

        // Snapshot of which cells count as road (pathfinding asks thousands of times).
        private System.Func<int, bool> RoadTest()
        {
            var map = Overworld; var run = Run;
            var road = new bool[map.cells.Length];
            bool worn = run.gridRoad.Length == map.cells.Length;
            for (int i = 0; i < road.Length; i++)
                road[i] = map.cells[i] == TowerTerrain.Road || map.cells[i] == TowerTerrain.Bridge || (worn && run.gridRoad[i] - '0' >= RoadWalked);
            return i => road[i];
        }

        private int GridSightRadius { get { return Mathf.Max(3, GridSight + 2 * (RevealRadius - 1) - WeatherSightPenalty); } }

        // Lifts the fog in a circle; places inside it become known.
        private void RevealCells(int cx, int cy, int radius)
        {
            var fog = FogBuffer();
            if (fog == null) return;
            RevealInto(fog, cx, cy, radius);
            CommitFog(fog);
        }

        // The fog as a buffer to edit many cells at once (one string per batch instead of one per cell).
        private char[] FogBuffer()
        {
            var run = Run; var map = Overworld;
            if (map == null) return null;
            if (run.gridFog.Length != map.cells.Length) run.gridFog = new string('0', map.cells.Length);
            return run.gridFog.ToCharArray();
        }

        private void RevealInto(char[] fog, int cx, int cy, int radius)
        {
            var map = Overworld;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (map.Inside(x, y) && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius + radius) fog[map.Index(x, y)] = '1';
        }

        private void CommitFog(char[] fog)
        {
            var run = Run; var map = Overworld;
            run.gridFog = new string(fog);
            foreach (var p in map.pois)
                if (fog[map.Index(p.x, p.y)] != '0' && !run.revealed.Contains(p.id)) run.revealed.Add(p.id);
        }

        private char[] RoadBuffer()
        {
            var run = Run; var map = Overworld;
            if (run.gridRoad.Length != map.cells.Length) run.gridRoad = new string('0', map.cells.Length);
            return run.gridRoad.ToCharArray();
        }

        private static void WearInto(char[] road, int i) { road[i] = (char)('0' + Mathf.Min(RoadMax, road[i] - '0' + RoadWalked)); }

        // ---------------------------------------------------------------- routes

        // The place the party stands at (on the web every stop is a place).
        public string PartyNode
        {
            get
            {
                var run = Run; var map = Overworld;
                if (run == null || map == null) return null;
                if (run.at.Length > 0 && map.Poi(run.at) != null) return run.at;
                var here = map.PoiAt(run.cx, run.cy) ?? NearestPoi(run.cx, run.cy);
                return here != null ? here.id : null;
            }
        }

        // The cheapest way to a reachable place: through completed places only, along the web's trails.
        public TowerAtlasRoute AtlasRoute(string targetId)
        {
            var route = new TowerAtlasRoute { target = targetId ?? "" };
            var run = Run; var map = Overworld; var web = Web;
            if (run == null || map == null) { route.error = "No grid map."; return route; }
            var target = map.Poi(targetId);
            if (target == null) { route.error = "Unknown place."; return route; }
            string from = PartyNode;
            if (from == null) { route.error = "The party is lost."; return route; }
            if (from == targetId) { route.hops.Add(from); route.path.Add(new Vector2Int(target.x, target.y)); return route; }
            int state = NodeState(targetId);
            if (state == NodeHidden) { route.error = "Hidden in the fog."; return route; }
            if (state < NodeOpen) { route.error = "Out of reach: complete a place linked to it first."; return route; }
            // Dijkstra over the places (a dozen or so): only the start and completed places may be passed through.
            var dist = new Dictionary<string, float> { { from, 0 } };
            var prev = new Dictionary<string, string>();
            var done = new HashSet<string>();
            while (true)
            {
                string cur = null; float best = float.MaxValue;
                foreach (var pair in dist)
                    if (!done.Contains(pair.Key) && (pair.Value < best || (pair.Value == best && string.CompareOrdinal(pair.Key, cur) < 0)))
                    { best = pair.Value; cur = pair.Key; }
                if (cur == null || cur == targetId) break;
                done.Add(cur);
                if (cur != from && !NodeComplete(cur)) continue;
                foreach (var e in web.EdgesOf(cur))
                {
                    string next = e.Other(cur);
                    float d = best + e.length;
                    float old;
                    if (!dist.TryGetValue(next, out old) || d < old) { dist[next] = d; prev[next] = cur; }
                }
            }
            if (!prev.ContainsKey(targetId)) { route.error = "No known way there."; return route; }
            for (string at = targetId; at != null; at = at == from ? null : prev[at]) route.hops.Insert(0, at);
            route.path.Add(new Vector2Int(map.Poi(from).x, map.Poi(from).y));
            for (int h = 1; h < route.hops.Count; h++)
            {
                var cells = web.Edge(route.hops[h - 1], route.hops[h]).From(route.hops[h - 1]);
                for (int i = 1; i < cells.Count; i++) route.path.Add(cells[i]);
            }
            var road = RoadTest();
            bool night = !IsDaylight(RunHour());
            float threat = 0, weather = WeatherThreatScale;
            route.cost = map.PathCost(route.path, road);
            for (int i = 1; i < route.path.Count; i++)
            {
                bool onRoad = road(map.Index(route.path[i].x, route.path[i].y));
                if (onRoad) route.roadCells++; else route.trailCells++;
                threat += ((onRoad ? ThreatPerRoadCell : ThreatPerTrailCell) + (night ? ThreatPerNightCell : 0)) * weather;
            }
            route.rations = route.cost * WeatherRationScale / RationCost;
            route.threat = Mathf.RoundToInt(threat);
            route.hours = route.cost * TravelSecondsPerCost / (DaySeconds / 24f);
            return route;
        }

        // Travels the whole route at once: every trail cell costs time, rations and threat and wears toward road; each
        // trail travelled may raise an event on arrival. An event, a tablet to choose or a shift stops the party at the
        // place it reached; the target is remembered so GridResume can finish the trip once the event is settled.
        public TowerGridStep AtlasTravel(string targetId)
        {
            var step = new TowerGridStep();
            var run = Run; var map = Overworld;
            if (run == null || map == null) { step.error = "No expedition."; return step; }
            if (run.dungeonPoi.Length > 0) { step.error = "Leave the dungeon first."; return step; }
            step.error = EventBlock();
            if (step.error != null) return step;
            CheckDayRoll();
            map = Overworld;
            int shiftBefore = run.shift;
            var route = AtlasRoute(targetId);
            if (route.error != null) { step.error = route.error; return step; }
            if (route.hops.Count < 2) { step.error = "You are here."; return step; }
            var target = map.Poi(targetId);
            run.targetX = target.x; run.targetY = target.y;
            step.walked.Add(route.path[0]);
            var roads = RoadTest();
            int sight = GridSightRadius;
            for (int h = 1; h < route.hops.Count; h++)
            {
                var edge = Web.Edge(route.hops[h - 1], route.hops[h]);
                var cells = edge.From(route.hops[h - 1]);
                var fog = FogBuffer(); var wear = RoadBuffer();
                int roadCells = 0;
                for (int i = 1; i < cells.Count; i++)
                {
                    var c = cells[i];
                    bool diagonal = c.x != cells[i - 1].x && c.y != cells[i - 1].y;
                    int index = map.Index(c.x, c.y);
                    bool onRoad = roads(index);
                    float cost = map.Cost(c.x, c.y, roads) * (diagonal ? 1.4142f : 1f);
                    run.cx = c.x; run.cy = c.y;
                    run.clock += cost * TravelSecondsPerCost;
                    bool night = !IsDaylight(RunHour());
                    step.walked.Add(c);
                    WearInto(wear, index);
                    RevealInto(fog, c.x, c.y, sight);
                    // Rations by distance and ground.
                    run.travelCarry += cost * WeatherRationScale;
                    while (run.travelCarry >= RationCost)
                    {
                        run.travelCarry -= RationCost;
                        if (run.rations > 0) run.rations--;
                        else
                        {
                            for (int k = 0; k < run.hp.Count; k++) if (run.hp[k] > 0) run.hp[k] = Mathf.Max(1, run.hp[k] - 10);
                            Note("No rations left: the party grows weak.");
                        }
                    }
                    run.threatCarry += ((onRoad ? ThreatPerRoadCell : ThreatPerTrailCell) + (night ? ThreatPerNightCell : 0)) * WeatherThreatScale;
                    if (onRoad) roadCells++;
                }
                run.gridRoad = new string(wear);
                CommitFog(fog);
                // Arrived at the next place on the way.
                var poi = map.Poi(route.hops[h]);
                run.cx = poi.x; run.cy = poi.y; run.at = poi.id;
                if (!run.visited.Contains(poi.id)) run.visited.Add(poi.id);
                if (!run.road.Contains(edge.key)) run.road.Add(edge.key);
                step.arrived = poi;
                if (ArriveAt(poi)) step.towerClimbed = true;
                // Threat by ground walked, settled at each place so a shift never strands the party between two.
                int raise = Mathf.FloorToInt(run.threatCarry);
                if (raise > 0) { run.threatCarry -= raise; RaiseThreat(raise); }
                if (run.shift != shiftBefore)
                {
                    // The forest moved: stop and look again (the old plan is dropped).
                    step.shifted = true;
                    step.halted = h < route.hops.Count - 1;
                    break;
                }
                RollEdgeEvent(route.hops[h], roadCells * 2 >= cells.Count - 1);
                if (EventBlock() != null && h < route.hops.Count - 1) { step.halted = true; break; }
            }
            if (!step.halted) run.targetX = run.targetY = -1;
            return step;
        }

        // The trip an event interrupted, if any.
        public bool GridHasTarget { get { var run = Run; return run != null && run.targetX >= 0 && (run.targetX != run.cx || run.targetY != run.cy); } }

        public TowerGridStep GridResume()
        {
            var run = Run; var map = Overworld;
            if (!GridHasTarget || map == null) return new TowerGridStep { error = "Nowhere to go." };
            var target = map.PoiAt(run.targetX, run.targetY);
            if (target == null) { GridCancelTarget(); return new TowerGridStep { error = "The way there is lost." }; }
            return AtlasTravel(target.id);
        }

        public void GridCancelTarget() { var run = Run; if (run != null) run.targetX = run.targetY = -1; }

        // One roll per trail travelled, on arrival: quiet into the camp and the lair, the Quiet Glade offered once on
        // reaching the lair or a place linked to it, otherwise trail 35% / road 10%, more with threat, never twice in a row.
        private void RollEdgeEvent(string toId, bool road)
        {
            var run = Run; var map = Overworld;
            run.steps++;
            if (!TraversalEvents) { run.lastStepEvent = false; return; }
            if (run.eventId.Length > 0 || AmbushPending) { run.lastStepEvent = true; return; }
            var to = map.Poi(toId); var lair = map.Lair;
            bool fired = false;
            bool byLair = lair != null && !run.cleared.Contains(lair.id) && (toId == lair.id || Web.Linked(toId, lair.id));
            if (byLair && !run.eventsSeen.Contains(TowerEvents.RestId) && TowerEvents.Get(TowerEvents.RestId) != null)
            {
                StartEvent(TowerEvents.RestId); fired = true;
            }
            else if (to.kind != "camp" && to.kind != "lair" && !run.lastStepEvent)
            {
                var rng = EventRng("event");
                float chance = (road ? EventChanceRoad : EventChanceTrail) + EventThreatBoost * run.threat / ThreatMax;
                if (rng.Value() < chance)
                {
                    var def = PickEvent(GridLayout.Node(toId), road, rng);
                    if (def != null) { StartEvent(def.id); fired = true; }
                }
            }
            run.lastStepEvent = fired;
        }

        public TowerOverworldPoi NearestPoi(int x, int y)
        {
            var map = Overworld;
            TowerOverworldPoi best = null; int bestD = int.MaxValue;
            if (map == null) return null;
            foreach (var p in map.pois)
            {
                int d = (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // Full threat on the grid: the forest wears down road that is not anchored by camp, a cleared place or the party.
        private int GridStir()
        {
            var run = Run; var map = Overworld;
            if (run.gridRoad.Length != map.cells.Length) return 0;
            var road = run.gridRoad.ToCharArray();
            int lost = 0;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    int i = map.Index(x, y);
                    if (road[i] == '0' || Anchored(x, y)) continue;
                    int before = road[i] - '0', after = Mathf.Max(0, before - RoadWornOnStir);
                    if (before >= RoadWalked && after < RoadWalked) lost++;
                    road[i] = (char)('0' + after);
                }
            run.gridRoad = new string(road);
            return lost;
        }

        private bool Anchored(int x, int y)
        {
            var run = Run; var map = Overworld;
            if ((x - run.cx) * (x - run.cx) + (y - run.cy) * (y - run.cy) <= 9) return true;
            foreach (var p in map.pois)
                if ((p.kind == "camp" || run.cleared.Contains(p.id)) && (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= 9) return true;
            return false;
        }
    }
}
