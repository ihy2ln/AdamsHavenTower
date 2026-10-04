using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// Transitions between the field and the full-screen cinematics (CM 10.3.3).
//  - Into an ultimate / awakening / decree video: in a summon battle the fighter's circle opens and she rises out of
//    it first; the camera then pushes into her while her element's light swells from her chest to fill the screen,
//    peaking on the video's own opening flash.
//  - Out of it: the video's closing flash fades back onto the field still framed on her (the clip's last frame is the
//    wide shot of her), a shockwave rings out at her feet, and the camera follows the payoff hit to the targets
//    before it pulls back. She dissolves into her contract after the pull-back.
//  - A staged skill in a summon battle waits for the summon to form before the action starts.
// Everything runs on the battle clock (fx), like BattleStage.cs.
public sealed partial class BattleMode
{
    const float CineZoom = 1.8f, CinePop = .85f;
    BattleUnit cineActor;
    float cinePushAt = -9f, cineCutAt = -9f, cineCutEnd = -9f;
    Color cineColor = Color.white;

    float SummonRiseTime { get { return Mathf.Max(SummonFadeIn + .08f, .4f * speed); } }
    float CinePushTime { get { return Mathf.Max(.3f, .45f * speed); } }
    float CineTailTime { get { return Mathf.Max(.15f, .3f * speed); } }

    // Would a summon starting at `from` open a fresh circle for this fighter (she is not out already)?
    bool FreshSummon(BattleUnit u, float from)
    {
        if (!Summons || !Fighter(u) || u == shownVanguard || u == plannedVanguard) return false;
        UnitVis v = V(u);
        return !(v.SummonIn > -8f && from <= v.SummonOut);
    }

    // Opens (or extends) the actor's summon window at the place SummonForGroup would give her.
    void SummonEarly(BattleUnit u, float from, float to)
    {
        if (!Summons || !Fighter(u)) return;
        BattleUnit holder = plannedVanguard ?? shownVanguard;
        Summon(u, from, to, holder == null || holder == u ? AtVanguard : AtStrike);
    }

    // Battle seconds from t0 to the cut into the video: the summon rise (if she has to be called out) and the push.
    // Returns the video's start; the push starts at pushAt.
    float CineLeadIn(BattleUnit actor, float t0, out float pushAt)
    {
        pushAt = t0 + (FreshSummon(actor, t0) ? SummonRiseTime * .6f : 0f);
        return pushAt + CinePushTime;
    }

    // The camera around a cinematic: pushed in on the actor up to the cut, held through the video (unseen), then from
    // the actor to the hit and back out. Returns when the shot ends.
    float BeginCineStage(BattleUnit actor, BattleCard card, List<BattleFact> group, float summonAt, float pushAt, float cutAt,
        float cutEnd, float contact, bool ranged)
    {
        SummonEarly(actor, summonAt, cutEnd);
        var targets = LayerTargets(group, actor);
        bool damaging = group.Exists(f => f.Kind == "damage" || f.Kind == "blocked");
        float back = Mathf.Max(.3f, .35f * speed);
        float end = Mathf.Max(contact, cutEnd + CineTailTime) + group.Count * .085f + (damaging ? .35f : .45f);
        camKeys.Clear();
        camKeys.Add(new CamKey { At = pushAt, Focus = Center, Zoom = 1f });
        camKeys.Add(new CamKey { At = cutAt, Follow = actor, Zoom = CineZoom });
        camKeys.Add(new CamKey { At = cutEnd, Follow = actor, Zoom = CineZoom });
        if (targets.Exists(u => u != actor))
        {
            Vector2 aim = Centroid(targets, actor);
            Vector2 focus = ranged ? (Chest(actor) + aim) * .5f : aim;
            camKeys.Add(new CamKey { At = Mathf.Max(cutEnd + CineTailTime, contact), Focus = focus, Zoom = Fit(Chest(actor), aim, 1.4f) });
        }
        else camKeys.Add(new CamKey { At = cutEnd + CineTailTime, Follow = actor, Zoom = 1.45f });
        var hold = camKeys[camKeys.Count - 1];
        hold.At = Mathf.Max(end, hold.At + .05f);
        camKeys.Add(hold);
        camKeys.Add(new CamKey { At = hold.At + back, Focus = Center, Zoom = 1f });
        stageActor = actor; stageCard = card; stageTargets.Clear(); stageTargets.AddRange(targets);
        stageEnd = hold.At + back; stageRushUntil = -9f; stageHold = hold.At;
        cineActor = actor; cinePushAt = pushAt; cineCutAt = cutAt; cineCutEnd = cutEnd; cineColor = ElementColor(actor.Element);
        var rig = ClipRig(actor);
        if (rig != null)
        {
            stageAction = rig.Flip.ActionFor(card);
            rig.Flip.LoadHi(stageAction);
            var set = rig.Flip; string action = stageAction;
            At(stageEnd, () => set.UnloadHi(action));
        }
        // The hand-back: a ring of her element at her feet as the field comes back.
        At(cutEnd, () =>
        {
            if (!slots.ContainsKey(actor)) return;
            Color c = cineColor;
            Spawn(5, Foot(actor), c, Slot(actor).W * 1.3f + 90f, .8f, Random.Range(0f, 360f));
            Spawn(1, Chest(actor), Color.Lerp(c, Color.white, .5f), 460f, .4f);
            Burst(Foot(actor) + new Vector2(0f, -10f), c, 14, 120f, 380f, 4f, 9f, .6f, 0f);
            shake = Mathf.Max(shake, 4f);
        });
        return stageEnd;
    }

