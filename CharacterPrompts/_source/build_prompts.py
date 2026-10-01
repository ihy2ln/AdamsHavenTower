"""Generate the Adams Haven Tower Mode character prompt pack.

Run:  python build_prompts.py
Reads heroes_1..3.py, residents_data.py and measurements.py; writes markdown, CSV, JSON and a prompt queue one folder up.
Hero fields: id name epithet rank race gender age body size height element cls role pri sec room
  look palette[3] battle casual work weapon(name,desc) passive a1 a2 a3 ult personality quote origin bg
Resident fields: id name epithet rank race gender age body size height stat room look palette[3]
  casual work tool(name,desc) perk personality quote origin bg
Measurements (the "3 sizes": chest/bust, waist, hips) live in measurements.py.
"""
import csv, hashlib, json, re, sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from heroes_1 import HEROES_1
from heroes_2 import HEROES_2
from heroes_3 import HEROES_3
from residents_data import RESIDENTS
from measurements import MEAS

OUT = Path(__file__).resolve().parent.parent
HEROES = HEROES_1 + HEROES_2 + HEROES_3
# Stylized gacha proportions for every female character: fuller bust and hips, much narrower waist.
FEMALE_EXAGGERATION = (1.16, 0.86, 1.20, 1.05)  # chest, waist, hips, weight multipliers
for _c in HEROES + RESIDENTS:
    if _c["gender"] == "female":
        _ch, _w, _hp, _kg = MEAS[_c["id"]]
        MEAS[_c["id"]] = (round(_ch * FEMALE_EXAGGERATION[0]), round(_w * FEMALE_EXAGGERATION[1]),
                          round(_hp * FEMALE_EXAGGERATION[2]), round(_kg * FEMALE_EXAGGERATION[3]))
STATS = ["Might", "Sight", "Grit", "Charm", "Wit", "Grace", "Luck"]
RANKS = ["F", "E", "D", "C", "B", "A", "S", "SS", "SSR"]
MIN_AGE = 21
ROOM_STAT = {
    "Stone Well": {"Might", "Grit"}, "Lumber Mill": {"Might", "Grit"}, "Stone Quarry": {"Might", "Grit", "Wit"},
    "The Forge": {"Might", "Grit"}, "Kitchen": {"Grit", "Charm"}, "Farmstead": {"Grit", "Grace"},
    "Argent Market": {"Charm", "Luck", "Grace"}, "The Frosted Mug": {"Charm", "Grace", "Wit"},
    "Silverbrook Adventure Guild": {"Sight", "Grace", "Charm"}, "Deck Hall": {"Wit", "Grace"},
    "Hearth Nursery": {"Grace", "Charm"}, "Barn": {"Luck", "Grit"}, "Heart Sanctuary": {"Luck", "Charm", "Wit", "Grit"},
}
BANDS = {"F": (2, 5, 20), "E": (4, 8, 30), "D": (7, 12, 40), "C": (11, 17, 50), "B": (16, 24, 60),
         "A": (22, 32, 70), "S": (30, 42, 80), "SS": (40, 55, 90), "SSR": (52, 70, 100)}
RANK_LANG = {
    "F": "Secondhand, patched, hand-mended clothing and gear; matte cloth, wood, rope and plain iron; no polished metal, no ornament, no glow.",
    "E": "Tidy, honest everyday gear; neat stitching, simple leather, a single small decorative touch; still no glow.",
    "D": "Competent working gear with small brass and steel fittings, clean cut, one signature color accent; faint element glow only on the weapon.",
    "C": "Proper armor pieces and heraldic cloth, matching set, confident tailoring; clear element accents and a soft weapon glow.",
    "B": "Refined tailoring with enamel-colored accents, layered fabrics; first hairline threads of blue-silver Celestium in the trim; noticeable element aura.",
    "A": "Elegant layered design with luminous accents; visible Celestium filigree; strong element FX and a graceful silhouette.",
    "S": "Silver-trimmed masterwork with ornate engraving; confident heroic silhouette; polished materials; vivid FX that frame the figure.",
    "SS": "Silver and violet refinement on every hem and plate; floating motifs; elaborate layered costume; striking aura and particle trails.",
    "SSR": "Mythic silhouette with contained Celestium crystals orbiting or embedded in the costume; grand aura, ornate details everywhere, dramatic lighting.",
}
RANK_FX = {
    "F": "tiny subtle effects", "E": "small subtle effects", "D": "modest effects", "C": "clear effects",
    "B": "strong effects", "A": "vivid sweeping effects", "S": "dramatic sweeping effects",
    "SS": "elaborate layered effects with floating motifs", "SSR": "epic screen-filling effects with contained Celestium crystal light",
}
ELEMENT = {
    "Fire": ("ember red, orange, gold", "embers, flame ribbons, heat shimmer"),
    "Wind": ("teal, pale green, silver", "spiraling leaves, gust lines, floating feathers"),
    "Earth": ("amber, moss green, slate", "stone shards, crystals, drifting dust and petals"),
    "Lightning": ("electric yellow, cyan, indigo", "branching arcs, static sparks, crackling halos"),
    "Water": ("aqua, deep blue, pearl", "ribbons of water, bubbles, droplets, moonlit spray"),
    "Light": ("warm gold, white, soft rose", "sun motes, feather-like light, halo rings"),
    "Dark": ("violet, black, magenta", "shadow smoke, star specks, dark sigils and ribbons"),
}
ROLE = {
    "Tank": "planted, wide protective stance, shield or body forward, calm determined expression",
    "Striker": "dynamic mid-action attack pose with forward momentum, intense focused expression",
    "Support": "open graceful stance, one hand extended toward allies, warm encouraging expression",
    "Controller": "poised casting stance with the focus item raised, one hand gesturing, sly confident expression",
}
# height class -> (word, chibi head count, relative chibi scale applied in Unity, not in the image prompt)
SIZE = {"Small": ("compact", "about 3 heads tall", 0.85), "Medium": ("average", "about 3.5 heads tall", 1.00),
        "Large": ("towering", "about 4 heads tall", 1.20)}
