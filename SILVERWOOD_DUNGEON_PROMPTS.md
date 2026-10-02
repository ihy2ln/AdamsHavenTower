# Silverwood dungeon art prompts

For Codex image generation. Design context: [BATTLE_MODE_GDD.md](BATTLE_MODE_GDD.md) section 6 and section 8. Companion docs: [SILVERWOOD_MAP_PROMPTS.md](SILVERWOOD_MAP_PROMPTS.md), [SILVERWOOD_EVENT_SCENES.md](SILVERWOOD_EVENT_SCENES.md). Draft 2026-09-30.

## What already exists

Dungeon art comes in two kinds, both in `Assets/Resources/AdamsHaven/Expedition/`:

| Kind | Folder | Look | Used for |
| --- | --- | --- | --- |
| **Room illustration cards** | `Rooms/<kind>_0..2.png` (3 variants per kind) | Eye-level, moody, atmospheric painted scene. Mostly 1024x576, some tall 473x1024 | The room popup when the party enters a room |
| **Room diorama tiles** | `Dungeon/AnimeV2/<theme>_room.png` (1254x1254, transparent) | Overhead 2.5D cutaway, anime painted, four cardinal openings | The dungeon grid |
| **Theme extras** | `Dungeon/AnimeV2/<theme>_vista.png`, `corridor.png`, `backdrop.png`, `mist.png`; `Dungeon/Silverbrook/<theme>_terrain.png` | Vista, corridor, backdrop, fog, ground texture | Dungeon screen backdrop and ground |

Existing themes: briar, keep, crystal, cave, marsh, ruin. Existing room kinds: standard combat, elite combat, boss arena, cave boss, loot, cave loot, safe camp, cave camp, merchant, shrine, puzzle, secret, upgrade, exit.

Reference style: look at `Rooms/standard_combat_0.png` (mossy stone stairs, candles in alcoves, mist) and `Dungeon/AnimeV2/ruin_room.png` (moon-sigil ruin diorama) before generating, and read `Dungeon/AnimeV2/provenance.json` for the exact wording used for the diorama prompts.

## What is new and needed

1. **Three new themes** for Silverwood depths: `mine` (mountain mining ruins), `blight` (corrupted ground), `heartwood` (deep magical forest). Each needs a room diorama, vista and terrain texture. Reuse the existing `corridor`, `backdrop` and `mist` unless a theme looks wrong with them.
2. **New room illustration kinds** for GDD room kinds that have no art yet: `trap`, `skill_check`, `story`, `key_gate`, `rest_alcove`. Three variants each.
3. **Five signature boss rooms**, one per depth (boss room cards, `Rooms/boss_d1..d5.png`).
4. **Mega-dungeon set pieces**: an entrance card and a floor-transition card.

## Output spec

| Item | Value |
| --- | --- |
| Diorama tiles | 1254x1254 or 2048x2048 PNG, transparent background, same framing as existing `*_room.png` |
| Vistas | 16:9 PNG 1672x941 |
| Terrain | 1024x1024 seamless PNG |
| Room cards | 16:9 PNG 1024x576 or 1672x941, **landscape** (standard_combat shape). Tall portraits only if told. |
| Folders | `Dungeon/AnimeV2/` (dioramas, vistas), `Dungeon/Silverbrook/` (terrain; keep name pattern `<theme>_terrain.png`), `Rooms/` (cards) |
| File names | As given per prompt below |

## Shared style blocks

### Room card block (eye-level)
> Use case: stylized-concept
> Asset type: 16:9 eye-level dungeon room illustration for a Unity 2D roguelite, shown as a popup banner above story text
> Style: Adams Haven painterly fantasy game art, same family as the Silverwood Forest battle backdrop and the existing dungeon room cards: moody, atmospheric depth, soft mist, warm candle or lantern accents against cool teal and blue shadow, moss, silver roots and old stone.
> Composition: looking into the room from the entrance. One clear focal subject in the centre two thirds, calm lower quarter for text overlay, strong silhouette.
> Constraints: no people, no party members, no creatures unless stated, no text, no labels, no UI, no cards, no watermark.

