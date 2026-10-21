"""Generate Korean Division default SFX set (procedural, no third-party samples).

usage: python generate.py [outdir] [--only substr]
Outputs mono 16-bit 44.1 kHz WAVs + index.html + README.md.
"""
import sys
import html
from pathlib import Path
import numpy as np
from sfxlib import *

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else \
    Path(r"C:\Users\seung\OneDrive\바탕 화면\coding\game-tools\korean-division-audio-default")
ONLY = sys.argv[sys.argv.index('--only') + 1] if '--only' in sys.argv else None

REG = []   # (file, group, desc, event key, suggested volume, maxVoices)


def emit(name, group, desc, key, vol, voices, x):
    if ONLY and ONLY not in name:
        return
    OUT.mkdir(parents=True, exist_ok=True)
    write_wav(OUT / (name + '.wav'), x)
    REG.append((name, group, desc, key, vol, voices, len(x) / SR))
    print(f'{name:36s} {len(x)/SR:5.2f}s peak {np.abs(x).max():.2f}')


def want(name):
    return not ONLY or ONLY in name


# ---------------------------------------------------------------- weapons
GUNS = {
    'rifle':   dict(crack=1.0, crack_hp=2200, body_lp=1900, body_tau=0.035, thump=100, thump_tau=0.06, dur=0.5, blast=0.25),
    'carbine': dict(crack=1.0, crack_hp=2600, body_lp=2300, body_tau=0.026, thump=125, thump_tau=0.045, dur=0.4, blast=0.12),
    'pistol':  dict(crack=0.9, crack_hp=2800, body_lp=2600, body_tau=0.014, thump=190, thump_tau=0.03, dur=0.25),
    'smg':     dict(crack=0.9, crack_hp=2600, body_lp=2400, body_tau=0.016, thump=170, thump_tau=0.032, dur=0.28),
    'lmg':     dict(crack=1.0, crack_hp=2000, body_lp=1800, body_tau=0.04, thump=95, thump_tau=0.06, dur=0.4, blast=0.3),
    'maxim':   dict(crack=0.85, crack_hp=1800, body_lp=1500, body_tau=0.05, thump=80, thump_tau=0.07, dur=0.45, blast=0.4),
    'atrifle': dict(crack=1.2, crack_hp=1800, body_lp=1300, body_tau=0.08, thump=62, thump_tau=0.13, dur=0.9, blast=0.9, blast_tau=0.2),
    'gun37':   dict(crack=1.1, crack_hp=1700, body_lp=1400, body_tau=0.09, thump=75, thump_tau=0.1, dur=1.0, blast=0.7, blast_tau=0.18),
    'field':   dict(crack=1.0, crack_hp=1300, body_lp=1000, body_tau=0.18, thump=52, thump_tau=0.22, dur=1.6, blast=1.0, blast_tau=0.3),
    'howitzer': dict(crack=0.9, crack_hp=1000, body_lp=700, body_tau=0.3, thump=40, thump_tau=0.4, dur=2.2, blast=1.2, blast_tau=0.45),
}


def dry(kind, rng):
    return gunshot(rng, **GUNS[kind])


def near(x, rng, rt=0.9, wet=0.22):
    return field(x, rng, wet=wet, rt=rt)


def far(x, rng, dist=1.0):
    return distant(x, rng, dist=dist, delay=0.05)


def bolt_cycle(rng, scale=1.0):
    """Bolt action: lift, pull back + case eject, push forward, lock down. ~0.7-0.9 s."""
    j = lambda v, a=0.012: v + rng.uniform(-a, a)
    buf = np.zeros(int(1.1 * SR))
    # 1. handle lift: sharp metal click + small knock
    t0 = j(0.02, 0.005)
    place(buf, metal_tick(rng, rng.uniform(2200, 3000), 1.0, 0.006), t0 * SR)
    place(buf, thud(rng, 600, 1800, 0.006, 0.04, 0.35), t0 * SR)
    # 2. pull back: metallic slide, stop clack, brass case ejection ping + tinkle
    t1 = t0 + j(0.13, 0.015)
    place(buf, metal_slide(rng, 0.11 * scale, 1500, 5500, 0.45), t1 * SR)
    t2 = t1 + 0.11 * scale
    place(buf, mix(metal_tick(rng, rng.uniform(1500, 2100), 1.1, 0.01), thud(rng, 450, 2200, 0.008, 0.05, 0.4)), t2 * SR)
    te = t2 + j(0.035, 0.01)
    f_c = rng.uniform(3600, 4600)
    tt = np.arange(int(0.25 * SR)) / SR
    tb = te
    for k, g in enumerate([0.35, 0.2, 0.11, 0.06]):
        fb = f_c * rng.uniform(0.92, 1.1)
        ping = (np.sin(2 * np.pi * fb * tt) + 0.5 * np.sin(2 * np.pi * fb * 2.76 * tt)) * np.exp(-tt / 0.04)
        place(buf, ping, tb * SR, g)
        tb += rng.uniform(0.04, 0.09) * (1 - 0.15 * k)
    # 3. push forward: slide + cartridge feed tick
    t3 = t2 + j(0.2, 0.02)
    place(buf, metal_slide(rng, 0.1 * scale, 1200, 4500, 0.4), t3 * SR)
    place(buf, metal_tick(rng, rng.uniform(1300, 1800), 0.5, 0.004), (t3 + 0.1 * scale + j(0.0, 0.003)) * SR)
    # 4. lock down: solid clack
    t4 = t3 + 0.1 * scale + j(0.09, 0.012)
    place(buf, mix(metal_tick(rng, rng.uniform(1200, 1700), 1.3, 0.012), thud(rng, 260, 1800, 0.012, 0.09, 0.8)), t4 * SR)
    return reverb(buf, rng, rt=0.22, wet=0.07, bright=4000)[:int(1.1 * SR)]


def burst(kind, rng, count, period, jit=0.12, gain_var=0.12, rt=0.9, wet=0.22, rattle=False):
    n = int((count * period + 1.0) * SR)
    buf = np.zeros(n)
    t = 0.0
    for i in range(count):
        s = dry(kind, rng)
        place(buf, s, t * SR, 1 - gain_var * rng.random())
        if rattle:
            place(buf, click(rng, rng.uniform(1500, 5000, 2), 0.008, 0.05, 0.12), (t + period * 0.5) * SR)
        t += period * (1 + rng.uniform(-jit, jit))
    return field(buf, rng, wet=wet, rt=rt)


