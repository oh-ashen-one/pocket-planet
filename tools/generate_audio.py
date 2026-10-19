#!/usr/bin/env python3
"""
Deterministic procedural audio generator for Pocket Planet.
Synthesizes original whimsical toybox SFX and an ambient space-garden music loop.
No third-party audio samples are used (CC BY 4.0 / MIT).
"""
import os
import math
import wave
import struct
import random

OUT_DIR = "/Users/midir/pocket-planet/Assets/PocketPlanet/Resources/Audio"
SR = 44100


def save_wav(name, samples):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.wav")
    peak = max((abs(s) for s in samples), default=1.0)
    scale = 0.85 / peak if peak > 0.85 else 1.0
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for s in samples:
            val = max(-1.0, min(1.0, s * scale))
            frames.extend(struct.pack("<h", int(val * 32767)))
        w.writeframes(frames)
    return path


def env_adsr(t, dur, a=0.01, d=0.08, s=0.5, r=0.1):
    if t < 0 or t > dur:
        return 0.0
    if t < a:
        return t / max(a, 1e-5)
    if t < a + d:
        return 1.0 - (1.0 - s) * ((t - a) / max(d, 1e-5))
    if t < dur - r:
        return s
    return s * max(0.0, (dur - t) / max(r, 1e-5))


def gen_footstep_grass(seed_offset=0):
    rng = random.Random(100 + seed_offset)
    dur = 0.14
    n = int(SR * dur)
    out = []
    base_f = 150.0 + seed_offset * 18.0
    lp = 0.0
    for i in range(n):
        t = i / SR
        env = math.exp(-t * 28.0) * math.sin(min(1.0, t / 0.008) * math.pi * 0.5)
        noise = rng.uniform(-1.0, 1.0)
        lp = 0.78 * lp + 0.22 * noise
        thud = math.sin(2 * math.pi * base_f * (1.0 - 0.4 * (t / dur)) * t) * math.exp(-t * 40.0)
        out.append((0.45 * lp + 0.55 * thud) * env * 0.6)
    return out


def gen_footstep_water(seed_offset=0):
    rng = random.Random(200 + seed_offset)
    dur = 0.26
    n = int(SR * dur)
    out = []
    bp = 0.0
    for i in range(n):
        t = i / SR
        env = math.exp(-t * 14.0) * math.sin(min(1.0, t / 0.01) * math.pi * 0.5)
        noise = rng.uniform(-1.0, 1.0)
        bp = 0.6 * bp + 0.4 * noise
        bloop_f = 380.0 + 420.0 * ((t / dur) ** 1.3) + seed_offset * 45.0
        bloop = math.sin(2 * math.pi * bloop_f * t) * math.exp(-((t - 0.06) ** 2) / 0.0018)
        out.append((0.45 * bp + 0.55 * bloop) * env * 0.7)
    return out


def gen_jump():
    dur = 0.28
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 240.0 + 480.0 * ((t / dur) ** 0.75)
        phase += 2 * math.pi * f / SR
        env = env_adsr(t, dur, a=0.008, d=0.08, s=0.65, r=0.09)
        s = 0.7 * math.sin(phase) + 0.3 * math.sin(phase * 2.0)
        out.append(s * env * 0.65)
    return out


def gen_land():
    dur = 0.18
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 210.0 * math.exp(-t * 9.0) + 65.0
        phase += 2 * math.pi * f / SR
        env = math.exp(-t * 20.0)
        out.append((0.75 * math.sin(phase) + 0.25 * math.sin(phase * 1.5)) * env * 0.65)
    return out


def gen_mushroom_boing():
    dur = 0.52
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        wobble = math.sin(2 * math.pi * 19.0 * t) * 55.0 * math.exp(-t * 3.5)
        f = 220.0 + 380.0 * (t / dur) + wobble
        phase += 2 * math.pi * f / SR
        env = math.exp(-t * 4.5) * math.sin(min(1.0, t / 0.01) * math.pi * 0.5)
        s = 0.7 * math.sin(phase) + 0.2 * math.sin(phase * 2.0) + 0.1 * math.sin(phase * 3.0)
        out.append(s * env * 0.75)
    return out


