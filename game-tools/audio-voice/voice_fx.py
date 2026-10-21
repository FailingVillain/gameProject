"""
녹음한 대사 → 게임 음성 (군인 목소리 · 무전기 소리) 변환.

  python voice_fx.py            이 폴더의 *.m4a / *.wav / *.mp3 전부 처리
  python voice_fx.py 파일...     지정한 파일만

결과 (44.1 kHz · 모노 · 16비트 · 최대 −3 dB, 게임 음성 규격):
  outputs/soldier/<이름>.wav   군인 목소리: 낮고 굵게(−3.5음) · 20% 느리게 + 압축 · 레트로(11 kHz · 8비트)
  outputs/radio/<이름>.wav     무전기: 군인 목소리 + 전화 대역(350~3000 Hz) · 찌그러짐 · 저음질 · 잡음 · 송수신 '치익' 소리

조절값은 아래 SETTINGS. 추가 설치 없음: m4a 디코딩 = Windows 기본 기능(decode_wav.ps1, Media Foundation),
처리 = 이미 설치된 numpy · scipy.
"""
import os
import subprocess
import sys
import wave
import zlib

import numpy as np
from scipy import signal

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "outputs")

SETTINGS = {
    "pitch_semitones": -2.5,     # 목소리 낮추기 (음수 = 낮게). 10-18: -3.5 는 너무 낮음 → -2.5
    "flatten_rise": 1.0,         # 말끝 등에서 음이 올라가는 것 제한: 문장 평균보다 이 반음 이상 높은 구간을 끌어내림 (0 = 끔)
    "body_db": 5.0,              # 굵기: 150~250 Hz 울림 강조 (dB)
    "stretch": 1.2,              # 길이 배율 (1.2 = 20% 길게 늘어뜨림, 음높이는 그대로)
    # 레트로 (8 · 16비트 게임 느낌): 낮은 샘플레이트로 계단처럼 + 비트 수 줄이기 — 두 결과물 모두에 적용
    "retro_rate": 11025,         # 낮출수록 거칠다 (8000 = 아주 옛날, 16000 = 16비트 콘솔풍)
    "retro_bits": 8,             # 8 = 8비트 (지글지글), 12~16 = 덜 거침
    "trim_db": -42,              # 앞뒤 무음 자르기 기준
    "peak_db": -3.0,             # 최종 최대 음량
    # 무전기
    "radio_low": 350, "radio_high": 3000,
    "radio_drive": 3.2,          # 찌그러짐 정도 (1 = 거의 없음)
    "radio_lofi_rate": 8000,     # 저음질 (낮을수록 거칠다)
    "radio_hiss_db": -34,        # 배경 잡음
    "squelch_in_ms": 70, "squelch_out_ms": 140,
}


