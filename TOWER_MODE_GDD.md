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
- **Opening:** the first hour is a guided 15-step chain (section 2.1).
- **Story delivery after the tutorial:** short dialogue beats tied to goals and Heart rank milestones, not cutscene-heavy. Regions unlock through expeditions (section 10).

### 2.1 First-hour tutorial chain

**Rules:** 15 steps, about 60 minutes. **Spotlight-guided** (everything dims except the target) and **skippable per step**. Every step pays a reward. The first free summon comes early (step 4) and is guaranteed **B rank or better**. The first incident is a **fire in the Shack**. The chain ends with the first expedition and the first Heart rank-up. The Goals board mirrors the chain.

| # | Step | Teaches | Reward |
| --- | --- | --- | --- |
| 1 | Name entry, wake in the shack, the Heart stirs | Premise, player role | n/a |
| 2 | Build the first Shack (F) | BUILD menu, placement | Gold |
| 3 | First villager arrives at the Gate; assign them to the Shack | PEOPLE, drag-assign | Food |
| 4 | **Free summon at the Heart** (B+ hero guaranteed) | HEART hub, Summon, cinematic | Hero + 5 Sigils |
| 5 | Build a Kitchen, assign a Grit dweller | Room-stat matching, collect bubble | Gold |
| 6 | Build a Stone Well | Resources, tap-for-detail on the top bar | Water |
| 7 | Collect all | Collect-all, offline summary habit | Gold |
| 8 | **Fire in the Shack:** drag a dweller in to put it out | Incidents, alert tray, tap-to-jump, fire response | Tonics |
| 9 | Rush the Kitchen | Rush, success chance, Tonics | Tonics |
| 10 | Build a Lumber Mill; learn Firewood and blackout | Power rule | Firewood |
| 11 | Upgrade the Kitchen F to E | Room card, upgrade, tier-up celebration | Gold |
| 12 | Start the first research node | Research tab, Celestium, timers | 20 Celestium |
| 13 | Build the Silverbrook Adventure Guild | Expeditions unlock | Gold |
| 14 | Send the first **auto** expedition with the summoned hero | Party, timers, hero away empties their room slot | Sigils |
| 15 | Collect results, then **Heart rank F to E** | Heart upgrade, floor unlock animation | New floor + 10 Sigils |

**After step 15:** the first goals introduce the manual roguelite run, digging, the Steward toggle and the Battle dock button. Exact reward amounts and step timing **[TBD]**.

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

Room-to-stat matching, Fallout Shelter style. **Each room lists the one to four stats that count as a match** (a matched worker gets the full +1% per stat point; an unmatched worker gets no stat bonus). Draft mapping, also used by the character prompt pack (`CharacterPrompts/`):

| Room | Matching stats |
| --- | --- |
| Stone Well, Lumber Mill, The Forge | Might, Grit |
| Stone Quarry | Might, Grit, Wit |
| Kitchen | Grit, Charm |
| Farmstead | Grit, Grace |
| Argent Market | Charm, Luck, Grace |
| The Frosted Mug | Charm, Grace, Wit |
| Silverbrook Adventure Guild | Sight, Grace, Charm |
| Deck Hall | Wit, Grace |
| Hearth Nursery | Grace, Charm |
| Barn | Luck, Grit |
| Celestium Heart (sanctuary helpers) | Luck, Charm, Wit, Grit |

Housing, Grain Silo and Warehouse take any worker with no stat bonus **[TBD]**.

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

### 6.6 Hero identity
- **Element (7):** Fire, Wind, Earth, Lightning, Water, plus the opposed Light/Dark pair, using the same chart as Battle Mode (Fire > Wind > Earth > Lightning > Water > Fire; Light and Dark oppose each other).
- **Role (4):** Tank, Striker, Support, Controller.
- Element and role are shared with Battle Mode, so there is **one hero definition** across Tower and Battle. In the Tower, a hero's **seven stats** drive room bonuses; element and role drive expeditions and Battle Mode.

### 6.7 Hero and resident scaling (draft numbers)
**Roster (v1): 36 heroes, 4 per rank (F to SSR), plus 24 named residents.** The full roster (names, races, body types, chest/waist/hips measurements, height, elements, roles, stats, outfits, weapons, abilities; every character is 21+) and 696 ready-to-run Codex image prompts are in `CharacterPrompts/` (`roster.csv`, `roster.json`, one file per character). Kestrel (Fire, B) and Sable (Wind, B) come from the VN canon; JD is the Battle Mode support and is not in the gacha roster.