STYLE = ("High-end Korean fantasy RPG and gacha illustration, polished hand-painted anime rendering, crisp clean linework, "
         "rich dimensional cel shading, luminous material highlights, cohesive Adams Haven world palette of dark weathered timber, "
         "slate and stone with moonlit blue-silver and violet Celestium accents.")
NEG = ("No text, no letters, no numbers, no watermark, no logo, no frame or border, no UI, no extra or missing limbs, "
       "no fused fingers, no cropped limbs, no photorealism, no 3D render look.")
REF_RULE = ("Use the attached reference image only for the character's face, hair, species traits, markings, palette and costume design; "
            "do not copy its pose, framing or background.")
CHIBI = ("Chibi proportions ({heads}), polished 2D anime game illustration, crisp outlines, restrained cel shading, the body shape and "
         "silhouette of the measurements above translated faithfully into chibi form. "
         "Front-facing relaxed A-stance: arms held diagonally about 25 degrees away from the sides, elbows gently bent, hands open, "
         "legs straight with a small gap, feet fully visible. Keep hair, tails, wings, capes, jewelry and clothing clear of shoulder, "
         "elbow, hip and knee joints so parts can be separated for later animation. Character only on a genuinely transparent background, "
         "no ground, no cast shadow, no scenery, no text, nothing cropped. Fill the canvas height with the character.")
LONGLIVED = " (apparent adult; long-lived race)"
QUEUE = []


def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def h(key, *parts):
    return int(hashlib.md5(("|".join((key,) + parts)).encode()).hexdigest(), 16)


def make_stats(c, primary_key):
    lo, hi, cap = BANDS[c["rank"]]
    pri, sec = c[primary_key], c.get("sec")
    out = {}
    for s in STATS:
        if s == pri:
            out[s] = hi
        elif s == sec:
            out[s] = round(hi - (hi - lo) * 0.25)
        else:
            span = max(1, round((hi - lo) * 0.5))
            out[s] = lo + h(c["id"], s) % (span + 1)
    return out, cap


def cm_in(cm):
    return f"{round(cm / 2.54)} in"


def silhouette(ch, w, hp):
    big = max(ch, hp)
    if hp / w >= 1.5 and ch / w >= 1.35:
        return "exaggerated hourglass: full bust, very narrow waist, wide curvy hips"
    if w / big <= 0.76 and abs(ch - hp) / big <= 0.10:
        return "hourglass"
    if hp > ch * 1.07 and w / hp <= 0.85:
        return "pear-shaped, fuller hips than chest"
    if ch > hp * 1.07:
        return "inverted-triangle, broad chest and shoulders"
    if w / big >= 0.9:
        return "straight and sturdy, little waist taper"
    return "athletic, gently tapered waist"


def build_text(c):
    ch, w, hp, kg = MEAS[c["id"]]
    sil = silhouette(ch, w, hp)
    style = " Stylized gacha-art proportions, deliberately exaggerated yet graceful, keeping the stated height and leg length." if sil.startswith("exaggerated") else ""
    return (f"Build: {sil}; chest {ch} cm, waist {w} cm, hips {hp} cm; {c['height']} cm tall; {kg} kg.{style}").rstrip(".")


def age_phrase(c):
    return f"age {c['age']} (21+)" + (LONGLIVED if c["age"] >= 80 else "")


def adult_line(c):
    return f"{c['name']} is a clearly adult character, {age_phrase(c)}. Tasteful confident silhouette, fully opaque coverage, non-explicit."


