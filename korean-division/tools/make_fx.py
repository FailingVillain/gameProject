#!/usr/bin/env python3
"""Procedural FX sheets for the ww2 theme (numpy + PIL, deterministic).
Writes assets/themes/ww2/fx/explosion.png: 8 frames of 96x96 in one row (fireball -> smoke)."""
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "themes", "ww2", "fx")
N, S = 8, 96

def explosion():
    rng = np.random.default_rng(7)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx = cy = S / 2 - 0.5
    # fixed set of blobs that drift outward over time
    blobs = [(rng.uniform(0, 6.283), rng.uniform(0.1, 0.8), rng.uniform(0.18, 0.34)) for _ in range(14)]
    sheet = np.zeros((S, S * N, 4), np.float32)
    for f in range(N):
        t = f / (N - 1)
        a = np.zeros((S, S), np.float32)
        heat = np.zeros((S, S), np.float32)
        grow = 0.35 + 0.65 * (1 - (1 - t) ** 2)
        for ang, rr, sz in blobs:
            bx = cx + np.cos(ang) * rr * grow * S * 0.55
            by = cy + np.sin(ang) * rr * grow * S * 0.5 - t * 5
            d = np.sqrt((xx - bx) ** 2 + (yy - by) ** 2) / (S * sz * (0.8 + 0.7 * grow))
            blob = np.clip(1 - d, 0, 1) ** 0.55
            a = np.maximum(a, blob)
            heat = np.maximum(heat, blob * (1 - t * 0.9))
        core = np.clip(1 - np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (S * 0.4 * (1.1 - t)), 0, 1) * (1 - t) ** 1.5
        a = np.clip(np.maximum(a, core), 0, 1)
        heat = np.clip(np.maximum(heat, core), 0, 1)
        fade = 1.0 if t < 0.6 else max(0.0, 1 - (t - 0.6) / 0.45)
        # shockwave ring early on
        rd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        ring = np.clip(1 - np.abs(rd - t * S * 0.46) / 2.5, 0, 1) * max(0.0, 1 - t * 1.6) * 0.25
        smoke = np.array([70, 62, 56], np.float32)
        fire = np.array([255, 150, 40], np.float32)
        hot = np.array([255, 240, 190], np.float32)
        rgb = smoke[None, None] * (1 - heat[..., None]) + fire[None, None] * heat[..., None]
        rgb = rgb * (1 - core[..., None] * 0.9) + hot[None, None] * core[..., None] * 0.9
        alpha = np.clip(a * fade, 0, 1)
        rgb = rgb * alpha[..., None] + np.array([240, 230, 210], np.float32) * ring[..., None]
        alpha = np.clip(alpha + ring, 0, 1)
        sheet[:, f * S:(f + 1) * S, :3] = rgb / np.maximum(alpha[..., None], 1e-3) * (alpha[..., None] > 0)
        sheet[:, f * S:(f + 1) * S, 3] = alpha * 255
    sheet[..., :3] = np.clip(sheet[..., :3], 0, 255)
    return Image.fromarray(sheet.astype(np.uint8), "RGBA")

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    explosion().save(os.path.join(OUT, "explosion.png"))
    print("wrote", os.path.join(OUT, "explosion.png"))
