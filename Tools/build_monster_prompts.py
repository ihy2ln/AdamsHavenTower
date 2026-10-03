"""Text-to-image prompt packs for every bestiary monster (Assets/Resources/AdamsHaven/Bestiary/bestiary.json).

Per form, 8 images in dependency order (generate the front battle view first and attach it to the rest):
  1 battle_front   2 battle_left   3 battle_back      transparent, rig references for 2D/3D battle models
  4 chibi_front    5 chibi_left    6 chibi_back       transparent chibi versions of the battle views
  7 playing_card   card illustration (art only; frame, name and rank are authored overlays)
  8 action_card    4-action move-list card art (empty move panels; text comes from actions.json)

Writes MonsterPrompts/ (README, 00_SHARED_STYLE.md, <family>/<form>/PROMPTS.md + actions.json, prompt_queue.jsonl/.csv).
Style and GKOM rank rules follow Game Assets/monsters/gkom_testers_v3 and CharacterPrompts/00_SHARED_STYLE.md.

Usage: python Tools/build_monster_prompts.py      (run Tools/build_bestiary.py first when the bestiary changes)
"""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BESTIARY = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'Bestiary' / 'bestiary.json'
OUT = ROOT / 'MonsterPrompts'
RANKS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']

MASTER = ('High-end Korean fantasy RPG and gacha illustration, polished hand-painted anime-influenced dark fantasy rendering, '
          'crisp clean linework, rich dimensional cel shading, luminous material highlights, detailed creature anatomy with '
          'integrated sharply faceted GKOM gemstone growth and luminous mineral fractures, cohesive Adams Haven world palette '
          'of dark weathered timber, slate and stone with moonlit blue-silver and violet accents.')
NEGATIVE = ('No text, no letters, no numbers, no rank letters, no watermark, no logo, no UI, no extra or missing limbs, '
            'no fused claws or fingers, no cropped body parts, no photorealism, no 3D render look, no gore.')
TRANSPARENT = ('Genuine RGBA transparency: alpha-zero background, no scenery, no ground, no cast shadow, no border. '
               'The entire creature, including every horn, ear, wing tip, tail tip, claw and weapon, is inside the frame '
               'with a generous transparent margin on all sides.')

# GKOM rank language (Weaververse codex + gkom_crystal_rank_rules.json): crystal coverage and core orb grow with rank.
COVERAGE = [
    'only a few small GKOM crystal growths (two or three finger-length points); most of its natural skin, hide or surface is exposed',
    'scattered small GKOM crystal clusters on shoulders and back; most of the body still natural',
    'GKOM crystals crust its joints and run along the spine; about a quarter of the body crystallised',
    'GKOM crystal plates over shoulders, back and limbs; about a third of the body crystallised',
    'half the body armoured in GKOM crystal, natural patches between the plates',
    'most of the body sheathed in GKOM crystal, natural surface only at the seams',
    'a near-complete GKOM crystal hide with thin bare seams',
    'sealed in GKOM crystal except its eyes, open mouth interior and core',
    'its entire outer body sealed in continuous GKOM crystal, including head, limbs, underside and tail; only eyes, mouth interior and core remain readable',
]
CORE = [
    ('marble-sized', 'murky grey, dim faint pulse'), ('golf-ball-sized', 'cloudy amber, faint glow'),
    ('tennis-ball-sized', 'dark amber with visible cracks'), ('fist-sized', 'deep red with internal light'),
    ('softball-sized', 'dark crimson with a steady pulse'), ('grapefruit-sized', 'near-black with a liquid shimmer'),
    ('volleyball-sized', 'black-red, radiating heat'), ('basketball-sized', 'void-black, warping light around it'),
    ('beach-ball-sized', 'obsidian-perfect, pulling loose debris toward it'),
]
FX = ['tiny subtle', 'small subtle', 'modest', 'clear', 'strong', 'vivid sweeping', 'dramatic sweeping',
      'elaborate layered', 'epic screen-filling']
