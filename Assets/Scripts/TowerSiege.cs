using UnityEngine;

namespace AdamsHaven.Tower
{
    // GDD 9.6 Gate sieges, auto-defend only for now (TT 10.30.0; the Battle Mode defence is the Battle session's
    // to wire). A Dire tower draws a siege in place of an ordinary event: ten minutes of warning, then the
    // defenders meet the wave. Losing opens a Last Stand raid at a Gate. Live play only; never offline.
    public sealed partial class TowerRules
    {
        public const float SiegeWarning = 600f, FirstSiegeAfter = 2160f, SiegeThreat = 75f;

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
            Note("Scouts see a Silverwood host gathering. A siege reaches the Gates in ten minutes: post guards, arm defenders.");
            Emit("siege_warning", 0, 0, "");
            return true;
        }

        private void TickSiege(float dt)
        {
            State.siegeCooldown = Mathf.Max(0, State.siegeCooldown - dt);
            if (State.siegeWarning <= 0) return;
            State.siegeWarning -= dt;
            if (State.siegeWarning <= 0) { State.siegeWarning = 0; ResolveSiege(); }
        }

        private void ResolveSiege()
        {
            State.siegeCooldown = 3.5f * DaySeconds * Storyteller.delay;
            float defence = SiegeDefence(), wave = SiegeWave();
            if (defence >= wave)
            {
                int gold = 100 * State.heartRank, celestium = 2 * State.heartRank;
                State.gold += gold; State.celestium += celestium;
                State.threat = Mathf.Max(0, State.threat - 30);
                State.siegesWon++;
                foreach (var r in State.residents)
                    if (r.ageStage == 0 && !r.downed && !r.exploring && r.priorityDefense > 0) GiveXp(r, 20);
                Note("The siege broke against the Gates (" + Mathf.RoundToInt(defence) + " against " + Mathf.RoundToInt(wave) +
                    "). The forest pulls back. +" + gold + " gold, +" + celestium + " Celestium.");
                Bump("siege_won");
                Emit("siege_won", 0, 0, "");
                return;
            }
            State.siegesLost++;
            Note("The siege overran the Gate guards (" + Mathf.RoundToInt(defence) + " against " + Mathf.RoundToInt(wave) +
                "). Last Stand: drive the raiders out before they reach the Heart!");
            if (StartRaid() == null)
            {
                var raid = State.incidents[State.incidents.Count - 1];
                raid.hp *= 1.5f;
                raid.severity += 1;
            }
            Emit("siege_lost", 0, 0, "");
        }
    }
}
