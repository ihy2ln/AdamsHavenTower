"""Perfect-generation loop for the battle-mode characters.

  python Tools/char_final.py Elara            # one character
  python Tools/char_final.py all

Per attempt: PixelArtistry workflow 02 (ComfyUI :8190, Pixal3D, 16 GB preset, props kept) -> voxel closing -> Blender finish:
watertight, <= 50,000 triangles, PNG projected for the anime colours -> QA gate. If the gate fails the next attempt uses a new
seed and more steps (24 -> 45 max). Outputs: <Folder>/final-v2/<Key>/ (GLB, textures, STL, QA renders, qa.json).
"""
import json, os, subprocess, sys, time, urllib.request
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
sys.path.insert(0, str(Path(__file__).parent))
import char_to_3d as C
import char_wt as W
import char_views as V

BLENDER = 'S:/AI/Game Engine/Blender/blender.exe'
COMFY_PY = 'S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe'
ATTEMPTS = [(42, 24), (43, 30), (44, 36), (45, 42), (46, 45)]       # (seed, steps), steps never above 45
TOOLS = Path(__file__).parent


def rgba_reference(key, out):
    folder, name = C.REFS[key]
    dst = out / f'{key}_ref_rgba.png'
    if dst.exists(): return dst
    im = Image.open(C.CHARS / folder / name)
    if im.mode == 'RGBA' and (np.array(im)[:, :, 3] < 10).mean() > 0.05:
        im.save(dst); return dst
    rgb = np.array(im.convert('RGB')).astype(np.int16)
    corners = np.concatenate([rgb[:20, :20].reshape(-1, 3), rgb[:20, -20:].reshape(-1, 3), rgb[-20:, :20].reshape(-1, 3), rgb[-20:, -20:].reshape(-1, 3)])
    bg = np.median(corners, 0)
    mask = Image.fromarray(((np.abs(rgb - bg).max(2) > 22) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    out_im = im.convert('RGBA'); out_im.putalpha(mask); out_im.save(dst)
    return dst


def keyed_back(path, dst):
    """Back view from Qwen on a flat grey background -> RGBA with a clean, slightly eroded cut-out."""
    if dst.exists(): return dst
    im = Image.open(path).convert('RGB'); rgb = np.array(im).astype(np.int16)
    corners = np.concatenate([rgb[:20, :20].reshape(-1, 3), rgb[:20, -20:].reshape(-1, 3), rgb[-20:, :20].reshape(-1, 3), rgb[-20:, -20:].reshape(-1, 3)])
    bg = np.median(corners, 0)
    m = Image.fromarray(((np.abs(rgb - bg).max(2) > 26) * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MaxFilter(3))
    out = im.convert('RGBA'); out.putalpha(m); out.save(dst)
    return dst


def wait_idle(url='http://127.0.0.1:8190'):
    while True:
        try:
            q = json.load(urllib.request.urlopen(url + '/queue', timeout=60))
            if not q['queue_running'] and not q['queue_pending']: return
        except Exception: pass
        time.sleep(30)


SIDES = {}


def finish(key, raw, front, back, out, tag):
    closed = raw.with_name(raw.stem + '_closed.glb')
    if not closed.exists():
        r = subprocess.run([COMFY_PY, str(TOOLS / 'char_close.py'), str(raw), str(closed)], capture_output=True, text=True)
        print(key, 'closed:', r.stdout[-160:], r.stderr[-160:], flush=True)
    target = out / f'attempt{tag}'
    for atlas in ('4096', '2048'):
        env = {**os.environ, 'CHAR_ATLAS': atlas, 'CHAR_CLOSED': str(closed), **SIDES}
        r = subprocess.run([BLENDER, '-b', '-P', str(TOOLS / 'char_finish.py'), '--', str(raw), str(front), str(back), str(target), key, '50000'],
                           env=env, capture_output=True, text=True)
        (out / f'{key}_finish{tag}_{atlas}.log').write_text(r.stdout + r.stderr)
        qa = target / f'{key}_qa.json'
        if qa.exists(): return json.load(open(qa))
    return None


def run(key):
    folder, _ = C.REFS[key]
    out = C.CHARS / folder / 'final-v2' / key
    out.mkdir(parents=True, exist_ok=True)
    if (out / 'ACCEPTED').exists(): print(key, 'already accepted'); return
    front = rgba_reference(key, out)
    views_dir = C.CHARS / folder / 'pixal-max-v1' / 'views'
    back = views_dir / 'back.png'
    if not back.exists():
        wait_idle(); V.make(key)
    for i, (seed, steps) in enumerate(ATTEMPTS):
        tag = f'_s{seed}_{steps}'
        raw = C.CHARS / folder / 'wt-v1' / f'{key}_highpoly{tag}.glb'
        if not raw.exists():
            wait_idle()
            for safe in (False, True):
                W.SAFE = safe
                try:
                    W.run(key, None, seed, steps, tag); break
                except SystemExit as e:
                    print(key, 'workflow failed', 'with the safe preset' if safe else '(2K preset); retrying with 1536/12M', flush=True)
                    wait_idle()
            W.SAFE = False
            if not raw.exists():
                continue
        backk = keyed_back(back, out / f'{key}_back_rgba.png') if back.exists() else Path('-')
        SIDES.clear()
        for side in ():                      # side-view projection is disabled: the Qwen side views are not aligned well enough

            if (views_dir / f'{side}.png').exists():
                SIDES['CHAR_SIDE_' + side.upper()] = str(keyed_back(views_dir / f'{side}.png', out / f'{key}_{side}_rgba.png'))
        qa = finish(key, raw, front, backk, out, tag)
        ok = bool(qa and qa.get('pass'))
        print(key, 'attempt', i, 'seed', seed, 'steps', steps, 'PASS' if ok else 'FAIL', json.dumps(qa and {k: qa[k] for k in ('final', 'orientation', 'pass')})[:300], flush=True)
        if ok:
            (out / 'ACCEPTED').write_text(tag); return
    print(key, 'NO attempt passed QA; best-effort result kept in', out, flush=True)


if __name__ == '__main__':
    args = sys.argv[1:]
    order = ['Elara', 'Daisy', 'Helda', 'Clarity', 'Ghislaine', 'CelestiumMed', 'CelestiumMuscle', 'CelestiumShort', 'Kaela']
    for k in (order if args in (['all'], []) else args):
        try: run(k)
        except BaseException as e: print(k, 'ERROR, moving on:', repr(e)[:300], flush=True)
