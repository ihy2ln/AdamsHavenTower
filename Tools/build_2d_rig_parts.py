"""Split a 2D battle stance into layered rig parts (run with ComfyUI's Python: torch, OpenCV, segment_anything).

  set RIG_ID=kaela
  "S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/python_embeded/python.exe" Tools/build_2d_rig_parts.py

Inputs: Tools/battle_rigs_2d/<id>.json (joints, draw order, SAM point prompts) and BattleRig2D/<id>/stance_cut.png
(Tools/build_2d_rig.py stance + cutout). Outputs Assets/Resources/AdamsHaven/BattleRigs2D/<id>/parts/*.png + rig.json
for Assets/Editor/Battle2DRigBuilder.cs, and debug images in BattleRig2D/<id>/.

Pixels are labelled by SAM regions (tail, back hair, side cloths) and otherwise by the nearest bone. Limb segments that
are hidden at rest (thigh tops under the shorts, the far upper arm behind the bust, the tail base behind the cloth) are
smeared out along the bone so they stay whole when the limb turns; back hair hidden by the near arm is inpainted.
"""
import json
import os
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
COMFY = Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
SAM = COMFY / 'models/sams/sam_vit_b_01ec64.pth'
RID = os.environ.get('RIG_ID', 'kaela')
SPEC = json.loads((TOOLS / 'battle_rigs_2d' / f'{RID}.json').read_text())
WORK = PROJECT / 'BattleRig2D' / RID
OUT = PROJECT / 'Assets/Resources/AdamsHaven/BattleRigs2D' / RID

BONES = {b['name']: b for b in SPEC['bones']}
LIMBS = [b for b in SPEC['bones'] if 'tip' in b]


def seg_dist(px, py, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / max(dx * dx + dy * dy, 1e-6), 0, 1)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))


def sam_regions(rgb, body):
    from segment_anything import sam_model_registry, SamPredictor
    sam = sam_model_registry['vit_b'](checkpoint=str(SAM)).to('cuda')
    pred = SamPredictor(sam)
    pred.set_image(rgb)
    out = {}
    for name, r in SPEC['regions'].items():
        pts = np.array(r['pos'] + r['neg'], np.float32)
        lab = np.array([1] * len(r['pos']) + [0] * len(r['neg']))
        masks, scores, _ = pred.predict(point_coords=pts, point_labels=lab, box=np.array(r['box'], np.float32), multimask_output=True)
        m = masks[int(np.argmax(scores))] & body
        out[name] = m
        print(f'  SAM {name}: {int(m.sum())} px, score {float(scores.max()):.3f}', flush=True)
    return out


def label_pixels(rgba, regions):
    h, w = rgba.shape[:2]
    body = rgba[..., 3] > 24
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    names = [b['name'] for b in LIMBS if not b['name'].startswith('tail') and b['name'] != 'hair']
    cost = np.full((len(names), h, w), np.inf, np.float32)
    for i, n in enumerate(names):
        b = BONES[n]
        d = seg_dist(xx, yy, b['pivot'], b['tip']) / b['r']
        # The body bones only claim their own band; limbs compete everywhere.
        if n == 'head': d[yy > 240] = np.inf
        if n == 'torso': d[(yy < 215) | (yy > 560)] = np.inf
        if n == 'hips': d[(yy < 530) | (yy > 780)] = np.inf
        cost[i] = d
    lab = np.argmin(cost, axis=0)
    # Past the far end of a limb (behind a knee, beyond an elbow) the pixel belongs to the next limb, or a bent joint
    # leaves a wedge of the upper limb sticking out.
    for i, n in enumerate(names):
        b = BONES[n]
        kids = [k for k, c in enumerate(names) if BONES[c]['parent'] == n and c in names]
        if not kids or n in ('torso', 'hips', 'head'):
            continue
        ax, ay = b['pivot']; dx, dy = b['tip'][0] - ax, b['tip'][1] - ay
        t = ((xx - ax) * dx + (yy - ay) * dy) / (dx * dx + dy * dy)
        past = (lab == i) & (t > 0.97)
        best = kids[int(np.argmin([np.nanmin(np.where(past, cost[k], np.inf)) for k in kids]))]
        lab[past] = best
    labels = np.full((h, w), '', object)
    for i, n in enumerate(names):
        labels[(lab == i) & body] = n
    # The top and bare midriff stay with the torso and the belt and shorts with the hips, whatever bone is nearer.
    if 'torso' in regions: labels[regions['torso'] & (yy > 214)] = 'torso'
    if 'shorts' in regions: labels[regions['shorts']] = 'hips'
    labels[regions['cloth_n'] | regions['cloth_f']] = 'hips'
    hair = regions['hair']
    labels[hair & (yy > 214)] = 'hair'
    labels[hair & (yy <= 214)] = 'head'
    tail = regions['tail']
    # Tail pixels go to the nearest tail bone.
    tb = [BONES[n] for n in ('tail1', 'tail2', 'tail3')]
    td = np.stack([seg_dist(xx, yy, b['pivot'], b['tip']) for b in tb])
    tl = np.argmin(td, axis=0)
    for i, n in enumerate(('tail1', 'tail2', 'tail3')):
        labels[tail & (tl == i)] = n
    labels[~body] = ''
    return labels


