# Celestium Gate art: east and west facing, flat-roof entrance hall, ranks F to SSR

Task for Codex: generate the Celestium Gate pictures for Adams Haven Tower Mode, one per rank, F to SSR, facing east
and facing west. Save them where the game reads them, then rebuild the room art manifest.

## What the Gate is

- The Tower is a side-view cutaway, like Fallout Shelter: a row of rooms on each floor, with the front walls removed.
- The ground floor ends in a **Celestium Gate** at each end:
  - The **east** Gate is on the right end and opens outward to the right.
  - The **west** Gate is on the left end and opens outward to the left.
- Each Gate fills one building slot (one room cell) as a **short entrance hallway with a FLAT roof**, with the gate set
  into the hallway's outer wall. It is an entrance, not a house: no pitched roof, no gables, no chimney.
- It is a **gate**: a tall pointed arch with glowing blue crystal doors. It is **never a round vault door** (no bolts, no
  gears, no metal wheel).
- The Gate ranks up from F to SSR like every building. Each rank must look clearly richer than the one below, while
  staying the same building: same framing, same proportions, gate in the same place.

## Deliverables

18 PNG files, every one with a **transparent background**, cropped tight to the building:

| Rank | East-facing (gate in the RIGHT wall) | West-facing (gate in the LEFT wall) |
| --- | --- | --- |
| F | `gate_hall.png` | `gate_hall_west.png` |
| E | `gate_hall_E.png` | `gate_hall_west_E.png` |
| D | `gate_hall_D.png` | `gate_hall_west_D.png` |
| C | `gate_hall_C.png` | `gate_hall_west_C.png` |
| B | `gate_hall_B.png` | `gate_hall_west_B.png` |
| A | `gate_hall_A.png` | `gate_hall_west_A.png` |
| S | `gate_hall_S.png` | `gate_hall_west_S.png` |
| SS | `gate_hall_SS.png` | `gate_hall_west_SS.png` |
| SSR | `gate_hall_SSR.png` | `gate_hall_west_SSR.png` |

- Save to: `Assets/Resources/AdamsHaven/TowerPresentation/Rooms/`. The east files replace the current ones.
- The west files can be exact horizontal mirrors of the east files. That is the preferred way: it keeps both Gates
  identical. Render west separately only if the art has text or asymmetric details that look wrong mirrored.
- Size: render at 1536 x 1152 (4:3), then remove the background and crop to the building. Keep the full roof parapet
  and the stone base in the crop.

## How to keep all nine ranks the same building

1. Render rank **F** first from the base prompt plus the F line.
2. Render E to SSR as **edits of the F picture** (image-to-image or reference-image edit): attach F, and use the base
   prompt plus that rank's line plus: *"Keep exactly the camera angle, framing, proportions, flat roof and the gate's
   place in the right wall; only upgrade the materials and dressing."*
3. Reject any picture where the roof turns pitched, the gate moves out of the outer wall, the gate turns into a round
   vault door, or the hallway grows into a big hall.

## Base prompt (east-facing)

```
Side-view cutaway of a short entrance hallway for a fantasy tower, front wall removed so we look straight into it,
straight-on camera at room height, like a one-room building cutaway in a mobile tower-sim game. One building slot wide:
a short, wide hallway (about 4:3), with a stone base under the floor. FLAT stone roof with a low crenellated parapet:
no gables, no pitched roof, no chimney, no attic. Plank or stone floor with a long runner rug leading to the RIGHT.
The RIGHT end wall is the tower's outer wall and holds the Celestium Gate: a tall pointed Gothic arch framed in dark
slate and polished gold trim, with two glowing blue crystal doors, small blue crystal ornaments on the gold trim and a
crystal at the peak. The gate fills the right wall from floor to ceiling, set into it and opening outward to the right,
toward the outside world. The LEFT end of the hallway is open, joining the rest of the tower. It is a gate, never a round
vault door: no bolts, no gears, no metal wheel. No people. Hand-painted stylized fantasy game art, dark timber and
moonlit crystal palette, soft rim light. Plain flat light grey background.
```

