# Battle Mode GDD: Silverwood Expeditions

Status: rewritten 2026-10-02 to match the game as built after the expedition review (commits 8d59843 to the step 8 commit); run map turned into an Atlas web and dungeons made room-to-room on 2026-10-03 (sections 2, 4, 6, 7). Sections marked **Built** describe code; **Planned** and **Open** are still design. The first draft (2026-09-30, painted plates and two map modes) is in git history.

Related docs: [BATTLE_PORT.md](BATTLE_PORT.md) (battle sandbox, `AnimationSignal`), [CHAOS_ZERO_REFERENCE.md](CHAOS_ZERO_REFERENCE.md) (CZN alignment), [TOWER_LIFE.md](TOWER_LIFE.md) (colony, storyteller), [TOWER_MODE_GDD.md](TOWER_MODE_GDD.md) §10 (the Tower side of expeditions), [BATTLE_UI_ART.md](BATTLE_UI_ART.md).

---

## 1. Vision and pillars

Leave the Tower, walk into the Silverwood, come back with loot and scars. An expedition is a short-session roguelite made of four layers:

| Layer | Feel | Player verbs |
| --- | --- | --- |
| Overview map | A generated forest played as an Atlas web | Pick the next place, spend rations, open the web |
| Traversal | Tabletop roleplay beats | Choose, roll trait checks, accept consequences |
| POI dungeon | Room-to-room crawl in fog of war | AUTO-explore, resolve rooms, fight, loot |
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
| "Atlas" | The Atlas is the region overview screen. The run map plays like the Path of Exile 2 Atlas (section 4); in code and here its graph is "the web". The old "Atlas reward mode" is renamed **Offerings** (section 13) and is not built. |
| Run map travel (2026-10-03) | Node to node along the web, never cell by cell. A place can be reached only when it is linked to a completed place; trips pass through completed places only. Free cell walking is retired. |
| Tiers and map mods (2026-10-03) | Waystone style: every dungeon place has a tier (region danger + hops from camp, the lair highest) and 0-3 seeded mods, each pairing more danger with more reward. A tower's tablet adds a mod to every place in its circle. |
| Dungeon crawl (2026-10-03) | Room to room: fog lifts a room at a time, a tap glides the party to a room, AUTO explores, GO TO GOAL once the goal is seen. Every room kind and choice stays. |
| Traits and bonds | Trait checks are live in events, traps and skill rooms. Bonds are deferred. |
| Orientation | Landscape (all expedition UI is laid out for 16:9). |
| Hero source | Heroes come from Sigil summons in the Tower. No shards or omens. |
| Haul currencies | Gold, ore, essence, celestium, tonics (what the code banks). |
| Regions | 13, from Silverbrook Edge to Silverwood depth 5 (section 9). |
| Battle speed | The old 1x clock was too fast, so half of it is the base and reads **1x**; the button cycles 1x / 2x / 4x (clock rates 0.5 / 1 / 2). Ultimate cut-ins can be skipped with a tap. (BM 10.3.0) |
| Combat model | Chaos Zero Nightmare systems on top of the owner's per-fighter AP/EP: 5-card hands with discard, Initiation/Retain/Exhaust, enemy action counts that interrupt mid-turn, tenacity and BREAK, GUARD shields, reserve partners with assists, epiphanies. See CHAOS_ZERO_REFERENCE.md. (BM 10.3.0) |
| Player media | A media library swaps card art, effects, cinematics, sounds, music, models and stages for the player's own files (MEDIA_LIBRARY.md). (BM 10.3.0) |

## 3. Macro loop (Built)

1. **Atlas:** pick an unlocked region. Regions open by conquering their neighbours and, for regions 2 to 8, by research at the Heart.
2. **Plan:** pick the party (up to 6), rations, tonics and firewood from Tower stores.
3. **Region card:** name, biome, danger, weather and a one-line hook.
4. **Overview map:** travel the web of places from the camp outward, complete places to open their neighbours, meet events, enter places.
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

Code: `TowerRules.EndExpedition(bool wiped)`, `AtSafeExit` (at the camp), `RetreatLoss`, `LastSummary`.

## 4. The overview map: an Atlas web (Built)

The run map plays like the Path of Exile 2 endgame Atlas on top of the generated forest.

