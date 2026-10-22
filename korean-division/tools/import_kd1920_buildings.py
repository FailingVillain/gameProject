"""
game-tools make_building.py 결과 (buildings/<name>/) → assets/themes/kd1920/buildings/<name>/ (LodSprite 형식) + theme.json 패치. 다시 돌려도 같은 결과 (멱등).

    python tools/import_kd1920_buildings.py [--src <korean-division-image-out 경로>] [--only <class-id glob>] [--dst <kd1920 폴더>] [--standin]

- 민가 기본 42종 (T55m): assets/data/house_classes.json 의 종 × 구운 변형(a, b, ...) → 테마 키
  `building_art.house.<style>.<id>.<tone>`, 렌더 폴더 `buildings/<id>-<tone>/` (그림 없으면 [없음] 경고, 키 안 씀).
  폐허(ruin) 는 같은 시트의 두 번째 칸 (intact · ruined). `--only "ko-*"` 면 민가 종만 다시 가져온다 (유닛 · 기존 키 안 건드림).
  `--standin` = 시험 전용: 그림 자리에 기존 house-thatch/tile/brick/barn 4종을 돌려 쓴다. `--dst` 없이는 못 쓴다 (저장소 theme.json 보호).

- 상태 격자 시트(@128 은 2 행) 를 한 줄 시트 (열 = 상태) 로 다시 묶고 기본 + 팀(_team) 쌍을 쓴다. 방향 = ["s"] 한 줄.
- theme.json 에서 이 스크립트가 만든 키만 덮어쓴다 (`import_kd1920.py` 는 theme.json 을 새로 쓰므로 그 다음에 이 스크립트를 다시 돌린다).
- 키: building.city / metropolis / village (village-capture 상태 neutral · owned), building_art.house.<thatch|tile|brick|barn> (상태 intact · ruined).
- scale = LOD0(128 px/칸) 1px → 월드 px = 0.25 × (칸 수 / 그림 발자국 칸 수).
"""
import fnmatch
import json
import os
import sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CODING = os.path.dirname(ROOT)
while CODING and not os.path.isdir(os.path.join(CODING, "game-tools")) and os.path.dirname(CODING) != CODING:
    CODING = os.path.dirname(CODING)
SRC = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else os.path.join(CODING, "game-tools", "korean-division-image-out")
DST = os.path.abspath(sys.argv[sys.argv.index("--dst") + 1]) if "--dst" in sys.argv else os.path.join(ROOT, "assets", "themes", "kd1920")
ONLY = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
STANDIN = "--standin" in sys.argv
HOUSE_REG = os.path.join(ROOT, "assets", "data", "house_classes.json")
HOUSE_STANDIN = ["house-thatch", "house-tile", "house-brick", "house-barn"]   # 시험용 그림 자리 (--standin)

# (렌더 폴더, 쓸 상태)
ART = {"village-capture": ["neutral", "owned"],
       "house-thatch": ["intact", "ruined"], "house-tile": ["intact", "ruined"],
       "house-brick": ["intact", "ruined"], "house-barn": ["intact", "ruined"]}
# 테마 키 → (렌더 폴더, 점령지 칸 수 (한 변))
# 2026-10-19: 도시 그림을 실제 크기로 — 대도시 · 소도시 8칸, 마을 6칸 (8 × 3/4). 점령 · 선택 판정 (balance.json size 4 / 3) 은 그대로
ENTRIES = {"building.city": ("village-capture", 8), "building.metropolis": ("village-capture", 8), "building.village": ("village-capture", 6)}
HOUSE_SCALE_TILES = 1.0   # 집 그림의 발자국(2칸)을 지형 1칸에 맞춘다


