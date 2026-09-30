"""Compose the final barn atlas: painting where projected, colour-corrected Trellis colours elsewhere.

  python Tools/barn_compose_texture.py <trellis_base.png> <proj.png> <factor.png> <out.png>
Fits an affine RGB map (Trellis -> painting) on texels the painting covers and applies it to the rest.
"""
import sys
import numpy as np
from PIL import Image
base, proj, fac, out = sys.argv[1:5]
T = np.array(Image.open(base).convert('RGB')).astype(np.float32)
P = np.array(Image.open(proj).convert('RGB')).astype(np.float32)
F = np.array(Image.open(fac).convert('L')).astype(np.float32)[..., None] / 255.0
used = T.sum(2) > 12
sel = (F[..., 0] > 0.9) & used
X = np.concatenate([T[sel], np.ones((sel.sum(), 1), np.float32)], 1)
M, *_ = np.linalg.lstsq(X, P[sel], rcond=None)
flat = np.concatenate([T.reshape(-1, 3), np.ones((T.shape[0] * T.shape[1], 1), np.float32)], 1)
C = (flat @ M).reshape(T.shape).clip(0, 255)
# keep Trellis' fine detail: luminance from the histogram-matched atlas, chroma from the affine fit
lum = lambda a: (a * np.array([0.299, 0.587, 0.114], np.float32)).sum(2, keepdims=True)
ref = P[sel]
Tm = T.copy()
for c in range(3):
    src, r = T[..., c][used], np.sort(ref[:, c])
    q = (np.argsort(np.argsort(src)) + 0.5) / len(src)
    Tm[..., c][used] = np.interp(q, (np.arange(len(r)) + 0.5) / len(r), r)
C = (C * (lum(Tm) / (lum(C) + 1.0)).clip(0.3, 3.0)).clip(0, 255)
res = F * P + (1 - F) * C
print('fit texels', int(sel.sum()), 'M', np.round(M, 2).tolist())
Image.fromarray(res.clip(0, 255).astype(np.uint8)).save(out)
