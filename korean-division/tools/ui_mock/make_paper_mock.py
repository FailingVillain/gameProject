"""Paper-texture UI mockup (static). Output: docs/art_review/ui_paper_mock.png (1920x1080).
Run from repo root: python tools/ui_mock/make_paper_mock.py
Fonts (Windows): HANBatangB/HANBatang (myeongjo titles/body), Courier New bold (typewriter numbers)."""
import random, math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
random.seed(7); np.random.seed(7)
F = "C:/Windows/Fonts/"
def font(n, s): return ImageFont.truetype(F + n, s)
MJ = lambda s: font("HANBatangB.ttf", s)
MB = lambda s: font("HANBatang.ttf", s)
TY = lambda s: font("cour.ttf", s)
TYB = lambda s: font("courbd.ttf", s)
INK = (48, 38, 30); RED = (160, 36, 30); BLUE = (40, 52, 80)

def paper(w, h, base=(222, 205, 168), stains=6, grain=9):
    a = np.zeros((h, w, 3), np.float32) + base
    n = np.random.normal(0, grain, (h, w, 1))
    low = Image.fromarray(np.random.randint(0, 255, (h//40+2, w//40+2), np.uint8)).resize((w, h), Image.BICUBIC)
    low = (np.asarray(low, np.float32)[..., None] - 128) / 128 * 9
    a += n + low
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im, "RGBA")
    for _ in range(stains):
        x, y, r = random.randint(0, w), random.randint(0, h), random.randint(20, 70)
        d.ellipse([x-r, y-r, x+r, y+r], outline=(120, 80, 40, 40), width=3)
        d.ellipse([x-r+6, y-r+6, x+r-6, y+r-6], fill=(150, 110, 60, 14))
    im = im.filter(ImageFilter.GaussianBlur(0.6))
    yy, xx = np.mgrid[0:h, 0:w]
    e = np.minimum.reduce([xx, yy, w-1-xx, h-1-yy]) / 40.0
    k = 0.82 + 0.18 * np.clip(e, 0, 1)
    return Image.fromarray((np.asarray(im, np.float32) * k[..., None]).astype(np.uint8))

def shadow(img, box, r=14, off=(8, 10), a=130):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rectangle([box[0]+off[0], box[1]+off[1], box[2]+off[0], box[3]+off[1]], fill=(0, 0, 0, a))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(r)))

def put_paper(img, box, base=(226, 210, 174), rot=0, **kw):
    x0, y0, x1, y1 = box
    p = paper(x1-x0, y1-y0, base, **kw).convert("RGBA")
    if rot:
        p = p.rotate(rot, expand=True, resample=Image.BICUBIC)
        x0 -= (p.size[0]-(x1-x0))//2; y0 -= (p.size[1]-(y1-y0))//2
        box = (x0, y0, x0+p.size[0], y0+p.size[1])
    shadow(img, box)
    img.alpha_composite(p, (x0, y0))

def fold(img, box, vertical=True, pos=0.5):
    x0, y0, x1, y1 = box
    d = ImageDraw.Draw(img, "RGBA")
    if vertical:
        x = int(x0 + (x1-x0)*pos)
        d.line([x, y0, x, y1], fill=(90, 60, 30, 70), width=2); d.line([x+2, y0, x+2, y1], fill=(255, 250, 230, 60), width=2)
    else:
        y = int(y0 + (y1-y0)*pos)
        d.line([x0, y, x1, y], fill=(90, 60, 30, 70), width=2); d.line([x0, y+2, x1, y+2], fill=(255, 250, 230, 60), width=2)

