"""Gacha summon videos (CM 10.3.6): the Celestium Heart pulses blue, turns the pulled tier's colour, a spark of that
colour leaves the heart and grows into a flash that fills the screen; the game then shows the summoned unit.

  python Tools/produce_summon_videos.py all          # every tier
  python Tools/produce_summon_videos.py F SS SSR     # some tiers
  python Tools/produce_summon_videos.py review       # contact sheet of the finished videos

Three shots that share one opening and one finish, so they read as one family:
  standard  F, E, D, C, B, A   pulse -> colour shift + spark -> flash                         (about 5 s)
  grand     S (gold-red), SS (gold-celestium)
                               pulse -> the colour floods in, rings blaze, a pillar rises -> spark -> flash (7 s)
  prismatic SSR (celestium rainbow)
                               pulse -> the heart falters dark -> erupts in rainbow -> aurora + star spark -> flash (9 s)
Every tier starts on the same base key (the Tower's Heart, heart_F.png, recomposed 16:9) and ends on a flash frame drawn
here, not by a model, so all of them land on the same full-screen flash in their colour.

Keys and segments are cached by prompt and seed (BattleMotion/summon/), the way produce_battle_motion.py does it.
Output: Assets/Resources/AdamsHaven/Summon/summon_<tier>.mp4 (1280x720, 60 fps) and a preview gif in the work folder.
"""
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import h3, qwen_edit, run, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
HEART = PROJECT / 'Assets/Resources/AdamsHaven/Tower/heart_F.png'
WORK = PROJECT / 'BattleMotion/summon'
OUT = PROJECT / 'Assets/Resources/AdamsHaven/Summon'
SIZE = (1248, 704)
CLIENT = 'ahcg-summon'

STYLE = ('Polished hand-painted anime fantasy game art, crisp clean linework, rich cel shading, luminous light. '
         'No characters, no figures, no text, no UI.')
MOTION_TAIL = ('Anime gacha summon cinematic, the same Celestium Heart shrine throughout, smooth camera, no cuts, no '
               'characters, no people, no text, no UI.')

# The pulled tier's light. F..A escalate from silver to purple (CharacterPrompts' rank burst palette); S, SS and SSR
# are the user's: gold-red, gold-celestium and celestium rainbow. flash = the RGB the screen whites out to.
TIERS = {
    'F': dict(shot='standard', light='dull silver-white', flash=(236, 238, 244), seed=9301),
    'E': dict(shot='standard', light='pale mint-green white', flash=(214, 255, 236), seed=9311),
    'D': dict(shot='standard', light='clear teal', flash=(120, 236, 222), seed=9321),
    'C': dict(shot='standard', light='bright sapphire blue', flash=(110, 160, 255), seed=9331),
    'B': dict(shot='standard', light='blue-violet with silver threads', flash=(160, 146, 255), seed=9341),
    'A': dict(shot='standard', light='rich royal purple with silver filigree', flash=(186, 96, 240), seed=9351),
    'S': dict(shot='grand', light='blazing gold and crimson red', flash=(255, 168, 86), seed=9361),
    'SS': dict(shot='grand', light='radiant gold laced with celestium blue', flash=(255, 224, 150), seed=9371),
    'SSR': dict(shot='prismatic', light='prismatic celestium rainbow', flash=None, seed=9381),
}

BASE_PROMPT = ('Image 1 is the Celestium Heart shrine: a large faceted blue crystal heart floating in rings of light '
               'inside an ornate stone and timber shrine. Recompose exactly this shrine and crystal as a wide 16:9 '
               'cinematic shot at night, centred, seen slightly from below, the crystal glowing a calm celestium blue, '
               'faint blue mist on the shrine floor, a dark starry sky behind. Keep the crystal, rings and shrine '
               'design identical to image 1. ' + STYLE)


def tier_prompt(light, grand):
    extra = (' The light rings around the crystal blaze and spin fast, cracks of the same light run through the '
             'crystal, and a tall pillar of that light rises from it into the sky.') if grand else ''
    return ('Image 1 is the Celestium Heart shrine at night. Keep exactly the same shrine, crystal, camera and framing. '
            f'The crystal has changed colour: it now glows {light} instead of blue, and the light bathes the whole '
            f'shrine in {light}. At the heart of the crystal a small bright spark of {light} light is forming.{extra} '
            + STYLE)


