"""Generate the "missing assets" Codex pack: extra stills for every character, the summon-cinematic keyframes and the
video prompts for the summon cinematic. Cards stay STILL pictures (no animated cards).

Run:  python build_missing_prompts.py
Writes (all under CharacterPrompts/Generated):
  CODEX_MISSING_ASSETS_BRIEF.md        the one file to hand to Codex (procedure + rules + index)
  missing_queue.csv / .jsonl           every image prompt with its target folder and output file
  <heroes|residents>/<unit>/MISSING_PROMPTS.md   the same prompts, next to the character's art
  _summon/SUMMON_SHARED_PROMPTS.md     rank-colour keyframes, charge-up frame and banner art (no characters)
  SUMMON_VIDEO_PROMPTS.md / summon_video_queue.csv   image-to-video prompts (MiniMax H3 in ComfyUI), not made by Codex
Reuses the character data and helpers of build_prompts.py, so edit the _source data and rerun both.
"""
import csv, json, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import build_prompts as bp

GEN = Path(__file__).resolve().parent.parent / "Generated"
HEROES, RESIDENTS = bp.HEROES, bp.RESIDENTS

# Summon colour escalation (GDD 12.9: neutral F up to violet-gold SSR).
RANK_LIGHT = {
    "F": ("dull silver-white", "a thin soft glow, a few dust motes, quiet and plain"),
    "E": ("pale mint-white", "a small gentle bloom of light and a few drifting sparks"),
    "D": ("clear teal-blue", "a modest ring of light and a rising spray of sparks"),
    "C": ("bright sapphire blue", "a clear expanding light ring with crisp rays"),
    "B": ("blue-violet with silver threads", "a strong burst, a double light ring and streaming silver Celestium threads"),
    "A": ("rich purple with silver filigree", "a vivid sweeping burst, wide light ribbons and glowing filigree glyphs"),
    "S": ("magenta and gold", "a dramatic starburst, long gold light ribbons and a rain of gold sparks"),
    "SS": ("violet and gold with floating motifs", "a layered burst, concentric glyph rings, floating motifs and trailing gold light"),
    "SSR": ("radiant violet-gold with prismatic edges", "an epic screen-filling burst, orbiting Celestium crystals, a vast halo of glyphs and falling golden light"),
}
HEART = ("the Celestium Heart, a large faceted blue-silver crystal that floats above a carved dark-timber and slate altar "
         "in a moonlit forest clearing at the edge of Silverwood")
HERO_FILES = [
    ("work-full", "Full-body work outfit", "1024x1536", False, "card-front", 1),
    ("portrait-bust", "Bust portrait (battle outfit)", "1024x1024", True, "card-front", 1),
    ("icon-avatar", "Roster icon avatar", "512x512", False, "card-front", 1),
    ("summon-reveal", "Summon reveal still (vertical)", "1024x1536", False, "card-front", 2),
    ("summon-reveal-wide", "Summon reveal still (wide)", "1536x1024", False, "card-front", 2),
]
RES_FILES = [
    ("portrait-bust", "Bust portrait (work outfit)", "1024x1024", True, "work-full", 1),
    ("icon-avatar", "Roster icon avatar", "512x512", False, "work-full", 1),
    ("summon-reveal", "Summon reveal still (vertical)", "1024x1536", False, "work-full", 2),
    ("summon-reveal-wide", "Summon reveal still (wide)", "1536x1024", False, "work-full", 2),
]
QUEUE = []


def ident_of(c, hero):
    if hero:
        ident = (f"{c['name']}, {c['epithet']}: {c['race']}, {c['gender']}, {c['body']}. {bp.build_text(c)}. "
                 f"{c['element']} element {c['cls']}, {c['role']} role.")
        look = c["look"].replace("Canon: ", "Personality note: ")
    else:
        ident = f"{c['name']}, {c['epithet']}: {c['race']}, {c['gender']}, {c['body']}. {bp.build_text(c)}. Tower resident, rank {c['rank']}."
        look = c["look"]
    anchors = f"Visual anchors that must survive every image: {look} Palette: {', '.join(c['palette'])}."
    return f"{ident} {anchors} {bp.adult_line(c)}"


