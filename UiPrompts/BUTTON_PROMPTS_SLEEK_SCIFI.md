# Tower Mode button art, sleek sci-fantasy style (Wuthering Waves inspired): Codex image prompts

Alternative to `BUTTON_PROMPTS.md` (the dark timber and slate look). This pack targets the **sleek, minimal, angular glass UI** of modern action gacha games such as Wuthering Waves: thin precise linework, chamfered corners, translucent smoked-glass panels, restrained color, one bright accent for the selected state. Each prompt describes the style traits itself, so nothing depends on naming a game. The Celestium crystal accent colors keep it tied to Adams Haven.

## Style summary (already inside every prompt)

- **Shapes:** rectangles with **chamfered (cut) corners** and sharp 45-degree notches, thin parallelogram slants, small diamond and hexagon motifs. No rounded cartoon shapes, no heavy ornament.
- **Surfaces:** smoked translucent dark glass (deep blue-black, about 70% opacity feel), a very thin pale-silver hairline border, a faint inner gradient, tiny corner tick marks, subtle fine grid or micro-etch texture. Soft frosted look.
- **Light:** minimal glow. Selected controls get a **bright accent edge, a diagonal highlight slash and corner brackets**, not a heavy aura.
- **Color:** monochrome ice-white, silver and charcoal as the base; accent **pale ice-cyan** for general selection, **warm champagne gold** for primary actions, **soft violet** for Celestium/summon. Red only for alerts.
- **Icons:** flat monochrome line icons with a thin stroke and one small filled accent, centered in a diamond or chamfered frame.

## How to use

1. Run **prompt 0 (style anchor)** first; keep the best result as `ui2_style_anchor.png`.
2. Run every other prompt attaching `ui2_style_anchor.png` (each says to copy material, shape language and palette only).
3. Every asset is a **transparent PNG, no text, no icons baked into frames** unless stated. Labels and icons are added in Unity.
4. State sheets are one image with equal cells, the control in the same position in each cell.
5. **9-slice** frames have fixed corners and plain, repeatable middles.

State language: **Normal** (smoked glass, hairline border), **Pressed** (slightly darker and smaller, border brighter), **Selected/Active** (accent edge, diagonal slash, corner brackets), **Disabled** (faded, border dashed-dim), **Ready** (thin pulsing accent rim, animated in Unity).

---

### 0. Style anchor board

`ui2_style_anchor.png` · 2048x1024 · no attachment

```text
Premium modern action-RPG gacha game UI concept board in a sleek, minimal, angular sci-fantasy style. On a plain dark charcoal-blue background, show: one wide button, one smaller button, one diamond-shaped icon button, one panel corner, and one slim resource chip, each shown in two states side by side (calm, and selected). All controls use chamfered cut corners with 45-degree notches, smoked translucent dark glass bodies, a very thin pale-silver hairline border, tiny corner tick marks, a subtle micro-etched grid texture, and restrained color: ice-white and silver base, with one bright pale ice-cyan accent edge, a diagonal highlight slash and corner brackets on the selected state. Clean, precise, elegant, high contrast, minimal glow, vector-sharp edges with soft frosted depth, like a top-grossing modern action game, not cartoonish, not ornate, no skeuomorphic wood or metal. No text, no letters, no numbers, no logos, no characters, no watermark.
```

### 1. Primary button (champagne gold) states

