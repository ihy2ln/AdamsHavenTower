# Adams Haven Card Game: Tower Mode GDD

**Status:** canonical design document for Tower Mode (v1 scope plus DLC roadmap).
**Supersedes:** `TOWER_LIFE.md` (as design authority; it remains a build log), the Godot `GDD.md` Tower Tycoon sections, `Tycoon-Tower.md`, `Tycoon-Tower-Handoff.md`.
**Decisions dated:** 2026-09-30; revised 2026-10-03 (TT 10.30.0: RimWorld depth is core, districts, outposts, floor caps, auto expeditions, sieges). Anything marked **[TBD]** is an open balance or content item, listed again in section 16.

---

## 1. Vision and pillars

**Elevator pitch:** You are pulled into the world of Adams Haven and handed a dying crystal, the Celestium Heart, at the edge of Silverwood Forest. Grow a tower town around it: build rooms, staff them with villagers and summoned heroes, defend against the forest, and dig toward the Celestium beneath. Fallout Shelter's loop, dressed in Adams Haven's dark-timber and moonlit-crystal fantasy.

**Pillars** (revised 2026-10-03, owner: "Fallout Shelter x RimWorld is the main foundation")
1. **Fallout Shelter x RimWorld.** Fallout Shelter's loop on the surface: rooms produce, dwellers staff by stat, rush, incidents, expeditions, lunchbox-style summons. RimWorld underneath: people with traits and backstories, moods built from named thoughts, breaks, bonds and families, a storyteller, work priorities. If a choice is between depth and clarity, keep the depth and make it readable.
2. **The Heart is the game.** Research, upgrades and summoning all live at the Heart. It is the tower's centre, its progression gate and its lose condition.
3. **Every upgrade is visible.** A building grows from one bay to three and gains new furniture at every rank (F to SSR). The player can read progress off the tower.
4. **Manage the town, not just the rooms.** Cities: Skylines-style tools sit on top: districts zone floor bands with a specialisation and policies, service coverage and appeal make placement matter, and outposts in conquered regions extend the economy beyond the tower (sections 17 and 18).
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

- **Layout (revised 2026-10-01):** side-view cutaway. The **Heart shaft** (cell 22) stands in the middle, with a wing on **each side** (up to 10 cells west and 10 east per floor). The ground floor has **two Celestium Gates**, one just beyond each end; a Gate steps outward when its side is extended. On the ground floor a side may run at most one cell ahead of the other, so the Heart is always within half a cell of the midpoint between the Gates. Upper floors have no Gates and grow either way.
- **Bay growth:** west-side buildings grow new bays to the left, east-side buildings to the right, always away from the Heart. Raiders can arrive at either Gate; both Gates take guards (2 each, 3 with DEF-3).
- **Digging:** digging down costs Gold and time and moves through strata: dirt, soft rock, hard rock, Celestium-bearing rock, bedrock, full Celestium (the old Godot ladder reaches floor -18; final depth **[TBD]**). Deeper strata yield more Celestium through the Stone Quarry.
- **Cells:** one building occupies one or more contiguous cells on a floor. A building cannot straddle the Heart shaft.
- **Floor constraints (from catalog):** Farmstead requires a sunlit (above-ground) floor. Stone Quarry requires an underground floor.
- **Move everything:** any placed building can be moved. Nothing is permanently stuck. **Built 2026-10-03:** the room card's MOVE button (then tap the new place) keeps the room's rank, workers, residents, progress and condition, for a fee of 20 + 10 x width x rank gold; the new place follows the build rules (founded, against the shaft or another room, ground-only / underground-only). DEMOLISH (tap twice) refunds 40% of the catalogue price plus a quarter of the upgrade gold, sends everyone inside home and rehouses the homeless. **Long-press (built 2026-10-03, TT 10.30.1):** press and hold a room for 0.45 s without panning to pick it up the same way; a ring closes in over the room from 0.15 s.
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