def gen_weapons():
    for i in range(1, 5):
        n = f'rifle_shot_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Rifle', 'Bolt-action rifle (Mosin/Arisaka class), near shot', 'rifle', 0.8, 4,
                 finish(near(dry('rifle', r), r), 0.9))
    for i in range(1, 4):
        n = f'rifle_shot_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Rifle', 'Rifle shot heard from far away: muffled, delayed, echoing', 'rifle_far', 0.6, 4,
                 finish(far(dry('rifle', r), r, 1.0), 0.5))
    for i in range(1, 4):
        n = f'carbine_shot_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Carbine', 'Carbine, lighter and snappier than the rifle', 'carbine', 0.7, 4,
                 finish(near(dry('carbine', r), r, 0.8), 0.9))
    for i in range(1, 3):
        n = f'carbine_shot_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Carbine', 'Carbine, distant', 'carbine_far', 0.5, 4,
                 finish(far(dry('carbine', r), r, 1.0), 0.45))
    for i in range(1, 4):
        n = f'pistol_shot_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Pistol', 'Pistol (Mauser C96 / Nambu class), near', 'pistol', 0.6, 3,
                 finish(near(dry('pistol', r), r, 0.6, 0.18), 0.85))
    for i in range(1, 3):
        n = f'pistol_shot_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Pistol', 'Pistol, distant', 'pistol_far', 0.4, 3,
                 finish(far(dry('pistol', r), r, 0.9), 0.35))
    for i in range(1, 4):
        n = f'smg_burst_near_{i}'
        if want(n):
            r = rng_for(n)
            c = [4, 5, 6][i - 1]
            emit(n, 'SMG', f'MP18-style SMG short burst ({c} rounds, ~480 rpm)', 'smg', 0.7, 3,
                 finish(burst('smg', r, c, 0.125), 0.9))
    for i in range(1, 3):
        n = f'smg_burst_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'SMG', 'SMG burst, distant', 'smg_far', 0.5, 3,
                 finish(far(burst('smg', r, 5, 0.125, wet=0.05), r, 1.0), 0.45))
    for i in range(1, 4):
        n = f'lmg_burst_near_{i}'
        if want(n):
            r = rng_for(n)
            c = [6, 8, 10][i - 1]
            emit(n, 'LMG', f'Light machine gun burst ({c} rounds, ~600 rpm) with rattle', 'lmg', 0.8, 3,
                 finish(burst('lmg', r, c, 0.1, rattle=True), 0.9))
    for i in range(1, 3):
        n = f'lmg_burst_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'LMG', 'LMG burst, distant', 'lmg_far', 0.55, 3,
                 finish(far(burst('lmg', r, 7, 0.1, wet=0.05), r, 1.1), 0.5))
    for i in range(1, 3):
        n = f'atrifle_shot_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'AT rifle', 'Anti-tank rifle: heavy boom with muzzle blast', 'at_rifle', 0.95, 2,
                 finish(near(dry('atrifle', r), r, 1.1, 0.28), 0.95))
    n = 'atrifle_shot_far_1'
    if want(n):
        r = rng_for(n)
        emit(n, 'AT rifle', 'Anti-tank rifle, distant', 'at_rifle_far', 0.7, 2,
             finish(far(dry('atrifle', r), r, 1.1), 0.55))

    # Maxim: sustained, loopable, water-cooled feel (heavier thump, belt/lock rattle, steam hiss)
    def maxim_loop(r, dur, dist):
        period = 0.118
        cnt = int(round(dur / period))
        L = cnt * period
        n = int(L * SR)
        buf = np.zeros(n)
        for i in range(cnt):
            s = dry('maxim', r)
            if dist:
                s = lp(s, 1400)
            pos = (i * period + r.uniform(-0.006, 0.006)) * SR
            place(buf, s, pos, 1 - 0.18 * r.random(), wrap=True)
            place(buf, click(r, r.uniform(1200, 4200, 3), 0.01, 0.06, 0.2), pos + period * 0.45 * SR, wrap=True)
            place(buf, thud(r, 90, 400, 0.02, 0.08, 0.25), pos + period * 0.55 * SR, wrap=True)  # lock slam
        hiss = bp(noise(r, L), 3000, 9000) * 0.015
        buf = buf + hiss
        return field(buf, r, wet=0.25, rt=1.0, circular=True)
    n = 'maxim_burst_loop'
    if want(n):
        r = rng_for(n)
        emit(n, 'Maxim HMG', 'Maxim MG08 sustained fire, seamless loop (~3 s), water-cooled heavy thump', 'hmg_loop', 0.8, 2,
             finish(maxim_loop(r, 3.0, False), 0.9, loop=True, target_rms=0.2))
    n = 'maxim_burst_far_loop'
    if want(n):
        r = rng_for(n)
        emit(n, 'Maxim HMG', 'Maxim sustained fire heard from far, seamless loop', 'hmg_loop_far', 0.55, 2,
             finish(lp(maxim_loop(r, 3.0, True), 1600) * 0.7, 0.8, loop=True, target_rms=0.09))
    for i in range(1, 3):
        n = f'maxim_short_burst_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Maxim HMG', 'Maxim short burst (one-shot version, 5-7 rounds)', 'hmg', 0.85, 2,
                 finish(burst('maxim', r, 5 + i, 0.118, rattle=True, rt=1.0, wet=0.25), 0.92))


# ---------------------------------------------------------------- artillery
def whistle(rng, dur, f0, f1, level=1.0):
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    f = f0 + (f1 - f0) * t ** 1.6 + smooth_noise(rng, dur, 12) * 40
    s = sine_sweep(f) + 0.45 * sine_sweep(f * 2.01) + 0.15 * sine_sweep(f * 3)
    s = s * (1 + 0.15 * np.sin(2 * np.pi * 14 * t * dur))
    hiss = bp(noise(rng, dur), f0 * 0.6, f0 * 2.6) * 0.5
    env = (0.08 + 0.92 * t ** 1.8)
    return level * (s * 0.6 + hiss) * env


