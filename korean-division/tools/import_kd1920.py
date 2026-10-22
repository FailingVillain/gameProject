"""
game-tools 렌더 결과 (새 형식: 128x160 + @64 + @32, 차량 hull/turret + LOD) → assets/themes/kd1920/ 로 복사하고 theme.json 을 쓴다.

    python tools/import_kd1920.py [--src <korean-division-image-out 경로>] [--only a,b] [--prune]

- 기본: 가져오는 유닛 폴더만 지우고 다시 복사한다 (그 밖의 units/ 폴더는 건드리지 않음).
  --only a,b : 렌더 폴더 이름이 a, b 인 것만 가져온다 (theme.json 의 나머지 항목은 기존 값 유지).
  --prune    : 이번에 가져오지 않은 units/ 폴더를 모두 삭제 (옛 폴더 정리). --only 와 같이 쓰면 거부.

- 유닛 폴더는 SRC 아래 진영 하위 폴더(korean/, japanese/, …)에서 이름으로 재귀 검색한다 (_drafts · _legacy · buildings 제외).
- 유닛 폴더마다 레이아웃이 가리키는 파일만 복사 (시트 · 팀 시트(_team.png, 회색조) · _anchors.json). contact · gif · 옛 LOD 잔재는 제외.
  LOD 레이아웃은 태그를 하드코딩하지 않고 '기본 폭의 1/2 · 1/4' 크기인 것을 고른다 (@64/@32, @96/@48, @168/@84 ...).
- theme.json 은 매번 새로 쓴다 (손으로 고칠 값은 아래 UNITS / VARIANTS / ALIASES 에서). "inherit": "ww2" → 없는 항목(건물 · 지형 · 효과 · 자주포 등)은 ww2 테마.
- 다시 돌려도 안전 (idempotent): 폴더를 지우고 다시 복사하고, 안 쓰는 옛 폴더 정리는 --prune 일 때만. 승무원 · 기병 · 포 재렌더 후에도 그대로 실행.
- 진영 변형: theme 키 "unit.<유닛>@<변형>" (japanese · fengtian · russian-white · russian-red). 게임이 지도 세력(factions[].en)으로 고른다.
- 게임 로더: src/Render/LodSprite.cs (LOD 선택, 점 필터, 팀 레이어는 시트 알파 254 로 접어 셰이더에서 칠함), 동작 매핑 src/Render/UnitArt.cs.
"""
import json
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CODING = os.path.dirname(ROOT)
# 워크트리 (C:/gameProject/korean-division/.claude/worktrees/x) 에서 실행해도 coding/ 을 찾는다
while CODING and not os.path.isdir(os.path.join(CODING, "game-tools")) and os.path.dirname(CODING) != CODING:
    CODING = os.path.dirname(CODING)
SRC = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else os.path.join(CODING, "game-tools", "korean-division-image-out")
ONLY = set(sys.argv[sys.argv.index("--only") + 1].split(",")) if "--only" in sys.argv else None
PRUNE = "--prune" in sys.argv
DST = os.path.join(ROOT, "assets", "themes", "kd1920")

SCALE = 0.25   # LOD 0 시트 1px = 월드 0.25px → 보병 128x160 = 32x40 (옛 64x80 x0.5 와 같은 크기), 차량은 보병 x1.2 줌으로 렌더됨

