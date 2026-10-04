using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using static BattleGui;

// The field side of combos and joint ultimates (rules in BattleCombos.cs).
//  - Combo: "COMBO!" and its name show as the team-up card lands, then the partner takes the front and performs the
//    combo as a staged shot (her own move's clip and layers until the combo has its own), while the fighter who
//    played the card stays out beside her. The partner no longer throws the plain team-up strike.
//  - Joint ultimate: offered in the ULT menu under the solo ultimates. Both fighters are summoned before the push-in
//    (BattleTransitions.cs), the cut-in names both, and the partner strikes beside the lead on the hand-back. Its video
//    is UltCutIns/ult_joint_<a>_<b>.mp4; until that exists the card-art cut-in plays, with both fighters' art.
public sealed partial class BattleMode
{
    // Fighters who perform a combo in the facts being ingested (they skip the plain team-up strike).
    readonly HashSet<BattleUnit> comboMates = new HashSet<BattleUnit>();
    readonly Dictionary<BattleUnit, BattleUnit> comboWith = new Dictionary<BattleUnit, BattleUnit>();
    BattleUnit jointPartner, cutPartner;

    void PrepareCombos(List<BattleFact> list)
    {
        foreach (BattleFact f in list) if (f.Kind == "combo" && f.Actor != null) comboMates.Add(f.Actor);
    }

    // The "combo" and "joint" facts are beats of their own: they come just before the move they announce.
    void ScheduleComboMark(BattleFact f, List<BattleFact> list)
    {
        BattleUnit performer = f.Actor, partner = f.Target;
        float at = Mathf.Max(queueEnd, fx);
        if (f.Kind == "combo")
        {
            comboWith[performer] = partner;
            string name = f.Card != null ? f.Card.Name.ToUpperInvariant() : "";
            At(at, () =>
            {
                Vector2 above = (partner != null && slots.ContainsKey(partner) ? Head(partner) : Center) + new Vector2(0f, -70f);
                Float(above, "COMBO!", 44, new Color(1f, .86f, .4f), 1.3f, 50f);
                Float(above + new Vector2(0f, 44f), name, 26, Color.white, 1.3f, 40f);
                Sfx("Sfx/epiphany", "chime", .8f);
            });
            queueEnd = at + .2f;
            return;
        }
        // A joint ultimate: the partner comes out with the lead and strikes beside her on the hand-back.
        jointPartner = partner;
        cutPartner = partner;
        if (Summons && Fighter(partner)) { pendingMate = partner; pendingMateFor = performer; }
    }

    // After a combo or joint group is scheduled: who stands where while it plays.
    void ComboForGroup(List<BattleFact> group, float actionAt, float lead, float end)
    {
        if (group.Count == 0) return;
        BattleFact head = group[0];
        BattleUnit actor = head.Actor;
        if (BattleCombos.IsCombo(head.Card))
        {
            comboMates.Remove(actor);
            BattleUnit partner;
            if (!comboWith.TryGetValue(actor, out partner)) return;
            comboWith.Remove(actor);
            if (!Summons || !Fighter(partner)) return;
            // The performer takes the front; the one who played the card stays out just behind her.
            float swap = actionAt - .3f;
            if (partner != shownVanguard) Summon(partner, swap, end + .1f, AtMate);
            At(swap, () =>
            {
                if (partner != shownVanguard) shownMate = partner;
                if (actor != shownVanguard) summonSpot[actor] = shownVanguard != null ? AtStrike : AtVanguard;
            });
            At(end + .1f, () => { if (shownMate == partner) shownMate = null; });
            return;
        }
        if (BattleCombos.IsJoint(head.Card) && jointPartner != null)
        {
            BattleUnit partner = jointPartner;
            jointPartner = null;
            // Classic battles: the partner is already standing and strikes beside the lead.
            if (!Summons && partner.Alive && group.Exists(f => f.Kind == "damage" || f.Kind == "blocked"))
            {
                BattleCard strike = BattleCatalog.Basic(partner);
                bool ranged = Ranged(partner, strike);
                List<BattleFact> snapshot = group;
                At(actionAt, () => { BeginAction(partner, strike, snapshot, ranged, lead); V(partner).Banner = "JOINT"; });
            }
        }
    }

