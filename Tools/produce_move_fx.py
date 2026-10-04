"""Per-card battle effect layers from local ComfyUI MiniMax H3, and the moves.json the battle reads them from.

  python Tools/produce_move_fx.py el_piercing_shot      # every layer of one card (or a prefix with *, or 'all')
  python Tools/produce_move_fx.py manifest              # only rewrite Fx/Moves/moves.json
  python Tools/produce_move_fx.py repack                # re-cut every sheet from its kept render (no ComfyUI)

The layers and their wording live in Tools/move_specs.py (charge at the muzzle, travel to the target, impact).
Every layer is rendered on pure black (H3 first and last frame black), then turned into transparent art:
  - impact / charge 'sheet': 16 frames in a 4x4 grid, alpha from brightness -> Fx/Moves/<card>[_charge].png
  - travel 'beam': 4 horizontal strips stacked in one 1024x1024 texture, no fade at the ends -> <card>_travel.png
  - travel 'bolt': a projectile pointing right, 16 frames in a 4x4 grid -> <card>_travel.png
  - 'video' (long auras): H.264 with the matte stacked under the colour (Fx/StackedAlpha.shader) -> <card>.mp4
Work files: BattleMotion/fx_<card>[_<layer>]/ (h3_24.mp4 is kept: delete it to re-render).
H3 letterboxes a square or tall request (black bands above and below the picture), and a frame cut from that shows
the picture's straight edges as a box in battle. Every layer is therefore cropped to the band its light actually
occupies (content_box) before the matte, so the cell is the whole effect and nothing else, with the edge fade on the
effect's own edges.
"""
import colorsys
import json
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import FX_TAIL, WORK, h3, run, to_input, frames_of  # noqa: E402
from move_specs import MOVES, PALETTE  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
OUT = PROJECT / 'Assets/Resources/AdamsHaven/Fx/Moves'
INSIDE = ' The whole effect stays inside the frame, centered, with wide black margins on every side.'
FFMPEG = 'ffmpeg'
FLOOR = 0.18

# Kaela's first effects were written by hand before move_specs.py; their renders are kept as they are.
LEGACY = {
    'mv_frost_jab': dict(size=(704, 704), length=33, seed=611, floor=0.2,
        prompt='A sharp punch impact of ice: a bright white-blue star flash in the center, a cone of jagged ice shards and '
               'frost sparkles bursting outward, then the shards scatter and fade.'),
    'mv_rime_sweep': dict(size=(960, 544), length=41, seed=612, floor=0.2,
        prompt='A wide sweeping crescent arc of frost and swirling snow slashes across the frame from left to right, '
               'leaving a glittering trail of tiny ice crystals that drift and fade.'),
    'mv_shatter_hook': dict(size=(704, 704), length=41, seed=613, floor=0.2,
        prompt='A big cluster of blue ice crystals forms in the center with a white flash, then violently shatters: '
               'large crystal shards explode outward in every direction with frost mist and sparkles, then fade.'),
    'mv_whiteout_counter': dict(size=(704, 704), length=41, seed=614, floor=0.2,
        prompt='A swirling ring of white snow and icy wind spins around the center, flakes and glittering frost '
               'streaming in a circle, then it bursts outward and fades.'),
    'mv_permafrost_brace': dict(size=(704, 704), length=41, seed=615, floor=0.2,
        prompt='Frost creeps up from the bottom center and a ring of short ice spikes grows from the ground in a circle, '
               'glowing pale blue, then cracks into sparkles and fades.'),
    'mv_glacier_guard': dict(size=(576, 768), length=57, seed=616, floor=0.18, video=True,
        prompt='Translucent pale-blue ice crystal plates form one by one into a tall protective shell in the center, '
               'facets glinting with white light, the shell pulses once with blue light, then dissolves into drifting '
               'sparkles.'),
}


