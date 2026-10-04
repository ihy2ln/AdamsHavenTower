"""Battle Mode motion + effects from local ComfyUI: Qwen Image Edit 2.1 keyframes -> MiniMax H3 video.

Direction comes from the user's Chaos Zero Nightmare reference videos (see CHAOS_ZERO_REFERENCE.md):
  - ultimates cut to a short cinematic: tight portrait beat -> weapon/fist swing -> wide impact, then back to field
  - hits use speed-line warps, white-core flashes, crescent slash trails and shard bursts

  python Tools/produce_battle_motion.py ult_kaela_avalanche  # keyframes (Qwen edit) -> H3 first/last -> 60 fps mp4
  python Tools/produce_battle_motion.py fx_slash         # H3 black->effect->black -> tintable 8-frame sheet
  python Tools/produce_battle_motion.py all             # or a prefix: ult_*  fx_*

Work files: BattleMotion/<job>/ (outside Assets). Unity outputs:
  Assets/Resources/AdamsHaven/Fx/Gen/<job>_sheet.png   white-on-alpha strip, 8 frames, tinted by BattleGui like fx_slash_sheet
  Assets/Resources/AdamsHaven/UltCutIns/<card id>.mp4  720p 60 fps H.264 cinematic, one per ultimate card
"""
from pathlib import Path
import json, shutil, subprocess, sys, time, urllib.request
import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parent.parent
WORK = PROJECT / 'BattleMotion'
FX_OUT = PROJECT / 'Assets/Resources/AdamsHaven/Fx/Gen'
ULT_OUT = PROJECT / 'Assets/Resources/AdamsHaven/UltCutIns'
ART = Path('S:/AI/Game/Game Assets/characters/portrait')
COMFY = Path('S:/AI/ComfyUI_windows_portable/ComfyUI-Easy-Install/ComfyUI')
URL = 'http://127.0.0.1:8188'
NEG = 'text, watermark, logo, UI, border, frame, extra limbs, extra fingers, deformed hands, blurry, lowres, jpeg artifacts'

FX_TAIL = (' Pure black background, nothing else in frame, no character, no ground, no text. The effect is bright '
           'white light on black, like an additive anime game VFX element. It begins on pure black '
           'and ends on pure black. Locked camera.')
CARDS = PROJECT / 'Assets/Resources/AdamsHaven/FullCards'
FIGHTERS = Path(__file__).resolve().parent / 'fighter_clips'
CHARS = Path('S:/AI/Game/Game Assets/characters')


def field_look(unit):
    """The look, battle sheet and weapon the field clips are drawn from (Tools/fighter_clips/<unit>.json), so the
    ultimate video's fighter wears the same outfit as the clip it hands over to."""
    spec = json.loads((FIGHTERS / f'{unit}.json').read_text(encoding='utf-8'))
    guard = WORK / f'fighter_{unit}' / 'keys' / 'guard_hi.png'
    return spec['look'], [guard, CHARS / spec['refs']['sheet'], CHARS / spec['refs']['weapon']]


