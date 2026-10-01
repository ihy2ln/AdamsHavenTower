# Tower Mode design handoff (for the next TT session)

Written 2026-10-01. Read this first, then `TOWER_MODE_GDD.md` (design authority) and `TOWER_BALANCE_REPORT.md` (measured numbers).

## 1. Where things stand

Tower Mode is a Fallout Shelter-style tower town in Unity (`S:\AI\Game\Unity AHCG\My project`). The player is the Summoner/Steward of the Celestium Heart. RimWorld depth is DLC. Tower tests: **102 pass** (`TowerSimulationTests`, EditMode). Last commits: `0bb395e`, `ba57690`.

| Area | State |
| --- | --- |
| GDD (`TOWER_MODE_GDD.md`) | Complete first draft: premise, loop, tower structure, buildings F to SSR with 1/2/3-bay growth, dwellers, economy model, Heart hub, threats, expeditions, UI wireframes, tutorial, Legacy restart. Many numbers are drafts marked **[TBD]** |
| Code | Heart ranks, warning stages, hard fail with Legacy restart, summoning, roster, incidents, expeditions (other session), HUD chrome with art |
| Characters | 36 heroes + 24 residents designed (`CharacterPrompts/`), prompts run, art generated (`CharacterPrompts/Generated`, 1.7 GB, **not** imported into the game) |
| Saves | 10 slots in `AppData\LocalLow\DefaultCompany\My project\AdamsHavenTower`. All pinned, maxed (storage F to SSR, full stock, full conditions) for testing. Backups: `slot_NN.json.before-max-*.bak` |

## 2. Decisions the user has locked (do not re-ask)

- Mobile-first, premium, no IAP. Free rotation in Tower and Battle. Sessions 5 to 10 minutes. **Keep the current offline catch-up behavior** (it runs on load even if the game was paused; capped at 4 h in code).
- Buildings upgrade in place F, E, D, C, B, A, S, SS, SSR. Footprint 1 bay (F to D), 2 (C, B), 3 (A to SSR), growing left. Guild is 3 cells. Same-type adjacency bonus (+12%) kept.
- Heart is the central shaft and the hub (Summon, Research, Upgrade, Status). **No death-spiral recovery: Heart falls = game over**, then a rogue-lite **Legacy restart** (harder start, stacking bonus per fallen run).
- Heart damage only from breaches (raiders in the Heart's chamber) and unattended fire on the Heart's floor. Pests never hurt it.
- 36 heroes (4 per rank), 24 residents with fixed ranks, 7 elements, 4 roles. All characters 21+. Female characters use deliberately exaggerated gacha proportions; costumes opaque and non-explicit.
- Heart pacing target: SSR in about 60 to 90 days of play (E d1, D d3, C d7, B d14, A d25, S d40, SS d60, SSR d80).
- First tutorial incident is a **fire** in the Shack. Dock: BUILD, PEOPLE, HEART (center), EXPEDITIONS, BATTLE.
- Rank-specific building functions need a **deep-dive session with the user** (they said so). Ask questions first.

## 3. Open work, in priority order

1. ~~Sigil sources~~ **Done 2026-10-01** (`TowerSigils.cs`, GDD 8.1): about 30 per real day from goals, a daily board, expeditions and Heart rank-ups. Daily board lives in a DAILY tab of the Goals popup.
2. ~~Summon economy~~ **Done 2026-10-01**: GDD adopted (10 per pull, 100 per 10-pull, GDD rates, 10-pull floor B, pity 50/60). Checkpoint save Sigils scaled x10 (pinned slots keep their old values).
3. **Heart pacing.** Costs are far too low (three quarries finish all Heart upgrades in about 11 days). Needs the research tree first, then a second measurement pass. Starting idea in the balance report: Heart Celestium costs x 4, quarry output halved from rank C.
4. **Research tree** (40 nodes in GDD 8.2.1). Only per-building blueprint research exists in code.
5. **Banners:** Featured, Pick-Your-Hero (2x cost), Resident. Not built.
6. **Rank-specific building functions and SSR buffs** (GDD 5.3, only the Barn buff is named). Design session needed.
7. Import character art into the game (`CharacterPrompts/_source/import_roster_art.py`, subset or `--all`). Wire portraits into the People/Hero detail and summon reveal. Decide what ships in builds (Resources ships everything).
8. Smaller TBDs listed in GDD section 16 (breach damage rates, siege waves, region passives, rations curve, Echoes, ascension cost).

## 4. Where things live

| What | Path (under `My project`) |
| --- | --- |
| Design authority | `TOWER_MODE_GDD.md` |
| Measured balance | `TOWER_BALANCE_REPORT.md` |
| Old build log | `TOWER_LIFE.md` (superseded as design) |
| Rules and state | `Assets/Scripts/TowerDomain.cs`, `TowerSystems*.cs`, `TowerLife.cs`, `TowerColony.cs`, `TowerHeart.cs` (Heart, Legacy, summoning), `TowerThreat.cs`, `TowerMilestones.cs` (checkpoint saves, `MaxedForTesting`, `pinned`) |
| Roster | `TowerRoster.cs`, `Assets/Resources/AdamsHaven/Roster/tower_roster.json` |
| HUD and skin | `TowerHud*.cs`, `TowerUiSkin.cs`, `TowerSleekArt.cs` (loads `UI/Sleek` and `UI/Wuwa` art) |
| Tests | `Assets/Editor/TowerSimulationTests.cs` |
| Character pack | `CharacterPrompts/` (`roster.csv/json`, `heroes/`, `residents/`, `_source/*.py`, `Generated/` art) |
| UI prompt packs and slicer | `UiPrompts/`, `UiSliced/slice_sheets.py`, mockup `UiWireframes/tower_ui_mockup.html` |

Regenerate the character pack: `python CharacterPrompts/_source/build_prompts.py`, then `export_game_roster.py` to refresh the game JSON.

## 5. Working notes and traps

- Another session works on the Expedition/Atlas UI and Battle at the same time. The user said **this line of work is Tower mode only**: leave the Expedition UI to the other session. Commit only your own hunks; do not stage `CharacterPrompts/Generated`.
- Run tests with the Unity MCP `Unity_RunCommand` + `TestRunnerApi` (EditMode, group `^TowerSimulationTests`, `runSynchronously`). Never edit scripts while in Play mode; check `EditorApplication.isPlaying` first (the user sometimes starts Play).
- Bash heredocs turn `\n` inside Python strings into real newlines. Use the Edit tool for C# strings with `\n`.
- Tower statics survive leaving Play mode in the editor. `TowerRules.DebugIgnoreGuild` is reset by `Assets/Editor/TowerDebugFlagReset.cs`.
- Saves: `pinned = true` stops the checkpoint generator rebuilding a slot. Slot 1 is the player's own. Statics like `InstantConstruction` must be restored after experiments.
- Time: `AdamsHavenPrototype.Update` uses `TowerRules.FrameSeconds(deltaTime, speed)`; paused = speed 0 advances nothing; speeds are proportional at 12 to 60 fps (tested).
- A game day is 720 s. Production is about 18 to 19 collect cycles per day at every rank; rank raises the amount per collect, not the speed.

## 6. Suggested first moves for the next session

1. Ask the user to confirm the Sigil income numbers and the summon economy (item 2), then implement Sigil sources and align costs and rates, with tests.
2. Draft the research tree in code against the GDD node list, then re-measure Heart pacing and tune costs.
3. Run the rank-function design deep-dive with the user, then write it into GDD 5.3.
