# Tower art direction — September 29, 2026

The active Unity project is `S:/AI/Game/Unity AHCG/My project`.

`TowerArtDirector` adds layered art to the existing Tower controller. It preserves the room grid and saves, uses wider visual cells, hides primitive architecture, and composes background, room interiors, residents, timber columns, crystal masonry, roof and foundation at distinct depths. The HUD reuses the Godot Celestium sidebar art. Original source art is preserved.

Generated with the built-in Codex image generator:
- `Structure/heart_sanctuary_v1.png`
- `Structure/foundation_v1.png` (transparent)
- `Structure/crown_v1.png` (transparent)

Project asset root: `Assets/Resources/AdamsHaven/TowerPresentation`.

Residents and Room Details are collapsible context panels. Selecting a room
opens its details. Camera framing adapts to the tower width and open panels,
while mouse/touch pan and zoom retain the same world-to-grid hit tests.
Room labels, character portraits and illustrated generic villagers complete
the live presentation. The Tower scene, save data and simulation remain in
the active Unity project; the older `unity-port` staging directory is not the
authoritative copy of the concurrently developed simulation.

Reused Godot sources:
- `art/tower/silverbrook_shelter/furnished/*.png` → `Rooms/`
- `art/tower/celestium_timber_v2/{tower_frame,tower_structure}.png` → `Structure/`
- `art/tower/silverbrook_shelter/base/gate_celestium_right_v1.png` → `Structure/gate.png`
- `art/ui/celestium_generated_v1/tower_sidebar.png` → `UI/sidebar.png`

Images are cropped non-destructively by Sprite UV rectangles at runtime to fit continuous Tower floors. The built-in image generator created new images; no AI source asset was edited with Python.

## Generation prompts

Verification: Unity compiled successfully. The starting save and a populated
21-room / 18-resident checkpoint were inspected in Play mode. The resident
toggle and automatic room-detail opening both passed. The Console reported
zero errors or exceptions after the final populated capture. Screenshots are
saved in the Godot workspace's `unity-port/tower-art-start-final.png` and
`unity-port/tower-art-populated-final.png`. The active editor was returned to
the starting save and paused for the user.


### Living room refresh

Use case: style-transfer. Asset type: finished full-bleed square cutaway room interior for Adams Haven fantasy Tower shelter game. The provided image is a reference for this room's humble timber construction and bedding. Reimagine it as a polished warm inviting shelter living room with lovely handcrafted 3D-painted materials. View straight from front with shallow floor depth. Remove all exterior roof, triangular attic, exterior background and outside foundation; the rectangular image shows ONLY the usable room interior from floor to flat wooden ceiling. Pale cream plaster and warm oak beams, a modest bed with muted teal quilt against rear wall, tiny bedside lantern, shelf of books and folded blankets, a leaded window with soft blue mountain daylight. Warm amber lantern glow lights the interior brightly and clearly. Charming rustic fantasy craft, detailed surfaces but clean readable silhouette and lighting suitable for a small game room. Floor/walkway in bottom 22 percent is open for separate animated characters. Keep furniture at rear and sides. No characters, no text, no UI, no border, no exterior roof. Full-bleed interior at all four canvas edges.

### Kitchen refresh

Use case: style-transfer. Asset type: finished wide 2:1 rectangular cutaway kitchen room interior for Adams Haven fantasy Tower shelter game. Use the provided kitchen image as an architectural and furniture reference. Create a beautifully finished, warmly lit fantasy kitchen interior with real visual depth and crisp painterly 3D game art. Straight-on orthographic front cutaway with shallow visible floor. Image is full-bleed usable room: remove exterior roof, sky/background and exterior foundation. Flat oak ceiling, pale plaster rear wall and thick honey-brown oak beams. Central stone hearth has small bright amber cooking fire and copper cauldron, copper pans hang nearby, rear preparation bench left holds bread and vegetables, wooden shelves right with ceramic jars and sacks. Two small arched leaded windows reveal cool mountain daylight. Bright inviting readable warm lantern/fire illumination, beautifully textured crafted materials, restrained teal accents. Clear uncluttered foreground walking strip fills bottom 20 percent for separate chibi residents. No characters, no text, no UI, no outer frame. Horizontal straight floor and ceiling, full-bleed room reaches every edge.

Additional generated assets: `Rooms/living_interior_v1.png` and `Rooms/kitchen_interior_v1.png`. Existing transparent Godot villagers are also reused for generic residents and HUD portraits; animated roster and Celestium models retain their own assets.


### Heart sanctuary