def import_art(name, states, out_name=None):
    out_name = out_name or name
    src = os.path.join(SRC, "buildings", name)
    lay = json.load(open(os.path.join(src, f"{name}_layout.json"), encoding="utf-8"))
    out = os.path.join(DST, "buildings", out_name)
    os.makedirs(out, exist_ok=True)
    files = []
    for tag, lod in lay["lods"].items():
        fw, fh = lod["frame"]
        w = fw * len(states)
        res = {}
        for kind, fn in (("base", lod["sheet"]), ("team", lod["team"])):
            sheet = Image.open(os.path.join(src, fn)).convert("RGBA")
            strip = Image.new("RGBA", (w, fh), (0, 0, 0, 0))
            for i, st in enumerate(states):
                cx, cy = lod["cells"][st]
                strip.paste(sheet.crop((cx * fw, cy * fh, (cx + 1) * fw, (cy + 1) * fh)), (i * fw, 0))
            res[kind] = strip
        base = f"{out_name}{'' if tag == '@128' else tag}"
        res["base"].save(os.path.join(out, base + ".png"))
        res["team"].save(os.path.join(out, base + "_team.png"))
        layout = {
            "frame": [fw, fh], "anchor": lod["anchor"], "dirs": ["s"], "mirror": False, "columns": states,
            "sheets": [{"file": base + ".png", "team": base + "_team.png"}],
            "cells": {st: {"sheet": base + ".png", "col": i} for i, st in enumerate(states)},
        }
        lf = base + "_layout.json"
        json.dump(layout, open(os.path.join(out, lf), "w", encoding="utf-8"), indent=1)
        files.append(f"buildings/{out_name}/{lf}")
    return files, lay["footprint_tiles"][0]


def house_jobs():
    """(테마 키, 렌더 폴더, 출력 이름, 상태) — house_classes.json 의 종 × 변형."""
    reg = json.load(open(HOUSE_REG, encoding="utf-8"))
    jobs = []
    for c in reg["classes"]:
        if ONLY and not fnmatch.fnmatch(c["id"], ONLY):
            continue
        states = ["intact", "ruined"] if c.get("ruin") else ["intact"]
        for t in range(c["tones"]):
            tone = "abcdefgh"[t]
            src = HOUSE_STANDIN[len(jobs) % 4] if STANDIN else f"{c['id']}-{tone}"
            jobs.append((f"building_art.house.{c['style']}.{c['id']}.{tone}", src, f"{c['id']}-{tone}", states))
    return jobs


def main():
    if STANDIN and "--dst" not in sys.argv:
        sys.exit("--standin 은 --dst <시험 폴더> 와 함께만 쓴다")
    tp = os.path.join(DST, "theme.json")
    theme = json.load(open(tp, encoding="utf-8"))
    sp = theme["sprites"]
    for key, src, out_name, states in house_jobs():
        if not os.path.isdir(os.path.join(SRC, "buildings", src)):
            print("[없음]", key, src)   # 그림 없음 = 경고 (키를 쓰지 않는다)
            continue
        files, fp = import_art(src, states, out_name)
        sp[key] = {"layouts": files, "scale": round(0.25 * HOUSE_SCALE_TILES / fp, 5)}
        print("[민가]", key, len(files), "LOD")
    if ONLY:
        json.dump(theme, open(tp, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print("[theme] 패치:", tp)
        return
    layouts = {}
    for name, states in ART.items():
        if not os.path.isdir(os.path.join(SRC, "buildings", name)):
            print("[없음]", name)
            continue
        layouts[name] = import_art(name, states)
        print("[복사]", name, len(layouts[name][0]), "LOD")
    for key, (name, tiles) in ENTRIES.items():
        if name in layouts:
            files, fp = layouts[name]
            sp[key] = {"layouts": files, "scale": round(0.25 * tiles / fp, 5)}
    for h in ("thatch", "tile", "brick", "barn"):
        n = "house-" + h
        if n in layouts:
            files, fp = layouts[n]
            sp["building_art.house." + h] = {"layouts": files, "scale": round(0.25 * HOUSE_SCALE_TILES / fp, 5)}
    json.dump(theme, open(tp, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("[theme] 패치:", tp)


if __name__ == "__main__":
    main()