def clean(labels):
    """Small islands join the label that surrounds them."""
    out = labels.copy()
    for n in set(labels.ravel()) - {''}:
        m = (labels == n).astype(np.uint8)
        count, comp, stats, _ = cv2.connectedComponentsWithStats(m, 8)
        for c in range(1, count):
            if stats[c, cv2.CC_STAT_AREA] >= 400:
                continue
            spot = comp == c
            ring = cv2.dilate(spot.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool) & ~spot
            around = [v for v in labels[ring] if v and v != n]
            if around:
                out[spot] = max(set(around), key=around.count)
    return out


def drop_slivers(labels):
    """Thin outline strokes left on a limb (for example a cloth's edge line) join the label they border."""
    out = labels.copy()
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
    limbs = [b['name'] for b in LIMBS if b['name'].startswith(('arm_', 'leg_', 'hand_', 'foot_'))]
    for n in limbs:
        m = (labels == n).astype(np.uint8)
        thin = (m > 0) & (cv2.morphologyEx(m, cv2.MORPH_OPEN, k) == 0)
        ys, xs = np.nonzero(thin)
        for y, x in zip(ys, xs):
            win = labels[max(y - 6, 0):y + 7, max(x - 6, 0):x + 7].ravel()
            other = [v for v in win if v and v != n]
            if other:
                out[y, x] = max(set(other), key=other.count)
    return out


def from_donor(parts, bone):
    """A limb hidden at rest (the far upper arm behind the bust) borrows the matching near limb, shaded for depth."""
    d = BONES[bone['donor']]
    src = parts[bone['donor']]
    sp = np.array(d['pivot'], np.float32); dp = np.array(bone['pivot'], np.float32)
    sv = np.array(d['tip'], np.float32) - sp; dv = np.array(bone['tip'], np.float32) - dp
    s = float(np.hypot(*dv) / np.hypot(*sv))
    k = bone.get('thin', 1.0)     # the far limb is seen narrower, partly edge-on
    a0, a1 = np.arctan2(sv[1], sv[0]), np.arctan2(dv[1], dv[0])
    rot = lambda t: np.array([[np.cos(t), -np.sin(t)], [np.sin(t), np.cos(t)]], np.float32)
    A = rot(a1) @ np.diag([s, s * k]).astype(np.float32) @ rot(-a0)
    M = np.hstack([A, (dp - A @ sp)[:, None]]).astype(np.float32)
    h, w = src.shape[:2]
    out = cv2.warpAffine(src, M, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
    out[..., :3] = (out[..., :3].astype(np.float32) * bone.get('shade', 1.0)).astype(np.uint8)
    print(f'  donor {bone["name"]} <- {bone["donor"]}', flush=True)
    return out


def smear_fill(part, bone):
    """Extend a limb part along its bone so it has no holes where it was hidden at rest."""
    piv = np.array(bone['pivot'], np.float32); tip = np.array(bone['tip'], np.float32)
    v = tip - piv
    angle = np.degrees(np.arctan2(v[1], v[0])) - 90      # rotate so the bone points down the image
    M = cv2.getRotationMatrix2D(tuple(map(float, piv)), angle, 1.0)
    h, w = part.shape[:2]
    rot = cv2.warpAffine(part, M, (w, h), flags=cv2.INTER_NEAREST, borderValue=(0, 0, 0, 0))
    length = float(np.hypot(*v)); r = bone['r']
    x0, x1 = int(piv[0] - 1.6 * r), int(piv[0] + 1.6 * r)
    y0, y1 = int(piv[1] - 0.25 * r), int(piv[1] + length)
    x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, w - 1), min(y1, h - 1)
    widths = {y: int((rot[y, x0:x1, 3] > 128).sum()) for y in range(y0, y1)}
    good = [y for y, c in widths.items() if c > 0]
    if not good:
        return part
    full = np.median([widths[y] for y in good])
    ref = [y for y in good if widths[y] >= 0.75 * full]
    added = 0
    for y in range(y0, y1):
        if widths[y] >= 0.75 * full or not ref:
            continue
        src = min(ref, key=lambda r_: abs(r_ - y))
        row = rot[src, x0:x1]
        hole = rot[y, x0:x1, 3] < 128
        rot[y, x0:x1][hole] = row[hole]
        added += int(hole.sum())
    back = cv2.warpAffine(rot, cv2.invertAffineTransform(M), (w, h), flags=cv2.INTER_NEAREST, borderValue=(0, 0, 0, 0))
    out = part.copy()
    fill = (part[..., 3] < 128) & (back[..., 3] >= 128)
    out[fill] = back[fill]
    print(f'  fill {bone["name"]}: +{int(fill.sum())} px', flush=True)
    return out