### 6.3 Needs, mood and the colony layer (core, revised 2026-10-03)
RimWorld's colony depth is part of the core game, not DLC. All of it runs in the simulation today (`TowerLife.cs`, `TowerColony.cs`, `TowerColonyDepth.cs`).
- **Needs:** Food, Water, Rest and **Joy**, plus injury and illness. Residents **walk to venues** to eat, drink and unwind (Kitchen, Frosted Mug, Well, Market, Guild Hall, Deck Hall; section 19.1). Without an open venue, and in offline catch-up, they eat and drink from tower stock on their own as before.
- **Mood** = 50 + named **thoughts** (hunger, home quality, amenities, friends and rivals nearby, grief, danger, district life, inspiration...). The MOOD tab lists the strongest. **Satisfaction** shows the average on five faces (angry, frown, blank, smiley, ecstatic at 0-19, 20-39, 40-59, 60-79, 80-100) and scales output from x0.65 to x1.15.
- **Traits (10)** shape work speed, rush odds, incident response, appetite, sociability and how a resident breaks. **Newcomers** (Gate recruits and children who grow up) carry **two traits** and a **backstory** (10, for example Hedge knight, Herbalist, Fallen noble): small stat bonuses and at most one job they will not do (never production). Founders, summons and checkpoint residents keep their single trait.
- **Mood breaks:** under 18 mood for 25 seconds starts a 40-second break by temperament: sulk, food binge, tantrum (damages the workplace) or wander.
- **Leaving:** a villager who walked in through the Gate (or grew up here) and stays miserable (under 25 mood, or breaking) for 1,080 live seconds walks out of the Gate; a warning comes at half-way. Heroes, Celestium Bodies and summoned named residents never leave; nobody leaves offline.
- **Inspirations:** 720 live seconds at 85+ mood gives a 240-second inspiration by temperament: work frenzy (x1.5 production), inspired to heal (x1.5 care) or inspired to guard (x1.5 defence).
- **Bonds and families:** residents who share a room build opinion; friends and rivals change mood, rivals can come to blows, close friends can fall in love and start a family. Hero traits also drive expedition event, trap and skill checks (ruling 2026-10-02).
- **Work priorities:** six jobs (production, haul, repair, fire, care, defence), each 0 to 3. The **WORK GRID** (PEOPLE hold menu) is RimWorld's Work tab: every adult against every job on one screen.
- **Schedules / day-night:** day, night or flexible. Gate recruits keep day or night hours (Night Owls work nights); founders, summons and checkpoint villagers stay flexible.

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

**Banners as built (TT 10.3.1, `TowerBanners.cs`; owner rulings 2026-10-03):**
- **Standard** keeps today's pool: **60% heroes, 40% residents** (not heroes only). It is the original `Summon(count)`, free first summon included.
- **SSR pity is shared** by Standard, Featured and Pick (`summonPity`, soft pity from 50, hard at 60). The Resident banner keeps its own counter (`residentPity`).
- **Featured** rotates **weekly** (Monday, local calendar), automatically: this week's SSR and SS hero step through their four-hero pools at different strides, so every hero takes a turn. The banner shows the pair and the end date. One "missed" flag covers both ranks.
- **Pick-Your-Hero:** any SS or SSR hero, changeable at any time; a pending guarantee **carries over** to the new target. Only pulls at the target's own rank roll for it (an SSR pull while aiming at an SS hero is a normal SSR).
- **Resident:** 5 / 50 Sigils; F to SS keep the Standard weights scaled to 99%, SSR is a flat 1% (no ramp); the 20th pull since the last A+ is A or better (A, S, SS, SSR by weight). The 10-pull B+ floor applies on every banner.
- **HUD:** banner tabs inside the SUMMON tab, a per-banner info line (featured pair, Pick target chooser with element and role, guarantee chip), the rates screen per banner, costs on the x1 / x10 buttons, and the pity bar for that banner.
- **Income (built, `TowerSigils.cs`):** about **30 free Sigils per real day**, so an active player reaches hard pity (600 Sigils) about every 20 days and sees about 4 SSR by day 80 (plus lucky natural SSRs).
  - Goals: 2 to 4 Sigils each (about 8 a day).
  - Daily board: dealt each local calendar day; "Visit the Tower" plus 3 tasks picked by date (harvests, rush, build, level up, expedition once a Guild exists), 3 Sigils each, +4 for clearing the board (16 a day). Unclaimed tasks expire at midnight; a fallen Heart keeps the same day's board.
  - Expeditions: a return that won a fight pays 2 + 1.5 x region reward (4 at Brook Edge, about 10 in the deep Silverwood), first 2 returns per day only; wiped parties pay nothing. First conquest of a region: +10.
  - Heart rank-ups: 10, 15, 20, 25, 30, 40, 50, 60 Sigils for ranks E to SSR (250 in all).
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
| 2 (T1) | Argent Market + Cottage | Cozy lighting (+satisfaction) | Sharper tools (+3% yield, all rooms) | Pest bait (pests -20%) | Pack mules (+20% expedition gold) |
| 3 (T2) | Barn + Grain Silo + Frosted Mug | Better beds (+10% housing) | Seasoned timber (+5% Firewood) | Gate guard post (3rd guard) | Sigil focus (+10% Sigil drops) |
| 4 (T2) | Stone Quarry + Warehouse | Apprenticeships (+XP) | Deep wells (+5% Water) | Field medicine (faster healing) | Regions 4-5 |
| 5 (T3) | The Forge + Deck Hall | Nursery births | Pack storage (+15% caps) | Wardstones (Heart warning stage delay) | Earlier soft pity (about pull 45) |
| 6 (T3) | Hearth Nursery + Terrace Row + Ashgrove Manor | Festival (satisfaction event) | Trade ledgers (+8% Gold) | Tonic still (+Tonic output) | Regions 6-7 |
| 7 (T4) | Celestium fittings (unlocks SSR upgrades) | Hero housing (heroes recover faster) | Cycle shortening (-8% cycle time) | Hero bulwark (defence heroes stronger) | Fusion boon (cheaper fusion) |
| 8 (T5) | Master builders (-10% upgrade Gold) | Steward Mk II (smarter auto-assign) | Celestium sieve (+Celestium from Quarry) | Celestial aegis (monster damage -30%, Heart regeneration) | Region 8 (Silverwood Gate) + SSR odds +0.5% |

