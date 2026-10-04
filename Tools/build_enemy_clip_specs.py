"""Clip specs for every bestiary form (CM 10.3.5): Tools/enemy_clips/<form>.json for produce_fighter_clips.py.

  python Tools/build_enemy_clip_specs.py              # write every spec (and the flattened guard sources)
  python Tools/build_enemy_clip_specs.py --list       # forms in production order, with their action counts

A monster gets the same kinds of animation as a party fighter: an idle loop, one action per move (2 to 5 by tier,
bestiary.json), a hit reaction, a block, a knockdown and a victory pose. Monsters have no combos. The guard key is the
bestiary's left-side battle view (MonsterPrompts/<family>/<form>/battle_left.png, facing the left like every enemy);
the front and back views are the key references. Production: python Tools/produce_enemy_clips.py.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
BESTIARY = PROJECT / 'Assets/Resources/AdamsHaven/Bestiary/bestiary.json'
PROMPTS = PROJECT / 'MonsterPrompts'
OUT = TOOLS / 'enemy_clips'
WORK = PROJECT / 'BattleMotion'
BG = (118, 118, 118)
CANVAS = 960
FEET_Y, BODY_X = 896, 560          # feet right of centre: a monster strikes toward the left


def seed(*parts):
    return 20000 + int(hashlib.sha1('/'.join(parts).encode()).hexdigest()[:6], 16) % 70000


def slug(card_id):
    return re.sub(r'^e_', '', card_id)


def look_of(form_dir, name):
    """The creature's description from its front-view prompt (identity, anatomy, rank cue, core, element)."""
    text = (form_dir / 'PROMPTS.md').read_text(encoding='utf-8')
    block = re.findall(r'```text\n(.*?)```', text, re.S)[0]
    i = block.find(name + ', a ')
    body = block[i:] if i >= 0 else block
    for stop in (' Hostile, menacing expression.', ' Mood:', ' Genuine RGBA'):
        j = body.find(stop)
        if j > 0:
            body = body[:j]
    body = re.sub(r' \(D&D[^)]*\)', '', body)
    # The side-view guard is the identity: the front view's "core orb embedded visibly in its chest" made Qwen paint
    # an orb (and extra crystals) the guard does not show, which H3 then grew in mid-move.
    body = re.sub(r' A [a-z -]+-sized GKOM core orb[^.]*\.', '', body)
    return body.strip().rstrip('.')


def kind_of(card):
    if card['power'] > 0:
        if card['target'] == 'AllEnemies':
            return 'area'
        return 'ranged' if card['magic'] else 'melee'
    return 'mend' if card['heal'] > 0 else 'buff'


POSES = {
    'melee': ('the peak of its {name} attack: it lunges forward toward the left side of the picture and strikes at full '
              'reach, body stretched into the blow, weight thrown onto its front. If it holds a weapon in image 1, the '
              'whole weapon is clearly visible in its hand, thrust forward; otherwise it strikes with claws, horns or jaws.'),
    'ranged': ('the peak of its {name}: it rears up and thrusts its head, hands or crystals forward toward the left side '
               'of the picture as if hurling power, mouth open, body braced.'),
    'area': ('the peak of its {name}: a huge wide attack, it rears up tall and sweeps or slams its whole body forward '
             'toward the left side of the picture with its limbs spread wide.'),
    'buff': ('using {name}: it rears up tall and puffs itself up, chest out, head raised high, crystals flared, '
             'gathering power.'),
    'mend': ('using {name}: it stands tall and still with its head bowed and its limbs drawn in, calm and focused.'),
}
MOTION = {
    'melee': ('It performs {name}: keeping its feet on the ground, it steps forward to the left and strikes hard.', 'It pulls back and settles into its battle stance.'),
    'ranged': ('It performs {name}: it rears and hurls its power forward to the left.', 'It lowers itself and returns to its battle stance.'),
    'area': ('It performs {name}: without leaving the ground, it rears up and unleashes a huge sweeping attack forward to the left.', 'It recovers and settles back into its battle stance.'),
    'buff': ('It uses {name}: it rears up and swells with power.', 'It settles back down into its battle stance.'),
    'mend': ('It uses {name}: it stands still and gathers itself, calm and focused.', 'It returns to its battle stance.'),
}
# Every pose: Qwen turns a recoiling or roaring creature around and drops held weapons unless told otherwise.
POSE_TAIL = (' It still faces the left side of the picture, exactly as in image 1, and keeps hold of any weapon it '
             'carries. Only the crystal growths and core it has in image 1, no new or larger ones.')