ULTS = [
    # card id, unit, element look, close-up beat, wide finishing shot, seed
    ('ult_kaela_avalanche', 'kaela', 'ice and snow',
     'one fist raised beside her face with frost crackling off the knuckles, fierce fanged grin',
     'she slams both fists into the frozen ground and a colossal eruption of ice crystals and snow bursts outward '
     'across the whole frame, avalanche and mountains behind', 9107),
    # Awakenings (aw_<unit>) play through the same cut-in path: UltCutIns/<card id>.mp4.
    ('aw_kaela', 'kaela', 'ice and aurora light',
     'her golden eye snaps open as frost crawls over her shoulders and her blue hair lifts in a freezing wind, a calm '
     'fierce smile',
     'she stands with both ice-claw gauntlets raised as a towering curtain of aurora and ice light sweeps over her '
     'allies behind her, snow swirling and crystal motes rising into the sky', 9109),
    ('aw_ghislaine', 'ghislaine', 'fire',
     'her tiger eyes flare gold as embers swirl around her face and her silver hair lifts in a hot wind',
     'she stands tall with her flaming greatsword raised as a great spectral white tiger of fire rises behind her and '
     'roars over her allies', 9113),
    ('aw_elara', 'elara', 'lightning',
     'she closes the spellbook with a snap, blue runes reflecting in her eyes, the glowing fountain pen between her '
     'fingers, a small confident smile',
     'she floats above a field of glowing script as every page of her book flies out and turns into shining blue '
     'runes circling her allies', 9123),
    ('aw_helda', 'helda', 'warm hearth light',
     'she wipes her brow with a hearty grin, warm golden light glowing on her face',
     'she raises her war hammer as a warm golden hearth glow spreads over her allies, healing motes drifting upward',
     9133),
    ('aw_daisy', 'daisy', 'fire',
     'her golden eyes blaze and the hibiscus in her hair glows as she laughs, embers swirling',
     'she throws her arms wide as a towering wildfire of pink and orange flame bursts up around her, petals and '
     'embers flying', 9143),
    ('aw_clarity', 'clarity', 'holy light',
     'she exhales slowly, eyes closed, soft golden light gathering around her face',
     'she stands in a calm centered stance as a pillar of warm golden light descends over her, healing rings '
     'spreading outward', 9153),
    ('ult_kaela_bastion', 'kaela', 'ice',
     'she crosses her forearms in front of her face, frost spreading over her gloves, a determined grin',
     'she stands planted with arms spread as a huge translucent dome of ice crystal walls rises around her allies, '
     'glittering frost aurora overhead', 9108),
    ('ult_ghislaine', 'ghislaine', 'fire',
     'her tiger eyes narrow as she raises her greatsword beside her face, the blade igniting with orange flame',
     'she leaps and rends downward with a huge flaming greatsword slash, a crescent of fire and tiger-claw streaks '
     'tearing across the frame', 9111),
    ('ult_ghislaine_oath', 'ghislaine', 'fire',
     'she lifts the hilt of her greatsword to her lips in a solemn oath, embers drifting past her face',
     'she drives her flaming greatsword into the ground and a great wall of golden fire rises behind her as a '
     'protective ward, her silver hair and tail billowing', 9112),
    ('ult_elara', 'elara', 'lightning',
     'she pushes up her thin gold glasses with a confident smile, her open spellbook crackling with blue runes',
     'she floats above a storm with her spellbook open as dozens of blue-white lightning bolts rain down across the '
     'whole battlefield, glowing rune circles in the sky', 9121),
    ('ult_elara_convergence', 'elara', 'lightning',
     'she points her glowing fountain pen forward, a spark gathering at its nib, her eyes glowing',
     'many rune circles line up in front of her outstretched pen and a single colossal blue-white lightning beam '
     'fires through them across the frame', 9122),
    ('ult_helda', 'helda', 'warm hearth light and gentle frost',
     'she smiles warmly and raises her glowing brass tankard in a toast, soft golden light on her face',
     'she stands with her hammer lifted as a warm golden hearth glow and swirling healing motes spread over the '
     'whole field, snowflakes melting into sparkles. She is the only person in the picture', 9161),
    ('ult_helda_sanctuary', 'helda', 'ice',
     'she hefts her crystal war hammer over her shoulder with a steady grin, frost gathering on its head',
     'she slams her hammer down and a gleaming sanctuary of ice pillars and a crystal dome rises around her, cold '
     'blue light. She stands alone, the only figure in the picture', 9182),
    ('ult_daisy', 'daisy', 'fire',
     'she laughs wildly and twirls her fire spear beside her face, embers and petals swirling around her',
     'she spins her fire spear overhead as a volcanic firestorm sweeps across a tropical island battlefield, walls of '
     'flame and flying embers', 9141),
    ('ult_daisy_inferno', 'daisy', 'fire',
     'her golden eyes blaze as she grips her fire spear with both hands, its crystal tip glowing hot',
     'she points her fire spear down and a towering pillar of flame erupts from the ground in front of her, a roaring '
     'spectral tiger of fire leaping out of it', 9142),
    ('ult_clarity', 'clarity', 'holy light',
     'she draws back a glowing fist beside her face, eyes locked forward, golden light streaming',
     'she drives a colossal straight punch forward and a blinding beam of golden light and shockwave rings '
     'explodes across the frame, rubble flying', 9151),
    ('ult_clarity_feast', 'clarity', 'holy light',
     'she presses her palms together in a calm bow, golden light blooming between her hands',
     'she stands in a centered stance as radiant golden rings and floating light motes pour over her allies, '
     'warm rays from above', 9152),
]
# JD's decrees (BattleCatalog.SummonerUltimates). JD keeps his 3D field rig, so his look comes from the portrait art.
JD_LOOK = ('the summoner JD: a tall dark-skinned man with short black hair, an open long silver brocade coat over a bare '
           'chest, a silver chain necklace, black trousers and black shoes')
