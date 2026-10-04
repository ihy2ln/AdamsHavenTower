"""Pre-rendered anime battle clips for a party fighter, from local ComfyUI (Qwen Image Edit 2.1 -> MiniMax H3 -> BiRefNet).

  python Tools/produce_fighter_clips.py kaela keys      # pose keys: Qwen edits of the guard key -> review/keys.jpg
  python Tools/produce_fighter_clips.py kaela motion    # H3 first/last-frame segments between keys
  python Tools/produce_fighter_clips.py kaela matte     # BiRefNet alpha per frame (on the ComfyUI server) + clean-up
  python Tools/produce_fighter_clips.py kaela pack      # atlas pages + clips.json -> Resources/AdamsHaven/BattleClips/<unit>/
  python Tools/produce_fighter_clips.py kaela cine      # cine_frame.png: the guard at the match-cut framing (produce_cine.py)
  python Tools/produce_fighter_clips.py kaela clips     # motion + matte + pack, once the keys are approved
An optional 3rd argument limits a stage to one key or action (e.g. `motion AH_attack_basic`), or for motion and matte
to one segment of an action (`motion AH_block:0`).

Spec: Tools/fighter_clips/<unit>.json. Work files: BattleMotion/fighter_<unit>/ (outside Assets):
  keys/<key>.png (RGB on the flat background, canvas size) + keys/<key>_a.png (alpha), review/keys.jpg
  segments/<action>_<i>/ key_first.png, key_last.png, spec.json, api-h3.json, h3_24.mp4, rgba/f_0000.png ...
Every segment keeps its first/last keys, prompt and spec, so it can be re-rendered by another model (Seedance) later:
drop a replacement h3_24.mp4 in and re-run matte + pack (Tools/export_seedance_handoff.py bundles them).

Each action starts and ends on the shared guard key, so clips join without a pop, and the contact frame is a segment
boundary, so it is known by construction. Unity reads clips.json (frames, contact, foot anchors) through BattleClips.cs.
"""
import hashlib
import json
import shutil
import subprocess
import sys
import urllib.request
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage, signal

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import URL, frames_of, h3, node, qwen_edit, run, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
SPECS = Path(__file__).resolve().parent / 'fighter_clips'
ENEMY_SPECS = Path(__file__).resolve().parent / 'enemy_clips'      # Tools/build_enemy_clip_specs.py
WORK = PROJECT / 'BattleMotion'
OUT = PROJECT / 'Assets/Resources/AdamsHaven/BattleClips'
CHARS = Path('S:/AI/Game/Game Assets/characters')
BACKDROP = PROJECT / 'Assets/Resources/AdamsHaven/Fx/cine_bg.png'
CINE_CENTER_X, CINE_CENTER_Y, CINE_HALF = .55, 1.35, .95     # BattleMode.CineCenterX/Y, CineHalf (BattleCinematics.cs)
PAGE, PAD = 2048, 4                                          # Android caps Resources textures at 2048
CLIENT = 'ahcg-clips'

KEY_PROMPT = ('Image 1 shows the character standing in her fighting stance, image 2 is her character sheet and image 3 '
              'her weapons. Draw exactly the same woman as in image 1, {look}, with the same face, hair, outfit, weapons, '
              'proportions, size and drawing style, in a new pose: {pose}. Keep the same camera and the same three-quarter '
              'view facing the right side of the picture, the same scale, and her feet on the same ground line as in '
              'image 1. The whole figure is visible. Plain flat even grey background, no shadow, no floor, no effects, '
              'no motion lines, no text. Clean high-detail cel-shaded anime game character art, sharp lineart.')
# Monsters (Tools/build_enemy_clip_specs.py, spec "kind": "monster"): painted creatures facing the left side of the
# picture, references are the bestiary's own views (MonsterPrompts/<family>/<form>/).
MONSTER_KEY_PROMPT = ('Image 1 shows the creature standing in its battle stance. Draw exactly the same creature as in image 1, {look}, with the same anatomy, '
                      'colours, crystal growths, core orb, proportions, size and painting style, in a new pose: {pose}. '
                      'Keep the same camera and the same side view facing the left side of the picture, the same scale, '
                      'and its feet on the same ground line as in image 1. The whole creature is visible. Plain flat even '
                      'grey background, no shadow, no floor, no effects, no motion lines, no text. Polished hand-painted '
                      'fantasy RPG creature art, crisp clean linework.')
MONSTER_MOTION_TAIL = ('Fantasy RPG battle creature animation, full body, locked-off static camera, no zoom, no pan, no '
                       'cuts. It stays in place on the same ground line. Plain flat even grey background, no shadow, no '
                       'floor, no effects, no particles, no text. Polished hand-painted creature, consistent design.')
MOTION_TAIL = ('Anime fighting game character animation, full body, locked-off static camera, no zoom, no pan, no cuts. '
               'She stays in place on the same ground line. Plain flat even grey background, no shadow, no floor, no '
               'effects, no particles, no text. Crisp cel-shaded anime character, consistent design.')


# ---------------------------------------------------------------- spec + small helpers ------------
def load(unit):
    path = SPECS / f'{unit}.json'
    if not path.exists():
        path = ENEMY_SPECS / f'{unit}.json'
    spec = json.loads(path.read_text(encoding='utf-8'))
    d = WORK / (f'enemy_{unit}' if spec.get('kind') == 'monster' else f'fighter_{unit}')
    for sub in ('keys', 'keys/raw', 'segments', 'review'):
        (d / sub).mkdir(parents=True, exist_ok=True)
    return spec, d


def digest(*parts):
    h = hashlib.sha1()
    for p in parts:
        h.update(p.read_bytes() if isinstance(p, Path) else json.dumps(p, sort_keys=True).encode())
    return h.hexdigest()[:16]


def flat(img, bg):
    """RGBA -> RGB on a flat colour (references and keys go to the models without alpha)."""
    img = img.convert('RGBA')
    out = Image.new('RGBA', img.size, tuple(bg) + (255,))
    out.alpha_composite(img)
    return out.convert('RGB')


