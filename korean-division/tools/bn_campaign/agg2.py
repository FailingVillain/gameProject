#!/usr/bin/env python3
"""Campaign 2 extra tables (scenario-specific, read-only on raw data) -> <raw>/tables2.md.

  python tools/bn_campaign/agg2.py [--raw docs/bncamp2_raw]

The generic tables (suppression, morale proxies, tempo, fire, cover per kind/role) come from aggregate.py (-> tables.md).
This file adds: per-scenario outcomes with end type, ambush first-volley / reaction time, cavalry, artillery duel and
counter-battery, river stall, counterattack, quality, organisation, objective-variant tables.
"""
import argparse, os, sys, statistics as st
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aggregate as A
from aggregate import ms, fmt, boot

OUT = []
def P(s=''): OUT.append(s)
def table(h, rows):
    P('| ' + ' | '.join(h) + ' |'); P('|' + '|'.join(['---'] * len(h)) + '|')
    for r in rows: P('| ' + ' | '.join(str(x) for x in r) + ' |')
    P()

def cnt(r, side, key): return r['C'].get(f"{side}|{key}", 0.0)

def alive_at(r, side, t):
    """alive of `side` at time t from the 5 s series (linear interpolation)."""
    ser = r['ser']; idx = 1 if side == 1 else 2
    if not ser: return r['start'][str(side)]
    pts = [(0, r['start'][str(side)])] + [(x[0], x[idx]) for x in ser]
    for (t0, a0), (t1, a1) in zip(pts, pts[1:]):
        if t0 <= t <= t1: return a0 + (a1 - a0) * (t - t0) / max(1e-9, t1 - t0)
    return pts[-1][1]

def losses_between(r, side, t0, t1): return alive_at(r, side, t0) - alive_at(r, side, t1)

def derive2(r):
    s1, s2 = r['start']['1'], r['start']['2']
    r['won'] = 1.0 if r['result'] == 'won' else 0.0
    r['lost'] = 1.0 if r['result'] == 'lost' else 0.0
    r['open'] = 1.0 if r['result'] not in ('won', 'lost') else 0.0
    fc = {int(k): v for k, v in r['firstCasSide'].items()}
    r['fc1'], r['fc2'] = fc.get(1), fc.get(2)
    fs = r['firstShot']
    R = r['R']
    # kill-quiet fraction: 10 s bins after first shot with zero kills (both sides)
    kb = [b for b in r['killBins'] if fs >= 0 and b[0] > fs]
    r['quiet'] = (sum(1 for b in kb if b[1] + b[2] == 0) / len(kb)) if kb else None
    kb2 = [b for b in r['killBins'] if b[0] > 120]
    r['quiet120'] = (sum(1 for b in kb2 if b[1] + b[2] == 0) / len(kb2)) if kb2 else None
    # ambush metrics: first casualty of side 1 (marching side) -> losses in the next 15 / 30 / 60 s, reaction time
    t_c = r['fc1']
    if t_c is not None:
        for w in (15, 30, 60):
            r[f'a{w}'] = losses_between(r, 1, t_c - 6, t_c + w)           # series is 5 s grained: count from just before first casualty
            r[f'd{w}'] = losses_between(r, 2, t_c - 6, t_c + w)
        by_t = defaultdict(list)
        for x in R:
            if x['side'] == 1: by_t[x['t']].append(x)
        react = None
        for t in sorted(by_t):
            if t < t_c: continue
            rows = by_t[t]
            if rows and all(x['state'] not in ('Advance', 'Approach') for x in rows): react = t - t_c; break
        r['react'] = react
        near = [x for x in R if x['side'] == 1 and abs(x['t'] - t_c) <= 2]
        r['depth_c'] = st.mean(x['depth'] for x in near) if near else None
        r['width_c'] = st.mean(x['width'] for x in near) if near else None
        r['dist_c'] = min((x['nearestEnemy'] for x in near), default=None)
    # crossing (river): any side-1 company centre north of y=50
    r['crossed'] = 1.0 if any(x['side'] == 1 and x['cy'] < 50 for x in R) else 0.0
    return r

def by(runs):
    d = defaultdict(list)
    for r in runs: d[r['cid']].append(r)
    return d

def fam(runs, kind): return [r for r in runs if r['cfg']['kind'] == kind]

