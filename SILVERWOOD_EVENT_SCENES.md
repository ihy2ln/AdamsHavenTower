# Silverwood traversal event scenes

For Codex image generation. One scene per traversal event in `Assets/Resources/AdamsHaven/Events/traversal.json`. Design context: [BATTLE_MODE_GDD.md](BATTLE_MODE_GDD.md) section 5. Same format as [ASSET_PROMPTS.md](ASSET_PROMPTS.md) and [SILVERWOOD_MAP_PROMPTS.md](SILVERWOOD_MAP_PROMPTS.md). Draft 2026-09-30.

## View: eye level, like the battle backdrop

These are **not** overhead map views. They are eye-level, side-on painted scenes in the same style as `Resources/AdamsHaven/Battle/silverwood_battle_v1.png`: the party's point of view standing in the forest looking at what they found. This matches the battle look, so the player feels the roleplay beat as a moment inside the world, and the hero portrait on the event card sits beside it.

The scene is a banner above the event text and choice buttons. So:

- 16:9 landscape, 1672x941 (same as the map plates), but the **subject sits in the centre two thirds** and the bottom 25% stays calm and simple, because the card text can overlap it;
- one clear focal subject, readable at small phone size;
- **no party members drawn**; the hero portrait is shown separately by the UI. Where an event involves a stranger or creature it is listed below and may appear small or as a silhouette.

## Output spec

| Item | Value |
| --- | --- |
| Format | PNG, 16:9, 1672x941 |
| Folder | `Assets/Resources/AdamsHaven/Events/Art/` |
| File name | `<event id>.png` (for example `spirit_lantern_moths.png`) |
| Count | 42, one per event id below (20 new events first, then the 22 original events) |

## Shared style block (prepend to every prompt)

> Use case: stylized-concept
> Asset type: 16:9 eye-level event scene plate for a Unity 2D roguelite expedition, shown as a banner above story text
> Style: Adams Haven painterly fantasy game art, matching the Silverwood Forest battle backdrop: rich but readable, soft brush texture, ancient Silverwood trees with silver-bark trunks and deep green canopy, cool teal and blue forest light with restrained amber accents. Slightly cinematic, atmospheric depth, mist and floating motes.
> Composition: eye-level view looking into the scene. One clear focal subject in the centre two thirds of the frame. Keep the lower quarter calm and simple (ground, moss, water) so text can sit over it. Strong silhouette against a softer background.
> Constraints: no party members, no heroes, no user interface, no text, no labels, no logos, no cards, no watermark, no map markers. Figures only where the scene says so.

## Scenes

### 1. spirit_lantern_moths (night)
> Scene: A dead iron lantern hangs from a gnarled branch in a dark forest, swarmed by hundreds of pale glowing moths that light the surrounding trunks like tiny moons. Deep blue night, soft glow, drifting dust motes.

### 2. hazard_rope_gorge
> Scene: A frayed rope-and-plank bridge sways over a deep stone gorge with white water far below. Misty cliff walls on both sides, pine roots clinging to the edge, one plank missing. Dramatic depth, the bridge is the focal line.

### 3. find_ranger_cairn
> Scene: A neat cairn of stacked grey stones beside a mossy trail, a weathered hunting knife driven into the top, a faded green ranger's cloth tied around it. Dappled morning light.

### 4. hazard_fever_tree
> Scene: A single gnarled tree with glowing amber sap running down its bark, heat shimmering in the air, a cloud of flies circling the trunk. The surrounding forest is cooler and bluer by contrast.

### 5. find_old_battlefield
> Scene: A quiet forest clearing where rusted helmets and broken spears poke out of the leaf litter, torn banners rotting on leaning spears, low ground mist. Sombre and still, with a shaft of pale light.

### 6. animal_hunters_blind
> Scene: A hidden hunter's blind of woven branches and bark overlooking a deer trail, a short bow and a leather quiver resting on a log inside. Warm early light through ferns. Nobody present.

### 7. spirit_singing_spring
> Scene: A crystal-clear spring bubbling from a mossy rock, ripples forming faint concentric rings of light, ferns and tiny glowing blossoms around it, the water faintly humming with soft light.