JD_REFS = [ART / 'jd_side.webp', ART / 'jd.png']
DECREES = [
    ('ult_sum_a', 'arcane blue and warm gold',
     'he fans a hand of glowing silver playing cards beside his face with a confident smile, blue arcane light on his '
     'face',
     'he throws the cards high and dozens of glowing playing cards spiral over the battlefield behind him, raining warm '
     'golden light and rally sigils down on his allies. He is the only person in the picture', 9171),
    ('ult_sum_b', 'dark violet arcane',
     'his eyes narrow as he raises a single dark playing card between two fingers, violet energy crackling around it',
     'he flicks the card forward and a giant spectral playing card slams down across the enemy line, a violet shockwave '
     'cracking the ground and shattering their armor. He is the only person in the picture', 9172),
]


def decree_job(card, element, close, wide, seed):
    refs = [str(r) for r in JD_REFS]
    return {
        'kind': 'ult', 'unit': 'jd', 'out': card, 'size': (1248, 704), 'length': 73, 'seed': seed,
        'keys': [
            ('first', refs,
             f'Image 1 and image 2 show the character. Recompose him as a tight cinematic anime close-up for an ultimate '
             f'skill cut-in, 16:9. He is {JD_LOOK}; keep his exact face, hair and outfit. {close}. Dark background with '
             f'{element} energy and strong rim light. Sharp cel-shaded anime game illustration.'),
            ('last', refs,
             f'Image 1 and image 2 show the character. A wide 16:9 cinematic anime shot of him ({JD_LOOK}); keep his '
             f'exact face, hair and outfit. {wide}. Dynamic camera angle, speed lines, dramatic {element} lighting, '
             f'sharp anime game illustration.'),
        ],
        'prompt': (f'Anime game ultimate skill cinematic. Starting on a tight close-up of {JD_LOOK}: {close}. A white '
                   f'flash and fast speed lines, then the camera whips back to a wide shot: {wide}. Punchy, fast, '
                   f'dramatic camera, consistent character, no text, no UI.'),
    }


def ult_job(card, unit, element, close, wide, seed):
    look, (guard, sheet, weapon) = field_look(unit)
    return {
        'kind': 'ult', 'unit': unit, 'out': card, 'size': (1248, 704), 'length': 73, 'seed': seed,
        'keys': [
            ('first', [str(guard), str(sheet)],
             f'Image 1 is the character as she looks in battle, image 2 her character sheet. Recompose her as a tight '
             f'cinematic anime close-up for an ultimate skill cut-in, 16:9. She is {look}; keep her exact face, eyes, '
             f'hair, ears and outfit from image 1. {close}. Dark background with {element} energy and strong rim '
             f'light. Sharp cel-shaded anime game illustration.'),
            ('last', [str(guard), str(sheet), str(weapon)],
             f'Image 1 is the character as she looks in battle, image 2 her character sheet, image 3 her weapon. A wide '
             f'16:9 cinematic anime shot of her ({look}); keep her exact face, hair, outfit and weapon from images 1 '
             f'and 3. {wide}. Dynamic camera angle, speed lines, dramatic {element} lighting, sharp anime game '
             f'illustration.'),
        ],
        'prompt': (f'Anime game ultimate skill cinematic. Starting on a tight close-up of {look}: {close}. A white '
                   f'flash and fast speed lines, then the camera whips back to a wide shot: {wide}. Punchy, fast, '
                   f'dramatic camera, consistent character, no text, no UI.'),
    }


