"""Slice the sleek_fantasy_controls_v1 state sheets into per-state PNGs.

Run:  python slice_sheets.py [source_dir]
Finds each state by its visible alpha bounds (the sheets are not on a regular grid), trims it,
then NORMALIZES every state of one control: each state is uniformly scaled so its solid body matches
the first state's body size, and all states share one canvas with the body centered on the same
pivot. Unity Sprite Swap therefore keeps the control exactly in place and the same size when the
state changes (glows may extend past the body). Icon sheets are only trimmed and centered, not
rescaled. Originals are never modified.

Outputs (next to this script, outside Assets so Unity does not import them until you move them):
  sliced/<sheet>/<sheet>__<state>.png   one PNG per state
  preview/<sheet>.png                   contact sheet on a dark checkerboard for review
  manifest.json                         per-state source bounds, canvas size, body size, glow margin
"""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"S:\AI\Game\Game Assets\ui\sleek_fantasy_controls_v1")
OUT = Path(__file__).resolve().parent
CORE = 60      # alpha used to find cells (ignores faint glow halos)
EDGE = 6       # alpha used to trim each state (keeps soft glow)
SOLID = 200    # alpha treated as the solid body of a control
NO_NORMALIZE = {"icons_dock", "icons_utility"}
STATE4 = ["normal", "pressed", "selected", "disabled"]
SHEETS = {
    "btn_primary_states": [STATE4], "btn_secondary_states": [STATE4], "btn_violet_states": [STATE4],
    "btn_dock_states": [["normal", "pressed", "active", "disabled"]],
    "btn_tab_states": [["inactive", "pressed", "active", "disabled"]],
    "btn_heart_states": [["idle", "pressed", "ready", "disabled"]],
    "btn_round_states": [["normal", "pressed", "active", "disabled"], ["pip_alert", "pip_ready", "pip_new"]],
    "btn_fab_states": [["collect_normal", "collect_pressed", "collect_selected", "collect_disabled"],
                       ["rush_normal", "rush_pressed", "rush_selected", "rush_disabled"]],
    "btn_window_modes": [["small", "medium_selected", "fullscreen", "close"]],
    "icons_dock": [["build", "people", "heart", "map", "battle"]],
    "icons_utility": [["pause", "speed_x1", "speed_x2", "steward", "alerts"],
                      ["goals", "menu", "collect", "rush", "drawer_arrow"]],
    "chip_resource_9slice": [["chip"]], "panel_frame_9slice": [["frame"]], "panel_header_9slice": [["header"]],
    "bar_progress": [["frame"], ["fill"]],
}


def runs(mask):
    """Contiguous True runs of a 1-D bool array as (start, end_exclusive)."""
    idx = np.flatnonzero(mask)
    if idx.size == 0:
        return []
    splits = np.flatnonzero(np.diff(idx) > 1)
    starts = np.r_[idx[0], idx[splits + 1]]
    ends = np.r_[idx[splits], idx[-1]] + 1
    return list(zip(starts.tolist(), ends.tolist()))


def merge_to(rs, n):
    """Merge neighbouring runs across the smallest gaps until exactly n runs remain."""
    rs = [list(r) for r in rs]
    while len(rs) > n:
        gaps = [rs[i + 1][0] - rs[i][1] for i in range(len(rs) - 1)]
        i = int(np.argmin(gaps))
        rs[i][1] = rs[i + 1][1]
        del rs[i + 1]
    return rs


def body_bbox(sub):
    """Bounding box (x0,y0,x1,y1) of the solid body, ignoring thin glow spikes and flares."""
    m = sub > SOLID
    cols, rows = m.sum(axis=0), m.sum(axis=1)
    cx = np.flatnonzero(cols > 0.15 * cols.max())
    ry = np.flatnonzero(rows > 0.15 * rows.max())
    return int(cx.min()), int(ry.min()), int(cx.max()) + 1, int(ry.max()) + 1


