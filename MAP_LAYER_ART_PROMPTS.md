# Expedition overworld layer art: Codex image prompts

Art for the new **layered expedition map** (plan: hidden square rules grid, painted layers on top, no visible squares). Unity assembles every map from these pieces at runtime:

1. **Ground**: seamless tiles blended together by a splat shader.
2. **Water and crossings**.
3. **Roads**: the player's walked routes, painted with the road tile.
4. **Canopy, undergrowth and rocks**: hundreds of cut-out stamps scattered and depth-sorted.
5. **POI props**: the places the party visits.
6. **Fog**: drifting clouds; smoke and glow "tells" show through it.
7. **Atlas extras**: for the region overview.

Because pieces are mixed freely, **consistency matters more than any single image**: same camera, same light, same palette, same scale. Every prompt below repeats the style and technical rules so it can be pasted on its own. Prompts never name other games.

Draft 2026-10-01. Tool: built-in Codex image generation in the ChatGPT harness. Format follows `UiPrompts/BUTTON_PROMPTS_WUWA_FANTASY.md`.

---

## Codex task (paste this first, then let it work through the list)

```text
You are producing game source art for the Unity project at S:\AI\Game\Unity AHCG\My project.
Read S:\AI\Game\Unity AHCG\My project\MAP_LAYER_ART_PROMPTS.md and generate every asset in it with your built-in image
generation tool, in order, starting with prompt 0.

Rules:
1. Save everything to S:\AI\Game\Game Assets\expedition\overworld_v1\ using exactly the file names in each prompt.
   Create the folder if needed. Do not modify anything else.
2. Reference images:
   - For prompt 0, attach these existing plates as style references:
     S:\AI\Game\Unity AHCG\My project\Assets\Resources\AdamsHaven\Expedition\Maps\silverwood_d1_forest_edge_hamlet.png
     S:\AI\Game\Unity AHCG\My project\Assets\Resources\AdamsHaven\Expedition\Maps\silverwood_d1_forest_edge_fallen_oaks.png
   - Save the strongest prompt 0 result as ow_style_anchor.png and attach it as the reference for every later prompt.
3. After each image, check it with Python (PIL + numpy) and regenerate (up to 3 tries) if a check fails:
   STAMPS and SHEETS (transparent):
   - All four corner pixels have alpha 0. There is no checkerboard, colour fill, ground plane, frame or separator line.
   - The number of separate subjects equals the cell count asked for. Count connected regions with alpha > 40 after a
     5 px dilation so loose leaves join their tree. Subjects need at least 48 px of clear transparent space between them.
   - No subject touches the image border.
   - No cast shadow on the ground. Pixels at alpha < 40 make up less than 8% of the subject's bounding box area.
   SEAMLESS TILES (opaque):
   - Alpha is 255 everywhere.
   - Offset test: roll the image by half its width and half its height. The mean absolute difference across the two
     centre seam lines must stay under 6 (0..255 scale) and show no visible line. Save the rolled preview as
     <name>__offset_check.png.
   - No single feature (stone, flower patch, puddle) is larger than 1/6 of the tile width, so repeats do not show.
   ON-BLACK FX (files that say "on pure black"):
   - Corners are pure black (#000000), with no other background colour. Then convert to alpha:
     alpha = max(R,G,B); RGB = RGB / alpha (un-premultiply; RGB = 0 where alpha = 0).
     Keep the original as <name>_black.png and save the converted image as <name>.png.
   ALL:
   - No text, letters, numbers, UI, icons, people or creatures.
   If the generator ignores the requested pixel size, keep the image and record its real size. Do not resample art to
   fake the size.
4. Then slice the sheets: run
   python "S:\AI\Game\Unity AHCG\My project\Tools\slice_overworld_sheets.py" "S:\AI\Game\Game Assets\expedition\overworld_v1"
   It writes sliced\<sheet>__<name>.png, preview\<sheet>.png and manifest.json. Check that every sheet sliced into the
   expected number of pieces. If a sheet did not, regenerate that sheet.
5. Write README.txt in the output folder: what was generated, the real pixel sizes, which checks passed or failed,
   retries used, and known issues. Also copy this prompt file there as PROMPTS_USED.md.
6. Do not import anything into Unity and do not edit any scripts or scenes.
```

