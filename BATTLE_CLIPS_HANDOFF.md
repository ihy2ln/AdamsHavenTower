# Hand-off: anime clip fighters and in-battle staged skills (CM 10.30.0, 2026-10-03)

Read this before touching battle presentation. Design authority: `BATTLE_MODE_GDD.md` §8 ("Pre-rendered anime clips
replace the rigs"). **Nothing below is committed**; see "Git state".

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

## 5. Open items (priority order)

1. **Commit decision (user).** My edits sit on top of the BM 10.3.0 session's uncommitted work in `BattleFx/BattleMode/BattleModels/BattleCinematics/BattleSheet.cs` and the docs. My hunks alone are in `BattleMotion/_handoff/`:
   - `r1_*.patch`: clip fighters;
   - `r2_*.patch`: staging and layers;
   - `r3_*.patch`: match-cut removal.

   All-mine new files:
   - Runtime: `BattleClips.cs`, `BattleMoveFx.cs`, `BattleStage.cs`.
   - Editor: `BattleClipImporter.cs`, `BattleClipTests.cs`.
   - Tools: `Tools/produce_fighter_clips.py`, `move_specs.py`, `matte_frames.py`, `export_seedance_handoff.py`, `typecheck_unity.py`, `fighter_clips/*.json`.
   - Assets: `Resources/AdamsHaven/BattleClips/`, the new `Fx/Moves/*`.

   Modified tools: `produce_move_fx.py` (rewritten), `produce_cine.py` (now legacy), `produce_battle_motion.py` (path fix and `aw_*`), `build_battle_chibi_atlases.py` (path fix).
2. **User review of the staged skills** across all 6 fighters (only Elara and Kaela were probed in Play mode). The four re-rolled effects (`gh_guard_stance`, `he_lend_strength`, `default_helda`, `ult_daisy`) were only checked as frames.
3. **Weak takes to re-roll:**
   - Ghislaine's `AH_hit_react`: H3 added fire slashes while she flinches.
   - Clarity's `AH_block`: too close to her guard.
4. **Ultimate and awakening videos** are built from portrait and card art (`produce_battle_motion.py ULTS`), so their outfits don't match the field clips. Offer to re-render them from the battle sheets (`fighter_clips/<unit>.json` refs).
5. **Delete the fallbacks** now that all 6 fighters are covered: the 3D/2D rigs for party fighters, `MoveSets`, `Battle2DRigBuilder`/`Battle2DMoves`, `guard_hi` and the cine framing constants. Update `Battle2DPilotTests` to match. Ask the user first.
6. **Android check:** memory with the hi-res pages and the effect LRU (there is no LRU cap yet; sheets are cached per battle and released on exit), ASTC banding on beams, and async hi-res page loading.
7. **Seedance pass** on whatever the user flags: `export_seedance_handoff.py`.
8. **Not done from the plan:** the hue check of effects against the retired videos (P2), and a per-card signature performance for cards that share a field action (Elara's 3 bolt cards share `AH_skill_cast`; the layers tell them apart).

## 6. Paste to resume

> Continue the Adams Haven battle clip / staged-skill work. Read `S:\AI\Game\Unity AHCG\My project\BATTLE_CLIPS_HANDOFF.md`
> first, then `BATTLE_MODE_GDD.md` §8. Check `git status` and which other sessions are active before editing battle
> files (shared tree and Editor). Start with open item [n].