### 8. hazard_thorn_maze
> Scene: Walls of black briars woven into narrow corridors and dead ends, thorns as long as fingers, small gaps of light showing one possible way through. Dense, claustrophobic, readable.

### 9. spirit_whispering_well
> Scene: An old dry stone well half-swallowed by roots in a ruin clearing, faint pale wisps rising from its dark mouth like whispering breath. Cold green-grey light, crumbling stone, uneasy mood.

### 10. spirit_well_answer (night)
> Scene: A small pale blue light hovers between distant trees at night, beckoning, with a glimmer of silver coins and ore visible near its base. The foreground is dark roots, the light is the only focus.

### 11. social_lost_child
> Scene: A hollow fallen log in a dim glade, a small child's silhouette crouched inside holding a wooden toy sword, only the eyes and the sword catching light. Slightly eerie, slightly sad. (Child silhouette allowed.)

### 12. animal_carcass
> Scene: A huge dead forest beast lying beside a trail, large claw marks scoring nearby trunks, steam still rising from the body in cold air, one huge track in the mud leading away. Grim but not gory.

### 13. spirit_mossy_idol
> Scene: A small mossy stone idol with a worn grin perched on a tree root, dried flowers and bread offerings in front, wisps of incense-like mist. Quiet, ancient, slightly sinister.

### 14. camp_night_vigil (night)
> Scene: A guttering campfire in a dark glade, embers and sparks, long shadows between the trees, and two faint eyes glinting from the darkness beyond the firelight. No people. Warm firelight against deep blue night.

### 15. social_ferryman
> Scene: A swollen dark river at dusk, a flat wooden ferry tied at a mossy dock, and a cloaked figure holding a long pole, hood hiding the face, one hand extended. Mist on the water. (Ferryman allowed, silhouette.)

### 16. find_fallen_banner
> Scene: A torn banner of a forgotten order, silver and green with a faded crest, hanging from a branch and wrapped around a small iron-banded chest. Shaft of light on it, leaves drifting.

### 17. magic_crystal_chorus
> Scene: A trail between towering glowing blue and violet crystal outcrops, faint rippling sound rings visible in the air between them, small shards floating. Magical, cool, slightly unsettling.

### 18. animal_silver_deer
> Scene: A tall silver deer with softly glowing antlers standing in a misty treeline, watching the viewer, light motes drifting around it. Serene, mythic, framed by dark trunks.

### 19. warden_sign
> Scene: A ring of trees with deep claw-gouged bark in a hushed clearing, the air heavy with mist, and a vast dark shape just barely visible deep between the trunks. Dread and watchfulness. (Dark shape allowed, never fully visible.)

### 20. warden_trail
> Scene: Fresh enormous gouges and flattened undergrowth along a forest path, broken branches, giant paw prints in soft earth, a faint trail of mist leading into the dark. Tension, no creature visible.

## Scenes 21 to 42: the original events

Same style block, view and output spec as above. Save to `Assets/Resources/AdamsHaven/Events/Art/<event id>.png`.

### 21. animal_snared_stag
> Scene: A silver stag with faintly glowing antlers thrashing in a poacher's rope snare among roots, one foreleg bleeding, leaves scattered, soft dappled light. Pathos, no humans.

### 22. hazard_fog_fork
> Scene: A forest trail splitting in two around a huge mossy boulder, both branches vanishing into thick white fog between silver-barked trunks. Quiet, uncertain, balanced composition.

### 23. social_lantern_stranger (night)
> Scene: A hooded cloaked figure with a green lantern standing under dark trees at night, a small cloth of wares laid on a root, green light spilling on the mossy ground. Face hidden in shadow. (Figure allowed, silhouette.)

### 24. hazard_collapsed_bridge
> Scene: A wooden footbridge broken in the middle over a cold fast stream, planks hanging, white water over rocks below, moss and ferns on both banks. Clear gap as focal point.

### 25. spirit_moonwell
> Scene: A still round pool in a ruined stone ring glowing faintly pale blue, faint wisps rising from the water like whispers, silver blossoms on the edge. Cool moonlight, magical, calm.

### 26. social_raider_tracks
> Scene: Muddy trail with fresh boot prints crossing it, a dropped knife stuck in a log, a torn scrap of red cloth on a thorn, the forest quiet and watchful beyond. Tension, no people.

