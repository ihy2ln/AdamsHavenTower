# Adams Haven Card Game: Tower Mode GDD

**Status:** canonical design document for Tower Mode (v1 scope plus DLC roadmap).
**Supersedes:** `TOWER_LIFE.md` (as design authority; it remains a build log), the Godot `GDD.md` Tower Tycoon sections, `Tycoon-Tower.md`, `Tycoon-Tower-Handoff.md`.
**Decisions dated:** 2026-09-30. Anything marked **[TBD]** is an open balance or content item, listed again in section 16.

---

## 1. Vision and pillars

**Elevator pitch:** You are pulled into the world of Adams Haven and handed a dying crystal, the Celestium Heart, at the edge of Silverwood Forest. Grow a tower town around it: build rooms, staff them with villagers and summoned heroes, defend against the forest, and dig toward the Celestium beneath. Fallout Shelter's loop, dressed in Adams Haven's dark-timber and moonlit-crystal fantasy.

**Pillars**
1. **Fallout Shelter first.** Rooms produce, dwellers staff by stat, rush, incidents, expeditions, lunchbox-style summons. If a choice is between depth and clarity, pick clarity.
2. **The Heart is the game.** Research, upgrades and summoning all live at the Heart. It is the tower's centre, its progression gate and its lose condition.
3. **Every upgrade is visible.** A building grows from one bay to three and gains new furniture at every rank (F to SSR). The player can read progress off the tower.
4. **Ship v1, expand with DLC.** RimWorld-style depth is deliberately out of v1 (section 14).
5. **AAA feel on mobile.** Juice on every tap, a premium summon cinematic, a consistent art-directed UI, free rotation.

**Platform and model:** Unity, Android first, offline single-player, premium (no IAP, no ads in v1). Free rotation (portrait and landscape) in both Tower and Battle modes.

---

## 2. Premise, story and opening

- **Player:** picks a name at start. They are the **Summoner / Steward**, bound to the Heart. They are never a walking avatar: they appear as a portrait in dialogue, in the HUD and in the summon cinematic.
- **Isekai:** the player is transported to Adams Haven and wakes in a shack at **Silverbrook Edge**, on the eastern edge of Silverwood Forest.
- **Mission:** build a **Tower Town** around the dormant **Celestium Heart**. Villagers drift to the light; the forest answers with GKOM-touched monsters.
- **Opening (first 10 minutes, scripted tutorial):**
  1. Name entry, short text/portrait intro.
  2. Wake in the shack. The Heart stirs and accepts the player's name.
  3. Build the first room (Shack, F rank) and meet the first villager at the Gate.
  4. Free tutorial summon at the Heart (guaranteed hero, ranked B or better **[TBD]**).
  5. First small incident (pest or fire), then first Gate arrival and first goal.
- **Story delivery after the tutorial:** short dialogue beats tied to goals and Heart rank milestones, not cutscene-heavy. Regions unlock through expeditions (section 10).

---

## 3. Core loop

**Moment to moment (seconds):** tap collect bubbles, assign or swap workers, rush a room, react to an alert.

**Session (5 to 10 minutes):**
1. Open tower: offline earnings summary, collect all.
2. Handle alerts (incidents, full rooms, ready timers).
3. Spend: build or upgrade, queue research, summon.
4. Send or collect expeditions.
5. Check goals, then leave timers running.

**Daily:** daily goal board, expedition rotation, Gate arrivals, Heart research queue.

**Long term:** raise Heart rank, unlock floors and depth, upgrade buildings to SSR, collect and fuse heroes, clear Silverwood regions.

**Fallout Shelter mapping**

| Fallout Shelter | Tower Mode |
| --- | --- |
| Vault rooms | Buildings (21 types, F to SSR) |
| Dwellers, SPECIAL | Villagers + Heroes, seven stats |
| Rush | Rush (Tonics or success-chance rush) |
| Incidents (fire, roaches, raiders, deathclaws) | Fire, pests, Gate raiders, monsters, illness |
| Wasteland | Silverwood expeditions (auto and roguelite) |
| Lunchboxes | Heart summons (Sigils) |
| Overseer's office | Celestium Heart hub |
| Objectives | Goals, daily board |
| Pets | Out of v1 (DLC candidate) |

---

## 4. Tower structure