def sec_outcomes(runs, cfgs):
    P('## A. 시나리오별 결과 (시드 평균 ±95% CI)'); P()
    P('결과 = 시뮬 종료 판정 (won = 한국군(1) 목표 달성, lost = 패배, 미결 = 600초 상한). 낮은 손실 승 = 최종 손실률이 더 낮은 쪽 (미결 포함). 교환비 = 2측 손실 / 1측 손실. 조용함 = 첫 사격 뒤 10초 구간 중 사상 0인 비율.'); P()
    rows = []
    for kind in A.KINDS:
        for cid, rs in by(fam(runs, kind)).items():
            rows.append([kind, cid, len(rs), f"{int(sum(r['won'] for r in rs))}/{int(sum(r['lost'] for r in rs))}/{int(sum(r['open'] for r in rs))}", fmt(ms([r['att_win'] for r in rs]), 2),
                         fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['ex'] for r in rs]), 2), fmt(ms([r['dur'] for r in rs]), 0), fmt(ms([r['quiet'] for r in rs]), 0, True)])
    table(['종류', '시나리오', 'n', '승/패/미결', '1측 낮은 손실 승', '1측 손실', '2측 손실', '교환비', '길이(초)', '조용함'], rows)
    P('판 종료 유형 (패자 기준, aggregate.derive):'); P()
    rows = []
    for kind in A.KINDS:
        c = defaultdict(int)
        for r in fam(runs, kind): c[r['end']] += 1
        rows.append([kind] + [c.get(k, 0) for k in ('전멸(>=90%)', '철수(>=70% 패자, 중대 절반 이상 후퇴)', '철수(<70%)', '시간 종료(미결)', '전투 종료(기타)')])
    table(['종류', '전멸 >=90%', '철수 (>=70%)', '철수 (<70%)', '시간 종료', '기타'], rows)

def sec_mgdef(runs, cfgs):
    rs_ = fam(runs, 'mgdef')
    if not rs_: return
    P('## B. 기관총 중심 방어 (방어 소총 2 중대 고정 + 맥심 5/경기관총 8 중대 m개, 공격 소총만)'); P()
    P('방어측 중대 크기 · 소총수는 공격측과 같다 (84 + 장교 3 + 정찰 2). 기관총은 막타 크레딧 기준 방어측 살상 비중.'); P()
    rows = []
    for cid, rs in by(rs_).items():
        k2 = lambda r, c: cnt(r, 2, 'kills.' + c)
        tot = lambda r: max(1.0, cnt(r, 2, 'kills.total'))
        rows.append([cid, len(rs), fmt(ms([r['won'] for r in rs]), 2), fmt(ms([r['lost'] for r in rs]), 2), fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['dur'] for r in rs]), 0),
                     fmt(ms([(k2(r, 'hmg') + k2(r, 'lmg')) / tot(r) for r in rs]), 0, True), fmt(ms([k2(r, 'rifle') / tot(r) for r in rs]), 0, True),
                     fmt(ms([(cnt(r, 2, 'shots.hmg') + cnt(r, 2, 'shots.lmg')) for r in rs]), 0)])
    table(['시나리오', 'n', '공격 승', '공격 패', '공격 손실', '방어 손실', '길이', '방어 살상 중 기관총', '방어 살상 중 소총', '방어 기관총 발수'], rows)

def sec_cav(runs, cfgs):
    rs_ = fam(runs, 'cavflank')
    if not rs_: return
    P('## C. 교전 중인 전열 측면에 기병 (t=75초 동쪽 가장자리 등장)'); P()
    rows = []
    for cid, rs in by(rs_).items():
        cs = lambda r: r['startByDef'].get('1|cavalry_default', 0); ca = lambda r: r['aliveByDef'].get('1|cavalry_default', 0)
        rows.append([cid, len(rs), cs(rs[0]), fmt(ms([r['won'] for r in rs]), 2), fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['dur'] for r in rs]), 0),
                     fmt(ms([cnt(r, 1, 'kills.cav') for r in rs]), 1), fmt(ms([(ca(r) / cs(r)) if cs(r) else None for r in rs]), 0, True), fmt(ms([cnt(r, 2, 'kills.rifle') + cnt(r, 2, 'kills.melee') for r in rs]), 0)])
    table(['시나리오', 'n', '기병 수', '1측 승', '1측 손실', '2측 손실', '길이', '기병 살상 (명)', '기병 생존율', '2측 소총+근접 살상'], rows)

def sec_amb(runs, cfgs):
    rs_ = fam(runs, 'ambush')
    if not rs_: return
    P('## D. 숲 매복 — 첫 일제 사상자 · 반응 시간'); P()
    P('행진측 = 1 (공격). 첫 사상 = 1측 첫 전사자 시각 (t_c). 사상 15/30/60초 = t_c-6초부터 그 뒤 N초 동안 1측 손실 / 2측 손실 (5초 해상도). 반응 = t_c 이후 1측 모든 소총 중대가 Advance · Approach 를 벗어난 첫 시각까지 (이동 종료 · 전개). 종심 · 정면 = t_c 때 1측 중대 평균.'); P()
    rows = []
    for cid, rs in by(rs_).items():
        g = lambda k: ms([r.get(k) for r in rs])
        rows.append([cid, len(rs), fmt(ms([r['firstShot'] for r in rs]), 0), fmt(g('react'), 0), fmt(g('depth_c'), 1), fmt(g('width_c'), 1), fmt(g('dist_c'), 0),
                     f"{fmt(g('a15'), 1)} / {fmt(g('d15'), 1)}", f"{fmt(g('a30'), 1)} / {fmt(g('d30'), 1)}", f"{fmt(g('a60'), 1)} / {fmt(g('d60'), 1)}",
                     fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['quiet120'] for r in rs]), 0, True)])
    table(['시나리오', 'n', '첫 사격', '반응(초)', '종심', '정면', '최근접 적', '15초 1측/2측 손실', '30초', '60초', '1측 손실', '2측 손실', '120초 뒤 조용함'], rows)

