"""Render the TownPrompts reference pictures with local ComfyUI (Qwen Image 2.1, the same graph the character views
use). F-D pictures render from text; C-B and A-SSR attach the type's F-D picture as an identity reference.

  python Tools/render_town_refs.py inn            # one type, all three bands
  python Tools/render_town_refs.py inn cottage    # several types
  python Tools/render_town_refs.py all            # the whole pack (48 pictures)
  python Tools/render_town_refs.py inn --seed 7 --force   # reroll (existing pictures are kept without --force)
  python Tools/render_town_refs.py inn --band C-B,A-SSR --text   # only some bands; --text skips the reference picture

Outputs land next to the prompts: TownPrompts/<district>/<type>/<file>.png (and TownPrompts/tower/).
"""
from pathlib import Path
import json, shutil, sys, time, urllib.error

sys.path.insert(0, str(Path(__file__).resolve().parent))
import produce_battle_motion as M

PROJECT = Path(__file__).resolve().parent.parent
PACK = PROJECT / 'TownPrompts'
QUEUE = [json.loads(l) for l in (PACK / 'prompt_queue.jsonl').read_text(encoding='utf-8').splitlines() if l.strip()]
SIZES = {'1536x1152': (1536, 1152), '1152x1536': (1152, 1536)}
TEXT_ONLY = False   # --text: render every band from its prompt alone, no reference picture
EDIT_LEAD = {
    'C-B': ("Edit: rebuild the attached village building as its upgraded TOWN version. Keep the same building family, "
            "layout idea and camera angle, but make it clearly larger (it now fills a 2-by-2 tile lot), replace the "
            "thatch with slate or shingles, add stone lower walls, a dormer, glazed windows and a small blue-silver "
            "Celestium lantern. It must look clearly richer than the reference, not a copy."),
    'A-SSR': ("Edit: rebuild the attached village building as its grand CITY version. Keep the same building family "
              "and camera angle, but make it much larger and taller (it fills a 3-by-3 tile lot), in dressed stone "
              "and polished dark timber with a tall layered slate roof, finials, carved trim, large leaded windows "
              "and softly glowing blue-silver Celestium crystal veins and lanterns. It must look dramatically grander "
              "than the reference, not a copy."),
}


def render(row, seed):
    out = PROJECT / row['file']
    refs = []
    prompt = row['prompt']
    if row['references'] and not TEXT_ONLY:
        ref = out.parent / row['references']
        if not ref.exists():
            raise SystemExit(f'reference missing, render the F-D picture first: {ref}')
        refs = [M.to_input(M.flat_ref(ref), f'town_{ref.stem}.png')]
        # Given the full description, the edit model redraws the reference as it is. Ask for the upgrade instead.
        prompt = EDIT_LEAD[row['band']] + ' ' + prompt
    prefix = f"AdamsHaven/Town/{row['type']}_{row['band'].replace('-', '').lower()}"
    t0 = time.time()
    files = None
    for attempt in range(6):
        try:
            files = M.run(M.qwen_edit(refs, prompt, SIZES[row['canvas']], seed, prefix), 'town-refs')
            break
        except (OSError, urllib.error.URLError) as e:   # the local server restarts now and then: wait and re-queue
            print(f'  ComfyUI unreachable ({e}); retry {attempt + 1}/6 in 30s', flush=True)
            time.sleep(30)
    if files is None:
        raise SystemExit('ComfyUI stayed unreachable')
    out.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(files[-1], out)
    print(f"  {out.relative_to(PROJECT)}  ({time.time() - t0:.0f}s)", flush=True)


def main(args):
    global TEXT_ONLY
    seed = 42
    bands = None
    force = '--force' in args   # without it, pictures already on disk are kept
    if force: args.remove('--force')
    if '--text' in args:
        args.remove('--text'); TEXT_ONLY = True
    if '--band' in args:
        i = args.index('--band'); bands = set(args[i + 1].split(',')); del args[i:i + 2]
    if '--seed' in args:
        i = args.index('--seed'); seed = int(args[i + 1]); del args[i:i + 2]
    types = None if args == ['all'] else set(args)
    rows = [r for r in QUEUE if (types is None or r['type'] in types) and (bands is None or r['band'] in bands)]
    if not rows:
        raise SystemExit('no such type; see TownPrompts/README.md')
    # F-D first so the later bands can attach it.
    rows.sort(key=lambda r: (r['type'], ['F-D', 'C-B', 'A-SSR'].index(r['band'])))
    for row in rows:
        if not force and (PROJECT / row['file']).exists():
            continue
        print(row['type'], row['band'], flush=True)
        render(row, seed)


if __name__ == '__main__':
    main(sys.argv[1:] or ['inn'])
