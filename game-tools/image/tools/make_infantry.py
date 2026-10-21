"""보병(rifleman, key: infantry) 스프라이트 시트 생성기 — M1 개런드를 든 2차대전 미군 소총수.

오른쪽을 보는 방향(s/se/e/ne/n)만 그린다 (SW/W/NW 는 게임이 좌우 반전).
구조: 방향별 설정 표(CFG) + 자세별 함수(다리/몸통/머리/팔/소총) 를 정해진 순서로 겹쳐 그린다.
세력 색(철모 띠 + 어깨 휘장)은 마지막에 한꺼번에 team 레이어에 찍는다 (다른 부위가 덮어쓰지 않도록).
"""
from __future__ import annotations

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import P, Frame, save_sheet  # noqa: E402

CX = 32            # 몸 중심 x
HEAD_Y = 20        # 철모 중심 y (bob 0 기준)
SHOULDER_Y = 31
HIP_Y = 47
FOOT_Y = 72
BASE_BOB = 2      # 상체 전체를 2px 내려 다리를 짧게 (몸 비율)

# 추가 색 (팔레트에 없는 보조 음영)
C_CANVAS_DK = (112, 104, 76, 255)
C_FARLEG = (44, 40, 28, 255)
C_SMOKE = (205, 205, 200, 120)
C_SMOKE2 = (230, 230, 225, 80)

# ---------------------------------------------------------------- 방향 설정
# torso: 몸통 (왼쪽, 오른쪽) 오프셋 / hx: 철모 중심 이동 / stride: (발 앞뒤 x 벌림, 뒤 발 들림 높이)
# shoulders: (L, R) 어깨 위치 (e 는 L=먼쪽 팔, R=가까운 팔)
CFG = {
    "s":  dict(torso=(-8, 7), hx=0, stride=(1, 4), hipx=(-3, 3), shoulders=((-7, 31), (6, 31))),
    "se": dict(torso=(-7, 7), hx=1, stride=(3, 3), hipx=(-3, 3), shoulders=((-6, 31), (6, 31))),
    "e":  dict(torso=(-5, 4), hx=1, stride=(5, 2), hipx=(0, 0),  shoulders=((2, 31), (-1, 31))),
    "ne": dict(torso=(-7, 7), hx=1, stride=(3, 2), hipx=(-3, 3), shoulders=((-6, 31), (6, 31))),
    "n":  dict(torso=(-8, 7), hx=0, stride=(1, 4), hipx=(-3, 3), shoulders=((-7, 31), (6, 31))),
}
BACK = {"ne", "n"}  # 등이 보이는 방향

# 소총 (개머리판 B, 총구 M) — CX 기준 상대 좌표, 위쪽 몸 기준(bob 별도)
PORT = {  # 가슴 앞 대각선 (port arms)
    "s": ((-5, 47), (8, 29)), "se": ((-4, 47), (10, 28)), "e": ((-5, 45), (13, 29)),
}
SLUNG = ((-5, 51), (6, 25))  # 등에 멘 총 (ne / n)
AIM = {
    "s": ((5, 33), (4, 52)), "se": ((4, 33), (15, 47)), "e": ((-2, 33), (20, 31)),
    "ne": ((2, 33), (15, 19)), "n": ((6, 33), (7, 13)),
}
ALT = {  # 머리 위로 치켜든 소총
    "s": ((-13, 9), (13, 9)), "se": ((-12, 11), (13, 8)), "e": ((-10, 11), (14, 8)),
    "ne": ((-12, 11), (13, 8)), "n": ((-13, 9), (13, 9)),
}
# 조준 시 다리 벌림 (왼발, 오른발) x 오프셋
AIM_FEET = {"s": (-5, 5), "se": (-4, 5), "e": (-5, 5), "ne": (-4, 5), "n": (-5, 5)}


# ---------------------------------------------------------------- 기본 그리기 도우미
def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def rnd(p):
    return (int(round(p[0])), int(round(p[1])))


