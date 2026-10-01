# Adams Haven Tower Mode: character prompt pack

Ready-to-run Codex image prompts for **36 heroes** (4 per rank, F to SSR) and **24 residents**: 504 hero prompts plus 192 resident prompts, 696 in total. All characters are 21+. Design source: `../TOWER_MODE_GDD.md` sections 6 and 8.

## Fastest way to use it

1. Skim `00_SHARED_STYLE.md` once (about 2 minutes).
2. Open one character file (for example `heroes/B-1_kestrel.md`). Its **Quick start** table lists every output, size and the single file to attach.
3. Run step 1 (no attachment), keep the best result, then run steps 2 onward attaching that file. Prompts are complete; nothing needs editing.
4. For batch or scripted use, `prompt_queue.csv` / `prompt_queue.jsonl` hold every prompt with its output file name, size, transparency flag and reference file.
5. To change a character, edit `_source/heroes_*.py`, `_source/residents_data.py` or `_source/measurements.py`, then run `python _source/build_prompts.py` to regenerate everything.

## Notes

- **Size = chest / waist / hips** (plus height and weight) per character, stated in every prompt; a Small/Medium/Large height class is kept only for chibi head count and Unity scale.
- **Female characters use deliberately exaggerated, stylized gacha proportions** (fuller bust and hips, much narrower waist), applied by `FEMALE_EXAGGERATION` in `_source/build_prompts.py` on top of the base table in `measurements.py`. Costumes stay fully opaque and non-explicit.
- Elements follow GDD 6.6 (7 elements). Hero element counts: Fire 6, the other six 5 each. Roles 9 each.
- Kestrel (B-1) and Sable (B-2) follow the Adams Haven VN canon. Linnet, the VN's future unlock, is not in this pack. The existing card roster (Kaela, Clarity, Daisy, Elara, Ghislaine, Helda, JD, others) is separate and untouched.
- Stats are deterministic drafts from the GDD 6.7 rank bands. Ability text is design intent; battle numbers are not set.

## Heroes

