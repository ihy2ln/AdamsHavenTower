"""Turn a Meshy base-colour atlas into flat anime cel colours that match the 2D character art.

  python Tools/anime_cel_texture.py <atlas.png|jpg> <source_art.png|webp> <out.png> [--colors 28] [--strength 1.0]

Meshy paints soft, photographic light into the atlas (gradients, AO, speculars). Anime art uses a handful of flat
fills plus one hard shadow tone per material. This pass:
  1  edge-preserving smoothing inside the UV islands (iterated median + bilateral-style range weights), which removes
     the painted gradients and noise but keeps colour borders sharp
  2  a palette taken from the 2D art itself (k-means in Lab over the opaque pixels, ink lines excluded)
  3  every texel snaps to its nearest palette colour when it is close enough (Lab distance); far-off texels
     (details the painting does not show, e.g. the back) are posterised instead of forced onto a wrong colour
  4  dark ink texels are kept dark so seams/lines from the atlas survive; islands are dilated into the padding
The output keeps the atlas size and UV layout, so it drops straight back into the GLB/FBX material.
"""
import sys, argparse
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.cluster.vq import kmeans2
from scipy.spatial import cKDTree


def srgb_to_lab(rgb):
    c = rgb / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    L = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], -1)


def island_mask(rgb):
    """Texels that belong to UV islands: Meshy pads with pure black / flat fill outside the islands."""
    pad = rgb.reshape(-1, 3)[0]
    diff = np.abs(rgb.astype(np.int16) - pad.astype(np.int16)).sum(-1)
    m = diff > 6
    m = ndimage.binary_opening(m, iterations=1)
    return m


def smooth(rgb, mask, iters=3, size=5, sigma_r=18.0):
    """Masked edge-preserving smoothing: median for noise, then range-weighted box blur (bilateral-ish)."""
    out = rgb.astype(np.float32)
    for _ in range(iters):
        med = np.stack([ndimage.median_filter(out[..., k], size=size) for k in range(3)], -1)
        out = np.where(mask[..., None], med, out)
    lab = srgb_to_lab(out)
    acc = np.zeros_like(out)
    wsum = np.zeros(out.shape[:2], np.float32)
    r = 3
    for dy in range(-r, r + 1, 1):
        for dx in range(-r, r + 1, 1):
            sh_lab = np.roll(lab, (dy, dx), (0, 1))
            sh = np.roll(out, (dy, dx), (0, 1))
            shm = np.roll(mask, (dy, dx), (0, 1))
            d = np.sqrt(((lab - sh_lab) ** 2).sum(-1))
            w = np.exp(-(d / sigma_r) ** 2) * shm
            acc += sh * w[..., None]
            wsum += w
    res = acc / np.maximum(wsum, 1e-6)[..., None]
    return np.where(mask[..., None], res, out)


def art_palette(src_path, k):
    im = Image.open(src_path).convert('RGBA')
    im.thumbnail((640, 640))
    a = np.asarray(im).astype(np.float32)
    px = a[a[..., 3] > 200][:, :3]
    lab = srgb_to_lab(px)
    px = px[lab[:, 0] > 14]  # drop ink lines; they are re-added from the atlas
    rng = np.random.default_rng(7)
    sample = px[rng.choice(len(px), min(len(px), 40000), replace=False)]
    cent, lbl = kmeans2(srgb_to_lab(sample), k, minit='++', seed=7, iter=30)
    # back to rgb: mean of the members (exact, no inverse Lab needed)
    pal = np.array([sample[lbl == i].mean(0) if (lbl == i).any() else sample[0] for i in range(k)])
    counts = np.bincount(lbl, minlength=k)
    keep = counts > len(sample) * 0.002
    return pal[keep]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('atlas'); ap.add_argument('art'); ap.add_argument('out')
    ap.add_argument('--colors', type=int, default=28)
    ap.add_argument('--snap', type=float, default=22.0, help='max Lab distance to snap onto the art palette')
    ap.add_argument('--levels', type=int, default=6, help='posterise levels for texels the art does not cover')
    ap.add_argument('--mask', help='UV coverage mask (white = inside an island); default guesses from the padding colour')
    a = ap.parse_args()

    img = Image.open(a.atlas).convert('RGB')
    rgb = np.asarray(img)
    mask = (np.asarray(Image.open(a.mask).convert('L')) > 127) if a.mask else island_mask(rgb)
    sm = smooth(rgb, mask)
    pal = art_palette(a.art, a.colors)
    pal_lab = srgb_to_lab(pal)
    lab = srgb_to_lab(sm)
    tree = cKDTree(pal_lab)
    d, idx = tree.query(lab.reshape(-1, 3))
    d = d.reshape(mask.shape); idx = idx.reshape(mask.shape)
    snapped = pal[idx]
    q = 255.0 / (a.levels - 1)
    poster = np.round(sm / q) * q
    out = np.where((d < a.snap)[..., None], snapped, poster)
    # keep ink: texels much darker than their smoothed neighbourhood stay as dark line colour
    L0 = srgb_to_lab(rgb.astype(np.float32))[..., 0]
    ink = (L0 < 22) & mask
    out = np.where(ink[..., None], np.minimum(out, rgb * 0.6 + 10), out)
    # light clean-up of salt-and-pepper after snapping (mode filter approximated by median on the index map)
    idx_m = ndimage.median_filter(np.where(d < a.snap, idx, -1), size=3)
    ok = (idx_m >= 0) & mask & ~ink
    out = np.where(ok[..., None], pal[np.clip(idx_m, 0, len(pal) - 1)], out)
    out = np.where(mask[..., None], out, rgb)
    # dilate islands into the padding so mip-mapping does not pull in the background
    grown = out.copy()
    m = mask.copy()
    for _ in range(8):
        nm = ndimage.binary_dilation(m)
        ring = nm & ~m
        if not ring.any():
            break
        for k in range(3):
            ch = np.where(m, grown[..., k], 0)
            s = ndimage.uniform_filter(ch, 3)
            c = ndimage.uniform_filter(m.astype(np.float32), 3)
            grown[..., k] = np.where(ring, s / np.maximum(c, 1e-6), grown[..., k])
        m = nm
    Image.fromarray(np.clip(grown, 0, 255).astype(np.uint8)).save(a.out)
    cover = float((d[mask] < a.snap).mean())
    print(f'CEL palette={len(pal)} snapped={cover:.2%} ink={float(ink[mask].mean()):.2%} -> {a.out}')


if __name__ == '__main__':
    main()
