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
    // Chaos Zero Nightmare keywords. Retain: stays in hand at the end of the turn (others are discarded).
    // Exhaust: leaves the battle once played. Initiation: always in the opening hand.
    public bool Retain, Exhaust, Initiation;
    // A shield the card also gives its user (epiphany "Bulwark"), and the epiphanies applied to it ("swift,echo").
    public float SelfShield;
    public string Epiphany = "";
    // Each card level above 1 (expedition upgrades) adds LevelStep power and healing.
    public const float LevelStep = 0.2f;
    public float EffectivePower { get { return Power * (1f + LevelStep * (Level - 1)); } }
    public float EffectiveHeal { get { return Heal * (1f + LevelStep * (Level - 1)); } }
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
    // Bestiary rank of an enemy, 1 (F) .. 9 (SSR); 0 for heroes and pre-bestiary units.
    public int Rank;
    // Enemies: the action count (each card the party plays lowers it; at 0 the enemy acts at once, mid-turn) and
    // tenacity (party hits wear it down; at 0 the enemy BREAKS). Both refill every round.
    public int ActionCount, ActionMax, Tenacity, MaxTenacity;
    public bool ActedThisRound;
    // Fighters: a reserve's once-per-battle partner assist is spent; recovered from breakdown (cheaper ultimate).
    public bool PartnerUsed, Overcame;
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
            // Shields stack; every other status is refreshed.
            Statuses[i].Magnitude = name == "Shield" ? Statuses[i].Magnitude + magnitude : magnitude;
            Statuses[i].Turns = name == "Shield" ? Math.Max(Statuses[i].Turns, turns) : turns;
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
    // Expedition relics: extra damage on every party attack, extra crit chance, and how fast stress builds.
    public float DamageBonus, CritBonus, StressScale = 1f;
}

public sealed class BattleState
{
    public const int FieldMax = 3, ReserveMax = 3, EnemyMax = 6;
    public const int AllyHandMax = 10, SummonerHandMax = 3, SpMax = 10, UltimateSpCost = 3;
    // Fighter cards drawn to at the start of every round (every unplayed card is discarded at the end of the turn
    // unless it Retains), and the SP a reserve partner's assist costs.
    public const int HandSize = 5, PartnerSpCost = 2;
    public readonly List<BattleUnit> Allies = new List<BattleUnit>();
    public readonly List<BattleUnit> Reserves = new List<BattleUnit>();
    public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
    public readonly List<BattleCard> DrawPile = new List<BattleCard>();
    public readonly List<BattleCard> Hand = new List<BattleCard>();
    public readonly List<BattleCard> Discard = new List<BattleCard>();
    public readonly List<BattleCard> Exhausted = new List<BattleCard>();
    // Cards played this turn (the CHAIN counter) and how many enemy actions interrupted the party this battle.
    public int CardsThisTurn, Interrupts;
    // Epiphany: the card that glows this battle (playing it offers 1 of 3 upgrades), the choice waiting to be made,
    // and every epiphany taken (card id -> "swift,echo") so the expedition can keep them for the run.
    public string GlowCard = "";
    public BattleCard EpiphanyCard;
    public readonly List<string> EpiphanyOptions = new List<string>();
    public readonly Dictionary<string, string> Epiphanies = new Dictionary<string, string>();
    public readonly List<string> Log = new List<string>();
    public readonly List<BattleFact> Facts = new List<BattleFact>();
    public readonly Dictionary<string, BattleCard> Intents = new Dictionary<string, BattleCard>();
    public readonly Dictionary<string, BattleUnit> IntentTargets = new Dictionary<string, BattleUnit>();
    public readonly Dictionary<string, int> BondRanks;
    public readonly BattleRunModifiers RunModifiers;
    public BattleUnit Summoner, EnemySummoner;
    public int Round, Sp, Cp, EnemySp, EnemyCp;
    // ---- summon battles (expeditions) --------------------------------------------------------------------------
    // The fighters are JD's contracts, not bodies on the field: playing a fighter's card summons them to act, and
    // they leave again. A card that anchors (a self shield or Guard, Taunt, a counter stance, a defense buff, a
    // charge) keeps its fighter out as the Vanguard, one at a time, until that hold runs out. Enemy hits land on the
    // Vanguard, else on JD. A card whose bond partner already acted this turn (or holds the line) is a team-up.
    public readonly bool SummonMode;
    public BattleUnit Vanguard;
    public readonly List<BattleUnit> ActedThisTurn = new List<BattleUnit>();
    // Fighters already used as a team-up partner this turn: each one joins one team-up per turn.
    public readonly List<BattleUnit> TeamedThisTurn = new List<BattleUnit>();
    public BattleUnit LastTeamUp;
    public static float TeamUpPower = 1.25f;
    // Summon battle tuning (balance sim): JD's health as a multiple of the base summoner (JD takes every hit no
    // Vanguard catches), and the enemies' damage (their blows no longer spread over three bodies). Lair bosses get
    // their own factor: their charged area blow lands whole on JD and the Vanguard.
    public static float SummonJdVitality = 1.6f, SummonEnemyPower = 1.6f, SummonBossPower = 1.05f;
    private static readonly string[] AnchorStatuses = { "Taunt", "Shield", "IceCounter", "DefenseUp", "Charging" };
    private float teamUpMultiplier = 1f;
    // Summon battles: AP and EP are JD's, one pool the whole contract draws on (the field fighters' maxima added up).
    // A fighter's own AP only says whether they can be called this round (a stun or a paid swap empties it).
    public BattleUnit Wallet(BattleUnit actor) { return SummonMode && actor != null && !actor.Enemy && Summoner != null ? Summoner : actor; }
    // Counts every fact ever recorded; Facts itself is capped, so presentation reads the tail by serial.
    public int FactSerial;
    public bool Finished, Victory, Withdrawn;
    public string LastMessage = "";
    private readonly Random rng;
    private bool relicSpent;
    private float runPlayMultiplier = 1f;