ELEMENT = {
    'Fire': ('ember red, orange and gold', 'embers, flame ribbons and heat shimmer'),
    'Wind': ('teal, pale green and silver', 'spiralling leaves, gust lines and floating feathers'),
    'Earth': ('amber, moss green and slate', 'stone shards, crystal dust and drifting petals'),
    'Lightning': ('electric yellow, cyan and indigo', 'branching arcs, static sparks and crackling halos'),
    'Water': ('aqua, deep blue and pearl', 'ribbons of water, bubbles and moonlit spray'),
    'Light': ('warm gold, white and soft rose', 'sun motes, feather-like light and halo rings'),
    'Dark': ('violet, black and magenta', 'shadow smoke, star specks and dark sigils'),
    'Neutral': ('weathered brown, iron grey and brass', 'dust motes and glinting coins'),
}

# Family anatomy and GKOM mineral (art direction; the bestiary holds names, ranks and moves).
FAMILY = {
    'goblinkin': ('wiry olive-grey goblinoids with long pointed ears, black GKOM veins and ragged scavenged cloth', 'smoky-grey quartz', False),
    'shardlings': ('living saplings and trees of bark and roots with crystal buds and geode hollows', 'pale green quartz', False),
    'flintjaws': ('armoured burrowing insects with flint-like mandibles and segmented chitin', 'ember-orange flint crystal', True),
    'amberhides': ('stocky horned grazing beasts like aurochs with amber plates growing through their hide', 'honey amber', True),
    'thorncrystal_stalkers': ('lean panther-like hunting cats with a ridge of crystal thorns along the back and a faint afterimage', 'teal thorn crystal', True),
    'quartzbacks': ('wolves with frost-quartz spines along the back and pale icy fur', 'frost-white quartz', True),
    'glasswings': ('insects with wings of beaten green glass and needle stingers', 'green bottle-glass crystal', False),
    'obsidian_talons': ('raptor birds whose feathers harden into knapped obsidian blades', 'glossy black obsidian with ember edges', True),
    'prism_wardens': ('floating faceted light-spirits and prism sentinels built of rotating crystal plates', 'green-gold prism crystal', False),
    'cobalt_burrowers': ('armoured segmented worms and grubs with ringed crystal teeth and cobalt shell plates', 'deep cobalt-blue crystal', True),
    'moonstone_ravagers': ('hulking bear-like predators with owlish facial discs and moonstone claws', 'milky moonstone', True),
    'stormglass_drakes': ('dragons and wyverns with storm-glass scales crackling with lightning', 'storm-blue glass crystal', True),
    'eclipse_golems': ('constructs of black rubble and crystal assembled around a dark eclipsed orb', 'black eclipse crystal with a violet rim', False),
    'gloam_oozes': ('translucent jellies and gel cubes with half-dissolved crystal shards drifting inside', 'murky violet gel crystal', True),
    'husks': ('undead male husks of GKOM victims, gaunt grey flesh, black veins, torn clothing, the orb where the heart was', 'bone-white crystal', False),
    'veinweb_spiders': ('giant spiders with crystal-tipped legs and black vein-like silk', 'black vein crystal', True),
    'glint_serpents': ('serpents with jewelled scales and petrifying eyes; higher forms have humanoid naga torsos', 'emerald glint crystal', True),
    'opal_sirens': ('winged women-shaped sirens and harpies with opal-feathered wings and taloned feet', 'blood-red opal', False),
    'cathedral_hydras': ('many-headed swamp hydras whose necks rise like crystal spires', 'green cathedral-glass crystal', True),
    'geode_knights': ('suits of plate armour with no body inside, filled and fused with geode crystal light', 'amethyst geode crystal', False),
    'maw_behemoths': ('massive horned quadruped calamity beasts with huge jaws and heavy clawed paws', 'obsidian crystal', True),
    'crag_trolls': ('huge stooped ogres and trolls with craggy rock-like skin and crude clubs', 'iron-grey crag crystal', False),
    'hoard_mimics': ('mimics disguised as crates, chests and vault doors with toothy lids and sticky tongues', 'gold-flecked citrine crystal', True),
    'cinder_motes': ('living sparks and fire elementals of black flame around a red orb', 'ember-red cinder crystal', False),
    'tide_wisps': ('living water: wisps, water maidens and serpents of black lake water', 'pearl-white tide crystal', False),
    'static_sprites': ('crackling spark sprites and storm spirits stitched with lightning', 'electric-yellow static crystal', False),
    'rotcaps': ('waddling mushrooms and fungus-overgrown shamblers with spore gills', 'sickly lilac spore crystal', False),
    'ruin_gargoyles': ('stone gargoyles and imps with bat wings, horns and pebbled stone skin', 'slate-grey grit crystal', False),
    'gloomshades': ('ghostly translucent shades, banshees and wraiths trailing shadow smoke', 'violet shade crystal', False),
    'spinetail_chimeras': ('chimera beasts: lynx and lion bodies with scorpion-like crystal-spined tails', 'blood-orange spine crystal', True),
    'whisperers': ('hooded human GKOM-A cultists with black-veined hands, cracked-marble skin and red-tinged energy', 'crimson crystal', False),
}

