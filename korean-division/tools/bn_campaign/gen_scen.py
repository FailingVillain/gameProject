#!/usr/bin/env python3
"""Battalion campaign scenario matrix (2026-10-19). Generates mission JSONs + matrix.json into an output dir.

Usage: python gen_scen.py <outdir>
Dimensions: kind (meet/assault/defence/flank), terrain, force ratio (attacker rifle companies / defender rifle companies),
composition letters (R rifle, M machine-gun company, G guns (+ howitzer & observers for the attacker), C cavalry),
faction pair of side 2 (ft Fengtian / jp Japanese / ru Russian skins: stat-identical rifleman, only the melee weapon differs), weather/time.
No tanks, no AT riflemen. Side 1 is always the Korean army.
"""
import json, sys, os

SEEDS = [1, 2, 3, 4, 5, 6, 7, 8]

FAC = {
    'kr': dict(name='한국군', en='korean', lang='ko', color=[60, 130, 235], off='officer', inf='infantry', n=84),
    'ft': dict(name='봉천 군벌', en='fengtian', lang='zh', color=[165, 95, 225], off='officer_fengtian', inf='infantry_fengtian', n=92),
    'jp': dict(name='일본군', en='japanese', lang='ja', color=[220, 70, 70], off='officer', inf='infantry_regular', n=92),
    'ru': dict(name='러시아군', en='russian', lang='ru', color=[90, 170, 90], off='officer', inf='infantry_reserve', n=92),
}

# ---- terrain recipes (160x120 map, attackers start south y~100, defenders north y~30)
ROAD = {"terrain": "dirt", "rect": [78, 0, 4, 120]}
VILLAGE_HOUSES = {"terrain": "house", "tiles": [[70, 22], [70, 28], [77, 22], [77, 28], [84, 22], [84, 28]]}
TRENCH = [{"terrain": "trench", "rect": r} for r in ([52, 40, 24, 2], [84, 40, 24, 2], [52, 40, 2, 8], [106, 40, 2, 8])] + \
         [{"terrain": "wire", "rect": r} for r in ([48, 50, 28, 1], [84, 50, 28, 1])]
CORNER_FOREST = [{"terrain": "forest", "rect": r} for r in ([16, 50, 24, 18], [120, 50, 24, 18], [26, 76, 18, 12])]
TOWN = [{"terrain": "house", "tiles": [[x, y] for x in range(62, 100, 6) for y in (24, 30, 36)]},
        {"terrain": "rubble", "rect": [72, 32, 16, 2]}]
TERRAINS = {
    'trench': dict(paint=[ROAD] + CORNER_FOREST + [VILLAGE_HOUSES] + TRENCH, area=[56, 16, 48, 30], def_at=(80, 41)),
    'open': dict(paint=[ROAD] + CORNER_FOREST, area=[56, 16, 48, 30], def_at=(80, 30)),
    'forest': dict(paint=[ROAD, {"terrain": "forest", "rect": [52, 18, 56, 26]}, {"terrain": "dirt", "rect": [78, 18, 4, 26]}], area=[52, 18, 56, 26], def_at=(80, 30)),
    'town': dict(paint=[ROAD] + CORNER_FOREST + TOWN, area=[56, 18, 48, 24], def_at=(80, 28)),
    'hill': dict(paint=[ROAD] + CORNER_FOREST + [{"terrain": "high", "rect": [40, 14, 80, 32]}], area=[56, 16, 48, 30], def_at=(80, 30)),
    # meeting engagements (both attack, area = centre)
    'meet_open': dict(paint=[ROAD, {"terrain": "dirt", "rect": [0, 58, 160, 3]}] + [{"terrain": "forest", "rect": r} for r in ([20, 40, 26, 18], [114, 62, 26, 18], [30, 76, 18, 12], [112, 32, 18, 12])] +
                      [{"terrain": "house", "tiles": [[x, y] for x in (68, 74, 80, 86) for y in (52, 57, 62)]}, {"terrain": "rubble", "rect": [76, 56, 6, 5]}], area=[62, 46, 36, 28], def_at=(80, 20)),
    'meet_flat': dict(paint=[ROAD, {"terrain": "dirt", "rect": [0, 58, 160, 3]}], area=[62, 46, 36, 28], def_at=(80, 20)),
    'meet_forest': dict(paint=[ROAD, {"terrain": "forest", "rect": [50, 42, 60, 32]}, {"terrain": "dirt", "rect": [78, 42, 4, 32]}], area=[62, 46, 36, 28], def_at=(80, 20)),
    'meet_hill': dict(paint=[ROAD, {"terrain": "dirt", "rect": [0, 58, 160, 3]}, {"terrain": "high", "circle": [80, 60, 18]}], area=[62, 46, 36, 28], def_at=(80, 20)),
}


