# CODEX TASK: regenerate 3 expedition map images (self-contained)

You are fixing game art for a painterly fantasy top-down expedition map. Use your built-in image generation tool.
This file is the ONLY instruction set. Ignore any other attached file (for example BattleModelRuntimeReport.txt is unrelated).

Folder: `S:\AI\Game\Game Assets\expedition\overworld_v1\`

## Reference images (open these files from disk and use them as image references)

| Role | Path |
|---|---|
| Style anchor (look, palette, brushwork) | `S:\AI\Game\Game Assets\expedition\overworld_v1\ow_style_anchor.png` |
| Job 1 current image (keep look, fix problems) | `S:\AI\Game\Game Assets\expedition\overworld_v1\ground_mud_marsh.png` |
| Job 2 current image | `S:\AI\Game\Game Assets\expedition\overworld_v1\water_shallow.png` |
| Job 3 current image | `S:\AI\Game\Game Assets\expedition\overworld_v1\ford_stones_sheet.png` |
| Matching good example of a seamless ground tile | `S:\AI\Game\Game Assets\expedition\overworld_v1\ground_forest_floor.png` |

For every job attach the style anchor AND that job's current image. Tell the generator: "Keep the palette, brush texture, light from the upper left and detail density of the second image. Change only what is listed."

## Output rules (all jobs)

- Save over the SAME file name in the folder above. First copy the old file to `S:\AI\Game\Game Assets\expedition\overworld_v1\old_before_fix\` (create it).
- Do not resize. Keep the generator's real pixel size.
- No text, letters, UI, people, creatures, borders or watermarks.
- Do not touch any other file. Do not edit scripts. Do not import anything into Unity.
- Up to 3 tries per job. Keep the best result. Write what you did to `old_before_fix\FIX_LOG.txt`.

## Job 1: ground_mud_marsh.png (square, opaque, must tile seamlessly)

What is wrong now: it contains tall upright reeds, rocks and stumps. In the game this ground repeats hundreds of times, so every upright object shows as a visible repeating pattern.

Prompt to give the generator:

> Adams Haven painterly fantasy game texture, hand-painted with soft visible brush texture, cool teal-green palette with restrained warm accents, soft values, light from the upper left. Boggy marsh ground seen straight down from above: dark wet mud, shallow teal puddles with soft ripples, green algae edges, reed stubble LYING FLAT on the mud, tiny flat leaves. FLAT TEXTURE ONLY: no upright reeds, no cattails, no rocks, no stumps, no objects with height. No single feature larger than one sixth of the image width. Perfectly seamless tileable square: the pattern continues across all four edges, nothing is cut off at an edge, no border, no vignette.

Check with Python (PIL + numpy): roll the image by half its width and half its height (`np.roll`), save it as `ground_mud_marsh__offset_check.png` and look at it. There must be no visible line at the centre cross, and no object bigger than one sixth of the width. If there is, retry.

## Job 2: water_shallow.png (square, opaque, must tile seamlessly)

What is wrong now: it has standing rocks or plants poking through the water. They repeat visibly.

Prompt:

> Adams Haven painterly fantasy game texture, hand-painted with soft visible brush texture, cool teal palette, light from the upper left. Shallow clear stream water seen straight down from above: sandy and pebbly stream bed visible through teal water, soft light ripples, gentle caustics. NO plants, NO standing stones, NO objects breaking the surface; only small flat pebbles on the bed, each smaller than one sixth of the image width. Perfectly seamless tileable square: the pattern continues across all four edges, no border, no vignette.

Same Python wrap test as job 1 (`water_shallow__offset_check.png`).

## Job 3: ford_stones_sheet.png (wide sheet, TRANSPARENT background, 2 stamps)

What is wrong now: the five stepping stones in the left stamp are separate pieces floating apart, so the row does not read as one crossing.

Prompt:

> Adams Haven painterly fantasy game art, hand-painted with soft visible brush texture, cool teal-green palette with restrained warm amber accents, elevated three-quarter bird's-eye view at about 60 degrees, light from the upper left. Isolated cut-out game stamps on a FULLY TRANSPARENT background (no ground, no water, no checkerboard, no colour fill, no cast shadow, no frame). Wide format, two separate stamps: one in the left half and one in the right half, with a wide empty transparent gap between them, nothing touching the image border. LEFT stamp: a row of five flat stepping stones from left to right, each stone almost touching the next, joined by thin strips of moss and wet gravel so the whole row reads as ONE connected crossing, wet tops catching light. RIGHT stamp: a shallow gravel ford, one connected patch of gravel and larger stones with a little white foam. Stones only; the water is painted underneath by the game.

Check with Python: all four corner pixels have alpha 0; there are exactly 2 separate subjects (connected regions with alpha > 40 after a 5 px dilation), and the left subject is ONE connected region, not five. No subject touches the border. If the check fails, retry (max 3 tries).

## When finished

Reply with: which jobs succeeded, the real pixel size of each output, and any check that failed. Do not run anything else.
