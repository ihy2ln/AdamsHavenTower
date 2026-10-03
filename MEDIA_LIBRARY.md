# Media library (BM 10.3.0)

Players can swap the game's media for their own without rebuilding the app: card art, move effects, move cinematics, move sounds, every game sound, music, fighter and monster models, portraits, and battle stages.

## Where it opens

- **Battle:** the **MEDIA** button under LOG in the top bar.
- **Character sheet** (press and hold a fighter): **MEDIA: MODEL & ART** opens on that fighter (or that monster).
- **Expedition screens:** **MEDIA LIBRARY** in the region panel and the run panel.

## Tabs and slots

| Tab | Slot (key) | Takes | Used for |
| --- | --- | --- | --- |
| MOVES | `card.<id>.art` | picture | the card face |
| | `card.<id>.fx` | picture or video | the field effect at the move's contact time. A picture pops at the target; a video plays over it additively, so black is see-through. |
| | `card.<id>.cine` | video | the cinematic. It follows the CINE setting, and its own soundtrack plays. A video here makes any move cinematic. |
| | `card.<id>.sfx` | sound | the move's sound, played instead of the shared swing |
| FIGHTERS | field model | another fighter's built-in model (OWN / KAELA / ...), a picture, or a `.glb`/`.gltf` | the field and the character sheet |
| | `unit.<id>.portrait` | picture | tiles, plates and intent faces |
| | `unit.<id>.card` | picture | full card art (cut-ins, sheet) |
| MONSTERS | `unit.<species>.model` | picture | the monster's field cutout and portrait |
| MAPS | battle stages | picture | Take the game's stages out, or add your own with a theme tag. Themes: any, briar, cave, crystal, marsh, keep, mine, ruin, blight, heartwood, boss. |
| SOUNDS | `sfx.<id>` | sound | any game sound, including the `Sfx/...` battle and exploration sounds |
| MUSIC | `music.battle`, `music.atlas`, `music.dungeon` | sound | looped music while in that place |

**Formats.** Pictures: .png (transparent is best) and .jpg. Videos: .mp4 (H.264) everywhere, plus .webm and .mov where the platform decodes them. Sounds: .ogg, .wav and .mp3. Models: .glb, or .gltf with its .bin and textures beside it.

**Stage choice.** A fight normally keeps the game's own stage. If that stage was taken out, or the player added stages for the fight's theme (or "any"), the fight's seed picks among what's left.

**Imported models.** Static meshes only. The loader reads the node hierarchy, positions, normals, UVs, indices and the base-colour texture. Skins and animations are ignored, and Draco or meshopt compression is refused with a message. On the field the battle animates the model procedurally: idle sway, a lean into attacks, a recoil on hits, and a fall when downed. Imported models get the same cel-shading and outline as the built-in rigs.

## Storage

- Files are **copied** into `<persistentDataPath>/Media/files/` and listed in `Media/media.json`. On Windows that is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project\Media`.
- Entries whose file has gone missing are dropped on load.
- **CLEAR** returns a slot to the game's own media and deletes the copy if nothing else uses it.
- **Inbox.** `Media/Inbox` is always listed first in the file browser. On Android it is reachable over USB at `Android/data/<app>/files/Media/Inbox`.
- **Android access.** The browser asks for media permissions the first time it opens, then lists Download, Pictures, Movies, Music and DCIM.
- **Desktop.** You can type or paste a full path into the browser.

## Code

| File | Role |
| --- | --- |
| `Assets/Scripts/Media/MediaLibrary.cs` | Slots, import/clear, loading (textures, async audio, video URLs, models), model swaps, stages, `PickStage` |
| `Assets/Scripts/Media/MediaPanel.cs` | The IMGUI screen (`MediaPanel.Show(tab, select, onClosed)`, `IsOpen`) |
| `Assets/Scripts/Media/MediaBrowser.cs` | In-game file picker |
| `Assets/Scripts/Media/MediaGltf.cs`, `MediaJson.cs` | glTF 2.0 reader |
| `Assets/Resources/AdamsHaven/Fx/ScreenAdd.shader` | Additive draw for imported effect videos |
| Hooks | `BattleMode.Art/MediaArt/FieldPicture/ModelId`, `BattleModels.CreateRig` (imported and swapped models, `PoseImported`), `BattleCinematics.SpawnImportedFx`, `BattleFx.MediaCineFor/PlayUltUrl`, `BattleCzn.MoveSound`, `TowerAudio.Play/PlayClip/Music` |

Tests: `Assets/Editor/BattleCznTests.cs` (`MediaJsonReadsGltfShapes`, `MediaGltfLoadsATriangle`, `MediaSlotsAcceptTheRightKinds`).