def rifle_co(fac, k=1):
    f = FAC[fac]
    return {"units": {f['off']: 3, f['inf']: f['n'], "infantry_scout": 2}, "formation": "line"}


def mg_units(fac, big):
    f = FAC[fac]
    return {f['off']: 1, "heavy_maxim": 5 if big else 2, "heavy_machinegunner": 8 if big else 4, "infantry": 4 if big else 3}


def forces(cfg):
    out = []
    A, B = 'kr', cfg['fac2']
    comp = cfg['comp']
    natk, ndef, nflank = cfg['natk'], cfg['ndef'], cfg.get('nflank', 0)
    ax, ay, dx, dy = 80, 100, cfg['def_at'][0], cfg['def_at'][1]
    # attacker battalion(s)
    out.append({"id": "f1_bn", "faction": 1, "name": "한국군 소총대대", "at": [ax, ay], "facing": 0, "formation": "line", "battalion": 1, "battalionForm": "line",
                "companies": [rifle_co(A) for _ in range(natk)]})
    if nflank:
        out.append({"id": "f1_flank", "faction": 1, "name": "한국군 측면 대대", "at": [140, 66], "facing": 270, "formation": "line", "battalion": 2, "battalionForm": "line",
                    "companies": [rifle_co(A) for _ in range(nflank)]})
    if 'M' in comp:
        for i in range(max(1, (natk + nflank + 2) // 3)):
            out.append({"id": f"f1_mg{i}", "faction": 1, "name": "한국군 기관총 중대", "at": [ax + 6 * i - 3, ay + 8], "facing": 0, "units": mg_units(A, True), "company": True, "formation": "line"})
        for i in range(max(1, (ndef + 2) // 3)):
            out.append({"id": f"f2_mg{i}", "faction": 2, "name": "적 기관총 중대", "at": [dx + 6 * i - 3, dy - 6], "facing": 180, "units": mg_units(B, False), "company": True, "formation": "line"})
    if 'G' in comp:
        out.append({"id": "f1_ig", "faction": 1, "name": "한국군 포 중대", "at": [ax - 10, ay + 10], "facing": 0, "units": {"heavy_infantry_gun": 2, "heavy_field_gun": 1}, "company": False})
        out.append({"id": "f1_how", "faction": 1, "name": "한국군 곡사포", "at": [ax, ay + 14], "facing": 0, "units": {"heavy_howitzer": 2, "officer": 1}, "company": False})
        out.append({"id": "f1_obs", "faction": 1, "name": "한국군 관측반", "at": [ax, ay + 11], "facing": 0, "units": {"infantry_scout": 3}, "company": True})
        out.append({"id": "f2_ig", "faction": 2, "name": "적 포 중대", "at": [dx - 10, dy - 8], "facing": 180, "units": {"heavy_infantry_gun": 2, "heavy_field_gun": 1}, "company": False})
    if 'C' in comp:
        cx = 130 if not nflank else 140
        out.append({"id": "f1_cav", "faction": 1, "name": "한국군 기병 소대", "at": [130, 104], "facing": 0, "formation": "line", "units": {"officer": 1, "cavalry_default": 11}})
        out.append({"id": "f2_cav", "faction": 2, "name": "적 기병 소대", "at": [130, dy - 8], "facing": 180, "formation": "line", "units": {"officer": 1 if B != 'ft' else "officer_fengtian", "cavalry_default": 11}})
        # fix officer key
        out[-2]["units"] = {"officer": 1, "cavalry_default": 11}
        out[-1]["units"] = {FAC[B]['off']: 1, "cavalry_default": 11}
    out.append({"id": "f2_bn", "faction": 2, "name": "적 소총대대", "at": [dx, dy], "facing": 180, "formation": "line", "battalion": 1, "battalionForm": "line",
                "companies": [rifle_co(B) for _ in range(ndef)]})
    return out


def mission(cfg):
    t = TERRAINS[cfg['terrain']]
    meet = cfg['kind'] == 'meet'
    facs = []
    for i, k in enumerate(('kr', cfg['fac2'])):
        f = FAC[k]
        facs.append({"name": f['name'], "en": f['en'], "lang": f['lang'], "color": f['color'], "team": i + 1,
                     "posture": "attack" if (meet or i == 0) else "defend", "area": "zone"})
    return {
        "id": cfg['id'], "title": cfg['id'], "date": "1920년 가을", "location": "간도", "briefing": "bncamp " + cfg['id'],
        "seed": 1920, "weather": cfg.get('weather', 'clear'), "time": cfg.get('time', 'day'), "player": 1, "spectate": True,
        "factions": facs, "areas": {"zone": {"rect": t['area']}},
        "mapData": {"name": cfg['id'], "width": 160, "height": 120, "base": "grass", "paint": t['paint'], "starts": [[80, 100], [80, 30]]},
        "objectives": [{"id": "crush", "type": "eliminate", "percent": 97, "faction": 2, "text": "적 격멸"}],
        "failures": [{"type": "losses", "percent": 97}, {"type": "timeLimit", "seconds": 900}],
        "events": [],
        "forces": forces(cfg),
    }


def cfgs():
    L = []
    def add(id_, kind, terrain, natk, ndef, comp, fac2='ft', **kw):
        d = dict(id=id_, kind=kind, terrain=terrain, natk=natk, ndef=ndef, comp=comp, fac2=fac2, def_at=TERRAINS[terrain]['def_at'])
        d.update(kw); L.append(d)
    # meeting engagements, 3 v 3 companies
    for c in ('R', 'RM', 'RMG', 'RMGC'): add(f"M_open_{c}", 'meet', 'meet_open', 3, 3, c)
    add("M_flat_RM", 'meet', 'meet_flat', 3, 3, 'RM')
    add("M_forest_RM", 'meet', 'meet_forest', 3, 3, 'RM')
    add("M_hill_RM", 'meet', 'meet_hill', 3, 3, 'RM')
    add("M_open_RM_r2.0", 'meet', 'meet_open', 6, 3, 'RM')
    add("M_open_RM_JP", 'meet', 'meet_open', 3, 3, 'RM', 'jp')
    add("M_open_RM_RU", 'meet', 'meet_open', 3, 3, 'RM', 'ru')
    add("M_open_RM_fog", 'meet', 'meet_open', 3, 3, 'RM', weather='fog')
    add("M_open_RM_night", 'meet', 'meet_open', 3, 3, 'RM', time='night')
    # assault on a trench line, defender 2 companies
    for r, n in ((1.0, 2), (1.5, 3), (2.0, 4), (3.0, 6), (4.0, 8)): add(f"T_r{r}_RM", 'assault', 'trench', n, 2, 'RM', ratio=r)
    for c in ('R', 'RMG', 'RMGC'): add(f"T_r2.0_{c}", 'assault', 'trench', 4, 2, c, ratio=2.0)
    add("T_r2.0_RM_fog", 'assault', 'trench', 4, 2, 'RM', weather='fog', ratio=2.0)
    add("T_r2.0_RM_night", 'assault', 'trench', 4, 2, 'RM', time='night', ratio=2.0)
    add("T_r2.0_RM_rain", 'assault', 'trench', 4, 2, 'RM', weather='rain', ratio=2.0)
    add("T_r2.0_RM_JP", 'assault', 'trench', 4, 2, 'RM', 'jp', ratio=2.0)
    add("T_r2.0_RM_RU", 'assault', 'trench', 4, 2, 'RM', 'ru', ratio=2.0)
    # defence in open ground / other cover
    for r, n in ((1.0, 2), (1.5, 3), (2.0, 4), (3.0, 6)): add(f"D_open_r{r}_RM", 'defence', 'open', n, 2, 'RM', ratio=r)
    add("D_hill_r2.0_RM", 'defence', 'hill', 4, 2, 'RM', ratio=2.0)
    add("D_forest_r2.0_RM", 'defence', 'forest', 4, 2, 'RM', ratio=2.0)
    add("D_town_r2.0_RM", 'defence', 'town', 4, 2, 'RM', ratio=2.0)
    add("D_town_r3.0_RM", 'defence', 'town', 6, 2, 'RM', ratio=3.0)
    # flank attack: front + a battalion arriving from the east flank
    add("F_trench_r1.5_RM", 'flank', 'trench', 2, 2, 'RM', nflank=1, ratio=1.5)
    add("F_open_r1.5_RM", 'flank', 'open', 2, 2, 'RM', nflank=1, ratio=1.5)
    add("F_trench_r2.0_RM", 'flank', 'trench', 3, 2, 'RM', nflank=1, ratio=2.0)
    add("F_open_r2.0_RM", 'flank', 'open', 3, 2, 'RM', nflank=1, ratio=2.0)
    add("F_trench_r1.0_RMC", 'flank', 'trench', 2, 2, 'RMC', ratio=1.0)
    return L


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    L = cfgs()
    for c in L:
        json.dump(mission(c), open(os.path.join(out, c['id'] + '.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    json.dump({"seeds": SEEDS, "configs": L}, open(os.path.join(out, 'matrix.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    print(len(L), "configs x", len(SEEDS), "seeds =", len(L) * len(SEEDS), "runs")
