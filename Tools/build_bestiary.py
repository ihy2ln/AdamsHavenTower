"""Adams Haven bestiary: GKOM variant families, F-SSR ranks, rank-threshold evolution.

Single source of truth. Writes
  Assets/Resources/AdamsHaven/Bestiary/bestiary.json   (loaded by BattleBestiary.cs)
  BESTIARY.md                                         (design doc)

Lore (Weaververse codex: GKOM Variant Rank System, GKOM Drops; gkom_crystal_rank_rules.json):
  every variant carries a GKOM core orb; orb size and crystal coverage grow with rank. The lore's SR is the
  game's SS. A form evolves into the next form of its family when its rank reaches that form's minimum.

Usage: python Tools/build_bestiary.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_JSON = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'Bestiary' / 'bestiary.json'
OUT_MD = ROOT / 'BESTIARY.md'
FIELD_MODELS = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'FieldModels'

RANKS = ['F', 'E', 'D', 'C', 'B', 'A', 'S', 'SS', 'SSR']
R = {n: i + 1 for i, n in enumerate(RANKS)}
POPULATION = [40, 28, 15, 9, 4, 2, 1, 0.5, 0.1]            # % of variants at each rank (codex)
CORE = ['marble', 'golf ball', 'tennis ball', 'fist', 'softball', 'grapefruit', 'volleyball', 'basketball', 'beach ball']
CORE_LOOK = ['murky grey, dim pulse', 'cloudy amber, faint glow', 'dark amber, visible cracks', 'deep red, internal light',
             'dark crimson, steady pulse', 'near-black, liquid shimmer', 'black-red, radiates heat',
             'void-black, warps light', 'obsidian-perfect, pulls loose objects']
COVERAGE = ['a few small growths', 'scattered clusters', 'crusted joints and spine', 'plated shoulders, back and limbs',
            'half the body armoured in crystal', 'most of the body sheathed', 'crystal hide with bare seams',
            'sealed in crystal but for eyes and maw', 'entire outer body sealed in crystal']
THREAT = ['minor nuisance', 'local threat', 'district threat', 'city-district threat', 'city-level threat',
          'regional devastator', 'nation-level threat', 'continental threat', 'world-class horror']
THEMES = {'briar', 'crystal', 'marsh', 'edge', 'ruin', 'mine', 'cave', 'heartwood', 'keep', 'blight'}
ROLES = ['Bruiser', 'Skirmisher', 'Caster', 'Guardian']
ELEMENTS = ['Neutral', 'Fire', 'Water', 'Wind', 'Earth', 'Lightning', 'Light', 'Dark']
# Expedition depth -> natural rank (BattleBestiary.RankForDepth)
DEPTH_RANK = {1: 1, 2: 1, 3: 2, 4: 2, 5: 3, 6: 3, 7: 4, 8: 4, 9: 5, 10: 5, 11: 6, 12: 7, 13: 8}
REGIONS = [('silverbrook_edge', 'Brook Edge', 1, 3), ('rootside_camp', 'Rootside', 2, 3), ('shallow_ford', 'Ford', 2, 4),
           ('moon_shrine', 'Moon Shrine', 3, 5), ('old_bridge', 'Old Bridge', 3, 5), ('sunken_marsh', 'Marsh', 4, 6),
           ('watchpost_ruin', 'Watchpost', 5, 7), ('silverwood_gate', 'Wood Gate', 7, 8), ('silverwood_d1', 'Wood Edge', 8, 9),
           ('silverwood_d2', 'Lakes', 9, 10), ('silverwood_d3', 'Mountains', 10, 11), ('silverwood_d4', 'Ruins', 11, 12),
           ('silverwood_d5', 'Heart', 12, 13)]


# ---------------------------------------------------------------- card helpers
def atk(id, name, ep, power, status='', mag=0, dur=0, magic=False, target='Enemy', unlock=0):
    return dict(id='e_' + id, name=name, ep=ep, power=power, target=target, status=status, magnitude=mag,
                duration=dur, heal=0, magic=magic, unlock=unlock)


def aoe(id, name, ep, power, status='', mag=0, dur=0, magic=False, unlock=0):
    return atk(id, name, ep, power, status, mag, dur, magic, 'AllEnemies', unlock)


def buff(id, name, ep, status, mag, dur, target='Self', unlock=0):
    return dict(id='e_' + id, name=name, ep=ep, power=0, target=target, status=status, magnitude=mag,
                duration=dur, heal=0, magic=False, unlock=unlock)


def mend(id, name, ep, heal, target='Ally', unlock=0):
    return dict(id='e_' + id, name=name, ep=ep, power=0, target=target, status='', magnitude=0,
                duration=0, heal=heal, magic=True, unlock=unlock)


def form(id, name, lo, hi, flavor, cards=None, role=None, large=None, element=None):
    return dict(id=id, name=name, minRank=R[lo], maxRank=R[hi], flavor=flavor, cards=cards,
                role=role, large=large, element=element)


def fam(id, name, inspiration, element, role, large, themes, lore, kit, forms, material):
    return dict(id=id, name=name, inspiration=inspiration, element=element, role=role, large=large,
                themes=themes, lore=lore, kit=kit, forms=forms, material=material)


# Move count by tier (CM 10.3.5): a form has 2 moves at F-E, 3 at D-C, 4 at B-A and 5 at S and above (by its top
# rank), and in battle a monster uses the moves of its current rank's tier (BattleCatalog.EnemyCards). A form short
# of its count takes its family's locked moves first, then its element's moves, then its role's.
def move_count(rank):
    return 2 if rank <= 2 else 3 if rank <= 4 else 4 if rank <= 6 else 5


ELEMENT_MOVES = {
    'Neutral': [atk('crystal_rend', 'Crystal Rend', 2, 1.5), aoe('shard_burst', 'Shard Burst', 3, 1.0)],
    'Fire': [atk('cinder_lash', 'Cinder Lash', 2, 1.3, 'Burn', 6, 2), aoe('ember_wave', 'Ember Wave', 3, .9, 'Burn', 5, 2)],
    'Water': [atk('rime_bite', 'Rime Bite', 2, 1.3, 'Slow', .3, 1), aoe('frost_surge', 'Frost Surge', 3, .9, 'Slow', .2, 1)],
    'Wind': [atk('gale_slash', 'Gale Slash', 2, 1.45), aoe('cutting_gust', 'Cutting Gust', 3, 1.0, 'AttackDown', .15, 2)],
    'Earth': [atk('quake_stomp', 'Quake Stomp', 2, 1.4, 'Stun', 1, 1), buff('stone_skin', 'Stone Skin', 2, 'DefenseUp', .35, 2)],
    'Lightning': [atk('arc_strike', 'Arc Strike', 2, 1.4, magic=True), aoe('chain_spark', 'Chain Spark', 3, .95, magic=True)],
    'Light': [atk('glare_beam', 'Glare Beam', 2, 1.4, 'AttackDown', .2, 2, magic=True), mend('radiant_mend', 'Radiant Mend', 2, 26, 'AllAllies')],
    'Dark': [atk('umbral_bite', 'Umbral Bite', 2, 1.35, 'Poison', 5, 2), aoe('dread_pulse', 'Dread Pulse', 3, .9, 'DefenseDown', .15, 2, magic=True)],
}
ROLE_MOVES = {
    'Bruiser': [atk('crushing_blow', 'Crushing Blow', 3, 1.85), buff('frenzy', 'Frenzy', 1, 'AttackUp', .3, 2)],
    'Skirmisher': [atk('flurry', 'Flurry', 2, 1.5), atk('hamstring', 'Hamstring', 1, .9, 'Slow', .3, 1)],
    'Caster': [atk('hex_bolt', 'Hex Bolt', 2, 1.45, 'AttackDown', .2, 2, magic=True), aoe('crystal_storm', 'Crystal Storm', 3, 1.05, magic=True)],
    'Guardian': [buff('bulwark', 'Bulwark', 1, 'DefenseUp', .4, 2), atk('shield_bash', 'Shield Bash', 2, 1.3, 'Stun', 1, 1)],
}


def sized(cards, n, family, form):
    """The form's moves cut or padded to n."""
    out = [dict(c) for c in cards[:n]]
    for c in family['kit'] + ELEMENT_MOVES[form['element']] + ROLE_MOVES[form['role']]:
        if len(out) >= n:
            break
        if all(o['id'] != c['id'] for o in out):
            out.append(dict(c))
    return out