def decode(path):
    """Windows Media Foundation 으로 WAV 로 풀고 (decode_wav.ps1) 읽는다. wav 는 그대로 읽음."""
    if path.lower().endswith(".wav"):
        wav = path
    else:
        tmp = os.path.join(OUT, "_decoded")
        os.makedirs(tmp, exist_ok=True)
        wav = os.path.join(tmp, "in_%08x.wav" % zlib.crc32(path.encode("utf-8")))   # WinRT 는 [ ] 이름을 싫어한다
        subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", os.path.join(HERE, "decode_wav.ps1"),
                        "-In", path, "-Out", wav], check=True, capture_output=True)
    with wave.open(wav, "rb") as w:
        sr, ch, sw = w.getframerate(), w.getnchannels(), w.getsampwidth()
        raw = w.readframes(w.getnframes())
    dt = {2: np.int16, 4: np.int32}[sw]
    x = np.frombuffer(raw, dtype=dt).astype(np.float64) / float(np.iinfo(dt).max)
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    if sr != SR:
        g = np.gcd(sr, SR)
        x = signal.resample_poly(x, SR // g, sr // g)
    return x


def time_stretch(x, rate, n_fft=2048, hop=512):
    """위상 보코더 시간 늘이기/줄이기 (rate > 1 = 빨라짐 · 짧아짐)."""
    f, t, Z = signal.stft(x, SR, nperseg=n_fft, noverlap=n_fft - hop)
    steps = np.arange(0, Z.shape[1] - 1, rate)
    phase = np.angle(Z[:, 0])
    omega = 2 * np.pi * hop * np.arange(Z.shape[0]) / n_fft
    out = np.empty((Z.shape[0], len(steps)), dtype=complex)
    for k, s in enumerate(steps):
        i = int(s)
        frac = s - i
        mag = (1 - frac) * np.abs(Z[:, i]) + frac * np.abs(Z[:, i + 1])
        out[:, k] = mag * np.exp(1j * phase)
        dphi = np.angle(Z[:, i + 1]) - np.angle(Z[:, i]) - omega
        dphi -= 2 * np.pi * np.round(dphi / (2 * np.pi))
        phase += omega + dphi
    _, y = signal.istft(out, SR, nperseg=n_fft, noverlap=n_fft - hop)
    return y


def load(path, semitones, stretch=1.0):
    """디코드 + 음높이 낮추기 + 길이 늘이기 (리샘플로 음높이를 낮춘 뒤 위상 보코더로 원하는 길이로)."""
    x = decode(path)
    for _ in range(2):   # 한 번에 다 못 내리므로 몇 번 반복 (그레인 방식은 조각 경계에서 일부만 반영된다)
        x = flatten_rise(x, SETTINGS["flatten_rise"])
    ratio = 2 ** (semitones / 12)              # < 1 이면 낮아짐
    up, down = 1000, int(round(1000 * ratio))
    y = signal.resample_poly(x, up, down) if semitones != 0 else x   # 길어지고 낮아짐
    target = int(len(x) * stretch)
    if len(y) == target:
        return y
    return time_stretch(y, len(y) / target)[:target]


def retro(x, rate, bits):
    """8 · 16비트 게임 소리: 샘플 앤 홀드로 샘플레이트를 낮추고(계단 · 에일리어싱) 비트 수를 줄인다."""
    step = SR / rate
    idx = (np.floor(np.arange(len(x)) / step) * step).astype(int)
    y = x[np.clip(idx, 0, len(x) - 1)]
    levels = 2 ** (bits - 1)
    y = np.round(np.clip(y, -1, 1) * levels) / levels
    # 너무 날카로운 고역만 살짝 깎아 귀가 아프지 않게 (계단 느낌은 남김)
    return signal.sosfilt(signal.butter(2, min(rate * 0.9, SR / 2 - 100), btype="lowpass", fs=SR, output="sos"), y)


def trim(x, db, pad_ms=40):
    thr = 10 ** (db / 20)
    env = np.convolve(np.abs(x), np.ones(441) / 441, mode="same")
    idx = np.where(env > thr)[0]
    if len(idx) == 0:
        return x
    pad = int(SR * pad_ms / 1000)
    return x[max(0, idx[0] - pad): min(len(x), idx[-1] + pad)]


def compress(x, thresh_db=-18, ratio=4.0, attack_ms=3, release_ms=80):
    """간단한 피크 압축기 (말소리를 고르게, 크게)."""
    a = np.exp(-1 / (SR * attack_ms / 1000))
    r = np.exp(-1 / (SR * release_ms / 1000))
    env = 0.0
    out = np.empty_like(x)
    thr = 10 ** (thresh_db / 20)
    for i, v in enumerate(np.abs(x)):
        env = a * env + (1 - a) * v if v > env else r * env + (1 - r) * v
        g = 1.0 if env <= thr else (thr * (env / thr) ** (1 / ratio)) / env
        out[i] = x[i] * g
    return out


def eq_peak(x, f0, gain_db, q=1.0):
    A = 10 ** (gain_db / 40)
    w0 = 2 * np.pi * f0 / SR
    alpha = np.sin(w0) / (2 * q)
    b = [1 + alpha * A, -2 * np.cos(w0), 1 - alpha * A]
    a = [1 + alpha / A, -2 * np.cos(w0), 1 - alpha / A]
    return signal.lfilter(b, a, x)


def band(x, lo, hi, order=4):
    sos = signal.butter(order, [lo, hi], btype="bandpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def highpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype="highpass", fs=SR, output="sos"), x)


def normalize(x, peak_db):
    m = np.max(np.abs(x)) or 1.0
    return x / m * 10 ** (peak_db / 20)


def gate(x, thresh_db=-38, floor_db=-30, attack_ms=2, release_ms=120):
    """잡음 게이트: 말이 없는 구간(방 잡음)을 줄인다 — 압축기가 잡음을 키우지 않게."""
    win = int(SR * 0.01)
    env = np.sqrt(np.convolve(x * x, np.ones(win) / win, mode="same"))
    target = np.where(env > 10 ** (thresh_db / 20), 1.0, 10 ** (floor_db / 20))
    a = np.exp(-1 / (SR * attack_ms / 1000)); r = np.exp(-1 / (SR * release_ms / 1000))
    g = np.empty_like(target); cur = target[0]
    for i, t in enumerate(target):
        cur = a * cur + (1 - a) * t if t > cur else r * cur + (1 - r) * t
        g[i] = cur
    return x * g


def soldier(x):
    b = SETTINGS["body_db"]
    y = gate(highpass(x, 80))
    y = eq_peak(y, 120, b * 0.5, 0.9)  # 깊은 울림
    y = eq_peak(y, 200, b, 0.8)        # 가슴 울림 (굵게)
    y = eq_peak(y, 450, -2.0, 1.0)     # 웅웅거림 정리
    y = eq_peak(y, 2600, 2.5, 1.2)     # 존재감 · 또렷함
    y = np.tanh(y * 2.0) / np.tanh(2.0)  # 포화 (배음이 늘어 두껍고 거친 소리)
    y = compress(y, -22, 4.5)
    return y


def f0_track(x, frame=1764, hop=441, fmin=70, fmax=300, thresh=0.15):
    """프레임별 기본 주파수 (YIN — 옥타브 착오에 강함). 무성음 · 무음 = 0."""
    out = []
    lo, hi = int(SR / fmax), int(SR / fmin)
    for i in range(0, max(1, len(x) - frame - hi), hop):
        seg = x[i:i + frame + hi]
        if np.sqrt(np.mean(seg[:frame] ** 2)) < 0.01:
            out.append(0.0); continue
        # 차이 함수 d(tau) (FFT 자기상관으로 빠르게)
        w = seg[:frame]
        n = len(seg)
        fft = np.fft.rfft(seg, 2 * n)
        r = np.fft.irfft(fft * np.conj(np.fft.rfft(np.pad(w, (0, n - frame)), 2 * n)))[:hi + 1]
        e0 = np.sum(w * w)
        cs = np.cumsum(np.concatenate([[0.0], seg ** 2]))
        et = cs[frame:frame + hi + 1] - cs[0:hi + 1]
        d = e0 + et - 2 * r
        d[0] = 0.0
        cm = np.cumsum(d[1:]); dn = np.ones_like(d)
        dn[1:] = d[1:] * np.arange(1, len(d)) / np.maximum(cm, 1e-12)
        cand = np.where(dn[lo:hi] < thresh)[0]
        if len(cand) == 0:
            out.append(0.0); continue
        t = lo + cand[0]
        while t + 1 < hi and dn[t + 1] < dn[t]:
            t += 1
        out.append(SR / t)
    return np.array(out), hop


def flatten_rise(x, max_semi):
    """문장 평균 음높이보다 max_semi 반음 넘게 올라가는 구간만 그만큼 끌어내린다 (겹친 조각별 음높이 이동 + 겹쳐 더하기)."""
    if max_semi <= 0:
        return x
    f0, hop = f0_track(x)
    voiced = f0[f0 > 0]
    if len(voiced) < 4:
        return x
    ref = np.median(voiced)
    semis = np.where(f0 > 0, 12 * np.log2(np.maximum(f0, 1) / ref), 0.0)
    corr = np.minimum(0.0, max_semi - semis)          # 넘친 만큼 (음수 = 내림)
    corr = np.convolve(corr, np.ones(5) / 5, mode="same")   # 부드럽게
    if np.min(corr) > -0.2:
        return x
    n = 2048; step = n // 4
    win = np.hanning(n)
    out = np.zeros(len(x) + n); wsum = np.zeros(len(x) + n)
    for start in range(0, len(x), step):
        seg = x[start:start + n]
        if len(seg) < n: seg = np.pad(seg, (0, n - len(seg)))
        k = min(len(corr) - 1, (start + n // 2) // hop)
        c = corr[k]
        if c < -0.05:
            ratio = 2 ** (c / 12)
            up, down = 1000, int(round(1000 * ratio))
            # 알갱이(그레인) 방식: 조각을 리샘플해 음을 낮추고(길어짐) 앞부분 n 만 쓴다 — 빈 구간이 생기지 않는다
            seg = signal.resample_poly(seg, up, down)[:n]
        out[start:start + n] += seg * win
        wsum[start:start + n] += win
    return (out / np.maximum(wsum, 1e-6))[: len(x)]


def radio(x, rng):
    s = SETTINGS
    y = band(x, s["radio_low"], s["radio_high"], 4)
    y = compress(y, -24, 6.0, 2, 60)
    d = s["radio_drive"]
    y = np.tanh(y / (np.max(np.abs(y)) or 1) * d) / np.tanh(d)   # 진공관 무전기 찌그러짐
    # 저음질: 낮은 샘플레이트로 내렸다 올림 (계단 · 거친 질감) + 8비트 양자화 느낌
    step = SR / s["radio_lofi_rate"]
    idx = (np.floor(np.arange(len(y)) / step) * step).astype(int)
    y = y[np.clip(idx, 0, len(y) - 1)]
    y = np.round(y * 96) / 96
    y = band(y, s["radio_low"] * 0.9, s["radio_high"] * 1.05, 2)
    # 송신 중 배경 잡음 (대역 제한된 쉿 소리 + 약한 웅웅거림)
    n = len(y)
    hiss = band(rng.standard_normal(n), 800, 4500, 2) * 10 ** (s["radio_hiss_db"] / 20) * 3
    t = np.arange(n) / SR
    hum = 0.004 * np.sin(2 * np.pi * 120 * t)
    y = y + hiss + hum

    def squelch(ms, rising):
        m = int(SR * ms / 1000)
        burst = band(rng.standard_normal(m), 600, 5000, 2) * 0.35
        env = np.linspace(0.2, 1.0, m) if rising else np.linspace(1.0, 0.0, m) ** 1.5
        burst *= env
        burst[:40] += np.hanning(80)[:40] * 0.6 * (1 if rising else 0.4)   # 키 누르는 '딸깍'
        return burst

    y = np.concatenate([squelch(s["squelch_in_ms"], True), y, squelch(s["squelch_out_ms"], False)])
    return y


def save(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def process(path):
    name = os.path.splitext(os.path.basename(path))[0]
    x = load(path, SETTINGS["pitch_semitones"], SETTINGS["stretch"])
    x = trim(x, SETTINGS["trim_db"])
    x = x * 0.7   # 원본이 0 dB 에 닿아 있어 여유를 둔다
    rng = np.random.default_rng(zlib.crc32(name.encode("utf-8")))   # 파일마다 같은 잡음 (다시 돌려도 같은 결과)
    base = soldier(x)
    rr, rb = SETTINGS["retro_rate"], SETTINGS["retro_bits"]
    s = normalize(retro(normalize(base, -1), rr, rb), SETTINGS["peak_db"])
    r = normalize(retro(normalize(radio(base, rng), -1), rr, rb), SETTINGS["peak_db"])
    save(os.path.join(OUT, "soldier", name + ".wav"), s)
    save(os.path.join(OUT, "radio", name + ".wav"), r)
    print(f"  {name}: 군인 {len(s) / SR:.2f}초 · 무전기 {len(r) / SR:.2f}초")


def main():
    files = sys.argv[1:] or sorted(
        os.path.join(HERE, f) for f in os.listdir(HERE) if f.lower().endswith((".m4a", ".wav", ".mp3", ".ogg", ".flac")))
    print(f"{len(files)}개 처리 → {OUT}")
    for f in files:
        process(f)
    import shutil
    shutil.rmtree(os.path.join(OUT, "_decoded"), ignore_errors=True)   # 임시 디코드 파일 정리
    print("완료")


if __name__ == "__main__":
    main()