def jobs():
    """One H3 job per layer that has a prompt: fx_<card> (impact), fx_<card>_charge, fx_<card>_travel."""
    out = {}
    for card, m in MOVES.items():
        palette = PALETTE.get(m['unit'], '')
        for layer in ('charge', 'travel', 'impact'):
            spec = m.get(layer)
            if not spec:
                continue
            if layer == 'impact' and not spec.get('prompt'):
                legacy = LEGACY.get(card)
                if legacy:
                    out[f'fx_{card}'] = dict(card=card, layer='impact', file=card, legacy=True,
                                             kind='video' if legacy.get('video') else 'sheet', **legacy)
                continue
            kind = spec.get('kind') if layer == 'travel' else ('video' if spec.get('video') else 'sheet')
            size = {'beam': (1248, 352), 'bolt': (704, 352)}.get(kind) or tuple(spec.get('sheet_size', (704, 704)))
            prompt = spec['prompt'] + (f' The light is {palette}.' if palette else '')
            name = card if layer == 'impact' else f'{card}_{layer}'
            out[f'fx_{name}'] = dict(card=card, layer=layer, file=name, kind=kind, size=size,
                                     length=spec.get('length', 33 if layer != 'impact' else 41),
                                     seed=spec.get('seed') or 9000, floor=FLOOR, prompt=prompt)
    return out


JOBS = jobs()


def render(job, spec, d):
    if not (d / 'h3_24.mp4').exists():
        size = spec['size']
        black = to_input(Image.new('RGB', size, (0, 0, 0)), f'ahcg-black-{size[0]}x{size[1]}.png')
        tail = '' if spec['kind'] == 'beam' else INSIDE
        print('h3', job, flush=True)
        g = h3(spec['prompt'] + tail + FX_TAIL, size, spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}',
               black, black, rife=1)
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        outs = run(g, 'ahcg-movefx')
        shutil.copyfile(next(s for s in outs if s.suffix == '.mp4'), d / 'h3_24.mp4')
    return frames_of(d / 'h3_24.mp4', d / '_frames')


def matte(rgb, floor, fade, fade_x=True, round_=False, ends=0.0):
    """Colour with alpha from brightness: black is clear, the H3 noise floor is crushed, borders fade out.

    `round_` (sheets, bolts, videos): an ellipse inscribed in the frame fades the light out before it can reach an
    edge, so an effect that fills its frame (a light pillar, a mist sweep) never shows the frame's straight sides
    as a box. `ends` (beam strips): a soft fade at both ends instead of a hard cut."""
    h, w = rgb.shape[:2]
    alpha = np.clip((rgb.max(axis=2) - floor) / (1 - floor), 0, 1) ** 0.85
    yy, xx = np.mgrid[0:h, 0:w]
    sides = [xx, w - 1 - xx, yy, h - 1 - yy] if fade_x else [yy, h - 1 - yy]
    border = np.clip(np.minimum.reduce(sides) / (min(w, h) * fade), 0, 1)
    if round_:
        r = np.hypot((xx + .5) / w * 2 - 1, (yy + .5) / h * 2 - 1)
        k = np.clip((1.0 - r) / 0.62, 0, 1)                 # 1 inside r = .38, 0 at the inscribed ellipse
        border = border * k * k * (3 - 2 * k)               # smoothstep: no line where the frame would show
    if ends > 0:
        border = border * np.clip(np.minimum(xx, w - 1 - xx) / (w * ends), 0, 1) ** 1.5
    alpha = alpha * border
    colour = np.clip(rgb / np.maximum(rgb.max(axis=2, keepdims=True), 1e-3), 0, 1)
    # Keep some of the real brightness so cores stay white-hot while the edges keep their hue.
    colour = np.clip(colour * 0.55 + rgb * 0.45 / max(float(rgb.max()), 1e-3), 0, 1)
    return colour, alpha


def content_box(files, floor):
    """The rows and columns the effect ever lights up (over its active frames): H3 letterboxes square and tall
    requests, so the picture sits in a band with black above and below. Cropping to that band keeps the whole effect
    and drops the straight edges that would show as a box. None when the effect uses the whole frame."""
    a, b = active_range(files)
    pick = np.linspace(a, b, min(12, b - a + 1)).round().astype(int)
    peak = None
    for k in pick:
        rgb = np.asarray(Image.open(files[k]).convert('RGB'), np.float32) / 255
        lum = rgb.max(axis=2)
        peak = lum if peak is None else np.maximum(peak, lum)
    h, w = peak.shape
    lit = peak > max(0.06, floor * 0.5)
    rows = np.where(lit.mean(axis=1) > 0.004)[0]
    cols = np.where(lit.mean(axis=0) > 0.004)[0]
    if len(rows) == 0 or len(cols) == 0:
        return None
    pad_y, pad_x = int(h * .02), int(w * .02)
    y0, y1 = max(0, rows[0] - pad_y), min(h, rows[-1] + 1 + pad_y)
    x0, x1 = max(0, cols[0] - pad_x), min(w, cols[-1] + 1 + pad_x)
    if (y1 - y0) > h * .92 and (x1 - x0) > w * .92:
        return None
    return (x0, y0, x1, y1)