# Legacy species keep their exact cards (BattleCatalog.EnemyCards before the bestiary).
LEGACY_CARDS = {
    'shardling_sprout': [atk('bash', 'Crystal Bash', 1, 1.2), buff('root_guard', 'Root Guard', 1, 'DefenseUp', .3, 2)],
    'amberhide_grazer': [atk('bash', 'Crystal Bash', 1, 1.2), atk('amber_charge', 'Amber Charge', 2, 1.6),
                         buff('root_guard', 'Root Guard', 1, 'DefenseUp', .3, 2)],
    'flintjaw_skitterer': [atk('claw', 'Corrupted Claw', 1, 1.2), atk('ember_bite', 'Ember Bite', 1, .9, 'Burn', 6, 2)],
    'thorncrystal_stalker': [atk('pounce', 'Thorn Pounce', 1, 1.35), atk('wail', 'Hollow Wail', 2, .8, 'AttackDown', .2, 2)],
    'quartzback_hound': [atk('frost_fang', 'Frost Fang', 1, 1.25, 'Slow', .3, 1),
                         buff('pack_howl', 'Pack Howl', 1, 'AttackUp', .2, 2, 'AllAllies')],
    'glasswing_mite': [atk('glass_sting', 'Glass Sting', 1, .8, 'Poison', 5, 2), aoe('swarm', 'Glittering Swarm', 2, .6)],
    'obsidian_talon': [atk('rake', 'Talon Rake', 1, 1.3), atk('dive', 'Obsidian Dive', 2, 1.8)],
    'viridian_prism_warden': [atk('prism_bolt', 'Prism Bolt', 1, 1.2, magic=True), mend('verdant_mend', 'Verdant Mend', 1, 26),
                              buff('prism_ward', 'Prism Ward', 2, 'DefenseUp', .25, 2, 'AllAllies')],
    'cobalt_burrower': [atk('burrow_slam', 'Burrow Slam', 2, 1.3, 'Stun', 1, 1), buff('cobalt_shell', 'Cobalt Shell', 1, 'DefenseUp', .4, 2)],
    'moonstone_ravager': [atk('moon_maul', 'Moon Maul', 1, 1.5), buff('lunar_roar', 'Lunar Roar', 2, 'AttackUp', .25, 2, 'AllAllies')],
    'stormglass_wyvern': [aoe('storm_breath', 'Storm Breath', 2, .9, magic=True), atk('lightning_dive', 'Lightning Dive', 1, 1.6)],
    'eclipse_core_golem': [atk('cleave', 'Eclipse Crush', 2, 1.9), aoe('quake', 'Core Quake', 3, 1.15)],
}

