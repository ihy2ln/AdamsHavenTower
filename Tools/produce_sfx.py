"""Game sound effects from the local ComfyUI MiniMax H3 model's native audio stream.

H3 ("fl2va") samples one latent that carries video AND audio. Here the video half is a throwaway: a tiny canvas
(384x224) with black first/last frames, and only the audio half is decoded (VAEDecodeAudio + the H3 audio VAE).
Each prompt's "Audio:" block describes ONE isolated effect (no music, no voice, no ambience); stingers may be musical.

  python Tools/produce_sfx.py mv_frost_jab        # one job, a prefix with *, or 'all'
  python Tools/produce_sfx.py sfx_* --force        # rebuild the .ogg from the cached raw render
  python Tools/produce_sfx.py hit --rerender       # render H3 again (new raw.flac), then rebuild

Outputs (Unity imports them; it writes the .meta files):
  Assets/Resources/AdamsHaven/Audio/Moves/<card id>.ogg   one per fighter / summoner card, ultimate and awakening
  Assets/Resources/AdamsHaven/Audio/Sfx/<id>.ogg          generic battle + exploration sounds
  Assets/Resources/AdamsHaven/Audio/<id>.ogg              overrides for TowerAudio's synthesised ids
Work files: BattleMotion/sfx_<job>/ (raw.flac = decoded H3 audio, api-h3.json, info.json, spectrogram.png).
Manifest: BattleMotion/sfx/MANIFEST.md (rewritten after every run from all info.json files).

Post-processing (numpy analysis, ffmpeg resample + Vorbis): mono 44.1 kHz, 40 Hz high-pass, leading/trailing
silence trimmed (15 ms pre-roll), length capped per job, an adaptive tilt EQ
(dark, rumbly renders get up to -9 dB below 180 Hz and +3 dB above 2.5 kHz), fade-out, then gain to a common loudness (max momentary
K-weighted loudness TARGET_LUFS) with the sample peak held at PEAK_DB by a gentle look-ahead limiter (<= 6 dB).
"""
import json
import shutil
import subprocess
import sys
import time
import zlib
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import signal
from scipy.ndimage import minimum_filter1d, uniform_filter1d

sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_battle_motion import COMFY, WORK, api, h3, node, to_input  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
AUDIO = PROJECT / 'Assets/Resources/AdamsHaven/Audio'
FOLDERS = {'move': AUDIO / 'Moves', 'sfx': AUDIO / 'Sfx', 'synth': AUDIO}
MANIFEST = WORK / 'sfx' / 'MANIFEST.md'
FFMPEG = shutil.which('ffmpeg') or 'S:/Music/youtube-dl/DLP/ffmpeg-2023-08-07-git/bin/ffmpeg.exe'
AUDIO_VAE = 'MiniMaxH3\\minimax_h3_audio_vae_fp32.safetensors'
SIZE = (384, 224)            # smallest sensible canvas: we discard the video
STEPS = 6                    # turbo LoRA; the pilot at 6 steps came out brighter / less sub-bass than at 4
RATE = 44100
TARGET_LUFS = -15.0          # max momentary (400 ms) K-weighted loudness of every clip
PEAK_DB = -1.0               # sample-peak ceiling
MAX_LIMIT_DB = 6.0           # most gain reduction the limiter may apply to reach TARGET_LUFS

SFX_RULES = ('One single isolated game sound effect that starts immediately at 0.0 seconds and is followed by '
             'silence. No music, no melody, no speech, no voice, no singing, no vocal sounds, no breathing, no '
             'ambience bed, no background noise, no room tone. Clean, crisp, punchy, close-miked studio foley '
             'for a fantasy anime card battle game.')
STINGER_RULES = ('A short game music stinger that starts immediately at 0.0 seconds and ends cleanly, followed by '
                 'silence. No speech, no voice, no singing, no choir, no ambience bed, no background noise.')
LOOK = {   # colour of the throwaway visual, matched to the sound's element
    'ice': 'pale blue ice shards and white frost', 'fire': 'orange flames and embers',
    'lightning': 'blue-white lightning arcs', 'light': 'golden holy light and sparkles',
    'magic': 'violet magic glyphs and glowing playing cards', 'dark': 'dark purple smoke and violet sparks',
    'earth': 'brown rock debris and dust', 'wind': 'pale green wind streaks', 'water': 'blue water splashes',
    'neutral': 'a white impact spark', 'gold': 'golden coins and sparkles',
}
UNIT_LOOK = {'kaela': 'ice', 'ghislaine': 'fire', 'elara': 'lightning', 'helda': 'ice', 'daisy': 'fire',
             'clarity': 'light', 'jd': 'magic'}


