"""Per-move battle effects from local ComfyUI MiniMax H3 (2D rig pilot: Kaela).

  python Tools/produce_move_fx.py fx_mv_frost_jab     # one job, or a prefix with *, or 'all'

Every effect is rendered on pure black (H3 first and last frame black), then turned into transparent art:
  - kind 'sheet': 16 frames in a 4x4 grid, full colour, alpha from brightness -> Fx/Moves/<card id>.png
    (short hits and bursts: no video decoder, many at once, instant start)
  - kind 'video': H.264 with the matte stacked under the colour image (top half colour, bottom half alpha)
    -> Fx/Moves/<card id>.mp4, drawn by Fx/StackedAlpha.shader (longer effects; hardware decode on Android)
Work files: BattleMotion/<job>/ (h3_24.mp4 is kept: delete it to re-render).
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import FX_TAIL, WORK, h3, run, to_input, frames_of  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
OUT = PROJECT / 'Assets/Resources/AdamsHaven/Fx/Moves'
INSIDE = ' The whole effect stays inside the frame, centered, with wide black margins on every side.'
FFMPEG = 'ffmpeg'

JOBS = {
    'fx_mv_frost_jab': dict(card='mv_frost_jab', kind='sheet', size=(704, 704), length=33, seed=611, floor=0.2,
        prompt='A sharp punch impact of ice: a bright white-blue star flash in the center, a cone of jagged ice shards and '
               'frost sparkles bursting outward, then the shards scatter and fade.'),
    'fx_mv_rime_sweep': dict(card='mv_rime_sweep', kind='sheet', size=(960, 544), length=41, seed=612, floor=0.2,
        prompt='A wide sweeping crescent arc of frost and swirling snow slashes across the frame from left to right, '
               'leaving a glittering trail of tiny ice crystals that drift and fade.'),
    'fx_mv_shatter_hook': dict(card='mv_shatter_hook', kind='sheet', size=(704, 704), length=41, seed=613, floor=0.2,
        prompt='A big cluster of blue ice crystals forms in the center with a white flash, then violently shatters: '
               'large crystal shards explode outward in every direction with frost mist and sparkles, then fade.'),
    'fx_mv_whiteout_counter': dict(card='mv_whiteout_counter', kind='sheet', size=(704, 704), length=41, seed=614, floor=0.2,
        prompt='A swirling ring of white snow and icy wind spins around the center, flakes and glittering frost '
               'streaming in a circle, then it bursts outward and fades.'),
    'fx_mv_permafrost_brace': dict(card='mv_permafrost_brace', kind='sheet', size=(704, 704), length=41, seed=615, floor=0.2,
        prompt='Frost creeps up from the bottom center and a ring of short ice spikes grows from the ground in a circle, '
               'glowing pale blue, then cracks into sparkles and fades.'),
    # Longer effect as a transparent video: an ice armour shell forming, glittering and dissolving.
    'fx_mv_glacier_guard': dict(card='mv_glacier_guard', kind='video', size=(576, 768), length=57, seed=616, floor=0.18,
        prompt='Translucent pale-blue ice crystal plates form one by one into a tall protective shell in the center, '
               'facets glinting with white light, the shell pulses once with blue light, then dissolves into drifting '
               'sparkles.'),
}


def render(job, spec, d):
    if not (d / 'h3_24.mp4').exists():
        size = spec['size']
        black = to_input(Image.new('RGB', size, (0, 0, 0)), f'ahcg-black-{size[0]}x{size[1]}.png')
        print('h3', job, flush=True)
        g = h3(spec['prompt'] + INSIDE + FX_TAIL, size, spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}', black, black, rife=1)
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        outs = run(g, 'ahcg-movefx')
        shutil.copyfile(next(s for s in outs if s.suffix == '.mp4'), d / 'h3_24.mp4')
    return frames_of(d / 'h3_24.mp4', d / '_frames')


def matte(rgb, floor, fade):
    """Colour with alpha from brightness: black is clear, the H3 noise floor is crushed, borders fade out."""
    h, w = rgb.shape[:2]
    alpha = np.clip((rgb.max(axis=2) - floor) / (1 - floor), 0, 1) ** 0.85
    yy, xx = np.mgrid[0:h, 0:w]
    border = np.clip(np.minimum.reduce([xx, w - 1 - xx, yy, h - 1 - yy]) / (min(w, h) * fade), 0, 1)
    alpha = alpha * border
    colour = np.clip(rgb / np.maximum(rgb.max(axis=2, keepdims=True), 1e-3), 0, 1)
    # Keep some of the real brightness so cores stay white-hot while the edges keep their hue.
    colour = np.clip(colour * 0.55 + rgb * 0.45 / max(float(rgb.max()), 1e-3), 0, 1)
    return colour, alpha


def active_range(files):
    lum = np.array([np.asarray(Image.open(f).convert('L'), np.float32).mean() for f in files])
    act = np.where(lum > max(2.0, lum.max() * 0.10))[0]
    return (int(act[0]), int(act[-1])) if len(act) else (0, len(files) - 1)


def sheet(job, spec, d, files):
    a, b = active_range(files)
    pick = np.linspace(a, b, 16).round().astype(int)
    w, h = spec['size']
    cw = 256 if w == h else 320
    ch = round(cw * h / w)
    out = Image.new('RGBA', (cw * 4, ch * 4))
    for i, k in enumerate(pick):
        rgb = np.asarray(Image.open(files[k]).convert('RGB').resize((cw, ch), Image.Resampling.LANCZOS), np.float32) / 255
        colour, alpha = matte(rgb, spec['floor'], 0.06)
        cell = Image.fromarray((np.dstack([colour, alpha]) * 255 + .5).astype(np.uint8), 'RGBA')
        out.paste(cell, ((i % 4) * cw, (i // 4) * ch))
    OUT.mkdir(parents=True, exist_ok=True)
    out.save(OUT / f"{spec['card']}.png")
    prev = Image.new('RGB', out.size, (22, 26, 36))
    prev.paste(out, (0, 0), out)
    prev.save(d / f'{job}_preview.png')
    print('SHEET ->', OUT / f"{spec['card']}.png", 'frames', list(pick))


def video(job, spec, d, files):
    """Colour on top, matte below, in one H.264 frame: Android decodes it in hardware, the shader recombines it."""
    a, b = active_range(files)
    stack = d / '_stack'
    shutil.rmtree(stack, ignore_errors=True)
    stack.mkdir()
    w, h = spec['size']
    w2, h2 = 384, round(384 * h / w / 2) * 2
    for i, k in enumerate(range(a, b + 1)):
        rgb = np.asarray(Image.open(files[k]).convert('RGB').resize((w2, h2), Image.Resampling.LANCZOS), np.float32) / 255
        colour, alpha = matte(rgb, spec['floor'], 0.05)
        frame = np.vstack([colour * alpha[..., None], np.dstack([alpha] * 3)])   # premultiplied colour over its matte
        Image.fromarray((frame * 255 + .5).astype(np.uint8), 'RGB').save(stack / f's_{i:04d}.png')
    OUT.mkdir(parents=True, exist_ok=True)
    dst = OUT / f"{spec['card']}.mp4"
    subprocess.run([FFMPEG, '-y', '-loglevel', 'error', '-framerate', '24', '-i', str(stack / 's_%04d.png'),
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(dst)], check=True)
    shutil.rmtree(stack)
    print('VIDEO ->', dst, 'frames', b - a + 1)


def main(which):
    todo = list(JOBS) if which == 'all' else [j for j in JOBS if j.startswith(which[:-1])] if which.endswith('*') else [which]
    for job in todo:
        spec = JOBS[job]
        d = WORK / job
        d.mkdir(parents=True, exist_ok=True)
        (d / 'spec.json').write_text(json.dumps(spec, indent=2))
        files = render(job, spec, d)
        (sheet if spec['kind'] == 'sheet' else video)(job, spec, d, files)
        shutil.rmtree(d / '_frames', ignore_errors=True)


if __name__ == '__main__':
    main(sys.argv[1])