def gen_artillery():
    for kind, label, key, vol, rt in [('gun37', '37 mm infantry gun', 'gun37_fire', 0.9, 1.0),
                                      ('field', '75 mm field gun', 'artillery_fire', 1.0, 1.4),
                                      ('howitzer', '150 mm howitzer', 'howitzer_fire', 1.0, 1.8)]:
        for i in range(1, 3):
            n = f'{kind}_fire_near_{i}'
            if want(n):
                r = rng_for(n)
                x = dry(kind, r)
                if kind == 'field':
                    c = thud(r, 140, 900, 0.03, 0.2, 0.6) + click(r, [900, 1700, 2900], 0.04, 0.2, 0.6)
                    place(x, c, 0.35 * SR)   # recoil / breech clank
                emit(n, label, f'{label} firing, near, with recoil tail', key, vol, 2,
                     finish(near(x, r, rt, 0.3), 0.95))
            n = f'{kind}_fire_far_{i}'
            if want(n):
                r = rng_for(n)
                emit(n, label, f'{label} firing, heard from far away', key + '_far', vol * 0.6, 2,
                     finish(far(dry(kind, r), r, 1.2), 0.55))
    for i, (d, a, b, lbl) in enumerate([(1.4, 2200, 900, 'light/short'), (2.0, 1800, 700, 'medium'), (2.8, 1300, 500, 'heavy howitzer')], 1):
        n = f'shell_whistle_{i}'
        if want(n):
            r = rng_for(n)
            x = whistle(r, d, a, b)
            emit(n, 'Incoming shell', f'Incoming shell whistle ({lbl}, {d:.1f} s); play right before the impact', 'shell_incoming', 0.7, 3,
                 finish(x, 0.7, fade_out=0.01))
    for i in range(1, 4):
        n = f'shell_impact_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Impact', 'Field-gun shell impact / explosion, near', 'artillery_impact', 0.95, 4,
                 finish(field(explosion(r, 1.0 + 0.1 * i), r, 0.3, 1.5), 0.95, fade_out=0.15))
    for i in range(1, 3):
        n = f'howitzer_impact_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Impact', 'Heavy howitzer shell impact, near; long rumble', 'howitzer_impact', 1.0, 3,
                 finish(field(explosion(r, 2.0 + 0.3 * i, 3.4), r, 0.35, 2.2), 0.98, fade_out=0.25))
    for i in range(1, 4):
        n = f'shell_impact_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Impact', 'Shell impact heard from far away (dull thump + rumble)', 'artillery_impact_far', 0.6, 4,
                 finish(far(explosion(r, 1.4, 2.5), r, 1.4), 0.55, fade_out=0.3))
    for i in range(1, 4):
        n = f'grenade_explosion_near_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Grenade', 'Hand grenade burst: sharp crack, short rumble, debris', 'explosion', 0.9, 3,
                 finish(field(explosion(r, 0.55 + 0.05 * i, 1.8), r, 0.25, 1.0), 0.92, fade_out=0.1))
    for i in range(1, 3):
        n = f'grenade_explosion_far_{i}'
        if want(n):
            r = rng_for(n)
            emit(n, 'Grenade', 'Grenade burst, distant', 'explosion_far', 0.55, 3,
                 finish(far(explosion(r, 0.7, 1.8), r, 1.2), 0.5, fade_out=0.2))


# ---------------------------------------------------------------- melee / cavalry
def metal_hit(rng, level=1.0, dur=0.7):
    t = np.arange(int(dur * SR)) / SR
    k = rng.integers(4, 8)
    s = np.zeros_like(t)
    base = rng.uniform(700, 1500)
    for _ in range(k):
        f = base * rng.uniform(1, 6)
        s += rng.uniform(0.3, 1) * np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * np.exp(-t / rng.uniform(0.03, 0.18))
    s = s / k
    s += 0.9 * hp(noise(rng, dur), 2500) * np.exp(-t / 0.004)           # strike
    s += 0.3 * bp(noise(rng, dur), 1500, 6000) * np.exp(-t / 0.03)     # scrape
    return level * s


def hoof(rng, level=1.0, soft=False):
    return thud(rng, rng.uniform(95, 140), 500 if soft else 900, 0.022, 0.16, level) + \
        (0 if soft else np.pad(click(rng, rng.uniform(1200, 3000, 2), 0.006, 0.05, 0.18 * level), (0, 7056 - 2205)))


def gen_melee():
    for i in range(1, 5):
        n = f'melee_clash_{i}'
        if want(n):
            r = rng_for(n)
            buf = np.zeros(int(1.0 * SR))
            hits = 1 + (i % 3)
            t0 = 0
            for h in range(hits):
                place(buf, metal_hit(r, 1 - 0.25 * h), t0 * SR)
                if h < 1:
                    place(buf, thud(r, 90, 400, 0.03, 0.15, 0.4), t0 * SR)
                t0 += r.uniform(0.12, 0.3)
            emit(n, 'Melee', 'Bayonet / sword clash (steel on steel), 1-3 strikes', 'melee_hit', 0.65, 4,
                 finish(field(buf, r, 0.1, 0.5), 0.85))
    swings = {1: ('Sabre swing: air whoosh, medium weight', 0.38, (300, 1200, 420), 0.5, 0.22),
              2: ('Dadao swing: heavy, longer air whoosh', 0.46, (230, 950, 300), 0.5, 0.3),
              3: ('Bayonet thrust: fast, light air rush', 0.25, (400, 1500, 600), 0.42, 0.14)}
    for i in range(1, 4):
        n = f'blade_swing_{i}'
        if want(n):
            r = rng_for(n)
            desc, d, (a_, b_, c_), pk, ru = swings[i]
            x = air_whoosh(r, d, a_, b_, c_, peak_at=pk, sharp=2.2 if i != 3 else 3.0, rustle=ru)
            emit(n, 'Melee', desc, 'melee_swing', 0.5, 3,
                 finish(x, 0.7, fade_in=0.012, fade_out=0.06))
    for i in range(1, 3):
        n = f'blade_flesh_{i}'
        if want(n):
            r = rng_for(n)
            x = thud(r, 70, 600, 0.04, 0.3, 1.0) + 0.5 * bp(noise(r, 0.3), 300, 1800) * np.exp(-np.arange(int(0.3 * SR)) / SR / 0.05)
            emit(n, 'Melee', 'Blade / bayonet hitting a body (dull wet thud)', 'melee_flesh', 0.5, 3,
                 finish(x, 0.8))

    # cavalry loops (circular, integer strides per loop)
    def gallop(r, L, horses, stride_set, tack, rumble):
        n = int(L * SR)
        buf = np.zeros(n)
        for h in range(horses):
            k = int(r.choice(stride_set))
            stride = L / k
            ph = r.uniform(0, stride)
            gain = r.uniform(0.5, 1.0) / np.sqrt(horses) * 2.2
            pat = [0.0, 0.085, 0.19]   # three-beat gallop
            for s in range(k):
                for j, off in enumerate(pat):
                    place(buf, hoof(r, 1 - 0.15 * j, soft=r.random() < 0.3), (ph + s * stride + off * (stride / 0.43)) * SR + r.integers(-80, 80),
                          gain * r.uniform(0.7, 1.0), wrap=True)
        if tack:
            tk = hp(noise(r, L), 4000) * (np.abs(smooth_noise(r, L, 40)) ** 3) * tack
            buf += tk
        if rumble:
            buf += lp(noise(r, L), 120) * (0.6 + 0.4 * smooth_noise(r, L, 3)) * rumble
        return buf
    n = 'cavalry_gallop_loop'
    if want(n):
        r = rng_for(n)
        x = gallop(r, 5.6, 6, [12, 13], 0.02, 0.5)
        emit(n, 'Cavalry', 'Small cavalry group galloping, seamless loop (5.6 s)', 'cavalry_move', 0.6, 2,
             finish(field(x, r, 0.15, 0.6, circular=True), 0.9, loop=True, target_rms=0.15))
    n = 'cavalry_charge_loop'
    if want(n):
        r = rng_for(n)
        x = gallop(r, 6.0, 22, [13, 14, 15], 0.05, 1.6)
        emit(n, 'Cavalry', 'Massed cavalry charge (22 horses), thunder of hooves, seamless loop', 'cavalry_charge', 0.8, 2,
             finish(field(x, r, 0.2, 0.8, circular=True), 0.9, loop=True, target_rms=0.2))
    # NOTE: synthetic whinny was not convincing -> replaced by snort / nicker / stomp
    def snort(r, d):
        N = int(d * SR); t = np.arange(N) / SR
        x = r.standard_normal(N)
        nasal = formant(x, [(350, 700), (900, 1500), (1900, 3000)], [1.0, 0.8, 0.35])
        flut = 0.55 + 0.45 * np.sin(2 * np.pi * r.uniform(24, 38) * t + r.uniform(0, 6)) * (0.6 + 0.4 * smooth_noise(r, d, 30))
        env = np.minimum(1, t / 0.015) * np.exp(-t / (d * 0.28))
        low = lp(r.standard_normal(N), 300) * np.exp(-t / (d * 0.3))
        return nasal * flut * env + 0.5 * low
    for i in (1, 2):
        n = f'horse_snort_{i}'
        if want(n):
            r = rng_for(n)
            buf = np.zeros(int(1.6 * SR))
            t0 = 0.02
            for k in range(1 if i == 1 else 2):
                d = r.uniform(0.3, 0.45)
                place(buf, snort(r, d), t0 * SR, 1 - 0.3 * k)
                t0 += d * r.uniform(0.8, 1.0) + 0.1
            emit(n, 'Horse', 'Horse snort / nasal blow' + (' (double)' if i == 2 else ''), 'horse_snort', 0.5, 2,
                 finish(field(buf, r, 0.1, 0.5), 0.8, fade_out=0.1))
    for i in (1, 2):
        n = f'horse_nicker_{i}'
        if want(n):
            r = rng_for(n)
            d = 1.0 + 0.3 * i
            N = int(d * SR); t = np.arange(N) / SR
            rate = r.uniform(6.5, 9)
            f0 = r.uniform(105, 135) * (1 - 0.12 * t / d) * (1 + 0.02 * smooth_noise(r, d, 15))
            ph = 2 * np.pi * np.cumsum(f0) / SR
            src = sum(np.sin(h * ph) / h for h in range(1, 30)) + 0.12 * r.standard_normal(N)
            voiced = formant(src, [(250, 450), (600, 1000), (1500, 2200)], [1.0, 0.8, 0.25])
            pulse = np.clip(np.sin(2 * np.pi * rate * t - 1.2), 0, 1) ** 1.5
            env = np.minimum(1, t / 0.08) * np.minimum(1, (d - t) / 0.25) * (1 - 0.3 * t / d)
            breath = formant(r.standard_normal(N), [(500, 1500)], [1.0]) * 0.25
            x = (voiced * (0.25 + pulse) + breath * pulse) * env
            emit(n, 'Horse', 'Horse nicker: low pulsed rumbling vocalisation', 'horse_nicker', 0.4, 2,
                 finish(field(x, r, 0.08, 0.4), 0.75, fade_out=0.15))
    n = 'horse_stomp_1'
    if want(n):
        r = rng_for(n)
        buf = np.zeros(int(0.9 * SR))
        t0 = 0.0
        for k in range(2):
            place(buf, mix(thud(r, 85, 600, 0.03, 0.25, 1.0 - 0.3 * k), hoof(r, 0.5)), t0 * SR)
            t0 += r.uniform(0.28, 0.4)
        place(buf, click(r, [2600, 4100], 0.006, 0.05, 0.12), 0.55 * SR)
        emit(n, 'Horse', 'Horse hoof stomp (2 beats, restless)', 'horse_stomp', 0.5, 2,
             finish(field(buf, r, 0.1, 0.5), 0.8))


