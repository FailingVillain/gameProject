"""WWII 테마 임시 픽셀 스프라이트 생성기 (결정적: 같은 입력 -> 같은 PNG).

사용: python tools/make_ww2_sprites.py [출력폴더=assets/themes/ww2]
- 모든 그림은 '네이티브 해상도' 로 그린 뒤 2배 최근접 확대 (월드 1타일 = 32px).
- 스프라이트는 위(북쪽)를 향한다. 유닛/포신/포탑은 이미지 중심이 회전 중심.
- <이름>.png = 팀색 무관 픽셀, <이름>_team.png = 팀색 영역 (회색조 -> 그릴 때 세력색을 곱함).
  팀 레이어는 항상 기본 레이어 '위' 에 그려지므로, 팀 영역을 다른 픽셀이 덮으면 안 된다.
- theme.json 도 함께 생성한다 (프레임 목록 형식: 지금은 1장, 나중에 애니메이션 프레임 추가).
"""
import json
import math
import os
import random
import sys

from PIL import Image, ImageDraw

T = (0, 0, 0, 0)
OUT = (26, 26, 22, 255)
METAL_D = (48, 52, 54, 255)
STEEL = (112, 118, 124, 255)
STEEL_L = (166, 174, 180, 255)
GLASS = (110, 150, 170, 255)
OLIVE = (84, 94, 58, 255)
OLIVE_L = (112, 126, 78, 255)
OLIVE_D = (56, 64, 40, 255)
KHAKI = (150, 138, 98, 255)
KHAKI_L = (186, 174, 132, 255)
KHAKI_D = (104, 94, 64, 255)
BROWN = (92, 62, 38, 255)
BROWN_L = (128, 90, 58, 255)
BROWN_D = (58, 38, 22, 255)
WOOD = (140, 98, 58, 255)
WOOD_L = (182, 136, 88, 255)
CONC = (132, 132, 128, 255)
CONC_L = (176, 176, 170, 255)
CONC_D = (84, 84, 82, 255)
SAND = (170, 154, 112, 255)
SAND_D = (116, 102, 70, 255)
SKIN = (214, 170, 134, 255)
ROOF_R = (146, 70, 52, 255)
ROOF_RL = (182, 96, 70, 255)
ROOF_RD = (102, 48, 36, 255)
DIRT = (104, 80, 52, 255)
GRASS = (84, 106, 56, 255)

TB, TM, TS_ = (246, 246, 246, 255), (190, 190, 190, 255), (124, 124, 124, 255)  # 팀 밝음/중간/그림자


