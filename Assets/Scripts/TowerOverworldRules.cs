using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Result of a planned walk on the grid map: the route and what it will cost.
    public sealed class TowerGridPreview
    {
        public List<Vector2Int> path;
        public float cost;
        public float rations;           // expected rations (fractional: road travel shares a ration)
        public int threat;
        public int roadCells, trailCells;
        public string error;
    }

    // Result of walking: the cells the party actually crossed, and whether something stopped them.
    public sealed class TowerGridStep
    {
        public List<Vector2Int> walked = new List<Vector2Int>();
        public bool halted, shifted;
        public TowerOverworldPoi arrived;
        public string error;
    }

    // Expedition rules on the layered grid map (TowerOverworld). Plate runs (mapKind "") keep the node rules: the game
    // no longer draws them (their paintings are gone), but the rules tests walk them as a small fixed graph.
    // Walking a cell wears it into road; roads cost less, raise less threat and calm the event odds.
    // A full threat meter makes the forest stir, which wears down road away from camp, cleared places and the party.
    public sealed partial class TowerRules
    {
        // New expeditions use the grid map. Rules tests switch it off to walk the plate graphs.
        public static bool GridMaps = true;

        public const string GridKind = "grid";
        public const float RationCost = 20f;            // cost units per ration (about one old trail hop)
        public const float ThreatPerTrailCell = 0.8f, ThreatPerRoadCell = 0.3f, ThreatPerNightCell = 0.4f;
        public const int EventCells = 10, RoadWalked = 3, RoadMax = 9, RoadWornOnStir = 3, GridSight = 5;
        // Time in the wild (Tower clock seconds; DaySeconds = one day): each cost unit walked is 8 minutes,
        // a fight half an hour, a camp rest sleeps until dawn at night or three hours by day.
        public const float TravelSecondsPerCost = DaySeconds / 24f * (8f / 60f), BattleSeconds = DaySeconds / 48f;

        public bool GridRun { get { var run = Run; return run != null && run.mapKind == GridKind; } }

        private TowerOverworld overworldCache;
        private TowerForestLayout overworldLayout;
        private string overworldKey = "";

        // The current run's grid map, regenerated from its seed (cached).
        public TowerOverworld Overworld
        {
            get
            {
                var run = Run;
                if (run == null || run.mapKind != GridKind) return null;
                string key = run.biome + ":" + run.gridSeed + ":" + run.shift + ":" + run.gridVersion;
                if (key != overworldKey || overworldCache == null)
                {
                    overworldCache = TowerOverworldGen.Generate(run.biome, (uint)run.gridSeed, run.shift, run.anchors, version: run.gridVersion);
                    overworldLayout = LayoutFromOverworld(overworldCache, run.layout);
                    overworldKey = key;
                }
                return overworldCache;
            }
        }

        // The grid's places seen as forest nodes, so dungeons, events and the run panel work unchanged.
        private static TowerForestLayout LayoutFromOverworld(TowerOverworld map, string id)
        {
            var layout = new TowerForestLayout { id = id, name = TowerForestLayouts.Pretty(map.biome == "edge" ? "forest_edge" : map.biome),
                backdrop = "", theme = map.pois.Count > 0 ? map.pois[0].theme : "ruin", entrance = "camp" };
            foreach (var p in map.pois)
                layout.nodes.Add(new TowerForestNode { id = p.id, kind = p.kind, name = p.name, theme = p.theme,
                    x = (p.x + 0.5f) / map.width, y = (p.y + 0.5f) / map.height });
            // Hidden neighbours: each place links to its three nearest, which scouting and the rest-before-lair rule use.
            foreach (var a in layout.nodes)
            {
                var near = new List<TowerForestNode>(layout.nodes);
                near.Remove(a);
                near.Sort((m, n) => Dist2(a, m).CompareTo(Dist2(a, n)));
                for (int i = 0; i < Mathf.Min(3, near.Count); i++) a.links.Add(near[i].id);
            }
            return layout;
        }

        private static float Dist2(TowerForestNode a, TowerForestNode b) { float dx = (a.x - b.x) * 1.6f, dy = a.y - b.y; return dx * dx + dy * dy; }

        private TowerForestLayout GridLayout { get { return Overworld == null ? null : overworldLayout; } }

        // ---------------------------------------------------------------- setup

        private void StartGridRun(TowerRun run, string region)
        {
            run.mapKind = GridKind;
            run.gridVersion = TowerOverworld.Version;
            run.biome = TowerOverworldGen.BiomeForRegion(region);
            run.gridSeed = (int)(TowerForestLayouts.Hash(region + ":grid", run.seed) & 0x7fffffff);
            run.shift = 0;
            run.layout = "grid_" + run.biome;
            var map = Overworld;
            var camp = map.Camp;
            run.cx = camp.x; run.cy = camp.y;
            run.at = camp.id;
            run.gridFog = new string('0', map.width * map.height);
            run.gridRoad = new string('0', map.width * map.height);
            run.targetX = run.targetY = -1;
            run.day = GameDay;
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

        // The woods rearrange around the party. Full (threat filled): every unexplored place moves, the woods regrow
        // and the fog closes in again away from what is known. Small (a new day): one unexplored place moves.
        // The camp, explored and cleared places, and the party's own spot never move.
        public bool ShiftForest(bool small)
        {
            var run = Run; var map = Overworld;
            if (map == null) return false;
            var keep = new List<TowerOverworldPoi>();
            var loose = new List<TowerOverworldPoi>();
            foreach (var p in map.pois)
            {
                bool fixedPlace = p.kind == "camp" || run.visited.Contains(p.id) || run.cleared.Contains(p.id) || p.id == run.at;
                (fixedPlace ? keep : loose).Add(p);
            }
            if (loose.Count == 0) return false;
            if (small)
            {
                // Move one place the party has not seen yet if there is one, else any unexplored place.
                var pick = loose.Find(p => !CellSeen(p.x, p.y)) ?? loose[EventRng("dayshift").Next(loose.Count)];
                foreach (var p in loose) if (p != pick) keep.Add(p);
            }
            run.anchors = new List<TowerOverworldPoi>();
            foreach (var p in keep)
                run.anchors.Add(new TowerOverworldPoi { id = p.id, kind = p.kind, name = p.name, theme = p.theme, x = p.x, y = p.y });
            run.shift++;
            run.targetX = run.targetY = -1;
            var moved = Overworld;     // regenerated with the new shift
            if (!small)
            {
                // The fog closes in again except around the party, the anchored places and well-worn road.
                run.gridFog = new string('0', moved.cells.Length);
                RevealCells(run.cx, run.cy, GridSightRadius);
                foreach (var p in run.anchors) RevealCells(p.x, p.y, 2);
                for (int y = 0; y < moved.height; y++)
                    for (int x = 0; x < moved.width; x++)
                        if (RoadStrength(x, y) >= RoadWalked) RevealCells(x, y, 1);
            }
            run.revealed.RemoveAll(id => moved.Poi(id) == null);
            foreach (var p in moved.pois) if (CellSeen(p.x, p.y) && !run.revealed.Contains(p.id)) run.revealed.Add(p.id);
            var here = moved.PoiAt(run.cx, run.cy);
            run.at = here != null ? here.id : "";
            Note(small ? "Overnight the trees have moved: one of the paths ahead is not where it was." :
                "The forest shifts! Trees turn and close in; unexplored places are somewhere else now.");
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
            var run = Run; var map = Overworld;
            if (map == null) return;
            if (run.gridFog.Length != map.cells.Length) run.gridFog = new string('0', map.cells.Length);
            var fog = run.gridFog.ToCharArray();
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (map.Inside(x, y) && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius + radius) fog[map.Index(x, y)] = '1';
            run.gridFog = new string(fog);
            foreach (var p in map.pois)
                if (fog[map.Index(p.x, p.y)] != '0' && !run.revealed.Contains(p.id)) run.revealed.Add(p.id);
        }

        private void Wear(int x, int y)
        {
            var run = Run; var map = Overworld;
            if (run.gridRoad.Length != map.cells.Length) run.gridRoad = new string('0', map.cells.Length);
            var road = run.gridRoad.ToCharArray();
            int i = map.Index(x, y);
            road[i] = (char)('0' + Mathf.Min(RoadMax, road[i] - '0' + RoadWalked));
            run.gridRoad = new string(road);
        }

        // ---------------------------------------------------------------- walking

        public TowerGridPreview GridPreview(int tx, int ty)
        {
            var run = Run; var map = Overworld;
            var preview = new TowerGridPreview();
            if (map == null) { preview.error = "No grid map."; return preview; }
            if (!map.Inside(tx, ty)) { preview.error = "Off the map."; return preview; }
            if (!CellSeen(tx, ty)) { preview.error = "You can't see a way there yet."; return preview; }
            if (!map.Walkable(tx, ty)) { preview.error = "No way through there."; return preview; }
            var road = RoadTest();
            preview.path = map.FindPath(new Vector2Int(run.cx, run.cy), new Vector2Int(tx, ty), road);
            if (preview.path == null) { preview.error = "No way through there."; return preview; }
            preview.cost = map.PathCost(preview.path, road);
            bool night = !IsDaylight(RunHour());
            float threat = 0, weather = WeatherThreatScale;
            for (int i = 1; i < preview.path.Count; i++)
            {
                bool onRoad = road(map.Index(preview.path[i].x, preview.path[i].y));
                if (onRoad) preview.roadCells++; else preview.trailCells++;
                threat += ((onRoad ? ThreatPerRoadCell : ThreatPerTrailCell) + (night ? ThreatPerNightCell : 0)) * weather;
            }
            preview.rations = preview.cost * WeatherRationScale / RationCost;
            preview.threat = Mathf.RoundToInt(threat);
            return preview;
        }

        // Walks toward a cell, one cell at a time. Stops early when an event or ambush breaks out;
        // the target is remembered so GridResume can finish the walk once it is settled.
        public TowerGridStep GridMove(int tx, int ty)
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
            var preview = GridPreview(tx, ty);
            if (preview.error != null) { step.error = preview.error; return step; }
            run.targetX = tx; run.targetY = ty;
            step.walked.Add(new Vector2Int(run.cx, run.cy));
            int eventRoad = 0, eventCells = 0;
            var roads = RoadTest();
            for (int i = 1; i < preview.path.Count; i++)
            {
                var c = preview.path[i];
                bool diagonal = c.x != run.cx && c.y != run.cy;
                bool onRoad = roads(map.Index(c.x, c.y));
                float cost = map.Cost(c.x, c.y, roads) * (diagonal ? 1.4142f : 1f);
                run.cx = c.x; run.cy = c.y;
                run.clock += cost * TravelSecondsPerCost;
                bool night = !IsDaylight(RunHour());
                step.walked.Add(c);
                Wear(c.x, c.y);
                RevealCells(c.x, c.y, GridSightRadius);
                // Rations by distance and ground.
                run.travelCarry += cost * WeatherRationScale;
                while (run.travelCarry >= RationCost)
                {
                    run.travelCarry -= RationCost;
                    if (run.rations > 0) run.rations--;
                    else
                    {
                        for (int h = 0; h < run.hp.Count; h++) if (run.hp[h] > 0) run.hp[h] = Mathf.Max(1, run.hp[h] - 10);
                        Note("No rations left: the party grows weak.");
                    }
                }
                // Threat by ground walked.
                run.threatCarry += ((onRoad ? ThreatPerRoadCell : ThreatPerTrailCell) + (night ? ThreatPerNightCell : 0)) * WeatherThreatScale;
                int raise = Mathf.FloorToInt(run.threatCarry);
                if (raise > 0) { run.threatCarry -= raise; RaiseThreat(raise); }
                // Something on the way, every few cells.
                eventCells++; if (onRoad) eventRoad++;
                var lair = Overworld.Lair;
                bool restDue = TraversalEvents && Near(lair, 9) && !Near(lair, 3) && !run.eventsSeen.Contains(TowerEvents.RestId);
                if (eventCells >= EventCells || i == preview.path.Count - 1 || restDue)
                {
                    RollGridEvent(eventRoad * 2 >= eventCells);
                    eventCells = eventRoad = 0;
                }
                if (run.shift != shiftBefore)
                {
                    // The forest moved under the party's feet: stop and look again (the target is kept).
                    step.halted = step.shifted = true;
                    break;
                }
                if (EventBlock() != null)
                {
                    step.halted = i < preview.path.Count - 1;
                    break;
                }
            }
            var poi = Overworld.PoiAt(run.cx, run.cy);
            run.at = poi != null ? poi.id : "";
            if (poi != null)
            {
                step.arrived = poi;
                if (!run.visited.Contains(poi.id)) run.visited.Add(poi.id);
            }
            if (!step.halted) run.targetX = run.targetY = -1;
            return step;
        }

        // The walk an event interrupted, if any.
        public bool GridHasTarget { get { var run = Run; return run != null && run.targetX >= 0 && (run.targetX != run.cx || run.targetY != run.cy); } }

        public TowerGridStep GridResume()
        {
            var run = Run;
            if (!GridHasTarget) return new TowerGridStep { error = "Nowhere to go." };
            return GridMove(run.targetX, run.targetY);
        }

        public void GridCancelTarget() { var run = Run; if (run != null) run.targetX = run.targetY = -1; }

        // Like RollTraversalEvent, but per stretch of cells: quiet near camp and lair, a rest offered near the lair,
        // otherwise trail 35% / road 10%, more with threat, never twice in a row.
        private void RollGridEvent(bool road)
        {
            var run = Run; var map = Overworld;
            run.steps++;
            if (!TraversalEvents) { run.lastStepEvent = false; return; }
            if (run.eventId.Length > 0 || AmbushPending) { run.lastStepEvent = true; return; }
            var near = NearestPoi(run.cx, run.cy);
            bool fired = false;
            var camp = map.Camp; var lair = map.Lair;
            bool quiet = Near(camp, 3) || Near(lair, 3);
            if (!quiet)
            {
                if (Near(lair, 9) && !run.eventsSeen.Contains(TowerEvents.RestId) && TowerEvents.Get(TowerEvents.RestId) != null)
                {
                    StartEvent(TowerEvents.RestId); fired = true;
                }
                else if (!run.lastStepEvent && near != null)
                {
                    var rng = EventRng("event");
                    float chance = (road ? EventChanceRoad : EventChanceTrail) + EventThreatBoost * run.threat / ThreatMax;
                    if (rng.Value() < chance)
                    {
                        var def = PickEvent(GridLayout.Node(near.id), road, rng);
                        if (def != null) { StartEvent(def.id); fired = true; }
                    }
                }
            }
            run.lastStepEvent = fired;
        }

        private bool Near(TowerOverworldPoi p, int cells)
        {
            var run = Run;
            return p != null && (p.x - run.cx) * (p.x - run.cx) + (p.y - run.cy) * (p.y - run.cy) <= cells * cells;
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
