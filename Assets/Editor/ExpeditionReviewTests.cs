using System.Collections.Generic;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// Expedition review fixes (BM plan, step 1): dungeon re-entry, retreat tax, the run's own clock, heroes away,
// and the capped dock skirmish.
public sealed class ExpeditionReviewTests
{
    [SetUp] public void Setup() { TowerRules.InstantConstruction = true; TowerRules.TraversalEvents = false; TowerRules.GridMaps = false; }
    [TearDown] public void Teardown()
    {
        TowerRules.InstantConstruction = false; TowerRules.TraversalEvents = true; TowerRules.GridMaps = false;
        TowerRules.Today = () => System.DateTime.Now.Date;
    }

    private static TowerRules Expedition(params string[] party)
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
        var list = new List<string>(party.Length > 0 ? party : new[] { "kaela", "ghislaine", "elara" });
        Assert.IsNull(rules.StartExpedition("silverbrook_edge", list, 6, 2, 3));
        return rules;
    }

    [Test]
    public void ReenteringADungeonKeepsItsRoomsDone()
    {
        var rules = Expedition();
        var poi = rules.RunLayout.nodes.Find(n => n.kind == "elite");
        rules.Run.at = poi.id;
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        int index = d.rooms.FindIndex(r => !r.goal && (r.kind == "treasure" || r.kind == "rest" || r.kind == "unknown"));
        Assert.GreaterOrEqual(index, 0, "the first floor has a non-fight room");
        var room = d.rooms[index];
        rules.Run.px = room.CenterX; rules.Run.py = room.CenterY;
        int haul = rules.Run.haul.Count;
        Assert.IsNull(rules.ResolveRoom("investigate"));
        Assert.IsNull(rules.LeaveDungeon());
        Assert.IsNull(rules.EnterPoi());
        rules.Run.px = room.CenterX; rules.Run.py = room.CenterY;
        Assert.AreEqual(-1, rules.PendingRoom, "a finished room stays finished on the next visit");
        Assert.IsTrue(rules.Run.roomsDone.Contains(index));
        Assert.LessOrEqual(rules.Run.haul.Count, haul + 1, "no second roll of the same room");
    }

    [Test]
    public void RetreatingAwayFromCampLeavesAQuarterOfTheHaul()
    {
        var rules = Expedition();
        Assert.IsTrue(rules.AtSafeExit, "a run starts at its entrance");
        var far = rules.RunLayout.nodes.Find(n => n.kind != "camp" && n.id != rules.RunLayout.entrance);
        rules.Run.at = far.id;
        Assert.IsFalse(rules.AtSafeExit);
        rules.Run.haul.Add(new TowerLoot { name = "test", gold = 100, ore = 4 });
        Assert.AreEqual(25, rules.RetreatLoss);
        int gold = rules.State.gold, ore = rules.State.ore;
        Assert.IsNull(rules.EndExpedition(false));
        Assert.AreEqual(gold + 75, rules.State.gold);
        Assert.AreEqual(ore + 3, rules.State.ore);
    }

    [Test]
    public void TheSafePocketIsNeverTaxed()
    {
        var rules = Expedition();
        rules.Run.at = rules.RunLayout.nodes.Find(n => n.kind != "camp" && n.id != rules.RunLayout.entrance).id;
        rules.Run.haul.Add(new TowerLoot { name = "kept", gold = 80 });
        Assert.IsNull(rules.PocketLoot(0));
        int gold = rules.State.gold;
        Assert.IsNull(rules.EndExpedition(false));
        Assert.AreEqual(gold + 80, rules.State.gold);
    }

    [Test]
    public void HeroesOnAnExpeditionAreAwayFromTheTower()
    {
        var rules = Expedition("kaela");
        var kaela = rules.HeroResident("kaela");
        Assert.IsNotNull(kaela, "the starter hero lives in the Tower");
        Assert.IsTrue(kaela.away);
        Assert.IsNull(rules.EndExpedition(false));
        Assert.IsFalse(kaela.away);
    }

    [Test]
    public void TheRunKeepsItsOwnClockAndRestSleepsUntilDawn()
    {
        TowerRules.GridMaps = true;
        var rules = Expedition();
        var run = rules.Run;
        float start = rules.RunClock;
        Assert.AreEqual(rules.State.clock, start, 0.001f);
        // Walk to the farthest seen cell: travel time passes on the run's clock, not the Tower's.
        var map = rules.Overworld;
        int bx = -1, by = -1, best = -1;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                int dd = (x - run.cx) * (x - run.cx) + (y - run.cy) * (y - run.cy);
                if (rules.CellSeen(x, y) && map.Walkable(x, y) && dd > best && rules.GridPreview(x, y).error == null) { best = dd; bx = x; by = y; }
            }
        Assert.Greater(best, 0);
        float towerClock = rules.State.clock;
        Assert.IsNull(rules.GridMove(bx, by).error);
        Assert.Greater(run.clock, 0f, "walking takes time");
        Assert.AreEqual(towerClock, rules.State.clock, 0.001f, "the Tower stays paused");
        // Back at camp at 22:00: a rest sleeps until 06:00.
        var camp = map.Camp;
        run.cx = camp.x; run.cy = camp.y; run.at = camp.id;
        float hourNow = rules.RunHour();
        run.clock += ((22f - hourNow + 24f) % 24f) * TowerRules.DaySeconds / 24f;
        Assert.AreEqual(22f, rules.RunHour(), 0.01f);
        Assert.IsNull(rules.CampRest());
        Assert.AreEqual(6f, rules.RunHour(), 0.01f);
    }

    // ---- step 3: run rewards

    private static TowerRules WonFight(string kind)
    {
        var rules = Expedition();
        var poi = rules.RunLayout.nodes.Find(n => n.kind == (kind == "elite" ? "elite" : "combat"));
        rules.Run.at = poi.id;
        Assert.IsNull(rules.EnterPoi());
        var d = rules.Dungeon;
        int index = d.rooms.FindIndex(r => r.kind == (kind == "elite" ? "elite" : "enemy"));
        Assert.GreaterOrEqual(index, 0);
        rules.Run.px = d.rooms[index].CenterX; rules.Run.py = d.rooms[index].CenterY;
        Assert.IsNull(rules.ResolveBattle(true, new List<int> { 80, 80, 80 }));
        return rules;
    }

    [Test]
    public void AWonFightOffersThreeRewardsAndBlocksTheWayUntilOneIsTaken()
    {
        var rules = WonFight("enemy");
        Assert.IsTrue(rules.RewardPending);
        Assert.AreEqual(TowerRules.RewardChoices, rules.Run.rewardOffer.Count);
        Assert.AreEqual(rules.Run.rewardOffer.Count, new HashSet<string>(rules.Run.rewardOffer).Count, "three different offers");
        Assert.AreEqual("Choose your reward first.", rules.EventBlock());
        Assert.IsNull(rules.ChooseReward(0));
        Assert.IsFalse(rules.RewardPending);
        Assert.IsNull(rules.EventBlock());
    }

    [Test]
    public void ElitesAlwaysOfferARelic()
    {
        var rules = WonFight("elite");
        Assert.IsTrue(rules.Run.rewardOffer.Exists(o => o.StartsWith("relic:")));
    }

    [Test]
    public void UpgradesRaiseACardForTheRestOfTheRun()
    {
        var rules = WonFight("enemy");
        rules.Run.rewardOffer = new List<string> { "upgrade:mv_frost_jab", "tonic", "rations" };
        Assert.IsNull(rules.ChooseReward(0));
        Assert.AreEqual(2, rules.CardLevel("mv_frost_jab"));
        Assert.AreEqual(2, rules.CardLevels()["mv_frost_jab"]);
        var card = new BattleCard { Power = 1f, Level = 2 };
        Assert.AreEqual(1f + BattleCard.LevelStep, card.EffectivePower, 1e-4f);
    }

    [Test]
    public void RelicsChangeTheFightAndTheRun()
    {
        var rules = WonFight("enemy");
        rules.Run.rewardOffer = new List<string> { "relic:war_drum", "tonic", "rations" };
        Assert.IsNull(rules.ChooseReward(0));
        Assert.AreEqual(.08f, rules.BattleModifiers().DamageBonus, 1e-4f);
        // Mending Moss: the next won fight heals the party by 8%.
        rules.Run.relics.Add("mending_moss");
        var d = rules.Dungeon;
        int next = d.rooms.FindIndex(r => !r.goal && r.kind != "entrance" && r.kind != "stairs" && !rules.Run.roomsDone.Contains(d.rooms.IndexOf(r)));
        Assert.GreaterOrEqual(next, 0);
        d.rooms[next].kind = "enemy";
        rules.Run.px = d.rooms[next].CenterX; rules.Run.py = d.rooms[next].CenterY;
        Assert.IsNull(rules.ResolveBattle(true, new List<int> { 50, 80, 80 }));
        Assert.AreEqual(58, rules.Run.hp[0]);
    }

    [Test]
    public void StressCarriesBetweenFightsAndRestEasesIt()
    {
        var rules = Expedition();
        rules.RecordStress(new List<int> { 60, 30, 0 });
        Assert.AreEqual(60, rules.Stress(0));
        var camp = rules.RunLayout.nodes.Find(n => n.kind == "camp");
        if (camp != null) { rules.Run.at = camp.id; Assert.IsNull(rules.CampRest()); Assert.AreEqual(20, rules.Stress(0)); Assert.AreEqual(0, rules.Stress(1)); }
    }

    [Test]
    public void DockSkirmishesPayThreeTimesADay()
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        int gold = rules.State.gold;
        for (int i = 0; i < 5; i++) rules.PaySkirmish(100, 4);
        Assert.AreEqual(gold + 300, rules.State.gold, "wins after the third are practice");
        Assert.IsFalse(rules.SkirmishPays);
    }
}
