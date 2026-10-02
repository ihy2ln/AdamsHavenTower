# Tower Mode button art, AAA angular sci-fantasy with depth and flare: Codex image prompts

Third style pack, after `BUTTON_PROMPTS.md` (timber and slate) and `BUTTON_PROMPTS_SLEEK_SCIFI.md` (flat smoked glass). The target is **Wuthering Waves-style precise angular UI fused with high-fantasy craft**, at AAA quality: layered physical depth, bevelled metal trim, faceted Celestium crystal insets, rim light, and controlled lens flare. Every prompt below is self-contained (style and technical rules are repeated inside it) and never names a game.

The sheet file names match the keys in `UiSliced/slice_sheets.py`, so the output drops straight into the existing slicer and `TowerSleekArt` loader.

---

## Codex task (paste this first, then let it work through the list)

```text
You are producing game UI source art for the Unity project at S:\AI\Game\Unity AHCG\My project.
Read S:\AI\Game\Unity AHCG\My project\UiPrompts\BUTTON_PROMPTS_WUWA_FANTASY.md and generate every asset in it with your built-in image generation tool, in order, starting with prompt 0.

Rules:
1. Save everything to S:\AI\Game\Game Assets\ui\wuwa_fantasy_controls_v1\ using exactly the file names given in each prompt. Create the folder if needed. Do not modify anything else in either location.
2. Generate prompt 0 first. Pick the strongest result, save it as ui3_style_anchor.png, and attach it as the reference image for every later prompt.
3. After each image, check it with Python (PIL/numpy) and regenerate (up to 3 tries) if any check fails:
   - Real alpha: all four corner pixels have alpha 0, and the background has no checkerboard, colour fill or separator lines. The only exception is fx_flares.png, which must be on pure black (#000000).
   - The count of separate controls (connected regions above alpha 60) equals the cell count the prompt asks for, with clear transparent space between them.
   - Pressed is visibly different from Normal: mean luminance differs by at least 6%, or the body is at least 3% smaller.
   - No text, letters or numbers are visible.
   - Glows fade smoothly to zero alpha with no ragged or stepped halo edge.
   If the generator ignores the requested pixel size, keep the image and record its real size. Do not resample art to fake the size.
4. Then slice: run
   python "S:\AI\Game\Unity AHCG\My project\UiSliced\slice_sheets.py" "S:\AI\Game\Game Assets\ui\wuwa_fantasy_controls_v1"
   after first adding these entries to the SHEETS dict in slice_sheets.py:
   "btn_danger_states": [STATE4],
   "btn_close_states": [STATE4],
   "fx_flares": [["star_glint", "light_streak", "ring_bloom", "selection_sweep"]],
   and adding "fx_flares" to NO_NORMALIZE. Because fx_flares.png is on black, first convert it to alpha:
   alpha = max(R,G,B); RGB = RGB / alpha (un-premultiply; leave RGB as 0 where alpha is 0). Save the black
   original as fx_flares_black.png and the converted image as fx_flares.png, so the slicer can find the cells.
   Set OUT in the script to the source folder's "sliced" subfolder for this run, so the existing UiSliced output is not overwritten.
5. Write README.txt in the output folder: what was generated, the real pixel sizes, which checks passed or failed, retries used, and any known art issues. Also copy this prompt file there as PROMPTS_USED.md.
6. Do not import anything into Unity and do not edit scripts or scenes other than the slice_sheets.py changes in step 4.
```

---

## Style block (already inside every prompt)

Premium AAA action-RPG gacha game UI in a sleek angular sci-fantasy style. Precise futurist geometry (45-degree chamfered cut corners, slim parallelogram slants, hairline linework, small diamond and hexagon motifs) fused with high-fantasy craft (thin gilded filigree inlay lines, faintly engraved celestial rune etching, faceted Celestium crystal insets). **Layered physical depth:** an obsidian smoked-glass base plate with a soft inner shadow, a raised bevelled brushed-silver trim with a crisp specular highlight on the top edge and a darker underside, a recessed inner etched panel one step lower, and a small faceted gem catching light. **Lighting** from the top-left: bright rim light on upper edges, subtle ambient occlusion in recesses, and a tight soft contact shadow directly under the control. **Flare:** a small four-point star glint on the top-left bevel and a fine diagonal light streak across the glass. **Palette:** obsidian blue-black, brushed silver and pale gold, with one accent per control: ice-cyan (selection), champagne gold (primary), Celestium violet (summon) or ember red (danger).

