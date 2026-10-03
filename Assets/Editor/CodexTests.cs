using System.Collections.Generic;
using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// The BESTIARY + CODEX cards (CodexCards, CodexPanel): every monster form, fighter, roster character and lair boss has
// a card with art, numbers and a back, and what the guild has not seen stays hidden.
public sealed class CodexTests
{
    [SetUp] public void Fresh() { CodexCards.Forget(); }

    [Test]
    public void EveryBestiaryFormHasACardWithFourBackRows()
    {
        var monsters = CodexCards.Monsters();
        int forms = BattleBestiary.Families.Sum(f => f.forms.Length);
        Assert.AreEqual(forms, monsters.Count);
        Assert.AreEqual(monsters.Count, monsters.Select(m => m.Id).Distinct().Count(), "one card per form");
        foreach (var m in monsters)
        {
            var rows = CodexCards.Moves(m);
            Assert.AreEqual(4, rows.Count, m.Id + ": the painted move card has four panels");
            int moves = Mathf.Min(4, m.Form.cards.Length);
            for (int i = 0; i < moves; i++)
            {
                Assert.AreEqual(m.Form.cards[i].name, rows[i].Name, m.Id);
                Assert.AreEqual(m.Form.cards[i].ep, rows[i].Cost, m.Id);
                Assert.IsNotEmpty(rows[i].Text, m.Id + " " + rows[i].Name);
            }
            for (int i = moves; i < 4; i++) Assert.AreEqual("Passive", rows[i].Tag, m.Id + " fills with GKOM passives");
            Assert.AreEqual(4, CodexCards.FrontStats(m).Count, m.Id);
            Assert.IsTrue(CodexCards.Front(m, true) != null || CodexCards.Cutout(m) != null, m.Id + " has painted art or a battle cutout");
        }
    }

    [Test]
    public void MonsterNumbersFollowTheRankShown()
    {
        for (int rank = 1; rank <= 8; rank++)
            Assert.AreEqual(rank, BattleBestiary.RankForDepth(BattleBestiary.DepthForRank(rank)), "rank " + rank + " is natural at its depth");
        foreach (var family in BattleBestiary.Families)
            foreach (var form in family.forms)
            {
                var low = BattleCatalog.Specimen(form, form.minRank);
                var high = BattleCatalog.Specimen(form, form.maxRank);
                Assert.AreEqual(form.id, low.Species);
                Assert.AreEqual(form.minRank, low.Rank);
                Assert.GreaterOrEqual(high.MaxHp, low.MaxHp, form.id + " grows with rank");
                Assert.AreEqual(form.minRank, BattleCatalog.Specimen(form, 0).Rank, form.id + " clamps into its span");
            }
    }

    [Test]
    public void FightersCardsCarryTheirWholeKit()
    {
        var fighters = CodexCards.Fighters();
        CollectionAssert.AreEqual(new[] { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "jd" }, fighters.Select(f => f.Id).ToArray());
        foreach (var f in fighters)
        {
            Assert.IsNotNull(CodexCards.Front(f, false), f.Id + " has card art");
            var rows = CodexCards.Moves(f);
            if (f.Id == "jd")
            {
                Assert.AreEqual(BattleCatalog.SummonerKit().Count + BattleCatalog.SummonerUltimates().Count, rows.Count);
                Assert.IsTrue(rows.Where(r => r.Tag != "Decree").All(r => r.CostUnit == "AP"), "JD pays in AP");
            }
            else
            {
                Assert.AreEqual(BattleCatalog.FighterKit(f.Fighter).Count + 2 + 1, rows.Count, f.Id + ": kit, two ultimates, awakening");
                Assert.AreEqual(2, rows.Count(r => r.Tag == "Ultimate"), f.Id);
                Assert.AreEqual(1, rows.Count(r => r.Tag == "Awakening"), f.Id);
            }
            Assert.That(CodexCards.Stats(f).Count, Is.GreaterThanOrEqualTo(3), f.Id);
        }
    }

    [Test]
    public void EveryRosterCharacterHasACard()
    {
        var roster = CodexCards.Roster();
        Assert.AreEqual(TowerRoster.All.Count, roster.Count);
        Assert.IsTrue(roster.TakeWhile(r => r.Unit.IsHero).Count() == roster.Count(r => r.Unit.IsHero), "heroes come first");
        foreach (var r in roster)
        {
            Assert.IsNotNull(CodexCards.Front(r, true), r.Id + " has card-front art");
            Assert.AreEqual(7, CodexCards.Stats(r).Count, r.Id);
            Assert.AreEqual(4, CodexCards.FrontStats(r).Count, r.Id);
            Assert.IsNotEmpty(CodexCards.Moves(r), r.Id + " has a back");
            Assert.IsTrue(CodexCards.Moves(r).All(m => !m.Text.Contains("Visual:")), r.Id + ": art notes stay off the card");
        }
    }

