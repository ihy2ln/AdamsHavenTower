"""Turn a Celestium weapon concept (transparent PNG, front view) into a textured 3D prop GLB.

python Tools/weapon_from_art.py <spec name>        e.g. ghislaine-celestium-greatsword   (see SPECS below)
python Tools/weapon_from_art.py all

The silhouette is inflated into a closed mesh: every point gets the radius of the largest disc of the shape
that covers it, so thin parts (grips, shafts, the pen barrel) come out round and wide parts (blades) are
capped at `flat` px half-thickness. Both faces carry the concept art as their texture, so the prop matches the
2D design exactly from the battle camera. The long axis is turned to +Z with the tip on top; that is the
orientation Tools/build_battle_rig.py expects for an unflipped prop (butt at y=0, tip toward +Y).

Output: MeshyJobs/anime-battle-v2/weapons/<name>.glb (+ <name>_preview.png from Blender).
Stage 1 (this file, numpy/scipy/PIL) writes a height field; stage 2 (Tools/weapon_from_art_mesh.py, run in
Blender) builds and exports the mesh.
"""
import json, pathlib, subprocess, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = pathlib.Path(__file__).resolve().parent.parent
ART = pathlib.Path('S:/AI/Game/Game Assets/characters')
OUT = ROOT / 'MeshyJobs' / 'anime-battle-v2' / 'weapons'
BLENDER = pathlib.Path('S:/AI/Game Engine/Blender/blender.exe')

# tip: a pixel near the business end in the source image; it ends up on top (+Z).
# alpha: solid threshold; open: px opening that drops orbit rings and loose sparks; grow: px dilation that keeps
# thin shafts readable at game scale; flat: max half-thickness (px of the 1024 working image); step: grid px.
SPECS = {
    'ghislaine-celestium-greatsword': dict(src='Ghislaine/Celestium Weapons/ghislaine-fire-greatsword.png',
                                      tip=(512, 1500), alpha=120, open=4, grow=1, flat=16, step=3),
    'daisy-celestium-spear': dict(src='Daisy/Celestium Weapons/daisy-fire-spear.png',
                             tip=(995, 30), alpha=110, open=4, grow=4, flat=14, step=2),
    'elara-celestium-pen': dict(src='Elara/Celestium Weapons/elara-magic-fountain-pen-v2.png',
                                     tip=(60, 1480), alpha=120, open=8, grow=2, flat=40, step=3),
}
WORK = 1024  # long side of the working image / texture


def keep_main(mask):
    lab, n = ndimage.label(mask)
    if n == 0:
        raise SystemExit('empty mask')
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    big = sizes.max()
    return np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 0.12 * big])


def medial_radius(mask, dt):
    """Radius of the largest inscribed disc covering each pixel (approximate, on a geometric ladder)."""
    r = np.where(mask, np.minimum(dt, 1.0), 0.0)
    t = 1.0
    while t <= dt.max():
        core = dt >= t
        covered = ndimage.distance_transform_edt(~core) <= t
        r = np.where(covered & mask, np.maximum(r, t), r)
        t *= 1.25
    return r


def prep(name, spec):
    src = Image.open(ART / spec['src']).convert('RGBA')
    rgba = np.asarray(src).astype(np.float32)
    mask = rgba[..., 3] > spec['alpha']
    if spec['open']:
        mask = ndimage.binary_opening(mask, structure=disk(spec['open']))
    mask = ndimage.binary_fill_holes(keep_main(mask))
    if spec['grow']:
        mask = ndimage.binary_dilation(mask, structure=disk(spec['grow']))
    ys, xs = np.nonzero(mask)
    pts = np.stack([xs, ys], 1).astype(np.float64)
    c = pts.mean(0)
    w, v = np.linalg.eigh(np.cov((pts - c).T))
    axis = v[:, np.argmax(w)]
    if np.dot(np.array(spec['tip'], float) - c, axis) < 0:
        axis = -axis
    # rotate so `axis` points up the image (-y); PIL rotates counter-clockwise in degrees
    ang = np.degrees(np.arctan2(-axis[1], axis[0])) - 90.0
    big = Image.fromarray(rgba.astype(np.uint8))
    m_img = Image.fromarray((mask * 255).astype(np.uint8))
    big = big.rotate(-ang, resample=Image.BICUBIC, expand=True)
    m_img = m_img.rotate(-ang, resample=Image.NEAREST, expand=True)
    m = np.asarray(m_img) > 127
    ys, xs = np.nonzero(m)
    pad = 6
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), xs.max() + pad + 1, ys.max() + pad + 1)
    big, m_img = big.crop(box), m_img.crop(box)
    scale = WORK / max(big.size)
    size = (max(8, round(big.size[0] * scale)), max(8, round(big.size[1] * scale)))
    tex = np.asarray(big.resize(size, Image.LANCZOS)).astype(np.float32)
    m = np.asarray(m_img.resize(size, Image.BILINEAR)) > 127
    m = ndimage.binary_fill_holes(m)
    # Edge fill: off-shape texels take the nearest on-shape colour so filtering never pulls in the background.
    _, (iy, ix) = ndimage.distance_transform_edt(~m, return_indices=True)
    rgb = tex[..., :3][iy, ix]
    dt = ndimage.distance_transform_edt(m)
    R = medial_radius(m, dt)
    h = np.sqrt(np.maximum(0.0, 2 * R * dt - dt * dt))
    h = np.where(R > 0, h * np.minimum(1.0, spec['flat'] / np.maximum(R, 1e-6)), 0.0)
    OUT.mkdir(parents=True, exist_ok=True)
    tex_path = OUT / f'{name}_tex.png'
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(tex_path)
    data = OUT / f'{name}_field.npz'
    np.savez_compressed(data, mask=m, height=h.astype(np.float32), step=spec['step'])
    print(f'PREP {name}: {size[0]}x{size[1]} px, rotated {ang:.1f} deg, max half-thickness {h.max():.1f} px', flush=True)
    return data, tex_path


def disk(r):
    y, x = np.ogrid[-r:r + 1, -r:r + 1]
    return x * x + y * y <= r * r


def build(name):
    data, tex = prep(name, SPECS[name])
    out = OUT / f'{name}.glb'
    cmd = [str(BLENDER), '-b', '--factory-startup', '--python', str(ROOT / 'Tools' / 'weapon_from_art_mesh.py'), '--',
           str(data), str(tex), str(out)]
    res = subprocess.run(cmd, capture_output=True, text=True)
    for line in res.stdout.splitlines():
        if line.startswith('WEAPON') or 'Error' in line:
            print(line, flush=True)
    if res.returncode != 0 or not out.exists():
        print(res.stdout[-3000:], res.stderr[-3000:])
        raise SystemExit(f'blender failed for {name}')


if __name__ == '__main__':
    names = list(SPECS) if sys.argv[1:] == ['all'] else sys.argv[1:]
    for n in names:
        build(n)
