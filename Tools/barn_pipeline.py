"""Barn GLB (Trellis.2) -> Unity-ready FBX + baked texture, per tier.

  python Tools/barn_pipeline.py F E D            # tiers whose BarnModels/<tier>.glb exist
  python Tools/barn_pipeline.py all

Steps: Blender fit/orient -> FBX, trim geometry outside the painting silhouette, Blender front-projection bake, numpy compose, copy to
Assets/Resources/AdamsHaven/TowerModels/barn/barn_<tier>.fbx + barn_<tier>.png (BarnModelImporter builds the prefabs).
"""
from pathlib import Path
import shutil, subprocess, sys
import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parent.parent
PACK = Path('S:/AI/Harness/Codex/home/generated_images/01a0f00a-6833-72c3-a8b8-4f6aef0e36b9/Barn F-SSR/Barn Game Ready')
BLENDER = 'S:/AI/Game Engine/Blender/blender.exe'
TIERS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']
BAYS = {'F': 1, 'E': 1, 'D': 1, 'C': 2, 'B': 2, 'A': 3, 'S': 3, 'SS': 3, 'SSR': 3}
WORK = PROJECT / 'BarnModels' / 'out'
DEST = PROJECT / 'Assets/Resources/AdamsHaven/TowerModels/barn'


def blender(script, *args):
    r = subprocess.run([BLENDER, '-b', '-P', str(PROJECT / 'Tools' / script), '--', *map(str, args)],
                       capture_output=True, text=True)
    if r.returncode != 0 or 'Traceback' in r.stdout + r.stderr:
        print(r.stdout[-2000:], r.stderr[-2000:]); raise SystemExit(f'{script} failed')
    return r.stdout


def run(tier):
    glb = PROJECT / 'BarnModels' / f'{tier}.glb'
    painting = PACK / f'barn_{tier}.png'
    alpha = np.array(Image.open(painting))[:, :, 3] > 16
    ys, xs = np.where(alpha)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    aspect = (y1 - y0) / (x1 - x0)
    WORK.mkdir(parents=True, exist_ok=True)
    blender('barn_glb_to_fbx.py', glb, WORK, tier, BAYS[tier], aspect)
    blender('barn_trim.py', WORK / f'barn_{tier}.fbx', painting, BAYS[tier], x0, x1, y0, y1)
    proj = WORK / f'barn_{tier}_proj.png'
    blender('barn_bake_projection.py', WORK / f'barn_{tier}.fbx', WORK / f'barn_{tier}_base.png', painting, proj,
            BAYS[tier], x0, x1, y0, y1)
    final = WORK / f'barn_{tier}_final.png'
    subprocess.run([sys.executable, str(PROJECT / 'Tools/barn_compose_texture.py'), WORK / f'barn_{tier}_base.png', proj,
                    WORK / f'barn_{tier}_proj_factor.png', final], check=True)
    DEST.mkdir(parents=True, exist_ok=True)
    shutil.copy(WORK / f'barn_{tier}.fbx', DEST / f'barn_{tier}.fbx')
    shutil.copy(final, DEST / f'barn_{tier}.png')
    print(tier, 'ready ->', DEST, flush=True)


if __name__ == '__main__':
    args = sys.argv[1:] or ['F']
    for t in (TIERS if args == ['all'] else args):
        if (PROJECT / 'BarnModels' / f'{t}.glb').exists(): run(t)
        else: print(t, 'no glb yet')