State language: **Normal** is calm, with a faint glint. **Pressed** is about 4% smaller, darker glass, a deeper inner shadow, no streak and a brighter trim edge. **Selected/Active** adds a luminous accent edge, corner bracket marks, a stronger star glint and a soft bloom that fades fully to transparent inside the gutter. **Disabled** is desaturated and dim, with no glint and a matte trim.

---

### 0. Style anchor board

`ui3_style_anchor.png` · 2048x1024 · opaque · no attachment

```text
Premium AAA action-RPG gacha game UI concept board, sleek angular sci-fantasy style, shown on a plain deep charcoal-blue background. Lay out in a tidy grid with generous space: one wide primary button (champagne gold accent), one wide secondary button (ice-cyan accent), one circular icon button, one large circular centre emblem button with a faceted violet crystal, one slim resource chip, one panel corner with a title bar, and one tab. Show each in two states side by side: calm, and selected. Style: precise futurist geometry with 45-degree chamfered cut corners, slim parallelogram slants, hairline linework, small diamond and hexagon motifs, fused with high-fantasy craft: thin gilded filigree inlay lines, faintly engraved celestial rune etching, faceted Celestium crystal insets. Layered physical depth: obsidian smoked-glass base plate with soft inner shadow, raised bevelled brushed-silver trim with a crisp specular highlight on the top edge and darker underside, a recessed inner etched panel, a small faceted gem catching light. Lighting from top-left: bright rim light, subtle ambient occlusion in recesses, tight soft contact shadow under each control. Flare: a small four-point star glint on the top-left bevel and a fine diagonal light streak across the glass; selected states add a luminous accent edge, corner bracket marks and a soft smooth bloom. Palette: obsidian blue-black, brushed silver, pale gold; accents ice-cyan, champagne gold, Celestium violet. Vector-sharp anti-aliased edges, rendered like a top-grossing modern gacha game, elegant and readable. Not cartoon, not plastic, no wood, no heavy baroque ornament, no rounded bubbly shapes. No text, letters, numbers, logos, characters or watermark.
```

### 1. Primary button (champagne gold)

`btn_primary_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha; do not draw a checkerboard, colour fill or separator lines), containing 4 copies of one wide game button left to right, each centered in its own 512x512 cell at about 440x140, identical position and size in every cell, with at least 64 px of clear transparent space around each button including its glow. The button: a long rectangle with 45-degree chamfered cut corners (larger at top-left and bottom-right), an obsidian smoked-glass body with a warm champagne-gold inner gradient, a raised bevelled pale-gold metal trim with a crisp specular top edge and darker underside, a thin gilded filigree inlay line along the inner edge, a small faceted gold gem set into the left chamfer, faint engraved rune etching in the glass, a tight soft contact shadow underneath, lighting from top-left, a small four-point star glint on the top-left bevel and a fine diagonal light streak across the glass. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker glass, deeper inner shadow, no streak, brighter trim edge. Cell 3 SELECTED: luminous gold accent edge, small corner bracket marks, stronger star glint, soft bloom fading fully to transparent within the gutter. Cell 4 DISABLED: desaturated grey, matte trim, no glint. Leave the center clean for a text label. AAA quality, vector-sharp, not cartoon, not plastic. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 2. Secondary button (ice)

`btn_secondary_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of one wide game button left to right, each centered in its own 512x512 cell at about 440x128, identical position and size in every cell, at least 64 px clear transparent space around each including glow. The button: a long rectangle with 45-degree chamfered cut corners, an obsidian smoked-glass body with a faint cool ice-white inner gradient, a raised bevelled brushed-silver trim with a crisp specular top edge and darker underside, a thin silver hairline inlay along the inner edge, a tiny ice-cyan diamond gem in the left chamfer, faint rune etching, a tight soft contact shadow, lighting from top-left, a small star glint on the top-left bevel and a fine diagonal light streak. Quieter and less saturated than a gold primary button. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker, deeper inner shadow, no streak. Cell 3 SELECTED: luminous ice-cyan accent edge, corner bracket marks, stronger glint, soft bloom fading fully to transparent. Cell 4 DISABLED: desaturated, matte, no glint. Center clean for a label. AAA quality, vector-sharp. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 3. Summon button (Celestium violet)

