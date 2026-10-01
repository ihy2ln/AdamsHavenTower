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
3. **Overview map:** travel node to node, uncovering fog and laying road.
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
| Party wipe | **Run loot is lost. Characters are kept**, return downed or injured and recover in the Tower. Unlocked depth, levels, bonds and discoveries are never lost. |

Existing code: `TowerRules.EndExpedition(bool wiped)`.

## 3. Two map modes

The player chooses at expedition start.

| | **Campaign** (hybrid) | **Unknown** (Atlas) |
| --- | --- | --- |
| Layout source | Premade overview map picked by RNG from the depth's pool | One large seed-generated forest |
| Fog | Nodes revealed along links | Fog of war on a continuous map, revealed by walking |
| POIs | Fixed slots on the map, contents rolled from run seed | Scattered by RNG, density and danger rise with distance from camp |
| Goal | Clear the map objective or boss, unlock next depth | Chase rewards: relics, currency, rare drops; boss POIs hidden in fog |
| Length | 10 to 15 min | 15 to 20 min, saveable at camps |
| Reward | Reliable depth progress and fixed drops | Higher variance, "roguelite gacha" pulls (open question OQ1) |
| Difficulty | Set by depth | Scales with distance walked and threat |

Both modes share the same fog, road, event, POI and battle systems. Only the layout provider and win condition differ, so the code path is one `MapProvider` with two implementations.

## 4. Fog of war and road-building

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
5. **Whispering moonwell:** drink for Stress relief and a Stress spike on the next battle.
6. **Raider tracks:** set an ambush (free first strike) or avoid (threat +).
7. **Sleepless night at camp:** companion story scene, bond +, firewood cost.
8. **Mushroom ring:** gamble for a relic or a weak enemy swarm.

Target content budget for Campaign launch: 40 events (20 universal, 20 depth or biome specific), each with 2+ outcomes. Event data lives in JSON so content grows without code.

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
- Auto/fast battle option for repeat fights, in the spirit of the "fast auto battle" reference.

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
| Per-run | Haul (gold, ore, essence, celestium, tonics), pocket slots (2) that survive a wipe if used wisely, relics, deck changes. |
| Roster | Character levels, **bond ranks**, new cards unlocked by bond. |
| Tower | Upgrades (guild, forge, barn, infirmary) raise carry capacity, rations efficiency, healing, vision. |
| Discoveries | Finding POIs, events and lore adds them to future pools (unlockable map/event pools). |

Open design: whether the pocket slots protect loot on a wipe (recommended: yes, that is their purpose).

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
| Fog reveal, roads, threat meter | `TowerThreat.cs` (P1 done) | Ambush on full threat (needs P2 event battles), lantern item |
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

- Portrait APK target. One-handed: map pan/zoom, tap node to preview cost (rations, risk), second tap confirms.
- Event card: art plate, text, 2 to 4 big buttons, check chances visible.
- Always-visible strip: rations, firewood, tonics, threat meter, party HP/Stress.
- Atlas map: pinch zoom, minimap, "return to camp" button.
- Settings: auto battle, reduced motion on shifts, text size, colour-blind-safe fog and road colours.
- Onboarding: first Campaign map is a fixed tutorial layout (rations, road, one event, one dungeon, one battle, camp).

## 14. Phased roadmap

| Phase | Deliverable |
| --- | --- |
| P1 | **Done 2026-09-30.** Fog reveal and road drawing on existing Campaign maps; threat meter; mid-run save/resume (state already saved after every action and on app pause/quit; new fields persist). |
| P2 | Event director plus 20 events; trait checks; consequences. |
| P3 | Run deckbuilding, relics, carry-over Stress; new dungeon room kinds. |
| P4 | Shift triggers and Silverwood depth pools; depth gating. |
| P5 | Unknown (Atlas) mode generator and reward loop. |
| P6 | Meta: bonds, discoveries, Tower upgrades tuned; content passes; balance. |

## 15. Open questions and risks

- **OQ1: Atlas "roguelite gacha".** Intended as a reward-chase loop. Needs rules: what is pulled (relics, cards, character shards?), pull currency source, pity, and a firm decision that no real-money mechanic is included.
- **OQ2:** Atlas map size and tile count on mobile; draw cost and save size.
- **OQ3:** Does the pocket protect loot on wipe? How many slots at max Tower level?
- **OQ4:** How are Campaign pools mapped (15 layouts across how many depths, reuse rules)?
- **OQ5:** Event content volume and who writes it (user, generated, or both); tone and rating.
- **OQ6:** Does permadeath ever apply (currently no)?
- **OQ7:** Auto-battle rules and whether it is allowed on bosses.
- Risk: shift can feel unfair if it erases progress without warning; mitigate with telegraphing and protected nodes.
- Risk: two map modes double test surface; share the provider interface and run both through the same simulation tests.
- Risk: concurrent battle-FX work is in `Scripts/Battle`; keep expedition work in Tower files and coordinate on shared hooks.
