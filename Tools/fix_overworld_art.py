"""Post-process the sliced overworld art: make ground tiles seamless and re-anchor stamps.

Run: python fix_overworld_art.py [overworld_v1 dir]
Reads sliced_orig/ (pristine copy of sliced/) and writes the fixed files into sliced/.
 - Tiles: cross-fade each tile with a half-offset copy of itself so the wrap seam disappears
   (variance-preserving blend, so the blend band keeps its contrast).
 - Stamps (trees, bushes, rocks, props): pad horizontally so the ground-contact point sits at the horizontal
   centre, which is where Unity pins the stamp's foot.
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"S:\AI\Game\Game Assets\expedition\overworld_v1")
ORIG, OUT = SRC / "sliced_orig", SRC / "sliced"
TILES = ["ground_meadow", "ground_forest_floor", "ground_deep_moss", "ground_dirt_road", "ground_worn_grass",
         "ground_mud_marsh", "ground_scree", "ground_ruin_flagstone", "ground_blight", "water_shallow", "water_deep",
         "cloud_tile"]
BAND = 0.32


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def seamless(img, variance_preserving=True):
    a = img.astype(np.float32)
    h, w = a.shape[:2]
    b = np.roll(a, (h // 2, w // 2), axis=(0, 1))
    xs = np.minimum(np.arange(w), w - 1 - np.arange(w)) / (BAND * w)
    ys = np.minimum(np.arange(h), h - 1 - np.arange(h)) / (BAND * h)
    m = (smooth(ys)[:, None] * smooth(xs)[None, :])[:, :, None]
    if variance_preserving:
        mean = a.reshape(-1, a.shape[2]).mean(0)
        out = ((m * a + (1 - m) * b) - mean) / np.sqrt(m * m + (1 - m) * (1 - m)) + mean
    else:
        out = m * a + (1 - m) * b
    return np.clip(out, 0, 255).astype(np.uint8)


def seam_error(a):
    a = a[:, :, :3].astype(np.float32)
    return (float(np.abs(a[:, 0] - a[:, -1]).mean()), float(np.abs(a[0] - a[-1]).mean()),
            float(np.abs(np.roll(a, a.shape[1] // 2, 1)[:, a.shape[1] // 2 - 1] - a[:, a.shape[1] // 2 - 1]).mean()))


def recenter(img):
    a = img[:, :, 3]
    h, w = a.shape
    solid = np.nonzero(a > 160)
    if len(solid[0]) == 0:
        return img
    bottom = solid[0].max()
    band = (solid[0] > bottom - max(6, int(0.06 * h)))
    cx = float(solid[1][band].mean())
    # keep the contact point within the middle third of the stamp so a wide object isn't thrown off by one root
    cx = float(np.clip(cx, w * 0.33, w * 0.67))
    left = int(round(cx))
    right = w - left
    pad = abs(left - right)
    if left < right:
        return np.pad(img, ((0, 0), (pad, 0), (0, 0)))
    return np.pad(img, ((0, 0), (0, pad), (0, 0)))


def main():
    for f in sorted(ORIG.glob("*.png")):
        name = f.stem
        img = np.array(Image.open(f).convert("RGBA"))
        if name in TILES:
            rgb = seamless(img if name == "cloud_tile" else img[:, :, :3], variance_preserving=name != "cloud_tile")
            if name != "cloud_tile":
                rgb = np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])
            Image.fromarray(rgb).save(OUT / f.name)
            print(f"{name}: seam edge diff {seam_error(img)[0]:.1f}/{seam_error(img)[1]:.1f} -> {seam_error(rgb)[0]:.1f}/{seam_error(rgb)[1]:.1f}")
        elif "tell" in name or "fog_wisps" in name:
            Image.fromarray(img).save(OUT / f.name)
        else:
            Image.fromarray(recenter(img)).save(OUT / f.name)
    print("done")


if __name__ == "__main__":
    main()
