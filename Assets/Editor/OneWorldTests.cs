using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// TT 10.5.0, one world (GDD 21): the dungeon dug beneath a Tower save. B1..Bn, the living band, the Heart's vault at the
// bottom; prey come down the Dungeon Gate stair from the ground floor. Off in EditMode unless a test opts in, so the
// Tower and Dungeon Mode suites keep the worlds they were written for.
public sealed class OneWorldTests
{
    [SetUp] public void OneWorld() { TowerRules.InstantConstruction = true; TowerRules.ForceOneWorld = true; }
    [TearDown] public void Reset() { TowerRules.InstantConstruction = false; TowerRules.ForceOneWorld = false; }

    private static TowerRules Started()
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.IsNull(rules.Build("house", 0, 21));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        rules.State.eventCooldown = 100000;
        rules.State.gold = 99999; rules.State.wood = rules.State.stone = 999; rules.State.celestium = 999;
        return rules;
    }

    [Test]
    public void ANewTowerOpensItsDungeonBelowGround()
    {
        var rules = Started();
        Assert.IsTrue(rules.Merged);
        Assert.IsFalse(rules.IsLair);
        Assert.AreEqual(-3, rules.VaultFloor);
        Assert.AreEqual(0, rules.HeartFloor, "the Heart room stays on the ground floor");
        Assert.AreEqual(0, rules.State.rooms.Find(r => r.type == "heart").floor);
        Assert.AreEqual("dungeon", rules.FloorKind(-1));
        Assert.AreEqual("living", rules.FloorKind(-2));
        Assert.AreEqual("heart", rules.FloorKind(-3));
        Assert.AreEqual("gate", rules.FloorKind(0));
        Assert.AreEqual("tower", rules.FloorKind(2));
        Assert.AreEqual("B1", rules.FloorLabel(-1));
        Assert.AreEqual("LIVING", rules.FloorLabel(-2));
        Assert.AreEqual("VAULT", rules.FloorLabel(-3));
        Assert.AreEqual("GROUND", rules.FloorLabel(0));
        Assert.AreEqual(1, rules.DungeonFloorCount);
        Assert.IsTrue(rules.State.blueprints.Contains("spike_hall"));
        Assert.IsTrue(rules.BuildableDefs().Any(d => d.id == "monster_lair"));
        Assert.AreEqual(0, rules.State.lair.parties.Count, "nothing hunts the dungeon in TT 10.5.0");
        rules.Advance(600, true);
        Assert.AreEqual(0, rules.State.lair.parties.Count, "no adventurers before the lure wakes");
    }

    [Test]
    public void WithoutOneWorldTheTowerHasNoDungeon()
    {
        TowerRules.ForceOneWorld = false;
        var rules = Started();
        Assert.IsFalse(rules.LairFounded);
        Assert.IsNull(rules.Floor(-1));
        StringAssert.Contains("Dig the dungeon first", rules.ZoneReason(TowerCatalog.Get("spike_hall"), -1));
    }

    [Test]
    public void AnOldTowerKeepsItsBasementsAsTheLivingBand()
    {
        TowerRules.ForceOneWorld = false;
        var rules = Started();
        Assert.IsNull(rules.OpenFloor(-1));
        rules.State.blueprints.Add("quarry");
        Assert.IsNull(rules.Build("quarry", -1, 21));
        int quarry = rules.State.rooms.Find(r => r.type == "quarry").uid;
        TowerRules.ForceOneWorld = true;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.IsTrue(loaded.Merged);
        Assert.AreEqual(-2, loaded.Room(quarry).floor, "the basement stepped down under B1");
        Assert.AreEqual("living", loaded.FloorKind(-2));
        Assert.AreEqual("dungeon", loaded.FloorKind(-1));
        Assert.AreEqual(-3, loaded.VaultFloor);
        Assert.AreEqual(1, loaded.State.lair.livingFloors);
        var again = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(loaded.State)));
        Assert.AreEqual(-3, again.VaultFloor, "founding happens once");
        Assert.AreEqual(4, again.State.floors.Count(f => f.number <= 0));
    }

    [Test]
    public void TrapsStayOnTheDungeonFloorsAndRoomsStayOffThem()
    {
        var rules = Started();
        Assert.IsNull(rules.CanBuild("spike_hall", -1, 21));
        StringAssert.Contains("dungeon floors", rules.CanBuild("spike_hall", 0, 21));
        StringAssert.Contains("dungeon floors", rules.CanBuild("spike_hall", -2, 21));
        StringAssert.Contains("only traps", rules.CanBuild("kitchen", -1, 21));
        Assert.IsNull(rules.CanBuild("kitchen", -2, 21), "the living band takes the residents' rooms");
        rules.State.blueprints.Add("quarry");
        Assert.IsNull(rules.CanBuild("quarry", -2, 21));
    }

    [Test]
    public void PreyComeDownTheDungeonGateAndCrossEveryDungeonFloor()
    {
        var rules = Started();
        Assert.IsTrue(rules.ShaftSealed(-1));
        Assert.IsFalse(rules.ShaftSealed(0));
        Assert.IsFalse(rules.ShaftSealed(-2));
        Assert.IsFalse(rules.ShaftSealed(1));
        var path = rules.RoutePath();
        Assert.AreEqual(new Vector2Int(-1, rules.DungeonGateX), path[0]);
        Assert.AreNotEqual(TowerRules.CoreX, rules.DungeonGateX);
        Assert.AreEqual(new Vector2Int(-3, TowerRules.CoreX), path[path.Count - 1], "the walk ends at the vault");
        Assert.IsTrue(path.All(c => c.x < 0), "never on the Tower's floors");
        var b1 = path.Where(c => c.x == -1).Select(c => c.y).ToList();
        Assert.AreEqual(rules.RouteEast(-1) - rules.RouteWest(-1) + 1, b1.Distinct().Count(), "B1 is crossed end to end");
    }

    // ---- TT 10.5.0b: the player builds the Gates; each closes an end of the ground floor --------------------------

    [Test]
    public void TheFoundingGateIsBuiltByThePlayer()
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        Assert.AreEqual("gate", rules.State.introPhase);
        Assert.IsTrue(rules.GateBuildable());
        StringAssert.Contains("ground floor", rules.BuildGate(1, 23));
        StringAssert.Contains("left or right", rules.BuildGate(0, TowerRules.CoreX), "the shaft is not an end");
        Assert.IsNull(rules.BuildGate(0, 23), "the first Gate is free");
        Assert.AreEqual("shack", rules.State.introPhase);
        Assert.AreEqual(TowerRules.CoreX + 1, rules.GateOn(1).x);
        StringAssert.Contains("already has", rules.BuildGate(0, 23));
    }

    [Test]
    public void TheSecondGateCostsGoldAndClosesTheOtherEnd()
    {
        var rules = Started();
        Assert.IsNull(rules.GateOn(-1), "no automatic west Gate with player-built Gates");
        Assert.IsTrue(rules.GateBuildable());
        rules.State.gold = 10;
        StringAssert.Contains("80 gold", rules.BuildGate(0, rules.GateEndX(-1)));
        rules.State.gold = 100;
        Assert.IsNull(rules.BuildGate(0, rules.GateEndX(-1) + 1), "a tap on the last founded cell takes that end");
        Assert.AreEqual(20, rules.State.gold);
        Assert.AreEqual(TowerRules.CoreX - rules.Floor(0).west - 1, rules.GateOn(-1).x);
        Assert.IsFalse(rules.GateBuildable(), "both ends are closed");
        rules.State.celestium = 999;
        int before = rules.GateOn(1).x;
        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.AreEqual(before + 1, rules.GateOn(1).x, "the Gate steps out with its end");
    }

    [Test]
    public void DiggingPushesTheLivingBandAndTheVaultDown()
    {
        var rules = Started();
        Assert.IsNull(rules.Build("kitchen", -2, 21));
        int kitchen = rules.State.rooms.Find(r => r.type == "kitchen" && r.floor == -2).uid;
        Assert.IsNull(rules.LairDigFloor());
        Assert.AreEqual("dungeon", rules.FloorKind(-2));
        Assert.AreEqual(-3, rules.Room(kitchen).floor);
        Assert.AreEqual(-4, rules.VaultFloor);
        StringAssert.Contains("holds 2 dungeon floors", rules.LairDigFloor(), "Heart F: two dungeon floors");
        StringAssert.Contains("keeps 1 living floors", rules.LairAddLivingFloor());
        rules.State.heartRank = 3;
        Assert.IsNull(rules.OpenFloor(-5), "OPEN BELOW adds a living floor in one world");
        Assert.AreEqual(2, rules.State.lair.livingFloors);
        Assert.AreEqual(-5, rules.VaultFloor);
        Assert.AreEqual("living", rules.FloorKind(-4));
        Assert.IsNull(rules.OpenFloor(1), "the Tower still grows up as before");
    }
}
