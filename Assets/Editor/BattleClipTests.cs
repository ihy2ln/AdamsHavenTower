using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Pre-rendered fighter clips (BattleClips.cs, Tools/produce_fighter_clips.py): manifest parsing, the completeness rule
// that keeps a unit on its rig until every everyday beat exists, card -> action mapping, contact timing, and the
// shipped sets themselves.
public sealed class BattleClipTests
{
    const string Sample = @"{""unit"":""t"",""version"":1,""fps"":12,""ppu"":160,""pages"":[""p0""],
      ""actions"":[
        {""name"":""AH_battle_guard"",""loop"":true,""hold"":false,""attack"":false,""contact"":-1,""release"":-1,""frames"":[0,0,0,10,20,5,19, 0,10,0,10,20,5,19]},
        {""name"":""AH_attack_basic"",""loop"":false,""hold"":false,""attack"":true,""contact"":8,""release"":5,""tips"":[0.1,1.5,0.2,1.5,0.3,1.5,0.4,1.5,0.5,1.5,0.9,1.6,0.9,1.6,0.9,1.6,0.9,1.6,0.6,1.5],""frames"":[0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19]},
        {""name"":""AH_skill_hook"",""attack"":true,""contact"":2,""frames"":[0,0,0,10,20,5,19,0,0,0,10,20,5,19,0,0,0,10,20,5,19]},
        {""name"":""AH_support"",""contact"":1,""frames"":[0,0,0,10,20,5,19,0,0,0,10,20,5,19]},
        {""name"":""AH_hit_react"",""frames"":[0,0,0,10,20,5,19]},
        {""name"":""AH_block"",""frames"":[0,0,0,10,20,5,19]},
        {""name"":""AH_knock_down"",""hold"":true,""frames"":[0,0,0,10,20,5,19,0,0,0,10,20,5,19]},
        {""name"":""AH_victory"",""hold"":true,""frames"":[0,0,0,10,20,5,19]}],
      ""cards"":[{""card"":""mv_counter"",""action"":""AH_support""},{""card"":""mv_hook"",""action"":""AH_skill_hook""}],
      ""cine"":{""file"":""guard_hi"",""footX"":100,""footY"":300,""ppu"":150},
      ""hi"":[{""name"":""AH_skill_hook"",""ppu"":240,""pages"":[""hi_AH_skill_hook_0""],""frames"":[0,0,0,15,30,7,28,0,0,0,15,30,7,28,0,0,0,15,30,7,28]}]}";

    static BattleCard Card(string id, BattleCardKind kind, float power) { return new BattleCard { Id = id, Kind = kind, Power = power }; }

    [Test]
    public void ManifestParsesActionsCardsAndCine()
    {
        var set = BattleClipSet.Parse("t", Sample);
        Assert.NotNull(set);
        Assert.AreEqual(12, set.Manifest.fps);
        Assert.AreEqual(2, set.Get("AH_battle_guard").Count);
        Assert.AreEqual(8, set.Get("AH_attack_basic").contact);
        Assert.AreEqual(150f, set.Manifest.cine.ppu);
        Assert.IsTrue(set.IsComplete);
    }

    [Test]
    public void IncompleteSetsStayOnTheirRigs()
    {
        Assert.IsFalse(BattleClipSet.Parse("t", Sample.Replace("AH_block", "AH_blockx")).IsComplete, "no block");
        Assert.IsFalse(BattleClipSet.Parse("t", Sample.Replace("AH_skill_hook", "AH_hook")).IsComplete, "no skill");
        Assert.IsNull(BattleClipSet.Parse("t", ""));
    }

    [Test]
    public void CardsMapToActions()
    {
        var set = BattleClipSet.Parse("t", Sample);
        Assert.AreEqual("AH_attack_basic", set.ActionFor(Card("basic_t", BattleCardKind.Attack, 1f)));
        Assert.AreEqual("AH_block", set.ActionFor(Card("guard_t", BattleCardKind.Skill, 0f)));
        Assert.AreEqual("AH_skill_hook", set.ActionFor(Card("mv_hook", BattleCardKind.Skill, 2f)), "card map");
        Assert.AreEqual("AH_support", set.ActionFor(Card("mv_counter", BattleCardKind.Skill, 0f)), "card map, support");
        Assert.AreEqual("AH_skill_hook", set.ActionFor(Card("mv_counter", BattleCardKind.Skill, 1.4f)),
            "a damaging card mapped to support (IceCounter retaliation) strikes with the first skill instead");
        Assert.AreEqual("AH_skill_hook", set.ActionFor(Card("mv_unknown", BattleCardKind.Skill, 1f)), "unmapped damage");
        Assert.AreEqual("AH_support", set.ActionFor(Card("mv_buff", BattleCardKind.Skill, 0f)), "unmapped support");
        Assert.AreEqual("AH_skill_hook", set.ActionFor(Card("ult_t", BattleCardKind.Ultimate, 3f)), "no ult finish: first skill");
        Assert.AreEqual("AH_support", set.ActionFor(Card("ult_t_b", BattleCardKind.Ultimate, 0f)));
        Assert.AreEqual("AH_support", set.ActionFor(Card("aw_t", BattleCardKind.Awakening, 0f)));
    }

    [Test]
    public void ContactSetsTheLeadOnTheBattleClock()
    {
        var set = BattleClipSet.Parse("t", Sample);
        Assert.AreEqual(8f / 12f, set.ContactTime("AH_attack_basic"), 1e-5f);
        Assert.AreEqual(.3333f, BattleClipSet.Lead(set.ContactTime("AH_attack_basic"), .5f, .3f), 1e-3f);
        Assert.AreEqual(BattleClipSet.MinLead, BattleClipSet.Lead(.05f, .5f, .3f), 1e-5f);
        Assert.AreEqual(BattleClipSet.MaxLead, BattleClipSet.Lead(4f, .5f, .3f), 1e-5f);
        Assert.AreEqual(.3f, BattleClipSet.Lead(-1f, .5f, .3f), 1e-5f, "no contact frame: the old fixed lead");
    }

    [Test]
    public void MuzzleAndHandOverPose()
    {
        var set = BattleClipSet.Parse("t", Sample);
        Vector2 tip;
        Assert.IsTrue(set.TipAt("AH_attack_basic", 5, out tip));
        Assert.AreEqual(.9f, tip.x, 1e-4f, "the release frame's muzzle");
        Assert.IsFalse(set.TipAt("AH_hit_react", 0, out tip), "no tips: effects fall back to the chest");
        Assert.AreEqual(5f / 12f, set.KeyTime("AH_attack_basic"), 1e-5f, "a thrown action hands over on its release");
        Assert.AreEqual(2f / 12f, set.KeyTime("AH_skill_hook"), 1e-5f, "a strike hands over on its contact");
        Assert.AreEqual(1, set.Manifest.hi.Length);
        Assert.AreEqual(240f, set.Manifest.hi[0].ppu);
        Assert.IsFalse(set.HiReady("AH_skill_hook"), "hi-res pages load only when staged");
    }

    // moves.json: every party card resolves to its own layers or its fighter's default, and the art exists.
    [Test]
    public void EveryPartyCardHasEffectLayers()
    {
        var text = Resources.Load<TextAsset>("AdamsHaven/Fx/Moves/moves");
        Assert.NotNull(text, "moves.json");
        var file = JsonUtility.FromJson<MoveFile>(text.text);
        Assert.Greater(file.moves.Length, 0);
        foreach (var unit in BattleCatalog.Party())
        {
            var cards = new List<BattleCard>(BattleCatalog.FighterKit(unit));
            cards.AddRange(BattleCatalog.Ultimates(unit));
            cards.Add(BattleCatalog.Awakening(unit));
            foreach (var card in cards)
            {
                var def = BattleMode.MoveDefFor(card, unit);
                bool ranged = card.Magic || unit.Role == BattleRole.Ranger || unit.Role == BattleRole.Support;
                if (ranged && card.Power > 0) Assert.NotNull(def, unit.Id + " " + card.Id + " would fall back to the generic orb");
                if (def == null) continue;
                foreach (var layer in new[] { def.charge, def.travel, def.impact })
                {
                    if (layer == null || !layer.Present) continue;
                    if (layer.video) Assert.NotNull(Resources.Load<UnityEngine.Video.VideoClip>("AdamsHaven/Fx/Moves/" + layer.sheet), layer.sheet);
                    else
                    {
                        var sheet = Resources.Load<Texture2D>("AdamsHaven/Fx/Moves/" + layer.sheet);
                        Assert.NotNull(sheet, layer.sheet);
                        Assert.LessOrEqual(Mathf.Max(sheet.width, sheet.height), 2048, layer.sheet);
                    }
                }
            }
        }
    }

    [Test]
    public void FramesLoopHoldOrClamp()
    {
        var set = BattleClipSet.Parse("t", Sample);
        Assert.AreEqual(1, set.FrameAt("AH_battle_guard", 1f / 12f + .001f));
        Assert.AreEqual(0, set.FrameAt("AH_battle_guard", 2f / 12f + .001f), "loops");
        Assert.AreEqual(1, set.FrameAt("AH_knock_down", 9f), "holds the last frame");
        Assert.AreEqual(-1, set.FrameAt("AH_nothing", 0f));
    }

    // Every set in Resources: pages inside the Android cap, frame rects on their pages, contact inside the clip,
    // and every mapped card a real card of that fighter.
    [Test]
    public void ShippedSetsAreConsistent()
    {
        var party = BattleCatalog.Party();
        int checkedSets = 0;
        foreach (var text in Resources.LoadAll<TextAsset>("AdamsHaven/BattleClips"))
        {
            if (text.name != "clips") continue;
            var probe = JsonUtility.FromJson<BattleClipManifest>(text.text);
            var set = BattleClipSet.Parse(probe.unit, text.text);
            Assert.NotNull(set, probe.unit);
            checkedSets++;
            var pages = set.Manifest.pages.Select(p => Resources.Load<Texture2D>(BattleClipSet.Root + probe.unit + "/" + p)).ToArray();
            foreach (var page in pages)
            {
                Assert.NotNull(page, probe.unit + " page");
                Assert.LessOrEqual(Mathf.Max(page.width, page.height), 2048, probe.unit + " page over the Android cap");
            }
            foreach (var a in set.Manifest.actions)
            {
                Assert.Greater(a.Count, 0, a.name);
                Assert.Less(a.contact, a.Count, a.name + " contact");
                for (int i = 0; i < a.Count; i++)
                {
                    int k = i * 7;
                    var page = pages[a.frames[k]];
                    Assert.LessOrEqual(a.frames[k + 1] + a.frames[k + 3], page.width, a.name + " frame " + i);
                    Assert.LessOrEqual(a.frames[k + 2] + a.frames[k + 4], page.height, a.name + " frame " + i);
                }
            }
            var unit = party.FirstOrDefault(u => u.Id == probe.unit);
            Assert.NotNull(unit, probe.unit + " is not a party fighter");
            var ids = new HashSet<string>(BattleCatalog.FighterKit(unit).Select(c => c.Id));
            ids.UnionWith(BattleCatalog.Ultimates(unit).Select(c => c.Id));
            ids.Add(BattleCatalog.Awakening(unit).Id);
            ids.Add(BattleCatalog.Basic(unit).Id);
            ids.Add(BattleCatalog.Guard(unit).Id);
            foreach (var c in set.Manifest.cards)
            {
                Assert.IsTrue(ids.Contains(c.card), probe.unit + ": " + c.card + " is not one of its cards");
                Assert.IsTrue(set.Has(c.action), c.card + " -> missing action " + c.action);
            }
            Assert.NotNull(set.HighGuard, probe.unit + " guard_hi");
        }
        if (checkedSets == 0) Assert.Ignore("no clip sets shipped yet");
    }
}
