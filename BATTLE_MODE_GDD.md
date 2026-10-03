# Battle Mode GDD: Silverwood Expeditions

Status: rewritten 2026-10-02 to match the game as built after the expedition review (commits 8d59843 to the step 8 commit). Sections marked **Built** describe code; **Planned** and **Open** are still design. The first draft (2026-09-30, painted plates and two map modes) is in git history.

Related docs: [BATTLE_PORT.md](BATTLE_PORT.md) (battle sandbox, `AnimationSignal`), [CHAOS_ZERO_REFERENCE.md](CHAOS_ZERO_REFERENCE.md) (CZN alignment), [TOWER_LIFE.md](TOWER_LIFE.md) (colony, storyteller), [TOWER_MODE_GDD.md](TOWER_MODE_GDD.md) §10 (the Tower side of expeditions), [BATTLE_UI_ART.md](BATTLE_UI_ART.md).

---

## 1. Vision and pillars

Leave the Tower, walk into the Silverwood, come back with loot and scars. An expedition is a short-session roguelite made of four layers:

| Layer | Feel | Player verbs |
| --- | --- | --- |
| Overview map | A generated forest you uncover | Plan a route, spend rations, wear a road |
| Traversal | Tabletop roleplay beats | Choose, roll trait checks, accept consequences |
| POI dungeon | D&D room crawl in fog of war | Explore, trigger rooms, fight, loot |
| Battle | Chaos Zero Nightmare-style card combat | Chain cards, manage AP/EP/Stress, ultimates |

Pillars:
1. **The forest is alive.** It rearranges every run and shifts mid-run when threat fills.
2. **Every step costs.** Rations, Stress, HP, threat, weather and the clock force trade-offs.
3. **The Tower matters.** Colony stores, gear and Guild feed expeditions; hauls feed the colony.
4. **Short and replayable.** One region is 10 to 20 minutes on a phone. State is saved after every action, so quit-and-resume always works.
5. **Authored characters, random world.** Kits and art are fixed; maps, events and loot are rolled.

## 2. Rulings (decided 2026-10-02)

These settle the contradictions found in the review. Code follows them.

| Topic | Ruling |
| --- | --- |
| Party | Up to 6 fighters: the first 3 fight, the rest wait in reserve. Fighters come from the Tower's heroes. |
| Leaving early | Heading home from the camp banks the whole haul. Leaving from anywhere else is a retreat: a confirm, then 25% of every find (gold, ore, essence, celestium) is dropped. The 2-slot Safe Pocket is never taxed. |
| Map | One procedurally generated grid map per run. The painted plates are retired (deleted 2026-10-02; old plate runs are moved onto the grid on load). |
| "Atlas" | The Atlas is the region overview screen. The old "Atlas reward mode" is renamed **Offerings** (section 13) and is not built. |
| Traits and bonds | Trait checks are live in events, traps and skill rooms. Bonds are deferred. |
| Orientation | Landscape (all expedition UI is laid out for 16:9). |
| Hero source | Heroes come from Sigil summons in the Tower. No shards or omens. |
| Haul currencies | Gold, ore, essence, celestium, tonics (what the code banks). |
| Regions | 13, from Silverbrook Edge to Silverwood depth 5 (section 9). |
| Battle speed | 0.5x by default so moves can be admired, 1x and 2x on the speed button; ultimate cut-ins can be skipped with a tap. |

## 3. Macro loop (Built)

1. **Atlas:** pick an unlocked region. Regions open by conquering their neighbours and, for regions 2 to 8, by research at the Heart.
2. **Plan:** pick the party (up to 6), rations, tonics and firewood from Tower stores.
3. **Region card:** name, biome, danger, weather and a one-line hook.
4. **Overview map:** walk the fogged grid, reveal it, wear roads, meet events, enter places.
5. **Places:** dungeons, merchants, shrines, camps; win fights and pick rewards.
6. **Lair:** clear the region's 3-floor lair to conquer it (conquest card, next regions open).
7. **Home:** head home from camp (or retreat). The run summary shows days out, places, fights, what was banked and what was dropped.

