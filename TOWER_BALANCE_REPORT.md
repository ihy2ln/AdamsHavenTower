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

## 6. Heart pacing pass (2026-10-01, later)

Request: first runs climb slowly; Legacy makes later runs faster.

- **Heart costs** (Celestium / Gold per rank-up, F to SSR): 10/200, 20/500, 130/1,000, 420/2,000, 1,250/4,000, 2,300/8,000, 3,800/12,000, 4,700/16,000. Total 12,630 Celestium and 43,700 Gold (was 1,753 and 18,000).
- **Quarry yield** per collect: 1 Celestium per rank up to D, then half a rank's worth (F..SSR: 1, 2, 2, 3, 3, 4, 4, 5, 5; was 1..9).
- **Legacy discount:** each Legacy rank cuts Heart upgrade costs by 6%, capped at 60% (Legacy rank 10). Shown on the Heart's Upgrade tab.
- **Climb model** (goals about 8 Celestium a day, 0 to 5 quarries at the Heart's building cap, half of all Celestium to research). Founding's 45 Celestium covers E and D at once; the climb slows from C:

| Legacy rank | C | B | A | S | SS | SSR |
| --- | --- | --- | --- | --- | --- | --- |
| GDD target (first run) | 7 | 14 | 25 | 40 | 60 | 80 |
| 0 (first run) | 3.6 | 10.7 | 21.6 | 36.7 | 56.8 | **76.8** |
| 3 | 2.3 | 8.1 | 17.0 | 29.5 | 45.9 | 62.3 |
| 5 | 1.5 | 6.4 | 14.0 | 24.7 | 38.7 | 52.7 |
| 10 | 0 | 2.5 | 6.8 | 12.9 | 21.0 | 28.9 |

The test `FirstRunsClimbSlowlyAndLegacyRunsFaster` runs this model against the real cost tables (first run 60 to 95 days; Legacy 10 under 60% of that). It is a model, not a played run: a measured playthrough is still worth doing once the tower's early game settles.

## 7. TT 10.30.0 numbers (2026-10-03, drafts, not yet playtested)

**Auto expeditions.** Founding heroes keep the default 3 in every stat, so a level-1 rank-D founder has about 13 power; checkpoint 6's level-25 founders about 20. Danger was rescaled to fit: Brook Edge 45 (two checkpoint-6 heroes: 41 power, ratio 0.91, Success; three level-1 founders: about 40, Partial), up to 290 for the deepest region (three maxed heroes about 361, ratio 1.24 Success, 1.37 Great with Tank + Support).

**Outposts.** A day's output per average (5) worker, before region reward and rank: 40 food, water or firewood; 6 wood; 4 stone; 3 ore; 2 essence; 60 gold; 0.5 Tonics; 0.4 Celestium. Examples at rank F with two average workers: Brookside Farm (Brook Edge, x1.0) 80 food and 6 wood a day; Gatekeep Dig (Wood Gate, x3.0) 2.4 Celestium and 9 ore a day; Heartwood Grove (depth 5, x5.2) at SSR (x5) with six workers 62 Celestium a day. For scale, one rank-F Quarry makes about 18 Celestium a day.

**Districts.** Industry at full purity +15% (Overtime another +15% for 30 gold a day); Market +20%; Arcane +20% training and Tonics; policy upkeep 0 to 80 gold a day, against a rank-F Market's 324 gold a day.

**Sieges.** Wave = 8 + 6 x Heart rank + 0.3 x day: about 15 at F on day 3, 36 at C on day 14, 86 at SSR on day 80. Defence: two Might-10, weapon-3 defenders already give 18.

**Performance.** Checkpoint 10 (60 residents, 374 rooms): Advance about 0.95 to 1.0 ms/frame with or without districts; TickDistricts, TickOutposts and TickSiege about 0.001 ms each, DistrictBonus over all rooms 0.004 ms. The earlier 0.55 ms figure was measured on a smaller day-90 tower; the bulk of today's cost is PlanJobs (0.43 ms) and TickRooms (0.29 ms).

**Town life slice 1 (TT 10.3.1).** Checkpoint 10 over two game days with no events: residents on production 58.4% of the time with errands off, 52.7% with them on (5.2% walking to venues, 1.4% eating, 1.1% drinking, 1.0% at leisure); average mood 100 either way, lowest hunger 39, no cold rations. Advance stays about 0.9 to 1.0 ms/frame. Venues near work floors are the lever.
