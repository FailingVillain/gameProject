#!/usr/bin/env python3
"""Temporary WWII RTS SFX synthesizer (stdlib only, deterministic).
Writes korean-division/assets/sounds/ww2/*.wav and sounds.json."""
import json, math, os, random, struct, wave

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "sounds", "ww2")


def n(sec): return int(sec * SR)

def noise(rng, sec): return [rng.uniform(-1, 1) for _ in range(n(sec))]

def lp(x, fc):
    a = 1 - math.exp(-2 * math.pi * fc / SR); y = 0.0; o = []
    for v in x:
        y += a * (v - y); o.append(y)
    return o

def hp(x, fc):
    l = lp(x, fc); return [v - b for v, b in zip(x, l)]

def bp(x, lo, hi): return hp(lp(x, hi), lo)

def env(x, decay, attack=0.0005):
    """exponential decay envelope; decay = time constant (s)"""
    o = []
    for i, v in enumerate(x):
        t = i / SR
        a = min(1.0, t / attack) if attack > 0 else 1.0
        o.append(v * a * math.exp(-t / decay))
    return o

def osc(sec, f0, f1=None, harm=(1.0,)):
    """additive oscillator with optional exponential pitch sweep f0->f1"""
    f1 = f0 if f1 is None else f1
    N = n(sec); o = []; ph = 0.0
    for i in range(N):
        f = f0 * (f1 / f0) ** (i / max(1, N - 1))
        ph += 2 * math.pi * f / SR
        o.append(sum(h * math.sin((k + 1) * ph) for k, h in enumerate(harm)))
    return o

def gain(x, g): return [v * g for v in x]

def mix(*parts):
    L = max(len(p[1]) + n(p[0]) for p in parts)
    o = [0.0] * L
    for off, x in parts:
        s = n(off)
        for i, v in enumerate(x): o[s + i] += v
    return o

def finish(x, peak_db=-3.0, fi=0.003, fo=0.01):
    x = [v if v == v and abs(v) != float("inf") else 0.0 for v in x]
    m = max(abs(v) for v in x) or 1.0
    g = 10 ** (peak_db / 20) / m
    x = [v * g for v in x]
    a, b = n(fi), n(fo)
    for i in range(min(a, len(x))): x[i] *= i / a
    for i in range(min(b, len(x))): x[len(x) - 1 - i] *= i / b
    return x

def write_wav(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(round(v * 32767))))) for v in x))

def midi(m): return 440.0 * 2 ** ((m - 69) / 12)

# ---------------------------------------------------------------- sounds
def rifle(rng, v):
    d = 0.35
    crack = env(hp(noise(rng, d), 1500 + rng.uniform(-200, 200)), 0.012 + 0.002 * v)
    body = env(bp(noise(rng, d), 300, 3000), 0.05)
    thump = env(osc(d, 150 + rng.uniform(-15, 15), 55), 0.06)
    tail = env(lp(noise(rng, d), 900), 0.12)
    return mix((0, crack), (0, gain(body, 0.5)), (0, gain(thump, 0.9)), (0.01, gain(tail, 0.25)))

def carbine(rng, v):
    d = 0.22
    crack = env(hp(noise(rng, d), 2200 + rng.uniform(-250, 250)), 0.008)
    body = env(bp(noise(rng, d), 500, 4000), 0.03)
    thump = env(osc(d, 220 + rng.uniform(-20, 20), 90), 0.035)
    return mix((0, crack), (0, gain(body, 0.4)), (0, gain(thump, 0.5)))


def tank_cannon(rng, v):
    d = 1.3
    boom = env(osc(d, 90, 28), 0.28)
    blast = env(lp(noise(rng, d), 1200), 0.09)
    crack = env(hp(noise(rng, d), 1200), 0.02)
    rumble = env(lp(noise(rng, d), 130), 0.45, attack=0.01)
    return mix((0, boom), (0, gain(blast, 0.8)), (0, gain(crack, 0.5)), (0.02, gain(rumble, 1.6)))

def turret_gun(rng, v):
    parts = []
    for k in range(4):
        t = k * 0.11 + rng.uniform(-0.005, 0.005)
        sh = mix((0, env(hp(noise(rng, 0.2), 900), 0.02)),
                 (0, gain(env(osc(0.2, 130, 60), 0.05), 0.8)),
                 (0, gain(env(bp(noise(rng, 0.2), 300, 2500), 0.04), 0.5)))
        parts.append((t, gain(sh, 1 - 0.1 * k)))
    parts.append((0.0, gain(env(lp(noise(rng, 0.6), 500), 0.2), 0.2)))
    return mix(*parts)