- **Layout:** side-view cutaway. The **Heart shaft** (cell 22) and the **Celestium Gate** (ground floor, cell 23) form the **right edge of the field**. Every building stands **west (left) of the shaft**; there is no east wing. Floors are founded westward, up to 10 cells.
- **Floors:** unlocked by Heart rank (table **[TBD]**, section 8.5). Above-ground floors build up; below-ground floors are dug.
- **Why right-edge:** buildings grow their new bays to the left (5.2), so with nothing east of the shaft every building always has room to grow.
- **Digging:** digging down costs Gold and time and moves through strata: dirt, soft rock, hard rock, Celestium-bearing rock, bedrock, full Celestium (the old Godot ladder reaches floor -18; final depth **[TBD]**). Deeper strata yield more Celestium through the Stone Quarry.
- **Cells:** one building occupies one or more contiguous cells on a floor. A building cannot straddle the Heart shaft.
- **Floor constraints (from catalog):** Farmstead requires a sunlit (above-ground) floor. Stone Quarry requires an underground floor.
- **Move everything:** any placed building can be moved (long-press). Nothing is permanently stuck.
- **Roof and foundation:** the tower is crowned by a roof sprite and rests on a foundation layer; these are presentation only (no gameplay).
- **Resolution of earlier docs:** the Godot docs said "no shared roof, nothing merges"; Unity added a roof crown. **Canon: roof crown is presentation only.** Same-type neighbour adjacency is covered in section 5.5.

---

## 5. Buildings

### 5.1 Tiers

Nine ranks per building: **F, E, D, C, B, A, S, SS, SSR**. Buildings **upgrade in place**; they are never replaced by a different building.

### 5.2 Bay growth (footprint)

| Ranks | Bays (cells) | Art |
| --- | --- | --- |
| F, E, D | 1 | Single room; furniture added each rank |
| C, B | 2 | Adds middle bay to the **left** |
| A, S, SS, SSR | 3 | Adds left bay |