# Joint ultimates (BattleCombos.cs, CM 10.3.4): both fighters in one shot. Two people in one H3 shot is where clones
# appear, so every key and the motion name them and their sides, and say there are exactly two.
JOINTS = [
    # card id, lead, partner, element look, close-up beat, wide finishing shot, seed
    ('ult_joint_ghislaine_kaela', 'ghislaine', 'kaela', 'fire and ice',
     'they stand back to back, Ghislaine’s flaming greatsword and Kaela’s ice-claw gauntlets crossed between them, both '
     'grinning fiercely, embers and frost swirling together',
     'they charge forward together as a spectral white tiger of fire and a towering wave of ice crash down in front of '
     'them, steam exploding across the frame', 9201),
    ('ult_joint_elara_kaela', 'elara', 'kaela', 'lightning and ice',
     'Elara’s glowing pen raised beside Kaela’s frosted fist, lightning and frost crackling in the air between them',
     'Kaela slams the ground and a field of ice spikes bursts up in front of them as Elara calls down a storm of '
     'lightning that shatters across the ice', 9202),
    ('ult_joint_daisy_ghislaine', 'daisy', 'ghislaine', 'fire',
     'Daisy’s fire spear and Ghislaine’s flaming greatsword crossed in an X between them, embers swirling, both '
     'laughing',
     'they swing together and two colossal waves of fire, one shaped like a roaring spectral tiger, sweep across the '
     'whole battlefield in front of them', 9203),
    ('ult_joint_elara_helda', 'elara', 'helda', 'aurora, frost and lightning',
     'Helda’s crystal war hammer raised beside Elara’s open spellbook, frost and lightning swirling around them',
     'Helda slams her hammer down as Elara writes a glowing sigil in the air, and an aurora storm of ice shards and '
     'lightning rains across the sky in front of them', 9204),
    ('ult_joint_clarity_elara', 'elara', 'clarity', 'golden light and lightning',
     'Clarity’s glowing fist beside Elara’s glowing pen, golden light and lightning meeting between them',
     'Elara draws a huge glowing prism sigil in the air and Clarity punches straight through it, a blinding beam of '
     'golden lightning blasting forward across the frame', 9205),
    ('ult_joint_clarity_helda', 'helda', 'clarity', 'warm golden hearth light',
     'Helda and Clarity side by side, Helda’s hammer and Clarity’s open palm raised together, warm golden light '
     'blooming between them',
     'a great dome of warm golden hearth light and gentle snow rises over the battlefield around them, healing motes '
     'pouring down', 9206),
    ('ult_joint_clarity_daisy', 'daisy', 'clarity', 'sunfire and holy light',
     'Daisy’s fire spear and Clarity’s glowing fist side by side, sunfire and golden light swirling around them',
     'Daisy hurls her fire spear and Clarity strikes it with a punch, and it streaks forward as a blazing sun across '
     'the frame', 9207),
]