`btn_violet_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of one premium wide button left to right, each centered in its own 512x512 cell at about 440x144, identical position and size in every cell, at least 64 px clear transparent space around each including glow. The button: a long rectangle with chamfered cut corners and a notched step at each end, a double thin trim at the left and right ends, an obsidian smoked-glass body with a deep Celestium violet gradient and fine shimmering crystal dust suspended in the glass, a raised bevelled silver-violet trim with a crisp specular top edge, faceted violet crystal shards set into both ends, faint engraved celestial rune etching, a tight soft contact shadow, lighting from top-left, a four-point star glint on the top-left bevel and a fine diagonal light streak. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker, deeper inner shadow, no streak. Cell 3 SELECTED: luminous violet accent edge, corner brackets, a few tiny floating sparkle points, soft bloom fading fully to transparent. Cell 4 DISABLED: desaturated, matte, no sparkles. Center clean for a label. AAA quality, magical but precise. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 4. Danger button (ember red)

`btn_danger_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of one wide warning button left to right, each centered in its own 512x512 cell at about 440x128, identical position and size in every cell, at least 64 px clear transparent space around each including glow. The button: a long rectangle with 45-degree chamfered cut corners, an obsidian smoked-glass body with a restrained deep ember-red inner gradient, a raised bevelled dark-silver trim with a crisp specular top edge, a thin ember-red hairline inlay, a small faceted red gem in the left chamfer, two short diagonal hazard notches cut into the right end of the trim, a tight soft contact shadow, lighting from top-left, a small star glint and a fine diagonal light streak. Serious, not cartoon. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker, deeper inner shadow, no streak. Cell 3 SELECTED: luminous ember accent edge, corner brackets, soft red bloom fading fully to transparent. Cell 4 DISABLED: desaturated, matte. Center clean for a label. AAA quality. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 5. Round icon button and badge pips

