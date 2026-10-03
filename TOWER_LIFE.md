# Tower life layer (Sept 29)

Simulation (`Assets/Scripts/TowerLife.cs`, partial `TowerRules`):
- Traits (10) and schedules (day / night / flexible). Sleepers go home and lie down; emergencies with priority 2+ wake them.
- Mood = 50 + named thoughts (`Thoughts()`), shown in the MOOD tab. Below 18 for 25s triggers a 40s mood break (no work).
- Storyteller: `State.threat` (0-100) tracks pressure minus defence. It picks incident kinds (raiders need threat 35+),
  shortens event gaps, and drops 8 per resolved incident. Positive events: caravan, harvest festival, self-arriving traveller.
- New incident `cave_in` (underground rooms only), answered by repair priority.
- Goals: three active from `GoalPool`, counters bumped by build/collect/rush/recruit/etc. Claim pays gold/Celestium/tonics.
- Adjacency: same-type neighbours merge (+12% each, max 3); pairs like Kitchen+Well add +8%.

Presentation (`TowerFx.cs`, attached at runtime like TowerArtDirector):
- Day/night tint, warm window glows, fireflies, drifting clouds, Celestium motes up the core shaft.
- Tap-to-collect resource bubbles, hazard alerts with particles, floating result text, mood icons over residents.
- It reads state and listens to `TowerRules.Signal`; the only thing it changes is a bubble tap calling `Collect`.

HUD: story bar (clock, threat meter, GOALS), MOOD tab with schedule cycling, adjacency line in room details.

Tests: `TowerSimulationTests` (28). Run by calling the methods from `Unity_RunCommand`, or via Test Runner.
Do not edit scripts while Play Mode is running: the domain reload leaves the controller with null rules.

## Sept 29 (2): centred Heart, premium buttons, guide and needs validation

- Layout: the Heart shaft (cell 22) is the middle. Each floor has `west` and `east` founded cells; the Gate holds the first
  east cell on the ground floor. Expand with `ExpandFloor(floor, -1|+1)`; rooms build outward from the shaft on both sides.
  Older saves gain `east = 1` on every floor at load. New checkpoints put an east wing on floors -4..+4.
- Buttons: `TowerUiSkin` (glossy tinted body, gold frame, drop shadow) and `TowerButtonFx` (hover/press spring).
- Needs: `IncomePerMinute / UpkeepPerMinute / NetPerMinute` feed the coloured trends in the top bar; `NeedsAdvice()` shows one
  actionable line under the story bar (which producer to build or staff, or a bed for a waiting wanderer).
- Guide: lessons 2 and 5 open the RESIDENTS panel by themselves.
- Tests: 40 in `TowerSimulationTests`, including a full guide run for all three founders and a one-hour fed economy.

## Sept 29 (3): decluttered HUD

- Top bar: day/clock, threat meter, one line of primary stocks (tap it for wood/stone/ore/tonics), TIME and MENU.
- Dock: BUILD, FLOORS, PEOPLE, TASKS. Tap opens a popup (PEOPLE toggles the roster panel). Press and hold (about 0.4s) opens a flyout:
  BUILD -> category shortcuts, FLOORS -> floor jumps, PEOPLE -> tab shortcuts, TASKS -> claim all / recruit / auto-haul,
  TIME -> pause, 1x, 2x, 4x, 8x time lapse, MENU -> save, checkpoints.
- Room panel opens when a room is selected and has its own close button. Rooms are only placed while a card is in hand
  (chip above the dock, CANCEL or Esc to drop it). TASKS turns red on an incident and gold when a goal can be claimed.
- Messages are toasts that fade after a few seconds. `TowerHoldButton` (tap vs hold) and `TowerUiSkin.ApplyPanel` are reusable.

## Sept 29 (4): colony layer (Fallout Shelter x RimWorld)

