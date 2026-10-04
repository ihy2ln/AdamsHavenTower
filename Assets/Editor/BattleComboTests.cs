using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// CM 10.3.4 (BattleCombos.cs): every bonded pair has a combo action (a team-up is followed by the partner's combo) and
// a joint ultimate (both meters full: spends both, one ultimate's SP). Both battle kinds.
public sealed class BattleComboTests
{
    static BattleState Battle(bool summon, int seed = 7)
    {
        var party = BattleCatalog.Party();
        var enemies = BattleCatalog.Build(new BattleEncounterSpec { Depth = 3, Kind = "normal", Theme = "briar", Seed = seed }).Enemies;
        return new BattleState(seed, party.Take(3), party.Skip(3), enemies, BattleCatalog.Deck(party), BattleCatalog.JD(), null, null, null, summon);
    }

    static BattleUnit Ally(BattleState b, string id) { return b.Allies.First(u => u.Id == id); }
    static BattleCard Kit(BattleUnit u, string id) { return BattleCatalog.FighterKit(u).First(c => c.Id == id); }

    [Test]
    public void EveryBondedPairHasAComboAndAJointUltimate()
    {
        var party = BattleCatalog.Party();
        var pairs = new HashSet<string>();
        foreach (var u in party)
            foreach (var p in BattleCatalog.FighterKit(u)[0].Partners)
                pairs.Add(BattleState.BondKey(u.Id, p));
        Assert.AreEqual(7, pairs.Count);
        Assert.AreEqual(7, BattleCombos.PairKeys().Count());
        foreach (string key in pairs)
        {
            string[] ab = key.Split(':');
            var a = party.First(u => u.Id == ab[0]);
            var b = party.First(u => u.Id == ab[1]);
            foreach (var duo in new[] { new[] { a, b }, new[] { b, a } })
            {
                BattleUnit actor = duo[0], partner = duo[1];
                var combo = BattleCombos.Action(actor, partner);
                Assert.IsNotNull(combo, key);
                Assert.AreEqual(partner.Id, combo.Owner, "the partner performs the combo");
                string look = BattleCombos.LookFor(combo);
                Assert.IsNotNull(look, key + ": " + partner.Id + " has a look");
                Assert.IsTrue(BattleCatalog.FighterKit(partner).Any(c => c.Id == look), look + " is " + partner.Id + "'s own move");
                Assert.IsNotNull(BattleMode.MoveDefFor(combo, partner), combo.Id + " borrows staged layers");
            }
            var joint = BattleCombos.Joint(a.Id, b.Id);
            Assert.IsNotNull(joint, key);
            Assert.AreEqual(BattleCardKind.Ultimate, joint.Kind);
            Assert.IsTrue(joint.Owner == a.Id || joint.Owner == b.Id);
            Assert.AreEqual(joint.Owner == a.Id ? b.Id : a.Id, BattleCombos.JointPartner(joint));
            Assert.IsTrue(joint.Power > 0 || joint.Heal > 0);
        }
        Assert.IsNull(BattleCombos.Action(party.First(u => u.Id == "kaela"), party.First(u => u.Id == "daisy")), "no combo without a bond");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ATeamUpIsFollowedByThePartnersCombo(bool summon)
    {
        var b = Battle(summon);
        var kaela = Ally(b, "kaela");
        var ghislaine = Ally(b, "ghislaine");
        kaela.Ap = ghislaine.Ap = 3; kaela.Ep = ghislaine.Ep = 3;
        b.Summoner.Ap = 9; b.Summoner.Ep = 9;          // a summon battle pays from JD's pool
        var jab = Kit(kaela, "mv_frost_jab"); b.Hand.Add(jab);
        var foe = b.Enemies.First(e => b.Exposed(e));
        Assert.IsTrue(b.TryPlay(jab, foe));
        Assert.IsNull(b.LastCombo, "nobody bonded acted before Kaela");
        var cleave = Kit(ghislaine, "gh_tiger_cleave"); b.Hand.Add(cleave);
        foe = b.Enemies.FirstOrDefault(e => b.Exposed(e));
        if (foe == null) Assert.Inconclusive("the jab ended the fight");
        int facts = b.Facts.Count;
        Assert.IsTrue(b.TryPlay(cleave, foe));
        Assert.AreSame(kaela, b.LastTeamUp);
        Assert.IsNotNull(b.LastCombo, "the team-up brings the pair's combo");
        Assert.AreEqual("Frostfang Rend", b.LastCombo.Name);
        var fresh = b.Facts.Skip(facts).ToList();
        int mark = fresh.FindIndex(f => f.Kind == "combo");
        Assert.GreaterOrEqual(mark, 0);
        Assert.AreSame(kaela, fresh[mark].Actor, "Kaela performs it");
        Assert.IsTrue(fresh.Skip(mark + 1).Any(f => f.Actor == kaela && f.Card == b.LastCombo && (f.Kind == "damage" || f.Kind == "blocked")), "the combo hits");
        Assert.IsTrue(fresh.Take(mark).Any(f => f.Actor == ghislaine && f.Card == cleave), "after Ghislaine's own card");
    }

    [Test]
    public void UltimatesAreNotFollowedByACombo()
    {
        var b = Battle(false);
        var kaela = Ally(b, "kaela");
        var ghislaine = Ally(b, "ghislaine");
        kaela.Ap = 3; kaela.Ep = 3;
        var jab = Kit(kaela, "mv_frost_jab"); b.Hand.Add(jab);
        b.TryPlay(jab, b.Enemies.First(e => b.Exposed(e)));
        ghislaine.Ultimate = 100; b.Sp = BattleState.UltimateSpCost;
        var foe = b.Enemies.FirstOrDefault(e => b.Exposed(e));
        if (foe == null) Assert.Inconclusive("the jab ended the fight");
        Assert.IsTrue(b.TryUltimate(ghislaine, 0, foe));
        Assert.IsNull(b.LastCombo);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AJointUltimateNeedsBothMetersAndSpendsBoth(bool summon)
    {
        var b = Battle(summon);
        var kaela = Ally(b, "kaela");
        var ghislaine = Ally(b, "ghislaine");
        var joint = BattleCombos.Joint("kaela", "ghislaine");
        b.Sp = BattleState.UltimateSpCost + 1;
        ghislaine.Ultimate = 100; kaela.Ultimate = 60;
        Assert.IsFalse(b.CanJoint(joint), "Kaela's meter is not full");
        Assert.IsEmpty(b.JointUltimates(ghislaine));
        kaela.Ultimate = 100;
        Assert.IsTrue(b.CanJoint(joint));
        Assert.AreEqual(1, b.JointUltimates(kaela).Count, "offered from either fighter");
        Assert.AreEqual(1, b.JointUltimates(ghislaine).Count);
        var foe = b.Enemies.First(e => b.Exposed(e));
        int hp = foe.Hp, sp = b.Sp, facts = b.Facts.Count;
        Assert.IsTrue(b.TryJointUltimate(joint, foe));
        Assert.AreEqual(0, kaela.Ultimate);
        Assert.AreEqual(0, ghislaine.Ultimate);
        Assert.AreEqual(sp - b.UltCost(ghislaine), b.Sp, "one ultimate's SP");
        Assert.Less(foe.Hp, hp);
        Assert.IsTrue(b.Facts.Skip(facts).Any(f => f.Kind == "joint" && f.Actor == ghislaine && f.Target == kaela));
        Assert.IsFalse(b.CanJoint(joint));
    }

    [Test]
    public void AJointUltimateHitsHarderThanTheLeadsSoloUltimate()
    {
        var solo = Battle(false, 21);
        var joint = Battle(false, 21);
        var g1 = Ally(solo, "ghislaine"); g1.Ultimate = 100; solo.Sp = BattleState.UltimateSpCost;
        var f1 = solo.Enemies.First(e => solo.Exposed(e)); f1.MaxHp = f1.Hp = 9999; int before1 = f1.Hp;   // no overkill
        solo.TryUltimate(g1, 0, f1);
        foreach (var u in joint.Allies) u.Ultimate = 100;
        joint.Sp = BattleState.UltimateSpCost;
        var f2 = joint.Enemies.First(e => joint.Exposed(e)); f2.MaxHp = f2.Hp = 9999; int before2 = f2.Hp;
        Assert.IsTrue(joint.TryJointUltimate(BattleCombos.Joint("ghislaine", "kaela"), f2));
        Assert.Greater(before2 - f2.Hp, before1 - f1.Hp);
    }

    // ---- the field ----------------------------------------------------------------------------------------

    GameObject host;
    int savedCine;
    [SetUp] public void Up() { savedCine = PlayerPrefs.GetInt(BattleMode.CinematicKey, (int)BattleMode.CinematicMode.FirstUse); }
    [TearDown]
    public void Down()
    {
        PlayerPrefs.SetInt(BattleMode.CinematicKey, savedCine);
        if (host != null) Object.DestroyImmediate(host);
    }

    BattleMode Mode(bool summon)
    {
        // The battle is a runtime component: in edit mode its texture trims log Destroy errors that do not matter here.
        LogAssert.ignoreFailingMessages = true;
        BattleMode.Cinematics = BattleMode.CinematicMode.UltimatesOnly;
        host = new GameObject("Combo test battle");
        var mode = host.AddComponent<BattleMode>();
        mode.enabled = false;
        mode.SummonMode = summon;
        mode.Seed = 11;
        mode.Begin(0, (win, reward) => { });
        for (int i = 0; i < 600 && mode.DebugFx < mode.DebugQueueEnd + .3f; i++) mode.DebugAdvance(1f / 30f);
        return mode;
    }

    [Test]
    public void ASummonedJointUltimateCallsOutBothFightersBeforeTheCut()
    {
        var m = Mode(true);
        var ids = m.DebugState.Allies.Select(u => u.Id).ToList();
        string a = null, b = null;
        foreach (string x in ids)
            foreach (string y in ids)
                if (a == null && x != y && BattleCombos.Joint(x, y) != null) { a = x; b = y; }
        if (a == null) Assert.Inconclusive("no bonded pair on the field");
        Assert.IsTrue(m.DebugJoint(a, b), "the joint ultimate plays");
        Vector3 beats = m.DebugCineBeats;
        Assert.Greater(beats.y, 0f);
        while (m.DebugFx < beats.y - .01f) m.DebugAdvance(1f / 60f);
        Assert.AreEqual(1f, m.DebugSummonAlpha(a), .01f, a + " is out at the cut");
        Assert.AreEqual(1f, m.DebugSummonAlpha(b), .01f, b + " is out at the cut");
        while (m.DebugFx < beats.z + .05f) m.DebugAdvance(1f / 60f);
        Assert.AreEqual(1f, m.DebugSummonAlpha(a), .01f);
        Assert.AreEqual(1f, m.DebugSummonAlpha(b), .01f, "the partner is still beside the lead on the hand-back");
        while (m.DebugFx < m.DebugQueueEnd + 1f) m.DebugAdvance(1f / 30f);
        Assert.AreEqual(0f, m.DebugSummonAlpha(a), .01f);
        Assert.AreEqual(0f, m.DebugSummonAlpha(b), .01f, "both go back to their contracts");
    }
}
