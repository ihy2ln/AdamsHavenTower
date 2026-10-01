"""Export the slim roster the game loads at runtime.

Run:  python export_game_roster.py
Reads ../roster.json (written by build_prompts.py) and writes
  ../../Assets/Resources/AdamsHaven/Roster/tower_roster.json
The game (TowerRoster.cs) loads that file: summon pools, names, stats, element/class/role and abilities.
Unit ids are slugs of the name (for example "pip-thistlewick"); `code` keeps the prompt-pack id (F-1, R-07).
Portraits go in Assets/Resources/AdamsHaven/Roster/Art/<unit id>/<file>.png, named as in the prompt pack
(card-front, casual, chibi-battle, chibi-work, chibi-casual, weapon, ability-passive, ability-ability-1..3,
ability-ultimate, ultimate-cutin, expressions, model-sheet; residents: work-full, casual, chibi-work,
chibi-casual, tool, card, expressions, model-sheet).
"""
import json, re
from pathlib import Path

HERE = Path(__file__).resolve().parent
SRC = HERE.parent / "roster.json"
OUT = HERE.parent.parent / "Assets" / "Resources" / "AdamsHaven" / "Roster" / "tower_roster.json"
RANKS = ["F", "E", "D", "C", "B", "A", "S", "SS", "SSR"]
STAT_KEYS = ["might", "sight", "grit", "charm", "wit", "grace", "luck"]


def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def main():
    data = json.loads(SRC.read_text(encoding="utf-8"))
    units = []
    for c in data:
        hero = c["kind"] == "hero"
        weapon = c.get("weapon") or ["", ""]
        tool = c.get("tool") or ["", ""]
        stats = {k: int(c["stats"][k.capitalize()]) for k in STAT_KEYS}
        units.append({
            "code": c["id"], "id": slug(c["name"]), "kind": c["kind"], "name": c["name"], "epithet": c["epithet"],
            "rank": RANKS.index(c["rank"]) + 1, "rankName": c["rank"], "race": c["race"], "gender": c["gender"],
            "age": c["age"], "body": c["body"], "size": c["size"], "height": c["height"],
            "chest": c["chest_cm"], "waist": c["waist_cm"], "hips": c["hips_cm"], "weight": c["weight_kg"],
            "element": c.get("element", ""), "cls": c.get("cls", ""), "role": c.get("role", ""),
            "primary": c["pri"] if hero else c["stat"], "secondary": c.get("sec", ""), "room": c["room"],
            "palette": c["palette"], "personality": c["personality"], "quote": c["quote"], "origin": c["origin"],
            "weapon": weapon[0] if hero else "", "weaponDesc": weapon[1] if hero else "",
            "passive": c.get("passive", ""), "ability1": c.get("a1", ""), "ability2": c.get("a2", ""),
            "ability3": c.get("a3", ""), "ultimate": c.get("ult", ""),
            "perk": c.get("perk", ""), "tool": tool[0] if not hero else "", "toolDesc": tool[1] if not hero else "",
            "levelCap": c["level_cap"], "stats": stats,
        })
    ids = [u["id"] for u in units]
    assert len(ids) == len(set(ids)), "duplicate unit ids"
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"units": units}, indent=1, ensure_ascii=False), encoding="utf-8")
    heroes = sum(1 for u in units if u["kind"] == "hero")
    print(f"wrote {OUT} : {heroes} heroes, {len(units) - heroes} residents")


if __name__ == "__main__":
    main()