def f32(img):
    return np.asarray(img, np.float32) / 255.0


def u8(a):
    return (np.clip(a, 0, 1) * 255 + .5).astype(np.uint8)


def segment_dir(d, action, i):
    return d / 'segments' / f'{action}_{i}'


# ---------------------------------------------------------------- BiRefNet on the ComfyUI server ---
EMBEDDED = Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe')


def server_up():
    try:
        urllib.request.urlopen(URL + '/queue', timeout=5)
        return True
    except OSError:
        return False


def masks_local(paths, tag):
    """Same BiRefNet weights through ComfyUI's Python directly (Tools/matte_frames.py): no server needed."""
    tmp = WORK / '_matte' / tag
    shutil.rmtree(tmp, ignore_errors=True)
    tmp.mkdir(parents=True)
    outs = [tmp / f'a_{i:04d}.png' for i in range(len(paths))]
    lst = tmp / 'list.txt'
    lst.write_text('\n'.join(f'{p}|{o}' for p, o in zip(paths, outs)), encoding='utf-8')
    subprocess.run([str(EMBEDDED), '-I', str(Path(__file__).resolve().parent / 'matte_frames.py'), str(lst)], check=True)
    masks = [f32(Image.open(o)) for o in outs]
    shutil.rmtree(tmp)
    return masks


def masks_for_images(paths, tag):
    if not server_up():
        return masks_local(paths, tag)
    g = {'m': node('LoadBackgroundRemovalModel', bg_removal_name='birefnet.safetensors')}
    for i, p in enumerate(paths):
        name = to_input(Image.open(p).convert('RGB'), f'ahcg-clip-{tag}-{i}.png')
        g[f'l{i}'] = node('LoadImage', image=name)
        g[f'r{i}'] = node('RemoveBackground', bg_removal_model=['m', 0], image=[f'l{i}', 0])
        g[f'i{i}'] = node('MaskToImage', mask=[f'r{i}', 0])
        g[f's{i}'] = node('SaveImage', images=[f'i{i}', 0], filename_prefix=f'AdamsHaven/BattleClips/{tag}_{i:03d}')
    files = run(g, CLIENT)
    out = []
    for i in range(len(paths)):
        f = next(f for f in files if f.name.startswith(f'{tag}_{i:03d}_'))
        out.append(f32(Image.open(f).convert('L')))
    return out


def masks_for_video(mp4, tag):
    g = {
        'm': node('LoadBackgroundRemovalModel', bg_removal_name='birefnet.safetensors'),
        'v': node('VHS_LoadVideoPath', video=str(mp4), force_rate=0, custom_width=0, custom_height=0, frame_load_cap=0,
                  skip_first_frames=0, select_every_nth=1, format='None'),
        'r': node('RemoveBackground', bg_removal_model=['m', 0], image=['v', 0]),
        'i': node('MaskToImage', mask=['r', 0]),
        's': node('SaveImage', images=['i', 0], filename_prefix=f'AdamsHaven/BattleClips/{tag}'),
    }
    files = sorted(run(g, CLIENT), key=lambda f: f.name)
    return [f32(Image.open(f).convert('L')) for f in files]


def clean_alpha(a):
    """Hysteresis: soft edge kept only next to solid body; specks away from the figure dropped."""
    a = np.where(a < .04, 0, a)
    hard = a > .5
    lab, n = ndimage.label(hard)
    if n == 0:
        return np.zeros_like(a)
    sizes = ndimage.sum(hard, lab, range(1, n + 1))
    keep = np.zeros(n + 1, bool)
    keep[1:] = sizes >= max(sizes.max() * .015, 40)
    body = keep[lab]
    near = ndimage.binary_dilation(body, iterations=10)
    return np.where(near, a, 0).astype(np.float32)


def decontaminate(rgb, a, bg):
    """Undo the flat background showing through soft edges: C = (I - (1 - a) B) / a."""
    a3 = a[..., None]
    fg = (rgb - (1 - a3) * bg) / np.maximum(a3, 1e-3)
    return np.clip(np.where(a3 > .995, rgb, fg), 0, 1)


def figure_metrics(a):
    """Bottom row, top row and the x-centre of the feet (bottom 6% of the figure) of an alpha mask."""
    ys, xs = np.nonzero(a > .5)
    top, bottom = ys.min(), ys.max()
    band = ys >= bottom - .06 * (bottom - top)
    return dict(top=int(top), bottom=int(bottom), feet_x=float(xs[band].mean()),
                left=int(xs.min()), right=int(xs.max()), cx=float(xs.mean()))


def torso_x(a):
    """x-centre of the head and upper body (12-45% of the figure's height from the top)."""
    m = figure_metrics(a)
    h = m['bottom'] - m['top']
    ys, xs = np.nonzero(a[m['top'] + int(.12 * h):m['top'] + int(.45 * h)] > .5)
    return float(xs.mean())