---

## Shared style block (already inside every prompt)

> Adams Haven painterly fantasy game art for a top-down expedition map. Rich but readable, soft visible brush texture, hand-painted rather than photographic. Palette: cool teal and blue-green forest light with restrained warm amber accents; Silverwood trees have silver-white bark and deep green crowns. Light comes from the upper left, soft and slightly golden, with gentle ambient occlusion. Values stay soft, never pure black or pure white, so everything sits well under drifting fog.

## Camera and scale rules (already inside every prompt)

> Camera: elevated three-quarter bird's-eye view looking down at about 60 degrees, as if over a painted tabletop landscape. Trees show their crown from above and a little of the trunk below it; buildings show roof and front face. No horizon, no perspective vanishing.
> Scale: one map cell is 1.5 m of ground. A single mature tree crown is about 3 cells wide. On a 1024 px stamp canvas, a one-tree stamp's crown spans about 35% of the canvas width. A three-tree cluster spans about 80%. A tent spans about 30%. Keep this scale identical across all prompts.
> Stamp anchor: the point where the object meets the ground (trunk base or building footprint centre) sits at horizontal centre, about 10% above the bottom edge. Unity uses that point as the pivot.

---

## 0. Style anchor (reference only, not used in game)

**File:** `ow_style_anchor.png`, 1536x1024, opaque.

> Adams Haven painterly fantasy game art for a top-down expedition map. Rich but readable, soft visible brush texture, hand-painted. Palette: cool teal and blue-green forest light with restrained warm amber accents; Silverwood trees have silver-white bark and deep green crowns. Light from the upper left, soft and slightly golden, gentle ambient occlusion. Soft values, no pure black or white.
> Camera: elevated three-quarter bird's-eye view at about 60 degrees, like a painted tabletop landscape; no horizon.
> Scene: a small vignette of the expedition map showing every layer the game will use. A meadow clearing in the centre with a small explorer's camp (one canvas tent, a campfire with a thin smoke column, a lantern on a post). A worn dirt footpath leads out of the clearing. A shallow stream with a ford of stepping stones crosses the lower right. Dense mixed forest (oak, pine, birch, a few silver-bark Silverwood giants) closes in on the left and top, shown as individual readable crowns rather than a green mass. Mossy boulders and ferns at the forest edge. Thin soft fog drifts across the upper corners.
> Constraints: no people, no creatures, no text, no labels, no UI, no icons, no map markers, no grid, no border, no watermark.

---

## A. Seamless ground tiles (11 images)

All are **1024x1024, opaque, perfectly seamless (tileable on all four sides), straight top-down texture with no height objects**. They must blend into each other, so keep a similar value range and the same light. Each prompt is the shared block plus the line below.

Shared tile wording (prepend to each):
> Adams Haven painterly fantasy game texture, hand-painted with soft visible brush texture, cool teal-green palette with restrained warm accents, soft values (no pure black or white). Perfectly seamless tileable square texture, edges wrap on all four sides. Straight top-down view of flat ground only, evenly lit from the upper left. No objects with height, no trees, no rocks larger than a fist, no shadows of off-screen objects, no single feature bigger than one sixth of the tile, no text, no border.