**Hero spread:** elements Fire 6 and the other six 5 each; roles 9 each (Tank, Striker, Support, Controller); size classes 12 each (Small, Medium, Large).

**Resident spread:** 24 individuals with a **fixed rank each** (F 4, E 4, D 3, C 3, B 3, A 3, S 2, SS 1, SSR 1), 8 per size class. They never fight and each has a tower perk tied to their room.

| Rank | Stat band (primary stat is highest) | Level cap |
| --- | --- | --- |
| F | 2 to 5 | 20 |
| E | 4 to 8 | 30 |
| D | 7 to 12 | 40 |
| C | 11 to 17 | 50 |
| B | 16 to 24 | 60 |
| A | 22 to 32 | 70 |
| S | 30 to 42 | 80 |
| SS | 40 to 55 | 90 |
| SSR | 52 to 70 | 100 |

- **Rank sets the stat range and level cap. Fusion adds stars.** A duplicate fused into the same hero adds **1 star** (max 5); each star gives **+8% stats**.
- **Ascension:** at 5 stars a hero can ascend one rank (costs Celestium), resetting to 1 star with the higher rank's stat band and level cap.
- **Echoes:** duplicates beyond 5 stars convert into Echoes, a fusion currency that can star up any hero (rate **[TBD]**).
- **Room bonus:** a matched worker gives **+1% output per matching stat point**. Villagers have stats of about 1 to 10, so +1% to +10%; an SSR hero's primary stat gives up to +70%.
- **Residents** use the same rank stat bands, with one key stat each (Might 5, Grit 5, Grace 4, Charm 3, Wit 3, Sight 2, Luck 2) and a perk such as "Farmstead cycles finish slightly faster". Perk values **[TBD]**.
- Heroes may staff rooms only while not on an expedition (6.4).

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

### 7.1 Building economy model (draft, numbers are tuning starting points)

**Upgrade cost curve:** exponential, about **2.2x per rank**. Base price is the F-rank build cost; each upgrade costs Gold = `base x 2.2^(rank step)`, rounded. Example with a 100 Gold base building:

| Upgrade to | E | D | C | B | A | S | SS | SSR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Gold | 100 | 220 | 480 | 1,060 | 2,340 | 5,150 | 11,350 | 24,950 |
| Phase | Early | Early | Mid | Mid | Mid | Late | Late | Late |
| Extra material | none | none | Firewood + Stone | Firewood + Stone | Firewood + Stone | Celestium | Celestium | Celestium |

- **Phase-based materials:** E and D cost Gold only. C, B and A add **Firewood and Stone** (roughly 40% and 25% of the Gold figure). S, SS and SSR add **Celestium** (draft 5, 15, 40 for a base-100 building). This pulls every production chain into play, and makes the Quarry and deep strata necessary for the endgame.
- **Base price by category (draft):** Shack 100, Well 150, Kitchen/Farmstead 250, Lumber Mill/Quarry 400, Barn/Silo/Warehouse 300 to 600, Frosted Mug/Forge/Deck Hall 500, Market 600, Guild 800, Manor 1,000.
- **Time:** upgrade timers scale at about 2x per rank (F to E in minutes, SS to SSR in many hours), all rushable. Rank C and above can only be started at a Heart rank that allows it (8.4).
- **Footprint:** C and A upgrades claim an extra cell (5.2); if blocked, the timer does not start.

**Worker slots:** **2 slots per bay**, so **2 / 4 / 6** at 1 / 2 / 3 bays (F-D, C-B, A-SSR). Single-bay buildings (Shack, Well, Silo) stay at 2 slots but gain output multipliers and capacity through rank instead. Housing rooms use the same count as resident capacity (Shack 2, House 4, Terrace Row 6, etc., final numbers **[TBD]**).