class Sprite:
    """기본 레이어(b) + 팀 레이어(t) 한 쌍. 좌표는 네이티브 픽셀(포함 범위)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.b = Image.new("RGBA", (w, h), T)
        self.t = Image.new("RGBA", (w, h), T)
        self.db = ImageDraw.Draw(self.b)
        self.dt = ImageDraw.Draw(self.t)

    # ---- 기본 레이어 ----
    def rect(self, x0, y0, x1, y1, fill, hi=None, lo=None, ol=OUT):
        self.db.rectangle([x0, y0, x1, y1], fill=fill, outline=ol)
        if hi and x1 - x0 > 2 and y1 - y0 > 2:
            self.db.line([(x0 + 1, y0 + 1), (x1 - 1, y0 + 1)], fill=hi)
            self.db.line([(x0 + 1, y0 + 1), (x0 + 1, y1 - 1)], fill=hi)
        if lo and x1 - x0 > 2 and y1 - y0 > 2:
            self.db.line([(x1 - 1, y0 + 1), (x1 - 1, y1 - 1)], fill=lo)
            self.db.line([(x0 + 1, y1 - 1), (x1 - 1, y1 - 1)], fill=lo)

    def ell(self, x0, y0, x1, y1, fill, hi=None, ol=OUT):
        self.db.ellipse([x0, y0, x1, y1], fill=fill, outline=ol)
        if hi:
            self.db.point([(x0 + 2, y0 + 1), (x0 + 1, y0 + 2)], fill=hi)

    def line(self, pts, fill, width=1):
        self.db.line(pts, fill=fill, width=width)

    def px(self, pts, fill):
        self.db.point(pts, fill=fill)

    # ---- 팀 레이어 (외곽선은 기본 레이어에, 색칠은 팀 레이어에) ----
    def trect(self, x0, y0, x1, y1):
        self.db.rectangle([x0, y0, x1, y1], fill=OUT)
        self.dt.rectangle([x0 + 1, y0 + 1, x1 - 1, y1 - 1], fill=TM)
        if x1 - x0 > 2 and y1 - y0 > 2:
            self.dt.line([(x0 + 1, y0 + 1), (x1 - 1, y0 + 1)], fill=TB)
            self.dt.line([(x0 + 1, y1 - 1), (x1 - 1, y1 - 1)], fill=TS_)

    def tell(self, x0, y0, x1, y1):
        self.db.ellipse([x0, y0, x1, y1], fill=OUT)
        self.dt.ellipse([x0 + 1, y0 + 1, x1 - 1, y1 - 1], fill=TM)
        self.dt.point([(x0 + 2, y0 + 2), (x0 + 3, y0 + 2)], fill=TB)

    def tpoly(self, pts):
        self.db.polygon(pts, fill=OUT, outline=OUT)
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        inner = [(p[0] + (1 if p[0] < cx else -1 if p[0] > cx else 0),
                  p[1] + (1 if p[1] < cy else -1 if p[1] > cy else 0)) for p in pts]
        self.dt.polygon(inner, fill=TM)

    def done(self):
        up = lambda im: im.resize((im.width * 2, im.height * 2), Image.NEAREST)
        return up(self.b), up(self.t)


def sandbags(sp, x0, y0, x1, y1, skip=()):
    """사각 둘레에 모래주머니 줄 (skip: 'n','s','e','w' 방향 생략 = 출입구)."""
    def bag(x, y, horiz=True):
        w, h = (5, 3) if horiz else (3, 5)
        sp.rect(x, y, x + w - 1, y + h - 1, SAND, SAND, SAND_D)
    if "n" not in skip:
        for x in range(x0, x1 - 3, 5): bag(x, y0)
    if "s" not in skip:
        for x in range(x0, x1 - 3, 5): bag(x, y1 - 2)
    if "w" not in skip:
        for y in range(y0 + 3, y1 - 4, 5): bag(x0, y, False)
    if "e" not in skip:
        for y in range(y0 + 3, y1 - 4, 5): bag(x1 - 2, y, False)


# ============================== 유닛 (16x16 / 24x24 -> 32 / 48) ==============================


def infantry():
    s = Sprite(16, 16)
    # 소총 (총구가 위): 개머리판 -> 총열
    s.rect(11, 1, 12, 11, STEEL, STEEL_L, METAL_D)
    s.rect(11, 7, 12, 12, WOOD, WOOD_L, BROWN_D)
    # 배낭
    s.rect(5, 10, 10, 14, OLIVE_D, OLIVE, OUT)
    # 어깨/몸통 (카키)
    s.rect(3, 6, 10, 11, KHAKI, KHAKI_L, KHAKI_D)
    s.px([(10, 4), (10, 5)], SKIN)  # 앞으로 뻗은 손
    # 철모 (팀색)
    s.tell(4, 3, 9, 8)
    return s.done()


def scout():
    s = Sprite(16, 16)
    # 무전기 배낭 + 안테나
    s.rect(5, 9, 9, 13, OLIVE_D, OLIVE, OUT)
    s.line([(9, 9), (10, 1)], STEEL_L)
    s.px([(10, 0)], STEEL)
    # 몸통 (연한 올리브, 날씬)
    s.rect(4, 6, 9, 10, OLIVE_L, (140, 156, 100, 255), OLIVE_D)
    # 쌍안경: 얼굴 앞
    s.rect(3, 1, 8, 3, METAL_D, STEEL, OUT)
    s.px([(4, 1), (7, 1)], GLASS)
    # 군모 (팀색)
    s.trect(4, 4, 9, 8)
    return s.done()




# ============================== 건물 ==============================






ASPH = (70, 72, 74, 255)
ASPH_L = (92, 94, 96, 255)
WALL_L = (196, 186, 160, 255)
WALL = (160, 150, 124, 255)
WALL_D = (112, 104, 86, 255)
ROOF_G = (96, 100, 108, 255)
ROOF_GL = (128, 134, 144, 255)
ROOF_B = (122, 94, 78, 255)
ROOF_BL = (156, 122, 100, 255)
WINL = (150, 190, 210, 255)
ORE = (62, 58, 56, 255)
ORE_L = (96, 90, 86, 255)
OIL = (30, 30, 34, 255)


def block(s, x, y, w, h, roof=ROOF_G, roofl=ROOF_GL, floors=3):
    """위에서 본 다층 건물: 지붕 + 오른쪽/아래 외벽(그림자) + 창문 점."""
    d = floors
    s.rect(x + 1, y + 1, x + w + d, y + h + d, WALL_D, None, None)
    for i in range(0, h + d - 2, 3):
        s.px([(x + w + 1, y + 3 + i)], WINL)
    s.rect(x, y, x + w - 1, y + h - 1, roof, roofl, WALL_D)
    if w > 8 and h > 8:
        s.line([(x + 2, y + h // 2), (x + w - 3, y + h // 2)], WALL_D)
    s.px([(x + w - 3, y + 2)], METAL_D)


def street_grid(s, w, h, xs, ys, wd=4):
    for x in xs:
        s.db.rectangle([x, 0, x + wd - 1, h - 1], fill=ASPH)
        s.line([(x + wd // 2, 2), (x + wd // 2, h - 3)], ASPH_L)
    for y in ys:
        s.db.rectangle([0, y, w - 1, y + wd - 1], fill=ASPH)
        s.line([(2, y + wd // 2), (w - 3, y + wd // 2)], ASPH_L)


def metropolis():
    s = Sprite(64, 64)
    s.rect(0, 0, 63, 63, CONC, None, None, ol=CONC_D)
    street_grid(s, 64, 64, (20, 42), (20, 42))
    rng = random.Random(21)
    cells = [(2, 2, 16, 16), (24, 2, 15, 16), (46, 2, 15, 16),
             (2, 24, 16, 15), (46, 24, 15, 15),
             (2, 46, 16, 15), (24, 46, 15, 15), (46, 46, 15, 15)]
    roofs = [(ROOF_G, ROOF_GL), (ROOF_B, ROOF_BL), (WALL, WALL_L)]
    for (x, y, w, h) in cells:
        if rng.random() < 0.5:
            block(s, x, y, w // 2 - 1, h - 3, *rng.choice(roofs))
            block(s, x + w // 2 + 1, y + 2, w // 2 - 3, h - 5, *rng.choice(roofs))
        else:
            block(s, x, y, w - 3, h // 2 - 1, *rng.choice(roofs))
            block(s, x + 2, y + h // 2 + 1, w - 5, h // 2 - 3, *rng.choice(roofs))
    # 시청: 중앙 큰 건물 + 팀색 지붕 + 깃발
    s.rect(24, 24, 39, 39, WALL_L, WALL_L, WALL_D)
    s.rect(26, 26, 37, 37, WALL, None, WALL_D)
    s.trect(27, 27, 36, 30)
    s.trect(27, 33, 36, 36)
    s.rect(30, 30, 33, 32, WALL_D, None, None)
    s.line([(31, 22), (31, 8)], STEEL_L, 1)
    s.trect(32, 8, 40, 14)
    s.ell(8, 28, 14, 34, STEEL_L, WINL)
    return s.done()


def city():
    s = Sprite(64, 64)
    rng = random.Random(33)
    s.db.rectangle([0, 0, 63, 63], fill=GRASS)
    for _ in range(60):
        s.px([(rng.randint(0, 63), rng.randint(0, 63))], (96, 120, 64, 255))
    s.db.rectangle([28, 0, 35, 63], fill=ASPH)
    s.db.rectangle([0, 28, 63, 35], fill=ASPH)
    s.line([(31, 1), (31, 62)], ASPH_L)
    s.line([(1, 31), (62, 31)], ASPH_L)
    spots = [(3, 3, 11, 11), (16, 5, 10, 9), (38, 3, 10, 12), (51, 6, 10, 10),
             (3, 38, 12, 10), (18, 40, 9, 11), (39, 38, 11, 11), (52, 40, 9, 12)]
    for i, (x, y, w, h) in enumerate(spots):
        ro, rl = (ROOF_B, ROOF_BL) if i % 2 else (ROOF_G, ROOF_GL)
        block(s, x, y, w, h, ro, rl, floors=2)
    s.rect(26, 26, 37, 37, WALL_L, WALL_L, WALL_D)
    s.trect(28, 28, 35, 31)
    s.rect(30, 33, 33, 36, METAL_D, None, None)
    s.line([(37, 26), (37, 14)], STEEL_L)
    s.trect(38, 14, 45, 20)
    return s.done()


def village():
    s = Sprite(48, 48)
    rng = random.Random(44)
    s.db.rectangle([0, 0, 47, 47], fill=GRASS)
    for _ in range(50):
        s.px([(rng.randint(0, 47), rng.randint(0, 47))], (96, 120, 64, 255))
    s.rect(26, 28, 46, 46, (150, 128, 70, 255), None, None, ol=BROWN_D)
    for y in range(30, 46, 3):
        s.line([(27, y), (45, y)], (112, 150, 62, 255))
        s.line([(27, y + 1), (45, y + 1)], BROWN)
    s.db.rectangle([0, 22, 47, 26], fill=KHAKI_D)
    s.db.rectangle([0, 23, 47, 25], fill=SAND_D)
    for (x, y) in ((3, 4), (19, 6), (4, 30)):
        s.rect(x, y, x + 13, y + 12, ROOF_R, None, None)
        s.db.rectangle([x + 1, y + 1, x + 12, y + 5], fill=ROOF_RL)
        s.line([(x + 1, y + 6), (x + 12, y + 6)], ROOF_RD)
        s.px([(x + 3, y + 9), (x + 4, y + 9), (x + 9, y + 9), (x + 10, y + 9)], KHAKI_L)
    s.ell(34, 6, 42, 13, (190, 166, 84, 255), (220, 198, 112, 255))
    s.ell(17, 30, 24, 37, CONC, CONC_L)
    s.ell(19, 32, 22, 35, METAL_D)
    s.line([(40, 24), (40, 14)], STEEL_L)
    s.trect(41, 14, 46, 19)
    return s.done()


def supply_plant():
    """공장(보급품 변환): 톱니 지붕 작업장 + 굴뚝 + 상자 더미 (48x48)."""
    s = Sprite(48, 48)
    s.rect(0, 0, 47, 47, CONC, CONC_L, CONC_D)
    s.rect(3, 6, 33, 34, CONC_D, CONC, METAL_D)
    for i in range(3):
        y = 8 + i * 9
        s.rect(5, y, 31, y + 3, GLASS, STEEL_L, None)
        s.rect(5, y + 4, 31, y + 7, STEEL, STEEL_L, METAL_D)
    s.ell(36, 4, 44, 12, METAL_D, STEEL)          # 굴뚝
    s.ell(38, 6, 42, 10, OUT)
    s.rect(13, 31, 23, 36, OUT, None, None)       # 출입문
    s.trect(4, 35, 12, 38)                         # 팀색 간판
    for (x, y) in ((36, 16), (36, 24), (42, 20), (38, 33), (38, 40)):   # 보급 상자
        s.rect(x, y, x + 5, y + 5, WOOD_L, (210, 170, 120, 255), BROWN)
        s.line([(x + 1, y + 1), (x + 4, y + 4)], BROWN)
    s.tpoly([(3, 40), (30, 40), (30, 46), (3, 46)])  # 팀색 방수포
    return s.done()


# ============================== 지형 (16x16 -> 32x32) ==============================

def hash_kind(kind):
    return sum(ord(c) * (i + 3) for i, c in enumerate(kind))


def terrain(kind, v):
    rng = random.Random(1944 + v * 1013 + hash_kind(kind))
    img = Image.new("RGBA", (16, 16))
    d = ImageDraw.Draw(img)
    if kind == "grass":
        d.rectangle([0, 0, 15, 15], fill=(80, 104, 54, 255))
        for _ in range(26):
            x, y = rng.randint(0, 15), rng.randint(0, 15)
            d.point([(x, y)], fill=rng.choice([(70, 92, 46, 255), (92, 118, 62, 255), (76, 98, 50, 255)]))
        for _ in range(4):
            x, y = rng.randint(1, 14), rng.randint(2, 14)
            d.point([(x, y), (x, y - 1)], fill=(104, 132, 70, 255))
        if v % 2 == 1:
            d.point([(rng.randint(2, 13), rng.randint(2, 13))], fill=(216, 196, 104, 255))
    elif kind == "dirt":
        d.rectangle([0, 0, 15, 15], fill=(112, 86, 56, 255))
        for _ in range(30):
            d.point([(rng.randint(0, 15), rng.randint(0, 15))], fill=rng.choice([(94, 70, 44, 255), (128, 100, 68, 255)]))
        for _ in range(3):
            x, y = rng.randint(1, 13), rng.randint(1, 13)
            d.point([(x, y), (x + 1, y)], fill=STEEL)
            d.point([(x, y + 1)], fill=METAL_D)
    elif kind == "sand":
        d.rectangle([0, 0, 15, 15], fill=(198, 178, 128, 255))
        for y in (2, 6, 10, 14):
            o = rng.randint(-1, 1)
            for x in range(16):
                d.point([(x, (y + o + (x // 5) % 2) % 16)], fill=(216, 198, 148, 255))
                d.point([(x, (y + o + 1 + (x // 5) % 2) % 16)], fill=(176, 156, 108, 255))
    elif kind == "water":
        d.rectangle([0, 0, 15, 15], fill=(48, 86, 128, 255))
        for _ in range(4):
            x, y = rng.randint(0, 11), rng.randint(0, 14)
            d.line([(x, y), (x + 3, y)], fill=(88, 138, 184, 255))
            d.line([(x + 1, y + 1), (x + 2, y + 1)], fill=(34, 64, 98, 255))
    elif kind == "rock":
        d.rectangle([0, 0, 15, 15], fill=(66, 66, 72, 255))
        for (cx, cy) in [(4, 4), (11, 5), (6, 11), (12, 12)]:
            cx += rng.randint(-1, 1)
            cy += rng.randint(-1, 1)
            r = rng.randint(3, 4)
            d.ellipse([cx - r + 1, cy - r + 1, cx + r + 1, cy + r + 1], fill=(40, 40, 46, 255))
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(104, 104, 112, 255), outline=(30, 30, 34, 255))
            d.point([(cx - 1, cy - 1), (cx - 2, cy)], fill=(158, 158, 166, 255))
    return img.resize((32, 32), Image.NEAREST)


# ============================== 출력 ==============================

UNITS = {"infantry": infantry, "scout": scout}
BUILDINGS = {"supply_plant": supply_plant, "metropolis": metropolis, "city": city, "village": village}
TERRAINS = ["grass", "dirt", "sand", "water", "rock"]
NAMES = {"infantry": "소총병", "scout": "정찰병",
         "metropolis": "대도시", "city": "소도시", "village": "마을", "supply_plant": "보급 공장"}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "assets", "themes", "ww2")
    for sub in ("units", "buildings", "terrain"):
        os.makedirs(os.path.join(out, sub), exist_ok=True)
    entries = {}

    def put(group, name, fn, key):
        b, t = fn()
        rb, rt = f"{group}/{name}.png", f"{group}/{name}_team.png"
        b.save(os.path.join(out, rb))
        t.save(os.path.join(out, rt))
        entries[key] = {"frames": [rb], "teamFrames": [rt], "size": [b.width, b.height]}

    for k, fn in UNITS.items():
        put("units", k, fn, f"unit.{k}")
    for k, fn in BUILDINGS.items():
        put("buildings", k, fn, f"building.{k}")
    for k in TERRAINS:
        vs = []
        for v in range(4):
            rel = f"terrain/{k}_{v}.png"
            terrain(k, v).save(os.path.join(out, rel))
            vs.append(rel)
        entries[f"terrain.{k}"] = {"variants": vs, "size": [32, 32]}

    # 기존 theme.json 을 읽어 우리가 만든 키만 갱신하고 나머지(HD 방향 시트, 별칭 등)는 보존한다.
    path = os.path.join(out, "theme.json")
    theme = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            theme = json.load(f)
    sprites = theme.setdefault("sprites", {})
    for k, v in entries.items():
        if "sheet" in sprites.get(k, {}):   # HD 시트 항목은 덮어쓰지 않음
            continue
        sprites[k] = v
    for k in ("building.hq", "building.resource"):   # 제거된 건물
        sprites.pop(k, None)
    # 보병 계열은 보병 시트를 공유 (전용 시트가 생기면 해당 항목만 교체)
    for k in ("recruit", "veteran", "at_infantry"):
        sprites.setdefault(f"unit.{k}", {"alias": "unit.infantry"})
    theme["name"] = "2차대전 (임시 픽셀)"
    names = theme.setdefault("names", {})
    names.update(NAMES)
    for k in ("hq", "resource"):
        names.pop(k, None)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(theme, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
