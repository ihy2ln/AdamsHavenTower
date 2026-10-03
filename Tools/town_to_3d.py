"""Town reference picture -> 3D building in the game (GDD 19.2).

  python Tools/town_to_3d.py inn_fd                 # one picture (file stem from TownPrompts)
  python Tools/town_to_3d.py inn                    # every rendered band of a type
  python Tools/town_to_3d.py all [--quick]          # everything rendered so far

Steps: TownPrompts/.../<stem>.png -> local ComfyUI (BiRefNet mask + Trellis.2, the char_to_3d graph) -> TownModels/<stem>.glb
-> Blender (Tools/town_glb_to_fbx.py: square to the grid, 1x1 footprint, decimate) ->
Assets/Resources/AdamsHaven/TowerModels/town_<type>/<stem>.fbx + .png. TowerModelImporter then builds the prefab, and
TowerTownView uses it in place of the stand-in block for that type and rank band.
"""
from pathlib import Path
import json, shutil, subprocess, sys, time

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_to_3d as C

PROJECT = Path(__file__).resolve().parent.parent
PACK = PROJECT / 'TownPrompts'
WORK = PROJECT / 'TownModels'
DEST = PROJECT / 'Assets/Resources/AdamsHaven/TowerModels'
BLENDER = 'S:/AI/Game Engine/Blender/blender.exe'


def pictures(args):
    files = sorted(p for p in PACK.rglob('*.png'))
    if args == ['all']: return files
    out = []
    for a in args:
        out += [p for p in files if p.stem == a or p.stem.rsplit('_', 1)[0] == a]
    return out


def to_glb(png, settings):
    glb = WORK / f'{png.stem}.glb'
    if glb.exists(): return glb
    image = f'town_{png.stem}.png'
    shutil.copy(png, C.COMFY / 'input' / image)
    for attempt, cfg in enumerate([settings, {**settings, 'up_res': 1024}]):
        try:
            pid = C.call('/prompt', {'prompt': C.graph(image, False, f'3d/town_{png.stem}', cfg)})['prompt_id']
        except OSError as e:
            print('  ComfyUI unreachable', e, '- retrying in 30s', flush=True); time.sleep(30); continue
        print(png.stem, 'queued', pid, flush=True)
        t0 = time.time()
        while True:
            try:
                h = C.call('/history/' + pid).get(pid)
            except Exception as e:
                print('  poll error', e, flush=True); time.sleep(30); continue
            if h and h.get('status', {}).get('completed') is not None and (
                    h['status']['completed'] or h['status'].get('status_str') == 'error'):
                break
            time.sleep(10)
        if h['status'].get('status_str') == 'error':
            print(json.dumps(h['status'], indent=1)[:1500]); continue
        files = [f for o in h['outputs'].values() for v in o.values() if isinstance(v, list) for f in v
                 if isinstance(f, dict) and str(f.get('filename', '')).endswith('.glb')]
        WORK.mkdir(exist_ok=True)
        shutil.copy(C.COMFY / 'output' / files[-1].get('subfolder', '') / files[-1]['filename'], glb)
        print(png.stem, 'glb in %ds' % (time.time() - t0), flush=True)
        return glb
    raise SystemExit(f'{png.stem}: Trellis failed')


def to_unity(png, glb):
    kind = png.stem.rsplit('_', 1)[0]
    out = WORK / 'out'
    r = subprocess.run([BLENDER, '-b', '-P', str(PROJECT / 'Tools/town_glb_to_fbx.py'), '--', str(glb), str(out), png.stem],
                       capture_output=True, text=True)
    if r.returncode != 0 or 'Traceback' in r.stdout + r.stderr:
        print(r.stdout[-2000:], r.stderr[-2000:]); raise SystemExit('Blender step failed')
    print('  ' + ' '.join(l for l in r.stdout.splitlines() if l.startswith(('YAW', 'FACES'))), flush=True)
    dest = DEST / f'town_{kind}'
    dest.mkdir(parents=True, exist_ok=True)
    shutil.copy(out / f'{png.stem}.fbx', dest / f'{png.stem}.fbx')
    shutil.copy(out / f'{png.stem}.png', dest / f'{png.stem}.png')
    print(png.stem, 'ready ->', dest.relative_to(PROJECT), flush=True)


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')] or ['inn_fd']
    settings = C.QUICK if '--quick' in sys.argv else C.MAX
    for png in pictures(args):
        to_unity(png, to_glb(png, settings))
