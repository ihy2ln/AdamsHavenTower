"""Match-cut cinematics for skill cards (2D rig pilot) from local ComfyUI MiniMax H3.

  python Tools/produce_cine.py cine_mv_shatter_hook

The first and last frame are the fighter's 2D rig at the cine framing (Battle2DRigBuilder.RenderCineFrame ->
BattleMotion/<job>/rig_frame.png) laid over Resources/AdamsHaven/Fx/cine_bg.png, the same backdrop the battle fades
to while it zooms in on the fighter. The clip therefore starts and ends exactly where the battle's zoom lands, so the
cut is seamless. Output: Resources/AdamsHaven/UltCutIns/<card id>.mp4, 1280x720, 60 fps (H3 24 fps -> RIFE 5x -> 60).
key_first.png / key_last.png and spec.json stay in BattleMotion/<job>/ for a later pass through another model
(for example Seedance) with the same first and last frames.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import WORK, ULT_OUT, fit, h3, run, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
BACKDROP = PROJECT / 'Assets/Resources/AdamsHaven/Fx/cine_bg.png'

JOBS = {
    # H3 wants 1248x704 (multiples of 32) and 8n+1 frames; the keyframe is fitted to it and the clip scaled back to 720p.
    'cine_mv_shatter_hook': dict(card='mv_shatter_hook', size=(1248, 704), length=41, seed=7301, prompt=(
        'Anime fighting game special move, locked camera, dark navy stage. The muscular blue-haired snow-leopard woman '
        'with glowing ice-crystal claw gauntlets coils back, then steps forward and drives a powerful rising hook punch '
        'to the right. On impact a huge cluster of blue ice crystals erupts in the right half of the frame and shatters '
        'into bright blue-white shards and frost mist with a white flash. The shards fade away and she settles back into '
        'exactly her starting ready stance, in the same place.')),
}


def keyframe(d):
    rig = Image.open(d / 'rig_frame.png').convert('RGBA')
    bg = Image.open(BACKDROP).convert('RGBA').resize(rig.size, Image.Resampling.LANCZOS)
    bg.alpha_composite(rig)
    return bg.convert('RGB')


def main(job):
    spec = JOBS[job]
    d = WORK / job
    d.mkdir(parents=True, exist_ok=True)
    (d / 'spec.json').write_text(json.dumps(spec, indent=2))
    key = keyframe(d)
    key.save(d / 'key_first.png'); key.save(d / 'key_last.png')
    if not (d / 'h3_hi.mp4').exists():          # delete h3_hi.mp4 to re-render
        first = to_input(fit(key, spec['size']), f'ahcg-{job}-first.png')
        g = h3(spec['prompt'], spec['size'], spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}', first, first)
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        print('h3', job, flush=True)
        for s in run(g, 'ahcg-cine'):
            if s.suffix == '.mp4':
                shutil.copyfile(s, d / ('h3_hi.mp4' if '_hi' in s.name else 'h3_24.mp4'))
    ULT_OUT.mkdir(parents=True, exist_ok=True)
    dst = ULT_OUT / f"{spec['card']}.mp4"
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(d / 'h3_hi.mp4'),
                    '-vf', 'select=not(mod(n\\,2)),setpts=N/60/TB,scale=1280:720:flags=lanczos', '-r', '60',
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart',
                    str(dst)], check=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(dst), '-vf', 'fps=15,scale=640:-1:flags=lanczos',
                    str(d / f'{job}_preview.gif')], check=True)
    print('CINE ->', dst)


if __name__ == '__main__':
    main(sys.argv[1])
