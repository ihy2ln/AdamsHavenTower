using System.Collections.Generic;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// Expedition immersion (BM plan, step 7): weather, the run clock, the end-of-run summary, the guild journal,
// synthesised sound and party banter.
public sealed class ExpeditionLifeTests
{
    [SetUp] public void Setup() { TowerRules.InstantConstruction = true; TowerRules.TraversalEvents = false; TowerRules.GridMaps = true; TowerRules.Weathers = true; }
    [TearDown] public void Teardown()
    {
        TowerRules.InstantConstruction = false; TowerRules.TraversalEvents = true; TowerRules.GridMaps = true; TowerRules.Weathers = true;
        TowerRules.Today = () => System.DateTime.Now.Date;
    }

    private static TowerRules Expedition(string region = "silverbrook_edge")
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
        if (!rules.State.regionsUnlocked.Contains(region)) rules.State.regionsUnlocked.Add(region);
        if (rules.State.research == null) rules.State.research = new List<string>();
        rules.State.research.Add("EXP-1");
        Assert.IsNull(rules.StartExpedition(region, new List<string> { "kaela", "ghislaine", "elara" }, 6, 2, 3));
        return rules;
    }

    // Moves the run's clock forward a day at a time until the weather is `want` (or gives up).
    private static bool SeekWeather(TowerRules rules, string want)
    {
        for (int d = 0; d < 60; d++)
        {
            if (rules.Weather == want) return true;
            rules.Run.clock += TowerRules.DaySeconds;
        }
        return false;
    }

    [Test]
    public void WeatherHoldsForADayAndShapesTheWalk()
    {
        var rules = Expedition("shallow_ford");     // the lakes: rain and fog are common
        string first = rules.Weather;
        rules.Run.clock += TowerRules.DaySeconds * 0.2f;
        Assert.AreEqual(first, rules.Weather, "one weather per day");
        var seen = new HashSet<string>();
        for (int d = 0; d < 40; d++) { seen.Add(rules.Weather); rules.Run.clock += TowerRules.DaySeconds; }
        Assert.IsTrue(seen.Contains("rain") && seen.Contains("fog") && seen.Contains("clear"), string.Join(",", seen));

        // The same route costs more rations and less threat in the rain.
        var map = rules.Overworld;
        string target = rules.Web.EdgesOf(map.Camp.id)[0].Other(map.Camp.id);    // a place linked to the camp
        Assert.IsTrue(SeekWeather(rules, "clear"));
        var dry = rules.AtlasRoute(target);
        Assert.IsNull(dry.error);
        Assert.IsTrue(SeekWeather(rules, "rain"));
        var wet = rules.AtlasRoute(target);
        Assert.AreEqual(dry.rations * TowerRules.RainRations, wet.rations, 0.01f);
        Assert.Less(wet.threat, dry.threat + 1, "rain never adds threat");

        TowerRules.Weathers = false;
        Assert.AreEqual("clear", rules.Weather, "tests can switch weather off");
    }

    [Test]
    public void ClockLineNamesTheDayHourAndWeather()
    {
        var rules = Expedition();
        string line = rules.ClockLine();
        StringAssert.StartsWith("Day 1", line);
        StringAssert.Contains(TowerRules.WeatherName(rules.Weather), line);
        Assert.AreEqual(0f, TowerRules.NightLevel(12f));
        Assert.AreEqual(1f, TowerRules.NightLevel(23f));
        Assert.AreEqual(0.5f, TowerRules.NightLevel(19f), 0.001f);
        Assert.AreEqual("Dusk", TowerRules.PartOfDay(18f));
    }

    [Test]
    public void SummaryCountsWhatCameHomeAndWhatWasLeft()
    {
        var rules = Expedition();
        rules.Run.haul.Add(new TowerLoot { name = "Purse", gold = 100, ore = 3 });
        rules.Run.battlesWon = 2;
        Assert.IsTrue(rules.AtSafeExit);
        Assert.IsNull(rules.EndExpedition(false));
        var s = rules.LastSummary;
        Assert.IsNotNull(s);
        Assert.IsFalse(s.wiped); Assert.IsFalse(s.retreated);
        Assert.AreEqual(100, s.gold); Assert.AreEqual(3, s.ore); Assert.AreEqual(0, s.goldLeft);
        Assert.AreEqual(2, s.fights);

        // A retreat from the wilds drops a quarter and says so.
        var away = Expedition();
        away.Run.haul.Add(new TowerLoot { name = "Purse", gold = 100 });
        var poi = away.Overworld.pois.Find(p => p.kind != "camp");
        away.Run.cx = poi.x; away.Run.cy = poi.y; away.Run.at = poi.id;
        Assert.IsFalse(away.AtSafeExit);
        Assert.IsNull(away.EndExpedition(false));
        Assert.IsTrue(away.LastSummary.retreated);
        Assert.AreEqual(25, away.LastSummary.goldLeft);
        Assert.AreEqual(75, away.LastSummary.gold);
    }

    [Test]
    public void JournalKeepsRegionsBeastsAndEventsAcrossSaves()
    {
        var rules = Expedition();
        Assert.Contains("silverbrook_edge", rules.Journal.regions);
        rules.RecordBeasts(new[] { "glasswing_mite", "glasswing_mite", "cobalt_burrower" });
        Assert.AreEqual(2, rules.Journal.beasts.Count, "each beast once");
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        Assert.Contains("cobalt_burrower", loaded.Journal.beasts);
        Assert.AreEqual("Cobalt Burrower", BattleCatalog.SpeciesName("cobalt_burrower"));
        StringAssert.Contains("found in", BattleCatalog.SpeciesInfo("cobalt_burrower"));
        Assert.AreEqual(98, BattleCatalog.SpeciesIds.Length, "every bestiary form");

        // An old save without a journal gets an empty one.
        var state = JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State));
        state.journal = null;
        Assert.IsNotNull(new TowerRules(state).Journal.events);
    }

    [Test]
    public void EverySoundSynthesisesCleanly()
    {
        string[] ids = { "click", "confirm", "error", "step", "step_road", "card", "swing", "hit", "crit", "heal", "status", "down",
            "ult", "chime", "reward", "door", "shift", "victory", "defeat", "amb_forest", "amb_marsh", "amb_highland", "amb_ruins",
            "amb_heart", "amb_cave", "amb_crystal", "amb_blight", "amb_battle", "amb_night" };
        foreach (var id in ids)
        {
            var b = TowerAudio.Synth(id);
            Assert.IsNotNull(b, id);
            Assert.Greater(b.Length, 100, id);
            float peak = 0;
            foreach (var v in b) { Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), id); peak = Mathf.Max(peak, Mathf.Abs(v)); }
            Assert.Greater(peak, 0.3f, id + " is audible");
            Assert.LessOrEqual(peak, 1f, id + " does not clip");
        }
        Assert.IsNull(TowerAudio.Synth("no_such_sound"));
        // Loops join without a jump at the seam.
        foreach (var id in new[] { "amb_forest", "amb_cave", "amb_battle" })
        {
            var b = TowerAudio.Synth(id);
            Assert.Less(Mathf.Abs(b[b.Length - 1] - b[0]), 0.2f, id + " seam");
        }
        Assert.AreEqual("marsh", TowerAudio.BiomeBed("lakes"));
        Assert.AreEqual("cave", TowerAudio.ThemeBed("mine"));
    }

    [Test]
    public void BanterComesFromALivingFighter()
    {
        var rng = new System.Random(3);
        var party = new List<string> { "kaela", "daisy" };
        for (int i = 0; i < 20; i++)
        {
            string line = TowerBanter.Pick(party, new List<int> { 0, 80 }, TowerBanter.Threat, rng);
            StringAssert.StartsWith("Daisy:", line, "the fallen do not speak");
        }
        Assert.IsNull(TowerBanter.Pick(party, new List<int> { 0, 0 }, TowerBanter.Camp, rng));
        var rules = Expedition();
        rules.Run.hp[0] = 20;
        Assert.AreEqual(TowerBanter.Hurt, TowerBanter.Mood(rules));
    }
}
