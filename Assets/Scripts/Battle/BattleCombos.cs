using System;
using System.Collections.Generic;

// Combos for every bonded pair (CM 10.3.4; the pairs are BattleCatalog's bond table).
//  - Combo action: a team-up (a fighter plays a card while a bond partner holds the line or has already acted this
//    turn) is followed by the pair's named combo, performed by the partner. In classic battles too.
//  - Joint ultimate: when two bonded fighters on the field both have a full meter, either one's ULT menu offers the
//    pair's joint ultimate. It spends both meters and one ultimate's SP and hits about 1.35x a solo ultimate.
public static class BattleCombos
{
    sealed class Pair
    {
        public string A, B;
        // The combo action: performed by whichever of the two did not play the card. Look = the performer's own move
        // whose clip and effect layers the combo borrows (moves.json), keyed by fighter id.
        public string ComboName;
        public BattleTarget ComboTarget;
        public float ComboPower, ComboHeal, ComboMagnitude;
        public string ComboStatus = "";
        public int ComboDuration;
        public Dictionary<string, string> Look;
        // The joint ultimate, played by Lead (its stats and its clip action).
        public string JointName, Lead;
        public BattleTarget JointTarget;
        public float JointPower, JointHeal, JointMagnitude;
        public string JointStatus = "";
        public int JointDuration;
        public bool JointMagic;
    }

    static Dictionary<string, string> L(string a, string lookA, string b, string lookB)
    {
        return new Dictionary<string, string> { { a, lookA }, { b, lookB } };
    }

    static readonly Pair[] Pairs =
    {
        new Pair { A = "ghislaine", B = "kaela",
            ComboName = "Frostfang Rend", ComboTarget = BattleTarget.Enemy, ComboPower = 1.5f, ComboStatus = "DefenseDown", ComboMagnitude = .25f, ComboDuration = 2,
            Look = L("ghislaine", "gh_tiger_riposte", "kaela", "mv_shatter_hook"),
            JointName = "Glacier Tiger Rend", Lead = "ghislaine", JointTarget = BattleTarget.Enemy, JointPower = 6.2f, JointStatus = "DefenseDown", JointMagnitude = .4f, JointDuration = 2 },
        new Pair { A = "elara", B = "kaela",
            ComboName = "Thunderfrost Lance", ComboTarget = BattleTarget.Enemy, ComboPower = 1.4f, ComboStatus = "Slow", ComboMagnitude = .25f, ComboDuration = 2,
            Look = L("elara", "el_piercing_shot", "kaela", "mv_frost_jab"),
            JointName = "Thunderfrost Cataclysm", Lead = "elara", JointTarget = BattleTarget.AllEnemies, JointPower = 3.9f, JointMagic = true, JointStatus = "Slow", JointMagnitude = .3f, JointDuration = 2 },
        new Pair { A = "daisy", B = "ghislaine",
            ComboName = "Twin Ember Rush", ComboTarget = BattleTarget.AllEnemies, ComboPower = 1f,
            Look = L("daisy", "da_wand_sweep", "ghislaine", "gh_fireline_cut"),
            JointName = "Twin Flame Apocalypse", Lead = "daisy", JointTarget = BattleTarget.AllEnemies, JointPower = 4.2f, JointStatus = "DefenseDown", JointMagnitude = .35f, JointDuration = 2 },
        new Pair { A = "elara", B = "helda",
            ComboName = "Storm Hearth", ComboTarget = BattleTarget.AllEnemies, ComboPower = .9f, ComboStatus = "AttackDown", ComboMagnitude = .2f, ComboDuration = 2,
            Look = L("elara", "el_storm_tally", "helda", "he_chilling_touch"),
            JointName = "Aurora Tempest", Lead = "elara", JointTarget = BattleTarget.AllEnemies, JointPower = 3.6f, JointMagic = true, JointStatus = "DefenseDown", JointMagnitude = .3f, JointDuration = 2 },
        new Pair { A = "clarity", B = "elara",
            ComboName = "Radiant Arc", ComboTarget = BattleTarget.Enemy, ComboPower = 1.5f,
            Look = L("clarity", "cy_smite", "elara", "el_arc_bolt"),
            JointName = "Prism Judgment", Lead = "elara", JointTarget = BattleTarget.Enemy, JointPower = 6.4f, JointMagic = true, JointStatus = "DefenseDown", JointMagnitude = .4f, JointDuration = 2 },
        new Pair { A = "clarity", B = "helda",
            ComboName = "Mending Light", ComboTarget = BattleTarget.AllAllies, ComboHeal = 16f, ComboStatus = "Regen", ComboMagnitude = 6f, ComboDuration = 2,
            Look = L("clarity", "cy_uplift", "helda", "he_frosted_ward"),
            JointName = "Hearthlight Sanctum", Lead = "helda", JointTarget = BattleTarget.AllAllies, JointHeal = 70f, JointStatus = "Regen", JointMagnitude = 12f, JointDuration = 3 },
        new Pair { A = "clarity", B = "daisy",
            ComboName = "Sunfire Strike", ComboTarget = BattleTarget.Enemy, ComboPower = 1.6f, ComboStatus = "DefenseDown", ComboMagnitude = .2f, ComboDuration = 2,
            Look = L("clarity", "cy_radiant_palm", "daisy", "da_spearflame_rush"),
            JointName = "Sunflare Heaven", Lead = "daisy", JointTarget = BattleTarget.Enemy, JointPower = 6.6f, JointStatus = "DefenseDown", JointMagnitude = .4f, JointDuration = 2 },
    };