def block(c, step, title, filename, size, prompt, ref=None, transparent=False):
    attach = f"attach `{ref}`" if ref else "no attachment (text only)"
    if ref:
        prompt = f"{REF_RULE} {prompt}"
    QUEUE.append(dict(id=c["id"], name=c["name"], step=step, title=title, file=filename, size=size,
                      transparent=transparent, reference=ref or "", prompt=prompt))
    return f"### {step}. {title}\n\n`{filename}` · {size}{' · transparent PNG' if transparent else ''} · {attach}\n\n```text\n{prompt}\n```\n"


def quick_start(first_file, steps):
    t = ["## Quick start (copy, paste, run in this order)", "",
         f"1. Run step 1 and keep the best result as `{first_file}`. Reject it and rerun if the face, hair, species traits or build drift from the sheet.",
         "2. Run every other step in any order. Each step lists the one file to attach; attach only that file.",
         "3. Do not add text to images; names, stats and card frames are added later by overlay.",
         "4. Prompts are complete as written. There is nothing to fill in.", "",
         "| Step | Output | Size | Attach |", "| --- | --- | --- | --- |"]
    t += steps
    t.append("")
    return t


def hero_md(c):
    assert c["age"] >= MIN_AGE, c["id"]
    stats, cap = make_stats(c, "pri")
    el_col, el_fx = ELEMENT[c["element"]]
    sz_word, heads, chibi_scale = SIZE[c["size"]]
    ch, wa, hp, kg = MEAS[c["id"]]
    ident = (f"{c['name']}, {c['epithet']}: {c['race']}, {c['gender']}, {c['body']}. {build_text(c)}. "
             f"{c['element']} element {c['cls']}, {c['role']} role.")
    adult = adult_line(c)
    look = c["look"].replace("Canon: ", "Personality note: ")
    anchors = f"Visual anchors that must survive every image: {look} Palette: {', '.join(c['palette'])}."
    rank_l = RANK_LANG[c["rank"]]
    wn, wd = c["weapon"]
    base = f"{c['id']}_{slug(c['name'])}"
    first = f"{base}_card-front.png"
    fx = f"{c['element']} effects ({el_fx}) in {el_col}, {RANK_FX[c['rank']]}"
    pose = ROLE[c["role"]]
    room_stat = ROOM_STAT.get(c["room"], set())
    fit = "matched" if c["pri"] in room_stat else f"off-stat (room keys {sorted(room_stat)})"

    full = (f"{STYLE} Full-body portrait for a card front, vertical 1024x1536. {ident} {anchors} {adult} "
            f"Battle outfit: {c['battle']} Rank design language ({c['rank']}): {rank_l} "
            f"Holding the weapon {wn}: {wd} Pose: {pose}. {fx}. Background: {c['bg']} "
            f"Rim lighting, strong focal lighting on the character, clear silhouette. Cover art only. {NEG}")
    casual = (f"{STYLE} Full-body casual-life illustration, vertical 1024x1536, relaxed natural pose showing personality ({c['personality'].rstrip('.')}). "
              f"{ident} {anchors} {adult} Casual outfit: {c['casual']} Soft warm interior of a timber-and-stone tower home, lantern light, "
              f"a hint of moonlit blue through a window. The weapon is not held. {NEG}")
    chibi_b = (f"{STYLE} Full-body animation-model source of {c['name']}. {ident} {anchors} {adult} "
               f"Battle outfit: {c['battle']} Rank language: {rank_l} The weapon {wn} is slung on the back or hip, clear of every arm joint. "
               f"{CHIBI.format(heads=heads)} {NEG}")
    chibi_w = (f"{STYLE} Full-body animation-model source of {c['name']} in tower work clothes for the {c['room']}. "
               f"{ident} {anchors} {adult} Work outfit: {c['work']} No weapon. {CHIBI.format(heads=heads)} {NEG}")
    chibi_c = (f"{STYLE} Full-body animation-model source of {c['name']} in relaxed casual clothing for tower idle and rest animations. "
               f"{ident} {anchors} {adult} Casual outfit: {c['casual']} No weapon. {CHIBI.format(heads=heads)} {NEG}")
    weapon = (f"{STYLE} A single weapon prop for a character-collection screen: {wn}. {wd} It belongs to {c['name']} ({c['race']} {c['cls']}, {c['element']} element, "
              f"rank {c['rank']}). Rank design language: {rank_l} {fx}. Show the whole weapon diagonally across the frame with a small inset close-up of the grip and "
              f"signature detail; strong rim lighting; centered; genuinely transparent background, no hands, no character, no ground shadow. {NEG}")
    ult = (f"{STYLE} Ultimate cut-in illustration, wide 2048x1024. {c['name']} ({c['race']} {c['cls']}, {c['element']}), dynamic knee-up shot with dramatic perspective, "
           f"speed lines and a focused burst of {c['element']} effects ({el_fx}). {c['ult']} {ident} {adult} Battle outfit: {c['battle']} {anchors} Eyes fierce and readable, "
           f"hair and cloth whipping, the weapon {wn} in frame. Intensity: {RANK_FX[c['rank']]}. Leave the left third calmer for a title graphic. {NEG}")
    expr = (f"{STYLE} Expression sheet, bust-up (head and shoulders, front 3/4 view) of {c['name']}, 8 panels in 2 rows of 4, equal square panels, plain neutral backdrop. "
            f"Row 1 satisfaction faces: furious scowl, sad worried frown, neutral blank, friendly smile. Row 2: ecstatic open-mouth grin, determined battle face, "
            f"wounded wince, victorious smirk. Same character in every panel. {ident} {anchors} {adult} Wear the casual outfit: {c['casual']} {NEG}")
    sheet = (f"{STYLE} Orthographic model sheet of {c['name']} on a plain light-grey background: full-body front, side and back views in the battle outfit ({c['battle']}), "
             f"standing in a neutral A-pose, with identical proportions across all three views, plus a height chart on the right showing {c['name']} at exactly {c['height']} cm "
             f"beside a plain 170 cm reference silhouette. {ident} {anchors} {adult} Small color swatches of the palette at the bottom. {NEG}")

    cards = []
    for label, key, extra in [("Passive", "passive", ""), ("Ability 1", "a1", ""), ("Ability 2", "a2", ""), ("Ability 3", "a3", ""),
                              ("Ultimate", "ult", " This is the ultimate: use the most dramatic composition and strongest effects.")]:
        nm, body = (s.strip() for s in c[key].split(":", 1))
        cards.append((label, nm,
            f"{STYLE} Ability-card illustration for \"{nm}\" by {c['name']} ({c['race']} {c['cls']}, {c['element']} element). Card art only, vertical 768x1024. "
            f"Ability: {body} Show {c['name']} in action. {ident} {anchors} {adult} Battle outfit: {c['battle']} Weapon: {wn}. {fx}. "
            f"Background: {c['bg']} The art must read clearly at thumbnail size.{extra} Leave the lower 22% of the frame visually calm for the text panel. {NEG}"))

    steps = [
        (1, "Full-body card art (battle)", first, "1024x1536", full, None, False),
        (2, "Full-body casual art", f"{base}_casual.png", "1024x1536", casual, first, False),
        (3, "Chibi model source (battle)", f"{base}_chibi-battle.png", "1024x1024", chibi_b, first, True),
        (4, "Chibi model source (tower work)", f"{base}_chibi-work.png", "1024x1024", chibi_w, first, True),
        (5, "Chibi model source (casual / idle)", f"{base}_chibi-casual.png", "1024x1024", chibi_c, first, True),
        (6, "Weapon solo", f"{base}_weapon.png", "1024x1024", weapon, first, True),
    ]
    for i, (label, nm, p) in enumerate(cards, start=7):
        steps.append((i, f"Ability card art: {label} ({nm})", f"{base}_ability-{slug(label)}.png", "768x1024", p, first, False))
    steps += [
        (12, "Ultimate cut-in", f"{base}_ultimate-cutin.png", "2048x1024", ult, first, False),
        (13, "Expression sheet (8 faces)", f"{base}_expressions.png", "1600x800", expr, first, False),
        (14, "Model sheet and height chart", f"{base}_model-sheet.png", "2048x1024", sheet, first, False),
    ]
    qs_rows = [f"| {s[0]} | {s[2]} | {s[3]}{', transparent' if s[6] else ''} | {s[5] or 'none'} |" for s in steps]

    lines = [
        f"# {c['id']} · {c['name']} — {c['epithet']}", "",
        f"**Rank {c['rank']}** · {c['element']} · {c['cls']} · **{c['role']}** · {c['race']} · age {c['age']} (21+) · {c['height']} cm", "",
        "## Character sheet", "", "| Field | Value |", "| --- | --- |",
        f"| Race / gender / age | {c['race']} / {c['gender']} / {c['age']}{LONGLIVED if c['age'] >= 80 else ''} (21+) |",
        f"| Body type | {c['body']} |",
        f"| **Size (chest / waist / hips)** | **{ch} / {wa} / {hp} cm** ({cm_in(ch)} / {cm_in(wa)} / {cm_in(hp)}), {silhouette(ch, wa, hp)} |",
        f"| Height / weight | {c['height']} cm / {kg} kg (height class {c['size']}, chibi {heads}, Unity scale {chibi_scale:.2f}) |",
        f"| Element / class / role | {c['element']} / {c['cls']} / {c['role']} |",
        f"| Preferred tower room | {c['room']} ({fit}) |",
        f"| Visual anchors | {look} |", f"| Palette | {', '.join(c['palette'])} |",
        f"| Battle outfit | {c['battle']} |", f"| Casual outfit | {c['casual']} |", f"| Work outfit | {c['work']} |",
        f"| Weapon | **{wn}**: {wd} |", f"| Personality | {c['personality']} |",
        f"| Quote | \"{c['quote']}\" |", f"| Origin | {c['origin']} |", "",
        "### Tower stats (rank band draft, GDD 6.7)", "",
        "| " + " | ".join(STATS) + " | Level cap |", "|" + " --- |" * (len(STATS) + 1),
        "| " + " | ".join(f"**{stats[s]}**" if s == c["pri"] else str(stats[s]) for s in STATS) + f" | {cap} |", "",
        f"Primary stat **{c['pri']}** ({stats[c['pri']]}): +{stats[c['pri']]}% output in a room that matches {c['pri']}. Secondary: {c['sec']}.", "",
        "### Abilities", "",
        f"- **Passive** — {c['passive']}", f"- **Ability 1** — {c['a1']}", f"- **Ability 2** — {c['a2']}",
        f"- **Ability 3** — {c['a3']}", f"- **Ultimate** — {c['ult']}", "", "---", "",
    ]
    lines += quick_start(first, qs_rows)
    lines += ["---", "", "## Prompts", "", "Shared rules: `../00_SHARED_STYLE.md`.", ""]
    for step, title, fn, size, p, ref, tr in steps:
        lines.append(block(c, step, title, fn, size, p, ref, tr))
    lines += ["## Revision log", "", "| Version | Change requested | Preserve | Change only | Reference |", "| --- | --- | --- | --- | --- |", "| v1 | | | | |", ""]
    return base, "\n".join(lines), stats, cap, fit