### Diorama block (overhead)
> Use case: stylized-concept
> Asset type: ONE high-fidelity modern anime fantasy RPG dungeon ROOM DIORAMA sprite for Adams Haven
> Camera: nearly overhead orthographic 2.5D cutaway with a little visible thickness on raised perimeter walls, aligned square footprint (NOT diamond isometric), north at top.
> Composition: open square playable centre occupies the central 70 percent; walls, props, ledges, pillars, rocks and foliage around the perimeter; a narrow open passage centred on each of the four edges so runtime corridors can join. All walking surfaces on a flat plane. No tall objects blocking the centre.
> Rendering: exquisite crisp detail, atmospheric bounced light, rich blue-violet shadows, luminous teal Celestium inlays, silver highlights, warm amber light pools. Clearly anime illustrated, clean intentional shapes, polished current-generation JRPG environment fidelity, NOT photorealistic noise, NOT pixel art.
> Output: square, aim 2048x2048, whole room and soft contact shadow isolated on a genuine transparent background, cropped close to the square footprint.
> Constraints: no other rooms, no paths outside the room, no labels, no UI, no grids, no characters, no badges or numbers.

## 1. New theme sets

Each theme gets a diorama, a vista and a terrain texture.

### mine (mountain mining ruins, depth 3)
- **`mine_room.png`** (diorama block) Scene: an abandoned mine chamber carved into blue-grey rock, timber props and rusted rails crossing the floor, ore carts, a collapsed side tunnel, hanging lanterns, glints of crystal ore in the walls, a winch frame. Cold blue light with warm lantern pools.
- **`mine_vista.png`** (16:9, eye-level, room card block) Scene: a long timber-braced mine gallery receding into darkness, rails on the floor, lanterns spaced along the roof, mist at the far end.
- **`mine_terrain.png`** Scene: seamless top-down texture of dark slate, gravel, old rail timbers and scattered ore flecks. Muted, low contrast.

### blight (corrupted ground, depth 4 and 5)
- **`blight_room.png`** (diorama block) Scene: a room of cracked grey stone with violet corruption seams, dead black thorn vines clawing up the walls, glowing violet spore pools, broken statues, ash drifts. The four openings are overgrown but clear. Menacing but readable.
- **`blight_vista.png`** (room card block) Scene: a hall of dead grey trees growing through ruined masonry, violet light seeping from cracks in the floor, drifting ash, a faint sickly glow in the distance.
- **`blight_terrain.png`** Scene: seamless top-down texture of cracked ash-grey earth with thin violet corruption veins and dead leaves.

### heartwood (deep magical forest, depth 5)
- **`heartwood_room.png`** (diorama block) Scene: a living-wood chamber whose walls are enormous silver-barked roots forming arches and benches, a stone rune circle in the floor glowing soft gold and teal, drifting pollen and fireflies, shafts of golden light from above. Calm, awe-inspiring.
- **`heartwood_vista.png`** (room card block) Scene: a colonnade of colossal silver trunks like a cathedral, golden light shafts, floating spirit lights, a distant glowing gate of living wood.
- **`heartwood_terrain.png`** Scene: seamless top-down texture of soft moss, silver root fibres, and fallen golden leaves.

## 2. New room illustration cards (3 variants each)

Generate **three different variants** per prompt (`_0`, `_1`, `_2`), varying theme (forest, ruin, cave) and lighting.

### trap → `Rooms/trap_0..2.png`
> Scene: A narrow stone hall with a pressure plate in the floor, thin tripwires catching the light, dart holes in the walls, a half-hidden pit with a rusted spike at its edge, a skeleton hand grasping from the shadow. Tense, watchful, one clear hazard as focus. Candlelight and mist. (Skeleton remains allowed; no living creatures.)

### skill_check → `Rooms/skill_check_0..2.png`
> Scene: A room with a mechanism that needs wit or strength: a rotating stone ring door with carved symbols, a lever and chain mechanism with a jammed gear, or a rope bridge with a broken winch. Sunbeam on the mechanism, quiet dust. Clear, readable puzzle focus. (Distinct from the existing `puzzle` art.)

### story → `Rooms/story_0..2.png`
> Scene: A quiet chamber holding a narrative tableau: a half-collapsed library shelf with a single open book and a candle, an abandoned campsite with a bedroll and a cold cup, or a carved mural wall telling a story. Warm single light, scattered papers, a sense of someone having just left.

### key_gate → `Rooms/key_gate_0..2.png`
> Scene: A massive sealed gate of iron and silver bark with a large ornate keyhole and a faint glow around it, flanked by carved guardians, roots grown across the frame. The gate is the focus, the lower quarter calm stone floor.

### rest_alcove → `Rooms/rest_alcove_0..2.png`
> Scene: A hidden, sheltered alcove with a small fire ring, bedrolls pushed against the wall, a kettle, a lantern, herbs hanging to dry, and a soft warm glow in a cool dark room. Safe, restful. (No people.)

## 3. Signature boss rooms (one per depth)