def checker(w, h, s=16):
    yy, xx = np.mgrid[0:h, 0:w]
    c = ((xx // s + yy // s) % 2) * 14 + 22
    return np.dstack([c, c, c + 4, np.full_like(c, 255)]).astype(np.uint8)


def slice_sheet(name, rows):
    img = Image.open(SRC / f"{name}.png").convert("RGBA")
    a = np.asarray(img)[:, :, 3]
    h, w = a.shape
    band_runs = merge_to(runs((a > CORE).any(axis=1)), len(rows))
    if len(band_runs) != len(rows):
        raise RuntimeError(f"{name}: found {len(band_runs)} rows, expected {len(rows)}")
    items = []   # (state, box)
    for bi, (y0, y1) in enumerate(band_runs):
        n = len(rows[bi])
        col_runs = merge_to(runs((a[y0:y1] > CORE).any(axis=0)), n)
        if len(col_runs) != n:
            raise RuntimeError(f"{name} row {bi}: found {len(col_runs)} cells, expected {n}")
        top = (band_runs[bi - 1][1] + y0) // 2 if bi else 0
        bot = (y1 + band_runs[bi + 1][0]) // 2 if bi + 1 < len(band_runs) else h
        for ci, (x0, x1) in enumerate(col_runs):
            left = (col_runs[ci - 1][1] + x0) // 2 if ci else 0
            right = (x1 + col_runs[ci + 1][0]) // 2 if ci + 1 < len(col_runs) else w
            sub = a[top:bot, left:right]
            ys, xs = np.nonzero(sub > EDGE)
            box = (left + int(xs.min()), top + int(ys.min()), left + int(xs.max()) + 1, top + int(ys.max()) + 1)
            bb = body_bbox(sub)
            bb_in_box = (bb[0] - (box[0] - left), bb[1] - (box[1] - top), bb[2] - (box[0] - left), bb[3] - (box[1] - top))
            items.append((rows[bi][ci], bi, box, bb_in_box))
    out_dir = OUT / "sliced" / name
    out_dir.mkdir(parents=True, exist_ok=True)
    manifest = []
    previews = []
    normalize = name not in NO_NORMALIZE
    for bi in range(len(rows)):
        row = [it for it in items if it[1] == bi]
        base_w = row[0][3][2] - row[0][3][0]
        base_h = row[0][3][3] - row[0][3][1]
        prepared = []
        for state, _, box, bb in row:
            crop = img.crop(box)
            bw, bh = bb[2] - bb[0], bb[3] - bb[1]
            sc = (base_w / bw) if normalize else 1.0
            if abs(sc - 1.0) > 0.002:
                crop = crop.resize((max(1, round(crop.width * sc)), max(1, round(crop.height * sc))), Image.LANCZOS)
            bcx, bcy = (bb[0] + bb[2]) / 2 * sc, (bb[1] + bb[3]) / 2 * sc
            prepared.append((state, box, crop, bcx, bcy, sc, (bw * sc, bh * sc)))
        if normalize:
            el = int(np.ceil(max(p[3] for p in prepared)))
            et = int(np.ceil(max(p[4] for p in prepared)))
            er = int(np.ceil(max(p[2].width - p[3] for p in prepared)))
            eb = int(np.ceil(max(p[2].height - p[4] for p in prepared)))
            cw, ch = el + er, et + eb
        else:
            cw = max(p[2].width for p in prepared); ch = max(p[2].height for p in prepared)
        for state, box, crop, bcx, bcy, sc, body_wh in prepared:
            canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
            if normalize:
                ox, oy = int(round(el - bcx)), int(round(et - bcy))
            else:
                ox, oy = (cw - crop.width) // 2, (ch - crop.height) // 2
            canvas.alpha_composite(crop, (ox, oy))
            fname = f"{name}__{state}.png"
            canvas.save(out_dir / fname)
            previews.append(canvas)
            manifest.append(dict(sheet=name, state=state, file=f"sliced/{name}/{fname}", source_box=list(box),
                                 canvas=[cw, ch], scale_applied=round(sc, 4), body=[round(body_wh[0]), round(body_wh[1])],
                                 pivot=[el, et] if normalize else [cw // 2, ch // 2],
                                 glow_margin=[(cw - round(body_wh[0])) // 2, (ch - round(body_wh[1])) // 2]))
    # contact sheet
    pad = 16
    row_items = [previews[sum(len(r) for r in rows[:i]):sum(len(r) for r in rows[:i + 1])] for i in range(len(rows))]
    pw = max(sum(p.width for p in ri) + pad * (len(ri) + 1) for ri in row_items)
    rowh = [max(p.height for p in ri) for ri in row_items]
    ph = sum(rowh) + pad * (len(rows) + 1)
    sheet = Image.fromarray(checker(pw, ph))
    y = pad
    k = 0
    for i, r in enumerate(rows):
        x = pad
        for _ in r:
            sheet.alpha_composite(previews[k], (x, y))
            x += previews[k].width + pad
            k += 1
        y += rowh[i] + pad
    (OUT / "preview").mkdir(exist_ok=True)
    sheet.convert("RGB").save(OUT / "preview" / f"{name}.png")
    return manifest


def main():
    full = []
    for name, rows in SHEETS.items():
        try:
            m = slice_sheet(name, rows)
            full += m
            print(f"ok   {name}: {len(m)} states")
        except Exception as e:
            print(f"FAIL {name}: {e}")
    (OUT / "manifest.json").write_text(json.dumps(full, indent=2), encoding="utf-8")
    print("total states", len(full))


if __name__ == "__main__":
    main()