**Output model (Fallout Shelter style):** each production room fills a **visible cycle timer**, then shows a **collect bubble**; **Collect all** gathers every full room.
- `cycle yield = base yield x rank multiplier x (1 + stat bonus) x adjacency bonus`
- `cycle time = base time / (1 + stat bonus)` so a matched stat shortens the cycle; a mismatched dweller is slower.
- **Rank multiplier (draft):** F 1.0, E 1.15, D 1.3, C 1.5, B 1.7, A 1.95, S 2.2, SS 2.5, SSR 3.0.
- **Base cycle (draft):** 4 minutes at F, so a 5 to 10 minute session sees one to two cycles per room. Higher ranks lengthen the cycle slightly while multiplying yield more, rewarding less frequent check-ins.
- **Stat bonus (draft):** **+1% per point of the matching stat** (6.7); a mismatched worker gets no bonus. Tuning **[TBD]**.
- **Offline:** cycles accumulate up to the cap in section 7 but **do not fire collect bubbles; they bank into the room** until collected, at most one full cycle per slot-count so idle players are not punished for leaving.
- **Storage:** each resource has a cap raised by Barn, Silo, Warehouse and Heart rank; rooms that cannot bank pause with a clear "Full" state.

**Tuning anchors:** SSR Heart in about 60 to 90 days of play; first-hour tutorial is affordable on starting Gold; a mid-game tower (about 25 buildings at B to A) should need roughly one to two weeks of play.

---

## 8. Celestium Heart

The Heart is tapped to open a **full-screen hub** with four tabs.

### 8.1 Summon (default tab)
- Big animated Heart, Sigil balance, **1x and 10x** pulls, banner tabs, pity meter, rates screen (mandatory).
- Pulls yield **heroes** (36) and **non-combatant residents** (24). Ranks F, E, D, C, B, A, S, SS, SSR.

**Banners (v1)**

| Banner | Pool | Cost per pull | Notes |
| --- | --- | --- | --- |
| Standard | All heroes | 10 Sigils (100 per 10-pull) | Permanent |
| Featured | All heroes, rate-up | 10 Sigils | Rotating; one SSR and one SS boosted |
| Pick-Your-Hero | All heroes, player picks one SSR or SS target | **20 Sigils (2x)** | Same rates as Featured, aimed at the chosen hero |
| Resident | The 24 residents | 5 Sigils | Cheap way to fill the tower with specialists |

**Standard rates per pull (draft):**

| F | E | D | C | B | A | S | SS | SSR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 26.5% | 22% | 17% | 12% | 8% | 6% | 4.5% | 2.5% | 1.5% |

- **10-pull guarantee:** at least one B or better.
- **Pity:** soft pity ramps SSR odds from about pull 50; **hard pity guarantees SSR at 60**.
- **Featured and Pick-Your-Hero:** 50% of SSR/SS pulls are the featured or chosen hero; after a miss, the next SSR/SS is guaranteed to be it.
- **Resident banner:** SSR about 1%, guaranteed A or better every 20 pulls.
- **Income target:** about 45 free Sigils per day (goals, daily board, expeditions, research), so an active player sees roughly one SSR every 2 to 3 weeks and 5 to 6 SSR by day 80.
- **Duplicates** fuse into the existing hero as stars (6.7), so no pull is dead.
- **Tutorial summon** is free and guaranteed B or better (default Kestrel, **[TBD]**).
- **Premium cinematic:** charge-up, per-rank reveal F to SSR with escalating VFX/SFX, skip button, 10-pull summary.

### 8.2 Research
- Pan/zoom tree UI. Nodes cost **Celestium** (plus Gold from tier 3), are **timed**, and can be rushed with Tonics.
- **One research at a time** in v1. A second queue slot is an open question (section 16).
- **40 nodes: 5 branches x 8.** Nodes 1-2 are tier 1, 3-4 tier 2, 5-6 tier 3, 7 tier 4, 8 tier 5. A node's tier must not exceed the Heart's research tier (8.4).
- **Draft costs and timers (per node):**

| Tier | Nodes | Celestium | Gold | Timer |
| --- | --- | --- | --- | --- |
| 1 | 10 | 10 | 0 | 10 min |
| 2 | 10 | 30 | 0 | 1 h |
| 3 | 10 | 90 | 500 | 3 h |
| 4 | 5 | 270 | 3,000 | 8 h |
| 5 | 5 | 800 | 15,000 | 16 h |

  Total about 6,650 Celestium for the full tree, to be validated against Quarry and expedition income (target: complete by about day 90 of play).
- **Starting buildings (no research):** Shack, Kitchen, Stone Well, Lumber Mill, Silverbrook Adventure Guild. Everything else unlocks through Construction.
- All non-Construction effects are small and additive, so no single node is mandatory.

#### 8.2.1 Node list

