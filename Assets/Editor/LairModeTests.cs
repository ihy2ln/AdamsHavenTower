using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// TT 10.4.0 Dungeon Mode, slice 1 (GDD 20): the fork's plumbing, founding, the route, encounters, monsters and the
// elite team. No disk I/O. Tower Mode must stay exactly as it was.
public sealed class LairModeTests
{
    [SetUp] public void InstantBuilds() { TowerRules.InstantConstruction = true; }
    [TearDown] public void TimedBuilds() { TowerRules.InstantConstruction = false; TowerModes.Active = TowerModes.Tower; }

    private static TowerRules Founded(bool summit = true)
    {
        var rules = TowerRules.NewLair();
        Assert.IsNull(rules.AwakenHeart());
        Assert.AreEqual("lair_site", rules.State.introPhase);
        Assert.IsNull(rules.LairFound(summit));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        rules.State.eventCooldown = 100000;
        rules.State.lair.nextPartySeconds = 1e9f;
        rules.State.lair.random = 12345;
        return rules;
    }

    // Spawns a party and plays live until every party has left; returns the newest report.
    private static LairReport Raid(TowerRules rules, string kind = "normal")
    {
        Assert.IsNull(rules.LairSpawnParty(kind));
        for (int i = 0; i < 40 && rules.State.lair.parties.Count > 0; i++) rules.Advance(30, true);
        Assert.AreEqual(0, rules.State.lair.parties.Count, "the raid should end");
        return rules.State.lair.reports[0];
    }

    private static void ClearMonsters(TowerRules rules) { rules.State.lair.monsters.Clear(); }

    private static void DisableTraps(TowerRules rules)
    { foreach (var room in rules.State.rooms) if (room.type == "spike_hall" || room.type == "snare_pit") room.condition = 0; }

    [Test] public void TowerNewIsUntouched()
    {
        var rules = TowerRules.New();
        Assert.AreEqual("tower", rules.State.mode);
        Assert.IsFalse(rules.IsLair);
        Assert.AreEqual(0, rules.HeartFloor);
        Assert.IsFalse(TowerCatalog.All.Any(d => d.kind == "lair"));
        Assert.IsNotNull(TowerCatalog.Get("spike_hall"), "labels and panels still find dungeon rooms");
        Assert.IsNotNull(rules.CanBuild("spike_hall", 0, 21));
        Assert.AreEqual("GROUND", rules.FloorLabel(0));
        Assert.AreEqual("+03", rules.FloorLabel(3));
        Assert.AreEqual("tower", TowerMilestones.Create(6).mode, "EditMode always builds Tower checkpoints");
    }

    [Test] public void OldSaveJsonLoadsAsTower()
    {
        var state = JsonUtility.FromJson<TowerState>("{\"schema\":2,\"slot\":3,\"introPhase\":\"dormant\"}");
        state.mode = null; state.lair = null;
        var rules = new TowerRules(state);
        Assert.AreEqual("tower", state.mode);
        Assert.IsNotNull(state.lair);
        Assert.IsNotNull(state.lair.monsters);
    }

    [Test] public void SummitFoundingLayout()
    {
        var rules = Founded(true);
        var s = rules.State;
        Assert.AreEqual(4, rules.HeartFloor);
        var heart = s.rooms.Find(r => r.type == "heart");
        Assert.AreEqual(4, heart.floor); Assert.AreEqual(TowerRules.CoreX, heart.x);
        for (int f = 0; f <= 4; f++) Assert.IsNotNull(rules.Floor(f), "floor " + f);
        Assert.AreEqual(2, rules.Gates().Count, "a Gate at each end of the entrance");
        Assert.AreEqual("ENTRANCE", rules.FloorLabel(0));
        Assert.AreEqual("B1", rules.FloorLabel(1));
        Assert.AreEqual("B2", rules.FloorLabel(2));
        Assert.AreEqual("LIVING", rules.FloorLabel(3));
        Assert.AreEqual("HEART", rules.FloorLabel(4));
        var kaela = s.residents.Find(r => r.unitId == "kaela");
        Assert.AreEqual(3, rules.Room(kaela.homeRoom).floor, "the starter lives beside the Heart");
        Assert.AreEqual("complete", s.introPhase);
        Assert.AreEqual(7, s.tutorialStep);
        Assert.AreEqual(2, s.lair.monsters.Count, "two free monsters");
        Assert.AreEqual(2, rules.DungeonFloorCount);
    }

