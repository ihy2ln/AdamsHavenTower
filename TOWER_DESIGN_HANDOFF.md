# Tower Mode handoff (for the next TT session)

Updated 2026-10-03 after TT 10.3.1. Read this first, then `TOWER_MODE_GDD.md` (design authority), `TOWER_LIFE.md` (build log, newest at the bottom) and `TOWER_BALANCE_REPORT.md` (measured numbers).

## 0. Start here

**TT 10.3.1 shipped summon banners (GDD 8.1) and town life slice 1 (GDD 19.1). TT 10.3.2 shipped the TOWN view greybox (GDD 19.2). TT 10.3.3 shipped town lots (auto growth + full player override), the 48-picture reference pack and the picture-to-3D pipeline.** The next jobs, in the owner's order: **real building assets for the town** (the blocks must go), then **town lots as rules data**, then the economy and visitors (GDD 19.3-19.4). Ask the owner the questions in section 3 first.

Paste-ready prompt for the next session:

> TT <next version>. Tower Tycoon: town building assets and town lots (GDD 19.2). Read `TOWER_DESIGN_HANDOFF.md` sections 0 and 3 first and ask me its open questions before coding.

## 1. Where things stand

Tower Mode is a **Fallout Shelter x RimWorld** tower town in Unity (`S:\AI\Game\Unity AHCG\My project`, branch master), with **Cities: Skylines-style management** on top. The player is the Summoner/Steward of the Celestium Heart.

**Tests (2026-10-03):** `TowerManagementTests` 73 + `TowerSimulationTests` 119, all green (EditMode). The `^Expedition` and `^Battle` groups were also green (253 in all four).

| Area | State |
| --- | --- |
| Core loop | Rooms F to SSR with 1/2/3-bay growth, stat matching, collect / rush, adjacency, firewood brownouts, construction timers, MOVE / DEMOLISH (also long-press), floor caps by Heart rank (stretched to ±24) |
| Heart | Ranks F to SSR with building and research caps, dweller cap (8 to 110), warning stages, hard fail with a rogue-lite Legacy restart, 40-node research tree, slow first-run climb |
| Summons (TT 10.3.1) | Four banners: Standard (60/40, free first summon), Featured (weekly SSR+SS pair), Pick-Your-Hero (2x cost), Resident (5 Sigils, SSR 1%, A+ every 20). Shared hero pity 50/60 |
| Colony (RimWorld, core) | Needs (Food, Water, Rest, **Joy**), thought-based mood, 10 traits, second trait and backstory for newcomers, mood breaks, leaving, inspirations, bonds / families / grief, work priorities + WORK GRID, schedules with free hours, three storytellers |
| Town life (TT 10.3.1) | Residents walk to venues to eat, drink and unwind; seats, crowding, cold rations, venue thoughts; live play only (offline keeps instant meals) |
| Threats | Fire, pests, illness, raiders, cave-ins; a third active incident from rank C; Gate sieges with auto-defend or DEFEND IN BATTLE (Battle Mode) and a Last Stand on a loss |
| Management (Skylines) | Districts on floor bands (specialisation, 8 policies with upkeep, service coverage, appeal, info-view overlays); outposts as staffed colonies in conquered regions (caravans, raids, ranks F to SSR) with Atlas markers |
| Expeditions | Played runs (other session); auto expeditions on the real clock with power vs danger, region elements and counters |
| Sigils | About 30 a real day from goals, the daily board, expeditions and Heart rank-ups (`TowerSigils.cs`) |
| Characters | 36 heroes + 24 residents designed (`CharacterPrompts/`); roster stats and names are in the game (`tower_roster.json`); the generated art (1.7 GB) is **not** imported |
| Saves | 10 slots in `AppData\LocalLow\DefaultCompany\My project\AdamsHavenTower`. Slot 1 is the player's own; slots 2-10 are pinned checkpoints. Back them up before any Play check (Play autosaves). |

## 2. Decisions the owner has locked (do not re-ask)

