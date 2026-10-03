using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// BM 10.3.0: the Chaos Zero Nightmare layer of the battle rules (hand flow, action counts, tenacity and BREAK, Guard
// shields, reserve partners, epiphanies) and the media library's file readers.
public sealed class BattleCznTests
{
    private static BattleState Fight(int seed = 11, List<BattleUnit> enemies = null)
    {
        var party = BattleCatalog.Party();
        if (enemies == null)
            enemies = BattleCatalog.Build(new BattleEncounterSpec { Depth = 3, Kind = "normal", Theme = "briar", Seed = seed }).Enemies;
        return new BattleState(seed, party.Take(3), party.Skip(3), enemies, BattleCatalog.Deck(party), BattleCatalog.JD());
    }

    private static BattleUnit Dummy(string id, int hp = 999, float speed = 10f)
    {
        return new BattleUnit { Id = id, Name = id, Species = id, Enemy = true, MaxHp = hp, Hp = hp, Attack = 10, Magic = 10,
            Defense = 5, Resistance = 5, Speed = speed, Role = BattleRole.Dps };
    }

    [Test]
    public void TheHandRefillsToFiveAndUnretainedCardsAreDiscarded()
    {
        var b = Fight();
        Assert.AreEqual(BattleState.HandSize, b.Hand.Count(c => c.Kind != BattleCardKind.Summoner), "five fighter cards to start");
        foreach (var id in new[] { "mv_frost_jab", "gh_tiger_cleave", "el_forest_scout" })
            Assert.IsTrue(b.Hand.Exists(c => c.Id == id), id + " is an Initiation card and opens in hand");
        var kept = b.Hand.Where(c => c.Retain).ToList();
        b.EndTurn();
        if (b.Finished) return;
        foreach (var c in kept) Assert.Contains(c, b.Hand, c.Name + " retains");
        Assert.AreEqual(BattleState.HandSize, b.Hand.Count(c => c.Kind != BattleCardKind.Summoner), "refilled next round");
    }

    [Test]
    public void EveryUnplayedCardIsThrownOutAtEndTurnUnlessItRetains()
    {
        var b = Fight(31);
        var jd = b.DrawPile.First(c => c.Kind == BattleCardKind.Summoner);
        b.DrawPile.Remove(jd); b.Hand.Add(jd);
        Assert.Greater(b.DrawPile.Count, 15, "no reshuffle can bring a thrown card straight back");
        var thrown = b.Hand.Where(c => !c.Retain).ToList();
        var kept = b.Hand.Where(c => c.Retain).ToList();
        b.EndTurn();
        if (b.Finished) return;
        foreach (var c in thrown) Assert.Contains(c, b.Discard, c.Name + " was not played, so it is discarded");
        Assert.Contains(jd, b.Discard, "JD's cards are thrown out too");
        foreach (var c in kept) Assert.Contains(c, b.Hand, c.Name + " retains");
    }

    [Test]
    public void ExhaustedCardsLeaveTheBattle()
    {
        var b = Fight();
        var smite = BattleCatalog.FighterKit(BattleCatalog.Party().Find(u => u.Id == "ghislaine")).Find(c => c.Id == "gh_reckless_swing");
        Assert.IsTrue(smite.Exhaust);
        var card = b.DrawPile.Concat(b.Discard).Concat(b.Hand).First(c => c.Id == "gh_reckless_swing");
        b.DrawPile.Remove(card); b.Discard.Remove(card); if (!b.Hand.Contains(card)) b.Hand.Add(card);
        var actor = b.OwnerOf(card); actor.Ep = 3; actor.Ap = 1;
        Assert.IsTrue(b.TryPlay(card, b.Enemies.First(e => b.IsTarget(card, actor, e))));
        Assert.Contains(card, b.Exhausted);
        Assert.IsFalse(b.Discard.Contains(card) || b.DrawPile.Contains(card));
    }