    [Test] public void DepthsFoundingMirrors()
    {
        var rules = Founded(false);
        Assert.AreEqual(-4, rules.HeartFloor);
        Assert.AreEqual("dungeon", rules.FloorKind(-1));
        Assert.AreEqual("living", rules.FloorKind(-3));
        Assert.AreEqual("heart", rules.FloorKind(-4));
        Assert.AreEqual("outside", rules.FloorKind(1));
        Assert.AreEqual(-3, rules.Room(rules.State.residents[0].homeRoom).floor);
        Assert.IsTrue(rules.RouteDistance(rules.RouteEntry().x, rules.RouteEntry().y) > 0);
    }

    [Test] public void ZoningRules()
    {
        var rules = Founded(true);
        var s = rules.State;
        s.gold = 99999; s.wood = s.stone = 999;
        Assert.IsNull(rules.CanBuild("spike_hall", 2, 20), "traps go on dungeon floors");
        StringAssert.Contains("entrance and dungeon", rules.CanBuild("spike_hall", 3, 23 + 1));
        Assert.IsNull(rules.CanBuild("kitchen", 4, 21), "rooms go beside the Heart");
        StringAssert.Contains("beside the Heart", rules.CanBuild("kitchen", 1, 20));
        Assert.IsNotNull(rules.Assign(s.residents[0].id, s.rooms.Find(r => r.type == "spike_hall").uid));
    }

    [Test] public void RouteCrossesEveryDungeonFloor()
    {
        foreach (bool summit in new[] { true, false })
        {
            var rules = Founded(summit);
            var path = rules.RoutePath();
            var last = path[path.Count - 1];
            Assert.AreEqual(rules.HeartFloor, last.x, "the route ends at the Heart");
            Assert.AreEqual(TowerRules.CoreX, last.y);
            for (int depth = 0; depth <= 2; depth++)
            {
                int floor = depth * rules.LairDir;
                Assert.GreaterOrEqual(path.Count(c => c.x == floor), 5, "the party crosses floor " + floor + " end to end");
            }
            int s0 = rules.StairX(0), s1 = rules.StairX(rules.LairDir);
            Assert.IsTrue(s0 > TowerRules.CoreX != s1 > TowerRules.CoreX, "stairs alternate ends");
            Assert.AreEqual(TowerRules.CoreX, rules.StairX(3 * rules.LairDir), "living floors use the shaft");
        }
    }

    [Test] public void DigFloorShiftsLivingAndHeart()
    {
        var rules = Founded(true);
        var s = rules.State;
        StringAssert.Contains("Raise it", rules.LairDigFloor(), "rank F holds two dungeon floors");
        s.heartRank = 2; s.celestium = 100;
        var heart = s.rooms.Find(r => r.type == "heart");
        int heartUid = heart.uid, home = s.residents[0].homeRoom;
        Assert.IsNull(rules.LairDigFloor());
        Assert.AreEqual(5, rules.HeartFloor);
        Assert.AreEqual(5, rules.Room(heartUid).floor);
        Assert.AreEqual(4, rules.Room(home).floor, "the living floor moved out with the Heart");
        Assert.AreEqual("dungeon", rules.FloorKind(3));
        Assert.AreEqual(100 - 12, s.celestium);
        Assert.IsNotNull(rules.LairDigFloor(), "rank E caps at three");
        Assert.GreaterOrEqual(rules.RoutePath().Count(c => c.x == 3), 5);
    }

