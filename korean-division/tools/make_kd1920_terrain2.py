"""kd1920 확장 지형 생성기 (결정적). 사용: python tools/make_kd1920_terrain2.py <출력폴더>   (초안 + contact_terrain2.png)
       python tools/make_kd1920_terrain2.py --install [테마폴더=assets/themes/kd1920]   (terrain/ 에 쓰고 theme.json 에 등록, 옛 초안 키 삭제)
키: forest_edge forest_deep rubble house barbed_wire trench_<piece> high_ground high_ground_rim_<n|e|s|w|ne|se|sw|nw>
저장: <out>/<key>_v<i>.png (32), @64, @128. --install 없으면 assets 에 쓰지 않는다."""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_kd1920_terrain as T

N = T.N
sh = T._sh
NV = 6


def tg(v):
    return T.normalise([T.grass(i) for i in range(NV)])[v]


def rnd(v, a, b):
    return random.Random(v).randint(a, b)


def conifer_tile(seed, ground, n_tree, sizes, greens, n_clump, gap):
    rng = np.random.default_rng(seed)
    r = random.Random(seed)
    img = T.to_img(T.base(rng, ground, 5))
    clumps = [(r.randint(0, N - 1), r.randint(0, N - 1)) for _ in range(n_clump)]
    trees = []
    for _ in range(n_tree):
        if r.random() < gap:
            continue
        if r.random() < 0.8:
            cx, cy = r.choice(clumps)
            x, y = int(round(cx + r.gauss(0, 8))) % N, int(round(cy + r.gauss(0, 8))) % N
        else:
            x, y = r.randint(0, N - 1), r.randint(0, N - 1)
        s_ = r.randint(*sizes)
        trees.append((x, y, s_, r.randrange(3), T._crown(r, s_)))
    trees.sort(key=lambda t: t[1])
    for x, y, s_, gi, pts in trees:
        def f(d, dx, dy, x=x, y=y, gi=gi, pts=pts):
            g = greens[gi]
            cx, cy = x + dx, y + dy

            def P(sc, ox, oy):
                return [(cx + px * sc + ox, cy + py * sc + oy) for px, py in pts]
            d.polygon(P(1.0, 2, 3), fill=sh(ground, -26) + (255,))
            d.polygon(P(1.0, 0, 0), fill=sh(g, -16) + (255,))
            d.polygon(P(0.72, -1, -1), fill=sh(g, 0) + (255,))
            d.polygon(P(0.34, -2, -2), fill=sh(g, 18) + (255,))
        T.wrap_draw(img, f)
    return img


def forest_deep(v):
    k = [0, 3, -3, 5, -2, 2][v]
    g = [(40 + k, 66 + k, 40), (32 + k, 56 + k, 36), (46 + k, 74 + k, 44)]
    return conifer_tile(2100 + v, (28 + k, 42 + k, 28), rnd(v, 54, 62), (5, 11), g, 6, 0.04)


def forest_edge(v):
    k = [0, 3, -3, 5, -2, 2][v]
    g = [(52 + k, 82 + k, 44), (44 + k, 72 + k, 40), (60 + k, 90 + k, 48)]
    return conifer_tile(2200 + v, (84 + k, 100 + k, 60), rnd(v, 18, 24), (4, 9), g, 3, 0.15)