| # | Construction (CON) | Settlement (SET) | Production (PRO) | Defence and Medicine (DEF) | Exploration and Summoning (EXP) |
| --- | --- | --- | --- | --- | --- |
| 1 (T1) | Farmstead + House | Shared meals (+satisfaction) | Crop rotation (+5% Food) | Watchfires (fire damage -15%) | Guild charts (opens regions 2-3) |
| 2 (T1) | Argent Market + Cottage | Cozy lighting (+satisfaction) | Sharper tools (+3% yield, all rooms) | Pest bait (pests -20%) | Second party slot (the third comes from Guild rank A) |
| 3 (T2) | Barn + Grain Silo + Frosted Mug | Better beds (+10% housing) | Seasoned timber (+5% Firewood) | Gate guard post (3rd guard) | Sigil focus (+10% Sigil drops) |
| 4 (T2) | Stone Quarry + Warehouse | Apprenticeships (+XP) | Deep wells (+5% Water) | Field medicine (faster healing) | Regions 4-5 |
| 5 (T3) | The Forge + Deck Hall | Nursery births | Pack storage (+15% caps) | Wardstones (Heart warning stage delay) | Earlier soft pity (about pull 45) |
| 6 (T3) | Hearth Nursery + Terrace Row + Ashgrove Manor | Festival (satisfaction event) | Trade ledgers (+8% Gold) | Tonic still (+Tonic output) | Regions 6-7 |
| 7 (T4) | Celestium fittings (unlocks SSR upgrades) | Hero housing (heroes recover faster) | Cycle shortening (-8% cycle time) | Hero bulwark (defence heroes stronger) | Fusion boon (cheaper fusion) |
| 8 (T5) | Master builders (-10% upgrade Gold) | Steward Mk II (smarter auto-assign) | Celestium sieve (+Celestium from Quarry) | Celestial aegis (monster damage -30%, Heart regeneration) | Region 8 (Silverwood Gate) + SSR odds +0.5% |

Note: the tutorial (2.1) only uses starting buildings, so it never depends on a Construction unlock. The Construction chain is paced to keep new buildings arriving in step with the Heart table.

### 8.3 Upgrade
Raises the Heart's own rank (F to SSR) using Celestium and materials.

### 8.4 Heart rank gates
Heart rank sets: maximum floors above and below ground, maximum building rank, research tier ceiling, dweller cap.

**Pacing target:** a player reaches **SSR Heart in about 60 to 90 days of play** (5 to 10 minute sessions). Each Heart upgrade costs Gold plus Celestium (Celestium from rank C) on the same 2.2x curve as buildings, with timers about 2x per rank. Table values are draft tuning targets:

| Heart rank | Target day | Floors (up / down) | Max building rank | Research tier | Dweller cap | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| F | 0 | 2 / 1 | D | 1 | 8 | Tutorial start |
| E | 1 | 3 / 3 | D | 1 | 14 | Tutorial ends here; first dig |
| D | 3 | 4 / 5 | C | 2 | 22 | Quarry becomes useful |
| C | 7 | 5 / 7 | B | 2 | 32 | Celestium starts costing Heart upgrades |
| B | 14 | 6 / 9 | A | 3 | 44 | |
| A | 25 | 7 / 12 | S | 3 | 58 | |
| S | 40 | 8 / 14 | SS | 4 | 74 | |
| SS | 60 | 9 / 16 | SSR | 4 | 92 | SSR building upgrades allowed (needs CON-7) |
| SSR | 80 | 10 / 18 | SSR | 5 | 110 | Endgame |

### 8.5 Status
Heart HP, ward status, recent incidents. Heart damage sources are only breaches and unattended fires on its floor (section 9.4, 9.6).

**Heart destroyed = hard fail, the run ends.** Legacy carry-over: **heroes, research and Sigils persist; the tower resets** and the player starts a new run at Silverbrook Edge. To keep this fair on mobile, the Heart has **warning stages** (stable, strained, critical), a prominent alert at each stage, and prevention tools (**Tonics, wards, guards, defence heroes**). Exact carry-over rules for Gold, Celestium and building ranks **[TBD]**.

---

## 9. Threats and incidents

Grounded in the existing Unity systems (`TowerLife.cs`, `TowerSystems.cs`, `TowerThreat.cs`, `TowerHeart.cs`). Numbers marked draft are tuning starting points.

### 9.1 Threat meter
- **Target** = population x 1.2 + rooms x 0.25 + max(0, day - 3) x 0.5 + Heart rank x 3, **minus 30% of defence**, clamped 0 to 100. Defence = for each defender (priority set to defence, not downed) weapon x 2 + Might x 0.3; heroes at home count the same way, scaled by stars.
- The live meter moves toward the target at 0.5 per second.
- **Labels:** Calm (under 25), Uneasy (under 50), Tense (under 75), Dire (75 or more).

