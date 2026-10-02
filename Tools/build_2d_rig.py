"""2D anime battle rig (pilot: Kaela) from local ComfyUI.

  python Tools/build_2d_rig.py kaela stance     # Qwen Image Edit: three-quarter fighting stance from the T-pose sheet
  python Tools/build_2d_rig.py kaela cutout     # background removal (BiRefNet) -> stance_cut.png
  python Tools/build_2d_rig.py kaela parts      # split into layered parts using Tools/battle_rigs_2d/<id>.json joints

Work files: BattleRig2D/<id>/ (outside Assets). Unity outputs: Assets/Resources/AdamsHaven/BattleRigs2D/<id>/parts/*.png
and rig.json, which Assets/Editor/Battle2DRigBuilder.cs turns into model.prefab + AH_* clips.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import COMFY, run, qwen_edit, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
WORK = PROJECT / 'BattleRig2D'
OUT = PROJECT / 'Assets/Resources/AdamsHaven/BattleRigs2D'
SPECS = Path(__file__).resolve().parent / 'battle_rigs_2d'
CHARS = Path('S:/AI/Game/Game Assets/characters')

STANCE = {
    'kaela': dict(
        refs=[CHARS / 'Kaela/kaela_battle_tpose_front_v6.png', CHARS / 'Kaela/Celestium Weapons/kaela-ice-gauntlets.png'],
        prompt=('Image 1 shows the character, image 2 her weapons. Draw the same woman from image 1 with exactly the same face, '
                'eyepatch, sky-blue hair, snow-leopard ears and spotted tail, tattoos, white top, belt, black shorts, white '
                'side cloths and sandals, and the same proportions and muscular build. She wears the glowing ice-crystal claw '
                'gauntlets from image 2 on both hands instead of the white wraps. Full body, three-quarter view turned toward '
                'the right side of the picture, standing in a ready brawler stance: feet apart, knees slightly bent, both '
                'elbows bent with loose fists held in front of her at chest height, the arms clear of the torso, her long '
                'hair hanging behind her back, the tail curving out behind her to the left. The whole figure is visible from '
                'the ear tips to the soles with a small margin. Plain flat light grey background, even soft lighting, no '
                'shadow, no ground, no effects. Clean high-detail anime game character art, sharp lineart.'),
        size=(1024, 1536), seed=7101),
    # Rig-friendly body: limbs clear of the torso, no gauntlets (they are separate sprites on the hand bones).
    'kaela_apose': dict(
        refs=[CHARS / 'Kaela/kaela_battle_tpose_front_v6.png'],
        prompt=('Draw the same woman from the image with exactly the same face, eyepatch, sky-blue hair, snow-leopard ears '
                'and spotted tail, tattoos, white top, belt, black shorts, white side cloths, white hand wraps and sandals, '
                'and the same proportions and muscular build. Full body, three-quarter view, her body and face turned '
                'toward the right side of the picture. She stands with her feet apart and her knees slightly bent. Both '
                'arms hang down away from her sides in a relaxed A-pose, a hand-width of empty space between each arm and '
                'her body, elbows slightly bent, hands in loose fists. Nothing covers her torso or legs. Her long hair falls '
                'down her back behind her shoulders, and her tail curves out to the left behind her. The whole figure is '
                'visible from the ear tips to the soles with a small margin. Plain flat light grey background, even soft '
                'lighting, no shadow, no ground, no effects. Clean high-detail anime game character art, sharp lineart.'),
        size=(1024, 1536), seed=7201),
}


def stance(cid, key=None):
    key = key or (sys.argv[4] if len(sys.argv) > 4 else cid)
    spec = STANCE[key]
    d = WORK / cid
    d.mkdir(parents=True, exist_ok=True)
    names = []
    for i, ref in enumerate(spec['refs']):
        img = Image.open(ref)
        if img.mode == 'RGBA':
            flat = Image.new('RGB', img.size, (200, 200, 200))
            flat.paste(img, (0, 0), img)
            img = flat
        names.append(to_input(img.convert('RGB'), f'ahcg-rig2d-{cid}-ref{i}.png'))
    seed = spec['seed'] + int(sys.argv[3]) if len(sys.argv) > 3 else spec['seed']
    out = run(qwen_edit(names, spec['prompt'], spec['size'], seed, f'AdamsHaven/BattleRig2D/{cid}_stance'), 'ahcg-rig2d')
    img = Image.open(out[0]).convert('RGB')
    path = d / f'stance_{key}_{seed}.png'
    img.save(path)
    print('stance ->', path)


if __name__ == '__main__':
    cid, stage = sys.argv[1], sys.argv[2]
    {'stance': stance}[stage](cid)