# ---------------------------------------------------------------- supply / work / engines
def gen_work():
    n = 'supply_wagon_loop'
    if want(n):
        r = rng_for(n)
        L = 6.0
        N = int(L * SR)
        revs = 7   # wheel revolutions per loop
        t = np.arange(N) / SR
        rumble = lp(noise(r, L), 220) * (0.6 + 0.4 * np.sin(2 * np.pi * revs * t / L + 1) ** 2)
        grit = bp(noise(r, L), 600, 2500) * 0.06 * (0.5 + 0.5 * np.abs(smooth_noise(r, L, 18)))
        buf = rumble * 1.2 + grit
        for k in range(revs * 2):   # wooden knocks (spokes/axle)
            place(buf, thud(r, r.uniform(150, 260), 700, 0.018, 0.1, r.uniform(0.3, 0.6)), (k * L / (revs * 2) + r.uniform(-0.01, 0.01)) * SR, wrap=True)
        for _ in range(5):          # creaks
            d = r.uniform(0.25, 0.5)
            f = r.uniform(380, 720) * (1 + 0.25 * np.linspace(0, 1, int(d * SR)) * r.choice([-1, 1]))
            cr = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.5 * np.sin(4 * np.pi * np.cumsum(f) / SR)) * np.hanning(int(d * SR))
            cr = bp(cr + 0.6 * noise(r, d), 400, 2200) * 0.07
            place(buf, cr, r.uniform(0, L) * SR, wrap=True)
        for _ in range(int(L * 1.6)):   # plodding hoof + harness
            place(buf, hoof(r, 0.3, soft=True), r.uniform(0, L) * SR, wrap=True)
        emit(n, 'Logistics', 'Wooden-wheeled supply wagon rolling on a dirt road, seamless loop', 'wagon_loop', 0.5, 2,
             finish(buf, 0.9, loop=True, target_rms=0.1))
    n = 'trench_digging_loop'
    if want(n):
        r = rng_for(n)
        L = 6.0
        buf = np.zeros(int(L * SR))
        period = L / 4
        for k in range(4):
            t0 = k * period + r.uniform(-0.1, 0.1)
            d = 0.28
            sc = bp(noise(r, d), 700, 3200) * np.hanning(int(d * SR)) * 0.5     # blade into soil
            place(buf, sc, t0 * SR, wrap=True)
            place(buf, thud(r, 110, 500, 0.035, 0.2, 0.7), (t0 + 0.2) * SR, wrap=True)
            for j in range(r.integers(4, 9)):   # dirt thrown
                place(buf, lp(noise(r, 0.05), 3000) * np.hanning(int(0.05 * SR)) * 0.2, (t0 + 0.5 + r.uniform(0, 0.5)) * SR, wrap=True)
            if r.random() < 0.6:                # stone clink
                place(buf, click(r, [r.uniform(2500, 4500), r.uniform(5000, 7000)], 0.012, 0.1, 0.35), (t0 + 0.06) * SR, wrap=True)
        emit(n, 'Logistics', 'Shovel digging a trench: scrape, thud, dirt thrown, seamless loop', 'construct_loop', 0.6, 2,
             finish(field(buf, r, 0.08, 0.4, circular=True), 0.85, loop=True, target_rms=0.09))
    for i in range(1, 3):
        n = f'shovel_hit_{i}'
        if want(n):
            r = rng_for(n)
            buf = np.zeros(int(0.9 * SR))
            place(buf, bp(noise(r, 0.22), 700, 3200) * np.hanning(int(0.22 * SR)) * 0.5, 0)
            place(buf, thud(r, 110, 500, 0.035, 0.2, 0.8), 0.17 * SR)
            if i == 2:
                place(buf, click(r, [3300, 5600], 0.012, 0.1, 0.4), 0.05 * SR)
            emit(n, 'Logistics', 'Single shovel strike (one-shot)', 'tool_hit', 0.6, 3, finish(buf, 0.8))

    # FT tank engine v2: Renault 4-cyl ~39 hp, low RPM. Everything placed circularly -> seamless.
    def engine(r, L, fire, jitter, tappet, whine, clank, squeak, rattle):
        N = int(L * SR)
        t = np.arange(N) / SR
        cycles = int(round(fire * L))
        per = L / cycles
        eng = np.zeros(N)
        pulse_t = []
        for k in range(cycles):
            tk = (k + r.uniform(-jitter, jitter)) * per
            pulse_t.append(tk)
            g = r.uniform(0.55, 1.0) * (0.35 if r.random() < 0.06 else 1.0)
            tt = np.arange(int(0.075 * SR)) / SR
            p = np.sin(2 * np.pi * r.uniform(52, 72) * tt) * np.exp(-tt / 0.03) \
                + 0.9 * lp(r.standard_normal(len(tt)), r.uniform(280, 520)) * np.exp(-tt / 0.018) \
                + 0.25 * bp(r.standard_normal(len(tt)), 600, 1500) * np.exp(-tt / 0.008)
            place(eng, p * np.minimum(1, tt / 0.002), tk * SR, g, wrap=True)
            if tappet:
                for off in (0.2, 0.55):
                    place(eng, metal_tick(r, r.uniform(2600, 4200), tappet * r.uniform(0.5, 1), 0.0025, 0.03, 0.4),
                          (tk + per * off + r.uniform(0, 0.004)) * SR, 1.0, wrap=True)
        wob = np.interp(t, np.linspace(0, L, cycles + 1), np.append(r.uniform(0, 1, cycles), 0))
        eng += 0.35 * circ(lambda z: lp(z, 140), r.standard_normal(N)) * (0.6 + 0.4 * wob)
        if rattle:
            for tk in pulse_t:
                if r.random() < 0.5:
                    d = r.uniform(0.02, 0.05)
                    rb = bp(r.standard_normal(int(d * SR)), 250, 900) * np.hanning(int(d * SR))
                    place(eng, rb, (tk + r.uniform(0.005, 0.02)) * SR, rattle * r.uniform(0.3, 1), wrap=True)
        if whine:   # low, subtle gear whine: integer-cycle tone + narrow noise
            k = max(1, round(whine * L))
            eng += 0.035 * np.sin(2 * np.pi * k / L * t) * (0.7 + 0.3 * wob)
            wn = circ(lambda z: bp(z, whine * 0.9, whine * 1.1, 2), r.standard_normal(N))
            eng += 0.05 * wn / (np.abs(wn).max() + 1e-9)
        if clank:   # track links, rate synced to speed
            nk = int(round(clank * L))
            for k in range(nk):
                tk = (k + r.uniform(-0.08, 0.08)) / clank
                c = metal_tick(r, r.uniform(700, 1500), 0.8 * r.uniform(0.6, 1), 0.012, 0.1, 0.5) \
                    + thud(r, r.uniform(110, 170), 500, 0.02, 0.1, 0.5)
                place(eng, c, tk * SR, 0.6, wrap=True)
            eng += 0.14 * circ(lambda z: lp(hp(z, 200), 1800), r.standard_normal(N)) * (0.6 + 0.4 * np.abs(np.sin(np.pi * clank * t)))
        for _ in range(squeak):
            d = r.uniform(0.18, 0.3)
            tt = np.arange(int(d * SR)) / SR
            fq = r.uniform(1300, 1900)
            sq = bp(r.standard_normal(len(tt)), fq * 0.93, fq * 1.07, 2) * np.hanning(len(tt)) * (0.6 + 0.4 * np.sin(2 * np.pi * 14 * tt))
            place(eng, sq / (np.abs(sq).max() + 1e-9), r.uniform(0, L - d) * SR, 0.12, wrap=True)
        return eng

    def ft(name, desc, key, vol, voices, L, fire, rms, **kw):
        if want(name):
            r = rng_for(name)
            x = engine(r, L, fire, **kw)
            emit(name, 'Tank (FT)', desc, key, vol, voices,
                 finish(field(x, r, 0.05, 0.25, circular=True), 0.9, loop=True, target_rms=rms))
    ft('ft_engine_idle_loop', 'FT-17 Renault 4-cyl idle: uneven low pulses (~14 Hz), putter, tappet clatter, seamless loop (4 s)',
       'tank_idle', 0.5, 1, 4.0, 14.0, 0.12, jitter=0.22, tappet=0.22, whine=0, clank=0, squeak=0, rattle=0.35)
    ft('ft_engine_moving_loop', 'FT-17 engine moving: higher pulse rate (~22 Hz), subtle low gear whine, track clank, seamless loop (4 s)',
       'tank_move', 0.6, 1, 4.0, 22.0, 0.15, jitter=0.15, tappet=0.2, whine=240, clank=7.0, squeak=1, rattle=0.3)
    ft('ft_tracks_loop', 'FT-17 engine + prominent track clank and idler squeak (full movement sound), seamless loop (4 s)',
       'tank_tracks', 0.65, 1, 4.0, 20.0, 0.16, jitter=0.15, tappet=0.15, whine=210, clank=9.0, squeak=2, rattle=0.3)