def cut_gauntlets():
    """Two crystal gauntlets from the Celestium weapon painting, as (rgba, cuff point, claw direction)."""
    import torch
    sys.argv = sys.argv[:1]
    sys.path.insert(0, str(COMFY))
    cwd = os.getcwd(); os.chdir(COMFY)
    import comfy.bg_removal_model
    model = comfy.bg_removal_model.load(str(COMFY / 'models/background_removal/birefnet.safetensors'))
    os.chdir(cwd)
    src = Image.open(SPEC['gauntlets']['file']).convert('RGB')
    with torch.no_grad():
        mask = model.encode_image(torch.from_numpy(np.asarray(src, np.float32) / 255).unsqueeze(0))[0].float().cpu().numpy()
    alpha = (np.clip(mask, 0, 1) * 255).astype(np.uint8)
    rgba = np.dstack([np.asarray(src), alpha])
    count, comp, stats, _ = cv2.connectedComponentsWithStats((alpha > 128).astype(np.uint8), 8)
    big = sorted(range(1, count), key=lambda c: -stats[c, cv2.CC_STAT_AREA])[:2]
    found = []
    cx_img = src.width / 2
    for c in big:
        ys, xs = np.nonzero(comp == c)
        pts = np.stack([xs, ys], 1).astype(np.float32)
        mean = pts.mean(0)
        _, _, vt = np.linalg.svd(pts - mean, full_matrices=False)
        axis = vt[0]
        proj = (pts - mean) @ axis
        a_end, b_end = mean + axis * proj.min(), mean + axis * proj.max()
        cuff, claw = (a_end, b_end) if abs(a_end[0] - cx_img) < abs(b_end[0] - cx_img) else (b_end, a_end)
        x, y, w_, h_ = stats[c, :4]
        sub = rgba[y:y + h_, x:x + w_].copy()
        sub[..., 3] = np.where(comp[y:y + h_, x:x + w_] == c, sub[..., 3], 0)
        found.append((sub, cuff - [x, y], claw - [x, y], mean[0]))
    found.sort(key=lambda f: f[3])     # left one first
    return found


def place_gauntlet(canvas_shape, g, bone, length, flip):
    img, cuff, claw, _ = g
    if flip:
        img = img[:, ::-1].copy()
        cuff = np.array([img.shape[1] - 1 - cuff[0], cuff[1]]); claw = np.array([img.shape[1] - 1 - claw[0], claw[1]])
    src_dir = claw - cuff
    src_len = float(np.hypot(*src_dir))
    piv = np.array(bone['pivot'], np.float32); tip = np.array(bone['tip'], np.float32)
    dst_dir = (tip - piv) / max(np.hypot(*(tip - piv)), 1e-6)
    s = length / src_len
    ang = np.degrees(np.arctan2(dst_dir[1], dst_dir[0]) - np.arctan2(src_dir[1], src_dir[0]))
    # The cuff sits a little up the forearm from the wrist so the glove covers it.
    anchor = piv - dst_dir * length * 0.18
    M = cv2.getRotationMatrix2D(tuple(map(float, cuff)), -ang, s)
    M[:, 2] += anchor - cuff
    h, w = canvas_shape
    return cv2.warpAffine(img, M, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))