def place(rgb, a, spec, scale, dx, dy):
    """Scale + translate a straight-alpha figure onto the canvas; returns (rgb on bg, alpha)."""
    cw, ch = spec['canvas']
    rgba = Image.fromarray(np.dstack([u8(rgb), u8(a)]), 'RGBA').convert('RGBa')
    if abs(scale - 1) > 1e-4:
        rgba = rgba.resize((round(rgba.width * scale), round(rgba.height * scale)), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBa', (cw, ch), (0, 0, 0, 0))
    canvas.paste(rgba, (round(dx), round(dy)))
    out = f32(canvas.convert('RGBA'))
    bg = np.array(spec['bg'], np.float32) / 255
    alpha = out[..., 3]
    return out[..., :3] * alpha[..., None] + bg * (1 - alpha[..., None]), alpha


# ---------------------------------------------------------------- stage: keys ---------------------
GUARD_PROMPT = ('Image 1 shows the character, image 2 her weapon. Draw the same woman from image 1, {look}, with exactly '
                'the same face, hair, outfit and accessories, and the same proportions and build. She carries the weapon '
                'from image 2. Full body, three-quarter view turned toward the right side of the picture, standing in a '
                'ready fighting stance: {stance}. The arms are clear of the torso. The whole figure is visible from the top '
                'of the head to the soles with a small margin. Plain flat light grey background, even soft lighting, no '
                'shadow, no ground, no effects. Clean high-detail anime game character art, sharp lineart.')


def guard_source(spec, d):
    """The stance the guard key is cut from: an approved file, or a Qwen stance from the battle sheet + weapon."""
    g = spec['guard']
    if 'source' in g:
        return PROJECT / g['source']
    raw = d / 'keys/raw' / f"guard_{g['seed']}.png"
    prompt = GUARD_PROMPT.format(look=spec['look'], stance=g['stance'])
    meta = raw.with_suffix('.json')
    if raw.exists() and meta.exists() and json.loads(meta.read_text()).get('prompt') == prompt:
        return raw
    names = [to_input(flat(Image.open(CHARS / spec['refs'][k]), (200, 200, 200)), f"ahcg-clip-{spec['unit']}-stance{i}.png")
             for i, k in enumerate(('sheet', 'weapon'))]
    print('qwen guard stance', g['seed'], flush=True)
    out = run(qwen_edit(names, prompt, (1024, 1536), g['seed'], f"AdamsHaven/BattleClips/{spec['unit']}_stance"), CLIENT)
    Image.open(out[0]).convert('RGB').save(raw)
    meta.write_text(json.dumps(dict(prompt=prompt), indent=2))
    return raw


def guard_key(spec, d):
    """The guard key G: the approved stance (or a Qwen stance) matted and placed on the canvas."""
    src = guard_source(spec, d)
    meta = d / 'keys' / 'guard.json'
    sig = digest(src, spec['canvas'], spec['bg'], spec['body_px'], spec['body_x'], spec['feet_y'])
    if (d / 'keys/guard.png').exists() and meta.exists() and json.loads(meta.read_text()).get('sig') == sig:
        return
    print('guard key <-', src.name, flush=True)
    img = Image.open(src).convert('RGB')
    a = clean_alpha(masks_for_images([src], 'guard_src')[0])
    rgb = decontaminate(f32(img), a, np.array(spec['guard'].get('source_bg', spec['bg']), np.float32) / 255)
    m = figure_metrics(a)
    scale = spec['body_px'] / (m['bottom'] - m['top'])
    dx = spec['body_x'] - m['feet_x'] * scale
    dy = spec['feet_y'] - m['bottom'] * scale
    key, alpha = place(rgb, a, spec, scale, dx, dy)
    Image.fromarray(u8(key)).save(d / 'keys/guard.png')
    Image.fromarray(u8(alpha)).save(d / 'keys/guard_a.png')
    # High-resolution guard for the match-cut zoom: the source cutout, trimmed, with its foot anchor and scale.
    l, t, r, b = Image.fromarray(u8(a)).getbbox()
    hi = Image.fromarray(np.dstack([u8(rgb), u8(a)]), 'RGBA').crop((l, t, r, b))
    hi.save(d / 'keys/guard_hi.png')
    meta.write_text(json.dumps(dict(sig=sig, hi=dict(footX=m['feet_x'] - l, footY=m['bottom'] + 1 - t,
                                                     ppu=(m['bottom'] - m['top']) / 2)), indent=2))


def keys(spec, d, only=None):
    guard_key(spec, d)
    bg = tuple(spec['bg'])
    g_name = to_input(Image.open(d / 'keys/guard.png').convert('RGB'), f"ahcg-clip-{spec['unit']}-guard.png")
    refs = [g_name]
    files = [Path(r) for r in spec['ref_files']] if 'ref_files' in spec else         [CHARS / spec['refs']['sheet'], CHARS / spec['refs']['weapon']]
    for i, r in enumerate(files):
        refs.append(to_input(flat(Image.open(r), bg), f"ahcg-clip-{spec['unit']}-ref{i}.png"))
    template = MONSTER_KEY_PROMPT if spec.get('kind') == 'monster' else KEY_PROMPT
    for name, k in spec['keys'].items():
        if only and name != only:
            continue
        prompt = template.format(look=spec['look'], pose=k['pose'])
        raw = d / 'keys/raw' / f"{name}_{k['seed']}.png"
        raw_meta = raw.with_suffix('.json')
        # A raw key stays valid while its prompt and seed (in the file name) do: re-framing the guard only re-registers.
        if raw.exists() and not raw_meta.exists():          # rendered before the meta file existed: adopt it
            raw_meta.write_text(json.dumps(dict(prompt=prompt), indent=2))
        if not (raw.exists() and raw_meta.exists() and json.loads(raw_meta.read_text()).get('prompt') == prompt):
            print('qwen key', name, k['seed'], flush=True)
            out = run(qwen_edit(refs, prompt, spec['canvas'], k['seed'], f"AdamsHaven/BattleClips/{spec['unit']}_{name}"), CLIENT)
            Image.open(out[0]).convert('RGB').resize(tuple(spec['canvas']), Image.Resampling.LANCZOS).save(raw)
            raw.with_name(raw.stem + '_a.png').unlink(missing_ok=True)
            raw_meta.write_text(json.dumps(dict(prompt=prompt), indent=2))
    # One BiRefNet job for every raw key still without a matte (each job waits its turn in a shared queue).
    todo = [d / 'keys/raw' / f"{n}_{k['seed']}.png" for n, k in spec['keys'].items() if not only or n == only]
    todo = [r for r in todo if r.exists() and not r.with_name(r.stem + '_a.png').exists()]
    if todo:
        for r, a in zip(todo, masks_for_images(todo, f"{spec['unit']}_keys")):
            Image.fromarray(u8(clean_alpha(a))).save(r.with_name(r.stem + '_a.png'))
    for name, k in spec['keys'].items():
        if only and name != only:
            continue
        raw = d / 'keys/raw' / f"{name}_{k['seed']}.png"
        reg = digest(raw, d / 'keys/guard.png', k, spec['body_x'], spec['feet_y'], REGISTER_VERSION)
        meta = d / 'keys' / f'{name}.json'
        if (d / f'keys/{name}.png').exists() and meta.exists() and json.loads(meta.read_text()).get('reg') == reg:
            continue
        register_key(spec, d, name, k, raw, reg)
    review_keys(spec, d)


REGISTER_VERSION = 3


def head_template(d):
    """Grey crop around the guard's head (ears, face, eyepatch): what auto-scaling looks for in every other key."""
    g = f32(Image.open(d / 'keys/guard.png').convert('L'))
    a = f32(Image.open(d / 'keys/guard_a.png'))
    m = figure_metrics(a)
    h = m['bottom'] - m['top']
    ys, xs = np.nonzero(a[m['top']:m['top'] + int(.04 * h)] > .5)
    cx = int(xs.mean())
    half = int(.14 * h)
    return g[m['top']:m['top'] + int(.22 * h), max(0, cx - half):cx + half]


def ncc_best(img, tpl):
    """Peak normalised cross-correlation of tpl inside img (both grey float arrays)."""
    th, tw = tpl.shape
    if th >= img.shape[0] or tw >= img.shape[1]:
        return -1.0
    t = tpl - tpl.mean()
    tn = np.sqrt((t * t).sum()) + 1e-6
    num = signal.fftconvolve(img, t[::-1, ::-1], mode='valid')
    ones = np.ones_like(tpl)
    s1 = signal.fftconvolve(img, ones, mode='valid')
    s2 = signal.fftconvolve(img * img, ones, mode='valid')
    var = s2 - s1 * s1 / tpl.size
    flat = var < (.03 ** 2) * tpl.size          # flat background windows: FFT noise over ~0 variance
    ncc = num / (np.sqrt(np.maximum(var, 1e-9)) * tn)
    return float(np.clip(np.where(flat, -1, ncc), -1, 1).max())


def auto_scale(d, raw_grey):
    """Qwen redraws the figure at its own size: find the scale at which the guard's head best matches this key."""
    tpl = head_template(d)
    small = lambda x, s: np.asarray(Image.fromarray(u8(x)).resize((max(8, round(x.shape[1] * s)), max(8, round(x.shape[0] * s))), Image.Resampling.LANCZOS), np.float32) / 255  # noqa: E731
    img = small(raw_grey, .5)
    best = (-1.0, 1.0)
    for s in np.arange(.70, 1.42, .02):
        score = ncc_best(img, small(tpl, .5 * s))
        best = max(best, (score, float(s)))
    return 1 / best[1], best[0]       # key -> canvas scale (the key's head is s times the guard's)


def register_key(spec, d, name, k, raw, reg):
    """Matte the Qwen output, match the guard's scale, and put its feet back on the guard's ground line."""
    img = Image.open(raw).convert('RGB')
    bg = np.array(spec['bg'], np.float32) / 255
    alpha_file = raw.with_name(raw.stem + '_a.png')
    if alpha_file.exists():
        a = f32(Image.open(alpha_file))
    else:
        a = clean_alpha(masks_for_images([raw], f"{spec['unit']}_{name}")[0])
        Image.fromarray(u8(a)).save(alpha_file)
    rgb = decontaminate(f32(img), a, bg)
    m = figure_metrics(a)
    ndx, ndy, nscale = k.get('nudge', [0, 0, 1])
    # Scale: an explicit 'scale', else the guard's head found in the key (when the match is confident), else the
    # figure's height (or width, for lying poses) against the guard's, times the pose's expected ratio from the spec.
    score, how = None, 'spec'
    if 'width' in k:
        s, how = spec['body_px'] * k['width'] / (m['right'] - m['left']), 'width'
    else:
        s, how = spec['body_px'] * k.get('height', 1.0) / (m['bottom'] - m['top']), 'height'
    if k.get('scale') is not None:
        s, how = float(k['scale']), 'spec'
    elif k.get('auto_scale', True) and 'width' not in k:
        s_head, score = auto_scale(d, f32(img.convert('L')))
        if score >= .55:
            s, how = s_head, 'head'
    s *= nscale
    print(f'  register {name}: scale {s:.3f} by {how}' + (f' (head match {score:.2f})' if score is not None else ''), flush=True)
    dx = dy = 0.0
    if k.get('grounded', True):
        dy = spec['feet_y'] - m['bottom'] * s
    anchor = k.get('anchor_x', 'feet')
    if anchor == 'feet':
        dx = spec['body_x'] - m['feet_x'] * s
    elif anchor == 'center':
        dx = figure_metrics(f32(Image.open(d / 'keys/guard_a.png')))['cx'] - m['cx'] * s
    elif anchor == 'torso':      # weapon on the ground would drag a feet anchor: line up the upper body instead
        dx = torso_x(f32(Image.open(d / 'keys/guard_a.png'))) - torso_x(a) * s
    key, alpha = place(rgb, a, spec, s, dx + ndx, dy + ndy)
    Image.fromarray(u8(key)).save(d / f'keys/{name}.png')
    Image.fromarray(u8(alpha)).save(d / f'keys/{name}_a.png')
    (d / 'keys' / f'{name}.json').write_text(json.dumps(dict(reg=reg, seed=k['seed'], raw=raw.name, scale=round(s, 4),
                                                             head_match=score), indent=2))


def review_keys(spec, d):
    """Every key over a 30% onion skin of the guard, with the ground line (green) and the guard's head line (blue)."""
    names = ['guard'] + list(spec['keys'])
    cw, ch = spec['canvas']
    cell = 384
    s = cell / cw
    cols = 5
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cell, rows * (cell + 24)), (24, 26, 32))
    g = f32(Image.open(d / 'keys/guard.png'))
    g_top = figure_metrics(f32(Image.open(d / 'keys/guard_a.png')))['top']
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        p = d / f'keys/{name}.png'
        if not p.exists():
            continue
        k = f32(Image.open(p))
        ka = f32(Image.open(d / f'keys/{name}_a.png'))[..., None]
        mix = k * ka + (g * .3 + k * .7) * (1 - ka) if name != 'guard' else k
        tile = Image.fromarray(u8(mix)).resize((cell, round(ch * s)), Image.Resampling.LANCZOS)
        x, y = (i % cols) * cell, (i // cols) * (cell + 24)
        sheet.paste(tile, (x, y))
        draw.line([(x, y + spec['feet_y'] * s), (x + cell, y + spec['feet_y'] * s)], fill=(60, 220, 90))
        draw.line([(x, y + g_top * s), (x + cell, y + g_top * s)], fill=(80, 140, 255))
        draw.text((x + 6, y + cell + 4), name, fill=(235, 235, 240))
    sheet.save(d / 'review/keys.jpg', quality=92)
    print('review ->', d / 'review/keys.jpg')


# ---------------------------------------------------------------- stage: motion -------------------
def split_only(only):
    """'AH_block' -> ('AH_block', None); 'AH_block:1' -> ('AH_block', 1)."""
    if only and ':' in only:
        action, i = only.split(':')
        return action, int(i)
    return only, None


def motion(spec, d, only=None):
    only, only_seg = split_only(only)
    for action, act in spec['actions'].items():
        if only and action != only:
            continue
        for i, (a, b, frames, seed, text) in enumerate(act['segments']):
            if only_seg is not None and i != only_seg:
                continue
            assert frames % 8 == 1, f'{action}[{i}]: H3 needs 8n+1 frames, got {frames}'
            sd = segment_dir(d, action, i)
            sd.mkdir(parents=True, exist_ok=True)
            first, last = d / f'keys/{a}.png', d / f'keys/{b}.png'
            if frames == 1:
                snap(spec, d, sd, b)
                continue
            if spec.get('kind') == 'monster':
                prompt = f"The creature is {spec['look']}, facing the left side of the picture. {text} {MONSTER_MOTION_TAIL}"
            else:
                prompt = f"The character is {spec['look']}, facing the right side of the picture. {text} {MOTION_TAIL}"
            seg = dict(action=action, index=i, first=a, last=b, frames=frames, seed=seed, prompt=prompt,
                       size=spec['canvas'], fps=24, model='MiniMax H3 fl2va int8 + turbo 4-step',
                       keys=digest(first, last))
            old = json.loads((sd / 'spec.json').read_text()) if (sd / 'spec.json').exists() else None
            if (sd / 'h3_24.mp4').exists() and old == seg:
                continue
            shutil.copyfile(first, sd / 'key_first.png')
            shutil.copyfile(last, sd / 'key_last.png')
            names = [to_input(Image.open(p).convert('RGB'), f"ahcg-clip-{spec['unit']}-{action}-{i}-{t}.png")
                     for p, t in ((first, 'first'), (last, 'last'))]
            print('h3', action, i, f'{a}->{b}', frames, 'frames', flush=True)
            g = h3(prompt, spec['canvas'], frames, seed, f"AdamsHaven/BattleClips/{spec['unit']}_{action}_{i}",
                   names[0], names[1], rife=1)
            (sd / 'api-h3.json').write_text(json.dumps(g, indent=2))
            outs = run(g, CLIENT)
            shutil.copyfile(next(s for s in outs if s.suffix == '.mp4'), sd / 'h3_24.mp4')
            (sd / 'spec.json').write_text(json.dumps(seg, indent=2))
            shutil.rmtree(sd / 'rgba', ignore_errors=True)


def snap(spec, d, sd, key):
    """A 1-frame segment is a cut straight to its key (blockstun snaps into the pose; H3 would fling a long weapon
    on the way): two frames of the matted key, no H3 render, nothing to matte."""
    sig = dict(snap=key, keys=digest(d / f'keys/{key}.png', d / f'keys/{key}_a.png'))
    out = sd / 'rgba'
    if (sd / 'snap.json').exists() and json.loads((sd / 'snap.json').read_text()) == sig and out.exists():
        return
    bg = np.array(spec['bg'], np.float32) / 255
    rgb, al = f32(Image.open(d / f'keys/{key}.png').convert('RGB')), f32(Image.open(d / f'keys/{key}_a.png'))
    shutil.rmtree(out, ignore_errors=True)
    out.mkdir()
    frame = Image.fromarray(np.dstack([u8(decontaminate(rgb, al, bg)), u8(al)]), 'RGBA')
    for j in range(2):
        frame.save(out / f'f_{j:04d}.png')
    (sd / 'snap.json').write_text(json.dumps(sig, indent=2))
    print('snap', sd.name, '->', key, flush=True)


# ---------------------------------------------------------------- stage: matte --------------------
def matte(spec, d, only=None):
    only, only_seg = split_only(only)
    bg = np.array(spec['bg'], np.float32) / 255
    for action, act in spec['actions'].items():
        if only and action != only:
            continue
        contact = act.get('contact')
        for i, (a, b, frames, *_rest) in enumerate(act['segments']):
            if only_seg is not None and i != only_seg:
                continue
            if frames == 1:          # a snap: motion wrote its frames from the key
                continue
            sd = segment_dir(d, action, i)
            mp4 = sd / 'h3_24.mp4'
            if not mp4.exists():
                print('  no motion yet:', sd.name)
                continue
            if (sd / 'rgba').exists() and (sd / 'rgba').stat().st_mtime >= mp4.stat().st_mtime:
                continue
            print('matte', sd.name, flush=True)
            files = frames_of(mp4, sd / '_frames')
            rgbs = [f32(Image.open(f).convert('RGB')) for f in files]
            tag = f"{spec['unit']}_{sd.name}"
            alphas = masks_for_video(mp4, tag) if server_up() else masks_local(files, tag)
            if len(alphas) != len(rgbs):
                raise RuntimeError(f'{sd.name}: {len(rgbs)} frames but {len(alphas)} masks')
            alphas = [clean_alpha(x) for x in alphas]
            n = len(rgbs)
            protect = set()
            if contact and b == contact:
                protect |= {n - 2, n - 1}
            if contact and a == contact:
                protect |= {0, 1}
            alphas = temporal_median(alphas, rgbs, protect)
            fix = colour_match(rgbs, alphas, d, a, b)
            out = sd / 'rgba'
            shutil.rmtree(out, ignore_errors=True)
            out.mkdir()
            for j, (rgb, al) in enumerate(zip(rgbs, alphas)):
                gain, off = fix(j / max(1, n - 1))
                fg = np.clip(decontaminate(rgb, al, bg) * gain + off, 0, 1)
                Image.fromarray(np.dstack([u8(fg), u8(al)]), 'RGBA').save(out / f'f_{j:04d}.png')
            shutil.rmtree(sd / '_frames')


def temporal_median(alphas, rgbs, protect):
    """3-tap median of alpha where the picture is still (kills edge shimmer, keeps fast motion sharp)."""
    out = [x.copy() for x in alphas]
    for i in range(1, len(alphas) - 1):
        if i in protect:
            continue
        med = np.median(np.stack(alphas[i - 1:i + 2]), axis=0)
        still = np.abs(rgbs[i + 1] - rgbs[i - 1]).max(axis=2) < .06
        out[i] = np.where(still, med, alphas[i])
    return out


def colour_match(rgbs, alphas, d, a, b):
    """H3's VAE round trip shifts colour a little: pull the first/last frame back to their keys, lerped between."""
    def fit(rgb, al, key):
        k = f32(Image.open(d / f'keys/{key}.png'))
        ka = f32(Image.open(d / f'keys/{key}_a.png'))
        m = (al > .9) & (ka > .9)
        if m.sum() < 500:
            return np.ones(3, np.float32), np.zeros(3, np.float32)
        fm, fs = rgb[m].mean(0), rgb[m].std(0)
        km, ks = k[m].mean(0), k[m].std(0)
        gain = np.clip(ks / np.maximum(fs, 1e-3), .9, 1.1)
        return gain, np.clip(km - fm * gain, -.06, .06)
    g0, o0 = fit(rgbs[0], alphas[0], a)
    g1, o1 = fit(rgbs[-1], alphas[-1], b)
    return lambda t: (g0 + (g1 - g0) * t, o0 + (o1 - o0) * t)


# ---------------------------------------------------------------- stage: pack ---------------------
HOLD_DIFF = .5 / 255     # mean abs frame-to-frame change below this is H3 holding its conditioning frame


def trim_holds(fs):
    """H3 holds its first and last conditioning frame still for ~4 frames: keep one frame of each hold."""
    if len(fs) < 4:
        return fs
    small = [f32(Image.open(f).convert('RGBA').reduce(4)) for f in fs]
    moving = [float(np.abs(small[i + 1] - small[i]).mean()) >= HOLD_DIFF for i in range(len(small) - 1)]
    start = next((i for i, m in enumerate(moving) if m), 0)
    end = len(fs) - 1 - next((i for i, m in enumerate(reversed(moving)) if m), 0)
    return fs[start:end + 1] if end > start else fs


def tighten(fs, head, settle=.45):
    """H3 lands the move early, then drifts slowly into the end key (the strike at ~40% of a segment, then a long
    settle). Compress that settle - every frame already within `settle` of the end key's distance - into a two-frame
    snap, so the contact frame is the strike, not the drift. With `head`, also drop the linger on the start key."""
    if len(fs) < 6:
        return fs
    small = [f32(Image.open(f).convert('RGBA').reduce(4)) for f in fs]
    to_last = [float(np.abs(x - small[-1]).mean()) for x in small]
    to_first = [float(np.abs(x - small[0]).mean()) for x in small]
    start = 0
    if head:
        while start + 1 < len(fs) and to_first[start + 1] < .12 * max(to_first):
            start += 1
    a = len(fs) - 1
    while a - 1 > start and to_last[a - 1] <= settle * max(to_last):
        a -= 1
    if len(fs) - 1 - a < 3:
        return fs[start:]
    return fs[start:a + 1] + [fs[(a + len(fs) - 1) // 2], fs[-1]]


def action_frames(spec, d, action, act):
    """The action's frames (24 fps, segments joined at their shared key) and the index of the contact frame."""
    frames, contact = [], -1
    for i, seg in enumerate(act['segments']):
        fs = sorted((segment_dir(d, action, i) / 'rgba').glob('f_*.png'))
        if not fs:
            return None, -1
        fs = trim_holds(fs)
        if not act.get('loop') and act.get('tighten', True):
            fs = tighten(fs, head=i > 0, settle=act.get('settle', .45))
        frames += fs[1:] if i else fs
        if act.get('contact') == seg[1] and contact < 0:
            contact = len(frames) - 1
    if act.get('loop'):
        frames = frames[:-1]          # the last frame is the first again
    return frames, contact


def pack(spec, d, only=None):
    unit = spec['unit']
    step = max(1, round(24 / spec['fps_out']))
    scale = spec['ppu_out'] * 2 / spec['body_px']
    fx0, fy0 = spec['body_x'] * scale, spec['feet_y'] * scale
    sprites, actions, drift = [], [], {}
    # Hi-res pages for the actions a staged card maps to: the in-battle cinematic draws them large.
    ppu_hi = spec.get('ppu_hi', 240)
    scale_hi = ppu_hi * 2 / spec['body_px']
    hi_actions = set(spec.get('hi_actions') or (a for c, a in spec['cards'].items() if staged_card(c)))
    hi_sprites = {}
    for action, act in spec['actions'].items():
        frames, contact = action_frames(spec, d, action, act)
        if frames is None:
            print('  skip (not matted):', action)
            continue
        base = contact % step if contact >= 0 else 0
        idx = list(range(base, len(frames), step))
        if act.get('hold') and idx[-1] != len(frames) - 1:
            idx.append(len(frames) - 1)
        rec = dict(name=action, loop=bool(act.get('loop')), hold=bool(act.get('hold')), attack=bool(act.get('attack')),
                   contact=idx.index(contact) if contact in idx else -1, release=-1, frames=[], _sprites=[])
        if act.get('release'):
            rec['release'] = rec['contact']
            rec['contact'] = min(len(idx) - 1, rec['contact'] + int(act.get('flight', 4)))
        lows = []
        tips = []
        for j in idx:
            full = Image.open(frames[j]).convert('RGBA')
            alpha = f32(full.getchannel('A'))
            if alpha.max() > .5:
                lows.append(figure_metrics(alpha)['bottom'])
            tips.append(muzzle(alpha, spec, act.get('tip', 'left' if spec.get('kind') == 'monster' else 'right')))
            sprite = sprite_of(full, scale, fx0, fy0)
            sprites.append(sprite)
            rec['_sprites'].append(sprite)
            if action in hi_actions:
                hi_sprites.setdefault(action, []).append(sprite_of(full, scale_hi, spec['body_x'] * scale_hi,
                                                                   spec['feet_y'] * scale_hi))
        rec['tips'] = smooth_tips(tips, spec)
        if lows and not act.get('airborne'):
            off = [abs(y - spec['feet_y']) / spec['body_px'] for y in lows]
            if max(off) > .03:
                drift[action] = round(max(off) * 100, 1)
        actions.append(rec)
    pages = shelf_pack(sprites)
    out = OUT / unit
    out.mkdir(parents=True, exist_ok=True)
    for old in list(out.glob('p*.png')) + list(out.glob('hi_*.png')):
        old.unlink()
    names = []
    for p, page in enumerate(pages):
        Image.fromarray(bleed(page)).save(out / f'p{p}.png')
        names.append(f'p{p}')
    for rec in actions:
        for s in rec.pop('_sprites'):
            rec['frames'] += [s['page'], s['x'], s['y'], s['img'].width, s['img'].height, s['footX'], s['footY']]
    hi = []
    for action, items in hi_sprites.items():
        hpages = shelf_pack(items)
        names_hi = []
        for p, page in enumerate(hpages):
            Image.fromarray(bleed(page)).save(out / f'hi_{action}_{p}.png')
            names_hi.append(f'hi_{action}_{p}')
        frames_hi = []
        for s in items:
            frames_hi += [s['page'], s['x'], s['y'], s['img'].width, s['img'].height, s['footX'], s['footY']]
        hi.append(dict(name=action, ppu=ppu_hi, pages=names_hi, frames=frames_hi))
    guard_meta = json.loads((d / 'keys/guard.json').read_text())['hi']
    shutil.copyfile(d / 'keys/guard_hi.png', out / 'guard_hi.png')
    manifest = dict(unit=unit, version=1, fps=spec['fps_out'], ppu=spec['ppu_out'], pages=names, actions=actions,
                    cards=[dict(card=c, action=a) for c, a in spec['cards'].items()], hi=hi,
                    cine=dict(file='guard_hi', footX=round(guard_meta['footX'], 1), footY=round(guard_meta['footY'], 1),
                              ppu=round(guard_meta['ppu'], 2)))
    (out / 'clips.json').write_text(json.dumps(manifest, separators=(',', ':')))
    previews(spec, d, actions, pages)
    print('PACK ->', out, f'{len(sprites)} frames on {len(pages)} page(s); hi-res', {h['name']: len(h['pages']) for h in hi})
    if drift:
        print('  feet-line drift (% of body) - camera moved?', drift)


def staged_card(card):
    """Cards the battle stages as an in-battle cinematic: every fighter card except basics, guards, ultimates (video)
    and awakenings (video)."""
    return not card.startswith(('basic_', 'guard_', 'ult_', 'aw_'))


def sprite_of(full, scale, fx0, fy0):
    """One frame scaled (premultiplied) and trimmed, with its foot anchor in the trimmed rect."""
    im = full.convert('RGBa').resize((round(full.width * scale), round(full.height * scale)),
                                     Image.Resampling.LANCZOS).convert('RGBA')
    box = im.getchannel('A').point(lambda v: 255 if v > 1 else 0).getbbox() or (0, 0, 1, 1)
    l, t, r, b = max(0, box[0] - 1), max(0, box[1] - 1), min(im.width, box[2] + 1), min(im.height, box[3] + 1)
    return dict(img=im.crop((l, t, r, b)), footX=round(fx0 - l), footY=round(fy0 - t))


def muzzle(alpha, spec, mode):
    """Where an effect leaves the fighter (canvas px): the point of the figure reaching furthest toward the enemy
    between 12% and 55% of body height from the top (hands and weapons, not
    knees) ("right"), the highest point ("top"), or a fixed [x, y]."""
    if isinstance(mode, (list, tuple)):
        return float(mode[0]), float(mode[1])
    top = spec['feet_y'] - spec['body_px']
    y0, y1 = int(top + .12 * spec['body_px']), int(top + .55 * spec['body_px'])
    band = alpha[max(0, y0):y1] > .5
    ys, xs = np.nonzero(band)
    if len(xs) == 0:
        return float(spec['body_x'] + .3 * spec['body_px'] / 2), float(top + .45 * spec['body_px'])
    if mode == 'top':
        ys_all, xs_all = np.nonzero(alpha > .5)
        i = np.argmin(ys_all)
        return float(xs_all[i]), float(ys_all[i])
    if mode == 'left':           # a monster faces the left: its reach is the leftmost point
        xmin = xs.min()
        near = xs <= xmin + 6
        return float(xmin), float(ys[near].mean() + max(0, y0))
    xmax = xs.max()
    near = xs >= xmax - 6
    return float(xmax), float(ys[near].mean() + max(0, y0))


def smooth_tips(tips, spec):
    """3-frame median, then body units from the foot: x toward the enemy, y up."""
    a = np.array(tips, np.float32)
    if len(a) >= 3:
        a = np.stack([np.median(a[max(0, i - 1):i + 2], axis=0) for i in range(len(a))])
    half = spec['body_px'] / 2
    toward = -1 if spec.get('kind') == 'monster' else 1      # x is measured toward the enemy line
    out = []
    for x, y in a:
        out += [round(float(toward * (x - spec['body_x']) / half), 3), round(float((spec['feet_y'] - y) / half), 3)]
    return out


def shelf_pack(sprites):
    """Tallest first into rows on <= 2048 px pages; fills sprite['page'/'x'/'y'] and returns the RGBA page arrays."""
    order = sorted(sprites, key=lambda s: -s['img'].height)
    pages, x, y, row_h = [[]], PAD, PAD, 0
    for s in order:
        w, h = s['img'].size
        if x + w + PAD > PAGE:
            x, y, row_h = PAD, y + row_h + PAD, 0
        if y + h + PAD > PAGE:
            pages.append([])
            x, y, row_h = PAD, PAD, 0
        s['page'], s['x'], s['y'] = len(pages) - 1, x, y
        pages[-1].append(s)
        x += w + PAD
        row_h = max(row_h, h)
    out = []
    for items in pages:
        w = max(s['x'] + s['img'].width for s in items) + PAD
        h = max(s['y'] + s['img'].height for s in items) + PAD
        page = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        for s in items:
            page.paste(s['img'], (s['x'], s['y']))
        out.append(np.asarray(page).copy())
    return out


def bleed(page):
    """Spread edge colour into the transparent padding so bilinear filtering never pulls in black."""
    a = page[..., 3:4].astype(np.float32) / 255
    rgb = page[..., :3].astype(np.float32)
    w = (a > 0).astype(np.float32)
    acc = ndimage.uniform_filter(rgb * w, size=(9, 9, 1))
    ww = ndimage.uniform_filter(w, size=(9, 9, 1))
    fill = acc / np.maximum(ww, 1e-6)
    rgb = np.where((w == 0) & (ww > 0), fill, rgb)
    return np.dstack([np.clip(rgb + .5, 0, 255).astype(np.uint8), page[..., 3]])


def previews(spec, d, actions, pages):
    """Per action: a GIF on the flat background and a strip with the contact frame outlined red."""
    bg = tuple(spec['bg'])
    for rec in actions:
        f = rec['frames']
        crops = []
        for k in range(0, len(f), 7):
            p, x, y, w, h, fx, fy = f[k:k + 7]
            crops.append((Image.fromarray(pages[p][y:y + h, x:x + w]), fx, fy))
        W, H = round(spec['canvas'][0] * spec['ppu_out'] * 2 / spec['body_px']), round(
            spec['canvas'][1] * spec['ppu_out'] * 2 / spec['body_px'])
        ox, oy = round(spec['body_x'] * W / spec['canvas'][0]), round(spec['feet_y'] * H / spec['canvas'][1])
        tiles = []
        for i, (im, fx, fy) in enumerate(crops):
            t = Image.new('RGBA', (W, H), bg + (255,))
            t.paste(im, (ox - fx, oy - fy), im)
            tips = rec.get('tips') or []
            if 2 * i + 1 < len(tips):      # the muzzle point effects leave from
                tx, ty = ox + tips[2 * i] * spec['ppu_out'], oy - tips[2 * i + 1] * spec['ppu_out']
                ImageDraw.Draw(t).ellipse([tx - 5, ty - 5, tx + 5, ty + 5], outline=(255, 230, 40), width=2)
            if i == rec['contact']:
                ImageDraw.Draw(t).rectangle([0, 0, W - 1, H - 1], outline=(230, 40, 40), width=4)
            tiles.append(t.convert('RGB'))
        tiles[0].save(d / f"review/{rec['name']}.gif", save_all=True, append_images=tiles[1:],
                      duration=round(1000 / spec['fps_out']), loop=0)
        strip = Image.new('RGB', (W // 2 * len(tiles), H // 2))
        for i, t in enumerate(tiles):
            strip.paste(t.resize((W // 2, H // 2), Image.Resampling.LANCZOS), (i * (W // 2), 0))
        strip.save(d / f"review/{rec['name']}_strip.jpg", quality=88)


# ---------------------------------------------------------------- stage: cine ---------------------
def cine(spec, d, only=None):
    """The guard at the match-cut framing over cine_bg: first/last frame for every produce_cine.py skill insert."""
    w, h = 1280, 720
    k = h / (2 * CINE_HALF)                     # px per body unit (the body is 2 units tall)
    meta = json.loads((d / 'keys/guard.json').read_text())['hi']
    hi = Image.open(d / 'keys/guard_hi.png').convert('RGBa')
    s = k / meta['ppu']
    hi = hi.resize((round(hi.width * s), round(hi.height * s)), Image.Resampling.LANCZOS).convert('RGBA')
    foot = (w / 2 - CINE_CENTER_X * k, h / 2 + CINE_CENTER_Y * k)
    frame = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    frame.paste(hi, (round(foot[0] - meta['footX'] * s), round(foot[1] - meta['footY'] * s)), hi)
    frame.save(d / 'cine_frame.png')
    bg = Image.open(BACKDROP).convert('RGBA').resize((w, h), Image.Resampling.LANCZOS)
    bg.alpha_composite(frame)
    bg.convert('RGB').save(d / 'review/cine_frame.jpg', quality=92)
    print('CINE FRAME ->', d / 'cine_frame.png')


STAGES = dict(keys=keys, motion=motion, matte=matte, pack=pack, cine=cine)

if __name__ == '__main__':
    unit, stage = sys.argv[1], sys.argv[2]
    only = sys.argv[3] if len(sys.argv) > 3 else None
    spec, d = load(unit)
    for name in (['motion', 'matte', 'pack'] if stage == 'clips' else [stage]):
        STAGES[name](spec, d, only)
