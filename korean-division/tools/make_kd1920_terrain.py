"""kd1920 기본 지형 (풀 · 흙 · 모래 · 물 · 바위) + 도시 그림 생성기 (결정적). 숲 · 잔해 · 가옥 · 철조망 · 고지 · 참호 = make_kd1920_terrain2.py --install. 사용: python tools/make_kd1920_terrain.py [테마폴더=assets/themes/kd1920]
- 원칙: 지형은 차분하게(저채도 · 저대비), 유닛이 튄다. 빛은 왼쪽 위.
- 타일은 64px 네이티브로 그려 (이음매 없이 타일링) 세 벌 저장:
  terrain/<key>_v0..v5.png (32px, 현재 로더 TileSize=32 와 일치 - theme.json variants 가 가리킴),
  terrain/<key>_v0@64.png, terrain/<key>_v0@128.png (로더가 칸 크기를 올리면 사용).
- 도시: 기와 지붕 + 흙벽돌 담의 만주 · 한국풍. 팀색 레이어(_team.png) 유지.
"""
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ww2_sprites as W

N = 64
NV = 6
KEYS = ["grass", "dirt", "sand", "water", "rock"]
T0 = (0, 0, 0, 0)


def vnoise(rng, size, cells):
    """타일링되는 값 노이즈 0..1 (코사인 보간)."""
    g = rng.random((cells, cells))
    xs = np.arange(size) * cells / size
    i = xs.astype(int)
    f = xs - i
    f = (1 - np.cos(f * np.pi)) / 2
    i1 = (i + 1) % cells
    top = g[i][:, i] * (1 - f)[None, :] + g[i][:, i1] * f[None, :]
    bot = g[i1][:, i] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
    return top * (1 - f)[:, None] + bot * f[:, None]


def base(rng, col, amp):
    n = 0.1 * vnoise(rng, N, 2) + 0.4 * vnoise(rng, N, 8) + 0.5 * vnoise(rng, N, 16)
    n = (n - 0.5) * 2 * amp
    arr = np.zeros((N, N, 3))
    for c in range(3):
        arr[:, :, c] = col[c] + n
    return arr


