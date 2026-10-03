# Battle 3D model integration

> **Oct 3: these rigs are now the FALLBACK.** A party fighter with a complete pre-rendered anime clip set
> (`Assets/Resources/AdamsHaven/BattleClips/<id>/`, made by `Tools/produce_fighter_clips.py`) draws that instead; the
> order in `CreateRig` is imported picture -> imported model -> clips -> 2D rig -> 3D rig. JD keeps his 3D rig.
> The rigs and `MoveSets` will be deleted once all six fighters have clips. See BATTLE_MODE_GDD.md section 8.
>
> The note below (Sept 29) is out of date: the field went back to the anime 3D rigs, then to the 2D rig for Kaela.
>
> **Sept 29: the field now uses the anime 2D chibis, not these 3D rigs.**
> Allies draw animated battle-outfit clips from `Assets/Resources/AdamsHaven/BattleChibi/<id>/{idle,walk_in_place}.png`
> (6x4 atlases, 24 frames at 12 fps, packed by `Tools/build_battle_chibi_atlases.py` from
> `S:/AI/Game/art/characters/character_cards/chibi-roster-dual-look-v1/<character>/battle/clips`). Idle loops; walk plays during melee dashes.
> JD falls back to the painted `Chibi/jd.png`. The 3D rigs below are kept but disabled by `Use3DRigs` in `BattleModels.cs`.

## Play
Open `Assets/Scenes/AdamsHavenBattleSandbox.unity` in Unity and press Play.
The six current party members load real skinned FBX models, with cards retained in hand.
JD now uses a rigged chibi model behind the allies, with card draw and placement gestures.

## Assets
`Assets/Resources/AdamsHaven/BattleModels/<id>/model.fbx`

Imported: Kaela, Ghislaine, Elara, Helda, Daisy, Clarity, Amara, and five Celestium variants.
The first six are connected to current BattleCatalog party IDs. The others are available assets,
not added to the combat roster or substituted for monsters.

Source: `S:/AI/Harness/Codex/home/worktrees/chibi-3d-tpose/AdamsHavenCardGame/art/character_cards/chibi-shared-3d-v2/<character>/battle-motion-v1/battle.glb`.
These source manifests label the animations battle-motion candidates. Each imported folder has provenance.json.
`Tools/export_battle_models.py` converts through Blender with consistent FBX units and external textures.

## Playback
BattleModels.cs samples the actual skeletal clips against BattleFx's clock, including battle speed and hit pauses.
Guard loops between actions. Action cues select rising strike (Kaela), cross slash, aimed shot,
cast release, guard pulse, or overhead burst. The primary clip contact is scaled to the existing impact time.
Health, hit tint, lunge positioning, targeting, and death presentation still use BattleFx.
The source battle exports do not contain dedicated hurt/death clips; those remain presentation effects.
Reserve cameras turn off until their units enter the field. Render targets and material instances are released on exit.
The transparent model cameras preserve the existing hand, battlefield, and HUD layout.

## Validation
Unity menu: Adams Haven > Validate Battle Models, while in Play Mode.
Creates temporary offscreen rigs, captures guard/attack PNGs for all six fighters, then destroys them.
Files: Battle3D-<id>.png and Battle3D-<id>-attack.png in the project root.

## JD: Meshy rig and card motions

Completed 2026-09-29. JD has a textured 60,000-triangle rig, a ready stance,
card-draw motion, and card-placement motion with hand-following deck/card props.
The generated humanoid motion was retargeted in Blender to the Meshy chibi skeleton.
Feet are kept in the standing stance. The Meshy rig has no individual finger bones;
precise finger gripping and coat secondary motion remain polish work.

Unity asset: `Assets/Resources/AdamsHaven/BattleModels/jd/model.fbx`.
Editable source: `MeshyJobs/20260929_010936_jd-card-duelist_01a0ebff/jd-card-actions.blend`.
Source generation and receipts live in that same MeshyJobs folder.
`Tools/retarget_jd_cards.py` rebuilds the animation/prop export.
The original detailed mesh is retained as `model.glb`; Blender reduced a copy to
60,000 triangles before rigging because the source had 1,763,176 triangles.

Clips:
- AH_battle_guard: looping ready stance.
- AH_draw_card: generated card-drawing gesture, retargeted and baked.
- AH_place_card: generated card-presenting/placing gesture, retargeted and baked.

DrawCards fires once when a batch reaches the hand. PlayCard fires only after a
hand-card play succeeds. JD plays the 3-second authored gestures at 2.5x battle speed
(about 1.2 seconds), returning to ready stance afterward.

### Meshy task trail and actual cost

| Resource | Task | Credits |
| --- | --- | ---: |
| image-to-3d | 01a0ebff-19ab-7686-8e15-0e8d6a087f4c | 30 |
| text-to-motion (draw) | 01a0ebff-f979-72af-9214-a5c5420258d6 | 10 |
| text-to-motion (place) | 01a0ec00-323a-759a-b925-cb69d772410c | 10 |
| rigging | 01a0ec05-c042-751f-ac75-1e65848bbe19 | 5 |
| Total reported consumed_credits | | 55 |

All four tasks succeeded. No paid reruns or remesh were submitted.

### Verification