DARK_PROMPT = ('Image 1 is the Celestium Heart shrine at night. Keep exactly the same shrine, crystal, camera and framing. '
               'The crystal has gone almost dark: its blue light has died to a faint ember, the rings of light have '
               'stopped, the shrine is in deep shadow, only a single thin crack of white light shows in the crystal. '
               + STYLE)

RAINBOW_PROMPT = ('Image 1 is the Celestium Heart shrine at night. Keep exactly the same shrine, crystal, camera and '
                  'framing. The crystal erupts in prismatic celestium rainbow light: every facet glows a different '
                  'colour from red through gold, green and blue to violet, rainbow rings spin around it, a rainbow '
                  'aurora fills the sky above the shrine, and a brilliant white star of light shines at the heart of '
                  'the crystal. ' + STYLE)


def flash_frame(rgb, size=SIZE):
    """The screen whiting out into the tier's light: near white at the centre, the tier's colour toward the edges."""
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2)) / math.sqrt(2)
    t = np.clip(r * 1.4, 0, 1)[..., None]
    if rgb is None:      # SSR: a rainbow ring around a white core
        ang = (np.arctan2(yy - h / 2, xx - w / 2) / (2 * math.pi) + .5)
        hue = np.stack([.5 + .5 * np.cos(2 * math.pi * (ang + k / 3)) for k in range(3)], -1) * 255
        edge = hue * .55 + 255 * .45
    else:
        edge = np.broadcast_to(np.array(rgb, np.float32), (h, w, 3))
    core = np.full((h, w, 3), 255, np.float32)
    img = core * (1 - t) + edge * t
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))


def key(name, refs, prompt, seed):
    """A Qwen edit, cached by prompt, refs and seed."""
    path = WORK / f'{name}.png'
    meta = path.with_suffix('.json')
    sig = dict(prompt=prompt, refs=[str(r) for r in refs], seed=seed)
    if not (path.exists() and meta.exists() and json.loads(meta.read_text()) == sig):
        names = []
        for i, r in enumerate(refs):
            img = Image.open(r).convert('RGBA')
            bg = Image.new('RGBA', img.size, (14, 16, 28, 255))
            bg.alpha_composite(img)
            names.append(to_input(bg.convert('RGB'), f'ahcg-summon-{name}-ref{i}.png'))
        print('qwen', name, flush=True)
        out = run(qwen_edit(names, prompt, SIZE, seed, f'AdamsHaven/Summon/{name}'), CLIENT)
        Image.open(out[0]).convert('RGB').resize(SIZE, Image.Resampling.LANCZOS).save(path)
        meta.write_text(json.dumps(sig, indent=2))
    return path


def segment(name, first, last, frames, seed, text):
    """One H3 first/last-frame segment, cached by prompt, keys and seed. Returns its 120 fps (RIFE) mp4."""
    d = WORK / 'segments' / name
    d.mkdir(parents=True, exist_ok=True)
    prompt = f'{text} {MOTION_TAIL}'
    sig = dict(prompt=prompt, frames=frames, seed=seed, first=first.stat().st_mtime, last=last.stat().st_mtime,
               first_name=first.name, last_name=last.name)
    meta = d / 'spec.json'
    if not ((d / 'h3_hi.mp4').exists() and meta.exists() and json.loads(meta.read_text()) == sig):
        a = to_input(Image.open(first).convert('RGB'), f'ahcg-summon-{name}-first.png')
        b = to_input(Image.open(last).convert('RGB'), f'ahcg-summon-{name}-last.png')
        print('h3', name, frames, 'frames', flush=True)
        outs = run(h3(prompt, SIZE, frames, seed, f'AdamsHaven/Summon/{name}', a, b), CLIENT)
        for s in outs:
            if s.suffix == '.mp4':
                shutil.copyfile(s, d / ('h3_hi.mp4' if '_hi' in s.name else 'h3_24.mp4'))
        meta.write_text(json.dumps(sig, indent=2))
    return d / 'h3_hi.mp4'