### Win, lose, retreat

| Outcome | Result |
| --- | --- |
| Head home from camp | Whole haul banked (+20% gold with research EXP-2), unused supplies return, Sigils paid. |
| Retreat elsewhere | Same, minus 25% of each find. Confirmed by a second tap. |
| Party wipe | Only the Safe Pocket and experience come home. Heroes return wounded. |
| Lair cleared | Region conquered; the run continues until the party heads home. |

Code: `TowerRules.EndExpedition(bool wiped)`, `AtSafeExit` (within 2 cells of the camp), `RetreatLoss`, `LastSummary`.

## 4. The overview map (Built)

- **Generator:** `TowerOverworldGen` builds a 64x40 cell map from the region's biome and the run seed: terrain (forest, dense forest, clearings, hills, rocks, rivers, lakes, marsh, ruins, blight), roads, bridges, fords and places. Map version 3; old runs keep the generator version they started with.
- **Biomes:** edge (Silverbrook Edge, Rootside Camp), lakes (Shallow Ford, Old Bridge, Sunken Marsh), ruins (Moon Shrine, Silverwood Gate), mountains (Watchpost), and one per Silverwood depth (edge, lakes, mountains, ruins, heart).
- **Places per map:** camp and lair always; the rest by biome (for example the lakes hide more mysteries, the mountains more elites and vaults, the Heart more elites and fewer packs).
- **Movement:** tap any seen cell to preview the route (rations, threat, % on road, a warning if the walk would fill threat); tap again or WALK to go. A* over terrain cost.
- **Roads:** walking a cell wears it (3 walks make road, up to 9). Road cells cost less and raise less threat.
- **Fog:** sight radius 5 cells (+2 per extra reveal step from a Curious hero by day or with a Night Owl, and Guild level 3; fog weather -2, never below 3).
- **Threat:** per cell 0.8 on trail, 0.3 on road, +0.4 at night; battles +4; clearing a place -15; camp rest -25. At 75 a warning; at 100 the forest **shifts**: unexplored places move, the woods regrow, some road is lost (never at the camp, cleared places or the party), threat resets to 40.
- **Daily shift:** each new day one unexplored place moves.
- **Rendering:** `TowerMapView` draws ground tiles, terrain masks (12 px per cell), stamps (trees, rocks), props, a fog layer tinted by biome and reddening with threat, and the walked road. The map is tinted by the hour (dusk, night, dawn) and shows rain streaks or fog haze.
- **Route bar:** a seen place shows its kind, cleared state and a hint (danger, reward).

Rations: 1 ration per 20 cost units walked (about one old trail hop), +20% in rain. At zero rations each ration due costs every fighter 10% HP instead.

## 5. Time and weather (Built)

- The Tower pauses while an expedition is out; the run keeps its own clock (`run.clock`). Walking costs 8 minutes per cost unit, a fight 30 minutes, cooking 1 hour, scouting 2 hours, a camp rest 3 hours by day or until 06:00 at night.
- Top bar: `Day N  •  HH:MM  •  part of day  •  weather`.
- **Weather** is drawn once per run day from the run seed, weighted by biome: clear, **rain** (walking threat x0.7, rations x1.2) or **fog** (sight -2). `TowerRules.Weathers` turns it off for rules tests.
- Night raises threat per cell, changes which events can fire and fades in the night ambience.

## 6. Traversal events (Built)

| Rule | Value |
| --- | --- |
| Roll | Every 10 cells walked (and at the end of a walk): 35% on trail, 10% on road, +25% at full threat. Never two in a row. None within 3 cells of the camp or the lair. |
| Selection | Weighted by depth, the nearest place's theme, day/night, road, `needsFlag`, not seen this run. Seeded by run seed + step. |
| Check | Base chance; a hero with the named trait adds the trait bonus (+30% default). Under = success, within 20% above = partial (if written), else failure. Odds shown on the buttons. |
| Costs and consequences | Rations, firewood, tonics, gold from the haul; HP, loot, threat, reveal, flags, ambush. |
| Rest before the boss | **The Quiet Glade** fires once within 9 cells of the lair. |
| Full threat | **The Forest Closes In**: fight or flee. |
| Content | 54 events in `Resources/AdamsHaven/Events/traversal.json`, covering every theme including mine, heartwood and blight, plus the two scripted ones. |

