# Codex brief: make the missing character art (stills only)

You are finishing the Adams Haven Tower Mode art pack. All paths are under `S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated`. The pack has 36 heroes (`heroes/<ID>_<slug>/`) and 24 residents (`residents/<ID>_<slug>/`). Each folder already holds the finished art and a `PROMPTS.md`. Your job is to add the images that are still missing, into the folder of the character they belong to.

## Scope
- **Stills only.** No animated cards, no GIFs, no video. Cards stay still pictures (the existing `card-front` / `card`). Videos are made elsewhere from your stills.
- 276 character images (36 heroes x 5 + 24 residents x 4) plus 24 shared summon images (14 prompts: 10 keyframes in vertical and wide, 4 banners).

## For each character folder
1. Open `MISSING_PROMPTS.md` in the folder. It lists every file to make with the full prompt, size, transparency and the reference file.
2. **Look at the reference image first** (`card-front.png` for heroes, `work-full.png` for residents) and read `PROMPTS.md`. Attach the reference to every generation. Identity must match exactly: face, hair, species traits, build, palette, outfit design.
3. Generate each file one call at a time with the prompt as written, then **view the result** and check: same character, correct outfit for that file, nothing cropped, no text or letters anywhere, no duplicate figures, adult and non-explicit, transparent where stated. Regenerate or write a `-v2` sibling (keep the original) if it fails.
4. Save as the exact file name given (for example `B-1_kestrel_icon-avatar.png`) in that same folder. Keep the generator's native canvas if it differs from the requested size, but keep the aspect ratio.
5. Append each accepted file to that folder's `PROMPTS.md` (prompt, reference, canvas, status), matching the existing entries.

## Per-file purpose (so you can judge quality)
| File | Used for |
| --- | --- |
| `work-full` (heroes only) | Heroes staff tower rooms; hero detail screen outfit tab |
| `portrait-bust` | Hero/resident detail and dialogue portrait, transparent |
| `icon-avatar` | Small roster/People card avatar and summon 10-pull grid; must read at 96 px |
| `summon-reveal` / `-wide` | The unit-reveal frame of the summon cinematic (portrait and landscape); also the end frame of the video |

## Shared summon art (do after the characters)
Open `_summon/SUMMON_SHARED_PROMPTS.md`. Make the charge-up frame, the 9 rank bursts (F to SSR, colour must clearly escalate from silver to violet-gold) and 4 banner arts, each vertical and wide where stated, saved in `_summon/`.

## Priority order
Priority 1 files first for all 60 characters (`work-full`, `portrait-bust`, `icon-avatar`), then priority 2 (`summon-reveal`, `summon-reveal-wide`), then `_summon`. `missing_queue.csv` / `missing_queue.jsonl` hold the same prompts with `folder`, `file`, `priority`, `reference` columns for batch use.

## Hard rules
- Every character is 21+, tasteful and fully opaque costume coverage, non-explicit. If a generation is refused, cover the costume a little more and retry; do not switch generators.
- No text, letters, numbers, watermark, logo, frame, border or UI in any image.
- Never overwrite an existing file. Do not edit `PROMPTS.md` entries that already exist; only append.
- Female characters keep their stated exaggerated gacha proportions; copy the build from the reference, not from your own idea of the species.
- Report at the end: files made, files retried, files that failed and why.

## Not for Codex
`SUMMON_VIDEO_PROMPTS.md` and `summon_video_queue.csv` are the image-to-video prompts for MiniMax H3 (start frame `_summon/summon-burst-<rank>.png`, end frame the unit's `summon-reveal`). Do not try to make video.
