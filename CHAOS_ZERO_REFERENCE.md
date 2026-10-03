# Battle Mode gameplay reference

User direction (2026-09-28): make Adams Haven's Unity Battle Mode play as close to **Chaos Zero Nightmare** as practical. Keep Adams Haven's characters and art direction, with chibi combatants and monster cutouts on the field and full-model move cards in the hand. This file is a design target for later Battle Mode work, not a description of features already complete.

## Verified reference points

- Smilegate describes turn-based combat with **three combatants and three partners**, chaining cards within a limited turn cost. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)
- Character-specific and shared cards combine into decks that can evolve during exploration. [Smilegate deck-building overview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=75) and [Protogenesis showcase announcement](https://newsroom.smilegate.com/eng/1752454486)
- Stress reaching a threshold causes a mental collapse; overcoming it unlocks an awakening skill. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)
- Combat uses simplified SD character presentation for tempo, with larger full-body animation for key moments. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)

## Rules brought over (BM 10.3.0, 2026-10-03)

Researched from GameWith, Game8 and itemlevel.net guides (CZN Season 3). Adams Haven keeps its own per-fighter AP/EP economy and JD's CP/SP (owner decisions); these CZN systems sit on top of it.

| CZN system | Adams Haven version | Code |
| --- | --- | --- |
| Draw 5 each turn, unplayed cards discarded, hand max 10 | Fighter cards refill to 5 at the start of each round (JD's support cards met on the way join the hand, up to 3). At End Turn every card not played, JD's included, goes to the discard pile unless it Retains; the turn box says how many will go. | `BattleState.RefillHand`, `EndTurn` |
| Card tags Initiation / Retain / Exhaust | Initiation: one signature card per fighter is in the opening hand. Retain: guards and setups stay in hand. Exhaust: the heaviest single moves leave the battle once played. | `BattleCatalog.Keywords` |
| Action Count: every card played lowers each enemy's counter, at 0 it acts at once | Counters 3 (fast), 4, 5 (slow, guards, bosses; 4 in phase 2). An enemy acts once per round: mid-turn when its count runs out, otherwise at End Turn. Ultimates, awakenings, decrees and partner assists do not tick it. | `TickActionCounts`, `EnemyAct` |
| Tenacity and Ravage (break) | 6 pips (guards 7, elites 9, bosses 12). Hits wear 1, +1 for a 2-EP card, +1 more at 3 EP, +1 on WEAK, +2 for ultimates. At 0 the enemy BREAKS: +25% damage taken until next round, its count is pushed back 1, the breaker gets 1 AP back and sheds 10 stress, JD gains 1 SP. Refills each round. | `WearTenacity`, `TenacityDamage` |
| Basic shield card; damage a shield fully blocks adds no stress | GUARD beside BASIC on every fighter tile: 1 AP, a shield of about one hit (defence x1.6 + 5% max HP). Shields stack and soak damage before HP. | `BattleCatalog.Guard` |
| Partners (3 combatants + 3 partners) | Reserve i partners field fighter i: +8% to their damage, healing and shields while alive. Each reserve has a once-per-battle ASSIST for 2 SP: their first ultimate at 60%, stepping onto the field beside their partner. Swapping still works. | `PartnerOf`, `TryPartnerAssist` |
| Epiphany (a glowing card upgrades mid-fight, pick 1 of 3) | In expedition fights one fighter card glows. Playing it offers 3 of 8 upgrades (Sharpened, Swift, Echo, Steadfast, Sweeping, Rending, Bulwark, Enduring). The pick applies to every copy at once and lasts for the rest of the run (`TowerRun.epiphanies`). | `OfferEpiphany`, `ChooseEpiphany`, `TowerRules.KeepEpiphanies` |
| Overcoming a mental breakdown makes Ego cheaper | A fighter who recovers from breakdown gets the awakening action and their ultimates cost 2 SP instead of 3 for the rest of the battle. | `BattleUnit.Overcame`, `UltCost` |
| Speed 1x / 2x / 4x | The button reads 1x / 2x / 4x over clock rates 0.5 / 1 / 2 (the old 1x was too fast, so half of it is the base). | `BattleMode.SpeedLabel` |

On screen: one turn panel in the top centre (YOUR TURN / ENEMY TURN, ROUND N and the CHAIN count; the fight's title; how many cards End Turn will discard), NEXT TO ACT beside it with that enemy's cards left, and the full order strip under it (YOU > enemies by count > the enemy commander at END). Each enemy's intent pill carries its action-count ring (red on its last card), tenacity pips sit under enemy health and turn into a red BROKEN bar, shields show as a pale band over health bars with their value, the hand sweeps to the discard pile at End Turn, an interrupting enemy gets an "ACTS!" banner, and the Epiphany choice is a full-screen pick.

**Balance.** The new tools made the party much stronger. Before tuning, normal fights cost 1–7% of party health instead of 4–14%. Enemy offence is now multiplied by a depth curve, 1.5x at danger 1 easing to 1.0x at danger 13, and lair bosses take a further 1.25x (`BattleCatalog.OffenseShallow/OffenseDeep/BossOffenseScale`). Enemy health is unchanged, so fight length is too. After tuning, the sim at the test's own sample size (80 fights per cell) gives: normal 5–10%, elite 13–29%, boss 21–71% party health lost. All 39 region/kind cells are inside the `ExpeditionBalanceTests` bands.

Not brought over: a shared team AP pool (would replace the owner's AP/EP design), Rewind, Save Data / Faint Memory (runs are expedition-scoped), Combo/Celestial card tags.

## Earlier alignment table (2026-09-29)

| Reference point | Unity Battle Mode then | Status now |
| --- | --- | --- |
| Three combatants plus three partners | Three fighters on the field; three fighter reserves can swap in | Reserves are partners (passive + assist) and can still swap in. |
| Card chains within a limited turn cost | Shared deck with per-fighter EP/AP and JD CP/SP | Chains now move enemy action counts; CHAIN counter on screen. |
| Stress, collapse, awakening | Stress and temporary strain affect card use; field HUD shows stress and breakdown. Recovery unlocks one free, character-specific awakening action. | Recovery also makes ultimates cheaper; blocked damage adds no stress. |
| Deck growth during exploration | Authored default deck; draw/discard/reshuffle | Epiphanies grow cards during runs (plus the existing reward-screen upgrades). |
| SD characters on field, full art on cards | Chibi allies, monster cutouts, full-model move cards | Unchanged; any of it can now be replaced from the media library (MEDIA_LIBRARY.md). |

Presentation targets already reached: enemy intent badges with expected damage and target, target-select reticles with damage previews, a curved hand with hover lift, a round end-turn button with draw/used piles, party tiles with AP/EP/stress/ult state, and per-hit feedback.

## Video direction supplied by the user

- [Fast auto battle](https://www.youtube.com/watch?v=68CJ9vzHS7g): brisk action pacing with the field and card hand remaining readable.
- [Nine combat motion](https://www.youtube.com/watch?v=7-QM7H_EkXc): a distinct key-move presentation layered over ordinary battlefield motion.
- [Nine, Khalipe, and Narja showcase](https://www.youtube.com/watch?v=Ckxt3Q8QvcI): fast impact streaks, floating damage and WEAK cues, and models on the field while cards stay in the hand.

The present cutout motion and effects are temporary interpretations of these references. The future rig driver should listen to `BattleMode.AnimationSignal` and play a character's own clips at the timeline beats.

Keep the card hand and field visually separate. Do not put framed playing cards on the battlefield. Use the reference to guide battle pacing, readable intent, character emotion, and deck decisions as the port continues.

## Generated motion and effects (2026-09-29)

Frame study of the three videos drove these choices: ultimates cut to a ~3 s cinematic (tight portrait grin → weapon/fist wind-up → wide impact) before damage lands; crits flash a full-screen speed-line warp; hits use a white-core crescent slash, a four-point star/ring impact and flying shards.

`Tools/produce_battle_motion.py <job|all>` builds them with local ComfyUI: **Qwen Image Edit 2.1** turns character art into 16:9 first/last keyframes, and **MiniMax H3** (turbo 4-step) animates between them. The FX are H3 black→effect→black clips packed into 8-frame white-on-alpha strips that BattleGui tints by element.

| Output | Used by |
| --- | --- |
| `Resources/AdamsHaven/UltCutIns/<card id>.mp4` (720p60) | `DrawCutIn` plays it through a VideoPlayer. The cut length follows the clip. Lookup is by card id, then unit id. A card with no clip (Awakening, JD decrees) keeps the card slide-in. |
| `Fx/Gen/fx_slash_sheet`, `fx_impact_sheet` | Layered over the procedural hit in `Impact()` |
| `Fx/Gen/fx_frost_sheet`, `fx_fire_sheet`, `fx_lightning_sheet`, `fx_light_sheet` | Element burst on Water / Fire / Lightning / Light hits (`BattleGui.ElementSheet`) |
| `Fx/Gen/fx_warp_sheet` | Full-screen flash on crits |

Work files and keyframes are in `BattleMotion/<job>/`. Delete a job's `h3_24.mp4` to re-render. Without that, only the sheet is rebuilt. All six party fighters have both of their ultimates rendered (12 clips). To add or re-shoot one, edit its row in `ULTS` (close-up beat, wide shot, seed) and run the job. Delete its `h3_hi.mp4` to re-render the motion, and delete its `key_*.png` files to regenerate the keyframes. `python Tools/produce_battle_motion.py ult_*` runs every ultimate. When rigged clips arrive, these stay as the overlay layer. The rig driver listens to `AnimationSignal` for the body motion.