| ID | Name | Rank | Element | Class | Role | Race | Chest/Waist/Hips cm | Primary | Room |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F-1 | [Pip Thistlewick](heroes/F-1_pip-thistlewick.md) | F | Earth | Tinker-Trapper | Controller | Gnome | 104/102/100 | Wit | Stone Quarry |
| F-2 | [Bram Oakhelm](heroes/F-2_bram-oakhelm.md) | F | Water | Shieldbearer | Tank | Human | 118/104/106 | Grit | Stone Well |
| F-3 | [Tilly Nettlebright](heroes/F-3_tilly-nettlebright.md) | F | Wind | Skirmisher | Striker | Human | 95/53/104 | Grace | Silverbrook Adventure Guild |
| F-4 | [Rook Emberwick](heroes/F-4_rook-emberwick.md) | F | Fire | Hearth Tender | Support | Kobold (lizardfolk) | 78/62/74 | Charm | Kitchen |
| E-1 | [Zippa Sparkwhisker](heroes/E-1_zippa-sparkwhisker.md) | E | Lightning | Courier | Striker | Marten-folk (beastfolk) | 94/50/101 | Grace | Silverbrook Adventure Guild |
| E-2 | [Sister Calla Dawnmend](heroes/E-2_sister-calla-dawnmend.md) | E | Light | Lamp Priest | Support | Human | 114/67/127 | Grace | The Frosted Mug |
| E-3 | [Mordecai Gloam](heroes/E-3_mordecai-gloam.md) | E | Dark | Hexer | Controller | Shade-touched human | 94/78/90 | Wit | Deck Hall |
| E-4 | [Ulric Cairnbreaker](heroes/E-4_ulric-cairnbreaker.md) | E | Earth | Wall Miner | Tank | Dwarf | 124/112/108 | Grit | Stone Quarry |
| D-1 | [Ashlyn Cinderbraid](heroes/D-1_ashlyn-cinderbraid.md) | D | Fire | Duelist | Striker | Tiefling | 106/57/113 | Might | The Forge |
| D-2 | [Juniper Gale](heroes/D-2_juniper-gale.md) | D | Wind | Windwarden | Controller | Sprite-kin (fae) | 90/48/96 | Wit | Deck Hall |
| D-3 | [Tarn Deepcurrent](heroes/D-3_tarn-deepcurrent.md) | D | Water | Bulwark | Tank | Saurian (lizardfolk) | 136/110/114 | Grit | Stone Well |
| D-4 | [Brother Isidore Lumen](heroes/D-4_brother-isidore-lumen.md) | D | Light | Lightmonk | Support | Human | 90/74/88 | Wit | Deck Hall |
| C-1 | [Nyx Veilwhisper](heroes/C-1_nyx-veilwhisper.md) | C | Dark | Shadowblade | Striker | Dusk-elf | 93/49/101 | Grace | Silverbrook Adventure Guild |
| C-2 | [Captain Rhea Stormhail](heroes/C-2_captain-rhea-stormhail.md) | C | Lightning | Banner Captain | Support | Human | 113/65/118 | Charm | Silverbrook Adventure Guild |
| C-3 | [Torvald Greywall](heroes/C-3_torvald-greywall.md) | C | Earth | Cliff Guardian | Tank | Goliath | 158/130/126 | Grit | Lumber Mill |
| C-4 | [Marisol Pyreheart](heroes/C-4_marisol-pyreheart.md) | C | Fire | Pyro-Alchemist | Controller | Human | 115/58/125 | Wit | The Frosted Mug |
| B-1 | [Kestrel](heroes/B-1_kestrel.md) | B | Fire | Twin-Blade Vanguard | Striker | Human | 101/55/107 | Might | The Forge |
| B-2 | [Sable](heroes/B-2_sable.md) | B | Wind | Windbow Scout | Support | Human | 100/53/108 | Sight | Silverbrook Adventure Guild |
| B-3 | [Thalassa Brinegate](heroes/B-3_thalassa-brinegate.md) | B | Water | Tide Sentinel | Tank | Tidebound (sea-elf) | 123/65/130 | Grit | Stone Well |
| B-4 | [Corvin Ashmantle](heroes/B-4_corvin-ashmantle.md) | B | Dark | Hexmaster | Controller | Crowfolk (beastfolk) | 92/76/88 | Wit | Deck Hall |
| A-1 | [Tsukiko Raikami](heroes/A-1_tsukiko-raikami.md) | A | Lightning | Thunder Shrine Duelist | Striker | Kitsune (fox-folk) | 97/50/106 | Grace | Argent Market |
| A-2 | [Seraphine Auric](heroes/A-2_seraphine-auric.md) | A | Light | Cantor | Support | Celestial (winged-kin) | 104/53/110 | Charm | The Frosted Mug |
| A-3 | [Ignatius Vael-Drakon](heroes/A-3_ignatius-vael-drakon.md) | A | Fire | Dragon Guardian | Tank | Dragonkin | 148/116/112 | Grit | Lumber Mill |
| A-4 | [Maelor Deepvein](heroes/A-4_maelor-deepvein.md) | A | Earth | Geomancer | Controller | Stone-giant | 170/140/132 | Wit | Stone Quarry |
| S-1 | [Nerissa Pearlwhisper](heroes/S-1_nerissa-pearlwhisper.md) | S | Water | Tide Singer | Support | Nereid | 118/60/127 | Charm | Argent Market |
| S-2 | [Vesper Nightingale](heroes/S-2_vesper-nightingale.md) | S | Dark | Night Soloist Assassin | Striker | Dusk-kin (shadow fae) | 95/50/103 | Grace | Deck Hall |
| S-3 | [Aeolus Brandt](heroes/S-3_aeolus-brandt.md) | S | Wind | Windwall Warden | Tank | Human (wind-touched) | 128/98/104 | Grit | Lumber Mill |
| S-4 | [TESSERAX-9 (Tess)](heroes/S-4_tesserax-9-tess.md) | S | Lightning | Dynamo Savant | Controller | Celestium Automaton | 96/84/92 | Wit | Deck Hall |
| SS-1 | [Lumina 'Lumi' Starlit](heroes/SS-1_lumina-lumi-starlit.md) | SS | Light | Star Weaver | Support | Star-fae | 88/47/96 | Charm | Heart Sanctuary |
| SS-2 | [Orsolya Granitecrown](heroes/SS-2_orsolya-granitecrown.md) | SS | Earth | Geo-Sovereign | Controller | Half-giant | 121/62/130 | Wit | Stone Quarry |
| SS-3 | [Draven Emberlord](heroes/SS-3_draven-emberlord.md) | SS | Fire | Infernal Marshal | Striker | Demonkin | 140/100/104 | Might | The Forge |
| SS-4 | [Thetis Maelstrom](heroes/SS-4_thetis-maelstrom.md) | SS | Water | Abyssal Bulwark | Tank | Tidal titan (sea-giant) | 137/76/144 | Grit | Stone Well |
| SSR-1 | [Elder Tuan Windshell](heroes/SSR-1_elder-tuan-windshell.md) | SSR | Wind | Wind-Shell Sage | Tank | Tortoise-folk (wind-spirit) | 112/108/106 | Grit | Heart Sanctuary |
| SSR-2 | [Solenne Auralis](heroes/SSR-2_solenne-auralis.md) | SSR | Light | Dawn Priestess | Support | Celestium-touched human | 109/55/118 | Charm | Heart Sanctuary |
| SSR-3 | [Erebus Nightcrown](heroes/SSR-3_erebus-nightcrown.md) | SSR | Dark | Void Regent | Controller | Voidborn sovereign | 108/82/96 | Wit | Heart Sanctuary |
| SSR-4 | [Ragnhild Thunderveil](heroes/SSR-4_ragnhild-thunderveil.md) | SSR | Lightning | Storm Valkyrie | Striker | Valkyrie (storm-kin) | 130/67/130 | Might | The Forge |