def hero_prompts(c):
    el_col, el_fx = bp.ELEMENT[c["element"]]
    wn, wd = c["weapon"]
    who = ident_of(c, True)
    fx = f"{c['element']} effects ({el_fx}) in {el_col}, {bp.RANK_FX[c['rank']]}"
    light, burst = RANK_LIGHT[c["rank"]]
    rank_l = bp.RANK_LANG[c["rank"]]
    room = c["room"] if c["room"].startswith("The ") else "the " + c["room"]
    return {
        "work-full": (f"{bp.STYLE} Full-body portrait, vertical 1024x1536, {c['name']} at work in {room} of the tower with a natural working pose "
                      f"(not fighting), warm lantern-lit timber-and-stone interior. {who} Work outfit: {c['work']} No weapon in hand. Rank design language "
                      f"({c['rank']}): {rank_l} Personality shows in the expression: {c['personality'].rstrip('.')}. {bp.NEG}"),
        "portrait-bust": (f"{bp.STYLE} Bust portrait for a hero-detail and dialogue screen: head, shoulders and chest of {c['name']}, front three-quarter view, "
                          f"confident expression, battle outfit ({c['battle']}), soft rim light and a faint {c['element']} glow ({el_fx}). {who} "
                          f"Square 1024x1024, the character only on a genuinely transparent background, no backdrop, no ground, nothing cropped except by the bust cut-off, "
                          f"hair and shoulders fully inside the canvas. {bp.NEG}"),
        "icon-avatar": (f"{bp.STYLE} Square roster-card icon, 512x512: tight head-and-shoulders of {c['name']} filling about 80% of the frame, facing the viewer, "
                        f"readable at 96 px, battle outfit collar and hair silhouette clear. {who} Background: a soft painted gradient of {el_col} with a few "
                        f"{el_fx.split(',')[0]} specks, no scenery, no frame, no border, edge to edge so a UI frame can be laid on top later. {bp.NEG}"),
        "summon-reveal": (f"{bp.STYLE} Summon reveal still, vertical 1024x1536: {c['name']} stepping out of a column of {light} light as they are summoned by the "
                          f"Celestium Heart, full body, dramatic low camera, the arrival pose of a {c['role']} ({bp.ROLE[c['role']]}), holding {wn} ({wd.rstrip('.')}). "
                          f"{who} Battle outfit: {c['battle']} Light language for rank {c['rank']}: {burst}; light colour {light}. Add {fx}. Soft glowing mist at the feet, "
                          f"silhouette strongly lit from behind, calm darker space in the upper third and lower fifth for later title overlay. Painted background only: "
                          f"blurred moonlit forest and the glow of {HEART}. {bp.NEG}"),
        "summon-reveal-wide": (f"{bp.STYLE} Summon reveal still, wide 1536x1024: the same moment as the vertical reveal, {c['name']} stepping out of a {light} light beam in "
                               f"the arrival pose of a {c['role']}, three-quarter to full body, standing right of centre with the glowing Celestium Heart and altar softly visible "
                               f"on the left. {who} Battle outfit: {c['battle']} Holding {wn}. Light language for rank {c['rank']}: {burst}. Add {fx}. Leave the lower fifth calm "
                               f"for a title overlay. {bp.NEG}"),
    }


def res_prompts(c):
    who = ident_of(c, False)
    tn, td = c["tool"]
    light, burst = RANK_LIGHT[c["rank"]]
    return {
        "portrait-bust": (f"{bp.STYLE} Bust portrait for a resident-detail and dialogue screen: head, shoulders and chest of {c['name']}, front three-quarter view, "
                          f"{c['personality'].rstrip('.').lower()} shown in the expression, work outfit ({c['work']}). {who} Square 1024x1024, character only on a "
                          f"genuinely transparent background, hair and shoulders fully inside the canvas. {bp.NEG}"),
        "icon-avatar": (f"{bp.STYLE} Square roster-card icon, 512x512: tight head-and-shoulders of {c['name']} filling about 80% of the frame, facing the viewer, readable at "
                        f"96 px. {who} Work outfit collar and hair silhouette clear. Background: a soft painted gradient in {c['palette'][0]} and cream, no scenery, no frame, "
                        f"no border, edge to edge so a UI frame can be added later. {bp.NEG}"),
        "summon-reveal": (f"{bp.STYLE} Summon reveal still, vertical 1024x1536: {c['name']} arriving through a column of {light} light summoned by the Celestium Heart, full body, "
                          f"a warm surprised-and-friendly arrival pose, holding the tool {tn} ({td.rstrip('.')}). {who} Work outfit: {c['work']} Light language for rank "
                          f"{c['rank']}: {burst}; light colour {light}. Soft glowing mist at the feet, silhouette lit from behind, calm darker space in the upper third and "
                          f"lower fifth for later title overlay. Painted background only: blurred moonlit forest and the glow of {HEART}. {bp.NEG}"),
        "summon-reveal-wide": (f"{bp.STYLE} Summon reveal still, wide 1536x1024: the same moment as the vertical reveal, {c['name']} stepping out of a {light} light beam, three-quarter to "
                               f"full body, standing right of centre with the glowing Celestium Heart and altar softly visible on the left. {who} Work outfit: {c['work']} "
                               f"Holding the tool {tn}. Light language for rank {c['rank']}: {burst}. Leave the lower fifth calm for a title overlay. {bp.NEG}"),
    }


