#!/usr/bin/env python3
"""Aggregate battalion campaign raw data -> <raw>/aggregate.json + <raw>/tables.md (Korean tables, 95% CI).

  python tools/bn_campaign/aggregate.py [--raw docs/bncamp_raw] [--filter REGEX]

CI: per-run values -> mean +- t(0.975, n-1) * sd / sqrt(n) (n = seeds, or runs when pooled). Pooled event-level
metrics (retreat events, shots) use a run-cluster bootstrap (2000 resamples, fixed RNG seed -> reproducible).
"""
import argparse, glob, gzip, json, math, os, random, re, statistics as st
from collections import defaultdict

T975 = {1: 12.706, 2: 4.303, 3: 3.182, 4: 2.776, 5: 2.571, 6: 2.447, 7: 2.365, 8: 2.306, 9: 2.262, 10: 2.228, 12: 2.179, 15: 2.131, 20: 2.086, 30: 2.042, 60: 2.0, 120: 1.98}


def tcrit(df):
    if df <= 0: return float('nan')
    for k in sorted(T975):
        if df <= k: return T975[k]
    return 1.96


def ms(xs):
    """mean, ci half width, n"""
    xs = [x for x in xs if x is not None and not (isinstance(x, float) and math.isnan(x))]
    n = len(xs)
    if n == 0: return (float('nan'), float('nan'), 0)
    m = sum(xs) / n
    if n < 2: return (m, float('nan'), n)
    sd = st.stdev(xs)
    return (m, tcrit(n - 1) * sd / math.sqrt(n), n)


def fmt(t, d=2, pct=False):
    m, h, n = t
    if n == 0 or math.isnan(m): return '-'
    k = 100 if pct else 1
    s = f"{m * k:.{d}f}{'%' if pct else ''}"
    if not math.isnan(h): s += f" ±{h * k:.{d}f}"
    return s


def boot(by_run, f, B=2000, seed=7):
    """cluster bootstrap over runs. by_run: list of lists of items; f(items)->float. Returns (mean, halfwidth, n_runs)."""
    runs = [r for r in by_run if r]
    if not runs: return (float('nan'), float('nan'), 0)
    allitems = [x for r in runs for x in r]
    m = f(allitems)
    if len(runs) < 2: return (m, float('nan'), len(runs))
    rng = random.Random(seed); vals = []
    for _ in range(B):
        samp = [x for _ in runs for x in runs[rng.randrange(len(runs))]]
        v = f(samp)
        if v == v: vals.append(v)
    if len(vals) < 10: return (m, float('nan'), len(runs))
    vals.sort(); lo, hi = vals[int(0.025 * len(vals))], vals[int(0.975 * len(vals)) - 1]
    return (m, (hi - lo) / 2, len(runs))


def pct(sorted_list, q):
    if not sorted_list: return float('nan')
    return sorted_list[min(len(sorted_list) - 1, int(q * (len(sorted_list) - 1)))]


# ---------------------------------------------------------------- loading
def load(raw, flt=None):
    runs = []
    for p in sorted(glob.glob(os.path.join(raw, 'runs', '*.json.gz'))):
        tag = os.path.basename(p)[:-8]
        cid, seed = tag.rsplit('_s', 1)
        if flt and not re.search(flt, cid): continue
        d = json.load(gzip.open(p, 'rt', encoding='utf8'))
        d['cid'] = cid; d['seed'] = int(seed)
        tp = p.replace('.json.gz', '.txt.gz')
        d['text'] = gzip.open(tp, 'rt', encoding='utf8').read() if os.path.exists(tp) else ''
        rc = d['rowCols']; d['R'] = [dict(zip(rc, r)) for r in d['rows']]
        ec = d['eventCols']; d['E'] = [dict(zip(ec, r)) for r in d['events']]
        d['C'] = d['counters']
        runs.append(d)
    return runs


def matrix(raw):
    m = json.load(open(os.path.join(raw, 'matrix.json'), encoding='utf8'))
    return {c['id']: c for c in m['configs']}


def cnt(run, side, key):
    return run['C'].get(f"{side}|{key}", 0.0)


# ---------------------------------------------------------------- text parsing (TacticsObs report)
def parse_text(txt):
    """per-side dict of battalion-tactics metrics parsed from the existing TacticsObs report."""
    out = {}
    parts = re.split(r'^## 세력 (\d+)', txt, flags=re.M)
    for i in range(1, len(parts), 2):
        side = int(parts[i]); body = parts[i + 1]
        o = {}
        m = re.search(r'중대 상태 시간\(중대-초\): (.*)', body)
        if m:
            o['stateSec'] = {k: float(v) for k, v in re.findall(r'(\w+) (\d+) \(', m.group(1))}
        m = re.search(r'역할 시간: (.*)', body)
        if m: o['roleSec'] = {k: float(v) for k, v in re.findall(r'(\w+) (\d+)', m.group(1))}
        m = re.search(r'중대 전술 틀 시간: (.*)', body)
        if m: o['tplSec'] = {k: float(v) for k, v in re.findall(r'(\w+) (\d+)', m.group(1))}
        m = re.search(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+) 우세 (\d+) 열세 (\d+); 미완 (\d+)', body)
        if m: o['assault'] = dict(done=int(m.group(1)), retreat=int(m.group(2)), up=int(m.group(3)), down=int(m.group(4)), open=int(m.group(5)))
        m = re.search(r'\[예비\] 투입 (\d+)회, 그중 곤경 .*? (\d+)회', body)
        if m: o['reserve'] = dict(n=int(m.group(1)), trouble=int(m.group(2)))
        m = re.search(r'ReserveCommits (\d+) \[([^\]]*)\]', body)
        if m: o['reserveT'] = [float(x) for x in re.findall(r'[\d.]+', m.group(2))]
        m = re.search(r'\[시간차\] 화력조 첫 사격 -> 기동 출발: 표본 (\d+), 사격 전 출발 (\d+), 2초 미만 (\d+), 중앙 ([\d.]+)s, 평균 ([\d.]+)s, 출발 때 화력조 사격 비율 평균 ([\d.]+)', body)
        if m: o['lag'] = dict(n=int(m.group(1)), before=int(m.group(2)), med=float(m.group(4)), mean=float(m.group(5)), ratio=float(m.group(6)))
        m = re.search(r'\[엄호\] 약진 중 \(적 가시\) 파트너 사격 비율 평균 ([\d.]+) \(표본 (\d+)초.*?기준: 교전 중 비약진 사격조 ([\d.]+)', body)
        if m: o['overwatch'] = dict(ratio=float(m.group(1)), n=int(m.group(2)), base=float(m.group(3)))
        m = re.search(r'\[틱시작\] (.*)', body)
        if m: o['tickStart'] = {k: int(v) for k, v in re.findall(r'(\S+?) (\d+)', m.group(1))}
        m = re.search(r'첫 교전 t=(-?\d+), 첫 기동\(약진/돌격\) t=(-?\d+)', body)
        if m: o['firstEngage'] = int(m.group(1)); o['firstManoeuvre'] = int(m.group(2))
        m = re.search(r'기동 개시 (\d+)회', body)
        if m: o['manoeuvres'] = int(m.group(1))
        m = re.search(r'유휴 중대\(30초\+.*?\): (\d+) 중대-초', body)
        if m: o['idle30'] = int(m.group(1))
        m = re.search(r'Drill 약진 다리: 횟수 (\d+)', body)
        if m: o['bounds'] = int(m.group(1))
        out[side] = o
    return out


