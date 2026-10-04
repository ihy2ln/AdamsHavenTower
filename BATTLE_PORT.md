# Adams Haven Battle Mode (Unity)

The project has one scene, `Assets/Scenes/AdamsHavenTower.unity`: it runs the Tower, town, expeditions and battles. To test Battle Mode, press Play there and start a fight from the Tower or the debug menu. (The old `AdamsHavenBattleSandbox` scene was retired on 2026-10-04; `BattleSandboxLauncher` still works if you add it to a GameObject.)

The battle implementation is in `Assets/Scripts/Battle/`. `BattleRules.cs` contains the engine-independent combat state and rules; `BattleCatalog.cs` contains the six fighter kits, JD support kit, ultimates, and encounter data. The screen is three files: `BattleMode.cs` (layout, input, HUD, hand), `BattleFx.cs` (the presentation timeline that replays `BattleFacts` as lunges, projectiles, impacts, floating numbers, HP drains and ultimate cut-ins) and `BattleGui.cs` (procedural textures, rounded panels, outlined text, alpha-trimmed sprites). The Tower script only starts the mode and receives its result.

## Controls

- Click a card in the fan, then a highlighted target (right-click or Esc cancels). Self and side-wide cards resolve immediately. While a damaging card is selected, hovering a target previews the damage (WEAK / RESIST / LETHAL).
- Party tiles (bottom left) show each fighter's AP/EP pips, stress and the **BASIC** / **ULT** / **AWAKEN** buttons. A full meter and 3 SP enable **ULT**, which opens a two-choice popup.
- Click a field fighter (or their tile) for a small action bar: **FORWARD** / **BACK** move a lane (costs AP) and **SWAP** picks a reserve. The **RESERVE** tokens under the JD plate also start a swap.
- **JD DECREE** (under the JD plate) applies a team buff and enemy debuff at 10 SP. The round **END TURN** button plays the telegraphed enemy intents; it pulses when nothing playable is left.
- Enemy intent badges sit above each enemy: an icon (attack / guard / debuff), the expected damage and the fighter it targets. Hover any unit for its stats and statuses.
- The top-right buttons are **LOG**, **1x/2x** playback speed, **AUTO** and **RUN** (withdraw; it becomes **EXIT** when the fight ends).
- Field fighters show stress beneath HP. At the threshold, the meter becomes a **BREAKDOWN** indicator for the temporary strain state.
- When a fighter recovers from breakdown, **AWAKEN** appears on their field model. Each fighter has a distinct, one-use, no-cost recovery skill.

Cards in the hand use full-size character art in an element-tinted frame with glossy EP / AP (or CP) gems, and show their move name and effect. Kaela's six moves use her dedicated battle fronts. The field shows chibi fighters, transparent monster cutouts, and JD, with light idle motion. Melee cutouts leave brief elemental afterimages, and impacts have a short pause for the slash, damage number, and WEAK/CRITICAL cue. These are visual placeholders for later animated Meshy/Blender models and effects.

The Godot source remains the authority for the full expedition. This Unity port currently uses a default six-fighter party and authored encounters by Tower depth. Equipped decks, roster progression, bond ranks, run relics, expedition saves, authored animation clips, and drag/flick gestures still need connection when the corresponding Tower and expedition systems reach Unity.

The Silverwood arena plate was generated with the Codex image generator. Existing Godot chibi, full-model card, JD, and transparent monster cutout art is reused in Unity `Resources/AdamsHaven`.

The gameplay target is to feel as close to Chaos Zero Nightmare as practical while keeping Adams Haven's cast and card/field separation. See `CHAOS_ZERO_REFERENCE.md` for verified reference points, current differences, and the next alignment work.

## Battle presentation notes

- **Rules resolve instantly; the screen replays them.** Every rules change goes through `BattleMode.Perform` / `AfterAction`, which calls `Ingest()` to turn new `BattleFact`s (tracked by `BattleState.FactSerial`) into timed beats. HP bars only drain when their hit beat fires, dead enemies stay drawn until their death beat, and new hand cards and enemy intents appear once playback is idle.
- **Generated art.** `Tools/GenerateBattleFx.py` writes the slash and burst sprite sheets, ground rune circle, god rays, card frame, glossy gem and sparkle to `Assets/Resources/AdamsHaven/Fx/`. They are white-on-transparent and tinted by element at draw time. `Assets/Editor/BattleFxImporter.cs` keeps them uncompressed.
- **Do not use `GUIUtility.RotateAroundPivot` / `ScaleAroundPivot` here.** They treat the pivot as a screen point, so under the scaled 1600x900 matrix everything drifts whenever the window is not exactly that size. Use `BattleGui.RotateAround` / `ScaleAround`.
- **Testing in the editor.** The Battle Sandbox sets `Application.runInBackground = true` and calls `BattleMode.Begin()` on Play. Editor test hooks on `BattleMode` include `DebugAdvance(seconds)`, `DebugMouse(virtualPoint)` + `DebugClick()`, `DebugReadyUltimate()`, `DebugUltimate(killAll)` and `DebugSummary()`.
- **Rig handoff.** `BattleMode.AnimationSignal` emits timed `Ultimate`, `Action`, `Hit`, `Heal`, `Status`, and `Down` signals. Each carries actor, target, card, and the resolved fact when applicable. A rig driver can subscribe and route these to Animator triggers by `BattleUnit.Id`. `Begin()` clears the presentation timeline so replaying the sandbox starts cleanly.