def to_img(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def normalise(ims):
    """같은 키의 변형들을 평균 RGB(=밝기 · 색조) 가 키 평균과 같아지도록 채널별로 곱한다.
    멀리서 줌아웃했을 때 변형 간 밝기 차이로 생기는 얼룩을 없앤다. 장식(결 · 돌 · 바퀴자국)은 상대 구조가 그대로 남는다."""
    arrs = [np.asarray(im).astype(float) for im in ims]
    means = [a[:, :, :3].reshape(-1, 3).mean(0) for a in arrs]
    target = np.mean(means, axis=0)
    out = []
    for a, m in zip(arrs, means):
        a = a.copy()
        a[:, :, :3] = np.clip(a[:, :, :3] * (target / m), 0, 255)
        out.append(Image.fromarray(a.astype(np.uint8), "RGBA"))
    return out


def wrap_draw(img, fn):
    """fn(draw, dx, dy) 를 9번 호출 -> 가장자리에 걸친 장식도 이음매 없이 이어진다."""
    d = ImageDraw.Draw(img)
    for dx in (-N, 0, N):
        for dy in (-N, 0, N):
            fn(d, dx, dy)


def shade(c, k):
    return tuple(max(0, min(255, int(v + k))) for v in c[:3]) + (255,)


def pebbles(img, r, col, n):
    ps = [(r.randint(2, N - 3), r.randint(2, N - 3), r.randint(1, 2)) for _ in range(n)]

    def f(d, dx, dy):
        for x, y, s in ps:
            d.ellipse([x + dx + 1, y + dy + 1, x + dx + s + 1, y + dy + s + 1], fill=shade(col, -52))
            d.ellipse([x + dx, y + dy, x + dx + s, y + dy + s], fill=col)
            d.point([(x + dx, y + dy)], fill=shade(col, 24))
    wrap_draw(img, f)


def grass(v):
    rng = np.random.default_rng(700 + v)
    r = random.Random(700 + v)
    dry = [0, 3, 6, 2, 4, 7][v]            # 변형마다 마른 정도가 다른 만주 초원
    col = (112 + dry, 122 + dry * 0.6, 74 - dry * 0.3)
    img = to_img(base(rng, col, 7))
    bc = tuple(int(x) for x in col)
    tufts = []
    for _ in range(r.randint(7, 11)):
        tufts.append((r.randint(0, N - 1), r.randint(0, N - 1),
                      [(r.randint(-3, 3), r.randint(3, 6)) for _ in range(r.randint(3, 5))]))

    def g(d, dx, dy):
        for x, y, bl in tufts:
            for i, (ox, h) in enumerate(bl):
                x0 = x + dx + i - len(bl) // 2
                d.line([(x0, y + dy), (x0 + ox // 2, y + dy - h)], fill=shade(bc, 22 if ox <= 0 else -14))
            d.point([(x + dx + 2, y + dy + 1)], fill=shade(bc, -22))
    wrap_draw(img, g)
    if v in (1, 4):
        pts = [(r.randint(0, N - 1), r.randint(0, N - 1)) for _ in range(3)]
        wrap_draw(img, lambda d, dx, dy: [d.point([(a + dx, b + dy)], fill=(176, 164, 112, 255)) for a, b in pts])
    if v in (2, 5):
        pebbles(img, r, shade(bc, 28), 3)
    return img


def dirt(v):
    rng = np.random.default_rng(800 + v)
    r = random.Random(800 + v)
    col = (158 + [0, 6, -6, 10, -4, 4][v], 130 + [0, 4, -5, 6, -3, 2][v], 90 + [0, 2, -4, 4, -2, 0][v])
    img = to_img(base(rng, col, 6))
    c = tuple(int(x) for x in col)
    d = ImageDraw.Draw(img)
    if v in (0, 3, 4):   # 수레바퀴 자국 (가로 두 줄, 좌우로 이어짐)
        y0 = r.randint(14, 40)
        wob = r.randint(0, 5)
        for off in (0, 9):
            for x in range(N):
                y = y0 + off + int(round(1.4 * np.sin((x + wob * 7) * 2 * np.pi / N)))
                d.point([(x, y % N)], fill=shade(c, -26))
                d.point([(x, (y + 1) % N)], fill=shade(c, -12))
                d.point([(x, (y + 2) % N)], fill=shade(c, 14))
    if v in (1, 2, 5):
        pebbles(img, r, shade(c, 18), r.randint(4, 7))
    if v in (2, 5):   # 갈라진 황토
        x, y = r.randint(8, 50), r.randint(8, 50)
        pts = [(x, y)]
        for _ in range(4):
            x += r.randint(-5, 5)
            y += r.randint(2, 6)
            pts.append((x, y))
        d.line(pts, fill=shade(c, -24))
    pts = [(r.randint(0, N - 1), r.randint(4, N - 1)) for _ in range(2)]
    wrap_draw(img, lambda dd, dx, dy: [dd.line([(a + dx, b + dy), (a + dx - 1, b + dy - 3)], fill=(132, 128, 78, 255)) for a, b in pts])
    return img


def sand(v):
    rng = np.random.default_rng(900 + v)
    r = random.Random(900 + v)
    col = (190 + [0, 5, -5, 8, -3, 3][v], 170 + [0, 4, -5, 6, -3, 2][v], 128 + [0, 2, -4, 4, -2, 1][v])
    img = to_img(base(rng, col, 5))
    c = tuple(int(x) for x in col)
    d = ImageDraw.Draw(img)
    for _ in range(3 + v % 3):   # 바람결
        y0 = r.randint(0, N - 1)
        amp = r.uniform(1.2, 2.2)
        ph = r.random() * N
        for x in range(N):
            y = int(round(y0 + amp * np.sin((x + ph) * 2 * np.pi / (N / 2)))) % N
            d.point([(x, y)], fill=shade(c, 12))
            d.point([(x, (y + 1) % N)], fill=shade(c, -14))
    if v in (1, 3, 5):
        pebbles(img, r, shade(c, -22), r.randint(2, 4))
    return img


def water(v):
    rng = np.random.default_rng(1000 + v)
    r = random.Random(1000 + v)
    k = [0, 2, -3, 4, -2, 1][v]
    col = (82 + k, 112 + k, 130 + k)
    img = to_img(base(rng, col, 6))
    c = tuple(int(x) for x in col)
    for _ in range(5 + v % 3):   # 잔물결
        x, y, ln = r.randint(0, N - 1), r.randint(0, N - 1), r.randint(4, 9)

        def f(d, dx, dy, x=x, y=y, ln=ln):
            d.line([(x + dx, y + dy), (x + dx + ln, y + dy)], fill=shade(c, 26))
            d.line([(x + dx + 2, y + dy + 1), (x + dx + ln - 1, y + dy + 1)], fill=shade(c, -16))
        wrap_draw(img, f)
    return img


def rock(v):
    rng = np.random.default_rng(1100 + v)
    r = random.Random(1100 + v)
    k = [0, 4, -4, 6, -2, 2][v]
    col = (104 + k, 100 + k, 94 + k)
    img = to_img(base(rng, col, 8))
    c = tuple(int(x) for x in col)
    spots = [(r.randint(0, N - 1), r.randint(0, N - 1), r.randint(6, 11)) for _ in range(r.randint(4, 6))]

    def f(d, dx, dy):
        for x, y, s in spots:
            x += dx
            y += dy
            d.ellipse([x - s // 2 + 2, y - s // 2 + 3, x + s // 2 + 2, y + s // 2 + 3], fill=shade(c, -42))
            d.ellipse([x - s // 2, y - s // 2, x + s // 2, y + s // 2], fill=shade(c, 6), outline=shade(c, -30))
            d.ellipse([x - s // 2 + 1, y - s // 2 + 1, x, y], fill=shade(c, 24))
            d.line([(x, y), (x + s // 3, y + s // 3)], fill=shade(c, -26))
    wrap_draw(img, f)
    if v in (1, 3, 5):   # 이끼
        pts = [(r.randint(0, N - 1), r.randint(0, N - 1)) for _ in range(10)]
        wrap_draw(img, lambda d, dx, dy: [d.point([(a + dx, b + dy)], fill=(104, 112, 74, 255)) for a, b in pts])
    return img


GEN = {"grass": grass, "dirt": dirt, "sand": sand, "water": water, "rock": rock}


# ============================== 도시 (만주 · 한국풍) ==============================
TILE_R = (86, 92, 96, 255)      # 회청색 기와
TILE_RL = (116, 124, 128, 255)
TILE_RD = (56, 60, 64, 255)
MUD = (168, 138, 98, 255)       # 흙벽돌
MUD_L = (194, 164, 120, 255)
MUD_D = (118, 92, 62, 255)
YARD = (176, 152, 112, 255)
YARD_D = (150, 126, 90, 255)
STRAW = (176, 150, 84, 255)
STRAW_D = (130, 108, 58, 255)
GRN = (112, 122, 74, 255)


def ground(s, w, h, seed, col=GRN):
    rng = random.Random(seed)
    s.db.rectangle([0, 0, w - 1, h - 1], fill=col)
    for _ in range(w * h // 40):
        s.px([(rng.randint(0, w - 1), rng.randint(0, h - 1))], rng.choice([shade(col, 10), shade(col, -10)]))


def hanok(s, x, y, w, h, team=False, thatch=False):
    """위에서 본 기와집: 왼쪽 면 밝음, 기와 줄, 용마루, 처마 그늘, 오른쪽 아래 그림자. team=팀색 용마루 띠."""
    s.db.rectangle([x + 2, y + 3, x + w + 1, y + h + 2], fill=shade(MUD_D, -18))
    hi, mid, lo = (STRAW, shade(STRAW, -8), STRAW_D) if thatch else (TILE_RL, TILE_R, TILE_RD)
    s.db.rectangle([x, y, x + w - 1, y + h - 1], fill=mid, outline=W.OUT)
    s.db.rectangle([x + 1, y + 1, x + w // 2 - 1, y + h - 2], fill=hi)
    for i in range(y + 2, y + h - 2, 2):
        s.line([(x + 1, i), (x + w - 2, i)], lo)
    s.line([(x + w // 2, y + 1), (x + w // 2, y + h - 2)], lo)
    if team:
        s.db.rectangle([x + w // 2 - 1, y + 2, x + w // 2 + 1, y + h - 3], fill=W.OUT)
        s.dt.rectangle([x + w // 2 - 1, y + 2, x + w // 2 + 1, y + h - 3], fill=W.TM)
    s.line([(x, y + h - 1), (x + w - 1, y + h - 1)], MUD_D)


def wall(s, x0, y0, x1, y1):
    s.db.rectangle([x0, y0, x1, y1], fill=MUD, outline=MUD_D)
    if y1 - y0 <= 3:
        s.line([(x0 + 1, y0 + 1), (x1 - 1, y0 + 1)], MUD_L)
    else:
        s.line([(x0 + 1, y0 + 1), (x0 + 1, y1 - 1)], MUD_L)


def compound(s, x, y, w, h, team=False):
    s.db.rectangle([x, y, x + w - 1, y + h - 1], fill=YARD)
    wall(s, x, y, x + w - 1, y + 2)
    wall(s, x, y, x + 2, y + h - 1)
    wall(s, x, y + h - 3, x + w - 1, y + h - 1)
    wall(s, x + w - 3, y, x + w - 1, y + h - 1)
    s.db.rectangle([x + w // 2 - 2, y + h - 3, x + w // 2 + 1, y + h - 1], fill=YARD_D)
    hanok(s, x + 4, y + 5, w - 8, h - 14, team)


def village():
    s = W.Sprite(48, 48)
    ground(s, 48, 48, 44)
    s.db.rectangle([0, 22, 47, 25], fill=YARD_D)
    s.line([(0, 23), (47, 23)], YARD)
    for (x, y) in ((2, 3), (18, 4), (3, 29)):
        hanok(s, x, y, 12, 11, thatch=True)
    s.rect(26, 29, 45, 45, shade(STRAW, -6), None, None, ol=MUD_D)
    for yy in range(31, 45, 3):
        s.line([(27, yy), (44, yy)], (118, 140, 66, 255))
    s.ell(34, 6, 40, 12, STRAW, STRAW_D)
    s.line([(40, 24), (40, 12)], W.STEEL_L)
    s.trect(41, 12, 46, 17)
    return s.done()


def city():
    s = W.Sprite(64, 64)
    ground(s, 64, 64, 33)
    s.db.rectangle([0, 28, 63, 35], fill=YARD_D)
    s.db.rectangle([28, 0, 35, 63], fill=YARD_D)
    s.line([(1, 31), (62, 31)], YARD)
    s.line([(31, 1), (31, 62)], YARD)
    for (x, y, w, h) in [(2, 2, 22, 22), (38, 2, 22, 22), (2, 39, 22, 22), (38, 39, 23, 22)]:
        compound(s, x, y, w, h)
    s.db.rectangle([24, 24, 39, 39], fill=MUD_D, outline=W.OUT)
    hanok(s, 25, 25, 14, 14, team=True)
    s.line([(41, 26), (41, 10)], W.STEEL_L)
    s.trect(42, 10, 49, 16)
    return s.done()


def metropolis():
    s = W.Sprite(64, 64)
    ground(s, 64, 64, 21, YARD)
    wall(s, 0, 0, 63, 4)
    wall(s, 0, 59, 63, 63)
    wall(s, 0, 0, 4, 63)
    wall(s, 59, 0, 63, 63)
    for (cx, cy) in ((0, 0), (54, 0), (0, 54), (54, 54)):
        s.db.rectangle([cx, cy, cx + 9, cy + 9], fill=MUD_D, outline=W.OUT)
        hanok(s, cx + 1, cy + 1, 8, 8)
    s.db.rectangle([28, 59, 35, 63], fill=YARD_D)
    s.db.rectangle([28, 5, 35, 58], fill=YARD_D)
    s.db.rectangle([5, 28, 58, 35], fill=YARD_D)
    rng = random.Random(8)
    for (x, y) in ((7, 7), (18, 7), (40, 7), (50, 7), (7, 17), (18, 17), (40, 17), (50, 17),
                   (7, 38), (18, 38), (40, 38), (50, 38), (7, 48), (18, 48), (40, 48), (50, 48)):
        w, h = rng.choice([(8, 8), (9, 7), (7, 9)])
        hanok(s, x, y, w, h, thatch=rng.random() < 0.25)
    s.db.rectangle([23, 23, 40, 40], fill=MUD_D, outline=W.OUT)
    hanok(s, 24, 24, 16, 16, team=True)
    s.line([(32, 22), (32, 7)], W.STEEL_L)
    s.trect(33, 7, 42, 13)
    return s.done()


# ============================== 공용 헬퍼 (숲 · 잔해 · 철조망 · 고지 · 참호 타일은 make_kd1920_terrain2.py) ==============================
from PIL import ImageFilter


def _sh(rgb, k):
    return tuple(int(max(0, min(255, v + k))) for v in rgb[:3])


def _crown(r, s):
    """뾰족한 침엽수 수관 윤곽 (중심 기준 로컬 좌표). 가지 끝 = 바깥 꼭짓점, 골 = 안쪽 꼭짓점.
    세 가지 끝(삼각형에 가까움) ~ 일곱 가지(불규칙 별) 를 섞고, 회전 · 세로 눌림 · 가지 길이를 매번 다르게."""
    m = r.choice((3, 3, 4, 5, 6, 7))
    inner = r.uniform(0.3, 0.5) if m == 3 else r.uniform(0.42, 0.66)
    ang = r.uniform(0, 2 * math.pi)
    ey = r.uniform(0.72, 1.12)
    pts = []
    for i in range(2 * m):
        a = ang + i * math.pi / m + r.uniform(-0.14, 0.14)
        rad = s * r.uniform(0.82, 1.14) if i % 2 == 0 else s * inner * r.uniform(0.75, 1.15)
        pts.append((math.cos(a) * rad, math.sin(a) * rad * ey))
    return pts


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "assets", "themes", "kd1920")
    os.makedirs(os.path.join(out, "terrain"), exist_ok=True)
    os.makedirs(os.path.join(out, "buildings"), exist_ok=True)
    path = os.path.join(out, "theme.json")
    with open(path, encoding="utf-8") as f:
        theme = json.load(f)
    sp = theme.setdefault("sprites", {})
    for k in KEYS:
        vs = []
        for v, im in enumerate(normalise([GEN[k](v) for v in range(NV)])):
            im.resize((128, 128), Image.NEAREST).save(os.path.join(out, "terrain", f"{k}_v{v}@128.png"))
            im.save(os.path.join(out, "terrain", f"{k}_v{v}@64.png"))
            rel = f"terrain/{k}_v{v}.png"
            im.resize((32, 32), Image.BOX).save(os.path.join(out, rel))
            vs.append(rel)
        sp[f"terrain.{k}"] = {"variants": vs, "size": [32, 32]}
    for k, fn, sz in (("village", village, 96), ("city", city, 128), ("metropolis", metropolis, 128)):
        b, t = fn()
        b.save(os.path.join(out, "buildings", f"{k}.png"))
        t.save(os.path.join(out, "buildings", f"{k}_team.png"))
        sp[f"building.{k}"] = {"frames": [f"buildings/{k}.png"], "teamFrames": [f"buildings/{k}_team.png"], "size": [sz, sz]}
    with open(path, "w", encoding="utf-8") as f:
        json.dump(theme, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