`btn2_primary_states.png` · 2048x512 (4 cells of 512x512) · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. One wide game button as a 4-cell state sheet, each cell exactly 512x512 with the button centered at about 448x152 and identical position in every cell, genuinely transparent background. The button is a long rectangle with chamfered cut corners (45-degree notches at top-left and bottom-right, small notches at the other corners), a smoked translucent dark glass body with a faint champagne-gold gradient, a thin pale-gold hairline border, tiny corner tick marks, and a slim diagonal gold highlight slash on the left. Cell 1 NORMAL, cell 2 PRESSED (slightly smaller and darker, border brighter), cell 3 SELECTED (bright gold edge, stronger diagonal slash, corner brackets, a soft but restrained gold glow), cell 4 DISABLED (desaturated grey, border dim). Minimal, elegant, crisp. No text, no icons, nothing cropped, no outer shadow. Keep the center empty for a label.
```

### 2. Secondary button (ice) states

`btn2_secondary_states.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. One wide game button as a 4-cell state sheet, each cell 512x512, button centered at about 448x136, identical position in every cell, genuinely transparent background. Chamfered cut corners, smoked translucent dark glass body, thin pale-silver hairline border, tiny corner ticks, a faint ice-white inner gradient. Cell 1 NORMAL, cell 2 PRESSED, cell 3 SELECTED (pale ice-cyan accent edge, diagonal highlight slash, corner brackets), cell 4 DISABLED. Quieter than the gold primary button. No text, no icons, nothing cropped. Center left empty for a label.
```

### 3. Summon / Celestium button (violet) states

`btn2_violet_states.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. One wide premium button as a 4-cell state sheet, each cell 512x512, button centered at about 448x152, identical position in every cell, genuinely transparent background. Chamfered cut corners with a slightly more elaborate outline (a notched step on each end and a thin double border on the left and right), smoked translucent dark glass body with a soft violet gradient and a fine shimmering crystal-dust texture, a thin silver-violet hairline border and small diamond motifs at the ends. Cell 1 NORMAL, cell 2 PRESSED, cell 3 SELECTED (violet accent edge, diagonal slash, corner brackets, tiny floating sparkle points), cell 4 DISABLED. No text, no icons, nothing cropped. Empty center for a label.
```

### 4. Heart button (center emblem) states

`btn2_heart_states.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. The center button of a game dock, a large hexagonal emblem in a 4-cell state sheet, each cell 512x512 with the emblem centered at about 384x384, identical position in every cell, genuinely transparent background. A thin silver hexagonal frame with notched corners and small tick marks around a smoked glass core, inside which a faint luminous faceted crystal shape glows pale violet-cyan (shape only, no heart symbol). Cell 1 IDLE (calm, soft inner light), cell 2 PRESSED (slightly smaller, frame brighter), cell 3 READY (a thin pulsing accent ring, radiating fine light lines, stronger crystal glow, to signal that research, summon or upgrade is available), cell 4 DISABLED (dark and desaturated). No icon, no text, nothing cropped, no cast shadow.
```

### 5. Dock button plate (diamond or chamfered square) states

`btn2_dock_states.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A dock button plate in a 4-cell state sheet, each cell 512x512 with the plate centered at about 280x280, identical position in every cell, genuinely transparent background. A chamfered square (cut corners) of smoked translucent dark glass with a thin pale-silver hairline border and small corner ticks, empty center for an icon sprite. Cell 1 NORMAL, cell 2 PRESSED, cell 3 ACTIVE (ice-cyan accent edge on the border, diagonal highlight slash, a short bright accent bar glowing below the plate), cell 4 DISABLED. No icons, no text, nothing cropped.
```

### 6. Round utility buttons (diamond style) states

`btn2_round_states.png` · 2048x1024 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A small utility button in a 4-cell state sheet in the top row (each cell 512x512, button centered at about 224x224, identical position), shaped as an octagon with small chamfers (clean geometric, not round), smoked glass face and a thin silver hairline border, empty center for an icon. Cell 1 NORMAL, cell 2 PRESSED, cell 3 ACTIVE (ice-cyan edge, small corner brackets), cell 4 DISABLED. In the bottom row (1024 px tall image total) show three small badge pips each about 64px, each in its own 512x512 cell, centered: a red alert pip, a champagne-gold ready pip and a violet new pip, all small chamfered diamonds with a soft glow. Genuinely transparent background. No icons, no text, nothing cropped.
```

### 7. Floating action buttons (Collect all, Rush)

`btn2_fab_states.png` · 2048x1024 (2 rows x 4 cells of 512x512) · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. Two floating action buttons as a state sheet: row 1 is the champagne-gold COLLECT button, row 2 is the violet RUSH button. Each row has 4 cells (NORMAL, PRESSED, SELECTED, DISABLED), every cell exactly 512x512 with the button centered at about 432x128, identical position in every cell, genuinely transparent background. Each is a slim slanted parallelogram with chamfered corners, a smoked glass body, a thin hairline border, a bright accent line along the lower edge (gold in row 1, violet in row 2), and a faint diagonal slash; light and readable over a busy scene. No text, no icons, nothing cropped. Empty center for a label.
```

### 8. Tabs (active and inactive)

`btn2_tab_states.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A panel tab in a 4-cell state sheet, each cell 512x512 with the tab centered at about 400x104, identical position, genuinely transparent background. A slim slanted tab (parallelogram with a chamfered top corner), smoked glass body, thin hairline border. Cell 1 INACTIVE (dim, low contrast), cell 2 PRESSED, cell 3 ACTIVE (bright accent underline with a short diagonal slash, brighter border, subtly raised), cell 4 DISABLED. No text, no icons, nothing cropped.
```

### 9. Window-mode switcher and close

`btn2_window_modes.png` · 2048x512 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. Four small square header buttons on a genuinely transparent background in a 2048x512 image, each in its own 512x512 cell, button about 190x190, centered. Left to right: SMALL-WINDOW, MEDIUM-WINDOW, FULL-SCREEN, CLOSE. Each is a chamfered square of smoked glass with a thin hairline border and a thin-line silver glyph: a small rectangle inside a frame, a larger rectangle, four corner brackets, and a thin X (close, with a tiny red accent). Show the MEDIUM button in its selected state (ice-cyan accent edge, corner brackets). No text, no letters, nothing cropped.
```