def joint_job(card, lead, partner, element, close, wide, seed):
    look_a, (guard_a, _, weapon_a) = field_look(lead)
    look_b, (guard_b, _, _) = field_look(partner)
    a, b = lead.capitalize(), partner.capitalize()
    who = (f'Exactly two people are in the picture: {a} on the left and {b} on the right. {a} is {look_a}. {b} is '
           f'{look_b}')
    return {
        'kind': 'ult', 'unit': lead, 'out': card, 'size': (1248, 704), 'length': 73, 'seed': seed,
        'keys': [
            ('first', [str(guard_a), str(guard_b)],
             f'Image 1 is {a} as she looks in battle, image 2 is {b}. Recompose them together as a tight cinematic anime '
             f'two-shot close-up for a joint ultimate cut-in, 16:9. {who}; keep each one’s exact face, hair and '
             f'outfit from her image. {close}. Dark background with {element} energy and strong rim light. Sharp '
             f'cel-shaded anime game illustration.'),
            ('last', [str(guard_a), str(guard_b), str(weapon_a)],
             f'Image 1 is {a} as she looks in battle, image 2 is {b}, image 3 is {a}’s weapon. A wide 16:9 cinematic '
             f'anime shot of the two of them. {who}; keep each one’s exact face, hair, outfit and weapon. {wide}. '
             f'Dynamic camera angle, speed lines, dramatic {element} lighting, sharp anime game illustration.'),
        ],
        'prompt': (f'Anime game joint ultimate cinematic. {who}. Starting on a tight two-shot: {close}. A white flash '
                   f'and fast speed lines, then the camera whips back to a wide shot: {wide}. Punchy, fast, dramatic '
                   f'camera, the same two characters throughout, no one else, no text, no UI.'),
    }


JOBS = {card: ult_job(card, unit, el, close, wide, seed) for card, unit, el, close, wide, seed in ULTS}
JOBS.update({card: decree_job(card, el, close, wide, seed) for card, el, close, wide, seed in DECREES})
JOBS.update({j[0]: joint_job(*j) for j in JOINTS})
JOBS.update({
    'fx_slash': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 311,
        'prompt': 'A single sharp crescent sword-slash arc sweeps fast across the frame from upper left to lower '
                  'right, a thin blazing white edge with a trailing smear of speed lines, flares once and '
                  'dissolves into fading sparks.' + FX_TAIL,
    },
    'fx_impact': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 312,
        'prompt': 'A punchy hit impact in the center: a sharp four-point star flash with a blinding white core, '
                  'a thin expanding shockwave ring, and radial spikes of light and small shards flying outward, '
                  'then quickly fading.' + FX_TAIL,
    },
    'fx_warp': {
        'kind': 'fx', 'size': (1248, 704), 'length': 56, 'seed': 313, 'fade_x': False,
        'prompt': 'Full-frame horizontal speed-line warp: dense streaks of white and electric blue light rush '
                  'from right to left at high speed like a hyperspace dash, a bright horizontal band across the '
                  'middle, building up then thinning out.' + FX_TAIL,
    },
    'fx_frost': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 314, 'floor': 0.22,
        'prompt': 'A burst of jagged ice crystal spikes erupts upward from the bottom center, glittering frost '
                  'shards and cold mist spray outward, then the crystals shatter into sparkling dust and fade.'
                  + FX_TAIL,
    },
    'fx_fire': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 315, 'floor': 0.12,
        'prompt': 'A fiery anime explosion in the center: a bright flash, then licking tongues of flame and curling '
                  'fire swirl outward and upward with flying embers, then burn out into drifting sparks.' + FX_TAIL,
    },
    'fx_lightning': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 316,
        'prompt': 'A jagged lightning bolt strikes straight down into the center, forking branches and a crackling '
                  'electric burst with arcs jumping outward, flickering twice then fading.' + FX_TAIL,
    },
    'fx_light': {
        'kind': 'fx', 'size': (704, 704), 'length': 56, 'seed': 317,
        'prompt': 'A holy radiant burst in the center: a blinding eight-point star, concentric rings of light '
                  'expanding outward and a vertical pillar of rays, glittering motes rising, then fading.' + FX_TAIL,
    },
})


def api(path, data=None):
    req = urllib.request.Request(URL + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(req, timeout=60))


def node(t, **kw):
    return {'class_type': t, 'inputs': kw}


def run(graph, client):
    pid = api('/prompt', {'prompt': graph, 'client_id': client})['prompt_id']
    print('  queued', pid, flush=True)
    while True:
        h = api('/history/' + pid)
        if h:
            h = h[pid]
            break
        time.sleep(5)
    if h['status']['status_str'] != 'success':
        raise RuntimeError(str(h['status'])[-3000:])
    files = []
    for out in h['outputs'].values():
        for key in ('images', 'gifs'):
            for f in out.get(key, []):
                files.append(COMFY / f.get('type', 'output') / f.get('subfolder', '') / f['filename'])
    return files


