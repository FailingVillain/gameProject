"""
game-tools 세력 휘장 아이콘 (badges_icons/badge-<세력>_<크기>.png) → assets/themes/kd1920/icons/ + theme.json 키 icon.badge.<세력>@<크기>.
    python tools/import_kd1920_badges.py [--src <badges_icons 경로>]
세력 = korean | japanese | fengtian | russian | soviet, 크기 = 16 24 32 48 64 (_team 변형은 가져오지 않음 — 게임은 일반 아이콘 + 세력 색 테두리).
다시 돌려도 안전 (폴더 재작성, theme.json 의 해당 키만 갱신).
"""
import json, os, shutil, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CODING = os.path.dirname(ROOT)
while CODING and not os.path.isdir(os.path.join(CODING, "game-tools")) and os.path.dirname(CODING) != CODING:
    CODING = os.path.dirname(CODING)
SRC = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else os.path.join(CODING, "game-tools", "korean-division-image-out", "badges_icons")
DST = os.path.join(ROOT, "assets", "themes", "kd1920")
FACTIONS = ("korean", "japanese", "fengtian", "russian", "soviet")
SIZES = (16, 24, 32, 48, 64)
dst_icons = os.path.join(DST, "icons")
if os.path.isdir(dst_icons):
    shutil.rmtree(dst_icons)
os.makedirs(dst_icons)
tp = os.path.join(DST, "theme.json")
theme = json.load(open(tp, encoding="utf-8"))
sp = theme.setdefault("sprites", {})
for k in [k for k in sp if k.startswith("icon.badge.")]:
    del sp[k]
n = 0
for f in FACTIONS:
    for s in SIZES:
        fn = f"badge-{f}_{s}.png"
        p = os.path.join(SRC, fn)
        if not os.path.isfile(p):
            print(f"[경고] {fn} 없음")
            continue
        shutil.copy2(p, os.path.join(dst_icons, fn))
        sp[f"icon.badge.{f}@{s}"] = {"base": f"icons/{fn}", "size": [s, s]}
        n += 1
json.dump(theme, open(tp, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print(f"[badges] {n} 아이콘 → {dst_icons}")
