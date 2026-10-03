# Tower Mode handoff (for the next TT session)

Updated 2026-10-03 after TT 10.30.2. Read this first, then `TOWER_MODE_GDD.md` (design authority), `TOWER_LIFE.md` (build log, newest at the bottom) and `TOWER_BALANCE_REPORT.md` (measured numbers).

## 0. Start here

**Next job: summon banners (GDD 8.1). The owner approved them on 2026-10-03.** Ask the four questions in section 3 first, then build them as section 3 lays out.

Paste-ready prompt for the next session:

> TT <next version>. Tower Tycoon: build the summon banners from GDD 8.1 (Standard, Featured, Pick-Your-Hero, Resident). Read `TOWER_DESIGN_HANDOFF.md` section 0 and 3 first and ask me its open questions before coding.

## 1. Where things stand

Tower Mode is a **Fallout Shelter x RimWorld** tower town in Unity (`S:\AI\Game\Unity AHCG\My project`, branch master), with **Cities: Skylines-style management** on top. The player is the Summoner/Steward of the Celestium Heart.

**Tests (2026-10-03):** `TowerManagementTests` 51 + `TowerSimulationTests` 119, all green (EditMode). The Expedition suites (owned by the other session) had one failure in its untracked, in-progress `ExpeditionAtlasTests.VaultsAlwaysHoldAFightFromLayoutVersionThree`. It is not a Tower test.

| Area | State |
| --- | --- |
| Core loop | Rooms F to SSR with 1/2/3-bay growth, stat matching, collect / rush, adjacency, firewood brownouts, construction timers, MOVE / DEMOLISH (also long-press), floor caps by Heart rank (stretched to ±24) |
| Heart | Ranks F to SSR with building and research caps, dweller cap (8 to 110), warning stages, hard fail with a rogue-lite Legacy restart, 40-node research tree, summons (one standard pool, pity 50/60), slow first-run climb |
| Colony (RimWorld, core) | Needs, thought-based mood, 10 traits, a second trait and a backstory for newcomers, mood breaks, leaving, inspirations, bonds / families / grief, work priorities + WORK GRID, schedules, three storytellers (picked on the founding panel and the Heart STATUS tab) |
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

## 3. Next: summon banners (GDD 8.1)

**Today:** one pool in `TowerHeart.cs` (`Summon(count)`, about line 230).
- `RollRank` uses the GDD standard rates, with soft pity from pull 50 (45 with EXP-5) and hard pity at 60; EXP-8 adds +0.5% SSR.
- A 10-pull guarantees one B or better. The free first summon is a hero of B or better.
- `HeroShare` 0.6 picks hero vs resident per pull. Duplicate heroes fuse (rank up or +1 level).
- With no free bed, a summon waits inside the Heart.
- The HUD is the Heart hub's SUMMON tab (`TowerHudHeart.cs`: `BuildHeartPopup` and the "Summon" block of `RefreshHeart`).

**The design (GDD 8.1, wireframe 12.14):**

| Banner | Pool | Cost | Rules |
| --- | --- | --- | --- |
| Standard | all heroes | 10 / 100 Sigils | permanent |
| Featured | all heroes, rate-up | 10 / 100 | rotating; one SSR and one SS hero featured; 50% of SSR/SS pulls are the featured one, and after a miss the next SSR/SS is guaranteed to be it |
| Pick-Your-Hero | all heroes | 20 / 200 | the player picks one SSR or SS target; Featured's rules aimed at that hero |
| Resident | the 24 residents | 5 / 50 | SSR about 1%; guaranteed A or better every 20 pulls |