- Mobile-first, premium, no IAP. Free rotation in Tower and Battle. Sessions 5 to 10 minutes. Keep the current offline catch-up (runs on load, capped at 4 h).
- **Fallout Shelter x RimWorld is the foundation.** Colony depth is core, never "DLC". Each TT batch also looks for gaps against the GDD ("always need to improve the mode").
- **Skylines-style management:** districts are **floor bands** (specialisation, policies, service coverage, appeal; unzoned floors lose nothing); outposts are **staffed colonies** in conquered regions. **District appeal does not lower Threat** (owner, 2026-10-03).
- Floor caps follow the Heart, stretched to ±24 (up 2,3,5,7,9,12,15,19,24 / down 1,3,5,7,9,12,15,19,24).
- Buildings upgrade in place F, E, D, C, B, A, S, SS, SSR; footprint 1 bay (F-D), 2 (C-B), 3 (A-SSR), growing away from the shaft. Same-type adjacency +12%.
- The Heart is the central shaft and hub (Summon, Research, Upgrade, Status). **Heart falls = run over**, then a Legacy restart. It is hurt only by breaches in its chamber and unattended fire on its floor.
- 36 heroes (4 per rank), 24 residents with fixed ranks, 7 elements, 4 roles. All characters 21+; exaggerated gacha proportions, opaque non-explicit costumes.
- Heart pacing target: SSR in about 60 to 90 days of play.
- Rank-specific building functions need a **deep-dive session with the owner** first.
- **Banners (2026-10-03):** Standard keeps its 60/40 hero/resident mix; SSR pity is shared by Standard, Featured and Pick; Featured rotates weekly (local calendar), automatically; the Pick target changes freely and a pending guarantee carries over.
- **Town (2026-10-03):** the town sim has four pillars (residents live in town, street outside the Gates, economy, visitors and traders). Venues are self-serve until wages exist; free hours are soft blocks for day / night schedules, and flexible residents go out on joy; joy shows in the resident detail only; the global "Tower amenities" bonus stays alongside venue thoughts.

## 3. Next: town slices 2 to 4 (GDD 19.2-19.4)

**Today** (`TowerTown.cs`, GDD 19.1): `VenueDefs` by room type, `EnsureVenues` cache (rebuilds on `LayoutStamp` / room count), `ConsiderErrand` in `PlanJob` (live ticks only, `townTick`), `TickErrand` (the single place stock is spent; `VenuePrice` returns 0), `TickJoy`, `FreeTime`, `AddTownThoughts`. Tests in `TowerManagementTests` "town life".

**Town view greybox (TT 10.3.2):** `TowerTownMap.cs` (tiles, ring, roads; pure), `TowerTownView.cs` (camera, placeholder scene, input), `TowerHudTown.cs` (TOWN button, overlay). Decided: own view, circle-ish ring on a square grid, radius 7..35 by Heart rank, ring roads every 8 tiles, iso 35° default (the owner has seen all three angles).

**Ask the owner first** (also GDD 16 item 16):
1. **Building assets (decided 2026-10-03):** 15 types across Residential / Market / Industry / Arcane / Defence plus the Tower, three rank bands each; **reference picture first, then the 3D shell**. `Tools/build_town_prompts.py` writes `TownPrompts/` (48 prompts); `Tools/render_town_refs.py <type>|all` renders them with local ComfyUI (Qwen Image 2.1, the character-view graph); then `Tools/barn_to_3d.py`-style Trellis.2 → GLB per picture. Still open: which pictures the owner accepts or rerolls (`--seed`), and whether the Tower exterior is a Trellis model or hand-built.
2. **Town lots as rules (decided 2026-10-03): both.** Lots fill on their own from population and appeal; the player can override any single lot or a selected group (an `auto` flag per lot). Still open: do town homes count toward the dweller cap; can raiders damage town buildings; growth speed per Heart rank.
3. **Economy:** a closed gold loop, or keep the Market minting? Wages and rent per resident or per room? Who sets prices: the player per venue, or district policy? Do venues need staff once wages exist?
4. **Visitors and traders:** visible walkers that use venues and pay? Do traders replace or extend the caravan event? Can a good visit turn a visitor into a recruit?
5. **Feel:** visible queues at full venues? Is the cold-rations penalty (-3) right?

**Suggested order:** building assets (replace the blocks in `TowerTownView.BuildPlaceholderTown`; keep `TowerTownMap` as the ground truth); then town lots as rules data (a town grid in `TowerState`, lots feeding `VenueDefs` and homes, Gate-and-road travel in `TravelSeconds`); then prices and wages through `TickErrand` / `VenuePrice` with a resident purse; then visitors as non-resident walkers.

## 4. Other open work (offer these after, or instead of, the town)

