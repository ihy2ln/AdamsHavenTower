"""Batch the monster clips (CM 10.3.5): every form in Tools/enemy_clips/_order.json, keys -> motion -> matte -> pack.

  python Tools/produce_enemy_clips.py                 # all forms still missing a pack, in production order
  python Tools/produce_enemy_clips.py --from flintjaw_skitterer
  python Tools/produce_enemy_clips.py --only ashvein_hobgoblin,amberhide_grazer
  python Tools/produce_enemy_clips.py --status        # what is done

Each form runs in its own process, so one failure (a ComfyUI hiccup) only skips that form; it is logged and the batch
moves on. Progress: BattleMotion/_enemy_batch.json and _enemy_batch.log. Re-running picks up where it stopped (every
stage caches by prompt, seed and keys). Specs come from Tools/build_enemy_clip_specs.py.
"""
import json
import subprocess
import sys
import time
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
ORDER = TOOLS / 'enemy_clips' / '_order.json'
CLIPS = PROJECT / 'Assets/Resources/AdamsHaven/BattleClips'
STATUS = PROJECT / 'BattleMotion' / '_enemy_batch.json'
LOG = PROJECT / 'BattleMotion' / '_enemy_batch.log'


def done(form):
    return (CLIPS / form / 'clips.json').exists()


def log(line):
    stamp = time.strftime('%Y-%m-%d %H:%M:%S')
    with LOG.open('a', encoding='utf-8') as f:
        f.write(f'{stamp} {line}\n')
    print(stamp, line, flush=True)


def main():
    order = json.loads(ORDER.read_text(encoding='utf-8'))
    status = json.loads(STATUS.read_text(encoding='utf-8')) if STATUS.exists() else {}
    if '--status' in sys.argv:
        finished = [f for f in order if done(f)]
        print(f'{len(finished)}/{len(order)} packed')
        for f in order:
            print(f"{f:34s} {'packed' if done(f) else status.get(f, {}).get('state', '-')}")
        return
    todo = [f for f in order if not done(f)]
    if '--from' in sys.argv:
        start = sys.argv[sys.argv.index('--from') + 1]
        todo = todo[todo.index(start):] if start in todo else todo
    if '--only' in sys.argv:
        only = sys.argv[sys.argv.index('--only') + 1].split(',')
        todo = [f for f in order if f in only]
    log(f'batch: {len(todo)} forms')
    for i, form in enumerate(todo):
        t0 = time.time()
        log(f'[{i + 1}/{len(todo)}] {form}')
        state = 'packed'
        for stage in ('keys', 'motion', 'matte', 'pack'):
            r = subprocess.run([sys.executable, str(TOOLS / 'produce_fighter_clips.py'), form, stage], cwd=PROJECT,
                               capture_output=True, text=True, encoding='utf-8', errors='replace')
            if r.returncode != 0:
                state = f'failed at {stage}'
                log(f'  {form} {state}: ' + (r.stderr.strip().splitlines() or ['?'])[-1][:300])
                break
        status[form] = dict(state=state, minutes=round((time.time() - t0) / 60, 1), at=time.strftime('%Y-%m-%d %H:%M'))
        STATUS.write_text(json.dumps(status, indent=1), encoding='utf-8')
        log(f'  {form} {state} in {status[form]["minutes"]} min')
    log('batch done: ' + str(sum(done(f) for f in order)) + f'/{len(order)} packed')


if __name__ == '__main__':
    main()