def produce(tier):
    t = TIERS[tier]
    WORK.mkdir(parents=True, exist_ok=True)
    base = key('base', [HEART], BASE_PROMPT, 9300)
    flash = WORK / f'flash_{tier}.png'
    flash_frame(t['flash']).save(flash)
    light, s = t['light'], t['seed']
    pulse = segment('pulse', base, base, 33, 9300,
                    'The Celestium Heart crystal pulses softly with celestium blue light twice, like a heartbeat, the '
                    'rings of light around it turning slowly. It ends exactly as it began.')
    parts = [pulse]
    if t['shot'] == 'standard':
        lit = key(f'lit_{tier}', [base], tier_prompt(light, False), s)
        parts.append(segment(f'shift_{tier}', base, lit, 49, s + 1,
                             f'The blue light of the crystal flickers and changes colour to {light}, the glow spreading '
                             f'over the shrine, and a small spark of {light} light forms at the heart of the crystal.'))
        # One colour named positively: a "no rainbow" negation draws one (the F pilot grew a rainbow halo).
        parts.append(segment(f'flash_{tier}', lit, flash, 33, s + 12,
                             f'The spark of {light} light rushes out of the crystal toward the camera, growing larger and '
                             f'brighter until it fills the whole screen in a blinding flash of pure {light}, one single '
                             f'colour of light.'))
    elif t['shot'] == 'grand':
        lit = key(f'lit_{tier}', [base], tier_prompt(light, False), s)
        blaze = key(f'blaze_{tier}', [lit], tier_prompt(light, True), s + 5)
        parts.append(segment(f'shift_{tier}', base, lit, 49, s + 1,
                             f'The blue heartbeat stutters, then {light} light floods through the crystal from within, '
                             f'washing over the whole shrine.'))
        parts.append(segment(f'blaze_{tier}', lit, blaze, 49, s + 3,
                             f'The light rings spin faster and blaze {light}, cracks of light race through the crystal, '
                             f'and a towering pillar of {light} light erupts from it into the sky.'))
        parts.append(segment(f'flash_{tier}', blaze, flash, 41, s + 2,
                             f'A brilliant spark of {light} light bursts out of the heart of the crystal and races toward '
                             f'the camera, swelling until it fills the whole screen in a blinding flash.'))
    else:
        dark = key('dark_SSR', [base], DARK_PROMPT, s)
        rainbow = key('rainbow_SSR', [base], RAINBOW_PROMPT, s + 5)
        parts.append(segment('falter_SSR', base, dark, 33, s + 1,
                             'The blue heartbeat falters and the crystal light dies away, the rings stop, and the shrine '
                             'sinks into darkness, a single thin crack of light left in the crystal.'))
        parts.append(segment('erupt_SSR', dark, rainbow, 57, s + 3,
                             'The crack splits open and prismatic rainbow light erupts from the crystal, every facet '
                             'blazing a different colour, rainbow rings whirling around it and a rainbow aurora sweeping '
                             'across the sky.'))
        parts.append(segment('flash_SSR', rainbow, flash, 41, s + 2,
                             'A brilliant white star at the heart of the crystal bursts out trailing rainbow light and '
                             'races toward the camera, swelling until it fills the whole screen in a blinding flash.'))
    OUT.mkdir(parents=True, exist_ok=True)
    dst = OUT / f'summon_{tier}.mp4'
    lst = WORK / f'concat_{tier}.txt'
    lst.write_text(''.join(f"file '{p.as_posix()}'\n" for p in parts))
    # 120 -> 60 fps, 1280x720. The segments share their boundary keys, so the joins are frame-continuous.
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', str(lst),
                    '-vf', 'select=not(mod(n\\,2)),setpts=N/60/TB,scale=1280:720:flags=lanczos', '-r', '60',
                    '-c:v', 'libx264', '-crf', '16', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an',
                    '-movflags', '+faststart', str(dst)], check=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(dst), '-vf', 'fps=12,scale=480:-1:flags=lanczos',
                    str(WORK / f'summon_{tier}_preview.gif')], check=True)
    print('SUMMON ->', dst, flush=True)


def review():
    """Every finished video as one row of 8 evenly spaced frames."""
    rows = []
    for tier in TIERS:
        src = OUT / f'summon_{tier}.mp4'
        if not src.exists():
            continue
        tile = WORK / f'sheet_{tier}.jpg'
        dur = float(subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0',
                                    str(src)], capture_output=True, text=True).stdout or 1)
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(src), '-vf',
                        f'fps={8 / dur:.4f},scale=240:-1,tile=8x1', '-frames:v', '1', str(tile)], check=True)
        rows.append(Image.open(tile))
    sheet = Image.new('RGB', (max(r.width for r in rows), sum(r.height for r in rows)))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height
    sheet.save(WORK / 'review.jpg', quality=90)
    print('review ->', WORK / 'review.jpg')


if __name__ == '__main__':
    args = sys.argv[1:] or ['all']
    if args == ['review']:
        review()
    else:
        for tier in (list(TIERS) if args == ['all'] else args):
            produce(tier)