HEIGHT = {'melee': .95, 'ranged': 1.0, 'area': 1.05, 'buff': 1.08, 'mend': 1.0}


def figure_box(path):
    a = np.asarray(Image.open(path).convert('RGBA').getchannel('A')) > 128
    ys, xs = np.nonzero(a)
    return xs.min(), ys.min(), xs.max(), ys.max()


def spec_for(family, form):
    fid = form['id']
    src_dir = PROMPTS / family['id'] / fid
    left = src_dir / 'battle_left.png'
    if not left.exists():
        return None
    work = WORK / f'enemy_{fid}'
    work.mkdir(parents=True, exist_ok=True)
    guard_src = work / 'guard_src.png'
    if not guard_src.exists() or guard_src.stat().st_mtime < left.stat().st_mtime:
        img = Image.open(left).convert('RGBA')
        flat = Image.new('RGBA', img.size, BG + (255,))
        flat.alpha_composite(img)
        flat.convert('RGB').save(guard_src)
    l, t, r, b = figure_box(left)
    w, h = r - l + 1, b - t + 1
    body_px = int(min(600, 560 * h / w))            # fits 560 wide and 600 tall on the 960 canvas
    look = look_of(src_dir, form['name'])
    keys, actions, cards = {}, {}, {}
    basic = None
    for c in form['cards']:
        k, name = kind_of(c), c['name']
        key = slug(c['id']) + '_hit'
        keys[key] = dict(height=HEIGHT[k], seed=seed(fid, key), pose=POSES[k].format(name=name) + POSE_TAIL)
        if basic is None and c['power'] > 0:
            action = 'AH_attack_basic'
            basic = c
        else:
            action = 'AH_skill_' + slug(c['id'])
        go, back = MOTION[k]
        # Motion text without the move's name: H3 draws what a name like "Crystal Shiv" suggests.
        act = dict(contact=key, segments=[['guard', key, 17, seed(fid, action, '0'), go.format(name='its attack' if c['power'] > 0 else 'its power')],
                                         [key, 'guard', 17, seed(fid, action, '1'), back]])
        if c['power'] > 0:
            act['attack'] = True
        if k in ('ranged', 'area') and c['magic']:
            act['release'] = True
            act['flight'] = 4
        actions[action] = act
        cards[c['id']] = action
    keys.update({
        'hit': dict(height=.95, seed=seed(fid, 'hit'), pose='flinching from a heavy blow that came from the left: it '
                    'hunches and leans its head and shoulders back, eyes squeezed shut, mouth open in pain, weight on its '
                    'back legs.'),
        'block': dict(height=.88, seed=seed(fid, 'block'), pose='a braced block: it hunches low behind its raised arms, '
                      'shell or armoured side, head tucked in, braced for impact.'),
        'down': dict(width=1.05, anchor_x='center', seed=seed(fid, 'down'), pose='defeated: it lies collapsed on its side on '
                     'the ground, limp and still, eyes closed, the whole body lying along the same ground line.'),
        'victory': dict(height=1.08, seed=seed(fid, 'victory'), pose='a triumphant pose: it rears up tall, roaring, head '
                        'raised high, roaring toward the left.'),
    })
    actions = dict(
        AH_battle_guard=dict(loop=True, segments=[['guard', 'guard', 49, seed(fid, 'idle'),
                             'It holds its battle stance and breathes, shifting its weight slightly, head and tail moving a '
                             'little. A subtle idle loop: it ends exactly as it began.']]),
        **actions,
        AH_hit_react=dict(segments=[['guard', 'hit', 9, seed(fid, 'hit0'), 'It is struck hard and recoils backward to the right.'],
                                    ['hit', 'guard', 17, seed(fid, 'hit1'), 'It shakes off the hit and recovers into its battle stance.']]),
        AH_block=dict(segments=[['guard', 'block', 9, seed(fid, 'block0'), 'It hunches and braces to block.'],
                                ['block', 'guard', 17, seed(fid, 'block1'), 'It lowers its guard and returns to its battle stance.']]),
        AH_knock_down=dict(hold=True, segments=[['guard', 'down', 25, seed(fid, 'down0'), 'It is knocked off its feet and collapses onto the ground, lying still.']]),
        AH_victory=dict(hold=True, segments=[['guard', 'victory', 25, seed(fid, 'victory0'), 'It rears up and roars in triumph.']]),
    )
    # Boss moves borrow the form's own: Gathering Fury its first buff (else the block), the signature its last attack.
    buffs = [cards[c['id']] for c in form['cards'] if c['power'] <= 0]
    hits = [cards[c['id']] for c in form['cards'] if c['power'] > 0]
    cards['e_gather'] = buffs[0] if buffs else 'AH_block'
    cards['e_cataclysm'] = hits[-1] if hits else 'AH_attack_basic'
    for name in ('hit', 'block', 'down', 'victory'):
        keys[name]['pose'] += POSE_TAIL
    return dict(
        unit=fid, kind='monster', family=family['id'], rank=[form['minRank'], form['maxRank']],
        _doc='Generated by Tools/build_enemy_clip_specs.py from bestiary.json and MonsterPrompts; edit there, not here.',
        canvas=[CANVAS, CANVAS], bg=list(BG), body_px=body_px, body_x=BODY_X, feet_y=FEET_Y, fps_out=12,
        ppu_out=200, ppu_hi=240, look=look,
        # Only the guard (image 1): the front view's extra crystals and chest orb made H3 morph them in mid-move.
        ref_files=[],
        guard=dict(source=str(guard_src), source_bg=list(BG)),
        keys=keys, actions=actions, cards=cards, hi_actions=[])