# One-line look per form: what makes this form different from its family siblings.
LOOK = {
    'ashvein_goblin_scavenger': 'small wiry adult goblin holding a plain flint shiv, ragged cloth, left-hip scavenger pouch',
    'ashvein_hobgoblin': 'taller disciplined hobgoblin in mismatched scavenged plate with a round shield and short spear',
    'ashvein_bugbrute': 'huge hairy hunched bugbear-like goblinoid with long arms and a spiked crystal-studded club',
    'ashvein_warlord': 'towering goblin warlord in a crown of grey quartz and a war banner on its back, wielding a great cleaver',
    'shardling_sprout': 'knee-high crystal-budded sapling waddling on root feet',
    'shardling_thicket': 'walking hedge of glassy thorns and tangled roots',
    'geode_treant': 'ancient oak treant split open to show a cavern of crystal inside its trunk',
    'flintjaw_skitterer': 'dog-sized beetle-like skitterer with sparking flint jaws',
    'flintjaw_ripper': 'soldier-caste insect with mandibles like flint axes and heavier armour',
    'flintjaw_hive_queen': 'bloated hive queen with a glowing crystal egg sac and sparking larvae around her',
    'amberhide_grazer': 'placid deer-like grazer with amber plates along its back',
    'amberhide_bull': 'massive bull with amber horns as long as spears',
    'amberhide_matriarch': 'great herd mother with a ridge of amber crystal down her back and branching amber horns',
    'thorncrystal_stalker': 'sleek panther with a ridge of crystal thorns, its outline slightly doubled',
    'thornshade_prowler': 'larger shadowy panther accompanied by a translucent reflection of itself',
    'mirage_panther': 'crystal-sheathed great cat trailing a fan of false afterimages',
    'quartzback_pup': 'young wolf pup with soft new quartz spines',
    'quartzback_hound': 'lean wolf with frost-quartz spines and rime on its paws',
    'quartzback_direwolf': 'pony-sized dire wolf with a mane of frozen quartz',
    'moonfang_alpha': 'colossal alpha wolf with crescent-moon quartz fangs, howling',
    'glasswing_mite': 'tiny glinting mite with glass wings',
    'glasswing_stinger': 'fist-sized wasp with a green glass stinger',
    'glasswing_hive_mother': 'walking crystal hive on many legs with glass wasps swarming from it',
    'obsidian_talon': 'hawk with feathers like knapped black glass',
    'obsidian_roc': 'enormous roc with obsidian-bladed wings spread wide',
    'ash_thunderbird': 'firestorm thunderbird with white-hot crystal plumage and lightning in its wake',
    'prism_wisp': 'small floating mote of green light inside a faceted prism shell',
    'viridian_prism_warden': 'faceted crystal sentinel with a healing green light at its centre',
    'prism_archon': 'tall floating archon made of a crown of rotating prisms around a core of light',
    'cobalt_grub': 'cart-sized cobalt larva with ringed mandibles',
    'cobalt_burrower': 'armoured worm with a cobalt shell and a drill-like head',
    'cobalt_wyrmworm': 'tunnel-wide wyrm-worm rearing up, maw ringed with crystal teeth',
    'moonstone_cub': 'playful bear cub with an owlish face and small moonstone claws',
    'moonstone_ravager': 'hulking owl-faced bear with moonstone claws and pale fur',
    'moonstone_ursine': 'ancient giant bear wearing a pale moonstone crown',
    'stormglass_drake': 'young drake crackling with static, storm-glass scales',
    'stormglass_wyvern': 'two-legged wyvern with lightning-veined storm-glass wings',
    'tempest_dragon': 'four-legged tempest dragon wreathed in a living thunderhead, crystal scales ringing',
    'shard_golem': 'rough golem of rubble held together by a pulsing orb',
    'eclipse_core_golem': 'titan of black stone and crystal around an eclipsed core',
    'heartrot_colossus': 'colossal rotting-wood-and-black-crystal colossus, roots and blight hanging from it',
    'gloam_ooze': 'low puddle-like ooze glinting with half-digested crystal',
    'crystal_jelly': 'wobbling translucent flan-like jelly with a red orb at its centre',
    'geode_cube': 'corridor-filling translucent cube with bones and crystal suspended inside',
    'abyssal_gel': 'towering black tide of gel swallowing the light around it',
    'hollow_husk': 'shambling male corpse with a grey orb in its open chest',
    'crystal_ghoul': 'fast crouching ghoul with crystal teeth and claws',
    'wight_knight': 'undead soldier in rusted oath-armour with a notched sword',
    'orb_lich': 'robed lich whose orb floats above an open ribcage, holding a crystal staff',
    'veinweb_spider': 'dog-sized spider with crystal-tipped legs',
    'crystalweb_broodmother': 'huge broodmother spider whose back is a nest of hatching crystal eggs',
    'silkqueen_arachne': 'arachne with an elegant female humanoid upper body on a giant crystal spider body, fully clothed in silk armour',
    'glint_adder': 'small snake with jewelled scales and bared fangs',
    'gazestone_basilisk': 'six-legged lizard basilisk with glowing petrifying eyes',
    'coilqueen_naga': 'naga priestess with a serpent lower body, ornate temple robes and a crystal crown',
    'worldcoil_serpent': 'colossal coiling world-serpent whose crystal coils look like mountain ridges',
    'opal_harpy': 'screeching harpy with opal-feathered wings and taloned feet, ragged cloth wraps',
    'blood_opal_siren': 'beautiful winged siren with a glowing blood-opal heart, flowing modest robes',
    'siren_matron': 'regal siren choir-mistress with vast opal wings and a pearl crown, elegant gown',
    'fen_hydra': 'three-headed swamp hydra',
    'marsh_hydra': 'five-headed marsh hydra with crystal heads regrowing from stumps',
    'verdant_cathedral_hydra': 'nine-headed hydra whose necks rise like the spires of a green-glass cathedral',
    'geode_squire': 'empty helmet and half-armour glowing from within',
    'geode_knight': 'full suit of plate grown together with quartz, longsword and kite shield',
    'crowned_geode_knight': 'kingly armoured knight with a crystal crown, cape and great sword',
    'maw_cub': 'horse-sized horned behemoth cub with oversized jaws',
    'maw_behemoth': 'massive horned behemoth with a roaring maw and heavy clawed forepaws',
    'obsidian_maw_behemoth': 'gargantuan behemoth sealed in obsidian crystal, gravity distortion around its core',
    'cragling_ogre': 'dim hungry ogre carrying half a tree as a club',
    'crag_troll': 'lanky stooped troll whose wounds are knit shut by crystal',
    'ironhide_troll_king': 'ancient troll king with ore-turned hide and a boulder-headed club',
    'crate_mimic': 'supply crate mimic with too many teeth and a sticky tongue',
    'chest_mimic': 'ornate treasure chest mimic with a gaping toothy lid and gold spilling out',
    'vault_devourer': 'whole vault door mimic on stubby legs, its door a giant fanged mouth',
    'cinder_mote': 'small grinning spark of black-and-red fire',
    'cinder_elemental': 'pillar of black fire with arms, around a red orb',
    'pyre_titan': 'giant of molten crystal and flame cracking the ground where it stands',
    'tide_wisp': 'drifting bubble of lake water with a grey orb inside',
    'undine': 'water maiden made of living water with crystal eyes, flowing water gown',
    'abyssal_tidewyrm': 'sea serpent of black water coiled around a crimson orb',
    'static_sprite': 'tiny crackling spark sprite with a mischievous face',
    'storm_elemental': 'whirling storm-cloud body stitched with crystal lightning',
    'thunderhead_djinn': 'muscular storm djinn rising from a thundercloud, bracers of crystal',
    'sporecap': 'waddling mushroom creature sneezing grey spores',
    'spore_shambler': 'fungus-overgrown husk still walking, mushrooms sprouting from its back',
    'rotbloom_matron': 'cottage-sized fungal mother-flower with drooping spore petals',
    'grit_imp': 'pebble-skinned grinning imp holding a handful of gravel',
    'ruin_gargoyle': 'classic stone gargoyle with bat wings, crouched and ready to pounce',
    'cathedral_gargoyle_lord': 'bell-tower-sized gargoyle lord with cathedral ornament carved into its stone',
    'gloomshade': 'wispy humanoid shadow moving against the light',
    'wailing_banshee': 'translucent banshee with streaming hair, mouth open in a scream',
    'void_wraith': 'tall tattered wraith around a tear in the air, crimson orb heart',
    'spinetail_lynx': 'lynx with a scorpion-like crystal tail',
    'manticore': 'lion-bodied manticore with a grinning face, bat wings and a tail full of crystal spines',
    'crystal_chimera': 'three-headed chimera of lion, goat and dragon fused in red crystal',
    'hollow_acolyte': 'robed initiate cultist with black-veined hands and a hood',
    'whisperer': 'cult leader in layered dark robes and a cowl, one hand raised as it whispers a command',
    'crimson_whisperer': 'cult queen in a red-crystal crown and elegant dark-crimson regalia, fully covered',
}


