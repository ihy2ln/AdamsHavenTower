"""Procedural Battle Mode art: animated VFX sheets, a rune circle, god rays, a card frame and glossy gems.

Everything is drawn in white/grey on transparent so BattleMode tints it with GUI.color (element colour,
buff/debuff colour, card accent). Re-run to regenerate:

    python Tools/GenerateBattleFx.py

Output: Assets/Resources/AdamsHaven/Fx/*.png
"""
import math
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "AdamsHaven", "Fx")
os.makedirs(OUT, exist_ok=True)


def grid(w, h=None):
    h = h or w
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    x = (xs + 0.5) / w * 2 - 1
    y = -((ys + 0.5) / h * 2 - 1)          # y up, like the texture
    return x, y


def smooth(edge0, edge1, v):
    t = np.clip((v - edge0) / (edge1 - edge0 + 1e-9), 0, 1)
    return t * t * (3 - 2 * t)


def save(alpha, path, rgb=None):
    """alpha: HxW float 0..1. rgb: optional HxWx3 float 0..1 (default white)."""
    h, w = alpha.shape
    img = np.zeros((h, w, 4), np.float32)
    img[..., :3] = 1.0 if rgb is None else rgb
    img[..., 3] = np.clip(alpha, 0, 1)
    # Premultiplication-safe edges: keep colour where alpha is tiny.
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, path))


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy + 1e-9), 0, 1)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))


def line(px, py, ax, ay, bx, by, width, soft):
    d = seg_dist(px, py, ax, ay, bx, by)
    return 1 - smooth(width, width + soft, d)


# ---------------------------------------------------------------- slash sheet ---------------------
def slash_sheet(frames=8, size=320):
    x, y = grid(size)
    rho = np.hypot(x, y)
    phi = np.degrees(np.arctan2(y, x))          # -180..180
    start, end = 150.0, -20.0                     # sweep clockwise from upper-left across the top-right
    span = start - end
    sheet = np.zeros((size, size * frames), np.float32)
    R = 0.66
    for k in range(frames):
        prog = (k + 1) / (frames - 3.0)          # head reaches the end at frame frames-3, then it lingers and fades
        head = start - span * min(prog, 1.0)
        s = (start - phi) / span                 # 0 at start of the arc, 1 at the end
        inside = (s >= 0) & (s <= 1)
        taper = np.maximum(np.sin(np.clip(s, 0, 1) * math.pi), 0) ** 0.8
        halfw = 0.17 * taper + 0.004
        d = np.abs(rho - R - 0.05 * np.sin(np.clip(s, 0, 1) * math.pi))
        band = 1 - smooth(halfw * 0.35, halfw, d)
        core = np.exp(-(d / (halfw * 0.32 + 1e-4)) ** 2)
        age = np.clip((head - phi) * -1 / span, 0, 1)
        age = np.clip((phi - head) / span, 0, 1)   # 0 at the head, larger toward the tail
        trail = np.exp(-age * 2.4)
        trail = trail * smooth(0.0, span * 0.05, phi - head)   # soft leading edge, nothing ahead of the head
        fade = 1.0 if prog <= 1.0 else max(0.0, 1.0 - (prog - 1.0) * 2.6)
        a = (band * 0.75 + core * 0.7) * trail * inside * fade
        a *= 1 - smooth(0.9, 1.0, rho)
        sheet[:, k * size:(k + 1) * size] = a
    save(sheet, "fx_slash_sheet.png")


# ---------------------------------------------------------------- burst sheet ---------------------
def burst_sheet(frames=8, size=256):
    x, y = grid(size)
    rho = np.hypot(x, y)
    phi = np.arctan2(y, x)
    sheet = np.zeros((size, size * frames), np.float32)
    rng = np.random.RandomState(11)
    rays = [(rng.rand() * math.pi * 2, 0.035 + rng.rand() * 0.06, 0.55 + rng.rand() * 0.45) for _ in range(15)]
    for k in range(frames):
        t = k / (frames - 1)
        reach = 0.30 + 0.66 * (1 - (1 - t) ** 2.4)
        flash = np.exp(-(rho / (0.20 * (1 - t) + 0.04)) ** 2) * (1 - t) ** 0.7
        star = np.zeros_like(rho)
        for ang, width, length in rays:
            d = np.abs(((phi - ang + math.pi) % (2 * math.pi)) - math.pi)
            spike = np.exp(-(d / (width * (1.0 - 0.5 * rho))) ** 2)
            L = length * reach
            star = np.maximum(star, spike * (1 - smooth(L * 0.35, L, rho)) * smooth(0.02, 0.14, rho))
        star *= (1 - t) ** 0.7
        ring = np.exp(-((rho - reach * 0.82) / (0.04 + 0.03 * t)) ** 2) * (1 - t) ** 1.3 * 0.7
        a = np.clip(flash + star + ring, 0, 1)
        a *= 1 - smooth(0.92, 1.0, rho)
        sheet[:, k * size:(k + 1) * size] = a
    save(sheet, "fx_burst_sheet.png")