- The **rightmost bay is fixed**; new bays appear to its left.
- **Upgrading C or A claims one more cell to the left.** If that cell is occupied or blocked, the upgrade **waits** until the player moves or clears the neighbour.
- Art is delivered as nine fixed-canvas tier images per building (example: `Barn_v2/tiers/barn_F.png` to `barn_SSR.png`, 2824 x 1024, bottom-right pivot). Gameplay footprint is logical and independent of the transparent art area.
- **Canon:** **logical footprint = bays shown** (1, 2, 3 by rank). Single-bay buildings (Gate, Heart, Shack, Stone Well, Grain Silo) stay 1 cell at every rank. **Every multi-bay building tops out at 3 cells, including the Guild** (the catalog's 4-cell Guild and other fixed widths are superseded; `catalog.json` and `TowerDomain.cs` must be updated to match).

### 5.3 What rank gives

Two axes, both required:
1. **More worker slots per bay** (each bay adds slots; exact counts **[TBD]**).
2. **New functions unlocked by rank** (for example a Kitchen unlocking stews at B; function table **[TBD]**).

Also rank raises: output multiplier, storage cap, and at **SSR** a named Celestium buff (Barn: storage capacity and animal production; others **[TBD]**).

Costs rise per rank in Gold plus materials; upgrades also require Heart rank (section 8.4) and a research unlock for S and above **[TBD]**.

### 5.4 Catalog (21 buildings)

Source of truth for art and ids: `Game Assets/buildings/Unity V1 Stuff/Celestium_Tier_Replacements_v1/catalog.json`; code authority: `TowerDomain.cs`.

| Category | Building | Role |
| --- | --- | --- |
| Core | Celestium Gate | Entrance; arrivals, guards, raids |
| Core | Celestium Heart | Research, upgrades, summon |
| Living | The Shack | Housing (starter) |
| Living | House | Housing (art family awaiting runtime assignment) |
| Living | Cottage | Housing |
| Living | Hearth Nursery | Births / children (simple v1, deeper in DLC) |
| Living | Terrace Row | Housing (high capacity) |
| Living | Ashgrove Manor | Housing (top tier, satisfaction bonus) |
| Produce | Kitchen | Food |
| Produce | Farmstead | Food (sunlit floors only) |
| Produce | Stone Well | Water |
| Produce | Lumber Mill | Firewood |
| Produce | Stone Quarry | Stone, Celestium ore (underground only) |
| Storage | Barn | Livestock, storage capacity |
| Storage | Grain Silo | Food storage |
| Storage | Warehouse | General storage |
| Service | The Frosted Mug | Tonics, healing (medic) |
| Service | Silverbrook Adventure Guild | Expeditions (herald) |
| Service | The Forge | Training, gear (train) |
| Service | Deck Hall | Training, strategy (train) |
| Service | Argent Market | Gold, trade |

Room-to-stat matching (each room keys off **one** stat, Fallout Shelter style; final mapping **[TBD]**, starting proposal):

| Stat | Rooms |
| --- | --- |
| Might | Stone Well, Lumber Mill, Stone Quarry, Forge |
| Grit | Kitchen, Farmstead |
| Charm | Argent Market, Frosted Mug |
| Sight | Guild Hall |
| Wit | Deck Hall, research helpers |
| Grace | Nursery, housing |
| Luck | Heart (summon luck helpers), Barn |

### 5.5 Adjacency
**Kept for v1.** Same-type neighbours give +12% each (max 3 neighbours) and pairs like Kitchen + Well give +8%. The bonus is shown in the UI as a **glowing link between connected rooms**; no merged art.

---

## 6. Dwellers

### 6.1 Types
- **Villagers:** arrive through the Gate, can be born (simple), run the economy. Non-combatants can also be **summoned at the Heart** (gacha).
- **Heroes:** summoned at the Heart, ranked F to SSR, stronger stats, go on expeditions and into Battle Mode. Heroes **can also staff rooms** (use their stats).

### 6.2 Stats
Seven stats: **Might, Sight, Grit, Charm, Wit, Grace, Luck.** Room output scales with the assigned dweller's matching stat (and optional training at Forge and Deck Hall).

### 6.3 Needs and mood (v1 light)
- Needs: **Food, Water, Rest** (plus injury/illness state).
- **Satisfaction** 0 to 100 with five face states (angry, frown, blank, smiley, ecstatic at 0-19, 20-39, 40-59, 60-79, 80-100).
- Low satisfaction lowers output and raises the chance of leaving. No mood-break minigame, no traits, no bonds in v1 (DLC, section 14).
- **Schedules / day-night:** day/night is presentation plus lighting. **Canon for v1: no schedule gameplay** (the Unity build's schedule system is parked with Colony Depth; see 16).

### 6.4 Levels and health
Dwellers earn XP and levels; heroes level faster via expeditions. Injured dwellers recover at the Frosted Mug (Tonics speed recovery). Critical wounds left untended cause death for villagers (timer **[TBD]**, current build: 240 live seconds). Heroes are pulled back by the Heart instead of dying.

**Hero availability:** a hero is either staffing a room **or** away on an expedition, never both. Sending a hero out empties their room slot until they return.

### 6.5 Steward (auto-assign)
A toggle that auto-assigns idle dwellers to the best-matching room. Manual assignment always overrides it.

---

## 7. Resources and economy

**Resources (icons in `Game Assets/ui/celestium_hud_v1`):**

| Resource | Role | Main sources | Main sinks |
| --- | --- | --- | --- |
| Firewood | Power for rooms | Lumber Mill | Rooms farthest from Heart go dark when empty |
| Food | Keeps dwellers fed | Kitchen, Farmstead | Dweller consumption, cooking |
| Water | Keeps dwellers hydrated | Stone Well | Dweller consumption, Farmstead |
| Gold | Build and upgrade currency | Argent Market, expeditions, goals | Building, digging, upgrades |
| Tonics | Healing and rush consumable | Frosted Mug | Healing, rush |
| Celestium | Research and high-rank upgrades | Deep strata (Quarry), expeditions, goals | Research, S+ upgrades |
| Sigils | Summon currency | Goals, research milestones, expeditions, daily | Heart summons |
| People | Population and housing cap | Gate arrivals, births, summons | n/a |
| Threat | Danger meter (0-100) | Population, floors, time, Heart rank | Falls with guards, wards, successful defence |

- **Firewood blackout rule (from the build):** when Firewood is zero, rooms farthest from the Heart go dark with a "NO FIREWOOD" state. The Mill, Heart and Gate never go dark.
- **Storage:** each resource has a cap raised by Barn, Silo, Warehouse and Heart rank.
- **Offline:** catch-up capped (**target 8 to 12 hours [TBD]**), produces resources but resolves incidents in a soft, non-lethal way. Construction and research continue offline.
- **No IAP in v1:** Sigils are earned generously so the gacha is playable for free.

---

## 8. Celestium Heart

The Heart is tapped to open a **full-screen hub** with four tabs.

### 8.1 Summon (default tab)
- Big animated Heart, Sigil balance, **1x and 10x** pulls, pity meter, rates screen (mandatory).
- Pulls yield **heroes** and **non-combatant residents** (villagers with stat bonuses).
- **Ranks:** F, E, D, C, B, A, S, SS, SSR (nine, same ladder as buildings).
- **Rates (draft [TBD]):** SSR about 1.5%, SS about 3%, S about 7%, lower ranks fill the rest. **Soft pity** ramps from about pull 50, **hard pity at about 60** for SSR.
- **Duplicates fuse** into the existing unit to raise its rank or level (no dead duplicates).
- **Tutorial summon** is free and guaranteed good.
- **Premium cinematic:** charge-up, per-rank reveal F to SSR with escalating VFX/SFX, skip button, 10-pull summary.

### 8.2 Research
- Tech tree pan/zoom UI, spent with **Celestium** (plus Gold for some nodes), **timed** with optional rush.
- Unlocks: new buildings, tier caps, floors, summon pool expansions, storage and production upgrades, expedition regions. Node list **[TBD]**.

### 8.3 Upgrade
Raises the Heart's own rank (F to SSR) using Celestium and materials.

### 8.4 Heart rank gates
Heart rank sets: maximum floors above and below ground, maximum building rank, research tier ceiling, dweller cap.

**Pacing target:** a player reaches **SSR Heart in about 60 to 90 days of play** (5 to 10 minute sessions). Rank E in about 1 day, D in about 3 days, C in about 1 week, then each step slows. Table values **[TBD]**, draft:

| Heart rank | Floors (up / down) | Max building rank | Notes |
| --- | --- | --- | --- |
| F | 2 / 2 | D | Tutorial |
| E | 3 / 4 | C | First stratum below dirt |
| D | 4 / 6 | B | |
| C | 5 / 8 | A | |
| B | 6 / 10 | A | |
| A | 7 / 12 | S | |
| S | 8 / 14 | SS | |
| SS | 9 / 16 | SSR | |
| SSR | 10 / 18 | SSR | Endgame |

### 8.5 Status
Heart HP, ward status, recent incidents.

**Heart destroyed = hard fail, the run ends.** Legacy carry-over: **heroes, research and Sigils persist; the tower resets** and the player starts a new run at Silverbrook Edge. To keep this fair on mobile, the Heart has **warning stages** (stable, strained, critical), a prominent alert at each stage, and prevention tools (**Tonics, wards, guards, defence heroes**). Exact carry-over rules for Gold, Celestium and building ranks **[TBD]**.

---

## 9. Threats and incidents

| Threat | Mechanic | Counter |
| --- | --- | --- |
| Fire | Spreads to adjacent rooms; damages room | Dwellers in the room fight it; Well-adjacent rooms resist |
| Pests | Infest a room, lower output | Dwellers in room clear it |
| Raiders at the Gate | Attack Gate; need threat 35+ | Gate guards (2 posts), defence heroes |
| Monsters from Battle Mode | GKOM-corrupted monsters can assault the tower | Heroes defend; can resolve through Battle Mode on demand |
| Illness and injury | Lowers output, can kill villagers | Frosted Mug, Tonics |
| Cave-ins | Risk when digging | Support upgrades, research |

- **Incident scheduling (v1):** simple weighted director driven by **Threat** and Heart rank. No selectable storyteller in v1 (DLC).
- **Alerts:** every incident generates a tray alert that jumps to the floor.
- **Rush:** shown with an explicit success chance; failure has a mild consequence (minor incident) so rush is a decision.

---

## 10. Expeditions

Run from the **Silverbrook Adventure Guild** (and the Expeditions dock button). Silverwood regions form an unlock chain beginning at **Silverbrook Edge** (8 regions in the current build; full list **[TBD]**).

Two modes per expedition:
1. **Auto:** send a party on a timer; the result is computed from party stats, rank and region difficulty. Default for idle play.
2. **Personal roguelite run:** the player plays the run themselves as a **20x20 dungeon crawl** that hands off to **Battle Mode** for fights.

**Rewards:** Gold, materials, Celestium, Sigils, Tonics, hero XP, rare region unlocks. Manual runs grant higher reward ceilings and exclusive drops; auto never punishes idle players with zero returns (floor on rewards).

**Battle Mode integration:** heroes used in expeditions are the same units used in Battle Mode; injuries carry back to the tower.

---

## 11. Progression, goals and endgame

- **Goals:** a rolling goal list (Fallout Shelter objectives), plus a **daily board** and a **story chain** that gates Heart ranks and regions. Goals pay Gold, Celestium, Sigils or Tonics.
- **Population:** grows via Gate arrivals, births (simple), and summons.
- **Endgame (endless with milestones):** SSR Heart, deepest stratum, all Silverwood regions cleared, full SSR building set, collection completion.
- **Soft difficulty:** Threat scales with tower size, so growth is always a decision.

---

## 12. Aesthetic and UI

### 12.1 Art direction
- **Tower:** side-view cutaway, **dark weathered timber, slate and stone** with **moonlit blue-silver and violet** Celestium light. Higher ranks add silver/violet refinement; SSR adds contained Celestium equipment.
- **Backdrop:** layered **Silverwood parallax** panorama with strata from sky down to Celestium rock.
- **Layers:** tower frame, rooms, residents, roof crown, foundation.
- **UI kit:** hand-painted anime-fantasy icons (`celestium_hud_v1` set) with a custom Celestium frame/button/font system. No default Unity UI styling in shipped screens.

### 12.2 Rotation and layout
Free rotation in **both** modes.

| Element | Portrait | Landscape |
| --- | --- | --- |
| Resource bar | Top | Top |
| Dock | Bottom | Right edge (vertical) |
| Panels (room card, build menu, roster) | Bottom sheets | Side sheets |
| Camera | Keeps the focused floor centered across rotation | Same |
| Safe areas | Respected (notches, gesture bars) | Respected |

### 12.3 Dock (five actions; Heart is the centre button)
1. **BUILD** (merged Build + Floors: categories, floor navigator, dig).
2. **PEOPLE** (villager + hero roster: filter, sort, assign, equip, fuse).
3. **HEART** (large centre button; glows when research, summon or upgrade is ready).
4. **EXPEDITIONS / MAP** (Silverwood map, active parties, roguelite entry).
5. **BATTLE** (direct entry to Battle Mode; the card game's permanent front door). Goals stay in the top-right cluster.

### 12.4 Top-right cluster
Menu, Speed (pause, 1x, 2x), **Steward toggle**, **Alert tray**, **Goals / Quest log**.
This replaces the old TIME and MENU dock buttons.

### 12.5 Top resource bar
Firewood, Food, Water, Gold, Tonics, Celestium, Sigils, People, Threat, Satisfaction. Tap any for detail (amount, cap, production/consumption rate, what to build). Low-stock pulse and colour warning.

### 12.6 Global actions
- **Collect all** (one tap).
- **Rush menu.**
- **Steward auto-assign.**
- **Alert tray** with tap-to-jump.
- **Goals / daily board.**

### 12.7 Room interaction
- **Tap:** opens the **room card** (workers, output and rate, upgrade with cost and Heart requirement, rush, move, demolish).
- **Drag** a dweller to reassign; **pinch** to zoom; **long-press** to move a room; **collect bubbles** float over full rooms.
- Upgrade blocked by a neighbour shows the blocking cell highlighted.

### 12.8 Screen list (v1)
Tower (main), Build menu, Room card, People roster, Hero detail, Heart hub (Summon, Research, Upgrade, Status), Summon cinematic, Expedition map, Expedition result, Roguelite run, Battle Mode, Alert tray, Goals and daily board, Menu and Settings, Offline earnings summary, Tutorial overlays.

### 12.9 AAA feedback catalogue
- **Juice:** press/release animation on every button, resource icons **fly to the top bar** on collect, room-ready glow, tier-up celebration (bay reveal with light sweep), floor unlock animation, numbers that tick up.
- **Haptics:** light on tap, medium on collect-all, heavy on SSR reveal and incidents.
- **Audio:** layered UI SFX per action type, dynamic music (calm day, tense incident, triumphant tier-up), ambient Silverwood.
- **Summon:** authored cinematic per rank, escalating colour from neutral (F) to violet-gold (SSR).
- **Onboarding and hierarchy:** guided tutorial, contextual tooltips, progressive disclosure (panels appear as features unlock), no more than one primary call to action per screen.

### 12.10 Accessibility and performance
- Text scale option, colour-blind-safe rank colours (rank is also conveyed by shape/icon), haptics and screen-shake toggles, reduce motion.
- Target 60 fps on mid-range Android; sprite atlases per building family; tier swaps are single-sprite swaps.

---

## 13. Save, offline and platform rules

- Single save slot per profile (beware of the **slot-switch save trap** noted in the project workflow memory).
- Offline catch-up capped, deterministic and non-punishing.
- Construction, research, expedition and rush timers use real time and continue offline.
- Autosave on background and on every meaningful change.
- Mobile-first build (Android 0.3.0 already builds). No network required.

---

## 14. v1 scope and DLC roadmap

### In v1
Fallout Shelter loop, 21 buildings with F to SSR tiers and bay growth, villagers and heroes, seven-stat room matching, needs and simple satisfaction, resources above, Heart hub (summon, research, upgrade), fire/pest/raid/monster/illness incidents, auto and roguelite expeditions, goals and daily board, free-rotation UI, AAA juice, summon cinematic.

### DLC expansions (RimWorld layer and content)
1. **Colony Depth:** traits, mood thoughts and mood breaks, bonds (friends, rivals, love), families, schedules.
2. **Storyteller:** incident director with selectable storytellers and difficulty.
3. **Crafting and Research:** workbenches, materials, gear chains.
4. **New regions and biomes:** more of Adams Haven beyond Silverwood.

Existing Unity systems beyond the v1 line (10 traits, mood breaks, bonds, storyteller, schedules) stay in the codebase behind flags and ship as the Colony Depth and Storyteller expansions.

---

## 15. Reconciled contradictions (what is canon)

| Topic | Old docs said | Canon |
| --- | --- | --- |
| Grid size | 15x21, 23x21, 24 cells, 20x12, 12x6 | Heart shaft and Gate on the right edge, one 10-cell wing to the west; Heart-rank tie-in **[TBD]** |
| Merging | Godot: nothing merges; Unity: +12% same-type | Adjacency bonus kept with glowing link UI (5.5), no merged art |
| Heart destroyed | Godot: defeat condition | Hard fail with legacy carry-over (8.5) |
| Building width | Catalog 1 to 4 cells | 1 / 2 / 3 cells by rank, max 3 (5.2) |
| Roof | Godot: no shared roof; Unity: roof crown | Roof crown is presentation only |
| Gate / stairs | Godot: east cells 22-23, stairs at 2; Unity: Heart centre with east wing | Heart and Gate are the right edge; no east wing (2026-09-30) |
| Tier ladders | Godot placeable F..SSR w/ SR; art uses SS | Buildings: F, E, D, C, B, A, S, SS, SSR; heroes the same nine ranks |
| Level vs rank | Level 1/2/3 = F/E/D | Rank is the only ladder; bays by rank (5.2) |
| Day/night | Godot: presentation; Unity: drives schedules | Presentation only in v1 |
| Villager death | 3 sim-minutes vs 240 live seconds | **[TBD]**, tune in balance pass |
| Cards vs moves | Collection + DP budget vs learned moves | Not a Tower concern; Battle Mode spec owns it |
| Battle system | AP/EP/CP/SP (Godot) vs CZN-style | Owned by `BATTLE_PORT.md` / `CHAOS_ZERO_REFERENCE.md` |

---

## 16. Open questions and balance TODO

1. **Code and catalog sync:** `TowerDomain.cs` now follows 5.2 (F-SSR for every building, 1/2/3 bays growing left, Guild max 3), 8.4 building-rank caps, and the 8.5 hard fail with legacy carry-over (2026-09-30). `catalog.json` still lists the old fixed widths. The east wing is removed: the Heart and Gate are the right edge, and old east-wing rooms move west on load.
2. Numbers: building costs per rank, production rates, storage caps, timers, worker slots per bay.
3. Heart rank to floors and building-rank table (8.4 is a draft).
4. Research tree node list and costs.
5. Hero roster size, rarity distribution per rank, resident (non-combatant) pool.
6. Gacha rates and pity final values; fusion rules.
7. Rank-function tables per building; SSR buffs per building (only Barn named).
8. Silverwood region list, and expedition reward tables.
9. Carry-over details after a hard fail (Gold, Celestium, building ranks).
10. Villager critical-wound timer.
11. Offline cap hours and incident handling offline.
12. **Tutorial script and first-hour goal chain (next round, drafted together).**
13. Audio direction (music, SFX) and font selection.
