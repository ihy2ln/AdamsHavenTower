"""Per-card battle effect layers: the single source of their wording and placement.

Each fighter card is staged in battle (BattleStage / BattleCinematics) from up to three transparent layers drawn over
the live field, instead of an opaque video:
  charge  - at the fighter's muzzle (hand / weapon tip from clips.json tips) while she winds up
  travel  - from the muzzle to the target: 'beam' (a stretched horizontal strip) or 'bolt' (a projectile sprite)
  impact  - on each target ('each'), once on the centroid, on the actor or on allies ('aura')
The wording comes from the retired skill-insert videos (Tools/produce_cine.py) so the look carries over.
Tools/produce_move_fx.py renders every layer with MiniMax H3 on black and writes Resources/AdamsHaven/Fx/Moves/moves.json.

Runtime params (virtual 1600x900 px, battle seconds): size = width; life = how long it plays; lift = fraction of the
unit's height above its feet; under = drawn beneath the units; hold = a charge that stays until the release;
width / grow / arc / count / stagger for travel.
"""

PALETTE = {
    'kaela': 'icy blue-white', 'ghislaine': 'orange-gold fire', 'daisy': 'blazing orange fire with pink sparks',
    'elara': 'electric blue-white', 'helda': 'warm golden light and pale frost blue', 'clarity': 'radiant golden holy light',
}

# Common shapes ----------------------------------------------------------------------------------------------------
BEAM = ' It is perfectly horizontal and crosses the whole frame from the left edge to the right edge, centered vertically.'
BOLT = ' It points to the right and stays centered in the frame, flickering in place.'
TIGHT = (' It is small and stays in the middle of the frame with wide empty black space all around it, never touching '
         'the edges.')


def impact(prompt, size=380, life=.55, lift=.5, each=True, at='target', under=False, sheet_size=(704, 704), length=41,
           video=False, seed=0):
    return dict(prompt=prompt, size=size, life=life, lift=lift, each=each, at=at, under=under, sheet_size=sheet_size,
                length=length, video=video, seed=seed)


def charge(prompt, size=200, hold=True, offset=(.2, 0), length=33, seed=0):
    return dict(prompt=prompt, size=size, hold=hold, at='muzzle', offset=list(offset), length=length, seed=seed)


def beam(prompt, width=70, life=.3, grow=.08, seed=0):
    return dict(kind='beam', prompt=prompt + BEAM, width=width, life=life, grow=grow, count=1, stagger=0, arc=0, seed=seed)


def bolt(prompt, size=150, count=1, stagger=0., arc=0, seed=0):
    return dict(kind='bolt', prompt=prompt + BOLT, width=size, count=count, stagger=stagger, arc=arc, life=0, grow=0, seed=seed)


def aura(prompt, size=380, life=.8, lift=.45, each=True, at='allies', under=False, video=False, length=41, seed=0):
    return impact(prompt, size, life, lift, each, at, under, length=length, video=video, seed=seed)


