using System.Collections.Generic;

// The AUTO button's player, and the policy the balance simulation fights with: fire a ready ultimate, then JD's
// decree when the SP bar is full, then the strongest affordable card, then basic attacks. Targets the weakest
// exposed enemy, heals the most hurt ally. Pure rules: BattleMode animates whatever it picks.
public static class BattleAutoPlayer
{
    public struct Move
    {
        public BattleCard Card;
        public BattleUnit Actor, Target;
        public int Ultimate;     // >= 0: ultimate choice for Actor
        public bool Decree;      // JD's summoner ultimate
    }

    public static bool Next(BattleState b, out Move move)
    {
        move = new Move { Ultimate = -1 };
        if (b == null || b.Finished) return false;
        foreach (BattleUnit ally in b.Allies)
        {
            if (!ally.Alive || ally.Ultimate < 100 || b.Sp < BattleState.UltimateSpCost) continue;
            List<BattleCard> options = BattleCatalog.Ultimates(ally);
            for (int i = 0; i < options.Count; i++)
            {
                BattleUnit target = Target(b, options[i], ally);
                if (!b.CanPay(options[i], ally) || !b.IsTarget(options[i], ally, target)) continue;
                // The healing / guarding ultimate only when someone is hurt; otherwise the damaging one.
                bool support = options[i].EffectivePower <= 0;
                if (support && !Hurt(b, .6f) && i + 1 < options.Count) continue;
                move = new Move { Card = options[i], Actor = ally, Target = target, Ultimate = i };
                return true;
            }
        }
        if (b.Sp >= BattleState.SpMax && b.Summoner != null && b.Summoner.Alive) { move.Decree = true; return true; }
        BattleCard best = null; BattleUnit bestActor = null, bestTarget = null; float bestValue = 0f;
        foreach (BattleCard card in b.Hand)
        {
            BattleUnit actor = b.OwnerOf(card);
            if (card.Kind != BattleCardKind.Summoner && actor == null) continue;
            if (!b.CanPay(card, actor)) continue;
            BattleUnit target = Target(b, card, actor);
            if (!b.IsTarget(card, actor, target)) continue;
            float value = Value(b, card);
            if (value > bestValue) { best = card; bestActor = actor; bestTarget = target; bestValue = value; }
        }
        if (best != null) { move = new Move { Card = best, Actor = bestActor, Target = bestTarget, Ultimate = -1 }; return true; }
        foreach (BattleUnit ally in b.Allies)
        {
            BattleCard basic = BattleCatalog.Basic(ally);
            BattleUnit target = Target(b, basic, ally);
            if (b.CanPay(basic, ally) && b.IsTarget(basic, ally, target))
            { move = new Move { Card = basic, Actor = ally, Target = target, Ultimate = -1 }; return true; }
        }
        return false;
    }

    // Plays a move straight into the rules (the simulation; BattleMode routes moves through its own animations).
    public static bool Apply(BattleState b, Move move)
    {
        if (move.Decree) return b.TrySummonerUltimate();
        if (move.Ultimate >= 0) return b.TryUltimate(move.Actor, move.Ultimate, move.Target);
        return b.TryPlay(move.Card, move.Target);
    }

    // Plays rounds until the fight ends (or a round cap); returns the number of rounds fought.
    public static int PlayOut(BattleState b, int maxRounds = 30)
    {
        while (!b.Finished && b.Round < maxRounds)
        {
            Move move;
            for (int guard = 0; guard < 40 && Next(b, out move); guard++) if (!Apply(b, move)) break;
            if (!b.Finished) b.EndTurn();
        }
        return b.Round;
    }

    private static float Value(BattleState b, BattleCard card)
    {
        float value = card.EffectivePower * (card.Target == BattleTarget.AllEnemies ? 1.6f : 1f) + card.Draw * .4f + card.EpGain * .3f + card.ApGain * .6f;
        if (card.EffectiveHeal > 0) value += Hurt(b, .7f) ? card.EffectiveHeal / 20f : .05f;
        if (!string.IsNullOrEmpty(card.Status)) value += .5f;
        if (card.TransferEp || card.TransferAp) value += .2f;
        return value;
    }

    private static bool Hurt(BattleState b, float below)
    {
        foreach (BattleUnit unit in b.Allies) if (unit.Alive && unit.Hp < unit.MaxHp * below) return true;
        return false;
    }

    public static BattleUnit Target(BattleState b, BattleCard card, BattleUnit actor)
    {
        if (card.Target == BattleTarget.Self) return actor;
        if (card.Target == BattleTarget.Ally)
        {
            BattleUnit weakest = null;
            foreach (BattleUnit unit in b.Allies)
                if (unit.Alive && (weakest == null || unit.Hp * (long)weakest.MaxHp < weakest.Hp * (long)unit.MaxHp)) weakest = unit;
            return weakest;
        }
        if (card.Target == BattleTarget.Enemy)
        {
            BattleUnit pick = null;
            foreach (BattleUnit enemy in b.Enemies)
                if (b.Exposed(enemy) && (pick == null || enemy.Hp < pick.Hp)) pick = enemy;
            if (pick != null) return pick;
            return b.EnemySummoner != null && b.EnemySummoner.Alive ? b.EnemySummoner : null;
        }
        return null;
    }
}