def rank_mid(form):
    return (form['minRank'] + form['maxRank']) // 2       # 1-based rank used for the art


def action_list(form, family):
    """Exactly 4 move-list actions: the form's moves (up to 4), then GKOM passives to fill."""
    acts = []
    for c in form['cards']:
        if len(acts) == 4:
            break
        if c['heal']:
            kind = 'Support'
        elif c['target'] == 'AllEnemies':
            kind = 'Area'
        elif c['target'] in ('Self', 'Ally', 'AllAllies'):
            kind = 'Guard' if c['target'] == 'Self' else 'Support'
        elif c['status'] in ('Stun', 'Slow', 'AttackDown', 'DefenseDown'):
            kind = 'Control'
        else:
            kind = 'Attack'
        acts.append(dict(id=c['id'], name=c['name'], type=kind, cost=c['ep'], description=describe(c)))
    if len(form['cards']) > 1 and len(acts) == 4:
        acts[-1]['type'] = 'Signature' if acts[-1]['type'] in ('Attack', 'Area') else acts[-1]['type']
    lo = rank_mid(form) - 1
    passives = [
        dict(id='gkom_core', name='GKOM Core', type='Passive', cost=0,
             description=f"Its {CORE[lo][0]} core orb ({CORE[lo][1]}) powers it; shatter the core and it falls."),
        dict(id='crystal_hide', name='Crystal Hide', type='Passive', cost=0,
             description=f"{family['material']} grows over its body as its rank rises."),
    ]
    acts += passives[:4 - len(acts)]
    return acts


