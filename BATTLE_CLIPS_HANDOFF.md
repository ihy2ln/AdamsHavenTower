# Hand-off: anime clip fighters and in-battle staged skills (CM 10.30.0 + CM 10.3.1, 2026-10-03)

Read this before touching battle presentation. Design authority: `BATTLE_MODE_GDD.md` §8 ("Pre-rendered anime clips
replace the rigs"). Committed on master: `4de0a69` (clips, staging, layers, re-rolls) and the ultimate/awakening
re-render commit after it. Latest batch: section 5a.

## 1. What and why

The user wanted ComfyUI-generated anime animation to replace the 3D/2D battle rigs, rendered with MiniMax H3 first and
upgraded with Seedance later if needed. A first pass used opaque full-screen videos for skills. The user rejected them
for two reasons:
- **Box edges:** the edges of the video frame were visible.
- **A replay with a generic effect:** after the video, the field replayed the action with a generic effect. Example: Elara's lightning beam through a rune circle was followed by a lobbed yellow orb.

Skills are now **staged inside the live battle** from transparent layers.

**User decisions (fixed; don't re-litigate):**
- **Seedance:** H3 for everything now. Seedance is only an upgrade pass after review. The user has a Seedance key and a browser login, and no Seedance client is built.
- **Scope:** all 6 party fighters. Enemies stay static painted cutouts, and JD stays 3D.
- **Basics and reactions:** they play on the field, reaction-based. The attacker's clip and the target's hit, block or knockdown meet at contact.
- **Skills:** a layered in-battle cinematic. There is never an opaque video for a skill.
- **Melee skills:** the camera follows the dash and the hit lands **at the target**.
- **Ultimates and awakenings:** they keep a full-screen video. Its end hands **straight to the payoff on the field**, with no wind-up replay.
- **Rigs:** the old 3D/2D rigs stay as fallback until every fighter is covered. All 6 are covered now, so the rigs and `MoveSets` can be deleted.
- **No generic orb:** no party card may ever show the generic orb. Enemies keep it.

## 2. State (all verified in the shared Editor, 2026-10-03)

**Clip sets:**
- All 6 fighters have complete clip sets in `Assets/Resources/AdamsHaven/BattleClips/<id>/`:
  - 9–10 actions each, 3–4 atlas pages of at most 2048 px, at 12 fps;
  - per-frame muzzle points;
  - hi-res pages for the staged actions (ppu 240);
  - `guard_hi.png`.
- Each fighter's clip set is used automatically once it is complete.

**Effect layers:**
- `Assets/Resources/AdamsHaven/Fx/Moves/moves.json` covers 57 cards with 75 layers: charge, beam or bolt travel, and impact or aura.
- It covers every skill, the ranged fighters' `default_<unit>` entries, 12 ultimate payoffs and 6 awakenings.
- Each layer's tint is measured from its own art, so Elara's lightning is blue.

**Cinematics:**
- 12 ultimate videos and 6 awakening videos are in `UltCutIns/`.
- The 36 skill videos were moved to `BattleMotion/_retired_cine/`. `mv_shatter_hook.mp4` was tracked, so git shows it as deleted.

**Play-mode probes passed:**
- Elara's Piercing Shot, staged and unstaged: rune circle, then beam from her pen, then blue impact, with no bolt.
- Elara's Arc Bolt, unstaged.
- Kaela's Shatter, staged: the camera tracks the dash, the hook lands, and the hi-res pages are used.
- Kaela's ultimate hand-over: after the video she is already at the enemies in her slam pose.
- During staging the HUD steps aside. Captures are in `BattleMotion/stage_*.png` and `_stage_*.jpg`.

**Tests:**
- EditMode tests pass (run in the Editor through Unity MCP `RunCommand`):
  - `BattleClipTests`, 8 tests, including `EveryPartyCardHasEffectLayers` against the full manifest;
  - `Battle2DPilotTests`;
  - the relevant `BattleCznTests`.
- The console has no errors.

## 3. How it works

### Runtime (`Assets/Scripts/Battle/`)

| File | Role |
|---|---|
| `BattleClips.cs` (new) | `BattleClipSet`: the manifest (`clips.json`), card-to-action mapping (`ActionFor`), frame drawing, hi-res pages (`LoadHi`/`UnloadHi`, async), muzzle (`TipAt`), the hand-over pose (`KeyTime`). The `BattleMode` partial holds the clip-fighter `FieldRig` (`Flip`), `ClipAction`/`ClipReaction`, `ImpactLead`/`ReleaseLead`, `Muzzle`, victory, and the sheet viewer. |
| `BattleMoveFx.cs` (new) | `moves.json` (`MoveDefFor`, `CardColor`), `ScheduleMoveLayers` (charge at A0, travel at the release, impact at contact), and the effect kinds 13 (charge on the muzzle), 14 (beam strip), 15 (bolt). Kind 11 is the impact sheet. |
| `BattleStage.cs` (new) | Camera keys driven by `fx`. `EvalCamera`/`UpdateCamera` produce `CamMatrix`, which only the field matrix uses; the HUD stays on the screen matrix. Also `BeginStage`, `StageLead`, `PayoffLead`, `SkipStage` (2.5x rush, nothing skipped), dim, ordering (the actor is drawn last), the letterbox overlay, and debug hooks (`DebugCam`, `DebugBoltCount`, `DebugEffectCount`, `DebugFx`, `DebugQueueEnd`). |
| `BattleFx.cs` | `ScheduleGroup` decides between payoff, staged and plain, and schedules the layers. `BeginAction` takes the `handed` flag and skips the orb and generic glows when the card has layers. `Impact(..., painted)`. `DrawEffects(under)`. Floaters are drawn in screen space. |
| `BattleMode.cs` | `OnGUI` adds the camera, `DrawStageDim`, the under-layers, the overlay, tap-to-skip, and hides the HUD while staged. Also `LungeOffset` `Arrive`, the stage-aware `DrawField`, and the hand duck. |
| `BattleCinematics.cs` | `CinematicFor` returns video only for ultimates and awakenings. `DrawMoveSheet` handles any grid. The match-cut code is removed. |
| `BattleModels.cs`, `BattleSheet.cs` | Seams for clip rigs: `CreateRig` order is picture, then imported .glb, then **clips**, then 2D, then 3D. The sheet has a SHOW RIG / SHOW ANIME CLIPS toggle. |
| `Assets/Editor/BattleClipImporter.cs`, `BattleFxImporter.cs`, `AndroidTextureCompression.cs` | Import settings: clip and effect pages use no mips, max 2048, CompressedHQ, and ASTC 6x6 on Android. |

**Timing.**
- Clip time runs in display seconds (`Rate = 1/SpeedBase`). Hit-stop freezes everything, because all of it is driven by `fx`.
- **Staged lead:** the clip's contact time × 0.5 / 0.8, clamped to 0.2–1.4 battle seconds.
- **Ultimate payoff:** the clip starts on its key frame, and the impact lands `max(.04, (contact - key)/12 × .5)` battle seconds after the video ends.

### Pipeline (`Tools/`)

| Command | What it does |
|---|---|
| `python Tools/produce_fighter_clips.py <unit> keys` | Guard stance: an approved file, or Qwen generates it from the battle sheet and Celestium weapon. Then pose keys as Qwen edits of the guard, auto-registered (scale from a height hint or a head match). Output: `BattleMotion/fighter_<unit>/review/keys.jpg`. |
| `... <unit> clips` | Motion: H3 first/last-frame segments. Then matte: BiRefNet on the server, or `Tools/matte_frames.py` if the server is down. Then pack: trim H3's holds, compress the drift into a snap, place muzzle dots, hi-res pages, `clips.json`, and GIFs/strips in `review/`. |
| `... <unit> pack` | Re-pack only (seconds). The `motion` / `matte` / `pack` stages can also run on their own, plus `<action>` to limit them to one action. |
| `python Tools/produce_move_fx.py <card \| prefix* \| all \| manifest>` | Effect layers from `Tools/move_specs.py`: H3 on black, then sheets, 4-strip beams or bolts, then `moves.json` with measured tints. |
| `python Tools/produce_battle_motion.py ult_* \| aw_*` | Ultimate and awakening videos. |
| `python Tools/export_seedance_handoff.py <unit> [action] \| cine <job*> \| import ... \| import-cine ...` | Seedance upgrade bundles, and re-import retimed to the segment. |
| `python Tools/typecheck_unity.py [--add f.cs] [--swap Assets/...=draft.cs]` | Offline Roslyn compile check, without the Editor lock. |

- Specs live in `Tools/fighter_clips/<unit>.json` and `Tools/move_specs.py`. Rendered work files stay in `BattleMotion/` (keys, segments with `key_first`/`key_last`/`spec.json` for Seedance, and `fx_*`).
- **Re-roll recipe:** change the seed and/or wording, delete that job's `h3_24.mp4` (or the raw key), re-run, then repack.
- **Line endings:** the patch scripts here use LF; keep files LF.

**Gotchas (measured):**
- Qwen redraws keys 15–25% larger than image 1.
- H3 holds its conditioning frames for about 4 frames, lands the move about 40% of the way in, then drifts into the end key.
- H3 returns more frames than the 8n+1 requested.
- Long weapons need a smaller `body_px` and `body_x`, or they leave the canvas.
- Poses with the weapon on the ground need `anchor_x: "torso"`.
- Effect prompts need wording like "small, wide black space all around", or H3 fills the frame and the effect reads as a box.
- ComfyUI crashed once mid-matte. It runs at `S:\AI\ComfyUI_windows_portable\ComfyUI-Easy-Install` on port 8188 and is shared with other sessions.

## 4. Verifying in the shared Editor

1. **Coordinate first.** Other sessions edit the same files and use the same Editor; "BM 10.3.0" is one of them. Check `git status` and ask before editing battle files. **Never edit a `.cs` file while the Editor is in Play mode.**
2. **Compile:** `python Tools/typecheck_unity.py`, then `AssetDatabase.Refresh()` through RunCommand, then check `Unity_GetConsoleLogs` for errors.
3. **Tests:** call the test methods directly from RunCommand, e.g. `new BattleClipTests().EveryPartyCardHasEffectLayers()` in a try/catch. `-runTests` can't run while the Editor holds the lock.
4. **Play-mode probe:**
   - Back up `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project` and `reg export "HKCU\Software\Unity\UnityEditor\DefaultCompany\My project"`.
   - Run `EditorApplication.EnterPlaymode()`.
   - Set `Time.timeScale = 0` and `PlayerPrefs.SetInt("AdamsHaven.Battle.Cinematics", 0)` (0 Always, 1 FirstUse, 3 Off).
   - Create a new GameObject, add `BattleMode`, and call `Begin(0, BattleCatalog.Party(ids), reserve, cb)`.
   - `DebugAdvance(3)`, then `DebugPlayCard(unit, card)`, then `DebugAdvance(realSeconds)` (battle s = real × 0.5), then `ScreenCapture.CaptureScreenshot(path)`. Use one capture per RunCommand.
   - Ultimates: `DebugUltimate(false, unit)`. The cut-in video runs on unscaled time, so it plays out between calls.
   - Finish with `ExitPlaymode`, then restore both backups (`robocopy /MIR`, `reg import`).

## 5a. CM 10.3.1 (2026-10-03): what changed

- **Committed** after BM 10.3.0 committed its side (`a0316fd`): the five shared battle files held only CM hunks
  (verified: HEAD + `BattleMotion/_handoff/r*.patch` reproduces them exactly). Left out on purpose: matted `rgba/`
  frames (regenerate with `matte`), the retired `cine_*` work and `_retired_cine/`, probe captures, logs.
- **Every party skill probed in Play mode** (wind-up + contact): `BattleMotion/_cm1031/staged_<unit>.jpg`.
- **Fixed `StageWeight`** (`BattleStage.cs`): it came from the camera zoom, so area hits that frame near 1x brought the
  HUD and hand back at contact (Wand Sweep, Matriarch Pyre, Burning Circle). It now holds 1 from the push-in key to the
  pull-back key by time; after tap-to-skip it still follows the zoom.
- **`DebugPlayCardAny`** (`BattleStage.cs`): like `DebugPlayCard`, but ally heals/buffs land on an ally.
- **Re-rolls:** Clarity `AH_block` (crossed-arm X); Ghislaine `AH_hit_react` (H3 drew fire bursts for "struck hard"
  and for every negated effect noun; positive wording + a 3-seed sweep per segment scored by added hot pixels, seeds
  8431/8434); Helda `he_chilling_touch` impact ("cold mist" filled the frame and read as a box; now ice shards).
- **Ultimate and awakening videos re-rendered from the field look** (`produce_battle_motion.py`): refs are the field
  guard cutout (`fighter_<unit>/keys/guard_hi.png`), the battle sheet and the Celestium weapon; the look text comes from
  `Tools/fighter_clips/<unit>.json`. Keys and motion re-render when their prompt/refs change (`key_*.json`, `h3.json`).
  v1 backups: `BattleMotion/_ults_v1/`.

- **Redo pass (same day):** JD's decrees got cut-ins (`ult_sum_a` Rally, `ult_sum_b` Sunder; `IsDecree` in
  `BattleCinematics.cs`, payoff handed over like an ultimate; refs from the portrait art, JD keeps his 3D rig).
  `ult_helda` and `ult_helda_sanctuary` re-rolled (both drew a second Helda: "around the party" and multi-view sheets
  invite clones; the wide beat now says she is alone). Clips re-rolled with `Tools/reroll_clip.py` (seed sweep, scored
  for added effect light, penalised when a glowing weapon drops out): Kaela and Ghislaine `AH_block`, Helda
  `AH_victory`, Daisy `AH_hit_react` (spear kept level) and Daisy `AH_block`, which is now a **snap**: a segment of 1
  frame is a cut to its key with no H3 render (H3 flung her spear in every take). Previous takes are kept, not deleted:
  `segments/_takes/` per fighter, earlier videos in `BattleMotion/_cm1031/keep/`. Test
  `EveryUltimateAwakeningAndDecreeHasACutIn`.

- **User report (borders + units moving), fixed:** effect sheets now fade through an oval inscribed in each frame
  (`produce_move_fx.matte(round_=True)`, smoothstep from r .38 to 1) and beam strips fade at both ends, so a layer
  that filled its H3 frame (Flare's light pillar, Rime Sweep's mist, Chilling Touch) no longer draws the frame as a
  box; all 75 layers were rebuilt from their renders (old sheets in `BattleMotion/_cm1031/keep/Fx_Moves_v1/`).
  A staged melee strike now holds at the target until the camera's pull-back (`UnitVis.HoldTo`,
  `BattleStage.HoldForStage`) and goes home with it; before, the fighter left the shot 0.2 s after contact while the
  camera still framed the hit, then the camera snapped after her. Measured: reaction clips still slide the feet
  10-24% of body height (H3 shuffles the stance), idle loops 0-2%.
