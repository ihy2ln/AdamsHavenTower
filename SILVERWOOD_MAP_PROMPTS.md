# Silverwood overview map prompts

For Codex image generation. Design context: [BATTLE_MODE_GDD.md](BATTLE_MODE_GDD.md) sections 3, 4 and 8. Same tool and prompt format as [ASSET_PROMPTS.md](ASSET_PROMPTS.md). Draft 2026-09-30.

## What these plates are for

Each plate is the **overview map** of one expedition. The party walks freely across it, leaves a traced trail that becomes a road, and uncovers it from fog. So the art must:

- read as a **real place**, not a board: painted landscape with natural walkable ground;
- contain **no drawn paths, roads, lines, arrows, icons or markers** pointing anywhere (the player's own trail is the only path);
- show **landmarks** clearly (they are always visible in game);
- leave **empty, believable spots** (clearings, shores, ledges, ruin foundations) where Unity places POI props later, so nothing needs to be painted over;
- keep **clear contrast between walkable and blocked ground** (dense trees, deep water, cliffs) so a walkable mask can be painted on top;
- fade well under fog (soft values, no pure black, no huge flat white).

## Output spec

| Item | Value |
| --- | --- |
| Format | PNG, 16:9 landscape, 1672x941 (matches the existing plates) |
| Folder | `Assets/Resources/AdamsHaven/Expedition/Maps/` |
| File name | `silverwood_<depth>_<slug>.png` (for example `silverwood_d1_forest_edge_hamlet.png`) |
| Delivery | Original plate only. Mask, anchors and POI data are authored afterwards in the Unity editor tool. |
| Camera | Elevated three-quarter bird's-eye view, as if looking down at a painted tabletop landscape; horizon not visible |
| Number | 15 plates (3 per depth). Proposal; Codex may return more variants per prompt. |

## Shared style block (prepend to every prompt)

> Use case: stylized-concept
> Asset type: 16:9 overview map plate for a Unity 2D roguelite expedition game
> Style: Adams Haven painterly fantasy game art, matching the Silverwood Forest battle backdrop: rich but readable, soft brush texture, cool teal and blue forest light with restrained warm amber accents, ancient Silverwood trees with silver-bark trunks and deep green canopy.
> Composition: elevated three-quarter bird's-eye view of a large wild landscape; the whole plate is explorable ground. Dense forest is the base everywhere, broken by the specific terrain below. Natural clearings, shores, ledges and ruin foundations spread across the plate as empty spaces for later markers. One camp-sized clearing near the lower edge for the party's entrance.
> Readability: strong value separation between open walkable ground (lighter, warmer, softer) and blocked ground (dense canopy, deep water, cliffs, darker, denser). Soft values suitable for fog overlay. No pure black, no pure white.
> Constraints: no people, no creatures, no text, no labels, no logos, no user interface, no icons, no map markers, no drawn roads or trails or lines, no arrows, no grid, no border, no watermark.

## Depth 1: Forest Edge (Silverbrook side of the wood; easiest; woodland, hamlets)

### d1_forest_edge_hamlet
> Scene: A woodcutters' hamlet and outpost at the forest's edge. A cluster of five or six timber cottages and a small palisaded outpost with a watch platform sit in a large clearing at the upper right as the town landmark. Terraced farm plots and stumps ring the hamlet. Cooler dense pines close in toward the left and bottom. A shallow stream curls through the centre with two natural crossing places.

### d1_forest_edge_fallen_oaks
> Scene: A storm-ravaged grove of colossal fallen oaks whose trunks form natural bridges, walls and hollow tunnels across the plate. A huge toppled tree with a hollow chamber is the landmark at the centre. Mushroom rings, tangled briars and mossy boulders create dense blocked patches. Small sunlit clearings between the logs.

### d1_forest_edge_charcoal_burn
> Scene: An old logging and charcoal-burning district. Scorched clearings, abandoned charcoal kilns, a ruined sawmill with a waterwheel as the landmark, stacked timber and a rusted cart near the middle. Young birch regrowth in lighter ground, old pines around the edge. A slow river runs along the right side with one old log dam.

## Depth 2: Lakes and Marsh (the wet wood; water slows and blocks)

### d2_lakes_mirror_lake
> Scene: A large still silver lake fills the centre, reflecting the canopy; it is deep and blocks travel. A rocky island with a ruined lakeside shrine is the landmark. Shore paths of natural bare ground, reed beds, a wooden fisher's jetty and a small fishing village with stilt houses on the lower left shore. Forest closes in on all other sides.

### d2_lakes_braided_river
> Scene: A wide river splits into several braided channels with sandbars and gravel islands across the plate. Some channels are shallow fords, some deep. A mossy stone bridge, half collapsed, is the landmark in the upper centre. Willows and tall reeds on the banks, dry forest ground on both sides. A beaver-built dam near the right.

### d2_lakes_drowned_marsh
> Scene: A drowned swamp forest: gnarled bald-cypress-like trees standing in dark water, boggy patches of moss and reed, mist hanging low. Firm ground appears as scattered islands and a few raised root causeways. A sunken, half-flooded watchtower is the landmark. Faint blue-green glowing spores in a few spots, lightly corrupted.

## Depth 3: Mountains and Caves (high ground, choke points)

### d3_mountains_switchback_ridge
> Scene: A steep forested ridge with cliff faces, ledges and narrow switchback shelves climbing the plate from bottom left to upper right. A stone border watchtower on a cliff is the landmark. A waterfall drops into a pool at the left. Rocky choke points and wider mossy terraces. Impassable cliffs read clearly as sheer rock.

### d3_mountains_mine_valley
> Scene: A mountain valley with an abandoned mining settlement: ore carts, timber mine entrances in the hillsides, a collapsed ore processing yard, a rusting winch tower as the landmark. Scree slopes and rock walls block travel; the valley floor and side ledges are walkable. Cold blue light, a few warm lantern glows left from old camps.

### d3_mountains_crystal_caverns
> Scene: A forested mountain basin scattered with luminous crystal outcrops in blue and violet. A huge cave mouth in a cliff wall at the top centre is the landmark: the entrance to a large multi-floor dungeon. Crystal clusters, rocky shelves and a small underground-fed pool. Slight magical haze.

## Depth 4: Ancient Ruins (old civilisation under the trees)

### d4_ruins_sunken_city
> Scene: Overgrown remains of an ancient city: broken paved plazas, toppled columns, arches and a grand crumbling stairway, all swallowed by roots and moss. A half-buried colossal statue head is the landmark. Ruined streets are natural open ground; roofless buildings and tight alleys block in places. Warm stone against deep green.

### d4_ruins_moonlit_terraces
> Scene: Terraced garden ruins stepping down a hillside, with a dry fountain, shattered stone staircases and a ring of standing stones. A great moon-pool shrine at the lowest terrace is the landmark. Cool moonlight palette with pale silver flowers in the grass.

### d4_ruins_blighted_citadel
> Scene: A ruined citadel whose grounds are tainted by blight: dead grey trees, cracked ground with violet corruption seams, thorny black vines, drifting ash. The broken keep is the landmark, with a large sealed gate facing the lower edge. The blight spreads in patches, leaving a few clean pockets of ground. Menacing but still readable.

## Depth 5: Heart of Silverwood (the deepest forest; most magical)

### d5_heart_world_tree_grove
> Scene: The heart of the forest: an enormous silver-barked world tree at the centre with a luminous canopy, roots arching like bridges across the plate. Giant roots form walls and natural paths. Glowing pollen and fireflies, a still spring at the base. Awe-inspiring and calm, with restrained golden light.

### d5_heart_hollow_gate_ruins
> Scene: A cathedral-like forest of immense trees whose trunks form colonnades. At the centre stands a huge ancient gate of living wood and stone, the landmark and the way into the final dungeon. Stone rune circles on the ground, dim shafts of light and drifting spirit lights. Shadows are deeper here, but still readable.

### d5_heart_blighted_wild
> Scene: The wild forest hit hardest by corruption: twisted, glowing roots, patches of mutated oversize flowers and fungus, black water pools, and a large seething blight-heart crater with violet light as the landmark. A narrow band of healthy golden forest survives along one edge. Dangerous, vivid and slightly surreal.

## Companion prompts

These are made after the plates exist, so style can match them.

### POI prop sprites (single objects, transparent background)
One sprite per POI kind, each in a normal and a fog-silhouette variant (the silhouette is the same drawing flattened to one cool grey-blue with a soft glow and no detail).

> Use case: stylized-concept
> Asset type: isolated 2D game prop sprite for an overview map, transparent background, three-quarter bird's-eye view, same painterly style and lighting as the Silverwood map plates
> Primary request: [see list]
> Constraints: single object only, centred, clean silhouette, soft contact shadow baked at the base, no ground plane, no text, no people, no UI.

List (one prompt each): small camp tent with lantern; ruined shrine with glowing moon-pool; monster den (hollow log with claw marks); elite lair (bone-ringed thorn thicket); boss lair gate; treasure cache (half-buried chest in roots); travelling merchant wagon; hamlet cottage cluster (town POI); outpost watch platform; mega-dungeon entrance (carved cave maw); blight crater; waystone landmark.

### Trail, road and fog
- **Trail strip:** seamless tileable footprint trail, 128 px high, dark soft brown on transparent, hand-painted look.
- **Worn road strip:** seamless tileable beaten-earth road with a few pebbles, lighter than the trail, on transparent.
- **Fog layer:** seamless 1024x1024 soft misty fog texture, grey-blue, wispy, smooth falloff, on transparent. Variants: cool (default), swamp green, blight violet.

## Hand-off notes for Codex

1. Generate each plate with the shared style block first, then its scene. Keep the same style across all 15 so the set feels like one forest.
2. Save at 1672x941 into `Assets/Resources/AdamsHaven/Expedition/Maps/` using the naming pattern. Do not edit Unity scripts or the existing plates.
3. If a plate shows any drawn path, line, icon or text, regenerate it. These must be absent.
4. Tell me which prompts needed several tries and which landmarks read poorly so I can adjust anchors and the GDD.
5. Anchors, walkable mask and POI data are authored in Unity afterwards (editor tool, GDD section 18). Nothing needs to be marked on the images.
