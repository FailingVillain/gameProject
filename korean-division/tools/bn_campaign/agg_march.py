#!/usr/bin/env python3
"""March-fire (돌격 사격) metrics from a BnCamp batch built with the mf.* counters (read-only observation).
  python tools/bn_campaign/agg_march.py docs/bncamp2_raw_march"""
import sys, glob, gzip, json, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from aggregate import ms, fmt
raw = sys.argv[1]
runs = []
for p in sorted(glob.glob(os.path.join(raw, 'runs', '*.json.gz'))):
    d = json.load(gzip.open(p, 'rt', encoding='utf8')); d['cid'] = os.path.basename(p)[:-8].rsplit('_s', 1)[0]; runs.append(d)
C = lambda r, k: sum(r['counters'].get(f'{s}|{k}', 0.0) for s in (1, 2))
def div(a, b): return a / b if b else None
out = [f"# 행진 사격 (돌격 사격) 측정 — {len(runs)}판 ({len(set(r['cid'] for r in runs))} 시나리오 x 시드 1-2)", '']
out += ['## 1. 공격 이동(AttackMove) 중 보병-초 대비 (병종별)', '', '| 병종 | 공격이동 보병-초 | 중대 돌격 사격 켜짐 | 지휘 성립 (장교/부사관) | 지휘 중 표적 보유 | 켜짐 중 제압>=400 차단 | 일반 이동(Move) 보병-초 |', '|---|---|---|---|---|---|---|']
for ut in ('rifle', 'lmg', 'scout'):
    am = [C(r, f'mf.amsec.{ut}') for r in runs]
    if sum(am) == 0: continue
    out.append(f"| {ut} | {int(sum(am))} | {fmt(ms([div(C(r,f'mf.on.{ut}'),C(r,f'mf.amsec.{ut}')) for r in runs]),1,True)} | {fmt(ms([div(C(r,f'mf.cmd.{ut}'),C(r,f'mf.amsec.{ut}')) for r in runs]),1,True)} | {fmt(ms([div(C(r,f'mf.tgt.{ut}'),C(r,f'mf.cmd.{ut}')) for r in runs]),1,True)} | {fmt(ms([div(C(r,f'mf.block.{ut}'),C(r,f'mf.on.{ut}')) for r in runs]),2,True)} | {int(sum(C(r,f'mf.movesec.{ut}') for r in runs))} |")
out += ['', '## 2. 이동 중 소총 사격 대 정지 사격', '', '| 구분 | 발수 | 명중률 (런 평균 ±CI) | 평균 사거리 | 살상 (막타) | 살상/1000발 |', '|---|---|---|---|---|---|']
for k, lab in (('mf', '행진 사격'), ('st', '정지 사격')):
    sh = sum(C(r, k + '.shots') for r in runs); kl = sum(C(r, k + '.kills') for r in runs)
    out.append(f"| {lab} | {int(sh)} | {fmt(ms([div(C(r,k+'.hits'),C(r,k+'.shots')) for r in runs if C(r,k+'.shots')>=20]),1,True)} | {sum(C(r,k+'.range') for r in runs)/max(1,sh):.1f} | {int(kl)} | {kl/max(1,sh)*1000:.1f} |")
tot = sum(C(r, 'mf.shots') + C(r, 'st.shots') for r in runs)
out += ['', f"- 소총 사격 중 행진 사격 비중 {sum(C(r,'mf.shots') for r in runs)/max(1,tot)*100:.2f}%.", '']
out += ['## 3. 행진 사격에서 돌격으로 (<=3칸 자동 돌격)', '', f"- 돌격 시작 병사 {int(sum(C(r,'chg.start') for r in runs))}명 중 직전 틱까지 행진 사격 지휘 상태(공격이동 + 지휘 성립)였던 병사 {int(sum(C(r,'chg.fromMarch') for r in runs))}명 ({sum(C(r,'chg.fromMarch') for r in runs)/max(1,sum(C(r,'chg.start') for r in runs))*100:.1f}%).", '']
out += ['## 4. 시나리오별', '', '| 시나리오 | 공격이동 보병-초(소총) | 켜짐 | 지휘 | 행진 발수 | 행진 명중률 | 정지 명중률 | 행진 살상 | 돌격 시작 | 행진 유래 돌격 |', '|---|---|---|---|---|---|---|---|---|---|']
for cid in sorted(set(r['cid'] for r in runs)):
    rs = [r for r in runs if r['cid'] == cid]
    out.append(f"| {cid} | {int(sum(C(r,'mf.amsec.rifle') for r in rs))} | {fmt(ms([div(C(r,'mf.on.rifle'),C(r,'mf.amsec.rifle')) for r in rs]),0,True)} | {fmt(ms([div(C(r,'mf.cmd.rifle'),C(r,'mf.amsec.rifle')) for r in rs]),0,True)} | {int(sum(C(r,'mf.shots') for r in rs))} | {fmt(ms([div(C(r,'mf.hits'),C(r,'mf.shots')) for r in rs if C(r,'mf.shots')>=10]),1,True)} | {fmt(ms([div(C(r,'st.hits'),C(r,'st.shots')) for r in rs]),1,True)} | {int(sum(C(r,'mf.kills') for r in rs))} | {int(sum(C(r,'chg.start') for r in rs))} | {int(sum(C(r,'chg.fromMarch') for r in rs))} |")
open(os.path.join(raw, 'march_tables.md'), 'w', encoding='utf8').write('\n'.join(out))
print('\n'.join(out))
