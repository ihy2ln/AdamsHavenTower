"""Town building reference prompts (TOWER_MODE_GDD 19.2, TT 10.3.3).

Writes TownPrompts/: one reference picture per town building type and rank band, as text-to-image prompts, plus a
batch queue. The owner generates the pictures; the 3D shells are then modelled from them (barn pipeline + Blender)
to replace the greybox blocks in TowerTownView. Edit this file for art direction, then re-run it.

Usage: python Tools/build_town_prompts.py
"""
import csv, json, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "TownPrompts"

STYLE = ("High-end Korean fantasy RPG and gacha illustration, polished hand-painted anime rendering, crisp clean "
         "linework, rich dimensional cel shading, luminous material highlights, cohesive Adams Haven world palette "
         "of dark weathered timber, slate and stone with moonlit blue-silver and violet Celestium accents.")
NEGATIVE = ("No text, no letters, no numbers, no signs with writing, no watermark, no logo, no frame or border, no UI, "
            "no people, no cropped walls or roofs, no photorealism, no fisheye, no extreme perspective.")
# The TOWN view camera: orthographic, pitched 35 degrees down, turned 45 degrees, so every reference matches it.
CAMERA = ("Isometric three-quarter exterior view of a single building, seen from above at about 35 degrees, turned "
          "45 degrees so two walls show equally, orthographic look with parallel edges and no vanishing points, the "
          "whole building in frame with air around it, flat neutral pale-grey ground and a plain soft gradient "
          "background, soft daylight from the upper left, light shadow on the ground to the lower right.")

# Rank bands match the Tower's footprint split: F-D (1 tile), C-B (2 tiles), A-SSR (3 tiles).
BANDS = [
    ("F-D", "village", 1, "humble village build: rough-sawn weathered dark timber, wattle or simple plank walls, "
                          "mossy thatch or split-shingle roof, one small window, a plain door, no ornament"),
    ("C-B", "town", 2, "established town build: fitted dark timber framing over cut slate and stone, a shingled or "
                       "slate roof with a dormer, glazed leaded windows, a carved lintel, one modest banner pole with "
                       "a plain cloth banner, small Celestium lantern by the door giving a faint blue-silver glow"),
    ("A-SSR", "city", 3, "grand city build: dressed stone and dark polished timber, a tall layered slate roof with "
                         "finials, large leaded windows, carved stone trim, iron and bronze fittings, several "
                         "Celestium lanterns and inlaid blue-silver crystal veins glowing softly, a proud silhouette"),
]

# District -> building types. Each type: (id, name, what it is, footprint shape, distinctive features).
DISTRICTS = [
    ("residential", "Residential", [
        ("cottage", "Cottage", "a family home", "square", "a chimney with a thin smoke wisp, a small garden patch and fence, flower boxes, laundry line"),
        ("rowhouse", "Row House", "a terrace of joined homes", "long rectangle along the street", "repeated doors and windows in a row, shared roofline, steps down to the street"),
        ("manor", "Townhouse Manor", "a wealthy family's home", "square with a walled yard", "a walled courtyard with a tree, a balcony, a tower corner, ornate gate"),
    ]),
    ("market", "Market (commercial)", [
        ("stall", "Market Stall", "a covered trading stall", "small rectangle, open front", "a striped awning, crates and barrels of goods, hanging wares, counter facing the street"),
        ("inn", "Inn", "an inn and tavern", "L-shape around a yard", "a hanging shield-shaped sign with no writing, lantern-lit porch, barrels, a stable lean-to, warm window glow"),
        ("bazaar", "Bazaar Hall", "a covered market hall", "wide rectangle with open arcade", "open arcade of arches along the front, many awnings, piled goods, a central dome or lantern roof"),
    ]),
    ("industry", "Industry", [
        ("workshop", "Workshop", "a craft workshop", "square with a lean-to", "a wide double door, a chimney, lumber and tool racks against the wall, a water trough"),
        ("mill", "Mill", "a grain or lumber mill", "rectangle with a wheel", "a large wooden waterwheel or windmill sails, a loading dock, sacks and planks stacked outside"),
        ("foundry", "Foundry", "a smithy and foundry", "wide rectangle", "two tall brick chimneys with a soft glow at the vents, an anvil yard, ore carts, an open forge mouth glowing orange"),
    ]),
    ("arcane", "Arcane (civic)", [
        ("shrine", "Celestium Shrine", "a small shrine to the Heart", "small square with steps", "a floating or mounted Celestium crystal, steps up to an open-fronted shrine, hanging silver chimes"),
        ("library", "Library", "a library and study hall", "rectangle with a round reading tower", "tall narrow windows, a round reading tower with a conical roof, a sundial or orrery in the yard"),
        ("bathhouse", "Bathhouse", "a public bathhouse", "square with a courtyard pool", "a domed roof with steam vents, a tiled courtyard pool visible through an arch, warm lantern light"),
    ]),
    ("defence", "Defence", [
        ("gatehouse", "Gatehouse", "a town gatehouse on the ring road", "rectangle spanning a road", "an arched passage for the road through it, a portcullis, two short flanking towers, a walkway"),
        ("wallsegment", "Wall Segment", "a straight stretch of town wall", "long thin rectangle", "a crenellated parapet, a walkway, one small buttress, torch brackets"),
        ("watchtower", "Watchtower", "a watchtower", "small square, tall", "a tall slender tower with a lookout platform, a brazier on top, arrow slits"),
    ]),
]