    public const string ComboPrefix = "combo_", JointPrefix = "ult_joint_";
    // Off: team-ups stay plain (balance comparisons).
    public static bool CombosOn = true;
    // A combo is a free extra action on top of the card: it hits and heals at this share of the table's numbers
    // (tuned against ExpeditionBalanceTests, whose lair bosses fell under their floor at full strength).
    public const float ComboScale = .65f;

    static Pair Find(string a, string b)
    {
        foreach (var p in Pairs) if ((p.A == a && p.B == b) || (p.A == b && p.B == a)) return p;
        return null;
    }
    static Pair FindById(string id, string prefix)
    {
        if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal)) return null;
        foreach (var p in Pairs) if (id == prefix + p.A + "_" + p.B) return p;
        return null;
    }

    public static bool IsCombo(BattleCard card) { return card != null && card.Id != null && card.Id.StartsWith(ComboPrefix, StringComparison.Ordinal); }
    public static bool IsJoint(BattleCard card) { return card != null && card.Id != null && card.Id.StartsWith(JointPrefix, StringComparison.Ordinal); }

    // Every bonded pair, as "a_b" (the ids the combo and joint cards carry).
    public static IEnumerable<string> PairKeys() { foreach (var p in Pairs) yield return p.A + "_" + p.B; }

    // The combo the partner performs after the actor's team-up card, or null when the two have none.
    public static BattleCard Action(BattleUnit actor, BattleUnit partner)
    {
        if (actor == null || partner == null) return null;
        Pair p = Find(actor.Id, partner.Id);
        if (p == null) return null;
        return new BattleCard
        {
            Id = ComboPrefix + p.A + "_" + p.B, Name = p.ComboName, Owner = partner.Id, Element = partner.Element,
            Kind = p.ComboPower > 0 ? BattleCardKind.Attack : BattleCardKind.Skill, Target = p.ComboTarget, Ap = 0, Ep = 0,
            Power = p.ComboPower * ComboScale, Heal = p.ComboHeal * ComboScale, Status = p.ComboStatus, Magnitude = p.ComboMagnitude, Duration = p.ComboDuration,
            Magic = partner.Magic > partner.Attack, Partners = new[] { actor.Id },
            Description = "Combo with " + actor.Name.Split(' ')[0] + ".",
        };
    }

    // The pair's joint ultimate, owned by its lead.
    public static BattleCard Joint(string a, string b)
    {
        Pair p = Find(a, b);
        if (p == null) return null;
        string other = p.Lead == p.A ? p.B : p.A;
        return new BattleCard
        {
            Id = JointPrefix + p.A + "_" + p.B, Name = p.JointName, Owner = p.Lead, Kind = BattleCardKind.Ultimate,
            Element = BattleCatalog.Party().Find(u => u.Id == p.Lead).Element, Target = p.JointTarget, Ap = 0, Ep = 0,
            Power = p.JointPower, Heal = p.JointHeal, Status = p.JointStatus, Magnitude = p.JointMagnitude, Duration = p.JointDuration,
            Magic = p.JointMagic, Partners = new[] { other }, Description = "Joint ultimate.",
        };
    }

    // The other fighter of a joint ultimate (its owner is the lead).
    public static string JointPartner(BattleCard joint)
    {
        Pair p = joint != null ? FindById(joint.Id, JointPrefix) : null;
        return p == null ? null : p.Lead == p.A ? p.B : p.A;
    }

    // The performer's own move a combo borrows its clip and effect layers from.
    public static string LookFor(BattleCard combo)
    {
        Pair p = combo != null ? FindById(combo.Id, ComboPrefix) : null;
        string look;
        return p != null && combo.Owner != null && p.Look.TryGetValue(combo.Owner, out look) ? look : null;
    }

    // The other fighter of a combo (its owner is the performer).
    public static string ComboPartner(BattleCard combo)
    {
        Pair p = combo != null ? FindById(combo.Id, ComboPrefix) : null;
        return p == null ? null : p.A == combo.Owner ? p.B : p.A;
    }
}