## Residents

| ID | Name | Rank | Race | Chest/Waist/Hips cm | Key stat | Room |
| --- | --- | --- | --- | --- | --- | --- |
| R-01 | [Tobias Rake](residents/R-01_tobias-rake.md) | F | Human | 92/78/90 | Grit | Farmstead |
| R-02 | [Mina Dewlight](residents/R-02_mina-dewlight.md) | F | Halfling | 102/60/113 | Might | Stone Well |
| R-03 | [Gorm Hatchet](residents/R-03_gorm-hatchet.md) | F | Orc | 130/118/116 | Might | Lumber Mill |
| R-04 | [Wren Kettle](residents/R-04_wren-kettle.md) | F | Human | 111/67/125 | Grit | Kitchen |
| R-05 | [Hobb Ledgerfoot](residents/R-05_hobb-ledgerfoot.md) | E | Halfling | 100/98/96 | Charm | Argent Market |
| R-06 | [Sera Quickneedle](residents/R-06_sera-quickneedle.md) | E | Elf | 96/53/107 | Grace | Hearth Nursery |
| R-07 | [Dunstan Pickaxe](residents/R-07_dunstan-pickaxe.md) | E | Dwarf | 112/100/102 | Might | Stone Quarry |
| R-08 | [Lio Marigold](residents/R-08_lio-marigold.md) | E | Hare-folk (beastfolk) | 92/76/88 | Grit | Farmstead |
| R-09 | [Ilsa Brewbright](residents/R-09_ilsa-brewbright.md) | D | Human | 118/67/130 | Charm | The Frosted Mug |
| R-10 | [Fenwick Quill](residents/R-10_fenwick-quill.md) | D | Gnome | 82/76/80 | Wit | Deck Hall |
| R-11 | [Nessa Tidewell](residents/R-11_nessa-tidewell.md) | D | Lakefolk (water-kin) | 100/55/108 | Grace | The Frosted Mug |
| R-12 | [Brakka Ironsmoke](residents/R-12_brakka-ironsmoke.md) | C | Half-orc | 130/74/132 | Might | The Forge |
| R-13 | [Odalys Thornfield](residents/R-13_odalys-thornfield.md) | C | Dryad | 99/53/106 | Grit | Farmstead |
| R-14 | [Cato Lanternjaw](residents/R-14_cato-lanternjaw.md) | C | Halfling | 90/76/86 | Sight | Silverbrook Adventure Guild |
| R-15 | [Yarrow Moss](residents/R-15_yarrow-moss.md) | B | Treant sprout | 139/86/149 | Grace | Hearth Nursery |
| R-16 | [Pell Fortunecoin](residents/R-16_pell-fortunecoin.md) | B | Fox-folk (beastfolk) | 80/64/78 | Luck | Argent Market |
| R-17 | [Dr. Ambrose Pennywhistle](residents/R-17_dr-ambrose-pennywhistle.md) | B | Human | 88/78/86 | Wit | Deck Hall |
| R-18 | [Kasimir Veil](residents/R-18_kasimir-veil.md) | A | Tiefling | 98/78/92 | Charm | Argent Market |
| R-19 | [Mother Hesper](residents/R-19_mother-hesper.md) | A | Elf | 95/60/103 | Grace | Hearth Nursery |
| R-20 | [Ruvik Glasseye](residents/R-20_ruvik-glasseye.md) | A | Goblin | 82/70/76 | Sight | Silverbrook Adventure Guild |
| R-21 | [Tamsin Starling](residents/R-21_tamsin-starling.md) | S | Cloverkin (fae) | 86/48/94 | Luck | Barn |
| R-22 | [Magister Oru](residents/R-22_magister-oru.md) | S | Celestium Automaton | 100/80/94 | Wit | Deck Hall |
| R-23 | [Sunniva Hearthsong](residents/R-23_sunniva-hearthsong.md) | SS | Celestium-touched human | 121/65/132 | Grit | Kitchen |
| R-24 | [Elder Ysolde Everwell](residents/R-24_elder-ysolde-everwell.md) | SSR | Wellspring spirit | 107/58/118 | Might | Stone Well |
