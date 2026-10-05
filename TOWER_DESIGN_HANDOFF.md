# Tower Mode handoff (for the next TT session)

Updated 2026-10-04 after TT 10.4.4 (docs: one world). Read this first, then `TOWER_MODE_GDD.md` (design authority; **section 21 is the one-world plan and the roadmap**, 20 the Dungeon Mode fork, 19 the town), `TOWER_LIFE.md` (build log, newest at the bottom) and `TOWER_BALANCE_REPORT.md` (measured numbers).

## 0. Start here: one world, two sides, one town

### NEW 2026-10-05: the room builder comes first (GDD section 22)

Owner ("TT 10.5.0 change of building design"): players **furnish their own rooms, Sims-style**. Rooms become **real 3D
shells** (stylized, not low-poly) seen through a slightly tilted side-on camera; residents walk front and back; **the
furniture and amenities make the rank**, and the **walls change at every rank-up** (one shared 9-step ladder + category
accents). **No restrictions**: any item works in any room; items matching the room's type give a bonus. **All residents
3D.** Order: Tower rooms, then dungeon rooms, then the town. Slices R1-R8 in GDD 22.6 run **before** the pest lure.

**R1 done 2026-10-05** (rules only; `TowerRules.RoomBuilder` = `ForceRoomBuilder`, off in Play until R3).
Code map: `TowerFurnishing.cs` (24-item catalog, slot grid 4 cols x wall/back/front per bay, coverage rank, presets,
place/move/sell, EXPAND/SHRINK, `HomePlaces`/`JobPlaces`/`BedsIn`/`StorageWeight`, `Product`/`Makes`/`YieldOf`,
`AssignHome`, `MigrateFurnishing` via `TowerRoom.furnished`). Hooks: `Capacity`, `HousingCap`, `AvailableHome`,
`Assign` (`HousesByDefault`), `StockCap`, `CollectAmount`/`BaseCollect`/`Collect` (multi-resource), `UpgradeRoom`
("Furnish the room..."), `Advance` (`RefreshAllRoomRanks`), and `def.produces` -> `Product`/`Makes`/`YieldOf` in the
colony, steward, income, rush, tick and dev code. View files (`TowerFx` bubbles, HUD labels) still read `def.produces`:
fix them in R2/R3 before the switch goes on. Demolish does not refund furniture yet (R3). Tests: `RoomBuilderTests` 9;
all five Tower suites 233 green.

> TT 10.5.0 room builder R2. Read `TOWER_DESIGN_HANDOFF.md` section 0 and `TOWER_MODE_GDD.md` section 22, then build
> slice R2, the 3D shell greybox: tilt the Tower camera about 12 degrees, draw each furnished room as a 3D shell per bay
> (back wall, floor, ceiling beam, side posts) coloured by the rank's wall-ladder step, furniture as sized blocks at their
> slots (wall / back / front rows), and residents walking the lane between back and front, stopping at their bed or
> station. Keep the painted rooms when `RoomBuilder` is off.


### The direction (owner, 2026-10-04)

**One world, two sides, one save.** The Tower grows up from the ground; the summoner's **dungeon is dug beneath it** (ENTRANCE via a Dungeon Gate from the town, B1..Bn, LIVING, then the HEART VAULT at the bottom). The dungeon exists **from the start** but first lures **pests**; it levels up (Dungeon rank) until it can take on **humans**. **Adventurers are town visitors**: they lodge, shop, take quests in town, raid the dungeon, and survivors come back and spend. The separate Dungeon Mode fork is **retired** once the merge plays (its saves are dev checkpoints, not migrated). Full design: GDD section 21.

### Roadmap (GDD 21.8): one batch per session, in order

