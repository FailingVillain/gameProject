"""
game-tools/image → 게임 테마로 내보내기.  python export.py [--game korean-division|wargame]
out/<unit>/ 의 시트 · 초상화를 coding/<game>/assets/themes/ww2/ 아래로 복사하고 theme.json 의 해당 항목을 갱신한다.
"""
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "tools"))
from common import ANCHOR, DIRS, FH, FRAMES, FW, OUT  # noqa: E402

CODING = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # C:/gameProject/game-tools/image -> coding
GAME = sys.argv[sys.argv.index("--game") + 1] if "--game" in sys.argv else "korean-division"
GAME_THEME = os.path.join(CODING, GAME, "assets", "themes", "ww2")
UNITS = ["recruit", "infantry", "veteran", "scout", "worker", "at_infantry"]
PORTRAIT_FALLBACK = {"recruit": "infantry", "veteran": "infantry"}   # tiers share the infantry portrait until they get their own


def main():
    # NOTE: the sheets in out/ are now FW x FH = 128x160 (common.py v0.5) while the game loader still expects 64x80.
    # The theme entry below is written with the current FW/FH/ANCHOR, so do not run this against the game until its
    # loader is updated (planned separately). make_unit.py also writes @64 / @32 LOD sheets for that transition.
    print(f"[주의] 시트 규격 {FW}x{FH}, 기준점 {ANCHOR} 로 내보냅니다 — 게임 로더가 64x80 을 기대하면 먼저 게임 쪽을 갱신하세요.")
    theme_path = os.path.join(GAME_THEME, "theme.json")
    with open(theme_path, encoding="utf-8") as f:
        theme = json.load(f)
    sprites = theme.setdefault("sprites", {})
    for unit in UNITS:
        src = os.path.join(OUT, unit)
        if not os.path.isfile(os.path.join(src, f"{unit}.png")):
            print(f"[건너뜀] {unit}: 시트 없음")
            continue
        dst = os.path.join(GAME_THEME, "units", "hd")
        os.makedirs(dst, exist_ok=True)
        for fn in (f"{unit}.png", f"{unit}_team.png", f"portrait_{unit}.png", f"portrait_{unit}_team.png", f"{unit}_anchors.json"):
            if os.path.isfile(os.path.join(src, fn)):
                shutil.copy2(os.path.join(src, fn), os.path.join(dst, fn))
        psrc = unit
        if not os.path.isfile(os.path.join(src, f"portrait_{unit}.png")) and unit in PORTRAIT_FALLBACK:
            psrc = PORTRAIT_FALLBACK[unit]
            for suf in ("", "_team"):
                fp = os.path.join(OUT, psrc, f"portrait_{psrc}{suf}.png")
                if os.path.isfile(fp):
                    shutil.copy2(fp, os.path.join(dst, f"portrait_{unit}{suf}.png"))
        entry = {
            "sheet": f"units/hd/{unit}.png",
            "teamSheet": f"units/hd/{unit}_team.png",
            "frame": [FW, FH],
            "dirs": DIRS,
            "mirror": True,
            "frameNames": FRAMES,
            "anims": {"idle": [0], "walk": [1, 2, 3, 4], "shoot": [5, 6], "shootVariants": [[5, 6], [7, 8], [9, 10]],
                      "idle_alt": [11], "death": [12, 13, 14]},
            "anchor": list(ANCHOR),
            "anchors": f"units/hd/{unit}_anchors.json",
            "scale": 0.5,
        }
        if os.path.isfile(os.path.join(OUT, psrc, f"portrait_{psrc}.png")):
            entry["portrait"] = f"units/hd/portrait_{unit}.png"
            if os.path.isfile(os.path.join(OUT, psrc, f"portrait_{psrc}_team.png")):
                entry["portraitTeam"] = f"units/hd/portrait_{unit}_team.png"
        sprites[f"unit.{unit}"] = entry
        print(f"[내보냄] {unit}")
    export_tank(theme, sprites)
    export_artillery(theme, sprites)
    export_towed(theme, sprites)
    export_fx(theme, sprites)
    with open(theme_path, "w", encoding="utf-8") as f:
        json.dump(theme, f, ensure_ascii=False, indent=2)