# 테마 키 → (렌더 폴더, 부품). 부품 None = 보병형 (name, name@64, name@32 ...), "vehicle" = hull + turret.
# 보병 · 승무원 · 포 · 기병 · 마차는 모두 한 시트 한 스프라이트 (승무원/포는 시트 안에 같이 그려져 있다).
UNITS = {
    "unit.infantry": ("infantry-default", None),
    "unit.officer": ("officer", None),
    "unit.infantry_scout": ("infantry-scout", None),       # 정찰병 (쌍안경 관측)
    "unit.infantry_regular": ("infantry-regular", None),   # 철모 보병
    "unit.infantry_assault": ("infantry-assault", None),   # 돌격병
    "unit.infantry_pioneer": ("infantry-pioneer", None),   # 전투공병 (T32d)
    "unit.infantry_guard": ("infantry-guard", None),       # 시위대
    "unit.infantry_reserve": ("infantry-reserve", None),   # 예비군 (옛 군복, 옛 이름 infantry-default-old). 폴더가 아직 없으면 건너뜀
    "unit.infantry_sniper": ("infantry-sniper", None),     # 저격수
    "unit.infantry_fengtian": ("infantry-fengtian", None), # 봉천 보병 (대도)
    "unit.officer_fengtian": ("officer-fengtian", None),
    "unit.infantry_dadao_fengtian": ("infantry-dadao-fengtian", None),   # 봉천 대도병
    "unit.bandit_thug": ("infantry-thug-bandit", None),    # 마적 잡병 (idle/walk = 등에 멘 대도, charge/melee = 뽑은 대도, 사격 없음)
    "unit.bandit_rifleman": ("infantry-bandit", None),
    "unit.bandit_horseman": ("cavalry-bandit", None),
    "unit.bandit_leader": ("officer-bandit", None),
    "unit.heavy_machinegunner_chauchat": ("heavy-machinegunner-chauchat", None),   # 한국 쇼샤 (대안 모델 — 쓰는 유닛 정의가 있을 때)
    "unit.heavy_machinegunner": ("heavy-machinegunner", None),
    "unit.heavy_at_rifleman": ("heavy-at-rifleman", None),
    "unit.heavy_maxim": ("heavy-maxim", None),             # 맥심 (2인)
    "unit.heavy_type3": ("heavy-type3-japanese", None),    # 3년식 중기관총 (일본 전용, 2인)
    "unit.heavy_infantry_gun": ("heavy-infantry-gun", None),
    "unit.heavy_field_gun": ("heavy-field-gun", None),
    "unit.heavy_howitzer": ("heavy-howitzer", None),
    "unit.cavalry_default": ("cavalry-default", None),
    "unit.officer_cavalry": ("cavalry-officer", None),    # 기병 장교 (cavcmd, 기병 모델 복사 + 장교 장비)
    "unit.supply_wagon": ("supply-wagon", None),
    "unit.cavalry_mongol": ("cavalry-mongol", None),       # 몽골 기병 (n63~n72 신규)
    "unit.cavalry_cossack": ("cavalry-cossack", None),     # 카자크 기병
    "unit.cavalry_scout": ("cavalry-scout", None),         # 기병 정찰
    "unit.common_gando_volunteer": ("infantry-gando", None),   # 간도 의용군
    "unit.infantry_guard_fengtian": ("infantry-guard-fengtian", None),   # 봉천 친위대
    "unit.common_hunter": ("infantry-hunter", None),       # 만주 사냥꾼 (newunits)
    "unit.bandit_shotgunner": ("infantry-shotgunner-bandit", None),   # 마적 엽총수 (newunits)
    "unit.common_draft_bandit": ("infantry-draft-bandit", None),       # 초안 마적 (newunits)
    "unit.common_village_militia": ("infantry-militia", None),   # 민단 자위대 (newunits)
    "unit.standard_bearer": ("standard-bearer", None),     # 기수 (진영 변형은 VARIANTS)
    "unit.heavy_tachanka": ("heavy-tachanka", None),       # 막심 마차 (마부 + 사수)
    "unit.heavy_horse_artillery": ("heavy-horse-artillery", None),   # 기마 포병 (마차 + 보병포)
    "unit.tank_ft_male": ("tank-ft-male", "vehicle"),      # 르노 FT 수컷 (37mm)
    "unit.tank_ft_female": ("tank-ft-female", "vehicle"),  # 르노 FT 암컷 (기관총)
    "unit.tank_armored_car": ("tank-armored_car", "vehicle"),   # 장갑차
    "unit.tank_mark_v": ("tank-mark_v", "vehicle"),         # Mark V 수컷 (armour)
    "unit.tank_lanchester": ("tank-lanchester", "vehicle"),
    "unit.tank_fengtian_truck": ("tank-fengtian_truck", "vehicle"),
}
# 진영 변형: "unit.<기본>@<변형>" — 게임이 세력(지도 factions[].en)으로 변형을 고른다 (src/Render/UnitArt.cs FactionArt).
# 없는 변형은 기본(한국) 그림. 새 변형 폴더가 렌더되면 여기에 한 줄만 추가.
_VARIANT_BASES = {
    "unit.infantry": "infantry", "unit.officer": "officer", "unit.heavy_machinegunner": "heavy-machinegunner",
    "unit.heavy_at_rifleman": "heavy-at-rifleman", "unit.heavy_maxim": "heavy-maxim", "unit.cavalry_default": "cavalry",
    "unit.infantry_sniper": "infantry-sniper", "unit.heavy_infantry_gun": "heavy-infantry-gun",
    "unit.heavy_field_gun": "heavy-field-gun", "unit.heavy_howitzer": "heavy-howitzer",
    "unit.standard_bearer": "standard-bearer",
    "unit.officer_cavalry": "cavalry-officer",   # 기병을 내는 세력만 변형 (지금 fengtian — 나머지는 폴더 없음 = 건너뜀)
}
VARIANTS = {k: {v: f"{b}-{v}" for v in ("japanese", "fengtian", "russian-white", "russian-red")} for k, b in _VARIANT_BASES.items()}
VARIANTS["unit.heavy_machinegunner"]["fengtian"] = "heavy-machinegunner-chauchat-fengtian"   # 봉천 경기관총수 = 쇼샤
VARIANTS["unit.heavy_machinegunner_chauchat"] = {"fengtian": "heavy-machinegunner-chauchat-fengtian"}
ALIASES = {
    "unit.recruit": "unit.infantry",       # 징집 · 정예 · 정찰병은 전용 그림이 없음 (기본 보병 + 진영 변형)
    "unit.veteran": "unit.infantry",
    "unit.scout": "unit.infantry",
    "unit.infantry_default_old": "unit.infantry_reserve",   # 지방대 = infantry-reserve
    }
