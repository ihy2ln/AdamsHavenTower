"""Key out a flat background by flood fill from the picture's border (keeps every painted pixel the fill cannot
reach, so cutaway interiors survive), soften the edge one pixel, trim. usage: key_flat.py in.png out.png [tolerance]"""
import sys
from collections import deque
import numpy as np
from PIL import Image, ImageFilter

src, dst = sys.argv[1], sys.argv[2]
tol = float(sys.argv[3]) if len(sys.argv) > 3 else 18
rgb = np.asarray(Image.open(src).convert('RGB'), dtype=np.int16)
h, w, _ = rgb.shape
border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
bg = np.median(border, axis=0)
near = np.abs(rgb - bg).max(axis=2) <= tol
outside = np.zeros((h, w), bool)
q = deque()
for x in range(w):
    for y in (0, h - 1):
        if near[y, x] and not outside[y, x]: outside[y, x] = True; q.append((y, x))
for y in range(h):
    for x in (0, w - 1):
        if near[y, x] and not outside[y, x]: outside[y, x] = True; q.append((y, x))
while q:
    y, x = q.popleft()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ny, nx = y + dy, x + dx
        if 0 <= ny < h and 0 <= nx < w and near[ny, nx] and not outside[ny, nx]:
            outside[ny, nx] = True; q.append((ny, nx))
alpha = Image.fromarray(np.where(outside, 0, 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
out = Image.fromarray(rgb.astype(np.uint8), 'RGB').convert('RGBA')
out.putalpha(alpha)
out = out.crop(alpha.point(lambda a: 255 if a > 20 else 0).getbbox())
out.save(dst)
print('keyed', dst, out.size, 'bg', bg.tolist())