# ---------------------------------------------------------------- derive
def derive(run, cfg):
    s1, s2 = run['start']['1'], run['start']['2']
    a1, a2 = run['alive']['1'], run['alive']['2']
    l1, l2 = 1 - a1 / s1, 1 - a2 / s2
    run['l1'], run['l2'] = l1, l2
    run['cfg'] = cfg
    run['att_win'] = 1.0 if l2 > l1 else 0.0
    run['win_dec'] = 1.0 if (l2 > l1 and l2 >= 0.7) else 0.0
    run['loser'] = 2 if l2 > l1 else 1
    run['loser_loss'] = max(l1, l2)
    run['ex'] = (l2 * s2) / max(1.0, l1 * s1)       # defender losses per attacker loss
    run['T'] = parse_text(run['text'])
    # end-type of the loser: final line companies
    fin = [g for g in run['finalGroups'] if g[0] == run['loser']]
    nfb = sum(1 for g in fin if g[2] == 'Fallback'); nal = sum(1 for g in fin if g[3] > 0)
    ll = run['loser_loss']
    if ll >= 0.9: run['end'] = '전멸(>=90%)'
    elif ll >= 0.7 and nal > 0 and nfb >= max(1, 0.5 * nal): run['end'] = '철수(>=70% 패자, 중대 절반 이상 후퇴)'
    elif nal > 0 and nfb >= max(1, 0.5 * nal): run['end'] = '철수(<70%)'
    elif run['dur'] >= 590: run['end'] = '시간 종료(미결)'
    else: run['end'] = '전투 종료(기타)'
    return run


# ---------------------------------------------------------------- tables
OUT = []
KINDS = ('meet', 'assault', 'defence', 'flank')   # campaign 2 sets this from the matrix (main)


def P(s=''): OUT.append(s)


def table(headers, rows):
    P('| ' + ' | '.join(headers) + ' |'); P('|' + '|'.join(['---'] * len(headers)) + '|')
    for r in rows: P('| ' + ' | '.join(str(x) for x in r) + ' |')
    P()


def by_cfg(runs):
    d = defaultdict(list)
    for r in runs: d[r['cid']].append(r)
    return d


def sec_outcomes(runs, cfgs):
    P('## 1. 결과 (시나리오별, 시드 평균 ±95% CI)'); P()
    P('한국군(1) = 공격측 (조우전은 양측 공격). 승 = 최종 손실률이 더 낮은 쪽. 결정적 승 = 승 + 패자 손실 70% 이상. 교환비 = 적 손실 / 아군 손실.'); P()
    rows = []
    for cid, rs in by_cfg(runs).items():
        c = cfgs[cid]
        rows.append([cid, len(rs), fmt(ms([r['att_win'] for r in rs]), 2), fmt(ms([r['win_dec'] for r in rs]), 2), fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True),
                     fmt(ms([r['ex'] for r in rs]), 2), fmt(ms([r['dur'] for r in rs]), 0), fmt(ms([r['perf']['stepMean'] for r in rs]), 2)])
    table(['시나리오', 'n', '한국 승률', '결정적 승', '한국 손실', '적 손실', '교환비', '길이(초)', '틱 ms 평균'], rows)


def sec_ratio(runs, cfgs):
    P('## 2. 병력비 (공격:방어 소총중대 비) — "참호 3:1" 교리 검증'); P()
    P('방어 소총중대 2 (약 190명) 고정, 공격 소총중대 2·3·4·6·8 개. 병력 수 비는 한국 87명 / 봉천 95명이라 명목비의 0.92배. 참호(T) 대 평지(D_open) 비교.'); P()
    rows = []
    for fam, label in (('T_r', '참호선(T)'), ('D_open_r', '평지(D_open)'), ('D_town', '마을(D_town)')):
        for cid, rs in sorted(by_cfg(runs).items()):
            if not cid.startswith(fam) or '_RM' != cid[-3:]: continue
            c = cfgs[cid]
            men = [(r['start']['1'] / r['start']['2']) for r in rs]
            rows.append([label, cid, f"{c.get('ratio', '')}", f"{st.mean(men):.2f}", len(rs), fmt(ms([r['att_win'] for r in rs]), 2), fmt(ms([r['win_dec'] for r in rs]), 2),
                         fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['ex'] for r in rs]), 2), fmt(ms([r['dur'] for r in rs]), 0)])
    table(['지형', '시나리오', '명목비', '실병력비', 'n', '공격 승률', '결정적 승', '공격 손실', '방어 손실', '교환비(방어손실/공격손실)', '길이(초)'], rows)


def role_of(cfg, side):
    if cfg['kind'] == 'meet' or cfg.get('both'): return 'both'
    if cfg.get('swap'): return 'def' if side == 1 else 'att'
    return 'att' if side == 1 else 'def'