    [Test] public void SpikeHallsKillAndPay()
    {
        var rules = Founded(true);
        ClearMonsters(rules);
        foreach (var room in rules.State.rooms) if (room.type == "spike_hall") room.level = 9;
        int kills = 0;
        for (int i = 0; i < 3; i++) kills += Raid(rules).kills;
        Assert.Greater(kills, 0);
        Assert.Greater(rules.State.lair.notoriety, 0);
        Assert.Greater(rules.State.essence, 0);
    }

    [Test] public void SnareCapturesForRansom()
    {
        var rules = Founded(true);
        ClearMonsters(rules);
        DisableTraps(rules);
        var pit = rules.State.rooms.Find(r => r.type == "snare_pit");
        pit.condition = 100; pit.level = 9;
        int captured = 0;
        for (int i = 0; i < 4; i++)
        {
            var report = Raid(rules);
            captured += report.captured;
            Assert.Less(report.captured, report.size, "the last member is never taken");
        }
        Assert.Greater(captured, 0);
        Assert.IsTrue(rules.State.lair.reports.Any(r => r.lines.Any(l => l.Contains("Ransom"))));
    }

    [Test] public void LairFightFollowsPowerRatio()
    {
        var rules = Founded(true);
        DisableTraps(rules);
        foreach (var m in rules.State.lair.monsters) { m.rank = 9; m.level = 50; }
        var strong = Raid(rules);
        Assert.AreNotEqual("breached", strong.outcome, "SSR monsters turn an early party back");
        Assert.AreEqual(0f, strong.heartDamage);

        var weak = Founded(true);
        DisableTraps(weak);
        weak.State.lair.fame = 90; weak.State.heartRank = 6;
        foreach (var r in weak.State.residents) r.priorityDefense = 0;
        var report = Raid(weak);
        Assert.AreEqual(0, report.kills, "F monsters cannot stop a strong party");
        Assert.IsTrue(weak.State.lair.monsters.Any(m => m.hp < 1f), "the monsters were hurt");
    }

    [Test] public void BaitVaultMakesPartiesCareless()
    {
        var rules = Founded(true);
        ClearMonsters(rules);
        DisableTraps(rules);
        var report = Raid(rules);
        Assert.IsTrue(report.lines.Any(l => l.Contains("careless")), string.Join("\n", report.lines.ToArray()));
    }

    [Test] public void BreachHurtsHeart_OfflineNeverBelow30Percent()
    {
        var rules = Founded(true);
        ClearMonsters(rules);
        DisableTraps(rules);
        foreach (var r in rules.State.residents) r.priorityDefense = 0;
        float max = TowerRules.HeartMaxHp(rules.State.heartRank);
        rules.State.heartHp = max * 0.32f;
        Assert.IsNull(rules.LairSpawnParty("elite"));
        rules.Advance(60, false);
        Assert.AreEqual(0, rules.State.lair.parties.Count);
        Assert.AreEqual("breached", rules.State.lair.reports[0].outcome);
        Assert.GreaterOrEqual(rules.State.heartHp, max * 0.3f - 0.01f);
        Assert.IsFalse(rules.State.defeated);
    }

    [Test] public void NotorietyStartsEliteTeam()
    {
        var rules = Founded(true);
        var s = rules.State;
        s.lair.notoriety = 80; s.siegeCooldown = 0; s.eventCooldown = 0.5f;
        rules.Advance(2, true);
        Assert.Greater(s.siegeWarning, 0, "high notoriety draws the elite team");
        s.siegeWarning = 0.5f;
        rules.Advance(1, true);
        Assert.IsTrue(s.lair.parties.Any(p => p.kind == "elite"));
        Assert.IsFalse(s.incidents.Any(i => i.kind == "raiders"));
        Assert.AreEqual(5, s.lair.parties.Find(p => p.kind == "elite").members.Count);

        s.lair.parties.Clear();
        s.lair.notoriety = 80; s.siegeWarning = 100;
        rules.ResolveSiegeBattle(true, null);
        Assert.Less(s.lair.notoriety, 55, "beating the elite team in battle calms the guilds");
        s.siegeWarning = 100;
        Assert.DoesNotThrow(() => rules.ResolveSiegeBattle(false, null));
        Assert.IsTrue(s.lair.parties.Any(p => p.kind == "elite"), "a lost battle lets the elite team in");
    }