| File | Content line |
|---|---|
| `ground_meadow.png` | Soft green meadow grass with small clover, a few tiny white and yellow wildflowers, slight colour variation in patches. The open, walkable "clearing" ground. |
| `ground_forest_floor.png` | Shaded forest floor: dark green moss, fallen leaves in muted amber and brown, pine needles, tiny twigs. Slightly darker than meadow. |
| `ground_deep_moss.png` | Very dense deep forest ground: thick dark blue-green moss, roots barely visible, damp, darkest of the greens but still soft (sits under heavy canopy). |
| `ground_dirt_road.png` | Packed earth footpath surface: warm light brown dirt, small pebbles, faint wheel and foot wear, sparse grass tufts. Reads clearly lighter and warmer than the forest grounds. |
| `ground_worn_grass.png` | Trampled grass halfway between meadow and dirt: flattened blades, bare earth showing through in small patches. Used where a new trail starts wearing in. |
| `ground_mud_marsh.png` | Boggy marsh ground: dark wet mud, shallow puddles reflecting a little teal sky, reed stubble, algae green edges. |
| `ground_scree.png` | Rocky mountain ground: grey-blue gravel and small flat stones, thin lichen, a few hardy grass tufts. |
| `ground_ruin_flagstone.png` | Broken ancient flagstones half reclaimed by moss and grass, pale grey stone with faint silver-blue tint, cracks with tiny plants. Stones are no bigger than one sixth of the tile. |
| `ground_blight.png` | Corrupted ground: ashen grey soil, withered grass, thin violet-black veins of blight with a very faint violet glow, a few crystal grains. Unsettling but still soft-valued. |
| `water_shallow.png` | Shallow clear stream water seen from above: visible sandy and pebbly bed through teal water, soft light ripples, gentle caustics. |
| `water_deep.png` | Deep still forest water from above: dark teal-blue, soft surface ripples, faint reflected sky glints, no visible bed. |

---

## B. Tree canopy sheets (6 sheets, 4 stamps each)

Each sheet is **2048x2048, transparent background, a 2x2 grid of four separate tree stamps**, each centred in its own quadrant with wide transparent gutters. Row-major order: top-left, top-right, bottom-left, bottom-right.

Shared stamp wording (prepend to each):
> Adams Haven painterly fantasy game art, hand-painted with soft visible brush texture, cool teal-green palette with restrained warm amber accents, soft values. Camera: elevated three-quarter bird's-eye view at about 60 degrees, crowns seen mostly from above with a little trunk visible underneath. Light from the upper left, soft and slightly golden, gentle ambient occlusion inside the foliage. Isolated cut-out game stamps on a fully transparent background: no ground, no grass under the trunk, no cast shadow, no frame. A 2x2 sheet of four separate stamps, each centred in its own quadrant with wide empty transparent space between them. Scale: a single mature tree crown spans about 35% of one quadrant's width; a three-tree cluster spans about 80%. Each stamp's trunk base sits at the quadrant's horizontal centre, about 10% above the quadrant's bottom edge. No text, no people, no creatures.

| File | Four stamps (row-major) |
|---|---|
| `canopy_oak_sheet.png` | 1) single broad oak, rounded crown; 2) single oak, slightly lopsided, a few amber leaves; 3) two oaks with overlapping crowns; 4) cluster of three oaks with interlocking crowns. Rich mid-green. |
| `canopy_pine_sheet.png` | 1) single tall pine seen from above (layered star-shaped crown); 2) single younger pine; 3) two pines; 4) tight cluster of three pines. Cool deep blue-green. |
| `canopy_birch_sheet.png` | 1) single birch, airy light crown, white bark visible; 2) single birch leaning slightly; 3) two birches; 4) three birches in a loose clump. Light yellow-green, feels like young regrowth. |
| `canopy_silverwood_sheet.png` | 1) single ancient Silverwood giant, very wide crown, silver-white trunk and roots visible, faint pale-blue motes in the leaves; 2) single Silverwood with a mossy split trunk; 3) two Silverwoods; 4) three Silverwoods. Crowns about 1.4x the size of an oak. Deep green with silver highlights. |
| `canopy_willow_sheet.png` | 1) single weeping willow with draping fronds; 2) single willow over a bit of reed; 3) two willows; 4) a cluster of three swamp cypress-like trees with knobby root knees. Blue-green, damp. For lakes and marsh. |
| `canopy_deadwood_sheet.png` | 1) single dead blighted tree with bare grey twisted branches and faint violet veins; 2) single half-dead tree with sparse withered leaves; 3) two dead trees; 4) three twisted blighted trees with a few violet crystal shards at the roots. For corrupted depths. |