    [Test]
    public void AnEnemyWhoseCountRunsOutActsMidTurnOnlyOnce()
    {
        var b = Fight(enemies: BattleCatalog.Build(new BattleEncounterSpec { Depth = 3, Kind = "normal", Theme = "briar", Seed = 5 }).Enemies.Take(1).ToList());
        var foe = b.Enemies[0];
        foe.Speed = 15f; foe.MaxHp = foe.Hp = 99999;
        b.EndTurn();                     // next round recomputes its count from the new speed
        Assert.AreEqual(3, foe.ActionMax, "fast enemies act after three cards");
        int usesBefore = b.Log.Count(l => l.StartsWith(foe.Name + " uses"));
        for (int i = 0; i < 3 && !b.Finished; i++)
        {
            var ally = b.Allies[i];
            ally.Ap = 1;
            Assert.IsTrue(b.TryPlay(BattleCatalog.Guard(ally), ally), "guard is always playable with AP");
        }
        Assert.IsTrue(foe.ActedThisRound, "the third card ran its count out");
        Assert.AreEqual(1, b.Interrupts);
        Assert.IsTrue(b.Facts.Exists(f => f.Kind == "interrupt"));
        int usesMid = b.Log.Count(l => l.StartsWith(foe.Name + " uses"));
        Assert.AreEqual(usesBefore + 1, usesMid, "it acted once, mid-turn");
        b.EndTurn();
        if (b.Log.Count >= 80) return;   // the log is capped; counting would be unreliable
        Assert.AreEqual(usesMid, b.Log.Count(l => l.StartsWith(foe.Name + " uses")), "and not again at the end of the turn");
    }

    [Test]
    public void WearingDownTenacityBreaksAnEnemy()
    {
        var target = Dummy("wall", 5000);
        var b = Fight(enemies: new List<BattleUnit> { target });
        Assert.AreEqual(BattleState.Tenacity[0], target.MaxTenacity);
        var hitter = b.Allies[0];
        for (int i = 0; i < target.MaxTenacity && !target.HasStatus("Broken"); i++)
        {
            hitter.Ap = 1;
            b.TryPlay(BattleCatalog.Basic(hitter), target);
        }
        Assert.IsTrue(target.HasStatus("Broken"), "one basic hit per tenacity point breaks it");
        Assert.IsTrue(b.Facts.Exists(f => f.Kind == "break"));
        Assert.AreEqual(1, hitter.Ap, "the breaker gets an action back");
        float element;
        var basic = BattleCatalog.Basic(hitter);
        target.Statuses.RemoveAll(s => s.Name == "Broken");
        int normal = b.PreviewDamage(basic, hitter, target, out element);
        target.ApplyStatus("Broken", .3f, 1);
        Assert.Greater(b.PreviewDamage(basic, hitter, target, out element), normal, "broken enemies take more");
    }

    [Test]
    public void AFullyBlockedHitCostsNoHealthAndNoStress()
    {
        var b = Fight(21);
        var tank = b.Allies[0];
        tank.ApplyStatus("Shield", 500f, 1);
        tank.ApplyStatus("Shield", 100f, 1);
        Assert.AreEqual(600f, tank.StatusValue("Shield"), 0.01f, "shields stack");
        foreach (var u in b.Allies) { if (u != tank) u.ApplyStatus("Shield", 9999f, 1); }
        b.Summoner.ApplyStatus("Shield", 9999f, 1);
        var stress = b.Allies.Select(u => u.Stress).ToList();
        int serial = b.FactSerial;
        b.EndTurn();
        int fresh = b.FactSerial - serial;
        var facts = b.Facts.Skip(Mathf.Max(0, b.Facts.Count - fresh)).ToList();
        Assert.IsFalse(facts.Exists(f => f.Kind == "damage" && !f.Target.Enemy && f.Amount > 0), "every hit on the party was absorbed");
        CollectionAssert.AreEqual(stress, b.Allies.Select(u => u.Stress).ToList(), "blocked hits add no stress");
    }