For a west-facing render (only if not mirroring): swap every RIGHT and LEFT, and write "opening outward to the left".

## Rank lines (append "Rank X: <line>" to the base prompt)

| Rank | Line |
| --- | --- |
| F | Rough dark stone and old timber, one hanging Celestium lantern, a worn blue runner, plain gold trim on the gate. |
| E | The same hall repaired and cleaner: iron bands bolted along the gate's frame, a second hanging lantern, a straw mat by the door. |
| D | Dressed square-cut stone walls, a polished plank floor, a spear rack against the back wall, a blue banner, iron sconces. |
| C | A fortified gatehouse hall: thicker crenellated parapet, gold-trimmed stone courses, small blue crystal sconces, a banner with the Heart's crest, a stone bench. |
| B | Carved stone arches along the back wall, smooth slate floor tiles, crystal lamps, gold trim on the gate frame, a guard's armour stand. |
| A | Grand: pale and dark stone with gold inlay, two glowing crystal braziers flanking the gate, an ornate gold gate frame studded with crystals, a long blue carpet. |
| S | Palatial: polished marble walls with gold filigree, thin glowing blue crystal veins in the stone, a crystal chandelier, a richly carved parapet. |
| SS | Radiant: slender crystal pillars along the hall, glowing crystal shards floating near the gate, gold and silver trim everywhere, a luminous blue floor inlay. |
| SSR | Legendary Celestium: walls veined with glowing blue crystal, the gate pure radiant crystal with a halo of light, a gold crown-like crest on the parapet, sparkling light motes. |

## Background removal

- Remove the flat grey background to full transparency. **Do not use a subject-detection matte (BiRefNet and the like)
  on these.** It eats the cutaway's interior, roof and floor.
- Use a flood fill from the picture's edges on colours close to the background (tolerance about 18 of 255), then soften
  the edge by one pixel and crop to the opaque area. The script does exactly this:
  `python Tools/key_flat_background.py <in.png> <out.png> 18`.

## After saving the files

1. Run `python Tools/build_room_art_manifest.py` from the project root.
   - It lists `gate_hall` (F) and `gate_hall_E` .. `gate_hall_SSR` under building `gate`, one rank each.
   - The Gate's crop band is `GATE_BAND` in that script (bottom 0.15, top 0.90, align 1.0, so it hugs the gate side).
   - If the new pictures put the floor or the gate's peak elsewhere, adjust `GATE_BAND`.
2. The game draws the **east** picture for the east Gate and **mirrors it for the west Gate**
   (`TowerArtDirector.cs`, the `room.type == "gate"` branch). The `gate_hall_west*.png` files are not read yet.
   - If you render true west-facing art, add a `gate_west` alias in `ALIASES` in `Tools/build_room_art_manifest.py`.
   - Then in `TowerArtDirector` pick `RoomArtFor("gate_west", room.level)` for `room.x < TowerRules.CoreX` and skip the
     mirror. Keep the change that small.
3. Check it in Play (Unity, scene `Assets/Scenes/AdamsHavenTower.unity`): both Gates show the hall, gates facing
   outward, and `UPGRADE` on a Gate's room card changes the picture at every rank.
4. Commit only these files: the Rooms PNGs and their `.meta` files, `room_art.json`, and any script you changed.
   Another session is editing other Tower files at the same time.

## Acceptance

- [ ] 9 east-facing ranks, F to SSR. The same building at every rank, visibly richer each step.
- [ ] Flat roof with a parapet on every rank; no pitched roofs or chimneys.
- [ ] A tall arched Celestium Gate with crystal doors in the outer wall, floor to ceiling, opening outward; never a
      vault door.
- [ ] West-facing versions (mirrors are fine) with the gate in the left wall, opening left.
- [ ] Transparent backgrounds with interiors intact, cropped tight, saved under the exact names above.
- [ ] The manifest rebuilt; in Play, both Gates show their rank's picture.