def sec_ctr(runs, cfgs):
    rs_ = fam(runs, 'counter')
    if not rs_: return
    P('## E. 역습 — 점령측(1)이 참호, 2측이 북쪽에서 탈환 (예비 대대 유무 · 시기)'); P()
    P('2측 승 = 참호 탈환 (1측 97% 손실 또는 격멸). 점령측 = 1.'); P()
    rows = []
    for cid, rs in by(rs_).items():
        rows.append([cid, len(rs), rs[0]['start']['2'], fmt(ms([r['lost'] for r in rs]), 2), fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['ex'] for r in rs]), 2), fmt(ms([r['dur'] for r in rs]), 0),
                     fmt(ms([cnt(r, 1, 'kills.total') for r in rs]), 0), fmt(ms([r['T'].get(2, {}).get('reserve', {}).get('n') for r in rs]), 1)])
    table(['시나리오', 'n', '2측 병력', '탈환(2측 승)', '점령측 손실', '반격측 손실', '교환비(2/1)', '길이', '점령측 살상', '2측 예비 투입 횟수'], rows)

def sec_arty(runs, cfgs):
    rs_ = fam(runs, 'arty')
    if not rs_: return
    P('## F. 포병 대결 (양측 곡사포 · 관측반 · 보병포)'); P()
    P('곡사포 생존율 = 끝 시점 생존 / 시작. 대포병 = 한쪽 곡사포가 상대 포에 입힌 손실은 직접 안 잰다 (생존율 비교로 대신). 포 살상 = 막타 크레딧.'); P()
    rows = []
    for cid, rs in by(rs_).items():
        hs = lambda r, s: r['startByDef'].get(f'{s}|heavy_howitzer', 0)
        ha = lambda r, s: r['aliveByDef'].get(f'{s}|heavy_howitzer', 0)
        gs = lambda r, s: r['startByDef'].get(f'{s}|heavy_infantry_gun', 0) + r['startByDef'].get(f'{s}|heavy_field_gun', 0)
        ga = lambda r, s: r['aliveByDef'].get(f'{s}|heavy_infantry_gun', 0) + r['aliveByDef'].get(f'{s}|heavy_field_gun', 0)
        rows.append([cid, len(rs), fmt(ms([r['won'] for r in rs]), 2), fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['dur'] for r in rs]), 0),
                     fmt(ms([(cnt(r, 1, 'shots.howitzer')) for r in rs]), 1), fmt(ms([(cnt(r, 2, 'shots.howitzer')) for r in rs]), 1),
                     fmt(ms([cnt(r, 1, 'kills.howitzer') + cnt(r, 1, 'kills.gun') for r in rs]), 1), fmt(ms([cnt(r, 2, 'kills.howitzer') + cnt(r, 2, 'kills.gun') for r in rs]), 1),
                     fmt(ms([ha(r, 1) / hs(r, 1) if hs(r, 1) else None for r in rs]), 0, True), fmt(ms([ha(r, 2) / hs(r, 2) if hs(r, 2) else None for r in rs]), 0, True),
                     fmt(ms([ga(r, 1) / gs(r, 1) if gs(r, 1) else None for r in rs]), 0, True), fmt(ms([ga(r, 2) / gs(r, 2) if gs(r, 2) else None for r in rs]), 0, True)])
    table(['시나리오', 'n', '1측 승', '1측 손실', '2측 손실', '길이', '1측 곡사 발수', '2측 곡사 발수', '1측 포 살상', '2측 포 살상', '1측 곡사 생존', '2측 곡사 생존', '1측 직사포 생존', '2측 직사포 생존'], rows)

def sec_riv(runs, cfgs):
    rs_ = fam(runs, 'river')
    if not rs_: return
    P('## G. 강 · 다리'); P()
    rows = []
    for cid, rs in by(rs_).items():
        rows.append([cid, len(rs), f"{int(sum(r['won'] for r in rs))}/{int(sum(r['lost'] for r in rs))}/{int(sum(r['open'] for r in rs))}", fmt(ms([r['crossed'] for r in rs]), 0, True),
                     fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['dur'] for r in rs]), 0), fmt(ms([r['quiet120'] for r in rs]), 0, True),
                     fmt(ms([cnt(r, 1, 'shots.rifle') + cnt(r, 2, 'shots.rifle') for r in rs]), 0)])
    table(['시나리오', 'n', '승/패/미결', '북안 도하(중대 중심 y<50) 런 비율', '공격 손실', '방어 손실', '길이', '120초 뒤 조용함', '소총 발수'], rows)

