#!/usr/bin/env python3
"""C1E1 MAP-B (Andong area) map generator.

Usage:  python tools/c1e1/gen_map_b.py [W] [H] [--out PATH] [--areas] [--check] [--sheet --out X.png]
  W H      map size in tiles (default 1200 1200 = Config.MapTiers "large")
  --out    output map JSON (default assets/maps/c1e1_map_b.json)
  --areas  print the area table as markdown (for docs/C1E1_MAPS.md); writes nothing
  --check  run the annex-separation test and exit non-zero on failure; writes nothing
Deterministic: fixed RNG seed, same args -> byte-identical file.
Layout is defined in fractions of W/H so any size keeps the same shape.
Source: campaign/spec_c1e1.txt 'MAP-B' terrain draft + R6 (annex rules). North = y small, river = east.
Area names follow docs/C1E1_IMPLEMENTATION_PLAN.md section 3.
"""
import json, math, random, sys, os

argv = sys.argv[1:]
out_path = None
if "--out" in argv:
    k = argv.index("--out")
    out_path = argv[k + 1]
    del argv[k:k + 2]
args = [a for a in argv if not a.startswith("--")]
flags = [a for a in argv if a.startswith("--")]
W = int(args[0]) if len(args) > 0 else 1200
H = int(args[1]) if len(args) > 1 else W
rng = random.Random(19201022)


def fx(v): return int(round(v * W))
def fy(v): return int(round(v * H))


def R(x0, y0, x1, y1):
    """fractional rect (x0,y0)-(x1,y1) -> [x, y, w, h] in tiles"""
    X0, Y0, X1, Y1 = fx(x0), fy(y0), fx(x1), fy(y1)
    return [X0, Y0, max(1, X1 - X0), max(1, Y1 - Y0)]


paint = []


def P(terrain, rect=None, circle=None, high=None):
    d = {"terrain": terrain}
    if rect is not None: d["rect"] = [int(v) for v in rect]
    if circle is not None: d["circle"] = [int(v) for v in circle]
    if high is not None: d["high"] = high
    paint.append(d)


# ---- areas (named zones; rect = [x, y, w, h]) ----
A = {}
A["b_city"] = R(0.44, 0.40, 0.76, 0.68)           # urban district, enemy 800
A["b_annex"] = R(0.66, 0.22, 0.84, 0.38)          # Japanese railway annex (no-fire / no-enter), grid streets
A["b_outskirts"] = R(0.26, 0.32, 0.62, 0.76)      # outer defence zone (trench N + W), enemy 500 (bounding box of the L)
A["b_dock"] = R(0.76, 0.46, 0.88, 0.60)           # wharf at the river
A["b_jingang_hill"] = R(0.12, 0.14, 0.26, 0.28)   # isolated NW hill, 4-gun battery
A["b_road_block"] = R(0.25, 0.30, 0.32, 0.38)     # road to Fenghuangcheng crossing the corner of the trench line
A["b_north_plain"] = R(0.28, 0.02, 0.64, 0.19)    # open flat ground (cavalry)
A["b_north_village"] = R(0.68, 0.04, 0.80, 0.14)  # village (no-looting test)
A["b_hq"] = R(0.55, 0.50, 0.63, 0.57)             # enemy command post inside the city
A["b_flank_path"] = R(0.05, 0.10, 0.27, 0.80)     # bounding box of the west/north detour track
A["b_sw_start"] = R(0.08, 0.80, 0.22, 0.94)       # player start, south-west of the outskirts
ANNEX_GAP = 8  # minimum tile gap between annex and city (separate connected region)

base = "grass"


# ---- river (east edge, meandering) ----
def river_edge(y):
    return fx(0.90) + int(round(fx(0.025) * math.sin(y / max(1, H) * 2 * math.pi * 1.5 + 0.7)))


