# Tower Mode balance report (2026-10-01)

Targets come from `TOWER_MODE_GDD.md`; numbers come from the game code, measured by running the real simulation (`TowerRules`) in the Unity editor. One game day is 720 s (`TowerRules.DaySeconds`). Production was measured with 2 workers (all stats 5), full stock and no incidents.

## 1. Measured income per game day (one room, 2 workers)

| Room | Rank F | Rank D | Rank B | Rank S | Rank SSR |
| --- | --- | --- | --- | --- | --- |
| Stone Quarry (Celestium) | 18 | 57 | 95 | 133 | 171 |
| Argent Market (Gold) | 324 | 684 | 1,026 | 1,368 | 1,710 |
| Kitchen (Food) | feeds itself and the two workers at every rank | | | | |

About 18 to 19 collect cycles run per day at every rank. Rank raises the amount per collect (Celestium: 1 per rank, Gold: 18 per rank, Food: 8 to 60), not the speed.

## 2. GDD targets against the code

| Topic | GDD | Code today | Verdict |
| --- | --- | --- | --- |
| **Sigil income** | about 45 per day (goals, daily board, expeditions, research) | **No source at all.** Sigils start at 1 and only checkpoints grant more. Goals pay Gold, Celestium and Tonics; expeditions pay Celestium and Tonics | **Broken: the gacha cannot be fed** |
| Summon cost | 10 Sigils per pull, 100 per 10-pull | 1 per pull, 10 per 10-pull | Mismatch |
| Standard rates F..SSR | 26.5, 22, 17, 12, 8, 6, 4.5, 2.5, 1.5 | 13.5, 15, 18, 18, 14, 10, 7, 3, 1.5 | Mismatch (SSR and pity match) |
| Banners | Standard, Featured, Pick-Your-Hero, Resident | One pool (60% heroes, the rest residents) | Not built |
| Research tree | 40 nodes, about 6,650 Celestium, tier-gated by Heart rank | Not present; only per-building blueprint research for Gold and Celestium | Not built |
| Heart rank pace | E day 1, D day 3, C day 7, B day 14, A day 25, S day 40, SS day 60, SSR day 80 | Total Heart upgrade cost 1,753 Celestium and 18,000 Gold for all 8 upgrades | **Far too cheap** (see section 3) |
| Offline cap | 8 to 12 hours | 4 hours (`CatchUp`) | Mismatch (you chose to keep current behavior) |
| Hero stat scale | band 2 to 70 | game uses 1 to 10 per stat (roster stats are mapped onto 3 to 10) | Roster mapped; GDD room bonus (+1% per point) not applicable at this scale |

## 3. Pacing check: the Heart ranks arrive much too fast

Heart upgrade cost in code: Celestium 8, 20, 45, 90, 160, 280, 450, 700 (total 1,753) and Gold 500 x current rank (total 18,000).

- One rank D Quarry makes about **57 Celestium per day**. Three quarries cover all eight Heart upgrades in **under 11 days**, against the GDD's 80.
- One rank F Market makes **324 Gold per day**, so the 18,000 Gold total is about 56 market-days. Two or three markets finish it inside 3 weeks.
- Once a research tree of about 6,650 Celestium exists, total Celestium demand is about 8,400 (the GDD figure for the 60 to 90 day pace). A mid-game tower with 3 to 4 quarries at rank C to A makes about 190 to 400 Celestium per day, so research alone would still finish in 20 to 40 days.

**Conclusion:** to hold the 60 to 90 day SSR target, Heart upgrade costs need to rise sharply or Celestium output needs to slow down. Suggested starting point once the research tree is in: Celestium cost x 4 on Heart upgrades (about 7,000 total) and quarry output halved at rank C and above. This needs a second measurement pass.

## 4. What to build or fix next (ordered)

1. **Add Sigil sources** (blocks the whole gacha): Goal rewards of 2 to 5 Sigils, a daily board worth about 20, expedition clears paying about 15 per day on average, plus Heart rank-up milestones. Target about 45 per day.
2. **Decide the summon economy:** adopt the GDD (10 per pull, GDD rates, soft pity at 50, hard at 60) or lower the GDD numbers. The two disagree today. With 45 Sigils per day and 10 per pull, one SSR arrives about every 13 days of income (60-pull pity = 600 Sigils).
3. **Research tree** (40 nodes) and the Heart upgrade cost curve, then re-measure pacing.
4. **Banners** (Featured, Pick-Your-Hero, Resident) once Sigil income exists.
5. Re-measure after each change (production per day by rank, Sigil and Celestium income, Heart upgrade pace). The probe runs the real rules in the editor, so it is quick to repeat on request.

## 5. Notes

- Roster integration: summons now draw named heroes (4 per rank) and residents from `tower_roster.json`, with pools and ranks tested.
- Rogue-lite Legacy: each fallen Heart earns points; the Legacy rank (0 to 10) grants a stacking start bonus (Gold, supplies, Tonics, Celestium). It deliberately leaves Sigils and Heart HP unchanged.
