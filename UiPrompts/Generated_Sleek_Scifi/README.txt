Sleek sci-fantasy UI generation set

Generated with Codex's built-in image_gen tool from BUTTON_PROMPTS_SLEEK_SCIFI.md.
PROMPTS_USED.md preserves the original prompt set. A style anchor was generated
first and supplied as a material/palette/shape reference for the other sheets.
Dock icons were refined to replace the heart silhouette with a mineral crystal.
Utility icons were refined to remove glass fills and glow.

Contains 16 PNGs: one opaque style anchor and 15 asset sheets with alpha.
asset_manifest.json records actual pixel dimensions and corner alpha checks.

These are generated source art, not a verified Unity-ready sprite package.
The generator did not follow the requested exact canvas sizes. Slice using
actual image dimensions and visible sprite bounds, not a 512px grid.
Some cells have uneven spacing, faint separator marks, strong selected-state
glow, or nearly identical Normal/Pressed states. The utility badge row has
three badges distributed across the row rather than the specified cell grid.
Panel/header/chip textures include diagonal highlights and grid texture;
their repeatability under 9-slice stretching needs Unity testing and cleanup.
Glass tint alpha and icon legibility at 32/40px have not been verified in-game.

Button state order: Normal, Pressed, Selected/Active, Disabled.
Heart state order: Idle, Pressed, Ready, Disabled.
FAB rows: Collect (gold), Rush (violet).
Utility button badges: alert (red), ready (gold), new (violet).
Window controls: small, medium selected, fullscreen, close.
Progress kit: frame above, fill below.

Keep labels separate in TextMeshPro. Use Unity Sprite (Multiple) for sheets.
Determine 9-slice borders after cropping panels to their visible frame bounds.
Implement glass blur and Ready/Active pulse animation in Unity.
No runtime, scene, importer, or existing asset changes were made.
