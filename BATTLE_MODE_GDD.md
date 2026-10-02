# Battle Mode GDD: Silverwood Expeditions

Status: design draft, 2026-09-30. This is a target document, not a description of finished features. Section 12 marks what already exists in code.

Related docs: [BATTLE_PORT.md](BATTLE_PORT.md) (battle sandbox, `AnimationSignal`), [CHAOS_ZERO_REFERENCE.md](CHAOS_ZERO_REFERENCE.md) (CZN alignment), [TOWER_LIFE.md](TOWER_LIFE.md) (colony, storyteller), [BATTLE_UI_ART.md](BATTLE_UI_ART.md).

---

## 1. Vision and pillars

Leave the Tower, walk into Silverwood Forest, come back with loot and scars. Battle Mode is a short-session roguelite built from four nested layers:

| Layer | Feel | Player verbs |
| --- | --- | --- |
| Overview map | Atlas / StS map, a forest you uncover | Pick route, spend rations, build a road |
| Traversal | Tabletop roleplay beats | Choose, roll checks, accept consequences |
| POI dungeon | D&D room crawl in fog of war | Explore, trigger, fight, loot |
| Battle | Chaos Zero Nightmare-style card combat | Chain cards, manage AP/EP/Stress, ultimates |

Pillars:
1. **The forest is alive.** Maps shift, roads vanish, nothing is memorised for long.
2. **Every step costs.** Rations, Stress, injuries and a threat meter force trade-offs.
3. **The Tower matters.** Colony state feeds expeditions and expeditions feed the colony.
4. **Short and replayable.** One map is 10 to 20 minutes on a phone. Quit-and-resume must always work.
5. **Authored characters, random world.** Kits and art are fixed; routes, events and loot are rolled.

## 2. Macro loop

1. **Tower prep:** pick party (up to 6: 3 field, 3 reserve), rations, tonics, firewood. Gear forged in the Tower goes with the hero.
2. **Choose mode:** Campaign or Unknown (section 3).
3. **Overview map:** walk freely across the painted forest, uncovering fog and leaving a traced trail that becomes road.
4. **Traversal events** fire on the way (section 5).
5. **POIs:** enter dungeon, crawl rooms, fight, loot (section 6).
6. **Resolve the map:** beat its objective or boss (Campaign), or find and beat the boss POI (Unknown).
7. **Shift and new choice:** the forest rearranges; pick the next map one depth deeper (section 8).
8. **Return to Tower:** haul is banked, characters recover, colony reacts.

### Win, lose, retreat

| Outcome | Result |
| --- | --- |
| Objective cleared | Haul and pocketed loot kept, depth unlocked, discoveries recorded. |
| Retreat at a camp or the entrance | Haul kept minus a small retreat tax; no depth progress. |
| Party wipe | **Run loot is lost except the two pocketed slots, which always survive.** Characters are kept, return downed or injured and recover in the Tower. Unlocked depth, levels, bonds and discoveries are never lost. |
| Wipe in Hard mode (opt-in) | Same, but a downed hero can be lost permanently. Hard mode pays better rewards and relic odds. Off by default. |

Existing code: `TowerRules.EndExpedition(bool wiped)`.

## 3. Two map modes

The player chooses at expedition start.

| | **Campaign** (hybrid) | **Unknown** (Atlas) |
| --- | --- | --- |
| Layout source | Premade overview map picked by RNG from the depth's pool | One large seed-generated forest |
| Fog | Fog of war on the painted plate, revealed by walking (section 4) | Fog of war on a continuous map, revealed by walking |
| POIs | Authored anchor spots on the plate, RNG picks which are used; contents rolled from run seed | Scattered by terrain-aware RNG, density and danger rise with distance from camp |
| Goal | Clear the map objective or boss, unlock next depth | Chase rewards: relics, currency, rare drops; boss POIs hidden in fog |
| Length | 10 to 15 min | 15 to 20 min, saveable at camps |
| Reward | Reliable depth progress and fixed drops | Higher variance, "roguelite gacha" pulls (section 17) |
| Difficulty | Set by depth | Scales with distance walked and threat |

Both modes share the same fog, road, event, POI and battle systems. Only the layout provider and win condition differ, so the code path is one `MapProvider` with two implementations.

## 4. Fog of war and road-building

### Map direction (decided 2026-09-30, supersedes node-hopping presentation)