def describe(c):
    who = {'Enemy': 'one foe', 'AllEnemies': 'every foe', 'Self': 'itself', 'Ally': 'an ally', 'AllAllies': 'all allies'}[c['target']]
    parts = []
    if c['power']:
        parts.append(f"{'Magic' if c['magic'] else 'Physical'} hit to {who} ({c['power']:g}x)")
    if c['heal']:
        parts.append(f"Heals {who} for {c['heal']:g}")
    if c['status']:
        status = {'DefenseUp': 'Defense up', 'AttackUp': 'Attack up', 'DefenseDown': 'Defense down', 'AttackDown': 'Attack down'}.get(c['status'], c['status'])
        dur = f" for {c['duration']} turns" if c['duration'] > 1 else (' for 1 turn' if c['duration'] == 1 else '')
        parts.append(f"{status}{'' if c['power'] or c['heal'] else ' on ' + who}{dur}")
    return '; '.join(parts) + '.'


def subject(form, family):
    anatomy, mineral, wide = FAMILY[family['id']]
    r = rank_mid(form) - 1
    colors, fx = ELEMENT.get(form['element'], ELEMENT['Neutral'])
    size = 'large, heavy and imposing' if form['large'] else 'compact and agile'
    return (f"{form['name']}, a {RANKS[r]}-rank GKOM variant from the {family['name']} family ({family['inspiration']} "
            f"archetype reimagined for Adams Haven): {LOOK[form['id']]}. Family anatomy: {anatomy}. Build: {size}. "
            f"GKOM rank cue: {COVERAGE[r]}; the crystals are {mineral}. A {CORE[r][0]} GKOM core orb, {CORE[r][1]}, "
            f"is embedded visibly in its chest or body centre. Element {form['element']}: {colors} accents, {FX[r]} "
            f"{fx}. Hostile, menacing expression. Mood: \"{form['flavor']}\"")


