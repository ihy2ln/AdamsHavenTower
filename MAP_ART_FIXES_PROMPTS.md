# Overworld art: optional Codex fixes

The seams, anchors and prop scale are already fixed in code (`Tools/fix_overworld_art.py`). Only regenerate these if they bother you in-game. Output to `S:\AI\Game\Game Assets\expedition\overworld_v1\`, same file names (overwrite), then tell Claude to re-run slice + fix + import.

Attach for every prompt: `ow_style_anchor.png` and the current version of the file being fixed (as "keep this look, fix the listed problems").

## Paste this first

```text
Regenerate the listed images in S:\AI\Game\Game Assets\expedition\overworld_v1\ with your image generation tool.
For each one attach ow_style_anchor.png (style) and the existing file of the same name (what to keep).
Keep palette, brushwork, light (upper left) and detail density of the existing file; change ONLY what the prompt says.
Save over the same file name. Real size is fine (do not resize). No text, people, creatures, UI or borders.
After each image run a Python wrap test: roll by half width and height and check no visible line at the centre cross.
```

## 1. ground_mud_marsh.png (seamless 1024 tile, opaque)

> Same boggy marsh ground as the attached file (dark wet mud, shallow teal puddles, algae edges), but FLAT top-down texture only. Remove every upright reed, cattail, rock and stump; keep only flat details: reed stubble lying on the mud, tiny flat leaves, ripples in the puddles. No feature larger than one sixth of the tile width. Perfectly seamless on all four edges: the mud and puddle pattern must continue across each edge, nothing cut off at an edge.

## 2. water_shallow.png (seamless 1024 tile, opaque)

> Same shallow clear stream water as the attached file (sandy and pebbly bed through teal water, soft ripples, gentle caustics), but with NO plants and NO standing stones: only flat pebbles on the bed, no object breaking the surface. Pebbles smaller than one sixth of the tile width. Perfectly seamless on all four edges.

## 3. ford_stones_sheet.png (2048x1024 transparent sheet, 2 stamps side by side)

> Two separate stamps, left and right half of the image, on a fully transparent background, wide empty gap between them, nothing touching the border. Same painterly three-quarter bird's-eye style as the attached file. Left: a line of five flat stepping stones left to right, each stone TOUCHING the next by a thin moss-covered gap so the row reads as one continuous crossing, wet tops catching light. Right: a shallow gravel ford, one connected patch of gravel and larger stones with a little white foam. No water body, no ground plane, no cast shadow.

## 4. Only if trees look wrong in-game: camera angle (canopy_*_sheet.png, saplings_sheet.png)

Currently the trees show a long trunk and front face. To get a steeper top-down look regenerate a sheet with this extra line added to its original prompt:

> Camera is a steep bird's-eye view, about 70 to 75 degrees from the horizontal: each tree is mostly a round crown seen from above, with only a short stub of trunk (at most 15% of the stamp height) visible under the crown. Crown width stays the same as the attached file.
