#!/usr/bin/env python3
"""Procedural, tileable room textures (albedo + normal) for the counseling room.

WebGL budget: 1024² for the floor and walnut, 512² for plaster and linen; PNG in the repo,
compressed by Unity at build time. Run from the repo root:
    python3 Tools/art/generate_room_textures.py
"""
import numpy as np
from PIL import Image
from scipy.ndimage import zoom, gaussian_filter

OUT = "Assets/Art/Textures/Room/"
rng = np.random.default_rng(1701)


def tile_noise(size, cells):
    """Smooth, seamlessly tiling value noise: a wrapped random grid upsampled cubically."""
    grid = rng.random((cells, cells))
    padded = np.pad(grid, 3, mode="wrap")
    up = zoom(padded, size / cells, order=3)
    off = int(round(3 * size / cells))
    return up[off:off + size, off:off + size]


def fbm(size, base, octaves=5, gain=0.5):
    total = np.zeros((size, size))
    amp, norm, cells = 1.0, 0.0, base
    for _ in range(octaves):
        if cells >= size:
            break
        total += amp * tile_noise(size, cells)
        norm += amp
        amp *= gain
        cells *= 2
    return total / norm


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    n = np.dstack((-dx * strength, dy * strength, np.ones_like(h)))
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")


def save_rgb(arr, name):
    Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8), "RGB").save(OUT + name, optimize=True)


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


def oak_floor(size=1024, planks=6):
    """Wide oak planks with staggered butt joints, per-plank tone and warm grain."""
    y, x = np.mgrid[0:size, 0:size] / size
    plank_w = 1.0 / planks
    idx = np.floor(x / plank_w).astype(int)
    u = (x - idx * plank_w) / plank_w
    offsets = rng.random(planks)
    lengths = 0.5 + rng.random(planks) * 0.5
    v = (y + offsets[idx]) % 1.0
    joint_pos = (v % lengths[idx]) / lengths[idx]
    board = idx * 7 + np.floor((y + offsets[idx]) / lengths[idx]).astype(int)
    tone = (np.sin(board * 12.9898) * 43758.5453) % 1.0
    warp = fbm(size, 4, 4)
    grain = np.sin((u * 18 + warp * 6 + tone * 3) * np.pi * 2 + fbm(size, 16, 3) * 4) * 0.5 + 0.5
    fine = fbm(size, 64, 3)
    light = np.array([0.74, 0.55, 0.36])
    dark = np.array([0.52, 0.35, 0.21])
    t = np.clip(0.35 + grain * 0.35 + (tone - 0.5) * 0.35 + (fine - 0.5) * 0.3, 0, 1)
    albedo = lerp(light, dark, t)
    seam = (u < 0.012) | (u > 0.988) | (joint_pos < 0.004)
    albedo[seam] *= 0.55
    save_rgb(albedo, "oak_floor_albedo.png")
    height = grain * 0.15 + fine * 0.2
    height[seam] -= 1.2
    normal_from_height(gaussian_filter(height, 0.7, mode="wrap"), 3.0).save(OUT + "oak_floor_normal.png", optimize=True)


def plaster(size=512):
    """Warm limewash plaster: soft mottling and a faint trowel texture."""
    mott = fbm(size, 3, 5)
    fine = fbm(size, 48, 3)
    base = np.array([0.93, 0.89, 0.81])
    albedo = base * (0.94 + mott[..., None] * 0.08 + (fine[..., None] - 0.5) * 0.03)
    save_rgb(albedo, "plaster_albedo.png")
    normal_from_height(gaussian_filter(fine * 0.6 + mott * 0.4, 0.8, mode="wrap"), 2.2).save(OUT + "plaster_normal.png", optimize=True)


def linen(size=512, threads=96):
    """Plain-weave linen with slubs: alternating over/under threads and yarn thickness noise."""
    y, x = np.mgrid[0:size, 0:size] / size
    slub_x = fbm(size, 8, 3)
    slub_y = fbm(size, 8, 3)
    wx = np.sin((x * threads + slub_x * 1.5) * np.pi * 2)
    wy = np.sin((y * threads + slub_y * 1.5) * np.pi * 2)
    over = (np.floor(x * threads) + np.floor(y * threads)) % 2
    height = np.where(over > 0, wx * 0.5 + 0.5, wy * 0.5 + 0.5) * 0.7 + fbm(size, 32, 3) * 0.3
    albedo = np.full((size, size, 3), 0.86)
    albedo *= (0.9 + height[..., None] * 0.14)
    save_rgb(albedo, "linen_albedo.png")  # neutral; tinted per material in Unity
    normal_from_height(height, 2.4).save(OUT + "linen_normal.png", optimize=True)


def walnut(size=1024):
    """Dark walnut with cathedral grain for the console, legs and frames."""
    y, x = np.mgrid[0:size, 0:size] / size
    warp = fbm(size, 3, 4)
    # Mostly straight grain with gentle drift; stretched noise keeps the figure long, like quarter-sawn boards.
    streak = zoom(np.pad(rng.random((4, 64)), 3, mode="wrap"), (size / 4, size / 64), order=3)[
        int(3 * size / 4):int(3 * size / 4) + size, int(3 * size / 64):int(3 * size / 64) + size]
    rings = np.sin((x * 26 + warp * 1.1 + streak * 0.8) * np.pi * 2) * 0.5 + 0.5
    fine = fbm(size, 96, 2)
    light = np.array([0.42, 0.27, 0.17])
    dark = np.array([0.22, 0.13, 0.08])
    t = np.clip(rings * 0.6 + (fine - 0.5) * 0.5 + 0.2, 0, 1)
    save_rgb(lerp(light, dark, t), "walnut_albedo.png")
    normal_from_height(gaussian_filter(rings * 0.3 + fine * 0.3, 0.6, mode="wrap"), 2.0).save(OUT + "walnut_normal.png", optimize=True)


def contact_shadow(size=256):
    """Soft radial occlusion for decals under furniture (alpha only)."""
    y, x = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    r = np.sqrt(x ** 2 + y ** 2)
    a = np.clip(1 - r, 0, 1) ** 2.2
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.putalpha(Image.fromarray((a * 255).astype(np.uint8), "L"))
    img.save(OUT + "contact_shadow.png", optimize=True)


if __name__ == "__main__":
    oak_floor(); plaster(); linen(); walnut(); contact_shadow()
    print("room textures written to", OUT)