def prompts(form, family):
    wide = FAMILY[family['id']][2]
    battle_canvas = '1536x1024 landscape' if wide else '1024x1536 portrait'
    s = subject(form, family)
    pose = ('battle-ready stance, weight forward, limbs and tail clearly separated from the body so the joints read for '
            'rigging, mouth and claws ready')
    out = []
    out.append(('battle_front', battle_canvas, True, [],
                f"{MASTER} Use case: creature battle sprite reference for 2D/3D rigging. FRONT view, facing the camera, "
                f"{pose}. {s} {TRANSPARENT} {NEGATIVE}"))
    out.append(('battle_left', battle_canvas, True, ['battle_front'],
                f"{MASTER} Use case: creature battle sprite reference for 2D/3D rigging. Reference 1 is the authoritative FRONT "
                f"view: copy its exact identity, colours, crystal placement and core. LEFT-SIDE profile view, the creature's "
                f"nose facing image LEFT, same {pose}. {s} {TRANSPARENT} {NEGATIVE}"))
    out.append(('battle_back', battle_canvas, True, ['battle_front'],
                f"{MASTER} Use case: creature battle sprite reference for 2D/3D rigging. Reference 1 is the authoritative FRONT "
                f"view: copy its exact identity, colours and crystal placement. REAR view, the creature facing away from the "
                f"camera, same {pose}; the core orb is on the front and must not show through the back. {s} {TRANSPARENT} {NEGATIVE}"))
    chibi = ('CHIBI version: oversized head about a third of total height, compact rounded body, short stubby limbs, big '
             'expressive eyes, still hostile and mischievous; simplified but readable crystal clusters in the same places and '
             'the same core orb; rig-friendly: limbs, tail and wings clear of the joints')
    out.append(('chibi_front', '1024x1024 square', True, ['battle_front'],
                f"{MASTER} Use case: chibi battle sprite for Battle Mode. Reference 1 is the authoritative FRONT battle view: "
                f"keep its identity, colours, crystal placement and core. {chibi}. FRONT view facing the camera. {s} "
                f"{TRANSPARENT} {NEGATIVE}"))
    out.append(('chibi_left', '1024x1024 square', True, ['chibi_front', 'battle_left'],
                f"{MASTER} Use case: chibi battle sprite for Battle Mode. Reference 1 is the authoritative CHIBI FRONT view, "
                f"reference 2 the normal LEFT battle view for anatomy. {chibi}. LEFT-SIDE profile, nose facing image LEFT. "
                f"{s} {TRANSPARENT} {NEGATIVE}"))
    out.append(('chibi_back', '1024x1024 square', True, ['chibi_front', 'battle_back'],
                f"{MASTER} Use case: chibi battle sprite for Battle Mode. Reference 1 is the authoritative CHIBI FRONT view, "
                f"reference 2 the normal REAR battle view. {chibi}. REAR view facing away from the camera; no core orb on "
                f"the back. {s} {TRANSPARENT} {NEGATIVE}"))
    habitat = ', '.join(family['themes'][:3])
    out.append(('playing_card', '1024x1536 portrait (5:7 card)', False, ['battle_front'],
                f"{MASTER} Use case: collectible playing-card illustration. Reference 1 is the authoritative FRONT battle "
                f"view: keep its exact identity, colours, crystal placement and core. Full-bleed dramatic hero shot of the "
                f"creature in a dynamic {('attacking' if form['role'] in ('Bruiser', 'Skirmisher') else 'spell-casting')} pose "
                f"in its Silverwood habitat ({habitat} places), atmospheric depth, {ELEMENT.get(form['element'], ELEMENT['Neutral'])[1]} "
                f"filling the air. Keep the top 12% and bottom 20% of the image calm and darker for an authored frame, "
                f"name plate and rank badge. {s} No frame, no border, no card text drawn: those are overlays. {NEGATIVE}"))
    acts = action_list(form, family)
    names = '; '.join(f"{a['type']}" for a in acts)
    out.append(('action_card', '1024x1536 portrait (5:7 card)', False, ['battle_front'],
                f"{MASTER} Use case: move-list card art for the creature's 4 actions. Reference 1 is the authoritative FRONT "
                f"battle view. Top 38% of the card: a cropped bust-and-claws vignette of the creature in a dark crystalline "
                f"ornamental arch. Below it, FOUR equal, empty horizontal move panels stacked vertically, each a dark smoky "
                f"glass slab with a faceted {FAMILY[family['id']][1]} rim and a small empty round icon socket on its left; the "
                f"panels are for authored text and must stay blank. Icon sockets hint at the move types ({names}) with tiny "
                f"abstract element glyphs only. Dark ornamental crystalline card frame in {ELEMENT.get(form['element'], ELEMENT['Neutral'])[0]} "
                f"accents. {s} {NEGATIVE}"))
    return out, acts