FALLBACK = {}   # 렌더 폴더가 아직 없을 때 임시 별칭 (infantry-reserve 연결 완료로 비움)
SKIP_DIRS = ("_drafts", "_legacy", "buildings", "_res96")   # _res96 = 96x120 재렌더 출력 (--src 로 직접 지정해서 가져옴)


def find_unit_dir(folder):
    """렌더 폴더 찾기: game-tools 는 진영별 하위 폴더(korean/, japanese/, fengtian/, russian-white/, russian-red/, vehicles/)에 쓴다.
    이름이 정확히 같은 폴더를 SRC 아래에서 재귀로 찾는다(_drafts · _legacy · buildings 제외). 평면 배치(옛 방식)도 그대로 인식."""
    hits = []
    for dp, dn, _ in os.walk(SRC):
        dn[:] = [d for d in dn if d not in SKIP_DIRS]
        if folder in dn:
            hits.append(os.path.join(dp, folder))
    if len(hits) > 1:
        raise SystemExit(f"[오류] {folder} 폴더가 여러 곳에 있음: {hits}")
    return hits[0] if hits else None


def frame_w(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)["frame"][0]


def select_layouts(src, folder, part):
    """src 폴더에서 쓸 레이아웃 파일명들 (큰 해상도 먼저). LOD 태그(@64 / @96 / @168 ...)는 하드코딩하지 않는다:
    기본 레이아웃 폭 W 의 1/2 · 1/4 (±1px) 인 것만 쓴다 → 옛 렌더가 남긴 다른 크기 파일(@136 등)은 무시."""
    if part is None:
        main = f"{folder}_layout.json"
        if not os.path.isfile(os.path.join(src, main)):
            return []
        w = frame_w(os.path.join(src, main))
        out = [main]
        cands = [fn for fn in sorted(os.listdir(src)) if fn.startswith(f"{folder}@") and fn.endswith("_layout.json")]
        for div in (2, 4):
            want = round(w / div)
            hit = [fn for fn in cands if abs(frame_w(os.path.join(src, fn)) - want) <= 1]
            if hit:
                out.append(hit[-1])
        return out
    # 차량: 주 레이아웃(<name>_<part>_layout.json) + 같은 렌더(수정 시각 10분 이내)의 LOD 레이아웃
    main = os.path.join(src, f"{folder}_{part}_layout.json")
    t0 = os.path.getmtime(main) if os.path.isfile(main) else None
    return [fn for fn in sorted(os.listdir(src)) if fn.startswith(f"{folder}_{part}") and fn.endswith("_layout.json")
            and (t0 is None or abs(os.path.getmtime(os.path.join(src, fn)) - t0) < 600)]


def referenced_files(src, layout_fn):
    """레이아웃이 가리키는 파일: 레이아웃 · 시트 · 팀 시트 · 기준점 json."""
    with open(os.path.join(src, layout_fn), encoding="utf-8") as f:
        d = json.load(f)
    names = [layout_fn]
    for sh in d.get("sheets", []):
        names.append(sh["file"])
        if sh.get("team"):
            names.append(sh["team"])
    if d.get("anchors"):
        names.append(d["anchors"])
    return names


copied = {}   # (폴더, 종류) → {부품: [레이아웃 파일명]}