# ---------------------------------------------------------------- suppression
def gen_pass():
    for i in range(1, 5):
        n = f'bullet_whiz_{i}'
        if want(n):
            r = rng_for(n)
            d = r.uniform(0.18, 0.3)
            N = int(d * SR)
            t = np.linspace(0, 1, N)
            f = (5200 + r.uniform(-600, 600)) * (1 - 0.55 * t ** 0.8)    # Doppler drop
            tone = sine_sweep(f) * 0.6 + bp(noise(r, d), 2500, 7000) * 0.7
            env = np.exp(-((t - 0.38) / 0.22) ** 2)
            x = tone * env
            x[: int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))
            emit(n, 'Suppression', 'Bullet passing close overhead: Doppler zing', 'bullet_whiz', 0.5, 4,
                 finish(x, 0.65, fade_out=0.03))
    for i in range(1, 4):
        n = f'bullet_crack_{i}'
        if want(n):
            r = rng_for(n)
            d = 0.35
            t = np.arange(int(d * SR)) / SR
            c = hp(noise(r, d), 3000) * np.exp(-t / 0.0016) * 1.4 + hp(noise(r, d), 1200) * np.exp(-t / 0.006) * 0.5
            c += whoosh(r, d, 2500, 7000, 0.15, 0.08, 4) * 0.2
            emit(n, 'Suppression', 'Supersonic crack of a near miss (sharp snap + brief echo)', 'bullet_crack', 0.6, 4,
                 finish(field(c, r, 0.12, 0.5), 0.85))
    for i in range(1, 5):
        n = f'ricochet_{i}'
        if want(n):
            r = rng_for(n)
            d = r.uniform(0.45, 0.8)
            N = int(d * SR)
            t = np.arange(N) / SR
            f0 = r.uniform(2200, 3600)
            f = f0 * (0.55 + 0.45 * np.exp(-t / 0.25)) * (1 + 0.012 * np.sin(2 * np.pi * r.uniform(18, 30) * t))
            ping = (sine_sweep(f) + 0.35 * sine_sweep(f * 1.51) + 0.2 * sine_sweep(f * 2.3)) * np.exp(-t / r.uniform(0.12, 0.25))
            x = ping * 0.7 + 0.9 * hp(noise(r, d), 2500) * np.exp(-t / 0.005) + 0.2 * bp(noise(r, d), 3000, 8000) * np.exp(-t / 0.08)
            emit(n, 'Suppression', 'Ricochet off stone/metal (ping with falling pitch)', 'ricochet', 0.55, 4,
                 finish(field(x, r, 0.12, 0.6), 0.8, fade_out=0.08))