Boss room cards, `Rooms/boss_d1.png` to `boss_d5.png`. Names are working titles and match GDD section 15's "one signature boss per depth". Mechanics are still to be designed, so these are only atmosphere. Each card shows the arena; the boss silhouette is allowed but never fully visible.

| File | Depth | Working title | Scene |
| --- | --- | --- | --- |
| `boss_d1.png` | 1 Forest Edge | The Rootbound Stag | A vast thorn-and-root arena clearing, a ring of antlered trophies, a huge crooked tree with antler-like branches at the far end, a dark antlered silhouette in mist. |
| `boss_d2.png` | 2 Lakes and Marsh | The Mire Queen | A flooded temple hall, black water over broken mosaic floors, bald-cypress pillars, a throne of coral-like roots, green-blue glow beneath the surface. |
| `boss_d3.png` | 3 Mountains and Caves | The Crystal Colossus | A huge cave cavern of luminous crystal pillars, a titanic dormant stone-and-crystal figure half-embedded in the far wall, violet light. |
| `boss_d4.png` | 4 Ancient Ruins | The Hollow Marshal | A ruined throne room of an old citadel under blighted roots, a tall armoured silhouette standing before a shattered throne, ash and violet embers. |
| `boss_d5.png` | 5 Heart of Silverwood | The Heartwood Warden | A cathedral of colossal silver trunks around a glowing spring, a vast shadowy shape of bark, antler and light behind the mist, shafts of gold light. |

All five use the room card block, 16:9, landscape.

## 4. Mega-dungeon set pieces

Used by the large multi-floor dungeon described in GDD section 8.

### megadungeon_entrance → `Rooms/megadungeon_entrance.png`
> Scene: A colossal carved cave maw in a cliff face, steps descending into darkness, silver roots wrapped around carved stone teeth, faint lantern glow within, mist pooling on the stairs. Awe and dread.

### megadungeon_descent → `Rooms/megadungeon_descent.png`
> Scene: A huge spiral staircase or vertical shaft descending through layers of old stone and roots, ladders and platforms, glowing teal at the bottom, drifting dust in shafts of light.

## Notes for Codex

1. Read the existing `provenance.json` and the reference images first so new themes stay consistent with the six themes already in use.
2. Do not overwrite existing files. Do not edit Unity scripts.
3. For dioramas: the **four cardinal openings must stay clear** so runtime corridors can join. Reject any plate with a blocked opening or a tall object in the centre.
4. If a plate contains text, a hero or any UI element, regenerate it.
5. Tell me which kinds you made so I can add code to load them (`TowerExpeditionUi.RoomArt` currently only knows the existing kinds and `TowerDungeonIllustration` only the six existing themes).

## Redo: blight_room and heartwood_room (2026-10-01)

The first `blight_room.png` and `heartwood_room.png` copied the ruin tile (same moon sigil, same pillars and candle layout, only recoloured). Regenerate both. **Do not use `ruin_room.png`, `provenance.json` or any existing room tile as a reference image.** Use only the text below and the diorama block above. Keep the four cardinal openings, the square footprint, the overhead 2.5D cutaway and the transparent background.

### blight_room.png (replace)
> Scene: A cracked, sunken chamber of dark grey stone overrun by corruption. No moon sigil and no ornate floor medallion. The floor is broken slabs split by glowing violet fissures that run from a central ragged crater of seething violet energy. Dead black thorn trees and twisted bramble walls claw up the perimeter, with shattered statues and toppled columns half swallowed by thorns. Pools of violet spores, drifting ash, a few corroded iron braziers burning violet flame. Colour: charcoal grey, deep violet and sickly magenta glow, small amber accents. Menacing but readable, the centre stays clear and walkable around the crater.

### heartwood_room.png (replace)
> Scene: A living-wood chamber grown rather than built. No stone columns, no moon sigil, no carved medallion. The floor is smooth pale wood and moss laid in concentric growth rings around a small glowing spring in the centre. Enormous silver-barked roots curve up to form the walls, arches and benches, with hollowed alcoves holding glowing golden lanterns of sap. Golden light shafts fall from above, drifting pollen, fireflies and soft white blossoms. Colour: silver, warm gold, deep forest green and soft teal water. Calm and awe-inspiring, centre clear and walkable.

### Check before returning
1. The tiles must look clearly different from `ruin_room.png` and from each other.
2. No tall object blocks the centre, and all four edge openings are clear.
3. Remove any text, UI or characters.
4. Overwrite `Dungeon/AnimeV2/blight_room.png` and `heartwood_room.png`. Leave the vistas and terrain files as they are unless they also look like copies of an existing theme.