### 9.2 Incidents

| Incident | Trigger | HP | Responding job | If ignored | Counters |
| --- | --- | --- | --- | --- | --- |
| Fire | Always possible; rush failure | 45 | Fire | Room condition drops, spreads to neighbours, burns workers | Watchfires (-15%), Well adjacency |
| Pests | Always possible; rush failure | 65 | Defence | Room output drops | Pest bait (-20%) |
| Illness | 5+ residents and Threat 20+ | 45 | Care | Lowers output, can kill a villager | Frosted Mug, Tonics, Field medicine |
| Raiders | Threat 35+ (double weight at 55+) | 100 | Defence | Breach (9.4) | Gate guards, defence heroes |
| Cave-in | Any underground room | 70 | Repair | Room blocked until repaired | Repair priority, research |

Severity = 1 + min(2, residents / 20); incident HP scales with it.

### 9.3 Pace
- One incident roughly every **480 to 600 live seconds** (8 to 10 minutes), shortened by up to 25% at Tense and Dire.
- The first hour is guided (tutorial 2.1). No two incidents in one room. At most **2 active incidents** until Heart rank C, **3** from C.
- **Positive events** also fire: a Silverbrook caravan (about 13%), a harvest festival (about 7%) and a lone traveller (about 6%) per roll.
- Every incident raises a tray alert that jumps to the floor. No selectable storyteller in v1 (DLC).
- **Rush failure** remains a minor fire or pests incident, so rush is a decision.