def video_prompt(c, hero):
    light, burst = RANK_LIGHT[c["rank"]]
    base = f"{c['id']}_{bp.slug(c['name'])}"
    if hero:
        el_col, el_fx = bp.ELEMENT[c["element"]]
        act = {"Tank": "plants their feet and braces with a steady, protective stance", "Striker": "snaps into a sharp ready stance with a quick flourish of the weapon",
               "Support": "opens one hand in a warm welcoming gesture", "Controller": "raises the focus item with a sly confident flourish"}[c["role"]]
        fx = f"{c['element']} effects ({el_fx.split(',')[0]}, {el_fx.split(',')[1].strip()}) in {el_col}"
    else:
        act = "looks around in warm surprise, then gives a small friendly wave"
        fx = "a few soft golden sparks"
    text = (f"Vertical 9:16 anime fantasy summon cinematic, 5 seconds, hand-painted 2D look, no text. Start from the {light} rank burst keyframe and end on the reveal still of {c['name']}. "
            f"0-1.5 s: the Celestium Heart pulses and {burst} blooms, camera pushes in slowly. 1.5-3 s: the light column parts and {c['name']} steps forward out of it, "
            f"hair and cloth moving in the updraft, {fx} swirling around the figure. 3-5 s: {c['name']} {act}, camera eases to a stable hero framing, the light settles into a soft "
            f"glow with drifting motes. Keep the face, outfit and proportions identical to the reference, no morphing, no extra limbs, no new characters, smooth easing, final frame matches the reveal still.")
    return dict(id=c["id"], name=c["name"], rank=c["rank"], start_frame=f"_summon/summon-burst-{c['rank']}.png", end_frame=f"{base}_summon-reveal.png",
                output=f"{base}_summon-video.mp4", aspect="9:16", seconds=5, prompt=text)


def shared_prompts():
    out = []
    ranks = [(r, *RANK_LIGHT[r]) for r in bp.RANKS]
    out.append(("summon-charge", "Heart charge-up keyframe (neutral)", "1024x1536 and 1536x1024",
                f"{bp.STYLE} Summon charge-up keyframe: {HEART}, seen from a low camera, the crystal just beginning to glow with a cold white-blue light, thin light threads drawn in "
                f"from the surrounding trees, calm empty space above the altar. No characters, no figures, no text. Render it twice: vertical 1024x1536 and wide 1536x1024. {bp.NEG}"))
    for r, light, burst in ranks:
        out.append((f"summon-burst-{r}", f"Rank {r} burst keyframe", "1024x1536 and 1536x1024",
                    f"{bp.STYLE} Summon burst keyframe for rank {r}: {HEART} releasing {burst}; the light colour is {light} and must be clearly distinct from the neighbouring ranks "
                    f"(escalating from neutral silver at F to violet-gold at SSR, this is rank {r} of F, E, D, C, B, A, S, SS, SSR). A vertical column of light rises from the crystal, "
                    f"leaving the centre of the column open and bright so a character can be composited in. No characters, no figures, no text. Render it twice: vertical 1024x1536 "
                    f"and wide 1536x1024. {bp.NEG}"))
    banners = [("banner-standard", "Standard banner key art", "Warm gold-and-silver light fanning out behind a ring of floating character-card silhouettes of varied races and weapons, none identifiable."),
               ("banner-featured", "Featured banner key art", "Violet and gold light, a single large empty pedestal of light at the centre flanked by two smaller ones, floating crystal shards, festive but elegant."),
               ("banner-pick-your-hero", "Pick-Your-Hero banner key art", "A glowing blue-silver crystal key and a lock of light at the centre, a ring of faint hero silhouettes around it, a sense of choosing a target."),
               ("banner-resident", "Resident banner key art", "Cozy warm lantern light, a stack of crates, tools, a loaf of bread and a work apron silhouette floating around the Heart's glow, humble and welcoming.")]
    for fn, title, body in banners:
        out.append((fn, title, "2048x768",
                    f"{bp.STYLE} Wide banner key art for the Heart summon screen, 2048x768. {body} Painted Adams Haven palette, strong central focus, calm space on the left and right "
                    f"thirds for authored text overlays. No readable characters, no faces, no text. {bp.NEG}"))
    return out


