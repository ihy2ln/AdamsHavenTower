# Battle Mode gameplay reference

User direction (2026-09-28): make Adams Haven's Unity Battle Mode play as close to **Chaos Zero Nightmare** as practical. Keep Adams Haven's characters and art direction, with chibi combatants and monster cutouts on the field and full-model move cards in the hand. This file is a design target for later Battle Mode work, not a description of features already complete.

## Verified reference points

- Smilegate describes turn-based combat with **three combatants and three partners**, chaining cards within a limited turn cost. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)
- Character-specific and shared cards combine into decks that can evolve during exploration. [Smilegate deck-building overview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=75) and [Protogenesis showcase announcement](https://newsroom.smilegate.com/eng/1752454486)
- Stress reaching a threshold causes a mental collapse; overcoming it unlocks an awakening skill. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)
- Combat uses simplified SD character presentation for tempo, with larger full-body animation for key moments. [Developer interview](https://newsroom.smilegate.com/en/bbs/board.php?bo_table=game&wr_id=76)

## Unity status and next alignment work

| Reference point | Unity Battle Mode now | Next step |
| --- | --- | --- |
| Three combatants plus three partners | Three fighters on the field; three fighter reserves can swap in | Design dedicated partner support slots and effects while retaining the desired reserve feature only if it adds value. |
| Card chains within a limited turn cost | Shared deck with per-fighter EP/AP and JD CP/SP | Evaluate a shared turn-cost layer and card sequencing without losing authored Adams Haven moves. |
| Stress, collapse, awakening | Stress and temporary strain affect card use; field HUD shows stress and breakdown. Recovery unlocks one free, character-specific awakening action. | Tune awakening effects and presentation through encounter playtests. |
| Deck growth during exploration | Authored default deck; draw/discard/reshuffle | Connect card upgrades and choices to the Unity expedition and save data. |
| SD characters on field, full art on cards | Chibi allies, transparent monster/JD cutouts on a painted stage with contact shadows; full-model move cards in an element-framed fan. Lunges, afterimages, projectiles, slash/burst sheets, rune circles, floating damage, ghost HP drain and ultimate cut-ins are code-driven. | Bind the timed `AnimationSignal` cues to rigged attack, hurt, support, and down clips; give every move card its own art. |

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