# ---------------------------------------------------------------- the families
FAMILIES = [
    fam('goblinkin', 'Ashvein Goblinkin', 'D&D / Pathfinder goblin, hobgoblin and bugbear', 'Dark', 'Skirmisher', False,
        ['ruin', 'mine', 'edge', 'briar'],
        'Goblins whose dormant GKOM woke in the ash of burned camps. They hoard crystal chips like coin; the more '
        'crystal they swallow, the larger and more organised they become, until a warlord commands whole warbands.',
        [atk('crystal_shiv', 'Crystal Shiv', 1, 1.1), atk('shard_flick', 'Shard Flick', 1, .7, 'Slow', .2, 1),
         buff('war_drum', 'War Drum', 2, 'AttackUp', .2, 2, 'AllAllies', unlock=1), atk('brute_haul', 'Brute Haul', 2, 1.7, 'Stun', 1, 1, unlock=2),
         aoe('warband_volley', 'Warband Volley', 3, 1.0, 'DefenseDown', .15, 2, unlock=3)],
        [form('ashvein_goblin_scavenger', 'Ashvein Goblin Scavenger', 'F', 'E', 'Only a few smoky crystals betray the corruption beneath its skin.'),
         form('ashvein_hobgoblin', 'Ashvein Hobgoblin', 'D', 'C', 'Drilled and armoured, it fights in a shield line and never alone.', role='Guardian'),
         form('ashvein_bugbrute', 'Ashvein Bugbrute', 'B', 'B', 'A hulking ambusher that drags prey into the dark by the ankle.', role='Bruiser', large=True),
         form('ashvein_warlord', 'Ashvein Warlord', 'A', 'S', 'Crowned in grey quartz, it speaks for every goblin in the valley.', role='Bruiser', large=True)],
        'Ash-black goblin hide'),
    fam('shardlings', 'Shardlings', 'D&D myconid / treant, FF Mandragora', 'Earth', 'Guardian', False,
        ['briar', 'crystal', 'marsh', 'edge'],
        'Saplings that took root over a buried GKOM orb. Each season they grow a ring of crystal instead of bark, '
        'and the oldest stand as geode trees that guard whole groves.',
        [atk('bash', 'Crystal Bash', 1, 1.2), buff('root_guard', 'Root Guard', 1, 'DefenseUp', .3, 2),
         atk('bramble_lash', 'Bramble Lash', 2, 1.0, 'Slow', .3, 1, unlock=1), mend('sap_mend', 'Sap Mend', 2, 30, 'AllAllies', unlock=2)],
        [form('shardling_sprout', 'Shardling Sprout', 'F', 'E', 'A crystal-budded sapling that shuffles toward warmth.'),
         form('shardling_thicket', 'Shardling Thicket', 'D', 'C', 'A walking hedge of glassy thorns.'),
         form('geode_treant', 'Geode Treant', 'B', 'A', 'An ancient oak split open to show a cavern of crystal inside.', large=True)],
        'Crystal sap'),
    fam('flintjaws', 'Flintjaws', 'D&D ankheg, FF antlion and killer insects', 'Fire', 'Skirmisher', False,
        ['briar', 'ruin', 'mine', 'edge'],
        'Burrowing insects whose mandibles strike sparks. A hive grows around a queen fat with a cracked amber orb.',
        [atk('claw', 'Corrupted Claw', 1, 1.2), atk('ember_bite', 'Ember Bite', 1, .9, 'Burn', 6, 2),
         aoe('spark_spray', 'Spark Spray', 2, .7, 'Burn', 5, 2, unlock=1), buff('brood_call', 'Brood Call', 2, 'Haste', 1, 2, 'AllAllies', unlock=2)],
        [form('flintjaw_skitterer', 'Flintjaw Skitterer', 'F', 'E', 'Its jaws strike sparks off every stone it bites.'),
         form('flintjaw_ripper', 'Flintjaw Ripper', 'D', 'C', 'A soldier caste with mandibles like flint axes.'),
         form('flintjaw_hive_queen', 'Flintjaw Hive-Queen', 'B', 'A', 'A bloated queen that births burning larvae from her crystal sac.', role='Caster', large=True)],
        'Flint mandible'),
    fam('amberhides', 'Amberhides', 'D&D gorgon bull, FF Behemoth-lite grazers, aurochs', 'Earth', 'Bruiser', False,
        ['briar', 'cave', 'heartwood', 'edge'],
        'Grazing beasts that lick crystal salt-licks until amber plates grow through their hides. The matriarch leads '
        'the herd and tramples anything that threatens the calves.',
        [atk('bash', 'Crystal Bash', 1, 1.2), atk('amber_charge', 'Amber Charge', 2, 1.6), buff('root_guard', 'Root Guard', 1, 'DefenseUp', .3, 2),
         aoe('stampede', 'Stampede', 3, 1.1, 'Stun', 1, 1, unlock=1)],
        [form('amberhide_grazer', 'Amberhide Grazer', 'F', 'D', 'A placid grazer until something steps between it and the herd.'),
         form('amberhide_bull', 'Amberhide Bull', 'C', 'B', 'Amber horns as long as spears.', large=True),
         form('amberhide_matriarch', 'Amberhide Matriarch', 'A', 'S', 'The herd mother, her back a ridge of amber crystal.', large=True)],
        'Amber hide plate'),
    fam('thorncrystal_stalkers', 'Thorncrystal Stalkers', 'D&D displacer beast, phase cats, FF Coeurl-style hunters', 'Wind', 'Skirmisher', False,
        ['briar', 'keep', 'heartwood', 'edge'],
        'Panthers that bend light through the crystal thorns on their backs, appearing a pace from where they stand.',
        [atk('pounce', 'Thorn Pounce', 1, 1.35), atk('wail', 'Hollow Wail', 2, .8, 'AttackDown', .2, 2),
         buff('blur', 'Mirage Blur', 1, 'Haste', 1, 2, unlock=1), atk('phase_rend', 'Phase Rend', 2, 1.8, 'DefenseDown', .2, 2, unlock=2)],
        [form('thorncrystal_stalker', 'Thorncrystal Stalker', 'E', 'D', 'It is never quite where your eyes say it is.'),
         form('thornshade_prowler', 'Thornshade Prowler', 'C', 'B', 'Hunts in pairs, one real, one a reflection.'),
         form('mirage_panther', 'Mirage Panther', 'A', 'S', 'A crystal-sheathed cat that leaves a trail of false copies.')],
        'Mirage thorn'),
    fam('quartzbacks', 'Quartzbacks', 'D&D dire wolf / hell hound, FF wolves', 'Water', 'Bruiser', False,
        ['cave', 'mine', 'heartwood', 'edge'],
        'Wolves with frost-quartz spines. The pack shares one rhythm, and the alpha howls the whole pack into a frenzy.',
        [atk('frost_fang', 'Frost Fang', 1, 1.25, 'Slow', .3, 1), buff('pack_howl', 'Pack Howl', 1, 'AttackUp', .2, 2, 'AllAllies'),
         atk('rime_rend', 'Rime Rend', 2, 1.6, unlock=2), aoe('blizzard_howl', 'Blizzard Howl', 3, .9, 'Slow', .3, 1, unlock=3)],
        [form('quartzback_pup', 'Quartzback Pup', 'F', 'F', 'Its first quartz spines are still soft as ice.', large=False),
         form('quartzback_hound', 'Quartzback Hound', 'E', 'D', 'Frost rimes the ground where it runs.'),
         form('quartzback_direwolf', 'Quartzback Direwolf', 'C', 'B', 'Pony-sized, with a mane of frozen quartz.', large=True),
         form('moonfang_alpha', 'Moonfang Alpha', 'A', 'A', 'The pack leader; its howl freezes rivers.', large=True)],
        'Frost-quartz fang'),
    fam('glasswings', 'Glasswings', 'D&D stirge, FF killer bee / hornet', 'Wind', 'Skirmisher', False,
        ['marsh', 'crystal', 'cave', 'blight'],
        'Insects with wings of beaten glass. One mite is a nuisance; the hive that follows is a cloud of cutting light.',
        [atk('glass_sting', 'Glass Sting', 1, .8, 'Poison', 5, 2), aoe('swarm', 'Glittering Swarm', 2, .6),
         atk('drain_sting', 'Draining Sting', 1, 1.1, 'Poison', 8, 2, unlock=1), buff('hive_hum', 'Hive Hum', 2, 'Haste', 1, 2, 'AllAllies', unlock=2)],
        [form('glasswing_mite', 'Glasswing Mite', 'F', 'E', 'A buzzing glint at the edge of sight.'),
         form('glasswing_stinger', 'Glasswing Stinger', 'D', 'D', 'A fist-sized wasp with a stinger of green glass.'),
         form('glasswing_hive_mother', 'Glasswing Hive Mother', 'C', 'B', 'A crystal hive that walks on a hundred legs.', role='Caster', large=True)],
        'Glass wing'),
    fam('obsidian_talons', 'Obsidian Talons', 'D&D roc and harpy-hawks, FF Zu', 'Fire', 'Skirmisher', False,
        ['ruin', 'keep', 'blight', 'mine'],
        'Raptors whose feathers harden to obsidian blades. The greatest of them circle the old bridge on wings of fire.',
        [atk('rake', 'Talon Rake', 1, 1.3), atk('dive', 'Obsidian Dive', 2, 1.8),
         aoe('feather_storm', 'Feather Storm', 2, .9, 'Burn', 6, 2, unlock=1), atk('sky_drop', 'Sky Drop', 3, 2.2, 'Stun', 1, 1, unlock=2)],
        [form('obsidian_talon', 'Obsidian Talon', 'E', 'D', 'A hawk with feathers like knapped glass.'),
         form('obsidian_roc', 'Obsidian Roc', 'C', 'A', 'Its shadow alone sends herds running.', large=True),
         form('ash_thunderbird', 'Ash Thunderbird', 'S', 'S', 'A firestorm with wings, its crystal plumage glowing white-hot.', large=True)],
        'Obsidian feather'),
    fam('prism_wardens', 'Prism Wardens', 'D&D will-o\'-wisp and animated sentinels, FF Magic Pot style spirits', 'Light', 'Caster', False,
        ['ruin', 'heartwood', 'keep', 'marsh'],
        'Lights that once guarded the moon shrine. GKOM bent their purpose: they still guard, but now they guard the corruption.',
        [atk('prism_bolt', 'Prism Bolt', 1, 1.2, magic=True), mend('verdant_mend', 'Verdant Mend', 1, 26),
         buff('prism_ward', 'Prism Ward', 2, 'DefenseUp', .25, 2, 'AllAllies'), aoe('refraction', 'Refraction', 3, 1.0, 'AttackDown', .2, 2, True, unlock=2)],
        [form('prism_wisp', 'Prism Wisp', 'F', 'E', 'A floating mote of green light that leads travellers astray.'),
         form('viridian_prism_warden', 'Prism Warden', 'D', 'C', 'A faceted sentinel that heals its allies with light.'),
         form('prism_archon', 'Prism Archon', 'B', 'A', 'A crown of rotating prisms judging everything that enters.', large=True)],
        'Prism shard'),
    fam('cobalt_burrowers', 'Cobalt Burrowers', 'D&D bulette / purple worm, FF sandworm', 'Earth', 'Guardian', True,
        ['cave', 'mine', 'crystal', 'keep'],
        'Armoured worms that eat ore and excrete cobalt crystal. Miners know the tremor that means one is passing below.',
        [atk('burrow_slam', 'Burrow Slam', 2, 1.3, 'Stun', 1, 1), buff('cobalt_shell', 'Cobalt Shell', 1, 'DefenseUp', .4, 2),
         aoe('cave_in', 'Cave-In', 3, 1.0, unlock=1), atk('swallow', 'Swallow Whole', 3, 2.1, 'Stun', 1, 1, unlock=2)],
        [form('cobalt_grub', 'Cobalt Grub', 'E', 'E', 'A cart-sized larva gnawing through the mine walls.', large=False),
         form('cobalt_burrower', 'Cobalt Burrower', 'D', 'C', 'Its cobalt shell turns aside picks and blades.'),
         form('cobalt_wyrmworm', 'Cobalt Wyrmworm', 'B', 'S', 'A tunnel-wide worm whose maw is ringed in crystal teeth.')],
        'Cobalt shell'),
    fam('moonstone_ravagers', 'Moonstone Ravagers', 'D&D owlbear, FF beasts', 'Water', 'Bruiser', True,
        ['marsh', 'cave', 'blight', 'ruin'],
        'Bear-like predators that hunt by moonlight. Moonstone grows in their fur and drinks in the light around them.',
        [atk('moon_maul', 'Moon Maul', 1, 1.5), buff('lunar_roar', 'Lunar Roar', 2, 'AttackUp', .25, 2, 'AllAllies'),
         atk('crushing_hug', 'Crushing Hug', 2, 1.9, 'Stun', 1, 1, unlock=1), aoe('tidal_swipe', 'Tidal Swipe', 3, 1.1, 'Slow', .3, 1, unlock=2)],
        [form('moonstone_cub', 'Moonstone Cub', 'E', 'E', 'Playful, until its mother answers its cry.', large=False),
         form('moonstone_ravager', 'Moonstone Ravager', 'D', 'B', 'A hulking beast with moonstone claws.'),
         form('moonstone_ursine', 'Moonstone Ursine', 'A', 'A', 'An ancient bear with a pale crystal crown.')],
        'Moonstone claw'),
    fam('stormglass_drakes', 'Stormglass Drakes', 'D&D dragons and wyverns, FF Bahamut-line dragons', 'Lightning', 'Skirmisher', True,
        ['crystal', 'heartwood', 'ruin'],
        'Dragons that nest in lightning-struck crystal spires. A drake becomes a wyvern, and a wyvern that survives a '
        'century of storms becomes a tempest dragon.',
        [aoe('storm_breath', 'Storm Breath', 2, .9, magic=True), atk('lightning_dive', 'Lightning Dive', 1, 1.6),
         buff('static_scales', 'Static Scales', 1, 'DefenseUp', .3, 2, unlock=1), aoe('thunderclap', 'Thunderclap', 3, 1.2, 'Stun', 1, 1, True, unlock=2)],
        [form('stormglass_drake', 'Stormglass Drake', 'D', 'C', 'A young drake crackling with static.'),
         form('stormglass_wyvern', 'Stormglass Wyvern', 'B', 'A', 'It rides the storm front and dives with the lightning.'),
         form('tempest_dragon', 'Tempest Dragon', 'S', 'SSR', 'A living thunderhead, its crystal scales ringing with every bolt.')],
        'Stormglass scale'),
    fam('eclipse_golems', 'Eclipse Golems', 'D&D golems, FF Iron Giant', 'Dark', 'Bruiser', True,
        ['blight'],
        'Constructs of rubble and crystal that assemble themselves around a dark orb. The largest is the blight\'s heart made flesh.',
        [atk('cleave', 'Eclipse Crush', 2, 1.9), aoe('quake', 'Core Quake', 3, 1.15),
         buff('dark_core', 'Dark Core', 1, 'Shield', 40, 2, unlock=1), atk('orb_beam', 'Orb Beam', 3, 2.3, 'DefenseDown', .25, 2, True, unlock=2)],
        [form('shard_golem', 'Shard Golem', 'C', 'C', 'Rubble held together by a pulsing orb.'),
         form('eclipse_core_golem', 'Eclipse Core Golem', 'B', 'A', 'A titan of black stone around an eclipsed core.'),
         form('heartrot_colossus', 'Heartrot Colossus', 'S', 'SSR', 'The Silverwood\'s rotting heart, risen and walking.')],
        'Eclipse stone'),
    fam('gloam_oozes', 'Gloam Oozes', 'D&D gelatinous cube and puddings, FF Flan', 'Water', 'Guardian', False,
        ['cave', 'marsh', 'blight'],
        'Jellies that dissolve everything but crystal. Swallowed shards drift inside them, and the oldest are cubes of '
        'living geode that fill a corridor wall to wall.',
        [atk('engulf', 'Engulf', 1, 1.0, 'Slow', .3, 1), buff('jelly_wobble', 'Jelly Wobble', 1, 'DefenseUp', .35, 2),
         atk('acid_splash', 'Acid Splash', 2, .9, 'DefenseDown', .2, 2, True, unlock=1), aoe('dissolve', 'Dissolve', 3, 1.0, 'Poison', 8, 2, unlock=2)],
        [form('gloam_ooze', 'Gloam Ooze', 'F', 'E', 'A puddle that glints with half-digested crystal.'),
         form('crystal_jelly', 'Crystal Jelly', 'D', 'C', 'A wobbling flan with a red orb at its centre.'),
         form('geode_cube', 'Geode Cube', 'B', 'B', 'A corridor-filling cube; skeletons hang inside it.', large=True),
         form('abyssal_gel', 'Abyssal Gel', 'A', 'A', 'A black tide that swallows light itself.', large=True)],
        'Gloam gel'),
    fam('husks', 'Husks', 'D&D zombies, ghouls, wights and liches; GKOM Husk lore', 'Dark', 'Bruiser', False,
        ['ruin', 'blight', 'keep'],
        'The men GKOM killed. Husks are what is left when the infection outlives the mind: they climb the rank ladder '
        'by devouring other orbs, and the oldest remember enough to cast.',
        [atk('grasp', 'Hollow Grasp', 1, 1.15), atk('rot_bite', 'Rot Bite', 1, .9, 'Poison', 6, 2),
         buff('undying', 'Undying', 2, 'Regen', 10, 2, unlock=1), aoe('death_knell', 'Death Knell', 3, 1.1, 'AttackDown', .2, 2, True, unlock=2)],
        [form('hollow_husk', 'Hollow Husk', 'F', 'E', 'A shambling corpse with a grey orb where its heart was.'),
         form('crystal_ghoul', 'Crystal Ghoul', 'D', 'C', 'A fast, starving husk with crystal teeth.', role='Skirmisher'),
         form('wight_knight', 'Wight Knight', 'B', 'A', 'A fallen soldier still wearing its rusted oath.', role='Guardian'),
         form('orb_lich', 'Orb Lich', 'S', 'SSR', 'A husk that kept its mind; its orb floats above an open ribcage.', role='Caster')],
        'Husk bone'),
    fam('veinweb_spiders', 'Veinweb Spiders', 'D&D giant spider and drider, FF spiders', 'Dark', 'Skirmisher', False,
        ['cave', 'heartwood', 'ruin'],
        'Spiders that spin crystal-fibre webs strung like black veins through the trees.',
        [atk('venom_bite', 'Venom Bite', 1, 1.0, 'Poison', 6, 2), atk('web_shot', 'Web Shot', 1, .6, 'Slow', .35, 1),
         buff('brood', 'Spawn Brood', 2, 'AttackUp', .2, 2, 'AllAllies', unlock=1), aoe('silk_storm', 'Silk Storm', 3, .9, 'Slow', .3, 1, unlock=2)],
        [form('veinweb_spider', 'Veinweb Spider', 'E', 'D', 'A dog-sized spider with crystal-tipped legs.'),
         form('crystalweb_broodmother', 'Crystalweb Broodmother', 'C', 'B', 'Her back is a nest of hatching crystal eggs.', large=True),
         form('silkqueen_arachne', 'Silkqueen Arachne', 'A', 'S', 'Half woman, half spider, wholly GKOM.', role='Caster', large=True)],
        'Veinweb silk'),
    fam('glint_serpents', 'Glint Serpents', 'D&D basilisk and naga, Norse world-serpent, FF Midgardsormr', 'Earth', 'Caster', False,
        ['marsh', 'ruin', 'crystal'],
        'Serpents whose gaze crystallises flesh. The eldest naga keep ruined temples; the last of the line coils around mountains.',
        [atk('fang', 'Glint Fang', 1, 1.0, 'Poison', 5, 2), atk('stone_gaze', 'Stone Gaze', 2, .8, 'Stun', 1, 1, True),
         buff('coil', 'Coil', 1, 'DefenseUp', .3, 2, unlock=1), aoe('petrify_wave', 'Petrify Wave', 3, 1.0, 'Slow', .4, 1, True, unlock=2)],
        [form('glint_adder', 'Glint Adder', 'F', 'E', 'A small snake with jewelled scales and a mean bite.', role='Skirmisher'),
         form('gazestone_basilisk', 'Gazestone Basilisk', 'D', 'C', 'Look away: its gaze turns skin to quartz.', role='Bruiser'),
         form('coilqueen_naga', 'Coilqueen Naga', 'B', 'A', 'A serpent priestess guarding a drowned temple.'),
         form('worldcoil_serpent', 'Worldcoil Serpent', 'S', 'SSR', 'Its crystal coils are mistaken for mountain ridges.', large=True)],
        'Glint scale'),
    fam('opal_sirens', 'Opal Sirens', 'D&D harpy and siren, FF Siren', 'Water', 'Caster', False,
        ['marsh', 'heartwood', 'ruin'],
        'Winged singers of the drowned groves. Their opal throats carry a song that pulls travellers into the water.',
        [atk('talon', 'Opal Talon', 1, 1.0), atk('lure_song', 'Lure Song', 2, .6, 'AttackDown', .25, 2, True),
         mend('tide_hymn', 'Tide Hymn', 2, 30, 'AllAllies', unlock=1), aoe('drowning_aria', 'Drowning Aria', 3, 1.1, 'Slow', .3, 1, True, unlock=2)],
        [form('opal_harpy', 'Opal Harpy', 'E', 'D', 'Screeching, opal-feathered and always hungry.', role='Skirmisher'),
         form('blood_opal_siren', 'Blood Opal Siren', 'C', 'B', 'Her song is beautiful. Her opal heart is not.'),
         form('siren_matron', 'Siren Matron', 'A', 'A', 'The choir mistress of the sunken marsh.')],
        'Opal plume'),
    fam('cathedral_hydras', 'Cathedral Hydras', 'D&D / Greek hydra', 'Earth', 'Bruiser', True,
        ['marsh', 'heartwood'],
        'Many-headed swamp serpents. Each new head grows around a fresh crystal; the oldest hydra wears a cathedral of them.',
        [atk('heads_bite', 'Many Bites', 2, 1.4), buff('regrow', 'Regrow', 1, 'Regen', 12, 2),
         aoe('acid_spray', 'Acid Spray', 3, 1.0, 'Poison', 8, 2, unlock=1), atk('hydra_fury', 'Hydra Fury', 3, 2.2, unlock=2)],
        [form('fen_hydra', 'Fen Hydra', 'C', 'C', 'Three heads, all of them angry.'),
         form('marsh_hydra', 'Marsh Hydra', 'B', 'A', 'Cut one head and two crystal heads grow back.'),
         form('verdant_cathedral_hydra', 'Verdant Cathedral Hydra', 'S', 'SSR', 'Its necks rise like the spires of a green-glass cathedral.')],
        'Hydra crystal'),
    fam('geode_knights', 'Geode Knights', 'D&D animated armor and death knight, FF Iron Knight', 'Light', 'Guardian', False,
        ['keep', 'ruin'],
        'Suits of armour filled with crystal instead of men. They still salute, still hold the gate, still challenge travellers.',
        [atk('geode_strike', 'Geode Strike', 1, 1.2), buff('shield_wall', 'Shield Wall', 1, 'DefenseUp', .4, 2),
         buff('rally', 'Rally', 2, 'AttackUp', .2, 2, 'AllAllies', unlock=1), atk('judgement', 'Crystal Judgement', 3, 2.0, 'DefenseDown', .25, 2, unlock=2)],
        [form('geode_squire', 'Geode Squire', 'D', 'D', 'An empty helmet glowing from within.'),
         form('geode_knight', 'Geode Knight', 'C', 'B', 'A full suit of plate grown together with quartz.'),
         form('crowned_geode_knight', 'Crowned Geode Knight', 'A', 'S', 'Its crystal crown is all that is left of a king.')],
        'Geode plate'),
    fam('maw_behemoths', 'Maw Behemoths', 'FF Behemoth / King Behemoth, Diablo demonic beasts', 'Dark', 'Bruiser', True,
        ['blight', 'mine'],
        'Horned calamity beasts. Even the cub is a B-rank threat; the full-grown Obsidian Maw is sealed in obsidian crystal '
        'and bends gravity around its beach-ball core.',
        [atk('gravitic_rend', 'Gravitic Rend', 2, 1.8), aoe('obsidian_quake', 'Obsidian Quake', 3, 1.2),
         atk('voidcore_pulse', 'Voidcore Pulse', 2, 1.0, 'Slow', .4, 1, True, unlock=1), aoe('maw_of_ruin', 'Maw of Ruin', 3, 1.6, 'DefenseDown', .25, 2, True, unlock=2)],
        [form('maw_cub', 'Maw Cub', 'B', 'B', 'Already the size of a horse.', large=False),
         form('maw_behemoth', 'Maw Behemoth', 'A', 'S', 'Its roar shakes stones loose from cliffs.'),
         form('obsidian_maw_behemoth', 'Obsidian Maw Behemoth', 'SS', 'SSR', 'Its entire outer body is sealed in obsidian GKOM crystals.')],
        'Obsidian horn'),
    fam('crag_trolls', 'Crag Trolls', 'D&D ogre and troll, FF Ogre', 'Earth', 'Bruiser', True,
        ['mine', 'cave', 'keep'],
        'Brutes of the mountain passes. Trolls heal around their crystal faster than a sword can cut.',
        [atk('club', 'Crag Club', 1, 1.4), buff('troll_blood', 'Troll Blood', 2, 'Regen', 12, 2),
         atk('boulder_toss', 'Boulder Toss', 2, 1.7, 'Stun', 1, 1, unlock=1), aoe('rockslide', 'Rockslide', 3, 1.2, unlock=2)],
        [form('cragling_ogre', 'Cragling Ogre', 'D', 'C', 'Dim, hungry and carrying half a tree.'),
         form('crag_troll', 'Crag Troll', 'B', 'A', 'Cut it and the crystal knits the wound shut.'),
         form('ironhide_troll_king', 'Ironhide Troll King', 'S', 'S', 'A troll so old its hide has turned to ore.')],
        'Troll crystal'),
    fam('hoard_mimics', 'Hoard Mimics', 'D&D mimic, FF Mimic', 'Neutral', 'Guardian', False,
        ['ruin', 'keep', 'mine'],
        'GKOM learned that adventurers open boxes. Mimics are orbs wearing furniture.',
        [atk('lid_snap', 'Lid Snap', 1, 1.3), buff('play_dead', 'Play Dead', 1, 'Shield', 30, 2),
         atk('adhesive_tongue', 'Sticky Tongue', 2, 1.0, 'Stun', 1, 1, unlock=1), atk('devour_hoard', 'Devour Hoard', 3, 2.2, unlock=2)],
        [form('crate_mimic', 'Crate Mimic', 'E', 'D', 'A supply crate with too many teeth.'),
         form('chest_mimic', 'Chest Mimic', 'C', 'B', 'A treasure chest that is the treasure\'s last owner.'),
         form('vault_devourer', 'Vault Devourer', 'A', 'A', 'A whole vault door, hungry.', large=True)],
        'Mimic tongue'),
    fam('cinder_motes', 'Cinder Motes', 'D&D fire elemental, FF Bomb', 'Fire', 'Caster', False,
        ['mine', 'blight', 'ruin'],
        'Sparks of corrupted fire that swell when fed. A mote that eats enough ore becomes a walking forge.',
        [atk('ember', 'Ember Bolt', 1, 1.1, 'Burn', 5, 2, True), atk('self_destruct', 'Swell', 2, 1.5, magic=True),
         aoe('flame_wave', 'Flame Wave', 3, 1.0, 'Burn', 7, 2, True, unlock=1), buff('stoke', 'Stoke', 1, 'AttackUp', .3, 2, unlock=1)],
        [form('cinder_mote', 'Cinder Mote', 'F', 'E', 'A grinning spark that grows when it burns.', role='Skirmisher'),
         form('cinder_elemental', 'Cinder Elemental', 'D', 'B', 'A pillar of black fire around a red orb.'),
         form('pyre_titan', 'Pyre Titan', 'A', 'S', 'A giant of molten crystal; the ground cracks where it walks.', role='Bruiser', large=True)],
        'Cinder core'),
    fam('tide_wisps', 'Tide Wisps', 'D&D water elemental, FF Leviathan', 'Water', 'Caster', False,
        ['marsh', 'cave', 'heartwood'],
        'Ripples on the lake that look back. In the deep lakes they gather into a serpent of black water.',
        [atk('splash', 'Splash', 1, 1.0, magic=True), mend('ebb', 'Ebb', 1, 24),
         atk('undertow', 'Undertow', 2, 1.1, 'Slow', .4, 1, True, unlock=1), aoe('tidal_wave', 'Tidal Wave', 3, 1.3, magic=True, unlock=2)],
        [form('tide_wisp', 'Tide Wisp', 'F', 'E', 'A drifting bubble of lake water with a grey orb inside.'),
         form('undine', 'Undine', 'D', 'B', 'A water maiden with crystal eyes.'),
         form('abyssal_tidewyrm', 'Abyssal Tidewyrm', 'A', 'SS', 'A serpent of black water coiled around a crimson orb.', role='Bruiser', large=True)],
        'Tide pearl'),
    fam('static_sprites', 'Static Sprites', 'D&D air elemental and djinni, FF Ramuh-style storm spirits', 'Lightning', 'Skirmisher', False,
        ['crystal', 'mine', 'heartwood'],
        'Sparks that dance between crystal spires. Enough of them become a storm with a face.',
        [atk('zap', 'Zap', 1, 1.0, 'Stun', 1, 1, True), buff('charge_up', 'Charge Up', 1, 'Haste', 1, 2),
         aoe('chain_lightning', 'Chain Lightning', 2, .9, magic=True, unlock=1), atk('thunder_lance', 'Thunder Lance', 3, 2.0, magic=True, unlock=2)],
        [form('static_sprite', 'Static Sprite', 'F', 'E', 'A crackling spark that loves metal.'),
         form('storm_elemental', 'Storm Elemental', 'D', 'B', 'A whirling cloud stitched with crystal lightning.', role='Caster'),
         form('thunderhead_djinn', 'Thunderhead Djinn', 'A', 'S', 'A storm giant that bargains, then betrays.', role='Caster', large=True)],
        'Static crystal'),
    fam('rotcaps', 'Rotcaps', 'D&D myconid and violet fungus, FF Funguar', 'Earth', 'Caster', False,
        ['marsh', 'blight', 'cave'],
        'Mushrooms that grew on husks. Their spores carry a mild GKOM that makes victims sleepy and slow.',
        [atk('spore_puff', 'Spore Puff', 1, .8, 'Poison', 5, 2, True), mend('mycelium_mend', 'Mycelium Mend', 1, 22),
         aoe('sleep_spores', 'Sleep Spores', 2, .5, 'Slow', .4, 1, True, unlock=1), aoe('rot_bloom', 'Rot Bloom', 3, 1.0, 'Poison', 9, 2, True, unlock=2)],
        [form('sporecap', 'Sporecap', 'F', 'E', 'A waddling mushroom that sneezes grey spores.'),
         form('spore_shambler', 'Spore Shambler', 'D', 'C', 'A husk overgrown by fungus, still walking.', role='Bruiser'),
         form('rotbloom_matron', 'Rotbloom Matron', 'B', 'B', 'A fungal mother-flower the size of a cottage.', large=True)],
        'Rotcap spore'),
    fam('ruin_gargoyles', 'Ruin Gargoyles', 'D&D gargoyle and imp, FF gargoyles', 'Earth', 'Guardian', False,
        ['ruin', 'keep'],
        'Statues the GKOM crept into. They sit still for decades, then move all at once.',
        [atk('stone_claw', 'Stone Claw', 1, 1.15), buff('statue_form', 'Statue Form', 1, 'DefenseUp', .45, 2),
         atk('swoop', 'Swoop', 2, 1.5, unlock=1), aoe('granite_cry', 'Granite Cry', 3, 1.0, 'AttackDown', .2, 2, unlock=2)],
        [form('grit_imp', 'Grit Imp', 'E', 'E', 'A pebble-skinned imp that throws gravel and giggles.', role='Skirmisher'),
         form('ruin_gargoyle', 'Ruin Gargoyle', 'D', 'B', 'It was on the roof. Now it is behind you.'),
         form('cathedral_gargoyle_lord', 'Cathedral Gargoyle Lord', 'A', 'A', 'A gargoyle the size of a bell tower.', large=True)],
        'Gargoyle grit'),
    fam('gloomshades', 'Gloomshades', 'D&D specter, banshee and wraith, FF ghosts', 'Dark', 'Caster', False,
        ['ruin', 'blight', 'heartwood'],
        'Shadows of the GKOM dead. A shade that keeps feeding becomes a banshee, and a banshee becomes a hole in the world.',
        [atk('chill_touch', 'Chill Touch', 1, 1.0, 'AttackDown', .15, 2, True), buff('fade', 'Fade', 1, 'Shield', 25, 2),
         aoe('wail', 'Banshee Wail', 2, .8, 'Stun', 1, 1, True, unlock=1), atk('soul_rend', 'Soul Rend', 3, 2.1, magic=True, unlock=2)],
        [form('gloomshade', 'Gloomshade', 'E', 'D', 'A shadow that moves against the light.'),
         form('wailing_banshee', 'Wailing Banshee', 'C', 'B', 'Her scream cracks crystal and eardrums alike.'),
         form('void_wraith', 'Void Wraith', 'A', 'S', 'A tear in the air with a crimson orb for a heart.')],
        'Shade essence'),
    fam('spinetail_chimeras', 'Spinetail Chimeras', 'D&D manticore and chimera, FF Chimera', 'Fire', 'Bruiser', False,
        ['keep', 'mine', 'crystal'],
        'Beasts stitched together by GKOM from whatever it caught. Their tails fire crystal spines.',
        [atk('maul', 'Maul', 1, 1.3), atk('spine_volley', 'Spine Volley', 2, 1.0, 'Poison', 6, 2),
         aoe('fire_breath', 'Fire Breath', 3, 1.1, 'Burn', 7, 2, True, unlock=1), atk('three_heads', 'Three Heads', 3, 2.3, unlock=2)],
        [form('spinetail_lynx', 'Spinetail Lynx', 'D', 'D', 'A lynx with a scorpion\'s crystal tail.', role='Skirmisher'),
         form('manticore', 'Manticore', 'C', 'A', 'A lion\'s body, a man\'s grin, and a tail full of spines.', large=True),
         form('crystal_chimera', 'Crystal Chimera', 'S', 'SS', 'Lion, goat and dragon, fused in red crystal.', large=True)],
        'Chimera spine'),
    fam('whisperers', 'Whisperers', 'GKOM-A cultists (Weaververse "Whisperers"), D&D cult fanatics', 'Dark', 'Caster', False,
        ['ruin', 'keep', 'blight'],
        'People who survived GKOM and chose to serve it. They command lower-rank variants and lead elite packs; their '
        'leaders are A-rank Whisperers like the Crimson Queen.',
        [atk('dark_bolt', 'Dark Bolt', 1, 1.1, magic=True), buff('dirge', 'Dirge', 1, 'AttackDown', .2, 2, 'AllEnemies'),
         aoe('creeping_rot', 'Creeping Rot', 2, .7, 'Poison', 7, 2, True, unlock=1), buff('blood_pact', 'Blood Pact', 2, 'AttackUp', .3, 2, 'AllAllies', unlock=2)],
        [form('hollow_acolyte', 'Hollow Acolyte', 'D', 'D', 'A robed initiate with black-veined hands.'),
         form('whisperer', 'Whisperer', 'C', 'B', 'It speaks, and the variants listen.'),
         form('crimson_whisperer', 'Crimson Whisperer', 'A', 'S', 'A cult queen crowned in red crystal.')],
        'Whisperer sigil'),
]

