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
    public void DemolishRefundsPartAndSendsOccupantsHome()
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
        Assert.AreEqual(40, rules.DemolishRefund(kitchen), "40% of the catalogue price");
        kitchen.level = 3;
        Assert.AreEqual(40 + 200, rules.DemolishRefund(kitchen), "plus a quarter of the 800 upgrade gold");
        int gold = rules.State.gold;
        Assert.IsNull(rules.Demolish(kitchen.uid));
        Assert.AreEqual(gold + 240, rules.State.gold);
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
}