Party **banter** (`TowerBanter`): after some walks and at camp, a living fighter says a line picked by mood (hurt, threat, near the lair, camp, rain, fog, night, road, wild). The lines are first drafts.

## 7. Places and dungeons (Built)

| Place | What happens |
| --- | --- |
| Combat / elite | Dungeon crawl; elite fights roll an affix and always offer a relic. |
| Mystery | Dungeon with unknown rooms and event-like rooms. |
| Treasure (vault) | Dungeon whose goal offers a pick of rewards. |
| Shrine | Trade HP or rations for a relic, or pray (heal 25%, ease Stress). |
| Merchant | Buy tonics (40g), 2 rations (30g), firewood (20g), a relic (160g) with haul gold. |
| Camp | Rest (1 firewood: heal 35%, Stress -40, threat -25), Cook (1 ration: heal 15%, Stress -25), Scout (2 hours: wider sight). Safe place to head home. |
| Lair | 3 floors, region boss at the bottom. |

Dungeons (`TowerDungeon`): a 20x20 grid of rooms on a 3x3 sector layout, fogged, click-to-route. Layout version 2 picks one of several shapes per visit from the run seed; version 1 saves keep the plus shape; version 0 saves are walked back out of the dungeon on load. Floors: lair 3, elite and landmark 2, others 1.

Room kinds: enemy, elite, boss, treasure, merchant, shrine, rest, stairs, unknown, **trap** (disarm with a trait check or dash through), **skill check** (force it open for loot), **story** (lore and a flag), **key gate** (a sealed door with loot behind it). Finished rooms stay finished when the party re-enters (`run.roomsCleared`).

## 8. Battle (Built)

Authoritative detail: BATTLE_PORT.md and CHAOS_ZERO_REFERENCE.md.

- 3 field fighters, 3 reserves, shared deck, per-fighter AP/EP, JD (summoner) with CP/SP. Enemy intents are telegraphed.
- **Encounters** (`BattleCatalog.Build(BattleEncounterSpec)`): 12 species with themes and minimum depths; line-ups drawn per region, theme and kind (normal, elite, boss). Stats scale with depth (health +12%, offence +6.5% per depth); elites x2 power, bosses x3.4.
- **Elite affixes:** Vampiric, Thorned, Hasted, Shielded, Enraged.
- **Lair bosses:** one named boss per region (for example the Amberhide Matriarch at Silverbrook Edge, the Heartrot Colossus at Silverwood depth 5), two phases with a telegraphed signature move.
- **Enemy AI:** prefers hurt or weak targets, buffs when allies are hurt, and aims at JD about one attack in eight.
- **Statuses:** stun, slow (costs AP), burn, shield, charging, plus the original set.
- **Run rewards** (`TowerRunRewards`): after each win pick 1 of 3: upgrade a fighter's card (up to level 3, +20% power per level), a relic (9 relics), or supplies. Elites, bosses and vaults always offer a relic. Upgrades and relics last for the run.
- **Carry-over:** HP and Stress persist between fights; camps, tonics, shrines and rest rooms heal.
- **Seeded fights:** the fight's seed comes from the run, so quitting mid-fight replays the same battle.
- **Pacing:** speed 0.5x / 1x / 2x (0.5x default, remembered). Auto battle plays the whole hand with ultimates (`BattleAutoPlayer`). Tap skips ultimate cut-ins.
- **Context:** background art per dungeon theme where landscape art exists (blight, crystal, heartwood, mine), the region name in the header.

### 2D anime rigs, move effects and cinematics (pilot: Kaela, 2026-10-02)

