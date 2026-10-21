"""DSP helpers for procedural 1920-era battlefield SFX (mono, 44.1 kHz, 16-bit)."""
import zlib
import numpy as np
from scipy import signal

SR = 44100


def rng_for(name):
    return np.random.default_rng(zlib.crc32(name.encode()))


def noise(rng, dur):
    return rng.standard_normal(int(dur * SR))


def _fc(fc):
    return float(np.clip(fc, 20, SR / 2 - 500))


def lp(x, fc, order=2):
    return signal.sosfilt(signal.butter(order, _fc(fc), 'low', fs=SR, output='sos'), x)


def hp(x, fc, order=2):
    return signal.sosfilt(signal.butter(order, _fc(fc), 'high', fs=SR, output='sos'), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [_fc(lo), _fc(hi)], 'band', fs=SR, output='sos'), x)


def smooth_noise(rng, dur, rate):
    """slow random curve in [-1,1], `rate` ~ changes per second"""
    n = int(dur * SR)
    k = max(2, int(dur * rate) + 2)
    pts = rng.uniform(-1, 1, k)
    return np.interp(np.arange(n), np.linspace(0, n, k), pts)


def sine_sweep(f_of_t):
    return np.sin(2 * np.pi * np.cumsum(f_of_t) / SR)


def place(buf, ev, pos, gain=1.0, wrap=False):
    pos = int(pos)
    n = len(buf)
    if wrap:
        idx = (np.arange(len(ev)) + pos) % n
        np.add.at(buf, idx, ev * gain)
        return
    if pos >= n or pos < 0:
        return
    m = min(len(ev), n - pos)
    buf[pos:pos + m] += ev[:m] * gain


def make_ir(rng, rt, early=(), bright=3500):
    n = int(rt * 1.1 * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / rt)
    ir = lp(ir, bright)
    ir *= np.minimum(1, t / 0.004)
    for d, g in early:
        i = int(d * SR)
        if i < n - 400:
            ir[i:i + 300] += g * lp(rng.standard_normal(300), 2500) * np.exp(-np.arange(300) / 90) * 6
    return ir / np.sqrt(np.sum(ir ** 2) + 1e-9)


def reverb(x, rng, rt=0.8, wet=0.3, early=(), bright=3500, circular=False):
    ir = make_ir(rng, rt, early, bright)
    w = signal.fftconvolve(x, ir)
    if circular:
        out = w[:len(x)].copy()
        tail = w[len(x):]
        for k in range(0, len(tail), len(x)):
            seg = tail[k:k + len(x)]
            out[:len(seg)] += seg
        return x * (1 - wet * 0.5) + out * wet * 1.6
    x = np.concatenate([x, np.zeros(len(w) - len(x))])
    return x * (1 - wet * 0.5) + w * wet * 1.6


def field(x, rng, wet=0.25, rt=0.9, circular=False):
    """open field: tree-line / hill echoes + diffuse tail"""
    early = [(rng.uniform(0.12, 0.22), 0.25), (rng.uniform(0.3, 0.5), 0.18), (rng.uniform(0.55, 0.8), 0.1)]
    return reverb(x, rng, rt=rt, wet=wet, early=early, bright=3200, circular=circular)


def distant(x, rng, dist=1.0, delay=0.0):
    """dist 0..~2: low-passed, extra tail, quieter, optional pre-delay"""
    y = lp(x, 2600 / (1 + dist * 0.9), 2)
    y = hp(y, 40)
    y = field(y, rng, wet=0.45 + 0.15 * dist, rt=1.3 + 0.4 * dist)
    y = y * (0.5 / (1 + 0.4 * dist))
    return np.concatenate([np.zeros(int(delay * SR)), y])


def make_loop(x, xfade=0.5):
    """Seamless loop from x (length L+xfade): crossfade tail into head."""
    X = int(xfade * SR)
    L = len(x) - X
    out = x[:L].copy()
    f = np.linspace(0, 1, X)
    out[:X] = x[:X] * np.sin(f * np.pi / 2) + x[L:] * np.cos(f * np.pi / 2)
    return out