def impact(rng, v):
    d = 0.15
    thud = env(osc(d, 120 + rng.uniform(-15, 15), 60), 0.03)
    dirt = env(bp(noise(rng, d), 1500, 5000), 0.035)
    dull = env(lp(noise(rng, d), 500), 0.04)
    return mix((0, thud), (0.005, gain(dirt, 0.5)), (0, gain(dull, 0.6)))

def formant(sec, f0, f1, forms, rng):
    """pulse train + noise through resonant formant bandpasses"""
    N = n(sec); src = []; ph = 0.0
    for i in range(N):
        f = f0 + (f1 - f0) * i / N
        ph += f / SR
        src.append(((ph % 1.0) * 2 - 1) * 0.7 + rng.uniform(-0.3, 0.3))
    out = [0.0] * N
    for fc, bw, g in forms:
        b = bp(src, max(60, fc - bw), fc + bw)
        for i in range(N): out[i] += b[i] * g
    return out

def death(rng, v):
    d = 0.4
    voice = env(formant(d, 130 + 12 * v, 80, [(500, 150, 1.0), (900, 200, 0.5)], rng), 0.18, attack=0.02)
    voice = lp(voice, 1500)
    thud = env(osc(d, 90, 45), 0.07)
    return mix((0, gain(voice, 1.2)), (0.08, gain(thud, 0.9)), (0.08, gain(env(lp(noise(rng, d), 400), 0.05), 0.4)))

def explosion(rng, v):
    d = 2.1
    burst = env(lp(noise(rng, d), 3500), 0.18)
    blast = env(osc(d, 70, 25), 0.5)
    rumble = env(lp(noise(rng, d), 150), 0.7, attack=0.02)
    deb = [0.0] * (n(d) + n(0.05))
    for _ in range(70):
        t = rng.uniform(0.08, 1.8); f = rng.uniform(1500, 6000)
        c = env(bp(noise(rng, 0.03), f * 0.7, f), 0.006)
        s = n(t)
        amp = rng.uniform(0.2, 0.8) * math.exp(-t / 0.8)
        for i, q in enumerate(c): deb[s + i] += q * amp * 2
    return mix((0, burst), (0, gain(blast, 1.2)), (0, gain(rumble, 2.0)), (0, deb))

def artillery_fire(rng, v):
    """self-propelled howitzer: deep boom, long low tail, short crack"""
    d = 1.8
    boom = env(osc(d, 70, 22), 0.4)
    blast = env(lp(noise(rng, d), 900), 0.12)
    crack = env(hp(noise(rng, d), 1500), 0.015)
    tail = env(lp(noise(rng, d), 110), 0.8, attack=0.015)
    return mix((0, boom), (0, gain(blast, 0.9)), (0, gain(crack, 0.5)), (0.03, gain(tail, 1.7)))

def artillery_impact(rng, v):
    """shell burst: sharp crack + rumble + a few fragments"""
    d = 1.6
    burst = env(lp(noise(rng, d), 4500), 0.1)
    blast = env(osc(d, 85, 30), 0.35)
    rumble = env(lp(noise(rng, d), 140), 0.55, attack=0.01)
    deb = [0.0] * (n(d) + n(0.05))
    for _ in range(40):
        t = rng.uniform(0.05, 1.2); f = rng.uniform(1500, 6000)
        c = env(bp(noise(rng, 0.03), f * 0.7, f), 0.006)
        st = n(t); amp = rng.uniform(0.2, 0.7) * math.exp(-t / 0.6)
        for i, q in enumerate(c): deb[st + i] += q * amp * 2
    return mix((0, burst), (0, gain(blast, 1.2)), (0, gain(rumble, 1.8)), (0, deb))


def bell(f, dur):
    return env(mix((0, osc(dur, f, harm=(1, 0.3, 0.1))), (0, gain(osc(dur, f * 2.76), 0.15))), dur / 4, attack=0.003)

def unit_ready(rng, v):
    sq = env(bp(noise(rng, 0.12), 800, 3500), 0.05, attack=0.002)
    beep = env(osc(0.14, 1100, harm=(1, 0.2)), 0.12, attack=0.003)
    sq2 = gain(env(hp(noise(rng, 0.06), 1500), 0.02), 0.4)
    return mix((0, sq), (0.1, gain(beep, 0.8)), (0.3, sq2))