def res_md(c):
    assert c["age"] >= MIN_AGE, c["id"]
    stats, cap = make_stats(c, "stat")
    sz_word, heads, chibi_scale = SIZE[c["size"]]
    ch, wa, hp, kg = MEAS[c["id"]]
    ident = (f"{c['name']}, {c['epithet']}: {c['race']}, {c['gender']}, {c['body']}. {build_text(c)}. Tower resident, rank {c['rank']}.")
    adult = adult_line(c)
    anchors = f"Visual anchors that must survive every image: {c['look']} Palette: {', '.join(c['palette'])}."
    rank_l = RANK_LANG[c["rank"]]
    tn, td = c["tool"]
    base = f"{c['id']}_{slug(c['name'])}"
    first = f"{base}_work-full.png"
    room_stat = ROOM_STAT.get(c["room"], set())
    fit = "matched" if c["stat"] in room_stat else f"off-stat (room keys {sorted(room_stat)})"
    work_full = (f"{STYLE} Full-body portrait, vertical 1024x1536, {c['name']} at work in the {c['room']} with a natural working pose, warm lantern-lit tower interior. {ident} {anchors} {adult} "
                 f"Work outfit: {c['work']} Holding the tool {tn}: {td} Rank design language ({c['rank']}): {rank_l} Personality shows in the expression: {c['personality'].rstrip('.')}. Background: {c['bg']} {NEG}")
    casual_full = (f"{STYLE} Full-body casual-life illustration, vertical 1024x1536, {c['name']} relaxing in a cozy timber-and-stone tower home. {ident} {anchors} {adult} Casual outfit: {c['casual']} {NEG}")
    chibi_w = (f"{STYLE} Full-body animation-model source of {c['name']} in tower work clothes for the {c['room']}. {ident} {anchors} {adult} Work outfit: {c['work']} "
               f"No tool in the hands (the tool is a separate prop). {CHIBI.format(heads=heads)} {NEG}")
    chibi_c = (f"{STYLE} Full-body animation-model source of {c['name']} in casual clothing for idle and rest animations. {ident} {anchors} {adult} Casual outfit: {c['casual']} "
               f"{CHIBI.format(heads=heads)} {NEG}")
    tool = (f"{STYLE} A single work-tool prop for a resident profile screen: {tn}. {td} It belongs to {c['name']} ({c['epithet']}). Rank design language: {rank_l} Show the whole object diagonally "
            f"with a small inset close-up of a signature detail, centered, genuinely transparent background, no hands, no character, no ground shadow. {NEG}")
    card = (f"{STYLE} Resident card illustration, vertical 768x1024: {c['name']} from the waist up, friendly 3/4 pose with the tool {tn} visible, work outfit ({c['work']}), the {c['room']} softly blurred behind. "
            f"{ident} {anchors} {adult} Background: {c['bg']} Leave the lower 22% calm for the text panel. {NEG}")
    expr = (f"{STYLE} Expression sheet, bust-up front 3/4 of {c['name']}, 5 panels in a row matching tower satisfaction levels 0-19, 20-39, 40-59, 60-79 and 80-100: furious scowl, sad worried frown, neutral blank, "
            f"friendly smile, ecstatic open-mouth grin. Square panels, plain neutral backdrop, same character every panel. {ident} {anchors} {adult} Wear the casual outfit: {c['casual']} {NEG}")
    sheet = (f"{STYLE} Orthographic model sheet of {c['name']} on a plain light-grey background: full-body front, side and back views in the work outfit ({c['work']}), neutral A-pose, identical proportions across views, "
             f"plus a height chart showing {c['name']} at exactly {c['height']} cm beside a plain 170 cm reference silhouette. {ident} {anchors} {adult} Palette swatches at the bottom. {NEG}")
    steps = [
        (1, "Full-body work portrait", first, "1024x1536", work_full, None, False),
        (2, "Full-body casual art", f"{base}_casual.png", "1024x1536", casual_full, first, False),
        (3, "Chibi model source (tower work)", f"{base}_chibi-work.png", "1024x1024", chibi_w, first, True),
        (4, "Chibi model source (casual / idle)", f"{base}_chibi-casual.png", "1024x1024", chibi_c, first, True),
        (5, "Tool solo", f"{base}_tool.png", "1024x1024", tool, first, True),
        (6, "Resident card art", f"{base}_card.png", "768x1024", card, first, False),
        (7, "Expression sheet (5 satisfaction faces)", f"{base}_expressions.png", "1600x400", expr, first, False),
        (8, "Model sheet and height chart", f"{base}_model-sheet.png", "2048x1024", sheet, first, False),
    ]
    qs_rows = [f"| {s[0]} | {s[2]} | {s[3]}{', transparent' if s[6] else ''} | {s[5] or 'none'} |" for s in steps]
    lines = [
        f"# {c['id']} · {c['name']} — {c['epithet']}", "",
        f"**Resident, Rank {c['rank']}** · {c['race']} · age {c['age']} (21+) · {c['height']} cm · key stat **{c['stat']}**", "",
        "## Character sheet", "", "| Field | Value |", "| --- | --- |",
        f"| Race / gender / age | {c['race']} / {c['gender']} / {c['age']}{LONGLIVED if c['age'] >= 80 else ''} (21+) |",
        f"| Body type | {c['body']} |",
        f"| **Size (chest / waist / hips)** | **{ch} / {wa} / {hp} cm** ({cm_in(ch)} / {cm_in(wa)} / {cm_in(hp)}), {silhouette(ch, wa, hp)} |",
        f"| Height / weight | {c['height']} cm / {kg} kg (height class {c['size']}, chibi {heads}, Unity scale {chibi_scale:.2f}) |",
        f"| Tower room | {c['room']} ({fit}) |",
        f"| Visual anchors | {c['look']} |", f"| Palette | {', '.join(c['palette'])} |",
        f"| Casual outfit | {c['casual']} |", f"| Work outfit | {c['work']} |",
        f"| Tool | **{tn}**: {td} |", f"| Tower perk | {c['perk']} |",
        f"| Personality | {c['personality']} |", f"| Quote | \"{c['quote']}\" |", f"| Origin | {c['origin']} |", "",
        "### Tower stats (rank band draft)", "", "| " + " | ".join(STATS) + " | Level cap |", "|" + " --- |" * (len(STATS) + 1),
        "| " + " | ".join(f"**{stats[s]}**" if s == c["stat"] else str(stats[s]) for s in STATS) + f" | {cap} |", "",
        f"Key stat **{c['stat']}** ({stats[c['stat']]}): +{stats[c['stat']]}% output in a room that matches {c['stat']}. Residents never fight.", "", "---", "",
    ]
    lines += quick_start(first, qs_rows)
    lines += ["---", "", "## Prompts", "", "Shared rules: `../00_SHARED_STYLE.md`.", ""]
    for step, title, fn, size, p, ref, tr in steps:
        lines.append(block(c, step, title, fn, size, p, ref, tr))
    lines += ["## Revision log", "", "| Version | Change requested | Preserve | Change only | Reference |", "| --- | --- | --- | --- | --- |", "| v1 | | | | |", ""]
    return base, "\n".join(lines), stats, cap, fit