# Families with no art yet borrow the closest existing silhouette until theirs is made.
STAND_IN = {
    'gloam_oozes': 'shardling_sprout', 'husks': 'enemy_summoner', 'veinweb_spiders': 'flintjaw_skitterer',
    'glint_serpents': 'cobalt_burrower', 'crag_trolls': 'moonstone_ravager', 'hoard_mimics': 'cobalt_burrower',
    'cinder_motes': 'viridian_prism_warden', 'tide_wisps': 'viridian_prism_warden', 'static_sprites': 'glasswing_mite',
    'rotcaps': 'shardling_sprout', 'ruin_gargoyles': 'obsidian_talon', 'gloomshades': 'enemy_summoner',
    'spinetail_chimeras': 'thorncrystal_stalker', 'whisperers': 'enemy_summoner',
}

# Lair bosses: region -> (family, title, minion theme). Titles are the names the game already uses.
BOSSES = {
    'silverbrook_edge': ('amberhides', 'Amberhide Matriarch', 'briar'),
    'rootside_camp': ('thorncrystal_stalkers', 'Thornfang Alpha', 'briar'),
    'shallow_ford': ('quartzbacks', 'Rimecoat Packlord', 'cave'),
    'moon_shrine': ('prism_wardens', 'Moon-Shrine Ascendant', 'ruin'),
    'old_bridge': ('obsidian_talons', 'Obsidian Roc', 'keep'),
    'sunken_marsh': ('moonstone_ravagers', 'Drowned Ravager', 'marsh'),
    'watchpost_ruin': ('cobalt_burrowers', 'Cobalt Siegeworm', 'keep'),
    'silverwood_gate': ('stormglass_drakes', 'Stormglass Wyvern Queen', 'crystal'),
    'silverwood_d1': ('quartzbacks', 'Fallen-Oak Packlord', 'heartwood'),
    'silverwood_d2': ('moonstone_ravagers', 'Mirror-Lake Ravager', 'marsh'),
    'silverwood_d3': ('cobalt_burrowers', 'Deepmaw of the Mines', 'mine'),
    'silverwood_d4': ('eclipse_golems', 'Eclipse Core Golem', 'blight'),
    'silverwood_d5': ('eclipse_golems', 'Heartrot Colossus', 'blight'),
}


