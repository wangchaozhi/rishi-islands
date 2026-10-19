"""生成水下焦散动画帧（可平铺、首尾循环），给太阳光的 cookie 用。
用法：python Tools/AssetGen/caustics.py Assets/Art/Marine/Caustics
"""
import os, sys
import numpy as np
from PIL import Image

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
N, FRAMES = 256, 32
rng = np.random.default_rng(7)

def layer(points, radius, phase, t):
    """可平铺的 Worley F2-F1：点在小圆上绕圈运动，一个周期正好 FRAMES 帧。"""
    yy, xx = np.mgrid[0:N, 0:N] / N
    ang = phase + 2 * np.pi * t
    px = (points[:, 0] + radius * np.cos(ang)) % 1.0
    py = (points[:, 1] + radius * np.sin(ang * 1.0 + phase)) % 1.0
    d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0)
    for x0, y0 in zip(px, py):
        dx = np.abs(xx - x0); dx = np.minimum(dx, 1 - dx)
        dy = np.abs(yy - y0); dy = np.minimum(dy, 1 - dy)
        d = np.sqrt(dx * dx + dy * dy)
        d2 = np.where(d < d1, d1, np.minimum(d2, d))
        d1 = np.minimum(d1, d)
    return d2 - d1

pa = rng.random((22, 2)); ra = rng.uniform(0.02, 0.05, 22); pha = rng.random(22) * 6.28
pb = rng.random((40, 2)); rb = rng.uniform(0.01, 0.03, 40); phb = rng.random(40) * 6.28
for f in range(FRAMES):
    t = f / FRAMES
    a = layer(pa, ra, pha, t)
    b = layer(pb, rb, phb, -t)
    lines = np.exp(-a / 0.018) * 0.8 + np.exp(-b / 0.012) * 0.45
    img = np.clip(0.42 + lines * 1.1, 0, 1)
    v = (img * 255).astype(np.uint8)
    Image.fromarray(np.dstack([v, v, v, v]), "RGBA").save(os.path.join(OUT, f"caustic_{f:02d}.png"))
print("ok", FRAMES)
