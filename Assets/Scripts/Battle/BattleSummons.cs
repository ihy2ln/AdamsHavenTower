using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// Summon battles (expeditions; rules in BattleState.SummonMode): JD stands on the field and the fighters are called
// out of their contracts. A fighter appears in a summon circle when their card plays, acts, and dissolves again; the
// Vanguard (a fighter whose card anchored them: Guard, Taunt, a stance) stays in front of JD until their hold runs
// out. A team-up brings the bond partner out beside them for a joint strike. Heals and buffs on a fighter target the
// party tiles, since the bodies are not on the field.
public sealed partial class BattleMode
{
    // Set by AdamsHavenPrototype.LaunchExpeditionBattle before Begin. The sandbox can opt in to compare.
    public bool SummonMode;
    public const string SummonSandboxKey = "AdamsHaven.Battle.SummonSandbox";
    public static bool SummonSandbox
    {
        get { return PlayerPrefs.GetInt(SummonSandboxKey, 0) == 1; }
        set { PlayerPrefs.SetInt(SummonSandboxKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    bool Summons { get { return battle != null && battle.SummonMode; } }

    // The field's view of the summons, timed to the action beats (the rules change at once, the field catches up).
    BattleUnit shownVanguard, shownMate;
    // The Vanguard as the action queue will have it (set when its beat is scheduled, not when it plays): where a
    // fighter summoned later in the same queue has to stand.
    BattleUnit plannedVanguard;
    BattleUnit pendingMate, pendingMateFor;
    // Summoned fighters glide between the summon places instead of jumping (a fighter taking up the Vanguard post).
    readonly Dictionary<BattleUnit, Vector2> spotPos = new Dictionary<BattleUnit, Vector2>();
    readonly Dictionary<BattleUnit, int> summonSpot = new Dictionary<BattleUnit, int>();
    const float SummonFadeIn = .22f, SummonFadeOut = .32f;

    // JD, the Vanguard's place in front of JD, the place an attacker steps out at when a Vanguard holds, and the
    // team-up partner's place just behind.
    static readonly Vector2 JdSpot = new Vector2(318f, 588f), VanguardSpot = new Vector2(548f, 606f),
        StrikeSpot = new Vector2(738f, 600f), MateSpot = new Vector2(652f, 566f);
    const string JdFieldArt = "FieldModels/jd";
    const int AtVanguard = 0, AtStrike = 1, AtMate = 2;

    void ResetSummons()
    {
        shownVanguard = null; shownMate = null; pendingMate = null; pendingMateFor = null; plannedVanguard = null;
        summonSpot.Clear(); spotPos.Clear();
    }

    static bool IsSummonFact(BattleFact f) { return f.Kind == "teamup" || f.Kind == "vanguard" || f.Kind == "recall"; }

    // Team-up, a fighter taking up the Vanguard post, a Vanguard recalled: beats of their own, not action groups.
    void ScheduleSummonFact(BattleFact f)
    {
        BattleUnit u = f.Actor;
        if (f.Kind == "teamup") { pendingMate = f.Target; pendingMateFor = f.Actor; return; }
        float at = Mathf.Max(queueEnd, fx);
        if (f.Kind == "vanguard") plannedVanguard = u;
        else if (f.Kind == "recall" && plannedVanguard == u) plannedVanguard = null;
        if (f.Kind == "vanguard")
        {
            At(at, () =>
            {
                shownVanguard = u;
                summonSpot[u] = AtVanguard;
                UnitVis v = V(u);
                if (SummonAlpha(u) < .99f) { v.SummonIn = fx; SummonFlash(u); }
                Float(Head(u) + new Vector2(0f, -40f), "HOLDS THE LINE", 26, Gold, 1.3f, 40f);
                Spawn(5, Foot(u), Gold, Slot(u).W * .95f + 50f, 1.1f, Random.Range(0f, 360f));
                Sfx("Sfx/shield_up", "status", .7f);
            });
            queueEnd = at + .35f;
        }
        else if (f.Kind == "recall")
        {
            At(at, () =>
            {
                if (shownVanguard == u) shownVanguard = null;
                UnitVis v = V(u);
                v.SummonOut = fx;
                if (u.Alive) Dissolve(u);
            });
        }
    }

    // A fighter's opacity on the field: 1 for JD, enemies and the Vanguard; otherwise the summon window's fade.
    float SummonAlpha(BattleUnit u)
    {
        if (!Summons || u == null || u.Enemy || u == battle.Summoner || u == shownVanguard) return 1f;
        if (assistUnit == u) return 1f;
        UnitVis v = V(u);
        if (fx < v.SummonIn) return 0f;
        float a = Mathf.Clamp01((fx - v.SummonIn) / SummonFadeIn);
        if (fx > v.SummonOut) a = Mathf.Min(a, 1f - Mathf.Clamp01((fx - v.SummonOut) / SummonFadeOut));
        return a;
    }

    // Where a summoned fighter stands this moment, eased toward their summon place (snapped while unseen).
    Vector2 SummonPos(BattleUnit u)
    {
        Vector2 target = SummonSpotOf(u), pos;
        if (!spotPos.TryGetValue(u, out pos) || SummonAlpha(u) <= .001f) pos = target;
        else pos = Vector2.Lerp(pos, target, 1f - Mathf.Exp(-Time.deltaTime * 9f));
        spotPos[u] = pos;
        return pos;
    }

    // JD's field body in a summon battle: the full-figure cutout (the chibi is for the plates and portraits).
    Cutout SummonerBody() { return Spr(JdFieldArt); }

    // Where a summoned fighter is headed.
    Vector2 SummonSpotOf(BattleUnit u)
    {
        if (u == shownVanguard) return VanguardSpot;
        if (u == shownMate) return MateSpot;
        int spot;
        if (!summonSpot.TryGetValue(u, out spot)) spot = AtVanguard;
        return spot == AtStrike ? StrikeSpot : spot == AtMate ? MateSpot : VanguardSpot;
    }

    // Called at the end of ScheduleGroup: the actor (and any fighter a heal or buff lands on) is summoned for the
    // action; a pending team-up brings the partner out to strike with them.
    void SummonForGroup(List<BattleFact> group, float actionAt, float lead, float end)
    {
        if (!Summons || group.Count == 0) return;
        BattleFact head = group[0];
        BattleUnit actor = head.Actor;
        if (Fighter(actor))
        {
            // The front place is free unless another fighter holds it (or will, by the time this action plays).
            BattleUnit holder = plannedVanguard ?? shownVanguard;
            Summon(actor, actionAt - .2f, end + .1f, holder == null || holder == actor ? AtVanguard : AtStrike);
        }
        foreach (BattleFact f in group)
            if (Fighter(f.Target) && f.Target != actor) Summon(f.Target, actionAt + lead * .5f - .2f, end + .15f, AtStrike);
        if (pendingMate != null && pendingMateFor == actor && Fighter(pendingMate))
        {
            BattleUnit mate = pendingMate;
            pendingMate = null; pendingMateFor = null;
            BattleCard strike = BattleCatalog.Basic(mate);
            bool damaging = group.Exists(f => f.Kind == "damage" || f.Kind == "blocked");
            bool ranged = Ranged(mate, strike);
            float mateLead = ImpactLead(mate, strike, damaging, ranged);
            float start = Mathf.Max(actionAt - .2f, actionAt + lead - mateLead);
            Summon(mate, start - .2f, end + .1f, AtMate);
            List<BattleFact> snapshot = group;
            At(actionAt, () =>
            {
                shownMate = mate;
                Float(Head(mate) + new Vector2(0f, -30f), "TEAM-UP!", 38, new Color(1f, .86f, .4f), 1.4f, 50f);
                Spawn(1, Chest(mate), new Color(1f, .86f, .4f), 380f, .5f);
                Sfx("Sfx/epiphany", "chime", .8f);
            });
            if (damaging) At(start, () => { BeginAction(mate, strike, snapshot, ranged, mateLead); V(mate).Banner = "TEAM-UP"; });
            At(end + .1f, () => { if (shownMate == mate) shownMate = null; });
        }
        else if (pendingMateFor == actor) { pendingMate = null; pendingMateFor = null; }
    }

    bool Fighter(BattleUnit u) { return u != null && !u.Enemy && u != battle.Summoner && battle.Allies.Contains(u); }

    // Opens (or extends) a fighter's summon window; a fresh summon gets its circle and flash.
    void Summon(BattleUnit u, float from, float to, int spot)
    {
        UnitVis v = V(u);
        bool showing = u == shownVanguard || (fx >= v.SummonIn && fx <= v.SummonOut + SummonFadeOut) || from <= v.SummonOut;
        if (showing && v.SummonIn > -8f)
        {
            if (to > v.SummonOut) { v.SummonOut = to; DissolveAt(u, to); }
            return;
        }
        summonSpot[u] = spot;
        v.SummonIn = Mathf.Max(fx, from);
        v.SummonOut = to;
        At(v.SummonIn, () => SummonFlash(u));
        DissolveAt(u, to);
    }

    // The dissolve at the window's end; a window that was extended since leaves it to the later one.
    void DissolveAt(BattleUnit u, float to)
    {
        At(to, () => { if (u != shownVanguard && u.Alive && fx >= V(u).SummonOut - .001f) Dissolve(u); });
    }

    void SummonFlash(BattleUnit u)
    {
        Color c = ElementColor(u.Element);
        Vector2 foot = SummonSpotOf(u);          // the place itself: the slot may not have moved there yet
        SlotInfo s = Slot(u);
        Spawn(5, foot, c, s.W * 1.1f + 70f, .9f, Random.Range(0f, 360f));
        Spawn(1, foot + new Vector2(0f, -8f), c, 320f, .45f);
        Spawn(0, foot + new Vector2(0f, -s.H * .52f), Color.white, 260f, .22f);
        Burst(foot + new Vector2(0f, -10f), c, 16, 60f, 260f, 5f, 11f, .8f, -260f);
        Sfx("Sfx/card_play", "card", .5f);
    }

    void Dissolve(BattleUnit u)
    {
        Color c = ElementColor(u.Element);
        Burst(Chest(u), c, 18, 40f, 180f, 4f, 9f, .9f, -220f);
        Spawn(6, Chest(u), new Color(c.r, c.g, c.b, .8f), 200f, .6f);
    }

    // Party tiles take the place of the bodies as targets for heals, buffs and Guard.
    BattleUnit TileHover()
    {
        if (selected == null || battle == null) return null;
        if (selected.Target != BattleTarget.Ally && selected.Target != BattleTarget.Self) return null;
        for (int i = 0; i < battle.Allies.Count; i++)
            if (new Rect(16f, 640f + i * 84f, 256f, 78f).Contains(mouse)) return battle.Allies[i];
        return null;
    }
}