    // Before the cut, taps wait (the video is about to start); from the video on, a tap skips as usual.
    bool CineLeadHolding { get { return cineActor != null && fx < cineCutAt; } }

    static Color CineLight(Color c) { return Color.Lerp(c, Color.white, .55f); }

    // The element light that carries the field into the video and the video back out (screen space, under the cut-in).
    void DrawCineFlash()
    {
        if (cineActor == null) return;
        Color light = CineLight(cineColor);
        Rect cover = new Rect(-VW, -VH, VW * 3f, VH * 3f);
        float flashFrom = Mathf.Lerp(cinePushAt, cineCutAt, .45f);
        if (fx >= flashFrom && fx < cineCutAt)
        {
            float k = (fx - flashFrom) / Mathf.Max(.0001f, cineCutAt - flashFrom);
            Vector2 at = slots.ContainsKey(cineActor) ? FieldToScreen(Chest(cineActor)) : Center;
            DrawGlow(at, Mathf.Lerp(160f, 1500f, k * k), Alpha(light, .9f * Mathf.Sqrt(k)));
            Fill(cover, Alpha(light, CinePop * k * k * k));
        }
        else if (fx >= cineCutEnd && fx < cineCutEnd + CineTailTime)
        {
            float k = 1f - (fx - cineCutEnd) / CineTailTime;
            Fill(cover, Alpha(light, CinePop * k * k));
        }
    }

    // 0 while a summoned fighter is still rising out of her circle, 1 once formed (and for everyone else).
    float SummonRise(BattleUnit u)
    {
        if (!Summons || !Fighter(u) || u == shownVanguard) return 1f;
        UnitVis v = V(u);
        if (v.SummonIn < -8f || fx < v.SummonIn) return 1f;
        return Mathf.Clamp01((fx - v.SummonIn) / SummonRiseTime);
    }

    // 0 until a summoned fighter starts to leave, 1 once she has dissolved.
    float SummonLeave(BattleUnit u)
    {
        if (!Summons || !Fighter(u) || u == shownVanguard) return 0f;
        UnitVis v = V(u);
        if (v.SummonOut < -8f || fx < v.SummonOut) return 0f;
        return Mathf.Clamp01((fx - v.SummonOut) / SummonFadeOut);
    }

    void ResetCine()
    {
        cineActor = null; cinePushAt = cineCutAt = cineCutEnd = -9f;
    }

#if UNITY_EDITOR
    // Test hooks (BattleTransitionTests): the transition's beats, a unit's summon opacity, the camera zoom now.
    public Vector3 DebugCineBeats { get { return new Vector3(cinePushAt, cineCutAt, cineCutEnd); } }
    public float DebugStageEnd { get { return stageEnd; } }
    public float DebugSummonAlpha(string unitId)
    {
        BattleUnit u = battle != null ? battle.Allies.Find(a => a.Id == unitId) : null;
        return u != null ? SummonAlpha(u) : -1f;
    }
    public float DebugSummonIn(string unitId)
    {
        BattleUnit u = battle != null ? battle.Allies.Find(a => a.Id == unitId) : null;
        return u != null ? V(u).SummonIn : -9f;
    }
    public float DebugZoom { get { Vector2 f; float z; EvalCamera(out f, out z); return z; } }
    public bool DebugCutIn { get { return CutInShowing; } }
#endif
}
