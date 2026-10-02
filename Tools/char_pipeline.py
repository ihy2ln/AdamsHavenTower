"""Full character pipeline: Qwen turnaround views -> Pixal3D multi-view mesh (max settings) -> watertight <=50k game model.

  python Tools/char_pipeline.py Elara            # one
  python Tools/char_pipeline.py all              # everyone, in order

Outputs land in <characters>/<Folder>/pixal-max-v1/final/<Key>/ (GLB, FBX-ready textures, STL, QA renders, qa.json).
Each stage is skipped if its output already exists, so the script can be restarted at any point.
"""
import os, subprocess, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import char_to_3d as C
import char_views as V

BLENDER = 'S:/AI/Game Engine/Blender/blender.exe'
COMFY_PY = 'S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe'
ORDER = ['Elara', 'Daisy', 'Helda', 'Clarity', 'Ghislaine', 'Kaela', 'CelestiumMed', 'CelestiumMuscle', 'CelestiumShort']


def finish(key):
    folder, _ = C.REFS[key]
    base = C.CHARS / folder / 'pixal-max-v1'
    raw = base / f'{key}_pixal_raw_mv.glb'
    out = base / 'final' / key
    if (out / f'{key}_qa.json').exists():
        print(key, 'already finished'); return
    closed = base / f'{key}_closed.glb'
    if not closed.exists():
        r = subprocess.run([COMFY_PY, str(Path(__file__).parent / 'char_close.py'), str(raw), str(closed)],
                           capture_output=True, text=True)
        print(key, 'closed:', r.stdout[-200:], r.stderr[-200:], flush=True)
    for atlas in ('4096', '2048'):                       # ComfyUI can leave too little RAM for a 4096 bake
        env = {**os.environ, 'CHAR_ATLAS': atlas, 'CHAR_CLOSED': str(closed)}
        r = subprocess.run([BLENDER, '-b', '-P', str(Path(__file__).parent / 'char_finish.py'), '--', str(raw),
                            str(base / f'{key}_reference.png'), str(base / 'views' / 'back.png'), str(out), key, '50000'],
                           env=env, capture_output=True, text=True)
        log = r.stdout + r.stderr
        (base / f'{key}_finish_{atlas}.log').write_text(log)
        if (out / f'{key}_qa.json').exists():
            print(key, 'finished at atlas', atlas, flush=True); return
        print(key, 'finish failed at atlas', atlas, log[-400:], flush=True)
    raise SystemExit(f'{key}: finishing failed')


def run(key):
    V.make(key)
    C.run(key, C.MAX, '_mv', True)
    finish(key)


if __name__ == '__main__':
    args = sys.argv[1:]
    for k in (ORDER if args in (['all'], []) else args):
        run(k)
