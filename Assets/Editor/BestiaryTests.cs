using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// The bestiary data (Resources/AdamsHaven/Bestiary/bestiary.json, Tools/build_bestiary.py) and how encounters use it.
public sealed class BestiaryTests
{
    private static readonly string[] Legacy = { "shardling_sprout", "flintjaw_skitterer", "amberhide_grazer", "thorncrystal_stalker",
        "quartzback_hound", "glasswing_mite", "obsidian_talon", "viridian_prism_warden", "cobalt_burrower", "moonstone_ravager",
        "stormglass_wyvern", "eclipse_core_golem" };

    [Test]
    public void FamiliesSplitTheirRankRangeIntoAnEvolutionChain()
    {
        Assert.IsTrue(BattleBestiary.Available);
        Assert.AreEqual(31, BattleBestiary.Families.Count);
        foreach (var f in BattleBestiary.Families)
        {
            Assert.That(f.minRank, Is.InRange(1, 9), f.id);
            Assert.That(f.maxRank, Is.InRange(f.minRank, 9), f.id);
            Assert.AreEqual(f.minRank, f.forms[0].minRank, f.id + " starts at its first form");
            Assert.AreEqual(f.maxRank, f.forms.Last().maxRank, f.id + " ends at its apex form");
            for (int i = 0; i < f.forms.Length; i++)
            {
                var form = f.forms[i];
                Assert.LessOrEqual(form.minRank, form.maxRank, form.id);
                Assert.IsNotEmpty(form.cards, form.id + " has moves");
                Assert.IsFalse(string.IsNullOrEmpty(form.art), form.id + " has art or a stand-in");
                if (i + 1 < f.forms.Length)
                {
                    Assert.AreEqual(form.maxRank + 1, f.forms[i + 1].minRank, form.id + " hands over without a gap");
                    Assert.AreEqual(f.forms[i + 1].id, form.evolvesTo, form.id);
                }
                else Assert.IsEmpty(form.evolvesTo, form.id + " is the apex");
            }
            for (int rank = f.minRank; rank <= f.maxRank; rank++)
            {
                var form = BattleBestiary.FormFor(f, rank);
                Assert.That(rank, Is.InRange(form.minRank, form.maxRank), f.id + " at " + BattleBestiary.RankName(rank));
            }
        }
        var goblins = BattleBestiary.Family("goblinkin");
        Assert.AreEqual("Ashvein Goblin Scavenger", BattleBestiary.FormFor(goblins, 1).name);
        Assert.AreEqual("Ashvein Hobgoblin", BattleBestiary.FormFor(goblins, 3).name, "goblin evolves into hobgoblin at D");
    }

    [Test]
    public void LegacySpeciesKeepTheirIdsAndMoves()
    {
        foreach (var id in Legacy) Assert.IsNotNull(BattleBestiary.Form(id), id);
        var unit = new BattleUnit { Id = "x", Species = "flintjaw_skitterer", Element = BattleElement.Fire };
        var cards = BattleCatalog.EnemyCards(unit);
        CollectionAssert.AreEqual(new[] { "Corrupted Claw", "Ember Bite" }, cards.Select(c => c.Name).ToArray());
        Assert.AreEqual("Burn", cards[1].Status);
    }

    [Test]
    public void EveryDepthAndThemeCanSpawnAndRanksStayNearTheDepth()
    {
        string[] themes = { "briar", "crystal", "marsh", "edge", "ruin", "mine", "cave", "heartwood", "keep", "blight", "" };
        for (int depth = 1; depth <= 13; depth++)
        {
            int natural = BattleBestiary.RankForDepth(depth);
            foreach (var theme in themes)
                for (int seed = 1; seed <= 6; seed++)
                {
                    var e = BattleCatalog.Build(new BattleEncounterSpec { Depth = depth, Kind = "normal", Theme = theme, Seed = seed * 31 + depth });
                    Assert.IsNotEmpty(e.Enemies);
                    foreach (var u in e.Enemies)
                    {
                        Assert.That(u.Rank, Is.InRange(natural - 1, natural + 1), depth + "/" + theme);
                        Assert.Less(u.Rank, 9, "packs never roll SSR");
                        var form = BattleBestiary.Form(u.Species);
                        Assert.That(u.Rank, Is.InRange(form.minRank, form.maxRank), u.Species + " fits its rank");
                    }
                }
        }
    }

    [Test]
    public void ElitesEvolveAndBossesUseTheirFamilysHigherForms()
    {
        var elite = BattleCatalog.Build(new BattleEncounterSpec { Depth = 5, Kind = "elite", Theme = "ruin", Seed = 9 });
        var e = elite.Enemies.First(u => u.Affix.Length > 0);
        Assert.GreaterOrEqual(e.Rank, BattleBestiary.RankForDepth(5) + 1, "an elite is a rank above the depth");
        foreach (var region in TowerRegionsWithBosses())
        {
            var boss = BattleCatalog.Build(new BattleEncounterSpec { Depth = region.Value, Kind = "boss", Region = region.Key, Seed = 4 });
            var form = BattleBestiary.Form(boss.Boss.Species);
            Assert.IsNotNull(form, region.Key);
            Assert.AreEqual(BattleBestiary.Boss(region.Key).family, form.Family.id, region.Key);
            Assert.That(boss.Boss.Rank, Is.InRange(form.minRank, form.maxRank));
        }
        var heart = BattleCatalog.Build(new BattleEncounterSpec { Depth = 13, Kind = "boss", Region = "silverwood_d5", Seed = 4 });
        Assert.AreEqual(9, heart.Boss.Rank, "the Silverwood Heart lair boss is SSR");
        Assert.AreEqual("heartrot_colossus", heart.Boss.Species);
    }

    [Test]
    public void APackAtItsNaturalRankKeepsThePreBestiaryNumbers()
    {
        // Same formula as before the bestiary: depth 5 amberhide grazer (Bruiser), rank D is the natural rank for depth 5.
        var e = BattleCatalog.Build(new BattleEncounterSpec { Depth = 5, Kind = "normal", Theme = "briar", Seed = 1 });
        foreach (var u in e.Enemies)
        {
            var form = BattleBestiary.Form(u.Species);
            float scale = 1f + BattleCatalog.RankStep * (u.Rank - BattleBestiary.RankForDepth(5));
            float shapeHp = form.role == "Bruiser" ? 1.35f : form.role == "Skirmisher" ? .72f : form.role == "Caster" ? .85f : 1.20f;
            int expected = (int)(BattleCatalog.EnemyHealth * shapeHp * (1f + BattleCatalog.HealthPerDepth * 4) * (form.large ? 1.25f : 1f) * scale);
            Assert.AreEqual(expected, u.MaxHp, 1, u.Species);
        }
    }

    [Test]
    public void TheCommanderIsAWhisperer()
    {
        var c = BattleCatalog.EnemyCommander(5);
        Assert.AreEqual(BattleBestiary.FormFor(BattleBestiary.Family("whisperers"), BattleBestiary.RankForDepth(5)).name, c.Name);
        Assert.Greater(c.Rank, 0);
    }

    private static IEnumerable<KeyValuePair<string, int>> TowerRegionsWithBosses()
    {
        foreach (var r in AdamsHaven.Tower.TowerRules.Regions)
            yield return new KeyValuePair<string, int>(r.id, r.bossDepth);
    }
}