`btn_round_states.png` · 2048x1024 · 2 rows (4 + 3 cells) · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x1024 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines). Top row: 4 copies of one circular game icon button, each centered in its own 512x512 cell at about 300x300, identical position and size in every cell, at least 64 px clear transparent space around each including glow. The button: a perfect circle of obsidian smoked glass with a soft inner shadow and a faint radial etched rune ring, framed by a raised bevelled brushed-silver ring with a crisp specular highlight on the upper arc and a darker lower arc, four tiny diamond studs at 12, 3, 6 and 9 o'clock on the ring, a thin gilded filigree hairline just inside the ring, a tight soft contact shadow below, lighting from top-left, and a small four-point star glint on the upper-left of the ring. The center is empty and calm for a line icon added later. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker glass, deeper inner shadow, brighter ring. Cell 3 ACTIVE: the ring becomes luminous champagne gold with a soft bloom fading fully to transparent and a short glowing arc on the upper-left. Cell 4 DISABLED: desaturated, matte, no glint. Bottom row: 3 small notification pips, each centered in its own 512x512 cell (cells 1 to 3 of the row; leave cell 4 empty), each about 96x96: a faceted ruby-red diamond gem (alert), a faceted gold diamond gem (ready) and a faceted violet diamond gem (new), each with a bevelled silver rim, a crisp specular facet and a small soft glow fading fully to transparent. AAA quality, vector-sharp. No icons, text, letters, numbers or watermark, nothing cropped.
```

### 6. Heart centre emblem button

`btn_heart_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of the large centre button of a game dock, each centered in its own 512x512 cell at about 380x380, identical position and size in every cell, at least 48 px clear transparent space around each including glow. The emblem: a circular obsidian smoked-glass core holding a tall faceted Celestium crystal (violet to ice-cyan, inner light, sharp facets catching specular highlights, no heart symbol). It sits inside a layered frame: an inner bevelled silver ring, then a slightly larger outer ring of thin gilded filigree with six small hexagonal studs, with a faint engraved rune band between the two rings. A tight soft contact shadow, lighting from top-left, a four-point star glint on the upper-left of the frame and a faint diagonal light streak across the crystal. Cell 1 IDLE: calm, soft inner crystal light. Cell 2 PRESSED: about 4% smaller, frame brighter, crystal dimmer. Cell 3 READY: the crystal glows strongly, fine light rays radiate from it, the outer filigree ring glows champagne gold, and a smooth soft bloom fades fully to transparent within the cell (this signals a summon or upgrade is waiting). Cell 4 DISABLED: dark, desaturated, crystal unlit. AAA quality, the hero element of the screen. No text, letters, numbers or watermark, nothing cropped.
```

### 7. Tabs

`btn_tab_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of one panel tab left to right, each centered in its own 512x512 cell at about 400x100, identical position and size in every cell, at least 64 px clear transparent space around each. The tab: a slim parallelogram with a chamfered top-right corner, obsidian smoked glass, a thin bevelled silver trim with a specular top edge, a faint rune etch, lighting from top-left. Cell 1 INACTIVE: dim, low contrast, mostly flat. Cell 2 PRESSED: slightly darker, trim brighter. Cell 3 ACTIVE: raised with a brighter glass face, a luminous champagne-gold underline bar with a short diagonal slash at its left end, small star glint, soft bloom under the underline fading fully to transparent. Cell 4 DISABLED: desaturated. Center clean for a label. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 8. Close button

`btn_close_states.png` · 2048x512 · 4 cells · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A 2048x512 sprite sheet with a genuinely transparent background (real alpha, no checkerboard, no fill, no separator lines), 4 copies of a small window close button, each centered in its own 512x512 cell at about 220x220, identical position and size in every cell, at least 64 px clear transparent space around each. The button: a diamond (a square rotated 45 degrees) of obsidian smoked glass with a raised bevelled silver trim, crisp specular top edges, a tight soft contact shadow, lighting from top-left, and inside it a clean thin-line silver X glyph with a tiny ember-red gem at its crossing. Cell 1 NORMAL. Cell 2 PRESSED: about 4% smaller, darker. Cell 3 SELECTED: the trim glows ember red with a soft bloom fading fully to transparent. Cell 4 DISABLED: desaturated. No text, letters, numbers or watermark, nothing cropped.
```

### 9. Panel frame (9-slice) and header

`panel_frame_9slice.png` · 1024x1024 · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A game window frame designed for 9-slice stretching, 1024x1024 with a genuinely transparent background (real alpha) outside the frame. A rectangle with chamfered cut corners (large 45-degree cuts at top-left and bottom-right, small ones at the other two), whose interior is flat dark obsidian smoked glass at about 90% opacity with a very subtle even micro-etch texture and no gradient that would show when stretched. A raised bevelled brushed-silver trim of constant width runs all the way round, with a crisp specular highlight on the top edge and a darker underside, plus a thin gilded filigree hairline just inside it. Decoration only in the four corners: small corner bracket marks, a faceted Celestium violet gem set into the top-left chamfer with a small four-point star glint, and a short engraved rune flourish in the bottom-right chamfer. Every straight edge between the corners is perfectly plain and uniform so it can repeat. A tight soft shadow around the outside, lighting from top-left. Calm empty interior for UI. No text, letters, numbers, icons or watermark, nothing cropped.
```

