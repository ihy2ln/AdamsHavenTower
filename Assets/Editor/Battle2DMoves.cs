using System.Collections.Generic;
using UnityEngine;

// Keyframed moves for 2D cutout rigs (Battle2DRigBuilder). The rig faces right (toward the enemy line).
// Aim = absolute direction of a limb in degrees (0 = toward the enemy, 90 = up, -90 = down), solved against the art's
// rest pose; Turn = relative rotation of a body bone (positive = counterclockwise, i.e. leaning back); Move = offset in
// rig units (the figure is 2 units tall). "_n" is the near side (drawn in front), "_f" the far side (behind the torso).
// Clip names and contact times match the 3D rigs (BattleModels.MoveSets): AH_attack_basic hits at 0.34 s,
// AH_skill_punch_combo at 1.33 s (played from 0.9 s), AH_ult_flying_fist_kick at 2.23 s (played from 1.7 s).
public static class Battle2DMoves
{
    public sealed class Pose
    {
        public readonly Dictionary<string, float> Aim = new Dictionary<string, float>();
        public readonly Dictionary<string, float> Turn = new Dictionary<string, float>();
        public readonly Dictionary<string, Vector2> Move = new Dictionary<string, Vector2>();

        public Pose Copy()
        {
            var p = new Pose();
            foreach (var kv in Aim) p.Aim[kv.Key] = kv.Value;
            foreach (var kv in Turn) p.Turn[kv.Key] = kv.Value;
            foreach (var kv in Move) p.Move[kv.Key] = kv.Value;
            return p;
        }
        public Pose A(string bone, float deg) { var p = Copy(); p.Aim[bone] = deg; return p; }
        public Pose T(string bone, float deg) { var p = Copy(); p.Turn[bone] = deg; return p; }
        public Pose M(string bone, float x, float y) { var p = Copy(); p.Move[bone] = new Vector2(x, y); return p; }
        // Arm in one call: upper arm, forearm and fist (the fist follows the forearm unless given).
        public Pose Arm(string side, float upper, float fore, float? fist = null)
        {
            var p = A("arm_" + side + "_up", upper).A("arm_" + side + "_lo", fore);
            if (fist.HasValue) p.Aim["hand_" + side] = fist.Value; else p.Aim.Remove("hand_" + side);
            return p;
        }
        public Pose Leg(string side, float thigh, float shin) { return A("leg_" + side + "_up", thigh).A("leg_" + side + "_lo", shin); }
    }

    public struct Key { public float Time; public Pose Pose; public Key(float t, Pose p) { Time = t; Pose = p; } }
    public sealed class ClipSpec { public string Name; public bool Loop; public List<Key> Keys = new List<Key>(); }

    static ClipSpec Clip(string name, bool loop, params Key[] keys) { var c = new ClipSpec { Name = name, Loop = loop }; c.Keys.AddRange(keys); return c; }
    static Key K(float t, Pose p) { return new Key(t, p); }

    // Ready stance: lead (far) fist forward at chin height, rear (near) fist by the jaw, knees bent, slight forward lean.
    public static Pose Guard()
    {
        return new Pose()
            .Arm("f", -45f, 70f, 40f).Arm("n", -70f, 95f, 60f)
            .Leg("n", -105f, -82f).Leg("f", -72f, -96f)
            .T("torso", -3f).T("head", 2f).M("hips", 0f, -.035f);
    }