def J(folder, cap, look, sound, music=False, hp=40, take=0):
    """cap: max seconds after trim. Render length is the 17k+5 frame count with ~0.7 s of headroom over the cap.
    hp: high-pass corner in Hz (paper / UI sounds use 150 to drop H3's low noise bed). take: bump to re-roll the seed."""
    frames = next(n for n in (39, 56, 73, 90, 107) if n / 24 >= cap + 0.6)
    return dict(folder=folder, cap=cap, look=look, sound=sound, music=music, frames=frames, hp=hp, take=take)


def M(unit, cap, sound):
    return J('move', cap, UNIT_LOOK[unit], sound)


JOBS = {
    # ---------------------------------------------------------------- Kaela: ice brawler, Celestium gauntlets
    'mv_frost_jab': M('kaela', 0.8, 'Frost Jab: a quick sharp punch from a metal gauntlet, a tight thud with a bright '
                      'icy crack and a short tinkle of ice crystals'),
    'mv_rime_sweep': M('kaela', 1.2, 'Rime Sweep: a wide sweeping whoosh of freezing wind with a crackle of frost '
                       'spreading and a scrape of ice'),
    'mv_glacier_guard': M('kaela', 1.4, 'Glacier Guard: thick ice plates forming with a crystalline creak and crunch, '
                          'then a solid glassy clunk and a cold shimmer'),
    'mv_shatter_hook': M('kaela', 1.3, 'Shatter: a heavy hook punch from a metal gauntlet slams into a block of ice '
                         'that explodes into shards, a big glassy shatter with tinkling debris'),
    'mv_whiteout_counter': M('kaela', 1.3, 'Whiteout Counter: a swirling blizzard gust whooshing in a circle that '
                             'ends in one sharp icy snap'),
    'mv_permafrost_brace': M('kaela', 1.5, 'Perma Brace: deep cracking ice spikes growing out of stone with a low '
                             'crunch, then a soft sparkling healing shimmer'),
    # ---------------------------------------------------------------- Ghislaine: fire greatsword
    'gh_tiger_cleave': M('ghislaine', 1.0, 'Tiger Cleave: a heavy greatsword swing whoosh and a meaty slash impact with '
                         'a burst of fire'),
    'gh_guard_stance': M('ghislaine', 1.2, 'Guard Stance: a greatsword planted into stone with a heavy metallic clang '
                         'and ring, and a crackle of embers'),
    'gh_fireline_cut': M('ghislaine', 1.4, 'Fireline Cut: a long sweeping blade swish followed by a roaring line of '
                         'flame igniting across the ground'),
    'gh_ember_chain': M('ghislaine', 1.2, 'Ember Chain: two fast greatsword slashes in a row, each with a crackling '
                        'burst of embers'),
    'gh_reckless_swing': M('ghislaine', 1.5, 'Reckless Hit: an enormous overhead greatsword smash, a heavy whoosh into '
                           'a booming fiery impact with crumbling stone debris'),
    'gh_tiger_riposte': M('ghislaine', 1.1, 'Tiger Riposte: a sharp metal parry clang, instantly followed by a fast '
                          'counter-slash with a flaming whoosh'),
    # ---------------------------------------------------------------- Elara: lightning caster with a pen
    'el_arc_bolt': M('elara', 0.8, 'Arc Bolt: a quick crackling electric zap and a sharp snap of a small lightning '
                     'bolt'),
    'el_piercing_shot': M('elara', 1.2, 'Piercing Shot: a fast rising electric charge whine, then a sharp piercing '
                          'crack of a lightning beam'),
    'el_forest_scout': dict(M('elara', 0.9, 'Forest Scout: a quick scratch of a pen nib on paper and two soft paper '
                              'card flicks with a tiny magic sparkle'), hp=150),
    'el_sapping_volley': M('elara', 1.2, 'Sap Volley: a rapid volley of three small electric zaps and a draining '
                           'downward electric buzz'),
    'el_artillery_barrage': M('elara', 1.6, 'Barrage: many crackling lightning bolts striking in rapid succession with '
                              'sharp thunder cracks'),
    'el_storm_tally': M('elara', 1.5, 'Storm Tally: a quick pen tick on paper, then a rolling crackle of thunder and '
                        'spreading electric hum'),
    # ---------------------------------------------------------------- Helda: frost support with a war hammer
    'he_hearthfire_mend': M('helda', 1.3, 'Hearth Mend: a soft warm whoosh of hearth fire with a gentle sparkling '
                            'healing shimmer'),
    'he_chilling_touch': M('helda', 0.9, 'Chilling Touch: a cold hiss of frost and a crisp freezing crackle'),
    'he_frosted_ward': M('helda', 1.4, 'Frost Ward: a war hammer taps stone, then a glassy ring of ice forms with a '
                         'protective shimmering hum'),
    'he_reserve_tonic': M('helda', 1.0, 'Reserve Tonic: a glass bottle cork pop and a short liquid slosh with a small '
                          'magic sparkle'),
    'he_lend_strength': M('helda', 1.1, 'Lend Strength: a rising warm magical whoosh ending in a soft pulse of '
                          'energy'),
    'he_bulwark_brew': M('helda', 1.6, 'Bulwark Brew: a heavy war hammer strikes the ground with a deep thud, then '
                         'thick ice walls rise with a crunching creak and a cold shimmer'),
    # ---------------------------------------------------------------- Daisy: fire spear
    'da_bonfire_brand': M('daisy', 1.0, 'Bonfire Brand: a spear thrust whoosh and a fire igniting with a whump and '
                          'crackle'),
    'da_wand_sweep': M('daisy', 1.3, 'Wand Sweep: a wide spear sweep swish trailing a roaring arc of fire'),
    'da_ember_thrust': M('daisy', 0.9, 'Ember Thrust: a quick spear stab, a sharp metallic pierce and a sizzle of '
                         'embers'),
    'da_matriarch_pyre': M('daisy', 1.6, 'Matriarch Pyre: a large bonfire erupting with a deep fiery whoosh and a '
                           'roaring crackle'),
    'da_spearflame_rush': M('daisy', 1.4, 'Flame Rush: a rapid charging dash whoosh into a fiery spear impact '
                            'explosion'),
    'da_burning_circle': M('daisy', 1.6, 'Fire Circle: a ring of flames igniting all around with a swirling circular '
                           'fire roar'),
    # ---------------------------------------------------------------- Clarity: light, kunai and healing
    'cy_radiant_palm': M('clarity', 0.9, 'Radiant Palm: a quick palm strike thump with a bright shimmering burst of '
                         'light'),
    'cy_barkeep_tonic': M('clarity', 1.1, 'Tonic: a glass bottle cork pop, a short liquid pour and a soft healing '
                          'sparkle'),
    'cy_flare': M('clarity', 1.4, 'Flare: a bright flash-bang of holy light, a whooshing radiant burst with a sparkly '
                  'shimmer'),
    'cy_uplift': M('clarity', 1.4, 'Uplift: an ascending magical shimmer that swells brighter, a rising glow'),
    'cy_smite': M('clarity', 1.4, 'Smite: a thrown kunai whistling through the air into a powerful holy light impact '
                  'boom with a ringing shimmer'),
    'cy_rejuvenating_draught': M('clarity', 1.2, 'Draught: a bubbly potion fizz and a soft warm healing sparkle'),
    # ---------------------------------------------------------------- JD: summoner cards
    'sm_rally': dict(M('jd', 0.9, 'Rally: two quick playing card flicks and snaps with a rising magical sparkle'), hp=150),
    'sm_surge': M('jd', 1.0, 'Surge: a quick energy charge-up whoosh ending in a bright magical pulse'),
    'sm_second': M('jd', 1.0, 'Second Wind: a fresh rushing gust of wind with a light airy sparkle'),
    'sm_banner': M('jd', 1.1, 'Banner: a heavy cloth banner unfurling with a strong flapping snap and a glowing '
                   'magical rise'),
    'sm_aegis': M('jd', 1.2, 'Aegis: a magical shield dome forming with a deep resonant hum and a glassy shimmer'),
    'sm_wither': M('jd', 1.2, 'Wither: a dark descending magical hiss with a crumbling, decaying crackle'),
    'sm_hush': M('jd', 1.1, 'Hush: a soft muffling whoosh that sucks inward, a dampening low swell'),
    'sm_mend': M('jd', 1.3, 'Blessing: a gentle sparkling healing shimmer of tiny tinkling crystals and a warm glow'),
    'sm_draw_eye': M('jd', 1.0, 'Taunt: a sharp magical sigil ping followed by a pulsing aggressive low throb'),
    'ult_sum_a': J('move', 2.6, 'magic', 'Rally Decree: a burst of dozens of playing cards fluttering, then a bright '
                   'powerful rising magical surge with a shimmering crescendo'),
    'ult_sum_b': J('move', 2.6, 'dark', 'Sunder Decree: cards slam down with a sharp snap, then a massive dark magical '
                   'shockwave with deep cracking rumble'),
    # ---------------------------------------------------------------- ultimates
    'ult_kaela_avalanche': J('move', 3.0, 'ice', 'Avalanche Breaker: two gauntlet fists slam into frozen ground, a '
                             'colossal eruption of ice, roaring avalanche rumble and shattering ice shards'),
    'ult_kaela_bastion': J('move', 2.8, 'ice', 'Winter Bastion: huge ice walls rising with deep crunching creaks, then '
                           'a resonant crystalline dome hum and glittering frost'),
    'ult_ghislaine': J('move', 2.8, 'fire', 'White Tiger Rend: a rising roar of flame along a greatsword, a giant '
                       'tearing slash and an explosive fiery impact'),
    'ult_ghislaine_oath': J('move', 2.8, 'fire', "Warden's Oath: a greatsword driven into stone with a heavy metallic "
                            'impact, then a great wall of fire roaring up'),
    'ult_elara': J('move', 3.0, 'lightning', 'Stormfall Barrage: rolling thunder and many lightning bolts striking one '
                   'after another with sharp electric crackles'),
    'ult_elara_convergence': J('move', 2.8, 'lightning', 'Lightning Convergence: a rising high electric charge whine, '
                               'then a colossal lightning beam blast with a huge thunder crack'),
    'ult_helda': J('move', 2.8, 'fire', 'Hearth Renewal: a warm roaring hearth fire swelling up with a soft magical '
                   'healing shimmer and gentle sparkles'),
    'ult_helda_sanctuary': J('move', 2.8, 'ice', 'Winter Sanctuary: a war hammer slams the ground with a deep boom, ice '
                             'pillars erupt with crystalline crackles and a resonant cold hum'),
    'ult_daisy': J('move', 3.0, 'fire', 'Island Conflagration: a huge whooshing firestorm sweeping across, roaring '
                   'flames, crackling embers and an explosion'),
    'ult_daisy_inferno': J('move', 2.8, 'fire', "Matriarch's Inferno: a deep rumble building into a towering pillar of "
                           'flame erupting with a roaring blaze'),
    'ult_clarity': J('move', 2.8, 'light', 'Final Heaven: a charged glowing hum, then a colossal punch impact with a '
                     'blinding holy light explosion and a booming shockwave'),
    'ult_clarity_feast': J('move', 2.6, 'light', 'Luminous Feast: a radiant ascending shimmer of holy light swelling '
                           'warmly with sparkling crystal tinkles'),
    # ---------------------------------------------------------------- awakenings
    'aw_kaela': J('move', 1.5, 'ice', 'Winter Resolve: a crisp ice crackle and a reviving cold shimmer swell'),
    'aw_ghislaine': J('move', 1.5, 'fire', "Tiger's Return: a fire igniting with a whoosh and a determined ring of a "
                      'greatsword blade'),
    'aw_elara': J('move', 1.4, 'lightning', 'Clear Script: a quick pen scratch, an electric spark crackle and a bright '
                  'magical ping'),
    'aw_helda': J('move', 1.5, 'fire', 'Hearth Reprieve: a warm fire crackle swelling with a soft healing sparkle'),
    'aw_daisy': J('move', 1.5, 'fire', 'Wildheart Surge: two deep heartbeat thumps and a fiery surging whoosh'),
    'aw_clarity': J('move', 1.5, 'light', 'Recovery: a soft rising shimmer of holy light with a gentle crystal '
                    'tinkle'),
    # ---------------------------------------------------------------- generic battle sounds
    'sfx_card_draw': J('sfx', 0.5, 'magic', 'a single playing card sliding off a deck with a quick papery swipe', hp=150),
    'sfx_card_play': J('sfx', 0.6, 'magic', 'a playing card slapped down onto a wooden table with a crisp snap and a '
                       'tiny magic sparkle', hp=150),
    'sfx_shield_up': J('sfx', 0.9, 'light', 'a magical barrier shield powering up, a quick rising whoosh into a glassy '
                       'resonant hum'),
    'sfx_shield_block': J('sfx', 0.7, 'neutral', 'a hit blocked by a magical shield, a solid glassy thunk with a short '
                          'metallic ring'),
    'sfx_break': J('sfx', 0.9, 'neutral', 'a heavy guard break, thick armor and a crystal barrier shattering with a big '
                   'crunching crack'),
    'sfx_turn_start': J('sfx', 0.9, 'light', 'a bright short magical whoosh with a clear swelling shimmer, signalling '
                        'the start of a turn'),
    'sfx_enemy_turn': J('sfx', 0.9, 'dark', 'a low ominous whoosh with a deep dark pulse, signalling danger'),
    'sfx_hit_fire': J('sfx', 0.7, 'fire', 'a fire spell hitting its target, a punchy fiery whump with crackling '
                      'embers'),
    'sfx_hit_water': J('sfx', 0.7, 'water', 'a water spell hitting its target, a punchy heavy splash'),
    'sfx_hit_wind': J('sfx', 0.7, 'wind', 'a wind blade hitting its target, a sharp cutting whoosh and slice'),
    'sfx_hit_earth': J('sfx', 0.7, 'earth', 'a rock hitting its target, a heavy thud of stone with crumbling gravel'),
    'sfx_hit_lightning': J('sfx', 0.7, 'lightning', 'a lightning bolt hitting its target, a sharp electric zap and '
                           'crackle'),
    'sfx_hit_light': J('sfx', 0.7, 'light', 'a holy light spell hitting its target, a bright radiant impact with a '
                       'shimmering ring'),
    'sfx_hit_dark': J('sfx', 0.7, 'dark', 'a dark magic spell hitting its target, a deep warped thump with a hissing '
                      'shadowy crackle'),
    'sfx_hit_neutral': J('sfx', 0.6, 'neutral', 'a solid physical punch impact, a meaty thud'),
    'sfx_buff': J('sfx', 0.9, 'light', 'a positive power-up, a quick bright ascending magical shimmer'),
    'sfx_debuff': J('sfx', 0.9, 'dark', 'a negative curse, a quick descending warbling magical drain'),
    'sfx_ult_ready': J('sfx', 1.2, 'light', 'an ultimate skill becoming ready, a powerful charging whoosh into a bright '
                       'sparkling flash'),
    'sfx_decree': J('sfx', 1.5, 'magic', 'a commanding magical decree, a deep resonant gong-like boom with a wave of '
                    'shimmering energy'),
    'sfx_enemy_claw': J('sfx', 0.8, 'neutral', 'a monster claw swipe, a fast tearing whoosh and three raking scratches'),
    'sfx_enemy_bite': J('sfx', 0.8, 'neutral', 'a monster bite, jaws snapping shut with a sharp crunchy chomp'),
    'sfx_enemy_magic': J('sfx', 0.9, 'dark', 'a monster casting a spell, a warbling dark magical whoosh into a burst'),
    'sfx_enemy_slam': J('sfx', 0.8, 'earth', 'a huge monster body slam, a heavy booming thud with the ground shaking'),
    # ---------------------------------------------------------------- generic exploration sounds
    'sfx_atlas_reveal': J('sfx', 1.2, 'gold', 'an old parchment map unrolling with a papery rustle and a soft magical '
                          'sparkle as a new region is revealed'),
    'sfx_atlas_travel': J('sfx', 1.2, 'wind', 'quick travel across a map, a brisk whoosh with light rustling footsteps '
                          'on a path'),
    'sfx_atlas_complete': J('sfx', 1.6, 'gold', 'a region completed, a bright magical sparkle swell ending in a clear '
                            'shimmering bell-like ping'),
    'sfx_tower_activate': J('sfx', 1.5, 'magic', 'an ancient stone tower activating, a deep rumble and grinding stone '
                            'with a rising magical hum'),
    'sfx_room_reveal': J('sfx', 1.0, 'neutral', 'a dungeon room revealed, a soft dusty whoosh as darkness lifts with a '
                         'faint shimmer'),
    'sfx_trap_spring': J('sfx', 0.8, 'neutral', 'a trap springing, a sharp metallic click and a fast spring snap'),
    'sfx_treasure_open': J('sfx', 1.2, 'gold', 'a wooden treasure chest creaking open with a clunk and a jingle of '
                           'gold coins'),
    'sfx_stairs_descend': J('sfx', 1.2, 'earth', 'footsteps quickly descending stone stairs, echoing slightly',
                            take=1),   # take 0 opened with 0.6 s of tonal drone before the steps
    'sfx_epiphany': J('sfx', 1.2, 'light', 'a sudden insight, a bright magical shimmer with a clear rising chime'),
    'sfx_partner': J('sfx', 0.8, 'light', 'a reserve partner stepping in, a quick heroic swoosh-in with a light '
                     'chime'),
    # ---------------------------------------------------------------- overrides for TowerAudio's synth ids
    'swing': J('synth', 0.5, 'neutral', 'a fast weapon swing, a short sharp whoosh through the air'),
    'hit': J('synth', 0.5, 'neutral', 'a weapon hitting its target, a punchy solid impact thud'),
    'crit': J('synth', 0.8, 'light', 'a critical hit, a heavy punchy impact with a bright sharp metallic ring'),
    'heal': J('synth', 1.0, 'light', 'healing, a soft rising sparkling shimmer of tiny crystal tinkles'),
    'status': J('synth', 0.6, 'magic', 'a status effect applied, a quick short magical blip and shimmer', hp=150),
    'down': J('synth', 1.0, 'dark', 'a fighter knocked down, a heavy body thump onto the ground with a descending '
              'whoosh'),
    'ult': J('synth', 1.5, 'light', 'an ultimate skill unleashed, a big rising whoosh into a powerful bright burst'),
    'card': J('synth', 0.4, 'magic', 'a playing card flicked, a short crisp papery snap', hp=150),
    'door': J('synth', 1.0, 'neutral', 'a heavy wooden dungeon door opening with a creak and a solid thud'),
    'victory': J('synth', 3.0, 'gold', 'a short triumphant fanfare stinger for winning a battle: bright brass and '
                 'strings rising to a major chord with a cymbal swell', music=True),
    'defeat': J('synth', 3.0, 'dark', 'a short sad stinger for losing a battle: a slow descending minor phrase on '
                'low strings and piano fading out', music=True),
    'reward': J('synth', 2.0, 'gold', 'a short happy reward jingle: quick ascending glockenspiel notes and sparkling '
                'chimes', music=True),
    'chime': J('synth', 1.8, 'light', 'a single clear magical chime: two soft bell tones ringing out', music=True),
}