MOVES = {
    # ---------------------------------------------------------------- Kaela (melee, ice): existing sheets migrate
    'mv_frost_jab': dict(unit='kaela', impact=impact(None, 300, .5, .55)),
    'mv_rime_sweep': dict(unit='kaela', impact=impact(None, 470, .62, .45)),
    'mv_shatter_hook': dict(unit='kaela', impact=impact(None, 430, .7, .55)),
    'mv_whiteout_counter': dict(unit='kaela', impact=aura(None, 380, .8, .5, at='actor')),
    'mv_permafrost_brace': dict(unit='kaela', impact=aura(None, 360, .8, .2, at='actor')),
    'mv_glacier_guard': dict(unit='kaela', impact=aura(None, 380, 0, .42, at='actor', video=True)),
    'ult_kaela_avalanche': dict(unit='kaela', impact=impact(
        'A huge eruption of jagged ice spikes bursts up from the bottom center with snow and frost spray, then the '
        'spikes shatter into glittering shards and fade.', 460, .8, .1, seed=9601)),
    'ult_kaela_bastion': dict(unit='kaela', impact=aura(
        'A translucent dome of glowing ice crystal walls rises from the bottom around the center, facets glinting, '
        'then it dissolves into sparkles.', 380, .9, .3, seed=9602)),
    'aw_kaela': dict(unit='kaela', impact=aura(
        'A curtain of pale aurora light and swirling snowflakes rises upward from the bottom center, glittering, '
        'then fades.', 360, .9, .3, seed=9603)),

    # ---------------------------------------------------------------- Ghislaine (melee, fire)
    'gh_tiger_cleave': dict(unit='ghislaine', impact=impact(
        'Three huge diagonal claw slashes of orange-gold fire tear across the center like tiger claw marks, embers '
        'flying, then the flames fade.', 440, .6, .55, seed=9611)),
    'gh_guard_stance': dict(unit='ghislaine', impact=aura(
        'A low curved wall of golden fire rises in a ring, flames rippling upward, then settles into floating embers.'
        + TIGHT, 380, .8, .2, at='actor', under=True, seed=9712)),
    'gh_fireline_cut': dict(unit='ghislaine', impact=impact(
        'A long horizontal line of burning fire sweeps across the whole frame from left to right through the middle, '
        'flames licking upward, then it burns out.', 900, .7, .4, each=False, sheet_size=(1248, 352), seed=9613)),
    'gh_ember_chain': dict(unit='ghislaine', impact=impact(
        'Two quick crossing slashes of fire leave a chain of glowing embers that pop in a row of small fiery '
        'explosions, then fade.', 400, .6, .55, seed=9614)),
    'gh_reckless_swing': dict(unit='ghislaine', impact=impact(
        'A massive downward smash of fire explodes from the center with shattered rocks and a fiery shockwave '
        'ring, then smoke and embers.', 480, .7, .45, seed=9615)),
    'gh_tiger_riposte': dict(unit='ghislaine', impact=impact(
        'The roaring head of a tiger made of orange-gold fire lunges forward from the left and bursts into '
        'flames in the center.', 440, .7, .55, seed=9616)),
    'ult_ghislaine': dict(unit='ghislaine', impact=impact(
        'A colossal crescent slash of orange-gold fire with tiger-claw streaks tears across the center, then a huge '
        'burst of flame and embers.', 520, .8, .55, seed=9617)),
    'ult_ghislaine_oath': dict(unit='ghislaine', impact=aura(
        'A great wall of golden fire rises from the bottom as a protective ward, embers drifting upward, then it '
        'settles into a warm glow.', 380, .9, .25, seed=9618)),
    'aw_ghislaine': dict(unit='ghislaine', impact=aura(
        'A spectral white tiger made of golden fire rises in the center and roars, flames swirling around it, then '
        'it fades into embers.', 380, .9, .45, at='actor', seed=9619)),

    # ---------------------------------------------------------------- Daisy (melee, fire + petals)
    'da_bonfire_brand': dict(unit='daisy', impact=impact(
        'A blazing fire sigil brands itself in the center with a swirl of flame and a bright burst, then fades.',
        380, .6, .55, seed=9621)),
    'da_wand_sweep': dict(unit='daisy', impact=impact(
        'A ring of flames and red hibiscus petals sweeps around the center in a circle, then scatters and fades.',
        380, .6, .45, seed=9622)),
    'da_ember_thrust': dict(unit='daisy', impact=impact(
        'A drilling spiral of embers bursts in the center into a fiery explosion with sparks, then fades.',
        400, .6, .55, seed=9623)),
    'da_matriarch_pyre': dict(unit='daisy', impact=impact(
        'A towering pillar of orange flame erupts from the bottom center with pink embers swirling, then burns out.',
        360, .8, .05, sheet_size=(704, 960), seed=9624)),
    'da_spearflame_rush': dict(unit='daisy', travel=bolt(
        'A streaking comet of orange fire with a long flaming trail behind it.', 220, seed=9625), impact=impact(
        'A fiery explosion bursts in the center with flying embers and a shockwave, then fades.', 420, .6, .55,
        seed=9626)),
    'da_burning_circle': dict(unit='daisy', impact=impact(
        'A circle of fire erupts on the ground at the bottom center, flames flaring high around the edge, then it '
        'burns out.', 380, .8, .05, under=True, seed=9627)),
    'ult_daisy': dict(unit='daisy', impact=impact(
        'A volcanic burst of flame erupts upward from the bottom center with flying embers, then dies down.' + TIGHT,
        460, .9, .2, seed=9728)),
    'ult_daisy_inferno': dict(unit='daisy', impact=impact(
        'A towering pillar of flame erupts from the bottom center and a roaring tiger made of fire leaps out of it, '
        'then it all bursts into embers.', 480, .9, .1, sheet_size=(704, 960), seed=9629)),
    'aw_daisy': dict(unit='daisy', impact=aura(
        'A towering wildfire of pink and orange flame bursts up from the bottom center, petals and embers flying, '
        'then it fades.', 380, .9, .3, at='actor', seed=9630)),

    # ---------------------------------------------------------------- Elara (ranged mage, lightning)
    'el_arc_bolt': dict(unit='elara',
        charge=charge('A small crackling ball of electric blue-white lightning sparks in the center.', 170, seed=9631),
        travel=beam('A jagged crackling arc of electric blue-white lightning, flickering.', 60, seed=9632),
        impact=impact('A burst of electric blue-white lightning: a bright flash with forking arcs crackling outward, '
                      'then fading.', 340, .5, .55, seed=9633)),
    'el_piercing_shot': dict(unit='elara',
        charge=charge('A glowing blue rune circle of magic symbols forms in the center, seen from the side as a '
                      'vertical ring, spinning, with a bright white spark at its middle.', 230, seed=9634),
        travel=beam('A single straight blinding spear of blue-white lightning with a white-hot core and crackling '
                    'edges.', 80, seed=9635),
        impact=impact('A piercing lightning impact: a blinding blue-white flash and a shockwave ring with sparks '
                      'shooting outward, then fading.', 380, .5, .55, seed=9636)),
    'el_forest_scout': dict(unit='elara', impact=aura(
        'Glowing white paper birds fly out from the center and scatter upward, leaving trails of sparkles, then '
        'fade.', 360, .9, .55, at='actor', seed=9637)),
    'el_sapping_volley': dict(unit='elara',
        travel=bolt('A single small violet lightning dart with a crackling purple trail.', 110, count=4, stagger=.05,
                    seed=9638),
        impact=impact('A small violet electric burst with draining purple wisps curling away, then fading.', 280, .45,
                      .55, seed=9639)),
    'el_artillery_barrage': dict(unit='elara', impact=impact(
        'A bolt of blue-white lightning strikes straight down from the top of the frame into the bottom center, '
        'with a glowing rune circle flashing on the ground and crackling sparks.', 360, .6, .05,
        sheet_size=(704, 960), seed=9640)),
    'el_storm_tally': dict(unit='elara', impact=impact(
        'A small dark storm cloud crackles at the top of the frame and a blue lightning bolt strikes down to the '
        'bottom center, then the cloud fades.', 320, .6, .05, sheet_size=(704, 960), seed=9641)),
    'default_elara': dict(unit='elara',
        travel=bolt('A small spark of electric blue light with a short crackling tail.', 90, seed=9642),
        impact=impact('A small spark burst of electric blue light, then fading.', 220, .4, .55, seed=9643)),
    'ult_elara': dict(unit='elara', impact=impact(
        'Many blue-white lightning bolts rain down from the top of the frame onto the bottom center with glowing '
        'rune circles flashing, then fade.', 420, .9, .05, sheet_size=(704, 960), seed=9644)),
    'ult_elara_convergence': dict(unit='elara',
        travel=beam('A colossal beam of blue-white lightning passing through a row of glowing rune circles.', 130,
                    life=.45, seed=9645),
        impact=impact('A colossal blue-white lightning explosion with a blinding flash and shockwave rings, then '
                      'fading sparks.', 520, .8, .55, seed=9646)),
    'aw_elara': dict(unit='elara', impact=aura(
        'Shining blue rune script swirls upward in a ring from the bottom center, sparkles rising, then fades.',
        360, .9, .3, seed=9647)),

    # ---------------------------------------------------------------- Helda (support, hearth light + frost)
    'he_hearthfire_mend': dict(unit='helda',
        travel=bolt('A floating ribbon of warm golden hearth light.', 140, arc=60, seed=9651),
        impact=aura('Warm golden healing light blooms in the center with sparkles rising upward, then fades.', 320,
                    .7, .5, at='target', seed=9652)),
    'he_chilling_touch': dict(unit='helda',
        travel=bolt('A spray of frost and small ice crystals with a cold mist trail.', 170, seed=9653),
        impact=impact('A compact starburst of sharp pale-blue ice crystal shards bursting outward from one point, then '
                      'the shards scatter and fade.', 320, .5, .55, seed=9664)),
    'he_frosted_ward': dict(unit='helda', impact=aura(
        'Shimmering shields of frosted ice rise up in a ring around the center, glinting, then fade to sparkles.',
        340, .8, .35, seed=9655)),
    'he_reserve_tonic': dict(unit='helda',
        travel=bolt('A small glass bottle of bubbling glowing orange tonic, sparkling.', 110, arc=90, seed=9656),
        impact=aura('A pop of orange sparkles and bubbles rising, then fading.', 260, .5, .5, at='target',
                    seed=9657)),
    'he_lend_strength': dict(unit='helda',
        travel=bolt('A pulsing wave of warm orange energy.', 160, seed=9658),
        impact=aura('A small burst of warm orange energy sparks rising upward, then fading.' + TIGHT, 300, .6, .5,
                    at='target', seed=9759)),
    'he_bulwark_brew': dict(unit='helda', impact=aura(
        'A sturdy dome of golden light forms around the center, glowing and rippling, then fades.', 360, .8, .35,
        seed=9660)),
    'default_helda': dict(unit='helda',
        travel=bolt('A rolling wave of frost along the ground with ice shards.', 160, seed=9661),
        impact=impact('A small burst of frost and ice shards, then fading.' + TIGHT, 260, .45, .45, seed=9762)),
    'ult_helda': dict(unit='helda', impact=aura(
        'A warm golden hearth glow spreads from the center with healing motes drifting upward, then fades.', 380,
        .9, .35, seed=9663)),
    'ult_helda_sanctuary': dict(unit='helda', impact=aura(
        'A sanctuary of ice pillars and a crystal dome rise from the bottom around the center in cold blue light, '
        'then fade to sparkles.', 380, .9, .25, seed=9664)),
    'aw_helda': dict(unit='helda', impact=aura(
        'A warm golden hearth glow swirls up from the bottom center with drifting healing motes, then fades.', 360,
        .9, .3, seed=9665)),

    # ---------------------------------------------------------------- Clarity (support, holy light)
    'cy_radiant_palm': dict(unit='clarity',
        charge=charge('A glowing golden palm print of light with radiant rays in the center.', 180, seed=9671),
        travel=bolt('A shockwave of golden light shaped like an open palm.', 170, seed=9672),
        impact=impact('A burst of golden light exploding outward with rays and sparks, then fading.', 380, .55, .55,
                      seed=9673)),
    'cy_barkeep_tonic': dict(unit='clarity',
        travel=bolt('A small cup of glowing golden tonic, sparkling.', 110, arc=80, seed=9674),
        impact=aura('Golden sparkles and soft light rise upward from the center, then fade.', 260, .6, .5,
                    at='target', seed=9675)),
    'cy_flare': dict(unit='clarity', impact=impact(
        'A blazing flare of holy light bursts at the top of the frame and rains golden sparks down to the bottom, '
        'then fades.', 360, .7, .1, sheet_size=(704, 960), seed=9676)),
    'cy_uplift': dict(unit='clarity', impact=aura(
        'Golden rings of light rise upward around the center with sparkles lifting into the air, then fade.', 320,
        .8, .3, seed=9677)),
    'cy_smite': dict(unit='clarity', impact=impact(
        'A column of holy golden light smites down from the top of the frame into the bottom center with a '
        'burst and a shockwave, then fades.', 380, .7, .05, sheet_size=(704, 960), seed=9678)),
    'cy_rejuvenating_draught': dict(unit='clarity', impact=aura(
        'Soft green and gold light swirls upward in a spiral from the bottom center, sparkles rising, then '
        'fades.', 320, .8, .3, at='actor', seed=9679)),
    'default_clarity': dict(unit='clarity',
        travel=bolt('A spinning golden crystal kunai with a short light trail.', 110, seed=9680),
        impact=impact('A small burst of golden light sparks, then fading.', 220, .4, .55, seed=9681)),
    'ult_clarity': dict(unit='clarity',
        travel=beam('A blinding beam of golden light with expanding shockwave rings along it.', 120, life=.45,
                    seed=9682),
        impact=impact('A colossal golden light explosion with a blinding flash and shockwave rings, rubble '
                      'sparks flying, then fading.', 520, .8, .55, seed=9683)),
    'ult_clarity_feast': dict(unit='clarity', impact=aura(
        'Radiant golden rings and floating light motes pour down from above around the center, warm rays, then '
        'fade.', 380, .9, .35, seed=9684)),
    'aw_clarity': dict(unit='clarity', impact=aura(
        'A pillar of warm golden light descends onto the center with healing rings spreading outward, then '
        'fades.', 360, .9, .3, at='actor', seed=9685)),
}