def finish(x, peak=0.9, fade_in=0.001, fade_out=0.03, loop=False, trim_db=-60, target_rms=None):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x)
    if not loop:
        a = np.abs(x)
        idx = np.nonzero(a > a.max() * 10 ** (trim_db / 20))[0]
        if len(idx):
            x = x[:min(len(x), idx[-1] + int(0.05 * SR))]
    if not loop:
        fi = int(fade_in * SR)
        fo = min(int(fade_out * SR), len(x) // 2)
        if fi:
            x[:fi] *= np.linspace(0, 1, fi)
        if fo:
            x[-fo:] *= np.linspace(1, 0, fo) ** 2
    if target_rms:
        x = x * (target_rms / (np.sqrt(np.mean(x ** 2)) + 1e-9))
        if np.abs(x).max() > peak:
            x = np.tanh(x / peak) * peak
    else:
        x = x / (np.abs(x).max() + 1e-9) * peak
    return np.clip(x, -0.999, 0.999).astype(np.float32)


def write_wav(path, x):
    import wave
    pcm = (np.clip(x, -1, 1) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


# ------------------------------------------------------------------ source models
def gunshot(rng, crack=1.0, crack_hp=2200, body_lp=1800, body_tau=0.03, thump=100, thump_tau=0.05,
            dur=0.6, blast=0.0, blast_tau=0.15, jitter=0.12):
    def j(v):
        return v * (1 + rng.uniform(-jitter, jitter))
    n = int(dur * SR)
    t = np.arange(n) / SR
    c = hp(noise(rng, dur), j(crack_hp)) * np.exp(-t / j(0.0022))
    c2 = hp(noise(rng, dur), 5000) * np.exp(-t / 0.0007)
    body = lp(noise(rng, dur), j(body_lp)) * np.exp(-t / j(body_tau)) * np.minimum(1, t / 0.0005)
    f = j(thump) * (1 + 1.6 * np.exp(-t / 0.018))
    th = sine_sweep(f) * np.exp(-t / j(thump_tau))
    out = crack * (1.4 * c + 0.9 * c2) + body + 0.9 * th
    if blast:
        out += blast * lp(noise(rng, dur), 700) * np.exp(-t / j(blast_tau)) * 1.5
    return out


def click(rng, freqs=None, tau=0.012, dur=0.12, level=1.0):
    t = np.arange(int(dur * SR)) / SR
    if freqs is None:
        freqs = rng.uniform(1500, 5500, 3)
    s = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * rng.uniform(0.4, 1) for f in freqs)
    s = s * np.exp(-t / tau)
    s = s + 0.8 * hp(noise(rng, dur), 3000) * np.exp(-t / 0.002)
    return level * s / len(freqs)


def thud(rng, f=120, lpf=700, tau=0.03, dur=0.2, level=1.0):
    t = np.arange(int(dur * SR)) / SR
    s = lp(noise(rng, dur), lpf) * np.exp(-t / tau) * 0.8
    s = s + np.sin(2 * np.pi * f * (1 + 0.5 * np.exp(-t / 0.02)) * t) * np.exp(-t / (tau * 1.3))
    s = s + 0.3 * hp(noise(rng, dur), 2500) * np.exp(-t / 0.003)
    return level * s


def whoosh(rng, dur, f_lo, f_hi, peak_at=0.5, width=0.25, bands=6, down=False):
    """swept band-limited noise (blade swing / pass-by)"""
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    base = noise(rng, dur)
    out = np.zeros(n)
    edges = np.geomspace(f_lo, f_hi, bands + 1)
    for i in range(bands):
        k = (bands - 1 - i) if down else i
        center = peak_at - width * 0.6 + width * 1.2 * k / max(1, bands - 1)
        env = np.exp(-((t - center) / (width * 0.5)) ** 2)
        out += bp(base, edges[i], edges[i + 1], 2) * env
    return out


def explosion(rng, size=1.0, dur=2.5):
    n = int(dur * SR)
    t = np.arange(n) / SR
    out = 1.3 * hp(noise(rng, dur), 1500) * np.exp(-t / 0.004)
    for fc, tau, g in [(5000, 0.05, 0.7), (2000, 0.14, 1.0), (700, 0.35, 1.1), (220, 0.7, 1.0)]:
        out += g * lp(noise(rng, dur), fc, 3) * np.exp(-t / (tau * size ** 0.4)) * np.minimum(1, t / 0.002)
    f = 55 * (1 + 1.5 * np.exp(-t / 0.08)) / (size ** 0.2)
    out += 1.6 * sine_sweep(f) * np.exp(-t / (0.5 * size ** 0.5))
    for _ in range(int(60 * size)):
        p = rng.exponential(0.5 * size ** 0.4) + 0.12
        if p < dur - 0.1:
            c = click(rng, rng.uniform(800, 4000, 2), tau=rng.uniform(0.004, 0.02), dur=0.06, level=rng.uniform(0.03, 0.15))
            place(out, c, p * SR)
    return out


# ------------------------------------------------------------------ revised models (v2)
def air_whoosh(rng, dur, f_lo=300, f_hi=1200, f_end=400, peak_at=0.5, sharp=2.0, q=1.4, rustle=0.25, tilt_lp=3200):
    """Realistic air whoosh: broadband noise through a moving band-pass (f_lo -> f_hi -> f_end),
    amplitude peaking mid-swing (Doppler-like), plus cloth/arm rustle. No tonal components."""
    n = int(dur * SR)
    x = rng.standard_normal(n)
    t = np.linspace(0, 1, n)
    # centre frequency track (log domain): rises to f_hi at peak_at then falls to f_end
    up = np.clip(t / peak_at, 0, 1)
    dn = np.clip((t - peak_at) / (1 - peak_at), 0, 1)
    fc = np.where(t < peak_at, f_lo * (f_hi / f_lo) ** (np.sin(up * np.pi / 2)),
                  f_hi * (f_end / f_hi) ** (np.sin(dn * np.pi / 2)))
    blk, hop = 1024, 512
    win = np.hanning(blk + 1)[:-1]
    out = np.zeros(n + blk)
    pad = np.concatenate([x, np.zeros(blk)])
    for s in range(-hop, n, hop):
        a = max(s, 0)
        seg = np.zeros(blk)
        lo_i = s
        idx = np.arange(blk) + lo_i
        valid = (idx >= 0) & (idx < n)
        seg[valid] = x[idx[valid]]
        c = fc[min(max(s + hop, 0), n - 1)]
        sos = signal.butter(2, [_fc(c / (1 + 0.5 / q * 1.2)), _fc(c * (1 + 0.5 / q * 1.2))], 'band', fs=SR, output='sos')
        y = signal.sosfilt(sos, seg) * win
        for_idx = np.arange(blk) + lo_i
        ok = (for_idx >= 0) & (for_idx < n)
        out[for_idx[ok]] += y[ok]
    out = out[:n]
    # Doppler-like envelope: asymmetric, fast rise, slower fall
    env = np.sin(np.pi * np.clip(t, 0, 1) ** 0.85) ** sharp
    out = out * env
    out = out / (np.abs(out).max() + 1e-9)
    # low-mid body: wide low-passed noise following same envelope
    body = lp(rng.standard_normal(n), 420) * env ** 1.5
    out = out + 0.5 * body / (np.abs(body).max() + 1e-9)
    # cloth / arm rustle: crackly AM noise, mostly in first third and tail
    ru = bp(rng.standard_normal(n), 800, 3500) * (np.abs(smooth_noise(rng, dur, 70)) ** 2)
    ru *= (0.6 * np.exp(-t / 0.25) + 0.3 * np.exp(-(1 - t) / 0.2) + 0.15 * env)
    out = out + rustle * ru / (np.abs(ru).max() + 1e-9)
    return lp(out, tilt_lp, 2)


def circ(fn, x):
    """apply a filter function to a periodic buffer without edge discontinuities"""
    n = len(x)
    return fn(np.concatenate([x, x, x]))[n:2 * n]


def metal_tick(rng, f0=None, level=1.0, tau=0.006, dur=0.08, bright=1.0):
    """crisp metallic mechanical click: inharmonic damped partials + 1 ms noise transient"""
    t = np.arange(int(dur * SR)) / SR
    f0 = f0 or rng.uniform(1800, 3200)
    s = np.zeros_like(t)
    for r_ in (1.0, 1.58, 2.31, 3.07, 4.1):
        s += rng.uniform(0.4, 1) / r_ ** 0.5 * np.sin(2 * np.pi * f0 * r_ * rng.uniform(0.98, 1.02) * t + rng.uniform(0, 6)) \
            * np.exp(-t / (tau * rng.uniform(0.5, 1.4)))
    s = s / 2.5
    s += bright * hp(rng.standard_normal(len(t)), 3500) * np.exp(-t / 0.0007) * 0.9
    return level * s


def metal_slide(rng, dur, lo=1200, hi=5000, level=1.0, rate=180):
    """metal-on-metal slide: band noise with irregular ripple (friction) and soft envelope"""
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = bp(rng.standard_normal(n), lo, hi)
    ripple = 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * rate * t * (1 + 0.2 * smooth_noise(rng, dur, 20)) + 1))
    env = np.minimum(1, t / 0.012) * np.minimum(1, (dur - t) / 0.025)
    return level * x * ripple * env


def formant(src, fs_bw, gains):
    out = 0
    for (lo, hi), g in zip(fs_bw, gains):
        out = out + g * bp(src, lo, hi, 2)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out