def out_path(job, spec):
    name = job[4:] if spec['folder'] == 'sfx' else job
    return FOLDERS[spec['folder']] / f'{name}.ogg'


def prompt_of(spec):
    look = LOOK[spec['look']]
    visual = (f'Abstract anime game VFX: a burst of {look} flashes in the center of a pure black frame at the very '
              f'first instant, in sync with the sound, then fades back to pure black. Locked camera, no character, '
              f'no text.')
    rules = STINGER_RULES if spec['music'] else SFX_RULES
    return f"{visual}\n\nAudio: {spec['sound']}. {rules} The sound lasts about {spec['cap']:g} seconds."


def seed_of(job, take=0):
    return 7000 + zlib.crc32(job.encode()) % 90000 + 1000 * take


# ---------------------------------------------------------------- ComfyUI ----------------------------------
def graph(prompt, frames, seed, prefix, black):
    """produce_battle_motion.h3 with the video decode replaced by the audio branch:
    SamplerCustomAdvanced.output -> VAEDecodeAudio(samples, vae=VAELoader(audio vae)) -> SaveAudio (FLAC)."""
    g = h3(prompt, SIZE, frames, seed, prefix, black, black, rife=1)
    g['298']['inputs']['steps'] = STEPS
    del g['122'], g['218']                     # VAEDecode + VHS_VideoCombine: the video is thrown away
    g['a_vae'] = node('VAELoader', vae_name=AUDIO_VAE)
    g['a_dec'] = node('VAEDecodeAudio', samples=['125', 0], vae=['a_vae', 0])
    g['a_save'] = node('SaveAudio', audio=['a_dec', 0], filename_prefix=prefix)
    return g


