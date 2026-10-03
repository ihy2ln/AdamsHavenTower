"""Re-roll one fighter action: a few H3 seeds per segment, scored for baked-in effects, the cleanest take packed.

  python Tools/reroll_clip.py ghislaine AH_hit_react          # 3 seeds per segment (spec seed + 20, 40, 60)
  python Tools/reroll_clip.py daisy AH_block --takes 4

H3 likes to paint its own effects into a fighter's motion (fire bursts, white flashes, slash arcs), and wording them
away backfires: a negated effect noun ("no explosion") draws it. So the motion text stays plain and the takes are
scored instead: the most bright, saturated or near-white pixels any frame adds over the action's first key (a flaming
weapon's own glow is in the key, so it does not count), plus a penalty when that glow drops below 40% in some frame (the
weapon vanished mid-motion). The lowest score wins; ties keep the earlier seed.

Nothing is deleted: the take that was packed before is kept as segments/_takes/<action>_<i>_<seed>_kept/, every new
take as segments/_takes/<action>_<i>_<seed>/ (h3_24.mp4, spec.json, score.json). To go back to a take, copy its
h3_24.mp4 and spec.json into segments/<action>_<i>/, put its seed in the spec, then run produce_fighter_clips matte+pack.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent


def hot(img):
    """Pixels on the figure that read as an effect: bright and saturated warm/cool light, or near white."""
    a = np.asarray(img.convert('RGBA'), np.float32) / 255
    rgb, al = a[..., :3], a[..., 3] > .5
    mx, mn = rgb.max(2), rgb.min(2)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    glow = (mx > .85) & (sat > .45)
    white = mn > .93
    return int(((glow | white) & al).sum())


def key_hot(d, key):
    k = Image.open(d / f'keys/{key}.png').convert('RGBA')
    k.putalpha(Image.open(d / f'keys/{key}_a.png').convert('L'))
    return hot(k)


def run(unit, *args):
    subprocess.run([sys.executable, str(TOOLS / 'produce_fighter_clips.py'), unit, *args], check=True, cwd=PROJECT)


def main():
    unit, action = sys.argv[1], sys.argv[2]
    takes = int(sys.argv[sys.argv.index('--takes') + 1]) if '--takes' in sys.argv else 3
    spec_path = TOOLS / 'fighter_clips' / f'{unit}.json'
    d = PROJECT / 'BattleMotion' / f'fighter_{unit}'
    segs = d / 'segments'
    spec = json.loads(spec_path.read_text(encoding='utf-8'))
    base_seeds = [seg[3] for seg in spec['actions'][action]['segments']]

    def set_seed(i, seed):
        s = json.loads(spec_path.read_text(encoding='utf-8'))
        s['actions'][action]['segments'][i][3] = seed
        spec_path.write_text(json.dumps(s, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')

    for i, base in enumerate(base_seeds):
        sd = segs / f'{action}_{i}'
        first = spec['actions'][action]['segments'][i][0]
        floor = key_hot(d, first)
        if (sd / 'h3_24.mp4').exists():          # keep what was packed before
            keep = segs / '_takes' / f'{action}_{i}_{base}_kept'
            if not keep.exists():
                keep.mkdir(parents=True)
                for f in ('h3_24.mp4', 'spec.json'):
                    if (sd / f).exists():
                        shutil.copyfile(sd / f, keep / f)
        scores = {}
        for seed in [base + 20 * (k + 1) for k in range(takes)]:
            set_seed(i, seed)
            run(unit, 'motion', f'{action}:{i}')
            run(unit, 'matte', f'{action}:{i}')
            frames = sorted((sd / 'rgba').glob('f_*.png'))
            vals = [hot(Image.open(f)) for f in frames]
            # Added effect light, plus a penalty when a glowing weapon drops out of a frame (it read as "clean").
            score = max(vals) - floor + (floor if floor > 500 and min(vals) < .4 * floor else 0)
            take = segs / '_takes' / f'{action}_{i}_{seed}'
            shutil.rmtree(take, ignore_errors=True)
            take.mkdir(parents=True)
            for f in ('h3_24.mp4', 'spec.json'):
                shutil.copyfile(sd / f, take / f)
            (take / 'score.json').write_text(json.dumps(dict(seed=seed, added_hot_px=score)))
            scores[seed] = score
            print(f'{action}[{i}] seed {seed}: added effect px {score}', flush=True)
        best = min(scores, key=lambda s: (scores[s], s))
        print(f'{action}[{i}] best {best}', flush=True)
        set_seed(i, best)
        take = segs / '_takes' / f'{action}_{i}_{best}'
        for f in ('h3_24.mp4', 'spec.json'):
            shutil.copyfile(take / f, sd / f)
        shutil.rmtree(sd / 'rgba', ignore_errors=True)
    run(unit, 'matte', action)
    run(unit, 'pack')
    print('DONE', unit, action, flush=True)


if __name__ == '__main__':
    main()
