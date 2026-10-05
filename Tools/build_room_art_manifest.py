"""Write the room art manifest the Tower reads (TT 10.5.0b): which picture a building shows at which rank, and the part
of the picture that holds the room (its interior band). The game crops that band to the room's real on-screen size at
the picture's own proportions, so no picture is ever stretched or squeezed.

  python Tools/build_room_art_manifest.py

Rules (owner, 2026-10-05: pictures keep their ratio, rooms fit the picture; crop while a band lacks its own art):
- Rooms/<type>_F.png serves rank F, _E rank E, _D ranks D to SSR (C and up reuse D until they get their own art).
- A file named <type>_<rank>.png for any rank F..SSR (e.g. kitchen_C.png) takes that rank and up, until the next file.
- The furnished interiors (living_interior_v1, kitchen_interior_v1) take the house and kitchen from rank B; the
  guild_hall_F_v2 cutaway replaces guild_hall_F.
- `align` places a narrower crop across the band (0 left, 0.5 centre, 1 right); the Gate hall hugs its door (1).
- `bays` is how many tower cells the picture was painted for (its band width / height against a 2.0 x 2.2 cell).
Writes Assets/Resources/AdamsHaven/TowerPresentation/Rooms/room_art.json.
"""
import json
from pathlib import Path
from PIL import Image

ROOMS = Path(__file__).resolve().parent.parent / 'Assets' / 'Resources' / 'AdamsHaven' / 'TowerPresentation' / 'Rooms'
RANKS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']
CELL_ASPECT = 2.0 / 2.2
# The painted buildings: the room interior sits between the stone base and the roof.
BAND = {'left': 0.035, 'right': 0.965, 'bottom': 0.14, 'top': 0.71}
FULL = {'left': 0.0, 'right': 1.0, 'bottom': 0.0, 'top': 1.0}
# The Gate's hall: the gate sits in the outer (east) wall, so a narrower crop hugs that side.
GATE_BAND = {'left': 0.0, 'right': 1.0, 'bottom': 0.15, 'top': 0.9, 'align': 1.0}
# Picture families named <prefix>_<rank>.png that belong to another building, with their own band.
ALIASES = {'gate_hall': ('gate', GATE_BAND)}
SPECIAL = {
    'living_interior_v1': ('house', 'B', {'left': 0.0, 'right': 1.0, 'bottom': 0.0, 'top': 1.0}),
    'kitchen_interior_v1': ('kitchen', 'B', FULL),
    'guild_hall_F_v2': ('guild_hall', 'F', {'left': 0.04, 'right': 0.96, 'bottom': 0.145, 'top': 0.695}),
    # The Gate's cell: a short hallway with the gate in its outer (east) wall; the west Gate draws it mirrored.
    'gate_hall': ('gate', 'F', GATE_BAND),   # rank F; gate_hall_E .. gate_hall_SSR take the ranks above
}
# Superseded by a special picture at the same rank; gate_F is a guardroom painting, not a gate.
REPLACED = {'guild_hall_F', 'gate_F'}


def entry(stem, building, rank, band):
    w, h = Image.open(ROOMS / (stem + '.png')).size
    aspect = (band['right'] - band['left']) * w / ((band['top'] - band['bottom']) * h)
    return {'file': stem, 'building': building, 'fromRank': RANKS.index(rank) + 1, 'toRank': 9,
            'left': band['left'], 'right': band['right'], 'bottom': band['bottom'], 'top': band['top'],
            'width': w, 'height': h, 'bays': max(1, round(aspect / CELL_ASPECT)),
            # Where a narrower room sits across the band: 0 left, 0.5 centre, 1 right (the Gate keeps its door side).
            'align': band.get('align', 0.5)}


def main():
    rows = []
    for png in sorted(ROOMS.glob('*.png')):
        stem = png.stem
        # West PNGs are mirrored companion exports; the renderer mirrors the east gate at runtime.
        if stem == 'gate_hall_west' or stem.startswith('gate_hall_west_'):
            continue
        if stem in SPECIAL:
            building, rank, band = SPECIAL[stem]
            rows.append(entry(stem, building, rank, band))
            continue
        if stem in REPLACED or '_' not in stem:
            continue
        building, rank = stem.rsplit('_', 1)
        if rank not in RANKS:
            continue
        if building in ALIASES:
            building, band = ALIASES[building]
            rows.append(entry(stem, building, rank, band))
            continue
        rows.append(entry(stem, building, rank, BAND))
    # Each picture serves its rank up to the rank before the building's next picture.
    by_building = {}
    for r in rows:
        by_building.setdefault(r['building'], []).append(r)
    for pics in by_building.values():
        pics.sort(key=lambda r: r['fromRank'])
        for a, b in zip(pics, pics[1:]):
            a['toRank'] = b['fromRank'] - 1
    rows.sort(key=lambda r: (r['building'], r['fromRank']))
    out = ROOMS / 'room_art.json'
    out.write_text(json.dumps({'rooms': rows}, indent=1), encoding='utf-8')
    for r in rows:
        print(f"{r['building']:14s} {RANKS[r['fromRank'] - 1]:>3s}-{RANKS[r['toRank'] - 1]:<3s} {r['file']:22s} painted for {r['bays']} bay(s)")
    print('wrote', out, len(rows), 'pictures')


if __name__ == '__main__':
    main()