def export_tank(theme, sprites):
    src = os.path.join(OUT, "tank")
    if not os.path.isfile(os.path.join(src, "tank.png")):
        print("[건너뜀] tank: 시트 없음")
        return
    dst = os.path.join(GAME_THEME, "units", "hd")
    os.makedirs(dst, exist_ok=True)
    for fn in ("tank.png", "tank_team.png", "tank_turret.png", "tank_turret_team.png", "tank_anchors.json",
               "portrait_tank.png", "portrait_tank_team.png"):
        if os.path.isfile(os.path.join(src, fn)):
            shutil.copy2(os.path.join(src, fn), os.path.join(dst, fn))
    with open(os.path.join(src, "tank_anchors.json"), encoding="utf-8") as f:
        rows = json.load(f)["rows"]
    common = {"frame": [112, 112], "dirs": rows, "mirror": True, "anchors": "units/hd/tank_anchors.json", "scale": 0.5}
    tank = dict(common, sheet="units/hd/tank.png", teamSheet="units/hd/tank_team.png",
                anims={"idle": [0], "walk": [1, 2], "dead": [3]}, anchor=[56, 66])
    if os.path.isfile(os.path.join(src, "portrait_tank.png")):
        tank["portrait"] = "units/hd/portrait_tank.png"
        if os.path.isfile(os.path.join(src, "portrait_tank_team.png")):
            tank["portraitTeam"] = "units/hd/portrait_tank_team.png"
    sprites["unit.tank"] = tank
    sprites["unit.tank.turret"] = dict(common, sheet="units/hd/tank_turret.png", teamSheet="units/hd/tank_turret_team.png",
                                       anims={"idle": [0], "shoot": [1], "dead": [2]}, anchor=[56, 47])
    print("[내보냄] tank (+turret)")


def export_artillery(theme, sprites):
    src = os.path.join(OUT, "artillery")
    if not os.path.isfile(os.path.join(src, "artillery.png")):
        print("[skip] artillery: no sheet")
        return
    dst = os.path.join(GAME_THEME, "units", "hd")
    os.makedirs(dst, exist_ok=True)
    for fn in ("artillery.png", "artillery_team.png", "artillery_anchors.json", "portrait_artillery.png", "portrait_artillery_team.png"):
        if os.path.isfile(os.path.join(src, fn)):
            shutil.copy2(os.path.join(src, fn), os.path.join(dst, fn))
    with open(os.path.join(src, "artillery_anchors.json"), encoding="utf-8") as f:
        meta = json.load(f)
    e = {"sheet": "units/hd/artillery.png", "teamSheet": "units/hd/artillery_team.png", "frame": [112, 112],
         "dirs": meta["rows"], "mirror": True, "anchors": "units/hd/artillery_anchors.json", "scale": 0.5,
         "anchor": [int(meta["ground"][0]), int(meta["ground"][1])],
         "anims": {"idle": [0], "walk": [1, 2], "shoot": [3], "dead": [4]}}
    if os.path.isfile(os.path.join(src, "portrait_artillery.png")):
        e["portrait"] = "units/hd/portrait_artillery.png"
        e["portraitTeam"] = "units/hd/portrait_artillery_team.png"
    sprites["unit.artillery"] = e
    print("[export] artillery")


def export_towed(theme, sprites):
    src = os.path.join(OUT, "towed_artillery")
    if not os.path.isfile(os.path.join(src, "towed_artillery.png")):
        print("[skip] towed_artillery: no sheet")
        return
    dst = os.path.join(GAME_THEME, "units", "hd")
    os.makedirs(dst, exist_ok=True)
    for fn in ("towed_artillery.png", "towed_artillery_team.png", "towed_artillery_anchors.json",
               "portrait_towed_artillery.png", "portrait_towed_artillery_team.png"):
        if os.path.isfile(os.path.join(src, fn)):
            shutil.copy2(os.path.join(src, fn), os.path.join(dst, fn))
    with open(os.path.join(src, "towed_artillery_anchors.json"), encoding="utf-8") as f:
        meta = json.load(f)
    e = {"sheet": "units/hd/towed_artillery.png", "teamSheet": "units/hd/towed_artillery_team.png", "frame": [112, 112],
         "dirs": meta["rows"], "mirror": True, "anchors": "units/hd/towed_artillery_anchors.json", "scale": 0.5,
         "anchor": [int(meta["ground"][0]), int(meta["ground"][1])],
         "anims": {"travel": [0, 1], "idle": [2], "shoot": [3], "dead": [4], "deploy": [5]}}
    if os.path.isfile(os.path.join(src, "portrait_towed_artillery.png")):
        e["portrait"] = "units/hd/portrait_towed_artillery.png"
        e["portraitTeam"] = "units/hd/portrait_towed_artillery_team.png"
    sprites["unit.towed_artillery"] = e
    print("[export] towed_artillery")


FX = {"fx_explosion": ("fx.explosion", 96, 6, 12, [48, 76], 8), "fx_muzzle_big": ("fx.muzzle_big", 64, 3, 20, [32, 44], 3)}


def export_fx(theme, sprites):
    dst = os.path.join(GAME_THEME, "units", "hd")
    for name, (key, size, n, fps, anchor, rows) in FX.items():
        fp = os.path.join(OUT, name, f"{name}.png")
        if not os.path.isfile(fp):
            print(f"[skip] {name}")
            continue
        os.makedirs(dst, exist_ok=True)
        shutil.copy2(fp, os.path.join(dst, f"{name}.png"))
        sprites[key] = {"sheet": f"units/hd/{name}.png", "frame": size, "frames": n, "fps": fps, "anchor": anchor, "variantRows": rows}
        print(f"[export] {key}")


if __name__ == "__main__":
    main()