def run_audio(g, client='ahcg-sfx'):
    for attempt in range(4):
        pid = api('/prompt', {'prompt': g, 'client_id': client})['prompt_id']
        while True:
            h = api('/history/' + pid)
            if h:
                h = h[pid]
                break
            time.sleep(2)
        if h['status']['status_str'] == 'success':
            return [COMFY / f.get('type', 'output') / f.get('subfolder', '') / f['filename']
                    for out in h['outputs'].values() for f in out.get('audio', [])]
        msg = str(h['status'])
        if 'hostbuf_file_reader_read' in msg and attempt < 3:   # transient memory pressure
            print('  hostbuf read failed, retrying', flush=True)
            time.sleep(10)
            continue
        raise RuntimeError(msg[-2000:])


def render(job, spec, d):
    black = to_input(Image.new('RGB', SIZE, (0, 0, 0)), f'ahcg-black-{SIZE[0]}x{SIZE[1]}.png')
    g = graph(prompt_of(spec), spec['frames'], seed_of(job, spec['take']), f'AdamsHaven/Sfx/{job}', black)
    (d / 'api-h3.json').write_text(json.dumps(g, indent=2))
    outs = run_audio(g)
    shutil.copyfile(outs[0], d / 'raw.flac')


# ---------------------------------------------------------------- audio ------------------------------------
def load_mono(path):
    pcm = subprocess.run([FFMPEG, '-v', 'error', '-i', str(path), '-ac', '1', '-ar', str(RATE), '-f', 'f32le', '-'],
                         check=True, capture_output=True).stdout
    return np.frombuffer(pcm, np.float32).astype(np.float64)