def sec_company(runs, cfgs):
    P('## 3. 중대 행동 (소총 중대, 2초 표본)'); P()
    groups = defaultdict(list)
    for r in runs:
        for side in (1, 2): groups[(r['cfg']['kind'], role_of(r['cfg'], side))].append((r, side))
    P('### 3-1. 상태 시간 점유율'); P()
    states = ['Advance', 'Approach', 'Engage', 'Defend', 'Fallback', 'Reserve']
    rows = []
    for (kind, role), lst in sorted(groups.items()):
        sh = {s: [] for s in states}
        for r, side in lst:
            rows_ = [x for x in r['R'] if x['side'] == side]
            if not rows_: continue
            for s in states: sh[s].append(sum(1 for x in rows_ if x['state'] == s) / len(rows_))
        rows.append([kind, role, len(lst)] + [fmt(ms(sh[s]), 1, True) for s in states])
    table(['종류', '역할', 'n(측-런)', *states], rows)
    P('### 3-2. 정면 폭·종심, 간격, 밀집, 엄폐, 측면, 유휴, 막힘'); P()
    rows = []
    for (kind, role), lst in sorted(groups.items()):
        vals = defaultdict(list)
        for r, side in lst:
            rs = [x for x in r['R'] if x['side'] == side]
            if not rs: continue
            eng = [x for x in rs if x['state'] in ('Engage', 'Defend')]
            vals['width'].append(st.mean(x['width'] for x in rs)); vals['depth'].append(st.mean(x['depth'] for x in rs))
            vals['wE'].append(st.mean(x['width'] for x in eng) if eng else None)
            vals['nn'].append(st.mean(x['nn'] for x in rs)); vals['clump'].append(st.mean(x['clump'] for x in rs))
            vals['clumpE'].append(st.mean(x['clump'] for x in eng) if eng else None)
            uf = sum(x['underFire'] for x in rs)
            if uf > 0:
                vals['trench'].append(sum(x['cTrench'] for x in rs) / uf); vals['forest'].append(sum(x['cForest'] for x in rs) / uf); vals['other'].append(sum(x['cOther'] for x in rs) / uf)
            fl = [x['flank'] for x in rs if x['flank'] >= 0]
            if fl: vals['flank'].append(sum(1 for f in fl if f >= 1) / len(fl)); vals['rear'].append(sum(1 for f in fl if f == 2) / len(fl))
            vals['idleOOR'].append(sum(x['idleOOR'] for x in rs) / len(rs)); vals['idleIn'].append(sum(x['idleIn'] for x in rs) / len(rs))
            jam = [x['stuck'] for x in rs]; vals['jam'].append(sum(1 for j in jam if j >= 0.2) / len(jam)); vals['stuck'].append(st.mean(jam))
        rows.append([kind, role, fmt(ms(vals['width']), 1), fmt(ms(vals['wE']), 1), fmt(ms(vals['depth']), 1), fmt(ms(vals['nn']), 2), fmt(ms(vals['clump']), 1, True), fmt(ms(vals['clumpE']), 1, True),
                     fmt(ms(vals['trench']), 1, True), fmt(ms(vals['forest']), 1, True), fmt(ms(vals['other']), 1, True), fmt(ms(vals['flank']), 1, True), fmt(ms(vals['rear']), 1, True),
                     fmt(ms(vals['idleOOR']), 1, True), fmt(ms(vals['idleIn']), 1, True), fmt(ms(vals['jam']), 1, True), fmt(ms(vals['stuck']), 1, True)])
    table(['종류', '역할', '폭(칸)', '교전 중 폭', '종심', '최근접 간격', '1칸내 밀집', '교전 중 밀집', '참호', '숲', '기타 엄폐', '측면(>60도)', '후방(>100도)', '유휴(사거리 밖)', '유휴(사거리 안, 무사격)', '막힘 중대 비율', '명령 중 정지 병사'], rows)
    P('엄폐 = 최근 5초 안 피격된 병사 중 참호 / 숲 / 기타(가옥 옆 · 잔해 · 구덩이) 칸에 선 비율. 측면 = 중대를 노리는 적 사수(3명 이상)의 무게중심 방향이 중대 정면(마지막 이동 도착 방향)과 이룬 각 60도 초과.'); P()


def sec_battalion(runs, cfgs):
    P('## 4. 대대 전술'); P()
    groups = defaultdict(list)
    for r in runs:
        for side in (1, 2): groups[(r['cfg']['kind'], role_of(r['cfg'], side))].append((r, side))
    rows = []
    for (kind, role), lst in sorted(groups.items()):
        v = defaultdict(list)
        for r, side in lst:
            T = r['T'].get(side, {})
            rs = T.get('roleSec')
            if rs:
                tot = sum(rs.values()) or 1
                for k in ('BaseOfFire', 'Manoeuvre', 'Line', 'MainLine'): v['role_' + k].append(rs.get(k, 0) / tot)
            if 'assault' in T and T['assault']['done'] > 0: v['asRetreat'].append(T['assault']['retreat'] / T['assault']['done']); v['asUp'].append(T['assault']['up'] / T['assault']['done']); v['asN'].append(T['assault']['done'])
            if 'reserve' in T: v['resN'].append(T['reserve']['n']); v['resTrouble'].append(T['reserve']['trouble'] / T['reserve']['n'] if T['reserve']['n'] else None)
            for t in T.get('reserveT', []): v['resT'].append(t)
            if 'lag' in T and T['lag']['n'] > 0: v['lag'].append(T['lag']['med'])
            if 'overwatch' in T and T['overwatch']['n'] > 0: v['ow'].append(T['overwatch']['ratio']); v['owBase'].append(T['overwatch']['base'])
            if 'firstEngage' in T and T['firstEngage'] >= 0: v['firstEng'].append(T['firstEngage'])
            if 'tickStart' in T:
                ts = T['tickStart']; tot = sum(ts.values())
                if tot: v['timerShare'].append(sum(n for k, n in ts.items() if ':타이머' in k) / tot); v['signalShare'].append(sum(n for k, n in ts.items() if ':신호' in k) / tot)
            rs2 = [x for x in r['R'] if x['side'] == side]
        sup = defaultdict(float)
        rows.append([kind, role, len(lst), fmt(ms(v['role_BaseOfFire']), 1, True), fmt(ms(v['role_Manoeuvre']), 1, True), fmt(ms(v['asN']), 1), fmt(ms(v['asRetreat']), 1, True), fmt(ms(v['asUp']), 1, True),
                     fmt(ms(v['resN']), 2), fmt(ms(v['resTrouble']), 0, True), fmt(ms(v['resT']), 0), fmt(ms(v['lag']), 1), fmt(ms(v['ow']), 2), fmt(ms(v['owBase']), 2), fmt(ms(v['timerShare']), 0, True), fmt(ms(v['signalShare']), 0, True)])
    table(['종류', '역할', 'n', '화력기반 시간', '기동 시간', '돌격 판 수/측', '돌격 후 40초 내 후퇴', '돌격 우세', '예비 투입 수', '곤경 중 투입', '투입 시각(초)', '화력→기동 시간차 중앙(초)', '약진 중 엄호 사격 비', '기준 사격 비', '약진 시작 타이머 비', '약진 시작 제압신호 비'], rows)
    P('### 4-2. 중대 간 상호 지원 거리 · 전선 틈 (활성 소총 중대, 병력 25% 이상)'); P()
    rows = []
    for (kind, role), lst in sorted(groups.items()):
        v = defaultdict(list)
        for r, side in lst:
            n = cnt(r, side, 'sup.n')
            if n > 0: v['d'].append(cnt(r, side, 'sup.dist') / n); v['w24'].append(cnt(r, side, 'sup.within24') / n); v['w40'].append(cnt(r, side, 'sup.within40') / n)
            g = cnt(r, side, 'gap.n')
            if g > 0: v['gmean'].append(cnt(r, side, 'gap.mean') / g); v['gmax'].append(cnt(r, side, 'gap.max') / g); v['g20'].append(cnt(r, side, 'gap.over20') / g); v['g40'].append(cnt(r, side, 'gap.over40') / g)
        rows.append([kind, role, fmt(ms(v['d']), 1), fmt(ms(v['w24']), 0, True), fmt(ms(v['w40']), 0, True), fmt(ms(v['gmean']), 1), fmt(ms(v['gmax']), 1), fmt(ms(v['g20']), 0, True), fmt(ms(v['g40']), 0, True)])
    table(['종류', '역할', '최근접 아군 중대 거리', '24칸 안(소총 사거리)', '40칸 안', '전선 틈 평균(칸)', '최대 틈 평균', '틈>20칸 시간', '틈>40칸 시간'], rows)