1. **Seated and drinking poses for errands** (small, needs `AdamsHavenPrototype.cs`, the other session's file): add `TowerRules.ErrandClipKey(resident)`; `TowerChibi3D.RoomClips` gains `errand_meal -> AH_sit`, `errand_drink -> AH_task_tavern`, `errand_chat -> AH_chat`; in `AdamsHavenPrototype.cs` `AtStation |= key != null` (about line 314) and `working |= key != null`, `workRoom = key ?? ...` (about lines 1408-1417).
2. Import the generated character art (`CharacterPrompts/_source/import_roster_art.py`, subset or `--all`) and use it in the People panel, hero detail and the summon reveal (no reveal animation exists yet; GDD 8.1 asks for one). Decide what ships: Resources ships everything, and the APK is already about 965 MB.
3. Rank-specific building functions and SSR buffs (GDD 5.3). This needs the owner deep-dive first.
4. A measured balance playthrough with town life on (GDD 16 items 14 and 17): errands cost checkpoint 10 about 10% of production time; recheck the Heart pacing.
5. Smaller TBDs in GDD 16: breach damage rates, siege waves, region passives, rations curve, Echoes, ascension cost, a dedicated outpost / siege art pass.

## 5. Where things live

| What | Path (under `My project`) |
| --- | --- |
| Design authority | `TOWER_MODE_GDD.md` (17 Districts, 18 Outposts, 19 Town) |
| Build log / balance | `TOWER_LIFE.md`, `TOWER_BALANCE_REPORT.md` |
| Town view | `TowerTownMap.cs` (ground plan), `TowerTownLots.cs` (lots, growth, player commands), `TowerTownView.cs` (scene, camera, input, models), `TowerHudTown.cs` (HUD, lot panel); `Tools/build_town_prompts.py`, `render_town_refs.py`, `town_to_3d.py`, `town_glb_to_fbx.py` |
| Rules and state | `Assets/Scripts/TowerDomain.cs` (state, catalog, build / upgrade, dweller cap), `TowerSystems*.cs` (tick), `TowerLife.cs` (mood, goals, storyteller), `TowerColony.cs` (levels, raids, bonds, Steward), `TowerHeart.cs` (Heart, Legacy, Standard summon), `TowerBanners.cs` (banners), `TowerTown.cs` (venues, errands, joy), `TowerResearch.cs`, `TowerSigils.cs`, `TowerMilestones.cs` (checkpoints) |
| TT 10.30.x systems | `TowerLayoutTools.cs` (floor caps, move / demolish, `MigrateColony`, colony random), `TowerColonyDepth.cs` (traits, backstories, leaving, inspirations, storytellers), `TowerDistricts.cs`, `TowerAutoExpedition.cs` (postings, auto runs, elements), `TowerOutposts.cs`, `TowerSiege.cs`, `TowerSiegeBattle.cs` (Battle Mode bridge) |
| HUD | `TowerHud.cs` (+ partials `TowerHudChrome`, `TowerHudHeart` (summon banners), `TowerHudResearch`, `TowerHudWork`, `TowerHudDistricts`, `TowerHudExpeditions`, `TowerHudOutposts`, `TowerHudSiege`, `TowerHudDev`), `TowerUiSkin.cs`, `TowerArtDirector.cs` (cutaway art and info-view overlays), `TowerFx.cs` (mood glyphs incl. errands) |
| Roster | `TowerRoster.cs`, `Assets/Resources/AdamsHaven/Roster/tower_roster.json` (the six founding Fighters are not in it; their element and role come from `BattleCatalog`) |
| Tests | `Assets/Editor/TowerManagementTests.cs` (Tower only, put new tests here), `TowerSimulationTests.cs` (shared with the Expedition work), `TowerPerfProbe.cs` (`Tools > Adams Haven > Tower Perf Probe`, or `TowerPerfProbe.Run()`) |
| Character pack | `CharacterPrompts/` (`roster.csv/json`, `_source/*.py`, `Generated/` art, never staged) |

## 6. Working notes and traps

- **The other session** works on Expedition / Atlas / Battle at the same time and may leave uncommitted edits (`AdamsHavenPrototype.cs`, `TowerSimulationTests.cs`, `TowerExpedition*.cs`, `TowerDungeon.cs`, `TowerMap*.cs`, `Battle/*`). On 2026-10-03 its Tower-side files were all committed.
  - Commit only your own hunks: write a patch from your pre-edit copy to your version, then `git apply --cached`.
  - Then typecheck the **exported index** (`git ls-files 'Assets/*.cs' | git checkout-index --stdin --prefix=<dir>/` + Roslyn), so a commit never needs their unstaged or untracked work.
- **Play mode:** check `EditorApplication.isPlaying` before any .cs edit. The Editor can sit in Play for hours. Never edit scripts during your own Play check either: Unity hot-reloads, the HUD's fields go null and you get an NRE in `RefreshTopChrome`. If Play is on, ask the owner to stop it; meanwhile stage edits in a scratch copy and typecheck offline with Unity's Roslyn from the csproj HintPaths (relative paths resolve against the project).
- **Unity MCP `Unity_RunCommand`:**
  - `System.Reflection` is forbidden, and nested classes get duplicated: declare `ICallbacks` collectors as top-level `internal` classes, with a new name each call.
  - Run tests with `TestRunnerApi`: EditMode, groups `^TowerManagementTests`, `^TowerSimulationTests`, `^Expedition`, `^Battle`, `runSynchronously`.
  - After editing scripts, an `AssetDatabase.Refresh()` call can fail with "Could not find type ...RunCommandMacroEvaluatorEntryPoint" while the domain reloads; that is harmless. Run the next command and check a new symbol compiled.
  - `result.Log` ignores format specifiers like `{1:0.0}`; build the string with `string.Format` first.
  - `SendMessage("Name")` reaches private no-arg HUD methods. With an int argument, 0 binds to `SendMessageOptions`; box values: `SendMessage("DoSummon", (object)10)`.
  - HUD screenshots: `ScreenCapture.CaptureScreenshot(path)` in Play (the overlay Canvas is included); the file appears after the frame ends, so read it on the next call.
  - BattleMode is IMGUI: drive it with `DebugMouse(point on its 1600x900 virtual screen)` + `DebugClick()`.
- **Play checks:** back up `LocalLow\DefaultCompany\My project\AdamsHavenTower` first; `AdamsHavenPrototype.LoadCheckpoint(n)` and `NewGame()` save the current slot. Afterwards restore the slots, move any slot 0 you created out of the folder, and set the PlayerPref `AdamsHaven.Tower.LastSlot` back to 1.
- **Saves:** never bump `TowerState.schema` (`Load` accepts only 1 and 2). Migrate through `ColonyVersion` (now 2) / `MigrateColony()` (steps gated per version), and put list guards in `NormalizeColony()` (runs every load). New random rolls in Tower systems use `ColonyRandom01()`, not `Random01()`, so seeded tests keep their sequences (summon banners use `Random01()` like Standard, off the Standard path).
- **Town assets in flight (2026-10-03 evening):** all 48 reference pictures rendered (`TownPrompts/`, git-ignored). `Tools/town_assets_chain.py --quick` runs detached (pythonw) and converts every picture to a model in `Resources/AdamsHaven/TowerModels/town_<type>/` (log `TownModels/chain.log`); village Inn and the three Bathhouses verified. Unity imports the FBXs on its next refresh and `TowerModelImporter` builds the prefabs (or run Tools > Tower > Rebuild model prefabs). Candidates to reroll: `watchtower_assr` (drifted into a manor with a tower). MAX quality: delete `TownModels/<stem>.glb` and run `python Tools/town_to_3d.py <stem>`.
- **Offline typecheck:** no `Tools/typecheck.sh` here; a Roslyn driver that reads `Assembly-CSharp.csproj` HintPaths plus every `Assets/Scripts/**/*.cs` lived in the TT 10.3.2 session scratchpad (`typecheck.py`, ~40 lines: csc.dll under `Editor/Data/DotNetSdk/sdk/*/Roslyn/bincore`, `-nostdlib+`, the csproj defines and LangVersion). Rebuild it when Play is locked.
- **TOWN view:** `TowerTownView` disables `Camera.main` and the scene's directional lights while active and restores them on Exit; its scene lives at x = 5000. The HUD overlay is the input catcher: the Tower's `OverUI` sees it, so no Tower code changed.
- **Town life is live-only:** `townTick` is set at the top of `Tick` from `live && TownErrands`. Timing-sensitive tests (`GameSpeedKeepsStatsProportionalAtAnyFrameRate`, `IncomeForecastMatchesWhatTheKitchenReallyMakes`) are what errand gates and serving rates must keep green.
- **Postings:** a resident away for a Tower reason (auto expedition, outpost) has `posting` set and `exploring = true`. Never reuse `away`: the expedition code rewrites it on every load. `Recall` routes postings home.
- **Caches:** district / coverage / appeal arrays and the venue list rebuild when `LayoutStamp` (or room, floor or resident counts) change. Anything that changes the tower's shape calls `TouchLayout()`.
- Tower statics survive leaving Play (`TowerRules.DebugIgnoreGuild` is reset by `TowerDebugFlagReset.cs`; restore `InstantConstruction` and `TownErrands` after experiments). A game day is 720 s. Time uses `TowerRules.FrameSeconds(deltaTime, speed)`; speed 0 advances nothing.
- Bash heredocs can mangle backslashes and apostrophes: write Python helpers to files, or use the Edit tool for C# strings with `\n`.