- **Unresolved:** the user saw `KeyNotFoundException: The given key 'BattleCard'` once; the console was cleared on Play
  and two minutes of autoplay plus staged probes did not reproduce it. The only BattleCard-keyed map is `cardVis`
  (every lookup guarded). Note that the default scene now starts its own "Adams Haven Battle Sandbox" BattleMode;
  probes must drive that instance (a second BattleMode draws under it).

## 5b. CM 10.3.3 (2026-10-04): summon-to-cinematic transitions

With summon battles (BM 10.3.1, `SUMMON_BATTLES.md`) the fighters are off the field until their card plays, so an
ultimate used to cut straight from an empty spot to the video and then flash a second summon after it.
`BattleTransitions.cs` now runs each ultimate, awakening and decree video as one shot on the battle clock:

1. **Summon.** In a summon battle the fighter's circle opens and she rises out of it: she grows from 78% to full size,
   lit in her element's light.
2. **Push-in.** The camera pushes in on her (zoom 1.8) and the HUD steps aside. Her light swells from her chest until it
   fills the screen on the video's opening flash, which is now tinted to her element instead of white.
3. **Video.** Unchanged.
4. **Hand-back.** The closing flash fades back onto the field, which is still framed on her. A ring of her element
   goes out at her feet, and the camera follows the payoff to the targets and pulls back.
