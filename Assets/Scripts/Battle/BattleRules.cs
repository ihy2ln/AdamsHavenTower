using System;
using System.Collections.Generic;

// Engine-independent port of the playable rules in core/battle.gd, core/unit.gd,
// core/card.gd, core/damage.gd and core/element_chart.gd.
public enum BattleElement { Neutral, Fire, Water, Wind, Earth, Lightning, Light, Dark }
public enum BattleRole { Tank, Dps, Ranger, Support }
public enum BattleTarget { Self, Enemy, AllEnemies, Ally, AllAllies, None }
public enum BattleCardKind { Attack, Skill, Summoner, Ultimate, Transfer, Awakening }

public sealed class BattleCard
{
    public string Id, Name, Owner, Description, Status;
    public BattleCardKind Kind = BattleCardKind.Attack;
    public BattleTarget Target = BattleTarget.Enemy;
    public BattleElement Element;
    public int Ep = 1, Ap = 1, Draw, EpGain, ApGain, Duration, Level = 1;
    public float Power, Heal, Magnitude, BondScale = 0.12f;
    public string Synergy = "";
    public float SynergyScale;
    public bool Magic, TransferEp, TransferAp;
    public string[] Partners = Array.Empty<string>();
    public float EffectivePower { get { return Power * (1f + 0.1f * (Level - 1)); } }
    public float EffectiveHeal { get { return Heal * (1f + 0.1f * (Level - 1)); } }
    public BattleCard Copy() { return (BattleCard)MemberwiseClone(); }
}

public sealed class BattleStatus
{
    public string Name;
    public float Magnitude;
    public int Turns;
}

public sealed class BattleUnit
{
    public string Id, Name, Species, Art;
    public BattleElement Element;
    public BattleRole Role;
    public bool Enemy, InnateTaunt, Moved, AwakeningReady;
    public int Lane, MaxHp, Hp, Ap = 1, Ep = 3, MaxAp = 1, MaxEp = 3;
    public int Ultimate, Stress, CollapseRounds;
    // Elite affix (Vampiric, Thorned, Hasted, Shielded, Enraged) and lair-boss phase (1, then 2 below half health).
    public string Affix = "";
    public bool Boss;
    public int Phase;
    public float Attack, Magic, Defense, Resistance, Speed, CritRate = 0.1f, CritDamage = 1.75f;
    public readonly List<BattleStatus> Statuses = new List<BattleStatus>();
    public bool Alive { get { return Hp > 0; } }
    public bool Taunting { get { return InnateTaunt || HasStatus("Taunt"); } }

    public bool HasStatus(string name)
    {
        for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Name == name) return true;
        return false;
    }

    public float StatusValue(string name)
    {
        for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Name == name) return Statuses[i].Magnitude;
        return 0f;
    }

    public void ApplyStatus(string name, float magnitude, int turns)
    {
        if (string.IsNullOrEmpty(name) || turns <= 0) return;
        for (int i = 0; i < Statuses.Count; i++)
        {
            if (Statuses[i].Name != name) continue;
            Statuses[i].Magnitude = magnitude;
            Statuses[i].Turns = turns;
            return;
        }
        Statuses.Add(new BattleStatus { Name = name, Magnitude = magnitude, Turns = turns });
    }

    public float AttackMultiplier { get { return Math.Max(0.1f, 1f + StatusValue("AttackUp") - StatusValue("AttackDown")); } }
    public float DefenseMultiplier { get { return Math.Max(0.1f, 1f + StatusValue("DefenseUp") - StatusValue("DefenseDown") + (HasStatus("IceCounter") ? 0.25f : 0f)); } }
    public float EffectiveSpeed { get { return Speed * Math.Max(0.1f, 1f + StatusValue("Haste") - StatusValue("Slow")); } }
    public void Refill() { Ap = MaxAp; Ep = MaxEp; Moved = false; }
    public void HealBy(int amount) { Hp = Math.Min(MaxHp, Hp + Math.Max(0, amount)); }
    public void DamageBy(int amount) { Hp = Math.Max(0, Hp - Math.Max(0, amount)); }
    public void Charge(int amount) { Ultimate = Math.Min(100, Ultimate + amount); }
    public bool Strained(BattleCard card)
    {
        if (CollapseRounds <= 0) return false;
        if (card.Kind == BattleCardKind.Ultimate) return true;
        if (Id == "kaela") return card.Kind == BattleCardKind.Skill;
        if (Id == "ghislaine") return card.Kind == BattleCardKind.Attack && !card.Id.StartsWith("basic_", StringComparison.Ordinal);
        if (Id == "elara") return card.Magic;
        return card.Ep > 1;
    }
}