def shared_md():
    t = ["# Shared style and rules for all 60 character prompt packs", "",
         "Applies to the 36 heroes and 24 residents. Every per-character file builds on these rules.", "",
         "## Hard rules", "",
         f"- **Every character is {MIN_AGE}+.** The age is stated in each sheet and each prompt. Long-lived races are written as apparent adults. Styling is tasteful, confident, fully opaque coverage and non-explicit, matching `ADAMS_HAVEN_CHARACTER_BRIEF_TEMPLATE.md`. If an image tool refuses a prompt, tone the costume down rather than routing it through another generator.",
         "- **No text in images.** Names, stats, quotes, card frames and numbers are authored overlays. Reserve calm space where noted.",
         "- **Identity first.** Generate step 1 first. Attach it to every other step (each prompt already carries the instruction to copy identity only, not pose or background).",
         "- **Transparent assets** (chibi, weapon, tool) must have real alpha, no ground, no shadow, nothing cropped.",
         "- **Rig-friendly chibi.** Relaxed A-stance, arms about 25 degrees from the torso; hair, tails, wings, capes and jewelry clear of shoulder, elbow, hip and knee joints. Wings and long capes are drawn as separate ribbons/layers that never cover the joints.",
         "- Log each accepted output (prompt, reference file, canvas, status) in a `PROMPTS.md` next to the images, as in the `adams-haven-layered-character-cards` skill.", "",
         "## Master style line (starts every prompt)", "", f"> {STYLE}", "",
         "## Negative block (ends every prompt)", "", f"> {NEG}", "",
         "## The 3 sizes: chest, waist, hips", "",
         "Every character has a measurement triple (chest or bust / waist / hips, in cm and inches) plus height and weight, listed in the character sheet, in `roster.csv`, and stated in the build line of every prompt. "
         "A silhouette label (hourglass, pear-shaped, inverted-triangle, straight and sturdy, athletic) is computed from the triple. The measurements are body-shape data for consistent proportions, not captions: they are never rendered as text.", "",
         "Height class (Small, Medium, Large) is a separate, secondary field used for the chibi head count and the Unity scale:", "",
         "| Height class | Height range | Chibi height | Unity chibi scale |", "| --- | --- | --- | --- |",
         "| Small | about 118 to 158 cm | about 3 heads tall | 0.85 |", "| Medium | about 165 to 182 cm | about 3.5 heads tall | 1.00 |",
         "| Large | about 185 to 230 cm | about 4 heads tall | 1.20 |", "",
         "## Rank design language (costume complexity by rank)", "", "| Rank | Costume and FX language |", "| --- | --- |"]
    for r in RANKS:
        t.append(f"| {r} | {RANK_LANG[r]} FX: {RANK_FX[r]}. |")
    t += ["", "## Element visual language", "", "| Element | Color cues | FX motifs |", "| --- | --- | --- |"]
    for e, (col, fx) in ELEMENT.items():
        t.append(f"| {e} | {col} | {fx} |")
    t += ["", "## Role pose cues", "", "| Role | Pose and expression |", "| --- | --- |"]
    for r, p in ROLE.items():
        t.append(f"| {r} | {p} |")
    t += ["", "## Asset list per hero (14 images)", "",
          "1 full-body card art · 2 casual full body · 3 chibi battle · 4 chibi tower-work · 5 chibi casual · 6 weapon solo · 7 to 11 ability cards (passive, 3 abilities, ultimate) · 12 ultimate cut-in · 13 expression sheet (8 faces) · 14 model sheet and height chart.",
          "", "## Asset list per resident (8 images)", "",
          "1 full-body work portrait · 2 casual full body · 3 chibi tower-work · 4 chibi casual · 5 tool solo · 6 resident card art · 7 expression sheet (5 satisfaction faces) · 8 model sheet and height chart.", ""]
    return "\n".join(t)


