using System.Collections.Generic;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// The run map as a Path of Exile 2-style Atlas (BATTLE_MODE_GDD.md sections 4 and 7): the web of places, reaching only
// places linked to a completed one, tiers and map mods, towers and tablets, room-by-room dungeon fog and AUTO, and old
// grid saves moving onto the web.
public sealed class ExpeditionAtlasTests
{
    [SetUp] public void Setup() { TowerRules.InstantConstruction = true; TowerRules.TraversalEvents = false; TowerRules.GridMaps = true; TowerRules.Weathers = false; }
    [TearDown] public void Teardown()
    {
        TowerRules.InstantConstruction = false; TowerRules.TraversalEvents = true; TowerRules.GridMaps = true; TowerRules.Weathers = true;
        TowerRules.Today = () => System.DateTime.Now.Date;
    }

    public static TowerRules Expedition(string region = "silverbrook_edge")
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.IsNull(rules.Build("house", 0, 21));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        rules.State.food = rules.State.water = 500;
        rules.State.firewood = 200;
        rules.State.tonics = 4;
        bool grid = TowerRules.GridMaps;
        TowerRules.GridMaps = true;
        try { Assert.IsNull(rules.StartExpedition(region, new List<string> { "kaela", "ghislaine", "elara" }, 9, 2, 3)); }
        finally { TowerRules.GridMaps = grid; }
        return rules;
    }

    // An open place with a dungeon next to the camp (the nearest by trail), or null.
    public static TowerOverworldPoi OpenDungeon(TowerRules rules)
    {
        var map = rules.Overworld;
        TowerMapEdge best = null;
        foreach (var e in rules.Web.EdgesOf(map.Camp.id))
        {
            var p = map.Poi(e.Other(map.Camp.id));
            if (TowerRules.ModdedKind(p.kind) && p.kind != "lair" && (best == null || e.length < best.length)) best = e;
        }
        return best == null ? null : map.Poi(best.Other(map.Camp.id));
    }

    // Completes every place on the fewest-hops way from the camp to `id` (test shortcut), so `id` is open.
    public static void OpenWayTo(TowerRules rules, string id)
    {
        var web = rules.Web; string camp = rules.Overworld.Camp.id;
        var prev = new Dictionary<string, string> { { camp, null } };
        var queue = new Queue<string>();
        queue.Enqueue(camp);
        while (queue.Count > 0)
        {
            string at = queue.Dequeue();
            foreach (var e in web.EdgesOf(at))
            {
                string next = e.Other(at);
                if (prev.ContainsKey(next)) continue;
                prev[next] = at; queue.Enqueue(next);
            }
        }
        Assert.IsTrue(prev.ContainsKey(id), id + " is on the web");
        for (string at = prev[id]; at != null && at != camp; at = prev[at])
            if (!rules.Run.nodesDone.Contains(at)) rules.Run.nodesDone.Add(at);
        Assert.GreaterOrEqual(rules.NodeState(id), TowerRules.NodeOpen, id + " is open");
    }

    // Travels one trail at a time (the threat meter is emptied between trails so the forest never shifts mid-test).
    public static void TravelTo(TowerRules rules, string id)
    {
        for (int guard = 0; guard < 16 && rules.Run.at != id; guard++)
        {
            var route = rules.AtlasRoute(id);
            Assert.IsNull(route.error, "route to " + id + ": " + route.error);
            rules.Run.threat = 0;
            var step = rules.AtlasTravel(route.hops[1]);
            Assert.IsNull(step.error, step.error);
        }
        Assert.AreEqual(id, rules.Run.at);
    }

    // Answers whatever the forest throws at the party: first affordable choice, ambushes are won.
    public static void Settle(TowerRules rules)
    {
        for (int guard = 0; guard < 10 && rules.Run != null; guard++)
        {
            if (rules.AmbushPending) { Assert.IsNull(rules.ResolveAmbush(true, null)); continue; }
            if (rules.RewardPending) { Assert.IsNull(rules.ChooseReward(0)); continue; }
            if (rules.TabletPending) { Assert.IsNull(rules.ChooseTablet(0)); continue; }
            var def = rules.PendingEvent;
            if (def == null) break;
            int pick = def.choices.FindIndex(c => rules.ChoiceBlocked(c) == null);
            Assert.GreaterOrEqual(pick, 0, def.id + " has no affordable choice");
            Assert.IsNull(rules.ResolveEvent(pick));
        }
        if (rules.Run != null) rules.DismissEventResult();
    }

    // Resolves the room the party stands in, whatever it holds.
    public static void Resolve(TowerRules rules, int room)
    {
        string kind = rules.Dungeon.rooms[room].kind;
        if (TowerRules.BattleRoom(kind)) Assert.IsNull(rules.ResolveBattle(true, new List<int> { 90, 90, 90 }), kind);
        else if (kind == "stairs") Assert.IsNull(rules.ResolveRoom("descend"));
        else if (kind == "trap") Assert.IsNull(rules.ResolveRoom("rush"));
        else if (kind == "shrine") Assert.IsNull(rules.ResolveRoom("pray"));
        else if (kind == "merchant" || kind == "story" || kind == "skillcheck" || kind == "keygate") Assert.IsNull(rules.ResolveRoom("leave"), kind);
        else Assert.IsNull(rules.ResolveRoom(kind == "treasure" ? "take" : kind == "rest" ? "rest" : "investigate"), kind);
    }

    // Clears the dungeon of the place the party stands at by going straight to each floor's goal.
    public static void ClearHere(TowerRules rules)
    {
        string id = rules.Run.at;
        Assert.IsNull(rules.EnterPoi());
        for (int guard = 0; guard < 6 && rules.Run.dungeonPoi.Length > 0; guard++)
        {
            var d = rules.Dungeon;
            int goal = d.rooms.FindIndex(r => r.goal || r.kind == "stairs");
            rules.Run.px = d.rooms[goal].CenterX; rules.Run.py = d.rooms[goal].CenterY;
            Resolve(rules, goal);
        }
        Settle(rules);
        Assert.IsTrue(rules.Run.cleared.Contains(id), id + " is cleared");
    }

    [Test]
    public void TheWebLinksEveryPlaceAndKeepsTheLairAwayFromCamp()
    {
        foreach (var biome in TowerOverworldGen.Biomes)
            for (uint seed = 1; seed <= 25; seed++)
            {
                var map = TowerOverworldGen.Generate(biome.id, seed * 7919);
                var web = TowerMapWeb.Build(map);
                string tag = biome.id + " seed " + seed;
                Assert.AreEqual(1, map.pois.FindAll(p => p.kind == "tower").Count, tag + " one tower");
                var hops = web.Hops(map.Camp.id);
                Assert.AreEqual(map.pois.Count, hops.Count, tag + " every place is on the web");
                Assert.IsFalse(web.Linked(map.Camp.id, map.Lair.id), tag + " no shortcut from the camp to the lair");
                Assert.Greater(hops[map.Lair.id], 1, tag);
                var keys = new HashSet<string>();
                foreach (var e in web.edges)
                {
                    Assert.IsTrue(keys.Add(e.key), tag + " one link per pair");
                    var a = map.Poi(e.a); var b = map.Poi(e.b);
                    Assert.AreEqual(new Vector2Int(a.x, a.y), e.path[0], tag);
                    Assert.AreEqual(new Vector2Int(b.x, b.y), e.path[e.path.Count - 1], tag);
                    foreach (var c in e.path) Assert.IsTrue(map.Walkable(c.x, c.y), tag + " trails cross open ground");
                    CollectionAssert.AreEqual(e.path, Reversed(e.From(e.b)), tag + " a trail walks both ways");
                }
                Assert.AreEqual(web.Signature(), TowerMapWeb.Build(TowerOverworldGen.Generate(biome.id, seed * 7919)).Signature(),
                    tag + " same seed, same web");
            }
    }

    private static List<Vector2Int> Reversed(List<Vector2Int> cells) { var r = new List<Vector2Int>(cells); r.Reverse(); return r; }

    [Test]
    public void ASavedRunRebuildsTheSameWeb()
    {
        var rules = Expedition();
        var first = OpenDungeon(rules);
        Assume.That(first != null, "the camp links to a dungeon");
        Assert.IsNull(rules.AtlasTravel(first.id).error);
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.AreEqual(rules.Overworld.Hash(), loaded.Overworld.Hash());
        Assert.AreEqual(rules.Web.Signature(), loaded.Web.Signature());
        foreach (var p in rules.Overworld.pois)
        {
            Assert.AreEqual(rules.NodeState(p.id), loaded.NodeState(p.id), p.id);
            Assert.AreEqual(rules.NodeTier(p.id), loaded.NodeTier(p.id), p.id);
            CollectionAssert.AreEqual(rules.NodeModIds(p.id), loaded.NodeModIds(p.id), p.id);
        }
    }

    [Test]
    public void OnlyPlacesLinkedToACompletedOneCanBeReached()
    {
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld; var web = rules.Web;
        Assert.AreEqual(TowerRules.WebVersion, run.webVersion);
        Assert.AreEqual(TowerRules.NodeDone, rules.NodeState(map.Camp.id));
        foreach (var p in map.pois)
        {
            if (p.kind == "camp") continue;
            bool linked = web.Linked(map.Camp.id, p.id);
            Assert.AreEqual(linked, rules.NodeState(p.id) == TowerRules.NodeOpen, p.id + " is open exactly when linked to the camp");
            if (linked)
            {
                Assert.IsTrue(rules.NodeVisible(p.id), p.id + " shows");
                Assert.IsTrue(rules.CellSeen(p.x, p.y), p.id + " is out of the fog");
                Assert.IsNull(rules.AtlasRoute(p.id).error, p.id);
            }
            else Assert.IsNotNull(rules.AtlasRoute(p.id).error, p.id + " is out of reach");
        }
        var far = map.pois.Find(p => p.kind != "camp" && !web.Linked(map.Camp.id, p.id));
        Assert.IsNotNull(rules.AtlasTravel(far.id).error);
        Assert.IsNotNull(rules.ForestMove(far.id));
        Assert.AreEqual(map.Camp.id, run.at, "no free walking");
        Assert.Less(rules.NodeState(map.Lair.id), TowerRules.NodeOpen, "the lair starts out of reach");
        Assert.IsTrue(new TowerRunMapSource(rules).Known(map.Lair), "but always shows as a beacon");
        Assert.IsNotNull(rules.AtlasTravel(map.Camp.id).error, "already there");
    }

    [Test]
    public void CompletingAPlaceRevealsItsNeighboursAndTripsPassThroughCompletedPlaces()
    {
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld; var web = rules.Web;
        var first = OpenDungeon(rules);
        Assume.That(first != null, "the camp links to a dungeon");
        int steps = run.steps;
        Assert.IsNull(rules.AtlasTravel(first.id).error);
        Assert.AreEqual(steps + 1, run.steps, "one event roll per trail");
        Assert.AreEqual(first.id, run.at);
        Assert.AreEqual(TowerRules.NodeOpen, rules.NodeState(first.id), "reaching a dungeon does not complete it");
        ClearHere(rules);
        Assert.AreEqual(TowerRules.NodeDone, rules.NodeState(first.id));
        foreach (var e in web.EdgesOf(first.id))
        {
            string next = e.Other(first.id);
            Assert.GreaterOrEqual(rules.NodeState(next), TowerRules.NodeOpen, next + " is open now");
            Assert.IsTrue(rules.NodeVisible(next));
            var p = map.Poi(next);
            Assert.IsTrue(rules.CellSeen(p.x, p.y), next + " comes out of the fog");
        }
        // A place beyond the cleared one is reached through it, in one trip.
        var beyond = web.EdgesOf(first.id).Find(e => e.Other(first.id) != map.Camp.id && !web.Linked(map.Camp.id, e.Other(first.id)));
        if (beyond == null) return;
        string target = beyond.Other(first.id);
        run.threat = 0;
        Assert.IsNull(rules.AtlasTravel(map.Camp.id).error, "back to camp");
        Assert.IsTrue(rules.AtSafeExit);
        var route = rules.AtlasRoute(target);
        Assert.IsNull(route.error);
        CollectionAssert.Contains(route.hops, first.id, "through the cleared place");
        run.threat = 0;
        var step = rules.AtlasTravel(target);
        Assert.IsNull(step.error);
        Assert.AreEqual(target, run.at);
        Assert.AreEqual(route.path.Count, step.walked.Count, "the whole route is walked");
        Assert.Contains(TowerRules.RoadKey(first.id, target), run.road, "the trail travelled is remembered");
    }

    [Test]
    public void TiersRiseWithDistanceAndTheLairIsHighest()
    {
        var rules = Expedition();
        var map = rules.Overworld; var web = rules.Web;
        int depth = rules.RunRegion.depth;
        int lairTier = rules.NodeTier(map.Lair.id);
        foreach (var p in map.pois)
        {
            if (!TowerRules.ModdedKind(p.kind) || p.kind == "lair") continue;
            int hops = web.CampHops(p.id);
            Assert.AreEqual(Mathf.Clamp(depth + hops - 1, 1, TowerRules.MaxTier), rules.NodeTier(p.id), p.id);
            Assert.Less(rules.NodeTier(p.id), lairTier, "the lair is the highest tier");
            Assert.LessOrEqual(rules.NodeModIds(p.id).Count, Mathf.Min(3, hops), p.id + ": more mods only farther out");
        }
        Assert.LessOrEqual(rules.NodeModIds(map.Lair.id).Count, 1, "the lair carries at most one mod of its own");
        Assert.AreEqual(0, rules.NodeModIds(map.Camp.id).Count);
        Assert.AreEqual("IV", TowerRules.Roman(4)); Assert.AreEqual("IX", TowerRules.Roman(9)); Assert.AreEqual("XVI", TowerRules.Roman(16));
        Assert.AreEqual(0, TowerRules.TierBand(5)); Assert.AreEqual(1, TowerRules.TierBand(6)); Assert.AreEqual(2, TowerRules.TierBand(11));
    }

    [Test]
    public void MapModsShapeTheFightTheLootAndTheRooms()
    {
        var totals = TowerRules.Totals(new List<string> { "brutal", "teeming", "fortified", "hasted", "rich" });
        Assert.AreEqual(1.25f, totals.offense, 1e-4f);
        Assert.AreEqual(1.1f, totals.vitality, 1e-4f);
        Assert.AreEqual(1, totals.extraFoes);
        Assert.AreEqual("Shielded", totals.affix, "Fortified wins over Hasted");
        Assert.IsTrue(totals.relic);
        Assert.AreEqual(1.3f * 1.25f, totals.loot, 1e-4f);
        Assert.AreEqual(1.2f, totals.gold, 1e-4f);
        Assert.AreEqual(1, totals.treasureRooms);
        foreach (var m in TowerRules.MapMods) StringAssert.Contains(";", m.Line, m.id + " pairs a danger with a reward");

        // A tablet from the tower puts Brutal on a place in its circle: every fight there carries it.
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld;
        var tower = map.pois.Find(p => p.kind == "tower");
        // A pack or elite place (its goal is a fight; a vault may hold none).
        var target = map.pois.Find(p => (p.kind == "combat" || p.kind == "elite") && TowerRules.InTowerRange(tower, p));
        Assume.That(target != null, "a fighting place in the tower's circle");
        int before = rules.NodeModIds(target.id).Count;
        run.tablets.Add(tower.id + ":brutal");
        var mods = rules.NodeModIds(target.id);
        Assert.AreEqual(before + 1, mods.Count);
        Assert.AreEqual("brutal", mods[mods.Count - 1]);
        var expected = TowerRules.Totals(mods);
        run.at = target.id; run.cx = target.x; run.cy = target.y;
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        int fight = d.rooms.FindIndex(r => TowerRules.BattleRoom(r.kind));
        Assert.GreaterOrEqual(fight, 0);
        run.px = d.rooms[fight].CenterX; run.py = d.rooms[fight].CenterY;
        var spec = rules.RoomEncounter(d.rooms[fight].kind);
        Assert.AreEqual(expected.offense, spec.Offense, 1e-4f);
        Assert.AreEqual(expected.vitality, spec.Vitality, 1e-4f);
        Assert.AreEqual(expected.extraFoes, spec.ExtraFoes);
        Assert.AreEqual(expected.affix, spec.Affix);
        Assert.GreaterOrEqual(spec.Offense, 1.25f - 1e-4f, "Brutal hits harder");
        Assert.GreaterOrEqual(rules.NodeLootScale(target.id), 1.3f - 1e-4f, "and pays more");
        Assert.GreaterOrEqual(spec.Depth, rules.RunRegion.depth + run.floor + rules.NodeDepthBonus(target.id));

        // Rich and Haunted change the room mix but never the entrance, the goal, stairs, elites or the last fight.
        var node = new TowerForestNode { id = "p3", kind = "combat", theme = "ruin" };
        int grew = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            var plain = TowerDungeon.Build("grid_edge", node, 0, seed * 131, 2);
            var kinds = plain.rooms.ConvertAll(r => r.kind);
            TowerRules.ApplyRoomMods(plain, TowerRules.Totals(new List<string> { "rich", "haunted" }));
            for (int i = 0; i < kinds.Count; i++)
                if (i == 0 || plain.rooms[i].goal || kinds[i] == "stairs" || kinds[i] == "elite" || kinds[i] == "boss")
                    Assert.AreEqual(kinds[i], plain.rooms[i].kind, "seed " + seed + " room " + i + " stays");
            Assert.IsTrue(plain.rooms.Exists(r => TowerRules.BattleRoom(r.kind)), "a fight stays");
            if (plain.rooms.FindAll(r => r.kind == "treasure").Count > kinds.FindAll(k => k == "treasure").Count) grew++;
        }
        Assert.Greater(grew, 20, "Rich adds a treasure room");
    }

    [Test]
    public void ATowerRevealsItsCircleAndSetsATablet()
    {
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld;
        var tower = map.pois.Find(p => p.kind == "tower");
        Assert.IsNotNull(tower, "every new map has a tower");
        OpenWayTo(rules, tower.id);
        TravelTo(rules, tower.id);
        Assert.AreEqual(TowerRules.NodeDone, rules.NodeState(tower.id), "climbing completes the tower");
        Assert.IsTrue(rules.TabletPending);
        Assert.AreEqual(TowerRules.TabletChoices, run.tabletOffer.Count);
        Assert.AreEqual("Choose a tablet first.", rules.EventBlock());
        foreach (var p in map.pois)
            if (TowerRules.InTowerRange(tower, p))
            {
                Assert.GreaterOrEqual(rules.NodeState(p.id), TowerRules.NodeScouted, p.id + " is seen from the tower");
                Assert.IsTrue(rules.CellSeen(p.x, p.y), p.id);
            }
        string offer = run.tabletOffer[0];
        var mod = TowerRules.TabletMod(offer);
        Assert.IsNotNull(mod);
        var inRange = map.pois.FindAll(p => TowerRules.ModdedKind(p.kind) && !rules.NodeComplete(p.id) && TowerRules.InTowerRange(tower, p));
        Assert.AreEqual(inRange.Count, rules.TabletReach(offer));
        var counts = inRange.ConvertAll(p => rules.NodeModIds(p.id).Count);
        Assert.IsNull(rules.ChooseTablet(0));
        Assert.IsFalse(rules.TabletPending);
        Assert.IsNull(rules.EventBlock());
        for (int i = 0; i < inRange.Count; i++)
        {
            var ids = rules.NodeModIds(inRange[i].id);
            Assert.AreEqual(counts[i] + 1, ids.Count, inRange[i].id);
            Assert.Contains(mod.id, ids);
        }
        // The tablet survives a save; a tower has no dungeon and is never climbed twice.
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.Contains(offer, loaded.Run.tablets);
        Assert.IsNotNull(loaded.EnterPoi(), "a tower has no dungeon");
        Assert.IsFalse(loaded.TabletPending);
    }

    [Test]
    public void DungeonsLiftTheirFogRoomByRoomAndAutoReachesTheGoal()
    {
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld;
        var place = map.pois.Find(p => p.kind == "elite") ?? map.pois.Find(p => p.kind == "combat");
        run.at = place.id; run.cx = place.x; run.cy = place.y;
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        // In the entrance: the entrance, its passages and every room linked to it are known, nothing else.
        var links = d.Links(0);
        Assert.Greater(links.Count, 0);
        for (int i = 0; i < d.rooms.Count; i++)
            Assert.AreEqual(i == 0 || links.Contains(i), rules.Revealed(d.rooms[i].CenterX, d.rooms[i].CenterY), "room " + i);
        foreach (int cell in d.Passages(0)) Assert.IsTrue(rules.Revealed(cell % TowerDungeon.Size, cell / TowerDungeon.Size), "passages out are known");
        Assert.IsTrue(links.Contains(rules.DungeonAutoTarget()), "AUTO heads for a room next door");
        var light = rules.DungeonLight();
        Assert.AreEqual(1f, light[TowerDungeon.Index(run.px, run.py)], "the party's room is lit");
        // AUTO: the nearest unfinished known room each time; resolve what is found; the place gets cleared.
        int guard = 0;
        for (; guard < 300 && run.dungeonPoi.Length > 0; guard++)
        {
            if (rules.RewardPending) { Assert.IsNull(rules.ChooseReward(0)); continue; }
            int pending = rules.PendingRoom;
            if (pending >= 0) { Resolve(rules, pending); continue; }
            Assert.IsNull(rules.DungeonExplore(), "AUTO always finds somewhere to go");
        }
        Assert.Less(guard, 300, "the crawl finished");
        Assert.IsTrue(run.cleared.Contains(place.id));
        Assert.AreEqual(TowerRules.NodeDone, rules.NodeState(place.id));
    }

    [Test]
    public void GoToGoalFindsTheStairsOnceSeenAndSkippedRoomsWait()
    {
        var rules = Expedition();
        var run = rules.Run; var map = rules.Overworld;
        var place = map.pois.Find(p => p.kind == "elite") ?? map.pois.Find(p => p.kind == "lair");
        run.at = place.id; run.cx = place.x; run.cy = place.y;
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        int stairs = d.rooms.FindIndex(r => r.kind == "stairs");
        Assert.GreaterOrEqual(stairs, 0, "a two-floor place has stairs on its first floor");
        Assert.AreEqual(d.Links(0).Contains(stairs) ? stairs : -1, rules.DungeonGoalRoom, "the stairs count once they are seen");
        // Every room but the stairs skipped: AUTO still ends at the stairs.
        var skip = new HashSet<int>();
        for (int i = 1; i < d.rooms.Count; i++) if (i != stairs) skip.Add(i);
        run.fog = new string('1', TowerDungeon.Size * TowerDungeon.Size);
        Assert.AreEqual(stairs, rules.DungeonAutoTarget(skip));
        Assert.AreEqual(stairs, rules.DungeonGoalRoom);
    }

    [Test]
    public void AnOldGridRunMovesOntoTheWebWithoutLosingAnything()
    {
        var rules = Expedition();
        var run = rules.Run;
        // A save from before the web: a version 3 map, free walking, the party between two places.
        run.gridVersion = 3; run.webVersion = 0; run.nodesDone.Clear(); run.webLinks.Clear(); run.tablets.Clear(); run.revealed.Clear();
        var old = rules.Overworld;
        Assert.IsNull(old.pois.Find(p => p.kind == "tower"), "version 3 maps have no tower");
        var visited = old.pois.Find(p => p.kind == "combat");
        run.visited.Add(visited.id);
        var path = old.FindPath(new Vector2Int(old.Camp.x, old.Camp.y), new Vector2Int(visited.x, visited.y));
        var mid = path.Find(c => old.PoiAt(c.x, c.y) == null && (c - new Vector2Int(old.Camp.x, old.Camp.y)).sqrMagnitude > 9);
        run.cx = mid.x; run.cy = mid.y; run.at = "";
        run.haul.Add(new TowerLoot { name = "purse", gold = 50 });
        string json = JsonUtility.ToJson(rules.State);
        TowerRules loaded = null;
        Assert.DoesNotThrow(() => loaded = new TowerRules(JsonUtility.FromJson<TowerState>(json)));
        var moved = loaded.Run;
        Assert.IsNotNull(moved, "the run survives");
        Assert.AreEqual(TowerRules.WebVersion, moved.webVersion);
        Assert.AreEqual(3, moved.gridVersion, "the map itself is kept");
        Assert.AreEqual(old.Hash(), loaded.Overworld.Hash());
        Assert.AreEqual(loaded.Overworld.Camp.id, moved.at, "the party walked back to camp");
        Assert.IsTrue(loaded.AtSafeExit);
        Assert.AreEqual(TowerRules.NodeDone, loaded.NodeState(visited.id), "places stood on count as completed");
        foreach (var e in loaded.Web.EdgesOf(visited.id)) Assert.GreaterOrEqual(loaded.NodeState(e.Other(visited.id)), TowerRules.NodeOpen);
        Assert.AreEqual(50, moved.haul.Find(l => l.name == "purse").gold);
        Assert.IsFalse(loaded.MigrateWebRun(), "only once");

        // An old save caught inside a dungeon with cell-by-cell fog keeps its dungeon and sees its room again.
        var inside = Expedition();
        var r2 = inside.Run;
        var place = inside.Overworld.pois.Find(p => p.kind == "combat");
        r2.at = place.id; r2.cx = place.x; r2.cy = place.y;
        Assert.IsNull(inside.EnterPoi());
        r2.webVersion = 0; r2.nodesDone.Clear();
        var fog = new string('0', TowerDungeon.Size * TowerDungeon.Size).ToCharArray();
        fog[TowerDungeon.Index(r2.px, r2.py)] = '1';
        r2.fog = new string(fog);
        TowerRules again = null;
        Assert.DoesNotThrow(() => again = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(inside.State))));
        Assert.AreEqual(place.id, again.Run.dungeonPoi, "still inside");
        var d = again.Dungeon;
        foreach (int q in d.Links(0)) Assert.IsTrue(again.Revealed(d.rooms[q].CenterX, d.rooms[q].CenterY), "the old fog is lifted room by room");
        Assert.AreEqual(TowerRules.NodeDone, again.NodeState(place.id), "the place being explored stays reachable");
        Assert.IsNull(again.LeaveDungeon());
        var home = again.AtlasRoute(again.Overworld.Camp.id);
        Assert.IsNull(home.error, "and the party can walk home from it over completed places");
        foreach (var id in home.hops) Assert.AreEqual(TowerRules.NodeDone, again.NodeState(id), id);
    }

    [Test]
    public void VaultsAlwaysHoldAFightFromLayoutVersionThree()
    {
        var vault = new TowerForestNode { id = "p9", kind = "treasure", theme = "crystal" };
        int emptyBefore = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            var v3 = TowerDungeon.Build("grid_edge", vault, 0, seed * 131, TowerDungeon.LayoutVersion);
            Assert.IsTrue(v3.rooms.Exists(r => TowerRules.BattleRoom(r.kind)), "seed " + seed + ": a vault has a guard");
            var v2 = TowerDungeon.Build("grid_edge", vault, 0, seed * 131, 2);
            if (!v2.rooms.Exists(r => TowerRules.BattleRoom(r.kind))) emptyBefore++;
            else for (int i = 0; i < v2.rooms.Count; i++) Assert.AreEqual(v2.rooms[i].kind, v3.rooms[i].kind, "only fightless vaults change");
        }
        Assert.Greater(emptyBefore, 0, "version 2 floors (old saves) keep their old rooms");
    }
}
