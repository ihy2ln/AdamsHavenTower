# Shared style and rules for all 60 character prompt packs

Applies to the 36 heroes and 24 residents. Every per-character file builds on these rules.

## Hard rules

- **Every character is 21+.** The age is stated in each sheet and each prompt. Long-lived races are written as apparent adults. Styling is tasteful, confident, fully opaque coverage and non-explicit, matching `ADAMS_HAVEN_CHARACTER_BRIEF_TEMPLATE.md`. If an image tool refuses a prompt, tone the costume down rather than routing it through another generator.
- **No text in images.** Names, stats, quotes, card frames and numbers are authored overlays. Reserve calm space where noted.
- **Identity first.** Generate step 1 first. Attach it to every other step (each prompt already carries the instruction to copy identity only, not pose or background).
- **Transparent assets** (chibi, weapon, tool) must have real alpha, no ground, no shadow, nothing cropped.
- **Rig-friendly chibi.** Relaxed A-stance, arms about 25 degrees from the torso; hair, tails, wings, capes and jewelry clear of shoulder, elbow, hip and knee joints. Wings and long capes are drawn as separate ribbons/layers that never cover the joints.
- Log each accepted output (prompt, reference file, canvas, status) in a `PROMPTS.md` next to the images, as in the `adams-haven-layered-character-cards` skill.

## Master style line (starts every prompt)

> High-end Korean fantasy RPG and gacha illustration, polished hand-painted anime rendering, crisp clean linework, rich dimensional cel shading, luminous material highlights, cohesive Adams Haven world palette of dark weathered timber, slate and stone with moonlit blue-silver and violet Celestium accents.

## Negative block (ends every prompt)

> No text, no letters, no numbers, no watermark, no logo, no frame or border, no UI, no extra or missing limbs, no fused fingers, no cropped limbs, no photorealism, no 3D render look.

## The 3 sizes: chest, waist, hips

Every character has a measurement triple (chest or bust / waist / hips, in cm and inches) plus height and weight, listed in the character sheet, in `roster.csv`, and stated in the build line of every prompt. A silhouette label (hourglass, pear-shaped, inverted-triangle, straight and sturdy, athletic) is computed from the triple. The measurements are body-shape data for consistent proportions, not captions: they are never rendered as text.

Height class (Small, Medium, Large) is a separate, secondary field used for the chibi head count and the Unity scale:

| Height class | Height range | Chibi height | Unity chibi scale |
| --- | --- | --- | --- |
| Small | about 118 to 158 cm | about 3 heads tall | 0.85 |
| Medium | about 165 to 182 cm | about 3.5 heads tall | 1.00 |
| Large | about 185 to 230 cm | about 4 heads tall | 1.20 |

## Rank design language (costume complexity by rank)

| Rank | Costume and FX language |
| --- | --- |
| F | Secondhand, patched, hand-mended clothing and gear; matte cloth, wood, rope and plain iron; no polished metal, no ornament, no glow. FX: tiny subtle effects. |
| E | Tidy, honest everyday gear; neat stitching, simple leather, a single small decorative touch; still no glow. FX: small subtle effects. |
| D | Competent working gear with small brass and steel fittings, clean cut, one signature color accent; faint element glow only on the weapon. FX: modest effects. |
| C | Proper armor pieces and heraldic cloth, matching set, confident tailoring; clear element accents and a soft weapon glow. FX: clear effects. |
| B | Refined tailoring with enamel-colored accents, layered fabrics; first hairline threads of blue-silver Celestium in the trim; noticeable element aura. FX: strong effects. |
| A | Elegant layered design with luminous accents; visible Celestium filigree; strong element FX and a graceful silhouette. FX: vivid sweeping effects. |
| S | Silver-trimmed masterwork with ornate engraving; confident heroic silhouette; polished materials; vivid FX that frame the figure. FX: dramatic sweeping effects. |
| SS | Silver and violet refinement on every hem and plate; floating motifs; elaborate layered costume; striking aura and particle trails. FX: elaborate layered effects with floating motifs. |
| SSR | Mythic silhouette with contained Celestium crystals orbiting or embedded in the costume; grand aura, ornate details everywhere, dramatic lighting. FX: epic screen-filling effects with contained Celestium crystal light. |

## Element visual language

| Element | Color cues | FX motifs |
| --- | --- | --- |
| Fire | ember red, orange, gold | embers, flame ribbons, heat shimmer |
| Wind | teal, pale green, silver | spiraling leaves, gust lines, floating feathers |
| Earth | amber, moss green, slate | stone shards, crystals, drifting dust and petals |
| Lightning | electric yellow, cyan, indigo | branching arcs, static sparks, crackling halos |
| Water | aqua, deep blue, pearl | ribbons of water, bubbles, droplets, moonlit spray |
| Light | warm gold, white, soft rose | sun motes, feather-like light, halo rings |
| Dark | violet, black, magenta | shadow smoke, star specks, dark sigils and ribbons |

## Role pose cues

| Role | Pose and expression |
| --- | --- |
| Tank | planted, wide protective stance, shield or body forward, calm determined expression |
| Striker | dynamic mid-action attack pose with forward momentum, intense focused expression |
| Support | open graceful stance, one hand extended toward allies, warm encouraging expression |
| Controller | poised casting stance with the focus item raised, one hand gesturing, sly confident expression |

## Asset list per hero (14 images)

1 full-body card art · 2 casual full body · 3 chibi battle · 4 chibi tower-work · 5 chibi casual · 6 weapon solo · 7 to 11 ability cards (passive, 3 abilities, ultimate) · 12 ultimate cut-in · 13 expression sheet (8 faces) · 14 model sheet and height chart.

## Asset list per resident (8 images)

1 full-body work portrait · 2 casual full body · 3 chibi tower-work · 4 chibi casual · 5 tool solo · 6 resident card art · 7 expression sheet (5 satisfaction faces) · 8 model sheet and height chart.
