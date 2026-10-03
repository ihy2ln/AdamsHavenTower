# Tower Mode design handoff (for the next TT session)

Written 2026-10-01, updated 2026-10-03 (TT 10.30.0). Read this first, then `TOWER_MODE_GDD.md` (design authority) and `TOWER_BALANCE_REPORT.md` (measured numbers).

## 0. TT 10.30.0 in one paragraph

The owner set the foundation: **Fallout Shelter x RimWorld**, with **Cities: Skylines-style management** on top. RimWorld depth is core (GDD 6.3): second traits, backstories, leaving, inspirations, storytellers, a work grid. New management layers: **districts** on floor bands (GDD 17) and **outposts** in conquered regions (GDD 18). Gap fixes: Heart-rank floor caps (stretched to +-24), MOVE / DEMOLISH, auto expeditions (GDD 10.3), Gate sieges (GDD 9.6, auto-defend). Build log: `TOWER_LIFE.md` "Oct 3". Tests: `TowerManagementTests` (47) + `TowerSimulationTests` (119) + `Expedition*` (40), all green on 2026-10-03.

**TT 10.30.1 (same day):** the four follow-ups. Sieges can be fought in Battle Mode (`TowerSiegeBattle.cs`, banner `TowerHudSiege.cs`, `AdamsHavenPrototype.LaunchSiegeBattle`); storyteller pick on the dormant founding panel; long-press a room to move it (`TickRoomHold` in `HandleCameraInput`, ring via `TowerFx.ShowHold`); outpost marker and panel line on the Atlas (my hunks in the Expedition session's `TowerMapSources.cs` / `TowerExpeditionUi.cs`).

## 1. Where things stand

Tower Mode is a Fallout Shelter x RimWorld tower town in Unity (`S:\AI\Game\Unity AHCG\My project`). The player is the Summoner/Steward of the Celestium Heart. Tower tests: **166 pass** (`TowerSimulationTests` 119 + `TowerManagementTests` 47, EditMode).

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
- **(2026-10-03, TT 10.30.0)** Fallout Shelter x RimWorld is the foundation: colony depth is core, never "DLC". Skylines-style management is welcome: **districts are floor bands** (specialisation, policies, service coverage, appeal; bonuses only for unzoned floors) and **outposts are staffed colonies** in conquered regions. Floor caps follow the Heart, **stretched to +-24** (up 2,3,5,7,9,12,15,19,24 / down 1,3,5,7,9,12,15,19,24). The owner wants each TT batch to also look for gaps against the GDD.

## 3. Open work, in priority order

1. ~~Sigil sources~~ **Done 2026-10-01** (`TowerSigils.cs`, GDD 8.1): about 30 per real day from goals, a daily board, expeditions and Heart rank-ups. Daily board lives in a DAILY tab of the Goals popup.
2. ~~Summon economy~~ **Done 2026-10-01**: GDD adopted (10 per pull, 100 per 10-pull, GDD rates, 10-pull floor B, pity 50/60). Checkpoint save Sigils scaled x10 (pinned slots keep their old values).
2b. **Done 2026-10-01 (later):** research tree (item 4) built, `TowerResearch.cs` + Heart RESEARCH tab, 40 nodes, real-time timers; summons with no bed wait in the Heart; compact gold (1.4k / 1mil); simulation 7.6x faster (`Tools > Adams Haven > Tower Perf Probe`); Android-only texture caps (`AndroidTextureCompression.cs`). APK is ~965 MB because the new Roster character art adds ~336 MB: decide what ships.
2c. **Done 2026-10-03 (TT 10.30.0):** see section 0 and `TOWER_LIFE.md` "Oct 3". Follow-ups in GDD 16 item 15: outpost marker on the Atlas (Expedition session's `TowerMapSources.cs`), Battle Mode siege option (Battle session), region elements, storyteller picker on NEW GAME, dweller cap by Heart rank, long-press move, a measured balance pass of the new numbers (GDD 16 item 14).
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
| Tests | `Assets/Editor/TowerSimulationTests.cs` (shared with the Expedition work), `Assets/Editor/TowerManagementTests.cs` (TT 10.30.0, Tower only) |
| Colony depth, districts, outposts (TT 10.30.0) | `TowerLayoutTools.cs` (floor caps, move, demolish, `MigrateColony`), `TowerColonyDepth.cs`, `TowerDistricts.cs`, `TowerAutoExpedition.cs` (postings), `TowerOutposts.cs`, `TowerSiege.cs`; HUD `TowerHudWork.cs`, `TowerHudDistricts.cs`, `TowerHudExpeditions.cs`, `TowerHudOutposts.cs` |
| Character pack | `CharacterPrompts/` (`roster.csv/json`, `heroes/`, `residents/`, `_source/*.py`, `Generated/` art) |
| UI prompt packs and slicer | `UiPrompts/`, `UiSliced/slice_sheets.py`, mockup `UiWireframes/tower_ui_mockup.html` |

Regenerate the character pack: `python CharacterPrompts/_source/build_prompts.py`, then `export_game_roster.py` to refresh the game JSON.

## 5. Working notes and traps

- Another session works on the Expedition/Atlas UI and Battle at the same time. The user said **this line of work is Tower mode only**: leave the Expedition UI to the other session. Commit only your own hunks; do not stage `CharacterPrompts/Generated`.
- Run tests with the Unity MCP `Unity_RunCommand` + `TestRunnerApi` (EditMode, groups `^TowerSimulationTests`, `^TowerManagementTests`, `^Expedition`, `runSynchronously`). RunCommand's code fixer duplicates nested classes: declare the `ICallbacks` collector as a top-level `internal` class. Never edit scripts while in Play mode, not even your own Play check (Unity hot-reloads and the HUD's non-serialized fields go null); check `EditorApplication.isPlaying` first (the user sometimes leaves Play running for hours).
- When the Editor is busy, work in a staging copy and typecheck offline with Unity's Roslyn against `Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj` (references from the csproj HintPaths, relative ones resolved against the project). TT 10.30.0 was built that way, then merged in with a 3-way `git merge-file`.
- **Save versioning:** never bump `TowerState.schema` (`TowerSaveFiles.Load` only accepts 1 and 2). New data goes through `ColonyVersion` / `MigrateColony()` (and `NormalizeColony()` for list guards on every load). New random rolls use `ColonyRandom01()` (`State.colonyRandom`), never `Random01()`, so the seeded tests' main stream never shifts.
- **Postings:** a resident away from the Tower for a Tower reason (auto expedition, outpost) has `posting` set and `exploring = true`. Do not reuse `away`: the expedition code rewrites it on every load (`MarkPartyAway`). Posted residents skip needs and upkeep and lose their job slot; `Recall` routes them home.
- **Caches:** district, coverage and appeal arrays rebuild when `LayoutStamp` (or room / floor / resident counts) change, plus every 10 s. Anything that changes the tower's shape should call `TouchLayout()`.
- **Committing next to the other session:** they edit `AdamsHavenPrototype.cs`, `TowerSimulationTests.cs` and the expedition files at the same time. Stage only your hunks (`git diff <file>`, trim the patch, `git apply --cached`) and put new Tower tests in `TowerManagementTests.cs`.
- Bash heredocs turn `\n` inside Python strings into real newlines. Use the Edit tool for C# strings with `\n`.
- Tower statics survive leaving Play mode in the editor. `TowerRules.DebugIgnoreGuild` is reset by `Assets/Editor/TowerDebugFlagReset.cs`.
- Saves: `pinned = true` stops the checkpoint generator rebuilding a slot. Slot 1 is the player's own. Statics like `InstantConstruction` must be restored after experiments.
- Time: `AdamsHavenPrototype.Update` uses `TowerRules.FrameSeconds(deltaTime, speed)`; paused = speed 0 advances nothing; speeds are proportional at 12 to 60 fps (tested).
- A game day is 720 s. Production is about 18 to 19 collect cycles per day at every rank; rank raises the amount per collect, not the speed.

## 6. Suggested first moves for the next session

1. Ask the user to confirm the Sigil income numbers and the summon economy (item 2), then implement Sigil sources and align costs and rates, with tests.
2. Draft the research tree in code against the GDD node list, then re-measure Heart pacing and tune costs.
3. Run the rank-function design deep-dive with the user, then write it into GDD 5.3.
