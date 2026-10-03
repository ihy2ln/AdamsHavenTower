"""Render the TownPrompts reference pictures with local ComfyUI (Qwen Image 2.1, the same graph the character views
use). F-D pictures render from text; C-B and A-SSR attach the type's F-D picture as an identity reference.

  python Tools/render_town_refs.py inn            # one type, all three bands
  python Tools/render_town_refs.py inn cottage    # several types
  python Tools/render_town_refs.py all            # the whole pack (48 pictures)
  python Tools/render_town_refs.py inn --seed 7   # reroll

Outputs land next to the prompts: TownPrompts/<district>/<type>/<file>.png (and TownPrompts/tower/).
"""
from pathlib import Path
import json, shutil, sys, time

sys.path.insert(0, str(Path(__file__).resolve().parent))
import produce_battle_motion as M

PROJECT = Path(__file__).resolve().parent.parent
PACK = PROJECT / 'TownPrompts'
QUEUE = [json.loads(l) for l in (PACK / 'prompt_queue.jsonl').read_text(encoding='utf-8').splitlines() if l.strip()]
SIZES = {'1536x1152': (1536, 1152), '1152x1536': (1152, 1536)}


def render(row, seed):
    out = PROJECT / row['file']
    refs = []
    if row['references']:
        ref = out.parent / row['references']
        if not ref.exists():
            raise SystemExit(f'reference missing, render the F-D picture first: {ref}')
        refs = [M.to_input(M.flat_ref(ref), f'town_{ref.stem}.png')]
    prefix = f"AdamsHaven/Town/{row['type']}_{row['band'].replace('-', '').lower()}"
    t0 = time.time()
    files = M.run(M.qwen_edit(refs, row['prompt'], SIZES[row['canvas']], seed, prefix), 'town-refs')
    out.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(files[-1], out)
    print(f"  {out.relative_to(PROJECT)}  ({time.time() - t0:.0f}s)", flush=True)


def main(args):
    seed = 42
    if '--seed' in args:
        i = args.index('--seed'); seed = int(args[i + 1]); del args[i:i + 2]
    types = None if args == ['all'] else set(args)
    rows = [r for r in QUEUE if types is None or r['type'] in types]
    if not rows:
        raise SystemExit('no such type; see TownPrompts/README.md')
    # F-D first so the later bands can attach it.
    rows.sort(key=lambda r: (r['type'], ['F-D', 'C-B', 'A-SSR'].index(r['band'])))
    for row in rows:
        print(row['type'], row['band'], flush=True)
        render(row, seed)


if __name__ == '__main__':
    main(sys.argv[1:] or ['inn'])