- **2D rig:** the fighter's anime art as a cutout puppet (20 bones, 21 layered parts, Celestium gauntlets on the hand
  bones) with the same clip names and contact times as the 3D rigs, so the battle drives either. The character sheet's
  SHOW 3D MODEL / SHOW 2D ART switch flips the whole field and is remembered. Pipeline: `Tools/build_2d_rig.py`
  (Qwen Image Edit A-pose from the T-pose sheet, BiRefNet cutout) -> `Tools/build_2d_rig_parts.py` (SAM regions,
  nearest-bone labels, hidden-limb fill) -> `Battle2DRigBuilder` + `Battle2DMoves` (prefab and keyframed clips).
- **Cinematic tiers:** ultimates and awakenings always have a cinematic; a skill card has one only if
  `UltCutIns/<card id>.mp4` exists (Kaela: Shatter). The CINE button (under LOG) picks every time / first use per
  battle (default) / ultimates only / off. Clips play at 1x whatever the battle speed; a tap skips.
- **Match cut:** for a 2D fighter the field zooms in until it matches the clip's first frame, the clip plays, its last
  frame (the same stance) zooms back out. Both frames come from the rig itself (`RenderCineFrame`) over `Fx/cine_bg`,
  and `Tools/produce_cine.py` generates the clip with MiniMax H3 first/last-frame video. `BattleMotion/<job>/key_first.png`
  and `key_last.png` stay for a higher-quality pass (for example Seedance) with the same anchors.
- **Move effects:** `Tools/produce_move_fx.py` renders effects on black with H3: short ones become 16-frame colour sheets
  (`Fx/Moves/<card id>.png`), long ones H.264 video with the matte stacked under the colour (`Fx/Moves/<card id>.mp4`,
  drawn by `Fx/StackedAlpha.shader`, hardware-decoded on Android). Placement per card: `BattleCinematics.MoveFx`.

Difficulty, measured by the headless sim (`ExpeditionBalanceTests`): normal fights cost about 4-14% party HP, elites 10-30%, bosses 20-65%.

## 9. Regions and progression (Built)

13 regions: Silverbrook Edge → Rootside Camp / Shallow Ford → Moon Shrine / Old Bridge / Sunken Marsh → Watchpost → Silverwood Gate → Silverwood depths 1 to 5. Conquering a region's lair opens its neighbours; regions 2 to 8 also need their map researched at the Heart (EXP branch). The lock text names whichever is missing.

What stays fixed between runs: conquered regions, research, the journal, hero levels and gear. Each run generates a fresh map.

## 10. Rewards and meta-progression

| Layer | What | Status |
| --- | --- | --- |
| Per run | Haul (gold, ore, essence, celestium, tonics), 2-slot Safe Pocket, relics, card upgrades | Built |
| Tower | Banked haul, Sigils for returns, hero XP and injuries | Built |
| Journal | Beasts fought, events met, regions walked (`TowerJournal`, saved) | Built |
| Bonds | Bond ranks and bond cards | Deferred |
| Offerings | Section 13 | Planned |

## 11. Immersion (Built)

- **Audio** (`TowerAudio`): every clip is synthesised at runtime until recorded audio exists; a file at `Resources/AdamsHaven/Audio/<id>` replaces a clip. UI ticks and buzz, footsteps (trail and road), dungeon steps, door, chimes for events, reward and victory stingers, shift rumble; battle swing, hit, crit, heal, status, down, card, ultimate whoosh, victory and defeat; looping ambience per biome (forest, marsh, highland, ruins, heart), per dungeon theme (cave, crystal, blight) and in battle (drone and drums), with a cricket and owl layer at night. SOUND ON/OFF in the top bar.
- **Region card** on setting out, **run summary** at the end, **conquest card** on clearing a lair.
- **Day and night** tint, **rain and fog** overlays, the clock in the top bar.
- **Guild journal** from the Atlas and the run panel.

## 12. Tower coupling