def active_range(files):
    lum = np.array([np.asarray(Image.open(f).convert('L'), np.float32).mean() for f in files])
    act = np.where(lum > max(2.0, lum.max() * 0.10))[0]
    return (int(act[0]), int(act[-1])) if len(act) else (0, len(files) - 1)


def cell(rgb_file, size, floor, fade, fade_x=True, round_=False, ends=0.0, box=None):
    img = Image.open(rgb_file).convert('RGB')
    if box:
        img = img.crop(box)
    rgb = np.asarray(img.resize(size, Image.Resampling.LANCZOS), np.float32) / 255
    colour, alpha = matte(rgb, floor, fade, fade_x, round_, ends)
    return Image.fromarray((np.dstack([colour, alpha]) * 255 + .5).astype(np.uint8), 'RGBA')


def sheet(job, spec, d, files):
    """16 frames, 4x4. Cells 256 wide (square / tall), 512 (wide strips) or 320 (other)."""
    a, b = active_range(files)
    pick = np.linspace(a, b, 16).round().astype(int)
    box = content_box(files, spec['floor'])
    w, h = (box[2] - box[0], box[3] - box[1]) if box else spec['size']
    if box:
        print('  crop', spec['file'], box, f'{w}x{h} of {spec["size"][0]}x{spec["size"][1]}')
    cw = 256 if h >= w else 512 if w / h > 2 else 320
    if spec['kind'] == 'bolt':
        cw = 256
    ch = round(cw * h / w)
    out = Image.new('RGBA', (cw * 4, ch * 4))
    for i, k in enumerate(pick):
        out.paste(cell(files[k], (cw, ch), spec['floor'], 0.06, round_=True, box=box), ((i % 4) * cw, (i // 4) * ch))
    return save(job, spec, d, out, 16, 4, 4)


def strip(job, spec, d, files):
    """A beam: 4 frames of the horizontal band, soft at both ends (not a hard cut), stacked in one texture."""
    a, b = active_range(files)
    pick = np.linspace(a + (b - a) * .25, a + (b - a) * .75, 4).round().astype(int)    # the steady middle
    out = Image.new('RGBA', (1024, 1024))
    for i, k in enumerate(pick):
        band = cell(files[k], (1024, 289), spec['floor'], 0.12, fade_x=False, ends=0.05)
        out.paste(band.crop((0, 16, 1024, 272)), (0, i * 256))
    return save(job, spec, d, out, 4, 1, 4)


def save(job, spec, d, img, frames, cols, rows):
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / f"{spec['file']}.png")
    prev = Image.new('RGB', img.size, (22, 26, 36))
    prev.paste(img, (0, 0), img)
    prev.save(d / f'{job}_preview.png')
    print('SHEET ->', OUT / f"{spec['file']}.png", f'{frames} frames')
    return dict(frames=frames, cols=cols, rows=rows)


def video(job, spec, d, files):
    """Colour on top, matte below, in one H.264 frame: Android decodes it in hardware, the shader recombines it."""
    a, b = active_range(files)
    stack = d / '_stack'
    shutil.rmtree(stack, ignore_errors=True)
    stack.mkdir()
    box = content_box(files, spec['floor'])
    w, h = (box[2] - box[0], box[3] - box[1]) if box else spec['size']
    if box:
        print('  crop', spec['file'], box)
    w2, h2 = 384, round(384 * h / w / 2) * 2
    for i, k in enumerate(range(a, b + 1)):
        img = Image.open(files[k]).convert('RGB')
        if box:
            img = img.crop(box)
        rgb = np.asarray(img.resize((w2, h2), Image.Resampling.LANCZOS), np.float32) / 255
        colour, alpha = matte(rgb, spec['floor'], 0.05, round_=True)
        frame = np.vstack([colour * alpha[..., None], np.dstack([alpha] * 3)])   # premultiplied colour over its matte
        Image.fromarray((frame * 255 + .5).astype(np.uint8), 'RGB').save(stack / f's_{i:04d}.png')
    OUT.mkdir(parents=True, exist_ok=True)
    dst = OUT / f"{spec['file']}.mp4"
    subprocess.run([FFMPEG, '-y', '-loglevel', 'error', '-framerate', '24', '-i', str(stack / 's_%04d.png'),
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(dst)], check=True)
    shutil.rmtree(stack)
    print('VIDEO ->', dst, 'frames', b - a + 1)
    return dict(frames=b - a + 1, cols=1, rows=1)


def tint_of(path):
    """The effect's own colour: alpha- and saturation-weighted mean of its pixels, at full brightness."""
    img = np.asarray(Image.open(path).convert('RGBA'), np.float32) / 255
    rgb, a = img[..., :3], img[..., 3]
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 1e-3, (mx - mn) / np.maximum(mx, 1e-3), 0)
    w = a * sat
    if w.sum() < 1:
        return [1, 1, 1, 1]
    mean = (rgb * w[..., None]).sum(axis=(0, 1)) / w.sum()
    h, s, _ = colorsys.rgb_to_hsv(*(float(v) for v in mean))
    r, g, b = colorsys.hsv_to_rgb(h, min(1, s * 1.15), 1)
    return [round(float(r), 3), round(float(g), 3), round(float(b), 3), 1]


def manifest():
    """Fx/Moves/moves.json: every card's layers with their sheets and runtime params (read by BattleCinematics)."""
    moves, missing = [], []
    for card, m in MOVES.items():
        entry = dict(card=card, unit=m['unit'], stage=not card.startswith(('ult_', 'aw_', 'default_')))
        tint = m.get('tint')
        for layer in ('charge', 'travel', 'impact'):
            spec = m.get(layer)
            if not spec:
                continue
            name = card if layer == 'impact' else f'{card}_{layer}'
            video = spec.get('video') or (layer == 'impact' and LEGACY.get(card, {}).get('video'))
            path = OUT / (name + ('.mp4' if video else '.png'))
            if not path.exists():
                missing.append(path.name)
                continue
            meta_file = WORK / f'fx_{name}' / 'layer.json'
            meta = json.loads(meta_file.read_text()) if meta_file.exists() else dict(frames=16, cols=4, rows=4)
            size = tuple(spec.get('sheet_size', (704, 704)))
            entry[layer] = dict(
                sheet=name, kind=spec.get('kind', 'video' if video else 'sheet'), at=spec.get('at', 'target'),
                size=spec.get('size', spec.get('width', 0)), life=spec.get('life', 0), lift=spec.get('lift', .5),
                width=spec.get('width', 0), grow=spec.get('grow', 0), arc=spec.get('arc', 0),
                stagger=spec.get('stagger', 0), count=spec.get('count', 1), each=spec.get('each', True),
                video=bool(video), hold=spec.get('hold', False), under=spec.get('under', False),
                bottom=bool(spec.get('under') or size[1] > size[0] or spec.get('lift', .5) <= .1),
                offset=spec.get('offset', [0, 0]), frames=meta['frames'], cols=meta['cols'], rows=meta['rows'])
            if tint is None and not video and layer in ('impact', 'travel'):
                tint = tint_of(path)
        entry['tint'] = tint or [1, 1, 1, 1]
        moves.append(entry)
    (OUT / 'moves.json').write_text(json.dumps(dict(version=1, moves=moves), indent=1))
    print('MANIFEST ->', OUT / 'moves.json', len(moves), 'cards;', len(missing), 'layers not rendered yet')


def main(which):
    if which != 'manifest':
        if which == 'all':
            todo = list(JOBS)
        elif which == 'repack':
            todo = [j for j in JOBS if (WORK / j / 'h3_24.mp4').exists()]
        elif which.endswith('*'):
            todo = [j for j in JOBS if JOBS[j]['card'].startswith(which[:-1])]
        else:
            todo = [j for j in JOBS if JOBS[j]['card'] == which]
        for job in todo:
            spec = JOBS[job]
            d = WORK / job
            d.mkdir(parents=True, exist_ok=True)
            (d / 'spec.json').write_text(json.dumps({k: v for k, v in spec.items() if k != 'legacy'}, indent=2, default=list))
            files = render(job, spec, d)
            make = video if spec['kind'] == 'video' else strip if spec['kind'] == 'beam' else sheet
            meta = make(job, spec, d, files)
            (d / 'layer.json').write_text(json.dumps(meta))
            shutil.rmtree(d / '_frames', ignore_errors=True)
    manifest()


if __name__ == '__main__':
    main(sys.argv[1])
