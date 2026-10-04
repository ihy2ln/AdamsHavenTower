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
import numpy as np
from PIL import Image

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


COPY_BELOW = 19.0   # mean grey difference (0-255, at 96x72) under which an upgrade is a copy of its reference


# Types whose shape must survive the upgrade (the generic lead-in grows everything into a big hall).
EDIT_KEEP = {
    'watchtower': ("Keep it ONE slender, tall watchtower: a narrow square stone tower with a lookout platform and a "
                   "brazier on top, standing alone on a small base. It is not a house, hall or manor; grow it taller "
                   "and grander (buttressed dressed-stone base, carved lookout, Celestium lanterns), not wider."),
    'wallsegment': ("Keep it a straight stretch of town wall with a crenellated walkway; make it taller and finer "
                    "(dressed stone, buttresses, a small guard turret), not a house."),
    'gatehouse': "Keep the arched road passage through it and the two flanking towers; it stays a gatehouse.",
    'stall': "Keep it a small open-fronted market stall with an awning and goods on the counter, not a shop hall.",
}


def difference(a, b):
    """How different two pictures are: mean absolute grey difference at 96x72. Measured on the first pack: copies of
    the F-D reference score 13-15, real C-B / A-SSR upgrades 23-29."""
    A = np.asarray(Image.open(a).convert('L').resize((96, 72)), float)
    B = np.asarray(Image.open(b).convert('L').resize((96, 72)), float)
    return float(np.abs(A - B).mean())


def queue(refs, prompt, row, seed):
    prefix = f"AdamsHaven/Town/{row['type']}_{row['band'].replace('-', '').lower()}"
    for attempt in range(6):
        try:
            return M.run(M.qwen_edit(refs, prompt, SIZES[row['canvas']], seed, prefix), 'town-refs')[-1]
        except (OSError, urllib.error.URLError) as e:   # the local server restarts now and then: wait and re-queue
            print(f'  ComfyUI unreachable ({e}); retry {attempt + 1}/6 in 30s', flush=True)
            time.sleep(30)
    raise SystemExit('ComfyUI stayed unreachable')


def render(row, seed):
    out = PROJECT / row['file']
    t0 = time.time()
    ref = out.parent / row['references'] if row['references'] else None
    result, how = None, 'text'
    if ref is not None and not TEXT_ONLY:
        if not ref.exists():
            raise SystemExit(f'reference missing, render the F-D picture first: {ref}')
        refs = [M.to_input(M.flat_ref(ref), f'town_{ref.stem}.png')]
        # Given the full description, the edit model redraws the reference as it is. Ask for the upgrade instead,
        # and reject copies: one reroll, then fall back to the prompt alone (as the F-D pictures are made).
        prompt = EDIT_LEAD[row['band']] + ' ' + EDIT_KEEP.get(row['type'], '') + ' ' + row['prompt']
        for s in (seed, seed + 1000):
            result = queue(refs, prompt, row, s)
            diff = difference(ref, result)
            if diff >= COPY_BELOW:
                how = f'upgrade of {ref.name}, difference {diff:.1f}'
                break
            print(f'  copy of the reference (difference {diff:.1f}); ' + ('rerolling' if s == seed else 'text only'), flush=True)
            result = None
    if result is None:
        result = queue([], row['prompt'], row, seed)
    out.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy(result, out)
    print(f"  {out.relative_to(PROJECT)}  ({how}, {time.time() - t0:.0f}s)", flush=True)


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
