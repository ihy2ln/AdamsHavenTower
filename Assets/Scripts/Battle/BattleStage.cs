using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// In-battle skill cinematics: instead of cutting to a video, the camera pushes in on the live field, the fighter plays
// her clip large (hi-res pages, BattleClips.cs), the card's effect layers (BattleMoveFx.cs) play over the field, and
// the camera follows the hit to the targets, who react on their own layer. The HUD and hand stay on the screen matrix.
// Everything is driven by the battle clock (fx), so hit-stop freezes the shot, the speed setting scales it and the
// debug stepper replays it exactly.
public sealed partial class BattleMode
{
    struct CamKey
    {
        public float At, Zoom;
        public Vector2 Focus;
        public BattleUnit Follow;     // track this unit's chest (it may be dashing) instead of a fixed point
    }
    readonly List<CamKey> camKeys = new List<CamKey>();
    readonly List<BattleUnit> stageTargets = new List<BattleUnit>();
    BattleUnit stageActor;
    BattleCard stageCard;
    string stageAction;
    float stageEnd = -9f, stageRushUntil = -9f, stageHold = -9f;
    Vector2 camFocus = new Vector2(VW * .5f, VH * .5f);
    float camZoom = 1f;
    Vector2 camJolt;
    static readonly Vector2 Center = new Vector2(VW * .5f, VH * .5f);
    const float StageZoom = 1.45f, StageSlow = .8f, StageRush = 2.5f;

    bool StageShowing { get { return stageActor != null && fx < stageEnd; } }
    // 0 at the field framing, 1 once the camera is pushed in: fades the dim, bars, hand and HUD bits.
    // Evaluated from the battle clock, not the last frame, so the hand and HUD follow the camera exactly. A staged
    // shot holds 1 from the push-in to the pull-back even when it frames wide (an area hit can sit near zoom 1);
    // after a tap-to-skip only the two return keys are left, so the weight follows the zoom back out.
    float StageWeight
    {
        get
        {
            int n = camKeys.Count;
            if (n >= 4)
            {
                float t0 = camKeys[0].At, tIn = camKeys[1].At, tOut = camKeys[n - 2].At, tEnd = camKeys[n - 1].At;
                if (fx < t0 || fx >= tEnd) return 0f;
                if (fx < tIn) return Mathf.Clamp01((fx - t0) / Mathf.Max(.0001f, tIn - t0));
                if (fx > tOut) return Mathf.Clamp01(1f - (fx - tOut) / Mathf.Max(.0001f, tEnd - tOut));
                return 1f;
            }
            Vector2 f; float z; EvalCamera(out f, out z); return Mathf.Clamp01((z - 1f) / .3f);
        }
    }
    bool InStage(BattleUnit u) { return StageShowing && (u == stageActor || stageTargets.Contains(u)); }

    // Should this card be staged? The CINE setting decides, as it did for the skill videos.
    bool StageWanted(BattleCard card)
    {
        var mode = Cinematics;
        if (BattleCombos.IsCombo(card)) return mode != CinematicMode.Off;     // a combo is always its own shot
        if (mode == CinematicMode.Always) return true;
        return mode == CinematicMode.FirstUse && !cinematicsSeen.Contains(card.Id);
    }

    // Battle seconds from the action's start to the hit when staged: the clip's own contact, slowed a little.
    float StageLead(BattleUnit actor, BattleCard card, float fieldLead)
    {
        var rig = ClipRig(actor);
        float contact = rig != null ? rig.Flip.ContactTime(rig.Flip.ActionFor(card)) : -1f;
        float lead = contact > 0f ? contact * SpeedBase / StageSlow : fieldLead / StageSlow;
        return Mathf.Clamp(lead, .2f, 1.4f);
    }