def gen_pickup_fruit():
    dur = 0.25
    n = int(SR * dur)
    out = []
    notes = [523.25, 659.25, 783.99]  # C5, E5, G5 pop
    for i in range(n):
        t = i / SR
        idx = min(2, int(t / (dur / 3.0)))
        lt = t - idx * (dur / 3.0)
        f = notes[idx]
        env = math.exp(-lt * 18.0)
        s = 0.75 * math.sin(2 * math.pi * f * t) + 0.25 * math.sin(4 * math.pi * f * t)
        out.append(s * env * 0.65)
    return out


def gen_throw_fruit():
    dur = 0.24
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 540.0 - 320.0 * (t / dur)
        phase += 2 * math.pi * f / SR
        env = env_adsr(t, dur, a=0.01, d=0.06, s=0.5, r=0.08)
        out.append(math.sin(phase) * env * 0.6)
    return out


def gen_plant_eat_bloom():
    dur = 0.95
    n = int(SR * dur)
    out = []
    chord = [523.25, 659.25, 783.99, 1046.50, 1318.51]
    for i in range(n):
        t = i / SR
        if t < 0.22:
            # Cute gulp
            f = 280.0 + 260.0 * math.sin(math.pi * t / 0.22)
            s = math.sin(2 * math.pi * f * t) * math.exp(-t * 6.0)
        else:
            # Magical bloom arpeggio
            bt = t - 0.22
            s = 0.0
            for k, nf in enumerate(chord):
                nt = bt - k * 0.09
                if nt > 0:
                    env = math.exp(-nt * 4.5) * min(1.0, nt / 0.01)
                    s += (0.6 * math.sin(2 * math.pi * nf * nt) + 0.3 * math.sin(4 * math.pi * nf * nt)) * env * 0.35
        out.append(s)
    return out


def gen_lever_pull():
    dur = 0.32
    n = int(SR * dur)
    out = []
    for i in range(n):
        t = i / SR
        f = 320.0 if t < 0.14 else 480.0
        lt = t if t < 0.14 else t - 0.14
        env = math.exp(-lt * 22.0)
        s = 0.65 * math.sin(2 * math.pi * f * lt) + 0.35 * math.sin(2 * math.pi * f * 2.4 * lt)
        out.append(s * env * 0.7)
    return out


def gen_bridge_unfold():
    dur = 0.85
    n = int(SR * dur)
    out = []
    steps = [392.0, 440.0, 493.88, 587.33, 659.25, 783.99]
    for i in range(n):
        t = i / SR
        idx = min(len(steps) - 1, int(t / (dur / len(steps))))
        lt = t - idx * (dur / len(steps))
        f = steps[idx]
        env = math.exp(-lt * 14.0)
        s = 0.7 * math.sin(2 * math.pi * f * lt) + 0.3 * math.sin(4 * math.pi * f * lt)
        out.append(s * env * 0.65)
    return out


def gen_bloop_happy():
    dur = 0.48
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        if t < 0.22:
            f = 580.0 + 280.0 * math.sin(math.pi * t / 0.22)
            env = math.sin(math.pi * t / 0.22)
        else:
            lt = t - 0.22
            f = 660.0 + 360.0 * math.sin(math.pi * lt / 0.26)
            env = math.sin(math.pi * lt / 0.26)
        phase += 2 * math.pi * f / SR
        out.append((0.8 * math.sin(phase) + 0.2 * math.sin(2 * phase)) * env * 0.65)
    return out


def gen_boulder_roll():
    rng = random.Random(303)
    dur = 0.6
    n = int(SR * dur)
    out = []
    lp = 0.0
    for i in range(n):
        t = i / SR
        noise = rng.uniform(-1.0, 1.0)
        lp = 0.92 * lp + 0.08 * noise
        rumble = math.sin(2 * math.pi * 68.0 * t + 0.5 * math.sin(2 * math.pi * 7.0 * t))
        env = math.sin(math.pi * t / dur)
        out.append((0.6 * lp + 0.4 * rumble) * env * 0.55)
    return out


def gen_block_topple():
    dur = 0.45
    n = int(SR * dur)
    out = []
    hits = [(0.0, 290.0), (0.13, 340.0), (0.26, 410.0)]
    for i in range(n):
        t = i / SR
        s = 0.0
        for ht, hf in hits:
            if t >= ht:
                lt = t - ht
                env = math.exp(-lt * 24.0)
                s += (0.7 * math.sin(2 * math.pi * hf * lt) + 0.3 * math.sin(2 * math.pi * hf * 1.85 * lt)) * env
        out.append(s * 0.6)
    return out


