using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerSimulationTests
{
    // Most scenarios lay out a tower in one go; the construction tests switch timing back on.
    // Traversal events stay off unless a test is about them, so forest walks are predictable.
    // Plate-map runs unless a test asks for the grid map: the plate graphs are small and fixed, so walks are predictable.
    // TearDown puts the game's default (grid) back, since statics outlive the test run.
    [SetUp] public void InstantBuilds() { TowerRules.InstantConstruction = true; TowerRules.TraversalEvents = false; TowerRules.GridMaps = false; TowerRules.Weathers = false; }
    [TearDown] public void TimedBuilds()
    {
        TowerRules.InstantConstruction = false; TowerRules.TraversalEvents = true; TowerRules.GridMaps = true; TowerRules.Weathers = true;
        TowerRules.Today = () => System.DateTime.Now.Date;
        TowerRules.NowUnix = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static TowerRules Expedition()
    {
        var rules = Started();
        rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        rules.State.food = rules.State.water = 500;
        rules.State.firewood = 200;
        rules.State.tonics = 4;
        Assert.IsNull(rules.StartExpedition("silverbrook_edge",
            new System.Collections.Generic.List<string> { "kaela", "ghislaine", "elara" }, 6, 2, 3));
        return rules;
    }

    [Test]
    public void ForestLayoutsAreFixedAndConnected()
    {
        Assert.AreEqual(15, TowerForestLayouts.Ids.Length);
        foreach (var id in TowerForestLayouts.Ids)
        {
            var layout = TowerForestLayouts.Get(id);
            TowerForestLayouts.ClearCache();
            var again = TowerForestLayouts.Get(id);
            Assert.AreEqual(JsonUtility.ToJson(layout), JsonUtility.ToJson(again), id + " must be identical every time");
            int landmarks = layout.nodes.FindAll(n => n.kind == "landmark").Count;
            int pois = layout.nodes.FindAll(n => n.kind != "landmark" && n.kind != "camp").Count;
            Assert.GreaterOrEqual(landmarks, 3, id);
            Assert.GreaterOrEqual(pois, 8, id);
            Assert.AreEqual(1, layout.nodes.FindAll(n => n.kind == "lair").Count, id);
            // Every node is reachable from the camp.
            var seen = new System.Collections.Generic.HashSet<string> { layout.entrance };
            var queue = new System.Collections.Generic.Queue<string>(); queue.Enqueue(layout.entrance);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (var n in layout.nodes) if (!seen.Contains(n.id) && layout.Linked(at, n.id)) { seen.Add(n.id); queue.Enqueue(n.id); }
            }
            Assert.AreEqual(layout.nodes.Count, seen.Count, id + " has unreachable nodes");
            foreach (var n in layout.nodes)
                Assert.IsTrue(new[] { "ruin", "cave", "marsh", "crystal", "briar", "keep" }.Contains(n.theme), id + " theme");
        }
    }

    [Test]
    public void OverworldIsDeterministicPerSeed()
    {
        foreach (var biome in TowerOverworldGen.Biomes)
        {
            var a = TowerOverworldGen.Generate(biome.id, 12345);
            var b = TowerOverworldGen.Generate(biome.id, 12345);
            var c = TowerOverworldGen.Generate(biome.id, 54321);
            Assert.AreEqual(a.Hash(), b.Hash(), biome.id + " same seed, same map");
            Assert.AreNotEqual(a.Hash(), c.Hash(), biome.id + " new seed, new map");
        }
    }

    [Test]
    public void OverworldPlacesAreAllReachable()
    {
        var themes = new[] { "ruin", "cave", "marsh", "crystal", "briar", "keep", "mine", "blight", "heartwood" };
        foreach (var biome in TowerOverworldGen.Biomes)
            for (uint seed = 1; seed <= 40; seed++)
            {
                var map = TowerOverworldGen.Generate(biome.id, seed * 7919);
                string tag = biome.id + " seed " + seed;
                Assert.AreEqual(1, map.pois.FindAll(p => p.kind == "camp").Count, tag);
                Assert.AreEqual(1, map.pois.FindAll(p => p.kind == "lair").Count, tag);
                Assert.GreaterOrEqual(map.pois.Count, 11, tag + " enough places");
                var camp = map.Camp;
                var start = new Vector2Int(map.entranceX, map.height - 1);
                Assert.IsNotNull(map.FindPath(start, new Vector2Int(camp.x, camp.y)), tag + " entrance to camp");
                foreach (var p in map.pois)
                {
                    Assert.IsTrue(map.Walkable(p.x, p.y), tag + " " + p.id + " stands on open ground");
                    Assert.IsNotNull(map.FindPath(new Vector2Int(camp.x, camp.y), new Vector2Int(p.x, p.y)), tag + " camp to " + p.id);
                    Assert.IsTrue(themes.Contains(p.theme), tag + " theme " + p.theme);
                }
                // The lair is the far end of the map from camp.
                var lair = map.Lair;
                foreach (var p in map.pois)
                    Assert.LessOrEqual((new Vector2Int(p.x, p.y) - new Vector2Int(camp.x, camp.y)).sqrMagnitude,
                        (new Vector2Int(lair.x, lair.y) - new Vector2Int(camp.x, camp.y)).sqrMagnitude, tag);
            }
    }

    private static TowerRules GridExpedition()
    {
        TowerRules.GridMaps = true;
        try { return Expedition(); }
        finally { TowerRules.GridMaps = false; }
    }

    private static TowerOverworldPoi NearestOther(TowerRules rules)
    {
        var map = rules.Overworld; var camp = map.Camp;
        TowerOverworldPoi best = null;
        foreach (var p in map.pois)
            if (p.kind != "camp" && p.kind != "lair" && (best == null ||
                (p.x - camp.x) * (p.x - camp.x) + (p.y - camp.y) * (p.y - camp.y) < (best.x - camp.x) * (best.x - camp.x) + (best.y - camp.y) * (best.y - camp.y)))
                best = p;
        return best;
    }

    [Test]
    public void GridRunStartsAtCampInFog()
    {
        var rules = GridExpedition();
        var run = rules.Run; var map = rules.Overworld;
        Assert.AreEqual(TowerRules.GridKind, run.mapKind);
        Assert.AreEqual("camp", run.at);
        Assert.AreEqual(map.Camp.x, run.cx);
        Assert.IsTrue(rules.CellSeen(run.cx, run.cy));
        Assert.IsFalse(rules.CellSeen(map.Lair.x, map.Lair.y), "the lair starts hidden");
        Assert.IsTrue(rules.NodeVisible("camp"));
        Assert.IsNotNull(rules.RunLayout.Node(map.Lair.id), "grid places act as forest nodes");
        // Unseen ground cannot be targeted.
        Assert.IsNotNull(rules.GridPreview(map.Lair.x, map.Lair.y).error);
    }

    [Test]
    public void GridPreviewMatchesWhatTheWalkCosts()
    {
        var rules = GridExpedition();
        var run = rules.Run;
        run.gridFog = new string('1', rules.Overworld.cells.Length);
        var target = NearestOther(rules);
        var preview = rules.GridPreview(target.x, target.y);
        Assert.IsNull(preview.error);
        int rations = run.rations; float carry = run.travelCarry;
        var step = rules.GridMove(target.x, target.y);
        Assert.IsNull(step.error);
        Assert.IsFalse(step.halted);
        Assert.AreEqual(target.id, run.at, "arrived at the place");
        Assert.AreEqual(target, step.arrived);
        float spent = (rations - run.rations) + (run.travelCarry - carry) / TowerRules.RationCost;
        Assert.AreEqual(preview.rations, spent, 0.01f, "preview cost equals what the walk charged");
        Assert.Greater(run.threat, 0);
        Assert.IsNull(rules.EnterPoi(), "a grid place opens its dungeon");
    }

    [Test]
    public void GridWalkingWearsARoad()
    {
        var rules = GridExpedition();
        var run = rules.Run; var map = rules.Overworld;
        run.gridFog = new string('1', map.cells.Length);
        var target = NearestOther(rules);
        var camp = map.Camp;
        var first = rules.GridPreview(target.x, target.y);
        rules.GridMove(target.x, target.y);
        var back = rules.GridPreview(camp.x, camp.y);
        Assert.Less(back.cost, first.cost, "the walked route is road now and costs less");
        Assert.IsTrue(rules.CellIsRoad(target.x, target.y));
        Assert.Less(back.threat, first.threat, "road raises less threat");
    }

    [Test]
    public void GridStirWearsAwayLooseRoad()
    {
        var rules = GridExpedition();
        var run = rules.Run; var map = rules.Overworld;
        run.gridFog = new string('1', map.cells.Length);
        var target = NearestOther(rules);
        var path = rules.GridPreview(target.x, target.y).path;
        rules.GridMove(target.x, target.y);
        rules.GridMove(map.Camp.x, map.Camp.y);
        var middle = path[path.Count / 2];
        bool anchoredMiddle = (middle - new Vector2Int(map.Camp.x, map.Camp.y)).sqrMagnitude <= 9;
        Assume.That(!anchoredMiddle && map.At(middle.x, middle.y) != TowerTerrain.Road, "middle of the route is loose trail");
        int before = rules.RoadStrength(middle.x, middle.y), camp = rules.RoadStrength(map.Camp.x, map.Camp.y);
        rules.RaiseThreat(TowerRules.ThreatMax);
        Assert.AreEqual(TowerRules.ThreatAfterStir, run.threat);
        Assert.Less(rules.RoadStrength(middle.x, middle.y), before, "loose road wears down");
        Assert.AreEqual(camp, rules.RoadStrength(map.Camp.x, map.Camp.y), "ground by the camp is anchored");
    }

    [Test]
    public void GridEventStopsTheWalkAndItResumes()
    {
        var rules = GridExpedition();
        var run = rules.Run; var map = rules.Overworld;
        run.gridFog = new string('1', map.cells.Length);
        var target = map.Lair;
        TowerRules.TraversalEvents = true;
        // Nearing the lair always offers a rest first, which stops the walk.
        var step = rules.GridMove(target.x, target.y);
        Assert.IsNull(step.error);
        Assert.IsTrue(step.halted, "stopped short by the event");
        Assert.AreEqual(TowerEvents.RestId, run.eventId);
        Assert.IsNotNull(rules.EventBlock());
        Assert.IsTrue(rules.GridHasTarget, "the target is remembered");
        // The halt survives a save.
        var copy = JsonUtility.FromJson<TowerRun>(JsonUtility.ToJson(run));
        Assert.AreEqual(target.x, copy.targetX);
        Assert.AreEqual(run.cx, copy.cx);
        Assert.IsNotNull(rules.GridResume().error, "cannot walk on until it is settled");
        TowerRules.TraversalEvents = false;
        run.eventId = ""; run.ambushDepth = 0;
        var rest = rules.GridResume();
        Assert.IsNull(rest.error);
        Assert.AreEqual(target.id, run.at);
        Assert.IsFalse(rules.GridHasTarget);
    }

    private static string PoiKey(TowerOverworldPoi p) { return p.id + "@" + p.x + "," + p.y + ":" + p.kind; }

    [Test]
    public void FullThreatShiftsTheForestButKeepsWhatIsKnown()
    {
        var rules = GridExpedition();
        var run = rules.Run;
        run.gridFog = new string('1', rules.Overworld.cells.Length);
        var first = NearestOther(rules);
        rules.GridMove(first.x, first.y);
        Assert.AreEqual(first.id, run.at);
        var before = rules.Overworld;
        var lair = before.Lair;
        run.threat = TowerRules.ThreatMax - 1;
        var step = rules.GridMove(before.Camp.x, before.Camp.y);
        Assert.IsTrue(step.shifted && step.halted, "the walk stops when the forest moves");
        Assert.AreEqual(1, run.shift);
        Assert.IsFalse(rules.GridHasTarget, "the old plan is dropped");
        var after = rules.Overworld;
        Assert.AreNotSame(before, after);
        // Camp and the explored place stay exactly where they were.
        Assert.AreEqual(PoiKey(before.Camp), PoiKey(after.Camp));
        Assert.AreEqual(PoiKey(first), PoiKey(after.Poi(first.id)));
        // The unexplored places moved, and every place is still reachable.
        int stayed = 0;
        foreach (var p in before.pois) if (p.kind != "camp" && p.id != first.id && after.pois.Exists(q => PoiKey(q) == PoiKey(p))) stayed++;
        Assert.AreEqual(0, stayed, "unexplored places moved");
        Assert.AreEqual(1, after.pois.FindAll(p => p.kind == "lair").Count);
        Assert.AreEqual(before.pois.Count, after.pois.Count);
        foreach (var p in after.pois)
            Assert.IsNotNull(after.FindPath(new Vector2Int(after.Camp.x, after.Camp.y), new Vector2Int(p.x, p.y)), "reachable " + p.id);
        Assert.IsFalse(rules.CellSeen(after.Lair.x, after.Lair.y), "fog closed in again");
        Assert.IsTrue(rules.CellSeen(run.cx, run.cy));
        // Same shift, same forest.
        var copy = TowerOverworldGen.Generate(run.biome, (uint)run.gridSeed, run.shift, run.anchors);
        Assert.AreEqual(after.Hash(), copy.Hash());
    }

    [Test]
    public void ANewDayMovesOneUnexploredPlace()
    {
        var rules = GridExpedition();
        var run = rules.Run;
        run.gridFog = new string('1', rules.Overworld.cells.Length);
        var before = rules.Overworld;
        var keys = before.pois.ConvertAll(PoiKey);
        rules.State.clock += TowerRules.DaySeconds;
        var target = NearestOther(rules);
        rules.GridMove(target.x, target.y);
        Assert.AreEqual(1, run.shift, "the day rolled over");
        var after = rules.Overworld;
        int moved = 0;
        foreach (var p in after.pois) if (!keys.Contains(PoiKey(p))) moved++;
        Assert.AreEqual(1, moved, "exactly one place moved");
        Assert.IsTrue(rules.CellSeen(after.Camp.x, after.Camp.y), "a small shift leaves the fog alone");
    }

    [Test]
    public void AtlasHoldsEveryRegionWithinReach()
    {
        var map = TowerAtlas.Map;
        var home = map.Poi(TowerAtlas.HomeId);
        Assert.IsNotNull(home);
        var cells = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var r in TowerRules.Regions)
        {
            var p = map.Poi(r.id);
            Assert.IsNotNull(p, r.id + " is on the Atlas");
            Assert.IsTrue(map.Walkable(p.x, p.y), r.id + " stands on open ground");
            Assert.IsTrue(cells.Add(new Vector2Int(p.x, p.y)), r.id + " has its own spot");
            Assert.IsNotNull(map.FindPath(new Vector2Int(home.x, home.y), new Vector2Int(p.x, p.y)), "home to " + r.id);
        }
        // A new guild knows only the first region; its neighbours are rumours, the deep wood is unknown.
        var rules = Started();
        var source = new TowerAtlasSource(rules);
        Assert.IsTrue(source.Seen(home.x, home.y));
        var edge = map.Poi("silverbrook_edge");
        Assert.IsTrue(source.Seen(edge.x, edge.y));
        Assert.IsTrue(source.Known(map.Poi("rootside_camp")), "the next regions glow through the fog");
        var deep = map.Poi("silverwood_d5");
        Assert.IsFalse(source.Seen(deep.x, deep.y));
        Assert.IsFalse(source.Known(deep));
        Assert.Greater(source.Road(edge.x, edge.y), 0f, "a trail leads from home to the first region");
    }

    [Test]
    public void OverworldArtLandsWhereTheMapLooksForIt()
    {
        TowerOverworldArtImporter.Kind kind;
        Assert.AreEqual("ground/ground_meadow", TowerOverworldArtImporter.Target("ground_meadow", out kind));
        Assert.AreEqual(TowerOverworldArtImporter.Kind.Tile, kind);
        Assert.AreEqual("ground/water_deep", TowerOverworldArtImporter.Target("water_deep", out kind));
        Assert.AreEqual("stamps/canopy_oak/canopy_oak_3", TowerOverworldArtImporter.Target("canopy_oak_sheet__3", out kind));
        Assert.AreEqual("stamps/canopy_dead/canopy_dead_2", TowerOverworldArtImporter.Target("canopy_deadwood_sheet__2", out kind));
        Assert.AreEqual("stamps/reed/reed_5", TowerOverworldArtImporter.Target("undergrowth_sheet__5", out kind));
        Assert.AreEqual("stamps/bush/bush_1", TowerOverworldArtImporter.Target("undergrowth_sheet__1", out kind));
        Assert.AreEqual("stamps/rock/rock_8", TowerOverworldArtImporter.Target("rocks_sheet__8", out kind));
        Assert.AreEqual("props/poi_camp", TowerOverworldArtImporter.Target("poi_camp", out kind));
        Assert.AreEqual("fx/tell_lair", TowerOverworldArtImporter.Target("tells_sheet__3", out kind));
        Assert.AreEqual("fx/cloud_tile", TowerOverworldArtImporter.Target("cloud_tile", out kind));
        Assert.IsNull(TowerOverworldArtImporter.Target("ow_style_anchor", out kind));
        // Every ground layer the shader blends has a file name the importer recognises.
        foreach (var g in TowerMapArt.Grounds) Assert.AreEqual("ground/" + g, TowerOverworldArtImporter.Target(g, out kind));
    }

    [Test]
    public void OverworldRoadsAreCheaperThanForest()
    {
        var map = new TowerOverworld(12, 3, "edge", 1);
        for (int i = 0; i < map.cells.Length; i++) map.cells[i] = TowerTerrain.Forest;
        var path = map.FindPath(new Vector2Int(0, 1), new Vector2Int(11, 1));
        float forest = map.PathCost(path);
        float worn = map.PathCost(path, i => true);
        Assert.Less(worn, forest, "walked road costs less than fresh forest");
        map.Set(5, 0, TowerTerrain.Water); map.Set(5, 1, TowerTerrain.Water); map.Set(5, 2, TowerTerrain.Water);
        Assert.IsNull(map.FindPath(new Vector2Int(0, 1), new Vector2Int(11, 1)), "a river with no ford blocks the way");
        map.Set(5, 2, TowerTerrain.Ford);
        var around = map.FindPath(new Vector2Int(0, 1), new Vector2Int(11, 1));
        Assert.IsNotNull(around);
        Assert.IsTrue(around.Contains(new Vector2Int(5, 2)), "the route uses the ford");
    }

    [Test]
    public void WalkedTrailsAreRemembered()
    {
        var rules = Expedition();
        var run = rules.Run;
        rules.RecordTrail("camp", "n1", new System.Collections.Generic.List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.2f, 0.2f) });
        rules.RecordTrail("n1", "camp", new System.Collections.Generic.List<Vector2> { new Vector2(0.2f, 0.2f), new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.2f) });
        Assert.AreEqual(1, run.trails.Count, "the same route is replaced, not duplicated");
        Assert.AreEqual(3, run.trails[0].pts.Count);
    }

    [Test]
    public void SilverwoodLayoutsLoadAndAreConnected()
    {
        Assert.AreEqual(15, TowerForestLayouts.SilverwoodIds.Length);
        for (int depth = 1; depth <= 5; depth++) Assert.AreEqual(3, TowerForestLayouts.SilverwoodDepthIds(depth).Length, "depth " + depth);
        var themes = new[] { "ruin", "cave", "marsh", "crystal", "briar", "keep", "mine", "blight", "heartwood" };
        foreach (var id in TowerForestLayouts.SilverwoodIds)
        {
            var layout = TowerForestLayouts.Get(id);
            Assert.IsNotNull(layout, id);
            Assert.AreEqual(1, layout.nodes.FindAll(n => n.kind == "lair").Count, id);
            var seen = new System.Collections.Generic.HashSet<string> { layout.entrance };
            var queue = new System.Collections.Generic.Queue<string>(); queue.Enqueue(layout.entrance);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (var n in layout.nodes) if (!seen.Contains(n.id) && layout.Linked(at, n.id)) { seen.Add(n.id); queue.Enqueue(n.id); }
            }
            Assert.AreEqual(layout.nodes.Count, seen.Count, id + " has unreachable nodes");
            foreach (var n in layout.nodes) Assert.IsTrue(themes.Contains(n.theme), id + " theme " + n.theme);
        }
        for (int depth = 1; depth <= 5; depth++) Assert.IsNotNull(TowerRules.Region("silverwood_d" + depth));
        Assert.AreEqual(3, TowerRules.SilverwoodDepth("silverwood_d3"));
        Assert.AreEqual(0, TowerRules.SilverwoodDepth("silverwood_gate"));
    }

    [Test]
    public void DungeonsAreDeterministicAndReachable()
    {
        foreach (var id in TowerForestLayouts.Ids)
            foreach (var node in TowerForestLayouts.Get(id).nodes)
            {
                if (node.kind == "camp") continue;
                for (int floor = 0; floor < TowerForestLayouts.Floors(node.kind); floor++)
                {
                    var a = TowerDungeon.Build(id, node, floor, 1234);
                    var b = TowerDungeon.Build(id, node, floor, 999);
                    CollectionAssert.AreEqual(a.cell, b.cell, "layout must not depend on the run");
                    Assert.GreaterOrEqual(a.rooms.Count, 4);
                    var dist = a.Distances(a.rooms[0].CenterX, a.rooms[0].CenterY);
                    foreach (var room in a.rooms) Assert.GreaterOrEqual(dist[TowerDungeon.Index(room.CenterX, room.CenterY)], 0);
                    bool last = floor == TowerForestLayouts.Floors(node.kind) - 1;
                    Assert.AreEqual(1, a.rooms.FindAll(r => last ? r.goal : r.kind == "stairs").Count);
                    if (node.kind == "lair" && last) Assert.IsTrue(a.rooms.Exists(r => r.goal && r.kind == "boss"));
                }
            }
    }

    [Test]
    public void ExpeditionPlanTakesProvisionsAndLeavingBanksTheHaul()
    {
        var rules = Started();
        Assert.IsNotNull(rules.CanStartExpedition("silverbrook_edge", new System.Collections.Generic.List<string> { "kaela" }, 0, 0, 0),
            "needs a guild hall");
        rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        Assert.IsNotNull(rules.CanStartExpedition("old_bridge", new System.Collections.Generic.List<string> { "kaela" }, 0, 0, 0), "locked");
        rules.State.food = rules.State.water = 100; rules.State.firewood = 50; rules.State.tonics = 3;
        Assert.IsNotNull(rules.CanStartExpedition("silverbrook_edge", new System.Collections.Generic.List<string> { "kaela" }, 50, 0, 0));
        Assert.IsNull(rules.StartExpedition("silverbrook_edge", new System.Collections.Generic.List<string> { "kaela", "elara" }, 4, 2, 2));
        Assert.AreEqual(80, rules.State.food, 0.01f);
        Assert.AreEqual(1, rules.State.tonics);
        Assert.AreEqual(30, rules.State.firewood, 0.01f);
        var run = rules.Run;
        run.haul.Add(new TowerLoot { name = "test", gold = 100 });
        int gold = rules.State.gold;
        Assert.IsNull(rules.EndExpedition(false));
        Assert.AreEqual(gold + 100, rules.State.gold);
        Assert.AreEqual(100, rules.State.food, 0.01f, "unused rations come back");
        Assert.AreEqual(3, rules.State.tonics);
        Assert.IsNull(rules.Run);
    }

    [Test]
    public void WipeKeepsOnlyThePocket()
    {
        var rules = Expedition();
        var run = rules.Run;
        run.haul.Add(new TowerLoot { name = "a", gold = 50 });
        run.haul.Add(new TowerLoot { name = "b", gold = 70 });
        run.haul.Add(new TowerLoot { name = "c", gold = 90 });
        Assert.IsNull(rules.PocketLoot(2));
        Assert.IsNull(rules.PocketLoot(0));
        Assert.IsNotNull(rules.PocketLoot(0), "only two pocket slots");
        int gold = rules.State.gold; float food = rules.State.food;
        Assert.IsNull(rules.EndExpedition(true));
        Assert.AreEqual(gold + 140, rules.State.gold);
        Assert.AreEqual(food, rules.State.food, 0.01f, "provisions are lost");
    }

    [Test]
    public void ForestTravelUsesRationsAndFog()
    {
        var rules = Expedition();
        var layout = rules.RunLayout;
        var camp = layout.Node(layout.entrance);
        Assert.IsTrue(rules.NodeVisible(camp.links[0]));
        var far = layout.nodes.Find(n => n.kind == "lair");
        Assert.IsFalse(rules.NodeVisible(far.id), "the lair starts under fog");
        Assert.IsNotNull(rules.ForestMove(far.id), "no trail");
        Assert.IsNull(rules.ForestMove(camp.links[0]));
        Assert.AreEqual(5, rules.Run.rations);
        rules.Run.rations = 0;
        Assert.IsNull(rules.ForestMove(layout.Node(camp.links[0]).links[0]), "a new trail");
        Assert.AreEqual(90, rules.Run.hp[0], "attrition without rations");
    }

    // BATTLE_MODE_GDD.md section 4: walked trails become road at half the ration cost.
    [Test]
    public void WalkedTrailsBecomeRoadAtHalfCost()
    {
        var rules = Expedition();
        rules.HeroResident("kaela").trait = "Brave";
        var layout = rules.RunLayout;
        string camp = layout.entrance, a = layout.Node(camp).links[0], b = layout.Node(a).links[0];
        Assert.IsNull(rules.ForestMove(a));
        Assert.IsNull(rules.ForestMove(b));
        Assert.AreEqual(4, rules.Run.rations, "two new trails");
        Assert.IsTrue(rules.OnRoad(camp, a) && rules.OnRoad(b, a), "walked trails are road");
        Assert.IsNull(rules.ForestMove(a));
        Assert.AreEqual(4, rules.Run.rations, "first road trip is free");
        Assert.IsNull(rules.ForestMove(b));
        Assert.AreEqual(3, rules.Run.rations, "every second road trip costs");
        Assert.AreEqual(2 * TowerRules.ThreatNewTrail + 2 * TowerRules.ThreatRoad, rules.Run.threat, "daylight threat");
    }

    [Test]
    public void FogRevealGrowsWithScoutAndGuild()
    {
        var started = Expedition();
        started.HeroResident("kaela").trait = "Brave";
        started.Run.revealed.Clear();
        // Reloading rebuilds the fog with the trait fixed, whatever the starter rolled.
        var rules = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(started.State)));
        var kaela = rules.HeroResident("kaela");
        var layout = rules.RunLayout;
        string a = layout.Node(layout.entrance).links[0], b = layout.Node(a).links[0];
        Assert.AreEqual(1, rules.RevealRadius);
        Assert.IsTrue(rules.NodeVisible(a));
        Assert.IsFalse(rules.NodeVisible(b), "two hops away is fogged");
        kaela.trait = "Curious";
        Assert.AreEqual(2, rules.RevealRadius, "a curious scout sees further");
        rules.State.clock = TowerRules.DaySeconds * 0.6f;
        Assert.AreEqual(1, rules.RevealRadius, "the scout loses the edge at night");
        rules.State.clock = 0;
        Assert.IsNull(rules.ForestMove(a));
        Assert.IsTrue(rules.NodeVisible(layout.Node(b).links[0]), "two hops from the party");
        rules.State.rooms.Find(r => r.type == "guild_hall").level = 3;
        Assert.AreEqual(3, rules.RevealRadius, "guild maps add a hop");
    }

    [Test]
    public void FullThreatMakesTheForestSwallowLooseRoad()
    {
        var rules = Expedition();
        var layout = rules.RunLayout;
        string camp = layout.entrance, a = layout.Node(camp).links[0], b = layout.Node(a).links[0],
            c = layout.Node(b).links[0], d = layout.Node(c).links[0];
        foreach (var step in new[] { a, b, c, d }) Assert.IsNull(rules.ForestMove(step));
        rules.Run.threat = TowerRules.ThreatMax - 1;
        Assert.IsNull(rules.ForestMove(c));
        Assert.AreEqual(TowerRules.ThreatAfterStir, rules.Run.threat);
        Assert.IsTrue(rules.OnRoad(camp, a), "road from the camp holds");
        Assert.IsTrue(rules.OnRoad(c, d) && rules.OnRoad(b, c), "road at the party holds");
        Assert.IsFalse(rules.OnRoad(a, b), "loose road is swallowed");
        Assert.IsTrue(rules.State.log.Exists(l => l.Contains("forest stirs")));
        int threat = rules.Run.threat;
        Assert.IsNull(rules.ForestMove(b));
        Assert.IsNull(rules.ForestMove(a));
        Assert.IsNull(rules.ForestMove(camp));
        threat = rules.Run.threat;
        Assert.IsNull(rules.CampRest());
        Assert.AreEqual(System.Math.Max(0, threat - TowerRules.ThreatCampRest), rules.Run.threat, "rest calms the forest");
    }

    [Test]
    public void OldSavesRebuildForestFog()
    {
        var rules = Expedition();
        var layout = rules.RunLayout;
        string a = layout.Node(layout.entrance).links[0];
        Assert.IsNull(rules.ForestMove(a));
        rules.Run.revealed.Clear();
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.IsTrue(loaded.NodeVisible(a) && loaded.NodeVisible(layout.Node(a).links[0]), "fog is rebuilt from visited places");
        Assert.AreEqual(rules.Run.threat, loaded.Run.threat, "threat survives a save");
        Assert.IsTrue(loaded.OnRoad(layout.entrance, a), "roads survive a save");
    }

    // ---- Traversal events (BATTLE_MODE_GDD.md section 5, roadmap P2) -------------------------------

    // Answers whatever the forest throws at the party: first affordable choice, ambushes are won.
    private static void SettleEvents(TowerRules rules)
    {
        for (int guard = 0; guard < 10 && rules.Run != null; guard++)
        {
            if (rules.AmbushPending) { Assert.IsNull(rules.ResolveAmbush(true, null)); continue; }
            var def = rules.PendingEvent;
            if (def == null) break;
            int pick = def.choices.FindIndex(c => rules.ChoiceBlocked(c) == null);
            Assert.GreaterOrEqual(pick, 0, def.id + " has no affordable choice");
            Assert.IsNull(rules.ResolveEvent(pick));
        }
        if (rules.Run != null) rules.DismissEventResult();
    }

    [Test]
    public void TraversalEventsAreWellFormed()
    {
        TowerEvents.ClearCache();
        var all = TowerEvents.All;
        Assert.GreaterOrEqual(all.FindAll(e => e.weight > 0).Count, 20, "launch budget: 20 rolled events");
        Assert.IsNotNull(TowerEvents.Get(TowerEvents.AmbushId));
        Assert.IsNotNull(TowerEvents.Get(TowerEvents.RestId));
        var themes = new[] { "ruin", "cave", "marsh", "crystal", "briar", "keep", "mine", "heartwood", "blight" };   // the grid map adds the last three
        foreach (var e in all)
        {
            Assert.IsNotEmpty(e.title, e.id); Assert.IsNotEmpty(e.text, e.id);
            Assert.Contains(e.time, new[] { "any", "day", "night" }, e.id);
            foreach (var t in e.themes.Split(',')) if (t.Length > 0) Assert.Contains(t.Trim(), themes, e.id);
            int outcomes = 0;
            foreach (var c in e.choices)
            {
                Assert.IsNotEmpty(c.label, e.id);
                Assert.IsFalse(c.success.Empty, e.id + " / " + c.label + " needs a success outcome");
                if (c.trait.Length > 0) Assert.Contains(c.trait, TowerRules.Traits, e.id + " trait");
                if (c.chance < 1f) Assert.IsFalse(c.failure.Empty && c.partial.Empty, e.id + " / " + c.label + " can fail");
                outcomes += 1 + (c.partial.Empty ? 0 : 1) + (c.failure.Empty ? 0 : 1);
            }
            Assert.GreaterOrEqual(outcomes, 2, e.id + " needs 2+ outcomes");
        }
    }

    [Test]
    public void TraversalEventsFireAndAreSeeded()
    {
        TowerRules.TraversalEvents = true;
        var first = Expedition();
        var second = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(first.State)));
        foreach (var rules in new[] { first, second })
        {
            var layout = rules.RunLayout;
            // Walk the shortest trail toward the lair; the place next to it always offers the quiet glade.
            var lair = layout.nodes.Find(x => x.kind == "lair");
            var prev = new System.Collections.Generic.Dictionary<string, string> { { layout.entrance, null } };
            var queue = new System.Collections.Generic.Queue<string>(); queue.Enqueue(layout.entrance);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                foreach (var x in layout.nodes)
                    if (x.kind != "lair" && !prev.ContainsKey(x.id) && layout.Linked(cur, x.id)) { prev[x.id] = cur; queue.Enqueue(x.id); }
            }
            string last = layout.nodes.Find(x => prev.ContainsKey(x.id) && layout.Linked(x.id, lair.id)).id;
            var path = new System.Collections.Generic.List<string>();
            for (string x = last; x != layout.entrance; x = prev[x]) path.Insert(0, x);
            foreach (string at in path)
            {
                Assert.IsNull(rules.ForestMove(at));
                if (rules.PendingEvent != null || rules.AmbushPending)
                {
                    Assert.IsNotNull(rules.ForestMove(layout.entrance), "events block travel until answered");
                    SettleEvents(rules);
                }
            }
            Assert.Contains(TowerEvents.RestId, rules.Run.eventsSeen, "a rest is offered before the lair");
        }
        CollectionAssert.AreEqual(first.Run.eventsSeen, second.Run.eventsSeen, "same seed, same events");
        CollectionAssert.AreEqual(first.Run.hp, second.Run.hp);
    }

    [Test]
    public void EventChoicesPayCostsAndTraitsImproveOdds()
    {
        var rules = Expedition();
        rules.HeroResident("kaela").trait = "Brave";
        var run = rules.Run;
        run.eventId = "social_hermit_herbalist";
        var def = rules.PendingEvent;
        var help = def.choices[0]; var barter = def.choices[1];
        Assert.AreEqual(0.7f, rules.ChoiceChance(help), 0.001f);
        rules.HeroResident("kaela").trait = "Kind";
        Assert.AreEqual(1f, rules.ChoiceChance(help), 0.001f, "a Kind hero helps");
        Assert.IsNotNull(rules.ChoiceBlocked(barter), "no gold in the haul");
        Assert.IsNotNull(rules.ResolveEvent(1));
        run.haul.Add(new TowerLoot { name = "coins", gold = 30 });
        int tonics = run.tonics;
        Assert.IsNull(rules.ResolveEvent(1));
        Assert.AreEqual(tonics + 1, run.tonics);
        Assert.AreEqual(5, run.haul[0].gold, "25 gold paid from the haul");
        Assert.IsNull(rules.PendingEvent);
        Assert.IsNotEmpty(run.eventResult);
        Assert.IsNull(rules.DismissEventResult());
        Assert.IsEmpty(run.eventResult);
    }

    [Test]
    public void AmbushOutcomesMustBeFought()
    {
        var rules = Expedition();
        var run = rules.Run;
        run.eventId = TowerEvents.AmbushId;
        Assert.IsNull(rules.ResolveEvent(0), "fight");
        Assert.IsTrue(rules.AmbushPending);
        var layout = rules.RunLayout;
        Assert.IsNotNull(rules.ForestMove(layout.Node(layout.entrance).links[0]), "no travel mid-ambush");
        Assert.IsNotNull(rules.CampRest(), "no rest mid-ambush");
        int haul = run.haul.Count, won = run.battlesWon;
        Assert.IsNull(rules.ResolveAmbush(true, null));
        Assert.IsFalse(rules.AmbushPending);
        Assert.AreEqual(haul + 1, run.haul.Count, "ambush spoils");
        Assert.AreEqual(won + 1, run.battlesWon);
        run.eventId = TowerEvents.AmbushId;
        Assert.IsNull(rules.ResolveEvent(0));
        var dead = new System.Collections.Generic.List<int>();
        foreach (var unused in run.party) dead.Add(0);
        Assert.IsNull(rules.ResolveAmbush(false, dead));
        Assert.IsNull(rules.Run, "a wiped party ends the expedition");
    }

    [Test]
    public void FullThreatBringsAnAmbushCard()
    {
        TowerRules.TraversalEvents = true;
        var rules = Expedition();
        var layout = rules.RunLayout;
        string a = layout.Node(layout.entrance).links[0];
        Assert.IsNull(rules.ForestMove(a));
        SettleEvents(rules);
        rules.Run.threat = TowerRules.ThreatMax - 1;
        Assert.IsNull(rules.ForestMove(layout.entrance));
        Assert.AreEqual(TowerEvents.AmbushId, rules.Run.eventId, "full threat: the forest closes in");
        Assert.AreEqual(TowerRules.ThreatAfterStir, rules.Run.threat);
    }

    [Test]
    public void DungeonCrawlRevealsFogAndClearsThePoi()
    {
        var rules = Expedition();
        var layout = rules.RunLayout;
        var poi = layout.Node(layout.Node(layout.entrance).links[0]);
        Assert.IsNull(rules.ForestMove(poi.id));
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        Assert.IsNotNull(d);
        int hidden = rules.Run.fog.Split('0').Length - 1;
        Assert.Greater(hidden, 200, "most of the map starts fogged");
        // Walk every floor: always head for the nearest revealed unexplored walkable cell, resolve rooms as found.
        for (int guard = 0; guard < 2000 && rules.Run != null && rules.Run.dungeonPoi.Length > 0; guard++)
        {
            if (rules.RewardPending) { Assert.IsNull(rules.ChooseReward(0)); continue; }   // a won fight offers a reward
            int pending = rules.PendingRoom;
            if (pending >= 0)
            {
                string kind = rules.Dungeon.rooms[pending].kind;
                if (TowerRules.BattleRoom(kind)) Assert.IsNull(rules.ResolveBattle(true, new System.Collections.Generic.List<int> { 90, 90, 90 }));
                else if (kind == "stairs") Assert.IsNull(rules.ResolveRoom("descend"));
                else if (kind == "merchant") Assert.IsNull(rules.ResolveRoom("leave"));
                else Assert.IsNull(rules.ResolveRoom("investigate"));
                continue;
            }
            d = rules.Dungeon;
            int target = -1, best = int.MaxValue;
            var dist = d.Distances(rules.Run.px, rules.Run.py);
            for (int i = 0; i < dist.Length; i++)
            {
                int x = i % TowerDungeon.Size, y = i / TowerDungeon.Size;
                if (dist[i] <= 0 || !rules.Revealed(x, y)) continue;
                bool frontier = false;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (TowerDungeon.Inside(nx, ny) && !rules.Revealed(nx, ny)) frontier = true;
                }
                int r = d.RoomAt(x, y);
                bool undone = r >= 0 && !rules.Run.roomsDone.Contains(r);
                if ((frontier || undone) && dist[i] < best) { best = dist[i]; target = i; }
            }
            Assert.GreaterOrEqual(target, 0, "the crawl got stuck");
            Assert.IsNull(rules.DungeonMove(target % TowerDungeon.Size, target / TowerDungeon.Size));
        }
        Assert.IsTrue(rules.Run.cleared.Contains(poi.id));
        Assert.AreEqual("", rules.Run.dungeonPoi);
        Assert.Greater(rules.Run.haul.Count, 0);
    }

    [Test]
    public void ClearingTheLairConquersAndUnlocksRegions()
    {
        var rules = Expedition();
        var layout = rules.RunLayout;
        var lair = layout.nodes.Find(n => n.kind == "lair");
        rules.Run.at = lair.id;
        Assert.IsNull(rules.EnterPoi());
        for (int floor = 0; floor < 3; floor++)
        {
            var d = rules.Dungeon;
            var goal = d.rooms.Find(r => r.goal || r.kind == "stairs");
            rules.Run.px = goal.CenterX; rules.Run.py = goal.CenterY;
            if (goal.kind == "stairs") Assert.IsNull(rules.ResolveRoom("descend"));
            else Assert.IsNull(rules.ResolveBattle(true, null));
        }
        Assert.IsTrue(rules.RegionConquered("silverbrook_edge"));
        Assert.IsFalse(rules.RegionUnlocked("rootside_camp"), "conquest alone is not enough: EXP-1 maps the region");
        Learn(rules, "EXP-1");
        Assert.IsTrue(rules.RegionUnlocked("rootside_camp"));
        Assert.IsTrue(rules.RegionUnlocked("shallow_ford"));
        Assert.IsFalse(rules.RegionUnlocked("old_bridge"));
    }

    [Test]
    public void LosingEveryoneEndsTheRunAndRunsSurviveSaving()
    {
        var rules = Expedition();
        var json = JsonUtility.ToJson(rules.State);
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(json));
        Assert.IsNotNull(loaded.Run);
        Assert.AreEqual(rules.Run.layout, loaded.Run.layout);
        var old = JsonUtility.FromJson<TowerState>("{\"schema\":2,\"introPhase\":\"complete\"}");
        var fresh = new TowerRules(old);
        Assert.IsNull(fresh.Run);
        Assert.IsTrue(fresh.RegionUnlocked("silverbrook_edge"));

        var layout = rules.RunLayout;
        var poi = layout.nodes.Find(n => n.kind == "combat");
        rules.Run.at = poi.id;
        Assert.IsNull(rules.EnterPoi());
        var goal = rules.Dungeon.rooms.Find(r => r.goal);
        rules.Run.px = goal.CenterX; rules.Run.py = goal.CenterY;
        Assert.IsNull(rules.ResolveBattle(false, new System.Collections.Generic.List<int> { 0, 0, 0 }));
        Assert.IsNull(rules.Run, "a wiped party ends the expedition");
    }

    [Test]
    public void GuildHallUnlocksExpeditions()
    {
        var rules = Started();
        Assert.IsTrue(rules.State.blueprints.Contains("guild_hall"));
        Assert.IsNotNull(rules.GuildRequired());
        var hall = rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        Assert.IsNull(rules.GuildRequired());
        hall.level = 3;
        Assert.AreEqual(3, rules.GuildLevel);
    }

    [Test]
    public void ConstructionTakesTimeAndCompletesOffline()
    {
        var rules = Started();
        TowerRules.InstantConstruction = false;
        rules.State.tutorialStep = 7;
        rules.State.celestium += 60;
        rules.State.gold += 500;
        rules.State.wood += 50;
        rules.State.stone += 50;

        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.IsNotNull(rules.ExpandFloor(0, 1), "one wing job at a time");
        Assert.IsNull(rules.OpenFloor(1));
        Assert.IsNotNull(rules.OpenFloor(1), "duplicate floor job");
        Assert.IsNull(rules.Floor(1), "floor does not exist yet");
        int east = rules.Floor(0).east;
        float wing = rules.WingWork(0, 1).remaining;
        Assert.Greater(wing, 10f);

        rules.Advance(wing - 2, true);
        Assert.AreEqual(east, rules.Floor(0).east, "still building");
        rules.Advance(3, true);
        Assert.AreEqual(east + 1, rules.Floor(0).east);
        Assert.AreEqual(TowerRules.CoreX + east + 2, rules.GateOn(1).x, "the east Gate stepped outward");

        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX + east + 1));
        var site = rules.WorkRoomAt(0, TowerRules.CoreX + east + 1);
        Assert.IsNotNull(site);
        Assert.IsNull(rules.RoomAt(0, TowerRules.CoreX + east + 1));
        Assert.IsNotNull(rules.Build("well", 0, TowerRules.CoreX + east + 1), "site is taken");

        rules.CatchUp(600);
        Assert.AreEqual(0, rules.State.works.Count);
        Assert.IsNotNull(rules.Floor(1));
        Assert.IsNotNull(rules.RoomAt(0, TowerRules.CoreX + east + 1));
    }

    private static TowerRules Started()
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.IsNull(rules.Build("house", 0, 21));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        return rules;
    }

    private static TowerRoom Kitchen(TowerRules rules)
    {
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var room = rules.RoomAt(0, 20);
        Assert.IsNotNull(room);
        Assert.IsNull(rules.Assign(rules.State.residents[0].id, room.uid));
        rules.Advance(10, true);
        return room;
    }

    [Test]
    public void StatMatchingAndPrioritiesChangeProduction()
    {
        var rules = Started();
        var room = Kitchen(rules);
        var worker = rules.State.residents[0];
        worker.grace = 2;
        float low = rules.ProductionRate(room);
        worker.grace = 9;
        float high = rules.ProductionRate(room);
        Assert.Greater(high, low);
        Assert.IsNull(rules.SetPriority(worker.id, "production", 0));
        rules.Advance(1, true);
        Assert.AreEqual(0, rules.ProductionRate(room));
    }

    [Test]
    public void RushUsesMatchingSkillAndCreatesAResult()
    {
        var rules = Started();
        var room = Kitchen(rules);
        var worker = rules.State.residents[0];
        worker.grace = 2;
        float low = rules.RushChance(room.uid);
        worker.grace = 9;
        Assert.Greater(rules.RushChance(room.uid), low);
        Assert.IsNull(rules.Rush(room.uid));
        Assert.Greater(room.rushFatigue, 0);
        Assert.IsTrue(room.ready || rules.State.incidents.Count > 0);
    }

    [Test]
    public void FamilyChildLivesAndMatures()
    {
        var rules = Started();
        Learn(rules, "CON-6");
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("nursery", 0, 20));
        var other = rules.AddResident("", "Rowan", "villager", 1);
        Assert.IsNull(rules.Assign(other.id, rules.RoomAt(0, 21).uid));
        Assert.IsNull(rules.PairFamily(rules.State.residents[0].id, other.id));
        rules.Advance(TowerRules.DaySeconds + 10, false);
        var child = rules.State.residents.Find(r => r.ageStage == 1);
        Assert.IsNotNull(child);
        Assert.AreEqual("nursery", rules.Room(child.homeRoom).type);
        Assert.IsNotNull(rules.Assign(child.id, rules.RoomAt(0, 20).uid));
        rules.Advance(2 * TowerRules.DaySeconds, false);
        Assert.AreEqual(0, child.ageStage);
    }

    [Test]
    public void FireCanBeResolvedInTheRoom()
    {
        var rules = Started();
        int home = rules.RoomAt(0, 21).uid;
        Assert.IsNull(rules.StartIncident("fire", home));
        rules.Advance(110, true);
        Assert.AreEqual(0, rules.State.incidents.Count);
        Assert.Greater(rules.State.heartHp, 0);
    }

    [Test]
    public void ExplorationReturnsDeterministicSupplies()
    {
        var rules = Started();
        var person = rules.State.residents[0];
        int wood = rules.State.wood;
        Assert.IsNull(rules.SendExploring(person.id, "supplies"));
        rules.Advance(121, false);
        Assert.Greater(person.exploreWood, 0);
        Assert.IsNull(rules.Recall(person.id));
        Assert.Greater(rules.State.wood, wood);
    }

    [Test]
    public void ShortagesLowerHealthAndMorale()
    {
        var rules = Started();
        var person = rules.State.residents[0];
        rules.State.food = rules.State.water = 0;
        person.hunger = person.thirst = 10;
        float hp = person.hp;
        rules.Advance(60, true);
        Assert.Less(person.hp, hp);
        Assert.Less(person.happiness, 70);
    }

    [Test]
    public void UnlockedHaulingCollectsReadyRoom()
    {
        var rules = Started();
        var room = Kitchen(rules);
        rules.AddResident("", "Mira", "villager", 1);
        rules.AddResident("", "Tess", "villager", 1);
        Assert.IsNull(rules.UnlockHauling());
        room.ready = true; room.progress = 1;
        float food = rules.State.food;
        rules.Advance(4, true);
        Assert.IsFalse(room.ready);
        Assert.Greater(rules.State.food, food);
    }

    [Test]
    public void ResearchAndConstructionConsumeResources()
    {
        var rules = Started();
        Assert.IsFalse(rules.State.blueprints.Contains("quarry"));
        StringAssert.Contains("CON-4", rules.ResearchBlueprint("quarry"), "buildings unlock through research now");
        int celestium = rules.State.celestium;
        Assert.IsNull(rules.StartResearch("CON-1"));
        Assert.Less(rules.State.celestium, celestium);
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        int wood = rules.State.wood, stone = rules.State.stone;
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        Assert.AreEqual(wood - rules.BuildWoodCost("kitchen"), rules.State.wood);
        Assert.AreEqual(stone - rules.BuildStoneCost("kitchen"), rules.State.stone);
    }

    [Test]
    public void GuidedOpeningTracksDecisionsAndIncidentRecovery()
    {
        var rules = Started();
        Assert.AreEqual(0, rules.State.tutorialStep);
        var room = Kitchen(rules);
        Assert.AreEqual(2, rules.State.tutorialStep);
        room.ready = true;
        Assert.IsNull(rules.Collect(room.uid));
        Assert.AreEqual(3, rules.State.tutorialStep);
        Assert.IsNull(rules.Rush(room.uid));
        Assert.AreEqual(4, rules.State.tutorialStep);
        Assert.IsNull(rules.SetPriority(rules.State.residents[0].id, "fire", 3));
        if (rules.State.incidents.Count == 0)
        {
            Assert.LessOrEqual(rules.State.eventCooldown, 20);
            rules.Advance(21, true);
        }
        Assert.Greater(rules.State.incidents.Count, 0);
        Assert.AreEqual(6, rules.State.tutorialStep);
        rules.Advance(160, true);
        Assert.AreEqual(7, rules.State.tutorialStep);
    }

    [Test]
    public void ManagedOpeningSurvivesTwentyMinutes()
    {
        var rules = Started();
        var kitchen = Kitchen(rules);
        FoundGround(rules, 4);
        Assert.IsNull(rules.Build("well", 0, 19));
        var well = rules.RoomAt(0, 19);
        rules.State.pendingVisitors = 1;
        Assert.IsNull(rules.RecruitVisitor());
        var newcomer = rules.State.residents[1];
        Assert.IsNull(rules.Assign(newcomer.id, well.uid));
        int collected = 0;
        for (int second = 0; second < 1200; second++)
        {
            if (second == 60) Assert.IsNull(rules.Rush(kitchen.uid));
            if (second == 300 && rules.State.incidents.Count == 0)
                Assert.IsNull(rules.StartIncident("fire", kitchen.uid));
            rules.Advance(1, true);
            if (kitchen.ready) { Assert.IsNull(rules.Collect(kitchen.uid)); collected++; }
            if (well.ready) Assert.IsNull(rules.Collect(well.uid));
        }
        Assert.Greater(collected, 0);
        Assert.Greater(rules.State.heartHp, 600);
        Assert.Greater(rules.State.food, 0);
        Assert.Greater(rules.State.water, 0);
        Assert.IsFalse(rules.State.defeated);
    }

    [Test]
    public void ManagedLateTowerRecoversFromShortageAndRaid()
    {
        var rules = new TowerRules(TowerMilestones.Create(10));
        var kitchen = rules.State.rooms.Find(r => r.type == "kitchen");
        rules.State.food = rules.State.water = rules.State.firewood = 30;
        rules.State.eventCooldown = 10000; // Isolate the planned raid from the event director.
        Assert.IsNull(rules.StartIncident("raiders", kitchen.uid));
        for (int second = 0; second < 900; second++) rules.Advance(1, true);
        Assert.IsFalse(rules.State.defeated);
        Assert.AreEqual(0, rules.State.incidents.Count);
        Assert.Greater(rules.State.food, 30);
        Assert.Greater(rules.State.water, 30);
        Assert.Greater(rules.State.firewood, 30);
    }

    [Test]
    public void DownedResidentCanBeRescued()
    {
        var rules = Started();
        var patient = rules.State.residents[0];
        var healer = rules.AddResident("", "Healer", "villager", 1);
        healer.wit = 8;
        Assert.IsNull(rules.Assign(healer.id, patient.homeRoom));
        patient.hp = 0; patient.downed = true; patient.injury = 60;
        rules.Advance(70, true);
        Assert.IsFalse(patient.downed);
        Assert.Greater(patient.hp, 35);
    }

    [Test]
    public void SickResidentReceivesCareAndConsumesATonic()
    {
        var rules = Started();
        var patient = rules.State.residents[0];
        var healer = rules.AddResident("", "Healer", "villager", 1);
        healer.wit = 8;
        Assert.IsNull(rules.Assign(healer.id, patient.homeRoom));
        patient.illness = 70;
        int tonics = rules.State.tonics;
        rules.Advance(60, true);
        Assert.Less(patient.illness, 30);
        Assert.Less(rules.State.tonics, tonics);
    }

    [Test]
    public void HeartDestructionEndsTheRunImmediately()
    {
        var rules = Started();
        var home = rules.RoomAt(0, 21);
        rules.State.residents[0].priorityDefense = 0;
        rules.State.heartHp = 0.2f;
        Assert.IsNull(rules.StartIncident("raiders", home.uid));
        // Raiders only wound the Heart once they have fought their way into its chamber.
        rules.State.incidents[0].roomUid = rules.State.rooms.Find(r => r.type == "heart").uid;
        rules.Advance(10, true);
        Assert.IsTrue(rules.State.defeated);
        Assert.AreEqual(0, rules.State.heartHp);
        float stoppedAt = rules.State.clock;
        rules.Advance(60, true);
        Assert.AreEqual(stoppedAt, rules.State.clock);
    }

    private static TowerRules NeedsScenario()
    {
        var rules = Started();
        var s = rules.State;
        s.food = s.water = s.firewood = 100; s.eventCooldown = 99999; s.steward = false;
        var kitchen = rules.AddRoom("kitchen", 0, 19, 1);
        rules.AddRoom("well", 0, 18, 1);
        rules.AddRoom("lumber_mill", 0, 15, 1);
        rules.Assign(s.residents[0].id, kitchen.uid);
        return rules;
    }

    [Test]
    public void PausedTimeChangesNothing()
    {
        var rules = NeedsScenario();
        var res = rules.State.residents[0];
        float clock = rules.State.clock, food = rules.State.food, hunger = res.hunger, rest = res.rest, happy = res.happiness;
        for (int i = 0; i < 3600; i++) rules.Advance(TowerRules.FrameSeconds(1f / 60f, 0f), true);
        Assert.AreEqual(clock, rules.State.clock);
        Assert.AreEqual(food, rules.State.food);
        Assert.AreEqual(hunger, res.hunger);
        Assert.AreEqual(rest, res.rest);
        Assert.AreEqual(happy, res.happiness);
    }

    [Test]
    public void GameSpeedKeepsStatsProportionalAtAnyFrameRate()
    {
        var baseline = NeedsScenario();
        baseline.Advance(120f, true);
        var b = baseline.State.residents[0];
        foreach (float speed in new[] { 1f, 2f, 4f, 8f })
            foreach (float fps in new[] { 60f, 30f, 20f, 12f })
            {
                var rules = NeedsScenario();
                float dt = 1f / fps;
                int frames = Mathf.RoundToInt(120f / speed * fps);   // 120 game seconds of real play at this speed
                for (int i = 0; i < frames; i++) rules.Advance(TowerRules.FrameSeconds(dt, speed), true);
                string label = speed + "x at " + fps + " fps";
                var r = rules.State.residents[0];
                Assert.AreEqual(120f, rules.State.clock, 0.6f, label + ": game clock");
                Assert.AreEqual(b.hunger, r.hunger, 0.8f, label + ": hunger");
                Assert.AreEqual(b.thirst, r.thirst, 0.8f, label + ": thirst");
                Assert.AreEqual(b.rest, r.rest, 0.8f, label + ": rest");
                Assert.AreEqual(baseline.State.water, rules.State.water, 0.5f, label + ": water");
                Assert.AreEqual(baseline.State.firewood, rules.State.firewood, 0.6f, label + ": firewood");
            }
    }

    [Test]
    public void RaiderLootDoesNotDependOnFrameRate()
    {
        float[] stolen = new float[3];
        for (int run = 0; run < 3; run++)
        {
            var rules = Started();
            rules.State.gold = 1000; rules.State.eventCooldown = 99999; rules.State.steward = false;
            rules.State.residents[0].priorityDefense = 0;
            Assert.IsNull(rules.StartIncident("raiders", rules.RoomAt(0, 21).uid));
            if (run == 0) rules.Advance(30f, true);
            else if (run == 1) for (int i = 0; i < 1800; i++) rules.Advance(1f / 60f, true);
            else for (int i = 0; i < 450; i++) rules.Advance(TowerRules.FrameSeconds(1f / 60f, 4f), true);
            stolen[run] = 1000 - rules.State.gold;
        }
        Assert.Greater(stolen[1], 10f, "raiders steal at 60 fps instead of rounding every tick to nothing");
        Assert.AreEqual(stolen[0], stolen[1], 2f, "one big step vs 60 fps");
        Assert.AreEqual(stolen[0], stolen[2], 2f, "one big step vs 4x speed");
    }

    [Test]
    public void RosterHasThirtySixHeroesAndTwentyFourResidents()
    {
        Assert.IsTrue(TowerRoster.Available, "tower_roster.json loads");
        int heroes = 0, residents = 0;
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var unit in TowerRoster.All)
        {
            Assert.IsTrue(ids.Add(unit.id), "unique id " + unit.id);
            Assert.GreaterOrEqual(unit.age, 21, unit.name + " must be 21+");
            foreach (int v in new[] { unit.stats.might, unit.stats.sight, unit.stats.grit, unit.stats.charm, unit.stats.wit, unit.stats.grace, unit.stats.luck })
            { Assert.GreaterOrEqual(TowerRoster.GameStat(v), 1); Assert.LessOrEqual(TowerRoster.GameStat(v), 10); }
            if (unit.IsHero) heroes++; else residents++;
        }
        Assert.AreEqual(36, heroes);
        Assert.AreEqual(24, residents);
        for (int rank = 1; rank <= 9; rank++)
        {
            Assert.AreEqual(4, TowerRoster.OfRank(rank, true).Count, "four heroes at rank " + rank);
            Assert.GreaterOrEqual(TowerRoster.OfRank(rank, false).Count, 1, "a resident at rank " + rank);
        }
    }

    [Test]
    public void SummonsDrawNamedUnitsOfTheRolledRankFromTheRoster()
    {
        var rules = Started();
        rules.State.sigils = 5000;
        int pulls = 0;
        for (int round = 0; round < 40; round++)
        {
            Assert.IsNull(rules.Summon(10));
            foreach (var pull in rules.LastSummon)
            {
                pulls++;
                var unit = TowerRoster.Unit(pull.unitId);
                Assert.IsNotNull(unit, "summoned " + pull.name + " is a roster unit");
                Assert.AreEqual(pull.rank, unit.rank, pull.name + " rank");
                Assert.AreEqual(pull.kind == "hero", unit.IsHero, pull.name + " kind");
                Assert.AreEqual(unit.name, pull.name);
            }
        }
        Assert.AreEqual(400, pulls);
        foreach (var resident in rules.State.residents)
            if (TowerRoster.Unit(resident.unitId) != null)
                Assert.AreEqual(TowerRoster.Unit(resident.unitId).rank, resident.rank, resident.name);
    }

    [Test]
    public void EachFallenHeartRaisesALegacyBonusForTheNextRun()
    {
        var rules = Started();
        rules.State.day = 40; rules.State.heartRank = 3;             // 10 + 12 = 22 points -> rank 2
        rules.State.heartHp = 0;
        rules.Advance(1, true);
        Assert.IsTrue(rules.State.defeated);
        Assert.AreEqual(22, TowerRules.LegacyEarned(rules.State));
        var next = TowerRules.LegacyRun(rules.State);
        Assert.AreEqual(22, next.legacyPoints);
        Assert.AreEqual(2, next.legacyRank);
        Assert.AreEqual(120 + 160, next.gold, "start gold + 80 per rank");
        Assert.AreEqual(8, next.celestium);
        Assert.AreEqual(2 + 2, next.tonics);
        Assert.AreEqual(60 + 50, next.food, 0.01f);
        Assert.AreEqual(1, next.sigils, "Sigils carry over unchanged");
        Assert.AreEqual(1200, next.heartHp, "a new Heart is as fragile as ever: the run starts hard");

        // A second, longer run stacks on the first.
        next.day = 80; next.heartRank = 5; next.runs = 1;            // 20 + 20 = 40 more points -> 62 -> rank 3
        var third = TowerRules.LegacyRun(next);
        Assert.AreEqual(62, third.legacyPoints);
        Assert.AreEqual(3, third.legacyRank);
        Assert.AreEqual(120 + 240, third.gold, "a higher rank starts richer");
    }

    [Test]
    public void LegacyRankIsCappedAndGrowsWithPoints()
    {
        Assert.AreEqual(0, TowerRules.LegacyRankFor(0));
        Assert.AreEqual(1, TowerRules.LegacyRankFor(4));
        Assert.AreEqual(2, TowerRules.LegacyRankFor(16));
        Assert.AreEqual(TowerRules.LegacyMaxRank, TowerRules.LegacyRankFor(100000));
        for (int rank = 1; rank <= TowerRules.LegacyMaxRank; rank++)
            Assert.Greater(TowerRules.LegacyBonusFor(rank).gold, TowerRules.LegacyBonusFor(rank - 1).gold);
        var fallen = new TowerState { day = 10, heartRank = 1, legacyPoints = 0 };
        StringAssert.Contains("Legacy rank", TowerRules.LegacyPreview(fallen));
    }

    [Test]
    public void PestsNeverHurtTheHeart()
    {
        var rules = Started();
        var home = rules.RoomAt(0, 21);
        rules.State.residents[0].priorityDefense = 0;
        rules.State.eventCooldown = 99999;
        float before = rules.State.heartHp;
        Assert.IsNull(rules.StartIncident("pests", home.uid));
        rules.Advance(60, true);
        Assert.AreEqual(before, rules.State.heartHp, 0.001f, "undefended pests must leave the Heart alone");
    }

    [Test]
    public void FireLeftBurningOnTheHeartsFloorScorchesTheHeart()
    {
        var rules = Started();
        var heart = rules.State.rooms.Find(r => r.type == "heart");
        var home = rules.RoomAt(0, 21);
        Assert.AreEqual(heart.floor, home.floor, "the starter home shares the Heart's floor");
        foreach (var resident in rules.State.residents) resident.priorityFire = 0;
        rules.State.eventCooldown = 99999;
        float before = rules.State.heartHp;
        Assert.IsNull(rules.StartIncident("fire", home.uid));
        rules.Advance(20, true);
        Assert.Less(rules.State.heartHp, before, "an unattended fire on the Heart's floor wounds it");
    }

    [Test]
    public void OfflineTimeNeverCreatesThreatsOrDownsResidents()
    {
        var rules = Started();
        rules.CatchUp(4 * 3600);
        Assert.AreEqual(0, rules.State.incidents.Count);
        Assert.IsFalse(rules.State.residents[0].downed);
    }

    [Test]
    public void VersionOneSaveMigratesWithoutLosingAssignments()
    {
        var original = Started().State;
        original.schema = 1;
        var restored = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(original));
        var resident = restored.residents[0];
        resident.hunger = resident.thirst = resident.rest = 0;
        int home = resident.homeRoom;
        TowerSaveFiles.Migrate(restored);
        Assert.AreEqual(2, restored.schema);
        Assert.AreEqual(home, resident.homeRoom);
        Assert.AreEqual(85, resident.hunger);
        Assert.AreEqual(home, resident.currentRoom);
    }

    [Test]
    public void AllMilestonesRemainPlayable()
    {
        for (int slot = 1; slot <= 10; slot++)
        {
            var state = TowerMilestones.Create(slot);
            var rules = new TowerRules(state);
            Assert.AreEqual(2, state.schema);
            Assert.IsNotNull(rules.RoomAt(0, TowerRules.CoreX));
            Assert.IsFalse(state.defeated);
            if (slot > 1)
            {
                Assert.Greater(state.residents.Count, 0);
                rules.Advance(20, false);
                Assert.IsFalse(state.defeated);
            }
        }
    }

    [Test]
    public void MoodIsBuiltFromNamedThoughts()
    {
        var rules = Started();
        var person = rules.State.residents[0];
        var fed = rules.MoodTarget(person);
        person.hunger = 10; person.thirst = 10;
        var thoughts = rules.Thoughts(person);
        Assert.IsTrue(thoughts.Exists(t => t.label == "Starving" && t.value < 0));
        Assert.Less(rules.MoodTarget(person), fed);
        person.homeRoom = 0;
        Assert.IsTrue(rules.Thoughts(person).Exists(t => t.label == "No bed of their own"));
    }

    [Test]
    public void SchedulesPutResidentsToBedAtNight()
    {
        var rules = Started();
        var person = rules.State.residents[0];
        Assert.IsNull(rules.SetSchedule(person.id, "day"));
        rules.State.clock = TowerRules.DaySeconds * 0.72f; // 23:17
        Assert.IsTrue(rules.Sleeping(person));
        rules.State.clock = TowerRules.DaySeconds * 0.1f;  // 08:24
        Assert.IsFalse(rules.Sleeping(person));
        Assert.IsNull(rules.SetSchedule(person.id, "night"));
        Assert.IsTrue(rules.Sleeping(person) == false);
        rules.State.clock = TowerRules.DaySeconds * 0.25f; // 12:00
        Assert.IsTrue(rules.Sleeping(person));
        Assert.IsNotNull(rules.SetSchedule(person.id, "lunch"));
    }

    [Test]
    public void SleepingResidentRestsAtHomeInsteadOfWorking()
    {
        var rules = Started();
        var kitchen = Kitchen(rules);
        var worker = rules.State.residents[0];
        Assert.IsNull(rules.SetSchedule(worker.id, "day"));
        rules.State.clock = TowerRules.DaySeconds * 0.75f - 3; // just before midnight
        rules.State.eventCooldown = 10000;
        rules.Advance(8, true);
        Assert.AreEqual("rest", worker.currentTask);
        Assert.AreEqual(worker.homeRoom, worker.targetRoom);
    }

    [Test]
    public void AdjacentSameRoomsMergeIntoAProductionBonus()
    {
        var rules = Started();
        rules.State.gold = 5000; rules.State.wood = 200; rules.State.stone = 200;
        for (int i = 0; i < 6; i++) Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("well", 0, 20));
        Assert.IsNull(rules.Build("well", 0, 19));
        var a = rules.RoomAt(0, 20); var b = rules.RoomAt(0, 19);
        Assert.AreEqual(1, rules.MergeNeighbours(a));
        Assert.AreEqual(1, rules.MergeNeighbours(b));
        Assert.Greater(rules.AdjacencyBonus(a), 1.1f);
        Assert.AreEqual(1f, rules.AdjacencyBonus(rules.RoomAt(0, 21)) , 0.001f);
    }

    [Test]
    public void ThreatRisesWithPopulationAndFallsWithResolvedIncidents()
    {
        var rules = new TowerRules(TowerMilestones.Create(6));
        rules.State.eventCooldown = 100000;
        rules.State.threat = 60;
        var kitchen = rules.State.rooms.Find(r => r.type == "kitchen");
        Assert.IsNull(rules.StartIncident("fire", kitchen.uid));
        rules.Advance(200, true);
        Assert.AreEqual(0, rules.State.incidents.Count);
        Assert.IsTrue(rules.ThreatLabel().Length > 0);
        Assert.Greater(rules.ThreatTarget(), new TowerRules(TowerMilestones.Create(2)).ThreatTarget());
    }

    [Test]
    public void GoalsProgressAndPayOut()
    {
        var rules = Started();
        Assert.AreEqual(TowerRules.ActiveGoals, rules.State.goals.Count);
        var goal = rules.State.goals[0];
        var def = TowerRules.GoalDef(goal.id);
        rules.State.counters.Add(new TowerCounter { key = def.counter, value = goal.baseline + def.target });
        Assert.IsTrue(rules.GoalComplete(goal));
        int gold = rules.State.gold;
        Assert.IsNull(rules.ClaimGoal(0));
        Assert.GreaterOrEqual(rules.State.gold, gold + def.gold);
        Assert.AreEqual(TowerRules.ActiveGoals, rules.State.goals.Count);
        Assert.IsFalse(rules.State.goals.Exists(g => g.id == def.id));
    }

    [Test]
    public void CollectingAndBuildingAdvanceGoalCounters()
    {
        var rules = Started();
        var room = Kitchen(rules);
        Assert.Greater(rules.Counter("build"), 0);
        room.ready = true;
        Assert.IsNull(rules.Collect(room.uid));
        Assert.AreEqual(1, rules.Counter("collect"));
    }

    [Test]
    public void DespairTriggersAMoodBreakThatStopsWork()
    {
        var rules = Started();
        var room = Kitchen(rules);
        var worker = rules.State.residents[0];
        rules.State.eventCooldown = 100000;
        worker.happiness = 5; worker.moodLow = 30;
        rules.State.food = rules.State.water = 0;
        worker.hunger = worker.thirst = 5;
        rules.Advance(3, true);
        Assert.Greater(worker.breakSeconds, 0);
        Assert.AreEqual(0, rules.ProductionRate(room));
        rules.Advance(60, true);
        Assert.AreEqual(0, worker.breakSeconds);
    }

    [Test]
    public void CaveInsOnlyHitUndergroundRoomsAndRepairersClearThem()
    {
        var rules = new TowerRules(TowerMilestones.Create(6));
        rules.State.eventCooldown = 100000;
        var deep = rules.State.rooms.Find(r => r.floor < 0 && r.type != "heart");
        Assert.IsNotNull(deep);
        Assert.IsNull(rules.StartIncident("cave_in", deep.uid));
        rules.Advance(400, true);
        Assert.IsFalse(rules.State.incidents.Exists(i => i.kind == "cave_in"));
    }

    [Test]
    public void NewRecruitsHaveTemperamentAndOldSavesGainOne()
    {
        var rules = Started();
        rules.State.pendingVisitors = 1;
        Assert.IsNull(rules.RecruitVisitor());
        var recruit = rules.State.residents[rules.State.residents.Count - 1];
        Assert.IsTrue(System.Array.IndexOf(TowerRules.Traits, recruit.trait) >= 0);
        Assert.IsTrue(recruit.schedule == "day" || recruit.schedule == "night");
        var restored = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        foreach (var resident in restored.residents) resident.trait = "";
        new TowerRules(restored);
        foreach (var resident in restored.residents)
            Assert.IsTrue(resident.origin == "body" || resident.trait.Length > 0);
    }

    [Test]
    public void HeartSitsInTheMiddleWithWingsOnBothSides()
    {
        var rules = Started();
        var ground = rules.Floor(0);
        Assert.AreEqual(1, ground.west);
        Assert.AreEqual(0, ground.east);
        Assert.AreEqual("heart", rules.RoomAt(0, TowerRules.CoreX).type);
        Assert.AreEqual("house", rules.RoomAt(0, TowerRules.CoreX - 1).type);
        Assert.AreEqual(2, rules.Gates().Count, "a Gate at each end");
        Assert.AreEqual(TowerRules.CoreX - 2, rules.GateOn(-1).x);
        Assert.AreEqual(TowerRules.CoreX + 1, rules.GateOn(1).x);
        Assert.LessOrEqual(Mathf.Abs((rules.GateOn(-1).x + rules.GateOn(1).x) / 2f - TowerRules.CoreX), 0.5f,
            "the Heart is within half a cell of the middle");
        Assert.IsTrue(rules.IsFounded(0, TowerRules.CoreX - 1));
        Assert.IsFalse(rules.IsFounded(0, TowerRules.CoreX));
    }

    [Test]
    public void GatesStepOutwardAndTheGroundFloorStaysCentred()
    {
        var rules = Started();
        rules.State.celestium = 200; rules.State.gold = 3000; rules.State.wood = 100; rules.State.stone = 100;
        StringAssert.Contains("east", rules.ExpandFloor(0, -1), "west is already one cell ahead");
        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.AreEqual(1, rules.Floor(0).east);
        Assert.AreEqual(TowerRules.CoreX + 2, rules.GateOn(1).x, "the Gate moved out to the new end");
        Assert.IsNull(rules.CanBuild("well", 0, TowerRules.CoreX + 1), "the old Gate cell is a building lot now");
        Assert.IsNotNull(rules.CanBuild("well", 0, TowerRules.CoreX + 2), "the Gate's own cell");
        Assert.IsNotNull(rules.CanBuild("well", 0, TowerRules.CoreX), "never on the shaft");
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX + 1));
        Assert.IsTrue(rules.RoomAt(0, TowerRules.CoreX + 1).flip, "east rooms grow away from the Heart");
        Assert.IsNull(rules.ExpandFloor(0));   // no side: the shorter one (west on a tie)
        Assert.AreEqual(2, rules.Floor(0).west);
        Assert.AreEqual(TowerRules.CoreX - 3, rules.GateOn(-1).x);
        Assert.IsNull(rules.OpenFloor(1));
        Assert.IsNull(rules.ExpandFloor(1, 1));
        Assert.IsNull(rules.ExpandFloor(1, 1), "upper floors have no Gates and grow freely");
        Assert.IsNull(rules.Build("house", 1, TowerRules.CoreX + 1));
    }

    [Test]
    public void OneGateSavesGainAWestGate()
    {
        var rules = Started();
        // A save from before the two Gates: the east Gate's cell counted in the ground floor, no west Gate.
        rules.State.rooms.Remove(rules.GateOn(-1));
        rules.State.floors[0].east = 1;
        rules.State.layoutVersion = 0;
        var state = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        var loaded = new TowerRules(state);
        Assert.AreEqual(0, loaded.Floor(0).east);
        Assert.AreEqual(TowerRules.CoreX + 1, loaded.GateOn(1).x, "the east Gate stays put");
        Assert.IsNotNull(loaded.GateOn(-1), "a west Gate joins");
        Assert.AreEqual(TowerRules.CoreX - loaded.Floor(0).west - 1, loaded.GateOn(-1).x);
    }

    [Test]
    public void WestExpansionRespectsTheWingCapAndUndergroundExcavation()
    {
        var rules = Started();
        rules.State.celestium = 5000;
        for (int i = 0; i < 2 * TowerRules.WingCells - 1; i++) Assert.IsNull(rules.ExpandFloor(0), "expansion " + i);
        Assert.AreEqual(TowerRules.WingCells, rules.Floor(0).west);
        Assert.AreEqual(TowerRules.WingCells, rules.Floor(0).east);
        Assert.IsNotNull(rules.ExpandFloor(0));
        Assert.AreEqual(TowerRules.CoreX - TowerRules.WingCells - 1, rules.GateOn(-1).x);
        Assert.AreEqual(TowerRules.CoreX + TowerRules.WingCells + 1, rules.GateOn(1).x);
        Assert.IsNull(rules.OpenFloor(-1));
        Assert.IsNotNull(rules.ExpandFloor(-1), "dig the cell out first");
        Assert.IsNull(rules.Excavate(-1, rules.NextExpansionX(-1, -1)));
        Assert.IsNull(rules.ExpandFloor(-1));
    }

    [Test]
    public void PopulatedCheckpointsBuildOnBothSidesWithAGateAtEachEnd()
    {
        var rules = new TowerRules(TowerMilestones.Create(4));
        Assert.IsTrue(rules.State.rooms.Exists(r => r.floor == 0 && r.x + r.width <= TowerRules.CoreX && r.type != "heart"));
        Assert.IsTrue(rules.State.rooms.Exists(r => r.floor == 0 && r.x > TowerRules.CoreX && r.type != "gate"));
        Assert.AreEqual(2, rules.Gates().Count);
        Assert.AreEqual(TowerRules.CoreX, (rules.GateOn(-1).x + rules.GateOn(1).x) / 2, "the Heart is in the middle");
        foreach (var a in rules.State.rooms)
            foreach (var b in rules.State.rooms)
                if (a != b && a.floor == b.floor)
                    Assert.IsFalse(a.x < b.x + b.width && b.x < a.x + a.width, a.type + " overlaps " + b.type);
    }

    [Test]
    public void FoundingGuideCannotBeSkippedOrStalled()
    {
        var rules = TowerRules.New();
        Assert.AreEqual("dormant", rules.State.introPhase);
        Assert.IsNotNull(rules.PlaceIntroGate());               // Heart first
        Assert.IsNotNull(rules.Build("house", 0, 21));          // no blueprint use before the Heart wakes is tolerated? gate not placed
        Assert.IsNotNull(rules.ChooseStarter("kaela"));         // cannot choose before the Shack
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNotNull(rules.AwakenHeart());
        Assert.IsNotNull(rules.ChooseStarter("kaela"));
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.AreEqual("shack", rules.State.introPhase);
        Assert.IsNotNull(rules.ChooseStarter("kaela"));         // the Shack is still missing
        Assert.IsNull(rules.Build("house", 0, TowerRules.CoreX - 1));
        Assert.AreEqual("choose", rules.State.introPhase);
        Assert.IsNotNull(rules.ChooseStarter("nobody"));
        Assert.IsNull(rules.ChooseStarter("elara"));
        Assert.AreEqual("complete", rules.State.introPhase);
        Assert.AreEqual(1, rules.State.residents.Count);
        Assert.AreEqual(rules.RoomAt(0, TowerRules.CoreX - 1).uid, rules.State.residents[0].homeRoom);
    }

    [Test]
    public void EveryFoundingHeroCanFollowTheWholeGuide()
    {
        foreach (string hero in new[] { "kaela", "ghislaine", "elara" })
        {
            var rules = TowerRules.New();
            Assert.IsNull(rules.AwakenHeart());
            Assert.IsNull(rules.PlaceIntroGate());
            Assert.IsNull(rules.Build("house", 0, TowerRules.CoreX - 1));
            Assert.IsNull(rules.ChooseStarter(hero));
            var person = rules.State.residents[0];
            // 1 BUILD: expand the foundation westward, pick the Kitchen, tap a lot.
            Assert.IsNull(rules.ExpandFloor(0));
            Assert.IsNull(rules.ExpandFloor(0));
            Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 2), hero + " kitchen");
            Assert.AreEqual(1, rules.State.tutorialStep);
            // 2 MATCH
            var kitchen = rules.RoomAt(0, TowerRules.CoreX - 2);
            Assert.Greater(rules.AssignmentImpact(person, kitchen), 0f);
            Assert.IsNull(rules.Assign(person.id, kitchen.uid));
            Assert.AreEqual(2, rules.State.tutorialStep);
            // 3 COLLECT: it becomes ready by itself in reasonable time
            rules.State.eventCooldown = 100000;
            float seconds = 0;
            while (!kitchen.ready && seconds < 600) { rules.Advance(1, true); seconds++; }
            Assert.IsTrue(kitchen.ready, hero + " kitchen never finished");
            Assert.Less(seconds, 400f, hero + " kitchen takes too long");
            Assert.IsNull(rules.Collect(kitchen.uid));
            Assert.AreEqual(3, rules.State.tutorialStep);
            // 4 RUSH is offered with a real chance
            Assert.Greater(rules.RushChance(kitchen.uid), 0f);
            Assert.IsNull(rules.Rush(kitchen.uid));
            Assert.AreEqual(4, rules.State.tutorialStep);
            // 5 PRIORITIES
            Assert.IsNull(rules.SetPriority(person.id, "fire", 3));
            Assert.GreaterOrEqual(rules.State.tutorialStep, 5);
            rules.State.eventCooldown = 5;
            for (int i = 0; i < 400 && rules.State.tutorialStep < 7; i++) rules.Advance(1, true);
            Assert.AreEqual(7, rules.State.tutorialStep, hero + " guide stalled at " + rules.State.tutorialStep);
            Assert.IsFalse(rules.State.defeated);
            Assert.IsFalse(person.downed && rules.State.tonics <= 0);
        }
    }

    [Test]
    public void EveryStockHasAProducerTheFounderKnows()
    {
        var rules = Started();
        foreach (string stock in TowerRules.Stocks)
        {
            string produces = stock;
            bool known = false;
            foreach (string id in rules.State.blueprints)
                if (TowerCatalog.Get(id).produces == produces) known = true;
            Assert.IsTrue(known, "no known blueprint makes " + stock);
        }
        // Gold path: the Argent Market is two tier-1 studies away, and founding grants enough Celestium for both.
        Assert.IsFalse(rules.State.blueprints.Contains("market"));
        Assert.AreEqual("CON-2", TowerRules.UnlockNode("market").id);
        Assert.GreaterOrEqual(rules.State.celestium,
            TowerRules.ResearchCelestium(TowerRules.ResearchDef("CON-1")) + TowerRules.ResearchCelestium(TowerRules.ResearchDef("CON-2")));
    }

    [Test]
    public void IncomeForecastMatchesWhatTheKitchenReallyMakes()
    {
        var rules = Started();
        var kitchen = Kitchen(rules);
        rules.State.eventCooldown = 100000;
        rules.State.food = 0;
        rules.State.tonics = 0;
        float predicted = rules.IncomePerMinute("food");
        Assert.Greater(predicted, 0.5f);
        float collected = 0;
        var worker = rules.State.residents[0];
        int cycles = 0;
        for (int second = 0; second < 900; second++)
        {
            rules.Advance(1, true);
            if (kitchen.ready) { collected += rules.CollectAmount(kitchen); rules.Collect(kitchen.uid); cycles++; }
        }
        float actual = collected / 15f; // per game minute over 900 s
        Assert.Greater(cycles, 3);
        Assert.AreEqual(predicted, actual, predicted * 0.35f, "forecast vs actual food per minute");
        Assert.IsNotNull(worker);
    }

    [Test]
    public void AdvisorNamesTheProducerWhenAStockIsRunningOut()
    {
        var rules = Started();
        Kitchen(rules);
        rules.State.tutorialStep = 7;
        rules.State.food = 400; rules.State.firewood = 400;
        rules.State.water = 3; // no Well exists
        string advice = rules.NeedsAdvice();
        StringAssert.Contains("WATER", advice);
        StringAssert.Contains("Well", advice);
        FoundGround(rules, 4);
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 3));
        rules.State.water = 3;
        StringAssert.Contains("Staff", rules.NeedsAdvice()); // built, but nobody works it
        var well = rules.RoomAt(0, TowerRules.CoreX - 3);
        var second = rules.AddResident("", "Rowan", "villager", 1);
        second.sight = 8;
        second.homeRoom = rules.State.residents[0].homeRoom;
        Assert.IsNull(rules.Assign(second.id, well.uid));
        rules.State.water = 3;
        rules.State.eventCooldown = 100000;
        rules.Advance(30, true);
        Assert.Greater(rules.IncomePerMinute("water"), 0f);
    }

    [Test]
    public void FreshFoundingStaysFedForAnHourWithKitchenWellAndMill()
    {
        var rules = Started();
        rules.State.eventCooldown = 100000; // isolate the economy from the storyteller
        FoundGround(rules, 4);
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 2));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 3));
        rules.State.gold += 2000; rules.State.wood += 60; rules.State.stone += 30; rules.State.celestium += 60;
        Learn(rules, "CON-6");   // the Hearth Nursery
        FoundGround(rules, 6);
        Assert.IsNull(rules.Build("lumber_mill", 0, TowerRules.CoreX - 4));
        Assert.IsNull(rules.Build("nursery", 0, TowerRules.CoreX - 5));
        rules.State.pendingVisitors = 2;
        for (int i = 0; i < 2; i++) Assert.IsNull(rules.RecruitVisitor());
        var residents = rules.State.residents;
        Assert.IsNull(rules.Assign(residents[0].id, rules.RoomAt(0, TowerRules.CoreX - 2).uid));
        Assert.IsNull(rules.Assign(residents[1].id, rules.RoomAt(0, TowerRules.CoreX - 3).uid));
        Assert.IsNull(rules.Assign(residents[2].id, rules.RoomAt(0, TowerRules.CoreX - 4).uid));
        Assert.IsNull(rules.UnlockHauling());
        float minFood = 9999, minWater = 9999, minFire = 9999;
        for (int second = 0; second < 3600; second++)
        {
            rules.Advance(1, true);
            minFood = Mathf.Min(minFood, rules.State.food);
            minWater = Mathf.Min(minWater, rules.State.water);
            minFire = Mathf.Min(minFire, rules.State.firewood);
        }
        Assert.Greater(minFood, 1f, "food ran out");
        Assert.Greater(minWater, 1f, "water ran out");
        Assert.Greater(minFire, 1f, "firewood ran out");
        foreach (var resident in residents)
        {
            Assert.IsFalse(resident.downed, resident.name + " collapsed");
            Assert.Greater(resident.happiness, 45f, resident.name + " is miserable");
        }
    }

    // ---- Colony layer: levels, brownouts, raids, relationships, death, steward ----

    private static TowerRules Quiet(TowerRules rules)
    {
        rules.State.eventCooldown = 100000;
        return rules;
    }

    [Test]
    public void CollectingGivesXpAndLevellingRaisesMaxHp()
    {
        var rules = Quiet(Started());
        var kitchen = Kitchen(rules);
        var worker = rules.State.residents[0];
        float before = TowerRules.MaxHp(worker);
        for (int i = 0; i < 12; i++) { kitchen.ready = true; Assert.IsNull(rules.Collect(kitchen.uid)); }
        Assert.Greater(worker.level, 1);
        Assert.Greater(TowerRules.MaxHp(worker), before);
        Assert.AreEqual(TowerRules.MaxHp(worker), worker.hp, 0.01f, "a level up heals fully");
    }

    [Test]
    public void BrownoutDarkensTheRoomsFarthestFromTheHeartFirst()
    {
        var rules = Quiet(Started());
        rules.State.gold += 5000; rules.State.wood += 100; rules.State.stone += 100; rules.State.celestium += 100;
        for (int i = 0; i < 6; i++) Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 2));
        rules.State.blueprints.Add("silo");
        Assert.IsNull(rules.Build("silo", 0, TowerRules.CoreX - 3));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 4));
        rules.State.firewood = 0;
        rules.Advance(1, true);
        var near = rules.RoomAt(0, TowerRules.CoreX - 1);
        var far = rules.RoomAt(0, TowerRules.CoreX - 4);
        Assert.IsFalse(rules.IsPowered(far), "with no mill, the far room is dark");
        Assert.Greater(rules.DarkRoomCount, 0);
        rules.State.firewood = 50;
        rules.Advance(1, true);
        Assert.IsTrue(rules.IsPowered(far) && rules.IsPowered(near), "firewood relights everything");
    }

    [Test]
    public void RaidersArriveAtTheGateAndGuardsDriveThemOff()
    {
        var rules = Quiet(Started());
        Assert.IsNull(rules.StartRaid());
        var gate = rules.Room(rules.State.incidents[0].roomUid);
        Assert.AreEqual("gate", gate.type, "raiders arrive at one of the Gates");
        var guard = rules.State.residents[0];
        guard.might = 10; guard.weapon = 3;
        Assert.IsNull(rules.Assign(guard.id, gate.uid));
        Assert.AreEqual(1, rules.GuardCount());
        Assert.AreEqual(2 * rules.Capacity(gate), rules.GuardSlots(), "both Gates take guards");
        int gold = rules.State.gold;
        for (int i = 0; i < 300 && rules.State.incidents.Count > 0; i++) rules.Advance(1, true);
        Assert.AreEqual(0, rules.State.incidents.Count, "the guard should beat the raid");
        Assert.GreaterOrEqual(rules.State.gold, gold, "beaten raiders return what they took, plus a bounty");
    }

    [Test]
    public void UndefendedRaidersLootAndPushDeeper()
    {
        var rules = Quiet(Started());
        foreach (var r in rules.State.residents) { r.priorityDefense = 0; r.priorityFire = 0; }
        var gate = rules.State.rooms.Find(r => r.type == "gate");
        rules.State.gold = 1000;
        Assert.IsNull(rules.StartRaid());
        for (int i = 0; i < (int)TowerRules.RaidMarchSeconds + 3; i++) rules.Advance(1, true);
        Assert.AreEqual(1, rules.State.incidents.Count);
        Assert.AreNotEqual(gate.uid, rules.State.incidents[0].roomUid, "raiders left the Gate");
        Assert.Less(rules.State.gold, 1000);
        Assert.Greater(rules.State.incidents[0].stolen, 0);
    }

    [Test]
    public void SharingARoomBuildsFriendshipAndAThought()
    {
        var rules = Quiet(Started());
        var kitchen = Kitchen(rules);
        var a = rules.State.residents[0];
        var b = rules.AddResident("", "Rowan", "villager", 1);
        b.homeRoom = a.homeRoom;
        Assert.IsNull(rules.Assign(b.id, kitchen.uid));
        rules.State.food = rules.State.water = rules.State.firewood = 100;
        rules.Advance(900, false);
        Assert.Greater(rules.Opinion(a.id, b.id), 40f);
        Assert.AreEqual("Friend", TowerRules.OpinionLabel(50));
        a.currentRoom = a.targetRoom = b.currentRoom = b.targetRoom = kitchen.uid;
        Assert.IsTrue(rules.Thoughts(a).Exists(t => t.label == "Beside a friend"));
    }

    [Test]
    public void UntendedCriticalVillagerDiesAndIsMourned()
    {
        var rules = Quiet(Started());
        var hero = rules.State.residents[0];
        var victim = rules.AddResident("", "Bram", "villager", 1);
        victim.homeRoom = victim.currentRoom = hero.homeRoom;
        var bond = new TowerBond { a = hero.id, b = victim.id, opinion = 60 };
        rules.State.bonds.Add(bond);
        hero.priorityCare = 0;
        victim.downed = true; victim.hp = 0; victim.injury = 80;
        for (int i = 0; i < (int)TowerRules.CriticalLimit + 5; i++) rules.Advance(1, true);
        Assert.IsNull(rules.Resident(victim.id), "the villager bled out");
        Assert.AreEqual(1, rules.State.memorial.Count);
        Assert.IsTrue(rules.Thoughts(hero).Exists(t => t.label.StartsWith("Mourning")));
        // Heroes are pulled back by the Heart instead of dying.
        hero.downed = true; hero.hp = 0; hero.injury = 80;
        for (int i = 0; i < (int)TowerRules.CriticalLimit + 5; i++) rules.Advance(1, true);
        Assert.IsNotNull(rules.Resident(hero.id));
    }

    [Test]
    public void CaregiversStabiliseWithoutTonics()
    {
        var rules = Quiet(Started());
        var nurse = rules.State.residents[0];
        nurse.priorityCare = 3;
        var patient = rules.AddResident("", "Bram", "villager", 1);
        patient.homeRoom = patient.currentRoom = nurse.homeRoom;
        rules.State.tonics = 0;
        patient.downed = true; patient.hp = 0; patient.injury = 70;
        for (int i = 0; i < 400; i++) rules.Advance(1, true);
        Assert.IsNotNull(rules.Resident(patient.id), "a tended patient does not bleed out");
        Assert.IsFalse(patient.downed);
    }

    [Test]
    public void StewardMovesAWorkerOntoAFailingStock()
    {
        var rules = Quiet(Started());
        rules.State.gold += 3000; rules.State.wood += 60; rules.State.stone += 30; rules.State.celestium += 60;
        FoundGround(rules, 4);
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 2));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 3));
        var worker = rules.State.residents[0];
        Assert.IsNull(rules.Assign(worker.id, rules.RoomAt(0, TowerRules.CoreX - 2).uid));
        rules.State.water = 2; rules.State.food = 100; rules.State.firewood = 100;
        rules.Advance(25, true);
        Assert.AreEqual(rules.RoomAt(0, TowerRules.CoreX - 3).uid, worker.jobRoom, "moved onto the Well");
        Assert.IsNull(rules.SetSteward(false));
        Assert.IsFalse(rules.State.steward);
    }

    [Test]
    public void MoodBreaksFollowTemperament()
    {
        var rules = Quiet(Started());
        var person = rules.State.residents[0];
        person.trait = "Stout";
        rules.State.food = 100;
        person.happiness = 0; person.hunger = 20;
        for (int i = 0; i < 40 && person.breakSeconds <= 0; i++) { person.happiness = 0; rules.Advance(1, true); }
        Assert.Greater(person.breakSeconds, 0);
        Assert.AreEqual("binge", person.breakKind);
        Assert.AreEqual("Food binge", rules.MoodLabel(person));
    }

    [Test]
    public void EveryCheckpointFeedsItselfForHalfAnHour()
    {
        for (int slot = 3; slot <= 10; slot++)
        {
            var state = TowerMilestones.Create(slot);
            var rules = Quiet(new TowerRules(state));
            Assert.AreEqual(TowerMilestones.Version, state.generator);
            foreach (string stock in TowerRules.Stocks)
                Assert.Greater(rules.PlannedNetPerMinute(stock), 0f, "slot " + slot + " " + stock);
            int people = state.residents.Count;
            for (int i = 0; i < 1800; i++) rules.Advance(1, true);
            Assert.AreEqual(0, state.memorial.Count, "slot " + slot + " lost residents");
            Assert.GreaterOrEqual(state.residents.Count, people);
            Assert.IsFalse(state.residents.Exists(r => r.downed), "slot " + slot + " has downed residents");
        }
    }

    // Founds the ground floor out to `west` cells (the east side one behind, as the centring rule allows) and moves
    // the Gates out to match. For tests about something other than the cost of founding.
    private static void FoundGround(TowerRules rules, int west)
    {
        var ground = rules.Floor(0);
        ground.west = Mathf.Max(ground.west, west);
        ground.east = Mathf.Max(ground.east, west - 1);
        rules.PlaceGates();
    }

    private static TowerRules BarnLot()
    {
        var rules = Started();
        rules.State.gold = 999999; rules.State.wood = rules.State.stone = 9999; rules.State.celestium = 9999;
        foreach (string id in new[] { "barn", "cottage" })
            if (!rules.State.blueprints.Contains(id)) rules.State.blueprints.Add(id);
        FoundGround(rules, 6);
        return rules;
    }

    [Test]
    public void BarnClimbsNineTiersAndWidensToThreeBays()
    {
        var rules = BarnLot();
        Learn(rules, "CON-7");   // SSR upgrades need Celestium fittings
        rules.State.heartRank = 9;
        Assert.IsNull(rules.Build("barn", 0, 20));
        var barn = rules.RoomAt(0, 20);
        Assert.AreEqual(1, barn.width);
        int[] bays = { 1, 1, 1, 2, 2, 3, 3, 3, 3 };
        for (int level = 1; level <= 9; level++)
        {
            Assert.AreEqual(level, barn.level);
            Assert.AreEqual(bays[level - 1], barn.width, "tier " + TowerTiers.BarnTier(level));
            Assert.AreEqual(20, barn.x + barn.width - 1, "the right bay stays on the anchor cell");
            if (level < 9) Assert.IsNull(rules.UpgradeRoom(barn.uid), "upgrade from tier " + TowerTiers.BarnTier(level));
        }
        Assert.AreEqual("SSR", TowerTiers.BarnTier(barn.level));
        Assert.AreEqual(18, barn.x);
        Assert.IsNotNull(rules.UpgradeRoom(barn.uid), "SSR is the top tier");
        Assert.AreEqual(3 * 9 * 60 + 120, rules.StockCap());
    }

    [Test]
    public void UpgradeWaitsWhenTheCellOnTheLeftIsTaken()
    {
        var rules = BarnLot();
        rules.State.heartRank = 9;
        Assert.IsNull(rules.Build("barn", 0, 20));
        Assert.IsNull(rules.Build("cottage", 0, 19));      // the cell the barn's second bay needs
        var barn = rules.RoomAt(0, 20);
        for (int i = 0; i < 2; i++) Assert.IsNull(rules.UpgradeRoom(barn.uid));
        StringAssert.Contains("to its left", rules.UpgradeRoom(barn.uid), "rank C needs cell 19");
        Assert.AreEqual(1, barn.width);
        Assert.AreEqual(3, barn.level);
        rules.Demolish(rules.RoomAt(0, 19).uid);
        Assert.IsNull(rules.UpgradeRoom(barn.uid));
        Assert.AreEqual(2, barn.width);
        Assert.AreEqual(19, barn.x, "the right bay stays fixed and the new bay joins on the left");
    }

    [Test]
    public void HeartRankCapsBuildingRank()
    {
        var rules = BarnLot();
        Assert.IsNull(rules.Build("barn", 0, 20));
        var barn = rules.RoomAt(0, 20);
        for (int i = 0; i < 2; i++) Assert.IsNull(rules.UpgradeRoom(barn.uid));
        StringAssert.Contains("rank E", rules.UpgradeRoom(barn.uid), "an F Heart stops buildings at D");
        Assert.IsNull(rules.UpgradeHeart());
        Assert.AreEqual(2, rules.State.heartRank);
        Assert.IsNull(rules.UpgradeRoom(barn.uid));
        Assert.AreEqual("C", TowerTiers.Tier(barn.level));
    }

    [Test]
    public void EveryMultiBayBuildingTopsOutAtThreeCells()
    {
        foreach (var def in TowerCatalog.All)
        {
            Assert.AreEqual(TowerTiers.SingleBay(def.id) ? 1 : 3, TowerTiers.Bays(def.id, 9), def.id);
            Assert.AreEqual(1, TowerTiers.Bays(def.id, 1), def.id + " opens as one bay");
            Assert.LessOrEqual(def.width, 3, def.id);
        }
    }

    [Test]
    public void FallenHeartStartsALegacyRunThatKeepsHeroesAndSigils()
    {
        var rules = Started();
        rules.State.sigils = 17;
        rules.State.residents[0].rank = 7;
        rules.State.heartHp = 0;
        rules.Advance(1, true);
        Assert.IsTrue(rules.State.defeated);
        var next = new TowerRules(TowerRules.LegacyRun(rules.State));
        Assert.AreEqual(17, next.State.sigils);
        Assert.AreEqual(1, next.State.runs);
        Assert.AreEqual(0, next.State.residents.Count, "the tower resets");
        Assert.AreEqual(1, next.State.legacyHeroes.Count);
        Assert.IsNull(next.AwakenHeart());
        Assert.IsNull(next.PlaceIntroGate());
        Assert.IsNull(next.Build("house", 0, 21));
        Assert.IsNull(next.ChooseStarter("kaela"));
        Assert.AreEqual(1, next.State.residents.Count, "kaela fuses with her returning self");
        Assert.AreEqual(7, next.State.residents[0].rank);
        Assert.AreEqual(0, next.State.legacyHeroes.Count);
    }

    [Test]
    public void HeartWarnsAsItWeakens()
    {
        var rules = Quiet(Started());
        rules.State.heartHp = TowerRules.HeartMaxHp(1) * 0.5f;
        rules.Advance(1, true);
        Assert.AreEqual("strained", rules.State.heartStage);
        rules.State.heartHp = TowerRules.HeartMaxHp(1) * 0.1f;
        rules.Advance(1, true);
        Assert.AreEqual("critical", rules.State.heartStage);
        StringAssert.Contains("CRITICAL", rules.State.log[rules.State.log.Count - 1]);
    }

    [Test]
    public void SummonsSpendSigilsAndHardPityGivesAnSsr()
    {
        var rules = Started();
        rules.State.sigils = 0;
        Assert.IsTrue(rules.FreeSummonReady);
        Assert.IsNull(rules.Summon(1));
        Assert.AreEqual("hero", rules.LastSummon[0].kind);
        Assert.GreaterOrEqual(rules.LastSummon[0].rank, 5, "the tutorial summon is B or better");
        Assert.IsNotNull(rules.Summon(1), "no Sigils left");
        rules.State.sigils = TowerRules.TenPullCost;
        rules.State.summonPity = TowerRules.HardPity - 1;
        Assert.IsNull(rules.Summon(10));
        Assert.AreEqual(0, rules.State.sigils);
        Assert.AreEqual(10, rules.LastSummon.Count);
        Assert.AreEqual(9, rules.LastSummon[0].rank, "pull 60 is a guaranteed SSR");
    }

    [Test]
    public void DuplicateHeroesFuseInsteadOfJoiningTwice()
    {
        var rules = Started();
        rules.State.sigils = 30 * TowerRules.TenPullCost;
        for (int i = 0; i < 30; i++) Assert.IsNull(rules.Summon(10));
        foreach (string id in TowerRules.SummonHeroIds)
            Assert.LessOrEqual(rules.State.residents.FindAll(r => r.origin == "hero" && r.unitId == id).Count, 1, id);
    }

    // ---------------------------------------------------------------- Sigil income (TowerSigils.cs)

    private static void Push(TowerRules rules, string counter, int amount)
    {
        var entry = rules.State.counters.Find(c => c.key == counter);
        if (entry == null) rules.State.counters.Add(new TowerCounter { key = counter, value = amount });
        else entry.value += amount;
    }

    private static System.DateTime Day(int day) { return new System.DateTime(2026, 10, day); }

    [Test]
    public void SummonsCostTenSigilsAndEveryTenPullHoldsABOrBetter()
    {
        var rules = Started();
        Assert.IsNull(rules.Summon(1), "the free summon");
        rules.State.sigils = TowerRules.PullCost - 1;
        Assert.IsNotNull(rules.Summon(1));
        rules.State.sigils = TowerRules.PullCost;
        Assert.IsNull(rules.Summon(1));
        Assert.AreEqual(0, rules.State.sigils);
        rules.State.sigils = 50 * TowerRules.TenPullCost;
        for (int pull = 0; pull < 50; pull++)
        {
            Assert.IsNull(rules.Summon(10));
            int best = 0;
            foreach (var s in rules.LastSummon) best = Mathf.Max(best, s.rank);
            Assert.GreaterOrEqual(best, TowerRules.TenPullFloor, "10-pull " + pull);
        }
        Assert.AreEqual(0, rules.State.sigils);
        float total = 0;
        foreach (float rate in TowerRules.SummonRates) total += rate;
        Assert.AreEqual(100f, total, 0.01f, "the rates screen adds up");
    }

    [Test]
    public void GoalsPaySigils()
    {
        var rules = Started();
        rules.State.sigils = 0;
        var def = TowerRules.GoalDef(rules.State.goals[0].id);
        Push(rules, def.counter, def.target);
        Assert.IsNull(rules.ClaimGoal(0));
        Assert.AreEqual(def.sigils, rules.State.sigils);
        foreach (var goal in TowerRules.GoalPool)
        {
            Assert.That(goal.sigils, Is.InRange(2, 4), goal.id);
            StringAssert.Contains(goal.sigils + " Sigils", goal.Reward);
        }
    }

    [Test]
    public void DailyBoardIsDealtOncePerCalendarDayAndPaysSixteen()
    {
        TowerRules.Today = () => Day(1);
        var rules = Started();
        rules.State.sigils = 0;
        var board = rules.State.daily;
        Assert.AreEqual(1 + TowerRules.DailyPicked, board.Count);
        Assert.AreEqual("checkin", board[0].id);
        Assert.IsTrue(rules.DailyComplete(board[0]), "visiting is enough");
        Assert.LessOrEqual(board.FindAll(t => TowerRules.DailyDef(t.id).counter == "collect").Count, 1);
        Assert.IsFalse(board.Exists(t => t.id == "expedition"), "no Guild, no expedition task");

        var again = Started();
        for (int i = 0; i < board.Count; i++) Assert.AreEqual(board[i].id, again.State.daily[i].id, "same date, same board");

        rules.Advance(30, true);
        Assert.AreEqual(1 + TowerRules.DailyPicked, rules.State.daily.Count, "the board is not re-dealt the same day");
        foreach (var task in board) Push(rules, TowerRules.DailyDef(task.id).counter, 20);
        for (int i = 0; i < board.Count; i++) Assert.IsNull(rules.ClaimDaily(i));
        Assert.AreEqual(4 * TowerRules.DailyTaskSigils + TowerRules.DailyBonusSigils, rules.State.sigils);
        Assert.IsTrue(rules.State.dailyBonusClaimed);
        Assert.IsNotNull(rules.ClaimDaily(0), "claimed once");

        TowerRules.Today = () => Day(2);
        rules.Advance(1, true);
        Assert.AreEqual("2026-10-02", rules.State.dailyDate);
        Assert.IsFalse(rules.State.dailyBonusClaimed);
        Assert.IsFalse(rules.State.daily.Exists(t => t.claimed));
        Assert.IsTrue(rules.State.daily.TrueForAll(t => t.id == "checkin" || !rules.DailyComplete(t)),
            "yesterday's progress does not count");
    }

    [Test]
    public void AFallenHeartDoesNotDealASecondDailyBoard()
    {
        TowerRules.Today = () => Day(1);
        var rules = Started();
        Assert.IsNull(rules.ClaimDaily(0));
        var next = new TowerRules(TowerRules.LegacyRun(rules.State));
        Assert.IsNull(next.AwakenHeart());
        Assert.IsNull(next.PlaceIntroGate());
        Assert.IsNull(next.Build("house", 0, 21));
        Assert.IsNull(next.ChooseStarter("kaela"));
        Assert.AreEqual("2026-10-01", next.State.dailyDate);
        Assert.IsTrue(next.State.daily[0].claimed);
        Assert.IsNotNull(next.ClaimDaily(0));
    }

    [Test]
    public void HeartRankUpsPaySigils()
    {
        var rules = Started();
        rules.State.sigils = 0;
        rules.State.celestium = 99999; rules.State.gold = 999999;
        Assert.IsNull(rules.UpgradeHeart());
        Assert.AreEqual(TowerRules.HeartRankUpSigils(2), rules.State.sigils);
        int total = 0;
        for (int rank = 2; rank <= TowerTiers.MaxRank; rank++) total += TowerRules.HeartRankUpSigils(rank);
        Assert.AreEqual(250, total);
    }

    [Test]
    public void ExpeditionReturnsPaySigilsTwiceADayAndConquestsOnce()
    {
        TowerRules.Today = () => Day(1);
        var rules = Expedition();
        rules.State.sigils = 0;
        Assert.IsNull(rules.EndExpedition(false));
        Assert.AreEqual(0, rules.State.sigils, "a walk without a fight pays nothing");
        var party = new System.Collections.Generic.List<string> { "kaela", "ghislaine", "elara" };
        int paid = Mathf.RoundToInt(2 + 1.5f * TowerRules.Region("silverbrook_edge").reward);
        for (int trip = 0; trip < 3; trip++)
        {
            Assert.IsNull(rules.StartExpedition("silverbrook_edge", party, 2, 0, 0));
            rules.State.run.battlesWon = 1;
            Assert.IsNull(rules.EndExpedition(false));
        }
        Assert.AreEqual(2 * paid, rules.State.sigils, "only the first two returns of the day pay");
        Assert.IsNull(rules.StartExpedition("silverbrook_edge", party, 2, 0, 0));
        rules.State.run.battlesWon = 1;
        TowerRules.Today = () => Day(2);
        Assert.IsNull(rules.EndExpedition(true));
        Assert.AreEqual(2 * paid, rules.State.sigils, "a wiped party pays nothing");

        var conquer = typeof(TowerRules).GetMethod("ConquerRegion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        conquer.Invoke(rules, new object[] { "silverbrook_edge" });
        conquer.Invoke(rules, new object[] { "silverbrook_edge" });
        Assert.AreEqual(2 * paid + TowerRules.ConquestSigils, rules.State.sigils);
    }

    // ---------------------------------------------------------------- research (TowerResearch.cs)

    private static long fakeNow = 1_800_000_000;

    // Studies a node and every predecessor in its branch at once, leaving the tower's stocks and Heart rank as they were.
    private static void Learn(TowerRules rules, string id)
    {
        var s = rules.State;
        int celestium = s.celestium, gold = s.gold, tonics = s.tonics, rank = s.heartRank;
        var def = TowerRules.ResearchDef(id);
        s.heartRank = TowerTiers.MaxRank;
        for (int i = 1; i <= def.index; i++)
        {
            string need = def.branch + "-" + i;
            if (rules.Researched(need)) continue;
            s.celestium = 99999; s.gold = 999999; s.tonics = 99;
            Assert.IsNull(rules.StartResearch(need), need);
            Assert.IsNull(rules.RushResearch(), need);
            Assert.IsTrue(rules.Researched(need), need);
        }
        s.celestium = celestium; s.gold = gold; s.tonics = tonics; s.heartRank = rank;
    }

    [Test]
    public void ResearchRunsOnTheRealClockAndUnlocksBuildings()
    {
        TowerRules.NowUnix = () => fakeNow;
        var rules = Started();
        Assert.IsFalse(rules.State.blueprints.Contains("farmstead"));
        Assert.IsNotNull(rules.Build("farmstead", 0, 20), "locked until CON-1");
        int celestium = rules.State.celestium;
        Assert.IsNull(rules.StartResearch("CON-1"));
        Assert.AreEqual(celestium - 10, rules.State.celestium);
        Assert.IsNotNull(rules.StartResearch("SET-1"), "one study at a time");
        rules.Advance(600, true);   // game time does not move the real-time timer
        Assert.IsFalse(rules.Researched("CON-1"));
        TowerRules.NowUnix = () => fakeNow + 599;
        rules.Advance(0.1f, true);
        Assert.IsFalse(rules.Researched("CON-1"));
        Assert.Greater(rules.ResearchProgress(), 0.99f);
        TowerRules.NowUnix = () => fakeNow + 600;
        rules.Advance(0.1f, true);
        Assert.IsTrue(rules.Researched("CON-1"));
        Assert.IsFalse(rules.Researching);
        Assert.IsTrue(rules.State.blueprints.Contains("farmstead"));
    }

    [Test]
    public void ResearchNeedsTheBranchBeforeItAndAHighEnoughHeart()
    {
        var rules = Started();
        rules.State.celestium = 9999; rules.State.gold = 99999;
        StringAssert.Contains("first", rules.CanResearch("CON-2"));
        Learn(rules, "CON-2");
        StringAssert.Contains("rank D", rules.CanResearch("CON-3"), "tier 2 needs Heart D");
        rules.State.heartRank = 3;
        Assert.IsNull(rules.CanResearch("CON-3"));
        rules.State.celestium = 0;
        StringAssert.Contains("Celestium", rules.CanResearch("CON-3"));
        foreach (var def in TowerRules.ResearchTree)
            Assert.AreEqual(def.index <= 2 ? 1 : def.index <= 4 ? 2 : def.index <= 6 ? 3 : def.index == 7 ? 4 : 5, def.Tier, def.id);
        Assert.AreEqual(40, TowerRules.ResearchTree.Length);
    }

    [Test]
    public void RushingResearchCostsATonicPerHalfHourLeft()
    {
        TowerRules.NowUnix = () => fakeNow;
        var rules = Started();
        Learn(rules, "CON-2");
        rules.State.heartRank = 3; rules.State.celestium = 999;
        Assert.IsNull(rules.StartResearch("CON-3"));   // one hour
        Assert.AreEqual(2, rules.RushTonicCost());
        rules.State.tonics = 1;
        Assert.IsNotNull(rules.RushResearch());
        rules.State.tonics = 2;
        Assert.IsNull(rules.RushResearch());
        Assert.AreEqual(0, rules.State.tonics);
        Assert.IsTrue(rules.Researched("CON-3"));
        Assert.IsTrue(rules.State.blueprints.Contains("frosted_mug"));
    }

    [Test]
    public void ResearchEffectsReachTheirSystems()
    {
        var rules = Started();
        var shack = rules.RoomAt(0, 21);
        var gate = rules.State.rooms.Find(r => r.type == "gate");
        int beds = rules.Capacity(shack), guards = rules.Capacity(gate);
        Learn(rules, "SET-3");
        Assert.Greater(rules.Capacity(shack), beds, "Better beds");
        Learn(rules, "DEF-3");
        Assert.AreEqual(guards + 1, rules.Capacity(gate), "a third guard");
        var kitchen = Kitchen(rules);
        float food = rules.CollectAmount(kitchen);
        Learn(rules, "PRO-2");
        Assert.AreEqual(food * 1.08f, rules.CollectAmount(kitchen), 0.001f, "crop rotation + sharper tools");
        float cap = rules.StockCap();
        Learn(rules, "PRO-5");
        Assert.AreEqual(cap * 1.15f, rules.StockCap(), 0.01f);
        Assert.AreEqual(50, rules.SoftPityStart());
        Learn(rules, "EXP-5");
        Assert.AreEqual(45, rules.SoftPityStart());
        rules.State.heartHp = TowerRules.HeartMaxHp(1) * 0.55f;
        Assert.AreEqual("strained", rules.HeartStage());
        Learn(rules, "DEF-5");
        Assert.AreEqual("stable", rules.HeartStage(), "Wardstones delay the warning");
        var room = rules.AddRoom("barn", 0, 19, 8);
        int full = rules.UpgradeGoldCost(room);
        rules.State.heartRank = 9; rules.State.gold = 999999; rules.State.wood = rules.State.stone = 9999;
        StringAssert.Contains("CON-7", rules.UpgradeRoom(room.uid));
        Learn(rules, "CON-8");
        Assert.AreEqual(Mathf.RoundToInt(full * 0.9f), rules.UpgradeGoldCost(room));
    }

    [Test]
    public void SigilFocusAddsTenPercentAndCarriesTheFraction()
    {
        TowerRules.Today = () => Day(1);
        var rules = Started();
        Learn(rules, "EXP-3");
        rules.State.sigils = 0;
        Assert.IsNull(rules.ClaimDaily(0));
        Assert.AreEqual(3, rules.State.sigils);
        Assert.AreEqual(0.3f, rules.State.sigilCarry, 0.001f);
    }

    [Test]
    public void RegionsNeedTheirMapResearchAsWellAsConquest()
    {
        var rules = Expedition();
        rules.State.regionsUnlocked.Add("rootside_camp");
        Assert.IsFalse(rules.RegionUnlocked("rootside_camp"));
        Learn(rules, "EXP-1");
        Assert.IsTrue(rules.RegionUnlocked("rootside_camp"));
        Assert.AreEqual("EXP-8", TowerRules.RegionResearchNode("silverwood_gate"));
    }

    [Test]
    public void OlderSavesKeepWhatTheyOwnAsResearch()
    {
        var state = TowerMilestones.Create(1);
        var founded = new TowerRules(state);
        Assert.IsNull(founded.AwakenHeart());
        Assert.IsNull(founded.PlaceIntroGate());
        Assert.IsNull(founded.Build("house", 0, 21));
        Assert.IsNull(founded.ChooseStarter("kaela"));
        state.researchVersion = 0;
        state.research.Clear();
        state.blueprints.Add("quarry");
        state.regionsUnlocked.Add("moon_shrine");
        var loaded = new TowerRules(state);
        foreach (string id in new[] { "CON-1", "CON-2", "CON-3", "CON-4", "EXP-1", "EXP-2", "EXP-3", "EXP-4" })
            Assert.IsTrue(loaded.Researched(id), id);
        Assert.IsFalse(loaded.Researched("CON-5"));
        Assert.IsTrue(state.blueprints.Contains("warehouse"), "CON-4 brings its whole unlock");
    }

    [Test]
    public void ResearchCarriesIntoALegacyRun()
    {
        var rules = Started();
        Learn(rules, "PRO-2");
        var next = TowerRules.LegacyRun(rules.State);
        CollectionAssert.Contains(next.research, "PRO-1");
        CollectionAssert.Contains(next.research, "PRO-2");
    }

    [Test]
    public void CheckpointsKnowTheResearchTheirHeartCouldReach()
    {
        var state = TowerMilestones.Create(10);
        var rules = new TowerRules(state);
        Assert.AreEqual(40, state.research.Count, "an SSR Heart reaches tier 5");
        Assert.IsTrue(rules.RegionUnlocked("silverbrook_edge"));
    }

    [Test]
    public void SummonsWithNoFreeBedWaitInsideTheHeartUntilOneOpens()
    {
        var rules = Started();
        rules.State.sigils = 20 * TowerRules.TenPullCost;
        for (int i = 0; i < 20; i++) Assert.IsNull(rules.Summon(10));
        Assert.LessOrEqual(rules.BiologicalPopulation(), rules.PopulationCap(), "summons never overfill housing");
        Assert.Greater(rules.State.heartWaiting.Count, 0, "the overflow waits in the Heart");
        foreach (var unit in rules.State.heartWaiting)
            Assert.IsFalse(rules.State.residents.Contains(unit));
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var unit in rules.State.residents.Concat(rules.State.heartWaiting))
            if (unit.origin == "hero") Assert.IsTrue(seen.Add(unit.unitId), "duplicates fuse even while waiting: " + unit.unitId);

        int waiting = rules.State.heartWaiting.Count, people = rules.BiologicalPopulation();
        rules.AddRoom("house", 1, TowerRules.CoreX - 3);
        rules.Advance(1, true);
        Assert.Less(rules.State.heartWaiting.Count, waiting, "a new house draws units out of the Heart");
        Assert.Greater(rules.BiologicalPopulation(), people);
        Assert.LessOrEqual(rules.BiologicalPopulation(), rules.PopulationCap());
        foreach (var r in rules.State.residents.FindAll(r => r.origin != "body" && r.homeRoom == 0))
            Assert.IsFalse(rules.State.heartWaiting.Contains(r));
    }

    [Test]
    public void HeroesWaitingInTheHeartCarryIntoALegacyRun()
    {
        var rules = Started();
        var hero = new TowerResident { id = 999, name = "Waiting Hero", unitId = "test_waiting_hero", origin = "hero", rank = 7 };
        rules.State.heartWaiting.Add(hero);
        var next = TowerRules.LegacyRun(rules.State);
        Assert.IsTrue(next.legacyHeroes.Exists(h => h.unitId == "test_waiting_hero"));
    }

    // The Heart climb model from TOWER_BALANCE_REPORT section 6: goals pay ~8 Celestium a day, the tower runs 0-5
    // quarries at the Heart's building cap (18.5 collects a day), and half of all Celestium goes to research.
    private static float DaysToSsr(int legacyRank)
    {
        int[] quarries = { 0, 0, 1, 2, 3, 4, 4, 5, 5 };
        var rules = new TowerRules(TowerMilestones.Create(1));
        rules.State.legacyRank = legacyRank;
        rules.State.heartRank = 1;
        float bank = 45 + TowerRules.LegacyBonusFor(legacyRank).celestium, day = 0;
        while (rules.State.heartRank < TowerTiers.MaxRank && day < 400)
        {
            int rank = rules.State.heartRank;
            int level = Mathf.Min(TowerTiers.MaxRank, TowerTiers.BuildingCap(rank));
            bank += (8 + quarries[rank - 1] * 18.5f * TowerRules.QuarryCelestium(level)) * 0.5f * 0.05f;
            day += 0.05f;
            if (bank >= rules.HeartUpgradeCelestium()) { bank -= rules.HeartUpgradeCelestium(); rules.State.heartRank++; }
        }
        return day;
    }

    [Test]
    public void FirstRunsClimbSlowlyAndLegacyRunsFaster()
    {
        float first = DaysToSsr(0), veteran = DaysToSsr(10);
        Assert.That(first, Is.InRange(60f, 95f), "a first run reaches SSR in about 60-90 days");
        Assert.Less(veteran, first * 0.6f, "Legacy rank 10 climbs much faster");
        Assert.Less(DaysToSsr(5), first);
        Assert.AreEqual(0.3f, TowerRules.LegacyHeartDiscount(5), 0.001f);
        Assert.AreEqual(0.6f, TowerRules.LegacyHeartDiscount(12), 0.001f, "capped");
        for (int level = 2; level <= TowerTiers.MaxRank; level++)
            Assert.GreaterOrEqual(TowerRules.QuarryCelestium(level), TowerRules.QuarryCelestium(level - 1), "never drops with rank");
        Assert.AreEqual(5, TowerRules.QuarryCelestium(9));
    }

    [Test]
    public void GoldReadsAsACompactNumberFromOneThousand()
    {
        Assert.AreEqual("999", TowerRules.Compact(999));
        Assert.AreEqual("1k", TowerRules.Compact(1000));
        Assert.AreEqual("1.4k", TowerRules.Compact(1400));
        Assert.AreEqual("1.4k", TowerRules.Compact(1499));
        Assert.AreEqual("205k", TowerRules.Compact(205000));
        Assert.AreEqual("999.9k", TowerRules.Compact(999999));
        Assert.AreEqual("1mil", TowerRules.Compact(1000000));
        Assert.AreEqual("300.5mil", TowerRules.Compact(300500000));
        Assert.AreEqual("12bil", TowerRules.Compact(12000000000));
        Assert.AreEqual("1500bil", TowerRules.Compact(1500000000000));
    }

    [Test]
    public void OldTwoCellBarnsShrinkToTheirTierWidth()
    {
        var rules = Started();
        var barn = rules.AddRoom("barn", 0, 19, 2);
        barn.width = 2;
        rules.Normalize();
        Assert.AreEqual(1, barn.width);
        Assert.AreEqual(20, barn.x, "the west wing frees its outer cell");
    }

    [Test]
    public void OlderPopulatedSaveSkipsTheFoundingGuide()
    {
        var state = TowerMilestones.Create(10);
        state.tutorialStep = 0;
        new TowerRules(state);
        Assert.AreEqual(7, state.tutorialStep);
    }
}