5. **Dissolve.** She dissolves only after the camera has pulled back. There is no second summon.

Also:
- Classic battles get the same push-in and flash, without the summon.
- A staged skill in a summon battle starts its push on her circle and waits until she has formed.
- Taps during the push-in are ignored, since the video's own tap-to-skip follows a beat later.
- A summon window that was extended no longer dissolves her at its old end (`DissolveAt`, `BattleSummons.cs`).

Tests: `BattleTransitionTests` (2). Verified by battle-clock tests only; nobody has watched it in the Editor yet.

## 5c. CM 10.3.4 (2026-10-04): combos and joint ultimates

One combo action and one joint ultimate for each of the 7 bonded pairs. Rules and table: `BattleCombos.cs`. Field:
`BattleCombosFx.cs`.

- **Combo action.**
  - A team-up (a bond partner holds the line or has already acted this turn) is followed by the pair's named combo,
    performed by the partner. Team-ups now happen in classic battles too.
  - The card itself no longer gets the 1.25x team-up bonus; the combo is the reward. Combos hit at 0.65x the table's
    numbers (`ComboScale`).
  - Each combo is always staged. It borrows the performer's own move (clip and moves.json layers, `BattleCombos.LookFor`)
    until it gets layers of its own under its id `combo_<a>_<b>`.