public sealed class BattleFact
{
    public string Kind, Status;
    public BattleUnit Actor, Target;
    public BattleCard Card;
    public int Amount, Hp, MaxHp;
    public float Element = 1f, Bond = 1f;
    public bool Crit, Fatal;
}

public sealed class BattleRunModifiers
{
    public string Relic = "";
    public bool Scout, Fortify;
}

public sealed class BattleState
{
    public const int FieldMax = 3, ReserveMax = 3, EnemyMax = 6;
    public const int AllyHandMax = 8, SummonerHandMax = 3, SpMax = 10, UltimateSpCost = 3;
    public readonly List<BattleUnit> Allies = new List<BattleUnit>();
    public readonly List<BattleUnit> Reserves = new List<BattleUnit>();
    public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
    public readonly List<BattleCard> DrawPile = new List<BattleCard>();
    public readonly List<BattleCard> Hand = new List<BattleCard>();
    public readonly List<BattleCard> Discard = new List<BattleCard>();
    public readonly List<string> Log = new List<string>();
    public readonly List<BattleFact> Facts = new List<BattleFact>();
    public readonly Dictionary<string, BattleCard> Intents = new Dictionary<string, BattleCard>();
    public readonly Dictionary<string, BattleUnit> IntentTargets = new Dictionary<string, BattleUnit>();
    public readonly Dictionary<string, int> BondRanks;
    public readonly BattleRunModifiers RunModifiers;
    public BattleUnit Summoner, EnemySummoner;
    public int Round, Sp, Cp, EnemySp, EnemyCp;
    // Counts every fact ever recorded; Facts itself is capped, so presentation reads the tail by serial.
    public int FactSerial;
    public bool Finished, Victory, Withdrawn;
    public string LastMessage = "";
    private readonly Random rng;
    private bool relicSpent;
    private float runPlayMultiplier = 1f;

    public BattleState(int seed, IEnumerable<BattleUnit> allies, IEnumerable<BattleUnit> reserves,
        IEnumerable<BattleUnit> enemies, IEnumerable<BattleCard> deck, BattleUnit summoner, BattleUnit enemySummoner = null,
        Dictionary<string, int> bondRanks = null, BattleRunModifiers runModifiers = null)
    {
        rng = new Random(seed);
        BondRanks = bondRanks ?? new Dictionary<string, int>();
        RunModifiers = runModifiers ?? new BattleRunModifiers();
        foreach (BattleUnit unit in allies) if (Allies.Count < FieldMax) Allies.Add(unit);
        foreach (BattleUnit unit in reserves) if (Reserves.Count < ReserveMax) Reserves.Add(unit);
        foreach (BattleUnit unit in enemies) if (Enemies.Count < EnemyMax) Enemies.Add(unit);
        for (int i = 0; i < Allies.Count; i++) Allies[i].Lane = i;
        for (int i = 0; i < Enemies.Count; i++) Enemies[i].Lane = i;
        foreach (BattleCard card in deck) DrawPile.Add(card.Copy());
        Shuffle(DrawPile);
        Summoner = summoner;
        EnemySummoner = enemySummoner;
        StartRound();
    }

    private void Say(string line)
    {
        LastMessage = line;
        Log.Add(line);
        if (Log.Count > 80) Log.RemoveAt(0);
    }

