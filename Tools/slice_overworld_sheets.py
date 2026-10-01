"""Slice the overworld stamp sheets from MAP_LAYER_ART_PROMPTS.md into one PNG per stamp.

Run:  python slice_overworld_sheets.py [source_dir]

Each sheet is cut on its known grid (cols x rows), then every cell is trimmed to its visible alpha and
padded. The pivot is the ground-contact point: horizontal centre of the cell, at the lowest solid pixel
(trunk base / footprint). Single-stamp images (poi_*, atlas_*, bridge_*) are trimmed the same way.
Seamless tiles are copied as they are. Originals are never modified.

Outputs (inside source_dir):
  sliced/<sheet>__<n>.png   one PNG per stamp (n = 1.. in row-major order)
  preview/<sheet>.png       pieces on a dark checkerboard for review
  manifest.json             per piece: size, pivot (0..1, Unity convention y from bottom), source cell
"""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"S:\AI\Game\Game Assets\expedition\overworld_v1")
EDGE = 6      # alpha kept when trimming (soft leaf edges)
SOLID = 160   # alpha counted as solid when finding the ground contact
PAD = 8

SHEETS = {   # name: (cols, rows)
    "canopy_oak_sheet": (2, 2), "canopy_pine_sheet": (2, 2), "canopy_birch_sheet": (2, 2),
    "canopy_silverwood_sheet": (2, 2), "canopy_willow_sheet": (2, 2), "canopy_deadwood_sheet": (2, 2),
    "undergrowth_sheet": (4, 2), "saplings_sheet": (3, 2), "rocks_sheet": (4, 2),
    "ford_stones_sheet": (2, 1), "fog_wisps_sheet": (2, 2), "tells_sheet": (2, 2),
}
SINGLES = ["bridge_wood", "bridge_stone", "poi_camp", "poi_lair", "poi_merchant", "poi_shrine", "poi_treasure",
           "poi_mystery", "poi_combat", "poi_elite", "atlas_tower_town", "atlas_silverwood_gate", "atlas_banner_conquered"]
TILES = ["ground_meadow", "ground_forest_floor", "ground_deep_moss", "ground_dirt_road", "ground_worn_grass",
         "ground_mud_marsh", "ground_scree", "ground_ruin_flagstone", "ground_blight", "water_shallow", "water_deep",
         "cloud_tile"]
FX = {"fog_wisps_sheet", "tells_sheet"}   # light/smoke: pivot is the centre, not the ground


def trim(cell, fx):
    a = cell[:, :, 3]
    ys, xs = np.nonzero(a > EDGE)
    if len(xs) == 0:
        return None, None
    x0, x1 = max(0, xs.min() - PAD), min(cell.shape[1], xs.max() + 1 + PAD)
    y0, y1 = max(0, ys.min() - PAD), min(cell.shape[0], ys.max() + 1 + PAD)
    piece = cell[y0:y1, x0:x1]
    h, w = piece.shape[:2]
    if fx:
        pivot = (0.5, 0.5)
    else:
        solid = np.nonzero(piece[:, :, 3] > SOLID)
        bottom = solid[0].max() if len(solid[0]) else h - 1
        cx = cell.shape[1] / 2 - x0
        pivot = (round(float(np.clip(cx / w, 0, 1)), 4), round(float(1 - (bottom + 1) / h), 4))
    return piece, pivot


def checker(w, h, s=16):
    yy, xx = np.mgrid[0:h, 0:w]
    v = np.where(((xx // s) + (yy // s)) % 2 == 0, 38, 52).astype(np.uint8)
    return np.dstack([v, v, v + 6, np.full_like(v, 255)])


def preview(name, pieces):
    if not pieces:
        return
    h = max(p.shape[0] for p in pieces) + 2 * PAD
    w = sum(p.shape[1] for p in pieces) + PAD * (len(pieces) + 1)
    canvas = Image.fromarray(checker(w, h))
    x = PAD
    for p in pieces:
        canvas.alpha_composite(Image.fromarray(p), (x, PAD))
        x += p.shape[1] + PAD
    (SRC / "preview").mkdir(exist_ok=True)
    canvas.convert("RGB").save(SRC / "preview" / f"{name}.png")


def main():
    out = SRC / "sliced"
    out.mkdir(exist_ok=True)
    manifest, problems = {}, []
    jobs = [(n, SHEETS[n]) for n in SHEETS] + [(n, (1, 1)) for n in SINGLES]
    for name, (cols, rows) in jobs:
        path = SRC / f"{name}.png"
        if not path.exists():
            problems.append(f"missing {path.name}")
            continue
        img = np.array(Image.open(path).convert("RGBA"))
        H, W = img.shape[:2]
        pieces = []
        for r in range(rows):
            for c in range(cols):
                cell = img[r * H // rows:(r + 1) * H // rows, c * W // cols:(c + 1) * W // cols]
                piece, pivot = trim(cell, name in FX)
                n = r * cols + c + 1
                if piece is None:
                    problems.append(f"{name}: cell {n} is empty")
                    continue
                key = f"{name}__{n}" if cols * rows > 1 else name
                Image.fromarray(piece).save(out / f"{key}.png")
                manifest[key] = {"size": [piece.shape[1], piece.shape[0]], "pivot": list(pivot),
                                 "sheet": name, "cell": [c, r]}
                pieces.append(piece)
        preview(name, pieces)
    for name in TILES:
        path = SRC / f"{name}.png"
        if not path.exists():
            problems.append(f"missing {path.name}")
            continue
        img = Image.open(path)
        img.save(out / f"{name}.png")
        manifest[name] = {"size": list(img.size), "tile": True}
    (SRC / "manifest.json").write_text(json.dumps(manifest, indent=1))
    print(f"{len(manifest)} pieces written to {out}")
    for p in problems:
        print("PROBLEM:", p)


if __name__ == "__main__":
    main()