**Ask the owner first** (GDD 16 item 6 leaves these open):
1. Should Standard become heroes only, as the GDD says, now that the Resident banner exists? Or keep today's 60/40 hero/resident mix?
2. Is SSR pity shared across the hero banners (Standard, Featured, Pick) or kept per banner? The Resident banner keeps its own A-every-20 counter either way.
3. What is the Featured rotation cadence (suggest: weekly by the local calendar, like the daily board's `TowerRules.Today`), and is the featured pair automatic or hand-authored?
4. Pick-Your-Hero: can the target change mid-way, and does the "guaranteed after a miss" flag carry over when it does?

**Suggested build:**
- **Data:** a `TowerBannerDef` table (`id`, name, pool, pull and ten-pull cost, rate-up rule) in a new `TowerBanners.cs` (partial `TowerRules`).
- **State, new fields** (defaults load fine; no schema bump):
  - `summonBanner` (the UI's last choice)
  - `featuredMissed`, `pickTarget`, `pickMissed`
  - `residentPity`
  - per-banner pity, only if the owner wants it
- **Rolls:** keep `Summon(count)` as the Standard banner, so `SummonsSpendSigilsAndHardPityGivesAnSsr`, `SummonsCostTenSigilsAndEveryTenPullHoldsABOrBetter`, `SummonsDrawNamedUnitsOfTheRolledRankFromTheRoster` and `SummonsWithNoFreeBedWaitInsideTheHeartUntilOneOpens` (all in `TowerSimulationTests`) stay green. Add `Summon(count, bannerId)`.
  - New banners may use `Random01()`, since summons already do. Do not add calls on the Standard path unless the owner changes its pool.
  - Featured and Pick: on an SS/SSR result, roll 50/50 (or the guarantee) between the target and the rank pool, then set or clear the missed flag.
  - Resident: the same `RollRank` ladder with SSR forced to 1%, and A+ forced on the 20th pull since the last A+.
- **Featured pair:** a deterministic pick from `TowerRoster.OfRank(9, true)` and `OfRank(8, true)`, indexed by week number. It shows on the banner with its end date.
- **HUD:**
  - A banner tab row in the SUMMON tab (Standard / Featured / Pick-Your-Hero / Resident).
  - Per-banner rates text, costs on the ×1 / ×10 buttons, and the pity meter for that banner.
  - A target chooser for Pick-Your-Hero (cycle SSR and SS heroes; show the name, element and role from `TowerRoster`).
  - Keep the GDD's mandatory rates screen (the existing rates text).
- **Tests** (in `TowerManagementTests`):
  - Standard is unchanged.
  - Featured: half of the top pulls hit, a guarantee after a miss, weekly rotation.
  - Pick: costs double and aims at the target.
  - Resident: costs 5 and gives A+ every 20.
  - Banner state survives a save.
- **Docs:** GDD 8.1 "built" notes, `TOWER_LIFE.md` entry, this file.

## 4. Other open work (offer these after banners)

1. Import the generated character art (`CharacterPrompts/_source/import_roster_art.py`, subset or `--all`) and use it in the People panel, hero detail and the summon reveal. Decide what ships: Resources ships everything, and the APK is already about 965 MB.
2. Rank-specific building functions and SSR buffs (GDD 5.3). This needs the owner deep-dive first.
3. A measured balance playthrough of the TT 10.30.x numbers (GDD 16 item 14): districts, outposts, auto expeditions, sieges, leaving and inspiration timers.
4. Smaller TBDs in GDD 16: breach damage rates, siege waves, region passives, rations curve, Echoes, ascension cost, a dedicated outpost / siege art pass.

## 5. Where things live

| What | Path (under `My project`) |
| --- | --- |
| Design authority | `TOWER_MODE_GDD.md` (sections 17 Districts and 18 Outposts are new) |
| Build log / balance | `TOWER_LIFE.md`, `TOWER_BALANCE_REPORT.md` |
| Rules and state | `Assets/Scripts/TowerDomain.cs` (state, catalog, build / upgrade, dweller cap), `TowerSystems*.cs` (tick), `TowerLife.cs` (mood, goals, storyteller), `TowerColony.cs` (levels, raids, bonds, Steward), `TowerHeart.cs` (Heart, Legacy, summoning), `TowerResearch.cs`, `TowerSigils.cs`, `TowerMilestones.cs` (checkpoints) |
| TT 10.30.x systems | `TowerLayoutTools.cs` (floor caps, move / demolish, `MigrateColony`, colony random), `TowerColonyDepth.cs` (traits, backstories, leaving, inspirations, storytellers), `TowerDistricts.cs`, `TowerAutoExpedition.cs` (postings, auto runs, elements), `TowerOutposts.cs`, `TowerSiege.cs`, `TowerSiegeBattle.cs` (Battle Mode bridge) |
| HUD | `TowerHud.cs` (+ partials `TowerHudChrome`, `TowerHudHeart`, `TowerHudResearch`, `TowerHudWork`, `TowerHudDistricts`, `TowerHudExpeditions`, `TowerHudOutposts`, `TowerHudSiege`, `TowerHudDev`), `TowerUiSkin.cs`, `TowerArtDirector.cs` (cutaway art and info-view overlays), `TowerFx.cs` |
| Roster | `TowerRoster.cs`, `Assets/Resources/AdamsHaven/Roster/tower_roster.json` (the six founding Fighters are not in it; their element and role come from `BattleCatalog`) |
| Tests | `Assets/Editor/TowerManagementTests.cs` (Tower only, put new tests here), `TowerSimulationTests.cs` (shared with the Expedition work), `TowerPerfProbe.cs` (`Tools > Adams Haven > Tower Perf Probe`) |
| Character pack | `CharacterPrompts/` (`roster.csv/json`, `_source/*.py`, `Generated/` art, never staged) |

## 6. Working notes and traps

- **The other session** works on Expedition / Atlas / Battle at the same time and leaves uncommitted edits. Today that includes `AdamsHavenPrototype.cs`, `TowerSimulationTests.cs`, `TowerExpedition*.cs`, `TowerDungeon.cs`, `TowerMap*.cs` and `Battle/*`, plus untracked `ExpeditionAtlasTests.cs`, `TowerAtlasNodes.cs` and `TowerMapWeb.cs`.
  - Commit only your own hunks: write a patch from your pre-edit copy to your version, then `git apply --cached`.
  - Then typecheck the **exported index** (`git ls-files 'Assets/*.cs' | git checkout-index --stdin --prefix=<dir>/` + Roslyn), so a commit never needs their unstaged or untracked work.
- **Play mode:** check `EditorApplication.isPlaying` before any .cs edit. The Editor can sit in Play for hours. Never edit scripts during your own Play check either: Unity hot-reloads, the HUD's fields go null and you get an NRE in `RefreshTopChrome`. If Play is on, ask the owner to stop it; meanwhile stage edits in a scratch copy and typecheck offline with Unity's Roslyn from the csproj HintPaths (relative paths resolve against the project).
- **Unity MCP `Unity_RunCommand`:**
  - `System.Reflection` is forbidden, and nested classes get duplicated: declare `ICallbacks` collectors as top-level `internal` classes.
  - Run tests with `TestRunnerApi`: EditMode, groups `^TowerManagementTests`, `^TowerSimulationTests`, `^Expedition`, `runSynchronously`.
  - `SendMessage("Name")` reaches private no-arg HUD methods. With an int argument, 0 binds to `SendMessageOptions`; click buttons with `onClick.Invoke()` instead, and box other values: `SendMessage("TapAtlas", (object)vp)`.
  - BattleMode is IMGUI: drive it with `DebugMouse(point on its 1600x900 virtual screen)` + `DebugClick()`.
  - A screenshot taken the frame a popup opens shows unskinned buttons; capture one frame later.
- **Play checks:** back up `LocalLow\DefaultCompany\My project\AdamsHavenTower` first; `AdamsHavenPrototype.LoadCheckpoint(n)` and `NewGame()` save the current slot. Afterwards restore the slots, move any slot 0 you created out of the folder, and set the PlayerPref `AdamsHaven.Tower.LastSlot` back to 1.
- **Saves:** never bump `TowerState.schema` (`Load` accepts only 1 and 2). Migrate through `ColonyVersion` / `MigrateColony()`, and put list guards in `NormalizeColony()` (runs every load). New random rolls in Tower systems use `ColonyRandom01()`, not `Random01()`, so seeded tests keep their sequences.
- **Postings:** a resident away for a Tower reason (auto expedition, outpost) has `posting` set and `exploring = true`. Never reuse `away`: the expedition code rewrites it on every load. `Recall` routes postings home.
- **Caches:** district / coverage / appeal arrays rebuild when `LayoutStamp` (or room, floor or resident counts) change, and every 10 s. Anything that changes the tower's shape calls `TouchLayout()`.
- Tower statics survive leaving Play (`TowerRules.DebugIgnoreGuild` is reset by `TowerDebugFlagReset.cs`; restore `InstantConstruction` after experiments). A game day is 720 s. Time uses `TowerRules.FrameSeconds(deltaTime, speed)`; speed 0 advances nothing.
- Bash heredocs can mangle backslashes in Python strings: write Python helpers to files, or use the Edit tool for C# strings with `\n`.
