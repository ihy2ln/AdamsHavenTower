using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // GDD 9.6 Gate sieges (TT 10.30.0, Battle Mode defence TT 10.30.1). A Dire tower draws a siege in place of an
    // ordinary event: ten minutes of warning, then the defenders meet the wave. During the warning the player can
    // take the heroes at home into Battle Mode (TowerSiegeBattle.cs); otherwise the guards auto-defend when it runs
    // out. Losing opens a Last Stand raid at a Gate. Live play only; never offline.
    public sealed partial class TowerRules
    {
        public const float SiegeWarning = 600f, FirstSiegeAfter = 2160f, SiegeThreat = 75f;
        public const int SiegeBattleXp = 30;

        public bool SiegeComing { get { return State.siegeWarning > 0; } }

        // Every adult who answers to defence, at home and on their feet; guards at the Gates count half again.
        public float SiegeDefence()
        {
            float total = 0;
            foreach (var r in State.residents)
            {
                if (r.ageStage != 0 || r.downed || r.exploring || r.away || r.origin == "body") continue;
                if (r.priorityDefense > 0)
                {
                    var job = Room(r.jobRoom);
                    total += (r.might * 0.3f + r.weapon * 2f) * (job != null && job.type == "gate" ? 1.5f : 1f);
                }
                if (r.origin == "hero") total += HeroPower(r) / 10f;
            }
            return total;
        }

        public float SiegeWave() { return 8f + 6f * State.heartRank + 0.3f * State.day; }

        // From TickEvents, once an event is due: no random roll, so the storyteller's sequence is untouched.
        private bool TryStartSiege()
        {
            if (State.siegeWarning > 0 || State.siegeCooldown > 0 || State.threat < SiegeThreat || !HasGate()) return false;
            State.siegeWarning = SiegeWarning;
            Note("Scouts see a Silverwood host gathering. A siege reaches the Gates in ten minutes: post guards, arm defenders, " +
                "or lead the heroes out in battle.");
            Emit("siege_warning", 0, 0, "");
            return true;
        }

        private void TickSiege(float dt)
        {
            State.siegeCooldown = Mathf.Max(0, State.siegeCooldown - dt);
            if (State.siegeWarning <= 0 || State.siegeBattle) return;   // the heroes are fighting it out in Battle Mode
            State.siegeWarning -= dt;
            if (State.siegeWarning <= 0) { State.siegeWarning = 0; ResolveSiege(); }
        }

        private void ResolveSiege()
        {
            float defence = SiegeDefence(), wave = SiegeWave();
            if (defence >= wave)
                WinSiege("The siege broke against the Gates (" + Mathf.RoundToInt(defence) + " against " + Mathf.RoundToInt(wave) + ").");
            else
                LoseSiege("The siege overran the Gate guards (" + Mathf.RoundToInt(defence) + " against " + Mathf.RoundToInt(wave) + ").");
        }

        private void WinSiege(string how)
        {
            State.siegeCooldown = 3.5f * DaySeconds * Storyteller.delay;
            int gold = 100 * State.heartRank, celestium = 2 * State.heartRank;
            State.gold += gold; State.celestium += celestium;
            State.threat = Mathf.Max(0, State.threat - 30);
            State.siegesWon++;
            foreach (var r in State.residents)
                if (r.ageStage == 0 && !r.downed && !r.exploring && r.priorityDefense > 0) GiveXp(r, 20);
            Note(how + " The forest pulls back. +" + gold + " gold, +" + celestium + " Celestium.");
            Bump("siege_won");
            Emit("siege_won", 0, 0, "");
        }

        private void LoseSiege(string how)
        {
            State.siegeCooldown = 3.5f * DaySeconds * Storyteller.delay;
            State.siegesLost++;
            Note(how + " Last Stand: drive the raiders out before they reach the Heart!");
            if (StartRaid() == null)
            {
                var raid = State.incidents[State.incidents.Count - 1];
                raid.hp *= 1.5f;
                raid.severity += 1;
            }
            Emit("siege_lost", 0, 0, "");
        }

        // ---- Battle Mode defence -------------------------------------------------------------------------------

        // The battle-ready heroes at home and well, strongest first (at most 6: 3 fight, 3 wait in reserve). Only the
        // Fighters have battle rigs; summoned roster heroes still defend through SiegeDefence.
        public List<string> SiegeFighters()
        {
            var list = new List<TowerResident>();
            foreach (string id in Fighters)
            {
                var hero = HeroResident(id);
                if (hero != null && hero.ageStage == 0 && !hero.downed && !hero.away && !hero.exploring && hero.injury < 50)
                    list.Add(hero);
            }
            list.Sort((a, b) => HeroPower(b).CompareTo(HeroPower(a)));
            var ids = new List<string>();
            for (int i = 0; i < list.Count && i < 6; i++) ids.Add(list[i].unitId);
            return ids;
        }

        public string BeginSiegeBattle()
        {
            if (!SiegeComing) return "No siege is gathering.";
            if (State.siegeBattle) return "The heroes are already fighting the siege.";
            if (SiegeFighters().Count == 0) return "No battle-ready hero is at home. The guards will hold the Gate alone.";
            State.siegeBattle = true;
            Note("The heroes march out to meet the siege before it reaches the Gates.");
            return null;
        }

        // Battle Mode's answer. A win breaks the siege like the guards would (and the fighters learn from it);
        // a loss or a retreat leaves the Gate to a Last Stand.
        public void ResolveSiegeBattle(bool won, IList<string> fought)
        {
            if (!State.siegeBattle && !SiegeComing) return;
            State.siegeBattle = false;
            State.siegeWarning = 0;
            if (won)
            {
                if (fought != null)
                    foreach (string id in fought) GiveXp(HeroResident(id), SiegeBattleXp);
                WinSiege("The heroes broke the siege in open battle.");
            }
            else LoseSiege("The heroes fell back and the siege reached the Gates.");
        }

        // A siege fight cut short by closing the game hands the choice back with at least a minute to decide.
        private void NormalizeSiege()
        {
            if (!State.siegeBattle) return;
            State.siegeBattle = false;
            State.siegeWarning = Mathf.Max(State.siegeWarning, 60f);
        }
    }
}