### 10. Panel frame (9-slice) and header

`panel2_frame_9slice.png` · 1024x1024 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A game panel frame designed for 9-slice stretching, on a genuinely transparent background at 1024x1024: a rectangle with chamfered cut corners (top-left and bottom-right larger, others smaller), a smoked translucent dark glass interior (flat, subtle micro-etch texture), a thin pale-silver hairline border of constant width, small corner brackets, a short diagonal accent slash at the top-left corner only, and perfectly plain repeatable straight edges between corners. Calm, empty interior for UI. No text, no icons, nothing cropped.
```

`panel2_header_9slice.png` · 1024x160 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A panel title bar designed for horizontal stretching, 1024x160 on a genuinely transparent background: a slim bar of smoked glass with chamfered ends, a thin hairline border, a short bright accent slash on the left end, a faint repeating diagonal hatch on the far right end, plain repeatable middle. Empty interior for a title and small buttons. No text, no icons, nothing cropped.
```

### 11. Resource chip and progress bar

`chip2_resource_9slice.png` · 512x112 · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A resource counter frame designed for horizontal stretching, 512x112 on a genuinely transparent background: a slim chamfered bar of smoked glass with a thin hairline border, a diamond-shaped recessed socket on the left for a resource icon (empty, faintly lit), plain repeatable middle for a number, a small notch on the right end. No icon, no text, nothing cropped.
```

`bar2_progress.png` · 1024x192 (top frame, bottom fill) · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. A progress bar kit on a genuinely transparent background, 1024x192: top half (1024x96) an empty slanted bar frame with a thin hairline border and tiny tick marks along the bottom edge; bottom half (1024x96) the matching fill strip, a clean gradient from ice-cyan to violet with a bright thin leading edge and the same slanted ends, sized to slot into the frame. No text, no numbers, nothing cropped.
```

### 12. Dock icons

`icons2_dock.png` · 2560x512 (5 cells of 512x512) · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. Five game dock icons on a genuinely transparent background in a 2560x512 image, each centered in its own 512x512 cell at about 300x300, in a flat monochrome thin-line style (pale silver stroke about 8px) with one small filled accent color per icon, geometric and minimal: 1 BUILD (a hammer crossing a beam, accent gold), 2 PEOPLE (two simple figure outlines, accent ice-cyan), 3 HEART (a faceted crystal outline, accent violet), 4 MAP (a folded map with a small pin, accent red), 5 BATTLE (two crossed swords, accent champagne). Crisp, readable at 40 px, no text, no frame, nothing cropped.
```

### 13. Utility icons

`icons2_utility.png` · 2560x1024 (5 x 2 cells of 512x512) · transparent · attach `ui2_style_anchor.png`

```text
Use the attached image only for shape language, material and palette; do not copy its layout. Ten utility icons on a genuinely transparent background in a 2560x1024 image, each centered in its own 512x512 cell at about 280x280, in the same flat monochrome thin-line silver style with one small accent fill each. Row 1: PAUSE (two bars), SPEED x1 (one chevron), SPEED x2 (two chevrons), STEWARD (a crown over a small hand), ALERTS (a bell with a red accent). Row 2: GOALS (a list with a check), MENU (three horizontal lines with different lengths), COLLECT (an open hand with a small gold coin), RUSH (a lightning bolt over an hourglass), DRAWER ARROW (a down chevron). Crisp, minimal, readable at 32 px, no text, no frame, nothing cropped.
```

---

## Unity import notes

- Slice state sheets as Sprite (Multiple) grids and use Sprite Swap for Normal, Pressed, Selected and Disabled.
- Panel frame, header, chip: 9-slice with borders at the corner size drawn.
- Add the **glass blur** in Unity (a UI blur shader or a pre-blurred backdrop plane) behind the smoked-glass panels; the art provides only the tinted glass and the lines.
- Animate Ready and Active (accent rim pulse, diagonal slash sweep, 1.2 s) in Unity instead of baking motion.
- Use TextMeshPro with a clean geometric sans for labels; keep 48 dp touch targets.