---

## C. Undergrowth and regrowth

**`undergrowth_sheet.png`**: 2048x1024, transparent, **4 columns x 2 rows = 8 stamps** (row-major).
> (shared stamp wording, adapted to a 4x2 sheet) Eight low undergrowth stamps, each about 15% to 25% of its cell's width, so they stay small next to trees: 1) round green bush; 2) bush with small white flowers; 3) fern clump; 4) tangled thorny briar patch with tiny red berries; 5) tall reed clump (marsh); 6) cluster of glowing pale-blue mushrooms; 7) fallen mossy log (short, lying horizontally); 8) old tree stump with moss.

**`saplings_sheet.png`**: 2048x1024, transparent, **3 columns x 2 rows = 6 stamps** (row-major). These show regrowth when the forest swallows an old trail.
> (shared stamp wording, adapted to a 3x2 sheet) Six regrowth stages: top row oak, bottom row pine. Left to right: 1) tiny sprout patch with a few leaves; 2) knee-high sapling; 3) young tree about half the size of a mature crown.

---

## D. Rocks and cliffs

**`rocks_sheet.png`**: 2048x1024, transparent, **4 columns x 2 rows = 8 stamps** (row-major).
> (shared stamp wording, adapted to a 4x2 sheet) Eight rock stamps in grey-blue stone with soft moss and lichen, lit from the upper left: 1) single mossy boulder; 2) pair of boulders; 3) flat rock slab; 4) cluster of small rocks; 5) tall jagged rock spire; 6) chunk of cliff edge (wider than tall, top face flat and lit, front face darker); 7) ancient carved standing stone with faint worn runes (no readable letters); 8) rock outcrop with violet crystal shards. Rocks block movement, so they read as solid and heavy.

---

## E. Water crossings

**`ford_stones_sheet.png`**: 2048x1024, transparent, **2 columns x 1 row = 2 stamps**.
> (shared stamp wording, adapted to a 2x1 sheet) 1) a line of five flat stepping stones running left to right, wet tops catching light; 2) a shallow gravel ford with a few larger stones and a little white water foam. Stones only, no water body; the water is painted underneath by the game.

**`bridge_wood.png`**: 2048x1024, transparent, single stamp.
> (shared stamp wording, single centred stamp) A narrow wooden plank footbridge with rope rails, seen from above at three-quarter view, spanning left to right across about 80% of the width. Weathered planks, one plank missing. No water and no banks, only the bridge.

**`bridge_stone.png`**: 2048x1024, transparent, single stamp.
> (shared stamp wording, single centred stamp) An old mossy stone arch bridge, half collapsed on the right side with fallen blocks, spanning left to right across about 80% of the width. Silver-grey stone with moss. No water and no banks, only the bridge.

---

## F. Fog and clouds

