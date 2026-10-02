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
        TowerRules.InstantConstruction = false; TowerRules.TraversalEvents = true; TowerRules.GridMaps = true;
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
    public void APlateRunMovesOntoTheGridAndKeepsItsHaul()
    {
        // A save from before the grid map: a plate run part way through a dungeon.
        var rules = Expedition();
        var run = rules.Run;
        Assert.AreEqual("", run.mapKind);
        var poi = rules.RunLayout.nodes.Find(n => n.kind == "combat");
        run.at = poi.id;
        Assert.IsNull(rules.EnterPoi());
        run.haul.Add(new TowerLoot { name = "Test purse", gold = 120 });
        run.hp[1] = 40;
        run.relics.Add("war_drum");
        var saved = JsonUtility.ToJson(rules.State);

        // The game loads it with grid maps on.
        TowerRules.GridMaps = true;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(saved));
        var moved = loaded.Run;
        Assert.IsNotNull(moved, "the run survives");
        Assert.IsTrue(loaded.GridRun);
        Assert.AreEqual("", moved.dungeonPoi, "back out on the map");
        Assert.AreEqual(loaded.Overworld.Camp.id, moved.at, "at the new camp");
        Assert.IsTrue(loaded.AtSafeExit);
        Assert.AreEqual(40, moved.hp[1]);
        Assert.AreEqual(120, moved.haul.Find(l => l.name == "Test purse").gold);
        Assert.IsTrue(loaded.HasRelic("war_drum"));
        Assert.IsTrue(loaded.CellSeen(moved.cx, moved.cy), "the camp is in sight");
        Assert.IsFalse(loaded.MigratePlateRun(), "only once");
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

    // ---- step 4: places and dungeons

    private static TowerRules Inside(string kind)
    {
        TowerRules.GridMaps = true;   // every grid map holds a merchant, a shrine, a vault and elites
        var rules = Expedition();
        var poi = rules.RunLayout.nodes.Find(n => n.kind == kind);
        Assert.IsNotNull(poi, "the map has a " + kind);
        rules.Run.at = poi.id;
        Assert.IsNull(rules.EnterPoi());
        return rules;
    }

    private static void StandIn(TowerRules rules, int room)
    {
        var d = rules.Dungeon;
        rules.Run.px = d.rooms[room].CenterX; rules.Run.py = d.rooms[room].CenterY;
    }

    [Test]
    public void MerchantsAndShrinesAreAStepFromTheDoor()
    {
        foreach (var kind in new[] { "merchant", "shrine" })
        {
            var rules = Inside(kind);
            Assert.AreEqual(2, rules.Dungeon.rooms.Count, kind + ": entrance and the goal only");
            Assert.AreEqual(kind, rules.Dungeon.rooms[1].kind);
        }
    }

    [Test]
    public void TheMerchantSellsForHaulGold()
    {
        var rules = Inside("merchant");
        StandIn(rules, 1);
        rules.Run.haul.Add(new TowerLoot { name = "purse", gold = 200 });
        int rations = rules.Run.rations;
        Assert.IsNull(rules.ResolveRoom("buy_rations"));
        Assert.AreEqual(rations + 2, rules.Run.rations);
        Assert.IsNull(rules.ResolveRoom("buy_relic"));
        Assert.AreEqual(1, rules.Run.relics.Count);
        Assert.AreEqual(200 - TowerRules.MerchantPrice("rations") - TowerRules.MerchantPrice("relic"), rules.HaulGold);
        Assert.IsNotNull(rules.ResolveRoom("buy_relic"), "not enough gold left");
    }

    [Test]
    public void AShrineTradesBloodForARelic()
    {
        var rules = Inside("shrine");
        StandIn(rules, 1);
        Assert.IsNull(rules.ResolveRoom("offer"));
        Assert.AreEqual(1, rules.Run.relics.Count);
        Assert.AreEqual(85, rules.Run.hp[0]);
    }

    [Test]
    public void VaultsOfferARelicAfterTheHoard()
    {
        var rules = Inside("treasure");
        var d = rules.Dungeon;
        StandIn(rules, d.rooms.FindIndex(r => r.goal));
        Assert.IsNull(rules.ResolveRoom("take"));
        Assert.IsTrue(rules.RewardPending);
        Assert.IsTrue(rules.Run.rewardOffer.Exists(o => o.StartsWith("relic:")));
    }

    [Test]
    public void NewRoomKindsShowUpAndResolve()
    {
        var seen = new HashSet<string>();
        var node = new TowerForestNode { id = "p4", kind = "elite", theme = "ruin" };
        for (int seed = 1; seed <= 60; seed++)
            foreach (var room in TowerDungeon.Build("grid_edge", node, 0, seed * 977, 2).rooms) seen.Add(room.kind);
        foreach (var kind in new[] { "trap", "skillcheck", "story", "keygate" }) Assert.IsTrue(seen.Contains(kind), kind + " appears");
        var shapes = new HashSet<int>();
        for (int seed = 1; seed <= 30; seed++) shapes.Add(TowerDungeon.Build("grid_edge", node, 0, seed * 977, 2).rooms.Count);
        Assert.Greater(shapes.Count, 2, "dungeons come in more than one shape");

        var rules = Inside("elite");
        var d = rules.Dungeon;
        foreach (var kind in new[] { "trap", "skillcheck", "story", "keygate" })
        {
            int i = d.rooms.FindIndex(r => !r.goal && r.kind != "entrance" && r.kind != "stairs" && !rules.Run.roomsDone.Contains(d.rooms.IndexOf(r)));
            if (i < 0) break;
            d.rooms[i].kind = kind;
            StandIn(rules, i);
            string choice = kind == "trap" ? "disarm" : kind == "skillcheck" ? "attempt" : kind == "story" ? "read" : "force";
            Assert.IsNull(rules.ResolveRoom(choice), kind);
            Assert.IsTrue(rules.Run.roomsDone.Contains(i), kind + " is finished either way");
        }
    }

    [Test]
    public void CampCookingAndScouting()
    {
        TowerRules.GridMaps = true;
        var rules = Expedition();
        rules.Run.hp[0] = 50;
        rules.RecordStress(new List<int> { 50, 0, 0 });
        int rations = rules.Run.rations;
        Assert.IsNull(rules.CampCook());
        Assert.AreEqual(rations - 1, rules.Run.rations);
        Assert.AreEqual(65, rules.Run.hp[0]);
        Assert.AreEqual(25, rules.Stress(0));
        int seen = rules.Run.gridFog.Split('1').Length;
        Assert.IsNull(rules.CampScout());
        Assert.Greater(rules.Run.gridFog.Split('1').Length, seen, "scouting from camp lifts fog");
    }

    [Test]
    public void ConqueringALairRaisesTheConquestCard()
    {
        var rules = Expedition();
        var lair = rules.RunLayout.nodes.Find(n => n.kind == "lair");
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
        Assert.IsTrue(rules.ConquestPending);
        rules.DismissConquest();
        Assert.IsFalse(rules.ConquestPending);
    }

    [Test]
    public void EveryDungeonThemeHasItsOwnEventsAndRegionsTheirBiome()
    {
        foreach (var theme in new[] { "mine", "heartwood", "blight", "marsh", "ruin" })
            Assert.GreaterOrEqual(TowerEvents.All.FindAll(e => ("," + e.themes.Replace(" ", "") + ",").Contains("," + theme + ",")).Count, 3, theme);
        var traits = new HashSet<string>(TowerRules.Traits) { "" };
        foreach (var e in TowerEvents.All) foreach (var c in e.choices) Assert.IsTrue(traits.Contains(c.trait), e.id + " uses a real trait");
        Assert.AreEqual("lakes", TowerOverworldGen.BiomeForRegion("shallow_ford"));
        Assert.AreEqual("mountains", TowerOverworldGen.BiomeForRegion("watchpost_ruin"));
        Assert.AreEqual("edge", TowerOverworldGen.BiomeForRegion("silverbrook_edge"));
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
