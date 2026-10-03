using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;

// 2D rig pilot (Kaela): the cutout rig, its move clips, the per-move effects and the match-cut cinematic.
public sealed class Battle2DPilotTests
{
    const string Rig = "AdamsHaven/BattleRigs2D/kaela";

    [Test]
    public void KaelaRigHasEveryClipTheBattleAsksFor()
    {
        var model = Resources.Load<GameObject>(Rig + "/model");
        Assert.IsNotNull(model, "2D rig prefab");
        var clips = Resources.LoadAll<AnimationClip>(Rig);
        string[] needed = { "AH_battle_guard", "AH_attack_basic", "AH_skill_punch_combo", "AH_ult_flying_fist_kick",
            "AH_attack_guard_pulse", "AH_hit_react", "AH_knock_down" };
        foreach (var name in needed)
            Assert.IsTrue(System.Array.Exists(clips, c => c.name == name), name);
        // Contact times shared with the 3D rigs (BattleModels.MoveSets) must fall inside the clips.
        Assert.Greater(System.Array.Find(clips, c => c.name == "AH_attack_basic").length, .34f);
        Assert.Greater(System.Array.Find(clips, c => c.name == "AH_skill_punch_combo").length, 1.33f);
        Assert.Greater(System.Array.Find(clips, c => c.name == "AH_ult_flying_fist_kick").length, 2.23f);
    }

    [Test]
    public void EveryRigPartHasItsSprite()
    {
        var model = Resources.Load<GameObject>(Rig + "/model");
        var parts = model.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.GreaterOrEqual(parts.Length, 20);
        foreach (var p in parts) Assert.IsNotNull(p.sprite, p.name);
    }

    [Test]
    public void SamplingAClipMovesTheBones()
    {
        var model = Object.Instantiate(Resources.Load<GameObject>(Rig + "/model"));
        try
        {
            var clips = Resources.LoadAll<AnimationClip>(Rig);
            var jab = System.Array.Find(clips, c => c.name == "AH_attack_basic");
            var arm = model.transform.Find("root/hips/torso/arm_f_up");
            Assert.IsNotNull(arm, "far upper arm bone");
            jab.SampleAnimation(model, 0f);
            float rest = arm.localEulerAngles.z;
            jab.SampleAnimation(model, .34f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(rest, arm.localEulerAngles.z)), 20f, "the jab swings the lead arm");
        }
        finally { Object.DestroyImmediate(model); }
    }

    [Test]
    public void KaelaMoveEffectsAndCinematicExist()
    {
        foreach (var card in new[] { "mv_frost_jab", "mv_rime_sweep", "mv_shatter_hook", "mv_whiteout_counter", "mv_permafrost_brace" })
        {
            var sheet = Resources.Load<Texture2D>("AdamsHaven/Fx/Moves/" + card);
            Assert.IsNotNull(sheet, card);
            Assert.AreEqual(0, sheet.width % 4, card + " 4x4 grid");
        }
        Assert.IsNotNull(Resources.Load<VideoClip>("AdamsHaven/Fx/Moves/mv_glacier_guard"), "transparent effect video");
        Assert.IsNotNull(Shader.Find("AdamsHaven/StackedAlpha"), "stacked matte shader");
        Assert.IsNotNull(Resources.Load<VideoClip>("AdamsHaven/UltCutIns/mv_shatter_hook"), "Shatter cinematic");
        Assert.IsNotNull(Resources.Load<Texture2D>("AdamsHaven/Fx/cine_bg"), "match-cut backdrop");
    }
}