    // After an ultimate's video: battle seconds from the hand-over pose to the hit (a beam still has to cross).
    float PayoffLead(BattleUnit actor, BattleCard card)
    {
        var rig = ClipRig(actor);
        if (rig == null) return .06f;
        string action = rig.Flip.ActionFor(card);
        return Mathf.Max(.04f, (rig.Flip.ContactTime(action) - rig.Flip.KeyTime(action)) * SpeedBase);
    }

    // Lays the camera over an action that starts at a0 and hits at contact. Returns when the stage ends.
    float BeginStage(BattleUnit actor, BattleCard card, List<BattleFact> group, float t0, float a0, float release, float contact, bool ranged)
    {
        var targets = LayerTargets(group, actor);
        bool damaging = group.Exists(f => f.Kind == "damage" || f.Kind == "blocked");
        bool melee = damaging && !ranged;
        float push = Mathf.Max(.2f, .25f * speed), back = Mathf.Max(.25f, .25f * speed);
        float end = contact + group.Count * .085f + (damaging ? .3f : .45f);
        Vector2 aim = Centroid(targets, actor);
        camKeys.Clear();
        camKeys.Add(new CamKey { At = t0, Focus = Center, Zoom = 1f });
        camKeys.Add(new CamKey { At = t0 + push, Follow = actor, Zoom = melee ? 1.35f : StageZoom });
        bool others = targets.Exists(u => u != actor);
        if (melee)
        {
            // The dash: keep the fighter in frame, then frame her and the target together at the hit.
            camKeys.Add(new CamKey { At = Mathf.Max(t0 + push, contact - .12f), Follow = actor, Zoom = 1.3f });
            camKeys.Add(new CamKey { At = contact, Focus = aim, Zoom = Fit(Chest(actor), aim, 1.45f) });
        }
        else if (ranged && others)
        {
            // The release: pull back to show the path, then land on the target at the hit.
            camKeys.Add(new CamKey { At = Mathf.Max(t0 + push, release), Follow = actor, Zoom = StageZoom });
            camKeys.Add(new CamKey { At = contact, Focus = (Chest(actor) + aim) * .5f, Zoom = Fit(Chest(actor), aim, 1.35f) });
            camKeys.Add(new CamKey { At = contact + .1f, Focus = aim, Zoom = targets.Count > 1 ? 1.15f : 1.3f });
        }
        else if (others)
            camKeys.Add(new CamKey { At = contact, Focus = aim, Zoom = targets.Count > 1 ? 1.12f : 1.3f });
        var hold = camKeys[camKeys.Count - 1];
        hold.At = end;
        camKeys.Add(hold);
        camKeys.Add(new CamKey { At = end + back, Focus = Center, Zoom = 1f });
        stageActor = actor; stageCard = card; stageTargets.Clear(); stageTargets.AddRange(targets);
        stageEnd = end + back; stageRushUntil = -9f; stageHold = end;
        var rig = ClipRig(actor);
        if (rig != null)
        {
            stageAction = rig.Flip.ActionFor(card);
            rig.Flip.LoadHi(stageAction);
            var set = rig.Flip; string action = stageAction;
            At(stageEnd, () => set.UnloadHi(action));
        }
        return stageEnd;
    }

    // A staged melee strike stays at the target while the camera holds on it and goes home with the pull-back, so the
    // fighter never leaves a frame that is still looking at the hit (and the camera never chases her home).
    void HoldForStage(BattleUnit actor, UnitVis v)
    {
        v.HoldTo = -1f;
        if (actor != stageActor || v.Ranged || v.LungeTo == Vector2.zero || stageHold <= fx) return;
        v.HoldTo = stageHold;
        v.LungeDur = Mathf.Max(v.LungeDur, stageEnd - v.LungeStart);
    }

    Vector2 Centroid(List<BattleUnit> units, BattleUnit fallback)
    {
        Vector2 sum = Vector2.zero; int n = 0;
        foreach (var u in units) if (u != null && slots.ContainsKey(u)) { sum += Chest(u); n++; }
        return n > 0 ? sum / n : fallback != null && slots.ContainsKey(fallback) ? Chest(fallback) : Center;
    }