# ---------------------------------------------------------------- rune circle ---------------------
def rune_circle(size=512):
    x, y = grid(size)
    rho = np.hypot(x, y)
    phi = np.arctan2(y, x)
    px_soft = 2.0 / size
    a = np.zeros_like(rho)

    def ring(radius, width):
        return 1 - smooth(width, width + px_soft * 1.6, np.abs(rho - radius))

    a = np.maximum(a, ring(0.97, 0.010))
    a = np.maximum(a, ring(0.905, 0.006))
    a = np.maximum(a, ring(0.62, 0.008))
    a = np.maximum(a, ring(0.30, 0.008))
    # dashed ring
    dash = (np.sin(phi * 36) > -0.15).astype(np.float32)
    a = np.maximum(a, ring(0.78, 0.007) * dash)
    # ticks between the outer rings
    for i in range(72):
        ang = i * 2 * math.pi / 72
        r0, r1 = (0.905, 0.97) if i % 3 == 0 else (0.925, 0.955)
        a = np.maximum(a, line(x, y, r0 * math.cos(ang), r0 * math.sin(ang), r1 * math.cos(ang), r1 * math.sin(ang), 0.004, px_soft * 1.4) * 0.9)
    # hexagram
    tri_r = 0.62
    for base in (math.pi / 2, -math.pi / 2):
        pts = [(tri_r * math.cos(base + i * 2 * math.pi / 3), tri_r * math.sin(base + i * 2 * math.pi / 3)) for i in range(3)]
        for i in range(3):
            ax, ay = pts[i]; bx, by = pts[(i + 1) % 3]
            a = np.maximum(a, line(x, y, ax, ay, bx, by, 0.006, px_soft * 1.6))
    # glyph blocks around the mid band
    for i in range(12):
        ang = i * 2 * math.pi / 12 + math.pi / 12
        cx, cy = 0.84 * math.cos(ang), 0.84 * math.sin(ang)
        tx, ty = -math.sin(ang), math.cos(ang)
        rx, ry = math.cos(ang), math.sin(ang)
        a = np.maximum(a, line(x, y, cx - tx * 0.035, cy - ty * 0.035, cx + tx * 0.035, cy + ty * 0.035, 0.010, px_soft * 1.5))
        a = np.maximum(a, line(x, y, cx - rx * 0.03, cy - ry * 0.03, cx + rx * 0.03, cy + ry * 0.03, 0.005, px_soft * 1.5) * 0.8)
    # centre dots
    for i in range(6):
        ang = i * math.pi / 3
        d = np.hypot(x - 0.16 * math.cos(ang), y - 0.16 * math.sin(ang))
        a = np.maximum(a, 1 - smooth(0.014, 0.014 + px_soft * 1.6, d))
    a = np.maximum(a, 1 - smooth(0.03, 0.03 + px_soft * 1.6, rho))
    a *= 1 - smooth(0.985, 1.0, rho)
    # soft inner glow so it reads on a busy floor
    glow = np.exp(-(np.abs(rho - 0.97) / 0.05) ** 2) * 0.25 + smooth(1.0, 0.0, rho) * 0.05
    save(np.clip(a + glow * (1 - a), 0, 1), "fx_rune.png")


# ---------------------------------------------------------------- god rays ------------------------
def god_rays(size=1024):
    x, y = grid(size)
    rho = np.hypot(x, y)
    phi = np.arctan2(y, x)
    rng = np.random.RandomState(21)
    rays = np.zeros_like(rho)
    for _ in range(26):
        ang = rng.rand() * math.pi * 2
        width = 0.02 + rng.rand() * 0.07
        amp = 0.35 + rng.rand() * 0.65
        d = np.abs(((phi - ang + math.pi) % (2 * math.pi)) - math.pi)
        rays += amp * np.exp(-(d / width) ** 2)
    rays = np.clip(rays, 0, 1)
    falloff = np.clip(1 - rho, 0, 1) ** 1.3
    save(rays * falloff * smooth(0.0, 0.08, rho), "fx_rays.png")


