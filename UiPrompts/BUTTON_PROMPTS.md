# Tower Mode button and control art: Codex image prompts

Turnkey prompts for AAA-quality UI controls. Layout comes from `../TOWER_MODE_GDD.md` section 12 and `../UiWireframes/tower_ui_mockup.html`.

## How to use

1. Run prompt **0 (style anchor)** first and keep the best result as `ui_style_anchor.png`.
2. Run every other prompt attaching `ui_style_anchor.png` (each prompt already says to copy material and palette only).
3. Save with the file names shown. Every asset is **transparent PNG, no text, no icons baked into frames** unless the prompt says so. Labels, numbers and icons are added in Unity (TextMeshPro and separate icon sprites), so one frame serves every label.
4. State sheets are one image with the states in a row, equal cells, same position in every cell, so they slice cleanly.
5. Frames marked **9-slice** must have flat, repeatable middles and fixed corners; Unity stretches the middle.

## Shared style (already included in every prompt)

Dark weathered timber and slate with silver trim, moonlit blue-silver and violet Celestium crystal light, hand-painted high-end Korean fantasy gacha UI, crisp clean edges, rich dimensional shading, subtle engraved detail, luminous crystal inlays. Matches the Celestium Tower art and the resource icons in `Game Assets/ui/celestium_hud_v1`.

State language used by every control: **Normal** (calm dim crystal), **Pressed** (inset, darker, crystal bright), **Active/selected** (steady moon-blue or violet glow, brighter trim), **Disabled** (desaturated, crystal dark), **Ready** (pulsing-ready glow baked as a bright rim, animated in Unity).

Palette: timber #3a2c20, slate #242c38, silver #c8d4e6, moon blue #8fb4ff, violet #9b7bff, gold #f2c14e (primary actions only), danger red #ff5d5d (alerts only).

---

### 0. Style anchor board

`ui_style_anchor.png` · 2048x1024 · no attachment

```text
High-end Korean fantasy RPG and gacha game UI art, polished hand-painted rendering, crisp clean edges, rich dimensional shading, luminous crystal inlays. A style board for a mobile tower-building game: on a plain dark charcoal background, show one large primary button, one medium secondary button, one round icon button, one tall panel frame corner and one pill-shaped resource chip, all built from dark weathered timber and slate with silver trim, engraved detail and glowing moonlit blue-silver and violet Celestium crystal inlays. Show each in two states side by side: calm and glowing-active. The materials must look tactile and premium, like a modern top-grossing fantasy gacha game, not flat vector. No text, no letters, no numbers, no logos, no characters, no watermark.
```

### 1. Primary button (gold action) states

