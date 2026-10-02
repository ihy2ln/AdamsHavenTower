using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Expedition review step 2: encounters per theme / kind / region, and a headless balance check that auto-plays
// fights in every region with the party a player would have there (BattleAutoPlayer, the AUTO button's policy).
public sealed class ExpeditionBalanceTests
{
    private static readonly (string id, int depth, int boss)[] Regions =
    {
        ("silverbrook_edge",1,3),("rootside_camp",2,3),("shallow_ford",2,4),("moon_shrine",3,5),("old_bridge",3,5),
        ("sunken_marsh",4,6),("watchpost_ruin",5,7),("silverwood_gate",7,8),("silverwood_d1",8,9),("silverwood_d2",9,10),
        ("silverwood_d3",10,11),("silverwood_d4",11,12),("silverwood_d5",12,13),
    };
    private static readonly string[] Themes = { "briar", "marsh", "ruin", "cave", "crystal", "heartwood", "blight", "mine", "keep" };

    // The hero level and gear a player is expected to bring to a region of this danger.
    private static int Level(int depth) { return 1 + 3 * (depth - 1); }
    private static int Gear(int depth) { return depth >= 11 ? 3 : depth >= 8 ? 2 : depth >= 4 ? 1 : 0; }

    private static List<BattleUnit> Party(int depth)
    {
        var all = BattleCatalog.Party();
        foreach (var u in all)
        {
            int level = Level(depth), gear = Gear(depth);
            u.Attack *= 1 + .08f * gear; u.Magic *= 1 + .08f * gear;
            u.MaxHp = Mathf.RoundToInt(u.MaxHp * (1 + .06f * gear + .02f * (level - 1))); u.Hp = u.MaxHp;
            u.Defense *= 1 + .01f * (level - 1); u.Resistance *= 1 + .01f * (level - 1);
        }
        return all;
    }

    private static (float win, float rounds, float lost) Play(string region, int depth, string kind, int fights)
    {
        int wins = 0; float rounds = 0, lost = 0;
        for (int i = 0; i < fights; i++)
        {
            var party = Party(depth);
            var field = party.Take(3).ToList();
            var enc = BattleCatalog.Build(new BattleEncounterSpec { Depth = depth, Kind = kind, Theme = Themes[(i + depth) % Themes.Length],
                Region = region, Seed = 1000 + i * 7919 + depth });
            // JD grows with the party, as in the game (TowerExpeditionUi.FightWithParty).
            var jd = BattleCatalog.JD();
            float vigor = .02f * (Level(depth) - 1) + .06f * Gear(depth);
            jd.MaxHp = jd.Hp = Mathf.RoundToInt(jd.MaxHp * (1 + vigor)); jd.Defense *= 1 + vigor * .5f; jd.Resistance *= 1 + vigor * .5f;
            var b = new BattleState(1000 + i * 7919 + depth, field, party.Skip(3), enc.Enemies, BattleCatalog.Deck(party), jd, enc.Commander);
            float max = field.Sum(u => u.MaxHp);
            rounds += BattleAutoPlayer.PlayOut(b);
            if (b.Victory) wins++;
            lost += 1f - field.Sum(u => Mathf.Max(0, u.Hp)) / max;
        }
        return (wins / (float)fights, rounds / fights, lost / fights);
    }

    [Test]
    public void FightsFollowTheDifficultyCurveInEveryRegion()
    {
        var report = new System.Text.StringBuilder();
        var failures = new List<string>();
        foreach (var r in Regions)
            foreach (var kind in new[] { "normal", "elite", "boss" })
            {
                int depth = kind == "boss" ? r.boss : kind == "elite" ? r.depth + 1 : r.depth;
                var res = Play(r.id, depth, kind, 80);
                report.AppendLine(string.Format("{0,-18}{1,-8}{2,3}  win {3:P0}  rounds {4:0.0}  hp lost {5:P0}", r.id, kind, depth, res.win, res.rounds, res.lost));
                // Floors leave room for sampling noise at 80 fights per cell.
                float minWin = kind == "normal" ? .93f : kind == "elite" ? .80f : .65f;
                float maxLost = kind == "normal" ? .22f : kind == "elite" ? .40f : .75f;
                float minLost = kind == "normal" ? .02f : kind == "elite" ? .08f : .15f;
                if (res.win < minWin || res.lost > maxLost || res.lost < minLost) failures.Add(r.id + " " + kind);
            }
        Debug.Log("Expedition balance\n" + report);
        Assert.IsEmpty(failures, "outside the difficulty bands:\n" + report);
    }