def to_input(img, name):
    img.save(COMFY / 'input' / name)
    return name


# ---------------------------------------------------------------- Qwen Image Edit 2.1 -------------
def qwen_edit(refs, prompt, size, seed, prefix):
    g = {
        'unet': node('UNETLoader', unet_name='qwenimage21\\qwen_image_2.1_int8_convrot.safetensors', weight_dtype='default'),
        'cache': node('QwenImage21Cache', model=['unet', 0], device='auto', dtype='default'),
        'clip': node('CLIPLoader', clip_name='qwen3vl_8b_bf16.safetensors', type='qwen_image', device='default'),
        'vae': node('VAELoader', vae_name='qwen_image_2.1_vae_bf16.safetensors'),
        'enc': node('TextEncodeQwenImage21', clip=['clip', 0], vae=['vae', 0], prompt=prompt, negative_prompt=NEG,
                    resolution=1024),
        'lat': node('EmptyLatentImage', width=size[0], height=size[1], batch_size=1),
        'ks': node('KSampler', model=['cache', 0], positive=['enc', 0], negative=['enc', 1], latent_image=['lat', 0],
                   seed=seed, steps=30, cfg=3.0, sampler_name='euler', scheduler='simple', denoise=1.0),
        'dec': node('VAEDecode', samples=['ks', 0], vae=['vae', 0]),
        'save': node('SaveImage', images=['dec', 0], filename_prefix=prefix),
    }
    for i, r in enumerate(refs, 1):
        g[f'img{i}'] = node('LoadImage', image=r)
        g['enc']['inputs'][f'images.image_{i}'] = [f'img{i}', 0]
    return g


# ---------------------------------------------------------------- MiniMax H3 ----------------------
def h3(prompt, size, length, seed, prefix, first=None, last=None, rife=5):
    i2v = dict(clip=['160', 0], vae=['119', 0], prompt=prompt, width=size[0], height=size[1], length=length)
    g = {
        '289': node('UNETLoader', unet_name='MiniMaxH3\\minimax_h3_fl2va_pruned_int8_convrot.safetensors', weight_dtype='default'),
        '160': node('CLIPLoader', clip_name='qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors', type='minimax', device='default'),
        '119': node('VAELoader', vae_name='MiniMaxH3\\minimax_h3_video_vae_fp16.safetensors'),
        '323': node('MiniMaxH3TurboLoRA', model=['289', 0], lora_name='H3\\minimax_h3_turbo_4step_ckpt500.safetensors', strength=1.0, low_vram=False),
        '324': node('MiniMaxH3SigmaShift', model=['323', 0], shift_video=11.0, shift_audio=3.0),
        '280': node('MiniMaxH3ImageToVideo', **i2v),
        '299': node('BasicGuider', model=['324', 0], conditioning=['280', 0]),
        '298': node('BasicScheduler', model=['324', 0], scheduler='simple', steps=4, denoise=1.0),
        '325': node('MiniMaxH3TurboSampler'),
        '179': node('RandomNoise', noise_seed=seed),
        '125': node('SamplerCustomAdvanced', noise=['179', 0], guider=['299', 0], sampler=['325', 0], sigmas=['298', 0], latent_image=['280', 1]),
        '122': node('VAEDecode', samples=['125', 0], vae=['119', 0]),
        '218': node('VHS_VideoCombine', images=['122', 0], frame_rate=24.0, loop_count=0, filename_prefix=prefix,
                    format='video/h264-mp4', pix_fmt='yuv420p', crf=15, save_metadata=False, pingpong=False, save_output=True),
    }
    if first:
        g['f1'] = node('LoadImage', image=first)
        g['280']['inputs']['first_frame'] = ['f1', 0]
    if last:
        g['f2'] = node('LoadImage', image=last)
        g['280']['inputs']['last_frame'] = ['f2', 0]
    if rife > 1:
        g['400'] = node('RIFE VFI', ckpt_name='rife49.pth', frames=['122', 0], clear_cache_after_n_frames=16, multiplier=rife,
                        fast_mode=True, ensemble=False, scale_factor=1.0, dtype='float16', torch_compile=False, batch_size=1)
        g['401'] = node('VHS_VideoCombine', images=['400', 0], frame_rate=24.0 * rife, loop_count=0, filename_prefix=prefix + '_hi',
                        format='video/h264-mp4', pix_fmt='yuv420p', crf=15, save_metadata=False, pingpong=False, save_output=True)
    return g