def write():
    data = json.loads(BESTIARY.read_text(encoding='utf-8'))
    OUT.mkdir(exist_ok=True)
    queue = []
    for family in data['families']:
        for form in family['forms']:
            items, acts = prompts(form, family)
            folder = OUT / family['id'] / form['id']
            folder.mkdir(parents=True, exist_ok=True)
            r = RANKS[rank_mid(form) - 1]
            (folder / 'actions.json').write_text(json.dumps(dict(
                id=form['id'], name=form['name'], family=family['name'], rank=r,
                rankRange=f"{RANKS[form['minRank'] - 1]}-{RANKS[form['maxRank'] - 1]}", element=form['element'],
                role=form['role'], flavor=form['flavor'], actions=acts), indent=2), encoding='utf-8')
            lines = [f"# {form['name']}  ·  {family['name']}", '',
                     f"Rank {RANKS[form['minRank'] - 1]}-{RANKS[form['maxRank'] - 1]} (art at {r})  ·  {form['element']} "
                     f"{form['role']}  ·  bestiary id `{form['id']}`", '', f"> {form['flavor']}", '',
                     '## Move-list card actions (authored text, see actions.json)', '']
            lines += [f"{i + 1}. **{a['name']}** ({a['type']}{', ' + str(a['cost']) + ' EP' if a['cost'] else ''}): {a['description']}"
                      for i, a in enumerate(acts)]
            lines += ['', '## Prompts (generate in this order)', '']
            for n, (name, canvas, transparent, refs, text) in enumerate(items, 1):
                lines += [f"### {n}. {name}.png", '', f"- Canvas: {canvas}", f"- Transparent background: {'yes' if transparent else 'no'}",
                          f"- Attach: {', '.join(r_ + '.png' for r_ in refs) if refs else 'nothing (identity master)'}", '',
                          '```text', text, '```', '']
                queue.append(dict(family=family['id'], form=form['id'], name=form['name'], rank=r, step=n, image=name,
                                  file=f"{family['id']}/{form['id']}/{name}.png", canvas=canvas, transparent=transparent,
                                  references=[f"{family['id']}/{form['id']}/{x}.png" for x in refs], prompt=text))
            (folder / 'PROMPTS.md').write_text('\n'.join(lines), encoding='utf-8')
    with open(OUT / 'prompt_queue.jsonl', 'w', encoding='utf-8') as f:
        for q in queue:
            f.write(json.dumps(q) + '\n')
    with open(OUT / 'prompt_queue.csv', 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(queue[0].keys()))
        w.writeheader()
        for q in queue:
            w.writerow(dict(q, references='|'.join(q['references'])))
    write_docs(data, len(queue))
    return data, queue