`btn_primary_states.png` · 2048x512 (4 cells of 512x512) · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A single wide rounded-rectangle game button drawn as a 4-cell state sheet, each cell exactly 512x512 with the button centered at about 448x176 and identical position in every cell, genuinely transparent background. Button body: dark slate and weathered timber with a warm gold top bevel, silver corner studs, a recessed center plate with a soft gold-lit crystal inlay band, fine engraving along the edges. Cell 1 NORMAL: calm warm gold glow. Cell 2 PRESSED: pushed inward, darker, slightly smaller highlight, crystal bright. Cell 3 SELECTED: brighter gold trim with a soft outer glow. Cell 4 DISABLED: desaturated grey, crystal dark, no glow. No text, no icons, no letters, no numbers, no drop shadow outside the button, nothing cropped. The center plate must stay empty for a text label.
```

### 2. Secondary button (slate) states

`btn_secondary_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A single wide rounded-rectangle game button drawn as a 4-cell state sheet, each cell 512x512 with the button centered at about 448x160, identical position in every cell, genuinely transparent background. Button body: dark slate with weathered timber edging, silver trim, a recessed center plate, a thin moon-blue crystal line along the bottom edge. Cell 1 NORMAL, cell 2 PRESSED (inset, darker), cell 3 SELECTED (moon-blue glow, brighter trim), cell 4 DISABLED (desaturated, crystal dark). Quieter than the gold primary button. No text, no icons, no letters, nothing cropped. Empty center plate for a label.
```

### 3. Summon / Celestium button (violet) states

`btn_violet_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A single wide rounded-rectangle premium button as a 4-cell state sheet, each cell 512x512, button centered at about 448x176, identical position in every cell, genuinely transparent background. Dark slate and timber body with ornate silver filigree corners and a recessed center plate holding a glowing violet Celestium crystal band; the most luxurious button in the set. Cell 1 NORMAL (steady violet light), cell 2 PRESSED (inset, crystal flares bright), cell 3 SELECTED (strong violet aura, tiny sparkle motes), cell 4 DISABLED (desaturated, crystal dark). No text, no icons, no letters, nothing cropped. Empty center plate for a label.
```

### 4. Heart button (dock center orb) states

`btn_heart_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. The center button of a game dock: a round orb button in a 4-cell state sheet, each cell 512x512 with the orb centered at about 384x384, identical position in every cell, genuinely transparent background. A ring of dark timber and slate with silver studs holding a glass-like violet and moon-blue Celestium crystal heart in the middle with fine inner light. Cell 1 IDLE (calm slow glow), cell 2 PRESSED (ring pushed in, crystal flares), cell 3 READY (strong pulsing-ready violet aura with radiating light threads, to signal that research, summon or upgrade is available), cell 4 DISABLED (dark, desaturated). No heart symbol or icon on top, no text, no letters, nothing cropped, no cast shadow.
```

### 5. Dock button plate (square) states

`btn_dock_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A square dock button plate drawn as a 4-cell state sheet, each cell 512x512 with the plate centered at about 288x288, identical position in every cell, genuinely transparent background. A softly rounded square of dark slate with timber edging, silver corners and an empty recessed center for an icon sprite. Cell 1 NORMAL (calm), cell 2 PRESSED (inset), cell 3 ACTIVE (steady moon-blue glow rim and brighter silver trim, a small crystal pip glowing at the bottom edge), cell 4 DISABLED (desaturated). No icons, no text, nothing cropped.
```

### 6. Round cluster button states

`btn_round_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A small round utility button (speed, steward, alerts, goals, menu) drawn as a 4-cell state sheet, each cell 512x512 with the circle centered at about 240x240, identical position in every cell, genuinely transparent background. Silver rim, dark slate face with a faint crystal inner glow, tiny timber inlay. Cell 1 NORMAL, cell 2 PRESSED, cell 3 ACTIVE (moon-blue glow), cell 4 DISABLED. Also include, in a separate row below the four cells in the same image (image becomes 2048x1024), a small red alert pip, a small gold ready pip and a small violet new pip, each about 64px, on transparent background. No icons, no text, nothing cropped.
```

### 7. Floating action pill (Collect all, Rush)

`btn_fab_states.png` · 2048x1024 (2 rows x 4 cells of 512x512) · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. Two pill-shaped floating action buttons as a state sheet: row 1 is the GOLD "collect" pill, row 2 is the VIOLET "rush" pill. Each row has 4 cells (NORMAL, PRESSED, SELECTED, DISABLED), every cell exactly 512x512 with the pill centered at about 440x136, identical position in every cell, genuinely transparent background. Pills have a curved silver rim, a dark slate body, a glowing crystal inlay line (gold in row 1, violet in row 2), and a soft inner highlight; they must feel light and floaty, readable over a busy game scene. No text, no icons, nothing cropped. Empty center for a label.
```

### 8. Tab buttons (active and inactive)

`btn_tab_states.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A panel tab drawn as a 4-cell state sheet, each cell 512x512 with the tab centered at about 400x112, identical position, genuinely transparent background. A slightly trapezoidal tab with a rounded top, silver edge, dark slate face. Cell 1 INACTIVE (dim, low), cell 2 PRESSED, cell 3 ACTIVE (raised, moon-blue crystal underline glowing, bright silver trim), cell 4 DISABLED. Designed so the active tab visually merges into a panel below it. No text, no icons, nothing cropped.
```

### 9. Window-mode switcher (pop-out, medium, full) and close