def frames_of(mp4, dst, every=1):
    shutil.rmtree(dst, ignore_errors=True)
    dst.mkdir(parents=True)
    vf = ['-vf', f'select=not(mod(n\\,{every}))', '-fps_mode', 'vfr'] if every > 1 else []
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(mp4), *vf, str(dst / 'f_%04d.png')], check=True)
    return sorted(dst.glob('f_*.png'))


def fit(img, size):
    """Center-crop to the target aspect, then resize."""
    w, h = img.size
    tw, th = size
    s = max(tw / w, th / h)
    img = img.resize((round(w * s), round(h * s)), Image.Resampling.LANCZOS)
    l, t = (img.width - tw) // 2, (img.height - th) // 2
    return img.crop((l, t, l + tw, t + th))


def flat_ref(path, bg=(200, 200, 200)):
    """A reference for Qwen: RGBA cutouts (the field guard) go on flat light grey, everything else as RGB."""
    img = Image.open(path)
    if img.mode in ('RGBA', 'LA', 'P'):
        out = Image.new('RGBA', img.size, bg + (255,))
        out.alpha_composite(img.convert('RGBA'))
        img = out
    return img.convert('RGB')


def ult(job, spec, d):
    size = spec['size']
    keys, sigs = {}, []
    for tag, refs, prompt in spec['keys']:
        path = d / f'key_{tag}.png'
        meta = path.with_suffix('.json')
        sig = dict(prompt=prompt, refs=refs, seed=spec['seed'] + len(keys))
        if not (path.exists() and meta.exists() and json.loads(meta.read_text()) == sig):
            names = [to_input(flat_ref(r if Path(r).is_absolute() else ART / r), f'ahcg-{job}-{tag}-ref{i}.png') for i, r in enumerate(refs)]
            print('qwen edit', tag, flush=True)
            out = run(qwen_edit(names, prompt, size, spec['seed'] + len(keys), f'AdamsHaven/BattleMotion/{job}_{tag}'), 'ahcg-motion')
            fit(Image.open(out[0]).convert('RGB'), size).save(path)
            meta.write_text(json.dumps(sig, indent=2))
        sigs.append(sig)
        keys[tag] = to_input(Image.open(path).convert('RGB'), f'ahcg-{job}-key-{tag}.png')
    motion = dict(prompt=spec['prompt'], keys=sigs, length=spec['length'], seed=spec['seed'])
    done = d / 'h3.json'
    if not ((d / 'h3_hi.mp4').exists() and done.exists() and json.loads(done.read_text()) == motion):
        # delete h3_hi.mp4 (or change the prompt / keys) to re-render the motion
        print('h3', job, flush=True)
        g = h3(spec['prompt'], size, spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}', keys.get('first'), keys.get('last'))
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        outs = run(g, 'ahcg-motion')
        for s in outs:
            if s.suffix == '.mp4':
                shutil.copyfile(s, d / ('h3_hi.mp4' if '_hi' in s.name else 'h3_24.mp4'))
        done.write_text(json.dumps(motion, indent=2))
    # 120 -> 60 fps, scale to 720p
    ULT_OUT.mkdir(parents=True, exist_ok=True)
    dst = ULT_OUT / f"{spec['out']}.mp4"
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(d / 'h3_hi.mp4'),
                    '-vf', 'select=not(mod(n\\,2)),setpts=N/60/TB,scale=1280:720:flags=lanczos', '-r', '60',
                    '-c:v', 'libx264', '-crf', '16', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart',
                    str(dst)], check=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(dst), '-vf', 'fps=15,scale=640:-1:flags=lanczos',
                    str(d / f'{job}_preview.gif')], check=True)
    print('ULT ->', dst)