def write_docs(data, count):
    forms = sum(len(f['forms']) for f in data['families'])
    (OUT / '00_SHARED_STYLE.md').write_text('\n'.join([
        '# Shared style and rules for the bestiary prompt packs', '',
        'Applies to every GKOM monster form. Each `PROMPTS.md` builds on these rules; the text is already merged into each prompt.', '',
        '## Hard rules', '',
        '- **No text in images.** Names, rank badges, move names and descriptions are authored overlays (`actions.json`).',
        '- **Identity first.** Generate `battle_front` first and attach it to every later step; chibi side/back views also attach `chibi_front`.',
        '- **Transparent assets** (battle and chibi views) need real alpha: no ground, no shadow, nothing cropped.',
        '- **Rig-friendly.** Limbs, tails, wings and weapons stay clear of the joints; left view faces image LEFT; the core orb is on the front only.',
        '- **No gore.** Husks are grey and gaunt, not bloody. Humanoid forms (sirens, naga, arachne, whisperers) are fully clothed adults.',
        '- Log each accepted output (prompt, references, canvas) next to the images, as the GKOM tester batch does.', '',
        '## Master style line (starts every prompt)', '', f'> {MASTER}', '',
        '## Negative block (ends every prompt)', '', f'> {NEGATIVE}', '',
        '## GKOM rank language (crystal coverage and core orb grow with rank)', '',
        '| Rank | Crystal coverage | Core orb | FX |', '| --- | --- | --- | --- |'] +
        [f'| {RANKS[i]} | {COVERAGE[i]} | {CORE[i][0]}, {CORE[i][1]} | {FX[i]} |' for i in range(9)] +
        ['', 'A form whose range spans several ranks is drawn at the middle of its range.', '',
         '## Element language', '', '| Element | Colours | FX motifs |', '| --- | --- | --- |'] +
        [f'| {k} | {v[0]} | {v[1]} |' for k, v in ELEMENT.items()]) + '\n', encoding='utf-8')
    (OUT / 'README.md').write_text('\n'.join([
        '# Bestiary image prompts', '',
        f'Text-to-image prompts for all {forms} forms of the {len(data["families"])} bestiary families ({count} prompts). '
        'Generated by `Tools/build_monster_prompts.py` from `Assets/Resources/AdamsHaven/Bestiary/bestiary.json`; edit the '
        'generator (art direction) or `Tools/build_bestiary.py` (names, ranks, moves), then re-run it.', '',
        '## Per form: 8 images', '',
        '| # | File | What | Canvas | Alpha |', '| --- | --- | --- | --- | --- |',
        '| 1 | battle_front.png | Battle pose, front (identity master) | portrait, or landscape for big quadrupeds | yes |',
        '| 2 | battle_left.png | Battle pose, left side | same | yes |',
        '| 3 | battle_back.png | Battle pose, rear | same | yes |',
        '| 4 | chibi_front.png | Chibi battle sprite, front | 1024 square | yes |',
        '| 5 | chibi_left.png | Chibi, left side | 1024 square | yes |',
        '| 6 | chibi_back.png | Chibi, rear | 1024 square | yes |',
        '| 7 | playing_card.png | Playing-card illustration (frame and text are overlays) | 5:7 portrait | no |',
        '| 8 | action_card.png | 4-action move-list card art with blank panels | 5:7 portrait | no |', '',
        'The 4 actions for each move-list card are in `<family>/<form>/actions.json`: the form\'s moves from the bestiary '
        '(type, EP cost and a short description), padded with GKOM passives (GKOM Core, Crystal Hide) when a form has fewer '
        'than four moves. Passives are card flavour, not in-game mechanics yet.', '',
        '## Files', '',
        '- `00_SHARED_STYLE.md`: rules, master style line, negative block, rank and element tables.',
        '- `<family>/<form>/PROMPTS.md`: the 8 prompts in order, with canvas and which images to attach.',
        '- `<family>/<form>/actions.json`: authored move-list text.',
        '- `prompt_queue.jsonl` / `.csv`: every prompt as one row (file, step, canvas, transparent, references, prompt) for batch tools.',
        '', 'The Ashvein Goblin Scavenger and Obsidian Maw Behemoth already have finished tester images in '
        '`S:/AI/Game/Game Assets/monsters/gkom_testers_v3`; attach those as identity references instead of regenerating step 1.']) + '\n',
        encoding='utf-8')


if __name__ == '__main__':
    data, queue = write()
    missing = [fm['id'] for f in data['families'] for fm in f['forms'] if fm['id'] not in LOOK]
    assert not missing, missing
    print(f"{len(queue)} prompts for {len(queue) // 8} forms -> {OUT.relative_to(ROOT)}")