def sec_org(runs, cfgs):
    for kind, title in (('quality', 'H. 비대칭 질 (징집병 recruit / 정규 / 정예 veteran)'), ('org', 'I. 장교 중심 대 부사관 편성'), ('aggr', 'J. 전술 AI 프로필 (기존 프로필만)'), ('sym', 'K. 대칭 편성 (캠페인 1 비대칭 보정)'), ('objective', 'L. 목표 변형 — 참호 점령 (30초 확보) 대 격멸')):
        rs_ = fam(runs, kind)
        if not rs_: continue
        P(f'## {title}'); P()
        rows = []
        for cid, rs in by(rs_).items():
            fr = lambda r: r['T'].get(1, {}).get('stateSec', {}).get('Fallback', 0) / max(1.0, sum(r['T'].get(1, {}).get('stateSec', {}).values()) or 1)
            rows.append([cid, len(rs), f"{rs[0]['start']['1']}/{rs[0]['start']['2']}", f"{int(sum(r['won'] for r in rs))}/{int(sum(r['lost'] for r in rs))}/{int(sum(r['open'] for r in rs))}", fmt(ms([r['att_win'] for r in rs]), 2),
                         fmt(ms([r['l1'] for r in rs]), 1, True), fmt(ms([r['l2'] for r in rs]), 1, True), fmt(ms([r['ex'] for r in rs]), 2), fmt(ms([r['dur'] for r in rs]), 0),
                         fmt(ms([fr(r) for r in rs]), 1, True), fmt(ms([r['tHalf'].get('1') for r in rs]), 0), fmt(ms([r['tHalf'].get('2') for r in rs]), 0)])
        table(['시나리오', 'n', '병력 1/2', '승/패/미결', '1측 낮은 손실 승', '1측 손실', '2측 손실', '교환비', '길이', '1측 Fallback 시간', '1측 50% 시각', '2측 50% 시각'], rows)

def sec_cover(runs, cfgs):
    P('## M. 시나리오 종류별 엄폐 · 밀집 (피격 병사 중 지형 비율, 소총 중대)'); P()
    rows = []
    for kind in A.KINDS:
        for role in ('both', 'att', 'def'):
            v = defaultdict(list)
            for r in fam(runs, kind):
                for s in (1, 2):
                    if A.role_of(r['cfg'], s) != role: continue
                    rs = [x for x in r['R'] if x['side'] == s]
                    uf = sum(x['underFire'] for x in rs)
                    if uf > 0:
                        v['trench'].append(sum(x['cTrench'] for x in rs) / uf); v['forest'].append(sum(x['cForest'] for x in rs) / uf); v['other'].append(sum(x['cOther'] for x in rs) / uf)
                        v['clump'].append(st.mean(x['clump'] for x in rs))
                    fl = [x['flank'] for x in rs if x['flank'] >= 0]
                    if fl: v['flank'].append(sum(1 for f in fl if f >= 1) / len(fl))
            if v['trench']: rows.append([kind, role, len(v['trench']), fmt(ms(v['trench']), 1, True), fmt(ms(v['forest']), 1, True), fmt(ms(v['other']), 1, True), fmt(ms(v['clump']), 1, True), fmt(ms(v['flank']), 1, True)])
    table(['종류', '역할', 'n', '참호 칸', '숲', '기타 엄폐', '1칸 밀집', '측면 피격 (>60도)'], rows)

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--raw', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'docs', 'bncamp2_raw'))
    a = ap.parse_args()
    cfgs = A.matrix(a.raw)
    runs = A.load(a.raw)
    for r in runs:
        A.derive(r, cfgs[r['cid']]); derive2(r)
    ks = []
    for c in cfgs.values():
        if c['kind'] not in ks and any(r['cid'] == c['id'] for r in runs): ks.append(c['kind'])
    A.KINDS = tuple(ks)
    P(f"# 캠페인 2 추가 표 ({len(runs)}판, {len(set(r['cid'] for r in runs))} 시나리오)"); P()
    for f in (sec_outcomes, sec_mgdef, sec_cav, sec_amb, sec_ctr, sec_arty, sec_riv, sec_org, sec_cover): f(runs, cfgs)
    open(os.path.join(a.raw, 'tables2.md'), 'w', encoding='utf8').write('\n'.join(OUT))
    print(f"wrote tables2.md ({len(OUT)} lines) from {len(runs)} runs")

if __name__ == '__main__':
    main()