| # | Batch | What | Ask the owner first |
| --- | --- | --- | --- |
| 1 | **TT 10.5.0 merge foundations** (done, 8edc228) | `ColonyVersion` 3 migration (insert B1 with `ShiftFloors`, basements become the living band, vault = lowest floor); `FloorKind` over the whole tower; zoning by band; Dungeon Gate in `RouteEntry`; dig buttons below ground; new game with B1 + vault; every `TowerRules.IsLair` branch per GDD 21.6; **BATTLE dock button becomes DUNGEON** (the dungeon hub) | Underground cap split (dungeon vs living inside the down-caps) |
| 2 | **TT 10.5.1 pest lure** (next) | Dungeon rank + XP, prey tiers, pest swarms, pest-incident spillover, Monster banner from E, **tutorial part 1: the dungeon tower** (living + dungeon lessons) | Pest timer and yields |
| 3 | TT 10.5.2 beasts and taming | Bestiary beasts at D; Snare captures become monsters | Beast families per region |
| 4 | TT 10.6.0 town lots as rules | Lot functions (homes, inn, shops, quest board, healing, defence); dweller cap; growth speed; raider damage; **tutorial part 2: the town** (place buildings, zone a district, city-sim basics) | Section 3 Q2 |
| 4b | TT 10.6.x expedition tutorial | **Tutorial part 3**: unlock EXPEDITIONS, a guided short run, materials home (needs the BM session's expedition hooks) | Which region and loot |
| 5 | TT 10.6.1 town walkers and economy | Visible wanderers; purses; inn / shop / shrine spending via `TickErrand` / `VenuePrice` | Section 3 Q3-5 |
| 6 | TT 10.6.2 humans in the dungeon | Parties form in town from Dungeon rank C; town buildings set size and rank; survivors walk back; replaces the `FinishParty` placeholder | Party balance |
| 7 | TT 10.6.3 elite and sieges together | Siege kind + queue, Threat and Notoriety both on the HUD, named heroes | none |
| 8 | TT 10.7.0 retire the fork | Remove the MENU switch, `AdamsHavenDungeon/`, `LairMilestones`; regenerate Tower checkpoints with the dungeon band | none |

**Done 2026-10-04: TT 10.4.5 compact HUD** (e66f32b): `TowerUiFit` shrinks every open panel to its content; new panels must follow the same rule (owner: mobile first, no empty space).

Track A (playing the Tower opening and fixing what breaks) pauses until batch 2 lands, because the opening gains the dungeon lessons there; urgent opening bugs can still be fixed any time.

**Done 2026-10-04: TT 10.5.0** (8edc228). Code map for the merge:
- `LairLayout.cs`: `Merged` (a Tower save with a dungeon), `VaultFloor` (lair.heartFloor, the dungeon's end) vs `HeartFloor`
  (where the Heart room is: 0 in a Tower), `FoundDungeonBand()`, `EnsureDungeonBand()` (every load, from `MigrateColony`),
  `FloorKind` adds "tower", zoning for one world. `TowerRules.OneWorld` is true only in Play (or `ForceOneWorld` in tests):
  EditMode suites written for the Tower see no dungeon. `OneWorldTests.cs` opts in.
- `LairRoute.cs`: `Walkable` keeps prey below ground, `DungeonGateX` (B1's east end), `RouteEntry`, `ShaftSealed` (dungeon
  floors only). `TickLair` still runs only for `IsLair`: batch 2 wires the merged dungeon's tick (prey, monsters).
- HUD: dock `Dock dungeon` -> `OpenLair` (hub built in both modes, `RefreshMergedDungeon`); floors popup DIG DUNGEON / ADD
  LIVING rows; build tab DUNGEON once a dungeon exists. `LairView` attaches in both modes (DUNGEON GATE badge).

> TT 10.5.1. Tower Tycoon, one world batch 2: the pest lure and tutorial part 1. Read `TOWER_DESIGN_HANDOFF.md` section 0 and `TOWER_MODE_GDD.md` 21.2-21.3, ask me the pest timer and yields, then build: Dungeon rank and XP, pest swarms down the Dungeon Gate (TickLair for `Merged`), traps on pests, pest-incident spillover into the living band, the Monster banner from Dungeon rank E in one world, and the dungeon-tower tutorial lessons.

> (Old prompt, done) TT 10.5.0. Tower Tycoon, one world batch 1: merge foundations. Read `TOWER_DESIGN_HANDOFF.md` section 0 and `TOWER_MODE_GDD.md` section 21 (and 20 for the Lair code), ask me the underground cap split, then build (and turn the BATTLE dock button into DUNGEON, the dungeon hub): the `ColonyVersion` 3 migration, `FloorKind` over the whole tower, zoning by band, the Dungeon Gate entry, dig buttons below ground, a new game with B1 and the vault, and the `IsLair` branches per 21.6. Keep the Dungeon Mode fork working on its own saves.

**Where the code is today (survey 2026-10-04):** `TowerState.lair` (`TowerLairState`, LairDomain.cs) is already in every Tower save; `TowerState.mode` is "tower" except saves made by `NewLair`. Rules read `TowerRules.IsLair` (LairLayout.cs, `State.mode == "lair"`); HUD, view, save folder and checkpoints read the static `TowerModes.IsLair` (LairMode.cs). About 25 rules branches, 25 HUD and 6 view branches (list in the GDD 21.6 dispositions). `Load` never checks that a save's mode matches its folder. Town lots have **no gameplay effect yet** (`TowerTownLots.cs` says so) and the town files have no mode branches; `TickTown` grows a town in both modes. The caravan event has no `IsLair` check.

### Today: two playable modes and the town view (until batch 8)

There are **two playable modes** in the same Unity project, and **the town is a view inside each of them**, not a third mode.

| What | Kind | How to reach it | Saves | State |
| --- | --- | --- | --- | --- |
| **Tower Mode** | Mode (the default) | Launch the game | `LocalLow/.../AdamsHavenTower/` (slot 0 = NEW GAME, 1 = player, 2-10 pinned checkpoints) | Full colony sim; opening reworked in TT 10.4.1-10.4.3 |
| **Dungeon Mode** | Mode (fork, GDD 20) | MENU > **DUNGEON MODE** (tap twice). The scene reloads; MENU > **TOWER MODE** goes back | `LocalLow/.../AdamsHavenDungeon/` (1 = dormant, 2 summit, 3 depths, 4 elite incoming) | Slice 1 built (TT 10.4.0): founding, traps, monsters, raids, notoriety, elite team |
| **Town** | A **view** in either mode (GDD 19) | **TOWN** button, top right, under the resource bar | Lives in the same save (`townLots`) | Ring town around the tower; lots grow by themselves or by the player; **all 48 building models now exist** (TT 10.3.2-10.3.3 + asset chain) |

Code map: Tower = `Tower*.cs`; Dungeon = `Lair*.cs` + `TowerHudLair.cs` (switch in `LairMode.cs`, `TowerModes.IsLair`); Town = `TowerTown*.cs` + `TowerHudTown.cs`.
Every Tower file edit for the dungeon is an `IsLair` branch, so Tower Mode plays exactly as before.

### What the owner asked for (2026-10-03/04), and where it stands

- **The pivot (2026-10-03):** the Heart becomes the summoner's **dungeon**, raided by adventurers (Dungeon Keeper x Kairosoft Dungeon Village). **The town stays and becomes the adventurer economy.** Ruled then: a separate mode, so the Tower stays playable. Built as Dungeon Mode slice 1; **the town is not yet wired to the dungeon**. **Revised 2026-10-04: one world** (the dungeon below the Tower in the same save; see the roadmap above and GDD 21).
- **Town (GDD 19):** its own view, a circle-ish ring on a square grid around the tower, growing with the Heart; it runs itself and the player can override any lot or group of lots. Reference picture first, then the 3D shell. **Done:** view, lots, the 48-picture pack and all 48 models. **Next:** lots as rules data, then economy and visitors.

### Recent batches (2026-10-04)

| Batch | Commits | What |
| --- | --- | --- |
| TT 10.4.0 | 8b820bc | Dungeon Mode fork, slice 1 (GDD 20) |
| TT 10.4.1 | a2da408 | Sell-back window (100% to 0% over 180 game s; undoes an upgrade), DECONSTRUCT afterwards, cancel construction for a full refund; ground floor could not grow (EAST button hidden); Gates step out when a foundation starts; placing ends after one build; no sky tint during the lessons; worker duty fix (newcomers became repairers and burned the last stone); Lumber Mill gives a little stone; gentler first 3 days |
| TT 10.4.2 | b76c5b7 | Compact see-through HUD (1600x900 reference, 80% panels, 50% menu backdrop, ease-in), outward-facing Gates, animated Heart beam (2 floors past top and bottom), days turn at midnight, supplies-run quest (25 wood, 15 stone), FOOD and WATER lessons |
| TT 10.4.3 | 1434e40, b8e6cc0, 175c383, 3645161 | Drag onto any producer completes the MATCH lesson; **rest hysteresis** (the "resident bugs out" jitter: rest at 25 flipped rest/meal every frame); the Steward no longer undoes the player's assignment for a game day; Shack and Kitchen use their F/E/D art (furnished interiors from rank B) |

Tests: `LairModeTests` + `TowerManagementTests` + `TowerSimulationTests` = **216 green** (EditMode, 2026-10-04).

### The old three tracks (2026-10-04 morning), now folded into the roadmap

Track A is the opening play-through (paused until batch 2); Track B became batches 1-3, 6 and 7; Track C became batches 4 and 5. Their notes and owner questions stay here for reference.

**Track A: Tower Mode, keep playing the opening.** The owner plays new games and reports what breaks. Open items:
- C to SSR rank pictures for every building (only F/E/D exist; C+ reuse D).
- Priority buttons still cycle 2>3>0 (spaced apart now, not redesigned).
- A full play-through to day 10 with the new sell-back, quest and lessons.

> TT <next>. Tower Tycoon, Track A: play a new Tower game to day 5 in the Editor (drive it through RunCommand: press HUD buttons by name, `WorldTap` via SendMessage, `ScreenCapture`), list what breaks, fix it. Read `TOWER_DESIGN_HANDOFF.md` section 0 first.

**Track B: Dungeon Mode slice 2 (GDD 20.6), and tie the town to it.** Slice 1 is playable but untuned. Ask the owner first:
1. Did the raid loop feel right? Tune `LairBalance` (one file of numbers).
2. **Town as the adventurer economy:** parties that flee spend gold in town (placeholder: `5 x survivors x rank` gold in `FinishParty`). Should adventurers visibly arrive through the town, shop, rest at the inn, and pick quests there? Should the town's buildings set party size and rank (an inn draws bigger parties, a temple heals them)? **Answered 2026-10-04: yes, adventurers are town visitors (GDD 21.4).**
3. Order of the slice-2 list (**now set by the roadmap**: taming in batch 3 as Snare captures; relocation is moot because the vault moves as floors are dug): Heart relocation (`ShiftFloors` is ready), monster taming on expeditions, prison and converts, walked retreats, real dungeon art, a dungeon tutorial.

> TT <next>. Tower Tycoon, Track B: Dungeon Mode slice 2 and the town as the adventurer economy. Read `TOWER_DESIGN_HANDOFF.md` section 0 and `TOWER_MODE_GDD.md` section 20, then ask me the Track B questions before coding.

**Track C: Town slices 2 to 4 (GDD 19.2-19.4).** Assets are done; the questions in section 3 below are still open (town homes and the dweller cap, raider damage, growth speed, the gold loop, wages and prices, visitors and traders).

> TT <next>. Tower Tycoon, Track C: town lots as rules data, then the town economy (GDD 19.2-19.4). Read `TOWER_DESIGN_HANDOFF.md` sections 0 and 3 first and ask me the open questions before coding.

### Working rules learned this week

- The Editor may be in Play (owner or the BM session): never edit `.cs` during Play; exit, `AssetDatabase.Refresh()`, wait for the compile, then test.
- Run EditMode tests through Unity MCP `TestRunnerApi` with a callback that writes to a file; the run can leave an untitled scene, so reopen `Assets/Scenes/AdamsHavenTower.unity` before Play.
- Commit only our own hunks; the BM session owns `Battle/*`, `TowerExpedition*`, `TowerDungeon*`, `TowerMap*`, `AdamsHavenPrototype.cs` (one-line hooks only), `TowerSimulationTests.cs`.
- A resident who "bugs out" is plan thrash: log `currentTask`/`targetRoom` per frame; fix with hysteresis.

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
- **One world (2026-10-04):** one save; the dungeon is dug below the Tower (Dungeon Gate from the town, B1..Bn, living band, Heart vault at the bottom); it exists from the start and lures pests first, humans from Dungeon rank C; adventurers are town visitors; the Dungeon Mode fork retires after the merge, no dungeon-save migration (GDD 21).
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