    [Test] public void NoRaidersOrVisitorsInLair()
    {
        var rules = Founded(true);
        rules.State.eventCooldown = 5;
        rules.State.lair.notoriety = 60;
        rules.Advance(1500, true);
        Assert.AreEqual(0, rules.State.pendingVisitors);
        Assert.IsFalse(rules.State.incidents.Any(i => i.kind == "raiders"));
    }

    [Test] public void MonsterBannerDrawsBestiaryForms()
    {
        var rules = Founded(true);
        var s = rules.State;
        s.heartRank = 9; s.sigils = 100;
        Assert.IsNull(rules.SummonMonsters(10));
        Assert.AreEqual(50, s.sigils);
        Assert.AreEqual(10, rules.LastMonsters.Count);
        Assert.IsTrue(rules.LastMonsters.Any(m => m.rank >= TowerRules.TenPullFloor), "a ten-pull holds a B or better");
        foreach (var m in s.lair.monsters.Where(m => m.formId.Length > 0))
        {
            var form = BattleBestiary.Form(m.formId);
            Assert.IsNotNull(form, m.formId);
        }
        Assert.Greater(s.lair.monsters.Count, 2);
        var tower = TowerRules.New();
        tower.State.introPhase = "complete"; tower.State.sigils = 100;
        Assert.IsNotNull(tower.SummonMonsters(1), "no Monster banner in a Tower");
    }

    [Test] public void MonsterPowerScale()
    {
        Assume.That(BattleBestiary.Available, "needs the bestiary");
        float Mean(int rank)
        {
            var forms = BattleBestiary.Families.Where(f => f.minRank <= rank && rank <= f.maxRank)
                .Select(f => BattleBestiary.FormFor(f, rank)).ToList();
            return forms.Average(f => TowerRules.MonsterPower(new LairMonster { formId = f.id, rank = rank, level = TowerRules.NaturalLevel(rank) }));
        }
        float low = Mean(1), mid = Mean(5), high = Mean(9);
        Assert.That(low, Is.InRange(8f, 25f));
        Assert.That(high, Is.InRange(60f, 140f));
        Assert.Less(low, mid); Assert.Less(mid, high);
    }

    [Test] public void SaveFoldersPerMode()
    {
        StringAssert.Contains("AdamsHavenDungeon", TowerSaveFiles.PathFor("lair", 1));
        StringAssert.Contains("AdamsHavenTower", TowerSaveFiles.PathFor("tower", 1));
        StringAssert.Contains("AdamsHavenTower", TowerSaveFiles.PathFor(1), "EditMode is always the Tower");
    }

    [Test] public void LegacyRunKeepsMode()
    {
        var rules = Founded(true);
        rules.State.defeated = true;
        var next = TowerRules.LegacyRun(rules.State);
        Assert.AreEqual("lair", next.mode);
        Assert.AreEqual("dormant", next.introPhase);
        Assert.AreEqual(0, next.lair.heartFloor);
    }

    [Test] public void SaveRoundTripKeepsDungeon()
    {
        var rules = Founded(false);
        Assert.IsNull(rules.LairSpawnParty("normal"));
        var copy = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        var loaded = new TowerRules(copy);
        Assert.AreEqual(-4, loaded.HeartFloor);
        Assert.AreEqual(1, copy.lair.parties.Count);
        Assert.AreEqual(rules.State.lair.monsters.Count, copy.lair.monsters.Count);
    }
}