### 9.4 Raids and the Gate
- Raiders arrive at the Gate. **Two guard posts** defend (a third from research DEF-3); guards fight with defence heroes at home who auto-join.
- If the guards lose, the Gate is **breached**: raiders push inward along the ground floor toward the Heart shaft. A visible breach timer and alert appear. They damage rooms in their path.
- **The Heart takes damage only once a raider reaches it** (draft: about 6 x severity HP per second), or from an unattended fire on its floor (fire on the Heart's floor gets top response urgency).

### 9.5 Heart warnings and care
- **Heart HP by rank (F to SSR):** 1,200, 1,800, 2,600, 3,400, 4,300, 5,300, 6,400, 7,600, 9,000.
- **Stages:** stable (above 60% HP), strained (25% to 60%), critical (under 25%), each with an alert; hitting 0 is the hard fail (8.5).
- **Care tools:** Wardstones (DEF-5) delay a stage change; Heart regeneration (DEF-7) slowly heals while stable; **Heart Repair** spends Celestium at any time (draft: 1 Celestium per 40 HP).

### 9.6 Gate sieges (Battle Mode monsters)
- **Rare:** about once every 3 to 4 days while Threat is Dire. A **10-minute warning banner** appears first. The wave scales with Heart rank.
- The player chooses **Battle Mode** (3 home heroes defend) or **auto-defend** (defender power vs wave danger, using the expedition power formula, 10.3).
- **Win:** Gold, Celestium, a chance of Echoes, and Threat drops sharply.
- **Lose:** the Gate falls and a 3-minute **Last Stand** breach begins (9.4 rules; it can still be won with defenders and heroes). Damage reaches the Heart only through that breach.
- **No sieges while offline.**

### 9.7 Illness and injury
Unchanged rules: a critically wounded villager dies after 240 live seconds untended **[TBD final value]**; heroes are pulled back by the Heart instead of dying. The Care job, Frosted Mug and Tonics treat them.

### 9.8 Offline soft resolve
Incidents that would start offline are handled by guards and idle dwellers with **small capped losses only** (a little room condition, food or gold). **No deaths, no Heart damage, no sieges.** A short event log is shown on return.

---

## 10. Expeditions

Run from the **Silverbrook Adventure Guild** (and the Expeditions dock button). Built on the existing Unity systems: `TowerExpedition.cs` (regions, provisions, Safe Pocket), `TowerForestLayouts.cs` (15 forest maps), `TowerDungeon.cs` (20x20 crawl), `TowerGuild.cs`.

### 10.1 Parties
- **3 heroes per party.** Villagers never go on expeditions.
- **Party slots:** 1 at the start, a 2nd from research (EXP-2), a 3rd from the Guild reaching rank A.
- A hero is either staffing a room or away; sending a hero out empties their room slot (6.4). Injured heroes cannot be sent until healed.

### 10.2 Regions
Eight regions in a branching unlock chain (existing build, region list from `TowerExpedition.cs`):

| # | Region | Opens after | Research gate | Danger (draft) | Auto time (draft) |
| --- | --- | --- | --- | --- | --- |
| 1 | Silverbrook Edge | start | none | 70 | 30 min |
| 2 | Rootside Camp | Edge | EXP-1 | 110 | 1 h |
| 3 | Shallow Ford | Edge | EXP-1 | 140 | 1.5 h |
| 4 | Moonlit Shrine | Rootside Camp | EXP-4 | 230 | 2 h |
| 5 | Old Bridge | Shallow Ford | EXP-4 | 300 | 3 h |
| 6 | Sunken Marsh | Shallow Ford | EXP-6 | 420 | 4 h |
| 7 | Ruined Watchpost | Old Bridge | EXP-6 | 560 | 6 h |
| 8 | Silverwood Gate | Watchpost | EXP-8 | 800 | 8 h |

A region unlocks when the previous region's lair boss is conquered **and** its research node is done. Each region has a creature element (**[TBD]**) that element counters apply against.

### 10.3 Auto expeditions
- **Hero power** = (sum of the 7 stats) x (1 + 0.08 x stars) x (0.5 + 0.5 x level / level cap). **Party power** = sum of the 3 heroes.
- **Ratio** = party power / region danger, with bonuses: +10% per element counter against the region's creatures, +10% if the party has both a Tank and a Support, +5% with a Controller.
- **Result tiers:** Fail (ratio under 0.6), Partial (0.6 to 0.9), Success (0.9 to 1.3), Great (1.3 or more).

| Tier | Reward multiplier | Injury chance |
| --- | --- | --- |
| Fail | 0.25 (never zero) | 70% |
| Partial | 0.6 | 30% |
| Success | 1.0 | 10% |
| Great | 1.4 | 0% |

- **Rewards:** Gold, Firewood and Stone, Tonics, hero XP; Celestium from region 4 on; a small amount of Sigils (about 15 per day on average, part of the 45 per day target). Scale = base x region multiplier (1.0 to 3.0 in the current build).
- Injured heroes recover at the Frosted Mug; Tonics speed it up.
- Auto expeditions **never conquer** a region.

### 10.4 Personal roguelite run
Reuses the existing flow: **region map, plan provisions (rations), forest map (travel costs rations), POI dungeon (20x20, fog of war, room events, stairs, goal room), lair boss conquers the region.**
- **Length target: 10 to 15 minutes**, trimmed to about 2 to 3 floors per POI. The run **autosaves on every room**, so the player can quit and resume exactly where they left.
- **Fights use Battle Mode** with the party's 3 heroes. HP and injuries carry through the run; Tonics heal.
- **Rewards:** up to 2x the auto ceiling plus exclusive drops (Echoes from boss clears, Celestium, rare Sigil caches).
- **First conquest of a region** grants a permanent passive (for example +10% auto rewards from that region) **[TBD]**.
- **Wipe or abandon:** the player keeps the 2-slot **Safe Pocket** and loses everything else carried; heroes return injured. No hero is ever lost.

### 10.5 Battle Mode integration
Heroes used in expeditions are the same units used in Battle Mode; injuries and level-ups carry back to the tower.

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
**Five resources are pinned** on the bar (Gold, Celestium, Sigils, Food, Water). A **pull-down drawer** (tap or swipe the bar) shows the rest: Firewood, Tonics, People, Threat, Satisfaction. Tap any resource for detail (amount, cap, production/consumption rate, what to build). Low-stock pulse and colour warning also appear on the pinned bar for a drawer resource that is in trouble (for example a Firewood blackout).

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

### 12.11 Layout rules and the three window modes
- **Safe areas** are respected on all edges (notches, gesture bars). Side gutters 16 px; minimum touch target 48 dp.
- **Top bar** 56 dp. **Dock** 72 dp: bottom in portrait, right edge in landscape.
- **Every panel has three window modes**, chosen by icons in the panel header and remembered per panel:

| Mode | Size | Used for | Tower behind it |
| --- | --- | --- | --- |
| Pop-out | about 40% of the short screen edge, anchored to what you tapped | Quick actions (room card, resource detail, node detail, alert) | Fully visible and live |
| Medium | about 65% of the short edge (bottom sheet in portrait, side sheet in landscape) | Normal use (room card with workers, Build menu, People list) | Dimmed but visible |
| Full screen | 100% | Deep work (Heart hub, Research tree, Hero detail, Expedition map) | Hidden, paused visuals |

- **Rotation reflow:** a panel keeps its mode when the screen rotates; bottom sheets become side sheets (and back). Scroll position and selection are kept.
- **Back/close:** back or tap-outside closes a pop-out or medium panel; full screen has an explicit close. Closing returns the camera to where the player was.
- **One primary action** per panel, in the same corner every time (bottom-right in portrait, bottom of the sheet in landscape).

### 12.12 Tower main screen
Portrait:

```text
+--------------------------------------+
| [Gold][Celest][Sigil][Food][Water] v | <- top bar, v = drawer
|                  [ii][1x][S][!][G][=]| <- pause/speed, Steward, alerts, goals, menu
|  floor  +----------------------+   M |
|  +4     |  room  | |  room     |   i |
|  +3     |  room  |H|  room  (!)|   n |
|  +2 ... |  room  |E|  room     |   i |
|  +1     |  room  |A|  room     |   m |
|   0     | shack  |R| kitchen G |   a |
|  -1     |  quarry|T| well      |   p |
|  -2     +----------------------+     |
|        [Collect all]   [Rush]        | <- floating action buttons
+--------------------------------------+
| [BUILD][PEOPLE][  HEART  ][MAP][BATTLE]| <- dock, Heart centered and larger
+--------------------------------------+
```

Landscape:

```text
+---------------------------------------------------------------+---+
| [Gold][Celestium][Sigils][Food][Water] v   [ii][1x][S][!][G][=]| B |
|                                                               | U |
|   floor +----------------------------+     (collect bubbles)  | I |
|   +2    |  room  |  H  |  room   (!) |                         | L |
|   +1    |  room  |  E  |  room       |   [Collect all]         | D |
|    0    | shack  |  A  | kitchen  G  |   [Rush]                | P |
|   -1    |  quarry|  R  | well        |                         | E |
|         +----------------------------+                         | O |
|   minimap strip on the left edge                               | H |
|                                                               | M |
+---------------------------------------------------------------+ B |
```

- **Camera:** vertical scroll with the **Heart shaft centered**; pinch to zoom; a **floor minimap strip** on the screen edge marks incidents (red), ready rooms (gold) and the player's current view; tap a minimap tick to jump.
- **Floating bubbles** (collect, incident (!)) sit above rooms; **Collect all** and **Rush** are the two floating actions.
- **Drawer** (pull down from the bar): Firewood, Tonics, People, Threat, Satisfaction, each tappable for the pop-out detail card.

### 12.13 Room card and Build menu
Room card (the same content, three sizes):

```text
Pop-out (anchored to the room)       Medium (bottom/side sheet)
+----------------------+            +--------------------------------------+
| Kitchen  C   [m][M][F]|            | Kitchen  rank C                [m][M][F]|
| Food  +12/cycle  62% |            | Output  Food +12 / 4:00   [Collect]    |
| [Collect] [Rush 78%] |            | Workers (2/4)  [Wit icon] [ + ]        |
| [Upgrade -> B]       |            | Bonus: Grit match +17%  adjacency +12% |
+----------------------+            | [Upgrade -> B  1,060g, Heart D+]  [Rush]|
                                    | [Move] [Demolish]                      |
                                    +--------------------------------------+
Full screen adds: stat history, per-worker breakdown, upgrade preview art (all 9 ranks), adjacency map.
```

Build menu (medium or full):

```text
+--------------------------------------------------+
| BUILD            [Floors v]  [Dig down]    [m][M][F]|
| [Living][Produce][Storage][Service][Core]        |
| +--------+ +--------+ +--------+ +--------+     |
| | Shack  | | Kitchen| | Well   | | Mill   |     |
| | 100g   | | 250g   | | 150g   | | 400g   |     |
| +--------+ +--------+ +--------+ +--------+     |
| locked items show the research or Heart rank that unlocks them |
+--------------------------------------------------+
```

- **Placement mode:** drag the building ghost over the tower; valid cells glow, blocked cells show why. An upgrade that needs the cell to the left shows the blocking cell highlighted.

### 12.14 Heart hub
Full screen with four tabs. Summon is the default.

```text
+--------------------------------------------------+
| [x]   HEART          Sigils 245     [m][M][F]    |
| [Summon][Research][Upgrade][Status]              |
|   [Standard][Featured][Pick-Your-Hero][Resident] |
|              ( animated Heart )                  |
|        pity  ####------  38 / 60                 |
|     [ x1  10 Sigils ]   [ x10  100 Sigils ]      |
|                                  [Rates]         |
+--------------------------------------------------+
```

- **Research:** pan and zoom tree with five branches; tap a node for a **pop-out detail** (cost, time, effect, Rush); the active research shows a progress ring on the Heart button.
- **Upgrade:** current Heart rank, what the next rank unlocks (floors, building rank cap, research tier, dweller cap), cost, and **Heart Repair**.
- **Status:** Heart HP, stage (stable, strained, critical), wards, recent incidents.
- **Summon cinematic frames:** charge-up, rank-colored light burst, unit reveal, rank and name plate (authored overlay), skip button, 10-pull summary grid.

### 12.15 People and Hero detail
```text
People (medium or full)                     Hero detail (full)
+------------------------------------+     +-------------------------------+
| PEOPLE  [Villagers|Heroes|All] [m][M][F]|  | < Kestrel   B  Fire Striker   |
| filter: rank element role | sort: v |     | [full art]   Stats  Might 24  |
| [card][card][card][card]            |     | Stars **--- [Fuse]  [Ascend]  |
| drag a card onto a room to assign   |     | Abilities: passive, 1, 2, 3, U |
+------------------------------------+     | Outfit: battle / casual / work |
                                            | Weapon  Twin Cinder Blades     |
                                            +-------------------------------+
```

- **Assign by drag:** drag a roster card onto a room (or onto its worker slot).
- A hero on an expedition shows as "away" with its return time; an injured hero shows a heal timer.
- Residents get a smaller detail card (key stat, perk, tool).

### 12.16 Expedition map
```text
+--------------------------------------------------+
| EXPEDITIONS                 Parties 1/2 [m][M][F]|
|  [ region map, 8 nodes, locked ones greyed ]     |
|  Rootside Camp (danger 110, 1 h)                 |
|  Party:  [hero][hero][hero]   role/element hints |
|  Predicted result: Success                       |
|        [ AUTO SEND ]        [ PLAY RUN ]         |
+--------------------------------------------------+
```

- **Party builder:** three slots with role and element hints and the predicted result tier (Fail, Partial, Success, Great).
- **Active parties** list with timers; **results screen** shows rewards, injuries and XP.
- **PLAY RUN** opens the provisions plan, then the forest map and dungeon (10.4). The **BATTLE** dock button leads straight into Battle Mode.

### 12.17 State and feedback catalogue
Every screen defines these states (copy and art **[TBD]**):

| State | Behaviour |
| --- | --- |
| Empty | Friendly line and one action (for example "No heroes yet: open the Heart") |
| Locked | Shows the exact unlock (research node or Heart rank) |
| Loading | Skeleton cards, never a blank panel |
| Error | One-line reason and a retry or fix action |
| Success | Short celebration, resource fly-to-bar, haptic |
| Alert | Tray entry, minimap tick, optional banner; tap jumps to the floor |

The **mockup page** `UiWireframes/tower_ui_mockup.html` shows these layouts rendered for portrait and landscape and for all three window modes.

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
2. Numbers: validate the section 7.1 drafts in a spreadsheet simulation (costs, yields, timers, storage caps, housing capacity).
3. Validate the Heart rank table (8.4) and research costs (8.2) against income in a spreadsheet simulation; decide the second research queue slot.
4. Per-node effect values in 8.2.1 are draft.
5. Review the drafted 36 heroes and 24 residents in `CharacterPrompts/` (names, designs, abilities), then generate the art.
6. Gacha: banner schedule cadence, Echoes rate, ascension Celestium cost, whether pity is shared across banners, final Sigil income.
7. Rank-function tables per building; SSR buffs per building (only Barn named).
8. Expeditions (section 10): final danger and reward tables, creature element per region, boss list, Echoes drop rates, region passives, rations cost curve, and trimming the existing 20x20 crawl to 10-15 minutes.
9. Carry-over details after a hard fail (Gold, Celestium, building ranks).
10. Threats (section 9): exact breach damage rates, siege wave and reward tables, per-incident ignore penalties, villager critical-wound timer. The guided first incident is now a fire in both the GDD and the code, so no sync is needed.
11. Offline cap hours and incident handling offline.
12. Tutorial reward amounts, step timing and the post-tutorial goal chain.
13. Audio direction (music, SFX) and font selection.