# ---------------------------------------------------------------- commands (officer whistle, charge shout)
def pea_whistle(rng, dur, f0, level=1.0, trill=38.0, breath=0.10, attack=0.025, release=0.06):
    """trench / pea whistle: near-sinusoidal shriek with 2nd/3rd partials, pea rattle as
    irregular amplitude + small frequency wobble, plus breath noise at the onset."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    tt = np.linspace(0, 1, n)
    vib = 1 + 0.006 * smooth_noise(rng, dur, 5) + 0.003 * np.sin(2 * np.pi * 6.0 * t)
    f = f0 * vib * (1 + 0.02 * np.exp(-t / 0.03))            # slight chiff-up at the start
    ph = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(ph) + 0.32 * np.sin(2 * ph + 0.4) + 0.10 * np.sin(3 * ph + 1.1)
    # pea rattle: rate wanders around `trill` Hz, depth ~45% with ragged peaks
    rate = trill * (1 + 0.18 * smooth_noise(rng, dur, 3))
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * np.cumsum(rate) / SR + 0.7)
    rattle = 1 - 0.45 * lfo ** 1.5 - 0.1 * np.abs(smooth_noise(rng, dur, 40))
    env = np.minimum(1, t / attack) * np.minimum(1, np.maximum(0, (dur - t)) / release)
    env = env * (0.92 + 0.08 * smooth_noise(rng, dur, 2))
    s = tone * rattle * env
    s += breath * bp(noise(rng, dur), f0 * 0.8, f0 * 3.0, 2) * np.minimum(1, t / 0.012) * np.exp(-t / 0.22)
    return level * s


def officer_whistle_sound(rng, pattern, f0):
    """pattern: list of (start, length) blasts in seconds; each blast separately shaped."""
    total = max(s + l for s, l in pattern) + 0.12
    out = np.zeros(int(total * SR))
    for s, l in pattern:
        b = pea_whistle(rng, l, f0 * (1 + rng.uniform(-0.02, 0.02)), level=rng.uniform(0.92, 1.0))
        place(out, b, s * SR)
    return out


def crowd_shout(rng, dur, pattern, n_voices=26, pitch=(135, 230)):
    """distant crowd 'hurrah': many glottal sources with random pitch/timing, formant-filtered
    into two vowel colours and crossfaded; syllable pattern shared but jittered per voice."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    vowel_a = [(600, 900), (1000, 1400), (2300, 3000)]    # 'ah'
    vowel_u = [(280, 420), (650, 900), (2000, 2600)]      # 'oo'
    vow_track = smooth_noise(rng, dur, 3)                 # 0..1 mix between vowels
    vmix = 0.5 + 0.5 * vow_track
    src_a = np.zeros(n)
    src_u = np.zeros(n)
    for _ in range(n_voices):
        f0 = rng.uniform(*pitch) * (1 + 0.04 * smooth_noise(rng, dur, 2))
        ph = 2 * np.pi * np.cumsum(f0) / SR
        g = np.zeros(n)
        for k in range(1, 22):
            if k * 400 > 4500:
                break
            g += np.sin(k * ph + rng.uniform(0, 6)) / k ** 1.1
        # per-voice syllable envelope: 'hu-RAH' then sustained roar, jittered start
        j = rng.uniform(-0.09, 0.09)
        tv = t - j
        env = np.zeros(n)
        for s, l, a in pattern:
            seg = (tv >= s) & (tv < s + l)
            x = (tv[seg] - s) / l
            env[seg] = a * np.sin(np.pi * np.clip(x, 0, 1)) ** 0.7
        env = env * (0.75 + 0.25 * smooth_noise(rng, dur, 6))
        amp = rng.uniform(0.6, 1.0)
        src_a += amp * env * g
        src_u += amp * env * g * rng.uniform(0.9, 1.1)
    # breath / consonant hiss mixed in, follows syllable energy
    hiss_env = np.zeros(n)
    for s, l, a in pattern:
        seg = (t >= s) & (t < s + l)
        hiss_env[seg] = a * np.exp(-((t[seg] - s) / (0.12 * l + 0.05)))
    hiss = bp(noise(rng, dur), 2500, 6500, 2) * hiss_env * 0.35
    fa = formant(src_a, vowel_a, [1.0, 0.7, 0.35])
    fu = formant(src_u, vowel_u, [1.0, 0.6, 0.25])
    out = fa * vmix + fu * (1 - vmix) + hiss
    return out


