using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;

// Bridges a Tower Gate siege (TowerSiege.cs) to Battle Mode (TT 10.30.1, GDD 9.6): the battle-ready heroes at home
// with their Tower gear and levels, against a siege wave built at the Heart's rank.
public static class TowerSiegeBattle
{
    // Field is the first three, reserve the rest; everyone starts at full health (Tower heroes carry no battle HP).
    public static void Party(TowerRules rules, IList<string> ids, out List<BattleUnit> field, out List<BattleUnit> reserve)
    {
        var units = BattleCatalog.Party(ids);
        var ready = new List<BattleUnit>();
        foreach (string id in ids)
        {
            var unit = units.Find(u => u.Id == id);
            if (unit == null) continue;
            unit.Attack *= 1 + rules.GearAttack(id);
            unit.Magic *= 1 + rules.GearAttack(id);
            unit.MaxHp = Mathf.RoundToInt(unit.MaxHp * (1 + rules.GearHp(id) + rules.LevelHp(id)));
            unit.Defense *= 1 + rules.LevelGuard(id);
            unit.Resistance *= 1 + rules.LevelGuard(id);
            unit.Hp = unit.MaxHp;
            ready.Add(unit);
        }
        field = ready.GetRange(0, Mathf.Min(3, ready.Count));
        reserve = ready.Count > 3 ? ready.GetRange(3, Mathf.Min(3, ready.Count - 3)) : new List<BattleUnit>();
    }

    // Battle depth 2 x rank - 1 fights enemies of the Heart's own rank; from rank B the wave is led by a lair-class boss.
    public static int Depth(TowerRules rules) { return Mathf.Max(1, 2 * rules.State.heartRank - 1); }

    public static BattleEncounterSpec Spec(TowerRules rules)
    {
        var state = rules.State;
        return new BattleEncounterSpec { Depth = Depth(rules), Kind = state.heartRank >= 5 ? "boss" : "elite", Theme = "keep",
            Seed = 7919 * (state.siegesWon + state.siegesLost + 1) + state.day };
    }

    public static BattleEncounter Encounter(TowerRules rules)
    {
        var encounter = BattleCatalog.Build(Spec(rules));
        if (encounter != null) encounter.Title = "GATE SIEGE";
        return encounter;
    }
}
