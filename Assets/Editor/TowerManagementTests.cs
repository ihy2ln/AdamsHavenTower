using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// TT 10.30.0: the management layer over the Fallout Shelter loop. Layout tools (floor caps, move, demolish),
// RimWorld colony depth, Skylines-style districts, postings, auto expeditions, outposts and Gate sieges.
// Kept apart from TowerSimulationTests, which the Expedition work also edits.
public sealed class TowerManagementTests
{
    [SetUp] public void InstantBuilds() { TowerRules.InstantConstruction = true; }
    [TearDown] public void TimedBuilds()
    {
        TowerRules.InstantConstruction = false;
        TowerRules.Today = () => System.DateTime.Now.Date;
        TowerRules.NowUnix = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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

    private static TowerRules Quiet(TowerRules rules)
    {
        rules.State.eventCooldown = 100000;
        return rules;
    }

    // A started tower with money to spend and a ground floor founded six cells west (five east).
    private static TowerRules Lot()
    {
        var rules = Quiet(Started());
        rules.State.gold = 99999; rules.State.wood = rules.State.stone = 999; rules.State.celestium = 999;
        var ground = rules.Floor(0);
        ground.west = Mathf.Max(ground.west, 6);
        ground.east = Mathf.Max(ground.east, 5);
        rules.PlaceGates();
        return rules;
    }

    // ---- Phase 1: layout tools ------------------------------------------------------------------------------

    [Test]
    public void FloorsOpenOnlyAsHighAsTheHeartAllows()
    {
        var rules = Lot();
        Assert.IsNull(rules.OpenFloor(1));
        Assert.IsNull(rules.OpenFloor(2));
        StringAssert.Contains("rank E", rules.OpenFloor(3), "an F Heart reaches two floors up");
        Assert.IsNull(rules.OpenFloor(-1));
        StringAssert.Contains("rank E", rules.OpenFloor(-2), "and one down");
        rules.State.heartRank = 2;
        Assert.IsNull(rules.OpenFloor(3));
        Assert.IsNull(rules.OpenFloor(-2));
        Assert.AreEqual(24, TowerRules.MaxFloorUp(9));
        Assert.AreEqual(24, TowerRules.MaxFloorDown(9));
        for (int rank = 2; rank <= 9; rank++)
        {
            Assert.GreaterOrEqual(TowerRules.MaxFloorUp(rank), TowerRules.MaxFloorUp(rank - 1));
            Assert.GreaterOrEqual(TowerRules.MaxFloorDown(rank), TowerRules.MaxFloorDown(rank - 1));
        }
    }

    [Test]
    public void CheckpointFloorsAboveTheCapAreKept()
    {
        var state = TowerMilestones.Create(7);
        var rules = new TowerRules(state);
        int top = state.floors.Max(f => f.number);
        Assert.Greater(top, TowerRules.MaxFloorUp(state.heartRank), "this checkpoint reaches past its Heart's cap");
        Assert.IsNotNull(rules.Floor(top), "floors already open stay open");
        state.celestium = 99999;
        StringAssert.Contains("Heart", rules.OpenFloor(top + 1));
    }

    [Test]
    public void MovedRoomKeepsItsWorkersRankAndProgress()
    {
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var kitchen = rules.RoomAt(0, 20);
        var worker = rules.State.residents[0];
        Assert.IsNull(rules.Assign(worker.id, kitchen.uid));
        kitchen.level = 2; kitchen.progress = 0.4f; kitchen.condition = 77;
        Assert.IsNull(rules.OpenFloor(1));
        rules.Floor(1).west = 4;
        int uid = kitchen.uid, gold = rules.State.gold, cost = rules.MoveCost(kitchen);
        int stamp = rules.LayoutStamp;
        Assert.IsNull(rules.MoveRoom(uid, 1, 21));
        Assert.AreSame(kitchen, rules.Room(uid));
        Assert.AreEqual(1, kitchen.floor);
        Assert.AreEqual(21, kitchen.x);
        Assert.AreEqual(2, kitchen.level);
        Assert.AreEqual(0.4f, kitchen.progress, 1e-4f);
        Assert.AreEqual(77f, kitchen.condition, 1e-3f);
        Assert.AreEqual(uid, worker.jobRoom, "the worker follows the room");
        Assert.AreEqual(gold - cost, rules.State.gold);
        Assert.IsNull(rules.RoomAt(0, 20), "the old lot is free again");
        Assert.Greater(rules.LayoutStamp, stamp);
    }

    [Test]
    public void MoveIgnoresItselfButRefusesOccupiedCells()
    {
        var rules = Lot();
        Assert.IsNull(rules.Build("well", 0, 20));
        Assert.IsNull(rules.Build("kitchen", 0, 19));
        var kitchen = rules.RoomAt(0, 19);
        kitchen.level = 4; kitchen.width = 2; kitchen.x = 17;   // a two-bay kitchen on cells 17-18
        StringAssert.Contains("occupies", rules.MoveRoom(kitchen.uid, 0, 19), "cell 20 holds the Well");
        StringAssert.Contains("against", rules.MoveRoom(kitchen.uid, 0, 16), "it cannot lean on its own old cell");
        Assert.IsNull(rules.MoveRoom(kitchen.uid, 0, 18), "sliding onto its own old cell is fine");
        Assert.AreEqual(18, kitchen.x);
        StringAssert.Contains("already", rules.MoveRoom(kitchen.uid, 0, 18));
        Assert.IsNotNull(rules.MoveRoom(rules.State.rooms.First(r => r.type == "heart").uid, 1, 21));
    }

    [Test]
    public void MovedRoomGrowsAwayFromTheShaft()
    {
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var kitchen = rules.RoomAt(0, 20);
        Assert.IsFalse(kitchen.flip);
        Assert.IsNull(rules.OpenFloor(1));
        rules.Floor(1).east = 3;
        Assert.IsNull(rules.MoveRoom(kitchen.uid, 1, TowerRules.CoreX + 1));
        Assert.IsTrue(kitchen.flip, "east of the shaft new bays grow to the right");
        StringAssert.Contains("foundation", rules.MoveRoom(kitchen.uid, 1, TowerRules.CoreX - 5));
    }

    [Test]
    public void StewardLeavesThePlayersPickAlone()
    {
        // TT 10.4.3: dragging Kaela to the Kitchen was undone 20 s later by the Steward re-staffing the Well.
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 23));
        Assert.IsNull(rules.Build("well", 0, 20));
        var kaela = rules.State.residents[0];
        var kitchen = rules.RoomAt(0, 23);
        rules.State.water = 3; rules.State.food = 90;   // water is the urgent stock
        Assert.IsNull(rules.AssignByPlayer(kaela.id, kitchen.uid));
        rules.Advance(60, true);
        Assert.AreEqual(kitchen.uid, kaela.jobRoom, "the Steward undid the player's assignment");
        rules.State.clock += TowerRules.PlayerAssignRespect;
        Assert.IsFalse(rules.PlayerPinned(kaela), "after a day the Steward may step in again");
    }

    [Test]
    public void TiredResidentRestsInsteadOfFlippingEveryTick()
    {
        // TT 10.4.3 regression: rest hovering at 25 flipped the plan between "rest" and a meal errand every
        // frame; each flip restarted the walk and the resident never arrived anywhere.
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 23));
        var kaela = rules.State.residents[0];
        Assert.IsNull(rules.Assign(kaela.id, rules.RoomAt(0, 23).uid));
        kaela.rest = 24.9f; kaela.hunger = 44; kaela.thirst = 72;
        int flips = 0;
        string last = kaela.currentTask;
        for (int i = 0; i < 120; i++)
        {
            rules.Advance(0.25f, true);
            if (kaela.currentTask != last) { flips++; last = kaela.currentTask; }
        }
        Assert.LessOrEqual(flips, 2, "the plan keeps changing its mind");
        Assert.AreEqual("rest", kaela.currentTask);
        Assert.AreEqual(kaela.homeRoom, kaela.currentRoom, "she made it home");
    }

    [Test]
    public void SellBackFallsToNothingThenDeconstructs()
    {
        var rules = Lot();
        int gold = rules.State.gold, wood = rules.State.wood;
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var kitchen = rules.RoomAt(0, 20);
        int cost = gold - rules.State.gold;
        Assert.AreEqual(cost, rules.DemolishRefund(kitchen));
        rules.State.clock += TowerRules.SellWindow / 2;
        Assert.AreEqual(cost / 2, rules.DemolishRefund(kitchen), 1, "half the window, half the money");
        rules.State.clock += TowerRules.SellWindow;
        Assert.AreEqual(0, rules.DemolishRefund(kitchen));
        TowerRules.InstantConstruction = false;
        int before = rules.State.gold;
        Assert.IsNull(rules.Demolish(kitchen.uid), "past the window it is deconstructed");
        Assert.IsNotNull(rules.Room(kitchen.uid), "deconstruction takes time");
        Assert.IsNotNull(rules.DeconstructWork(kitchen.uid));
        Assert.IsNotNull(rules.Demolish(kitchen.uid), "already coming down");
        rules.Advance(rules.DeconstructSeconds(kitchen) + 2, false);
        Assert.IsNull(rules.Room(kitchen.uid));
        Assert.LessOrEqual(rules.State.gold, before + 50, "nothing comes back after the window");
    }

    [Test]
    public void SellingAnUpgradeUndoesIt()
    {
        var rules = Lot();
        rules.State.heartRank = 5;
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var kitchen = rules.RoomAt(0, 20);
        rules.State.clock += TowerRules.SellWindow + 1;   // the build itself is past its window
        int gold = rules.State.gold;
        Assert.IsNull(rules.UpgradeRoom(kitchen.uid));
        Assert.AreEqual(2, kitchen.level);
        Assert.IsTrue(rules.SellingUndoesUpgrade(kitchen));
        Assert.IsNull(rules.Demolish(kitchen.uid));
        Assert.AreEqual(1, kitchen.level, "the upgrade is undone, the room stays");
        Assert.IsNotNull(rules.Room(kitchen.uid));
        Assert.AreEqual(gold, rules.State.gold, "all of the upgrade gold back");
    }

    [Test]
    public void CancellingConstructionRefundsEverything()
    {
        var rules = Lot();
        TowerRules.InstantConstruction = false;
        int gold = rules.State.gold, wood = rules.State.wood, stone = rules.State.stone, celestium = rules.State.celestium;
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.AreEqual(2, rules.State.works.Count);
        foreach (var work in rules.State.works.ToArray()) Assert.IsNull(rules.CancelWork(work));
        Assert.AreEqual(0, rules.State.works.Count);
        Assert.AreEqual(gold, rules.State.gold);
        Assert.AreEqual(wood, rules.State.wood);
        Assert.AreEqual(stone, rules.State.stone);
        Assert.AreEqual(celestium, rules.State.celestium);
    }

    [Test]
    public void DemolishSellsBackAndSendsOccupantsHome()
    {
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        Assert.IsNull(rules.Build("house", 0, 19));
        var kitchen = rules.RoomAt(0, 20);
        var spare = rules.RoomAt(0, 19);
        var shack = rules.RoomAt(0, 21);
        var worker = rules.State.residents[0];
        Assert.AreEqual(shack.uid, worker.homeRoom);
        Assert.IsNull(rules.Assign(worker.id, kitchen.uid));
        worker.currentRoom = worker.targetRoom = kitchen.uid;
        worker.currentTask = "production";
        Assert.AreEqual(rules.State.receipts.Find(r => r.room == kitchen.uid).gold, rules.DemolishRefund(kitchen),
            "sold back at 100% the moment it is built");
        int gold = rules.State.gold;
        int refund = rules.DemolishRefund(kitchen);
        Assert.IsNull(rules.Demolish(kitchen.uid));
        Assert.AreEqual(gold + refund, rules.State.gold);
        Assert.IsNull(rules.Room(kitchen.uid));
        Assert.AreEqual(0, worker.jobRoom);
        Assert.AreEqual(shack.uid, worker.currentRoom, "walked home");
        Assert.IsNull(rules.Demolish(shack.uid));
        Assert.AreEqual(spare.uid, worker.homeRoom, "the homeless take a free bed");
        Assert.IsNotNull(rules.Demolish(rules.State.rooms.First(r => r.type == "heart").uid));
    }

    [Test]
    public void OlderSavesGainColonyDefaultsOnce()
    {
        var fresh = Started().State;
        fresh.colonyVersion = 0; fresh.colonyRandom = 0;
        var old = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(fresh));
        new TowerRules(old);
        Assert.AreEqual(TowerRules.ColonyVersion, old.colonyVersion);
        Assert.AreNotEqual(0, old.colonyRandom);
        int seed = old.colonyRandom, main = old.randomState;
        new TowerRules(old);
        Assert.AreEqual(seed, old.colonyRandom, "the one-time step runs once");
        Assert.AreEqual(main, old.randomState, "the colony layer never draws on the main stream");
    }

    // ---- Phase 2: RimWorld colony depth ---------------------------------------------------------------------

    private static TowerResident Recruit(TowerRules rules)
    {
        rules.State.pendingVisitors = 1;
        if (rules.BiologicalPopulation() >= rules.PopulationCap())
            Assert.IsNull(rules.Build("house", 0, rules.State.rooms.Where(r => r.floor == 0 && r.x < TowerRules.CoreX).Min(r => r.x) - 1));
        Assert.IsNull(rules.RecruitVisitor());
        return rules.State.residents.Last();
    }

    [Test]
    public void NewRecruitsGetASecondTraitAndABackstory()
    {
        var rules = Lot();
        var recruit = Recruit(rules);
        Assert.IsNotEmpty(recruit.trait2);
        Assert.AreNotEqual(recruit.trait, recruit.trait2);
        Assert.IsTrue(TowerRules.HasTrait(recruit, recruit.trait2));
        Assert.IsNotNull(TowerRules.Backstory(recruit.backstory));
        StringAssert.Contains(TowerRules.Backstory(recruit.backstory).name, TowerRules.TraitLine(recruit));
        var founder = rules.State.residents[0];
        Assert.AreEqual("", founder.trait2, "founders keep the one trait they always had");
        Assert.AreEqual("", founder.backstory);
        // The depth rolls come from the colony stream: the main stream lands in the same place either way.
        var a = Lot(); var b = Lot();
        b.State.colonyRandom = 12345;
        Recruit(a); Recruit(b);
        Assert.AreEqual(a.State.randomState, b.State.randomState);
    }

    [Test]
    public void IncapableResidentRefusesThatJob()
    {
        var rules = Lot();
        var person = rules.State.residents[0];
        person.backstory = "hedge_knight";
        StringAssert.Contains("will not", rules.SetPriority(person.id, "care", 2));
        Assert.IsNull(rules.SetPriority(person.id, "care", 0));
        Assert.IsNull(rules.SetPriority(person.id, "defense", 3));
        Assert.AreEqual("care", TowerRules.Incapable(person));
        foreach (var story in TowerRules.Backstories) Assert.AreNotEqual("production", story.incapable);
    }

    [Test]
    public void MiserableVillagerLeavesButHeroesStay()
    {
        var rules = Lot();
        var villager = Recruit(rules);
        var hero = rules.State.residents[0];
        rules.State.food = rules.State.water = 999; rules.State.firewood = 500;
        for (int i = 0; i < 1300 && rules.State.residents.Contains(villager); i++)
        {
            villager.happiness = 0; hero.happiness = 0;
            rules.Advance(1, true);
        }
        Assert.IsFalse(rules.State.residents.Contains(villager), "the villager walked out");
        Assert.IsTrue(rules.State.residents.Contains(hero), "heroes are bound to the Heart");
        Assert.AreEqual(1, rules.Counter("departed"));
        Assert.AreEqual(0, rules.State.memorial.Count, "leaving is not dying");
        Assert.IsFalse(rules.State.bonds.Exists(b => b.a == villager.id || b.b == villager.id));
    }

    [Test]
    public void OfflineMiseryNeverEmptiesTheTower()
    {
        var rules = Lot();
        var villager = Recruit(rules);
        rules.State.food = rules.State.water = 999; rules.State.firewood = 500;
        villager.happiness = 0;
        rules.CatchUp(3 * 3600);
        Assert.IsTrue(rules.State.residents.Contains(villager));
        Assert.AreEqual(0f, villager.leaveSeconds);
    }

    [Test]
    public void LongContentmentBringsAnInspiration()
    {
        var rules = Lot();
        var person = rules.State.residents[0];
        rules.State.food = rules.State.water = 999; rules.State.firewood = 500;
        for (int i = 0; i < 760 && person.inspirationSeconds <= 0; i++) { person.happiness = 100; rules.Advance(1, true); }
        Assert.Greater(person.inspirationSeconds, 0f);
        Assert.AreEqual(TowerRules.InspirationFor(person), person.inspiration);
        Assert.AreEqual(1.5f, TowerRules.InspirationBonus(person, person.inspiration));
        Assert.AreEqual(1, rules.Counter("inspired"));
        Assert.IsTrue(rules.Thoughts(person).Exists(t => t.label == TowerRules.InspirationLabel(person.inspiration)));
        for (int i = 0; i < 250; i++) rules.Advance(1, true);
        Assert.AreEqual(1f, TowerRules.InspirationBonus(person, "work"), "an inspiration runs out");
    }

    [Test]
    public void BalancedStorytellerKeepsTheOldPace()
    {
        var rules = Lot();
        Assert.AreEqual("balanced", rules.State.storyteller);
        var teller = rules.Storyteller;
        Assert.AreEqual(1f, teller.delay);
        Assert.AreEqual(1f, teller.positive);
        Assert.AreEqual(35, teller.raidThreat);
        Assert.IsTrue(teller.doubleRaids);
        Assert.AreEqual(2, teller.maxIncidents);
        var old = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        old.storyteller = "";   // a save from before storytellers
        Assert.AreEqual("balanced", new TowerRules(old).State.storyteller, "older saves get the usual storyteller");
    }

    // Runs two game hours of live events with incidents cleared every second, so nothing blocks the next roll.
    private static void Storytold(string teller, out int events, out int raids)
    {
        var rules = Lot();
        Assert.IsNull(rules.SetStoryteller(teller));
        rules.State.eventCooldown = 10;
        rules.State.food = rules.State.water = 999; rules.State.firewood = 500;
        raids = 0;
        for (int i = 0; i < 7200; i++)
        {
            rules.State.threat = 30;
            rules.Advance(1, true);
            raids += rules.State.incidents.Count(n => n.kind == "raiders");
            rules.State.incidents.Clear();
        }
        events = Mathf.RoundToInt(rules.State.eventTimer);
    }

    [Test]
    public void CalmSpacesIncidentsAndChaoticCrowdsThem()
    {
        int calm, balanced, chaotic, calmRaids, balancedRaids, chaoticRaids;
        Storytold("calm", out calm, out calmRaids);
        Storytold("balanced", out balanced, out balancedRaids);
        Storytold("chaotic", out chaotic, out chaoticRaids);
        Assert.Less(calm, balanced);
        Assert.Less(balanced, chaotic);
        Assert.AreEqual(0, balancedRaids, "threat 30 is below the usual raid line");
        Assert.AreEqual(0, calmRaids);
        Assert.Greater(chaoticRaids, 0, "the Briar's Whim sends raiders from threat 25");
    }

    [Test]
    public void StorytellerCarriesIntoALegacyRun()
    {
        var rules = Lot();
        Assert.IsNull(rules.SetStoryteller("chaotic"));
        Assert.AreEqual("chaotic", TowerRules.LegacyRun(rules.State).storyteller);
        StringAssert.Contains("Unknown", rules.SetStoryteller("nobody"));
        Assert.IsNull(rules.CycleStoryteller());
        Assert.AreEqual("calm", rules.State.storyteller, "the button cycles calm, balanced, chaotic");
    }

    // ---- Phase 3: districts (Cities: Skylines zoning on floor bands) ----------------------------------------

    private static void OpenFloors(TowerRules rules, int top)
    {
        for (int f = 1; f <= top; f++) if (rules.Floor(f) == null) Assert.IsNull(rules.OpenFloor(f));
    }

    [Test]
    public void DistrictsAreContiguousBandsThatNeverOverlap()
    {
        var rules = Lot();
        rules.State.heartRank = 6;   // three districts
        OpenFloors(rules, 3);
        Assert.IsNull(rules.CreateDistrict(1));
        var a = rules.State.districts[0];
        Assert.IsNull(rules.ResizeDistrict(a.id, 1, 2));
        Assert.IsNull(rules.CreateDistrict(3));
        var b = rules.State.districts[1];
        StringAssert.Contains("already belongs", rules.ResizeDistrict(b.id, 2, 3));
        StringAssert.Contains("already belongs", rules.CreateDistrict(2));
        StringAssert.Contains("not open", rules.ResizeDistrict(b.id, 3, 4));
        StringAssert.Contains("at least one", rules.ResizeDistrict(b.id, 3, 2));
        Assert.AreSame(a, rules.DistrictAt(2));
        Assert.AreSame(b, rules.DistrictAt(3));
        Assert.IsNull(rules.DistrictAt(0));
        Assert.AreNotEqual(a.name, b.name);
        Assert.IsNull(rules.DissolveDistrict(a.id));
        Assert.IsNull(rules.DistrictAt(2));
        var reloaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.AreEqual(3, reloaded.DistrictAt(3).floorMin, "districts survive a save");
    }

    [Test]
    public void DistrictCountFollowsHeartRank()
    {
        var rules = Lot();
        Assert.AreEqual(0, TowerRules.MaxDistricts(1));
        StringAssert.Contains("rank E", rules.CreateDistrict(0));
        rules.State.heartRank = 2;
        Assert.IsNull(rules.CreateDistrict(0));
        Assert.IsNull(rules.OpenFloor(1));
        StringAssert.Contains("allows 1", rules.CreateDistrict(1));
        Assert.AreEqual(6, TowerRules.MaxDistricts(9));
    }

    [Test]
    public void PureIndustryDistrictProducesMore()
    {
        var rules = Lot();
        rules.State.heartRank = 2;
        rules.State.food = rules.State.water = 500;
        Assert.IsNull(rules.OpenFloor(1));
        Assert.IsNull(rules.Build("kitchen", 1, TowerRules.CoreX - 1));
        var kitchen = rules.RoomAt(1, TowerRules.CoreX - 1);
        Assert.IsNull(rules.Assign(rules.State.residents[0].id, kitchen.uid));
        rules.Advance(15, true);
        float plain = rules.ProductionRate(kitchen);
        Assert.Greater(plain, 0f);
        Assert.AreEqual(1f, rules.DistrictBonus(kitchen));
        Assert.IsNull(rules.CreateDistrict(1));
        int id = rules.State.districts[0].id;
        Assert.IsNull(rules.SetDistrictSpec(id, "industry"));
        Assert.AreEqual(1f, rules.DistrictPurity(rules.State.districts[0]), 1e-4f, "only the Kitchen on this floor");
        Assert.AreEqual(plain * 1.15f, rules.ProductionRate(kitchen), plain * 1e-3f);
        Assert.IsNull(rules.TogglePolicy(id, "overtime"));
        Assert.AreEqual(plain * 1.15f * 1.15f, rules.ProductionRate(kitchen), plain * 1e-3f);
        Assert.IsTrue(rules.Thoughts(rules.State.residents[0]).Exists(t => t.label == "Overtime"));
        Assert.IsNull(rules.CycleDistrictSpec(id));
        Assert.AreEqual("market", rules.State.districts[0].spec);
        Assert.AreEqual(plain * 1.15f, rules.ProductionRate(kitchen), plain * 1e-3f, "a Market district does not speed a Kitchen");
    }

    [Test]
    public void PolicyUpkeepIsChargedAndUnpaidPoliciesLapse()
    {
        var rules = Lot();
        rules.State.heartRank = 2;
        Assert.IsNull(rules.CreateDistrict(0));
        int id = rules.State.districts[0].id;
        Assert.IsNull(rules.TogglePolicy(id, "festival_days"));
        Assert.AreEqual(80, rules.PolicyUpkeepPerDay());
        rules.State.gold = 1000;
        rules.Advance(TowerRules.DaySeconds, true);
        Assert.AreEqual(920, rules.State.gold, 1, "one game day of Festival Days");
        var person = rules.State.residents[0];
        Assert.IsTrue(rules.Thoughts(person).Exists(t => t.label == "Festival days"));
        rules.State.gold = 0;
        rules.Advance(30, true);
        Assert.IsTrue(rules.State.policiesLapsed);
        Assert.IsFalse(rules.PolicyOn(0, "festival_days"));
        Assert.IsFalse(rules.Thoughts(person).Exists(t => t.label == "Festival days"));
        rules.State.gold = 500;
        rules.Advance(30, true);
        Assert.IsFalse(rules.State.policiesLapsed);
        Assert.IsTrue(rules.PolicyOn(0, "festival_days"));
        Assert.IsNull(rules.TogglePolicy(id, "festival_days"));
        Assert.AreEqual(0, rules.PolicyUpkeepPerDay());
    }

    [Test]
    public void RationingStretchesFoodButSoursMood()
    {
        var rules = Lot();
        rules.State.heartRank = 2;
        var person = rules.State.residents[0];
        float upkeep = rules.UpkeepPerMinute("food"), mood = rules.MoodTarget(person);
        Assert.IsNull(rules.CreateDistrict(0));
        Assert.IsNull(rules.TogglePolicy(rules.State.districts[0].id, "rationing"));
        Assert.AreEqual(upkeep * 0.8f, rules.UpkeepPerMinute("food"), upkeep * 1e-3f);
        Assert.IsTrue(rules.Thoughts(person).Exists(t => t.label == "Rationing"));
        Assert.Less(rules.MoodTarget(person), mood);
    }

    [Test]
    public void CoverageReachesOnlyNearbyFloors()
    {
        var rules = Lot();
        rules.State.heartRank = 9;
        OpenFloors(rules, 5);
        rules.State.blueprints.Add("frosted_mug");
        Assert.IsNull(rules.Build("frosted_mug", 2, TowerRules.CoreX - 1));
        var mug = rules.RoomAt(2, TowerRules.CoreX - 1);
        Assert.IsFalse(rules.Covered(2, TowerRules.MedicalService), "an empty Mug serves nobody");
        Assert.IsNull(rules.Assign(rules.State.residents[0].id, mug.uid));
        rules.Advance(TowerRules.CoverageRefresh + 1, true);
        Assert.IsTrue(rules.Covered(1, TowerRules.MedicalService));
        Assert.IsTrue(rules.Covered(3, TowerRules.MedicalService));
        Assert.IsFalse(rules.Covered(4, TowerRules.MedicalService), "a rank F Mug reaches one floor each way");
        Assert.IsFalse(rules.Covered(0, TowerRules.MedicalService));
        mug.level = 3;
        rules.Advance(TowerRules.CoverageRefresh + 1, true);
        Assert.IsTrue(rules.Covered(4, TowerRules.MedicalService), "rank D reaches two");
        Assert.IsFalse(rules.Covered(0, TowerRules.SafetyService), "unguarded Gates keep nobody safe");
    }

    [Test]
    public void DistrictCachesRebuildOnlyOnLayoutChange()
    {
        var rules = Lot();
        rules.Appeal(0);
        int stamp = rules.OverlayStamp;
        for (int i = 0; i < 20; i++) { rules.Appeal(0); rules.DistrictAt(0); rules.Covered(0, TowerRules.FoodService); }
        Assert.AreEqual(stamp, rules.OverlayStamp, "lookups alone never rebuild");
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        rules.Appeal(0);
        Assert.Greater(rules.OverlayStamp, stamp, "a new room does");
    }

    [Test]
    public void LovelyFloorsCheerAndDrawWanderersFaster()
    {
        var rules = Lot();
        rules.State.heartRank = 2;
        var person = rules.State.residents[0];
        float plain = rules.GateArrivalFactor();
        Assert.IsFalse(rules.Thoughts(person).Exists(t => t.label == "Lovely district"));
        Assert.IsNull(rules.CreateDistrict(0));
        Assert.IsNull(rules.TogglePolicy(rules.State.districts[0].id, "beautification"));
        rules.RoomAt(0, 21).level = 9;   // a fully ranked, well-kept home
        rules.Advance(TowerRules.CoverageRefresh + 1, true);
        Assert.GreaterOrEqual(rules.Appeal(0), 75f);
        Assert.IsTrue(rules.Thoughts(person).Exists(t => t.label == "Lovely district"));
        Assert.Greater(rules.GateArrivalFactor(), plain);
        Assert.IsNull(rules.TogglePolicy(rules.State.districts[0].id, "open_gate"));
        Assert.AreEqual(4f, rules.DistrictThreat());
    }

    [Test]
    public void DistrictsNeverMakeACheckpointWorse()
    {
        var rules = new TowerRules(TowerMilestones.Create(6));
        var before = TowerRules.Stocks.Select(s => rules.PlannedNetPerMinute(s)).ToArray();
        int low = rules.State.floors.Min(f => f.number), high = rules.State.floors.Max(f => f.number);
        Assert.IsNull(rules.CreateDistrict(0));
        int id = rules.State.districts[0].id;
        Assert.IsNull(rules.ResizeDistrict(id, low, high));
        Assert.IsNull(rules.SetDistrictSpec(id, "industry"));
        for (int i = 0; i < TowerRules.Stocks.Length; i++)
            Assert.GreaterOrEqual(rules.PlannedNetPerMinute(TowerRules.Stocks[i]), before[i] - 1e-3f, TowerRules.Stocks[i]);
    }

    // ---- Phase 4: postings and auto expeditions -------------------------------------------------------------

    private static long clock = 1000000;

    // A started tower with a Guild on floor 1 and a clock the test controls.
    private static TowerRules Guild()
    {
        var rules = Lot();
        rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        if (rules.Floor(1) == null) rules.State.floors.Add(new TowerFloor { number = 1, west = 1, east = 3 });
        rules.State.food = rules.State.water = 500;
        clock = 1000000;
        TowerRules.NowUnix = () => clock;
        return rules;
    }

    private static System.Collections.Generic.List<int> Party(params TowerResident[] heroes)
    {
        return heroes.Select(h => h.id).ToList();
    }

    [Test]
    public void AutoExpeditionTiersFollowThePowerRatio()
    {
        Assert.AreEqual(0, TowerRules.AutoTier(0.59f));
        Assert.AreEqual(1, TowerRules.AutoTier(0.6f));
        Assert.AreEqual(2, TowerRules.AutoTier(0.95f));
        Assert.AreEqual(3, TowerRules.AutoTier(1.3f));
        var hero = new TowerResident { origin = "hero", rank = 9, level = TowerRules.LevelCap,
            might = 10, sight = 10, grit = 10, charm = 10, wit = 10, grace = 10, luck = 10 };
        Assert.AreEqual(70f * 1.72f, TowerRules.HeroPower(hero), 0.01f);
        Assert.Less(3 * TowerRules.HeroPower(hero), TowerRules.AutoDanger.Last() * 1.3f, "the deepest region needs a balanced party for Great");
        var rules = Guild();
        var tank = TowerRoster.All.FirstOrDefault(u => u.IsHero && u.role == "Tank");
        var support = TowerRoster.All.FirstOrDefault(u => u.IsHero && u.role == "Support");
        Assume.That(tank != null && support != null, "the roster JSON is needed for roles");
        var a = rules.AddResident(tank.id, tank.name, "hero", 10);
        var b = rules.AddResident(support.id, support.name, "hero", 10);
        float plain = TowerRules.HeroPower(a) + TowerRules.HeroPower(b);
        Assert.AreEqual(plain * 1.10f, rules.PartyPower(Party(a, b)), 0.01f, "Tank and Support together");
    }

    [Test]
    public void AutoExpeditionRunsOnTheRealClockAndNeverConquers()
    {
        var rules = Guild();
        var kaela = rules.State.residents[0];
        int gold = rules.State.gold;
        float food = rules.State.food;
        Assert.IsNull(rules.SendAuto("silverbrook_edge", Party(kaela)));
        Assert.AreEqual(food - TowerRules.AutoFood, rules.State.food, 0.01f);
        Assert.IsTrue(kaela.exploring);
        Assert.AreEqual("auto", kaela.posting);
        Assert.AreEqual(0, kaela.jobRoom);
        StringAssert.Contains("already", rules.SendAuto("silverbrook_edge", Party(kaela)));
        rules.Advance(120, true);
        Assert.IsNotNull(rules.AutoRun, "half an hour of real time has not passed");
        clock += 1800;
        rules.Advance(1, true);
        Assert.IsNull(rules.AutoRun);
        Assert.AreEqual("", kaela.posting);
        Assert.IsFalse(kaela.exploring);
        Assert.Greater(rules.State.gold, gold);
        Assert.IsNotEmpty(rules.State.autoReport);
        Assert.AreEqual(1, rules.Counter("expedition"));
        Assert.IsFalse(rules.State.regionsConquered.Contains("silverbrook_edge"), "auto runs never conquer");
    }

    [Test]
    public void HeroesOnAutoRunsCannotWorkOrJoinAPlayedRun()
    {
        var rules = Guild();
        var kaela = rules.State.residents[0];
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        Assert.IsNull(rules.SendAuto("silverbrook_edge", Party(kaela)));
        StringAssert.Contains("unavailable", rules.Assign(kaela.id, rules.RoomAt(0, 20).uid));
        StringAssert.Contains("cannot travel", rules.StartExpedition("silverbrook_edge",
            new System.Collections.Generic.List<string> { "kaela" }, 2, 0, 0));
        float hunger = kaela.hunger;
        rules.Advance(300, true);
        Assert.AreEqual(hunger, kaela.hunger, "the road feeds them, not the Tower's stores");
        Assert.IsNull(rules.Recall(kaela.id), "RECALL on a posted hero brings the party home");
        Assert.IsNull(rules.AutoRun);
        Assert.IsFalse(kaela.exploring);
    }

    [Test]
    public void PostingsSurviveSaveAndLoad()
    {
        var rules = Guild();
        var kaela = rules.State.residents[0];
        Assert.IsNull(rules.SendAuto("silverbrook_edge", Party(kaela)));
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        var again = loaded.Resident(kaela.id);
        Assert.IsTrue(loaded.State.hasAutoRun);
        Assert.AreEqual("auto", again.posting);
        Assert.IsTrue(again.exploring, "the expedition code's away flag does not clear a posting");
        Assert.AreEqual(kaela.id, loaded.AutoRun.party[0]);
    }

    [Test]
    public void NewSystemsLeaveTheMainRandomStreamUntouched()
    {
        int[] streams = new int[2];
        for (int run = 0; run < 2; run++)
        {
            var rules = Guild();
            rules.State.colonyRandom = run == 0 ? 11 : 977;
            rules.State.regionsConquered.Add("silverbrook_edge");
            Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
            Assert.IsNull(rules.Station(rules.State.residents[0].id, "silverbrook_edge"));
            rules.Outpost("silverbrook_edge").threat = 100;
            for (int i = 0; i < 2000; i++) rules.Advance(1, true);
            streams[run] = rules.State.randomState;
        }
        Assert.AreEqual(streams[0], streams[1], "outposts, caravans and raids roll on the colony stream");
    }

    // ---- Phase 5: outposts -------------------------------------------------------------------------------------

    [Test]
    public void ConqueredRegionsOfferOutpostSites()
    {
        var rules = Guild();
        Assert.AreEqual(0, rules.OutpostSites().Count);
        rules.State.regionsConquered.Add("silverbrook_edge");
        rules.Advance(1, true);
        Assert.Contains("silverbrook_edge", rules.State.outpostSitesSeen, "the player is told once");
        CollectionAssert.AreEqual(new[] { "silverbrook_edge" }, rules.OutpostSites());
        StringAssert.Contains("Conquer", rules.FoundOutpost("rootside_camp"));
        int gold = rules.State.gold;
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        Assert.AreEqual(gold - 250, rules.State.gold);
        Assert.IsNotNull(rules.Outpost("silverbrook_edge"));
        Assert.AreEqual(0, rules.OutpostSites().Count);
        StringAssert.Contains("already", rules.FoundOutpost("silverbrook_edge"));
    }

    [Test]
    public void OutpostCapFollowsHeartRank()
    {
        var rules = Guild();
        rules.State.regionsConquered.AddRange(new[] { "silverbrook_edge", "rootside_camp", "shallow_ford" });
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        StringAssert.Contains("hold 1", rules.FoundOutpost("rootside_camp"));
        rules.State.heartRank = 3;
        Assert.IsNull(rules.FoundOutpost("rootside_camp"));
        Assert.AreEqual(8, TowerRules.MaxOutposts(9));
    }

    [Test]
    public void StationedVillagersLeaveAndReturnOnRecall()
    {
        var rules = Guild();
        var villager = Recruit(rules);
        rules.State.regionsConquered.Add("silverbrook_edge");
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        Assert.IsNull(rules.Station(villager.id, "silverbrook_edge"));
        Assert.IsTrue(villager.exploring);
        StringAssert.StartsWith("outpost:", villager.posting);
        StringAssert.Contains("unavailable", rules.Assign(villager.id, rules.RoomAt(0, 20).uid));
        StringAssert.Contains("stationed", rules.TaskExplanation(villager));
        Assert.IsFalse(TowerRules.CanLeave(villager), "stationed villagers do not walk out of the Gate");
        Assert.IsNull(rules.Recall(villager.id));
        Assert.AreEqual("", villager.posting);
        Assert.IsFalse(villager.exploring);
        Assert.AreEqual(0, rules.Outpost("silverbrook_edge").staff.Count);
    }

    [Test]
    public void OutpostsProduceRegionGoodsAndCaravansBringThemHome()
    {
        var rules = Guild();
        var kaela = rules.State.residents[0];
        rules.State.regionsConquered.Add("silverbrook_edge");
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        var outpost = rules.Outpost("silverbrook_edge");
        Assert.AreEqual(0f, rules.OutpostDaily(outpost, "food"), "nobody stationed, nothing made");
        Assert.IsNull(rules.Station(kaela.id, "silverbrook_edge"));
        Assert.Greater(rules.OutpostDaily(outpost, "food"), 0f);
        Assert.AreEqual(rules.OutpostDaily(outpost, "food") / 2f / 40f * 6f, rules.OutpostDaily(outpost, "wood"), 1e-3f,
            "the secondary good comes at half rate");
        rules.State.food = 50;
        rules.Advance(TowerRules.CaravanSeconds + 1, true);
        Assert.AreEqual(1, rules.Counter("caravan"));
        Assert.Greater(rules.State.food, 50f, "the caravan brought food home");
        Assert.IsNotEmpty(outpost.report);
    }

    [Test]
    public void OfflineOutpostRaidsNeverInjure()
    {
        var rules = Guild();
        var kaela = rules.State.residents[0];
        kaela.might = 1; kaela.weapon = 0; kaela.injury = 0;
        rules.State.regionsConquered.Add("silverbrook_edge");
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        Assert.IsNull(rules.Station(kaela.id, "silverbrook_edge"));
        var outpost = rules.Outpost("silverbrook_edge");
        outpost.threat = 100;
        rules.CatchUp(4 * 3600);
        Assert.Greater(rules.Counter("outpost_raid"), 0, "the weak outpost was raided");
        Assert.AreEqual(0f, kaela.injury, "offline raids never wound");
        for (int i = 0; i < 40 && kaela.injury <= 0; i++)
        {
            outpost.threat = 100;
            outpost.raidSeconds = TowerRules.OutpostRaidCheck;
            rules.Advance(1, true);
        }
        Assert.Greater(kaela.injury, 0f, "a live raid can");
        Assert.IsFalse(kaela.downed);
    }

    [Test]
    public void OutpostUpgradesAddStaffUpToTheHeartCap()
    {
        var rules = Guild();
        rules.State.regionsConquered.Add("silverbrook_edge");
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        var outpost = rules.Outpost("silverbrook_edge");
        Assert.AreEqual(2, TowerRules.OutpostSlots(outpost.level));
        Assert.IsNull(rules.UpgradeOutpost("silverbrook_edge"));
        Assert.IsNull(rules.UpgradeOutpost("silverbrook_edge"));
        StringAssert.Contains("Raise", rules.UpgradeOutpost("silverbrook_edge"), "an F Heart stops outposts at D");
        rules.State.heartRank = 2;
        Assert.IsNull(rules.UpgradeOutpost("silverbrook_edge"));
        Assert.AreEqual(4, TowerRules.OutpostSlots(outpost.level));
    }

    // ---- Phase 6: Gate sieges ---------------------------------------------------------------------------------

    [Test]
    public void DireThreatWarnsBeforeASiege()
    {
        var rules = Lot();
        rules.State.siegeCooldown = 0;
        rules.State.eventCooldown = 1;
        rules.State.threat = 90;
        rules.Advance(3, true);
        Assert.IsTrue(rules.SiegeComing);
        Assert.AreEqual(0, rules.State.incidents.Count, "the siege took the event's place");
        Assert.Greater(rules.State.siegeWarning, TowerRules.SiegeWarning - 5);
    }

    [Test]
    public void StrongDefendersWinASiegeAndThreatFalls()
    {
        var rules = Lot();
        for (int i = 0; i < 2; i++)
        {
            var guard = rules.AddResident("", "Warden " + i, "villager", 1);
            guard.might = 10; guard.weapon = 3; guard.priorityDefense = 3;
        }
        Assert.Greater(rules.SiegeDefence(), rules.SiegeWave());
        rules.State.threat = 80;
        rules.State.siegeWarning = 1;
        int gold = rules.State.gold;
        rules.Advance(2, true);
        Assert.AreEqual(1, rules.State.siegesWon);
        Assert.Less(rules.State.threat, 60f);
        Assert.Greater(rules.State.gold, gold);
        Assert.Greater(rules.State.siegeCooldown, TowerRules.DaySeconds);
    }

    [Test]
    public void LostSiegeBreachesTheGate()
    {
        var rules = Lot();
        rules.State.heartRank = 5;
        var kaela = rules.State.residents[0];
        kaela.might = 1; kaela.weapon = 0;
        Assert.Less(rules.SiegeDefence(), rules.SiegeWave());
        rules.State.threat = 80;
        rules.State.siegeWarning = 1;
        rules.Advance(2, true);
        Assert.AreEqual(1, rules.State.siegesLost);
        var raid = rules.State.incidents.FirstOrDefault(i => i.kind == "raiders");
        Assert.IsNotNull(raid, "Last Stand: raiders at a Gate");
        Assert.AreEqual("gate", rules.Room(raid.roomUid).type);
        Assert.Greater(raid.hp, 100f);
    }

    // ---- TT 10.30.1: sieges in Battle Mode, NEW GAME storyteller, long-press, Atlas outposts ----------------

    private static TowerRules Besieged()
    {
        var rules = Lot();
        rules.State.threat = 80;
        rules.State.siegeWarning = 300;
        return rules;
    }

    [Test]
    public void SiegeBattleWinPaysLikeABrokenSiege()
    {
        var rules = Besieged();
        var kaela = rules.State.residents[0];
        int gold = rules.State.gold, xp = kaela.xp + kaela.level * 1000;
        Assert.IsNull(rules.BeginSiegeBattle());
        StringAssert.Contains("already", rules.BeginSiegeBattle());
        rules.Advance(400, true);
        Assert.IsTrue(rules.SiegeComing, "the warning waits while the heroes fight");
        Assert.AreEqual(0, rules.State.siegesWon + rules.State.siegesLost);
        rules.ResolveSiegeBattle(true, rules.SiegeFighters().Count > 0 ? rules.SiegeFighters() : new System.Collections.Generic.List<string> { "kaela" });
        Assert.AreEqual(1, rules.State.siegesWon);
        Assert.IsFalse(rules.SiegeComing);
        Assert.IsFalse(rules.State.siegeBattle);
        Assert.AreEqual(gold + 100 * rules.State.heartRank, rules.State.gold);
        Assert.Greater(kaela.xp + kaela.level * 1000, xp, "the fighters learn from it");
        Assert.Greater(rules.State.siegeCooldown, TowerRules.DaySeconds);
    }

    [Test]
    public void SiegeBattleLossOpensTheLastStand()
    {
        var rules = Besieged();
        Assert.IsNull(rules.BeginSiegeBattle());
        rules.ResolveSiegeBattle(false, null);
        Assert.AreEqual(1, rules.State.siegesLost);
        var raid = rules.State.incidents.FirstOrDefault(i => i.kind == "raiders");
        Assert.IsNotNull(raid);
        Assert.AreEqual("gate", rules.Room(raid.roomUid).type);
        Assert.Greater(raid.hp, 100f);
    }

    [Test]
    public void InterruptedSiegeBattleOffersTheChoiceAgain()
    {
        var rules = Besieged();
        Assert.IsNull(rules.BeginSiegeBattle());
        rules.State.siegeWarning = 5;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.IsFalse(loaded.State.siegeBattle, "a fight cut short by closing the game is not left hanging");
        Assert.GreaterOrEqual(loaded.State.siegeWarning, 60f);
        Assert.IsTrue(loaded.SiegeComing);
    }

    [Test]
    public void SiegeFightersAreBattleHeroesAtHome()
    {
        var rules = Lot();
        var kaela = rules.State.residents[0];
        var ghislaine = rules.AddResident("ghislaine", "Ghislaine", "hero", 30);
        ghislaine.rank = 7;
        var stranger = rules.AddResident("not_a_fighter", "Wanderer Hero", "hero", 30);
        stranger.rank = 9;
        var ids = rules.SiegeFighters();
        CollectionAssert.AreEqual(new[] { "ghislaine", "kaela" }, ids, "strongest first, battle rigs only");
        ghislaine.exploring = true;
        CollectionAssert.AreEqual(new[] { "kaela" }, rules.SiegeFighters(), "heroes away cannot fight at the Gate");
        kaela.injury = 60;
        Assert.AreEqual(0, rules.SiegeFighters().Count);
        rules.State.siegeWarning = 300;
        StringAssert.Contains("No battle-ready hero", rules.BeginSiegeBattle());
    }

    [Test]
    public void SiegeEncounterScalesWithTheHeart()
    {
        var rules = Lot();
        Assert.AreEqual(1, TowerSiegeBattle.Depth(rules));
        Assert.AreEqual("elite", TowerSiegeBattle.Spec(rules).Kind);
        rules.State.heartRank = 6;
        Assert.AreEqual(11, TowerSiegeBattle.Depth(rules));
        Assert.AreEqual("boss", TowerSiegeBattle.Spec(rules).Kind, "from Heart rank B a lair-class foe leads the wave");
        var encounter = TowerSiegeBattle.Encounter(rules);
        Assert.IsNotNull(encounter);
        Assert.AreEqual("GATE SIEGE", encounter.Title);
        Assert.Greater(encounter.Enemies.Count, 0);
        System.Collections.Generic.List<BattleUnit> field, reserve;
        TowerSiegeBattle.Party(rules, new[] { "kaela" }, out field, out reserve);
        Assert.AreEqual(1, field.Count);
        Assert.AreEqual(0, reserve.Count);
        Assert.AreEqual(field[0].MaxHp, field[0].Hp, "siege fighters start at full health");
    }

    [Test]
    public void StorytellerCanBeChosenBeforeFounding()
    {
        var rules = TowerRules.New();
        Assert.AreEqual("dormant", rules.State.introPhase);
        Assert.IsNull(rules.SetStoryteller("chaotic"));
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.IsNull(rules.Build("house", 0, 21));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        Assert.AreEqual("chaotic", rules.State.storyteller, "the founding keeps the pick");
    }

    [Test]
    public void CanStartMoveMatchesTheMoveRules()
    {
        var rules = Lot();
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        var kitchen = rules.RoomAt(0, 20);
        Assert.IsNull(rules.CanStartMove(kitchen.uid));
        StringAssert.Contains("stay", rules.CanStartMove(rules.State.rooms.First(r => r.type == "heart").uid));
        StringAssert.Contains("stay", rules.CanStartMove(rules.State.rooms.First(r => r.type == "gate").uid));
        rules.State.gold = 0;
        StringAssert.Contains("gold", rules.CanStartMove(kitchen.uid));
        rules.State.gold = 999;
        Assert.IsNull(rules.StartIncident("fire", kitchen.uid));
        StringAssert.Contains("incident", rules.CanStartMove(kitchen.uid));
        StringAssert.Contains("incident", rules.CanMoveRoom(kitchen.uid, 0, 19), "placing checks the same rules");
    }

    [Test]
    public void AtlasRedrawsWhenAnOutpostIsFounded()
    {
        var rules = Lot();
        rules.AddRoom("guild_hall", 1, TowerRules.CoreX + 1);
        rules.State.regionsConquered.Add("silverbrook_edge");
        string before = new TowerAtlasSource(rules).Key;
        Assert.IsNull(rules.FoundOutpost("silverbrook_edge"));
        Assert.AreNotEqual(before, new TowerAtlasSource(rules).Key, "the Atlas rebuilds to show the outpost");
    }

    // ---- TT 10.30.2: the Heart's dweller cap, incidents from rank C, region elements -------------------------

    [Test]
    public void HeartRankCapsHowManyPeopleTheTowerHolds()
    {
        var rules = Lot();
        for (int i = 0; i < 6; i++) rules.AddRoom("terrace_row", 1, 10 + i * 2, 9);   // far more beds than people
        Assert.Greater(rules.HousingCap(), 20);
        Assert.AreEqual(8, rules.PopulationCap(), "an F Heart shelters 8");
        Assert.IsTrue(rules.HeartLimitsPopulation());
        rules.State.heartRank = 2;
        Assert.AreEqual(14, rules.PopulationCap());
        rules.State.heartRank = 1;
        for (int i = 0; i < 7; i++) rules.AddResident("", "Settler " + i, "villager", 1).homeRoom = rules.RoomAt(1, 10).uid;
        Assert.AreEqual(8, rules.BiologicalPopulation());
        rules.State.pendingVisitors = 1;
        StringAssert.Contains("Heart shelters 8", rules.RecruitVisitor());
        rules.State.tutorialStep = 7;
        rules.State.food = rules.State.water = rules.State.firewood = 999;
        StringAssert.Contains("Raise the Heart", rules.NeedsAdvice());
        Assert.AreEqual(110, TowerRules.DwellerCap(9));
    }

    [Test]
    public void CheckpointsAboveTheDwellerCapKeepTheirPeople()
    {
        var rules = Quiet(new TowerRules(TowerMilestones.Create(6)));
        int people = rules.BiologicalPopulation();
        Assert.Greater(people, TowerRules.DwellerCap(rules.State.heartRank), "this checkpoint predates the cap");
        for (int i = 0; i < 600; i++) rules.Advance(1, true);
        Assert.GreaterOrEqual(rules.BiologicalPopulation(), people, "nobody is turned out");
        Assert.AreEqual(0, rules.State.pendingVisitors, "but nobody new is drawn in");
    }

    [Test]
    public void IncidentsCanStackHigherFromHeartRankC()
    {
        var rules = Lot();
        Assert.AreEqual(2, rules.MaxActiveIncidents());
        rules.State.heartRank = 4;
        Assert.AreEqual(3, rules.MaxActiveIncidents(), "GDD 9.3: three at once from rank C");
        Assert.IsNull(rules.SetStoryteller("chaotic"));
        Assert.AreEqual(4, rules.MaxActiveIncidents());
    }

    [Test]
    public void HeroesWhoCounterTheRegionStrengthenAnAutoRun()
    {
        Assert.IsTrue(TowerRules.ElementBeats("Fire", "Wind"));
        Assert.IsTrue(TowerRules.ElementBeats("Water", "Fire"));
        Assert.IsTrue(TowerRules.ElementBeats("Light", "Dark"));
        Assert.IsTrue(TowerRules.ElementBeats("Dark", "Light"));
        Assert.IsFalse(TowerRules.ElementBeats("Wind", "Fire"));
        Assert.AreEqual("Wind", TowerRules.RegionElement("rootside_camp"));
        foreach (var region in TowerRules.Regions) Assert.IsNotEmpty(TowerRules.RegionElement(region.id), region.id);
        var rules = Lot();
        var ghislaine = rules.AddResident("ghislaine", "Ghislaine", "hero", 10);
        Assert.AreEqual("Fire", TowerRules.HeroElement(ghislaine), "founding Fighters take their battle element");
        Assert.AreEqual("Tank", TowerRules.HeroRole(ghislaine));
        var party = new System.Collections.Generic.List<int> { ghislaine.id };
        float plain = rules.PartyPower(party);
        Assert.AreEqual(1, rules.ElementCounters(party, "rootside_camp"));
        Assert.AreEqual(plain * 1.10f, rules.PartyPower(party, "rootside_camp"), 0.01f, "Fire counters Rootside's Wind");
        Assert.AreEqual(plain, rules.PartyPower(party, "shallow_ford"), 0.01f, "Fire does not counter Water");
        var kaela = rules.State.residents[0];
        var helda = rules.AddResident("helda", "Helda", "hero", 10);
        var pair = new System.Collections.Generic.List<int> { kaela.id, helda.id };
        Assert.AreEqual((TowerRules.HeroPower(kaela) + TowerRules.HeroPower(helda)) * 1.10f, rules.PartyPower(pair), 0.01f,
            "Kaela (Tank) and Helda (Support) earn the pairing bonus");
    }

    [Test]
    public void NoSiegesOfflineOrWhileEventsAreQuiet()
    {
        var rules = Lot();
        rules.State.siegeCooldown = 0;
        for (int i = 0; i < 600; i++) { rules.State.threat = 90; rules.Advance(1, true); }
        Assert.IsFalse(rules.SiegeComing, "no event was due");
        rules.State.eventCooldown = 1;
        rules.State.threat = 90;
        rules.CatchUp(3600);
        Assert.IsFalse(rules.SiegeComing, "never while the game is closed");
    }

    // ---- TT 10.3.1: summon banners (GDD 8.1) ------------------------------------------------------------------

    private static TowerRules Summoner()
    {
        TowerRules.Today = () => new System.DateTime(2026, 10, 5);   // a Monday
        var rules = Quiet(Started());
        rules.State.freeSummonUsed = true;
        rules.State.sigils = 100000;
        return rules;
    }

    [Test]
    public void StandardBannerIsTheOriginalSummon()
    {
        var a = Summoner(); var b = Summoner();
        for (int i = 0; i < 12; i++)
        {
            Assert.IsNull(a.Summon(10));
            Assert.IsNull(b.Summon(10, "standard"));
            CollectionAssert.AreEqual(a.LastSummon.Select(p => p.unitId + p.rank).ToList(),
                b.LastSummon.Select(p => p.unitId + p.rank).ToList());
        }
        Assert.AreEqual(a.State.randomState, b.State.randomState);
        Assert.AreEqual(a.State.sigils, b.State.sigils);
        var fresh = Quiet(Started());
        fresh.State.sigils = 0;
        Assert.IsNotNull(fresh.Summon(1, "featured"), "the free summon is Standard's alone");
        Assert.IsFalse(fresh.State.freeSummonUsed);
        Assert.IsNull(fresh.Summon(1, "standard"));
        Assert.IsTrue(fresh.State.freeSummonUsed);
    }

    [Test]
    public void FeaturedBannerHalvesTopPullsAndGuaranteesAfterAMiss()
    {
        var rules = Summoner();
        var ssr = TowerRules.FeaturedHero(9); var ss = TowerRules.FeaturedHero(8);
        Assert.IsNotNull(ssr); Assert.IsNotNull(ss);
        Assert.AreEqual(9, ssr.rank); Assert.AreEqual(8, ss.rank);
        int sigils = rules.State.sigils;
        Assert.IsNull(rules.Summon(1, "featured"));
        Assert.IsNull(rules.Summon(10, "featured"));
        Assert.AreEqual(sigils - 110, rules.State.sigils, "10 a pull, 100 a ten-pull");
        int top = 0, hits = 0;
        bool missed = rules.State.featuredMissed;
        for (int n = 0; n < 300; n++)
        {
            Assert.IsNull(rules.Summon(10, "featured"));
            foreach (var pull in rules.LastSummon)
            {
                Assert.AreEqual("hero", pull.kind, "Featured draws heroes only");
                if (pull.rank < 8) continue;
                bool hit = pull.unitId == (pull.rank == 9 ? ssr.id : ss.id);
                if (missed) Assert.IsTrue(hit, "the SS/SSR after a miss is the featured hero");
                missed = !hit;
                top++; if (hit) hits++;
            }
            Assert.AreEqual(missed, rules.State.featuredMissed);
        }
        Assert.Greater(top, 60);
        Assert.That(hits / (float)top, Is.InRange(0.55f, 0.8f), "a 50/50 with a guarantee lands near two thirds");
    }

    [Test]
    public void FeaturedPairRotatesEveryMonday()
    {
        TowerRules.Today = () => new System.DateTime(2026, 10, 5);
        string ssr = TowerRules.FeaturedHero(9).id, ss = TowerRules.FeaturedHero(8).id;
        Assert.AreEqual(new System.DateTime(2026, 10, 12), TowerRules.FeaturedEnds());
        TowerRules.Today = () => new System.DateTime(2026, 10, 11);
        Assert.AreEqual(ssr, TowerRules.FeaturedHero(9).id, "the same week keeps the pair");
        Assert.AreEqual(ss, TowerRules.FeaturedHero(8).id);
        TowerRules.Today = () => new System.DateTime(2026, 10, 12);
        Assert.AreNotEqual(ssr, TowerRules.FeaturedHero(9).id, "Monday brings a new SSR");
        Assert.AreNotEqual(ss, TowerRules.FeaturedHero(8).id, "and a new SS");
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int week = 0; week < 8; week++)
        {
            int w = week;
            TowerRules.Today = () => new System.DateTime(2026, 10, 5).AddDays(7 * w);
            seen.Add(TowerRules.FeaturedHero(9).id);
        }
        Assert.AreEqual(TowerRoster.OfRank(9, true).Count, seen.Count, "every SSR hero takes a turn");
    }

    [Test]
    public void PickBannerCostsDoubleAndAimsAtTheTarget()
    {
        var rules = Summoner();
        var target = TowerRoster.OfRank(8, true)[2];
        Assert.IsNull(rules.SetPickTarget(target.id));
        Assert.IsNotNull(rules.SetPickTarget(TowerRoster.OfRank(5, true)[0].id), "only SS or SSR heroes");
        Assert.AreEqual(target.id, rules.PickTarget().id);
        int sigils = rules.State.sigils;
        Assert.IsNull(rules.Summon(1, "pick"));
        Assert.IsNull(rules.Summon(10, "pick"));
        Assert.AreEqual(sigils - 220, rules.State.sigils, "20 a pull, 200 a ten-pull");
        int ssPulls = 0, hits = 0;
        for (int n = 0; n < 300; n++)
        {
            Assert.IsNull(rules.Summon(10, "pick"));
            foreach (var pull in rules.LastSummon)
            {
                Assert.AreEqual("hero", pull.kind);
                if (pull.rank == 9) Assert.AreNotEqual(target.id, pull.unitId);
                if (pull.rank != 8) continue;
                ssPulls++; if (pull.unitId == target.id) hits++;
            }
        }
        Assert.Greater(ssPulls, 40);
        Assert.That(hits / (float)ssPulls, Is.InRange(0.55f, 0.8f));

        // A pending guarantee follows the player to a new target.
        var ssrTarget = TowerRoster.OfRank(9, true)[3];
        rules.State.pickMissed = true;
        Assert.IsNull(rules.SetPickTarget(ssrTarget.id));
        rules.State.summonPity = TowerRules.HardPity - 1;
        Assert.IsNull(rules.Summon(1, "pick"));
        Assert.AreEqual(9, rules.LastSummon[0].rank);
        Assert.AreEqual(ssrTarget.id, rules.LastSummon[0].unitId);
        Assert.IsFalse(rules.State.pickMissed);
    }

    [Test]
    public void ResidentBannerIsCheapAndGivesAnAEveryTwentyPulls()
    {
        var rules = Summoner();
        int sigils = rules.State.sigils, pity = rules.State.summonPity;
        Assert.IsNull(rules.Summon(1, "resident"));
        Assert.IsNull(rules.Summon(10, "resident"));
        Assert.AreEqual(sigils - 55, rules.State.sigils, "5 a pull, 50 a ten-pull");
        int dry = 0, longest = 0, ssr = 0, pulls = 0;
        for (int n = 0; n < 200; n++)
        {
            Assert.IsNull(rules.Summon(10, "resident"));
            foreach (var pull in rules.LastSummon)
            {
                Assert.AreEqual("resident", pull.kind, "the Resident banner draws residents only");
                pulls++;
                if (pull.rank == 9) ssr++;
                dry = pull.rank >= TowerRules.ResidentFloorRank ? 0 : dry + 1;
                longest = Mathf.Max(longest, dry);
            }
        }
        Assert.Less(longest, TowerRules.ResidentPityAt, "never twenty pulls without an A or better");
        Assert.That(ssr / (float)pulls, Is.InRange(0.004f, 0.03f), "SSR about 1%");
        Assert.AreEqual(pity, rules.State.summonPity, "the hero SSR pity is untouched");
    }

    [Test]
    public void HeroBannersShareTheSsrPity()
    {
        var rules = Summoner();
        rules.State.summonPity = TowerRules.HardPity - 1;
        Assert.IsNull(rules.Summon(1, "featured"));
        Assert.AreEqual(9, rules.LastSummon[0].rank, "Standard's pity pays out on Featured");
        Assert.AreEqual(0, rules.State.summonPity);
        rules.State.featuredMissed = true;
        rules.State.summonPity = TowerRules.HardPity - 1;
        Assert.IsNull(rules.Summon(1, "featured"));
        Assert.AreEqual(TowerRules.FeaturedHero(9).id, rules.LastSummon[0].unitId, "a pending guarantee pays the featured SSR");
        Assert.IsFalse(rules.State.featuredMissed);
        foreach (string banner in new[] { "pick", "standard", "featured" })
        {
            int before = rules.State.summonPity;
            Assert.IsNull(rules.Summon(1, banner));
            int expected = rules.LastSummon[0].rank == 9 ? 0 : before + 1;
            Assert.AreEqual(expected, rules.State.summonPity, banner + " advances the one shared counter");
        }
    }

    [Test]
    public void BannerStateSurvivesASave()
    {
        var rules = Summoner();
        rules.SetSummonBanner("resident");
        rules.CyclePickTarget(2);
        rules.State.featuredMissed = true; rules.State.pickMissed = true; rules.State.residentPity = 7;
        string pick = rules.State.pickTarget;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.AreEqual("resident", loaded.State.summonBanner);
        Assert.AreEqual(pick, loaded.State.pickTarget);
        Assert.IsTrue(loaded.State.featuredMissed); Assert.IsTrue(loaded.State.pickMissed);
        Assert.AreEqual(7, loaded.State.residentPity);
        rules.State.summonBanner = "nonsense"; rules.State.pickTarget = "kaela";
        var repaired = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.AreEqual("standard", repaired.State.summonBanner);
        Assert.AreEqual(9, TowerRoster.Unit(repaired.State.pickTarget).rank, "a bad target falls back to an SSR hero");
    }

    // ---- TT 10.3.1: town life, slice 1 (GDD 19.1) ---------------------------------------------------------------

    // Kaela works the Well at x18 (home x21); a Lumber Mill keeps the rooms lit.
    private static TowerRules Town()
    {
        var rules = Quiet(Started());
        var s = rules.State;
        s.food = s.water = s.firewood = 100; s.steward = false;
        var well = rules.AddRoom("well", 0, 18, 1);
        rules.AddRoom("lumber_mill", 0, 15, 1);
        rules.Assign(s.residents[0].id, well.uid);
        return rules;
    }

    private static TowerResident Patron(TowerRules rules, int room, float hunger)
    {
        var patron = rules.AddResident("", "Patron " + rules.State.nextResidentId, "villager", 1);
        patron.currentRoom = patron.targetRoom = room;
        patron.hunger = hunger;
        patron.thirst = 90;
        return patron;
    }

    [Test]
    public void HungryResidentWalksToTheKitchenAndEatsThere()
    {
        var rules = Town();
        var kitchen = rules.AddRoom("kitchen", 0, 19, 1);
        var kaela = rules.State.residents[0];
        kaela.hunger = 45;
        rules.Advance(1, true);
        Assert.AreEqual("meal", kaela.currentTask);
        Assert.AreEqual(kitchen.uid, kaela.targetRoom);
        Assert.Greater(kaela.travelDuration, 0, "the cutaway slides them over");
        Assert.AreEqual(100, rules.State.food, 0.01f, "nothing eaten from the stores on the way");
        StringAssert.Contains("the Kitchen", rules.TaskExplanation(kaela));
        for (int i = 0; i < 20 && kaela.hunger < 99; i++) rules.Advance(1, true);
        Assert.AreEqual(kitchen.uid, kaela.currentRoom);
        Assert.GreaterOrEqual(kaela.hunger, 98f);
        Assert.Less(rules.State.food, 99f, "the meal came out of the stores at the Kitchen");
        Assert.IsTrue(rules.Thoughts(kaela).Any(t => t.label == "Ate at the Kitchen"));
        for (int i = 0; i < 5; i++) rules.Advance(1, true);
        Assert.AreNotEqual("meal", kaela.currentTask, "fed, back to work");
    }

    [Test]
    public void WithoutAVenueResidentsEatFromStockAsBefore()
    {
        var rules = Town();
        var kaela = rules.State.residents[0];
        kaela.hunger = 60;
        rules.Advance(1, true);
        Assert.Greater(kaela.hunger, 85f, "the old instant meal");
        Assert.Less(rules.State.food, 99f);
        Assert.AreNotEqual("cold", kaela.mealMemory, "no cold rations thought without a venue");
        Assert.AreNotEqual("meal", kaela.currentTask);
    }

    [Test]
    public void FullVenueSendsPatronsToTheNextOne()
    {
        var rules = Town();
        var well = rules.State.rooms.First(r => r.type == "well");
        var near = rules.AddRoom("kitchen", 0, 19, 1);
        var far = rules.AddRoom("kitchen", 0, 13, 1);
        Assert.AreEqual(2, rules.Capacity(near));
        var a = Patron(rules, well.uid, 45); var b = Patron(rules, well.uid, 45); var c = Patron(rules, well.uid, 45);
        rules.Advance(1, true);
        var patrons = new[] { a, b, c };
        Assert.IsTrue(patrons.All(p => p.currentTask == "meal"), "all three go out to eat");
        Assert.AreEqual(near.uid, a.targetRoom, "the nearer Kitchen first");
        Assert.LessOrEqual(patrons.Count(p => p.targetRoom == near.uid), rules.Capacity(near), "never past its seats");
        Assert.IsTrue(patrons.Any(p => p.targetRoom == far.uid), "the rest spill over to the next Kitchen");

        var crowded = Town();
        var crowdedWell = crowded.State.rooms.First(r => r.type == "well");
        crowded.AddRoom("kitchen", 0, 19, 1);
        var x = Patron(crowded, crowdedWell.uid, 45); var y = Patron(crowded, crowdedWell.uid, 45);
        var z = Patron(crowded, crowdedWell.uid, 45);
        crowded.Advance(1, true);
        Assert.AreNotEqual("meal", z.currentTask, "every seat taken: wait");
        z.hunger = 34;
        crowded.Advance(1, true);
        Assert.Greater(z.hunger, 55f, "cold rations from the stores");
        Assert.AreEqual("cold", z.mealMemory);
        Assert.IsTrue(crowded.Thoughts(z).Any(t => t.label == "Ate cold rations"));
        Assert.AreEqual("meal", x.currentTask); Assert.AreEqual("meal", y.currentTask);
    }

    [Test]
    public void OfflineCatchUpKeepsInstantMeals()
    {
        var rules = Town();
        rules.AddRoom("kitchen", 0, 19, 1);
        rules.AddRoom("market", 0, 16, 1);
        var kaela = rules.State.residents[0];
        rules.CatchUp(3600);
        Assert.IsFalse(TowerRules.IsErrand(kaela.currentTask), "nobody is left mid-trip by catch-up");
        Assert.Greater(kaela.hunger, 35f);
        Assert.Greater(kaela.thirst, 35f);
    }

    [Test]
    public void FreeTimeSendsDayWorkersToTheMarket()
    {
        var rules = Town();
        var market = rules.AddRoom("market", 0, 17, 1);
        var kaela = rules.State.residents[0];
        kaela.schedule = "day";
        kaela.joy = 50;
        rules.State.clock = (19.5f - 6f) / 24f * TowerRules.DaySeconds;   // 19:30
        Assert.IsTrue(rules.FreeTime(kaela));
        rules.Advance(1, true);
        Assert.AreEqual("leisure", kaela.currentTask);
        Assert.AreEqual(market.uid, kaela.targetRoom);
        for (int i = 0; i < 20 && kaela.joy < 99; i++) rules.Advance(1, true);
        Assert.GreaterOrEqual(kaela.joy, 98f);
        Assert.IsTrue(rules.Thoughts(kaela).Any(t => t.label == "Browsed the Argent Market"));
        Assert.IsTrue(rules.Thoughts(kaela).Any(t => t.label == "Well entertained"));
        rules.Advance(2, true);
        Assert.AreEqual("production", kaela.currentTask, "back to the Well");

        kaela.joy = 50;
        rules.State.clock = (12f - 6f) / 24f * TowerRules.DaySeconds;   // noon: working hours
        rules.Advance(1, true);
        Assert.AreEqual("production", kaela.currentTask, "outside free time joy 50 is not low enough");
    }

    [Test]
    public void NoLeisureVenueNeverSoursMood()
    {
        var rules = Town();
        rules.State.heartRank = 2;
        var kaela = rules.State.residents[0];
        kaela.joy = 10;
        Assert.IsFalse(rules.Thoughts(kaela).Any(t => t.label == "No free time"));
        rules.Advance(1, true);
        Assert.GreaterOrEqual(kaela.joy, TowerRules.JoyFloorWithoutLeisure);
        rules.AddRoom("market", 0, 17, 1);
        kaela.joy = 10;
        Assert.IsTrue(rules.Thoughts(kaela).Any(t => t.label == "No free time"), "a Market nobody visits");
        rules.State.heartRank = 1;
        Assert.IsFalse(rules.Thoughts(kaela).Any(t => t.label == "No free time"), "never in the first hour");
    }

    [Test]
    public void VenueCacheRebuildsOnlyOnLayoutChange()
    {
        var rules = Town();
        int before = rules.Venues().Count, stamp = rules.VenueStamp;
        rules.Advance(3, true);
        rules.Venues();
        Assert.AreEqual(stamp, rules.VenueStamp, "ticks reuse the cache");
        rules.AddRoom("kitchen", 0, 19, 1);
        Assert.AreEqual(before + 1, rules.Venues().Count);
        Assert.AreEqual(stamp + 1, rules.VenueStamp);
    }

    [Test]
    public void OlderSavesStartTownLifeContent()
    {
        var rules = Started();
        var fresh = rules.State;
        fresh.colonyVersion = 1;
        fresh.residents[0].joy = 0;
        int seed = fresh.colonyRandom;
        fresh.siegeCooldown = 5;
        var old = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(fresh));
        new TowerRules(old);
        Assert.AreEqual(TowerRules.ColonyVersion, old.colonyVersion);
        Assert.AreEqual(70, old.residents[0].joy, 0.01f);
        Assert.AreEqual(seed, old.colonyRandom, "version 1 steps do not run again");
        Assert.AreEqual(5, old.siegeCooldown, 0.01f);
        Assert.AreEqual("", old.residents[0].mealMemory);
    }

    // ---- TT 10.3.2: the TOWN view's ground plan (GDD 19.2) -------------------------------------------------------

    [Test]
    public void TownRingGrowsWithTheHeartAndStaysRound()
    {
        int lastLots = 0;
        for (int rank = 1; rank <= TowerTiers.MaxRank; rank++)
        {
            int r = TowerTownMap.Radius(rank);
            Assert.Greater(TowerTownMap.LotCount(rank), lastLots, "every Heart rank opens more lots");
            lastLots = TowerTownMap.LotCount(rank);
            Assert.IsTrue(TowerTownMap.InRing(r, 0, rank), "the ring reaches its radius along the roads");
            Assert.IsTrue(TowerTownMap.InRing(0, -r, rank));
            Assert.IsFalse(TowerTownMap.InRing(r, r, rank), "corners stay outside: a circle on the square grid");
            Assert.IsFalse(TowerTownMap.InRing(r + 1, 0, rank));
        }
        Assert.AreEqual(TowerTownMap.Radius(TowerTiers.MaxRank), TowerTownMap.MaxRadius);
    }

    [Test]
    public void TownRoadsLeaveFromTheGatesAndTheTowerIsNeverALot()
    {
        for (int rank = 1; rank <= TowerTiers.MaxRank; rank++)
        {
            int r = TowerTownMap.Radius(rank);
            for (int x = -TowerTownMap.TowerHalf; x <= TowerTownMap.TowerHalf; x++)
                for (int z = -TowerTownMap.TowerHalf; z <= TowerTownMap.TowerHalf; z++)
                    Assert.AreEqual(TowerTownMap.Tile.Tower, TowerTownMap.At(x, z, rank));
            Assert.AreEqual(TowerTownMap.Tile.Gate, TowerTownMap.At(-TowerTownMap.TowerHalf - 1, 0, rank));
            Assert.AreEqual(TowerTownMap.Tile.Gate, TowerTownMap.At(TowerTownMap.TowerHalf + 1, 0, rank));
            for (int x = TowerTownMap.TowerHalf + 2; x <= r; x++)
            {
                Assert.AreNotEqual(TowerTownMap.Tile.Lot, TowerTownMap.At(x, 0, rank), "the east road runs to the ring");
                Assert.AreNotEqual(TowerTownMap.Tile.Lot, TowerTownMap.At(-x, 0, rank), "and the west road");
            }
            var north = TowerTownMap.At(0, r, rank);
            Assert.AreEqual(rank >= TowerTownMap.CrossRoadRank ? TowerTownMap.Tile.Road : TowerTownMap.Tile.Lot, north,
                "the north-south road opens at rank C");
            Assert.IsTrue(TowerTownMap.Frontage(TowerTownMap.TowerHalf + 3, 2, rank), "lots beside the plaza front it");
            Assert.IsFalse(TowerTownMap.Buildable(r + 1, 0, rank));
        }
        // Ring roads cut a big city into blocks, and never run along the edge of the buildable ring.
        Assert.IsFalse(TowerTownMap.IsRingRoad(8, 0, 1), "no ring road inside the F village (radius 7)");
        Assert.IsTrue(TowerTownMap.IsRingRoad(0, 8, 4), "the first ring road at rank C (radius 14)");
        Assert.AreEqual(TowerTownMap.Tile.Road, TowerTownMap.At(6, 5, 4), "sqrt(61) is about 7.8: on the ring road");
        Assert.AreEqual(TowerTownMap.Tile.Lot, TowerTownMap.At(5, 5, 4), "sqrt(50) is about 7.1: a lot");
        Assert.IsTrue(TowerTownMap.IsRingRoad(0, 32, TowerTiers.MaxRank));
        Assert.IsFalse(TowerTownMap.IsRingRoad(0, 16, 5), "radius 17: the 16 ring would hug the edge");
    }

    // ---- TT 10.3.3: town lots - the town runs itself, the player can take over (GDD 19.2) -------------------------

    [Test]
    public void TownFillsItsLotsOnItsOwnFromTheGatesOut()
    {
        var rules = Quiet(Started());
        rules.Advance(1, true);
        int target = rules.TownTargetLots();
        Assert.Greater(target, 0);
        Assert.AreEqual(target, rules.TownBuiltLots(), "a fresh town fills to its target at once");
        foreach (var lot in rules.State.townLots)
        {
            Assert.IsTrue(TowerTownMap.Buildable(lot.x, lot.z, rules.State.heartRank), "only on lots inside the ring");
            Assert.IsFalse(lot.manual);
            Assert.IsNotNull(TowerRules.TownBuilding(lot.type));
        }
        Assert.IsTrue(rules.State.townLots.All(l => TowerTownMap.Frontage(l.x, l.z, rules.State.heartRank)),
            "a small village hugs the roads and Gate squares");
        Assert.IsTrue(rules.State.townLots.Any(l => TowerRules.TownBuilding(l.type).district == "residential"));
        Assert.AreEqual(rules.State.townLots.Count, rules.State.townLots.Select(l => l.x * 1000 + l.z).Distinct().Count(),
            "one building per tile");

        // More people: the town grows one lot every TownGrowSeconds, never past its target.
        for (int i = 0; i < 6; i++) rules.AddResident("", "Settler " + i, "villager", 1);
        int before = rules.TownBuiltLots();
        rules.Advance(TowerRules.TownGrowSeconds * 3 + 1, true);
        Assert.AreEqual(before + 3, rules.TownBuiltLots());
        rules.Advance(TowerRules.TownGrowSeconds * 40, true);
        Assert.AreEqual(rules.TownTargetLots(), rules.TownBuiltLots());
    }

    [Test]
    public void AutoLotsRiseWithTheHeartButNeverPastIt()
    {
        var rules = Quiet(Started());
        rules.Advance(1, true);
        Assert.IsTrue(rules.State.townLots.All(l => l.rank == 1));
        rules.State.heartRank = 3;
        rules.Advance(TowerRules.TownUpgradeSeconds * 200, true);
        Assert.IsTrue(rules.State.townLots.All(l => l.rank == 3), "every auto lot climbs to the Heart's rank");
    }

    [Test]
    public void PlayerLotsAreNeverChangedByTheTown()
    {
        var rules = Quiet(Started());
        rules.State.gold = 10000;
        rules.Advance(1, true);
        var auto = rules.State.townLots[0];
        Assert.IsNull(rules.SetLot(auto.x, auto.z, "foundry"));
        Assert.AreEqual("foundry", auto.type);
        Assert.IsTrue(auto.manual, "swapping a lot makes it yours");
        Assert.AreEqual(10000 - TowerRules.TownLotGold, rules.State.gold);
        rules.State.heartRank = 4;
        rules.Advance(TowerRules.TownUpgradeSeconds * 200, true);
        Assert.AreEqual(1, auto.rank, "the town does not upgrade your lots");
        Assert.AreEqual("foundry", auto.type);

        // Clearing keeps the lot empty: the town does not build over it.
        var other = rules.State.townLots[1];
        Assert.IsNull(rules.SetLot(other.x, other.z, ""));
        rules.Advance(TowerRules.TownGrowSeconds * 50, true);
        Assert.AreEqual("", rules.Lot(other.x, other.z).type);

        // Handing it back reopens it for the town.
        Assert.AreEqual(1, rules.SetLotsAuto(new[] { new Vector2Int(other.x, other.z) }, true));
        Assert.IsNull(rules.Lot(other.x, other.z));
        Assert.IsNotNull(rules.SetLot(0, 0, "cottage"), "the Tower's tiles are not lots");
        Assert.IsNotNull(rules.SetLot(auto.x, auto.z, "dragon_lair"));
    }

    [Test]
    public void GroupCommandsRetypeAndLockMany()
    {
        var rules = Quiet(Started());
        rules.State.gold = 100;
        rules.Advance(1, true);
        var tiles = rules.State.townLots.Take(4).Select(l => new Vector2Int(l.x, l.z)).ToList();
        tiles.Add(new Vector2Int(0, 0));   // the Tower: skipped
        StringAssert.Contains("gold", rules.SetLots(tiles, "library"), "all or nothing when gold is short");
        Assert.IsFalse(rules.State.townLots.Any(l => l.type == "library"));
        rules.State.gold = 10000;
        Assert.IsNull(rules.SetLots(tiles, "library", 1));
        Assert.AreEqual(4, rules.State.townLots.Count(l => l.type == "library" && l.manual));
        Assert.AreEqual(4, rules.SetLotsAuto(tiles, true));
        Assert.AreEqual(0, rules.State.townLots.Count(l => l.manual), "handed back to the town");
        Assert.AreEqual(4, rules.SetLotsAuto(tiles, false), "LOCK freezes them as they are");
        Assert.AreEqual(4, rules.State.townLots.Count(l => l.manual && l.type == "library"));
    }

    [Test]
    public void TownLotsSurviveASave()
    {
        var rules = Quiet(Started());
        rules.State.gold = 10000;
        rules.Advance(1, true);
        var lot = rules.State.townLots[2];
        Assert.IsNull(rules.SetLot(lot.x, lot.z, "inn"));
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.AreEqual(rules.State.townLots.Count, loaded.State.townLots.Count);
        var back = loaded.Lot(lot.x, lot.z);
        Assert.AreEqual("inn", back.type);
        Assert.IsTrue(back.manual);
        var old = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State).Replace("\"townLots\"", "\"oldLots\""));
        Assert.IsNotNull(new TowerRules(old).State.townLots, "saves from before town lots load with an empty town");
    }
}