def rubble(v):
    rng = np.random.default_rng(2300 + v)
    r = random.Random(2300 + v)
    k = [0, 4, -4, 6, -2, 2][v]
    arr = np.asarray(tg(v)).astype(float)
    dust = T.base(rng, (132 + k, 116 + k, 94 + k), 6)
    m = T.vnoise(rng, N, 4) > 0.42            # 먼지/흙 얼룩이 풀 위로 번짐
    arr[:, :, :3][m] = dust[m] * 0.9 + arr[:, :, :3][m] * 0.1
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    c = (132, 116, 94)
    pieces = []
    for _ in range(r.randint(22, 30)):
        pieces.append((r.choice(["brick", "brick", "brick", "stone", "tile", "beam", "beam"]),
                       r.randint(0, N - 1), r.randint(0, N - 1), r.random() * math.pi, r.randint(3, 7)))
    pieces.sort(key=lambda p: p[2])

    def poly(cx, cy, ang, a, b):
        ca, sa = math.cos(ang), math.sin(ang)
        return [(cx + ca * u - sa * w, cy + sa * u + ca * w) for u, w in ((-a, -b), (a, -b), (a, b), (-a, b))]

    def fp(d, dx, dy):
        for kind, x, y, ang, sz in pieces:
            cx, cy = x + dx, y + dy
            if kind == "brick":
                col, hi, a, b = (156, 88, 64, 255), (190, 116, 88, 255), sz * .7, 2
            elif kind == "stone":
                col, hi, a, b = (140, 136, 128, 255), (178, 174, 164, 255), sz * .6, sz * .45
            elif kind == "tile":
                col, hi, a, b = (92, 98, 102, 255), (122, 130, 134, 255), sz * .7, 2.5
            else:
                col, hi, a, b = (64, 48, 36, 255), (104, 80, 56, 255), sz * 1.4, 1.2
            d.polygon(poly(cx + 1, cy + 2, ang, a, b), fill=sh(c, -56) + (255,))
            d.polygon(poly(cx, cy, ang, a, b), fill=col)
            d.line([(cx - 1, cy - 1), (cx + 1, cy - 1)], fill=hi)
            if kind == "beam":
                d.point([(cx + int(math.cos(ang) * a * .6), cy + int(math.sin(ang) * a * .6))], fill=(20, 16, 12, 255))
    T.wrap_draw(img, fp)
    return img


def barbed_wire(v):
    """X자 교차 말뚝 + 사이에 늘어진 철조망 코일, 풀 위."""
    r = random.Random(2400 + v)
    arr = np.asarray(tg(v)).astype(float)
    arr[:, :, :3] *= 0.95
    img = Image.fromarray(arr.astype(np.uint8), "RGBA")
    ys = (18 + r.randint(-3, 3), 46 + r.randint(-3, 3))
    xo = r.randint(0, 10)
    step = 21
    WIRE, WL = (36, 36, 42, 255), (160, 160, 168, 255)

    def f(d, dx, dy):
        for ry in ys:
            for i in range(-1, 5):
                x0 = xo + i * step + dx
                for amp in (3.2, -3.2):
                    pts = [(x0 + t, ry + dy + amp * math.sin(t * 0.9)) for t in range(0, step + 1)]
                    d.line(pts, fill=WIRE, width=1)
                d.point([(x0 + t, ry + dy - 3.2 * math.sin(t * 0.9)) for t in range(0, step + 1, 2)], fill=WL)
            for i in range(-1, 5):
                px = xo + i * step + dx
                d.line([(px + 2, ry + dy + 7), (px + 5, ry + dy + 8)], fill=(26, 40, 20, 200))
                for sgn, w in ((1, (112, 82, 54, 255)), (-1, (88, 62, 40, 255))):   # X 말뚝
                    d.line([(px - 4 * sgn, ry + dy - 6), (px + 4 * sgn, ry + dy + 6)], fill=(24, 18, 14, 255), width=4)
                    d.line([(px - 4 * sgn, ry + dy - 6), (px + 4 * sgn, ry + dy + 6)], fill=w, width=2)
    T.wrap_draw(img, f)
    return img


HOUSE_STYLES = ["tile", "thatch", "courtyard"]