def stamp(f: Frame, x0, y0, w, c):
    f.rect(x0, y0, x0 + w - 1, y0 + w - 1, c)


def limb(f: Frame, p0, p1, w, dark, mid, light=None):
    """굵은 선분: 오른쪽 · 아래 1px 은 dark, 안쪽 mid, 왼쪽 위 하이라이트 light (왼쪽 위 광원)."""
    p0, p1 = rnd(p0), rnd(p1)
    n = max(abs(p1[0] - p0[0]), abs(p1[1] - p0[1]), 1)
    pts = [rnd(lerp(p0, p1, i / n)) for i in range(n + 1)]
    for c, ww in ((dark, w), (mid, w - 1), (light, max(1, w - 3))):
        if c is None or ww < 1:
            continue
        for x, y in pts:
            stamp(f, x - w // 2, y - w // 2, ww, c)


def disc(f: Frame, cx, cy, r, c):
    f.ellipse(cx, cy, r, r, c)


# ---------------------------------------------------------------- 그림 컨텍스트
class Pose:
    def __init__(self, d: str, name: str):
        self.d, self.name = d, name
        self.cfg = CFG[d]
        self.f = Frame()
        self.team: list[tuple[int, int, int]] = []   # (x, y, 회색조) 지연 기록
        # 자세별 몸 높낮이(bob)와 걸음 부호
        self.step = 0
        self.bob = 0
        if name == "walk_a":
            self.step, self.bob = 1, BASE_BOB
        elif name == "walk_b":
            self.step, self.bob = -1, BASE_BOB - 1      # 1px 위아래 흔들림
        elif name in ("aim", "fire"):
            self.bob = BASE_BOB + 2      # 살짝 웅크림
        else:
            self.bob = BASE_BOB
        self.recoil = name == "fire"

    # 상대 좌표 → 프레임 좌표 (위쪽 몸은 bob 적용)
    def U(self, p):
        return (CX + p[0], p[1] + self.bob)

    def mark(self, x, y, g):
        self.team.append((int(x), int(y), g))


# ---------------------------------------------------------------- 다리
def foot_targets(ps: Pose):
    """두 다리의 발 위치 [(x, y), (x, y)] (프레임 좌표)."""
    d, n = ps.d, ps.name
    sx, lift = ps.cfg["stride"]
    hx = ps.cfg["hipx"]
    base = [CX + hx[0], CX + hx[1]]
    if n in ("aim", "fire"):
        a = AIM_FEET[d]
        return [(CX + a[0], FOOT_Y), (CX + a[1], FOOT_Y)]
    if n in ("walk_a", "walk_b"):
        out = []
        for i, s in enumerate((ps.step, -ps.step)):
            x = base[i] + sx * s
            if d == "e":
                x += 0
            y = FOOT_Y - (lift if s < 0 else 0)
            out.append((x, y))
        return out
    # 서 있기 / 대기: e 는 발을 약간 벌림
    if d == "e":
        return [(CX + 2, FOOT_Y), (CX - 2, FOOT_Y)]
    if d in ("se", "ne"):
        return [(base[0], FOOT_Y), (base[1], FOOT_Y)]
    return [(base[0], FOOT_Y), (base[1], FOOT_Y)]


def draw_leg(ps: Pose, hip, foot, far=False):
    f = ps.f
    toe = {"e": 3, "se": 1, "ne": 0}.get(ps.d, 0)
    ank = (foot[0], foot[1] - 3)
    knee = lerp(hip, ank, 0.55)
    if far:
        pc = (C_FARLEG, P["khaki_dark"], P["khaki_dark"])
        lc = (C_FARLEG, C_CANVAS_DK, C_CANVAS_DK)
    else:
        pc = (P["khaki_dark"], P["khaki"], P["khaki_light"])
        lc = (C_CANVAS_DK, P["canvas"], P["canvas_light"])
    limb(f, hip, knee, 5, *pc)
    limb(f, knee, ank, 5, *lc)
    # 각반 끈 힌트
    kx, ky = rnd(lerp(knee, ank, 0.45))
    f.rect(kx - 2, ky, kx + 2, ky, C_CANVAS_DK)
    # 군화
    fx, fy = foot
    f.rect(fx - 2, fy - 2, fx + 2 + toe, fy, P["leather"])
    f.rect(fx - 2, fy, fx + 2 + toe, fy, P["leather_dark"])
    f.px(fx - 2, fy - 2, P["leather_dark"] if far else P["leather"])
    if not far:
        f.px(fx - 1, fy - 2, (136, 98, 62, 255))


def draw_legs(ps: Pose):
    feet = foot_targets(ps)
    hy = HIP_Y + ps.bob
    hips = [(CX + ps.cfg["hipx"][0], hy), (CX + ps.cfg["hipx"][1], hy)]
    # 위쪽(먼) 발을 먼저 그린다
    order = sorted(range(2), key=lambda i: feet[i][1])
    if ps.d == "e":  # 옆모습: 오른쪽 다리(가까운) 가 항상 앞
        order = [0, 1]
    for k, i in enumerate(order):
        draw_leg(ps, hips[i], feet[i], far=(ps.d == "e" and k == 0))


# ---------------------------------------------------------------- 몸통 · 배낭 · 허리띠
def draw_torso(ps: Pose):
    f, d = ps.f, ps.d
    xl, xr = ps.cfg["torso"]
    y0, y1 = 28 + ps.bob, 47 + ps.bob
    inset = {0: 3, 1: 2, 2: 1, 3: 1}
    for y in range(y0, y1 + 1):
        ins = inset.get(y - y0, 0)
        # 재킷 밑단은 약간 좁게
        if y >= y1 - 1:
            ins = 1
        for x in range(CX + xl + ins, CX + xr - ins + 1):
            rel = x - (CX + xl)
            wid = xr - xl
            if rel <= 1:
                c = P["od_light"]
            elif wid - rel <= 2:
                c = P["od_dark"]
            else:
                c = P["od"]
            f.px(x, y, c)
    # 주름 · 앞 여밈 / 등 이음매
    mid = CX + (xl + xr) // 2
    if d == "s":
        f.line(mid, y0 + 3, mid, y1 - 4, P["od_dark"])
        f.rect(CX - 6, y0 + 7, CX - 4, y0 + 8, P["od_dark"])          # 가슴 주머니
        f.rect(CX + 3, y0 + 7, CX + 5, y0 + 8, P["od_dark"])
        f.px(CX - 6, y0 + 7, P["od_light"])
    elif d == "se":
        f.line(mid + 2, y0 + 3, mid + 2, y1 - 4, P["od_dark"])
        f.rect(CX + 3, y0 + 7, CX + 5, y0 + 8, P["od_dark"])
    elif d == "e":
        f.line(CX + 3, y0 + 4, CX + 3, y1 - 5, P["od_dark"])
        f.px(CX - 3, y0 + 8, P["od_dark"])
    else:  # 등
        f.line(mid, y0 + 2, mid, y1 - 3, P["od_dark"])
        f.rect(CX - 6, y0 + 5, CX + 5, y0 + 5, P["od_dark"])           # 어깨 요크선
    # 허리띠 + 탄약 주머니
    by = y1 - 5
    f.rect(CX + xl + 1, by, CX + xr - 1, by + 1, P["leather"])
    f.rect(CX + xl + 1, by + 1, CX + xr - 1, by + 1, P["leather_dark"])
    pouches = {"s": (-6, 3), "se": (-5, 3), "e": (0, None), "ne": (-6, 3), "n": (-7, 4)}[d]
    for px0 in pouches:
        if px0 is None:
            continue
        f.rect(CX + px0, by, CX + px0 + 2, by + 3, P["leather"])
        f.rect(CX + px0, by + 3, CX + px0 + 2, by + 3, P["leather_dark"])
        f.px(CX + px0, by, (136, 98, 62, 255))
    if d in ("s", "se"):
        f.px(CX + (0 if d == "s" else 1), by, P["steel_light"])        # 버클


def draw_pack(ps: Pose, before_torso: bool):
    """배낭 + 야전삽. e/se 는 몸 뒤쪽에 (먼저), ne/n 은 등 위에 (나중)."""
    f, d = ps.f, ps.d
    y0, y1 = 31 + ps.bob, 45 + ps.bob
    if d in ("e", "se") and before_torso:
        x0, x1 = {"e": (CX - 9, CX - 4), "se": (CX - 9, CX - 5)}[d]
    elif d in ("n", "ne") and not before_torso:
        x0, x1 = {"n": (CX - 6, CX + 5), "ne": (CX - 7, CX + 4)}[d]
    else:
        return
    for y in range(y0, y1 + 1):
        ins = 1 if (y == y0 or y == y1) else 0
        for x in range(x0 + ins, x1 - ins + 1):
            rel = x - x0
            c = P["canvas_light"] if rel <= 1 else (C_CANVAS_DK if x1 - x <= 1 else P["canvas"])
            if y >= y1 - 1:
                c = C_CANVAS_DK if c != P["canvas_light"] else P["canvas"]
            f.px(x, y, c)
    f.rect(x0, y0 + 3, x1, y0 + 3, P["leather_dark"])                   # 덮개 끈
    if d in ("n", "ne"):
        sx = x0 + (3 if d == "n" else 2)
        f.line(sx, y0 + 1, sx, y1 - 1, P["od_dark"])                     # 멜빵
        f.line(x1 - 3, y0 + 1, x1 - 3, y1 - 1, P["od_dark"])
        # 야전삽: 나무 자루 + 강철 날
        hx = x0 + 6 if d == "n" else x0 + 5
        f.line(hx, y0 + 5, hx, y1 + 1, P["wood"])
        f.rect(hx - 1, y1 + 1, hx + 2, y1 + 3, P["steel"])
        f.rect(hx - 1, y1 + 1, hx, y1 + 1, P["steel_light"])
        f.line(x0, y0 - 1, x1, y0 - 1, P["od_dark"])                     # 모포 말이 윗선
        f.rect(x0 + 1, y0 - 2, x1 - 1, y0 - 2, P["od"])


# ---------------------------------------------------------------- 머리
def draw_head(ps: Pose):
    f, d = ps.f, ps.d
    hx = CX + ps.cfg["hx"]
    cy = HEAD_Y + ps.bob
    skin, skd, skl = P["skin"], P["skin_dark"], P["skin_light"]
    # --- 얼굴 / 목
    if d == "s":
        f.rect(hx - 3, cy + 4, hx + 3, cy + 7, skin)
        f.rect(hx - 2, cy + 8, hx + 2, cy + 8, skd)
        f.rect(hx - 3, cy + 4, hx + 3, cy + 4, skd)
        f.px(hx - 3, cy + 6, skl); f.px(hx - 3, cy + 7, skl)
        f.px(hx - 2, cy + 6, P["eye"]); f.px(hx + 2, cy + 6, P["eye"])
        f.px(hx, cy + 7, skd)
        f.px(hx - 1, cy + 8, skd); f.px(hx + 1, cy + 8, skd)           # 입
    elif d == "se":
        f.rect(hx - 3, cy + 4, hx + 4, cy + 7, skin)
        f.rect(hx - 2, cy + 8, hx + 3, cy + 8, skd)
        f.rect(hx - 3, cy + 4, hx + 4, cy + 4, skd)
        f.px(hx - 3, cy + 6, skl); f.px(hx - 3, cy + 7, skl)
        f.px(hx - 1, cy + 6, P["eye"]); f.px(hx + 3, cy + 6, P["eye"])
        f.px(hx + 1, cy + 6, skd); f.px(hx + 4, cy + 6, skd)
        f.px(hx + 1, cy + 7, skd)
    elif d == "e":
        f.rect(hx - 2, cy + 4, hx + 4, cy + 7, skin)
        f.rect(hx - 1, cy + 8, hx + 4, cy + 8, skd)
        f.rect(hx - 2, cy + 4, hx + 4, cy + 4, skd)
        f.px(hx + 5, cy + 6, skin)                                      # 코
        f.px(hx + 3, cy + 6, P["eye"])
        f.rect(hx - 2, cy + 5, hx - 1, cy + 7, skd)                      # 귀 · 뺨 그늘
        f.px(hx + 2, cy + 7, skd); f.px(hx + 3, cy + 7, skd)
    elif d == "ne":
        f.rect(hx - 3, cy + 4, hx + 3, cy + 7, skd)
        f.rect(hx + 2, cy + 4, hx + 4, cy + 6, skin)
        f.px(hx + 2, cy + 6, skd)
    else:  # n
        f.rect(hx - 3, cy + 4, hx + 3, cy + 7, skd)
        f.rect(hx - 3, cy + 4, hx - 2, cy + 6, skin)
        f.rect(hx - 2, cy + 8, hx + 2, cy + 8, skd)
    # --- 철모 (타원 + 음영 + 위장망)
    rx, ry = 7.0, 5.2
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(hx - 8, hx + 9):
            u, v = (x - hx) / rx, (y - cy) / ry
            if u * u + v * v > 1.0:
                continue
            s = -(u + v) * 0.7
            if v > 0.55:
                c = P["helmet_dark"]                                    # 챙 그림자
            elif s > 0.42:
                c = P["helmet_light"]
            elif s < -0.45:
                c = P["helmet_dark"]
            else:
                c = P["helmet"]
                if v < 0.5 and (x + 2 * y) % 5 == 0:
                    c = P["net"]                                        # 위장망 점
            if v < -0.45 and s > 0.1 and abs(u) < 0.6:
                c = P["helmet_light"]
            f.px(x, y, c)
    # 챙(앞/뒤로 살짝 튀어나옴)
    if d == "e":
        f.rect(hx - 6, cy + 5, hx + 7, cy + 5, P["helmet_dark"])
    elif d in ("s", "se"):
        f.rect(hx - 6, cy + 4, hx + 6, cy + 4, P["helmet_dark"])
    else:
        f.rect(hx - 7, cy + 5, hx + 7, cy + 5, P["helmet_dark"])
    # 턱끈
    if d in ("s", "se"):
        f.px(hx - 4, cy + 5, P["leather"]); f.px(hx + 4, cy + 5, P["leather"])
        f.px(hx - 4, cy + 6, P["leather_dark"]); f.px(hx + 4, cy + 6, P["leather_dark"])
    elif d == "e":
        f.px(hx - 1, cy + 6, P["leather_dark"]); f.px(hx + 1, cy + 7, P["leather_dark"])
    # 세력 색 띠 (철모를 두른 곡선 띠, 2px)
    for x in range(hx - 7, hx + 8):
        u = (x - hx) / rx
        if abs(u) >= 1:
            continue
        top = cy + 1.4 + 1.6 * (1 - u * u) ** 0.5 * 0.9 - 0.2
        yy = int(round(cy + 1 + 0.9 * (1 - u * u)))
        for k in range(2):
            y = yy + k
            if ((x - hx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                g = 215 if u < -0.35 else (110 if u > 0.45 else 165)
                g = g if k == 0 else g - 35
                ps.mark(x, y, g)


# ---------------------------------------------------------------- 소총 · 팔
def draw_rifle(ps: Pose, B, M):
    """B, M = 프레임 좌표. 개머리판(나무) → 손잡이/덮개(나무) → 총열(강철)."""
    f = ps.f
    t1, t2 = 0.27, 0.52
    p1, p2 = lerp(B, M, t1), lerp(B, M, t2)
    limb(f, p2, M, 2, P["steel_dark"], P["steel"], None)
    f.px(*rnd(M), P["steel_light"])
    limb(f, B, p1, 3, P["wood_dark"], P["wood"], None)
    limb(f, p1, p2, 2, P["wood_dark"], P["wood"], None)
    # 리시버(강철) 와 방아쇠울 힌트
    r = rnd(lerp(B, M, 0.34))
    f.px(r[0], r[1], P["steel"])
    # 윗면 하이라이트
    h0, h1 = lerp(B, p2, 0.1), lerp(B, p2, 0.9)
    f.px(*rnd(h0), (156, 110, 66, 255))
    f.px(*rnd(h1), (156, 110, 66, 255))


def draw_arm(ps: Pose, sh, hand, side):
    """어깨 → 팔꿈치 → 손. 소매 OD + 손(살색). 어깨 휘장 위치를 기록."""
    f = ps.f
    sh, hand = (float(sh[0]), float(sh[1])), (float(hand[0]), float(hand[1]))
    mid = lerp(sh, hand, 0.5)
    elbow = (mid[0] + side * 2, mid[1] + 1)
    limb(f, sh, elbow, 4, P["od_dark"], P["od"], P["od_light"])
    limb(f, elbow, hand, 3, P["od_dark"], P["od"], P["od_light"])
    hx, hy = rnd(hand)
    f.rect(hx - 1, hy - 1, hx, hy, P["skin"])
    f.px(hx, hy, P["skin_dark"])
    # 어깨 휘장 (3x3, 위쪽이 밝음)
    c = rnd(lerp(sh, elbow, 0.45))
    for dy, g in ((-1, 220), (0, 165), (1, 120)):
        for dx in (-1, 0, 1):
            ps.mark(c[0] + dx, c[1] + dy, g if dx < 1 else g - 25)


def hang_hand(ps: Pose, side, swing):
    """내린 팔(총을 등에 멘 방향 / 일부). 손 위치."""
    d = ps.d
    sx = -8 if side < 0 else 7
    if d == "ne":
        sx = -7 if side < 0 else 6
    return (CX + sx, 44 + ps.bob + swing)


# ---------------------------------------------------------------- 조립
def draw_unit(d: str, name: str) -> Frame:
    ps = Pose(d, name)
    f = ps.f
    cfg = ps.cfg
    L, R = cfg["shoulders"]
    Ls, Rs = ps.U(L), ps.U(R)
    sideL, sideR = (-1, 1) if d != "e" else (1, -1)   # e: L=먼쪽(앞), R=가까운쪽(뒤)
    if d == "e":
        sideL, sideR = 1, -1

    pose_kind = {"idle": "port", "walk_a": "port", "walk_b": "port",
                 "aim": "aim", "fire": "aim", "idle_alt": "alt"}[name]
    # 등을 보이는 방향의 서 있기/걷기 = 총을 등에 멤
    sling = pose_kind == "port" and d in BACK

    # --- 소총 좌표 결정
    if pose_kind == "port":
        Bm, Mm = SLUNG if sling else PORT[d]
        sw = 1 if ps.step > 0 else (-1 if ps.step < 0 else 0)
        B, M = ps.U((Bm[0] + (sw if not sling else 0), Bm[1])), ps.U((Mm[0] + (sw if not sling else 0), Mm[1]))
    elif pose_kind == "aim":
        Bm, Mm = AIM[d]
        B, M = ps.U(Bm), ps.U(Mm)
    else:
        Bm, Mm = ALT[d]
        B, M = ((CX + Bm[0], Bm[1]), (CX + Mm[0], Mm[1]))
    u = (M[0] - B[0], M[1] - B[1])
    ul = math.hypot(*u) or 1.0
    u = (u[0] / ul, u[1] / ul)
    if ps.recoil:
        k = 2 if d in ("e", "se", "ne") else 1.5
        B = (B[0] - u[0] * k, B[1] - u[1] * k)
        M = (M[0] - u[0] * k, M[1] - u[1] * k)

    # --- 팔 목표(손)
    if pose_kind == "alt":
        hL = (CX + (Ls[0] - CX) + (-2 if d != "e" else 0), 12)
        hR = (CX + (Rs[0] - CX) + (2 if d != "e" else 0), 12)
        if d == "e":
            hL, hR = (CX + 6, 12), (CX - 4, 12)
        elif d in ("se", "ne"):
            hL, hR = (CX - 8, 12), (CX + 8, 12)
        hands = (hL, hR)
    elif pose_kind == "aim":
        hands = (lerp(B, M, 0.55), lerp(B, M, 0.10))
    elif sling:
        swv = ps.step * 2
        hands = (hang_hand(ps, -1, -swv), hang_hand(ps, 1, swv))
    else:
        hands = (lerp(B, M, 0.30 if d != "e" else 0.65), lerp(B, M, 0.65 if d != "e" else 0.28))
        if d != "e":
            hands = (lerp(B, M, 0.30), lerp(B, M, 0.65))
        else:
            hands = (lerp(B, M, 0.65), lerp(B, M, 0.28))

    # ===== 그리는 순서 =====
    draw_pack(ps, before_torso=True)
    rifle_behind = d == "n" and pose_kind == "aim"
    if rifle_behind:
        draw_rifle(ps, B, M)
    draw_legs(ps)
    if d == "e":
        draw_arm(ps, Ls, hands[0], sideL)           # 먼쪽 팔은 몸통 뒤
    draw_torso(ps)
    draw_pack(ps, before_torso=False)
    if sling:
        draw_rifle(ps, ps.U(SLUNG[0]) if False else B, M)
    if d == "n" and pose_kind == "aim":
        draw_arm(ps, Ls, hands[0], sideL)           # 왼팔은 머리 뒤
    if pose_kind == "alt":
        draw_arm(ps, Ls, hands[0], sideL)
        draw_arm(ps, Rs, hands[1], sideR)
    draw_head(ps)
    if not rifle_behind and not sling:
        draw_rifle(ps, B, M)
    if pose_kind != "alt":
        if d != "e" and not (d == "n" and pose_kind == "aim"):
            draw_arm(ps, Ls, hands[0], sideL)
        draw_arm(ps, Rs, hands[1], sideR)
    else:
        # 손을 소총 위에 다시 덮음
        for h in hands:
            hx, hy = rnd(h)
            f.rect(hx - 1, hy - 1, hx, hy, P["skin"])
            f.px(hx, hy, P["skin_dark"])
    if d == "e" and pose_kind == "alt":
        pass

    # 세력 색 (마지막에)
    for x, y, g in ps.team:
        f.px(x, y, g, team=True)
    f.outline()

    # 총구 섬광 + 연기 (테두리 밖에 그림)
    if ps.recoil:
        draw_flash(f, M, u)
    return f


def draw_flash(f: Frame, M, u):
    c = (M[0] + u[0] * 3, M[1] + u[1] * 3)
    f.ellipse(c[0], c[1], 3.6, 3.6, P["flash_edge"])
    # 총구 방향으로 길쭉한 불꽃
    tip = (M[0] + u[0] * 7, M[1] + u[1] * 7)
    f.line(round(M[0]), round(M[1]), round(tip[0]), round(tip[1]), P["flash_edge"], 2)
    f.ellipse(c[0], c[1], 2.6, 2.6, P["flash"])
    f.line(round(M[0] + u[0]), round(M[1] + u[1]), round(M[0] + u[0] * 6), round(M[1] + u[1] * 6), P["flash"])
    f.ellipse(M[0] + u[0] * 2.2, M[1] + u[1] * 2.2, 1.5, 1.5, P["flash_core"])
    # 연기
    sc = (M[0] + u[0] * 9, M[1] + u[1] * 9)
    f.ellipse(sc[0], sc[1], 1.8, 1.8, C_SMOKE)
    f.px(round(sc[0] - u[1] * 2), round(sc[1] + u[0] * 2), C_SMOKE2)
    f.px(round(sc[0] + u[1] * 2), round(sc[1] - u[0] * 2), C_SMOKE2)


if __name__ == "__main__":
    save_sheet("infantry", draw_unit)
    print("ok")