    [Test]
    public void APartnerAssistsOncePerBattleForTwoSp()
    {
        var b = Fight(enemies: new List<BattleUnit> { Dummy("a", 5000), Dummy("b", 5000) });
        var reserve = b.Reserves[0];
        Assert.AreSame(b.Allies[0], b.PartneredBy(reserve));
        Assert.AreEqual(BattleState.PartnerPassive, b.PartnerBonus(b.Allies[0]), 1e-4f);
        b.Sp = BattleState.PartnerSpCost;
        var move = b.PartnerMove(reserve);
        BattleUnit target = move.Target == BattleTarget.Enemy ? b.Enemies.First(e => b.Exposed(e)) : move.Target == BattleTarget.Ally ? b.Allies[1] : null;
        Assert.IsTrue(b.TryPartnerAssist(reserve, target));
        Assert.AreEqual(0, b.Sp);
        Assert.IsTrue(reserve.PartnerUsed);
        b.Sp = 10;
        Assert.IsFalse(b.CanPartnerAssist(reserve), "once per battle");
    }

    [Test]
    public void AnEpiphanyUpgradesEveryCopyAndIsRecorded()
    {
        var b = Fight();
        var card = b.Hand.First(c => c.Kind != BattleCardKind.Summoner && c.Power > 0 && c.Target == BattleTarget.Enemy && b.OwnerOf(c) != null);
        b.GlowCard = card.Id;
        var actor = b.OwnerOf(card); actor.Ap = 2; actor.Ep = 5;
        Assert.IsTrue(b.TryPlay(card, b.Enemies.First(e => b.IsTarget(card, actor, e))));
        if (b.Finished) return;
        Assert.AreSame(card, b.EpiphanyCard);
        Assert.AreEqual(3, b.EpiphanyOptions.Count);
        string mod = b.EpiphanyOptions[0];
        Assert.IsTrue(b.ChooseEpiphany(0));
        Assert.IsNull(b.EpiphanyCard);
        Assert.AreEqual(mod, b.Epiphanies[card.Id]);
        StringAssert.Contains(mod, card.Epiphany);
        Assert.AreEqual("", b.GlowCard, "one epiphany per battle");
    }

    [Test]
    public void EpiphaniesChangeTheCard()
    {
        var c = new BattleCard { Id = "x", Ep = 2, Power = 1f, Target = BattleTarget.Enemy };
        BattleCatalog.ApplyEpiphany(c, "swift"); Assert.AreEqual(1, c.Ep);
        BattleCatalog.ApplyEpiphany(c, "sweeping"); Assert.AreEqual(BattleTarget.AllEnemies, c.Target); Assert.AreEqual(.7f, c.Power, 1e-4f);
        BattleCatalog.ApplyEpiphany(c, "steadfast"); Assert.IsTrue(c.Retain);
        BattleCatalog.ApplyEpiphany(c, "bulwark"); Assert.Greater(c.SelfShield, 0f);
        Assert.AreEqual("swift,sweeping,steadfast,bulwark", c.Epiphany);
    }

    [Test]
    public void TheAutoPlayerFinishesFightsUnderTheNewRules()
    {
        int wins = 0;
        for (int i = 0; i < 20; i++)
        {
            var b = Fight(100 + i);
            BattleAutoPlayer.PlayOut(b);
            Assert.IsTrue(b.Finished, "fight " + i + " ended");
            if (b.Victory) wins++;
        }
        Assert.GreaterOrEqual(wins, 16, "a depth-3 pack is still a fight the party wins");
    }