Built (2026-10-01, `TowerResearch.cs`, Heart hub RESEARCH tab): all 40 nodes with real-time timers, Tonic rush (1 per 30 min left), branch order and the Heart research tier. Construction nodes replace the old per-building blueprint purchases. Regions 2-8 need their EXP node as well as the conquest. Small first versions where the full system does not exist yet: Wardstones move the warning stages to 50% / 20%, Celestial aegis heals the Heart 0.5 HP/s while its chamber is calm, Steward Mk II auto-places idle residents, Fusion boon gives duplicates one extra level, Festival runs every second game day, Better beds is +10% per home rounded up. EXP-2 was "second party slot"; the game runs one expedition at a time, so it is Pack mules until parallel parties exist. Older saves are migrated to the nodes matching what they own.

Note: the tutorial (2.1) only uses starting buildings, so it never depends on a Construction unlock. The Construction chain is paced to keep new buildings arriving in step with the Heart table.

### 8.3 Upgrade
Raises the Heart's own rank (F to SSR) using Celestium and materials.

### 8.4 Heart rank gates
Heart rank sets: maximum floors above and below ground, maximum building rank, research tier ceiling, dweller cap.

**Pacing target:** a player reaches **SSR Heart in about 60 to 90 days of play** (5 to 10 minute sessions). Each Heart upgrade costs Gold plus Celestium (Celestium from rank C) on the same 2.2x curve as buildings, with timers about 2x per rank. Table values are draft tuning targets:

| Heart rank | Target day | Floors (up / down) | Max building rank | Research tier | Dweller cap | Districts | Outposts | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F | 0 | 2 / 1 | D | 1 | 8 | 0 | 1 | Tutorial start |
| E | 1 | 3 / 3 | D | 1 | 14 | 1 | 1 | Tutorial ends here; first dig; first district |
| D | 3 | 5 / 5 | C | 2 | 22 | 1 | 2 | Quarry becomes useful |
| C | 7 | 7 / 7 | B | 2 | 32 | 2 | 2 | Celestium starts costing Heart upgrades |
| B | 14 | 9 / 9 | A | 3 | 44 | 2 | 3 | |
| A | 25 | 12 / 12 | S | 3 | 58 | 3 | 4 | |
| S | 40 | 15 / 15 | SS | 4 | 74 | 4 | 5 | |
| SS | 60 | 19 / 19 | SSR | 4 | 92 | 5 | 6 | SSR building upgrades allowed (needs CON-7) |
| SSR | 80 | 24 / 24 | SSR | 5 | 110 | 6 | 8 | Endgame |

**Floors (built 2026-10-03, owner ruling):** the floor limits are stretched to the game world's +24 / -24 so the late tower has room for districts. `OpenFloor` refuses a floor past the Heart's reach and the Floors popup names the rank that opens it; floors already open (older saves, checkpoints) are never taken away. Building ranks, research tiers, districts, outposts and (TT 10.30.2) the dweller cap follow this table in code. The dweller cap limits newcomers only (Gate arrivals, births, summons stepping out of the Heart): a tower already above it keeps everyone and simply stops growing, and the advisor says to raise the Heart when the cap, not the beds, is what turns a wanderer away.

### 8.5 Status
Heart HP, ward status, recent incidents. Heart damage sources are only breaches and unattended fires on its floor (section 9.4, 9.6).

**Heart destroyed = hard fail, the run ends.** Legacy carry-over: **heroes, research and Sigils persist; the tower resets** and the player starts a new run at Silverbrook Edge. To keep this fair on mobile, the Heart has **warning stages** (stable, strained, critical), a prominent alert at each stage, and prevention tools (**Tonics, wards, guards, defence heroes**). There is **no rescue or recovery system**: when the Heart falls, the run is over.

**Rogue-lite Legacy (implemented).** A new run always starts hard, from nothing, but each fallen run makes the next start easier, so the game gets kinder over time:
- **Points earned per run** = days survived / 4 + 4 x Heart rank reached + residents / 3 + goals claimed.
- **Legacy rank** = floor(sqrt(total points / 4)), 0 to 10. Points accumulate across every run in the save.
- **Start bonus per rank:** +80 Gold, +25 food, water and firewood (up to the starting stock cap), +1 Tonic, +4 Celestium. Sigils and summon pity carry over unchanged; Heart HP is never boosted.
- The defeat screen shows points earned, the next run's Legacy rank, and what it grants. The Heart status and the menu show the current Legacy rank. Bonus values are draft **[TBD]**; further perks (for example a free first upgrade) can attach to higher ranks.

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
- Every incident raises a tray alert that jumps to the floor.
- **Storytellers (built 2026-10-03, RimWorld):** picked on the founding panel while the Heart is still dormant (NEW GAME and every Legacy restart), changeable any time on the Heart's STATUS tab, and carried into Legacy runs.