### 27. camp_sleepless_night (night)
> Scene: A low-burning campfire with empty bedrolls around it in a dark glade, the forest black and close, pairs of faint eye-glints deep between the trees, a thin trail of smoke. No people.

### 28. magic_mushroom_ring
> Scene: A perfect circle of pale glowing mushrooms in a mossy clearing, the air inside shimmering with soft light and floating spores, ordinary dark forest outside the ring. Enchanted, slightly eerie.

### 29. find_abandoned_wagon
> Scene: A tipped wooden cart on a forest road with a canvas cover half pulled off, crates and sacks showing underneath, a broken wheel, ropes trailing, no oxen. Overgrown, quiet, a story of a hurried departure.

### 30. animal_howling_pack (night)
> Scene: A moonlit forest at night, many pairs of glowing eyes ringing the view from the dark between the trunks, one lone howling silhouette on a rise against the moon. Threatening, no gore.

### 31. social_hermit_herbalist (day)
> Scene: A sunny clearing with a small thatched hut and a neat herb garden, drying bundles hanging from the eaves, a small kettle on a fire, bees and butterflies. Warm and welcoming. An elderly woman may appear small, seen from behind, tending the plants.

### 32. hazard_bramble_wall
> Scene: A trail blocked by an enormous wall of thick black thorny brambles as wide as wrists, a faint gap of light in the middle, a few torn scraps of cloth snagged on the thorns.

### 33. magic_crystal_echo
> Scene: A cave-like forest hollow lined with tall glowing blue and violet crystals, faint ripples of sound visible in the air between them, small shards floating, soft cold light.

### 34. find_sunken_cache
> Scene: A swampy pool with an iron-banded strongbox visible a foot under dark murky water, reeds around, bubbles rising, faint green light on the surface. Mist and still water.

### 35. spirit_roadside_shrine
> Scene: A small moss-covered stone shrine to old forest spirits at the side of a trail, carved with leaf and antler patterns, a few dried flowers and a candle stub in front of it, soft green light through leaves.

### 36. spirit_lost (night)
> Scene: A small glowing humanoid spirit curled up and crying between huge roots in a dark forest, pale blue light on the bark, drifting motes. Sad and tender. (Spirit allowed, small and simple.)

### 37. spirit_gift
> Scene: A hollow in an ancient tree with a small glowing spirit peeking out and beckoning, warm golden-green light spilling from inside, flowers blooming at the base of the tree. Gentle and hopeful.

### 38. social_smugglers_toll
> Scene: A narrow forest trail blocked by a rope barrier and a crude barricade of crates and barrels, a lantern on a post, a few armed silhouettes just visible behind it in shadow. Tension, no faces. (Silhouettes allowed.)

### 39. hazard_cave_draft
> Scene: A dark cave crack in a rock wall with faint daylight and drifting leaves and dust flowing out of it, a cold mist along the floor, a thin sliver of sky glimpsed beyond. Mysterious, hopeful.

### 40. hazard_downpour
> Scene: A dense forest in a heavy hazard_downpour, rain hammering through the canopy in silver streaks, puddles rippling, leaves bowed, a dim grey-blue light with a faint warm glow far away. Atmospheric.

### 41. camp_quiet_glade
> Scene: A hushed sunlit glade surrounded by tall silver-barked trees, still and silent, golden light shafts and drifting pollen, a ring of soft grass. Every shadow beyond the glade is dark. Calm before something. (Rest scene for the pre-boss pause.)

### 42. threat_ambush
> Scene: A forest path with branches creaking and many dark shapes moving between the trunks, glimpses of eyes, claws and weapons in shadow, mist, an urgent red-tinted light low on the horizon. Danger closing in. (Shapes in silhouette only.)

## Notes for Codex

1. Generate with the shared style block first. Keep the same palette as `silverwood_battle_v1.png` so scenes feel like part of the same world.
2. Save to `Assets/Resources/AdamsHaven/Events/Art/<event id>.png`. Do not edit Unity scripts or existing plates.
3. If any plate contains text, a hero, a UI element or a map marker, regenerate it.
4. Scenes 21 to 42 cover the original 22 events (including `camp_quiet_glade` and `threat_ambush`). Same style block.