def fx(job, spec, d):
    size = spec['size']
    if not (d / 'h3_24.mp4').exists():      # delete h3_24.mp4 to re-render; otherwise only the sheet is rebuilt
        black = to_input(Image.new('RGB', size, (0, 0, 0)), f'ahcg-black-{size[0]}x{size[1]}.png')
        print('h3', job, flush=True)
        g = h3(spec['prompt'], size, spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}', black, black, rife=1)
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        outs = run(g, 'ahcg-motion')
        src = next(s for s in outs if s.suffix == '.mp4')
        shutil.copyfile(src, d / 'h3_24.mp4')
    floor = spec.get('floor', 0.06)
    files = frames_of(d / 'h3_24.mp4', d / '_frames')
    lum = np.array([np.asarray(Image.open(f).convert('L'), np.float32).mean() for f in files])
    active = np.where(lum > max(2.0, lum.max() * 0.12))[0]
    a, b = (active[0], active[-1]) if len(active) else (0, len(files) - 1)
    pick = np.linspace(a, b, 8).round().astype(int)
    fw = 320 if size[0] == size[1] else 512
    fh = round(fw * size[1] / size[0])
    white = Image.new('RGBA', (fw * 8, fh))
    color = Image.new('RGBA', (fw * 8, fh))
    for i, k in enumerate(pick):
        rgb = np.asarray(Image.open(files[k]).convert('RGB').resize((fw, fh), Image.Resampling.LANCZOS), np.float32) / 255
        # black-level crush so the H3 noise floor becomes fully transparent
        alpha = np.clip((rgb.max(axis=2) - floor) / (1 - floor), 0, 1) ** 0.9
        yy, xx = np.mgrid[0:fh, 0:fw]
        sides = [xx, fw - 1 - xx, yy, fh - 1 - yy] if spec.get('fade_x', True) else [yy, fh - 1 - yy]
        border = np.clip(np.minimum.reduce(sides) / (fw * 0.06), 0, 1)
        if fw == fh:                           # square bursts: round falloff so a full frame never reads as a box
            r = np.hypot(xx / (fw - 1) * 2 - 1, yy / (fh - 1) * 2 - 1)
            border = border * np.clip((1.0 - r) / 0.3, 0, 1) ** 1.5
        alpha = alpha * border
        w = np.dstack([np.ones_like(alpha)] * 3 + [alpha])
        white.paste(Image.fromarray((w * 255 + .5).astype(np.uint8), 'RGBA'), (i * fw, 0))
        c = np.dstack([np.clip(rgb / np.maximum(rgb.max(axis=2, keepdims=True), 1e-3), 0, 1), alpha])
        color.paste(Image.fromarray((c * 255 + .5).astype(np.uint8), 'RGBA'), (i * fw, 0))
    FX_OUT.mkdir(parents=True, exist_ok=True)
    white.save(FX_OUT / f'{job}_sheet.png')
    color.save(d / f'{job}_sheet_color.png')
    prev = Image.new('RGB', white.size, (24, 26, 34))
    prev.paste(color, (0, 0), color)
    prev.save(d / f'{job}_preview.png')
    shutil.rmtree(d / '_frames')
    print('FX ->', FX_OUT / f'{job}_sheet.png', 'frames', list(pick), 'of', len(files))


def main(which):
    if which == 'all':
        todo = list(JOBS)
    elif which.endswith('*'):
        todo = [j for j in JOBS if j.startswith(which[:-1])]
    else:
        todo = [which]
    for job in todo:
        spec = JOBS[job]
        d = WORK / job
        d.mkdir(parents=True, exist_ok=True)
        (d / 'spec.json').write_text(json.dumps(spec, indent=2))
        (ult if spec['kind'] == 'ult' else fx)(job, spec, d)


if __name__ == '__main__':
    main(sys.argv[1])
