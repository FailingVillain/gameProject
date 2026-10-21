#!/usr/bin/env python3
"""Battalion campaign 2 scenario matrix (2026-10-19): UNTRIED combinations. Observation only (no engine / balance / AI change).

Usage: python gen_scen2.py <outdir>      (run.py --gen gen_scen2 uses cfgs()/mission()/SEEDS)
Differences to gen_scen (campaign 1): every side is described by a spec so company sizes and MG counts can be made symmetric;
per-faction posture / aiProfile / unit quality / squadLeaderRank / officer count; per-config env (KD_OBS_* hooks); capture objective;
reinforcements with `time`; river+bridge terrain; pincers from several directions; hold/counterattack setups.
No tanks, no AT riflemen.
"""
import json, sys, os, copy
import gen_scen as G
from gen_scen import FAC, TERRAINS, ROAD, CORNER_FOREST, TRENCH

SEEDS = [1, 2, 3, 4, 5]
SEEDS8 = [1, 2, 3, 4, 5, 6, 7, 8]

# ---- extra terrain
RIVER = [{"terrain": "water", "rect": [0, 52, 160, 6]}, {"terrain": "dirt", "rect": [78, 52, 4, 6]}]
RIVER2 = [{"terrain": "water", "rect": [0, 52, 160, 6]}, {"terrain": "dirt", "rect": [56, 52, 4, 6]}, {"terrain": "dirt", "rect": [100, 52, 4, 6]}]
TERRAINS = dict(TERRAINS)
TERRAINS['river1'] = dict(paint=[ROAD] + RIVER + [{"terrain": "dirt", "rect": [78, 30, 4, 22]}, {"terrain": "dirt", "rect": [78, 58, 4, 42]}], area=[56, 28, 48, 22], def_at=(80, 46))
TERRAINS['river2'] = dict(paint=[ROAD] + RIVER2, area=[56, 28, 48, 22], def_at=(80, 46))
TERRAINS['open_nf'] = dict(paint=[ROAD], area=[56, 16, 48, 30], def_at=(80, 30))
# counterattack: holder sits IN the trench, enemy comes from the north (behind the trench), area = trench zone
TERRAINS['trench_cap'] = dict(paint=TERRAINS['trench']['paint'], area=[56, 34, 48, 16], def_at=(80, 41))


# ---- unit helpers
def rifle_co(fac, n=None, off=3, scouts=2, inf=None, rank=0, slr=None):
    f = FAC[fac]
    n = f['n'] if n is None else n
    d = {"units": {f['off']: off, inf or f['inf']: n, "infantry_scout": scouts}, "formation": "line"}
    if rank: d["rank"] = rank
    if slr is not None: d["squadLeaderRank"] = slr
    return d


def mg_units(fac, maxim, lmg, inf):
    f = FAC[fac]
    return {f['off']: 1, "heavy_maxim": maxim, "heavy_machinegunner": lmg, "infantry": inf}


MG1 = (2, 4, 3)     # campaign 1 defender MG company
MG2 = (5, 8, 4)     # campaign 1 attacker MG company ("big")


def bn(id_, side, fac, name, at, facing, cos, n_bn=1, form="line", time=None, **ckw):
    d = {"id": id_, "faction": side, "name": name, "at": list(at), "facing": facing, "formation": "line", "battalion": n_bn, "battalionForm": form,
         "companies": [rifle_co(fac, **ckw) for _ in range(cos)]}
    if time is not None: d["time"] = time
    return d


def mgco(id_, side, fac, at, facing, spec, time=None):
    d = {"id": id_, "faction": side, "name": "기관총 중대", "at": list(at), "facing": facing, "units": mg_units(fac, *spec), "company": True, "formation": "line"}
    if time is not None: d["time"] = time
    return d


def guns(id_p, side, fac, at, facing, obs=True, how=2, ig=True, dirn=1):
    """gun park: infantry guns + field gun (+ howitzers + observers) placed behind `at` (dirn=+1 behind a south attacker, -1 behind a north defender)."""
    x, y = at
    out = []
    if ig: out.append({"id": id_p + "_ig", "faction": side, "name": "포 중대", "at": [x - 10, y + 10 * dirn], "facing": facing, "units": {"heavy_infantry_gun": 2, "heavy_field_gun": 1}, "company": False})
    if how: out.append({"id": id_p + "_how", "faction": side, "name": "곡사포", "at": [x, y + 14 * dirn], "facing": facing, "units": {"heavy_howitzer": how, FAC[fac]['off']: 1}, "company": False})
    if obs: out.append({"id": id_p + "_obs", "faction": side, "name": "관측반", "at": [x, y + 11 * dirn], "facing": facing, "units": {"infantry_scout": 3}, "company": True})
    return out


