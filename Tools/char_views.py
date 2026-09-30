"""Generate left / back / right T-pose views of a character from its front reference PNG (Qwen Image Edit 2.1),
for Pixal3D multi-view conditioning.

  python Tools/char_views.py Elara          # one character
  python Tools/char_views.py all

Writes <Name>/pixal-max-v1/views/{front,left,back,right}.png on a flat grey background. 'left' means the camera stands
on the character's left side (their left arm nearest the camera).
"""
from pathlib import Path
import sys
from PIL import Image
import numpy as np
sys.path.insert(0, str(Path(__file__).parent))
import char_to_3d as C
import produce_battle_motion as M

BG = (128, 128, 128)
VIEWS = {
    'back': 'seen directly from behind (rear view), the character facing away from the camera',
    'left': "seen in exact side profile from the character's left side, the character facing to the right of the image",
    'right': "seen in exact side profile from the character's right side, the character facing to the left of the image",
}


def flat(src, size=(1024, 1536)):
    im = Image.open(src).convert('RGBA')
    bg = Image.new('RGBA', im.size, BG + (255,))
    bg.alpha_composite(im)
    return bg.convert('RGB')


def make(key):
    folder, name = C.REFS[key]
    out = C.CHARS / folder / 'pixal-max-v1' / 'views'
    out.mkdir(parents=True, exist_ok=True)
    front = flat(C.CHARS / folder / name)
    front.save(out / 'front_flat.png')
    ref = M.to_input(front, f'view_{key}_front.png')
    C_size = (1024, 1536) if front.height >= front.width else (1536, 1024)
    for view, desc in VIEWS.items():
        dst = out / f'{view}.png'
        if dst.exists():
            continue
        prompt = (f'The same character exactly as in the reference image, {desc}. Full body in the same T-pose, the same '
                  'outfit, hairstyle, colours, accessories, weapon and props, with every detail of the design preserved '
                  'and consistent with the reference. Orthographic character turnaround sheet view, flat even lighting, '
                  'plain solid grey background, no text, no shadow.')
        files = M.run(M.qwen_edit([ref], prompt, C_size, 1234 + list(VIEWS).index(view), f'AdamsHaven/CharViews/{key}_{view}'),
                      'char-views')
        Image.open(files[-1]).convert('RGB').save(dst)
        print(key, view, 'done', flush=True)


if __name__ == '__main__':
    args = sys.argv[1:]
    for k in (list(C.REFS) if args in (['all'], []) else args):
        make(k)