    [Test]
    public void MapModsChangeTheEncounter()
    {
        var plain = BattleCatalog.Build(new BattleEncounterSpec { Depth = 4, Kind = "normal", Theme = "marsh", Seed = 9 });
        var modded = BattleCatalog.Build(new BattleEncounterSpec { Depth = 4, Kind = "normal", Theme = "marsh", Seed = 9, Offense = 1.25f, Vitality = 1.5f, ExtraFoes = 1, Affix = "Hasted" });
        Assert.AreEqual(Mathf.Min(BattleState.EnemyMax, plain.Enemies.Count + 1), modded.Enemies.Count);
        Assert.Greater(modded.Enemies[0].MaxHp, plain.Enemies[0].MaxHp);
        Assert.AreEqual(1, modded.Enemies.Count(e => e.Affix == "Hasted"));
    }

    // ---- media library readers -----------------------------------------------------------------------------------

    [Test]
    public void MediaJsonReadsGltfShapes()
    {
        var root = MediaJson.Parse("{\"a\":[1,2.5,-3e2],\"b\":{\"c\":\"x\\\"y\",\"d\":true,\"e\":null}}");
        CollectionAssert.AreEqual(new[] { 1f, 2.5f, -300f }, MediaJson.Floats(root, "a"));
        Assert.AreEqual("x\"y", MediaJson.Str(MediaJson.Obj(root, "b"), "c"));
        Assert.AreEqual(-1, MediaJson.Int(MediaJson.Obj(root, "b"), "missing"));
    }

    [Test]
    public void MediaGltfLoadsATriangle()
    {
        // A one-triangle .gltf with its buffer as a data URI.
        var data = new List<byte>();
        foreach (var f in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) data.AddRange(System.BitConverter.GetBytes(f));
        foreach (var i in new ushort[] { 0, 1, 2 }) data.AddRange(System.BitConverter.GetBytes(i));
        data.AddRange(new byte[2]);
        string b64 = System.Convert.ToBase64String(data.ToArray());
        string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0,\"translation\":[0,0,2]}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
            "\"buffers\":[{\"byteLength\":44,\"uri\":\"data:application/octet-stream;base64," + b64 + "\"}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
        string path = Path.Combine(Application.temporaryCachePath, "ahcg_triangle.gltf");
        File.WriteAllText(path, json);
        string error;
        var go = MediaGltf.Load(path, out error);
        Assert.IsNull(error);
        Assert.IsNotNull(go);
        var filter = go.GetComponentInChildren<MeshFilter>(true);
        Assert.AreEqual(3, filter.sharedMesh.vertexCount);
        Assert.AreEqual(-2f, filter.transform.parent.localPosition.z, 1e-4f, "glTF +Z becomes Unity -Z");
        Object.DestroyImmediate(go);
        File.Delete(path);
    }

    [Test]
    public void MediaSlotsAcceptTheRightKinds()
    {
        CollectionAssert.AreEqual(new[] { MediaLibrary.Image }, MediaLibrary.Accepts("card.mv_frost_jab.art"));
        CollectionAssert.AreEquivalent(new[] { MediaLibrary.Image, MediaLibrary.Video }, MediaLibrary.Accepts("card.mv_frost_jab.fx"));
        CollectionAssert.AreEqual(new[] { MediaLibrary.Audio }, MediaLibrary.Accepts("sfx.hit"));
        CollectionAssert.AreEquivalent(new[] { MediaLibrary.Image, MediaLibrary.Model }, MediaLibrary.Accepts("unit.kaela.model"));
        Assert.AreEqual(MediaLibrary.Model, MediaLibrary.KindOf("x/Hero.GLB"));
        Assert.AreEqual("", MediaLibrary.KindOf("notes.txt"));
    }

    [Test]
    public void SpeedLabelsReadFromTheSlowerBase()
    {
        Assert.AreEqual("1x", BattleMode.SpeedLabel(.5f));
        Assert.AreEqual("2x", BattleMode.SpeedLabel(1f));
        Assert.AreEqual("4x", BattleMode.SpeedLabel(2f));
    }
}
