"""Bake Tower-mode motion atlases from the battle rigs (TowerChibiAnimator layout).

python Tools/pack_tower_atlas.py [id ...]        # default: every entry in UNITS

For each unit: Blender renders the frames (Tools/bake_tower_motion.py) from its battle .blend, then each clip's
24 frames are downsampled to 124x186 and packed 8 per row into a 1024x1024 RGBA atlas at
Assets/Resources/AdamsHaven/ChibiMotion/<atlas>/<clip>.png (cell 128x192, frame inset 2,3), the layout
TowerChibiAnimator.SetFrame reads. The atlas .meta files are left alone, so import settings carry over.
"""
import json, os, subprocess, sys, tempfile
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
BLENDER = 'S:/AI/Game Engine/Blender/blender.exe'
JOBS = ROOT / 'MeshyJobs' / 'anime-battle-v2'
ATLAS_DIR = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'ChibiMotion'
CLIPS = ['idle', 'walk_in_place', 'task', 'knocked_down']
# atlas folder -> battle .blend (heroes by roster id; Celestium bodies by chassis variant)
UNITS = {
    'kaela': 'kaela/kaela-battle.blend',
    'ghislaine': 'ghislaine/ghislaine-battle.blend',
    'elara': 'elara/elara-battle.blend',
    'helda': 'helda/helda-battle.blend',
    'daisy': 'daisy/daisy-battle.blend',
    'clarity': 'clarity/clarity-battle.blend',
    'body_normal': 'celestium-med/celestium-normal-battle.blend',
    'body_muscle': 'celestium-muscle/celestium-muscle-battle.blend',
    'body_short': 'celestium-short/celestium-short-battle.blend',
}
FRAMES, COLUMNS, ATLAS = 24, 8, 1024
CELL = (128, 192)
FRAME = (124, 186)
PAD = (2, 3)


def pack(frames_dir, unit):
    out = ATLAS_DIR / unit
    out.mkdir(parents=True, exist_ok=True)
    for clip in CLIPS:
        atlas = Image.new('RGBA', (ATLAS, ATLAS), (0, 0, 0, 0))
        for i in range(FRAMES):
            im = Image.open(frames_dir / f'{clip}_{i:02d}.png').convert('RGBA')
            im = im.resize(FRAME, Image.LANCZOS)
            col, row = i % COLUMNS, i // COLUMNS
            atlas.paste(im, (col * CELL[0] + PAD[0], row * CELL[1] + PAD[1]))
        atlas.save(out / f'{clip}.png', optimize=True)
    return out


def update_manifest(unit, blend):
    path = ATLAS_DIR / 'atlas-manifest.json'
    if not path.exists():
        return
    data = json.loads(path.read_text(encoding='utf-8'))
    entry = data.setdefault('units', {}).setdefault(unit, {})
    for clip in CLIPS:
        entry[clip] = {
            'source': 'MeshyJobs/anime-battle-v2/' + blend,
            'source_clip': {'idle': 'procedural idle', 'task': 'procedural task',
                            'walk_in_place': 'AH_walk', 'knocked_down': 'AH_knock_down'}[clip],
            'atlas': f'Assets/Resources/AdamsHaven/ChibiMotion/{unit}/{clip}.png',
            'bytes': (ATLAS_DIR / unit / f'{clip}.png').stat().st_size,
            'loop': clip != 'knocked_down',
            'baker': 'Tools/pack_tower_atlas.py',
        }
    path.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')


def main():
    units = sys.argv[1:] or list(UNITS)
    for unit in units:
        blend = UNITS[unit]
        with tempfile.TemporaryDirectory() as tmp:
            r = subprocess.run([BLENDER, '-b', str(JOBS / blend), '--python', str(ROOT / 'Tools' / 'bake_tower_motion.py'),
                                '--', tmp], capture_output=True, text=True)
            if r.returncode or 'BAKED knocked_down' not in r.stdout:
                print(r.stdout[-3000:], r.stderr[-3000:])
                raise SystemExit(f'{unit}: Blender bake failed')
            out = pack(Path(tmp), unit)
        update_manifest(unit, blend)
        print(unit, '->', out.relative_to(ROOT), flush=True)


if __name__ == '__main__':
    main()