# The Tower itself: the town's landmark, drawn once per band of Heart rank.
TOWER = [
    ("F-D", "a young tower: a squat five-storey stone keep of dark fitted stone with a timber top floor, two arched gates facing left and right, a small dim blue crystal at its crown"),
    ("C-B", "a grown tower: a twelve-storey keep of dark dressed stone banded with timber galleries, two fortified gates with portcullises facing left and right, a bright blue-silver Celestium crystal shining at its crown"),
    ("A-SSR", "a great tower: a soaring twenty-storey keep of dark stone and polished timber with buttresses and lantern galleries, grand twin gates facing left and right, a huge radiant blue-violet Celestium crystal at its crown with light spilling down the walls"),
]


def building_prompt(district_name, type_name, what, shape, features, band_desc, tiles):
    return " ".join([
        STYLE, CAMERA,
        f"Subject: {type_name}, {what}, in the {district_name} district of a fantasy tower town; footprint {shape}, "
        f"sized for a {tiles}-by-{tiles} tile lot. Build: {band_desc}. Distinctive features: {features}.",
        "Materials read clearly as a game asset reference: distinct wall, trim and roof colours, clean silhouette, "
        "no clutter beyond the features listed.",
        NEGATIVE,
    ])


def main():
    OUT.mkdir(exist_ok=True)
    queue = []
    index = ["# Town building reference pictures (GDD 19.2)", "",
             "Generated by `Tools/build_town_prompts.py`. One reference per building type and rank band; the 3D shells "
             "are modelled from these to replace the TOWN view's greybox blocks. Rank bands follow the Tower's footprint "
             "split: F-D = 1 tile, C-B = 2 tiles, A-SSR = 3 tiles.", "",
             "Canvas: 1536 x 1152 landscape, no alpha needed (the ground and background are cropped away when modelling).",
             "Camera in every prompt: isometric three-quarter from 35 degrees above, turned 45 degrees (the TOWN view).", "",
             "Generate the F-D picture of a type first and attach it to the C-B and A-SSR prompts of that type as an "
             "identity reference (same building family, grown up), as the character packs do.", "",
             "| District | Type | F-D | C-B | A-SSR |", "| --- | --- | --- | --- | --- |"]
    for did, dname, types in DISTRICTS:
        ddir = OUT / did
        ddir.mkdir(exist_ok=True)
        for tid, tname, what, shape, features in types:
            tdir = ddir / tid
            tdir.mkdir(exist_ok=True)
            lines = [f"# {tname} ({dname})", "", f"{what}; footprint {shape}.", ""]
            cells = []
            for band, word, tiles, band_desc in BANDS:
                file = f"{tid}_{band.replace('-', '').lower()}.png"
                prompt = building_prompt(dname, tname, what, shape, features, band_desc, tiles)
                refs = "" if band == "F-D" else f"{tid}_fd.png"
                lines += [f"## {band} ({word}, {tiles}x{tiles} tiles) -> `{file}`", "",
                          f"Canvas 1536x1152. References: {refs or 'none'}.", "", f"> {prompt}", ""]
                queue.append({"file": f"TownPrompts/{did}/{tid}/{file}", "district": did, "type": tid, "band": band,
                              "tiles": tiles, "canvas": "1536x1152", "transparent": False, "references": refs, "prompt": prompt})
                cells.append(f"`{file}`")
            (tdir / "PROMPTS.md").write_text("\n".join(lines), encoding="utf-8")
            index.append(f"| {dname} | {tname} | " + " | ".join(cells) + " |")
    tdir = OUT / "tower"
    tdir.mkdir(exist_ok=True)
    lines = ["# The Tower (town landmark)", "", "The Celestium Heart's tower as seen from the town, one picture per Heart rank band. "
             "Its footprint is the 5x5 tile centre of the ring; the gates face west and east (left and right).", ""]
    for band, desc in TOWER:
        file = f"tower_{band.replace('-', '').lower()}.png"
        prompt = " ".join([STYLE, CAMERA.replace("a single building", "a single tall tower"),
                           f"Subject: the Celestium Heart's tower at the centre of a fantasy tower town, {desc}. "
                           "Dark weathered timber, slate and fitted stone; blue-silver and violet Celestium light.",
                           NEGATIVE])
        refs = "" if band == "F-D" else "tower_fd.png"
        lines += [f"## {band} -> `{file}`", "", f"Canvas 1152x1536 portrait. References: {refs or 'none'}.", "", f"> {prompt}", ""]
        queue.append({"file": f"TownPrompts/tower/{file}", "district": "tower", "type": "tower", "band": band, "tiles": 5,
                      "canvas": "1152x1536", "transparent": False, "references": refs, "prompt": prompt})
    (tdir / "PROMPTS.md").write_text("\n".join(lines), encoding="utf-8")
    index += ["| Tower | Tower | `tower_fd.png` | `tower_cb.png` | `tower_assr.png` |", "",
              f"{len(queue)} prompts in all. Batch rows: `prompt_queue.csv` / `prompt_queue.jsonl`.", "",
              "## Shared style line", "", f"> {STYLE}", "", "## Camera line", "", f"> {CAMERA}", "",
              "## Negative block", "", f"> {NEGATIVE}", ""]
    (OUT / "README.md").write_text("\n".join(index), encoding="utf-8")
    with open(OUT / "prompt_queue.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.DictWriter(f, fieldnames=list(queue[0].keys()))
        wr.writeheader()
        wr.writerows(queue)
    with open(OUT / "prompt_queue.jsonl", "w", encoding="utf-8") as f:
        for q in queue:
            f.write(json.dumps(q, ensure_ascii=False) + "\n")
    print(len(queue), "prompts ->", OUT)


if __name__ == "__main__":
    main()