def gen_grass_rustle():
    rng = random.Random(404)
    dur = 0.22
    n = int(SR * dur)
    out = []
    prev = 0.0
    for i in range(n):
        t = i / SR
        noise = rng.uniform(-1.0, 1.0)
        hp = noise - prev
        prev = noise
        env = math.sin(math.pi * t / dur) * math.exp(-t * 7.0)
        out.append(hp * env * 0.35)
    return out


def gen_star_seed_collect():
    dur = 1.35
    n = int(SR * dur)
    out = []
    notes = [
        (0.00, 523.25),
        (0.11, 659.25),
        (0.22, 783.99),
        (0.33, 1046.50),
        (0.46, 1318.51),
        (0.60, 1567.98),
    ]
    for i in range(n):
        t = i / SR
        s = 0.0
        for st, nf in notes:
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 3.8) * min(1.0, lt / 0.008)
                s += (0.65 * math.sin(2 * math.pi * nf * lt) + 0.25 * math.sin(4 * math.pi * nf * lt) + 0.10 * math.sin(6 * math.pi * nf * lt)) * env * 0.28
        out.append(s)
    return out


def gen_ship_repair_fanfare():
    dur = 2.6
    n = int(SR * dur)
    out = []
    melody = [
        (0.00, 523.25, 0.22),
        (0.22, 659.25, 0.22),
        (0.44, 783.99, 0.22),
        (0.66, 1046.50, 0.40),
        (1.08, 987.77, 0.20),
        (1.28, 1046.50, 0.22),
        (1.52, 1318.51, 0.30),
        (1.84, 1567.98, 0.72),
    ]
    harmony = [523.25, 659.25, 783.99, 1046.50]
    for i in range(n):
        t = i / SR
        s = 0.0
        for st, nf, nd in melody:
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 3.2) * min(1.0, lt / 0.01)
                s += (0.6 * math.sin(2 * math.pi * nf * lt) + 0.3 * math.sin(4 * math.pi * nf * lt)) * env * 0.32
        if t >= 1.52:
            lt = t - 1.52
            cenv = math.exp(-lt * 2.0) * min(1.0, lt / 0.03)
            for cf in harmony:
                s += 0.12 * math.sin(2 * math.pi * cf * lt) * cenv
        out.append(s)
    return out


def gen_bgm_loop():
    # Seamless 16-second whimsical space-garden marimba/celesta loop (120 BPM, 32 beats)
    dur = 16.0
    n = int(SR * dur)
    out = [0.0] * n
    beat_s = 0.5
    # C major9 / F major7 / A minor7 / G6 progression
    chords = [
        [261.63, 329.63, 392.00, 493.88],
        [349.23, 440.00, 523.25, 659.25],
        [220.00, 261.63, 329.63, 392.00],
        [196.00, 246.94, 293.66, 392.00],
    ]
    arp_pattern = [0, 1, 2, 3, 2, 1, 3, 2]
    for beat in range(32):
        bar = (beat // 8) % 4
        chord = chords[bar]
        note_f = chord[arp_pattern[beat % 8]] * 2.0
        st = beat * beat_s
        start_idx = int(st * SR)
        note_len = int(0.95 * SR)
        for j in range(note_len):
            idx = (start_idx + j) % n
            lt = j / SR
            env = math.exp(-lt * 5.2) * min(1.0, lt / 0.01)
            val = (0.7 * math.sin(2 * math.pi * note_f * lt) + 0.3 * math.sin(4 * math.pi * note_f * lt)) * env * 0.18
            out[idx] += val
    # Warm soft pad underneath (periodic over dur)
    for i in range(n):
        t = i / SR
        bar = int(t // 4.0) % 4
        lt = t % 4.0
        pad_env = math.sin(math.pi * lt / 4.0) ** 0.7
        pad = 0.0
        for cf in chords[bar]:
            pad += math.sin(2 * math.pi * cf * t) * 0.045
        out[i] += pad * pad_env
    return out


def gen_npc_chirp():
    dur = 0.34
    n = int(SR * dur)
    out = []
    notes = [440.0, 554.37, 659.25, 587.33]
    for i in range(n):
        t = i / SR
        idx = min(3, int(t / (dur / 4.0)))
        lt = t - idx * (dur / 4.0)
        f = notes[idx] * (1.0 + 0.06 * math.sin(2 * math.pi * 24.0 * lt))
        env = math.exp(-lt * 18.0) * min(1.0, lt / 0.006)
        out.append((0.75 * math.sin(2 * math.pi * f * lt) + 0.25 * math.sin(4 * math.pi * f * lt)) * env * 0.6)
    return out


def gen_solaris_core_pickup():
    dur = 1.85
    n = int(SR * dur)
    out = []
    notes = [
        (0.00, 587.33),
        (0.12, 739.99),
        (0.24, 880.00),
        (0.36, 1174.66),
        (0.50, 1479.98),
        (0.66, 1760.00),
    ]
    for i in range(n):
        t = i / SR
        s = 0.0
        for st, nf in notes:
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 2.8) * min(1.0, lt / 0.008)
                s += (0.6 * math.sin(2 * math.pi * nf * lt) + 0.3 * math.sin(4 * math.pi * nf * lt) + 0.1 * math.sin(6 * math.pi * nf * lt)) * env * 0.28
        out.append(s)
    return out


def gen_saucer_launch():
    dur = 1.4
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 140.0 + 620.0 * ((t / dur) ** 1.4) + 35.0 * math.sin(2 * math.pi * 14.0 * t)
        phase += 2 * math.pi * f / SR
        env = math.sin(math.pi * min(1.0, t / dur)) ** 0.5
        s = 0.65 * math.sin(phase) + 0.25 * math.sin(phase * 2.0) + 0.10 * math.sin(phase * 3.0)
        out.append(s * env * 0.65)
    return out


def gen_saucer_boost():
    dur = 0.65
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 320.0 + 740.0 * math.sin(0.5 * math.pi * (t / dur))
        phase += 2 * math.pi * f / SR
        env = math.exp(-t * 3.8) * min(1.0, t / 0.015)
        out.append((0.7 * math.sin(phase) + 0.3 * math.sin(2.0 * phase)) * env * 0.65)
    return out


def gen_shrine_ignite():
    dur = 2.8
    n = int(SR * dur)
    out = []
    chord = [293.66, 440.00, 587.33, 739.99, 880.00, 1174.66, 1479.98]
    for i in range(n):
        t = i / SR
        s = 0.0
        for k, nf in enumerate(chord):
            st = k * 0.11
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 1.65) * min(1.0, lt / 0.02)
                s += (0.65 * math.sin(2 * math.pi * nf * lt) + 0.25 * math.sin(4 * math.pi * nf * lt)) * env * 0.22
        out.append(s)
    return out


