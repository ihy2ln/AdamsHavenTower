# Map anchor and mask editor: spec

Draft 2026-10-01. Part of [BATTLE_MODE_GDD.md](BATTLE_MODE_GDD.md) sections 4 and 18 (map direction, free movement, immersive POIs). Replaces the auto-placed nodes in `silverwood_layouts.json`.

## Why

Each Silverwood plate (`Expedition/Maps/silverwood_*_v2.png`) is just a painting. The game needs two extra things per plate:

1. **A walkable mask:** where the party can walk, where it is slow, and where it is blocked (deep water, cliffs, solid forest). This drives free movement, ration cost by distance and terrain, and the traced trail.
2. **Anchor spots:** exact places on the painting where a camp, POI, landmark or lair can sit, so POIs look placed on real features (a clearing, a ruin, a shore) and not on guessed coordinates.

Colour analysis put the first-pass nodes on open ground but is wrong on plates like the mine valley. A human click-and-paint tool is exact and quick (about 5 minutes per plate).

## Where it lives

- Editor window: `Assets/Editor/MapAnchorEditor.cs`, menu **Adams Haven / Map Anchor Editor**.
- Runtime data (in `Resources`, so builds can load it):
  - `Assets/Resources/AdamsHaven/Expedition/Anchors/<layoutId>.json`
  - `Assets/Resources/AdamsHaven/Expedition/Anchors/<layoutId>_mask.png`
- Runtime reader: `TowerMapAnchors.cs` (new, in `Assets/Scripts`).
- Layouts keep working without anchors (fall back to the current node data), so nothing breaks while plates are authored one by one.

## Data

### Mask PNG
128x72 pixels (one pixel is about 13 px on the 1672x941 plate). Single channel, three classes:

| Value | Meaning | Movement |
| --- | --- | --- |
| 255 (white) | Open ground | Full speed, 1 cost unit per step |
| 128 (grey) | Slow ground (dense forest, shallow water, marsh, scree) | 2 cost units per step |
| 0 (black) | Blocked (deep water, cliff, solid trees) | Not walkable |

Import settings: Read/Write on, no compression, point filter, linear.

### Anchor JSON
```json
{
  "layoutId": "silverwood_d1_forest_edge_hamlet",
  "version": 1,
  "camp": { "x": 0.52, "y": 0.88 },
  "anchors": [
    { "id": "a01", "x": 0.18, "y": 0.35, "radius": 0.03,
      "role": "poi", "allow": ["combat", "elite", "treasure", "shrine", "mystery"],
      "landmark": "", "theme": "briar", "note": "ruined arch clearing" },
    { "id": "a02", "x": 0.80, "y": 0.22, "radius": 0.03,
      "role": "town", "allow": ["merchant"], "landmark": "hamlet", "theme": "keep", "note": "hamlet" },
    { "id": "a09", "x": 0.30, "y": 0.10, "radius": 0.04,
      "role": "lair", "allow": ["lair"], "landmark": "", "theme": "briar", "note": "stag court" }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `x`, `y` | Normalised on the plate, y from the top (matches existing nodes) |
| `radius` | How close the party must be to enter it |
| `role` | `poi`, `town`, `landmark`, `lair` (exactly one lair) |
| `allow` | POI kinds the generator may put here (so a shrine is not placed on a lake shore) |
| `landmark` | Always-visible marker name, or empty for a hidden POI |
| `theme` | Dungeon theme (briar, keep, cave, marsh, crystal, ruin, mine, blight, heartwood) |

`camp` is the party's entrance. Landmarks are always visible in fog. Non-landmark anchors are drawn as fog silhouettes until the party is close (GDD section 4).

## The editor window

Layout dropdown (all `TowerForestLayouts` ids), then a canvas showing the plate with the mask overlaid as a translucent colour, plus tools on the left.

| Tool | Action |
| --- | --- |
| **Paint open / slow / block** | Brush with size slider. Left click paints, right click erases to the default class. |
| **Fill** | Flood fill a region with a class (for quickly blocking a lake). |
| **Auto-suggest** | Runs the colour analysis already used for the first-pass nodes and proposes a starting mask for you to correct. |
| **Camp** | Click to set the party entrance. |
| **Anchor** | Click to add. Choose role, allowed kinds, theme, landmark name in the side panel. |
| **Move / Delete** | Drag an anchor, or press Delete. |
| **Reachability check** | Highlights anchors the party cannot reach from camp over the mask. |
| **Cost preview** | Click two points to show the path and its ration cost, for tuning. |
| **Save** | Writes the JSON and PNG, then refreshes the asset database. |

Canvas: scroll to zoom, middle mouse drag to pan, toggle for mask overlay opacity, toggle for anchor labels.

### Validation (blocks Save with a warning, can be overridden)
- Exactly one camp and exactly one lair anchor.
- At least 11 `poi`/`town` anchors, and at least 3 `landmark` anchors.
- Every anchor reachable from camp over open or slow cells.
- Anchors at least 0.06 apart (normalised) so props do not overlap.
- Camp and lair at least 0.4 apart, so the run has a journey.
- Every `allow` set is non-empty.

## Runtime

1. `TowerForestLayouts.Get(id)` loads the anchors if present and builds the layout nodes from them: camp, lair, and the rest filled with POI kinds using the seeded generator (kinds picked from each anchor's `allow`). Layout seed is the run seed, so a different run puts different POIs on the same anchors.
2. Hidden links between nodes (still used by fog reveal and the current node-hop UI) are generated by path distance over the mask: each node links to its two to three nearest reachable neighbours, plus a minimum spanning tree so all nodes connect.
3. The mask is exposed as `TowerMapMask` with `Cost(x, y)`, `Walkable(x, y)` and a grid A* path function. This is what free movement (P1b) uses for distance cost and the traced trail.
4. Missing anchors or mask: fall back to today's nodes, with a console warning in the editor.

## Tests (added to `TowerSimulationTests`)
- Every Silverwood layout with anchors has one camp, one lair, 11+ POIs.
- All anchors reachable from camp.
- Generated nodes are deterministic for a given run seed and differ across seeds.
- Missing anchor files fall back without errors.

## Build order
1. Data classes, JSON loader, mask reader, `TowerMapMask` (runtime). Layouts load anchors when present.
2. Editor window: view, mask painting, anchors, save.
3. Auto-suggest and validation, reachability check, cost preview.
4. Tests.
5. Author the 15 plates (about 5 minutes each) and review in play.

## Open questions
- Mask resolution: 128x72 is coarse near thin bridges and shore paths. Use 192x108 if bridges get lost.
- Whether `radius` should be per kind (town larger than a camp) instead of per anchor.
- Whether landmarks can also be a POI (a landmark that is also a dungeon entrance). The spec assumes yes.
