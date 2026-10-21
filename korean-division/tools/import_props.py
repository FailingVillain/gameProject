"""game-tools props 렌더 (korean-division-image-out/props/<name>/) -> assets/themes/kd1920/props/<name>/ 복사 (F6 엄폐 소품, v1 그림 그대로).

    python tools/import_props.py [--src <korean-division-image-out 경로>]
<name>.png / @64 / @32 + <name>_shadow*.png + <name>_layout.json 만 복사 (contact · gif 제외). 다시 돌려도 안전. 게임 로더 = src/Render/PropArt.cs.
"""
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CODING = os.path.dirname(ROOT)
while CODING and not os.path.isdir(os.path.join(CODING, "game-tools")) and os.path.dirname(CODING) != CODING:
    CODING = os.path.dirname(CODING)
SRC = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else os.path.join(CODING, "game-tools", "korean-division-image-out")
DST = os.path.join(ROOT, "assets", "themes", "kd1920", "props")
src_props = os.path.join(SRC, "props")
if os.path.isdir(DST):
    shutil.rmtree(DST)
n = 0
for name in sorted(os.listdir(src_props)):
    d = os.path.join(src_props, name)
    if not os.path.isdir(d):
        continue
    out = os.path.join(DST, name)
    os.makedirs(out, exist_ok=True)
    for fn in sorted(os.listdir(d)):
        if fn.endswith(".png") or fn.endswith("_layout.json"):
            shutil.copy2(os.path.join(d, fn), os.path.join(out, fn))
            n += 1
print(f"[props] {n} files -> {DST}")
