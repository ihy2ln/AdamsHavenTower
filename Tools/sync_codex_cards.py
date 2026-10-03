"""Bestiary card art for the game's BESTIARY + CODEX screen (Assets/Scripts/Codex/CodexPanel.cs).

  python Tools/sync_codex_cards.py            # copy what MonsterPrompts has so far (safe to re-run any time)
  python Tools/sync_codex_cards.py --check    # report only: forms with art, and the move-panel detection

For every bestiary form with finished art in MonsterPrompts/<family>/<form>/ it writes, under
Assets/Resources/AdamsHaven/Bestiary/Cards/:
  <form>_card.jpg    playing_card.png (card front illustration), 640x960
  <form>_thumb.jpg   the same front at 256x384 for the card grid
  <form>_moves.jpg   action_card.png (the move-list card with four blank panels), 640x960
  <form>_shadow.png  the silhouette of battle_front.png (white, with its alpha), trimmed, longest side 384: unmet
                     beasts are drawn as this shape in black
  field_<art>_shadow.png  the same for each battle cutout in FieldModels whose border is fully transparent, for forms
                     without their own battle sprite yet (cutouts that kept painted background get none)
It also gives every form its own battle cutout: battle_left.png (the sprite facing the party, like the original
cutouts; battle_front.png when there is no left view) goes to Assets/Resources/AdamsHaven/FieldModels/<form>.png,
trimmed, longest side 1024. The 12 original hand-placed cutouts are kept. Run Tools/build_bestiary.py afterwards so
bestiary.json points each form at its own art.
and panels.json: for each moves card, the four blank text panels found on it (normalised x, y, w, h from the
top-left, 16 numbers), so the game writes each move into its own panel. A card whose panels cannot be found gets no
entry and the game draws its own panels instead. MonsterPrompts is read only; art still being generated is picked up
next run, and the game shows it the next time the codex opens (in the Editor, after Unity imports it).
"""
import json, os, sys
import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(PROJECT, 'MonsterPrompts')
OUT = os.path.join(PROJECT, 'Assets', 'Resources', 'AdamsHaven', 'Bestiary', 'Cards')
BESTIARY = os.path.join(PROJECT, 'Assets', 'Resources', 'AdamsHaven', 'Bestiary', 'bestiary.json')
SIZE, THUMB = (640, 960), (256, 384)
SHADOW = 384
FIELD_MAX = 1024
# The original cutouts, placed and sized by hand in BattleMode.EnemyHeight: the sprites never replace these.
KEEP_FIELD = {'amberhide_grazer', 'ashvein_goblin_scavenger', 'blood_opal_siren', 'cobalt_burrower', 'crowned_geode_knight',
              'eclipse_core_golem', 'flintjaw_skitterer', 'glasswing_mite', 'moonstone_ravager', 'obsidian_maw_behemoth',
              'obsidian_talon', 'quartzback_hound', 'shardling_sprout', 'stormglass_wyvern', 'thorncrystal_stalker',
              'verdant_cathedral_hydra', 'viridian_prism_warden'}


def field_cutout(src, dst):
    """The battle sprite as a field cutout: trimmed to its alpha with a small margin, longest side FIELD_MAX."""
    if os.path.exists(dst) and os.path.getmtime(dst) >= os.path.getmtime(src): return
    im = Image.open(src).convert('RGBA')
    box = im.getchannel('A').point(lambda v: 255 if v > 20 else 0).getbbox()
    if box:
        m = 12
        im = im.crop((max(0, box[0] - m), max(0, box[1] - m), min(im.width, box[2] + m), min(im.height, box[3] + m)))
    scale = FIELD_MAX / max(im.size)
    if scale < 1: im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
    im.save(dst, optimize=True)
FIELD = os.path.join(PROJECT, 'Assets', 'Resources', 'AdamsHaven', 'FieldModels')


def shadow(src, dst, strict=False):
    """White silhouette of a cutout (its alpha), trimmed and scaled. strict: only when the image border is clear, so a
    cutout that kept chunks of its painted background makes no silhouette (and any old one is removed)."""
    if os.path.exists(dst) and os.path.getmtime(dst) >= os.path.getmtime(src): return True
    alpha = Image.open(src).convert('RGBA').getchannel('A')
    a = np.asarray(alpha)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    if strict and (border < 20).mean() < .95:
        if os.path.exists(dst): os.remove(dst)
        return False
    box = alpha.point(lambda v: 255 if v > 20 else 0).getbbox()
    if not box: return False
    alpha = alpha.crop(box)
    scale = SHADOW / max(alpha.size)
    if scale < 1: alpha = alpha.resize((max(1, round(alpha.width * scale)), max(1, round(alpha.height * scale))), Image.LANCZOS)
    out = Image.new('RGBA', alpha.size, (255, 255, 255, 0))
    out.putalpha(alpha)
    out.save(dst, optimize=True)
    return True