dock_y0, dock_y1 = A["b_dock"][1], A["b_dock"][1] + A["b_dock"][3]
flat_edge = river_edge((dock_y0 + dock_y1) // 2)


def edge(y):
    # flatten the bank in front of the wharf so the dock reads as a straight quay
    if dock_y0 - 20 <= y <= dock_y1 + 20: return flat_edge
    return river_edge(y)


run_start, run_x = 0, edge(0)
for y in range(1, H + 1):
    e = edge(y) if y < H else None
    if e != run_x:
        P("water", [run_x, run_start, W - run_x, y - run_start])
        run_start, run_x = y, e
for y in range(0, H, 4):
    e = edge(y)
    P("sand", [e - 3, y, 3, 4])  # sand bank

# ---- farmland / woods in the open country ----
for _ in range(14):
    x0, y0 = rng.uniform(0.02, 0.80), rng.uniform(0.02, 0.97)
    P("dirt", R(x0, y0, x0 + rng.uniform(0.02, 0.05), y0 + rng.uniform(0.015, 0.035)))
for _ in range(12):
    cx, cy, r = fx(rng.uniform(0.04, 0.82)), fy(rng.uniform(0.04, 0.96)), fx(rng.uniform(0.008, 0.02))
    P("forest", circle=[cx, cy, r])
P("grass", A["b_north_plain"])  # the plain stays obstacle-free (repaint after the scatter)

# ---- main road from Fenghuangcheng (west edge) to the city ----
rb = A["b_road_block"]
road_y = fy(0.335)
P("dirt", [0, road_y - 2, rb[0] + rb[2] // 2, 5])
cx_i, cy_i = rb[0] + rb[2] // 2, road_y
tx, ty = A["b_city"][0] + 4, A["b_city"][1] + A["b_city"][3] // 2
steps = max(abs(tx - cx_i), abs(ty - cy_i))
for i in range(steps + 1):
    P("dirt", [cx_i + (tx - cx_i) * i // steps - 2, cy_i + (ty - cy_i) * i // steps - 2, 5, 5])
P("dirt", [fx(0.45) - 2, fy(0.19), 5, fy(0.40) - fy(0.19)])  # road north to the plain

# ---- jingang hill: independent hill, high ground, rock outcrops, 4 gun pits ----
jh = A["b_jingang_hill"]
hx, hy, hr = jh[0] + jh[2] // 2, jh[1] + jh[3] // 2, jh[2] // 2
P("grass", circle=[hx, hy, hr], high=True)
P("rock", circle=[hx - hr // 4, hy + hr // 5, max(2, hr // 5)])
P("rock", circle=[hx + hr // 3, hy - hr // 4, max(2, hr // 6)])
for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
    px, py = hx + dx * max(4, hr // 4), hy + dy * max(4, hr // 4)
    P("trench", [px - 2, py - 1, 5, 3])  # gun pit
P("trench", circle=[hx, hy, max(3, hr // 8)])  # observation post

# ---- outskirts: north trench (y ~ 0.345H) and west trench (x ~ 0.285W), zig-zag + wire ----
oy, ox = fy(0.345), fx(0.285)
for i, x in enumerate(range(ox, fx(0.60), 12)):
    j = (i % 2) * 3
    P("wire", [x, oy - 9 - j, 12, 2])
    P("trench", [x, oy - j, 12, 2])
for i, y in enumerate(range(oy, fy(0.72), 12)):
    j = (i % 2) * 3
    P("wire", [ox - 9 - j, y, 2, 12])
    P("trench", [ox - j, y, 2, 12])
# road gap through the trench line = the checkpoint
P("dirt", [rb[0], road_y - 3, rb[2], 7])
P("rock", [rb[0] + rb[2] // 2 - 2, road_y - 6, 4, 3])
P("rock", [rb[0] + rb[2] // 2 - 2, road_y + 4, 4, 3])

# ---- city: irregular alleys, merchant row, timber yard ----
cx0, cy0, cw, ch = A["b_city"]
P("dirt", A["b_city"])
y = cy0 + 2
while y < cy0 + ch - 8:
    rowh = rng.randint(7, 15)
    x = cx0 + 2 + rng.randint(0, 3)
    while x < cx0 + cw - 8:
        bw = min(rng.randint(6, 16), cx0 + cw - 2 - x)
        if bw >= 4:
            P("house", [x, y + rng.randint(0, 2), bw, max(4, rowh - rng.randint(1, 3))])
        x += bw + rng.randint(2, 4)
    y += rowh + rng.randint(2, 4)
P("dirt", [cx0, cy0 + ch // 2 - 2, cw, 5])          # main street (merchant row), west gate to dock
P("dirt", [cx0 + cw // 2 - 2, cy0, 5, ch])          # cross street
P("rubble", [cx0 + fx(0.03), cy0 + ch - fy(0.05), fx(0.07), fy(0.04)])  # timber yard
hq = A["b_hq"]
P("dirt", hq)
P("house", [hq[0] + hq[2] // 2 - 4, hq[1] + hq[3] // 2 - 3, 8, 6])

# ---- dock ----
dk = A["b_dock"]
P("sand", [dk[0], dk[1], flat_edge - dk[0], dk[3]])
for yy in range(dk[1] + 4, dk[1] + dk[3] - 2, 12):
    P("sand", [flat_edge - 2, yy, 8, 3])             # piers
for yy in range(dk[1] + 2, dk[1] + dk[3] - 4, 14):
    P("house", [dk[0] + 3, yy, 7, 5])                # warehouses
city_r = A["b_city"][0] + A["b_city"][2]
P("dirt", [city_r, dk[1] + dk[3] // 2 - 2, dk[0] - city_r + 4, 5])

# ---- annex: grid streets, regular blocks, marked boundary (no railway, R9 substitute) ----
ax, ay, aw, ah = A["b_annex"]
P("grass", A["b_annex"])
P("dirt", [ax - 1, ay - 1, aw + 2, 2]); P("dirt", [ax - 1, ay + ah - 1, aw + 2, 2])
P("dirt", [ax - 1, ay - 1, 2, ah + 2]); P("dirt", [ax + aw - 1, ay - 1, 2, ah + 2])  # boundary line
step = max(14, aw // 6)
for gx in range(ax + step, ax + aw - 4, step):
    P("dirt", [gx, ay, 3, ah])
for gy in range(ay + step // 2 * 2, ay + ah - 4, step):
    P("dirt", [ax, gy, aw, 3])
for gx in range(ax + 3, ax + aw - 4 - step, step):
    for gy in range(ay + 3, ay + ah - 4 - step // 2, step):
        P("house", [gx + 2, gy + 2, step - 8, step - 8])
P("dirt", [ax + 4, ay + ah // 2 - 6, step - 2, 12])  # station yard, plain dirt
# the belt between city and annex is left unpainted (grass)

# ---- north village (SK3 no-looting) ----
vx, vy, vw, vh = A["b_north_village"]
P("dirt", A["b_north_village"])
for k in range(12):
    P("house", [vx + 3 + (k % 4) * (vw // 4), vy + 3 + (k // 4) * (vh // 3), max(4, vw // 4 - 4), max(3, vh // 3 - 4)])

# ---- flank path (west edge north, then east toward the plain) ----
P("dirt", [fx(0.055), fy(0.12), 4, fy(0.80) - fy(0.12)])
P("dirt", [fx(0.055), fy(0.12), fx(0.27) - fx(0.055), 4])

# ---- player start area ----
s = A["b_sw_start"]
P("grass", s)
P("dirt", [s[0] + s[2] // 2 - 2, s[1] - fy(0.05), 5, fy(0.05) + 2])
P("dirt", [s[0] + s[2] // 2 - 2, s[1] - fy(0.05), fx(0.14), 5])  # track NE toward the outskirts

starts = [[s[0] + s[2] // 2, s[1] + s[3] // 2], [hq[0] + hq[2] // 2, hq[1] + 8]]


def raster():
    code = {"grass": 0, "dirt": 1, "sand": 2, "water": 3, "rock": 4, "forest": 5, "rubble": 6, "house": 7, "wire": 8, "trench": 9}
    ter = [[0] * W for _ in range(H)]
    for p in paint:
        t = code[p["terrain"]]
        if "rect" in p:
            x, y, w, h = p["rect"]
            for yy in range(max(0, y), min(H, y + h)):
                row = ter[yy]
                for xx in range(max(0, x), min(W, x + w)): row[xx] = t
        if "circle" in p:
            cx, cy, r = p["circle"]
            for yy in range(max(0, cy - r), min(H, cy + r + 1)):
                for xx in range(max(0, cx - r), min(W, cx + r + 1)):
                    if (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r: ter[yy][xx] = t
    return ter


def check():
    ok = True
    cx0_, cy0_, cw_, ch_ = A["b_city"]
    ax_, ay_, aw_, ah_ = A["b_annex"]
    gap = max(ax_ - (cx0_ + cw_), cx0_ - (ax_ + aw_), ay_ - (cy0_ + ch_), cy0_ - (ay_ + ah_), 0)
    print(f"annex-city gap = {gap} tiles (need >= {ANNEX_GAP})")
    ok &= gap >= ANNEX_GAP
    ter = raster()
    free = lambda x, y: ter[y][x] not in (3, 7, 4)
    open_t = [(x, y) for y in range(ay_, ay_ + ah_) for x in range(ax_, ax_ + aw_) if free(x, y)]
    seen, stack = set(), [open_t[0]]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen: continue
        seen.add((x, y))
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if ax_ <= nx < ax_ + aw_ and ay_ <= ny < ay_ + ah_ and free(nx, ny) and (nx, ny) not in seen: stack.append((nx, ny))
    print(f"annex open tiles {len(open_t)}, one connected region {len(seen)}")
    ok &= len(seen) == len(open_t)
    water_in = sum(1 for y in range(ay_, ay_ + ah_) for x in range(ax_, ax_ + aw_) if ter[y][x] == 3)
    print(f"water tiles in annex {water_in}"); ok &= water_in == 0
    # the annex must not overlap any other named area
    for k, v in A.items():
        if k == "b_annex": continue
        ox_ = min(ax_ + aw_, v[0] + v[2]) - max(ax_, v[0]); oy_ = min(ay_ + ah_, v[1] + v[3]) - max(ay_, v[1])
        if ox_ > 0 and oy_ > 0:
            print(f"annex overlaps {k}"); ok = False
    for sx, sy in starts: ok &= ter[sy][sx] != 3
    # player start must reach the enemy HQ and the annex over walkable land (water/rock/house/wire block), 4-connected
    walk = lambda x, y: ter[y][x] not in (3, 4, 7, 8)
    seen2, st = set(), [tuple(starts[0])]
    while st:
        x, y = st.pop()
        if (x, y) in seen2: continue
        seen2.add((x, y))
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < W and 0 <= ny < H and walk(nx, ny) and (nx, ny) not in seen2: st.append((nx, ny))
    reach_hq = tuple(starts[1]) in seen2
    reach_annex = (ax_ + 1, ay_ + 1) in seen2
    print(f"start reaches hq: {reach_hq}, annex: {reach_annex}")
    ok &= reach_hq and reach_annex
    print("CHECK", "PASS" if ok else "FAIL")
    return ok


desc = {
    "b_city": "시가지 (성내 800). 불규칙 골목 · 화상가 · 목재 야적장(rubble) · 십자로",
    "b_annex": "일본 철도 부속지 (발포 금지 · 진입 금지). 격자 도로, 철로 없음. 경계선 = 둘레 흙길 2칸",
    "b_outskirts": "외곽 참호 500 구역의 경계 상자. 북 참호 + 서 참호 (L자, 철조망 포함)",
    "b_dock": "부두. 강변 모래 + 잔교 + 창고. 부두 봉쇄 지점",
    "b_jingang_hill": "진강산. 시가 서북 독립 구릉(high), 야포 갱 4 + 관측호",
    "b_road_block": "봉황성 방면 도로가 북·서 참호 모서리를 지나는 도로 차단 지점",
    "b_north_plain": "북부 평야 (SK4 기병전). 장애물 없는 풀밭",
    "b_north_village": "북부 마을 (SK3). 가옥 12",
    "b_hq": "지휘소 (시가지 중앙 광장)",
    "b_flank_path": "SK1 우회로 상자. 서쪽 가장자리 북상 흙길 + 북부 평야 쪽으로 동진",
    "b_sw_start": "플레이어 시작 (외곽 남서)",
}


def areas_md():
    lines = ["| 구역 id | x | y | w | h | 설명 |", "|---|---|---|---|---|---|"]
    for k, v in A.items():
        lines.append(f"| {k} | {v[0]} | {v[1]} | {v[2]} | {v[3]} | {desc[k]} |")
    return "\n".join(lines)


def sheet(path):
    from PIL import Image, ImageDraw
    col = [(70,116,60),(126,102,70),(194,178,126),(42,84,138),(92,90,98),(38,78,40),(118,110,100),(150,92,70),(96,104,76),(98,78,52)]
    ter = raster()
    img = Image.new("RGB", (W, H))
    img.putdata([col[t] for row in ter for t in row])
    for p in paint:  # high ground tint
        if p.get("high"):
            cx, cy, r = p["circle"]
            d = ImageDraw.Draw(img, "RGBA"); d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=(255,255,255,40))
    d = ImageDraw.Draw(img)
    for k, (x, y, w, h) in A.items():
        d.rectangle([x, y, x+w, y+h], outline=(255,240,60) if k != "b_annex" else (255,60,60), width=max(1, W//600))
        d.text((x+3, y+3), k, fill=(255,255,255))
    img.resize((min(W, 1200), min(H, 1200))).save(path)
    # zone sheet: crops of each area at native resolution (max 300px wide) in a grid
    tiles = []
    for k, (x, y, w, h) in A.items():
        pad = 12
        c = img.crop((max(0,x-pad), max(0,y-pad), min(W,x+w+pad), min(H,y+h+pad)))
        sc = min(3.0, 300 / c.width, 220 / c.height)
        c = c.resize((max(1,int(c.width*sc)), max(1,int(c.height*sc))), Image.NEAREST)
        t = Image.new("RGB", (310, 240), (20,20,20)); t.paste(c, (5, 18)); ImageDraw.Draw(t).text((5, 3), k, fill=(255,255,255)); tiles.append(t)
    cols = 4; rows = (len(tiles)+cols-1)//cols
    sh = Image.new("RGB", (cols*310, rows*240), (20,20,20))
    for i, t in enumerate(tiles): sh.paste(t, ((i%cols)*310, (i//cols)*240))
    sh.save(path.replace(".png", "_zones.png"))
    print("sheet", path)


if "--sheet" in flags:
    sheet(out_path or "c1e1_map_b_overview.png")
elif "--areas" in flags:
    print(f"크기 {W}x{H}"); print(areas_md())
elif "--check" in flags:
    sys.exit(0 if check() else 1)
else:
    here = os.path.dirname(os.path.abspath(__file__))
    out = out_path or os.path.normpath(os.path.join(here, "..", "..", "assets", "maps", "c1e1_map_b.json"))
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("{\n")
        f.write(' "name": "C1E1 MAP-B 안동 일대", "width": %d, "height": %d, "base": "%s",\n' % (W, H, base))
        f.write(' "starts": %s,\n "paint": [\n' % json.dumps(starts))
        f.write(",\n".join("  " + json.dumps(p, ensure_ascii=False, separators=(",", ":")) for p in paint))
        f.write("\n ]\n}\n")
    print(f"wrote {out} ({W}x{H}, {len(paint)} paints)")