`btn_window_modes.png` · 2048x512 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A row of four small square header buttons for a game panel, on a genuinely transparent background in a 2048x512 image: each button sits in its own 512x512 cell, button about 200x200, centered. Left to right: SMALL-WINDOW button, MEDIUM-WINDOW button, FULL-SCREEN button, CLOSE button. Each is a dark slate rounded square with a silver rim; draw a clear engraved glyph in silver and moon blue on each: a small rectangle inside a frame (small window), a larger rectangle (medium), four corner brackets (full screen), an X (close; the close glyph in soft red-silver). Show the MEDIUM button in its active glowing moon-blue state to demonstrate the selected look. No text, no letters, no numbers, nothing cropped.
```

### 10. Panel frame (9-slice) and header bar

`panel_frame_9slice.png` · 1024x1024 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A game panel frame designed for 9-slice stretching, drawn on a genuinely transparent background at 1024x1024: a rounded-rectangle panel with a dark slate interior (flat, subtly textured, empty), a silver and timber border of constant thickness about 40px with ornate corner pieces (engraved filigree and small crystal studs at the four corners only), and perfectly plain, repeatable straight edges between the corners. No decoration in the middle of the sides, no text, no icons. The interior must be a calm flat dark area so UI can sit on top. Nothing cropped.
```

`panel_header_9slice.png` · 1024x192 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A game panel title bar designed for horizontal stretching, 1024x192 on a genuinely transparent background: a slim timber and slate bar with a silver rim, rounded top corners to match a panel frame, a thin glowing moon-blue crystal line along the bottom edge, plain repeatable middle, ornate fixed end caps. Empty interior for a title and small buttons. No text, no icons, nothing cropped.
```

### 11. Resource chip and progress bar

`chip_resource_9slice.png` · 512x128 · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A small pill-shaped resource counter frame designed for horizontal stretching, 512x128 on a genuinely transparent background: dark slate pill with a thin silver rim, a round recessed socket on the left side where a resource icon sits (leave the socket empty, softly lit from inside), plain repeatable middle for a number, a rounded right cap. No icon, no text, nothing cropped.
```

`bar_progress.png` · 1024x256 (top half frame, bottom half fill) · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. A progress bar kit on a genuinely transparent background, 1024x256: in the top half (1024x128) an empty horizontal bar frame with silver rim, dark recessed trough and small end caps; in the bottom half (1024x128) the matching fill strip, a glowing gradient of moon blue to violet crystal light with a subtle bright leading edge, same height and rounded ends so it slots into the trough. No text, no numbers, nothing cropped.
```

### 12. Dock icons (draw once, tint in Unity)

`icons_dock.png` · 2560x512 (5 cells of 512x512) · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. Five game dock icons on a genuinely transparent background in a 2560x512 image, each centered in its own 512x512 cell at about 360x360, in the same hand-painted fantasy style with silver and moon-blue highlights: 1 BUILD (a hammer crossed with a small timber beam), 2 PEOPLE (two stylized resident silhouettes, front-facing, one taller than the other), 3 HEART (a faceted glowing violet Celestium crystal heart), 4 MAP (a folded parchment map with a red marker pin), 5 BATTLE (two crossed swords with a small crystal in the guard). Bold readable silhouettes that still read at 40 px. No text, no frame, nothing cropped.
```

### 13. Utility icons

`icons_utility.png` · 2560x1024 (5 x 2 cells of 512x512) · transparent · attach `ui_style_anchor.png`

```text
Use the attached image only for material, palette and style; do not copy its layout. Ten small game utility icons on a genuinely transparent background in a 2560x1024 image, each centered in its own 512x512 cell at about 320x320, same hand-painted fantasy style with silver and moon-blue highlights, bold silhouettes that read at 32 px. Row 1: PAUSE (two bars), SPEED x1 (single chevron), SPEED x2 (double chevron), STEWARD (a small crowned hand holding a quill), ALERTS (a bell with a flame-red clapper). Row 2: GOALS (a scroll with a checkmark), MENU (three stacked bars as engraved planks), COLLECT (an open hand receiving golden coins), RUSH (a lightning bolt in a small hourglass), DRAWER ARROW (a downward chevron). No text, no frame, nothing cropped.
```

---

## Unity import notes

- Import state sheets as **Sprite (Multiple)**, grid-slice into equal cells, and drive the Button transition with a **Sprite Swap** (Normal, Pressed, Selected, Disabled).
- 9-slice frames: set borders to the corner size (panel frame about 40px at 1024 scale, header and chip caps as drawn).
- Animate Ready and Active glows in Unity (additive overlay, pulse 1.2 s) instead of baking motion.
- Use TextMeshPro for all labels; keep 48 dp minimum touch targets regardless of art size.