def runs(mask, min_len, max_gap=2):
    """Runs of True in a 1-D mask, bridging gaps of at most max_gap."""
    out, start, gap = [], None, 0
    for i, v in enumerate(list(mask) + [False] * (max_gap + 1)):
        if v:
            if start is None: start = i
            gap = 0
        elif start is not None:
            gap += 1
            if gap > max_gap:
                end = i - gap + 1
                if end - start >= min_len: out.append((start, end))
                start, gap = None, 0
    return out


def find_panels(path):
    """The four stacked move panels on an action card, as (x, y, w, h) normalised, top to bottom; None if not found."""
    im = np.asarray(Image.open(path).convert('RGB').resize((512, 768)), np.float32) / 255
    lum = im.mean(axis=2)
    H, W = lum.shape
    prof = lum[:, int(W * .35):int(W * .9)].mean(axis=1)
    prof = np.convolve(prof, np.ones(3) / 3, mode='same')
    cand = [r for r in runs(prof < .21, int(H * .045)) if r[0] > H * .28 and r[1] < H * .985]
    if len(cand) < 4: return None
    # The four panels are similar in height; take the run of four whose heights agree best.
    best, score = None, 1e9
    for i in range(len(cand) - 3):
        group = cand[i:i + 4]
        hs = [b - a for a, b in group]
        s = (max(hs) - min(hs)) / max(1, np.mean(hs))
        if s < score: best, score = group, s
    if best is None or score > .45: return None
    # The panels share one column, so their horizontal edges are the median over all four (a swirl in the art can
    # cut one short), kept right of the move icons and inside the frame.
    xs = []
    for a, b in best:
        cols = lum[a + 3:b - 3].mean(axis=0)
        cols = np.convolve(cols, np.ones(5) / 5, mode='same')
        spans = runs(cols < .24, int(W * .35), max_gap=4)
        if spans: xs.append(max(spans, key=lambda r: r[1] - r[0]))
    if not xs: return None
    x0 = min(max(float(np.median([x for x, _ in xs])) / W, .19), .3)
    x1 = max(min(float(np.median([x for _, x in xs])) / W, .95), .8)
    return [[round(x0, 4), round(a / H, 4), round(x1 - x0, 4), round((b - a) / H, 4)] for a, b in best]


def main():
    check = '--check' in sys.argv
    data = json.load(open(BESTIARY, encoding='utf-8-sig'))
    os.makedirs(OUT, exist_ok=True)
    panels_path = os.path.join(OUT, 'panels.json')
    panels = {}
    if os.path.exists(panels_path):   # {"cards": [{"id", "panels": [16 numbers]}]}, the shape Unity's JsonUtility reads
        for c in json.load(open(panels_path, encoding='utf-8')).get('cards', []):
            panels[c['id']] = c['panels']
    have = missing = found = shadows = fields = 0
    if not check:
        for name in sorted(os.listdir(FIELD)):
            if name.endswith('.png'):
                shadow(os.path.join(FIELD, name), os.path.join(OUT, 'field_' + name[:-4] + '_shadow.png'), strict=True)
    for family in data['families']:
        for form in family['forms']:
            folder = os.path.join(SRC, family['id'], form['id'])
            sprite = os.path.join(folder, 'battle_front.png')
            if os.path.exists(sprite):
                shadows += 1
                if not check: shadow(sprite, os.path.join(OUT, form['id'] + '_shadow.png'))
            side = os.path.join(folder, 'battle_left.png')
            side = side if os.path.exists(side) else sprite
            if os.path.exists(side) and form['id'] not in KEEP_FIELD:
                fields += 1
                if not check: field_cutout(side, os.path.join(FIELD, form['id'] + '.png'))
            card, moves = os.path.join(folder, 'playing_card.png'), os.path.join(folder, 'action_card.png')
            if not os.path.exists(card):
                missing += 1
                continue
            have += 1
            if not check:
                for src, name, size in ((card, '_card', SIZE), (card, '_thumb', THUMB), (moves, '_moves', SIZE)):
                    if not os.path.exists(src): continue
                    dst = os.path.join(OUT, form['id'] + name + '.jpg')
                    if os.path.exists(dst) and os.path.getmtime(dst) >= os.path.getmtime(src): continue
                    Image.open(src).convert('RGB').resize(size, Image.LANCZOS).save(dst, quality=88, optimize=True)
            if os.path.exists(moves):
                p = find_panels(moves)
                if p:
                    found += 1
                    panels[form['id']] = [v for rect in p for v in rect]
                else:
                    panels.pop(form['id'], None)
                    print('panels not found:', form['id'])
    if not check:
        cards = [dict(id=k, panels=panels[k]) for k in sorted(panels)]
        json.dump(dict(cards=cards), open(panels_path, 'w', encoding='utf-8'), indent=1)
    print('forms with art %d, still to come %d, move panels found on %d, silhouettes %d, field cutouts %d' % (have, missing, found, shadows, fields))


if __name__ == '__main__':
    main()