WCLS = ['rifle', 'lmg', 'hmg', 'gun', 'howitzer', 'sniper', 'pistol', 'smg']


def hist_stats(hists):
    tot = [0] * 101
    for h in hists:
        for i, c in enumerate(h): tot[i] += c
    n = sum(tot)
    if n == 0: return None
    cum = 0; q = {}
    for i, c in enumerate(tot):
        cum += c
        for name, qq in (('med', 0.5), ('p90', 0.9), ('p99', 0.99)):
            if name not in q and cum >= qq * n: q[name] = i / 100
    ab = lambda t: sum(tot[int(t * 100):]) / n
    return dict(n=n, med=q.get('med', 0), p90=q.get('p90', 0), p99=q.get('p99', 0), a02=ab(0.2), a05=ab(0.5), a08=ab(0.8), mean=sum(i / 100 * c for i, c in enumerate(tot)) / n)


def sec_supp(runs, cfgs):
    P('## 5. 제압 적정성 (재설계 핵심 자료)'); P()
    P('### 5-1. 무기별 제압 가산 (표적 한 명 기준, 엄폐 · 저항 적용 후)'); P()
    P('활동 사수-초 = 최근 5초 안에 1발 이상 쏜 무장 유닛의 초 합 (실제 교전에 참가한 사수가 1초에 받는 제압을 표적에 얹는 양). 무장-초 = 살아 있는 무장 유닛 수의 합 (사격하지 않는 시간 포함). 제압 단위 = 0..1000 (SuppressMax), 1000 = 비율 1.0.'); P()
    rows = []
    for cls in WCLS:
        sh = sum(cnt(r, s, 'shots.' + cls) for r in runs for s in (1, 2))
        if sh < 20: continue
        hit = sum(cnt(r, s, 'hits.' + cls) for r in runs for s in (1, 2))
        gain = sum(cnt(r, s, 'gain.' + cls) for r in runs for s in (1, 2)); gn = sum(cnt(r, s, 'gainN.' + cls) for r in runs for s in (1, 2))
        fsec = sum(cnt(r, s, 'firersec.' + cls) for r in runs for s in (1, 2)); actsec = sum(cnt(r, s, 'actsec.' + cls) for r in runs for s in (1, 2)); asec = sum(cnt(r, s, 'armedsec.' + cls) for r in runs for s in (1, 2))
        # per-run CI of gain per firer-second
        per = []; per_hit = []
        for r in runs:
            f_ = sum(cnt(r, s, 'actsec.' + cls) for s in (1, 2))
            if f_ >= 20: per.append(sum(cnt(r, s, 'gain.' + cls) for s in (1, 2)) / f_)
            s_ = sum(cnt(r, s, 'shots.' + cls) for s in (1, 2))
            if s_ >= 20 and cls not in ('gun', 'howitzer'): per_hit.append(sum(cnt(r, s, 'hits.' + cls) for s in (1, 2)) / s_)
        rows.append([cls, int(sh), fmt(ms(per_hit), 1, True) if per_hit else '-', f"{gain / max(1, gn):.1f}", f"{gain / max(1, sh):.1f}", fmt(ms(per), 1), f"{gain / max(1, asec):.2f}", f"{fsec / max(1, asec):.3f}"])
    table(['무기', '사격(발/착탄)', '명중률', '표적당 가산(1발)', '발당 가산(전체)', '활동 사수-초당 제압/초 (±CI)', '무장-초당 제압/초', '무장 중 사격한 시간 비'], rows)
    P('### 5-2. 교전 중 병사 제압 분포 (최근 5초 안 피격된 소총병 · 비장교, 2초 표본)'); P()
    rows = []
    for (kind, role), _ in sorted({(r['cfg']['kind'], role_of(r['cfg'], s)): 0 for r in runs for s in (1, 2)}.items()):
        for label, hk in (('교전 중', 'supEng'), ('전체 보병', 'supAll')):
            hs = [r['hist'].get(f'{s}|{hk}') for r in runs if r['cfg']['kind'] == kind for s in (1, 2) if role_of(r['cfg'], s) == role and r['hist'].get(f'{s}|{hk}')]
            S = hist_stats(hs)
            if not S: continue
            per02 = []; per05 = []; per08 = []
            for r in runs:
                if r['cfg']['kind'] != kind: continue
                for s in (1, 2):
                    if role_of(r['cfg'], s) != role: continue
                    h = r['hist'].get(f'{s}|{hk}')
                    if h and sum(h) >= 30:
                        n = sum(h); per02.append(sum(h[20:]) / n); per05.append(sum(h[50:]) / n); per08.append(sum(h[80:]) / n)
            rows.append([kind, role, label, S['n'], f"{S['mean']:.3f}", f"{S['med']:.2f}", f"{S['p90']:.2f}", f"{S['p99']:.2f}", fmt(ms(per02), 1, True), fmt(ms(per05), 1, True), fmt(ms(per08), 2, True)])
    table(['종류', '역할', '모집단', '표본 병사-초', '평균', '중앙', 'p90', 'p99', '>=0.2 시간', '>=0.5 시간', '>=0.8 시간'], rows)
    P('### 5-3. 정확도 대 제압 (소총, 사수 자신의 제압 구간별)'); P()
    P('엔진 코드 확인: `World.HitChance` (Combat2.cs:106) = 거리 곡선 x 유닛 명중률 x 행진 사격 x 지형 x 날씨. 사수 제압은 들어 있지 않다. 제압의 효과는 사격 간격 배수 `FireIntervalMul` (1 + suppressFireSlow x 제압비, 최대 2배)와 이동 속도 (suppressMoveSlow 0.5), 멍칫 (>=400), 행진 사격 불가 (>=400), 자동 돌격 불가 (>=400)뿐. 아래는 자료 확인.'); P()
    bl = ['<0.05', '0.05-0.2', '0.2-0.5', '>=0.5']
    rows = []
    for key, label in (('sb', '전 사거리'), ('sb2', '사거리 12-24 칸')):
        for b in range(4):
            sh = [cnt(r, s, f'{key}s.{b}') for r in runs for s in (1, 2)]; hi = [cnt(r, s, f'{key}h.{b}') for r in runs for s in (1, 2)]
            by_run = [[(cnt(r, 1, f'{key}s.{b}') + cnt(r, 2, f'{key}s.{b}'), cnt(r, 1, f'{key}h.{b}') + cnt(r, 2, f'{key}h.{b}'))] for r in runs]
            m = boot(by_run, lambda it: sum(h for _, h in it) / max(1, sum(s for s, _ in it)))
            rows.append([label, bl[b], int(sum(sh)), fmt(m, 1, True)])
    table(['범위', '사수 제압', '사격 수', '명중률 (±CI, 런 단위 bootstrap)'], rows)
    P('사격률 (교전 중 소총병이 최근 1초 안 쏜 비율, 제압 구간별):'); P()
    rows = []
    for b in range(4):
        sec = sum(cnt(r, s, f'sbsec.{b}') for r in runs for s in (1, 2)); fire = sum(cnt(r, s, f'sbfire.{b}') for r in runs for s in (1, 2))
        by_run = [[(cnt(r, 1, f'sbsec.{b}') + cnt(r, 2, f'sbsec.{b}'), cnt(r, 1, f'sbfire.{b}') + cnt(r, 2, f'sbfire.{b}'))] for r in runs]
        m = boot(by_run, lambda it: sum(h for _, h in it) / max(1, sum(s for s, _ in it)))
        rows.append([bl[b], int(sec), fmt(m, 1, True)])
    table(['사수 제압', '병사-초(교전 중)', '1초 안 사격 비율'], rows)
    P('### 5-4. 근접 탄착(가상) — 빗나간 소총탄이 표적 외 적 병사 근처를 지난 수'); P()
    P('이 엔진에는 탄도 객체가 없고 소총탄은 표적 한 명만 제압한다. 아래는 사선(사수-착탄 선분) 1.5칸 안 또는 착탄점 1.5칸 안에 있던 표적 외 적 보병 수로, 제압을 받지 못한 "스쳐 지나간 탄"의 잠재량이다.'); P()
    ms_ = sum(cnt(r, s, 'nm.shots') for r in runs for s in (1, 2))
    if ms_ > 0:
        passing = sum(cnt(r, s, 'nm.passing') for r in runs for s in (1, 2)); near = sum(cnt(r, s, 'nm.impactNear') for r in runs for s in (1, 2)); withp = sum(cnt(r, s, 'nm.shotsWithPassing') for r in runs for s in (1, 2))
        sprayN = sum(cnt(r, s, 'nm.sprayNeighbors') for r in runs for s in (1, 2))
        table(['빗나간 소총탄', '사선 1.5칸 안 표적 외 적/발', '1명 이상 지난 발 비율', '착탄점 1.5칸 안 적/발', '기관총 빗나감 때 추가 제압 이웃'], [[int(ms_), f"{passing / ms_:.2f}", f"{withp / ms_ * 100:.0f}%", f"{near / ms_:.2f}", int(sprayN)]])
    P('### 5-5. 제압 수지 (가산 · 감쇠 · 낭비)'); P()
    rows = []
    for (kind, role), _ in sorted({(r['cfg']['kind'], role_of(r['cfg'], s)): 0 for r in runs for s in (1, 2)}.items()):
        g = []; d = []; low = []; cap = []
        for r in runs:
            if r['cfg']['kind'] != kind: continue
            for s in (1, 2):
                if role_of(r['cfg'], s) != role: continue
                go = cnt(r, s, 'gain.obs'); dt = cnt(r, s, 'dec.total')
                if go > 1000:
                    g.append(go); d.append(dt / go); low.append(cnt(r, s, 'dec.low') / max(1, dt)); cap.append(cnt(r, s, 'cap.unitTicks') / 60.0)
        rows.append([kind, role, len(g), fmt(ms(d), 1, True), fmt(ms(low), 1, True), fmt(ms(cap), 0)])
    table(['종류', '역할', 'n', '가산 중 감쇠로 사라진 비율(관측 증가분 기준)', '감쇠 중 제압 <0.2 구간에서 사라진 비율', '상한(1000) 병사-초'], rows)
    P('### 5-6. 후퇴 직전 중대 제압 (Fallback 진입 사건)'); P()
    rows = []
    for (kind, role), _ in sorted({(r['cfg']['kind'], role_of(r['cfg'], s)): 0 for r in runs for s in (1, 2)}.items()):
        ev = [[e for e in r['E'] if e['to'] == 'Fallback' and e['from'] != 'Fallback' and e['side'] in (1, 2) and role_of(r['cfg'], e['side']) == role] for r in runs if r['cfg']['kind'] == kind]
        N = sum(len(x) for x in ev)
        if N < 3: continue
        row = [kind, role, N]
        for f in ('sup10', 'sup30', 'supEngage', 'frac02'):
            row.append(fmt(boot(ev, lambda it, f=f: st.mean(e[f] for e in it)), 3))
        row.append(fmt(boot(ev, lambda it: st.mean(e['sup10'] >= 0.2 for e in it)), 2))
        rows.append(row)
    table(['종류', '역할', '후퇴 사건', '중대 평균 제압 직전 10초', '직전 30초', '교전 구간 평균', '제압>=0.2 병사 비율', '직전 10초 평균>=0.2 인 후퇴 비율'], rows)