- **No pre-drawn paths to POIs.** The overview is a painted scene. The player never sees a line pointing at a POI.
- **Free movement.** The party walks anywhere walkable on the painted map. Cost in rations is by distance and terrain (dense forest, water, rock cost more; open ground and roads cost less).
- **Traced trail.** The route actually walked is drawn behind the party as a line. It is the player's own record, not a hint.
- **Worn road.** A trail that has been walked becomes a persistent road: cheaper, safer, and **backtracking along your own road is cheap and safe**. Forest shifts can erase stretches; the camp and cleared POIs never vanish.
- **POIs and objectives are obscured but visible in the fog:** silhouettes, glows, smoke, sounds, circling birds. Identity (kind, danger) resolves as the party gets closer or scouts. **Landmarks are always visible** and act as navigation anchors.
- **Immersive placement.** POIs are in-world props (a ruin in a clearing, a tent by the river, a glowing tree), not UI icons. Campaign: each painted plate has authored anchor spots on real scene features and RNG picks which anchors hold which POI. Atlas: terrain-aware rules (shrines in groves, lairs in deep forest, camps near water) with minimum spacing so nothing overlaps or floats.
- **Under the hood:** `TowerForestNode.links` stays as hidden adjacency and anchor data. It is no longer drawn. Movement becomes continuous position along walkable space; the P1 trail-hop rules below are the reference for cost and threat values and get re-expressed per distance.
- **Rework note:** P1 and P2 below were built on node hops. Phase P1b (section 14) ports them to free movement.

### Rules

- **Reveal:** standing on a node/tile reveals a radius. Modifiers: lantern item, scout trait, time of day, Tower tech.
- **Road:** every edge the party walks becomes a road segment. A road makes that edge cheaper in rations, lowers ambush chance, and lets the party fall back without penalty. Roads are the visible record of the run.
- **Loss:** a forest shift (threat full, day roll) can erase part of the road. Cleared POIs and the camp are never erased.
- **Campaign form:** road is drawn along the premade links. **Atlas form:** road is drawn on free tiles, with forest tiles costing extra to cut through (forest density is a movement cost, not an obstacle).
- **Data:** `TowerRun.revealed` (node ids), `TowerRun.road` (`TowerRules.RoadKey(a,b)` = `"a|b"`, sorted), `TowerRun.threat`, `TowerRun.roadSteps`. Dungeon fog already exists (`TowerRun.fog`, `TowerRules.Revealed/InSight`).

**Implemented (P1, `TowerThreat.cs`):**