def ui_click(rng, v):
    d = 0.05
    return mix((0, env(osc(d, 1800, 900), 0.008)), (0, gain(env(hp(noise(rng, d), 3000), 0.003), 0.3)))

def alert(rng, v):
    N = n(0.8); x = []; ph = 0.0
    for i in range(N):
        seg = int(i / SR / 0.2) % 2
        f = 880 if seg == 0 else 660
        ph += 2 * math.pi * f / SR
        t = (i / SR) % 0.2
        e = min(1, t / 0.008) * min(1, (0.2 - t) / 0.015)
        x.append((math.sin(ph) + 0.35 * math.sin(2 * ph) + 0.15 * math.sin(3 * ph)) * e)
    return x


def brass_note(f, dur):
    N = n(dur); o = []; ph = 0.0
    for i in range(N):
        t = i / SR
        ff = f * (1 + 0.004 * math.sin(2 * math.pi * 5.5 * t) * min(1, t / 0.2))
        ph += 2 * math.pi * ff / SR
        bright = 0.5 + 0.5 * min(1, t / 0.06)
        s = sum((1.0 / k) * bright ** (k - 1) * math.sin(k * ph) for k in range(1, 9))
        e = min(1, t / 0.03) * (min(1, (dur - t) / 0.12) if dur - t < 0.12 else 1.0)
        o.append(s * e)
    return o

def victory(rng, v):
    seq = [(60, 0, .2), (64, .2, .2), (67, .4, .2), (72, .6, .35), (67, .95, .15), (72, 1.1, .9)]
    parts = []
    for m, t, dur in seq:
        parts.append((t, brass_note(midi(m), dur)))
        if t >= 1.1:
            parts.append((t, gain(brass_note(midi(m - 8), dur), 0.5)))
            parts.append((t, gain(brass_note(midi(m - 5), dur), 0.5)))
    return lp(mix(*parts), 5000)

def defeat(rng, v):
    seq = [(69, 0, .4), (67, .4, .4), (65, .8, .4), (64, 1.2, .8)]  # A G F E, minor feel
    parts = []
    for m, t, dur in seq:
        parts.append((t, brass_note(midi(m - 12), dur)))
        parts.append((t, gain(brass_note(midi(m - 9), dur), 0.4)))  # minor third above
    return lp(mix(*parts), 2500)

# key: (fn, variants, volume, maxVoices, minInterval)
SPEC = {
    "rifle": (rifle, 3, 0.8, 4, 0.05),
    "carbine": (carbine, 3, 0.7, 4, 0.05),
    "tank_cannon": (tank_cannon, 1, 1.0, 2, 0.2),
    "turret_gun": (turret_gun, 1, 0.8, 2, 0.2),
    "impact": (impact, 3, 0.5, 4, 0.04),
    "death": (death, 2, 0.7, 3, 0.1),
    "explosion": (explosion, 1, 1.0, 3, 0.15),
    "artillery_fire": (artillery_fire, 1, 1.0, 2, 0.25),
    "artillery_impact": (artillery_impact, 2, 0.9, 4, 0.08),
    "unit_ready": (unit_ready, 1, 0.8, 1, 0.5),
    "ui_click": (ui_click, 1, 0.5, 1, 0.03),
    "alert": (alert, 1, 0.9, 1, 3.0),
    "victory": (victory, 1, 0.9, 1, 1.0),
    "defeat": (defeat, 1, 0.9, 1, 1.0),
}

def hash_seed(key, v):
    return sum(ord(c) * (i + 1) for i, c in enumerate(key)) * 100 + v + 1234

def main():
    os.makedirs(OUT, exist_ok=True)
    cfg = {}; rows = []
    for key, (fn, nv, vol, mv, mi) in SPEC.items():
        files = []
        for v in range(nv):
            rng = random.Random(hash_seed(key, v))
            x = finish(fn(rng, v))
            name = f"{key}_{v + 1}.wav" if nv > 1 else f"{key}.wav"
            write_wav(os.path.join(OUT, name), x)
            files.append(name)
            rows.append((key, v + 1, len(x) / SR, 20 * math.log10(max(abs(s) for s in x))))
        cfg[key] = {"files": files, "volume": vol, "maxVoices": mv, "minInterval": mi}
    with open(os.path.join(OUT, "sounds.json"), "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=2)
    print(f"{'key':<16}{'var':>4}{'dur(s)':>9}{'peak dBFS':>11}")
    for r in rows: print(f"{r[0]:<16}{r[1]:>4}{r[2]:>9.2f}{r[3]:>11.2f}")

if __name__ == "__main__":
    main()