All in `Assets/Scripts/TowerColony.cs` (partial `TowerRules`), hooked into the existing ticks.
- Levels: collecting, training, rescues, repelling incidents and expeditions give XP. Level-ups heal fully and raise
  `MaxHp` (105 + 2.5 per level). The roster shows `Lv`; resident details show xp/next and HP/max.
- Brownout: with no firewood banked, `TickPower` keeps only as many cells lit as the mills can feed, nearest the Heart
  first. Dark rooms stop working and are drawn dark with "NO FIREWOOD". Mills, Heart and Gate never go dark.
- Gate guard post: the Gate takes 2 workers (task `guard`, uses Defend priority). `StartRaid` puts raiders at the Gate.
  Undefended raiders loot gold (returned if beaten), push to a neighbouring room every 30s (same floor, or via the core
  landing), and only wound the Heart inside its chamber. Merged same-type rooms draw without the dividing post.
- Health: collapsing from hunger is not a wound. Downed residents who are fed get back up on bed rest. Critical ones
  (injury >= 50) bleed out after 240 live seconds untended (a full day if starving). Villagers die into
  `State.memorial`; heroes are pulled back by the Heart. Care works without tonics at 45% strength. Offline catch-up never kills.
- Relationships: residents sharing a room build opinion (`State.bonds`); traits scale it, misery reverses it. Rivals
  can fight (injuries), close friends can fall in love (auto family). Thoughts: friend/rival nearby, mourning, partner lost.
- Mood breaks by temperament: sulk (home), binge (eats stores), tantrum (damages workplace), wander (to the Gate).
- Steward (on by default, TASKS toggle): every 20s (5s when a stock is empty) moves one best-matching worker onto the
  most urgent failing stock, using `PlannedNetPerMinute`. AUTO-ASSIGN IDLE places jobless adults by best stat.
- Checkpoints: `TowerMilestones.Version = 2`. Generation staffs survival rooms and posts two Gate guards. On start, slots
  2-10 built by an older generator are rebuilt and the old file is kept as `slot_XX.json.gen0.bak`. Slot 1 is never touched.
- Tests: 50 in `TowerSimulationTests`, including every checkpoint feeding itself for 30 minutes with no deaths.

## Sept 29 (5): construction time, zoom
- `TowerConstruction.cs`: `Build`, `OpenFloor` and `ExpandFloor` start a `TowerWork` job in `State.works` (cost paid up front) that counts down in game
  seconds, offline too (`CatchUp`). Room 15+10*width+cost/20 s, wing 20+3*cells, floor 45+3*|n|; halved while the guide runs (`tutorialStep < 7`).
  Sites reserve their cells (`WorkRoomAt`), one wing job per side, one job per floor. `TowerRules.InstantConstruction` is for tests only.
- Art (from S:\AI\Game\art): `Structure/construction_frame.png` (tower/pilot_hd_v1/building_shell_v1) and `construction_floor.png`
  (tower/silverbrook_low_tier/empty_floor). `TowerArtDirector.BuildSites` draws frame, fading preview of the room, timer and bar.
- Camera: mouse wheel and pinch zoom toward the cursor / pinch centre (2.5..30), two-finger drag pans, `UserView` stops auto-reframing.
- Tests: 51 (adds `ConstructionTakesTimeAndCompletesOffline`).

## Sept 29 (6): Guild Hall expeditions
- `TowerGuild.cs`: expeditions are chosen at the Silverbrook Adventure Guild. `GuildRequired()` gates the HUD (build one first; the blueprint is granted at
  `ChooseStarter`). Battle tiers by hall level: OUTSKIRTS (Lv1, depth 1, 80g), DEEP WOODS (Lv2, depth 4, 160g), BOSS LAIR (Lv3, depth 8, 300g).
- HUD: selecting a Guild Hall opens the board (`popupGuild`): three battle tiers plus SUPPLIES / RELICS / PATROL / RECALL for the selected resident.
  Also PEOPLE flyout -> GUILD EXPEDITIONS, the room panel EXPEDITIONS button, and the resident panel GO tab's GUILD button.
  `SendExploring` itself stays ungated so tests and saves are unaffected.