- **Generator:** `TowerOverworldGen` builds a 64x40 cell map from the region's biome and the run seed: terrain (forest, dense forest, clearings, hills, rocks, rivers, lakes, marsh, ruins, blight), roads, bridges, fords and places. Map version 4 adds one **tower** per map (the spot nearest the middle). Old runs keep the generator version they started with.
- **Biomes:** edge (Silverbrook Edge, Rootside Camp), lakes (Shallow Ford, Old Bridge, Sunken Marsh), ruins (Moon Shrine, Silverwood Gate), mountains (Watchpost), and one per Silverwood depth (edge, lakes, mountains, ruins, heart).
- **Places per map:** camp, lair and tower always; the rest by biome (for example the lakes hide more mysteries, the mountains more elites and vaults, the Heart more elites and fewer packs).
- **The web** (`TowerMapWeb`): places are nodes; links form a Gabriel graph (two places link when no other place lies inside the circle with them as its diameter), so the web is planar, connected and rebuilt exactly from the map. Each link is an A* trail over the terrain. Trails over 36 cells are dropped while the web holds together without them; the camp never links straight to the lair while another way exists. An offline run of 1000 generated maps (with a stand-in noise function) gave about 22 links, the camp has about 3, the lair is 3 or 4 hops out (rarely up to 9), half the trails are 13 cells or less.
- **Node states:** completed (the camp, cleared dungeons, merchants, shrines and towers once reached), open (linked to a completed place), scouted (seen, but not linked to anything completed), the lair's beacon (always glowing through the fog), hidden. Open and scouted places show their name, tier and mod count; completing a place lifts the fog over its neighbours and the trails to them, and they rise into view.
- **Travel:** tap a place: the route card shows its name, kind and theme, tier, mods, reward hint and the trip's cost (rations, threat, hours, % on road, places passed). Tap again or GO and the party walks the whole route. A trip may pass only through completed places, and its target must be open or completed.
- **Trail costs** (per cell, as before): 1 ration per 20 cost units walked (+20% in rain; at zero rations each ration due costs every fighter 10% HP), threat 0.8 on trail, 0.3 on road, +0.4 at night, 8 minutes per cost unit. Threat is settled on reaching each place, so a shift never strands the party between two. Walked cells wear toward road (3 walks make road, up to 9); travelled links are drawn in gold.
- **Tiers:** region danger + hops from the camp - 1 (I to XVI), the hops counted when the place appears (so a shift never changes a known place's tier or mods); the lair one above the highest. Colours as waystones: I-V white, VI-X yellow, XI and up red. Each tier above the region's danger adds 10% loot and +1 battle danger per two tiers (at most +2). Lair bosses keep their own danger.
- **Map mods** (`TowerAtlasNodes`): seeded per place, 0 to min(3, hops) (the lair at most one), on dungeon places only:

| Mod | Danger | Reward |
| --- | --- | --- |
| Brutal | monsters deal 25% more damage | +30% loot |
| Resilient | monsters have 30% more health | +25% loot |
| Teeming | one more enemy in every fight | +25% loot |
| Fortified | one foe in every fight is Shielded | a relic always on offer |
| Hasted | one foe in every fight is Hasted | +20% gold |
| Rich | monsters have 10% more health | an extra treasure room |
| Haunted | two rooms turn into mystery rooms | +15% loot |

  Fights get them through `BattleEncounterSpec` (`Offense`, `Vitality`, `ExtraFoes` capped at 2, `Affix`; Shielded wins over Hasted); loot through `RollLoot`; the room mix when the dungeon is built (never the entrance, the goal, stairs, elites, bosses or the last fight).
- **Towers:** reaching the tower completes it, lifts the fog within 13 cells, scouts every place there and offers one of three tablets. A tablet adds its mod to every place in the circle that still has a dungeon. Selecting a tower draws its circle. A tower has no dungeon.
- **Lair (citadel):** always visible as a beacon; it can be entered only once a place linked to it is completed.
- **Fog:** cells as before: sight 5 cells along every trail walked (+2 per extra reveal step from a Curious hero by day or with a Night Owl, and Guild level 3; fog weather -2, never below 3); a known place clears 2 cells around it, a known trail 1. A place whose cell is seen counts as scouted. Camp scouting clears 15 cells.
- **Threat:** battles +4; clearing a place -15; camp rest -25. At 75 a warning; at 100 the forest **shifts**: places nobody has seen move, the woods regrow, loose road wears down (never at the camp, cleared places or the party), threat resets to 40. Completed, open and scouted places, the lair and the party's place never move, and the links among them are carried over (`run.webLinks`). Each new day one unseen place moves.
- **Rendering:** `TowerMapView` draws ground tiles, terrain masks, stamps, place props tinted by state (completed green-grey, scouted dimmed), dotted trail lines (faint, brighter from a completed place, gold once travelled), a fog layer tinted by biome and reddening with threat, the worn road, the lair's breathing beacon and the tower's circle. Names and tiers float over known places. The map is tinted by the hour and shows rain streaks or fog haze.
- **Old saves:** a grid run saved before the web (`webVersion` 0) keeps its map. Every place it cleared or stood on, and the fewest-hop way from the camp to each, counts as completed; a party caught between places walks back to the camp; a party inside a dungeon stays there.

## 5. Time and weather (Built)

- The Tower pauses while an expedition is out; the run keeps its own clock (`run.clock`). Walking costs 8 minutes per cost unit, a fight 30 minutes, cooking 1 hour, scouting 2 hours, a camp rest 3 hours by day or until 06:00 at night.
- Top bar: `Day N  •  HH:MM  •  part of day  •  weather`.
- **Weather** is drawn once per run day from the run seed, weighted by biome: clear, **rain** (walking threat x0.7, rations x1.2) or **fog** (sight -2). `TowerRules.Weathers` turns it off for rules tests.
- Night raises threat per cell, changes which events can fire and fades in the night ambience.

## 6. Traversal events (Built)

| Rule | Value |
| --- | --- |
| Roll | Once per trail travelled, on reaching the next place: 35% on trail, 10% on road (half its cells road), +25% at full threat. Never two in a row. None on trails into the camp or the lair. An event stops a longer trip at the place reached; the trip carries on once it is settled. |
| Selection | Weighted by depth, the theme of the place reached, day/night, road, `needsFlag`, not seen this run. Seeded by run seed + step. |
| Check | Base chance; a hero with the named trait adds the trait bonus (+30% default). Under = success, within 20% above = partial (if written), else failure. Odds shown on the buttons. |
| Costs and consequences | Rations, firewood, tonics, gold from the haul; HP, loot, threat, reveal, flags, ambush. |
| Rest before the boss | **The Quiet Glade** fires once on reaching a place linked to the lair (or the lair itself). |
| Full threat | **The Forest Closes In**: fight or flee. |
| Content | 54 events in `Resources/AdamsHaven/Events/traversal.json`, covering every theme including mine, heartwood and blight, plus the two scripted ones. |

Party **banter** (`TowerBanter`): after some walks and at camp, a living fighter says a line picked by mood (hurt, threat, near the lair, camp, rain, fog, night, road, wild). The lines are first drafts.

## 7. Places and dungeons (Built)

| Place | What happens |
| --- | --- |
| Combat / elite | Dungeon crawl; elite fights roll an affix and always offer a relic. |
| Mystery | Dungeon with unknown rooms and event-like rooms. |
| Treasure (vault) | Dungeon whose goal offers a pick of rewards. Since layout version 3 (BM 10.3.0) every vault holds at least one guard fight; floors entered before keep their rooms. |
| Shrine | Trade HP or rations for a relic, or pray (heal 25%, ease Stress). Completed on arrival; the shrine room stays open until used. |
| Merchant | Buy tonics (40g), 2 rations (30g), firewood (20g), a relic (160g) with haul gold. Completed on arrival; the stall stays open until left. |
| Tower | No dungeon: climbing it maps a 13-cell circle and sets a tablet (section 4). |
| Camp | Rest (1 firewood: heal 35%, Stress -40, threat -25), Cook (1 ration: heal 15%, Stress -25), Scout (2 hours: wider sight). Safe place to head home. |
| Lair | 3 floors, region boss at the bottom. |

Dungeons (`TowerDungeon`): a 20x20 grid of rooms on a 3x3 sector layout. Layout version 2 picks one of several shapes per visit from the run seed; version 1 saves keep the plus shape; version 0 saves are walked back out of the dungeon on load. Floors: lair 3, elite and landmark 2, others 1.

The crawl is room to room:
- **Fog by room:** entering a room reveals it, every passage out of it and the rooms those passages reach (their contents show as badges). Rooms link through passages (`TowerDungeon.Links`, `Passages`). Saves keep `run.fog` (still per cell); an old save with cell-by-cell fog sees its current room again on load.
- **Glide:** tap a known room and the party glides there (about 0.08 s a cell), stopping at the first room with something to resolve. Rooms turned down for now (NOT NOW at a rest, NOT YET at the stairs) are passed through unless they are the destination.
- **AUTO** (or space): heads for the nearest known room still to explore; the goal or stairs come last. **GO TO GOAL** appears once the goal or stairs room is seen. The bar shows rooms done and rooms unseen.
- Empty rooms and the entrance complete on entry; finished rooms are drawn dimmed; the place's tier and mods show in the run panel. The run is saved where a glide stops, not every cell.

Room kinds: enemy, elite, boss, treasure, merchant, shrine, rest, stairs, unknown, **trap** (disarm with a trait check or dash through), **skill check** (force it open for loot), **story** (lore and a flag), **key gate** (a sealed door with loot behind it). Finished rooms stay finished when the party re-enters (`run.roomsCleared`).

## 8. Battle (Built)

Authoritative detail: BATTLE_PORT.md and CHAOS_ZERO_REFERENCE.md.

- 3 field fighters, 3 reserves, shared deck, per-fighter AP/EP, JD (summoner) with CP/SP. Enemy intents are telegraphed.
- **CZN layer (BM 10.3.0):** fighter cards refill to 5 each round and every unplayed card (JD's too) is discarded at End Turn unless it Retains; a turn-order bar under the round shows whose turn it is and which enemy acts next; Initiation cards open in hand; Exhaust cards leave the battle. Each enemy shows an action count (3 to 5): every card played lowers it, and at 0 the enemy acts at once, mid-turn, then starts counting again. Tenacity pips under enemy health wear down with hits; at 0 the enemy BREAKS (+25% damage taken, its count pushed back, the breaker gets 1 AP back). Every fighter has a free GUARD (a shield; fully blocked hits add no stress). Reserve i partners field fighter i (+8%) and can ASSIST once per battle for 2 SP. In expedition fights one card glows: playing it offers an Epiphany (1 of 3 upgrades kept for the run, `TowerRun.epiphanies`). Full table: CHAOS_ZERO_REFERENCE.md.
- **Encounters** (`BattleCatalog.Build(BattleEncounterSpec)`): 12 species with themes and minimum depths; line-ups drawn per region, theme and kind (normal, elite, boss). Stats scale with depth (health +12%, offence +6.5% per depth); elites x2 power, bosses x3.4. Since BM 10.3.0 offence is also multiplied by a curve from x1.5 at danger 1 to x1.0 at danger 13 (lair bosses x1.25 more), to keep the difficulty bands after the CZN rules.
- **Elite affixes:** Vampiric, Thorned, Hasted, Shielded, Enraged.
- **Lair bosses:** one named boss per region (for example the Amberhide Matriarch at Silverbrook Edge, the Heartrot Colossus at Silverwood depth 5), two phases with a telegraphed signature move.
- **Enemy AI:** prefers hurt or weak targets, buffs when allies are hurt, and aims at JD about one attack in eight.
- **Statuses:** stun, slow (costs AP), burn, shield, charging, plus the original set.
- **Run rewards** (`TowerRunRewards`): after each win pick 1 of 3: upgrade a fighter's card (up to level 3, +20% power per level), a relic (9 relics), or supplies. Elites, bosses and vaults always offer a relic. Upgrades and relics last for the run.
- **Carry-over:** HP and Stress persist between fights; camps, tonics, shrines and rest rooms heal.
- **Seeded fights:** the fight's seed comes from the run, so quitting mid-fight replays the same battle.
- **Pacing:** speed 1x / 2x / 4x (1x = half the old clock, the default; remembered). Auto battle plays the whole hand with ultimates (`BattleAutoPlayer`). Tap skips ultimate cut-ins.
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

Difficulty, measured by the headless sim (`ExpeditionBalanceTests`): normal fights cost about 5-10% party HP, elites 13-29%, bosses 21-71% (re-measured after the BM 10.3.0 combat rules and offence curve).

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
- **Recorded SFX (BM 10.3.0):** 111 clips generated from MiniMax H3's native audio track (`Tools/produce_sfx.py`, manifest `BattleMotion/sfx/MANIFEST.md`): every card, ultimate and awakening has its own sound (`Audio/Moves/<card id>`, played at the action; ultimates with their cut-in), 33 battle and exploration sounds (`Audio/Sfx/`: element hits, shields, BREAK, turns, epiphany, partner, atlas, rooms), and recorded versions of 13 shared ids (hit, crit, swing, ult, victory ...). Synthesised clips remain the fallback. Fire and heavy impacts are bass-heavy (an H3 trait); re-roll with `--rerender` if they sound thin on phones. Players can replace any of them in the media library.
- **Recorded effects:** `Resources/AdamsHaven/Audio/Sfx/<id>` plays when present (atlas_reveal, atlas_travel, atlas_complete, tower_activate, room_reveal, trap_spring, treasure_open, stairs_descend), else a synthesised stand-in (chime, confirm, reward, door, status). Imported music plays per place: `atlas` on the run map, `dungeon` in dungeons, off back in the Tower.
- **Media library** button on the region panel and the run panel (`MediaPanel`); a clear blocker stops clicks reaching the expedition screen under it.
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
- **Landmarks and outposts:** towns for trade and rumours (towers, section 4, are the first landmark built).
- **Weather events:** event tables keyed to rain and fog.
- **Bonds:** bond ranks, banter pairs with small buffs, bond cards.
- **Recorded audio and music:** replace the synthesised clips and add per-biome music and boss themes.
- **Art:** 16:9 room art (the room cards use portrait 473x1024 art), a landscape cave battle background (the 1024 px one was dropped), upscaled event plates. SeedVR2 7B is installed in ComfyUI but its command-line runner fails to load weights in the current Python environment; run it from the ComfyUI UI or fix the environment.
- **Tutorial run:** a fixed first map teaching rations, roads, one event, one dungeon, one battle, camp and heading home.

## 14. Code map

| Area | Files |
| --- | --- |
| Run state, plan, start/end, regions, summary | `TowerExpedition.cs`, `TowerExpeditionLife.cs` |
| Grid map rules, travel along the web, shift, weather hooks | `TowerOverworldRules.cs` (`AtlasRoute`, `AtlasTravel`, `GridResume`, `ShiftForest`, `MigrateWebRun`), `TowerOverworld.cs`, `TowerOverworldGen.cs` |
| Atlas web, node states, tiers, map mods, towers and tablets | `TowerMapWeb.cs`, `TowerAtlasNodes.cs` |
| Region Atlas map | `TowerMapSources.cs` (`TowerAtlas`, `TowerAtlasSource`) |
| Threat, fog, reveal | `TowerThreat.cs` |
| Events | `TowerEvents.cs`, `Resources/AdamsHaven/Events/traversal.json` |
| Dungeons and rooms, room fog, AUTO | `TowerDungeon.cs` (`DungeonAutoTarget`, `DungeonGoalRoom`, `DungeonExplore`, `DungeonLight`), `TowerDungeonIllustration.cs` |
| Rewards, relics, stress | `TowerRunRewards.cs` |
| Map rendering | `TowerMapView.cs`, `TowerMapArt.cs`, `TowerMapSources.cs`, `TowerRainLayer.cs`, shaders in `Resources/AdamsHaven/Expedition/Overworld` |
| Expedition UI | `TowerExpeditionUi.cs` |
| Audio and banter | `TowerAudio.cs`, `TowerBanter.cs` |
| Battle | `Scripts/Battle/*` (`BattleCatalog`, `BattleRules`, `BattleMode`, `BattleFx`, `BattleAutoPlayer`, `BattleSheet`, `BattleCzn` for the CZN layer) |
| Media library | `Scripts/Media/*` (`MediaLibrary`, `MediaPanel`, `MediaBrowser`, `MediaGltf`, `MediaJson`); MEDIA_LIBRARY.md |
| Plate graphs (rules tests only) | `TowerForestLayouts.cs`, `Resources/AdamsHaven/Expedition/*_layouts.json` |

Tests: `TowerSimulationTests`, `ExpeditionReviewTests`, `ExpeditionBalanceTests` (headless battle sim), `ExpeditionLifeTests`, `ExpeditionAtlasTests` (the web, reaching places, tiers and mods, towers, room fog and AUTO, old grid saves). Many rules tests walk the old plate graphs (`TowerRules.GridMaps = false`) because they are small and fixed; the game always uses the grid.

## 15. Open questions

- **OQ1:** Offerings currency, pull table and pity numbers.
- **OQ2:** On-device check that runtime-packed map stamps survive Android ASTC compression; frame time and memory on a 1440p phone.
- **OQ3:** Safe Pocket size at max Tower level and its upgrade path.
- **OQ4:** Bond design (ranks, cards, banter buffs).
- **OQ5:** Event tone and content rating; final banter voice per hero.
- **OQ6:** Settled in BM 10.3.0: the slower clock is the base and reads 1x.

## 16. Accessibility and telemetry

- Colour-blind-safe fog, road and threat colours (add pattern or icon, not colour alone); text scaling; reduced motion for shifts and rain; large touch targets.
- A local-only run log for balancing; no network analytics unless opt-in for a public release.
