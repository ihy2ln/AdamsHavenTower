"""Match-cut cinematics for skill cards from local ComfyUI MiniMax H3.

  python Tools/produce_cine.py cine_kaela_shatter_hook
  python Tools/produce_cine.py cine_kaela_*            # every job with that prefix

The first and last frame are the fighter's guard at the cine framing laid over Resources/AdamsHaven/Fx/cine_bg.png, the
same backdrop the battle fades to while it zooms in on the fighter. The clip therefore starts and ends exactly where the
battle's zoom lands, so the cut is seamless. The guard comes from the clip fighter
(Tools/produce_fighter_clips.py <unit> cine -> BattleMotion/fighter_<unit>/cine_frame.png) or, for the old 2D rig pilot,
from Battle2DRigBuilder.RenderCineFrame -> BattleMotion/<job>/rig_frame.png.
Output: Resources/AdamsHaven/UltCutIns/<card id>.mp4, 1280x720, 60 fps (H3 24 fps -> RIFE 5x -> 60).
key_first.png / key_last.png and spec.json stay in BattleMotion/<job>/ for a later pass through another model
(for example Seedance) with the same first and last frames.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import WORK, ULT_OUT, fit, h3, run, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
BACKDROP = PROJECT / 'Assets/Resources/AdamsHaven/Fx/cine_bg.png'

KAELA = ('The muscular blue-haired snow-leopard beastfolk woman with an eyepatch, spotted tail and glowing ice-crystal '
         'claw gauntlets')
CINE_TAIL = (' Anime fighting game special move, locked camera, dark navy stage. She starts and ends in exactly her '
             'starting ready stance, in the same place. No text, no UI.')


def skill(unit, card, seed, action, length=41):
    # H3 wants 1248x704 (multiples of 32) and 8n+1 frames; the keyframe is fitted to it and the clip scaled back to 720p.
    return dict(unit=unit, card=card, size=(1248, 704), length=length, seed=seed, prompt=action + CINE_TAIL)


JOBS = {
    'cine_kaela_frost_jab': skill('kaela', 'mv_frost_jab', 7311,
        f'{KAELA} snaps a lightning-fast jab to the right. On impact a ring of frost and ice shards bursts off her '
        'knuckles with a white flash, then the shards fade and she pulls back.'),
    'cine_kaela_rime_sweep': skill('kaela', 'mv_rime_sweep', 7312,
        f'{KAELA} drops low and sweeps one ice-claw gauntlet across the frame in a wide arc. A crescent of rime frost '
        'and ice crystals sprays across the right half of the frame and glitters away as she rises.'),
    'cine_kaela_glacier_guard': skill('kaela', 'mv_glacier_guard', 7313,
        f'{KAELA} crosses her forearms and plants her feet. Thick plates of glacier ice grow over her gauntlets and '
        'forearms with a cold blue glow, then she lowers her arms, the ice still gleaming.'),
    'cine_kaela_shatter_hook': skill('kaela', 'mv_shatter_hook', 7301,
        f'{KAELA} coils back, then steps forward and drives a powerful rising hook punch to the right. On impact a huge '
        'cluster of blue ice crystals erupts in the right half of the frame and shatters into bright blue-white shards '
        'and frost mist with a white flash. The shards fade away.'),
    'cine_kaela_whiteout_counter': skill('kaela', 'mv_whiteout_counter', 7314,
        f'{KAELA} narrows her eye and sinks into a coiled counter stance as a swirling white blizzard gathers around '
        'her, frost crackling over her gauntlets, then the snow settles around her.'),
    'cine_kaela_permafrost_brace': skill('kaela', 'mv_permafrost_brace', 7315,
        f'{KAELA} braces and breathes out a cloud of frost. A thin shell of glittering permafrost crystallises over her '
        'body with soft healing light sparkling inside it, then it fades to a faint shimmer.'),
}

GH = ('The silver-haired white-tiger beastfolk swordswoman in silver plate armor, with a ringed tail and a glowing flaming '
      'crystal greatsword,')
DA = 'The curly red-haired elf wild-woman in a red floral bikini and sarong, with a glowing crystal fire spear,'
EL = ('The elf mage-scholar with a brown bun, a navy coat and a white pleated skirt, holding a spellbook and a glowing '
      'crystal fountain pen,')
HE = ('The stocky white-braided dwarf brewmaster with brass goggles, a blue top, denim shorts and a glowing crystal war '
      'hammer,')
CY = ('The short-haired martial artist in a red and white wrap top and red shorts, carrying glowing crystal kunai,')
for job, unit, card, seed, who, action in [
    ('cine_ghislaine_tiger_cleave', 'ghislaine', 'gh_tiger_cleave', 7401, GH,
     'raises the greatsword overhead and brings it down in a blazing cleave to the right; a tiger-shaped slash of fire tears across the right half of the frame and fades.'),
    ('cine_ghislaine_guard_stance', 'ghislaine', 'gh_guard_stance', 7402, GH,
     'plants her feet and raises the greatsword before her as a wall of golden fire rises in front of her, then settles into a glowing ember aura.'),
    ('cine_ghislaine_fireline_cut', 'ghislaine', 'gh_fireline_cut', 7403, GH,
     'swings the greatsword in a wide horizontal arc to the right, leaving a long burning line of fire across the whole frame.'),
    ('cine_ghislaine_ember_chain', 'ghislaine', 'gh_ember_chain', 7404, GH,
     'slashes twice to the right in quick succession, each cut leaving a chain of bright embers that bursts in a row of small explosions.'),
    ('cine_ghislaine_reckless_swing', 'ghislaine', 'gh_reckless_swing', 7405, GH,
     'roars and heaves the greatsword in a huge reckless downward smash to the right; a massive explosion of fire and shattered earth fills the right of the frame.'),
    ('cine_ghislaine_tiger_riposte', 'ghislaine', 'gh_tiger_riposte', 7406, GH,
     'parries with the flat of the greatsword, then counters with a lightning-fast thrust to the right as a roaring fire tiger head flashes along the blade.'),
    ('cine_daisy_bonfire_brand', 'daisy', 'da_bonfire_brand', 7411, DA,
     'twirls the fire spear and brands a blazing sigil into the air to the right with its tip; the sigil flares into a bonfire burst.'),
    ('cine_daisy_wand_sweep', 'daisy', 'da_wand_sweep', 7412, DA,
     'spins and sweeps the fire spear in a wide circle, throwing a ring of flames and flower petals across the frame.'),
    ('cine_daisy_ember_thrust', 'daisy', 'da_ember_thrust', 7413, DA,
     'lunges and drives the fire spear forward to the right; a spiral of embers shoots from the tip and bursts.'),
    ('cine_daisy_matriarch_pyre', 'daisy', 'da_matriarch_pyre', 7414, DA,
     'raises the fire spear high, her pink heart markings glowing, and calls up a towering pyre of flame across the right of the frame.'),
    ('cine_daisy_spearflame_rush', 'daisy', 'da_spearflame_rush', 7415, DA,
     'dashes forward wrapped in flame, the fire spear leading, leaving a long trail of fire streaking across the frame to the right.'),
    ('cine_daisy_burning_circle', 'daisy', 'da_burning_circle', 7416, DA,
     'plants the fire spear and a great circle of fire erupts around her, spreading out across the ground and flaring high.'),
    ('cine_elara_arc_bolt', 'elara', 'el_arc_bolt', 7421, EL,
     'flicks the glowing pen forward and a crackling arc of blue lightning leaps from its nib across the frame to the right.'),
    ('cine_elara_piercing_shot', 'elara', 'el_piercing_shot', 7422, EL,
     'aims the pen like an arrow and a single blinding spear of lightning pierces straight across the frame to the right through a rune circle.'),
    ('cine_elara_forest_scout', 'elara', 'el_forest_scout', 7423, EL,
     'writes quickly in the floating book and glowing paper birds fly out of its pages and scatter away into the distance.'),
    ('cine_elara_sapping_volley', 'elara', 'el_sapping_volley', 7424, EL,
     'flicks the pen several times and a volley of small violet lightning darts streaks across the frame to the right.'),
    ('cine_elara_artillery_barrage', 'elara', 'el_artillery_barrage', 7425, EL,
     'raises the open book overhead and many glowing rune circles appear in the sky, raining bolts of lightning across the right of the frame.'),
    ('cine_elara_storm_tally', 'elara', 'el_storm_tally', 7426, EL,
     'tallies marks in the air with the pen; each mark becomes a crackling storm cloud that strikes down with lightning across the frame.'),
    ('cine_helda_hearthfire_mend', 'helda', 'he_hearthfire_mend', 7431, HE,
     'raises a glowing brass tankard and warm golden hearth light flows out of it in healing ribbons.'),
    ('cine_helda_chilling_touch', 'helda', 'he_chilling_touch', 7432, HE,
     'thrusts her open palm forward to the right and a burst of frost and ice crystals sprays across the frame.'),
    ('cine_helda_frosted_ward', 'helda', 'he_frosted_ward', 7433, HE,
     'taps the war hammer on the ground and shimmering shields of frosted ice rise up around her.'),
    ('cine_helda_reserve_tonic', 'helda', 'he_reserve_tonic', 7434, HE,
     'pulls a small bubbling tonic bottle from her belt and tosses it forward with a wink; it sparkles as it flies.'),
    ('cine_helda_lend_strength', 'helda', 'he_lend_strength', 7435, HE,
     'flexes and claps her hands together, sending a wave of warm orange strength glowing outward.'),
    ('cine_helda_bulwark_brew', 'helda', 'he_bulwark_brew', 7436, HE,
     'raises a big foaming mug in a toast as a sturdy dome of golden light forms around her.'),
    ('cine_clarity_radiant_palm', 'clarity', 'cy_radiant_palm', 7441, CY,
     'sinks into a deep stance and drives a glowing open palm strike to the right; a burst of golden light explodes from her palm.'),
    ('cine_clarity_barkeep_tonic', 'clarity', 'cy_barkeep_tonic', 7442, CY,
     'pours a glowing tonic from a bottle into a cup with a flourish and offers it forward, golden sparkles rising.'),
    ('cine_clarity_flare', 'clarity', 'cy_flare', 7443, CY,
     'raises both hands overhead and a blazing flare of holy light bursts in the sky and rains down across the frame.'),
    ('cine_clarity_uplift', 'clarity', 'cy_uplift', 7444, CY,
     'presses her palms together and golden rings of light rise upward around her, lifting sparkles into the air.'),
    ('cine_clarity_smite', 'clarity', 'cy_smite', 7445, CY,
     'leaps and brings down a glowing fist to the right as a column of holy light smites the ground.'),
    ('cine_clarity_rejuvenating_draught', 'clarity', 'cy_rejuvenating_draught', 7446, CY,
     'uncorks a small glowing draught and drinks it, soft green and gold light swirling around her.'),
]:
    JOBS[job] = skill(unit, card, seed, f'{who} {action}')


def keyframe(job, spec, d):
    unit = spec.get('unit')
    src = WORK / f'fighter_{unit}' / 'cine_frame.png' if unit else d / 'rig_frame.png'
    fighter = Image.open(src).convert('RGBA')
    bg = Image.open(BACKDROP).convert('RGBA').resize(fighter.size, Image.Resampling.LANCZOS)
    bg.alpha_composite(fighter)
    return bg.convert('RGB')


def main(job):
    spec = JOBS[job]
    d = WORK / job
    d.mkdir(parents=True, exist_ok=True)
    key = keyframe(job, spec, d)
    stale = not (d / 'key_first.png').exists() or list(Image.open(d / 'key_first.png').convert('RGB').getdata()) != list(key.getdata())
    old = json.loads((d / 'spec.json').read_text()) if (d / 'spec.json').exists() else None
    if stale or old != json.loads(json.dumps(spec)):
        (d / 'h3_hi.mp4').unlink(missing_ok=True)          # new guard or prompt: render again
    (d / 'spec.json').write_text(json.dumps(spec, indent=2))
    key.save(d / 'key_first.png'); key.save(d / 'key_last.png')
    if not (d / 'h3_hi.mp4').exists():          # delete h3_hi.mp4 to re-render
        first = to_input(fit(key, spec['size']), f'ahcg-{job}-first.png')
        g = h3(spec['prompt'], spec['size'], spec['length'], spec['seed'], f'AdamsHaven/BattleMotion/{job}', first, first)
        (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
        print('h3', job, flush=True)
        for s in run(g, 'ahcg-cine'):
            if s.suffix == '.mp4':
                shutil.copyfile(s, d / ('h3_hi.mp4' if '_hi' in s.name else 'h3_24.mp4'))
    ULT_OUT.mkdir(parents=True, exist_ok=True)
    dst = ULT_OUT / f"{spec['card']}.mp4"
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(d / 'h3_hi.mp4'),
                    '-vf', 'select=not(mod(n\\,2)),setpts=N/60/TB,scale=1280:720:flags=lanczos', '-r', '60',
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart',
                    str(dst)], check=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(dst), '-vf', 'fps=15,scale=640:-1:flags=lanczos',
                    str(d / f'{job}_preview.gif')], check=True)
    print('CINE ->', dst)


if __name__ == '__main__':
    which = sys.argv[1]
    for job in ([j for j in JOBS if j.startswith(which[:-1])] if which.endswith('*') else [which]):
        main(job)