| Tower to expedition | Expedition to Tower |
| --- | --- |
| Food, water, firewood and tonics become supplies | Haul banked, supplies returned |
| Hero levels (+2% HP, +1% defence per level) and gear (weapon +8% attack, tool +6% HP per level) | Heroes come home wounded (injury) and are away while out |
| Guild Hall required; level 3 widens sight | Sigils for returns (capped per day) |
| Research opens regions 2 to 8 and pack mules (EXP-2) | Journal entries |
| Resident traits drive event checks | Log lines in the Tower |

The dock BATTLE button is a separate skirmish: its spoils pay for the first 3 wins a day (`SkirmishPaidWins`).

## 13. Planned

- **Offerings** (was "Atlas reward loop"): an earned-only pull at special POIs or on return, paying relics, cards and Tower materials, with pity. No real-money purchase, ever. Heroes stay with Sigil summons.
- **Roaming enemies and the Forest Warden:** silhouettes on the map, drawn to high threat; the Warden stalks during shifts and is beatable only at a late depth.
- **Landmarks and outposts:** always-visible landmarks with a one-time boon; towns for trade and rumours.
- **Weather events:** event tables keyed to rain and fog.
- **Bonds:** bond ranks, banter pairs with small buffs, bond cards.
- **Recorded audio and music:** replace the synthesised clips and add per-biome music and boss themes.
- **Art:** 16:9 room art (the room cards use portrait 473x1024 art), a landscape cave battle background (the 1024 px one was dropped), upscaled event plates. SeedVR2 7B is installed in ComfyUI but its command-line runner fails to load weights in the current Python environment; run it from the ComfyUI UI or fix the environment.
- **Tutorial run:** a fixed first map teaching rations, roads, one event, one dungeon, one battle, camp and heading home.

## 14. Code map

| Area | Files |
| --- | --- |
| Run state, plan, start/end, regions, summary | `TowerExpedition.cs`, `TowerExpeditionLife.cs` |
| Grid map rules, walking, shift, weather hooks | `TowerOverworldRules.cs`, `TowerOverworld.cs`, `TowerOverworldGen.cs`, `TowerAtlas.cs` |
| Threat, fog, reveal | `TowerThreat.cs` |
| Events | `TowerEvents.cs`, `Resources/AdamsHaven/Events/traversal.json` |
| Dungeons and rooms | `TowerDungeon.cs` |
| Rewards, relics, stress | `TowerRunRewards.cs` |
| Map rendering | `TowerMapView.cs`, `TowerMapArt.cs`, `TowerMapSources.cs`, `TowerRainLayer.cs`, shaders in `Resources/AdamsHaven/Expedition/Overworld` |
| Expedition UI | `TowerExpeditionUi.cs` |
| Audio and banter | `TowerAudio.cs`, `TowerBanter.cs` |
| Battle | `Scripts/Battle/*` (`BattleCatalog`, `BattleRules`, `BattleMode`, `BattleFx`, `BattleAutoPlayer`, `BattleSheet`) |
| Plate graphs (rules tests only) | `TowerForestLayouts.cs`, `Resources/AdamsHaven/Expedition/*_layouts.json` |

Tests: `TowerSimulationTests`, `ExpeditionReviewTests`, `ExpeditionBalanceTests` (headless battle sim), `ExpeditionLifeTests`. The rules tests walk the old plate graphs (`TowerRules.GridMaps = false`) because they are small and fixed; the game always uses the grid.

## 15. Open questions

- **OQ1:** Offerings currency, pull table and pity numbers.
- **OQ2:** On-device check that runtime-packed map stamps survive Android ASTC compression; frame time and memory on a 1440p phone.
- **OQ3:** Safe Pocket size at max Tower level and its upgrade path.
- **OQ4:** Bond design (ranks, cards, banter buffs).
- **OQ5:** Event tone and content rating; final banter voice per hero.
- **OQ6:** Whether the 0.5x default should become 1x on expeditions once players have seen the moves.

## 16. Accessibility and telemetry

- Colour-blind-safe fog, road and threat colours (add pattern or icon, not colour alone); text scaling; reduced motion for shifts and rain; large touch targets.
- A local-only run log for balancing; no network analytics unless opt-in for a public release.
