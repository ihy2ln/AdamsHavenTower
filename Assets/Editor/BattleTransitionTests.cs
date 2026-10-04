using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// CM 10.3.3 (BattleTransitions.cs): the way into and out of an ultimate's video. In a summon battle the fighter is
// called out and fully formed before the camera pushes into her, she stays out through the video (no second summon
// on the hand-back), the camera is pushed in at both cuts, and she dissolves only after the camera has pulled back.
public sealed class BattleTransitionTests
{
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
        host = new GameObject("Transition test battle");
        var mode = host.AddComponent<BattleMode>();
        mode.enabled = false;
        mode.SummonMode = summon;
        mode.Seed = 11;
        mode.Begin(0, (win, reward) => { });
        // The opening beats (round banner, draws) play out before a card can be played.
        for (int i = 0; i < 600 && mode.DebugFx < mode.DebugQueueEnd + .3f; i++) mode.DebugAdvance(1f / 30f);
        return mode;
    }

    static void RunTo(BattleMode m, float fx) { while (m.DebugFx < fx - .001f) m.DebugAdvance(1f / 60f); }

    [Test]
    public void ASummonedFighterFormsBeforeTheCutAndStaysOutThroughTheHandBack()
    {
        var m = Mode(true);
        string id = m.DebugState.Allies[0].Id;
        Assert.AreEqual(0f, m.DebugSummonAlpha(id), .001f, "in a summon battle the fighter starts in her contract");
        float start = m.DebugFx;
        m.DebugUltimate(false, id);
        Vector3 beats = m.DebugCineBeats;
        Assert.Greater(beats.y, 0f, "the ultimate was played: " + m.DebugSummary());
        float push = beats.x, cut = beats.y, cutEnd = beats.z;
        Assert.Greater(cut, push, "the camera pushes in before the cut");
        Assert.Greater(cutEnd, cut, "the video has a length");
        float summonIn = m.DebugSummonIn(id);
        Assert.Less(summonIn, push, "her circle opens before the push");
        RunTo(m, cut - .01f);
        Assert.AreEqual(1f, m.DebugSummonAlpha(id), .01f, "fully formed when the video starts");
        Assert.Greater(m.DebugZoom, 1.6f, "pushed in on her at the cut");
        RunTo(m, cut + .02f);
        Assert.IsTrue(m.DebugCutIn, "the video plays");
        float stageEnd = m.DebugStageEnd;
        RunTo(m, cutEnd + .02f);
        Assert.Greater(m.DebugZoom, 1.4f, "the field comes back still framed on her");
        while (m.DebugFx < stageEnd - .05f)
        {
            Assert.AreEqual(1f, m.DebugSummonAlpha(id), .01f, "she stays out through the hand-back (fx " + m.DebugFx + ")");
            m.DebugAdvance(1f / 30f);
        }
        Assert.AreEqual(summonIn, m.DebugSummonIn(id), .001f, "no second summon after the video");
        RunTo(m, m.DebugQueueEnd + 1f);
        Assert.AreEqual(0f, m.DebugSummonAlpha(id), .01f, "she dissolves back into her contract after the shot");
        Assert.Greater(m.DebugFx, start);
    }

    [Test]
    public void AClassicBattlePushesInWithoutASummon()
    {
        var m = Mode(false);
        string id = m.DebugState.Allies[0].Id;
        Assert.AreEqual(1f, m.DebugSummonAlpha(id), .001f);
        float now = m.DebugFx;
        m.DebugUltimate(false, id);
        Vector3 beats = m.DebugCineBeats;
        Assert.AreEqual(now, beats.x, .05f, "no summon rise: the push starts at once");
        Assert.Greater(beats.y, beats.x);
        RunTo(m, beats.y - .01f);
        Assert.Greater(m.DebugZoom, 1.6f);
    }
}