| Storyteller | Event gap | Raiders from threat | Double raider weight at 55+ | Positive events | Most active incidents |
| --- | --- | --- | --- | --- | --- |
| Lys the Hearthkeeper (calm) | x1.4 | 35 | no | x1.3 | 2 |
| The Silverbrook Chronicler (balanced, default) | x1 | 35 | yes | x1 | 2 |
| The Briar's Whim (chaotic) | x0.75 | 25 | yes | x1 | 3 |

  Balanced is exactly the pace the Tower always had, except that from Heart rank C one more incident may be active at once (2, then 3; chaotic 3, then 4), as listed above (built TT 10.30.2).
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
- **Built 2026-10-03 (`TowerSiege.cs`), auto-defend only.** When an event is due, threat is 75+ and the siege cooldown is spent, the event becomes a siege warning (600 s; the threat line on the top bar counts down). The first siege can come after 2,160 live seconds; after each, the cooldown is 3.5 game days x the storyteller's pace. Defence = every adult on defence duty: Might x 0.3 + weapon x 2 (x1.5 for Gate guards), plus hero power / 10 for heroes at home. Wave = 8 + 6 x Heart rank + 0.3 x day. Win: 100 gold and 2 Celestium per Heart rank, threat -30, defenders gain XP. Loss: a Last Stand raid at a Gate with 1.5x HP and one more severity. **Battle Mode option (built TT 10.30.1, `TowerSiegeBattle.cs`):** during the warning a banner under the top bar offers DEFEND IN BATTLE. The battle-ready heroes at home (Kaela, Ghislaine, Elara, Helda, Daisy, Clarity: well, not away; strongest first, 3 fight and 3 wait in reserve, at full health with Tower gear and levels) meet an elite wave at battle depth 2 x Heart rank - 1, led by a lair-class boss from Heart rank B. The warning waits while they fight. A win pays like an auto win plus 30 XP per fighter; a loss or retreat opens the Last Stand. Closing the game mid-fight hands the choice back with at least a minute left. Echoes are not built.

### 9.7 Illness and injury
Unchanged rules: a critically wounded villager dies after 240 live seconds untended **[TBD final value]**; heroes are pulled back by the Heart instead of dying. The Care job, Frosted Mug and Tonics treat them.

### 9.8 Offline soft resolve
Incidents that would start offline are handled by guards and idle dwellers with **small capped losses only** (a little room condition, food or gold). **No deaths, no Heart damage, no sieges.** A short event log is shown on return.

---

## 10. Expeditions

Run from the **Silverbrook Adventure Guild** (and the Expeditions dock button). Built on the existing Unity systems: `TowerExpedition.cs` (regions, provisions, Safe Pocket), `TowerOverworldGen.cs` (a generated forest map per run), `TowerDungeon.cs` (20x20 crawl), `TowerGuild.cs`. The played run is specified in [BATTLE_MODE_GDD.md](BATTLE_MODE_GDD.md); its rulings (2026-10-02) win where this section disagrees.