Use case: stylized-concept. Asset type: production game environment layer for Adams Haven, a fantasy vertical shelter management game. Generate a beautiful front-facing orthographic cutaway INTERIOR of the Celestium Heart sanctuary, one rectangular room, landscape 3:2 image. Full-bleed room interior; no roof and no exterior building silhouette. Rich hand-painted 3D fantasy diorama art with elegant carved dark oak pillars, pale weathered stone, brass accents, climbing ivy, and a suspended luminous faceted cyan crystal heart at center held inside a delicate brass celestial armillary. Arched rear windows show soft blue mountains. Warm amber hanging lanterns balance luminous turquoise crystal. Strong controlled lighting, exquisite believable surface details, crisp readable shapes at small game scale. Floor takes bottom 18 percent with clear walkable foreground space for separately rendered chibi characters. The room's rear wall and furnishings are behind that walkway. Straight horizontal floor, symmetrical front elevation, shallow visible floor depth, no fisheye. Designed to sit inside a modular tower frame. No characters, no text, no logos, no UI, no frame outside image, no black bars. This is a finished game asset, not a concept sheet.

### Foundation

Use case: stylized-concept. Asset type: transparent foreground terrain platform for Adams Haven fantasy tower management game. A gorgeous wide horizontal floating stone terrace foundation viewed perfectly straight from the front, orthographic side-view cutaway, landscape 3:1 composition. Upper edge is a nearly straight level walkable pale stone platform spanning almost full width, with a modest stone stair descending at far right. Beneath it, layered ancient ashlar retaining wall, rough natural bedrock, moss, delicate ivy, a few ferns, tiny cyan glowing crystal seams and trailing roots. Warm late afternoon rim light, atmospheric illustrated 3D fantasy game diorama style with realistic painted texture, crisp silhouette, sophisticated muted slate-blue stone, green moss and sparse amber lanterns. The top surface is empty to receive a separate modular tower building. Main mass occupies bottom half to two thirds of canvas; nothing rises above the platform except two small lanterns at extreme left and right edges. Entire terrain island silhouette visible with transparent space around it. No tower, no building, no rooms, no people, no background scenery, no sky, no ground plane outside the asset, no text, no UI. Genuine transparent alpha background. Final production sprite layered in front of a mountain backdrop and behind the tower rooms.

### Roof crown

Use case: stylized-concept. Asset type: transparent architectural roof crown sprite for a modular fantasy tower shelter game, Adams Haven. A single elegant wide medieval fantasy rooftop crown, straight-on front elevation, landscape 4:1 composition. Continuous shallow pitched roof of richly painted indigo slate shingles with moss accents, dark oak eaves, fine pale stone molding and subtle warm bronze trim. One restrained central dormer with glowing amber window and a tiny cyan crystal finial, small graceful upturned end caps, modest ivy at outer edges. The base eave is perfectly horizontal. Keep the roof shallow: the tall central dormer stays within top 80 percent. Shallow orthographic diorama depth, warm afternoon light, detailed painterly 3D game environment material quality matching hand-painted fantasy stone and timber room interiors. Complete silhouette within image with small transparent margin. No building walls below the eave, no rooms, no background, no sky, no ground, no characters, no text, no UI. Genuine transparent alpha background. This roof will be layered on top of separately rendered rooms in a growing vertical tower.


## Godot world reference pass

Ran Godot Shelter.tscn with autosave disabled and inspected its live reference screenshot. Added the original Silverbrook panorama, continuous sky, continuous soil-to-Celestium rock, bedrock and crystalline depths to Unity's fixed world coordinates. These no longer follow the camera as a flat full-screen background. Panorama imports at its native 9202 x 941 pixels, with mipmaps. Transparent horizon and stratum materials use the included TowerHorizon.shader, retained through Resources material references for player builds.

Core landings use Godot's stairwell and F-rank freight lift. The original Heart energy plate runs through the core. Upper and underground floors terminate at the core; the eastern Gate column is ground-only. Build buttons now show existing building-card art, with furnished room thumbnails for other definitions. New copies are under Scenery, Structure and Cards in TowerPresentation (88 PNG assets total including the prior pass).

Unity Play initialization was repaired by restoring normal scene/domain reload. TowerPortValidation.Run passed after the concurrent simulation update: ten checkpoints, the founding tutorial, 48 motion atlases and fifteen simulation scenarios. Visual review covers the founding scene, populated slot 3 and floor -16 in slot 10. This is still a partial gameplay migration; the validation does not establish full Godot feature parity.