Unity imported all three JD clips. Offscreen rendering uses the same BattleMode rig
construction, material conversion, clock sampling, and animation signals as the live scene.
The validation menu captures ready, placement, and draw poses as Battle3D-jd*.png in the project root.
The six party rigs were also visually checked in ready and attack poses.
The current Tower scene was retained during offscreen testing.

## Anime battle models (Sept 29, pilot: Kaela)

Pipeline per character:
1. Meshy web (image-to-3D, Meshy 7.1, textured, A-pose) from the anime battle-outfit art
   `chibi-roster-dual-look-v1/<character>/battle/neutral.png` (inputs in `MeshyJobs/anime-battle-v1/inputs`).
2. Free remesh to 30K triangles, Humanoid auto-rig with hand-placed markers, preset motions added
   (Hit Reaction, Knock Down, Victory + character moves), downloaded as one GLB with the MeshyRig skeleton.
3. `Tools/retarget_anime_battle.py` (Blender, headless) copies the nine CZN-referenced actions from
   `chibi-shared-3d-v2/<character>/battle-motion-v1/battle.glb` onto the new rig by bone name (world
   direction + roll, hips scaled), renames the Meshy clips to AH_*, yaw-corrects side-on Meshy stances,
   and exports `BattleModels/<id>/model.fbx` + textures.
4. `AdamsHaven/AnimeToon` shader (cel two-tone + rim + ink outline) replaces URP Lit on the rigs.

Runtime: units whose model has `AH_hit_react` (plus JD) render as 3D rigs; the rest use the painted
BattleChibi loops. `MoveSets` in BattleModels.cs picks basic / skill / ultimate clips per unit; targets
flinch with AH_hit_react and hold AH_knock_down when downed.
Kaela: basic = AH_attack_cross_slash, skill = AH_attack_rising_strike, ultimate = AH_flying_kick.
Meshy cost for Kaela: 35 credits (model); remesh, rig and preset animations were free in the web app.

## Celestium weapons, 0.5x speed, character sheet (Oct 2, BM 10.2.0)

**Weapons.** Every fighter now holds the Celestium weapon from `S:/AI/Game/Game Assets/characters/<Name>/Celestium Weapons/`.
Kaela (ice gauntlets), Helda (dwarf hammer) and Clarity (kunai) already used Meshy models of that art. The other three did not:
Ghislaine held the steel sword split off her body mesh, Daisy a plain red stick cut from hers, and Elara only had the quill
modelled into her left hand. Those three are now built from the 2D art itself by `Tools/weapon_from_art.py`
(stage 2 `Tools/weapon_from_art_mesh.py` runs in Blender): the transparent PNG's silhouette is inflated into a closed mesh
(thin parts come out round, blades are capped at a set half-thickness) and both faces carry the art as texture.
Output: `MeshyJobs/anime-battle-v2/weapons/{ghislaine-celestium-greatsword,daisy-celestium-spear,elara-celestium-pen}.glb`.
No Meshy credits. `python Tools/weapon_from_art.py all` rebuilds them; tune `SPECS` (alpha, open, grow, flat, step) per weapon.
Specs updated: `ghislaine.json` (new file, `flip: false` because the new mesh has its tip on +Z), `daisy.json` (new file),
`elara.json` (carves the quill out of the left hand, adds `Weapon_Pen` there). Rebuilt with `build_battle_rig.py` as usual.
The old Daisy spear is kept as `weapons/daisy-fire-spear.glb` (re-exported from her old .blend).
At runtime `Weapon_*` renderers get a "crystal light" toon setup in `CreateRig` (no cel shadow, brighter, strong rim, fine line).

**Field framing.** The field camera now spans -0.55..2.95 model units (`FieldCamCenter`/`FieldCamHalf`, 600 px image); the old
-0.5..2.5 clipped weapons raised overhead (Ghislaine's cleave reaches ~3.0 body units, measured in Blender).

**Speed.** The battle clock's base rate is half the old one (`Speeds = {0.5, 1, 2}` in BattleMode.cs). Since BM 10.3.0
the button labels are relative to that base, so it reads 1x / 2x / 4x (`SpeedLabel`). The choice is kept in PlayerPrefs
`AdamsHaven.BattleSpeed.v2` (a new key, so saved fast speeds reset to the new 1x). Everything on the clock slows together (lunges, hit timing, rig clips), so contacts stay in sync.

**Character sheet** (`Assets/Scripts/Battle/BattleSheet.cs`). Press and hold (0.45 s) a party tile in the bottom-left, a reserve
portrait, JD's plate or an ally on the field. A quick tap still selects as before. The left half is a model viewer with its own
1024 px rig (`CreateRig`, stage at x=2000): drag sideways to turn, up/down to move, scroll or +/- to zoom (0.6x-4x),
buttons for every AH_* clip (idle, basic, skill, ultimate, hit, down, walk; JD: draw / play card), play/pause, a scrub bar,
and 0.25x / 0.5x / 1x playback (0.5x default). The right half shows rank, element and role, live stats, stress and statuses,
the Celestium weapon, and every move and ultimate with its cost and effect. X, a tap outside or Escape closes it.
Editor test hooks: `DebugOpenSheet(id)`, `DebugSheetPose(clip, time, yaw, zoom)`, `DebugBeginHold(id)`, `DebugSpeed`.