def gen_bgm_obsidian_loop():
    # Seamless 16-second brooding dark-crystal ambient loop (D minor9 / Bb maj7#11 / G minor7 / A7b9)
    dur = 16.0
    n = int(SR * dur)
    out = [0.0] * n
    rng = random.Random(909)
    chords = [
        [146.83, 220.00, 261.63, 329.63],
        [116.54, 174.61, 220.00, 293.66],
        [196.00, 233.08, 293.66, 349.23],
        [110.00, 164.81, 220.00, 277.18],
    ]
    arp_pattern = [0, 2, 1, 3, 2, 3, 1, 0]
    beat_s = 0.5
    for beat in range(32):
        bar = (beat // 8) % 4
        chord = chords[bar]
        note_f = chord[arp_pattern[beat % 8]] * 2.0
        st = beat * beat_s
        start_idx = int(st * SR)
        note_len = int(1.35 * SR)
        for j in range(note_len):
            idx = (start_idx + j) % n
            lt = j / SR
            env = math.exp(-lt * 3.2) * min(1.0, lt / 0.02)
            val = (0.75 * math.sin(2 * math.pi * note_f * lt) + 0.25 * math.sin(3 * math.pi * note_f * lt)) * env * 0.14
            out[idx] += val
    lp = 0.0
    for i in range(n):
        t = i / SR
        bar = int(t // 4.0) % 4
        lt = t % 4.0
        pad_env = math.sin(math.pi * lt / 4.0) ** 0.6
        pad = 0.0
        for cf in chords[bar]:
            pad += math.sin(2 * math.pi * cf * t + 0.2 * math.sin(2 * math.pi * 0.5 * t)) * 0.055
        noise = rng.uniform(-1.0, 1.0)
        lp = 0.985 * lp + 0.015 * noise
        wind = lp * 0.08 * (0.6 + 0.4 * math.sin(2 * math.pi * 0.125 * t))
        out[i] += pad * pad_env + wind
    return out


def gen_saucer_laser():
    # Crisp retro-cute star-pulse pew
    dur = 0.24
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = 1480.0 * math.exp(-t * 11.5) + 240.0
        phase += 2 * math.pi * f / SR
        env = math.exp(-t * 14.0) * min(1.0, t / 0.005)
        s = 0.7 * math.sin(phase) + 0.3 * math.sin(2.5 * phase)
        out.append(s * env * 0.72)
    return out


def gen_asteroid_shatter():
    # Crystalline meteor shatter + crunch + high chime sparkles
    rng = random.Random(612)
    dur = 0.68
    n = int(SR * dur)
    out = []
    lp = 0.0
    chimes = [880.0, 1174.66, 1318.51, 1760.0]
    for i in range(n):
        t = i / SR
        noise = rng.uniform(-1.0, 1.0)
        lp = 0.85 * lp + 0.15 * noise
        crunch = lp * math.exp(-t * 9.0) * 0.65
        sparkle = 0.0
        for k, cf in enumerate(chimes):
            st = k * 0.055
            if t >= st:
                lt = t - st
                sparkle += math.sin(2 * math.pi * cf * lt) * math.exp(-lt * 10.0) * 0.22
        out.append((crunch + sparkle) * 0.85)
    return out


def gen_pylon_attune():
    # Resonant crystal pylon harmonic chord swell
    dur = 0.95
    n = int(SR * dur)
    out = []
    notes = [440.0, 554.37, 659.25, 880.0, 1108.73]
    for i in range(n):
        t = i / SR
        s = 0.0
        for k, nf in enumerate(notes):
            st = k * 0.07
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 3.2) * min(1.0, lt / 0.015)
                s += (0.7 * math.sin(2 * math.pi * nf * lt) + 0.3 * math.sin(2 * math.pi * nf * 2.0 * lt)) * env * 0.25
        out.append(s)
    return out


def gen_barrier_dissolve():
    # Magical shield barrier shatter + ascending liberación sweep
    dur = 1.25
    n = int(SR * dur)
    out = []
    chord = [293.66, 440.0, 587.33, 739.99, 880.0, 1174.66]
    for i in range(n):
        t = i / SR
        sweep_f = 220.0 + 760.0 * (t / dur) ** 1.4
        sweep = math.sin(2 * math.pi * sweep_f * t) * math.sin(math.pi * t / dur) * 0.25
        s = sweep
        for k, nf in enumerate(chord):
            st = k * 0.08
            if t >= st:
                lt = t - st
                env = math.exp(-lt * 2.6) * min(1.0, lt / 0.02)
                s += (0.65 * math.sin(2 * math.pi * nf * lt) + 0.35 * math.sin(4 * math.pi * nf * lt)) * env * 0.22
        out.append(s)
    return out


def main():
    clips = {
        "footstep_grass_0": gen_footstep_grass(0),
        "footstep_grass_1": gen_footstep_grass(1),
        "footstep_grass_2": gen_footstep_grass(2),
        "footstep_water_0": gen_footstep_water(0),
        "footstep_water_1": gen_footstep_water(1),
        "jump": gen_jump(),
        "land": gen_land(),
        "mushroom_boing": gen_mushroom_boing(),
        "pickup_fruit": gen_pickup_fruit(),
        "throw_fruit": gen_throw_fruit(),
        "plant_eat_bloom": gen_plant_eat_bloom(),
        "lever_pull": gen_lever_pull(),
        "bridge_unfold": gen_bridge_unfold(),
        "bloop_happy": gen_bloop_happy(),
        "boulder_roll": gen_boulder_roll(),
        "block_topple": gen_block_topple(),
        "grass_rustle": gen_grass_rustle(),
        "star_seed_collect": gen_star_seed_collect(),
        "ship_repair_fanfare": gen_ship_repair_fanfare(),
        "bgm_pocket_planet": gen_bgm_loop(),
        "npc_chirp": gen_npc_chirp(),
        "solaris_core_pickup": gen_solaris_core_pickup(),
        "saucer_launch": gen_saucer_launch(),
        "saucer_boost": gen_saucer_boost(),
        "shrine_ignite": gen_shrine_ignite(),
        "bgm_obsidian_deadwood": gen_bgm_obsidian_loop(),
        "saucer_laser": gen_saucer_laser(),
        "asteroid_shatter": gen_asteroid_shatter(),
        "pylon_attune": gen_pylon_attune(),
        "barrier_dissolve": gen_barrier_dissolve(),
    }
    for name, samples in clips.items():
        p = save_wav(name, samples)
        print(f"Generated {p} ({len(samples)/SR:.2f}s)")


if __name__ == "__main__":
    main()