def apply_overrides(spec):
    """Hand fixes that survive regeneration (Tools/enemy_clips/_overrides.json): {form: {"keys": {key: {...}},
    "actions": {action: {...}}}}, merged over the generated entries (a re-rolled seed, a reworded pose)."""
    path = OUT / '_overrides.json'
    if not path.exists():
        return spec
    o = json.loads(path.read_text(encoding='utf-8')).get(spec['unit'], {})
    for part in ('keys', 'actions'):
        for name, fields in o.get(part, {}).items():
            if name in spec[part]:
                spec[part][name].update(fields)
    return spec


def production_order(bestiary):
    """Early-game monsters first (lowest rank), then by family; lair-boss families' top forms come with their rank."""
    forms = [(f, fm) for f in bestiary['families'] for fm in f['forms']]
    return sorted(forms, key=lambda p: (p[1]['minRank'], p[0]['id'], p[1]['stage']))


def main():
    bestiary = json.loads(BESTIARY.read_text(encoding='utf-8'))
    OUT.mkdir(exist_ok=True)
    order = production_order(bestiary)
    rows = []
    for family, form in order:
        spec = spec_for(family, form)
        if spec is not None:
            spec = apply_overrides(spec)
        if spec is None:
            print('no battle_left view:', form['id'])
            continue
        if '--list' not in sys.argv:
            (OUT / f"{form['id']}.json").write_text(json.dumps(spec, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')
        segs = sum(len(a['segments']) for a in spec['actions'].values())
        rows.append((form['id'], len(form['cards']), len(spec['keys']), segs))
    (OUT / '_order.json').write_text(json.dumps([r[0] for r in rows], indent=1) + '\n', encoding='utf-8')
    for r in rows if '--list' in sys.argv else []:
        print(f'{r[0]:34s} moves {r[1]}  keys {r[2]:2d}  segments {r[3]:2d}')
    print(len(rows), 'specs;', sum(r[2] for r in rows), 'keys,', sum(r[3] for r in rows), 'H3 segments ->', OUT)


if __name__ == '__main__':
    main()