def cav(id_, side, fac, at, facing, n=11, time=None):
    d = {"id": id_, "faction": side, "name": "기병 소대", "at": list(at), "facing": facing, "formation": "line", "units": {FAC[fac]['off']: 1, "cavalry_default": n}}
    if time is not None: d["time"] = time
    return d


# ---- mission builder
def mission(cfg):
    t = TERRAINS[cfg['terrain']]
    facs = []
    for i, (k, posture, prof) in enumerate(((cfg.get('fac1', 'kr'), cfg['p1'], cfg.get('prof1')), (cfg.get('fac2', 'ft'), cfg['p2'], cfg.get('prof2')))):
        f = FAC[k]
        d = {"name": f['name'], "en": f['en'], "lang": f['lang'], "color": f['color'], "team": i + 1, "posture": posture, "area": "zone"}
        if prof: d["aiProfile"] = prof
        facs.append(d)
    if cfg.get('fac1', 'kr') == cfg.get('fac2', 'ft'):
        raise ValueError('same faction on both sides: ' + cfg['id'])
    area = cfg.get('area') or t['area']
    if cfg.get('obj') == 'capture':
        z = cfg['capZone']
        objectives = [{"id": "take", "type": "capture", "area": "cap", "seconds": cfg.get('capSec', 30), "text": "진지 점령"}]
        areas = {"zone": {"rect": area}, "cap": {"rect": z}}
    else:
        objectives = [{"id": "crush", "type": "eliminate", "percent": 97, "faction": 2, "text": "적 격멸"}]
        areas = {"zone": {"rect": area}}
    return {
        "id": cfg['id'], "title": cfg['id'], "date": "1920년 가을", "location": "간도", "briefing": "bncamp2 " + cfg['id'],
        "seed": 1920, "weather": cfg.get('weather', 'clear'), "time": cfg.get('time', 'day'), "player": 1, "spectate": True,
        "factions": facs, "areas": areas,
        "mapData": {"name": cfg['id'], "width": 160, "height": 120, "base": "grass", "paint": t['paint'], "starts": [[80, 100], [80, 30]]},
        "objectives": objectives,
        "failures": [{"type": "losses", "percent": 97}, {"type": "timeLimit", "seconds": 900}],
        "events": [],
        "forces": cfg['forces'],
    }