    // The widest zoom that still shows both points with room around them.
    static float Fit(Vector2 a, Vector2 b, float max)
    {
        float span = Mathf.Abs(a.x - b.x) + 420f, spanY = Mathf.Abs(a.y - b.y) + 360f;
        return Mathf.Clamp(Mathf.Min(VW * .95f / span, VH * .95f / spanY), 1f, max);
    }

    Vector2 KeyFocus(CamKey k)
    {
        if (k.Follow != null && slots.ContainsKey(k.Follow))
        {
            Vector2 off; float lean;
            LungeOffset(V(k.Follow), fx, k.Follow.Enemy, out off, out lean);
            return Chest(k.Follow) + off;
        }
        return k.Follow != null ? Center : k.Focus;
    }

    // Once per OnGUI: the camera for this frame, and the stage cleared once it has ended.
    void UpdateCamera()
    {
        EvalCamera(out camFocus, out camZoom);
        if (camKeys.Count > 0 && fx >= camKeys[camKeys.Count - 1].At && fx >= stageEnd)
        { camKeys.Clear(); stageActor = null; stageCard = null; stageTargets.Clear(); }
    }

    // The camera at the current battle time (a pure function of fx and the keys).
    void EvalCamera(out Vector2 camAt, out float zoomAt)
    {
        camAt = Center; zoomAt = 1f;
        if (camKeys.Count == 0 || fx < camKeys[0].At || fx >= camKeys[camKeys.Count - 1].At) return;
        int i = 0;
        while (i + 1 < camKeys.Count && camKeys[i + 1].At <= fx) i++;
        CamKey a = camKeys[i], b = camKeys[i + 1];
        float k = Ease((fx - a.At) / Mathf.Max(.0001f, b.At - a.At));
        Vector2 focus = Vector2.Lerp(KeyFocus(a), KeyFocus(b), k);
        float zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(1f, a.Zoom)), Mathf.Log(Mathf.Max(1f, b.Zoom)), k));
        float hw = VW / (2f * zoom), hh = VH / (2f * zoom);
        focus.x = Mathf.Clamp(focus.x, hw, VW - hw);
        focus.y = Mathf.Clamp(focus.y, hh, VH - hh);
        camAt = focus; zoomAt = zoom;
    }

    Matrix4x4 CamMatrix
    {
        get
        {
            return Matrix4x4.TRS(Center, Quaternion.identity, new Vector3(camZoom, camZoom, 1f))
                   * Matrix4x4.TRS(-camFocus, Quaternion.identity, Vector3.one);
        }
    }
    // Field point -> virtual screen point (numbers and plates are drawn crisp, outside the camera).
    Vector2 FieldToScreen(Vector2 p) { return (Vector2)CamMatrix.MultiplyPoint3x4(p) + camJolt; }

    // Tap while staged: the camera returns at once and the rest of the action runs fast; no beat is skipped.
    void SkipStage()
    {
        if (CineLeadHolding) return;          // the cut is a beat away: the video's own tap-to-skip takes over
        EvalCamera(out camFocus, out camZoom);
        camKeys.Clear();
        camKeys.Add(new CamKey { At = fx, Focus = camFocus, Zoom = camZoom });
        camKeys.Add(new CamKey { At = fx + .08f, Focus = Center, Zoom = 1f });
        stageRushUntil = stageEnd;
    }
    float StageClock { get { return fx < stageRushUntil ? StageRush : 1f; } }

    // Darkens the field under the staged units (drawn after the background, before the units).
    void DrawStageDim()
    {
        float w = StageWeight;
        if (w <= 0f) return;
        Fill(new Rect(-VW, -VH, VW * 3f, VH * 3f), new Color(0f, 0f, .02f, .38f * w));
    }

    // Units outside the shot fade back; the actor is drawn last so she is never hidden behind a lane.
    List<BattleUnit> StageOrder(List<BattleUnit> order)
    {
        if (!StageShowing) return order;
        var list = new List<BattleUnit>(order.Count);
        foreach (var u in order) if (!InStage(u)) list.Add(u);
        foreach (var u in order) if (InStage(u) && u != stageActor) list.Add(u);
        if (order.Contains(stageActor)) list.Add(stageActor);
        return list;
    }
    float StageFade(BattleUnit u) { return StageShowing && !InStage(u) ? StageWeight : 0f; }

    // Letterbox bars, the card's name and the skip hint, in screen space.
    void DrawStageOverlay()
    {
        float w = StageWeight;
        if (w <= 0f || stageActor == null) return;
        Fill(new Rect(-VW, -VH, VW * 3f, VH + 64f * w), new Color(0, 0, 0, .9f));
        Fill(new Rect(-VW, VH - 64f * w, VW * 3f, VH + 64f), new Color(0, 0, 0, .9f));
        string name = stageCard != null ? stageCard.Name : "";
        Text(new Rect(60f, VH - 64f * w - 64f, 900f, 56f), name.ToUpperInvariant(), 40, new Color(1, 1, 1, w), TextAnchor.MiddleLeft, true, false, 3f);
        Text(new Rect(62f, VH - 64f * w - 16f, 900f, 26f), stageActor.Name.ToUpperInvariant(), 16, Alpha(CardColor(stageCard, stageActor), w * .9f), TextAnchor.MiddleLeft, true, false, 2f);
        Text(new Rect(VW - 300f, VH - 64f * w + 18f, 260f, 28f), "TAP TO SKIP", 13, new Color(.75f, .8f, .9f, w * .7f), TextAnchor.MiddleRight);
    }

    void ResetStage()
    {
        camKeys.Clear(); stageTargets.Clear(); stageActor = null; stageCard = null; stageAction = null;
        stageEnd = -9f; stageRushUntil = -9f; stageHold = -9f; camFocus = Center; camZoom = 1f; camJolt = Vector2.zero;
        ResetCine();
    }