def seal(img, cx, cy, r=46, text="임명"):
    L = Image.new("RGBA", img.size, (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    d.ellipse([cx-r, cy-r, cx+r, cy+r], outline=RED+(215,), width=5)
    d.ellipse([cx-r+9, cy-r+9, cx+r-9, cy+r-9], outline=RED+(180,), width=2)
    f = MJ(26)
    for i, ch in enumerate(text):
        d.text((cx-26+i*26, cy-14), ch, font=f, fill=RED+(225,))
    nz = (np.random.rand(img.size[1], img.size[0]) > 0.82)
    A = np.asarray(L.split()[3], np.float32) * (1 - nz * 0.7)
    L.putalpha(Image.fromarray(A.astype(np.uint8)))
    img.alpha_composite(L.rotate(-9, center=(cx, cy), resample=Image.BICUBIC))

def map_sketch(img, box):
    x0, y0, x1, y1 = box
    d = ImageDraw.Draw(img, "RGBA")
    for k in range(7):
        pts = []
        for t in range(0, 101):
            a = t/100*2*math.pi
            rr = 1 + 0.25*math.sin(3*a+k) + 0.12*math.sin(7*a+k*2)
            pts.append((x0+(x1-x0)*0.3 + math.cos(a)*(40+k*26)*rr*1.3, y0+(y1-y0)*0.62 + math.sin(a)*(26+k*17)*rr))
        d.line(pts, fill=(110, 90, 60, 70), width=1)
    riv = [(x0+30, y0+(y1-y0)*0.9)]
    for i in range(1, 40):
        riv.append((x0+30+i*(x1-x0-60)/39, y0+(y1-y0)*(0.86-0.16*i/39)+math.sin(i/2.3)*14))
    d.line(riv, fill=(70, 100, 130, 200), width=5)
    d.text((x0+40, y0+(y1-y0)*0.9+12), "압록강", font=MB(15), fill=BLUE)
    cities = [("안동", .12, .80), ("봉황성", .22, .66), ("안산", .40, .56), ("요양", .52, .47),
              ("봉천", .64, .38), ("장춘", .78, .26), ("하얼빈", .90, .14)]
    pts = [(x0+(x1-x0)*a, y0+(y1-y0)*b) for _, a, b in cities]
    d.line(pts, fill=INK+(190,), width=3)
    for (px, py), (px2, py2) in zip(pts, pts[1:]):
        n = int(math.hypot(px2-px, py2-py)//14)
        for j in range(n):
            t = j/n; cx, cy = px+(px2-px)*t, py+(py2-py)*t
            d.line([cx-4, cy-4, cx+4, cy+4], fill=INK+(150,), width=1)
    for i, ((nm, a, b), (px, py)) in enumerate(zip(cities, pts)):
        col = BLUE if i < 3 else RED if i == 3 else INK
        if i == 3:
            d.ellipse([px-20, py-20, px+20, py+20], outline=RED+(230,), width=3)
        d.rectangle([px-6, py-6, px+6, py+6], fill=col+(240,))
        d.text((px+12, py-30 if i % 2 else py+10), nm, font=MJ(19), fill=INK)
    cx, cy = x1-70, y0+80
    d.ellipse([cx-32, cy-32, cx+32, cy+32], outline=INK+(180,), width=2)
    d.polygon([(cx, cy-30), (cx-7, cy), (cx+7, cy)], fill=INK+(220,)); d.text((cx-6, cy-58), "N", font=TYB(20), fill=INK)
    d.text((x0+20, y1-34), "축척 1:2,000,000  ·  만주 전선", font=MB(15), fill=INK+(190,))

def tape(img, x, y, w=90, h=28, rot=-6):
    t = Image.new("RGBA", (w, h), (235, 225, 170, 150)).rotate(rot, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(t, (x, y))

W, H = 1920, 1080
wood = np.zeros((H, W, 3)) + (62, 44, 32) + np.random.normal(0, 5, (H, W, 1)) + np.sin(np.arange(H)[:, None, None]/3.2) * 4
img = Image.fromarray(np.clip(wood, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).convert("RGBA")

# main map sheet
MAPB = (80, 100, 1180, 770)
put_paper(img, MAPB, base=(228, 212, 176), stains=8)
fold(img, MAPB, True); fold(img, MAPB, False)
map_sketch(img, (MAPB[0]+20, MAPB[1]+60, MAPB[2]-20, MAPB[3]-10))
d = ImageDraw.Draw(img, "RGBA")
d.text((MAPB[0]+30, MAPB[1]+18), "작 전  지 도", font=MJ(34), fill=INK)
d.text((MAPB[0]+300, MAPB[1]+30), "대한제국 제17사단  ·  1920.10.21", font=MB(18), fill=INK+(200,))
tape(img, MAPB[0]-14, MAPB[1]-10); tape(img, MAPB[2]-70, MAPB[1]-8, rot=7)
d = ImageDraw.Draw(img, "RGBA")
d.rectangle([MAPB[0]+560, MAPB[1]+400, MAPB[0]+830, MAPB[1]+450], fill=(240, 228, 195, 235), outline=RED+(200,), width=2)
d.text((MAPB[0]+572, MAPB[1]+404), "다음 작전: 요양 도하", font=MJ(19), fill=RED)
d.text((MAPB[0]+572, MAPB[1]+427), "D-03   보급 비용 412", font=MB(16), fill=INK)

# folder with index tabs
FB = (1230, 80, 1820, 780)
shadow(img, FB, 16, (10, 12), 150)
tabs = ["사단 상태", "신병·부대 관리", "편제 관리", "병참", "외교", "참모진", "진급·군령권", "본국"]
cols = [(170, 134, 84), (176, 140, 88), (182, 146, 92), (188, 152, 98), (176, 138, 90), (182, 144, 94), (170, 136, 88), (176, 140, 92)]
th = 62; ty0 = FB[1] + 20
d = ImageDraw.Draw(img, "RGBA")
d.rectangle([FB[0]+60, FB[1], FB[2], FB[3]], fill=(186, 150, 96, 255), outline=(90, 62, 34, 255), width=2)
body = paper(FB[2]-FB[0]-84, FB[3]-FB[1]-40, (232, 217, 182), stains=5).convert("RGBA")
img.alpha_composite(body, (FB[0]+72, FB[1]+20))
bx, by = FB[0]+96, FB[1]+34
d = ImageDraw.Draw(img, "RGBA")
for i, t in enumerate(tabs):
    tx = FB[0] - 96 - (20 if i == 1 else 0)
    y = ty0 + i*(th+8)
    fill = (214, 184, 126, 255) if i == 1 else cols[i]+(255,)
    d.rounded_rectangle([tx-8, y, FB[0]+80, y+th], 10, fill=fill, outline=(90, 62, 34, 255), width=2)
    d.text((tx+8, y+20), f"{i+1:02d}", font=TYB(18), fill=INK+(190,))
    d.text((tx+46, y+19), t, font=MJ(20 if len(t) < 8 else 17), fill=INK)
d.text((bx, by), "신병 · 부대 관리", font=MJ(32), fill=INK)
d.line([bx, by+48, FB[2]-40, by+48], fill=INK+(160,), width=2)
rows = [("제1보병연대", 1840, 2100, "보충 중"), ("제2보병연대", 1795, 2100, ""),
        ("제3보병연대", 1912, 2100, ""), ("시위대 분대", 640, 700, "황손 직할"),
        ("산포연대", 520, 600, "편성 중"), ("공병대대", 310, 340, "")]
y = by + 66
d.text((bx, y), "부대", font=MB(16), fill=INK+(170,)); d.text((bx+210, y), "현원 / 정원", font=MB(16), fill=INK+(170,)); d.text((bx+400, y), "비고", font=MB(16), fill=INK+(170,))
y += 34
for nm, a, b, note in rows:
    d.text((bx, y), nm, font=MJ(21), fill=INK)
    d.text((bx+210, y+2), f"{a:>5,} / {b:,}", font=TY(20), fill=INK)
    bw = 150; d.rectangle([bx+210, y+30, bx+210+bw, y+36], outline=INK+(130,))
    d.rectangle([bx+210, y+30, bx+210+int(bw*a/b), y+36], fill=(110, 90, 60, 190))
    if note: d.text((bx+400, y+2), note, font=MB(17), fill=RED if "황" in note else BLUE)
    y += 58
y += 6
d.line([bx, y, FB[2]-40, y], fill=INK+(120,), width=1)
for k, (lab, val) in enumerate([("신병 대기", "0,318"), ("부상 회복", "0,047"), ("사기", "71 / 100"), ("피로", "34 / 100")]):
    cx = bx + (k % 2)*190; cy = y + 14 + (k//2)*44
    d.text((cx, cy), lab, font=MB(18), fill=INK+(200,)); d.text((cx+85, cy-2), val, font=TYB(22), fill=INK)
for k, lab in enumerate(["자동 편성", "보충"]):
    x = bx + 385; yy = y + 10 + k*46
    d.rounded_rectangle([x-4, yy-4, x+84, yy+30], 4, fill=(210, 190, 140, 255), outline=INK+(160,), width=2)
    d.text((x+10, yy), lab, font=MJ(18), fill=INK)

# telegram, note, appointment
put_paper(img, (110, 560, 470, 740), base=(236, 226, 196), rot=3, stains=3)
d = ImageDraw.Draw(img, "RGBA")
d.rectangle([128, 578, 452, 612], fill=(60, 50, 40, 230)); d.text((140, 581), "전  보", font=MJ(20), fill=(235, 225, 195)); d.text((250, 582), "TELEGRAM", font=TYB(18), fill=(235, 225, 195))
for i, ln in enumerate(["군단 사령부 발  ·  제17사단장 앞", "안산 방면 관동군 증강 미확인", "금일 1800까지 철도 수송 회신 요"]):
    d.text((130, 626+i*30), ln, font=MB(17), fill=BLUE)
d.text((130, 716), "No.0472   10.21  14:05", font=TY(15), fill=INK+(190,))

put_paper(img, (500, 590, 760, 750), base=(240, 230, 150), rot=-4, stains=1, grain=5)
d = ImageDraw.Draw(img, "RGBA")
d.text((518, 606), "참모장 쪽지", font=MJ(20), fill=RED)
for i, ln in enumerate(["부사단장께서 시위대 우선", "배치를 원하십니다.", "보급 잔량 확인 바람.  - 주현"]):
    d.text((516, 640+i*30), ln, font=MB(16), fill=BLUE)
tape(img, 600, 584, 70, 22, 4)

put_paper(img, (800, 560, 1150, 745), base=(238, 228, 200), rot=-2, stains=2)
d = ImageDraw.Draw(img, "RGBA")
d.rectangle([814, 574, 1136, 731], outline=INK+(150,), width=2)
d.text((860, 586), "임  명  장", font=MJ(30), fill=INK)
d.text((830, 636), "육군 참장 김 ○○", font=MB(18), fill=INK)
d.text((830, 664), "제17사단장에 임함.", font=MB(18), fill=INK)
d.text((830, 700), "광무 · 원수부", font=MB(15), fill=INK+(170,))
seal(img, 1060, 676, 44)

# bottom inset: battle HUD style strip (lighter texture)
IB = (80, 810, 1840, 1030)
shadow(img, IB, 10, (0, 8), 110)
img.alpha_composite(paper(IB[2]-IB[0], IB[3]-IB[1], (214, 204, 178), stains=2, grain=4).convert("RGBA"), (IB[0], IB[1]))
d = ImageDraw.Draw(img, "RGBA")
d.rectangle(IB, outline=(90, 70, 50, 200), width=3)
d.rectangle([IB[0], IB[1], IB[0]+420, IB[1]+30], fill=(70, 58, 44, 235))
d.text((IB[0]+12, IB[1]+4), "전투 HUD 시안 (야전 수첩 띠 · 질감 약하게)", font=MB(16), fill=(236, 226, 196))
nb = [IB[0]+20, IB[1]+46, IB[0]+700, IB[3]-18]
d.rectangle(nb, fill=(232, 224, 196, 235), outline=INK+(150,), width=2)
for k in range(5): d.ellipse([nb[0]+10, nb[1]+14+k*30, nb[0]+22, nb[1]+26+k*30], fill=(60, 50, 40, 200))
d.text((nb[0]+44, nb[1]+8), "야전 수첩  ·  제1연대 제2중대", font=MJ(21), fill=INK)
for i, (lab, v) in enumerate([("병력", "0,096 / 0,110"), ("탄약", "0,640"), ("사기", "68%")]):
    d.text((nb[0]+44, nb[1]+46+i*30), lab, font=MB(18), fill=INK+(200,)); d.text((nb[0]+160, nb[1]+44+i*30), v, font=TYB(21), fill=INK)
d.line([nb[0]+360, nb[1]+10, nb[0]+360, nb[3]-10], fill=INK+(90,), width=1)
for i, ln in enumerate(["· 북동 고지 적 집결 징후", "· 공병 교량 복구 중", "· 부상 후송 3명"]):
    d.text((nb[0]+380, nb[1]+14+i*34), ln, font=MB(18), fill=BLUE)
pc = [IB[0]+730, IB[1]+46, IB[0]+1010, IB[3]-18]
d.rectangle(pc, fill=(238, 230, 204, 240), outline=INK+(150,), width=2)
d.rectangle([pc[0]+14, pc[1]+14, pc[0]+94, pc[1]+114], fill=(150, 140, 120, 255), outline=INK+(180,), width=2)
d.ellipse([pc[0]+34, pc[1]+26, pc[0]+74, pc[1]+66], fill=(110, 100, 86, 255)); d.pieslice([pc[0]+18, pc[1]+62, pc[0]+90, pc[1]+140], 180, 360, fill=(110, 100, 86, 255))
d.text((pc[0]+108, pc[1]+14), "김 참장", font=MJ(24), fill=INK); d.text((pc[0]+108, pc[1]+46), "제17사단장", font=MB(16), fill=INK+(190,))
d.text((pc[0]+108, pc[1]+72), "지휘 반경 40", font=MB(15), fill=INK+(190,)); d.text((pc[0]+108, pc[1]+94), "독일학파", font=MB(15), fill=BLUE)
tb = [IB[0]+1040, IB[1]+46, IB[2]-20, IB[3]-18]
for i, (t, c) in enumerate([("14:02  본대 아군 보급대 도착 예정", BLUE), ("14:05  적 기병 북동 방향 접근", RED), ("14:07  제3중대 보고 대기", INK)]):
    yy = tb[1] + i*50
    d.rectangle([tb[0], yy, tb[2], yy+40], fill=(232, 224, 190, 235), outline=INK+(130,), width=1)
    d.rectangle([tb[0], yy, tb[0]+8, yy+40], fill=c+(230,))
    d.text((tb[0]+20, yy+9), t, font=MB(17), fill=INK)

d.text((84, 34), "사령관 책상", font=MJ(40), fill=(236, 220, 180))
d.text((330, 52), "대한제국 제17사단  ·  1920년 10월  ·  캠페인 홈 시안", font=MB(18), fill=(206, 190, 150))
a = np.asarray(img.convert("RGB"), np.float32); yy, xx = np.mgrid[0:H, 0:W]
v = 1 - 0.28*(((xx-W/2)/(W/2))**2 + ((yy-H/2)/(H/2))**2)/2
os.makedirs("docs/art_review", exist_ok=True)
Image.fromarray(np.clip(a*v[..., None], 0, 255).astype(np.uint8)).save("docs/art_review/ui_paper_mock.png")