    // BeginCineStage: a joint ultimate's partner is called out with the lead, to her side.
    void SummonJointPartner(float from, float to)
    {
        BattleUnit partner = jointPartner;
        if (partner == null || !Summons || !Fighter(partner)) return;
        Summon(partner, from, to, AtMate);
        At(from, () => shownMate = partner);
    }

    VideoClip JointClip(BattleCard card)
    {
        string key = "joint/" + card.Id;
        VideoClip clip;
        if (!ultClips.TryGetValue(key, out clip)) { clip = Resources.Load<VideoClip>("AdamsHaven/UltCutIns/" + card.Id); ultClips[key] = clip; }
        return clip;
    }

    bool CutIsJoint { get { return BattleCombos.IsJoint(cutCard) && cutPartner != null; } }

    string CutKindLabel()
    {
        if (CutIsJoint) return "JOINT ULTIMATE";
        return cutCard.Kind == BattleCardKind.Awakening ? "AWAKENING" : "ULTIMATE";
    }

    string CutNames()
    {
        if (!CutIsJoint) return cutUnit.Name.ToUpperInvariant();
        return (cutUnit.Name.Split(' ')[0] + "  &  " + cutPartner.Name.Split(' ')[0]).ToUpperInvariant();
    }

    // The card-art cut-in of a joint ultimate: the partner's art slides in behind and to the right of the lead's.
    void DrawJointArt(float t, float a, Matrix4x4 old)
    {
        if (!CutIsJoint) return;
        Texture2D art = Art("FullCards/" + cutPartner.Id);
        if (art == null) return;
        float slide = EaseOut(Mathf.Clamp01((t - .06f) / 0.28f));
        float x = Mathf.Lerp(-260f, 470f, slide) + t * 30f;
        Color before = GUI.color; GUI.color = new Color(.85f, .85f, .9f, a * .95f);
        Rect ar = new Rect(x, 70f, 460f, 780f);
        RotateAround(-7f, new Vector2(800f, 450f));
        GUI.DrawTexture(ar, art, ScaleMode.ScaleAndCrop, true);
        Outline(ar, Alpha(ElementColor(cutPartner.Element), .85f * a), 4f, 6f);
        GUI.matrix = old;
        GUI.color = before;
    }

    // The joint options in a fighter's ULT menu, below her solo ultimates.
    void DrawJointChoices(BattleUnit u, Rect box, int row)
    {
        foreach (BattleCard joint in battle.JointUltimates(u))
        {
            string other = BattleCombos.JointPartner(joint);
            if (other == u.Id) other = joint.Owner;
            BattleUnit mate = battle.Allies.Find(a => a.Id == other);
            string label = joint.Name + "  +" + (mate != null ? mate.Name.Split(' ')[0] : other);
            if (MiniButton(new Rect(box.x + 10f, box.y + 26f + row * 40f, box.width - 20f, 34f), label, true, false, new Color(1f, .7f, .35f), -1f, 13))
                SelectJoint(joint);
            row++;
        }
    }

    void SelectJoint(BattleCard joint)
    {
        if (Busy || !battle.CanJoint(joint)) return;
        selected = joint; selectedActor = battle.OwnerOf(joint); selectedUltimate = -1;
        chooseUltimateFor = null; focusUnit = null;
        if (joint.Target == BattleTarget.AllAllies || joint.Target == BattleTarget.AllEnemies) Commit(null);
    }

    void ResetCombos()
    {
        comboMates.Clear(); comboWith.Clear(); jointPartner = null; cutPartner = null;
    }

#if UNITY_EDITOR
    // Test hook: plays a joint ultimate between two fighters (meters and SP topped up) on the first living enemy.
    public bool DebugJoint(string leadOrPartner, string other)
    {
        if (battle == null) return false;
        BattleCard joint = BattleCombos.Joint(leadOrPartner, other);
        if (joint == null) return false;
        foreach (BattleUnit u in battle.Allies) if (u.Id == leadOrPartner || u.Id == other) u.Ultimate = 100;
        if (battle.Sp < BattleState.UltimateSpCost) battle.Sp = BattleState.UltimateSpCost;
        BattleUnit target = joint.Target == BattleTarget.Enemy ? battle.Enemies.Find(e => battle.Exposed(e)) : null;
        return Perform(joint, battle.OwnerOf(joint), target);
    }
    public BattleUnit DebugShownMate { get { return shownMate; } }
#endif
}