# ---------------------------------------------------------------- card frame ----------------------
def rounded_sdf(x, y, hw, hh, r):
    qx = np.abs(x) - (hw - r)
    qy = np.abs(y) - (hh - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def card_frame(w=344, h=516):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    px = xs + 0.5 - w / 2
    py = -(ys + 0.5 - h / 2)
    outer = rounded_sdf(px, py, w / 2, h / 2, 26)
    band = 20.0
    inner = rounded_sdf(px, py, w / 2 - band, h / 2 - band, 14)
    body = (1 - smooth(-1.0, 0.6, outer)) * smooth(-0.6, 0.8, inner)
    # bevel: bright toward the top-left, dark toward the bottom-right, plus a fine engraved line.
    light = (-px / w * 0.9 + py / h * 0.9)
    shade = 0.62 + 0.34 * np.clip(light * 1.6 + 0.2, -1, 1)
    depth = np.clip(-outer / band, 0, 1)          # 0 at the outer edge, 1 at the inner edge
    shade = shade * (0.78 + 0.22 * np.sin(depth * math.pi))
    line_in = np.exp(-((-outer - band * 0.5) / 1.3) ** 2)
    shade = shade + line_in * 0.28
    edge_dark = np.exp(-((-outer) / 1.6) ** 2)
    shade = shade * (1 - edge_dark * 0.55)
    rgb = np.clip(shade, 0, 1)[..., None].repeat(3, axis=2)
    alpha = body.copy()
    # inner highlight hairline inside the window
    hair = np.exp(-((inner + 2.2) / 1.1) ** 2) * (inner < 0) * 0.55
    alpha = np.maximum(alpha, hair * 0.7)
    rgb = np.where(hair[..., None] > alpha[..., None] * 0.5, np.clip(rgb + 0.3, 0, 1), rgb)
    # corner diamonds and a top-centre gem notch
    def diamond(cx, cy, size):
        d = (np.abs(px - cx) + np.abs(py - cy)) / size
        return 1 - smooth(0.85, 1.0, d)
    orn = np.zeros_like(alpha)
    for sx in (-1, 1):
        for sy in (-1, 1):
            orn = np.maximum(orn, diamond(sx * (w / 2 - band - 15), sy * (h / 2 - band - 15), 11))
    orn = np.maximum(orn, diamond(0, h / 2 - band * 0.5, 9))
    orn = np.maximum(orn, diamond(0, -h / 2 + band * 0.5, 9))
    rgb = np.where(orn[..., None] > 0.3, np.clip(rgb * 0.4 + 0.65, 0, 1), rgb)
    alpha = np.maximum(alpha, orn * 0.95)
    save(alpha, "ui_card_frame.png", rgb)


# ---------------------------------------------------------------- gems ----------------------------
def gem(size=128):
    x, y = grid(size)
    rho = np.hypot(x, y)
    body = 1 - smooth(0.90, 0.94, rho)
    # spherical lighting from the upper left
    nz = np.sqrt(np.clip(1 - rho ** 2, 0, 1))
    lx, ly, lz = -0.45, 0.6, 0.66
    diff = np.clip(x * lx + y * ly + nz * lz, 0, 1)
    shade = 0.42 + 0.58 * diff ** 0.8
    rim = smooth(0.62, 0.92, rho)
    shade = shade * (1 - rim * 0.28)
    # bottom bounce light
    bounce = np.exp(-(((y + 0.62) / 0.22) ** 2 + (x / 0.55) ** 2)) * 0.22
    shade = shade + bounce
    # glossy highlight
    hx, hy = -0.32, 0.4
    hl = np.exp(-(((x - hx) / 0.36) ** 2 + ((y - hy) / 0.2) ** 2))
    rgb = np.clip(shade, 0, 1)[..., None].repeat(3, axis=2)
    rgb = np.clip(rgb + (hl * 0.7)[..., None], 0, 1)
    ring = np.exp(-((rho - 0.92) / 0.028) ** 2)
    rgb = np.where(ring[..., None] > 0.35, 0.10, rgb)
    save(np.maximum(body, ring * 0.9), "ui_gem.png", rgb)


# ---------------------------------------------------------------- button glow ---------------------
def sparkle(size=128):
    x, y = grid(size)
    rho = np.hypot(x, y)
    cross = np.exp(-(x / 0.035) ** 2) * np.clip(1 - np.abs(y), 0, 1) ** 2 + np.exp(-(y / 0.035) ** 2) * np.clip(1 - np.abs(x), 0, 1) ** 2
    diag = 0.5 * (np.exp(-(((x - y) / 0.05) ** 2)) + np.exp(-(((x + y) / 0.05) ** 2))) * np.clip(1 - rho, 0, 1) ** 2
    core = np.exp(-(rho / 0.12) ** 2)
    save(np.clip(cross + diag + core, 0, 1), "fx_sparkle.png")


if __name__ == "__main__":
    slash_sheet()
    burst_sheet()
    rune_circle()
    god_rays()
    card_frame()
    gem()
    sparkle()
    print("wrote", sorted(os.listdir(OUT)))