def gen_command():
    # --- officer whistle: short blast, long blast, triple-blast charge signal
    whistle_plan = [
        ('officer_whistle_1', 'short blast (single 0.45 s blast, ~2.9 kHz)', [(0.0, 0.45)], 2900, 0.70),
        ('officer_whistle_2', 'long blast (1.6 s blast, ~2.7 kHz)', [(0.0, 1.6)], 2700, 0.70),
        ('officer_whistle_3', 'charge signal (three short blasts then a long blast)',
         [(0.0, 0.22), (0.30, 0.22), (0.60, 0.22), (0.95, 1.1)], 2800, 0.70),
    ]
    for n, lbl, pattern, f0, peak in whistle_plan:
        if want(n):
            r = rng_for(n)
            x = officer_whistle_sound(r, pattern, f0)
            x = field(x, r, wet=0.28, rt=1.1)              # open-ground outdoor echo
            emit(n, 'Commands', f'Trench/pea whistle {lbl}; officer order (charge / advance / fire)',
                 'officer_whistle', 0.7, 2, finish(x, peak, fade_in=0.002, fade_out=0.05))
    # --- distant crowd 'hurrah' charge shouts
    shout_plan = [
        ('charge_shout_far_1', 5.0, [(0.00, 0.42, 1.0), (0.48, 0.42, 0.9), (1.02, 1.6, 1.0), (2.9, 1.4, 0.8)]),
        ('charge_shout_far_2', 5.6, [(0.10, 0.38, 0.9), (0.56, 0.38, 1.0), (1.02, 1.9, 1.0), (3.2, 1.8, 0.75)]),
    ]
    for n, dur, pattern in shout_plan:
        if want(n):
            r = rng_for(n)
            x = crowd_shout(r, dur + 0.5, pattern)
            x = x[: int(dur * SR)]
            x = distant(x, r, dist=1.2, delay=0.05)
            emit(n, 'Commands', 'Distant crowd "hurrah" roar of a charging company (no words; far version with pre-delay and tail)',
                 'charge_shout', 0.8, 2, finish(x, 0.72, fade_in=0.02, fade_out=0.6))


# ---------------------------------------------------------------- ambient
def gen_ambient():
    n = 'ambient_battle_distant_loop'
    if want(n):
        r = rng_for(n)
        L = 24.0
        buf = np.zeros(int(L * SR))
        # rumble bed
        buf += lp(noise(r, L), 90) * (0.5 + 0.5 * np.abs(smooth_noise(r, L, 0.7))) * 0.5
        for _ in range(38):      # far rifle exchanges
            p = r.uniform(0, L)
            kind = r.choice(['rifle', 'carbine'])
            s = lp(dry(kind, r), 1500)
            place(buf, s, p * SR, r.uniform(0.15, 0.4), wrap=True)
            for _ in range(r.integers(0, 4)):
                place(buf, lp(dry(kind, r), 1400), (p + r.uniform(0.2, 2.5)) * SR, r.uniform(0.1, 0.3), wrap=True)
        for _ in range(4):       # far MG bursts
            p = r.uniform(0, L)
            for k in range(r.integers(8, 18)):
                place(buf, lp(dry('maxim', r), 1200), (p + k * 0.118) * SR, 0.22, wrap=True)
        for _ in range(7):       # far artillery thumps
            p = r.uniform(0, L)
            place(buf, lp(explosion(r, 1.6, 2.5), 400, 3), p * SR, r.uniform(0.35, 0.7), wrap=True)
        x = field(buf, r, 0.45, 2.2, circular=True)
        emit(n, 'Ambient', 'Distant battle: scattered rifles, MG bursts, artillery thumps, rumble (24 s loop)', 'ambient_battle', 0.4, 1,
             finish(lp(x, 2200), 0.85, loop=True, target_rms=0.06))
    n = 'wind_loop'
    if want(n):
        r = rng_for(n)
        L, X = 12.0, 1.0
        N = int((L + X) * SR)
        base = noise(r, L + X)
        w1 = bp(base, 150, 600) * (0.6 + 0.4 * smooth_noise(r, L + X, 0.5))
        w2 = bp(base, 600, 1800) * (0.35 + 0.35 * smooth_noise(r, L + X, 0.8)) * 0.4
        w3 = hp(base, 2500) * 0.02 * (0.5 + 0.5 * smooth_noise(r, L + X, 1.5))
        x = (w1 + w2 + w3) * (0.7 + 0.3 * smooth_noise(r, L + X, 0.25))
        emit(n, 'Ambient', 'Open-field wind with slow gusts (12 s loop)', 'ambient_wind', 0.35, 1,
             finish(make_loop(x, X), 0.8, loop=True, target_rms=0.07))
    n = 'ambient_night_loop'
    if want(n):
        r = rng_for(n)
        L, X = 14.0, 1.0
        tot = L + X
        base = noise(r, tot)
        w = bp(base, 120, 450) * (0.6 + 0.4 * smooth_noise(r, tot, 0.35)) + bp(base, 450, 1200) * 0.12 * (0.5 + 0.5 * smooth_noise(r, tot, 0.6))
        wind = make_loop(w * (0.7 + 0.3 * smooth_noise(r, tot, 0.2)), X)
        wind = wind / np.sqrt(np.mean(wind ** 2)) * 0.05
        NL = len(wind)
        t_ = lambda d: np.arange(int(d * SR)) / SR
        buf = np.zeros(NL)

        def chirp(f, pulses, rate, level):
            d = pulses / rate + 0.03
            t = t_(d)
            car = np.sin(2 * np.pi * f * t * (1 + 0.01 * np.sin(2 * np.pi * 7 * t)))
            ph = (t * rate) % 1.0
            env = np.where(ph < 0.55, np.sin(np.pi * ph / 0.55) ** 2, 0.0) * np.minimum(1, (d - t) / 0.03)
            return car * env * level
        voices = [(r.uniform(3600, 5200), r.uniform(22, 34), r.uniform(2.8, 5.0), r.uniform(0.3, 0.9)) for _ in range(6)]
        for f0, rate, gap, lv in voices:       # each voice: bursts of 2-4 pulse groups with jitter
            p = r.uniform(0, gap)
            while p < L:
                for k in range(r.integers(2, 5)):
                    ev = chirp(f0 * r.uniform(0.985, 1.015), int(r.integers(3, 6)), rate * r.uniform(0.95, 1.05), lv * r.uniform(0.7, 1.0))
                    place(buf, ev, (p + k * 0.2 * r.uniform(0.9, 1.2)) * SR, wrap=True)
                p += gap * r.uniform(0.6, 1.5)
        for f0 in (2700, 3100):                  # slow low trills (field crickets / katydids)
            for _ in range(2):
                place(buf, chirp(f0, int(r.integers(10, 18)), 14, 0.5), r.uniform(0, L) * SR, wrap=True)
        ev = np.zeros(NL)
        t = t_(0.9)                              # owl: two soft hoots
        hoot = lambda f, d: np.sin(2 * np.pi * f * t_(d) * (1 - 0.04 * t_(d))) * np.sin(np.pi * np.linspace(0, 1, int(d * SR))) ** 2
        o = np.concatenate([hoot(390, 0.35), np.zeros(int(0.15 * SR)), hoot(360, 0.5)])
        place(ev, lp(o, 900) * 0.8, r.uniform(1, L - 2) * SR, wrap=True)
        for k in range(3):                       # distant dog: bark triplet
            d = 0.16
            tt = t_(d)
            b = (bp(noise(r, d), 350, 1400) * 0.7 + np.sin(2 * np.pi * (480 - 120 * tt / d) * tt) * 0.5) * np.exp(-tt / 0.06)
            place(ev, b, (r.uniform(0, 0.0) + 8.2 + k * 0.4) * SR, 0.5, wrap=True)
        for k in range(2):                       # very distant gunfire pops
            place(ev, lp(dry('rifle', r), 1200), (3.0 + k * r.uniform(5, 6.5)) * SR, 0.5, wrap=True)
        ev = field(ev, r, 0.5, 2.0, circular=True)
        ev = distant(ev, r, dist=1.6)[:NL] if False else ev
        ev = lp(ev, 1800) * 0.35
        crick = buf / (np.sqrt(np.mean(buf ** 2)) + 1e-9) * 0.028
        x = wind + crick + ev
        emit(n, 'Ambient', 'Manchurian night field: crickets, soft wind, rare far dog/owl/gunfire (14 s loop)', 'ambient_night', 0.35, 1,
             finish(x, 0.4, loop=True, target_rms=0.06))
    n = 'rain_loop'
    if want(n):
        r = rng_for(n)
        L, X = 12.0, 1.0
        tot = L + X
        base = bp(noise(r, tot), 1500, 9000) * 0.5 + lp(noise(r, tot), 600) * 0.15
        x = base * (0.85 + 0.15 * smooth_noise(r, tot, 6))
        buf = np.zeros(int(tot * SR))
        for _ in range(int(tot * 160)):   # big drops
            place(buf, click(r, [r.uniform(2500, 6000)], 0.004, 0.025, r.uniform(0.04, 0.2)), r.uniform(0, tot - 0.05) * SR)
        x = x + buf
        emit(n, 'Ambient', 'Steady rain on open ground (12 s loop)', 'ambient_rain', 0.4, 1,
             finish(make_loop(x, X), 0.8, loop=True, target_rms=0.08))
    n = 'rain_heavy_loop'
    if want(n):
        r = rng_for(n)
        L, X = 12.0, 1.0
        tot = L + X
        x = bp(noise(r, tot), 800, 10000) * 0.8 + lp(noise(r, tot), 400) * 0.3
        x = x * (0.85 + 0.15 * smooth_noise(r, tot, 4))
        x = x + lp(noise(r, tot), 120) * 0.2 * (0.5 + 0.5 * np.abs(smooth_noise(r, tot, 0.3)))   # distant thunder-ish rumble
        emit(n, 'Ambient', 'Heavy downpour (12 s loop)', 'ambient_rain_heavy', 0.45, 1,
             finish(make_loop(x, X), 0.8, loop=True, target_rms=0.11))