`panel_header_9slice.png` · 1024x160 · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A window title bar designed for horizontal stretching, 1024x160 with a genuinely transparent background (real alpha). A slim bar of obsidian smoked glass with chamfered ends, a raised bevelled silver trim with a crisp specular top edge, a short luminous champagne-gold accent slash with a tiny faceted gem at the left end, and a faint diagonal hatch of fine engraved lines at the far right end. The middle stretch is perfectly plain and uniform so it can repeat. Lighting from top-left, tight soft shadow underneath. Empty interior for a title and small buttons. No text, letters, numbers, icons or watermark, nothing cropped.
```

### 10. Resource chip and progress bar

`chip_resource_9slice.png` · 512x112 · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A resource counter frame designed for horizontal stretching, 512x112 with a genuinely transparent background (real alpha). A slim chamfered bar of obsidian smoked glass with a thin bevelled silver trim and a crisp specular top edge. At the left end, a recessed diamond-shaped socket with a bevelled silver rim, empty and faintly lit from inside, to hold a resource icon. A plain uniform middle for a number, and a small notched cut at the right end with a tiny star glint. Lighting from top-left, tight soft shadow underneath. No icon, text, letters, numbers or watermark, nothing cropped.
```

`bar_progress.png` · 1024x256 · 2 rows · transparent · attach `ui3_style_anchor.png`

```text
Use the attached image only for material, depth, lighting and palette; do not copy its layout. A progress bar kit, 1024x256 with a genuinely transparent background (real alpha) and clear space between the two rows. Top row: an empty slanted bar frame about 960x64, a recessed obsidian glass channel with a bevelled silver trim, a specular top edge and tiny engraved tick marks along the bottom. Bottom row: the matching fill strip, same slanted ends, sized to fit inside the channel, a smooth luminous gradient from ice-cyan to Celestium violet with a bright thin leading edge on the right and a soft inner shine along its top. Middles are plain and uniform for stretching. No text, letters, numbers or watermark, nothing cropped.
```

### 11. Flare and glow overlays (additive)

`fx_flares.png` · 2048x512 · 4 cells · **pure black background** · attach `ui3_style_anchor.png`

```text
Use the attached image only for colour and light quality; do not copy its layout. A 2048x512 sheet of 4 additive light effects on a pure solid black background (#000000, no gradient, no texture, no separator lines), each centered in its own 512x512 cell with at least 64 px of pure black around it. Cell 1 STAR GLINT: a crisp four-point star with long thin rays and a tiny bright core, pale gold-white, about 300 px across. Cell 2 LIGHT STREAK: a single fine diagonal streak of light at 30 degrees, bright in the middle and fading to nothing at both ends, pale ice-white, about 440 px long. Cell 3 RING BLOOM: a soft circular ring of light, about 360 px across, with a thin bright ring line and a smooth falloff inside and out, champagne gold. Cell 4 SELECTION SWEEP: a soft wide diagonal band of light, bright in the centre, feathered edges, ice-cyan, about 440x160. All effects fade smoothly to pure black with no banding, no hard edges and no noise. No text, letters, numbers or watermark, nothing cropped.
```

---

## After Codex delivers (for the Unity side)

- `slice_sheets.py` already handles the irregular cell placement; Codex extends `SHEETS` for `btn_danger_states`, `btn_close_states` and `fx_flares`.
- Copy the sliced folders into `Assets/Resources/AdamsHaven/TowerPresentation/UI/Sleek/` (new manifest replaces `sleek_manifest.json`), and `TowerSleekArt` picks them up.
- `TowerUiSkin` then maps tints to sheets: Gold = primary, Teal = secondary, violet = summon, Alert = danger, round = icon buttons, plus heart, tab and close. Flares are drawn as separate additive Images: a star glint on hover, the sweep on select, and the ring bloom pulsing on the Heart when it is ready. This keeps the glows clean instead of baked in.
- Labels stay as live text, and icons stay as the existing line icons centered on the round plates.