def biquad(kind, G, Q, fc, fs=RATE):
    """RBJ-cookbook shelves / high-pass (the BS.1770 K-weighting filters are the same shapes)."""
    A, w0 = 10 ** (G / 40), 2 * np.pi * fc / fs
    a, c, r = np.sin(w0) / (2 * Q), np.cos(w0), 2 * np.sqrt(10 ** (G / 40)) * np.sin(w0) / (2 * Q)
    if kind == 'high_shelf':
        b = [A * ((A + 1) + (A - 1) * c + r), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - r)]
        den = [(A + 1) - (A - 1) * c + r, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - r]
    elif kind == 'low_shelf':
        b = [A * ((A + 1) - (A - 1) * c + r), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - r)]
        den = [(A + 1) + (A - 1) * c + r, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - r]
    else:
        b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
        den = [1 + a, -2 * c, 1 - a]
    return np.array(b) / den[0], np.array(den) / den[0]


def k_weight(x, fs=RATE):
    """ITU-R BS.1770 K-weighting (high shelf + high pass), coefficients for any sample rate."""
    for kind, G, Q, fc in (('high_shelf', 4.0, 1 / np.sqrt(2), 1500.0), ('hp', 0.0, 0.5, 38.0)):
        x = signal.lfilter(*biquad(kind, G, Q, fc, fs), x)
    return x