# ---------------------------------------------------------------- docs
def write_docs():
    REG.sort(key=lambda e: (e[1], e[0]))
    groups = {}
    for e in REG:
        groups.setdefault(e[1], []).append(e)
    h = ['<!doctype html><html lang="ko"><meta charset="utf-8"><title>Korean Division SFX audition</title>',
         '<style>body{font:15px sans-serif;max-width:900px;margin:20px auto;padding:0 12px}h2{border-bottom:1px solid #999;margin-top:28px}',
         'div.s{display:flex;align-items:center;gap:10px;margin:6px 0}div.s b{width:230px;font-family:monospace;font-size:13px}',
         'span.d{color:#555;font-size:13px}audio{height:32px}</style>',
         '<h1>Korean Division default SFX (procedural)</h1><p>mono / 16-bit / 44.1 kHz. Click to audition.</p>']
    for g, es in groups.items():
        h.append(f'<h2>{html.escape(g)}</h2>')
        for name, _, desc, key, vol, voices, dur in es:
            h.append(f'<div class="s"><b>{name}.wav</b><audio controls preload="none" src="{name}.wav"></audio>'
                     f'<span class="d">{dur:.1f}s - {html.escape(desc)}</span></div>')
    (OUT / 'index.html').write_text('\n'.join(h), encoding='utf-8')

    keys = {}
    for name, g, desc, key, vol, voices, dur in REG:
        keys.setdefault(key, []).append((name, vol, voices))
    m = ['# Korean Division default SFX set (procedural)', '',
         'Generated by `game-tools/audio-sfx/generate.py`. Mono, 16-bit, 44.1 kHz WAV (same as the game\'s existing ww2 sounds).',
         'Peaks: near/one-shot ~0.9, far ~0.5; loops are RMS-normalised and seamless. All files are original synthesis (no samples).', '',
         '## Suggested sounds.json mapping', '',
         'Existing keys are marked (existing). "far" variants: use when the source is off-screen / far from the camera (PlayAt falloff). `vol` is the suggested sounds.json volume, `voices` = maxVoices.', '',
         '| event key | files | vol | maxVoices | note |', '|---|---|---|---|---|']
    existing = {'rifle', 'carbine', 'explosion', 'artillery_fire', 'artillery_impact', 'tool_hit', 'tank_cannon'}
    notes = {'hmg_loop': 'loop while firing; stop on cease-fire',
             'cavalry_charge': 'loop while charging', 'tank_tracks': 'loop while moving',
             'shell_incoming': 'play ~whistle-length before impact (pick by shell calibre)',
             'ambient_night': 'night weather loop (crickets, soft wind)', 'ambient_battle': 'global loop, scale with fighting intensity', 'tank_cannon': 'reuse field/gun37 fire as alternative (new)',
             'officer_whistle': 'play when an officer orders company charge / marching-fire start (triple blast = charge; single = advance/halt)',
             'charge_shout': 'company charge: play together with officer_whistle_3 at charge start; far variants only (no near version yet)'}
    for key, fs in sorted(keys.items()):
        files = ', '.join(f'`{n}.wav`' for n, _, _ in fs)
        tag = ' (existing)' if key in existing else ''
        m.append(f'| `{key}`{tag} | {files} | {fs[0][1]} | {fs[0][2]} | {notes.get(key, "")} |')
    m += ['', '## Notes', '- Loops (`*_loop.wav`) loop sample-exactly; use Raylib `PlaySound` re-trigger when `!IsSoundPlaying`, or `LoadMusicStream` for the long ambient loops.',
          '- Far versions already include a ~50 ms pre-delay, tail and low-pass; the game only needs to pick the near or far key by distance.',
          '- RENAMED: `horse_whinny_1..3` -> `horse_snort_1..2` (key `horse_snort`), `horse_nicker_1..2` (`horse_nicker`), `horse_stomp_1` (`horse_stomp`); a synthetic whinny was not convincing. `blade_swing_1..3` = sabre / dadao / bayonet thrust (key `melee_swing` unchanged).',
          '- Pitch jitter in Sfx.Play (+-6%) works fine on all of these.',
          '- Re-generate / tweak: `python generate.py [outdir] [--only substring]`. Files are deterministic (seeded by name).']
    (OUT / 'README.md').write_text('\n'.join(m), encoding='utf-8')


if __name__ == '__main__':
    for fn in (gen_weapons, gen_artillery, gen_melee, gen_work, gen_pass, gen_command, gen_ambient):
        fn()
    write_docs()
    total = sum(p.stat().st_size for p in OUT.glob('*.wav'))
    print(f'{len(REG)} files, {total/1e6:.1f} MB')