- Art: `Rooms/guild_hall_F_v2.png` from Game Assets/buildings/tower/buildings/Guild Hall (level 1). E and D still use the old art until replaced.

## Sept 29 (7): Guild expeditions (region map -> plan -> forest map -> D&D dungeon crawl)
- Entry: Guild Hall board -> OPEN THE EXPEDITION MAP (or RESUME). `AdamsHavenPrototype.OpenExpedition/CloseExpedition` hide/show the Tower
  (`HideTower/ShowTower`, shared with battle). `TowerExpeditionUi` owns its own canvas.
- Rules (no UI): `TowerExpedition.cs` (8 regions in an unlock chain starting at Silverbrook Edge, plan validation, provisions,
  haul + 2-slot Safe Pocket, forest travel costs rations, EndExpedition), `TowerForestLayouts.cs` (15 maps; fog_hollow is hand-placed in
  `Resources/AdamsHaven/Expedition/forest_layouts.json`, the rest come from a generator seeded by map id so each map never changes;
  silverfall_glen / thornwood_gate / ashen_barrow show a mirrored stand-in plate until they get their own painting),
  `TowerDungeon.cs` (20x20 grid, layout fixed by map+POI+floor, room contents rolled from the run seed; fog of war; room events;
  stairs; goal room clears the POI; the lair's boss conquers the region). State: `regionsUnlocked/regionsConquered/hasRun/run/lastLayout`.
- Themes (ruin, cave, marsh, crystal, briar, keep) come from the POI/landmark terrain. `TowerDungeonTiles.cs` paints each theme's
  floor/corridor/wall (autotile by neighbour mask)/door at runtime; drop `Expedition/Tiles/<theme>_<part>.png` to override.
- Battle hook (additive): `BattleMode.Begin(depth, field, reserve, onLeave)`, `BattleMode.ReturnLabel`, `BattleCatalog.Party(ids)`.
  Gear: hero weapon level +8% attack, tool level +6% HP. HP carries between fights as percent.
- Art copied from Game Assets (Godot-era, used only as art): expedition region/forest maps, trail props, dungeon parchment/fog mats,
  room scenes per category, node icons. Importer rule in `TowerPresentationImporter` for `/AdamsHaven/Expedition/`.
- Tests: 60 in `TowerSimulationTests` (layouts fixed and connected, dungeons deterministic and reachable, full crawl, wipe keeps pocket,
  region unlocks, save round trip). The old guild tier buttons are gone; `ExpeditionTiers/CanLaunchBattle` remain but are unused.

## Oct 2: 3D chibi residents in the live Tower

- `TowerResident3D` (Assets/Scripts) shows a resident as its rigged `TowerChibi3D` model instead of the 2D atlas quad.
  `ModelFor()` picks it: roster heroes ghislaine/kaela/helda/elara/clarity use their own rig; Celestium bodies map
  normal -> `celestium-med`, short -> `celestium-short`, muscle -> `celestium-muscle`. Tall/hourglass bodies and every other
  unit (amara, daisy, villagers, children) keep the 2D `TowerChibiAnimator` / painted chibi path.
- Clips: knocked down > sleep (futon) > walk > the room's work loop (`TowerChibi3D.RoomClips`, hauling plays the barn loop) > idle.
  Walking turns the model 70 degrees toward its heading; idle settles at a 20 degree three-quarter view; work faces the camera.
- Placement follows `TowerRoomDepth`: the transform sits at the quad centre, the model's feet on the floor, scaled by depth.
- Rigs are pooled in "Tower 3D residents" across `RebuildScene` (off-screen ones parked inactive, removed when the resident
  leaves) and hidden while Battle / expeditions own the screen.
- Residents at work or resting in the same room now share it out in id order (`ResidentPosition`) instead of stacking.

## Oct 3: TT 10.30.0 - Fallout Shelter x RimWorld core, districts, outposts

Owner direction: Fallout Shelter x RimWorld is the foundation (colony depth is core, not DLC), with Cities: Skylines-style
management on top. Design: `TOWER_MODE_GDD.md` 6.3, 8.4, 9.3, 9.6, 10.3, 17, 18. Tests: `Assets/Editor/TowerManagementTests.cs`
(separate from `TowerSimulationTests`, which the Expedition session also edits).

- Groundwork (`TowerLayoutTools.cs`): `ColonyVersion` + `MigrateColony()` after `MigrateResearch()` (no schema bump: `Load`
  only accepts schema 1-2). `State.colonyRandom` is the new systems' own xorshift, so `randomState` sequences never shift.
  `layoutStamp` (bumped by AddRoom, UpgradeRoom, Demolish, MoveRoom, floor and wing completion, zoning) keys the caches.
- Floor caps by Heart rank, stretched to the +-24 world: up 2,3,5,7,9,12,15,19,24 / down 1,3,5,7,9,12,15,19,24.
  `OpenFloor` refuses past the cap; open floors are never removed. Floors popup says which Heart rank opens the next one.
- MOVE (room card, then tap the new place; `AdamsHavenPrototype.BeginMove` / `PlaceMovingRoom`) keeps uid, rank, workers,
  residents, progress and condition; fee 20 + 10 x width x rank. DEMOLISH (tap twice) refunds 40% of the catalogue price
  plus a quarter of upgrade gold, evacuates the room and rehouses the homeless.
- Colony depth (`TowerColonyDepth.cs`): newcomers (recruits, grown children) get `trait2` and a `backstory` (10; stat bonus,
  at most one barred job; `SetPriority` refuses it). Miserable Gate villagers leave after 1,080 live s (warning at 540);
  heroes, bodies and summoned named residents never do; nothing leaves offline. 720 s at 85+ mood gives a 240 s
  inspiration (work / care / guard x1.5). Storytellers calm / balanced (default, identical to before) / chaotic on the
  Heart STATUS tab, carried into Legacy runs. WORK GRID (`TowerHudWork.cs`, PEOPLE hold menu): all adults x six jobs.
- Districts (`TowerDistricts.cs`, `TowerHudDistricts.cs`): floor bands, 0..6 by Heart rank; Residential / Industry /
  Market / Arcane by purity; 8 policies with gold upkeep that lapse when unpaid; service coverage (Medical, Safety, Food,
  Leisure) by radius; per-floor appeal (thoughts, faster Gate arrivals). Per-floor arrays rebuilt on layout change and every
  10 s. Info views (`TowerArtDirector.Overlay`) tint each floor; `SceneSignature` hashes them.
- Postings (`TowerAutoExpedition.cs`): `resident.posting` ("auto" / "outpost:<region>") + `exploring = true`, because the
  expedition code rewrites `away` on every load. Posted residents skip needs and upkeep, lose their job slot, keep their bed.
- Auto expeditions (GDD 10.3): one party, real-time, power vs rescaled danger, Fail/Partial/Success/Great, never conquers,
  2 Sigils a return (6 a day). AUTO EXPEDITION on the Guild board and the Expeditions dock hold menu (`TowerHudExpeditions.cs`).
- Outposts (`TowerOutposts.cs`, `TowerHudOutposts.cs`): a site per conquered region (no expedition-file hook: sites are
  `regionsConquered` minus outposts), staffed colonies with region goods, caravans every 360 s with ambush risk, outpost
  threat and raids (live raids wound, offline never), ranks F-SSR capped by the Heart, homesickness after 3 game days.
- Gate sieges (`TowerSiege.cs`, GDD 9.6 auto-defend): a due event at threat 75+ becomes a 600 s siege warning; defence vs
  wave; a loss is a Last Stand raid at a Gate. First siege after 2,160 live s; live only.
- `TowerPerfProbe` also times TickDistricts, TickOutposts and TickSiege.
- Left for the Expedition / Battle sessions: an outpost marker on the Atlas (`TowerMapSources.cs`), the Battle Mode siege option.
