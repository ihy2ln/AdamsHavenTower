using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// Summon battles (BattleState.SummonMode, expeditions): fighters are JD's contracts. An anchoring card makes its
// fighter the Vanguard (one at a time) who takes the enemy hits; with no Vanguard JD is hit; bond partners acting in
// the same turn team up once each.
public sealed class SummonBattleTests
{
    private static BattleState Battle(bool summon, int seed = 7)
    {
        var party = BattleCatalog.Party();
        var enemies = BattleCatalog.Build(new BattleEncounterSpec { Depth = 3, Kind = "normal", Theme = "briar", Seed = seed }).Enemies;
        return new BattleState(seed, party.Take(3), party.Skip(3), enemies, BattleCatalog.Deck(party), BattleCatalog.JD(), null, null, null, summon);
    }

    private static BattleUnit Ally(BattleState b, string id) { return b.Allies.First(u => u.Id == id); }

    [Test]
    public void WithNoVanguardTheEnemiesAimAtJd()
    {
        var b = Battle(true);
        Assert.IsTrue(b.SummonMode);
        Assert.IsNull(b.Vanguard);
        foreach (var kv in b.IntentTargets)
            if (b.Intents[kv.Key].Target == BattleTarget.Enemy) Assert.AreSame(b.Summoner, kv.Value, kv.Key);
        foreach (var ally in b.Allies) Assert.IsFalse(b.Exposed(ally), ally.Id + " is in the contract, not on the field");
        Assert.Greater(b.Summoner.MaxHp, BattleCatalog.JD().MaxHp, "JD is sturdier in a summon battle");
    }

    [Test]
    public void AnAnchoringCardMakesTheVanguardWhoTakesTheHits()
    {
        var b = Battle(true);
        var kaela = Ally(b, "kaela");
        Assert.IsTrue(b.TryPlay(BattleCatalog.Guard(kaela), kaela));
        Assert.AreSame(kaela, b.Vanguard);
        Assert.IsTrue(b.Exposed(kaela));
        foreach (var kv in b.IntentTargets)
            if (b.Intents[kv.Key].Target == BattleTarget.Enemy) Assert.AreSame(kaela, kv.Value, kv.Key + " retargets the Vanguard");
        // A plain attack does not anchor.
        var elara = Ally(b, "elara");
        var bolt = b.Hand.FirstOrDefault(c => c.Owner == "elara" && c.EffectivePower > 0 && !BattleState.Anchors(c)) ?? BattleCatalog.Basic(elara);
        var foe = b.Enemies.First(e => b.Exposed(e));
        elara.Ap = 3; elara.Ep = 3;
        Assert.IsTrue(b.TryPlay(bolt, bolt.Target == BattleTarget.Enemy ? foe : null));
        Assert.AreSame(kaela, b.Vanguard, "the Vanguard holds while others act");
    }

    [Test]
    public void OnlyOneVanguardAndTheHoldRunsOut()
    {
        var b = Battle(true);
        var kaela = Ally(b, "kaela"); var ghislaine = Ally(b, "ghislaine");
        b.TryPlay(BattleCatalog.Guard(kaela), kaela);
        b.TryPlay(BattleCatalog.Guard(ghislaine), ghislaine);
        Assert.AreSame(ghislaine, b.Vanguard, "a new anchor replaces the Vanguard");
        Assert.IsFalse(b.Exposed(kaela));
        b.EndTurn();
        if (b.Finished) return;
        // Guard's shield lasts the enemy turn only: next round the Vanguard returns to the contract.
        Assert.IsTrue(b.Vanguard == null || ghislaine.HasStatus("Shield") || ghislaine.HasStatus("Taunt") || ghislaine.HasStatus("DefenseUp"));
    }

    [Test]
    public void BondPartnersActingInTheSameTurnTeamUpOnce()
    {
        var b = Battle(true);
        var kaela = Ally(b, "kaela"); var ghislaine = Ally(b, "ghislaine");
        Assert.Contains("kaela", BattleCatalog.FighterKit(ghislaine)[0].Partners);
        var foe = b.Enemies.First(e => b.Exposed(e));
        b.TryPlay(BattleCatalog.Guard(kaela), kaela);
        Assert.IsNull(b.LastTeamUp, "nobody bonded acted before Kaela");
        var cleave = BattleCatalog.FighterKit(ghislaine).First(c => c.Id == "gh_tiger_cleave");
        b.Hand.Add(cleave);
        ghislaine.Ap = 3; ghislaine.Ep = 3;
        Assert.IsTrue(b.TryPlay(cleave, foe));
        Assert.AreSame(kaela, b.LastTeamUp, "Ghislaine teams up with her bonded Vanguard");
        var again = BattleCatalog.FighterKit(ghislaine).First(c => c.Id == "gh_ember_chain");
        b.Hand.Add(again);
        foe = b.Enemies.FirstOrDefault(e => b.Exposed(e));
        if (foe == null) return;
        Assert.IsTrue(b.TryPlay(again, foe));
        Assert.IsNull(b.LastTeamUp, "a partner joins one team-up per turn");
    }

    [Test]
    public void ApAndEpRunOffJdsPool()
    {
        var b = Battle(true);
        var jd = b.Summoner;
        int ap = b.Allies.Where(u => u.Alive).Sum(u => u.MaxAp), ep = b.Allies.Where(u => u.Alive).Sum(u => u.MaxEp);
        Assert.AreEqual(ap, jd.MaxAp, "JD's AP pool is the contract's AP");
        Assert.AreEqual(ep, jd.MaxEp, "JD's EP pool is the contract's EP");
        var kaela = Ally(b, "kaela");
        int kaelaAp = kaela.Ap, kaelaEp = kaela.Ep;
        var guard = BattleCatalog.Guard(kaela);
        Assert.IsTrue(b.TryPlay(guard, kaela));
        Assert.AreEqual(ap - guard.Ap, jd.Ap, "the card was paid from JD's pool");
        Assert.AreEqual(kaelaAp, kaela.Ap, "the fighter's own AP is untouched");
        Assert.AreEqual(kaelaEp, kaela.Ep);
        // One fighter can be called again while the pool lasts.
        Assert.IsTrue(b.CanPay(BattleCatalog.Basic(kaela), kaela));
        jd.Ap = 0;
        Assert.IsFalse(b.CanPay(BattleCatalog.Basic(kaela), kaela), "an empty pool stops every fighter");
    }

    [Test]
    public void ClassicBattlesAreUnchanged()
    {
        var b = Battle(false);
        Assert.IsFalse(b.SummonMode);
        var kaela = Ally(b, "kaela");
        b.TryPlay(BattleCatalog.Guard(kaela), kaela);
        Assert.IsNull(b.Vanguard);
        Assert.AreEqual(BattleCatalog.JD().MaxHp, b.Summoner.MaxHp);
        Assert.IsTrue(b.Allies.Any(u => b.Exposed(u)), "the party stands on the field");
    }
}