def main():
    rgba = np.asarray(Image.open(WORK / 'stance_cut.png').convert('RGBA')).copy()
    stance = np.asarray(Image.open(WORK / SPEC['stance']).convert('RGB'))
    body = rgba[..., 3] > 24
    print('SAM regions', flush=True)
    regions = sam_regions(stance, body)
    labels = clean(drop_slivers(label_pixels(rgba, regions)))
    h, w = labels.shape

    # Debug: label colours.
    names = SPEC['order']
    rng = np.random.default_rng(3)
    colors = {n: rng.integers(60, 255, 3) for n in names}
    dbg = np.zeros((h, w, 3), np.uint8) + 40
    for n, c in colors.items():
        dbg[labels == n] = c
    for b in SPEC['bones']:
        p = tuple(map(int, b['pivot']))
        cv2.circle(dbg, p, 6, (255, 255, 255), -1)
        if 'tip' in b:
            cv2.line(dbg, p, tuple(map(int, b['tip'])), (0, 0, 0), 2)
    Image.fromarray(dbg).save(WORK / 'labels.png')

    parts = {}
    for n in names:
        if n.startswith('gauntlet'):
            continue
        m = labels == n
        part = np.zeros_like(rgba)
        part[m] = rgba[m]
        b = BONES[n]
        # Elbow, knee, wrist, ankle and tail joints: a disc of the joint itself rides with the child, hiding the seam.
        if b['parent'] not in ('', 'root', 'hips', 'torso', 'head'):
            disc = np.zeros((h, w), np.uint8)
            cv2.circle(disc, tuple(map(int, b['pivot'])), int(b['r'] * 0.5), 1, -1)
            add = disc.astype(bool) & body & (part[..., 3] == 0)
            part[add] = rgba[add]
        if b.get('fill'):
            part = smear_fill(part, b)
        parts[n] = part
    for n in names:
        b = BONES.get(n)
        if b and 'donor' in b:
            parts[n] = from_donor(parts, b)

    # Back hair hidden behind the near arm: inpaint the gap so it does not open up when the arm moves.
    hair = parts['hair']
    hm = (hair[..., 3] > 128).astype(np.uint8)
    closed = cv2.morphologyEx(hm, cv2.MORPH_CLOSE, np.ones((41, 41), np.uint8))
    arm = np.isin(labels, ['arm_n_up', 'arm_n_lo', 'hand_n'])
    gap = (closed > 0) & (hm == 0) & arm
    if gap.any():
        filled = cv2.inpaint(np.ascontiguousarray(rgba[..., :3]), gap.astype(np.uint8) * 255, 9, cv2.INPAINT_TELEA)
        hair[gap, :3] = filled[gap]
        hair[gap, 3] = 255
        print(f'  hair inpaint: +{int(gap.sum())} px', flush=True)

    # Gauntlets on the hand bones.
    if 'gauntlets' in SPEC:
        g = cut_gauntlets()
        L = SPEC['gauntlets']['width']
        parts['gauntlet_f'] = place_gauntlet((h, w), g[1], BONES['hand_f'], L, False)
        parts['gauntlet_n'] = place_gauntlet((h, w), g[0], BONES['hand_n'], L, True)

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'parts').mkdir(exist_ok=True)
    top = int(np.nonzero(body.any(axis=1))[0][0])
    ppu = (SPEC['feet'][1] - top) / SPEC['height']
    rig = {'id': RID, 'ppu': round(float(ppu), 3), 'image': [w, h], 'feet': SPEC['feet'], 'bones': [], 'parts': []}
    for b in SPEC['bones']:
        rig['bones'].append({'name': b['name'], 'parent': b['parent'], 'pivot': b['pivot'], 'tip': b.get('tip', [])})
    composite = np.zeros((h, w, 4), np.float32)
    for order, n in enumerate(names):
        part = parts.get(n)
        if part is None or not (part[..., 3] > 0).any():
            print('  empty part', n)
            continue
        ys, xs = np.nonzero(part[..., 3] > 0)
        x0, y0 = max(int(xs.min()) - 4, 0), max(int(ys.min()) - 4, 0)
        x1, y1 = min(int(xs.max()) + 5, w), min(int(ys.max()) + 5, h)
        Image.fromarray(part[y0:y1, x0:x1], 'RGBA').save(OUT / 'parts' / f'{n}.png')
        bone = n.replace('gauntlet_', 'hand_') if n.startswith('gauntlet') else n
        rig['parts'].append({'name': n, 'bone': bone, 'file': f'parts/{n}.png', 'x': x0, 'y': y0, 'w': x1 - x0, 'h': y1 - y0, 'order': order})
        a = part[..., 3:4].astype(np.float32) / 255
        composite[..., :3] = composite[..., :3] * (1 - a) + part[..., :3].astype(np.float32) * a
        composite[..., 3:4] = composite[..., 3:4] * (1 - a) + 255 * a
    (OUT / 'rig.json').write_text(json.dumps(rig, indent=1))
    prev = Image.new('RGBA', (w, h), (70, 72, 82, 255))
    prev.alpha_composite(Image.fromarray(np.clip(composite, 0, 255).astype(np.uint8), 'RGBA'))
    prev.convert('RGB').save(WORK / 'assembled.png')
    print('rig ->', OUT / 'rig.json', 'ppu', rig['ppu'], 'parts', len(rig['parts']))


if __name__ == '__main__':
    main()
