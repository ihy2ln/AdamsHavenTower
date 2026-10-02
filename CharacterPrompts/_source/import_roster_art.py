"""Copy finished roster art into the game.

Run:  python import_roster_art.py              game subset, downscaled (default)
      python import_roster_art.py --all        every image, full size (about 1.7 GB: not recommended in Resources)
      python import_roster_art.py --dry-run    only list what would be copied

Reads  CharacterPrompts/Generated/{heroes,residents}/<code>_<slug>/<code>_<slug>_<name>.png
Writes Assets/Resources/AdamsHaven/Roster/Art/<unit id>/<name>.png   (loaded by TowerRoster.Portrait)
Unit ids come from tower_roster.json (slug of the character name). A "-v2" file replaces the original of the same
name. Default subset: card-front or card (cards), work-full/casual (full body), chibi-battle/chibi-work/chibi-casual
(tower and battle models), all downscaled to at most 1024 px on the long edge. Everything under Resources ships in
builds, so the rest (ability cards, cut-ins, sheets, weapons) stays out until a screen needs it.
"""
import json, sys
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
GEN = HERE.parent / "Generated"
ROOT = HERE.parent.parent / "Assets" / "Resources" / "AdamsHaven" / "Roster"
SUBSET = {"card-front", "card", "work-full", "casual", "chibi-battle", "chibi-work", "chibi-casual",
          "icon-avatar", "portrait-bust", "summon-reveal", "summon-reveal-wide"}
MAX_EDGE = 1024
roster = json.loads((ROOT / "tower_roster.json").read_text(encoding="utf-8"))["units"]
by_code = {u["code"]: u["id"] for u in roster}
dry, everything = "--dry-run" in sys.argv, "--all" in sys.argv
copied = skipped = 0
for kind in ("heroes", "residents"):
    for folder in sorted((GEN / kind).glob("*")) if (GEN / kind).exists() else []:
        code = folder.name.split("_", 1)[0]
        unit = by_code.get(code)
        if not unit:
            print("no roster unit for", folder.name)
            continue
        prefix = folder.name + "_"
        chosen = {}
        for png in sorted(folder.glob("*.png")):
            stem = png.stem[len(prefix):] if png.stem.startswith(prefix) else png.stem
            v2 = stem.endswith("-v2")
            name = stem[:-3] if v2 else stem
            if not everything and name not in SUBSET:
                continue
            if name not in chosen or v2:
                chosen[name] = png
        for name, png in chosen.items():
            dest = ROOT / "Art" / unit / (name + ".png")
            if dest.exists() and dest.stat().st_mtime >= png.stat().st_mtime:
                skipped += 1
                continue
            copied += 1
            print(("would write " if dry else "write ") + f"Art/{unit}/{name}.png  <- {png.name}")
            if dry:
                continue
            dest.parent.mkdir(parents=True, exist_ok=True)
            img = Image.open(png)
            if not everything and max(img.size) > MAX_EDGE:
                scale = MAX_EDGE / max(img.size)
                img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
            img.save(dest, optimize=True)
print(f"{'would write' if dry else 'wrote'} {copied}, already up to date {skipped}")