    public static List<ClipSpec> Clips()
    {
        var g = Guard();
        var list = new List<ClipSpec>();

        // Idle: breathing bob, a slow tail swish, hair settling.
        var breathe = g.T("torso", -4.5f).M("hips", 0f, -.05f).T("head", 1f).Arm("f", -42f, 76f, 44f).Arm("n", -67f, 100f, 64f)
            .T("tail1", 7f).T("tail2", 12f).T("hair", -2.5f);
        list.Add(Clip("AH_battle_guard", true, K(0f, g), K(1f, breathe), K(2f, g)));

        // Basic: lead jab. Contact 0.34 s.
        var cock = g.Arm("f", -62f, 52f, 30f).T("torso", -1f).M("hips", -.02f, -.04f);
        var jab = g.Arm("f", -3f, 0f, 0f).T("torso", -12f).T("head", -5f).M("hips", .07f, -.05f).T("tail1", 10f).Leg("f", -60f, -100f);
        var recoil = g.Arm("f", -14f, 12f, 8f).T("torso", -9f).M("hips", .05f, -.045f);
        list.Add(Clip("AH_attack_basic", false, K(0f, g), K(.18f, cock), K(.34f, jab), K(.46f, recoil), K(.8f, g)));

        // Skill: rear straight (1.33 s), lead hook, rising uppercut.
        var twist = g.Arm("n", -125f, 35f, 20f).T("torso", 5f).M("hips", -.03f, -.05f);
        var straight = g.Arm("n", -2f, 0f, 0f).Arm("f", -60f, 60f, 35f).T("torso", -15f).T("head", -6f).M("hips", .1f, -.06f)
            .Leg("n", -125f, -95f).T("tail1", 12f);
        var hookWind = straight.Arm("n", -65f, 90f, 60f).Arm("f", -10f, 40f, 20f).T("torso", -6f);
        var hook = hookWind.Arm("f", 12f, -18f, -18f).T("torso", -12f).M("hips", .12f, -.06f);
        var dip = hook.Arm("n", -110f, -40f, -30f).M("hips", .1f, -.11f).T("torso", -4f);
        var upper = dip.Arm("n", -55f, 82f, 80f).Arm("f", -60f, 65f, 40f).T("torso", 7f).T("head", 6f).M("hips", .1f, .02f).T("tail2", 20f);
        list.Add(Clip("AH_skill_punch_combo", false, K(0f, g), K(.9f, g), K(1.15f, twist), K(1.33f, straight), K(1.48f, hookWind),
            K(1.62f, hook), K(1.74f, dip), K(1.88f, upper), K(2.25f, g)));

        // Ultimate: crouch, leap, diving fist with a flying knee (contact 2.23 s), land.
        var crouch = g.M("hips", 0f, -.16f).Leg("n", -40f, -135f).Leg("f", -55f, -140f).Arm("f", -150f, -150f).Arm("n", -160f, -155f)
            .T("torso", -14f).T("tail1", -10f);
        var leap = g.M("root", .18f, .5f).T("torso", -22f).Arm("f", 115f, 95f, 90f).Arm("n", 135f, 120f, 100f)
            .Leg("n", -25f, -150f).Leg("f", -50f, -160f).T("tail1", 18f).T("tail2", 25f).T("hair", 6f);
        var dive = g.M("root", .38f, .26f).T("torso", -28f).T("head", -10f).Arm("f", -22f, -25f, -25f).Arm("n", -165f, -150f)
            .Leg("n", 8f, 2f).Leg("f", -120f, -60f).T("tail1", 22f).T("hair", 10f);
        var land = g.M("root", .12f, 0f).M("hips", 0f, -.13f).T("torso", -12f).Leg("n", -50f, -130f).Leg("f", -65f, -135f)
            .Arm("f", -30f, -10f, -10f).Arm("n", -150f, -120f);
        list.Add(Clip("AH_ult_flying_fist_kick", false, K(0f, g), K(1.7f, g), K(1.88f, crouch), K(2.06f, leap), K(2.23f, dive),
            K(2.32f, dive.M("root", .4f, .2f)), K(2.55f, land), K(3.05f, g)));

        // Guard / buff cards: arms cross, then a pulse outward (0.65 s).
        var cross = g.Arm("f", -38f, 112f, 100f).Arm("n", -48f, 104f, 95f).T("torso", -2f).M("hips", 0f, -.07f).T("head", -4f);
        var pulse = g.Arm("f", -62f, 62f, 45f).Arm("n", -78f, 75f, 50f).T("torso", 5f).T("head", 6f).M("hips", 0f, -.02f).T("tail2", 18f);
        list.Add(Clip("AH_attack_guard_pulse", false, K(0f, g), K(.32f, cross), K(.65f, pulse), K(.85f, pulse), K(1.25f, g)));

        // Hit reaction (played from 0.35 s): snap back, arms flung, recover.
        var flinch = g.M("root", -.12f, 0f).T("torso", 16f).T("head", 12f).Arm("f", -125f, -150f).Arm("n", -135f, -160f)
            .M("hips", 0f, -.05f).T("tail1", -12f).T("hair", -6f);
        list.Add(Clip("AH_hit_react", false, K(0f, g), K(.35f, g), K(.46f, flinch), K(.7f, flinch.M("root", -.08f, 0f).T("torso", 8f)), K(1.05f, g)));

        // Knockdown (held on the last frame): stagger, topple backwards about the feet, lie still.
        // The root turns about the feet, so the body is slid back under the camera as it topples (it lies about 1 unit wide).
        var stagger = flinch.M("root", -.15f, 0f).T("torso", 20f);
        var falling = stagger.T("root", 40f).M("root", .25f, -.05f).Arm("f", 150f, 170f).Arm("n", 160f, 175f);
        var down = g.T("root", 84f).M("root", .95f, .12f).T("torso", 4f).T("head", 6f).Arm("f", 175f, 178f).Arm("n", 170f, 176f)
            .Leg("n", -15f, -20f).Leg("f", -25f, -30f).M("hips", 0f, 0f).T("tail1", 20f);
        list.Add(Clip("AH_knock_down", false, K(0f, g), K(.2f, g), K(.42f, stagger), K(.8f, falling), K(1.1f, down), K(1.4f, down)));

        // Victory (character sheet): fist pump.
        var pump = g.Arm("f", 82f, 98f, 95f).Arm("n", -80f, 60f, 40f).T("head", 8f).T("torso", 4f).M("hips", 0f, .02f).T("tail2", 22f);
        list.Add(Clip("AH_victory", false, K(0f, g), K(.3f, pump), K(.55f, pump.T("head", 4f)), K(1.4f, pump), K(1.8f, g)));
        return list;
    }
}