def sec_morale(runs, cfgs):
    P('## 6. 사기 대용 지표'); P()
    P('후퇴 = 전술 AI 중대 상태가 Fallback 으로 바뀐 사건. 중단 = Advance 에서 Engage / Defend 로 바뀐 사건(전진 정지). 사상률 = 1 - 현재 소총병 / 처음 소총병.'); P()
    allev = lambda f: [[e for e in r['E'] if f(e, r)] for r in runs]
    rows = []
    for kind in KINDS:
        for role in ('both', 'att', 'def'):
            ev = [[e for e in r['E'] if e['to'] == 'Fallback' and e['from'] != 'Fallback' and role_of(r['cfg'], e['side']) == role] for r in runs if r['cfg']['kind'] == kind]
            N = sum(len(x) for x in ev)
            if N < 3: continue
            cas = lambda it: st.mean(1 - e['alive'] / max(1, e['ini']) for e in it)
            med = lambda it: sorted(1 - e['alive'] / max(1, e['ini']) for e in it)[len(it) // 2]
            stop = [[e for e in r['E'] if e['from'] == 'Advance' and e['to'] in ('Engage', 'Defend') and role_of(r['cfg'], e['side']) == role] for r in runs if r['cfg']['kind'] == kind]
            rows.append([kind, role, N, fmt(boot(ev, cas), 1, True), f"{boot(ev, med)[0] * 100:.0f}%", fmt(boot(ev, lambda it: st.mean(1 - e['alive'] / max(1, e['ini']) <= 0.1 for e in it)), 0, True),
                         fmt(boot(stop, lambda it: st.mean(1 - e['alive'] / max(1, e['ini']) for e in it) if it else float('nan')), 1, True) if sum(len(x) for x in stop) >= 3 else '-'])
    table(['종류', '역할', '후퇴 사건', '후퇴 때 사상률 평균', '중앙', '사상 10% 이하에서 후퇴', '전진 중단 때 사상률'], rows)
    P('### 6-2. 첫 사상자에서 후퇴까지, 전파(contagion), 장교'); P()
    # time from first casualty to retreat
    d = []
    for r in runs:
        d.append([e['t'] - e['firstCasT'] for e in r['E'] if e['to'] == 'Fallback' and e['from'] != 'Fallback' and e['firstCasT'] >= 0])
    m = boot(d, lambda it: st.mean(it)); mm = boot(d, lambda it: sorted(it)[len(it) // 2])
    P(f"- 중대의 첫 사상자에서 첫 후퇴 사건까지 (모든 후퇴 사건) 평균 {fmt(m, 0)}초, 중앙 {mm[0]:.0f}초 (n={sum(len(x) for x in d)})."); P()
    # first retreat per company
    firsts = []
    for r in runs:
        seen = {};
        for e in r['E']:
            if e['to'] == 'Fallback' and e['from'] != 'Fallback' and (e['side'], e['gid']) not in seen and e['firstCasT'] >= 0: seen[(e['side'], e['gid'])] = e['t'] - e['firstCasT']
        firsts.append(list(seen.values()))
    P(f"- 중대별 *첫* 후퇴까지: 평균 {fmt(boot(firsts, st.mean), 0)}초 (n={sum(len(x) for x in firsts)})."); P()
    # contagion: after a retreat start of one company, probability another friendly line company begins retreating within W s; baseline = same window anywhere
    W = 20
    hit = []; base = []
    for r in runs:
        per = defaultdict(list)
        for e in r['E']:
            if e['to'] == 'Fallback' and e['from'] != 'Fallback': per[e['side']].append((e['t'], e['gid']))
        locs = {}
        for x in r['R']: locs[(x['side'], x['gid'], x['t'])] = (x['cx'], x['cy'])
        hh = []; bb = []
        ncos = defaultdict(set)
        for x in r['R']: ncos[x['side']].add(x['gid'])
        for side, lst in per.items():
            ts = sorted(lst)
            for (t, g) in ts:
                others = [(t2, g2) for (t2, g2) in ts if g2 != g and t < t2 <= t + W]
                hh.append(1.0 if others else 0.0)
            # baseline: random window of W seconds across [first row .. last row] where a different company retreats: rate per company-window
            tmax = max(x['t'] for x in r['R']) if r['R'] else 0
            nco = max(1, len(ncos[side]) - 1)
            if tmax > W and len(ncos[side]) > 1:
                total_retreats = len(ts)
                # expected number of other-company retreat starts in a W window under uniform arrivals, probability >=1 (Poisson)
                lam = total_retreats * (nco / (nco + 1)) * W / tmax
                bb.append(1 - math.exp(-lam))
        hit.append(hh); base.append(bb)
    mh = boot(hit, st.mean); mb = (st.mean([x for b in base for x in b]) if any(base) else float('nan'))
    P(f"- 전파: 한 중대가 후퇴를 시작한 뒤 {W}초 안에 *다른* 아군 소총 중대가 후퇴를 시작한 비율 {fmt(mh, 0, True)} (n={sum(len(x) for x in hit)}). 후퇴가 시간에 균일하게 흩어졌다고 가정한 기준(Poisson) {mb * 100:.0f}%."); P()
    # officer effect
    rows = []
    for label, f in (('장교 전원 생존', lambda x: x['off'] >= x['iniOff'] and x['iniOff'] > 0), ('장교 일부 사망', lambda x: 0 < x['off'] < x['iniOff']), ('장교 전멸', lambda x: x['off'] == 0 and x['iniOff'] > 0)):
        v = defaultdict(list)
        for r in runs:
            rs = [x for x in r['R'] if f(x) and x['state'] != 'Idle']
            if len(rs) < 10: continue
            n = len(rs)
            for s in ('Engage', 'Advance', 'Approach', 'Defend', 'Fallback'): v[s].append(sum(1 for x in rs if x['state'] == s) / n)
            v['fire'].append(st.mean(x['firing'] / max(1, x['alive']) for x in rs if x['underFire'] > 0 or x['nearestEnemy'] < 30) if any(x['underFire'] > 0 or x['nearestEnemy'] < 30 for x in rs) else None)
            v['clump'].append(st.mean(x['clump'] for x in rs)); v['n'].append(n)
        rows.append([label, len(v['n']), fmt(ms(v['Engage']), 0, True), fmt(ms(v['Advance']), 0, True), fmt(ms(v['Defend']), 0, True), fmt(ms(v['Fallback']), 1, True), fmt(ms(v['fire']), 0, True), fmt(ms(v['clump']), 1, True)])
    table(['중대 장교 상태', '런 수', 'Engage', 'Advance', 'Defend', 'Fallback', '교전 근처 사격 병사 비', '1칸 밀집'], rows)
    ret = defaultdict(list)
    for r in runs:
        for e in r['E']:
            if e['to'] == 'Fallback' and e['from'] != 'Fallback' and e['iniOff'] > 0: ret['all' if e['off'] > 0 else 'dead'].append((1 - e['alive'] / max(1, e['ini'])))
    if ret['dead'] and ret['all']:
        P(f"- 후퇴 시 사상률: 장교 1명 이상 생존 {st.mean(ret['all']) * 100:.0f}% (n={len(ret['all'])}), 장교 전멸 {st.mean(ret['dead']) * 100:.0f}% (n={len(ret['dead'])})."); P()
    P('### 6-2b. 중대별 *첫* 후퇴 시점 (사상률 · 최근 30초 손실 · 국지 병력비)'); P()
    rows = []
    for kind in KINDS:
        for role in ('both', 'att', 'def'):
            ev = []
            for r in runs:
                if r['cfg']['kind'] != kind: continue
                seen = set(); lst = []
                for e in r['E']:
                    if e['to'] == 'Fallback' and e['from'] != 'Fallback' and role_of(r['cfg'], e['side']) == role and (e['side'], e['gid']) not in seen:
                        seen.add((e['side'], e['gid'])); lst.append(e)
                ev.append(lst)
            N = sum(len(x) for x in ev)
            if N < 3: continue
            cs = sorted(1 - e['alive'] / max(1, e['ini']) for x in ev for e in x)
            rows.append([kind, role, N, fmt(boot(ev, lambda it: st.mean(1 - e['alive'] / max(1, e['ini']) for e in it)), 1, True), f"{pct(cs, .1) * 100:.0f}% / {pct(cs, .5) * 100:.0f}% / {pct(cs, .9) * 100:.0f}%",
                         fmt(boot(ev, lambda it: st.mean(e['lost30'] for e in it)), 1, True), fmt(boot(ev, lambda it: st.mean(e['foe30'] / max(1, e['alive']) for e in it)), 2), fmt(boot(ev, lambda it: st.mean(e['t'] for e in it)), 0)])
    table(['종류', '역할', '첫 후퇴 중대 수', '그때 사상률 평균', 'p10 / 중앙 / p90', '최근 30초 손실(처음 병력 대비)', '반경 30칸 적 / 아군 병력', '시각(초)'], rows)
    P('### 6-3. 전투의 끝'); P()
    rows = []
    for kind in KINDS:
        rs = [r for r in runs if r['cfg']['kind'] == kind]
        if not rs: continue
        ends = defaultdict(int)
        for r in rs: ends[r['end']] += 1
        rows.append([kind, len(rs), fmt(ms([r['loser_loss'] for r in rs]), 0, True), fmt(ms([1.0 if r['loser_loss'] > 0.8 else 0.0 for r in rs]), 0, True)] + [f"{ends[k]}" for k in ('전멸(>=90%)', '철수(>=70% 패자, 중대 절반 이상 후퇴)', '철수(<70%)', '시간 종료(미결)', '전투 종료(기타)')])
    table(['종류', 'n', '패자 손실 평균', '패자 손실>80% 비율 (끝까지 싸움)', '전멸(>=90%)', '철수(>=70%)', '철수(<70%)', '시간 종료(미결)', '기타'], rows)


def sec_tempo(runs, cfgs):
    P('## 7. 템포'); P()
    rows = []
    for kind in KINDS:
        rs = [r for r in runs if r['cfg']['kind'] == kind]
        if not rs: continue
        v = defaultdict(list)
        for r in rs:
            v['fs'].append(r['firstShot'] if r['firstShot'] >= 0 else None); v['fc'].append(r['firstCharge'] if r['firstCharge'] >= 0 else None); v['cas'].append(r['firstCas'] if r['firstCas'] >= 0 else None)
            th = [t for t in r['tHalf'].values()]
            v['half'].append(min(th) if th else None)
            kb = r['killBins']
            if len(kb) >= 4:
                tot = [a + b for _, a, b in kb]
                w3 = [(sum(tot[i:i + 3]), kb[i + 1][0]) for i in range(len(tot) - 2)]
                best = max(w3); v['peak'].append(best[1]); v['peakRate'].append(best[0] / 30.0 * 60.0)
            v['dur'].append(r['dur'])
            fs = r['firstShot'] if r['firstShot'] >= 0 else None
            if fs is not None and r['tHalf']:
                v['dec_after'].append(min(r['tHalf'].values()) - fs)
        rows.append([kind, len(rs), fmt(ms(v['fs']), 0), fmt(ms(v['cas']), 0), fmt(ms(v['fc']), 0), fmt(ms(v['peak']), 0), fmt(ms(v['peakRate']), 0), fmt(ms(v['half']), 0), fmt(ms(v['dec_after']), 0), fmt(ms(v['dur']), 0)])
    table(['종류', 'n', '첫 사격(초)', '첫 사상', '첫 돌격(8명+)', '사상 최대 구간 중심', '그때 분당 사상(양측)', '한쪽 50% 미만(초)', '첫 사격 → 결정적 순간', '전체 길이'], rows)
    P('### 7-2. 국면 (접근 / 교전 / 돌격 / 추격)'); P()
    P('접근 = 0 → 첫 사격, 교전 = 첫 사격 → 첫 돌격 (돌격 없으면 한쪽 50% 미만 또는 끝), 돌격 = 첫 돌격 → 한쪽 50% 미만 (없으면 끝), 추격 = 한쪽 50% 미만 → 끝.'); P()
    rows = []
    for kind in KINDS:
        rs = [r for r in runs if r['cfg']['kind'] == kind]
        if not rs: continue
        v = defaultdict(list)
        for r in rs:
            end = r['dur']; fs = r['firstShot'] if r['firstShot'] >= 0 else end
            half = min(r['tHalf'].values()) if r['tHalf'] else end
            fc = r['firstCharge'] if r['firstCharge'] >= 0 else None
            asl = fc if (fc is not None and fc < half) else None
            v['app'].append(fs);
            if asl is not None: v['fire'].append(asl - fs); v['asl'].append(half - asl)
            else: v['fire'].append(max(0, half - fs)); v['asl'].append(0)
            v['purs'].append(max(0, end - half))
            v['hasAssault'].append(1.0 if asl is not None else 0.0)
        rows.append([kind, len(rs), fmt(ms(v['app']), 0), fmt(ms(v['fire']), 0), fmt(ms(v['asl']), 0), fmt(ms(v['purs']), 0), fmt(ms(v['hasAssault']), 0, True)])
    table(['종류', 'n', '접근(초)', '교전', '돌격', '추격', '돌격 있었던 판'], rows)
    P('### 7-3. 분당 사상 곡선 (양측 합, 30초 구간 평균, 런 평균)'); P()
    bins = defaultdict(lambda: defaultdict(list))
    for r in runs:
        for t, a, b in r['killBins']: bins[r['cfg']['kind']][int(t // 30)].append((a + b) * 6.0)   # 10s bin -> per minute
    hdr = ['종류'] + [f"{k * 30}-{k * 30 + 30}s" for k in range(0, 10)]
    rows = []
    for kind in KINDS:
        if kind not in bins: continue
        rows.append([kind] + [f"{st.mean(bins[kind][k]):.0f}" if bins[kind][k] else '-' for k in range(0, 10)])
    table(hdr, rows)
    durs = sorted(r['dur'] for r in runs)
    P(f"길이 분포 (초): 최소 {durs[0]:.0f}, p10 {pct(durs, .1):.0f}, 중앙 {pct(durs, .5):.0f}, p90 {pct(durs, .9):.0f}, 최대 {durs[-1]:.0f} (n={len(durs)}); 600초 상한 도달 {sum(1 for d in durs if d >= 599)}판."); P()


def sec_fire(runs, cfgs):
    P('## 8. 화력'); P()
    P('### 8-1. 사거리 구간별 명중률 (발당, 직사 무기)'); P()
    rbn = ['0-3', '3-6', '6-9', '9-12', '12-15', '15-18', '18-21', '21-24', '24+']
    rows = []
    for cls in ('rifle', 'lmg', 'hmg'):
        for b in range(9):
            by_run = [[(sum(cnt(r, s, f'rb.{cls}.{b}') for s in (1, 2)), sum(cnt(r, s, f'rbh.{cls}.{b}') for s in (1, 2)))] for r in runs]
            n = sum(x[0][0] for x in by_run)
            if n < 30: continue
            rows.append([cls, rbn[b], int(n), fmt(boot(by_run, lambda it: sum(h for _, h in it) / max(1, sum(s for s, _ in it))), 1, True)])
    table(['무기', '사거리(칸, 중심간)', '발수', '명중률 (±CI)'], rows)
    P('### 8-2. 교전 거리 분포 (소총 사격의 사거리 구간 비율)'); P()
    rows = []
    for kind in KINDS:
        rs = [r for r in runs if r['cfg']['kind'] == kind]
        if not rs: continue
        tot = [sum(cnt(r, s, f'rb.rifle.{b}') for r in rs for s in (1, 2)) for b in range(9)]
        n = sum(tot)
        rows.append([kind, int(n)] + [f"{t / max(1, n) * 100:.0f}%" for t in tot])
    table(['종류', '소총 발수'] + rbn, rows)
    P('### 8-3. 무기별 살상 비중'); P()
    kcls = ['rifle', 'lmg', 'hmg', 'gun', 'howitzer', 'melee', 'cav', 'smg', 'pistol', 'sniper']
    rows = []
    for kind in KINDS:
        for role in ('both', 'att', 'def'):
            rs = [(r, s) for r in runs if r['cfg']['kind'] == kind for s in (1, 2) if role_of(r['cfg'], s) == role]
            if not rs: continue
            row = [kind, role, int(sum(cnt(r, s, 'kills.total') for r, s in rs))]
            for k in kcls[:8]:
                by = [[(cnt(r, s, 'kills.' + k), cnt(r, s, 'kills.total'))] for r, s in rs]
                row.append(fmt(boot(by, lambda it: sum(a for a, _ in it) / max(1, sum(b for _, b in it))), 0, True))
            rows.append(row)
    table(['종류', '역할', '살상(크레딧) 합'] + kcls[:8], rows)
    P('살상 크레딧 = 막타 사수의 Unit.Kills 증가. "melee" = 적과 1.6칸 안에서 근접/돌격 중 사수. 곡사포 크레딧은 포가 죽으면 사라질 수 있다.'); P()
    P('### 8-4. 과잉 · 표적 분산 · 아군 오사 · 포격 효율'); P()
    rows = []
    for kind in KINDS:
        rs = [r for r in runs if r['cfg']['kind'] == kind]
        if not rs: continue
        by = [[(sum(cnt(r, s, 'conc.firers') for s in (1, 2)), sum(cnt(r, s, 'conc.targets') for s in (1, 2)), sum(cnt(r, s, 'conc.hot') for s in (1, 2)), sum(cnt(r, s, 'conc.hotFirers') for s in (1, 2)))] for r in rs]
        spt = boot(by, lambda it: sum(a for a, _, _, _ in it) / max(1, sum(b for _, b, _, _ in it)))
        hot = boot(by, lambda it: sum(c for _, _, c, _ in it) / max(1, sum(b for _, b, _, _ in it)))
        hotf = boot(by, lambda it: sum(d for _, _, _, d in it) / max(1, sum(a for a, _, _, _ in it)))
        ff = sum(cnt(r, s, 'ff.units') for r in rs for s in (1, 2)); ffi = sum(cnt(r, s, 'ff.impacts') for r in rs for s in (1, 2)); imp = sum(cnt(r, s, 'imp.howitzer') + cnt(r, s, 'imp.gun') for r in rs for s in (1, 2))
        imp0 = sum(cnt(r, s, 'imp0.howitzer') + cnt(r, s, 'imp0.gun') for r in rs for s in (1, 2))
        rows.append([kind, fmt(spt, 2), fmt(hot, 0, True), fmt(hotf, 0, True), int(imp), f"{imp0 / max(1, imp) * 100:.0f}%", int(ffi), int(ff)])
    table(['종류', '1초 동안 표적당 사수 수', '3명+ 집중된 표적 비율', '사수 중 3명+ 집중 표적을 쏜 비율', '포 착탄 수', '적이 반경 안에 없던 착탄', '아군 근처 착탄(사건)', '그 안 아군(명)'], rows)


def sec_perf(runs, cfgs):
    P('## 9. 성능 (틱 시간, 이 PC, 다른 에이전트 작업과 겹쳤을 수 있음)'); P()
    rows = []
    for cid, rs in sorted(by_cfg(runs).items()):
        n0 = rs[0]['start']['1'] + rs[0]['start']['2']
        rows.append([cid, n0, fmt(ms([r['perf']['stepMean'] for r in rs]), 3), fmt(ms([r['perf']['stepP99'] for r in rs]), 2), fmt(ms([r['perf']['stepMax'] for r in rs]), 1), fmt(ms([r['perf']['obsMean'] for r in rs]), 3)])
    table(['시나리오', '시작 병력', '틱 ms 평균', 'p99', '최대', '측정기 오버헤드 ms/틱'], rows)


def sec_bigpicture(runs, cfgs):
    P('## 0. 전체 요약'); P()
    P(f"- 런 {len(runs)}판, 시나리오 {len(set(r['cid'] for r in runs))}개, 시드 {sorted(set(r['seed'] for r in runs))}."); P()
    tot_shots = sum(cnt(r, s, 'shots.rifle') for r in runs for s in (1, 2))
    P(f"- 소총 사격 {int(tot_shots):,}발, 살상 크레딧 {int(sum(cnt(r, s, 'kills.total') for r in runs for s in (1, 2))):,}.")
    P(f"- 총 시뮬 시간 {sum(r['dur'] for r in runs) / 3600:.1f} 시간(게임 시간).")
    P()


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--raw', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'docs', 'bncamp_raw')); ap.add_argument('--filter', default=None)
    a = ap.parse_args()
    cfgs = matrix(a.raw)
    runs = load(a.raw, a.filter)
    global KINDS
    ks = []
    for c in cfgs.values():
        if c['kind'] not in ks and any(r['cid'] == c['id'] for r in runs): ks.append(c['kind'])
    KINDS = tuple(ks)
    for r in runs: derive(r, cfgs[r['cid']])
    man = json.load(open(os.path.join(a.raw, 'manifest.json'))) if os.path.exists(os.path.join(a.raw, 'manifest.json')) else {}
    P(f"# 대대 캠페인 집계 (코드 {man.get('commit', '?')[:10]}, {len(runs)}판)"); P()
    for f in (sec_bigpicture, sec_outcomes, sec_ratio, sec_company, sec_battalion, sec_supp, sec_morale, sec_tempo, sec_fire, sec_perf):
        f(runs, cfgs)
    open(os.path.join(a.raw, 'tables.md'), 'w', encoding='utf8').write('\n'.join(OUT))
    print(f"wrote tables.md ({len(OUT)} lines) from {len(runs)} runs")


if __name__ == '__main__':
    main()