    [Test]
    public void LairBossCardsMatchTheBossYouFight()
    {
        var bosses = CodexCards.Bosses();
        Assert.AreEqual(13, bosses.Count);
        foreach (var b in bosses)
        {
            Assert.IsTrue(b.Fighter.Boss, b.Id);
            var spec = new BattleEncounterSpec { Depth = b.Region.bossDepth, Kind = "boss", Region = b.Region.id, Theme = b.Lair.theme, Seed = 7 };
            var fought = BattleCatalog.Build(spec).Boss;
            Assert.AreEqual(fought.Species, b.Fighter.Species, b.Id);
            Assert.AreEqual(fought.Name, b.Name, b.Id);
            Assert.AreEqual(fought.MaxHp, b.Fighter.MaxHp, b.Id);
            Assert.AreEqual(fought.Rank, b.Rank, b.Id);
            var rows = CodexCards.Moves(b);
            Assert.AreEqual(BattleCatalog.EnemyCards(b.Fighter).Count, rows.Count, b.Id);
            Assert.AreEqual(1, rows.Count(r => r.Tag == "Signature"), b.Id);
        }
    }

    [Test]
    public void UnseenBeastsAndUnwalkedLairsStayHidden()
    {
        var monster = CodexCards.Monsters()[0];
        var boss = CodexCards.Bosses()[0];
        var none = new CodexContext { Beasts = new List<string>(), Regions = new List<string>(), Conquered = new List<string>(), Recruited = new List<string>() };
        Assert.IsFalse(none.Knows(monster));
        Assert.IsFalse(none.Knows(boss));
        Assert.IsTrue(none.Knows(CodexCards.Fighters()[0]), "the party is always known");
        Assert.IsTrue(none.Knows(CodexCards.Roster()[0]), "every Tower character is listed");
        var seen = new CodexContext { Beasts = new List<string> { monster.Id }, Regions = new List<string> { boss.Lair.region },
            Conquered = new List<string>(), Recruited = new List<string> { CodexCards.Roster()[0].Id } };
        Assert.IsTrue(seen.Knows(monster));
        Assert.IsTrue(seen.Knows(boss));
        Assert.IsFalse(seen.Defeated(boss));
        Assert.IsTrue(seen.Recruits(CodexCards.Roster()[0]));
        Assert.IsTrue(CodexContext.Everything.Knows(monster) && CodexContext.Everything.Knows(boss), "sandbox battles hide nothing");
    }

    [Test]
    public void TheContextComesFromTheGuildJournal()
    {
        var rules = TowerRules.New();
        var monster = CodexCards.Monsters()[0];
        var context = CodexContext.From(rules);
        Assert.IsFalse(context.Knows(monster));
        rules.RecordBeasts(new[] { monster.Id });
        Assert.IsTrue(CodexContext.From(rules).Knows(monster));
    }

    [Test]
    public void PaintedMoveCardsHaveFourStackedPanels()
    {
        int painted = 0;
        foreach (var m in CodexCards.Monsters())
        {
            if (CodexCards.MovesArt(m) == null) continue;
            painted++;
            var panels = CodexCards.Panels(m);
            Assert.AreNotSame(CodexCards.DefaultPanels, panels, m.Id + " has detected panels");
            Assert.AreEqual(4, panels.Length, m.Id);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(panels[i].x, Is.InRange(0f, .5f), m.Id);
                Assert.That(panels[i].xMax, Is.InRange(.5f, 1f), m.Id);
                Assert.That(panels[i].yMax, Is.LessThanOrEqualTo(1f), m.Id);
                if (i > 0) Assert.Greater(panels[i].y, panels[i - 1].yMax - .01f, m.Id + " panels stack downward");
            }
        }
        Debug.Log("Codex: " + painted + " monster cards painted so far.");
    }

    [Test]
    public void MoveTextReadsLikeThePaintedCards()
    {
        Assert.AreEqual("Physical hit to one foe (1.2x).", CodexCards.Describe(BattleTarget.Enemy, 1.2f, false, 0f, "", 0f, 0));
        Assert.AreEqual("Magic hit to every foe (0.7x); Burn 5/turn for 2 turns.", CodexCards.Describe(BattleTarget.AllEnemies, .7f, true, 0f, "Burn", 5f, 2));
        Assert.AreEqual("Defense up 35% on itself for 2 turns.", CodexCards.Describe(BattleTarget.Self, 0f, false, 0f, "DefenseUp", .35f, 2));
        Assert.AreEqual("Heals an ally for 28.", CodexCards.Describe(BattleTarget.Ally, 0f, false, 28f, "", 0f, 0));
    }
}