    public BattleState(int seed, IEnumerable<BattleUnit> allies, IEnumerable<BattleUnit> reserves,
        IEnumerable<BattleUnit> enemies, IEnumerable<BattleCard> deck, BattleUnit summoner, BattleUnit enemySummoner = null,
        Dictionary<string, int> bondRanks = null, BattleRunModifiers runModifiers = null, bool summonMode = false)
    {
        rng = new Random(seed);
        SummonMode = summonMode;
        BondRanks = bondRanks ?? new Dictionary<string, int>();
        RunModifiers = runModifiers ?? new BattleRunModifiers();
        foreach (BattleUnit unit in allies) if (Allies.Count < FieldMax) Allies.Add(unit);
        foreach (BattleUnit unit in reserves) if (Reserves.Count < ReserveMax) Reserves.Add(unit);
        foreach (BattleUnit unit in enemies) if (Enemies.Count < EnemyMax) Enemies.Add(unit);
        for (int i = 0; i < Allies.Count; i++) Allies[i].Lane = i;
        for (int i = 0; i < Enemies.Count; i++) { Enemies[i].Lane = i; InitEnemy(Enemies[i]); }
        foreach (BattleCard card in deck) DrawPile.Add(card.Copy());
        Shuffle(DrawPile);
        // Initiation cards of the fighters on the field go on top (the pile is drawn from its end).
        var opening = DrawPile.FindAll(c => c.Initiation && OwnerOnField(c));
        foreach (BattleCard card in opening) { DrawPile.Remove(card); DrawPile.Add(card); }
        Summoner = summoner;
        EnemySummoner = enemySummoner;
        if (SummonMode && Summoner != null)
        {
            float hp = Summoner.Hp / (float)Math.Max(1, Summoner.MaxHp);
            Summoner.MaxHp = Math.Max(1, (int)Math.Round(Summoner.MaxHp * SummonJdVitality));
            Summoner.Hp = Math.Max(1, (int)Math.Round(Summoner.MaxHp * hp));
        }
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

    // Tuning (balance sim: ExpeditionBalanceTests). Tenacity for packs, guards, elites, bosses; the extra damage a
    // broken enemy takes.
    public static int[] Tenacity = { 6, 7, 9, 12 };
    public static float BreakDamage = .25f;

    // Action count by build: quick skirmishers act after 3 party cards, bruisers and guards after 5, bosses after 5
    // (4 once enraged). Tenacity: see the tuning table above.
    private static int ActionCountFor(BattleUnit u)
    {
        if (u.Boss) return u.Phase >= 2 ? 4 : 5;
        if (u.Affix == "Hasted") return 3;
        return u.Speed >= 13f ? 3 : u.Speed <= 9.5f ? 5 : 4;
    }

    private static void InitEnemy(BattleUnit u)
    {
        u.ActionMax = ActionCountFor(u);
        u.ActionCount = u.ActionMax;
        u.MaxTenacity = u.Boss ? Tenacity[3] : u.Affix.Length > 0 ? Tenacity[2] : u.Role == BattleRole.Tank ? Tenacity[1] : Tenacity[0];
        u.Tenacity = u.MaxTenacity;
    }

    private bool OwnerOnField(BattleCard card)
    {
        if (card.Kind == BattleCardKind.Summoner || string.IsNullOrEmpty(card.Owner)) return true;
        for (int i = 0; i < Allies.Count; i++) if (Allies[i].Id == card.Owner && Allies[i].Alive) return true;
        return false;
    }

    // The reserve paired with a field fighter (same slot), or null.
    public BattleUnit PartnerOf(BattleUnit fighter)
    {
        int i = Allies.IndexOf(fighter);
        return i >= 0 && i < Reserves.Count ? Reserves[i] : null;
    }

    // The field fighter a reserve partners, or null.
    public BattleUnit PartneredBy(BattleUnit reserve)
    {
        int i = Reserves.IndexOf(reserve);
        return i >= 0 && i < Allies.Count ? Allies[i] : null;
    }

    public int UltCost(BattleUnit unit) { return unit != null && unit.Overcame ? UltimateSpCost - 1 : UltimateSpCost; }

    private void StartRound()
    {
        Round++;
        CardsThisTurn = 0;
        ActedThisTurn.Clear();
        TeamedThisTurn.Clear();
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
                    unit.Overcame = true;
                    Say(unit.Name + " overcomes the breakdown: an awakening skill, and ultimates cost 1 SP less.");
                }
            }
        }
        if (SummonMode && Summoner != null)
        {
            int ap = 0, ep = 0;
            foreach (BattleUnit unit in Allies) if (unit.Alive) { ap += unit.MaxAp; ep += unit.MaxEp; }
            Summoner.MaxAp = Math.Max(1, ap); Summoner.MaxEp = Math.Max(1, ep);
            Summoner.Ap = Summoner.MaxAp; Summoner.Ep = Summoner.MaxEp;
            foreach (BattleUnit unit in Allies) if (unit.Alive && slowed.Contains(unit)) Summoner.Ep = Math.Max(0, Summoner.Ep - 1);
        }
        if (SummonMode && Vanguard != null && (!Vanguard.Alive || !Anchored(Vanguard))) Recall();
        if (Round == 1 && Allies.Count > 0 && Allies[0].Alive)
        {
            if (RunModifiers.Scout) Wallet(Allies[0]).Ep++;
            if (RunModifiers.Fortify) Allies[0].ApplyStatus("DefenseUp", .20f, 1);
        }
        Sp = Math.Min(SpMax, Sp + 1);
        EnemySp = Math.Min(SpMax, EnemySp + 1);
        Cp = 2; EnemyCp = 2;
        foreach (BattleUnit enemy in Enemies)
        {
            if (!enemy.Alive) continue;
            enemy.ActedThisRound = false;
            enemy.ActionMax = ActionCountFor(enemy);
            enemy.ActionCount = enemy.ActionMax;
            if (!enemy.HasStatus("Broken")) enemy.Tenacity = enemy.MaxTenacity;
        }
        EnsureWall(Enemies);
        RollIntents();
        RefillHand();
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

    // Draws fighter cards until HandSize of them are in hand. JD's support cards met on the way join the hand too
    // (up to three); cards whose owner is benched or down are set aside.
    private void RefillHand()
    {
        int misses = 0;
        while (CountHand(false) < HandSize)
        {
            if (DrawPile.Count == 0)
            {
                if (Discard.Count == 0) break;
                DrawPile.AddRange(Discard); Discard.Clear(); Shuffle(DrawPile); Say("The deck is reshuffled.");
            }
            BattleCard card = DrawPile[DrawPile.Count - 1]; DrawPile.RemoveAt(DrawPile.Count - 1);
            if (CanDraw(card)) { Hand.Add(card); if (card.Kind != BattleCardKind.Summoner) misses = 0; continue; }
            Discard.Add(card);
            // Nothing left that can be drawn: stop instead of reshuffling forever.
            if (++misses > DrawPile.Count + Discard.Count + 1) break;
        }
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
        if (card.Kind == BattleCardKind.Ultimate) return actor.Ultimate >= 100 && Sp >= UltCost(actor);
        if (SummonMode && actor.Ap <= 0) return false;
        BattleUnit wallet = Wallet(actor);
        return wallet.Ap >= card.Ap && wallet.Ep >= card.Ep;
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
        if (SummonMode && !target.Enemy) return target == Vanguard;
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
        else if (card.Kind == BattleCardKind.Ultimate) { Sp -= UltCost(actor); actor.Ultimate = 0; }
        else if (card.Kind == BattleCardKind.Awakening) actor.AwakeningReady = false;
        else { BattleUnit wallet = Wallet(actor); wallet.Ap -= card.Ap; wallet.Ep -= card.Ep; actor.Charge(18); }
        Say((actor == null ? Summoner.Name : actor.Name) + " uses " + card.Name + ".");
        LastTeamUp = SummonMode ? TeamUpPartner(card, actor) : null;
        if (LastTeamUp != null)
        {
            TeamedThisTurn.Add(LastTeamUp);
            Fact("teamup", actor, LastTeamUp, card, 0, false, 1f, 1f);
            Say(actor.Name + " and " + LastTeamUp.Name + " team up!");
        }
        teamUpMultiplier = LastTeamUp != null ? TeamUpPower : 1f;
        Resolve(card, actor, target);
        teamUpMultiplier = 1f;
        if (SummonMode) AfterSummon(card, actor, target);
        if (Hand.Remove(card)) (card.Exhaust ? Exhausted : Discard).Add(card);
        if (GlowCard.Length > 0 && card.Id == GlowCard) OfferEpiphany(card);
        CheckEnd();
        // Only the fighters' own cards move the enemies' action counts and the chain: ego moves (ultimates, awakenings)
        // and JD's command cards and decrees (the summoner's turn) do not.
        if (card.Kind != BattleCardKind.Ultimate && card.Kind != BattleCardKind.Awakening && card.Kind != BattleCardKind.Summoner)
        {
            CardsThisTurn++;
            TickActionCounts();
        }
        return true;
    }

    // Each card played lowers every enemy's action count; one that reaches 0 acts at once, mid-turn, then its count
    // starts over with a fresh intent, so a long chain can draw a second action from a quick enemy. An enemy that
    // acted mid-turn does not act again at the end of the turn.
    private void TickActionCounts()
    {
        if (Finished) return;
        var ready = new List<BattleUnit>();
        foreach (BattleUnit enemy in Enemies)
        {
            if (!enemy.Alive || enemy.ActionMax <= 0) continue;
            enemy.ActionCount = Math.Max(0, enemy.ActionCount - 1);
            if (enemy.ActionCount == 0) ready.Add(enemy);
        }
        ready.Sort((a, b) => b.EffectiveSpeed.CompareTo(a.EffectiveSpeed));
        foreach (BattleUnit enemy in ready)
        {
            if (Finished) return;
            if (!enemy.Alive) continue;
            EnemyAct(enemy, true);
            if (Finished || !enemy.Alive) continue;
            enemy.ActionCount = enemy.ActionMax;
            enemy.Ap = enemy.MaxAp; enemy.Ep = enemy.MaxEp;
            RollIntent(enemy);
        }
    }

    // ---- reserve partners ------------------------------------------------------------------------------------

    // A reserve's assist: their first ultimate at 60%, once per battle, for PartnerSpCost SP.
    public BattleCard PartnerMove(BattleUnit reserve)
    {
        List<BattleCard> ults = BattleCatalog.Ultimates(reserve);
        if (ults.Count == 0) return null;
        BattleCard card = ults[0].Copy();
        card.Power *= .6f; card.Heal *= .6f;
        card.Name = card.Name + " (Assist)";
        return card;
    }

    public bool CanPartnerAssist(BattleUnit reserve)
    {
        return !Finished && reserve != null && Reserves.Contains(reserve) && reserve.Alive && !reserve.PartnerUsed
            && Sp >= PartnerSpCost && PartneredBy(reserve) != null && PartnerMove(reserve) != null;
    }

    public bool TryPartnerAssist(BattleUnit reserve, BattleUnit target)
    {
        if (!CanPartnerAssist(reserve)) return false;
        BattleCard card = PartnerMove(reserve);
        if (card.Target == BattleTarget.Enemy && (target == null || !target.Enemy || !Exposed(target))) return false;
        if (card.Target == BattleTarget.Ally && (target == null || target.Enemy || !Allies.Contains(target) || !target.Alive)) return false;
        Sp -= PartnerSpCost;
        reserve.PartnerUsed = true;
        Say(reserve.Name + " steps in beside " + PartneredBy(reserve).Name + ": " + card.Name + "!");
        Resolve(card, reserve, card.Target == BattleTarget.Self ? reserve : target);
        CheckEnd();
        return true;
    }

    // ---- summons ---------------------------------------------------------------------------------------------

    // Does this card keep its fighter on the field (the Vanguard)? A self shield or Guard, or a hold on themselves:
    // Taunt, a counter stance, a defense buff, a charge.
    public static bool Anchors(BattleCard card)
    {
        if (card == null || card.Kind == BattleCardKind.Summoner) return false;
        if (card.SelfShield > 0) return true;
        return card.Target == BattleTarget.Self && Array.IndexOf(AnchorStatuses, card.Status) >= 0;
    }

    private static bool Anchored(BattleUnit unit)
    {
        foreach (string status in AnchorStatuses) if (unit.HasStatus(status)) return true;
        return false;
    }

    // The bond partner who makes this card a team-up: the Vanguard, or the latest bonded fighter to act this turn.
    public BattleUnit TeamUpPartner(BattleCard card, BattleUnit actor)
    {
        if (!SummonMode || card == null || actor == null || !Allies.Contains(actor) || card.Partners.Length == 0) return null;
        if (Vanguard != null && Vanguard != actor && Vanguard.Alive && !TeamedThisTurn.Contains(Vanguard)
            && Array.IndexOf(card.Partners, Vanguard.Id) >= 0) return Vanguard;
        for (int i = ActedThisTurn.Count - 1; i >= 0; i--)
        {
            BattleUnit u = ActedThisTurn[i];
            if (u != actor && u.Alive && !TeamedThisTurn.Contains(u) && Array.IndexOf(card.Partners, u.Id) >= 0) return u;
        }
        return null;
    }

    private float TeamUpScale(BattleUnit actor)
    {
        if (actor == null) return 1f;
        if (!actor.Enemy) return teamUpMultiplier;
        return SummonMode ? (actor.Boss ? SummonBossPower : SummonEnemyPower) : 1f;
    }

    // After a summoned fighter acts: an anchoring card makes them the Vanguard (replacing the one out), and JD's
    // Taunt calls the chosen fighter out to hold the line.
    private void AfterSummon(BattleCard card, BattleUnit actor, BattleUnit target)
    {
        if (actor != null && Allies.Contains(actor))
        {
            ActedThisTurn.Remove(actor); ActedThisTurn.Add(actor);
            if (Anchors(card) && actor.Alive) SetVanguard(actor, card);
        }
        else if (card.Kind == BattleCardKind.Summoner && card.Status == "Taunt" && target != null && Allies.Contains(target) && target.Alive)
            SetVanguard(target, card);
    }

    private void SetVanguard(BattleUnit unit, BattleCard card)
    {
        if (Vanguard == unit) return;
        if (Vanguard != null) Recall();
        Vanguard = unit;
        Fact("vanguard", unit, unit, card, 0, false, 1f, 1f);
        Say(unit.Name + " holds the line.");
        // Intents aimed at JD (or at the old Vanguard) now meet the new one.
        foreach (BattleUnit enemy in Enemies)
        {
            BattleCard intent; BattleUnit aimed;
            if (!Intents.TryGetValue(enemy.Id, out intent) || intent.Target != BattleTarget.Enemy) continue;
            if (IntentTargets.TryGetValue(enemy.Id, out aimed) && aimed != null && !aimed.Enemy) IntentTargets[enemy.Id] = unit;
        }
    }

    private void Recall()
    {
        BattleUnit was = Vanguard;
        Vanguard = null;
        if (was == null) return;
        Fact("recall", was, was, null, 0, false, 1f, 1f);
        Say(was.Name + " returns to the contract.");
        foreach (BattleUnit enemy in Enemies)
        {
            BattleCard intent; BattleUnit aimed;
            if (!Intents.TryGetValue(enemy.Id, out intent) || intent.Target != BattleTarget.Enemy) continue;
            if (IntentTargets.TryGetValue(enemy.Id, out aimed) && aimed == was) IntentTargets[enemy.Id] = Summoner;
        }
    }

    // ---- epiphany --------------------------------------------------------------------------------------------

    private void OfferEpiphany(BattleCard card)
    {
        GlowCard = "";
        EpiphanyOptions.Clear();
        var pool = BattleCatalog.EpiphanyChoices(card);
        while (pool.Count > 0 && EpiphanyOptions.Count < 3)
        {
            int i = rng.Next(pool.Count);
            EpiphanyOptions.Add(pool[i]); pool.RemoveAt(i);
        }
        if (EpiphanyOptions.Count > 0) { EpiphanyCard = card; Say("Epiphany! " + card.Name + " can grow."); }
    }

    // Applies option i to every copy of the pending card for the rest of the battle (and records it for the run).
    public bool ChooseEpiphany(int index)
    {
        if (EpiphanyCard == null || index < 0 || index >= EpiphanyOptions.Count) return false;
        string mod = EpiphanyOptions[index];
        BattleCard picked = EpiphanyCard;
        string id = picked.Id;
        bool appliedToPicked = false;
        foreach (var list in new[] { DrawPile, Hand, Discard, Exhausted })
            foreach (BattleCard c in list)
                if (c.Id == id) { BattleCatalog.ApplyEpiphany(c, mod); if (c == picked) appliedToPicked = true; }
        if (!appliedToPicked) BattleCatalog.ApplyEpiphany(picked, mod);
        string before;
        Epiphanies[id] = Epiphanies.TryGetValue(id, out before) && before.Length > 0 ? before + "," + mod : mod;
        BattleUnit owner = OwnerOf(picked);
        Fact("epiphany", owner, owner ?? Summoner, picked, 0, false, 1f, 1f);
        Say(picked.Name + " gains " + BattleCatalog.EpiphanyName(mod) + ".");
        EpiphanyCard = null;
        EpiphanyOptions.Clear();
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
        // In a summon battle the pool is shared, so a transfer changes nothing and a gain goes to JD.
        if (!SummonMode && actor != null && chosen != null && card.TransferAp) { chosen.Ap += actor.Ap; actor.Ap = 0; }
        if (!SummonMode && actor != null && chosen != null && card.TransferEp) { chosen.Ep += actor.Ep; actor.Ep = 0; }
        if (chosen != null && !chosen.Enemy) { BattleUnit gain = Wallet(chosen); gain.Ap += card.ApGain; gain.Ep += card.EpGain; }
        else if (chosen != null) { chosen.Ap += card.ApGain; chosen.Ep += card.EpGain; }
        if (card.SelfShield > 0 && actor != null && actor.Alive && !actor.Enemy)
        {
            int amount = Math.Max(1, (int)Math.Round(card.SelfShield * MendMultiplier(actor)));
            actor.ApplyStatus("Shield", amount, 1);
            Fact("shield", actor, actor, card, amount, false, 1f, 1f);
        }
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
                if (card.Status == "Shield") magnitude = card.Magnitude * MendMultiplier(actor);
                target.ApplyStatus(card.Status, magnitude, card.Duration + (fullBond && card.Status != "IceCounter" ? 1 : 0));
                Fact("status", actor, target, card, 0, false, 1f, bond);
                Say(target.Name + " gains " + card.Status + ".");
            }
        }
    }

    private IEnumerable<BattleUnit> Targets(BattleCard card, BattleUnit actor, BattleUnit chosen)
    {
        if (card.Target == BattleTarget.Self || card.Target == BattleTarget.Ally || card.Target == BattleTarget.Enemy) { if (chosen != null) yield return chosen; yield break; }
        // A summon battle's field holds only JD and the Vanguard: an enemy's area attack hits those two.
        if (SummonMode && actor != null && actor.Enemy && card.Target == BattleTarget.AllEnemies)
        {
            if (Vanguard != null && Vanguard.Alive) yield return Vanguard;
            if (Summoner != null && Summoner.Alive) yield return Summoner;
            yield break;
        }
        List<BattleUnit> side = actor != null && actor.Enemy ? Enemies : Allies;
        List<BattleUnit> foe = actor != null && actor.Enemy ? Allies : Enemies;
        if (card.Target == BattleTarget.AllAllies) for (int i = 0; i < side.Count; i++) if (side[i].Alive) yield return side[i];
        // JD stands on a summon battle's field with the party, so the party's "all allies" covers JD too.
        if (SummonMode && side == Allies && card.Target == BattleTarget.AllAllies && Summoner != null && Summoner.Alive) yield return Summoner;
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
        float bonus = 1f + RunModifiers.DamageBonus;
        if (RunModifiers.Relic != "stormglass" && RunModifiers.Relic != "ironbark") return bonus;
        bool favored = RunModifiers.Relic == "stormglass" ? card.Magic : !card.Magic;
        return bonus * (favored ? relicSpent ? 1f : 1.25f : .9f);
    }

    private bool Covered(BattleUnit unit)
    {
        if (SummonMode && !unit.Enemy) return Vanguard != null && Vanguard != unit && Vanguard.Alive;
        List<BattleUnit> side = unit.Enemy ? Enemies : Allies;
        for (int i = 0; i < side.Count; i++) if (side[i] != unit && side[i].Alive && side[i].Taunting) return true;
        return false;
    }

    private float StrikeMultiplier(BattleUnit unit)
    {
        float partner = PartnerBonus(unit);
        if (unit.Role == BattleRole.Dps && !Covered(unit)) return 1.15f * partner;
        if (unit.Role == BattleRole.Ranger && Covered(unit)) return 1.20f * partner;
        return partner;
    }

    // A field fighter with a living reserve partner in their slot hits, heals and shields 8% harder (CZN partners).
    public const float PartnerPassive = 1.08f;
    public float PartnerBonus(BattleUnit unit)
    {
        if (unit == null || unit.Enemy || !Allies.Contains(unit)) return 1f;
        BattleUnit partner = PartnerOf(unit);
        return partner != null && partner.Alive ? PartnerPassive : 1f;
    }

    private float GuardMultiplier(BattleUnit unit) { return unit.Role == BattleRole.Tank && unit.Taunting ? 1.25f : 1f; }
    private float MendMultiplier(BattleUnit unit)
    {
        if (unit == null) return 1f;
        return (unit.Role == BattleRole.Support && Covered(unit) ? 1.25f : 1f) * PartnerBonus(unit);
    }

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
        float raw = Math.Max(1f, card.EffectivePower * synergy * bond * runMultiplier * TeamUpScale(actor) * attack * rage * actor.AttackMultiplier * StrikeMultiplier(actor) - defense * target.DefenseMultiplier * GuardMultiplier(target));
        // A broken enemy takes more from everything until it recovers.
        return raw * element * (target.HasStatus("Broken") ? 1f + target.StatusValue("Broken") : 1f);
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
        bool crit = rng.NextDouble() < actor.CritRate + (actor.Enemy ? 0f : RunModifiers.CritBonus);
        float raw = BaseDamage(card, actor, target, bond, runPlayMultiplier, out element);
        raw *= crit ? Math.Max(1f, actor.CritDamage) : 1f;
        int amount = Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        if (target.Enemy && !actor.Enemy) WearTenacity(card, actor, target, element);
        int shield = (int)target.StatusValue("Shield");
        if (shield > 0)
        {
            int absorbed = Math.Min(shield, amount);
            amount -= absorbed;
            if (shield - absorbed <= 0) target.Statuses.RemoveAll(s => s.Name == "Shield");
            else target.Statuses.Find(s => s.Name == "Shield").Magnitude = shield - absorbed;
            Say(target.Name + "'s shield absorbs " + absorbed + ".");
            // Fully blocked: no health lost and no stress gained.
            if (amount <= 0) { Fact("blocked", actor, target, card, absorbed, crit, element, bond); return; }
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
        if (SummonMode && target == Vanguard && !target.Alive) Recall();
        if (!target.Enemy)
        {
            Sp = Math.Min(SpMax, Sp + 1);
            target.Stress = Math.Min(100, target.Stress + (int)Math.Round(RunModifiers.StressScale * Math.Max(8, (int)Math.Round(35f * amount / Math.Max(1, target.MaxHp)))));
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

    // Party hits wear an enemy's tenacity: 1, +1 for a 2 EP card, +1 more at 3 EP, +1 on a WEAK hit, +2 for an ultimate.
    // At 0 the enemy BREAKS: it takes 25% more damage until next round, its action count is pushed back by one,
    // and the fighter who broke it gets 1 AP back and sheds 10 stress, and JD gains 1 SP.
    public static int TenacityDamage(BattleCard card, float element)
    {
        if (card == null) return 1;
        int wear = 1 + (card.Ep >= 2 ? 1 : 0) + (card.Ep >= 3 ? 1 : 0) + (element > 1.01f ? 1 : 0);
        if (card.Kind == BattleCardKind.Ultimate) wear += 2;
        return wear;
    }

    private void WearTenacity(BattleCard card, BattleUnit actor, BattleUnit target, float element)
    {
        if (target.MaxTenacity <= 0 || target.HasStatus("Broken") || !target.Alive) return;
        target.Tenacity = Math.Max(0, target.Tenacity - TenacityDamage(card, element));
        if (target.Tenacity > 0) return;
        target.ApplyStatus("Broken", BreakDamage, 1);
        target.ActionCount += 1;
        if (actor != null && Allies.Contains(actor) && actor.Alive) { Wallet(actor).Ap += 1; actor.Stress = Math.Max(0, actor.Stress - 10); }
        Sp = Math.Min(SpMax, Sp + 1);
        Fact("break", actor, target, card, 0, false, element, 1f);
        Say(target.Name + " BREAKS!");
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
        for (int i = 0; i < Enemies.Count; i++) RollIntent(Enemies[i]);
    }

    private void RollIntent(BattleUnit enemy)
    {
        Intents.Remove(enemy.Id); IntentTargets.Remove(enemy.Id);
        if (!enemy.Alive) return;
        List<BattleCard> pool = BattleCatalog.EnemyCards(enemy);
        if (pool.Count == 0) return;
        BattleCard card = null;
        if (enemy.Boss && enemy.HasStatus("Charging"))
        {
            // The charge-up announced last round: the signature move lands now.
            card = pool.Find(c => c.Id == "e_cataclysm");
            enemy.Statuses.RemoveAll(s => s.Name == "Charging");
        }
        if (card == null) card = ChooseEnemyCard(enemy, pool);
        if (card == null) return;
        Intents[enemy.Id] = card;
        IntentTargets[enemy.Id] = card.Target == BattleTarget.Enemy ? EnemyVictim(card) : card.Target == BattleTarget.Self ? enemy :
            card.Target == BattleTarget.Ally ? MostHurt(Enemies) : null;
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
        // Summon battles: the Vanguard holds the line; with nobody summoned, JD is hit.
        if (SummonMode) return Vanguard != null && Vanguard.Alive ? Vanguard : Summoner != null && Summoner.Alive ? Summoner : null;
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

    // One enemy carries out its intent (at the end of the turn, or mid-turn when its action count runs out).
    private void EnemyAct(BattleUnit enemy, bool interrupt)
    {
        enemy.ActedThisRound = true;
        enemy.ActionCount = 0;
        BattleCard card;
        if (!Intents.TryGetValue(enemy.Id, out card)) return;
        BattleUnit target;
        IntentTargets.TryGetValue(enemy.Id, out target);
        if (interrupt)
        {
            Interrupts++;
            Fact("interrupt", enemy, enemy, card, 0, false, 1f, 1f);
            Say(enemy.Name + " moves before the party can finish!");
        }
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
        Intents.Remove(enemy.Id);
        IntentTargets.Remove(enemy.Id);
    }

    public void EndTurn()
    {
        if (Finished) return;
        // Every card not played this turn is thrown out (to the discard pile) unless it Retains, JD's included.
        for (int i = Hand.Count - 1; i >= 0; i--)
            if (!Hand[i].Retain) { Discard.Add(Hand[i]); Hand.RemoveAt(i); }
        if (EpiphanyCard != null) ChooseEpiphany(0);
        List<BattleUnit> ordered = new List<BattleUnit>(Enemies);
        ordered.Sort((a, b) => b.EffectiveSpeed.CompareTo(a.EffectiveSpeed));
        for (int i = 0; i < ordered.Count; i++)
        {
            BattleUnit enemy = ordered[i];
            if (!enemy.Alive || enemy.ActedThisRound) continue;
            EnemyAct(enemy, false);
            if (Finished) return;
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