def low_share(x):
    """Share of the energy below 250 Hz."""
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    p = np.abs(np.fft.rfft(x)) ** 2
    return float(p[f < 250].sum() / (p.sum() + 1e-20))


def tilt(y):
    """H3 renders roars / booms / smashes as low rumbles (70-90% of the energy under 250 Hz, crackle 20-30 dB
    down), which a phone speaker barely plays. Dark clips get a low-shelf cut scaled by how dark they are and a
    small presence lift; balanced clips (UI, paper, glass, the stingers) are left alone."""
    share = low_share(y)
    cut = float(np.clip((share - 0.5) * 22, 0, 9))
    if cut > 0:
        y = signal.lfilter(*biquad('low_shelf', -cut, 1 / np.sqrt(2), 180.0), y)
        y = signal.lfilter(*biquad('high_shelf', min(3.0, cut / 2), 1 / np.sqrt(2), 2500.0), y)
    return y, dict(low_share=round(share, 2), eq_cut_db=round(cut, 1), low_share_eq=round(low_share(y), 2))


def momentary_max(x):
    """Loudest 400 ms window (whole clip if shorter), in LUFS."""
    k = k_weight(x) ** 2
    win = min(len(k), int(0.4 * RATE))
    ms = uniform_filter1d(k, win, mode='constant')[win // 2: len(k) - win // 2 + 1] if len(k) > win else [k.mean()]
    return -0.691 + 10 * np.log10(max(float(np.max(ms)), 1e-12))


def envelope_db(x, hop):
    n = len(x) // hop
    rms = np.sqrt(np.mean(x[:n * hop].reshape(n, hop) ** 2, axis=1) + 1e-12)
    return 20 * np.log10(rms)


def limiter(x, ceiling):
    """Look-ahead peak limiter: the gain never exceeds what each sample needs; 2 ms look-ahead, 60 ms release."""
    need = np.minimum(1.0, ceiling / np.maximum(np.abs(x), 1e-9))
    la = int(0.002 * RATE)
    g = minimum_filter1d(need, 2 * la + 1)
    g = uniform_filter1d(g, la + 1)
    g = np.minimum(g, need)
    rel = np.exp(-1 / (0.06 * RATE))           # release: recover slowly, never above the requirement
    out = g.copy()
    for i in range(1, len(out)):
        out[i] = min(g[i], rel * out[i - 1] + (1 - rel) * g[i])
    return x * out


def analyse(x):
    """Numbers used to sanity-check a clip: drone, tonal (voice/music) content, brightness."""
    hop = int(0.005 * RATE)
    env = envelope_db(x, hop)
    e = 10 ** (env / 10)
    third = max(1, len(e) // 3)
    f, t, S = signal.spectrogram(x, RATE, nperseg=1024, noverlap=512)
    band = (f >= 100) & (f <= 12000)          # the source is 32 kHz: nothing above 16 kHz
    f, P = f[band], S[band].mean(axis=1) + 1e-20
    flat = float(np.exp(np.mean(np.log(P))) / np.mean(P))
    centroid = float((f * P).sum() / P.sum())
    # crude voicing: share of loud 40 ms frames with a strong pitch period in 80-400 Hz (speech or a tune)
    fl = int(0.04 * RATE)
    voiced = loud = 0
    for i in range(0, len(x) - fl, fl // 2):
        seg = x[i:i + fl] - x[i:i + fl].mean()
        if np.sqrt(np.mean(seg ** 2)) < 10 ** ((env.max() - 25) / 20):
            continue
        loud += 1
        ac = np.correlate(seg, seg, 'full')[fl - 1:]
        lo, hi = int(RATE / 400), int(RATE / 80)
        if ac[0] > 0 and ac[lo:hi].max() / ac[0] > 0.6:
            voiced += 1
    return dict(dur=round(len(x) / RATE, 3), peak_db=round(20 * np.log10(np.abs(x).max() + 1e-12), 2),
                rms_db=round(10 * np.log10(np.mean(x ** 2) + 1e-12), 2), lufs_m=round(momentary_max(x), 2),
                tail_ratio=round(float(e[-third:].sum() / (e.sum() + 1e-12)), 3), flatness=round(flat, 3),
                centroid_hz=round(centroid), voiced=round(voiced / max(loud, 1), 2))


def process(raw, cap, hp=40):
    x = load_mono(raw)
    raw_stats = dict(raw_dur=round(len(x) / RATE, 3), raw_peak_db=round(20 * np.log10(np.abs(x).max() + 1e-12), 2))
    # H3 puts a lot of energy under 60 Hz (a third to half of it in the pilot): cut the useless sub-bass
    x = signal.sosfilt(signal.butter(4, hp, 'hp', fs=RATE, output='sos'), x)
    hop = int(0.005 * RATE)
    env = envelope_db(x, hop)
    top, floor = env.max(), np.percentile(env, 10)
    if top < -60:
        raise RuntimeError(f'silent render (peak env {top:.1f} dB)')
    # H3 often leads with a quiet swell: short sounds start at the real attack, long ones keep their build-up
    on = np.where(env > top - (15 if cap <= 1.0 else 20 if cap <= 1.8 else 30))[0]
    end_thr = max(top - 42, floor + 6)
    off = np.where(env > end_thr)[0]
    a = max(0, on[0] * hop - int(0.015 * RATE))
    b = min(len(x), (off[-1] + 1) * hop + int(0.03 * RATE))
    capped = bool((b - a) / RATE > cap)
    if capped:
        b = a + int(cap * RATE)
    y, eq = tilt(x[a:b].copy())
    fade = (min(0.35, max(0.08, 0.2 * cap)) if capped else min(0.12, max(0.02, 0.1 * len(y) / RATE)))
    nf = min(len(y), int(fade * RATE))
    y[-nf:] *= np.cos(np.linspace(0, np.pi / 2, nf)) ** 2
    ni = int(0.005 * RATE)
    y[:ni] *= np.linspace(0, 1, ni)
    # loudness: aim every clip at TARGET_LUFS, peak held at PEAK_DB, limiter allowed MAX_LIMIT_DB of reduction
    peak = 20 * np.log10(np.abs(y).max())
    gain = min(TARGET_LUFS - momentary_max(y), PEAK_DB - peak + MAX_LIMIT_DB)
    y = limiter(y * 10 ** (gain / 20), 10 ** (PEAK_DB / 20))
    return y, dict(raw_stats, **eq, start_s=round(a / RATE, 3), capped=capped, gain_db=round(gain, 1))


def write_ogg(y, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([FFMPEG, '-y', '-v', 'error', '-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-i', '-',
                    '-c:a', 'libvorbis', '-q:a', '6', str(dst)], input=y.astype(np.float32).tobytes(), check=True)


def spectrogram(src, dst):
    subprocess.run([FFMPEG, '-y', '-v', 'error', '-i', str(src), '-lavfi',
                    'showspectrumpic=s=640x256:legend=1:scale=log:fscale=log', str(dst)], check=True)


# ---------------------------------------------------------------- driver -----------------------------------
def build(job, force=False, rerender=False):
    spec = JOBS[job]
    dst = out_path(job, spec)
    d = WORK / f'sfx_{job}'
    if dst.exists() and not (force or rerender):
        print('skip', job)
        return
    d.mkdir(parents=True, exist_ok=True)
    (d / 'spec.json').write_text(json.dumps(dict(spec, prompt=prompt_of(spec), seed=seed_of(job, spec['take'])), indent=2))
    t0 = time.time()
    if rerender or not (d / 'raw.flac').exists():
        print('h3', job, flush=True)
        render(job, spec, d)
    y, info = process(d / 'raw.flac', spec['cap'], spec['hp'])
    write_ogg(y, dst)
    info.update(analyse(load_mono(dst)), id=job, out=str(dst.relative_to(AUDIO)).replace('\\', '/'),
                sound=spec['sound'], secs=round(time.time() - t0, 1))
    spectrogram(dst, d / 'spectrogram.png')
    (d / 'info.json').write_text(json.dumps(info, indent=2))
    print(f"  -> {info['out']}  {info['dur']:.2f}s  peak {info['peak_db']} dB  {info['lufs_m']} LUFS-M  "
          f"start {info['start_s']}s  tail {info['tail_ratio']}  voiced {info['voiced']}  ({info['secs']}s)", flush=True)


def write_manifest():
    rows = []
    for job, spec in JOBS.items():
        f = WORK / f'sfx_{job}' / 'info.json'
        if f.exists() and out_path(job, spec).exists():
            i = json.loads(f.read_text())
            rows.append(f"| `{i['out']}` | {i['dur']:.2f} s | {i['lufs_m']:.1f} | {spec['sound']} |")
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(
        '# Adams Haven SFX manifest\n\nGenerated by `Tools/produce_sfx.py` from the local MiniMax H3 audio stream. '
        f'Paths are relative to `Assets/Resources/AdamsHaven/Audio/`. Mono 44.1 kHz Ogg Vorbis, max momentary '
        f'loudness about {TARGET_LUFS:g} LUFS, sample peak <= {PEAK_DB:g} dBFS.\n\n'
        f'{len(rows)} clips.\n\n| clip | length | LUFS-M | prompt |\n|---|---|---|---|\n' + '\n'.join(rows) + '\n')


def main(argv):
    which = argv[0]
    force, rerender = '--force' in argv, '--rerender' in argv
    todo = list(JOBS) if which == 'all' else [j for j in JOBS if j.startswith(which[:-1])] if which.endswith('*') \
        else [which]
    failed = []
    for job in todo:
        try:
            build(job, force, rerender)
        except Exception as e:   # noqa: BLE001 - keep the batch going, retry once
            print('  FAILED', job, str(e)[-400:], '- retrying', flush=True)
            try:
                build(job, True, True)
            except Exception as e2:  # noqa: BLE001
                print('  FAILED twice', job, str(e2)[-400:], flush=True)
                failed.append(job)
    write_manifest()
    if failed:
        print('FAILED:', ' '.join(failed))


if __name__ == '__main__':
    main(sys.argv[1:])
