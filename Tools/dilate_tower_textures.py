"""Fill the empty space around the UV islands of the Tower 3D chibi textures (push-pull dilation).

The UVGame atlas from build_tower_rig.py is thousands of small islands on black. Mipmaps average those
islands with the black gaps, so at Tower zoom (where the 2048 texture is sampled at mip 3-5) the models
render dark and muddy. Filling every empty texel with the colour of its nearest islands keeps each mip
level clean. Covered texels are never changed.

Usage: python Tools/dilate_tower_textures.py [id ...]     (default: every Resources/AdamsHaven/TowerChibi3D/<id>)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'TowerChibi3D'
JOBS = Path(r'S:\AI\Game\Game Assets\characters\chibi-meshy-tasks-v1')


def push_pull(rgb, weight):
    """rgb HxWx3 float, weight HxW in 0..1. Returns rgb with every zero-weight texel filled from coarser levels."""
    if min(rgb.shape[:2]) <= 1 or weight.min() > 0:
        return rgb
    h, w = rgb.shape[:2]
    ph, pw = h + h % 2, w + w % 2
    rgb_p = np.zeros((ph, pw, 3), np.float64); rgb_p[:h, :w] = rgb
    wt_p = np.zeros((ph, pw), np.float64); wt_p[:h, :w] = weight
    wsum = wt_p.reshape(ph // 2, 2, pw // 2, 2).sum(axis=(1, 3))
    csum = (rgb_p * wt_p[..., None]).reshape(ph // 2, 2, pw // 2, 2, 3).sum(axis=(1, 3))
    coarse = np.where(wsum[..., None] > 0, csum / np.maximum(wsum, 1e-9)[..., None], 0)
    coarse = push_pull(coarse, np.minimum(wsum, 1.0))
    up = coarse.repeat(2, axis=0).repeat(2, axis=1)[:h, :w]
    return rgb * weight[..., None] + up * (1 - weight[..., None])


def dilate(model_id):
    textures = list((MODELS / model_id).glob('texture_0_*_albedo.png'))
    if not textures:
        print(model_id, ': no albedo texture'); return
    tex_path = textures[0]
    img = Image.open(tex_path).convert('RGB')
    rgb = np.asarray(img, np.float64) / 255.0
    covered = rgb.sum(axis=2) > 0.04                       # the baked islands and their bake margin
    mask_path = JOBS / model_id / 'uv_mask.png'
    if mask_path.exists():
        mask = np.asarray(Image.open(mask_path).convert('L').resize(img.size)) > 127
        covered |= mask                                     # dark-but-real texels (black clothing) stay
    filled = push_pull(rgb, covered.astype(np.float64))
    out = np.clip(filled * 255.0 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(out, 'RGB').save(tex_path)
    print(f'{model_id}: filled {100 * (1 - covered.mean()):.0f}% empty texels in {tex_path.name}')


if __name__ == '__main__':
    ids = sys.argv[1:] or sorted(p.name for p in MODELS.iterdir() if p.is_dir())
    for i in ids:
        dilate(i)