public sealed partial class BattleState
{
    // The combo that followed the last card played, or null.
    public BattleCard LastCombo;

    // Will this team-up card be followed by a combo?
    private bool ComboFollows(BattleCard card, BattleUnit actor, BattleUnit partner)
    {
        return BattleCombos.CombosOn && card != null && partner != null && card.Kind != BattleCardKind.Ultimate
            && card.Kind != BattleCardKind.Awakening && card.Kind != BattleCardKind.Summoner && BattleCombos.Action(actor, partner) != null;
    }

    // TryPlay, after a team-up card resolved: the partner follows with the pair's combo.
    private void ComboFollowUp(BattleCard card, BattleUnit actor, BattleUnit partner, BattleUnit chosen)
    {
        LastCombo = null;
        if (!BattleCombos.CombosOn || card == null || actor == null || partner == null || !partner.Alive || Finished) return;
        if (card.Kind == BattleCardKind.Ultimate || card.Kind == BattleCardKind.Awakening || card.Kind == BattleCardKind.Summoner) return;
        BattleCard combo = BattleCombos.Action(actor, partner);
        if (combo == null) return;
        BattleUnit target = null;
        if (combo.Target == BattleTarget.Enemy)
        {
            target = chosen != null && chosen.Enemy && Exposed(chosen) ? chosen : Enemies.Find(e => Exposed(e));
            if (target == null) return;
        }
        else if (combo.Target == BattleTarget.AllEnemies && !Enemies.Exists(e => e.Alive)) return;
        LastCombo = combo;
        Fact("combo", partner, actor, combo, 0, false, 1f, 1f);
        Say(partner.Name + " follows up: " + combo.Name + "!");
        Resolve(combo, partner, target);
    }

    // The joint ultimates this fighter can play now (both meters full, both on the field, the SP to pay).
    public List<BattleCard> JointUltimates(BattleUnit unit)
    {
        var list = new List<BattleCard>();
        if (unit == null || !Allies.Contains(unit)) return list;
        foreach (BattleUnit other in Allies)
        {
            if (other == unit) continue;
            BattleCard joint = BattleCombos.Joint(unit.Id, other.Id);
            if (joint != null && CanJoint(joint)) list.Add(joint);
        }
        return list;
    }

    public bool CanJoint(BattleCard joint)
    {
        if (Finished || joint == null) return false;
        BattleUnit lead = OwnerOf(joint);
        string partnerId = BattleCombos.JointPartner(joint);
        BattleUnit partner = Allies.Find(u => u.Id == partnerId);
        if (lead == null || partner == null) return false;
        foreach (BattleUnit u in new[] { lead, partner })
            if (!u.Alive || u.Ultimate < 100 || u.Strained(joint)) return false;
        return Sp >= UltCost(lead);
    }

    public bool TryJointUltimate(BattleCard joint, BattleUnit target)
    {
        if (!CanJoint(joint)) return false;
        BattleUnit lead = OwnerOf(joint);
        BattleUnit partner = Allies.Find(u => u.Id == BattleCombos.JointPartner(joint));
        if (!IsTarget(joint, lead, target)) return false;
        Sp -= UltCost(lead);
        lead.Ultimate = 0; partner.Ultimate = 0;
        Say(lead.Name + " and " + partner.Name + " unleash " + joint.Name + "!");
        Fact("joint", lead, partner, joint, 0, false, 1f, 1f);
        Resolve(joint, lead, target);
        if (SummonMode) { AfterSummon(joint, lead, target); ActedThisTurn.Remove(partner); ActedThisTurn.Add(partner); }
        CheckEnd();
        return true;
    }
}
