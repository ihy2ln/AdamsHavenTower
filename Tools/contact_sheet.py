"""Assemble battle_rig_preview.py frames into one labelled contact sheet per clip.

python Tools/contact_sheet.py <frames_dir> <out_dir> [cols]
"""
import sys, re, pathlib
from collections import defaultdict
from PIL import Image, ImageDraw

src, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 8
out.mkdir(parents=True, exist_ok=True)
groups = defaultdict(list)
for p in sorted(src.glob('*.png')):
    m = re.match(r'(.+)_(\d\d)\.png$', p.name)
    if m:
        groups[m.group(1)].append(p)
for clip, paths in groups.items():
    ims = [Image.open(p).convert('RGB') for p in paths]
    w, h = ims[0].size
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new('RGB', (w * cols, h * rows + 24), (20, 20, 24))
    d = ImageDraw.Draw(sheet)
    d.text((6, 4), clip, fill=(255, 230, 120))
    for i, im in enumerate(ims):
        sheet.paste(im, ((i % cols) * w, 24 + (i // cols) * h))
        d.text(((i % cols) * w + 4, 26 + (i // cols) * h), str(i), fill=(255, 255, 0))
    sheet.save(out / f'{clip}.jpg', quality=85)
    print('SHEET', out / f'{clip}.jpg')
