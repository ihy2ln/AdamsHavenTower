"""Character PNG -> watertight, game-ready GLB with the PixelArtistry workflow 02 on the second ComfyUI (port 8190).

  python Tools/char_wt.py Elara            # one character
  python Tools/char_wt.py all

Workflow: Pixal3D -> WTiVo watertight -> LODTailor (Blender) -> Bake Forger -> Fast Merge. Settings follow the workflow's
own "16 GB+" preset: Quad Reconstruct bypassed, WTiVo 2048 / 25M proxy points, 2048 shape upsample; the low-poly is capped
at 45,000 triangles. Only one job is submitted at a time (the card has 16 GB VRAM).
Outputs: <characters>/<Folder>/wt-v1/<Key>_highpoly.glb and <Key>_lowpoly.glb
"""
from pathlib import Path
import json, shutil, sys, time, urllib.request
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).parent))
from comfy_ui2api import convert
import char_to_3d as C

URL = 'http://127.0.0.1:8190'
ROOT = Path('S:/AI/ComfyUI_Watertight_v1.1/ComfyUI')
WF = ROOT / 'user/default/workflows/PixelArtistry/PixelArtistry_02_Image_to_GameReady_Asset.json'
TARGET_TRIS = 50000
SAFE = False      # True: the workflow's default 1536 / 12M-point preset (used when the 2K preset crashes WTiVo)


def call(path, data=None, timeout=120):
    req = urllib.request.Request(URL + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(req, timeout=timeout))


def prepare(key):
    folder, name = C.REFS[key]
    im = Image.open(C.CHARS / folder / name).convert('RGBA')
    bg = Image.new('RGBA', im.size, (128, 128, 128, 255)); bg.alpha_composite(im)
    dst = ROOT / 'input' / f'wt_{key}.png'
    bg.convert('RGB').save(dst)
    return dst.name


def build(key, image, seed=42, steps=24):
    p = convert(json.load(open(WF, encoding='utf-8')), URL)
    p['122']['inputs']['image'] = image
    p['316']['inputs']['value'] = False                              # Pixal3D
    # 16 GB preset: bypass Quad Reconstruct, 2K WTiVo with 25M proxy points, 2048 upsample
    del p['325']
    p['326']['inputs']['input_1'] = ['92', 0]
    res, pts = (1536, 12000000) if SAFE else (2048, 25000000)
    p['327']['inputs'].update(input_res=res, final_res=res, proxy_points=pts)
    p['94']['inputs']['target_resolution'] = str(res)
    steps = min(45, steps)                                            # hard cap requested by the user
    p['18']['inputs'].update(steps=steps, cfg=6, seed=seed)
    p['3']['inputs'].update(steps=steps, cfg=6, seed=seed + 14)
    p['23']['inputs'].update(steps=min(45, max(12, steps // 2)), cfg=6, seed=seed)
    p['12']['inputs'].update(seed=seed + 1, steps=min(45, max(12, steps // 2)))
    p['327']['inputs']['faithc_component_mode'] = 'auto'               # keep floating props (book, quill, staff)
    p['335']['inputs'].update(target_tris=TARGET_TRIS, passes=8)
    p['56']['inputs'].setdefault('refine_steps', 3)
    for nid, suffix in (('334', 'highpoly'), ('343', 'lowpoly')):        # Save3DAdvanced needs a viewport: use SaveGLB
        src = p[nid]['inputs']['model_3d']
        p[nid] = {'class_type': 'SaveGLB', 'inputs': {'mesh': src, 'filename_prefix': f'3d/{key}_{suffix}'}}
    return p


def run(key, attach=None, seed=42, steps=24, tag=''):
    folder, _ = C.REFS[key]
    out = C.CHARS / folder / 'wt-v1'
    out.mkdir(exist_ok=True)
    if (out / f'{key}_lowpoly{tag}.glb').exists():
        print(key, 'already done'); return
    image = prepare(key)
    pid = attach or call('/prompt', {'prompt': build(key, image, seed, steps)})['prompt_id']
    print(key, 'queued', pid, flush=True)
    t0 = time.time()
    while True:
        try:
            h = call('/history/' + pid, timeout=300).get(pid)
        except Exception as e:
            print('poll error', e, flush=True); time.sleep(30); continue
        if h and h.get('status', {}).get('completed') is not None and (
                h['status']['completed'] or h['status'].get('status_str') == 'error'):
            break
        time.sleep(15)
    if h['status'].get('status_str') == 'error':
        print(json.dumps([m for m in h['status']['messages'] if m[0] == 'execution_error'], indent=1)[:2500])
        raise SystemExit(f'{key} failed')
    files = [f for o in h['outputs'].values() for v in o.values() if isinstance(v, list) for f in v
             if isinstance(f, dict) and str(f.get('filename', '')).endswith('.glb')]
    print(key, 'done in %ds' % (time.time() - t0), files, flush=True)
    for f in files:
        kind = 'lowpoly' if 'lowpoly' in f['filename'] else 'highpoly'
        shutil.copy(ROOT / 'output' / f.get('subfolder', '') / f['filename'], out / f'{key}_{kind}{tag}.glb')
    shutil.copy(ROOT / 'input' / image, out / f'{key}_reference_flat.png')


if __name__ == '__main__':
    args = sys.argv[1:]
    attach = None
    if '--attach' in args:
        attach = args[args.index('--attach') + 1]; args = [a for a in args if a not in ('--attach', attach)]
    for k in (list(C.REFS) if args in (['all'], []) else args):
        run(k, attach)
