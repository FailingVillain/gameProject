#!/usr/bin/env python3
"""C1E1 MAP-A (압록강 서쪽 어귀) 지도 생성기. 같은 입력 = 같은 바이트.

사용: python tools/c1e1/gen_map_a.py [--width 800] [--height 800] [--out assets/maps/c1e1_map_a.json]
                                      [--doc docs/C1E1_MAPS.md] [--sheet <구역시트.png>]
좌표는 800x800 기준으로 설계하고 위치 / 큰 덩어리만 W,H 비율로 늘린다 (강 폭 · 여울 폭 · 가옥 크기는 절대 칸 수).
북쪽 = 위(y 작음), 남안(출발) = 아래. 강은 동서, 상류 = 동쪽(압록강은 서쪽 어귀로 흘러 나감).
Ford / Bridge 지형은 main 에 아직 없으므로 여울 = 강 전폭을 sand/dirt 로 메운 틈 (c1_01_yalu 방식).
"""
import argparse, json, os, sys

BASE = 800
RIVER_T = 26          # 강 폭 (칸): 스펙 '20~30칸'
BANK = 2              # 모래 둑 폭
FORD_W = 14           # 여울 폭 (칸, 동서 방향)
CHUNK = 8             # 사행(蛇行) 단위 열 수


def build(W, H):
    sx = lambda v: round(v * W / BASE)
    sy = lambda v: round(v * H / BASE)
    paint = []
    def P(t, **kw):
        d = {"terrain": t}; d.update(kw); paint.append(d)
    def rect(t, x, y, w, h, scale=True):
        if scale:
            x2, y2 = sx(x + w), sy(y + h); x, y = sx(x), sy(y); w, h = x2 - x, y2 - y
        P(t, rect=[x, y, w, h])

    # --- 지세 바탕 (남안: 평야 / 북안: 평야 / 내륙: 밭 · 숲)
    cy = sy(380)                                    # 강 중심선 y
    def tri(x):                                     # 정수 삼각파 사행 (-10..10)
        period = max(8, sx(240)); k = (x % period) * 4 // period
        ph = (x % period) * 4 - k * period
        v = [ph, period - ph, -ph, ph - period][k]
        return v * 10 // max(1, period)
    centre = lambda x: cy + tri(x)

    # 남안 밭 · 바닥 흙 (밭 = dirt)
    for (x, y, w, h) in [(40, 560, 120, 60), (220, 600, 140, 70), (560, 580, 150, 80), (470, 700, 120, 50), (60, 690, 130, 55)]:
        rect("dirt", x, y, w, h)
    # 주도로: 남 -> 주 도하 지점 -> 북안 마을 -> 내륙 (dirt 4칸)
    rect("dirt", 386, 0, 4, 800)
    # 서쪽 소구릉(기관총 거점) / 동쪽 소나무 숲 / 내륙 외곽 숲 / 남안 숲 조각
    P("high", circle=[sx(170), sy(300), max(6, sx(14))])
    P("forest", rect=[sx(600), sy(270), sx(100), sy(80)])
    for (x, y, w, h) in [(40, 60, 90, 80), (620, 40, 110, 90), (250, 190, 70, 40), (560, 170, 80, 60), (90, 470, 70, 60), (680, 480, 80, 70)]:
        rect("forest", x, y, w, h)

    # --- 강 (사행) : 열 CHUNK 단위로 water + 양안 sand 둑
    fords = {  # 이름 -> 중심 x (800 기준)
        "a_ford_up": 570,           # 상류 모래톱 (sand)
        "a_ford_down": 180,         # 하류 갈대섬 (sand + grass 중주)
        "a_main_crossing": 388,     # 임시 부교 (dirt)
    }
    fx = {k: sx(v) - FORD_W // 2 for k, v in fords.items()}
    def ford_of(x):
        for k, v in fx.items():
            if v <= x < v + FORD_W: return k
        return None
    half = RIVER_T // 2
    x = 0
    while x < W:
        w = min(CHUNK, W - x)
        c = centre(x)
        fk = None
        for xx in range(x, x + w):
            fk = fk or ford_of(xx)
        P("sand", rect=[x, c - half - BANK, w, RIVER_T + 2 * BANK])   # 둑 포함 모래띠
        P("water", rect=[x, c - half, w, RIVER_T])
        x += w
    # 여울: 강 전폭 + 둑을 열 단위로 메운다 (칠하기 순서상 강 뒤)
    for k, x0 in fx.items():
        t = "dirt" if k == "a_main_crossing" else "sand"
        xx = x0
        while xx < x0 + FORD_W:
            w = min(CHUNK, x0 + FORD_W - xx); c = centre(xx)
            P(t, rect=[xx, c - half - BANK, w, RIVER_T + 2 * BANK])
            xx += w
    # 하류 갈대섬: 여울 중앙에 풀(갈대 대용 — reed 지형은 없음) 3칸 폭 줄무늬는 통행 가능
    c = centre(fx["a_ford_down"] + FORD_W // 2)
    P("grass", rect=[fx["a_ford_down"] + 5, c - 4, 4, 8])
    # 제방(남안 / 북안) : 고지 표시만, 여울 앞 통로는 그대로 통행
    P("high", rect=[0, cy + half + BANK + 1, W, 2])
    P("high", rect=[0, cy - half - BANK - 3, W, 2])

    # --- 가옥
    houses = []
    # 소 나루 마을 (북안, 가옥 10): 5x2 격자
    for i in range(10):
        houses.append((sx(430) + (i % 5) * 14, sy(295) + (i // 5) * 14, 5, 4))
    # 소초(SK2): 창고 + 연락소
    houses += [(sx(290), sy(270), 8, 5), (sx(302), sy(270), 4, 4)]
    # 내륙 마을 (가옥 10)
    seed = 12345
    for i in range(10):
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        jx = (seed >> 8) % 5 - 2
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        jy = (seed >> 8) % 5 - 2
        houses.append((sx(400) + (i % 5) * 16 + jx, sy(110) + (i // 5) * 16 + jy, 5, 4))
    for hx, hy, hw, hh in houses:
        P("house", rect=[hx, hy, hw, hh])

    starts = [[sx(395), sy(740)], [sx(395), sy(60)]]

    # --- 구역 (docs 표 · 구역 시트). 이름 약속 = IMPLEMENTATION_PLAN §3. (x, y, w, h)
    A = {}
    A["a_south"] = (0, sy(400), W, H - sy(400))
    A["a_river"] = (0, cy - half - BANK - 10, W, RIVER_T + 2 * BANK + 20)
    A["a_north_bank"] = (sx(100), sy(255), sx(600), sy(105))
    A["a_inland"] = (0, 0, W, sy(255))
    for k, v in fx.items():
        A[k] = (v - 8, cy - half - BANK - 14, FORD_W + 16, RIVER_T + 2 * BANK + 28)
    A["a_village"] = (sx(422), sy(287), 5 * 14 + 14, 2 * 14 + 12)
    A["a_hill_mg"] = (sx(170) - max(6, sx(14)) - 2, sy(300) - max(6, sx(14)) - 2, 2 * max(6, sx(14)) + 4, 2 * max(6, sx(14)) + 4)
    A["a_pine_east"] = (sx(600), sy(270), sx(100), sy(80))
    A["a_outpost"] = (sx(282), sy(262), 36, 24)
    A["a_escape"] = (sx(300), 0, sx(200), max(8, sy(12)))
    A["a_inland_village"] = (sx(392), sy(102), 5 * 16 + 14, 2 * 16 + 14)

    mf = {"name": "압록강 서쪽 어귀 (C1E1 MAP-A)", "width": W, "height": H, "base": "grass",
          "paint": paint, "starts": starts}
    return mf, A, fx, houses, centre


HEADER = {"a_south": "남안 출발 (평야 · 제방)", "a_river": "압록강 (폭 26칸, 사행 ±10)", "a_north_bank": "북안 교두보",
          "a_inland": "압록강 이북 내륙 (SK1 비적 소탕)", "a_ford_up": "상류 여울 (모래톱)", "a_ford_down": "하류 여울 (갈대섬)",
          "a_main_crossing": "주 도하 지점 (임시 부교)", "a_village": "소 나루 마을 (가옥 10)", "a_hill_mg": "서쪽 소구릉 (기관총 거점)",
          "a_pine_east": "동쪽 소나무 숲 (구련성 방면)", "a_outpost": "SK2 소초 (창고 · 연락소, 북안 외곽)",
          "a_escape": "탈출 구역 (북단) [기본값]", "a_inland_village": "내륙 마을 (가옥 10, SK1) [내부 보조 구역]"}
ORDER = ["a_south", "a_river", "a_north_bank", "a_inland", "a_ford_up", "a_ford_down", "a_main_crossing", "a_village",
         "a_hill_mg", "a_pine_east", "a_outpost", "a_escape", "a_inland_village"]


def dump(mf):
    lines = ["{",
             "  // C1E1 MAP-A: tools/c1e1/gen_map_a.py 가 생성 (손대지 말고 생성기를 고칠 것). 북 = 위, 강 = 동서, 상류 = 동.",
             f'  "name": {json.dumps(mf["name"], ensure_ascii=False)},',
             f'  "width": {mf["width"]}, "height": {mf["height"]}, "base": "grass",',
             '  "paint": [']
    ps = [f"    {json.dumps(p, ensure_ascii=False, separators=(', ', ': '))}" for p in mf["paint"]]
    lines.append(",\n".join(ps))
    lines.append("  ],")
    lines.append(f'  "starts": {json.dumps(mf["starts"])}')
    lines.append("}")
    return "\n".join(lines) + "\n"


def doc_section(W, H, A, fx, cy):
    out = ["<!-- MAP-A:begin (gen_map_a.py 가 생성) -->", "## MAP-A  압록강 서쪽 어귀 (medium %dx%d)" % (W, H), "",
           "생성: `python tools/c1e1/gen_map_a.py --width %d --height %d` -> `assets/maps/c1e1_map_a.json`. 북 = 위(y 작음), 강 = 동서, 상류 = 동." % (W, H),
           "구역 이름은 계획서 §3 약속. 좌표 = 칸 단위 `x, y, w, h` (미션 JSON `areas` 에 그대로 `rect` 로 쓴다).", "",
           "| 구역 | 설명 | x | y | w | h |", "|---|---|---|---|---|---|"]
    for k in ORDER:
        x, y, w, h = A[k]
        out.append(f"| `{k}` | {HEADER[k]} | {x} | {y} | {w} | {h} |")
    out += ["", "시작 위치(지도 필수 2개): 남 [%d, %d], 북 [%d, %d] (임시, 미션 JSON 이 forces 로 대체)." % (round(395 * W / BASE), round(740 * H / BASE), round(395 * W / BASE), round(60 * H / BASE)),
            "", "참고:",
            "- 강은 지도 전폭 water. 통행 가능한 도하는 정확히 3곳 (`a_ford_up` sand, `a_ford_down` sand + 중주, `a_main_crossing` dirt, 폭 %d칸). 그 밖은 통행 불가 (시험: `KD_SELFTEST_ONLY=c1e1maps`)." % FORD_W,
            "- 압록강철교(안동-신의주)는 일본 관리 구간이라 도하 불가: 지도에는 그리지 않음 (강 water).",
            "- 갈대밭: 갈대 지형이 없어 grass 로 둠 (DECISIONS_PENDING 질문). 밭 = dirt. 제방 = 고지(`high`) 표시 띠.",
            "- 가옥: 북안 마을 10 + SK2 소초 2 + 내륙 마을 10. 가옥 위치는 고정값 + 상수 시드(재현 가능).",
            "<!-- MAP-A:end -->", ""]
    return "\n".join(out)


def sheet(path, mf, A, W, H):
    from PIL import Image, ImageDraw
    col = {"grass": (70, 116, 60), "dirt": (126, 102, 70), "sand": (194, 178, 126), "water": (42, 84, 138), "forest": (38, 78, 40), "house": (150, 92, 70)}
    img = Image.new("RGB", (W, H), col["grass"]); px = img.load()
    for p in mf["paint"]:
        t = p["terrain"]
        if "rect" in p and t in col:
            x, y, w, h = p["rect"]
            for yy in range(max(0, y), min(H, y + h)):
                for xx in range(max(0, x), min(W, x + w)): px[xx, yy] = col[t]
        if "circle" in p:
            cx, cyy, r = p["circle"]
            d = ImageDraw.Draw(img); d.ellipse([cx - r, cyy - r, cx + r, cyy + r], outline=(240, 240, 240))
    d = ImageDraw.Draw(img)
    pal = [(255, 80, 80), (255, 220, 60), (80, 255, 120), (80, 220, 255), (255, 120, 255), (255, 160, 60)]
    for i, k in enumerate(ORDER):
        x, y, w, h = A[k]; c = pal[i % len(pal)]
        d.rectangle([x, y, x + w - 1, y + h - 1], outline=c, width=2)
        d.text((x + 4, y + 3), k, fill=c)
    for s in mf["starts"]:
        d.ellipse([s[0] - 5, s[1] - 5, s[0] + 5, s[1] + 5], outline=(255, 255, 255), width=2)
    img.save(path)


def main():
    here = os.path.dirname(os.path.abspath(__file__)); root = os.path.normpath(os.path.join(here, "..", ".."))
    ap = argparse.ArgumentParser()
    ap.add_argument("--width", type=int, default=800); ap.add_argument("--height", type=int, default=800)
    ap.add_argument("--out", default=os.path.join(root, "assets", "maps", "c1e1_map_a.json"))
    ap.add_argument("--doc", default=os.path.join(root, "docs", "C1E1_MAPS.md"))
    ap.add_argument("--no-doc", action="store_true"); ap.add_argument("--sheet")
    a = ap.parse_args()
    mf, A, fx, houses, centre = build(a.width, a.height)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f: f.write(dump(mf))
    if not a.no_doc:
        sec = doc_section(a.width, a.height, A, fx, round(380 * a.height / BASE))
        old = open(a.doc, encoding="utf-8").read() if os.path.exists(a.doc) else "# C1E1 지도 (MAP-A / MAP-B)\n\n"
        b, e = old.find("<!-- MAP-A:begin"), old.find("<!-- MAP-A:end -->")
        if b >= 0 and e >= 0: new = old[:b] + sec + old[e + len("<!-- MAP-A:end -->\n"):]
        else: new = old.rstrip("\n") + "\n\n" + sec
        with open(a.doc, "w", encoding="utf-8", newline="\n") as f: f.write(new)
    if a.sheet: sheet(a.sheet, mf, A, a.width, a.height)
    print("ok", a.out, len(mf["paint"]), "paint entries")


if __name__ == "__main__":
    sys.exit(main())