def write_unit(c, hero, specs, prompts):
    base = f"{c['id']}_{bp.slug(c['name'])}"
    folder = GEN / ("heroes" if hero else "residents") / base
    lines = [f"# {c['id']} · {c['name']}: missing assets", "",
             f"Folder: `{folder.as_posix()}`. Read `{base}_{'card-front' if hero else 'work-full'}.png` and `PROMPTS.md` there first. Save each output in this folder with the exact file name below.", ""]
    for name, title, size, transp, ref, prio in specs:
        fn = f"{base}_{name}.png"
        ref_fn = f"{base}_{ref}.png"
        prompt = f"{bp.REF_RULE} {prompts[name]}"
        QUEUE.append(dict(id=c["id"], name=c["name"], kind="hero" if hero else "resident", priority=prio, file=fn, folder=folder.as_posix(), size=size,
                          transparent=transp, reference=ref_fn, prompt=prompt))
        lines += [f"## {title}", "", f"`{fn}` · {size}{' · transparent PNG' if transp else ''} · priority {prio} · attach `{ref_fn}`", "", "```text", prompt, "```", ""]
    (folder / "MISSING_PROMPTS.md").write_text("\n".join(lines), encoding="utf-8")


def main():
    for c in HEROES:
        write_unit(c, True, HERO_FILES, hero_prompts(c))
    for c in RESIDENTS:
        write_unit(c, False, RES_FILES, res_prompts(c))
    with open(GEN / "missing_queue.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.DictWriter(f, fieldnames=list(QUEUE[0].keys()))
        wr.writeheader()
        wr.writerows(QUEUE)
    with open(GEN / "missing_queue.jsonl", "w", encoding="utf-8") as f:
        for q in QUEUE:
            f.write(json.dumps(q, ensure_ascii=False) + "\n")

    shared = shared_prompts()
    (GEN / "_summon").mkdir(exist_ok=True)
    sl = ["# Shared summon art (no characters)", "", "Save every file in `S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated/_summon/`. "
          "The 10 keyframes (charge-up and 9 ranks) are each made in a vertical and a wide version: add `-wide` before `.png` for the wide one (for example `summon-burst-SSR.png` and `summon-burst-SSR-wide.png`).", ""]
    for fn, title, size, prompt in shared:
        sl += [f"## {title}", "", f"`{fn}.png` · {size}", "", "```text", prompt, "```", ""]
    (GEN / "_summon" / "SUMMON_SHARED_PROMPTS.md").write_text("\n".join(sl), encoding="utf-8")

    vids = [video_prompt(c, True) for c in HEROES] + [video_prompt(c, False) for c in RESIDENTS]
    with open(GEN / "summon_video_queue.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.DictWriter(f, fieldnames=list(vids[0].keys()))
        wr.writeheader()
        wr.writerows(vids)
    charge = ("Vertical 9:16 anime fantasy summon cinematic, 3 seconds, hand-painted 2D look, no text. The Celestium Heart crystal above its carved altar slowly brightens from a faint glow, "
              "thin threads of light stream in from the moonlit trees, a soft pulse builds, camera pushing in gently. No characters. Loopable end frame equal to start frame brightness.")
    vl = ["# Summon cinematic: video prompts", "",
          "Codex only makes stills. Videos come from MiniMax H3 (ComfyUI), image-to-video with a start and an end frame, per `battle-motion-direction`. 9:16 first, then rerun at 16:9 from the `-wide` stills.", "",
          "## Shared charge-up (3 s)", "", f"Start frame `_summon/summon-charge.png`.", "", "```text", charge, "```", "",
          "## Per unit (5 s each, 60 clips)", "", "Start frame `_summon/summon-burst-<rank>.png`, end frame the unit's `_summon-reveal.png`, output `<unit>_summon-video.mp4` saved in the unit folder. "
          "Full list with columns in `summon_video_queue.csv`.", ""]
    for v in vids:
        vl += [f"### {v['id']} {v['name']} (rank {v['rank']})", "", f"start `{v['start_frame']}` · end `{v['end_frame']}` · out `{v['output']}`", "", "```text", v["prompt"], "```", ""]
    (GEN / "SUMMON_VIDEO_PROMPTS.md").write_text("\n".join(vl), encoding="utf-8")

    brief = f"""# Codex brief: make the missing character art (stills only)

You are finishing the Adams Haven Tower Mode art pack. All paths are under `S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated`. The pack has 36 heroes (`heroes/<ID>_<slug>/`) and 24 residents (`residents/<ID>_<slug>/`). Each folder already holds the finished art and a `PROMPTS.md`. Your job is to add the images that are still missing, into the folder of the character they belong to.

## Scope
- **Stills only.** No animated cards, no GIFs, no video. Cards stay still pictures (the existing `card-front` / `card`). Videos are made elsewhere from your stills.
- {len(QUEUE)} character images ({len(HEROES)} heroes x {len(HERO_FILES)} + {len(RESIDENTS)} residents x {len(RES_FILES)}) plus {len(shared) + 10} shared summon images ({len(shared)} prompts: 10 keyframes in vertical and wide, 4 banners).

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
"""
    (GEN / "CODEX_MISSING_ASSETS_BRIEF.md").write_text(brief, encoding="utf-8")
    print("character images", len(QUEUE), "shared", len(shared), "video clips", len(vids))


if __name__ == "__main__":
    main()