    [Test]
    public void DeepRegionsFightPacksInRoomsAndTheBossOnlyInTheLair()
    {
        var pack = BattleCatalog.Build(new BattleEncounterSpec { Depth = 12, Kind = "normal", Theme = "crystal", Region = "silverwood_d4", Seed = 77 });
        Assert.IsFalse(pack.Enemies.Exists(u => u.Boss), "a room fight in the Ruins is a pack, not the lair boss");
        Assert.IsNull(pack.Commander);
        var boss = BattleCatalog.Build(new BattleEncounterSpec { Depth = 12, Kind = "boss", Theme = "crystal", Region = "silverwood_d4", Seed = 77 });
        Assert.IsNotNull(boss.Boss);
        Assert.AreEqual("Eclipse Core Golem", boss.Boss.Name);
        var lair1 = BattleCatalog.Build(new BattleEncounterSpec { Depth = 3, Kind = "boss", Region = "silverbrook_edge", Seed = 5 });
        Assert.AreEqual("Amberhide Matriarch", lair1.Boss.Name);
    }

    [Test]
    public void EncountersFollowTheirThemeAndSeed()
    {
        var spec = new BattleEncounterSpec { Depth = 6, Kind = "normal", Theme = "marsh", Region = "sunken_marsh", Seed = 1234 };
        var a = BattleCatalog.Build(spec);
        var b = BattleCatalog.Build(spec);
        CollectionAssert.AreEqual(a.Enemies.Select(u => u.Species).ToList(), b.Enemies.Select(u => u.Species).ToList(), "same seed, same fight");
        var marsh = new[] { "shardling_sprout", "glasswing_mite", "viridian_prism_warden", "moonstone_ravager" };
        for (int seed = 1; seed < 40; seed++)
        {
            var e = BattleCatalog.Build(new BattleEncounterSpec { Depth = 6, Kind = "normal", Theme = "marsh", Seed = seed });
            foreach (var u in e.Enemies) CollectionAssert.Contains(marsh, u.Species, "marsh fights draw on the marsh roster");
        }
        Assert.IsFalse(Enumerable.Range(1, 60).Any(s => BattleCatalog.Build(new BattleEncounterSpec { Depth = 2, Kind = "normal", Theme = "blight", Seed = s })
            .Enemies.Exists(u => u.Species == "eclipse_core_golem")), "the golem only appears from danger 8");
    }

    [Test]
    public void ElitesCarryAnAffixAndBossesEnterASecondPhase()
    {
        var elite = BattleCatalog.Build(new BattleEncounterSpec { Depth = 5, Kind = "elite", Theme = "ruin", Seed = 9 });
        Assert.AreEqual(1, elite.Enemies.Count(u => u.Affix.Length > 0));
        var spec = new BattleEncounterSpec { Depth = 5, Kind = "boss", Region = "moon_shrine", Seed = 3 };
        var enc = BattleCatalog.Build(spec);
        var party = Party(5);
        var b = new BattleState(3, party.Take(3), party.Skip(3), enc.Enemies, BattleCatalog.Deck(party), BattleCatalog.JD(), enc.Commander);
        int guard = 0;
        while (!b.Finished && enc.Boss.Phase == 1 && guard++ < 40)
        {
            BattleAutoPlayer.Move move;
            while (BattleAutoPlayer.Next(b, out move) && BattleAutoPlayer.Apply(b, move)) { }
            if (!b.Finished) b.EndTurn();
        }
        Assert.IsTrue(enc.Boss.Phase == 2 || !enc.Boss.Alive, "the boss reaches its second phase before it falls");
    }
}