- **Joint ultimate.**
  - Offered in the ULT menu when both bonded fighters on the field have full meters. It spends both meters and one
    ultimate's SP, and hits about 1.35x a solo ultimate.
  - In summon battles both fighters are called out before the push-in, and the partner strikes beside the lead on the
    hand-back.
  - Its video is `UltCutIns/ult_joint_<a>_<b>.mp4`, and none exist yet. Until then the card-art cut-in shows both
    fighters; it never falls back to the lead's solo video.
- **Balance.** Combos pushed the rootside_camp and moon_shrine lair bosses under the 15% health-lost floor, so
  `SummonBossPower` went from 1.05 to 1.15 (`BattleRules.cs`, BM's file). All 39 cells are inside the bands again.
- **Tests:** `BattleComboTests` (8).
- **Owed:** 7 joint-ultimate videos (two fighters per shot) and the combos' own effect layers.

## 5d. CM 10.3.5 (2026-10-04): monster movesets and clips, joint cut-ins

- **Movesets by tier.** Every bestiary form has 2 moves at F-E, 3 at D-C, 4 at B-A and 5 at S and above
  (`Tools/build_bestiary.py move_count`).
  - A form that is short takes its family's locked moves first, then new element and role moves.
  - In battle a monster uses the moves of its current rank's tier (`BattleCatalog.MoveCount`).
  - Lair bosses count Gathering Fury and their signature toward the total.
- **Monster clips.** Each form gets the same kinds of animation as a fighter: an idle loop, one action per move, hit,
  block, knockdown and victory. All of them face the left. Monsters have no combos.
  - Specs: `Tools/build_enemy_clip_specs.py` writes `Tools/enemy_clips/<form>.json` from `bestiary.json` and
    `MonsterPrompts/` (guard = `battle_left.png`, reference = `battle_front.png`). Hand fixes go in
    `Tools/enemy_clips/_overrides.json`.
  - Rendering: `produce_fighter_clips.py` with `"kind": "monster"`. The batch is `python Tools/produce_enemy_clips.py`
    (`--status`, `--from`, `--only`). Work files are in `BattleMotion/enemy_<form>/`, packs in
    `Resources/AdamsHaven/BattleClips/<form>/`.
  - Cost: 748 keys and 1,398 H3 segments, about 10 to 14 minutes per form on the local GPU (roughly a day for all 98).
  - Field: `BattleEnemyClips.cs`. A monster with a complete set plays it; one without keeps its painted cutout.
- **Prompt lessons from the pilot.** Without explicit wording Qwen turns a recoiling or roaring creature to face
  right and drops held weapons. Every monster pose now ends with "still faces the left ... keeps hold of any weapon",
  and the hit pose avoids "toward the right".
- **Joint cut-ins.** `produce_battle_motion.py ult_joint_*` renders the 7 joint-ultimate videos, with both fighters
  named and placed left and right, and "exactly two people".
- **AUTO** plays a joint ultimate first when one is available.

## 5. Open items (priority order)

1. **User review** of `BattleMotion/_cm1031/staged_<unit>.jpg` and the 18 new ultimate/awakening videos.
3. **Reserve Tonic shows nothing** on the field: `BattleRules.Resolve` applies the ally's draw and +EP without recording
   a fact, so `Ingest` never schedules a group (no stage, no layers). BM owns `BattleRules.cs`; it needs a fact (or
   the presentation needs a hook) before the card can play its layers.
4. **Delete the fallbacks** (ask the user first): party 3D/2D rigs, `MoveSets`, `Battle2DRigBuilder`/`Battle2DMoves`,
   `guard_hi` and the cine framing constants; update `Battle2DPilotTests`.
5. **Android check:** memory with the hi-res pages and the effect cache (no LRU cap yet), ASTC banding on beams, async
   hi-res loading.
6. **Seedance pass** on whatever the user flags: `export_seedance_handoff.py`.
7. **Not done from the plan:** the hue check of effects against the retired videos, and a per-card signature for cards
   that share a field action (Elara's three bolt cards share `AH_skill_cast`; at contact they look alike).
8. Probe gaps: Glacier Guard's contact frame (no impact sheet, the stage ended first) and Helda's basic.

## 6. Paste to resume

> Continue the Adams Haven battle clip / staged-skill work. Read `S:\AI\Game\Unity AHCG\My project\BATTLE_CLIPS_HANDOFF.md`
> first, then `BATTLE_MODE_GDD.md` §8. Check `git status` and which other sessions are active before editing battle
> files (shared tree and Editor). Start with open item [n].