    private void Shuffle(List<BattleCard> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            BattleCard temp = list[i]; list[i] = list[j]; list[j] = temp;
        }
    }

    private void StartRound()
    {
        Round++;
        // Stun and Slow bite on the round after they land, so they are read before durations tick down.
        var stunned = new HashSet<BattleUnit>();
        var slowed = new HashSet<BattleUnit>();
        foreach (BattleUnit unit in BothSides())
        {
            if (!unit.Alive) continue;
            if (unit.HasStatus("Stun")) stunned.Add(unit);
            if (unit.HasStatus("Slow")) slowed.Add(unit);
            for (int i = unit.Statuses.Count - 1; i >= 0; i--)
            {
                BattleStatus status = unit.Statuses[i];
                if (status.Name == "Poison" || status.Name == "Burn") ApplyTick(unit, "poison", Math.Max(1, (int)status.Magnitude));
                if (status.Name == "Regen") ApplyTick(unit, "regen", Math.Max(1, (int)status.Magnitude));
                status.Turns--;
                if (status.Turns <= 0) { unit.Statuses.RemoveAt(i); Say(unit.Name + ": " + status.Name + " expired"); }
            }
        }
        foreach (BattleUnit unit in BothSides())
        {
            if (!unit.Alive) continue;
            unit.Refill();
            if (stunned.Contains(unit)) { unit.Ap = 0; Say(unit.Name + " is stunned and loses the round."); }
            if (slowed.Contains(unit)) unit.Ep = Math.Max(0, unit.Ep - 1);
            if (!unit.Enemy && unit.CollapseRounds > 0)
            {
                unit.CollapseRounds--;
                if (unit.CollapseRounds == 0)
                {
                    unit.Stress = Math.Min(unit.Stress, 40);
                    unit.AwakeningReady = true;
                    Say(unit.Name + " recovers and gains an awakening skill.");
                }
            }
        }
        if (Round == 1 && Allies.Count > 0 && Allies[0].Alive)
        {
            if (RunModifiers.Scout) Allies[0].Ep++;
            if (RunModifiers.Fortify) Allies[0].ApplyStatus("DefenseUp", .20f, 1);
        }
        Sp = Math.Min(SpMax, Sp + 1);
        EnemySp = Math.Min(SpMax, EnemySp + 1);
        Cp = 2; EnemyCp = 2;
        EnsureWall(Enemies);
        RollIntents();
        DrawCards(1);
        for (int i = 0; i < Allies.Count; i++) if (Allies[i].Alive) DrawCards(Round == 1 ? 2 : 1);
        CheckEnd();
        if (!Finished) Say("Round " + Round + ": choose a card, basic attack, or ultimate.");
    }

    private IEnumerable<BattleUnit> BothSides()
    {
        foreach (BattleUnit unit in Allies) yield return unit;
        foreach (BattleUnit unit in Enemies) yield return unit;
    }

    private static void EnsureWall(List<BattleUnit> side)
    {
        for (int i = 0; i < side.Count; i++) if (side[i].Alive && side[i].Taunting) return;
        BattleUnit wall = null;
        for (int i = 0; i < side.Count; i++)
            if (side[i].Alive && (wall == null || side[i].MaxHp > wall.MaxHp)) wall = side[i];
        if (wall != null) wall.InnateTaunt = true;
    }

    private bool CanDraw(BattleCard card)
    {
        if (card.Kind == BattleCardKind.Summoner) return CountHand(true) < SummonerHandMax;
        if (CountHand(false) >= AllyHandMax) return false;
        if (string.IsNullOrEmpty(card.Owner)) return true;
        for (int i = 0; i < Allies.Count; i++) if (Allies[i].Id == card.Owner && Allies[i].Alive) return true;
        return false;
    }

    private int CountHand(bool summoner)
    {
        int count = 0;
        for (int i = 0; i < Hand.Count; i++) if ((Hand[i].Kind == BattleCardKind.Summoner) == summoner) count++;
        return count;
    }

    public int DrawCards(int count)
    {
        int gained = 0;
        for (int n = 0; n < count; n++)
        {
            bool got = false;
            for (int tries = 0; tries < 20; tries++)
            {
                if (DrawPile.Count == 0)
                {
                    if (Discard.Count == 0) break;
                    DrawPile.AddRange(Discard); Discard.Clear(); Shuffle(DrawPile); Say("The deck is reshuffled.");
                }
                BattleCard card = DrawPile[DrawPile.Count - 1]; DrawPile.RemoveAt(DrawPile.Count - 1);
                if (CanDraw(card)) { Hand.Add(card); gained++; got = true; break; }
                Discard.Add(card);
            }
            if (!got) break;
        }
        return gained;
    }

    public BattleUnit OwnerOf(BattleCard card)
    {
        if (card == null || card.Kind == BattleCardKind.Summoner) return null;
        for (int i = 0; i < Allies.Count; i++) if (Allies[i].Id == card.Owner) return Allies[i];
        return null;
    }

    public bool CanPay(BattleCard card, BattleUnit actor)
    {
        if (Finished || card == null) return false;
        if (card.Kind == BattleCardKind.Summoner) return Summoner != null && Summoner.Alive && Cp >= Math.Max(1, card.Ap);
        if (actor == null || !actor.Alive || actor.Enemy || actor.Strained(card)) return false;
        if (!string.IsNullOrEmpty(card.Owner) && actor.Id != card.Owner) return false;
        if (card.Kind == BattleCardKind.Awakening) return actor.AwakeningReady && actor.CollapseRounds == 0 && Allies.Contains(actor);
        if (card.Kind == BattleCardKind.Ultimate) return actor.Ultimate >= 100 && Sp >= UltimateSpCost;
        return actor.Ap >= card.Ap && actor.Ep >= card.Ep;
    }

    public bool IsTarget(BattleCard card, BattleUnit actor, BattleUnit target)
    {
        if (card == null) return false;
        if (target == null) return card.Target == BattleTarget.AllEnemies || card.Target == BattleTarget.AllAllies || card.Target == BattleTarget.None;
        if (!target.Alive || Reserves.Contains(target)) return false;
        bool actorEnemy = actor != null && actor.Enemy;
        bool same = target.Enemy == actorEnemy;
        if (card.Target == BattleTarget.Self) return target == actor;
        if (card.Target == BattleTarget.Ally) return same && card.Power <= 0;
        if (card.Target == BattleTarget.Enemy) return !same && Exposed(target);
        return false;
    }

    public bool Exposed(BattleUnit target)
    {
        if (target == null || !target.Alive) return false;
        if (target == Summoner || target == EnemySummoner) return true;
        List<BattleUnit> side = target.Enemy ? Enemies : Allies;
        if (!side.Contains(target)) return false;
        if (target.Taunting) return true;
        for (int i = 0; i < side.Count; i++) if (side[i].Alive && side[i].Taunting) return false;
        return true;
    }

    public bool TryPlay(BattleCard card, BattleUnit target)
    {
        BattleUnit actor = OwnerOf(card);
        if (!CanPay(card, actor) || !IsTarget(card, actor, target)) return false;
        if (card.Kind == BattleCardKind.Summoner) Cp -= Math.Max(1, card.Ap);
        else if (card.Kind == BattleCardKind.Ultimate) { Sp -= UltimateSpCost; actor.Ultimate = 0; }
        else if (card.Kind == BattleCardKind.Awakening) actor.AwakeningReady = false;
        else { actor.Ap -= card.Ap; actor.Ep -= card.Ep; actor.Charge(18); }
        Say((actor == null ? Summoner.Name : actor.Name) + " uses " + card.Name + ".");
        Resolve(card, actor, target);
        if (Hand.Remove(card)) Discard.Add(card);
        CheckEnd();
        return true;
    }

    public bool TryBasic(BattleUnit actor, BattleUnit target)
    {
        if (actor == null) return false;
        return TryPlay(BattleCatalog.Basic(actor), target);
    }

    public bool TryUltimate(BattleUnit actor, int choice, BattleUnit target)
    {
        List<BattleCard> options = BattleCatalog.Ultimates(actor);
        if (choice < 0 || choice >= options.Count) return false;
        return TryPlay(options[choice], target);
    }

    public bool TryAwakening(BattleUnit actor)
    {
        BattleCard card = BattleCatalog.Awakening(actor);
        return card != null && TryPlay(card, null);
    }

    public bool TrySummonerUltimate()
    {
        List<BattleCard> options = BattleCatalog.SummonerUltimates();
        if (Finished || Sp < SpMax || Summoner == null || !Summoner.Alive) return false;
        Sp = 0;
        Say(Summoner.Name + " issues a rallying and sundering decree.");
        foreach (BattleCard card in options) Resolve(card, null, null);
        CheckEnd();
        return true;
    }

    public bool TryMove(BattleUnit unit, int lane)
    {
        if (Finished || unit == null || !Allies.Contains(unit) || !unit.Alive || unit.Ap <= 0 || lane < 0 || lane >= FieldMax || unit.Lane == lane) return false;
        BattleUnit other = null;
        for (int i = 0; i < Allies.Count; i++) if (Allies[i] != unit && Allies[i].Lane == lane) other = Allies[i];
        if (other != null && !other.Alive) return false;
        int oldLane = unit.Lane;
        unit.Lane = lane; unit.Ap = 0; unit.Moved = true;
        if (other != null) { other.Lane = oldLane; other.Moved = true; }
        Say(other == null ? unit.Name + " moves to lane " + (lane + 1) + "." : unit.Name + " trades lanes with " + other.Name + ".");
        return true;
    }

    public bool TrySwap(BattleUnit field, BattleUnit reserve)
    {
        if (Finished || field == null || reserve == null || !Allies.Contains(field) || !Reserves.Contains(reserve) || !reserve.Alive) return false;
        bool free = !field.Alive;
        int lane = field.Lane, index = Allies.IndexOf(field), bench = Reserves.IndexOf(reserve);
        Allies[index] = reserve; Reserves[bench] = field; reserve.Lane = lane;
        for (int i = Hand.Count - 1; i >= 0; i--)
            if (Hand[i].Owner == field.Id) { DrawPile.Add(Hand[i]); Hand.RemoveAt(i); }
        Shuffle(DrawPile);
        reserve.Refill();
        if (!free) reserve.Ap = 0;
        DrawCards(2);
        Say(reserve.Name + " replaces " + field.Name + (free ? " for free." : " and spends the round."));
        CheckEnd();
        return true;
    }

    private void Resolve(BattleCard card, BattleUnit actor, BattleUnit chosen)
    {
        runPlayMultiplier = RunDamageMultiplier(card, actor);
        if (actor != null && chosen != null && card.TransferAp) { chosen.Ap += actor.Ap; actor.Ap = 0; }
        if (actor != null && chosen != null && card.TransferEp) { chosen.Ep += actor.Ep; actor.Ep = 0; }
        if (chosen != null) { chosen.Ap += card.ApGain; chosen.Ep += card.EpGain; }
        bool fullBond = FullBond(card, actor);
        if (card.Draw > 0) DrawCards(card.Draw + (fullBond ? 1 : 0));
        float bond = Bond(card, actor);
        if (runPlayMultiplier > 1f)
        {
            foreach (BattleUnit target in Targets(card, actor, chosen))
                if (target != null && target.Alive) { relicSpent = true; break; }
        }
        foreach (BattleUnit target in Targets(card, actor, chosen))
        {
            if (target == null || !target.Alive) continue;
            if (card.EffectivePower > 0 && actor != null) Deal(card, actor, target, bond, true);
            if (card.EffectiveHeal > 0)
            {
                int amount = Math.Max(1, (int)Math.Round(card.EffectiveHeal * bond * MendMultiplier(actor)));
                int before = target.Hp; target.HealBy(amount);
                target.Stress = Math.Max(0, target.Stress - Math.Max(8, amount / 3));
                Fact("heal", actor, target, card, target.Hp - before, false, 1f, bond);
            }
            if (!string.IsNullOrEmpty(card.Status))
            {
                float magnitude = card.Status == "IceCounter" ? card.Magnitude : card.Magnitude * bond * MendMultiplier(actor);
                target.ApplyStatus(card.Status, magnitude, card.Duration + (fullBond && card.Status != "IceCounter" ? 1 : 0));
                Fact("status", actor, target, card, 0, false, 1f, bond);
                Say(target.Name + " gains " + card.Status + ".");
            }
        }
    }

    private IEnumerable<BattleUnit> Targets(BattleCard card, BattleUnit actor, BattleUnit chosen)
    {
        if (card.Target == BattleTarget.Self || card.Target == BattleTarget.Ally || card.Target == BattleTarget.Enemy) { if (chosen != null) yield return chosen; yield break; }
        List<BattleUnit> side = actor != null && actor.Enemy ? Enemies : Allies;
        List<BattleUnit> foe = actor != null && actor.Enemy ? Allies : Enemies;
        if (card.Target == BattleTarget.AllAllies) for (int i = 0; i < side.Count; i++) if (side[i].Alive) yield return side[i];
        if (card.Target == BattleTarget.AllEnemies) for (int i = 0; i < foe.Count; i++) if (foe[i].Alive) yield return foe[i];
    }

    private float Bond(BattleCard card, BattleUnit actor)
    {
        float bonus = 0f;
        for (int p = 0; p < card.Partners.Length; p++)
            for (int i = 0; i < Allies.Count; i++)
                if (Allies[i].Alive && Allies[i].Id == card.Partners[p])
                {
                    int rank;
                    BondRanks.TryGetValue(BondKey(card.Owner, card.Partners[p]), out rank);
                    bonus += card.BondScale * (1f + .30f * rank);
                }
        return 1f + bonus;
    }

    private bool FullBond(BattleCard card, BattleUnit actor)
    {
        if (card.Partners.Length == 0) return false;
        for (int p = 0; p < card.Partners.Length; p++)
        {
            bool present = false;
            for (int i = 0; i < Allies.Count; i++)
                if (Allies[i].Alive && Allies[i].Id == card.Partners[p]) present = true;
            if (!present) return false;
        }
        return true;
    }

    public static string BondKey(string first, string second)
    {
        return string.CompareOrdinal(first, second) < 0 ? first + ":" + second : second + ":" + first;
    }

    private float RunDamageMultiplier(BattleCard card, BattleUnit actor)
    {
        if (card.EffectivePower <= 0 || (actor != null && actor.Enemy)) return 1f;
        if (RunModifiers.Relic != "stormglass" && RunModifiers.Relic != "ironbark") return 1f;
        bool favored = RunModifiers.Relic == "stormglass" ? card.Magic : !card.Magic;
        return favored ? relicSpent ? 1f : 1.25f : .9f;
    }

    private bool Covered(BattleUnit unit)
    {
        List<BattleUnit> side = unit.Enemy ? Enemies : Allies;
        for (int i = 0; i < side.Count; i++) if (side[i] != unit && side[i].Alive && side[i].Taunting) return true;
        return false;
    }

    private float StrikeMultiplier(BattleUnit unit)
    {
        if (unit.Role == BattleRole.Dps && !Covered(unit)) return 1.15f;
        if (unit.Role == BattleRole.Ranger && Covered(unit)) return 1.20f;
        return 1f;
    }

    private float GuardMultiplier(BattleUnit unit) { return unit.Role == BattleRole.Tank && unit.Taunting ? 1.25f : 1f; }
    private float MendMultiplier(BattleUnit unit) { return unit != null && unit.Role == BattleRole.Support && Covered(unit) ? 1.25f : 1f; }

    private static float ElementMultiplier(BattleElement attack, BattleElement defend)
    {
        if (attack == BattleElement.Neutral || defend == BattleElement.Neutral) return 1f;
        BattleElement[] beats = { BattleElement.Neutral, BattleElement.Wind, BattleElement.Fire, BattleElement.Earth, BattleElement.Lightning, BattleElement.Water, BattleElement.Dark, BattleElement.Light };
        if (beats[(int)attack] == defend) return 1.5f;
        if (beats[(int)defend] == attack) return 1f / 1.5f;
        return 1f;
    }

    // Damage before crit. Shared by Deal and PreviewDamage so the numbers on screen match what lands.
    private float BaseDamage(BattleCard card, BattleUnit actor, BattleUnit target, float bond, float runMultiplier, out float element)
    {
        element = ElementMultiplier(card.Element, target.Element);
        float attack = card.Magic ? actor.Magic : actor.Attack;
        float defense = card.Magic ? target.Resistance : target.Defense;
        float synergy = card.Synergy == "combo_marked" && target.HasStatus("DefenseDown") ? 1f + card.SynergyScale : 1f;
        float rage = actor.Affix == "Enraged" && actor.Hp * 2 <= actor.MaxHp ? 1.5f : 1f;
        float raw = Math.Max(1f, card.EffectivePower * synergy * bond * runMultiplier * attack * rage * actor.AttackMultiplier * StrikeMultiplier(actor) - defense * target.DefenseMultiplier * GuardMultiplier(target));
        return raw * element;
    }

    // Expected non-critical damage of a card, for target previews and enemy intent badges.
    public int PreviewDamage(BattleCard card, BattleUnit actor, BattleUnit target, out float element)
    {
        element = 1f;
        if (card == null || actor == null || target == null || card.EffectivePower <= 0) return 0;
        float raw = BaseDamage(card, actor, target, Bond(card, actor), RunDamageMultiplier(card, actor), out element);
        return Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
    }

    private void Deal(BattleCard card, BattleUnit actor, BattleUnit target, float bond, bool counter)
    {
        float element;
        bool crit = rng.NextDouble() < actor.CritRate;
        float raw = BaseDamage(card, actor, target, bond, runPlayMultiplier, out element);
        raw *= crit ? Math.Max(1f, actor.CritDamage) : 1f;
        int amount = Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        int shield = (int)target.StatusValue("Shield");
        if (shield > 0)
        {
            int absorbed = Math.Min(shield, amount);
            amount -= absorbed;
            if (shield - absorbed <= 0) target.Statuses.RemoveAll(s => s.Name == "Shield");
            else target.ApplyStatus("Shield", shield - absorbed, 99);
            Say(target.Name + "'s shield absorbs " + absorbed + ".");
            if (amount <= 0) { Fact("damage", actor, target, card, 0, crit, element, bond); return; }
        }
        target.DamageBy(amount);
        target.Charge(12);
        if (actor.Affix == "Vampiric" && actor.Alive)
        {
            int drained = Math.Max(1, amount * 3 / 10);
            actor.HealBy(drained);
            Fact("heal", actor, actor, card, drained, false, 1f, 1f);
        }
        if (target.Affix == "Thorned" && !card.Magic && actor.Alive && target != actor)
        {
            int thorns = Math.Max(1, amount / 5);
            actor.DamageBy(thorns);
            Fact("poison", null, actor, null, thorns, false, 1f, 1f);
            Say(actor.Name + " is cut by thorns for " + thorns + ".");
        }
        if (target.Boss && target.Phase == 1 && target.Alive && target.Hp * 2 <= target.MaxHp) EnterSecondPhase(target);
        if (!target.Enemy)
        {
            Sp = Math.Min(SpMax, Sp + 1);
            target.Stress = Math.Min(100, target.Stress + Math.Max(8, (int)Math.Round(35f * amount / Math.Max(1, target.MaxHp))));
            if (target.Stress >= 100 && target.CollapseRounds == 0)
            {
                target.CollapseRounds = 2;
                target.AwakeningReady = false;
                Say(target.Name + " enters breakdown.");
            }
        }
        Fact("damage", actor, target, card, amount, crit, element, bond);
        Say(target.Name + " takes " + amount + (element > 1.01f ? " WEAK" : element < 0.99f ? " resisted" : "") + (crit ? " CRIT" : "") + ".");
        if (counter && target.Alive && actor.Alive && target.HasStatus("IceCounter"))
        {
            float power = target.StatusValue("IceCounter");
            target.Statuses.RemoveAll(s => s.Name == "IceCounter");
            BattleCard retaliation = new BattleCard { Id = "mv_whiteout_counter", Name = "Whiteout Counter", Owner = target.Id, Element = target.Element, Power = power };
            float previousMultiplier = runPlayMultiplier;
            runPlayMultiplier = 1f;
            Deal(retaliation, target, actor, 1f, false);
            runPlayMultiplier = previousMultiplier;
        }
    }

    private void Fact(string kind, BattleUnit actor, BattleUnit target, BattleCard card, int amount, bool crit, float element, float bond)
    {
        Facts.Add(new BattleFact { Kind = kind, Actor = actor, Target = target, Card = card, Amount = amount,
            Hp = target.Hp, MaxHp = target.MaxHp, Fatal = !target.Alive, Crit = crit, Element = element, Bond = bond,
            Status = kind == "status" ? card.Status : "" });
        FactSerial++;
        if (Facts.Count > 40) Facts.RemoveAt(0);
    }

    private void ApplyTick(BattleUnit unit, string kind, int amount)
    {
        if (kind == "poison") unit.DamageBy(amount); else unit.HealBy(amount);
        Fact(kind, null, unit, null, amount, false, 1f, 1f);
        Say(unit.Name + (kind == "poison" ? " takes " : " regenerates ") + amount + " HP.");
    }

    // Below half health a lair boss shakes off its debuffs, hits harder for the rest of the fight and raises a
    // shield; from then on it charges its signature move more often.
    private void EnterSecondPhase(BattleUnit boss)
    {
        boss.Phase = 2;
        boss.Statuses.RemoveAll(s => s.Name == "AttackDown" || s.Name == "DefenseDown" || s.Name == "Slow" ||
            s.Name == "Poison" || s.Name == "Burn" || s.Name == "Stun");
        boss.ApplyStatus("AttackUp", .25f, 99);
        boss.ApplyStatus("Shield", boss.MaxHp * .12f, 99);
        Fact("status", boss, boss, new BattleCard { Id = "boss_phase", Name = "Frenzy", Status = "Frenzy" }, 0, false, 1f, 1f);
        Say(boss.Name + " flies into a frenzy!");
    }

    private void RollIntents()
    {
        Intents.Clear(); IntentTargets.Clear();
        for (int i = 0; i < Enemies.Count; i++)
        {
            BattleUnit enemy = Enemies[i];
            if (!enemy.Alive) continue;
            List<BattleCard> pool = BattleCatalog.EnemyCards(enemy);
            if (pool.Count == 0) continue;
            BattleCard card = null;
            if (enemy.Boss && enemy.HasStatus("Charging"))
            {
                // The charge-up announced last round: the signature move lands now.
                card = pool.Find(c => c.Id == "e_cataclysm");
                enemy.Statuses.RemoveAll(s => s.Name == "Charging");
            }
            if (card == null) card = ChooseEnemyCard(enemy, pool);
            if (card == null) continue;
            Intents[enemy.Id] = card;
            IntentTargets[enemy.Id] = card.Target == BattleTarget.Enemy ? EnemyVictim(card) : card.Target == BattleTarget.Self ? enemy :
                card.Target == BattleTarget.Ally ? MostHurt(Enemies) : null;
        }
    }

    // Weighted by the situation: heal a hurt friend, buff when nobody is buffed, guard when unguarded, else attack.
    private BattleCard ChooseEnemyCard(BattleUnit enemy, List<BattleCard> pool)
    {
        float total = 0f;
        var weights = new float[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            BattleCard card = pool[i];
            float w;
            if (card.Id == "e_cataclysm" || card.Ep > enemy.Ep || card.Ap > enemy.Ap) w = 0f;
            else if (card.Id == "e_gather") w = enemy.HasStatus("Charging") ? 0f : enemy.Phase >= 2 ? 2.2f : 1.1f;
            else if (card.Target == BattleTarget.Ally) { BattleUnit hurt = MostHurt(Enemies); w = hurt != null && hurt.Hp * 100 < hurt.MaxHp * 55 ? 4f : 0f; }
            else if (card.Target == BattleTarget.AllAllies && card.EffectivePower <= 0)
                w = Enemies.Exists(u => u.Alive && u.HasStatus(card.Status)) ? .3f : 2f;
            else if (card.Target == BattleTarget.Self) w = enemy.HasStatus(card.Status) ? .2f : 1.2f;
            else if (card.Target == BattleTarget.AllEnemies && card.EffectivePower <= 0) w = 1.5f;
            else w = card.Target == BattleTarget.AllEnemies ? 2.5f : 3f;
            weights[i] = w; total += w;
        }
        if (total <= 0f) return null;
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++) { roll -= weights[i]; if (roll <= 0) return pool[i]; }
        return pool[pool.Count - 1];
    }

    private static BattleUnit MostHurt(List<BattleUnit> side)
    {
        BattleUnit best = null;
        foreach (BattleUnit unit in side)
            if (unit.Alive && unit.Hp < unit.MaxHp && (best == null || unit.Hp * (long)best.MaxHp < best.Hp * (long)unit.MaxHp)) best = unit;
        return best;
    }

    // Enemies go for the weakest exposed fighter more often than not; JD is a rarer, opportunistic target.
    private BattleUnit EnemyVictim(BattleCard card)
    {
        List<BattleUnit> available = new List<BattleUnit>();
        for (int i = 0; i < Allies.Count; i++) if (Exposed(Allies[i])) available.Add(Allies[i]);
        if (Summoner != null && Summoner.Alive && card.Target == BattleTarget.Enemy && (available.Count == 0 || rng.NextDouble() < 0.12)) return Summoner;
        if (available.Count == 0) return null;
        if (rng.NextDouble() < 0.55)
        {
            BattleUnit weakest = available[0];
            foreach (BattleUnit unit in available) if (unit.Hp * (long)weakest.MaxHp < weakest.Hp * (long)unit.MaxHp) weakest = unit;
            return weakest;
        }
        return available[rng.Next(available.Count)];
    }

    public void EndTurn()
    {
        if (Finished) return;
        List<BattleUnit> ordered = new List<BattleUnit>(Enemies);
        ordered.Sort((a, b) => b.EffectiveSpeed.CompareTo(a.EffectiveSpeed));
        for (int i = 0; i < ordered.Count; i++)
        {
            BattleUnit enemy = ordered[i];
            if (!enemy.Alive) continue;
            BattleCard card;
            if (!Intents.TryGetValue(enemy.Id, out card)) continue;
            BattleUnit target = IntentTargets[enemy.Id];
            // Hasted elites have two actions and repeat their move while they can pay for it.
            for (int act = 0; act < 2 && enemy.Alive && enemy.Ap >= card.Ap && enemy.Ep >= card.Ep; act++)
            {
                if (act > 0 && enemy.Affix != "Hasted") break;
                if (card.Target == BattleTarget.Enemy && (target == null || !target.Alive || !Exposed(target))) target = EnemyVictim(card);
                if (card.Target == BattleTarget.Ally && (target == null || !target.Alive)) target = MostHurt(Enemies);
                if ((card.Target == BattleTarget.Enemy || card.Target == BattleTarget.Self || card.Target == BattleTarget.Ally) && target == null) break;
                enemy.Ap -= card.Ap; enemy.Ep -= card.Ep;
                Say(enemy.Name + " uses " + card.Name + ".");
                Resolve(card, enemy, target);
                CheckEnd();
                if (Finished) return;
            }
        }
        if (EnemySummoner != null && EnemySummoner.Alive)
        {
            if (EnemySp >= SpMax)
            {
                EnemySp = 0;
                foreach (BattleUnit enemy in Enemies) if (enemy.Alive) enemy.ApplyStatus("AttackUp", 0.4f, 3);
                foreach (BattleUnit ally in Allies) if (ally.Alive) ally.ApplyStatus("DefenseDown", 0.3f, 3);
                Say("The enemy commander empowers the line.");
            }
            else if (EnemyCp > 0)
            {
                List<BattleCard> pool = BattleCatalog.EnemyCommanderCards();
                BattleCard support = pool[rng.Next(pool.Count)];
                EnemyCp -= Math.Max(1, support.Ap);
                Say("Enemy commander uses " + support.Name + ".");
                Resolve(support, EnemySummoner, null);
            }
        }
        StartRound();
    }

    private void CheckEnd()
    {
        if (Finished) return;
        if (Summoner != null && !Summoner.Alive) { Finished = true; Say("Defeat: your Summoner has fallen."); return; }
        if (EnemySummoner != null && !EnemySummoner.Alive) { Finished = Victory = true; Say("Victory: enemy commander defeated."); return; }
        bool partyAlive = false;
        foreach (BattleUnit unit in Allies) partyAlive |= unit.Alive;
        foreach (BattleUnit unit in Reserves) partyAlive |= unit.Alive;
        if (!partyAlive) { Finished = true; Say("Defeat: the party has fallen."); return; }
        bool enemiesAlive = false;
        foreach (BattleUnit unit in Enemies) enemiesAlive |= unit.Alive;
        if (!enemiesAlive && EnemySummoner == null) { Finished = Victory = true; Say("Victory: Silverwood grove cleansed."); }
    }

    public void Forfeit() { if (Finished) return; Finished = Withdrawn = true; Say("Withdrawn from battle."); }
}