def readme_md():
    t = ["# Adams Haven Tower Mode: character prompt pack", "",
         f"Ready-to-run Codex image prompts for **36 heroes** (4 per rank, F to SSR) and **24 residents**: 504 hero prompts plus 192 resident prompts, {len(QUEUE)} in total. All characters are {MIN_AGE}+. Design source: `../TOWER_MODE_GDD.md` sections 6 and 8.", "",
         "## Fastest way to use it", "",
         "1. Skim `00_SHARED_STYLE.md` once (about 2 minutes).",
         "2. Open one character file (for example `heroes/B-1_kestrel.md`). Its **Quick start** table lists every output, size and the single file to attach.",
         "3. Run step 1 (no attachment), keep the best result, then run steps 2 onward attaching that file. Prompts are complete; nothing needs editing.",
         "4. For batch or scripted use, `prompt_queue.csv` / `prompt_queue.jsonl` hold every prompt with its output file name, size, transparency flag and reference file.",
         "5. To change a character, edit `_source/heroes_*.py`, `_source/residents_data.py` or `_source/measurements.py`, then run `python _source/build_prompts.py` to regenerate everything.", "",
         "## Notes", "",
         "- **Size = chest / waist / hips** (plus height and weight) per character, stated in every prompt; a Small/Medium/Large height class is kept only for chibi head count and Unity scale.",
         "- **Female characters use deliberately exaggerated, stylized gacha proportions** (fuller bust and hips, much narrower waist), applied by `FEMALE_EXAGGERATION` in `_source/build_prompts.py` on top of the base table in `measurements.py`. Costumes stay fully opaque and non-explicit.",
         "- Elements follow GDD 6.6 (7 elements). Hero element counts: Fire 6, the other six 5 each. Roles 9 each.",
         "- Kestrel (B-1) and Sable (B-2) follow the Adams Haven VN canon. Linnet, the VN's future unlock, is not in this pack. The existing card roster (Kaela, Clarity, Daisy, Elara, Ghislaine, Helda, JD, others) is separate and untouched.",
         "- Stats are deterministic drafts from the GDD 6.7 rank bands. Ability text is design intent; battle numbers are not set.", "",
         "## Heroes", "", "| ID | Name | Rank | Element | Class | Role | Race | Chest/Waist/Hips cm | Primary | Room |", "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for c in HEROES:
        ch, w, hp, _ = MEAS[c["id"]]
        t.append(f"| {c['id']} | [{c['name']}](heroes/{c['id']}_{slug(c['name'])}.md) | {c['rank']} | {c['element']} | {c['cls']} | {c['role']} | {c['race']} | {ch}/{w}/{hp} | {c['pri']} | {c['room']} |")
    t += ["", "## Residents", "", "| ID | Name | Rank | Race | Chest/Waist/Hips cm | Key stat | Room |", "| --- | --- | --- | --- | --- | --- | --- |"]
    for c in RESIDENTS:
        ch, w, hp, _ = MEAS[c["id"]]
        t.append(f"| {c['id']} | [{c['name']}](residents/{c['id']}_{slug(c['name'])}.md) | {c['rank']} | {c['race']} | {ch}/{w}/{hp} | {c['stat']} | {c['room']} |")
    t.append("")
    return "\n".join(t)


def main():
    (OUT / "heroes").mkdir(exist_ok=True)
    (OUT / "residents").mkdir(exist_ok=True)
    rows, js, mism = [], [], []
    for kind, group, fn in (("hero", HEROES, hero_md), ("resident", RESIDENTS, res_md)):
        for c in group:
            base, md, stats, cap, fit = fn(c)
            (OUT / ("heroes" if kind == "hero" else "residents") / f"{base}.md").write_text(md, encoding="utf-8")
            ch, w, hp, kg = MEAS[c["id"]]
            key = c.get("pri") or c.get("stat")
            rows.append([kind, c["id"], c["name"], c["rank"], c["race"], c["gender"], c["age"], ch, w, hp, c["height"], kg, silhouette(ch, w, hp),
                         c["size"], c.get("element", ""), c.get("cls", ""), c.get("role", ""), key, c["room"], fit, *[stats[s] for s in STATS], cap])
            js.append({**c, "kind": kind, "chest_cm": ch, "waist_cm": w, "hips_cm": hp, "weight_kg": kg, "stats": stats, "level_cap": cap})
            if fit != "matched":
                mism.append((c["id"], fit))
    with open(OUT / "roster.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["kind", "id", "name", "rank", "race", "gender", "age", "chest_cm", "waist_cm", "hips_cm", "height_cm", "weight_kg", "silhouette", "height_class",
                     "element", "class", "role", "key_stat", "room", "room_fit", *STATS, "level_cap"])
        wr.writerows(rows)
    (OUT / "roster.json").write_text(json.dumps(js, indent=2, ensure_ascii=False), encoding="utf-8")
    with open(OUT / "prompt_queue.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.DictWriter(f, fieldnames=list(QUEUE[0].keys()))
        wr.writeheader()
        wr.writerows(QUEUE)
    with open(OUT / "prompt_queue.jsonl", "w", encoding="utf-8") as f:
        for q in QUEUE:
            f.write(json.dumps(q, ensure_ascii=False) + "\n")
    (OUT / "00_SHARED_STYLE.md").write_text(shared_md(), encoding="utf-8")
    (OUT / "README.md").write_text(readme_md(), encoding="utf-8")
    print("heroes", len(HEROES), "residents", len(RESIDENTS), "prompts", len(QUEUE))
    print("min age", min(c["age"] for c in HEROES + RESIDENTS))
    print("silhouettes", dict(Counter(r[12] for r in rows)))
    print("hero sizes", dict(Counter(c["size"] for c in HEROES)), "res sizes", dict(Counter(c["size"] for c in RESIDENTS)))
    print("room mismatches", mism)
    longest = max(QUEUE, key=lambda q: len(q["prompt"]))
    print("longest prompt chars", len(longest["prompt"]), longest["id"], longest["step"])


if __name__ == "__main__":
    main()
