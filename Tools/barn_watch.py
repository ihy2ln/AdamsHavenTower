"""Run barn_pipeline for each tier as soon as its GLB appears (leave running while barn_to_3d.py generates)."""
import subprocess, sys, time
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
TIERS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']
done = {t for t in TIERS if (PROJECT / f'Assets/Resources/AdamsHaven/TowerModels/barn/barn_{t}.fbx').exists()}
while len(done) < len(TIERS):
    for t in TIERS:
        if t not in done and (PROJECT / 'BarnModels' / f'{t}.glb').exists():
            time.sleep(5)
            if subprocess.run([sys.executable, str(PROJECT / 'Tools/barn_pipeline.py'), t]).returncode == 0:
                done.add(t); print('packaged', t, flush=True)
    time.sleep(20)
print('all tiers packaged')