#if UNITY_EDITOR
    public string DebugCam() { return "focus=" + camFocus + " zoom=" + camZoom.ToString("0.00") + " w=" + StageWeight.ToString("0.00") + " staged=" + StageShowing + " end=" + stageEnd.ToString("0.00") + " fx=" + fx.ToString("0.00"); }
    public int DebugBoltCount { get { return bolts.Count; } }
    public int DebugEffectCount(int kind) { int n = 0; foreach (var e in effects) if (e.Kind == kind) n++; return n; }
    public float DebugFx { get { return fx; } }
    public float DebugQueueEnd { get { return queueEnd; } }

    // Test hook: like DebugPlayCard, but ally-target cards (heals, buffs) land on the first other ally they can take.
    public bool DebugPlayCardAny(string unitId, string cardId)
    {
        if (battle == null) return false;
        BattleUnit actor = battle.Allies.Find(u => u.Id == unitId);
        if (actor == null) return false;
        BattleCard card = BattleCatalog.FighterKit(actor).Find(c => c.Id == cardId);
        if (card == null) return false;
        BattleUnit wallet = battle.Wallet(actor);
        actor.Ap = Mathf.Max(actor.Ap, card.Ap); wallet.Ap = Mathf.Max(wallet.Ap, card.Ap); wallet.Ep = Mathf.Max(wallet.Ep, card.Ep);
        BattleUnit target = card.Target == BattleTarget.Self ? actor : battle.Enemies.Find(e => battle.IsTarget(card, actor, e));
        if (target == null) target = battle.Allies.Find(a => a != actor && battle.IsTarget(card, actor, a));
        if (target == null) target = battle.Allies.Find(a => battle.IsTarget(card, actor, a));
        return Perform(card, actor, target);
    }
#endif
}