### 10.1 Parties
- **A played run takes up to 6 heroes: 3 fight, 3 wait in reserve** (Battle Mode's field and reserve). Auto expeditions send 3. Villagers never go on expeditions. Heroes come from Sigil summons.
- **Party slots:** 1 at the start, a 2nd from research (EXP-2), a 3rd from the Guild reaching rank A. **As built:** one auto expedition at a time; EXP-2 is Pack mules (+20% expedition gold) until parallel parties exist.
- A hero is either staffing a room or away; sending a hero out empties their room slot (6.4). Injured heroes cannot be sent until healed.

### 10.2 Regions
Thirteen regions in a branching unlock chain (region list from `TowerExpedition.cs`): the eight Silverbrook regions below, then Silverwood depths 1 to 5 beyond the Gate, each opened by conquering the one before.

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
| 9-13 | Silverwood depths 1 to 5 | the previous depth (the Gate for depth 1) | none | **[TBD]** | **[TBD]** |

A region unlocks when the previous region's lair boss is conquered **and** its research node is done. Each region's creatures share its lair family's element (built TT 10.30.2): Brook Edge Earth, Rootside Wind, Ford Water, Moon Shrine Light, Old Bridge Fire, Marsh Water, Watchpost Earth, Wood Gate Lightning, depths 1-5 Water, Water, Earth, Dark, Dark.

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
- **Built 2026-10-03 (`TowerAutoExpedition.cs`, AUTO EXPEDITION from the Guild board or the Expeditions dock button's hold menu).** Any opened region, one to three heroes who are well and at home, 6 food and 6 water each. The run takes real time (like research, offline too). Stats in the simulation are 1 to 10, so power uses rank in place of stars, `stat sum x (1 + 0.08 x rank) x (0.5 + 0.5 x level / 50)`, and danger is rescaled so three founding heroes (all stats 3, about 13 power each at level 1) nearly succeed at Brook Edge and three maxed heroes (about 360) can face the deepest region:

| Region | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9-13 (depths 1-5) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Danger | 45 | 55 | 65 | 80 | 95 | 115 | 135 | 160 | 180, 205, 230, 260, 290 |
| Hours | 0.5 | 1 | 1.5 | 2 | 3 | 4 | 6 | 8 | 9, 10, 11, 12, 12 |

  Rewards x region reward x tier: 80 gold, 12 firewood, 6 wood, 4 stone, a chance of a Tonic, 30 XP per hero; Celestium (1 + region reward) from region 4. 2 Sigils per return that is not a Fail, at most 6 a day. Heroes away on an auto run cannot staff rooms, join a played run or be stationed. Element counters are built (TT 10.30.2): +10% per hero whose element beats the region's (Fire > Wind > Earth > Lightning > Water > Fire; Light and Dark beat each other). The six founding heroes take their element and role from their battle definitions (Ranger counts as Controller).

### 10.4 Personal roguelite run
Reuses the existing flow: **Atlas (region map), plan provisions, a forest map generated fresh for every run (walking costs rations by distance and ground; walked ground becomes road), POI dungeons (20x20, fog of war, room events, stairs, goal room), lair boss conquers the region.** Full rules: BATTLE_MODE_GDD.md sections 3 to 9.
- **Length target: 10 to 15 minutes**, trimmed to about 2 to 3 floors per POI. The run **autosaves on every room**, so the player can quit and resume exactly where they left.
- **Fights use Battle Mode** with the party's 3 heroes. HP and injuries carry through the run; Tonics heal.
- **Rewards:** the haul (Gold, Ore, Essence, Celestium, Tonics) is banked on return, plus Sigils for the return (capped per day). During the run each won fight offers a pick of a card upgrade, a relic or supplies; relics and upgrades last for that run. Target: up to 2x the auto ceiling plus exclusive drops (Echoes from boss clears, rare Sigil caches) **[TBD]**.
- **First conquest of a region** grants a permanent passive (for example +10% auto rewards from that region) **[TBD]**. **It also opens an outpost site there (section 18).**
- **Head home from camp:** the whole haul is banked. **Retreat from anywhere else:** 25% of each find is dropped (the Safe Pocket is never taxed).
- **Wipe:** the player keeps the 2-slot **Safe Pocket** and loses everything else carried; heroes return injured. No hero is ever lost.

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
Free rotation in the Tower. **Expeditions and Battle Mode run in landscape** (ruling 2026-10-02: their UI is laid out for 16:9).

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
Fallout Shelter loop, 21 buildings with F to SSR tiers and bay growth, villagers and heroes, seven-stat room matching, resources above, Heart hub (summon, research, upgrade), fire/pest/raid/siege/illness incidents, auto and roguelite expeditions, goals and daily board, free-rotation UI, AAA juice, summon cinematic.
**The RimWorld colony layer is core (revised 2026-10-03):** traits and backstories, needs and thought-based mood, mood breaks, leaving and inspirations, bonds, families and grief, work priorities with a work grid, schedules, and a choice of three storytellers (6.3, 9.3).
**Skylines-style management is core:** districts with specialisations, policies, service coverage and appeal (17), and outposts in conquered regions (18).

### DLC expansions (content and deeper systems)
1. **Colony Depth II:** ideologies or beliefs, more traits and backstories, relationships beyond pairs, a difficulty slider per storyteller.
2. **Crafting and Research:** workbenches, materials, gear chains.
3. **New regions and biomes:** more of Adams Haven beyond Silverwood, with their outposts.

The old note that traits, mood breaks, bonds, the storyteller and schedules sat "behind flags" was never true of the code: they always ran. They are now design canon too.

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
| Day/night | Godot: presentation; Unity: drives schedules | Schedules are live: recruits keep day or night hours (6.3, 2026-10-03) |
| RimWorld depth | v1 pillar 4: out of v1, DLC | Core (2026-10-03) |
| Floors by Heart rank | GDD 8.4: +10 / -18 at SSR; code: +-24 always | Stretched table to +-24, enforced on opening (8.4) |
| "Outposts" | BATTLE_MODE_GDD 13: towns on the run map | Two things: run-map outposts stay Battle's; Tower outposts are persistent staffed colonies (18) |
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
6. Gacha: Echoes rate, ascension Celestium cost, final Sigil income. **Banners built in TT 10.3.1** (8.1): weekly automatic Featured rotation, shared hero pity, Standard keeps its 60/40 mix, Pick guarantee carries over.
7. Rank-function tables per building; SSR buffs per building (only Barn named).
8. Expeditions (section 10): final danger and reward tables, creature element per region, boss list, Echoes drop rates, region passives, rations cost curve, and trimming the existing 20x20 crawl to 10-15 minutes.
9. Carry-over details after a hard fail (Gold, Celestium, building ranks).
10. Threats (section 9): exact breach damage rates, siege wave and reward tables, per-incident ignore penalties, villager critical-wound timer. The guided first incident is now a fire in both the GDD and the code, so no sync is needed.
11. Offline cap hours and incident handling offline.
12. Tutorial reward amounts, step timing and the post-tutorial goal chain.
13. Audio direction (music, SFX) and font selection.
14. **TT 10.30.0 balance (all draft):** district specialisation strengths (15% / 20% / 20%), policy upkeep, coverage radius (1 + rank / 3 floors), appeal thresholds (50 / 75); outpost base yields, caravan interval (360 s), ambush and raid curves, founding cost (250 x region reward); auto-expedition danger and hours; siege defence and wave formulas; leaving (1,080 s) and inspiration (720 s) timers. Measure in a playthrough.
15. **TT 10.30.0 follow-ups: all closed.** District appeal does **not** lower Threat (owner, 2026-10-03). (Built in TT 10.30.1: the Atlas outpost marker, sieges in Battle Mode, the founding storyteller pick, long-press to move. Built in TT 10.30.2: region elements and counters, the dweller cap by Heart rank, a third active incident from rank C.)
16. **Town (section 19), owner to decide before slices 2-4:** the town is its own view around the Tower (decided 2026-10-03); circle or square boundary, and how fast it grows per Heart rank; can raiders damage town buildings; a closed gold loop or keep the Market minting; wages and rent per resident or per room; who sets prices (player or district policy); do visitors and traders replace or extend the caravan event; can a good visit turn a visitor into a recruit; visible queues at full venues; the cold-rations penalty (-3 today).
17. **Town slice 1 balance (draft):** errands start below 50 and fill to 99; meal 10, drink 15, joy 5 points a second at a venue; joy drains 0.07 a second; free hours 19-22 (day) and 07-10 (night). Checkpoint 10 measured 58.4% to 52.7% of resident time on production (TOWER_BALANCE_REPORT section 7). Measure the Heart pacing again with it on.

---

## 17. Districts (Cities: Skylines layer, built 2026-10-03)

A **district** is a band of consecutive floors the player zones (`TowerDistricts.cs`, BUILD hold menu or the Floors popup: DISTRICTS AND ZONING). Bands never overlap; floors outside every band are unzoned and lose nothing. The Heart allows 0 districts at F, then 1, 1, 2, 2, 3, 4, 5, 6 up to SSR (8.4).

**Specialisation.** Purity = the share of the band's room cells whose kind matches; the bonus scales with it.

| Specialisation | Counts | Bonus at full purity |
| --- | --- | --- |
| Residential | homes | +2 to +6 mood for everyone housed there; +10 appeal |
| Industry | production and storage rooms | +15% production |
| Market | Argent Market, Guild | +20% |
| Arcane | Frosted Mug, Forge, Deck Hall | +20% training and Tonics; +10% care |

**Policies** (per district, gold upkeep per game day, charged as it accrues). When the treasury cannot pay, every policy lapses until gold comes back.

| Policy | Effect | Upkeep |
| --- | --- | --- |
| Rationing | meals 20% smaller; residents here -4 mood | 0 |
| Overtime | production +15%; workers here -3 mood | 30 |
| Curfew | no brawls on these floors; residents here -2 mood | 15 |
| Festival Days | residents here +6 mood | 80 |
| Quiet Hours | rest at home 30% faster; production -5% | 10 |
| Hearth Watch | fire and pest damage here -25% | 25 |
| Open Gate (band holds the ground floor) | wanderers arrive 25% sooner; Threat +4 | 20 |
| Beautification | +25 appeal on these floors | 40 |

**Service coverage** (every floor, zoned or not; bonuses only). A staffed provider covers floors within 1 + rank / 3 of its own; a guarded Gate covers 2 + guards / 2.

| Service | Providers | On covered floors |
| --- | --- | --- |
| Medical | Frosted Mug | care x1.25 |
| Safety | Gate guards | incident response x1.2 |
| Food | Kitchen, Farmstead | meals 10% smaller for residents housed there |
| Leisure | Argent Market, Guild, Deck Hall | +3 mood ("Leisure nearby") |

**Appeal** (0 to 100 per floor): each room scores up to 3 (well kept at 80%+ condition, rank, decorative types: Manor, Terrace Row, Frosted Mug, Market), plus Beautification and Residential purity. Residents housed on a floor at 50+ feel "Pleasant floor" (+2), at 75+ "Lovely district" (+5). The average appeal of the floors people live on speeds Gate arrivals by up to 40%. Appeal does not change Threat (owner, 2026-10-03).

**Info views:** the Districts panel switches the cutaway between VIEW OFF, DISTRICTS (tinted by specialisation, named), COVERAGE (red to green by services reached, with MED / SAFE / FOOD / FUN tags) and APPEAL.

## 18. Outposts (built 2026-10-03)

Conquering a region on a played expedition (clearing its lair) opens an **outpost site** there; the Tower announces it once. Outposts are **staffed colonies** run from the Tower (`TowerOutposts.cs`; OUTPOSTS from the Guild board or the Expeditions hold menu). They are not the run-map "outposts" of BATTLE_MODE_GDD section 13.

- **Founding:** 250 x region reward gold, 15 wood, 15 stone, with a Guild standing. The Heart holds 1, 1, 2, 2, 3, 4, 5, 6, 8 outposts (F to SSR).
- **Staff:** any adult who is well and at home, villagers included (unlike expeditions); Celestium Bodies stay. Stationed residents leave their job slot (their bed stays theirs), are fed by the outpost, slowly heal light wounds, and feel "Frontier pride" (+6) for three game days, then "Homesick" (-8). RECALL brings them home. Places: 2 / 4 / 6 by rank (F-D, C-B, A-SSR); ranks rise like a room's, capped by the Heart.
- **Production per game day** = base x region reward x (1 + 0.5 x (rank - 1)) x staff skill x condition, where staff skill adds 1.0 for each average (5) worker in the region's key stat. The secondary good comes at half rate. Base per worker: 40 food, water or firewood; 6 wood; 4 stone; 3 ore; 2 essence; 60 gold; 0.5 Tonics; 0.4 Celestium.

| Region | Outpost | Makes (secondary) | Key stat |
| --- | --- | --- | --- |
| Brook Edge | Brookside Farm | food (wood) | Grace |
| Rootside | Rootside Logging Camp | firewood (wood) | Might |
| Ford | Fordwatch Mill | water (food) | Sight |
| Moon Shrine | Moonlit Sanctum | essence (Tonics) | Wit |
| Old Bridge | Bridge Tollhouse | stone (gold) | Grit |
| Marsh | Marsh Apothecary | Tonics (food) | Wit |
| Watchpost | Watchpost Mine | ore (gold) | Might |
| Wood Gate | Gatekeep Dig | Celestium (ore) | Grit |
| Wood Edge | Woodcutters' Hamlet | wood (firewood) | Might |
| Lakes | Lakeside Weir | water (essence) | Sight |
| Mountains | Crystal Delve | ore (Celestium) | Grit |
| Ruins | Rootcity Bazaar | gold (essence) | Luck |
| Heart | Heartwood Grove | Celestium (essence) | Wit |

- **Caravans** carry whole units home every 360 game seconds (or on SEND CARAVAN). Ambush chance on the road = 0.05 + threat / 200 - defence / 100, between 2% and 40%; an ambush loses 30 to 60% of the cargo.
- **Threat and raids:** an outpost's threat climbs (6 + 2 x region reward) a game day. From 40, every 360 s there is a threat / 200 chance of a raid: attack = threat x (0.3 + 0.1 x region reward) against defence = 2 x rank + each worker's Might x 0.3 + weapon x 2 (+4 for heroes). A held raid drops threat by 25 and gives XP; a lost one loots 40% of the waiting stock and 20 condition, and, in live play only, wounds the staff (never downs them). Offline catch-up raids cost goods but never wound.
- **Upkeep-free by design:** outposts cost founding and upgrades, and the people they take out of the tower.
- **On the Atlas (built TT 10.30.1):** a region with an outpost shows a camp marker and "(OUTPOST)" on its label, and the region panel reads "CONQUERED  •  OUTPOST: Brookside Farm F, 2 staff" (or "outpost site open" when unfounded).
- **Later:** region passives from first conquest (10.4), and outposts reacting to Gate sieges.

---

## 19. Town (living town sim, slice 1 built 2026-10-03)

The tower is a town, so its people should live in it, not only work in it. The owner wants four pillars: **residents live in the town**, a **street outside the Gates**, a **town economy**, and **visitors and traders**. Slice 1 is built (TT 10.3.1, `TowerTown.cs`); slices 2 to 4 are design only and wait on the questions in section 16 item 16.

### 19.1 Residents live in the town (built)
- **Venues** are rooms that serve the town. Kitchen: meals. Frosted Mug: meals, drinks and leisure. Stone Well: drinks. Argent Market, Guild Hall, Deck Hall: leisure. Seats = the room's capacity (2 per bay + rank - 1). A venue serves while it is lit, at 20%+ condition, free of incidents and, for meals and drinks, while the stores hold food or water. **Self-serve** for now (owner): staffing arrives with wages (19.3).
- **Errands:** below 50 hunger or thirst an adult weighs a trip against their job: need, travel time and crowding. They walk there (the cutaway's shaft slide), are served (meal 10, drink 15 points a second, from the stores at the same food and water per point as before), and walk back. A full venue sends them to the next one; with all full they keep working, and below 35 they eat **cold rations** from the stores (-3 thought). Sleep, incidents and emergencies still come first; an errand is seen through once started.
- **Joy** is the fourth need. It drains 0.07 a second and refills at leisure venues (5 a second x venue quality). **Free hours** (soft): day schedule 19:00-22:00, night schedule 07:00-10:00, when a resident goes out unless something urgent calls. **Flexible** residents (founders, summons, checkpoint villagers) go when joy runs low. Joy shows in the resident detail, not as a bar (owner).
- **Thoughts:** "Ate at the Kitchen" +2, "Had a good meal at the Frosted Mug" +5, "A round at the Frosted Mug" +5, "Browsed the Argent Market" / "Swapped tales at the Guild" / "Card night at the Deck Hall" +4 (each for 240 s), "Well entertained" +3 at joy 75+, "No free time" -5 under 20 (only with a leisure venue built and the Heart at E or above). The global "Tower amenities" bonus stays on top (owner).
- **Safety nets:** a tower with no open venue of a kind eats and drinks from stock exactly as before (no first-hour penalty; joy floors at 50 with no leisure venue). **Offline catch-up keeps the instant meals**, so nobody is stranded mid-trip. `TowerRules.TownErrands = false` turns the whole slice off for balance runs.
- **On screen:** the roster and task line read "Eating at the Kitchen", "Heading to the Stone Well for a drink", "Free time at the Argent Market"; green F / W and gold J glyphs over residents at a venue; the room card shows "Patrons 2/3 • meals". Seated and drinking poses (`AH_sit`, `AH_task_tavern`, `AH_chat`) are a small follow-up in `AdamsHavenPrototype.cs`.

### 19.2 The town around the Tower (design; owner direction 2026-10-03)
- **Its own view** (owner): a TOWN view the player switches to from the Tower, not a strip on the cutaway. The Tower stands at the centre as the landmark; the village, then town, then city **surrounds it in a circle or square** and starts small, growing over time.
- **Camera (recommended):** 3D buildings under an **orthographic camera at an isometric angle** (about 30-35 degrees down, 45 degrees yaw). It gives the isometric look without per-direction 2D art, sorts depth for free, takes the existing 3D chibis as they are, and keeps tiles the same size for taps as the city grows. Optional low perspective camera for close-ups only.
- **Layout (recommended):** a square tile grid with a ring-shaped (circle or octagon) buildable boundary that widens with the Heart rank, like floor caps. Main roads leave from the Tower's two Gates; later rings add a north-south road and walls. Raids come in along the roads.
- **Buildings:** low-poly 3D exteriors with hand-painted textures in the Tower's art style; rank shows as trim, banners and roof material, and the Tower's 1/2/3-bay footprints become 1/2/3-tile lots. Tapping a building opens its existing cutaway art as the interior. Town types: market stalls, an inn, a bathhouse, a fountain plaza, homes, plus town versions of venues.
- **Simulation:** town lots are venues and homes in the same rules (19.1 table, travel, seats); a resident walking from the Tower to the town takes the Gate and a road. Town venues raise Gate-side appeal (section 17).
- **Scaling on mobile:** chunked grid, building LODs, flat pictures for far zoom, instanced repeats.
- **Greybox built (TT 10.3.2, `TowerTownMap.cs`, `TowerTownView.cs`, `TowerHudTown.cs`):** TOWN button in the top-right cluster; the view has its own camera and sun. Ring radius by Heart rank F..SSR: 7, 9, 11, 14, 17, 21, 25, 30, 35 tiles (a circle on the square grid; corners stay wild). The Tower fills tiles -2..2, Gates at x = ±3, a 3x3 square in front of each Gate, roads from both Gates to the ring, the north-south road from rank C, **ring roads every 8 tiles** (never within 2 of the edge) so a city grows in blocks. Owner picked the circle-ish ring (2026-10-03). Angles for comparison: iso 30°, iso 35° (default), perspective 45°. Placeholder blocks for everything; townsfolk capsules pace the roads for residents on errands; tap a tile to inspect; RANK buttons preview the ring at other ranks. SSR fill is about 2,700 placeholder buildings (7k renderers) and still runs in the Editor, but the real city needs the scaling work above.
- **Asset need (owner, 2026-10-03):** real building exteriors must replace the blocks. Plan: one painted exterior per town type and rank band, turned into a low-poly shell with the barn pipeline (`Tools/barn_to_3d.py`, `barn_bake_projection.py`, Blender), 1/2/3-tile footprints, roof and trim by rank.

### 19.3 Town economy (design)
- Resident purses: **wages** by job, **rent** by home, **prices** at venues (`VenuePrice`, 0 today), and a **tax** set per district policy. Every serving already flows through one place (`TickErrand`), so prices and wages plug in there.
- Gold becomes partly circulating instead of only minted by the Market. Venues may need staff to serve once wages exist.

### 19.4 Visitors and traders (design)
- Visible non-resident walkers come in at the Gate, use venues (taking seats), pay prices and leave; appeal and the Open Gate draw more.
- Travelling traders set up for a day with stock to buy (tools, weapons, blueprints, Tonics). Outpost caravans become visible arrivals at the Gate.
- Ties to today's Gate wanderers (`pendingVisitors`), the caravan event and outpost caravans (section 18).
