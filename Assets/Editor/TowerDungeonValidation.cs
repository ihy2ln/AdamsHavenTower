using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public static class TowerDungeonValidation
{
    [MenuItem("Adams Haven/Expedition/Validate Silverbrook Dungeon")]
    public static void Run()
    {
        var tests = new TowerSimulationTests();
        tests.InstantBuilds();
        try
        {
            tests.ForestLayoutsAreFixedAndConnected();
            tests.DungeonsAreDeterministicAndReachable();
            tests.DungeonCrawlRevealsFogAndClearsThePoi();
            tests.ClearingTheLairConquersAndUnlocksRegions();
            CheckBranches();
            CheckTravelAndSave();
        }
        finally { tests.TimedBuilds(); }
        Debug.Log("Silverbrook dungeon validation PASSED: layouts, branches, route purity, incremental travel, fog, encounters, saves, old floors, progression and six illustrated POI profiles.");
    }

    private static void CheckBranches()
    {
        foreach (string theme in new[] { "cave", "ruin", "keep", "marsh", "briar", "crystal" })
        {
            Assert.IsNotNull(Resources.Load<Texture2D>("AdamsHaven/Expedition/Dungeon/Silverbrook/" + theme + "_terrain"), theme);
            Assert.IsTrue(TowerDungeonIllustration.Available(theme), theme + " illustrated map resources");
            Assert.IsNotNull(Resources.Load<Texture2D>(TowerDungeonIllustration.ArtRoot + theme + "_vista"), theme + " existing battle art");
            for (int seed = 0; seed < 100; seed++)
            {
                var node = new TowerForestNode { id = "probe" + seed, kind = "lair", theme = theme };
                var d = TowerDungeon.Build("probe", node, seed % 3, seed);
                Assert.That(d.rooms.Count, Is.InRange(6, 8));
                var distances = d.Distances(d.rooms[0].CenterX, d.rooms[0].CenterY);
                foreach (var room in d.rooms) Assert.GreaterOrEqual(distances[TowerDungeon.Index(room.CenterX, room.CenterY)], 0);
                for (int a = 0; a < d.rooms.Count; a++) for (int b = a + 1; b < d.rooms.Count; b++)
                {
                    var ra = d.rooms[a]; var rb = d.rooms[b];
                    Assert.IsFalse(ra.x < rb.x + rb.w && rb.x < ra.x + ra.w && ra.y < rb.y + rb.h && rb.y < ra.y + ra.h);
                }
                Assert.IsTrue(d.rooms.Exists(r => r.kind == "treasure" && !r.goal), "Reward detour");
                Assert.IsTrue(d.rooms.Exists(r => TowerRules.BattleRoom(r.kind)), "Combat room");
                var hub = d.rooms[1];
                bool left = false, right = false, top = false, bottom = false;
                for (int y = hub.y; y < hub.y + hub.h; y++)
                { left |= d.Walkable(hub.x - 1, y); right |= d.Walkable(hub.x + hub.w, y); }
                for (int x = hub.x; x < hub.x + hub.w; x++)
                { top |= d.Walkable(x, hub.y - 1); bottom |= d.Walkable(x, hub.y + hub.h); }
                Assert.GreaterOrEqual((left ? 1 : 0) + (right ? 1 : 0) + (top ? 1 : 0) + (bottom ? 1 : 0), 3, "Physical branching junction");
            }
        }
    }

    private static void CheckTravelAndSave()
    {
        var state = TowerMilestones.Create(10);
        var rules = new TowerRules(state);
        state.hasRun = true;
        state.run = new TowerRun { layout = "briar_tangle", region = "silverbrook_edge", seed = 321,
            party = new List<string> { "kaela", "elara", "ghislaine" }, hp = new List<int> { 100, 100, 100 } };
        var node = rules.RunLayout.nodes.Find(n => n.kind == "lair");
        state.run.at = node.id;
        Assert.IsNull(rules.EnterPoi());
        var run = rules.Run; var d = rules.Dungeon;
        Assert.AreEqual(1, run.dungeonLayoutVersion);
        List<int> route;
        string before = JsonUtility.ToJson(run);
        Assert.IsNotNull(rules.DungeonRoute(19, 19, out route), "Rock is unreachable");
        Assert.AreEqual(before, JsonUtility.ToJson(run), "Planning never mutates the run");
        int sx = run.px, sy = run.py;
        Assert.IsNotNull(rules.DungeonAdvance(sx + 2, sy), "No teleportation");
        Assert.AreEqual(sx, run.px);
        var known = d.Distances(sx, sy);
        int frontier = -1, farthest = -1;
        for (int i = 0; i < known.Length; i++)
            if (known[i] > farthest && rules.Revealed(i % 20, i / 20)) { frontier = i; farthest = known[i]; }
        before = JsonUtility.ToJson(run);
        Assert.IsNull(rules.DungeonRoute(frontier % 20, frontier / 20, out route));
        Assert.AreEqual(before, JsonUtility.ToJson(run), "Valid route planning is pure");
        int discoveredBefore = run.fog.Replace("0", "").Length;
        foreach (int cell in route)
        {
            Assert.IsNull(rules.DungeonAdvance(cell % 20, cell / 20));
            if (rules.PendingRoom >= 0) break;
        }
        Assert.Greater(run.fog.Replace("0", "").Length, discoveredBefore, "Walking reveals fog progressively");
        run.fog = new string('1', 400);
        var target = d.rooms[d.rooms.Count - 1];
        Assert.IsNull(rules.DungeonRoute(target.CenterX, target.CenterY, out route));
        Assert.Greater(route.Count, 0);
        foreach (int cell in route)
        {
            int oldX = run.px, oldY = run.py;
            Assert.IsNull(rules.DungeonAdvance(cell % 20, cell / 20));
            Assert.AreEqual(1, Math.Abs(run.px - oldX) + Math.Abs(run.py - oldY));
            if (rules.PendingRoom >= 0) break;
        }
        var loaded = JsonUtility.FromJson<TowerRun>(JsonUtility.ToJson(run));
        CollectionAssert.AreEqual(run.roomsDone, loaded.roomsDone);
        Assert.AreEqual(run.fog, loaded.fog); Assert.AreEqual(run.px, loaded.px); Assert.AreEqual(run.py, loaded.py);
        run.dungeonLayoutVersion = 0;
        var legacy = TowerDungeon.Build(run.layout, node, run.floor, run.seed, 0);
        CollectionAssert.AreEqual(legacy.cell, rules.Dungeon.cell, "Legacy save layout retained");
    }
}
