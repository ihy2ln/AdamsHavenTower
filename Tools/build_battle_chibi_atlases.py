"""Pack the anime dual-look battle clips into Unity atlases for BattleMode.

Source: S:/AI/Game/art/character_cards/chibi-roster-dual-look-v1/<character>/battle/clips/<clip>/NNN.png
Output: Assets/Resources/AdamsHaven/BattleChibi/<id>/<clip>.png
Layout: 2048x2048, 6 columns x 4 rows, 24 frames, row 0 at the top. Every frame of a
character shares one scale, horizontally centred and foot-aligned to the cell bottom.
"""
import os
from PIL import Image

SRC = "S:/AI/Game/art/character_cards/chibi-roster-dual-look-v1"
DST = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "AdamsHaven", "BattleChibi")
ROSTER = {"kaela": "kaela-stormfang", "amara": "amara-the-alluring-empress", "clarity": "clarity-lockhart",
          "daisy": "daisy-bonfire-wildheart", "elara": "elara-vellum", "ghislaine": "ghislaine-dedoldia",
          "helda": "helda-frostmane"}
CLIPS = ["idle", "walk_in_place"]
SIZE, COLS, ROWS, PAD = 2048, 6, 4, 4
CELL_W, CELL_H = SIZE / COLS, SIZE / ROWS

for uid, folder in ROSTER.items():
    frames = {}
    box = None
    for clip in CLIPS:
        d = os.path.join(SRC, folder, "battle", "clips", clip)
        frames[clip] = [Image.open(os.path.join(d, f)).convert("RGBA")
                        for f in sorted(os.listdir(d)) if f.endswith(".png")]
        for im in frames[clip]:
            b = im.getbbox()
            box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    # Centre horizontally on the canvas middle so the feet stay put between frames.
    canvas_w = frames[CLIPS[0]][0].width
    half = max(canvas_w / 2 - box[0], box[2] - canvas_w / 2)
    left, right = canvas_w / 2 - half, canvas_w / 2 + half
    scale = min((CELL_W - 2 * PAD) / (right - left), (CELL_H - 2 * PAD) / (box[3] - box[1]))
    os.makedirs(os.path.join(DST, uid), exist_ok=True)
    for clip in CLIPS:
        atlas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        for i, im in enumerate(frames[clip][:COLS * ROWS]):
            crop = im.crop((int(left), box[1], int(round(right)), box[3]))
            crop = crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), Image.LANCZOS)
            cx, cy = (i % COLS) * CELL_W, (i // COLS) * CELL_H
            atlas.alpha_composite(crop, (int(cx + (CELL_W - crop.width) / 2), int(cy + CELL_H - PAD - crop.height)))
        atlas.save(os.path.join(DST, uid, clip + ".png"), optimize=True)
    print(uid, "scale %.3f" % scale, "box", box)