def copy_unit(folder, kind):
    """폴더를 kd1920/units/<folder>/ 로 (필요한 파일만, 재실행 안전: 지우고 다시 복사)."""
    src = find_unit_dir(folder)
    if src is None:
        return False
    parts = ["hull", "turret"] if kind == "vehicle" else [None]
    wanted = []
    layouts = {}
    for part in parts:
        ls = select_layouts(src, folder, part)
        if not ls:
            if part == "turret":
                continue
            return False
        layouts[part] = ls
        for ln in ls:
            wanted += referenced_files(src, ln)
    dst = os.path.join(DST, "units", folder)
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    n = 0
    for fn in dict.fromkeys(wanted):
        fp = os.path.join(src, fn)
        if os.path.isfile(fp):
            shutil.copy2(fp, os.path.join(dst, fn))
            n += 1
        else:
            print(f"[경고] {folder}: {fn} 없음")
    copied[(folder, kind)] = layouts
    print(f"[복사] {folder}: {n} 파일 (LOD {len(layouts[parts[0]])}개)")
    return True


def layouts_for(folder, kind, part):
    return [f"units/{folder}/{n}" for n in copied[(folder, kind)][part]]


def layout_scale(rel):
    """96x120 재렌더 (layout "res96": true) = 화면 크기 유지: 스케일 = 32 / 주 프레임 폭 (192 → 0.1667). 그 외 SCALE."""
    with open(os.path.join(DST, rel), encoding="utf-8") as f:
        L = json.load(f)
    return round(32.0 / L["frame"][0], 6) if L.get("res96") else SCALE


def sprite_entry(folder, kind, sprites, key):
    if kind == "vehicle":
        sprites[key] = {"layouts": layouts_for(folder, kind, "hull"), "scale": SCALE}
        if "turret" in copied[(folder, kind)]:
            sprites[key + ".turret"] = {"layouts": layouts_for(folder, kind, "turret"), "scale": SCALE}
    else:
        lays = layouts_for(folder, kind, None)
        sprites[key] = {"layouts": lays, "scale": layout_scale(lays[0])}


def main():
    os.makedirs(DST, exist_ok=True)
    sprites = {}
    missing = []
    if PRUNE and ONLY:
        raise SystemExit("[오류] --prune 은 --only 와 같이 쓸 수 없음 (일부만 가져오면 나머지가 지워짐)")
    for key, (folder, kind) in UNITS.items():
        if ONLY and folder not in ONLY:
            continue
        if not copy_unit(folder, kind):
            missing.append(folder)
            if key in FALLBACK:
                sprites[key] = {"alias": FALLBACK[key]}
            continue
        sprite_entry(folder, kind, sprites, key)
    variant_keys = {}   # 기본 키 → [변형 이름]
    for base, vs in VARIANTS.items():
        for var, folder in vs.items():
            if ONLY and folder not in ONLY:
                continue
            if (folder, None) not in copied and not copy_unit(folder, None):
                missing.append(folder)
                continue
            sprite_entry(folder, None, sprites, f"{base}@{var}")
            variant_keys.setdefault(base, []).append(var)
    for k, v in ALIASES.items():
        sprites[k] = {"alias": v}
        for var in variant_keys.get(v, []):
            sprites[f"{k}@{var}"] = {"alias": f"{v}@{var}"}
    # --prune 일 때만: 이번에 쓰지 않는 옛 유닛 폴더 정리 (폴더 이름 변경 후 청소)
    used = {f for f, _ in copied}
    ud = os.path.join(DST, "units")
    if PRUNE and os.path.isdir(ud):
        for d in sorted(os.listdir(ud)):
            if d not in used and os.path.isdir(os.path.join(ud, d)):
                shutil.rmtree(os.path.join(ud, d))
                print(f"[정리] 안 쓰는 폴더 삭제: {d}")
    theme = {
        "name": "1920 (복셀 128 · LOD)",
        "inherit": "ww2",
        "names": {},
        "sprites": sprites,
    }
    # 손으로 추가한 항목(terrain.* · 별칭 · _doc_* 키)은 보존: 기존 theme.json 에만 있는 것을 되살린다.
    tp = os.path.join(DST, "theme.json")
    if os.path.isfile(tp):
        with open(tp, encoding="utf-8") as f:
            old = json.load(f)
        for k, v in old.get("sprites", {}).items():
            sprites.setdefault(k, v)
        for k, v in old.items():
            if k not in theme:
                theme[k] = v
    with open(os.path.join(DST, "theme.json"), "w", encoding="utf-8") as f:
        json.dump(theme, f, ensure_ascii=False, indent=2)
    print(f"[theme] {os.path.join(DST, 'theme.json')} : 항목 {len(sprites)}개" + (f", 없음: {missing}" if missing else ""))


if __name__ == "__main__":
    main()
