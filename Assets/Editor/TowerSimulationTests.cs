using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerSimulationTests
{
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
        Assert.IsNull(rules.Build("kitchen", 0, 19));
        var room = rules.RoomAt(0, 19);
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
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("nursery", 0, 19));
        var other = rules.AddResident("", "Rowan", "villager", 1);
        Assert.IsNull(rules.Assign(other.id, rules.RoomAt(0, 21).uid));
        Assert.IsNull(rules.PairFamily(rules.State.residents[0].id, other.id));
        rules.Advance(TowerRules.DaySeconds + 10, false);
        var child = rules.State.residents.Find(r => r.ageStage == 1);
        Assert.IsNotNull(child);
        Assert.AreEqual("nursery", rules.Room(child.homeRoom).type);
        Assert.IsNotNull(rules.Assign(child.id, rules.RoomAt(0, 19).uid));
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
        int researchGold = rules.State.gold;
        Assert.IsNull(rules.ResearchBlueprint("quarry"));
        Assert.IsTrue(rules.State.blueprints.Contains("quarry"));
        Assert.Less(rules.State.gold, researchGold);
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        int wood = rules.State.wood, stone = rules.State.stone;
        Assert.IsNull(rules.Build("kitchen", 0, 19));
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
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("well", 0, 18));
        var well = rules.RoomAt(0, 18);
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
        Assert.AreEqual(1, ground.east); // the Gate holds the first east cell
        Assert.AreEqual("heart", rules.RoomAt(0, TowerRules.CoreX).type);
        Assert.AreEqual("house", rules.RoomAt(0, TowerRules.CoreX - 1).type);
        Assert.AreEqual("gate", rules.RoomAt(0, TowerRules.CoreX + 1).type);
        Assert.IsTrue(rules.IsFounded(0, TowerRules.CoreX - 1));
        Assert.IsTrue(rules.IsFounded(0, TowerRules.CoreX + 1));
        Assert.IsFalse(rules.IsFounded(0, TowerRules.CoreX));
        Assert.IsFalse(rules.IsFounded(0, TowerRules.CoreX + 2));
    }

    [Test]
    public void EastWingBuildsOutwardFromTheGate()
    {
        var rules = Started();
        rules.State.celestium = 200; rules.State.gold = 3000; rules.State.wood = 100; rules.State.stone = 100;
        Assert.IsNotNull(rules.CanBuild("kitchen", 0, 24)); // not founded yet
        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.AreEqual(3, rules.Floor(0).east);
        Assert.IsNotNull(rules.CanBuild("kitchen", 0, 25)); // must touch the Gate first
        Assert.IsNull(rules.Build("kitchen", 0, 24));
        Assert.AreEqual("kitchen", rules.RoomAt(0, 25).type);
        Assert.IsNotNull(rules.CanBuild("well", 0, 23));   // the Gate's cell
        Assert.IsNotNull(rules.CanBuild("well", 0, TowerRules.CoreX)); // never on the shaft
        Assert.IsNotNull(rules.CanBuild("cottage", 0, TowerRules.CoreX - 1)); // straddles the shaft
    }

    [Test]
    public void UpperFloorsFoundBothSidesAndBuildFromTheShaft()
    {
        var rules = Started();
        rules.State.celestium = 200; rules.State.gold = 3000; rules.State.wood = 100; rules.State.stone = 100;
        Assert.IsNull(rules.OpenFloor(1));
        var floor = rules.Floor(1);
        Assert.AreEqual(1, floor.west);
        Assert.AreEqual(1, floor.east);
        Assert.IsNull(rules.Build("house", 1, TowerRules.CoreX + 1));
        Assert.IsNull(rules.Build("house", 1, TowerRules.CoreX - 1));
        Assert.IsNull(rules.ExpandFloor(1, 1));
        Assert.IsNull(rules.Build("house", 1, TowerRules.CoreX + 2));
    }

    [Test]
    public void EastExpansionRespectsTheWingCapAndUndergroundExcavation()
    {
        var rules = Started();
        rules.State.celestium = 5000;
        for (int i = 0; i < 10; i++) Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.AreEqual(TowerRules.WingCells + 1, rules.Floor(0).east);
        Assert.IsNotNull(rules.ExpandFloor(0, 1));
        Assert.IsNull(rules.OpenFloor(-1));
        Assert.IsNotNull(rules.ExpandFloor(-1, 1));
        Assert.IsNull(rules.Excavate(-1, rules.NextExpansionX(-1, 1)));
        Assert.IsNull(rules.ExpandFloor(-1, 1));
    }

    [Test]
    public void OlderSavesGainAnEastSideOnEveryFloor()
    {
        var rules = new TowerRules(TowerMilestones.Create(3));
        var state = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        foreach (var floor in state.floors) floor.east = 0;
        new TowerRules(state);
        foreach (var floor in state.floors) Assert.GreaterOrEqual(floor.east, 1);
    }

    [Test]
    public void PopulatedCheckpointsHaveRoomsOnBothSidesOfTheHeart()
    {
        var rules = new TowerRules(TowerMilestones.Create(4));
        bool west = rules.State.rooms.Exists(r => r.floor == 0 && r.x + r.width <= TowerRules.CoreX && r.type != "heart");
        bool east = rules.State.rooms.Exists(r => r.floor == 0 && r.x > TowerRules.GateX);
        Assert.IsTrue(west && east);
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
            // 1 BUILD: expand the foundation twice (either side), pick the Kitchen, tap a lot.
            Assert.IsNull(rules.ExpandFloor(0, 1));
            Assert.IsNull(rules.ExpandFloor(0, 1));
            Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX + 2), hero + " east kitchen");
            Assert.AreEqual(1, rules.State.tutorialStep);
            // 2 MATCH
            var kitchen = rules.RoomAt(0, TowerRules.CoreX + 2);
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
        Assert.IsTrue(rules.State.blueprints.Contains("market")); // gold path
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
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 4));
        rules.State.water = 3;
        StringAssert.Contains("Staff", rules.NeedsAdvice()); // built, but nobody works it
        var well = rules.RoomAt(0, TowerRules.CoreX - 4);
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
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 3));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 4));
        rules.State.gold += 2000; rules.State.wood += 60; rules.State.stone += 30; rules.State.celestium += 60;
        for (int i = 0; i < 5; i++) Assert.IsNull(rules.ExpandFloor(0, 1));
        Assert.IsNull(rules.Build("lumber_mill", 0, TowerRules.CoreX + 2));
        Assert.IsNull(rules.Build("nursery", 0, TowerRules.CoreX + 5));
        rules.State.pendingVisitors = 2;
        for (int i = 0; i < 2; i++) Assert.IsNull(rules.RecruitVisitor());
        var residents = rules.State.residents;
        Assert.IsNull(rules.Assign(residents[0].id, rules.RoomAt(0, TowerRules.CoreX - 3).uid));
        Assert.IsNull(rules.Assign(residents[1].id, rules.RoomAt(0, TowerRules.CoreX - 4).uid));
        Assert.IsNull(rules.Assign(residents[2].id, rules.RoomAt(0, TowerRules.CoreX + 2).uid));
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
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 3));
        rules.State.blueprints.Add("silo");
        Assert.IsNull(rules.Build("silo", 0, TowerRules.CoreX - 4));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 5));
        rules.State.firewood = 0;
        rules.Advance(1, true);
        var near = rules.RoomAt(0, TowerRules.CoreX - 1);
        var far = rules.RoomAt(0, TowerRules.CoreX - 5);
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
        var gate = rules.State.rooms.Find(r => r.type == "gate");
        var guard = rules.State.residents[0];
        guard.might = 10; guard.weapon = 3;
        Assert.IsNull(rules.Assign(guard.id, gate.uid));
        Assert.AreEqual(1, rules.GuardCount());
        Assert.IsNull(rules.StartRaid());
        Assert.AreEqual(gate.uid, rules.State.incidents[0].roomUid);
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
        for (int i = 0; i < 3; i++) Assert.IsNull(rules.ExpandFloor(0));
        Assert.IsNull(rules.Build("kitchen", 0, TowerRules.CoreX - 3));
        Assert.IsNull(rules.Build("well", 0, TowerRules.CoreX - 4));
        var worker = rules.State.residents[0];
        Assert.IsNull(rules.Assign(worker.id, rules.RoomAt(0, TowerRules.CoreX - 3).uid));
        rules.State.water = 2; rules.State.food = 100; rules.State.firewood = 100;
        rules.Advance(25, true);
        Assert.AreEqual(rules.RoomAt(0, TowerRules.CoreX - 4).uid, worker.jobRoom, "moved onto the Well");
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

    [Test]
    public void OlderPopulatedSaveSkipsTheFoundingGuide()
    {
        var state = TowerMilestones.Create(10);
        state.tutorialStep = 0;
        new TowerRules(state);
        Assert.AreEqual(7, state.tutorialStep);
    }
}