| Rule | Value |
| --- | --- |
| Reveal radius (trail hops) | 1; +1 with a Curious hero in the party (at night only if a Night Owl is along); +1 at Guild Hall level 3; max 3 |
| New trail | 1 ration, threat +8 |
| Road (already walked) | 1 ration every second trip, threat +3 |
| Night travel | threat +4 more per step |
| Battle won | threat +4 |
| POI cleared | threat -15 |
| Camp rest | threat -25 |
| Threat 75 | warning in the log |
| Threat 100 | forest stirs: up to 2 road segments vanish (never ones touching the camp, a cleared POI or the party's node), threat resets to 40 |
| Old saves | fog rebuilt from `visited` on load |

## 5. Traversal: roleplay moments

Each step along an edge rolls the **event director**. It reads biome, depth, time of day, threat meter, party traits and mood, and whether the edge is already a road (roads roll safer tables).

| Part | Rule |
| --- | --- |
| Frequency | Base ~35% per new edge, 10% on road, boosted by threat. Never two in a row; guaranteed rest option before bosses. |
| Format | Short narrative card with 2 to 4 choices, an art plate and a portrait of the speaking companion. |
| Checks | Choice can require or favour a trait (the 10 colony traits), stat, item or mood. Outcomes: success, partial, failure. |
| Consequences | Rations, HP, Stress, injury, bond points, items, relics, ambush battle, hidden POI revealed, threat change, flag for later events. |
| Banter | Pairs with bond ranks trigger optional dialogue for small buffs. |

Example events:
1. **Wounded stag in a snare:** free it (Stress -, bond +), hunt it (rations +), leave.
2. **Fork in the fog:** scout check reveals a POI or costs a ration.
3. **Lantern-lit stranger:** trade, accept a cursed relic, or refuse.
4. **Collapsed bridge:** Tank forces it (injury risk), carpenter trait repairs (builds road), detour.
5. **Whispering spirit_moonwell:** drink for Stress relief and a Stress spike on the next battle.
6. **Raider tracks:** set an ambush (free first strike) or avoid (threat +).
7. **Sleepless night at camp:** companion story scene, bond +, firewood cost.
8. **Mushroom ring:** gamble for a relic or a weak enemy swarm.

Target content budget for Campaign launch: 40 events (20 universal, 20 depth or biome specific), each with 2+ outcomes. Event data lives in JSON so content grows without code.

**Implemented (P2, `TowerEvents.cs` + `Resources/AdamsHaven/Events/traversal.json`):**

| Rule | Value |
| --- | --- |
| Roll | After each forest step: 35% on a new trail, 10% on road, plus up to 25% at full threat. Never two steps in a row. None at the camp or the lair. |
| Selection | Weighted pick filtered by depth (`minDepth`/`maxDepth`), node theme, day/night, road, `needsFlag`, and not seen this run (unless `repeat`). Seeded by run seed + step count. |
| Check | Each choice has a base `chance`; a hero with the named `trait` in the party adds `traitBonus` (default +30%). Roll under chance = success, within 20% above = partial (if defined), else failure. Odds and the helping trait are shown on the button. |
| Costs | `costRations`, `costFirewood`, `costTonics`, `costGold` (paid from the haul); unaffordable choices are disabled. |
| Consequences | rations, firewood, tonics, HP % to the living party, gold/ore/essence into the haul, threat up/down, reveal N fogged places, flag for later events, ambush battle. |
| Rest before the boss | The first time the party reaches a place next to the lair, **The Quiet Glade** offers a rest (HP +25, threat -10). |
| Full threat | Besides swallowing road, the forest sends **The Forest Closes In**: fight, or try to flee (Careful helps). |
| Ambush | Fought on the forest map with the normal battle; win = spoils, XP, threat -10; lose = flee and drop a ration; wipe ends the run. |
| Content | 20 rolled events + 2 scripted (`camp_quiet_glade`, `threat_ambush`). All 10 colony traits are used by at least one check. |

Not yet: companion banter and bond ranks, mood/stat checks, Stress and injury consequences, per-event art plates (the card shows the speaking hero's portrait).

## 6. POIs: D&D-style dungeons

POI kinds (already present in layouts): **combat, elite, lair (boss), treasure, merchant, shrine, mystery, landmark, camp**.

- A POI opens a **20x20 room-and-corridor grid** with fog. Layout is fixed by (map, POI, floor); room contents are rolled from the run seed.
- Floors per kind: lair 3, elite/landmark 2, others 1, camp 0.
- Room kinds: enemy, elite, boss, treasure, merchant, shrine, trap, rest, stairs, unknown, plus new: **skill check** (trait/stat puzzle), **story** (event card inside dungeon), **key/door** (gate that needs a key found elsewhere).
- Movement is click-to-route; rooms in line of sight are revealed; explored rooms stay lit.
- Branch design: central junction, arms and optional detour chambers, at least one non-goal branch pays off.
- Retreat from a dungeon is free until a goal room is cleared; battles resolve HP carry-over (`ResolveBattle`).
- Theming: each theme (briar, keep, crystal, cave, marsh, ruin) changes tile art, room mix, enemy pool, and trap type.

## 7. Battle layer (CZN-like)

Summary. Authoritative detail is in BATTLE_PORT.md and CHAOS_ZERO_REFERENCE.md.

- Three field fighters, three reserves. Shared deck, per-fighter AP/EP, JD support CP/SP.
- Enemy intents telegraphed with damage and target.
- **Stress -> Breakdown -> Awaken** loop; ultimates with a meter plus SP and a cut-in.
- Encounter tiers: grove (floor 0 to 2), elite (3 to 7), boss (8+).
- **Carry-over inside a run:** HP and Stress persist between fights. Only camps, tonics, shrines and rest rooms heal.
- **Auto battle is always available**, including bosses, with a speed toggle, in the spirit of the "fast auto battle" reference. Auto plays a sensible hand; the player can take over at any turn.

### Run deckbuilding
Players modify the deck during the run, as in CZN and StS:
- **Card rewards** after fights (pick 1 of 3, skip allowed).
- **Removal** at shrines or from a merchant.
- **Upgrade** at camps (costs firewood) or shrine.
- **Relics** change rules for the run (section 9).
- Starting deck is the equipped one from the Tower; run changes reset when the run ends.

### Missing battle pieces (from existing docs)
Equipped decks and roster progression, bond ranks, run relics, expedition save of battle state, shared turn-cost layer, dedicated partner slots.

## 8. Silverwood progression and the shifting forest

Regions already defined: Silverbrook Edge -> Rootside Camp / Shallow Ford -> Moonlit Shrine / Old Bridge / Sunken Marsh -> Ruined Watchpost -> **Silverwood Gate**. Beyond the Gate the player enters **Silverwood Forest depths** (Depth 1, 2, 3... each harder, with deeper themes).

- **Depth pools (Campaign):** Silverwood Forest is the base theme of every overview map, varied by terrain: **towns and outposts** (safe hubs), a **large multi-floor mega-dungeon**, **lakes, rivers and marsh** (slow or block movement; bridges, fords), and **mountains, caves, ruins and blight zones**. Proposal: 5 depths x 3 new maps (15), each depth with a theme and difficulty band; a run draws one of the depth's maps and never the same twice in a row. The new plates are made by Codex from the prompts in [SILVERWOOD_MAP_PROMPTS.md](SILVERWOOD_MAP_PROMPTS.md). The 15 existing plates stay for the Silverbrook-region expeditions and may be reused where they fit.
- Beating a map unlocks the next depth; the player chooses the next map (Campaign pool or Unknown).
- Guild level gates depth (existing `TowerGuild`/`GuildRequired`).
- **Shift triggers** (all four, each with a different feel):

| Trigger | Effect |
| --- | --- |
| Map cleared / new depth | New layout drawn from the new depth's pool. |
| Threat meter full | Mid-run: unexplored POIs reposition, some links change, parts of the road vanish. Warned a few steps ahead. |
| In-game day roll | Small reshuffle: one landmark or event table swap. |
| Return to Tower | Next expedition always starts on a fresh arrangement (never the same layout twice in a row, which already exists). |

- **What stays fixed:** camp, cleared bosses, discovered lore and unlocked pools.
- Presentation: the map plate crossfades while trees visibly "turn", with soft audio sting, so the shift reads as the forest moving.

## 9. Rewards and meta-progression

| Layer | What |
| --- | --- |
| Per-run | Haul (gold, ore, essence, celestium, tonics), **pocket slots (2, upgradable via the Tower) that survive a wipe**, relics, deck changes. |
| Roster | Character levels, **bond ranks**, new cards unlocked by bond. |
| Tower | Upgrades (guild, forge, barn, infirmary) raise carry capacity, rations efficiency, healing, vision. |
| Discoveries | Finding POIs, events and lore adds them to future pools (unlockable map/event pools). |

Decided: pocket slots protect loot on a wipe.

## 10. Risk economy and difficulty

- **Rations:** 1 per forest step, shared among the party. At zero, party loses 10% HP per step (exists).
- **Stress and injuries:** carry over, heal at camps and in the Tower only.
- **Threat meter:** rises with steps, battles, noise, night; drops with rest and cleared POIs. Full threat = ambush plus forest shift. Reuses the storyteller idea from TOWER_LIFE.md.
- **Firewood:** needed to rest and upgrade, creating a supply loop with the Tower.
- Difficulty scales with depth plus (Unknown mode) distance. Optional "Hard" modifiers give better relic odds.
- Fairness: guaranteed rest or camp before each boss; pity counter on rare rewards; no event can kill outright without a warning choice.

## 11. Tower coupling (tight)

| Tower to expedition | Expedition to Tower |
| --- | --- |
| Food/water/firewood stock sets rations | Haul and materials |
| Resident traits and mood affect event checks and Stress | Injured/exhausted characters need rooms and time |
| Guild level gates depth | Discoveries unlock Tower projects |
| Gear (weapon/tool level) boosts attack/HP | Expedition events feed colony storyteller incidents |
| Forge/infirmary/library upgrades | Return reports shown in the Tower log |

## 12. Existing code and tech mapping

Evolve, do not replace.

| Feature | Exists | Work needed |
| --- | --- | --- |
| Region map, run state, rations, camp rest, tonics | `TowerExpedition.cs` (`TowerRun`, `StartExpedition`, `ForestMove`, `CampRest`, `EndExpedition`) | Add mode choice, shift, depth beyond Gate |
| Fog reveal, roads, threat meter | `TowerThreat.cs` (P1 done) | Lantern item |
| Traversal events, checks, ambushes | `TowerEvents.cs`, `Events/traversal.json` (P2 done) | Banter, mood/stat checks, Stress consequences, 20 more events |
| 15 premade forest layouts | `TowerForestLayouts.cs` | Group into depth pools; add Atlas provider |
| POI dungeon grid, fog, routes, room resolution | `TowerDungeon.cs` | New room kinds (skill check, story, key); relics hook |
| Guild gating | `TowerGuild.cs` | Depth gating |
| Expedition UI | `TowerExpeditionUi.cs` | Fog rendering, road lines, event cards, mode select |
| Battle | `Scripts/Battle/*` | Run deck, relics, carry-over Stress, auto battle |
| RNG | `TowerRng`, `TowerForestLayouts.Hash` | Per-step event seeds, shift seeds |
| Save | `TowerSaveFiles`, `TowerState.run` | Mid-run snapshot, roads, fog, threat |

New files (proposed): `TowerEvents.cs` (event director and data), `TowerAtlas.cs` (generator), `TowerThreat.cs`, `TowerRelics.cs`, `Resources/AdamsHaven/Events/*.json`.

Testing: add simulation tests in the `TowerSimulationTests` style (seeded runs, shift determinism, wipe/loot rules).

## 13. UI/UX (mobile first)

- Portrait APK target. One-handed: map pan/zoom; tap or drag a destination to preview ration cost and a faint predicted walk, second tap confirms. The trail is drawn only after it is walked. No lines are ever drawn to unvisited POIs.
- POIs appear as in-world props or fog silhouettes; tap to inspect.
- Event card: art plate, text, 2 to 4 big buttons, check chances visible.
- Always-visible strip: rations, firewood, tonics, threat meter, party HP/Stress.
- Atlas map: pinch zoom, minimap, "return to camp" button.
- Settings: auto battle, reduced motion on shifts, text size, colour-blind-safe fog and road colours.
- Onboarding: first Campaign map is a fixed tutorial layout (rations, road, one event, one dungeon, one battle, camp).

## 14. Phased roadmap

| Phase | Deliverable |
| --- | --- |
| P1 | **Done 2026-09-30.** Fog reveal and road drawing on existing Campaign maps; threat meter; mid-run save/resume (state already saved after every action and on app pause/quit; new fields persist). |
| P2 | **Done 2026-09-30.** Event director plus 20 events; trait checks; consequences; full-threat ambush; rest before the lair. |
| Art wiring | **Done 2026-09-30.** 15 Silverwood plates (v2) registered as layouts in `silverwood_layouts.json` with auto-placed hidden nodes (to be replaced by the anchor tool), 5 Silverwood depth regions after the Gate, new dungeon themes mine/blight/heartwood, new room-card kinds and per-depth boss rooms, event scene banners. |
| P1b | Map rework: free movement, traced trail, fog-silhouette POIs, authored anchor spots per plate, distance-based costs; port P1/P2 rules to it. |
| P3 | Run deckbuilding, relics, carry-over Stress; new dungeon room kinds. |
| P4 | Shift triggers and Silverwood depth pools; depth gating. |
| P5 | Unknown (Atlas) mode generator and reward loop. |
| P6 | Meta: bonds, discoveries, Tower upgrades tuned; content passes; balance. |

## 15. Enemies and bosses

- **Biome rosters:** each theme (briar, keep, crystal, cave, marsh, ruin) has its own enemy set, used in dungeons and ambushes on that terrain.
- **Elite affixes:** elites roll 1 to 2 random affixes (e.g. Thorned reflects damage, Mending heals each turn, Frenzied gains attack when hit, Fogbound hides its intent for one turn).
- **Signature boss per depth (5):** each changes how cards or Stress work in that fight (examples to design: a boss that turns Stress into damage, one that locks the shared deck's top cards, one that swaps lanes). Bosses are fought at the depth's lair POI.
- **Roaming enemies:** wandering threats visible in fog as moving silhouettes. The player can avoid them, ambush them (free first strike) or be caught. They are drawn toward high threat.
- **Forest warden:** a recurring entity tied to the threat meter and forest shifts. It appears as threat rises, is seen near shifts, and is never fully beatable until a late-depth encounter; early meetings are escapes or chases.
- Enemy intents use the existing telegraph system (BATTLE_PORT.md).

## 16. Tutorial and first run

A fixed, guided first expedition on a scripted small map teaches in order: rations and free movement, the traced trail and road, one traversal event, one dungeon with fog, one battle (with auto battle shown), camp and retreat. It then hands over to free play. Contextual tips remain afterwards for first uses of relics, shift warnings and roaming enemies.

## 17. Atlas reward loop ("roguelite gacha")

Earned-only. There is no real-money purchase and none is planned; keep systems clean so this stays true.

| Item | Rule |
| --- | --- |
| What pulls give | Relics and cards; Tower materials and gear; hero shards (new heroes); lore and discovery unlocks (new events, POIs, map variants). |
| Currency | Earned in Atlas runs and POIs (e.g. omens or celestium); never bought. Spent at "Offering" POIs or on return to the Tower. |
| Pity | Counter per pull type guarantees a rare after N misses; counter persists across runs. |
| Duplicates | Duplicate cards convert to upgrade material; duplicate hero shards fill a bond/rank bar. |
| Risk | Higher distance and threat improve pull quality; wiping loses unspent currency unless pocketed. |
| Numbers (placeholders, tune in playtest) | Pull cost 3 omens. **Soft pity at 8 misses** (rare odds ramp each pull), **hard pity at 12** (rare guaranteed), tracked per pull type. 10 shards recruit a hero; further shards raise rank. |

### Recruiting and partners
- Heroes are recruited from Atlas shards and story unlocks.
- Party follows the CZN model: **3 field combatants plus 3 partner slots**. The current reserves become partner slots with passive or assist effects. Design of partner effects is open work (see CHAOS_ZERO_REFERENCE.md).

## 18. Content authoring and assets

- **Events:** Claude drafts event JSON in bulk (`Resources/AdamsHaven/Events/*.json`), the user edits. Target 40 for Campaign launch (20 exist), more for Atlas.
- **Art pass (decided: full new art pass first):**

| Asset | Purpose |
| --- | --- |
| New painted map plates | New Silverwood set (proposal 15, prompts in SILVERWOOD_MAP_PROMPTS.md, made by Codex) plus Atlas tile/backdrop set, fog-friendly composition |
| Walkable masks and anchor-spot data per plate | Free movement and immersive POI placement; authored with an in-Unity editor tool (click the plate to place anchors, paint the mask; saves JSON + mask PNG per layout) |
| In-world POI prop sprites | Ruin, camp, shrine, lair, merchant, treasure, landmark; plus fog silhouette versions |
| Trail and road art | Footprint/dirt trail, worn road, fade-out for erased road |
| Fog art | Layered fog with soft edge, per-biome tint |
| Event art plates | Per event or per category, plus rest scenes |
| Roaming enemy and warden sprites | Silhouette plus revealed versions |
| Shift animation | Plate crossfade with trees "turning" |
| Boss art | 5 signature bosses plus warden |
| UI | Threat meter, rations strip, event card frame, relic icons |

Use the local ComfyUI pipeline (Qwen Image Edit 2.1 and others) per the existing battle art workflow.
- **Audio:** ambience per biome, trail footsteps by terrain, shift sting, threat heartbeat layer, event stingers, boss themes, UI sounds.

## 19. Open questions and risks

Resolved 2026-09-30: gacha scope, earned-only currency and pity numbers (section 17), pocket survives wipe, permadeath only in opt-in Hard mode, events drafted by Claude and edited by the user, new Silverwood map set made by Codex (5 depths x 3 proposed), auto battle always available, anchors and masks authored in an in-Unity editor tool, heroes via shards and story with 3 partner slots.

- **OQ1:** Duplicate-card conversion rates and per-hero shard counts beyond the first 10.
- **OQ2:** Atlas map size and resolution on mobile; draw cost, trail/fog texture size and save size.
- **OQ3:** Pocket slot count at max Tower level and the upgrade path.
- **OQ4:** Final map list per depth once Codex returns plates.
- **OQ5:** Event tone and content rating.
- **OQ8:** Partner slot effects design.

## 20. Accessibility and telemetry

- **Accessibility:** colour-blind-safe fog, road and threat colours (never colour alone: add pattern or icon); text scaling; reduced-motion option for the forest shift and screen effects; one-handed play with large touch targets.
- **Telemetry:** a local-only run log (steps, events, battles, loot, deaths, shift triggers) saved on device for balancing, no network. Opt-in anonymous analytics exists only for a public release and is off by default.
- Risk: shift can feel unfair if it erases progress without warning; mitigate with telegraphing and protected nodes.
- Risk: two map modes double test surface; share the provider interface and run both through the same simulation tests.
- Risk: concurrent battle-FX work is in `Scripts/Battle`; keep expedition work in Tower files and coordinate on shared hooks.
