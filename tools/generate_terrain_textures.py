#!/usr/bin/env python3
"""
Generates 6 tileable 256x256 PNG terrain layer textures for Pocket Planet:
- VerdantCloverGrass.png
- VerdantPathDirt.png
- VerdantCliffStone.png
- ObsidianCrackedBasalt.png
- ObsidianRunePath.png
- ObsidianRidgeAsh.png
Uses pure Python + zlib/struct PNG writer (zero external dependencies).
"""
import math
import os
import struct
import zlib

OUT_DIR = "/Users/midir/pocket-planet/Assets/PocketPlanet/Art/Terrain"
SIZE = 256


def write_png(path, width, height, rgb_bytes):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    raw = bytearray()
    stride = width * 3
    for y in range(height):
        raw.append(0)
        raw.extend(rgb_bytes[y * stride : (y + 1) * stride])

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    idat = zlib.compress(bytes(raw), 6)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", ihdr))
        f.write(chunk(b"IDAT", idat))
        f.write(chunk(b"IEND", b""))


def hash2d(ix, iy, seed):
    n = (ix * 374761393 + iy * 668265263 + seed * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def tile_noise(u, v, freq, seed):
    x = u * freq
    y = v * freq
    x0 = int(math.floor(x))
    y0 = int(math.floor(y))
    fx = x - x0
    fy = y - y0
    sx = fx * fx * (3.0 - 2.0 * fx)
    sy = fy * fy * (3.0 - 2.0 * fy)
    ix0 = x0 % freq
    iy0 = y0 % freq
    ix1 = (x0 + 1) % freq
    iy1 = (y0 + 1) % freq
    n00 = hash2d(ix0, iy0, seed)
    n10 = hash2d(ix1, iy0, seed)
    n01 = hash2d(ix0, iy1, seed)
    n11 = hash2d(ix1, iy1, seed)
    nx0 = n00 + (n10 - n00) * sx
    nx1 = n01 + (n11 - n01) * sx
    return nx0 + (nx1 - nx0) * sy


def fbm_tile(u, v, base_freq, seed):
    val = 0.0
    amp = 0.5
    freq = base_freq
    for i in range(4):
        val += amp * tile_noise(u, v, freq, seed + i * 17)
        freq *= 2
        amp *= 0.5
    return val


def clamp8(x):
    return max(0, min(255, int(round(x * 255.0))))


def gen_verdant_clover_grass():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 6, 101)
            n2 = tile_noise(u, v, 24, 202)
            blades = math.sin((u * 18.0 + n1 * 2.5) * 2 * math.pi) * math.cos((v * 18.0 - n1 * 2.0) * 2 * math.pi)
            clover = 1.0 if n2 > 0.78 else 0.0
            blossom = 1.0 if tile_noise(u, v, 32, 303) > 0.91 else 0.0

            r = 0.16 + 0.10 * n1 + 0.04 * blades + 0.06 * clover
            g = 0.72 + 0.16 * n1 + 0.06 * blades + 0.10 * clover
            b = 0.62 + 0.12 * n1 - 0.03 * blades + 0.05 * clover

            if blossom > 0.5:
                r, g, b = 0.96, 0.54, 0.78

            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def gen_verdant_path_dirt():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 8, 404)
            pebble = tile_noise(u, v, 28, 505)
            p_boost = 0.08 if pebble > 0.74 else (-0.05 if pebble < 0.22 else 0.0)
            r = 0.86 + 0.10 * (n1 - 0.5) + p_boost
            g = 0.71 + 0.09 * (n1 - 0.5) + p_boost * 0.9
            b = 0.58 + 0.08 * (n1 - 0.5) + p_boost * 0.8
            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def gen_verdant_cliff_stone():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 6, 606)
            strata = 0.5 + 0.5 * math.sin((v * 6.0 + n1 * 1.4) * 2 * math.pi)
            r = 0.60 + 0.14 * n1 + 0.06 * strata
            g = 0.54 + 0.12 * n1 + 0.05 * strata
            b = 0.78 + 0.14 * n1 + 0.06 * strata
            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def gen_obsidian_cracked_basalt():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 8, 707)
            n2 = fbm_tile(u, v, 16, 808)
            crack = abs(n2 - 0.5)
            is_crack = max(0.0, 1.0 - crack * 18.0)
            r = 0.18 + 0.10 * n1 + is_crack * 0.28
            g = 0.15 + 0.08 * n1 + is_crack * 0.10
            b = 0.28 + 0.12 * n1 + is_crack * 0.42
            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def gen_obsidian_rune_path():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 8, 909)
            vein = abs(math.sin((u * 4.0 + n1 * 1.6) * math.pi) * math.cos((v * 4.0 - n1 * 1.6) * math.pi))
            glow = max(0.0, 1.0 - abs(vein - 0.35) * 14.0)
            r = 0.26 + 0.10 * n1 + glow * 0.58
            g = 0.20 + 0.08 * n1 + glow * 0.22
            b = 0.38 + 0.12 * n1 + glow * 0.60
            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def gen_obsidian_ridge_ash():
    buf = bytearray(SIZE * SIZE * 3)
    idx = 0
    for y in range(SIZE):
        v = y / SIZE
        for x in range(SIZE):
            u = x / SIZE
            n1 = fbm_tile(u, v, 10, 111)
            speck = tile_noise(u, v, 32, 222)
            glow = 1.0 if speck > 0.90 else 0.0
            r = 0.13 + 0.08 * n1 + glow * 0.38
            g = 0.11 + 0.07 * n1 + glow * 0.16
            b = 0.22 + 0.10 * n1 + glow * 0.52
            buf[idx] = clamp8(r)
            buf[idx + 1] = clamp8(g)
            buf[idx + 2] = clamp8(b)
            idx += 3
    return buf


def main():
    textures = {
        "VerdantCloverGrass.png": gen_verdant_clover_grass(),
        "VerdantPathDirt.png": gen_verdant_path_dirt(),
        "VerdantCliffStone.png": gen_verdant_cliff_stone(),
        "ObsidianCrackedBasalt.png": gen_obsidian_cracked_basalt(),
        "ObsidianRunePath.png": gen_obsidian_rune_path(),
        "ObsidianRidgeAsh.png": gen_obsidian_ridge_ash(),
    }
    for filename, buf in textures.items():
        out_path = os.path.join(OUT_DIR, filename)
        write_png(out_path, SIZE, SIZE, buf)
        print(f"Generated {out_path} ({SIZE}x{SIZE})")


if __name__ == "__main__":
    main()