def cfgs():
    L = []
    A, B = 'kr', 'ft'

    def add(id_, kind, terrain, forces, p1='attack', p2='defend', seeds=None, **kw):
        d = dict(id=id_, kind=kind, terrain=terrain, forces=forces, p1=p1, p2=p2, fac1='kr', fac2='ft', def_at=TERRAINS[terrain]['def_at'], seeds=seeds or SEEDS)
        d.update(kw); L.append(d)

    ax, ay = 80, 100

    def dpos(terrain): return TERRAINS[terrain]['def_at']

    # equal company: 84 infantry (kr 84 / ft set to 84 here too = symmetric headcount 89 per rifle company)
    def atk(n_co, **kw): return bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, n_co, **kw)
    def dfn(terrain, n_co, **kw):
        x, y = dpos(terrain)
        return bn("f2_bn", 2, B, "적 소총대대", (x, y), 180, n_co, **{**dict(n=84), **kw})

    # ============ A. MG-heavy defence vs rifle-only assault (defender MG companies m0/m1/m2, symmetric company sizes)
    for terrain, tag in (('trench', 'T'), ('open', 'O')):
        for r in (2, 3):
            for m in ((0, 1, 2) if (tag == 'T' and r == 2) else (2,) if (tag == 'T') else (0, 2) if r == 2 else ()):
                x, y = dpos(terrain)
                fs = [atk(2 * r, n=84), dfn(terrain, 2)]
                for i in range(m):
                    fs.append(mgco(f"f2_mg{i}", 2, B, (x + 6 * i - 3, y - 6), 180, MG2))
                add(f"MGD_{tag}_r{r}_m{m}", 'mgdef', terrain, fs, ratio=float(r), mgdef=m, seeds=SEEDS8 if (tag == 'T' and r == 2 and m in (0, 2)) else SEEDS)

    # ============ B. cavalry squadron hits the flank of an already-engaged line (arrives from the east edge at t=75s)
    for terrain, tag in (('open', 'O'), ('trench', 'T')):
        x, y = dpos(terrain)
        base = [atk(2, n=84), dfn(terrain, 2)]
        add(f"CAV_{tag}_r1_none", 'cavflank', terrain, base, ratio=1.0)
        for nc in (11,):
            add(f"CAV_{tag}_r1_c{nc + 1}", 'cavflank', terrain, base + [cav("f1_cav", 1, A, (148, y + 4), 270, nc, time=75)], ratio=1.0, cav=nc + 1)

    # ============ C. forest ambush: defender hidden in forest, attacker marching in column (vs line deployment)
    for terrain, tag in (('forest', 'F'), ('open', 'O')):
        x, y = dpos(terrain)
        for form in ('column', 'line'):
            fs = [bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, 2, form=form, n=84), dfn(terrain, 2)]
            if form == 'column':
                for c in fs[0]['companies']: c['formation'] = 'column'
                fs[0]['formation'] = 'column'
            if tag == 'O' and form == 'line': continue
            add(f"AMB_{tag}_{form}_r1", 'ambush', terrain, fs, ratio=1.0, form=form, seeds=SEEDS)
    x, y = dpos('forest')
    col = bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, 2, form='column', n=84)
    col['formation'] = 'column'
    for c in col['companies']: c['formation'] = 'column'
    add("AMB_F_column_night", 'ambush', 'forest', [copy.deepcopy(col), dfn('forest', 2)], ratio=1.0, form='column', time='night')

    # ============ D. night vs day assault on trenches (symmetric company size)
    for r in (2,):
        for tm in ('day', 'night'):
            add(f"NT_T_r{r}_{tm}", 'night', 'trench', [atk(2 * r, n=84), dfn('trench', 2)], ratio=float(r), time=tm, seeds=SEEDS8)

    # ============ E. counterattack: kr holds the trench; ft retakes from the north with / without a reserve battalion
    hx, hy = 80, 41
    holder = bn("f1_bn", 1, A, "한국군 점령 중대", (hx, hy), 180, 2, n=84)
    nb = lambda id_, cos, at, nbn, **k: bn(id_, 2, B, "봉천 반격대", at, 0, cos, n_bn=nbn, **{**dict(n=84), **k})
    add("CTR_front2", 'counter', 'trench_cap', [holder, nb("f2_bn", 2, (80, 12), 1)], p1='defend', p2='attack', ratio=1.0)
    add("CTR_res2", 'counter', 'trench_cap', [copy.deepcopy(holder), nb("f2_bn", 2, (80, 12), 1), nb("f2_res", 2, (80, 0), 2, form='reserve')], p1='defend', p2='attack', ratio=2.0)
    add("CTR_one4", 'counter', 'trench_cap', [copy.deepcopy(holder), nb("f2_bn", 4, (80, 12), 1)], p1='defend', p2='attack', ratio=2.0)

    # ============ F. pincer: attacker split to converge from different directions (total 4 companies on the trench / 2 in the open)
    def bn_at(id_, at, facing, cos, nbn): return bn(id_, 1, A, "한국군 대대", at, facing, cos, n_bn=nbn, n=84)
    add("PIN_T_front4", 'pincer', 'trench', [atk(4, n=84), dfn('trench', 2)], ratio=2.0)
    add("PIN_T_2S2E", 'pincer', 'trench', [bn_at("f1_bn", (ax, ay), 0, 2, 1), bn_at("f1_e", (140, 66), 270, 2, 2), dfn('trench', 2)], ratio=2.0)
    add("PIN_T_2S1E1W", 'pincer', 'trench', [bn_at("f1_bn", (ax, ay), 0, 2, 1), bn_at("f1_e", (140, 66), 270, 1, 2), bn_at("f1_w", (20, 66), 90, 1, 3), dfn('trench', 2)], ratio=2.0)
    add("PIN_O_front2", 'pincer', 'open', [atk(2, n=84), dfn('open', 2)], ratio=1.0)
    add("PIN_O_1S1E", 'pincer', 'open', [bn_at("f1_bn", (ax, ay), 0, 1, 1), bn_at("f1_e", (140, 66), 270, 1, 2), dfn('open', 2)], ratio=1.0)

    # ============ G. artillery duel with observers (symmetric: both sides 2 howitzers + 3 scouts + 2 infantry guns + 1 field gun)
    dx, dy = dpos('open')
    for tag, kw in (('full', dict(obs=True, how=2)), ('noobs', dict(obs=False, how=2))):
        fs = [atk(2, n=84), dfn('open', 2)] + guns("f1", 1, A, (ax, ay), 0, dirn=1, **kw) + guns("f2", 2, B, (dx, dy), 180, dirn=-1, **kw)
        add(f"ART_sym_{tag}", 'arty', 'open', fs, ratio=1.0, arty=tag)
    add("ART_duel4", 'arty', 'open', [atk(1, n=84), dfn('open', 1)] + guns("f1", 1, A, (ax, ay), 0, obs=True, how=4, ig=False, dirn=1) + guns("f2", 2, B, (dx, dy), 180, obs=True, how=4, ig=False, dirn=-1), ratio=1.0, arty='duel4')

    # ============ H. river / bridge
    for terrain, nm, r in (('river1', 'br1', 2), ('river2', 'br2', 2)):
        add(f"RIV_{nm}_r{r}", 'river', terrain, [atk(2 * r, n=84), dfn(terrain, 2)], ratio=float(r))

    # ============ I. asymmetric quality (recruit / infantry / veteran), meeting engagement on flat ground, equal headcount unless noted
    def q(id_, k1, k2, n1, n2, **kw):
        fs = [bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, n1[0], n=n1[1], inf=k1), bn("f2_bn", 2, B, "적 소총대대", (80, 20), 180, n2[0], n=n2[1], inf=k2)]
        add(id_, 'quality', 'meet_flat', fs, p1='attack', p2='attack', **kw)
    q("QL_eq", 'infantry', 'infantry_fengtian', (3, 84), (3, 84), ratio=1.0)
    q("QL_rec_v_vet", 'recruit', 'veteran', (3, 84), (3, 84), ratio=1.0)
    q("QL_rec1.5_v_vet", 'recruit', 'veteran', (3, 84), (2, 84), ratio=1.5, seeds=SEEDS8)   # 1.5:1 headcount in favour of the recruits

    # ============ J. organisation: officer-heavy vs NCO-led (squadLeaderRank: 0 none, 5 default NCO)
    def org(id_, o1=3, s1=None, o2=3, s2=None, env=None, **kw):
        fs = [bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, 3, n=84 - (o1 - 3), off=o1, slr=s1), bn("f2_bn", 2, B, "적 소총대대", (80, 20), 180, 3, n=84 - (o2 - 3), off=o2, slr=s2)]
        add(id_, 'org', 'meet_flat', fs, p1='attack', p2='attack', ratio=1.0, **({'env': env} if env else {}), **kw)
    org("ORG_base")
    org("ORG_kr_nco0", s1=0)
    org("ORG_kr_off6", o1=6)
    org("ORG_kr_off6_nco0", o1=6, s1=0)
    org("ORG_nco_killall", env={"KD_OBS_KILL": "all", "KD_OBS_KILLSIDE": "1", "KD_OBS_KILLDELAY": "30"})

    # ============ K. AI profile variants (existing profiles only), long attrition at low aggression
    def agg(id_, p1, p2, **kw):
        fs = [bn("f1_bn", 1, A, "한국군 소총대대", (ax, ay), 0, 3, n=84), bn("f2_bn", 2, B, "적 소총대대", (80, 20), 180, 3, n=84)]
        add(id_, 'aggr', 'meet_open', fs, p1='attack', p2='attack', prof1=p1, prof2=p2, ratio=1.0, **kw)
    agg("AGG_def_def", "defensive", "defensive")
    agg("AGG_agg_agg", "aggressive", "aggressive")

    # ============ L. symmetric composition (fix of campaign 1 asymmetry): equal MG counts, equal company sizes
    def sym(id_, mg, fac1='kr', fac2='ft', terrain='meet_open', **kw):
        fs = [bn("f1_bn", 1, fac1, "소총대대 1", (ax, ay), 0, 3, n=84), bn("f2_bn", 2, fac2, "소총대대 2", (80, 20), 180, 3, n=84)]
        if mg:
            fs.append(mgco("f1_mg0", 1, fac1, (ax - 3, ay + 8), 0, mg)); fs.append(mgco("f2_mg0", 2, fac2, (77, 12), 180, mg))
        add(id_, 'sym', terrain, fs, p1='attack', p2='attack', fac1=fac1, fac2=fac2, ratio=1.0, **kw)
    sym("SYM_R_krft", None, seeds=SEEDS8)
    sym("SYM_R_ftjp", None, fac1='ft', fac2='jp')        # same stats, different skin / position bias check
    sym("SYM_RM1_krft", MG1, seeds=SEEDS8)

    # ============ M. objective: take the position (capture trench zone 30 s) instead of annihilating
    capz = [60, 36, 40, 12]
    for r in (1, 2, 3):
        add(f"OBJ_T_r{r}_cap", 'objective', 'trench', [atk(2 * r, n=84), dfn('trench', 2)], ratio=float(r), obj='capture', capZone=capz, capSec=30, seeds=SEEDS)
    for c in L:
        if c['kind'] in ('quality', 'org', 'aggr', 'sym'): c['both'] = True
        if c['kind'] == 'counter': c['swap'] = True
    return L


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    L = cfgs()
    for c in L:
        json.dump(mission(c), open(os.path.join(out, c['id'] + '.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    json.dump({"seeds": SEEDS, "configs": L}, open(os.path.join(out, 'matrix.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    print(len(L), "configs,", sum(len(c['seeds']) for c in L), "runs")