def build():
    have_art = {p.stem for p in FIELD_MODELS.glob('*.png')} if FIELD_MODELS.exists() else set()
    families, problems = [], []
    for f in FAMILIES:
        assert f['element'] in ELEMENTS and f['role'] in ROLES, f['id']
        assert set(f['themes']) <= THEMES, (f['id'], set(f['themes']) - THEMES)
        forms = f['forms']
        for i, fm in enumerate(forms):
            if i > 0 and fm['minRank'] != forms[i - 1]['maxRank'] + 1:
                problems.append(f"{f['id']}: {fm['id']} does not start right after {forms[i - 1]['id']}")
            fm['evolvesTo'] = forms[i + 1]['id'] if i + 1 < len(forms) else ''
            fm['stage'] = i
            fm['role'] = fm['role'] or f['role']
            fm['large'] = f['large'] if fm['large'] is None else fm['large']
            fm['element'] = fm['element'] or f['element']
            if fm['cards'] is None:
                fm['cards'] = LEGACY_CARDS.get(fm['id']) or [
                    {k: v for k, v in c.items() if k != 'unlock'}
                    for c in f['kit'] if c['unlock'] <= i]
            fm['cards'] = [{k: v for k, v in c.items() if k != 'unlock'}
                           for c in sized(fm['cards'], move_count(fm['maxRank']), f, fm)]
            # New moves stay inside the pre-bestiary envelope (single target up to 1.9x, area up to 1.15x);
            # rank already scales the monster's stats.
            if fm['id'] not in LEGACY_CARDS:
                for c in fm['cards']:
                    c['power'] = min(c['power'], 1.15 if c['target'] == 'AllEnemies' else 1.9)
            # art: its own, else the nearest form in the family that has art (later forms first), else ''
            order = [fm['id']] + [x['id'] for x in forms[i + 1:]] + [x['id'] for x in reversed(forms[:i])]
            fm['art'] = next((o for o in order if o in have_art), STAND_IN.get(f['id'], ''))
            fm['ownArt'] = fm['id'] in have_art
            lo, hi = fm['minRank'] - 1, fm['maxRank'] - 1
            fm['core'] = CORE[lo] if lo == hi else CORE[lo] + ' to ' + CORE[hi]
            fm['crystal'] = COVERAGE[lo] if lo == hi else COVERAGE[lo] + ' to ' + COVERAGE[hi]
            fm['drops'] = [f'{RANKS[lo]}-rank Death Crystal', f['material']] + (['GKOM Dust'] if lo < 4 else ['Refined GKOM Essence'])
        families.append(dict(id=f['id'], name=f['name'], inspiration=f['inspiration'], element=f['element'],
                             role=f['role'], large=f['large'], themes=f['themes'], lore=f['lore'],
                             minRank=forms[0]['minRank'], maxRank=forms[-1]['maxRank'], material=f['material'],
                             forms=forms))
    ids = [fm['id'] for f in families for fm in f['forms']]
    assert len(ids) == len(set(ids)), 'duplicate form ids'
    for region, (family, title, theme) in BOSSES.items():
        assert any(f['id'] == family for f in families), region
    if problems:
        raise SystemExit('\n'.join(problems))
    bosses = [dict(region=r, family=f, title=t, theme=th) for r, (f, t, th) in BOSSES.items()]
    return families, bosses