**`cloud_tile.png`**: 1024x1024, opaque, **seamless**, on pure black.
> Seamless tileable soft cloud and mist texture, white wisps on pure black (#000000), soft painterly billows with varied density (some thick, some thin, some empty black gaps), no hard edges, no recognisable shapes, evenly distributed so repeats do not show. Used as a fog mask. No text, no border.

**`fog_wisps_sheet.png`**: 2048x2048, on pure black, **2x2 = 4 wisps** (row-major).
> Four separate soft drifting fog wisps, painterly white mist on pure black (#000000), each centred in its own quadrant with black space between them: 1) long thin horizontal streak; 2) rounded puff; 3) torn ragged band; 4) low ground-hugging mist pool. Soft edges fading fully to black.

---

## G. POI props (8 images)

Each is **1024x1024, transparent, a single centred stamp**, using the shared stamp wording (single stamp, ground contact at centre about 10% above the bottom edge). Footprint fits in about 40% of the canvas width, so props sit inside a forest glade. The game derives the dark fog silhouette from these, so no extra version is needed. **Use readable silhouettes**: each kind must be recognisable from its outline alone.

| File | Prop |
|---|---|
| `poi_camp.png` | Explorer's camp: one canvas tent, a ring-stone campfire with small flames, a lantern on a post, a bedroll and a supply crate. Warm amber light from the fire. |
| `poi_lair.png` | The region's lair: a dark cave maw opening in a mound of rock and twisted roots, old bones and a broken banner at the entrance, faint ominous red-violet glow from inside. |
| `poi_merchant.png` | A travelling merchant's covered wagon with an awning stall, crates, hanging lanterns and cloth bundles. No people. |
| `poi_shrine.png` | Small ruined moon shrine: a circle of broken pale stone pillars around a basin of softly glowing silver-blue water. |
| `poi_treasure.png` | Half-buried ruined stone cache under tree roots, an iron-bound chest visible inside, a few scattered gold coins glinting. |
| `poi_mystery.png` | Three leaning standing stones around a ring of pale glowing mushrooms, thin luminous mist curling between them. Uncanny but inviting. |
| `poi_combat.png` | Raider camp: crude wooden barricade of sharpened stakes, a ragged tent, a weapon rack and a smouldering fire pit. No people. |
| `poi_elite.png` | Ruined wooden watch tower with a torn war banner, spiked palisade around its base, braziers burning. Taller than the other props (about 55% of canvas height). |

---

## H. Fog tells (seen through fog before a POI is revealed)

**`tells_sheet.png`**: 2048x2048, on pure black, **2x2 = 4 effects** (row-major).
> Four separate soft light and smoke effects, painterly, on pure black (#000000), each centred in its own quadrant with black space between them, soft edges fading fully to black: 1) a thin rising chimney smoke column, pale grey-white, drifting slightly right; 2) a warm amber campfire glow halo with a few sparks; 3) an ominous red-violet glow with a faint rising haze; 4) a small four-point silver glint sparkle. No objects, light and smoke only.

---

## I. Atlas extras (region overview map)

Each is **1024x1024, transparent, a single centred stamp**, shared stamp wording. These sit on the macro map, where one stamp stands for a whole place.

| File | Prop |
|---|---|
| `atlas_tower_town.png` | Adams Haven: a small walled village of timber houses around a tall slender stone tower with a glowing violet crystal at its top. Reads as "home". |
| `atlas_silverwood_gate.png` | The Silverwood Gate: two colossal moss-covered stone guardian statues facing each other across an ancient archway, giant silver-bark trees behind. |
| `atlas_banner_conquered.png` | A tall planted pole with a long pennant banner in deep teal and gold with an abstract crest (a simple tower shape, no letters), slightly rippling. Marks a conquered region. |

---

## Checklist for the user (after Codex finishes)

- Open `preview/*.png` (dark checkerboard). Every stamp should share one light direction (upper left), one camera angle and one brush feel. Reject outliers and regenerate them with the anchor attached.
- Scale check: put `canopy_oak_sheet__1`, `poi_camp` and `rocks_sheet__1` side by side at the same scale. The tent should be about as wide as one oak crown.
- Seam check: open each `*__offset_check.png`. No line should be visible.
- When it's done, tell Claude the folder is ready. Claude imports the files to `Assets/Resources/AdamsHaven/Expedition/Overworld/` (ground, stamps, props, fx, atlas), sets the import settings (sprite pivot from the manifest, mipmaps, wrap mode Repeat for tiles) and swaps out the placeholder stamps.