def house(v):
    """스타일 = v % 3 (기와 / 초가 / 마당+담 기와), 방향/크기 = v // 3. 타일 전체가 막힌 블록."""
    r = random.Random(2500 + v)
    style = HOUSE_STYLES[v % 3]
    img = tg(v).copy()
    d = ImageDraw.Draw(img)
    yard = (176, 152, 112, 255)
    d.rectangle([2, 2, N - 3, N - 3], fill=yard)
    for _ in range(30):
        d.point([(r.randint(3, N - 4), r.randint(3, N - 4))], fill=sh(yard, r.choice((-12, 12))) + (255,))
    MUD, MUD_D, MUD_L = (168, 138, 98, 255), (112, 88, 60, 255), (194, 164, 120, 255)
    if style == "courtyard":
        for box in ([2, 2, N - 3, 5], [2, 2, 5, N - 3], [2, N - 6, N - 3, N - 3], [N - 6, 2, N - 3, N - 3]):
            d.rectangle(box, fill=MUD, outline=MUD_D)
        d.line([(3, 3), (N - 4, 3)], fill=MUD_L)
        d.rectangle([N // 2 - 4, N - 6, N // 2 + 3, N - 3], fill=sh(yard, -24) + (255,))
        x0, y0, w, h = 10, 11, N - 20, N - 28
    else:
        flip = (v // 3) % 2
        w, h = (44, 30) if not flip else (36, 38)
        x0, y0 = (N - w) // 2, (N - h) // 2 - 1
    d.rectangle([x0 + 3, y0 + 4, x0 + w + 3, y0 + h + 3], fill=sh(MUD_D, -26) + (255,))
    d.rectangle([x0, y0 + h - 5, x0 + w - 1, y0 + h + 1], fill=MUD, outline=MUD_D)
    d.line([(x0 + 1, y0 + h - 4), (x0 + w - 2, y0 + h - 4)], fill=MUD_L)
    dx_ = x0 + w // 2 - 3
    d.rectangle([dx_, y0 + h - 4, dx_ + 5, y0 + h + 1], fill=(60, 44, 32, 255))
    if style == "thatch":
        hi, mid, lo = (200, 172, 98, 255), (176, 150, 84, 255), (126, 104, 56, 255)
        d.rounded_rectangle([x0, y0, x0 + w - 1, y0 + h - 5], radius=4, fill=mid, outline=(86, 70, 40, 255))
        d.rounded_rectangle([x0 + 1, y0 + 1, x0 + w // 2, y0 + h - 6], radius=3, fill=hi)
        for yy in range(y0 + 3, y0 + h - 6, 3):
            for _ in range(r.randint(2, 4)):
                xx = r.randint(x0 + 3, x0 + w - 4)
                d.line([(xx, yy), (xx + r.randint(-2, 2), yy + 2)], fill=lo)
        d.line([(x0 + 4, y0 + (h - 5) // 2), (x0 + w - 5, y0 + (h - 5) // 2)], fill=(104, 86, 48, 255), width=2)
    else:
        hi, mid, lo = (122, 130, 134, 255), (88, 94, 98, 255), (56, 60, 64, 255)
        d.rectangle([x0, y0, x0 + w - 1, y0 + h - 5], fill=mid, outline=(30, 32, 36, 255))
        d.rectangle([x0 + 1, y0 + 1, x0 + w // 2 - 1, y0 + h - 6], fill=hi)
        for xx in range(x0 + 2, x0 + w - 2, 3):
            d.line([(xx, y0 + 2), (xx, y0 + h - 7)], fill=lo)
        d.line([(x0 + w // 2, y0 + 1), (x0 + w // 2, y0 + h - 6)], fill=(36, 38, 42, 255), width=2)
        d.line([(x0 + 1, y0 + 1), (x0 + w - 2, y0 + 1)], fill=hi)
        if r.random() < .7:   # 굴뚝
            d.rectangle([x0 + w - 9, y0 + 3, x0 + w - 6, y0 + 7], fill=MUD_D, outline=(30, 24, 18, 255))
    return img


def high_ground(v):
    g = np.asarray(tg(v)).astype(float)
    g[:, :, 0] += 12
    g[:, :, 1] += 9
    g[:, :, 2] += 1
    g[:, :, :3] = g[:, :, :3] * 0.9 + np.array((176, 160, 100)) * 0.1
    return Image.fromarray(np.clip(g, 0, 255).astype(np.uint8), "RGBA")


def _rim_side(side, seed, W=11):
    """고지 가장자리 오버레이. s/e = 어두운 절벽면, n/w = 밝은 턱. 안쪽 경계는 들쭉날쭉."""
    o = np.zeros((N, N, 4))
    rng = np.random.default_rng(seed)
    wob = T.vnoise(rng, N, 8)[0] * 3.2
    for i in range(N):
        depth = int(round(W - 3 + wob[i]))
        for t in range(depth):
            if side in "se":
                if t == 0:
                    c = (58, 46, 32)
                elif t == depth - 1:
                    c = (186, 176, 116)
                else:
                    c = sh((118, 96, 68), -5 * (depth - t) // 2 + (3 if (i + t) % 5 == 0 else 0))
                a = 255
            else:
                f = 1 - t / depth
                c, a = ((86, 76, 50), 255) if t == 0 else ((214, 208, 148), int(230 * f))
            x, y = {"n": (i, t), "w": (t, i), "s": (i, N - 1 - t), "e": (N - 1 - t, i)}[side]
            o[y, x] = (*c, a)
    return o


def hg_rim(name):
    out = np.zeros((N, N, 4))
    for s_ in name:
        o = _rim_side(s_, 2600 + "nesw".index(s_) * 10)
        m = o[:, :, 3:4] / 255.0
        out[:, :, :3] = out[:, :, :3] * (1 - m) + o[:, :, :3] * m
        out[:, :, 3:4] = np.maximum(out[:, :, 3:4], o[:, :, 3:4])
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def trench(conn, v):
    """지그재그 흙 참호 + 모래주머니 턱. conn = 열린 방향. 이탈량은 변/중앙에서 0이라 이웃과 이어진다."""
    rng = np.random.default_rng(2700 + v)
    r = random.Random(2700 + v)
    dcol = (158 + [0, 6, -6][v % 3], 130 + [0, 4, -5][v % 3], 90 + [0, 2, -4][v % 3])
    arr = T.base(rng, dcol, 5)
    c, hw, pw = N // 2, 6, 6
    sgn = 1 if v % 2 == 0 else -1
    m = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(m)
    d.rectangle([c - hw, c - hw, c + hw - 1, c + hw - 1], fill=255)
    straight = conn in ("ew", "ns")
    for s_ in conn:
        for u in range(0, c + 1):
            jit = int(round(sgn * 3.0 * math.sin(math.pi * u / c))) if straight else 0
            if s_ in "ew":
                x = u if s_ == "w" else N - 1 - u
                d.rectangle([x, c - hw + jit, x, c + hw - 1 + jit], fill=255)
            else:
                y = u if s_ == "n" else N - 1 - u
                d.rectangle([c - hw + jit, y, c + hw - 1 + jit, y], fill=255)
    ch = np.array(m) > 0
    dil = np.array(m.filter(ImageFilter.MaxFilter(2 * pw + 1))) > 0
    par = dil & ~ch

    def sh_(a, dx, dy):
        return np.roll(np.roll(a, -dy, 0), -dx, 1)
    out = arr.copy()
    out[par] += 6
    floor = np.array((72, 56, 40), dtype=float)
    out[ch] = floor + (arr[ch] - np.array(dcol)) * 0.5
    out[ch & ~sh_(ch, -2, -2)] -= 38
    out[ch & ~sh_(ch, 2, 2)] += 12
    out[~dil & sh_(dil, -2, -2)] -= 14
    near = par & (np.array(m.filter(ImageFilter.MaxFilter(2 * 4 + 1))) > 0)
    BW, BH = 6, 3
    bag, bag_d, bag_l = np.array((182, 164, 122.)), np.array((112, 96, 66.)), np.array((208, 192, 148.))
    for y in range(N):
        for x in range(N):
            if not near[y, x]:
                continue
            row = y // BH
            xx = (x + (BW // 2 if row % 2 else 0)) % BW
            yy = y % BH
            col = bag + (rng.random() - .5) * 8
            if xx == 0 or yy == BH - 1:
                col = bag_d
            elif yy == 0 and xx < BW - 1:
                col = bag_l
            out[y, x] = col
    out[par & ~near & sh_(near, -1, -1)] -= 22
    for _ in range(3 + v % 3):
        x, y = r.randint(0, N - 1), r.randint(0, N - 1)
        if par[y, x] and not near[y, x]:
            out[y, x] = (176, 150, 108)
    return T.to_img(out)


TRENCH_PIECES = {"h": "ew", "v": "ns", "ne": "ne", "nw": "nw", "se": "se", "sw": "sw",
                 "end_n": "n", "end_e": "e", "end_s": "s", "end_w": "w"}
def crater(v):
    """포탄 구덩이: 높이장(구덩이 + 솟은 흙 테)을 왼쪽 위 빛으로 음영, 가운데 그을림, 흙 덩이, 날아간 풀. v>=3 은 갓 생긴 (더 진한 그을림)."""
    rng = np.random.default_rng(2600 + v)
    r = random.Random(2600 + v)
    fresh = v >= 3
    arr = np.asarray(tg(v)).astype(float)[:, :, :3]
    cx, cy = N / 2 + r.uniform(-3, 3), N / 2 + r.uniform(-3, 3)
    R = r.uniform(15, 20)
    yy, xx = np.mgrid[0:N, 0:N].astype(float)
    ang = np.arctan2(yy - cy, xx - cx)
    wob = 1 + 0.10 * np.sin(ang * 3 + r.random() * 6) + 0.06 * np.sin(ang * 5 + r.random() * 6) + 0.12 * (T.vnoise(rng, N, 8) - .5)
    d = np.hypot(xx - cx, yy - cy) / (R * wob)
    bowl = 1 - np.clip(d / 0.8, 0, 1) ** 2                      # 구덩이 깊이 0..1
    rim = np.exp(-((d - 0.98) / 0.2) ** 2)                      # 솟은 흙 테
    hgt = (rim * 4.5 - bowl * 5.0) * R / 18 + 0.4 * (T.vnoise(rng, N, 16) - .5)
    gy, gx = np.gradient(hgt)
    lit = np.clip((-gx - gy) * 22, -48, 48)                     # 빛: 왼쪽 위 (경사가 오른쪽 아래로 내려가면 밝음 -> 높이 증가 방향 부호 보정)
    dirt = np.array([150, 120, 84], float) + (6 if v % 2 else -4) + 8 * (T.vnoise(rng, N, 16)[:, :, None] - .5)
    scorch = np.clip(1 - d / (1.75 if fresh else 1.55), 0, 1) ** 1.3
    blown = np.clip(1.15 - d / 1.0, 0, 1)                       # 풀이 날아간 정도
    out = arr * (1 - blown[:, :, None] * 0.65) + dirt * blown[:, :, None] * 0.65
    dark = scorch * (0.55 if fresh else 0.34)
    out = out * (1 - dark[:, :, None]) + np.array([34, 30, 26]) * dark[:, :, None]
    out = out * (1 - 0.35 * bowl[:, :, None] * (1 if fresh else .6))   # 바닥은 더 어둡게
    out += lit[:, :, None]
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    # 흙 덩이 + 튄 풀 (반대 방향으로 그림자)
    clods = []
    for _ in range(r.randint(9, 15)):
        a = r.random() * math.tau
        rr = R * r.uniform(1.0, 1.55)
        clods.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr, r.choice([1, 1, 2, 2, 3]), r.random() < .3))

    def f(dr, dx, dy):
        for x, y, sz, grass_ in clods:
            x += dx; y += dy
            col = (74, 100, 52, 255) if grass_ else (132, 104, 74, 255)
            dr.ellipse([x + 1, y + 1, x + sz + 1, y + sz + 1], fill=(46, 40, 30, 255))
            dr.ellipse([x, y, x + sz, y + sz], fill=col)
            dr.point([(x, y)], fill=sh(col, 26))
    T.wrap_draw(img, f)
    return img


TV = 3
ENTRIES = []


def save(out, key, imgs):
    for v, im in enumerate(imgs):
        im.resize((128, 128), Image.NEAREST).save(os.path.join(out, f"{key}_v{v}@128.png"))
        im.save(os.path.join(out, f"{key}_v{v}@64.png"))
        im.resize((32, 32), Image.BOX).save(os.path.join(out, f"{key}_v{v}.png"))
    ENTRIES.append((key, imgs))


def contact(out):
    S, P, LH = 64, 4, 14
    font = ImageFont.load_default()
    cols = 10
    E = {k: i for k, i in ENTRIES}
    rows = sum(math.ceil(len(i) / cols) for _, i in ENTRIES)
    cw, ch_ = 17, 8
    gy0 = rows * (S + P + LH) + P + 16
    W_ = max(cols * (S + P) + P, cw * S + 2 * P)
    sheet = Image.new("RGBA", (W_, gy0 + ch_ * S + P), (40, 40, 44, 255))
    d = ImageDraw.Draw(sheet)
    y = P
    hg0 = E["high_ground"][0]
    for key, imgs in ENTRIES:
        for i, im in enumerate(imgs):
            x = P + (i % cols) * (S + P)
            yy = y + (i // cols) * (S + P + LH)
            if key.startswith("high_ground_rim"):
                bg = hg0.copy()
                bg.alpha_composite(im)
                im = bg
            sheet.paste(im, (x, yy + LH))
            if i % cols == 0:
                d.text((x, yy + 1), f"{key} ({len(imgs)})", fill=(235, 235, 200, 255), font=font)
        y += math.ceil(len(imgs) / cols) * (S + P + LH)
    gx, gy = P, gy0
    d.text((gx, gy - 13), "sample map patch: grass / road / forest / new tiles", fill=(235, 235, 200, 255), font=font)
    road = [T.dirt(i) for i in range(NV)]
    forest = [forest_deep(i) for i in range(NV)]
    layer = {}
    for x in range(cw):
        layer[(x, 6)] = ("road", 0)
    for yy in range(4, ch_):
        layer[(9, yy)] = ("road", 0)
    for yy in range(0, 4):
        layer[(0, yy)] = ("forest_deep", yy)
        layer[(1, yy)] = ("forest_deep", yy + 2)
        layer[(2, yy)] = ("forest_edge", yy)
    layer[(3, 1)] = ("forest_edge", 4)
    hg_all = {(x, yy) for yy in range(0, 3) for x in range(5, 9)}
    for p in hg_all:
        layer[p] = ("high_ground", (p[0] + p[1]) % NV)
    layer[(11, 2)] = ("house", 0)
    layer[(12, 2)] = ("house", 1)
    layer[(13, 2)] = ("house", 2)
    layer[(14, 2)] = ("house", 3)
    layer[(11, 3)] = ("rubble", 0)
    layer[(12, 3)] = ("rubble", 3)
    for i_ in range(6):
        layer[(13 + i_ % 4, 3 + i_ // 4)] = ("crater", i_)
    for x in range(10, 17):
        layer[(x, 5)] = ("barbed_wire", x % NV)
    layer[(0, 5)] = ("trench_end_w", 0)
    for x in range(1, 4):
        layer[(x, 5)] = ("trench_h", x % TV)
    layer[(4, 5)] = ("trench_ne", 0)
    layer[(4, 4)] = ("trench_se", 1)
    layer[(5, 4)] = ("trench_end_w", 2)
    layer[(5, 4)] = ("trench_h", 0)
    layer[(6, 4)] = ("trench_end_e", 0)
    for x in range(cw):
        for yy in range(ch_):
            sheet.paste(tg((x * 2 + yy) % NV), (gx + x * S, gy + yy * S))
    for (x, yy) in [(x, 7) for x in range(11, 17)]:
        sheet.paste(forest[x % NV], (gx + x * S, gy + yy * S))
    for (x, yy), (k, i) in layer.items():
        if (x, yy) in hg_all:
            continue
        im = road[(x + yy) % NV] if k == "road" else E[k][i % len(E[k])]
        sheet.paste(im, (gx + x * S, gy + yy * S))
    for (x, yy) in hg_all:
        sheet.paste(E["high_ground"][layer[(x, yy)][1]], (gx + x * S, gy + yy * S))
        sides = ("n" if (x, yy - 1) not in hg_all else "") + ("e" if (x + 1, yy) not in hg_all else "") + \
                ("s" if (x, yy + 1) not in hg_all else "") + ("w" if (x - 1, yy) not in hg_all else "")
        for s_ in sides:
            sheet.alpha_composite(E["high_ground_rim_" + s_][0], (gx + x * S, gy + yy * S))
    sheet.save(os.path.join(out, "contact_terrain2.png"))


def gen_all(out):
    save(out, "forest_edge", [forest_edge(v) for v in range(NV)])
    save(out, "forest_deep", [forest_deep(v) for v in range(NV)])
    save(out, "rubble", T.normalise([rubble(v) for v in range(NV)]))
    save(out, "crater", [crater(v) for v in range(NV)])
    save(out, "house", [house(v) for v in range(NV)])
    save(out, "barbed_wire", [barbed_wire(v) for v in range(NV)])
    save(out, "high_ground", T.normalise([high_ground(v) for v in range(NV)]))
    for nm in ["n", "e", "s", "w", "ne", "se", "sw", "nw"]:
        save(out, f"high_ground_rim_{nm}", [hg_rim(nm)])
    for nm, conn in TRENCH_PIECES.items():
        save(out, f"trench_{nm}", [trench(conn, v) for v in range(TV)])


OLD_KEYS = ("forest", "forest_sparse", "wire", "highground")   # make_kd1920_terrain.py 옛 초안 (terrain.* 키)


def install(theme_dir):
    import glob
    import json
    tdir = os.path.join(theme_dir, "terrain")
    os.makedirs(tdir, exist_ok=True)
    for f in glob.glob(os.path.join(tdir, "*")):   # 옛 초안 파일 정리
        b = os.path.basename(f)
        if b.startswith(("forest_v", "forest_sparse_v", "wire_v", "highground", "trench_line_", "rubble_v", "crater_v")):
            os.remove(f)
    ENTRIES.clear()
    gen_all(tdir)
    path = os.path.join(theme_dir, "theme.json")
    with open(path, encoding="utf-8") as f:
        theme = json.load(f)
    sp = theme.setdefault("sprites", {})
    for k in list(sp):
        if k.startswith("terrain.") and (k[8:] in OLD_KEYS or k.startswith(("terrain.highground", "terrain.trench_line_"))):
            del sp[k]
    theme.pop("_doc_terrain_extra", None)
    for key, imgs in ENTRIES:
        sp[f"terrain.{key}"] = {"variants": [f"terrain/{key}_v{v}.png" for v in range(len(imgs))], "size": [32, 32]}
    theme["_doc_terrain_extra"] = (
        "terrain.forest_edge/forest_deep (숲 가장자리/깊은 숲, 8방향 이웃이 모두 숲이면 deep), rubble, crater (포탄 구덩이 0-2 오래된 · 3-5 갓 생긴), house, barbed_wire, "
        "high_ground (풀 고지 칸), high_ground_rim_<n|e|s|w> (고지 칸 위 오버레이: 그 방향 이웃이 평지일 때; ne/se/sw/nw 는 두 변 합성본, 미사용), "
        "trench_<h|v|ne|nw|se|sw|end_n|end_e|end_s|end_w> (4방향 참호 이웃 마스크로 선택). 생성: tools/make_kd1920_terrain2.py --install. 변형 수: 일반 6, rim 1, trench 3.")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(theme, f, ensure_ascii=False, indent=2)


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--install":
        here = os.path.dirname(os.path.abspath(__file__))
        install(sys.argv[2] if len(sys.argv) > 2 else os.path.join(here, "..", "assets", "themes", "kd1920"))
        return
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    gen_all(out)
    contact(out)


if __name__ == "__main__":
    main()