def write_json(families, bosses):
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    data = {'version': 1, 'ranks': RANKS, 'population': POPULATION, 'families': families, 'bosses': bosses}
    OUT_JSON.write_text(json.dumps(data, indent=1), encoding='utf-8')


def rank_span(lo, hi):
    return RANKS[lo - 1] if lo == hi else RANKS[lo - 1] + '–' + RANKS[hi - 1]


def card_text(c):
    bits = []
    if c['power']: bits.append(f"{c['power']}× {'magic' if c['magic'] else 'physical'}")
    if c['heal']: bits.append(f"heal {c['heal']:g}")
    if c['status']: bits.append(f"{c['status']} {c['magnitude']:g}" + (f" for {c['duration']}" if c['duration'] else ''))
    target = {'Enemy': '', 'Self': ' (self)', 'Ally': ' (ally)', 'AllAllies': ' (all allies)', 'AllEnemies': ' (all foes)'}[c['target']]
    return f"**{c['name']}** ({c['ep']} EP{target}): " + (', '.join(bits) or 'utility')


def write_md(families, bosses):
    forms = [fm for f in families for fm in f['forms']]
    lines = [
        '# Adams Haven Bestiary: GKOM Variants',
        '',
        f'Generated by `Tools/build_bestiary.py` (the source of truth); runtime data: `Assets/Resources/AdamsHaven/Bestiary/bestiary.json`.',
        f'**{len(families)} families, {len(forms)} forms.** Inspired by the D&D / Pathfinder monster manuals and Final Fantasy, rebuilt as GKOM variants.',
        '',
        '## How ranks work',
        '- Every variant carries a **GKOM core orb**. Orb size and crystal coverage grow with rank (Weaververse codex: *GKOM Variant Rank System*, *GKOM Drops*; the lore\'s SR is the game\'s SS).',
        '- Each **family** has a minimum starting rank and a maximum allowed rank. Each **form** owns a slice of that range.',
        '- **Evolution:** when a monster\'s rank reaches the next form\'s minimum it becomes that form (Ashvein Goblin Scavenger → Ashvein Hobgoblin at D).',
        '- **Spawning:** an expedition depth has a natural rank (below); packs roll the rank one either side, weighted by the codex population share; elites are one rank up, lair bosses two (so only the deepest lair bosses, depth 12+, reach SSR).',
        '- **Stats:** a monster at its depth\'s natural rank has exactly the pre-bestiary numbers; each rank above or below adds or removes 8% health and offence.',
        "- **Moves:** a form has 2 moves at F-E, 3 at D-C, 4 at B-A and 5 at S and above (by its top rank): its family's moves unlocked by its stage, then the family's locked moves, its element's and its role's. In battle a monster uses the moves of its current rank's tier; an **elite** (one rank up, with an affix) always adds the form's signature (its last move); lair bosses add Gathering Fury and their element signature. New moves stay within the original roster's power (1.9x single target, 1.15x area).",
        '',
        '| Rank | Core orb | Look | Crystal coverage | Threat | Population | Depths |',
        '|---|---|---|---|---|---|---|',
    ]
    for i, r in enumerate(RANKS):
        depths = [d for d, rr in DEPTH_RANK.items() if rr == i + 1]
        d = (f'{depths[0]}–{depths[-1]}' if len(depths) > 1 else str(depths[0])) if depths else 'bosses only'
        lines.append(f'| {r} | {CORE[i]} | {CORE_LOOK[i]} | {COVERAGE[i]} | {THREAT[i]} | {POPULATION[i]}% | {d} |')
    lines += ['', '## Family overview', '', '| Family | Inspiration | Element | Ranks | Evolution line |', '|---|---|---|---|---|']
    for f in families:
        line = ' → '.join(f"{fm['name']} ({rank_span(fm['minRank'], fm['maxRank'])})" for fm in f['forms'])
        lines.append(f"| {f['name']} | {f['inspiration']} | {f['element']} | {rank_span(f['minRank'], f['maxRank'])} | {line} |")
    for f in families:
        lines += ['', f"## {f['name']}", '',
                  f"*{f['inspiration']}*  ·  {f['element']}  ·  {f['role']}  ·  ranks {rank_span(f['minRank'], f['maxRank'])}  ·  "
                  f"found in {', '.join(f['themes'])} places", '', f['lore'], '']
        for fm in f['forms']:
            evo = f" → evolves into **{next(x['name'] for x in f['forms'] if x['id'] == fm['evolvesTo'])}** at {RANKS[fm['maxRank']]}" if fm['evolvesTo'] else ' (apex form)'
            art = 'art ✓' if fm['ownArt'] else (f"art owed (uses `{fm['art']}`)" if fm['art'] else 'art owed (no family art yet)')
            lines += [f"### {fm['name']}  ·  {rank_span(fm['minRank'], fm['maxRank'])}{evo}",
                      f"`{fm['id']}`  ·  {fm['element']} {fm['role']}{', large' if fm['large'] else ''}  ·  {art}", '',
                      f"> {fm['flavor']}", '',
                      f"- **GKOM:** {fm['crystal']}; core orb {fm['core']}.",
                      f"- **Moves:** " + '; '.join(card_text(c) for c in fm['cards']) + '.',
                      f"- **Drops:** {', '.join(fm['drops'])}.", '']
    lines += ['## Lair bosses', '', '| Region | Boss | Family | Minions from |', '|---|---|---|---|']
    names = {f['id']: f['name'] for f in families}
    for b in bosses:
        lines.append(f"| {b['region']} | {b['title']} | {names[b['family']]} | {b['theme']} |")
    lines += ['', '## Where ranks appear', '', '| Region | Depths | Natural ranks |', '|---|---|---|']
    for rid, name, lo, hi in REGIONS:
        ranks = sorted({DEPTH_RANK[d] for d in range(lo, hi + 1)})
        lines.append(f"| {name} (`{rid}`) | {lo}–{hi} | {', '.join(RANKS[r - 1] for r in ranks)} |")
    owed = [fm for fm in forms if not fm['ownArt']]
    lines += ['', f'## Art status', '', f'{len(forms) - len(owed)} of {len(forms)} forms have their own art in `Resources/AdamsHaven/FieldModels`. '
              'The rest borrow the nearest family form\'s art until theirs is made:', '']
    lines += [f"- {fm['name']} (`{fm['id']}`)" + (f" → uses `{fm['art']}`" if fm['art'] else ' → generic') for fm in owed]
    OUT_MD.write_text('\n'.join(lines) + '\n', encoding='utf-8')


if __name__ == '__main__':
    fams, bosses = build()
    write_json(fams, bosses)
    write_md(fams, bosses)
    n = sum(len(f['forms']) for f in fams)
    print(f'{len(fams)} families, {n} forms -> {OUT_JSON.relative_to(ROOT)}, {OUT_MD.name}')
