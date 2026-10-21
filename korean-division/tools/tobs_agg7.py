# 7차 집계: python -I tools/tobs_agg7.py docs/tactics_obs_raw7   (조건별 · 시나리오별 시드 1~3 합)
import re, sys, os
sys.stdout.reconfigure(encoding="utf-8")
D = sys.argv[1]
def num(m, i=1, d=0.0): return float(m.group(i)) if m else d
def parse(path):
    t = open(path, encoding='utf-8').read(); L = t.splitlines()
    r = dict(win=1 if 'won at' in L[1] else 0, dur=num(re.search(r'at ([\d.]+)s', t)),
             K=int(re.search(r'faction 1 .*?lost (\d+)', t).group(1)), E=int(re.search(r'faction 2 .*?lost (\d+)', t).group(1)),
             sides={})
    for i, sec in enumerate(t.split('## 세력')[1:]):
        g = lambda p: re.search(p, sec)
        s = {}
        m = g(r'\[사거리\].*?표본 (\d+), 평균 ([\d.]+), 중앙 ([\d.]+), 20칸 이상 (\d+), 10칸 미만 (\d+)')
        s['rn'], s['ravg'], s['r20'], s['r10'] = (int(m.group(1)), float(m.group(2)), int(m.group(4)), int(m.group(5))) if m else (0, 0, 0, 0)
        m = g(r'\[엄호\] 약진 중 \(적 가시\) 파트너 사격 비율 평균 ([\d.]+) \(표본 (\d+)초')
        s['ovN'], s['ov'] = (int(m.group(2)), float(m.group(1))) if m else (0, 0)
        m = g(r'\[시간차\] .*?표본 (\d+), 사격 전 출발 (\d+), 2초 미만 (\d+), 중앙 ([\d.]+)s')
        s['lagN'], s['lagMed'] = (int(m.group(1)), float(m.group(4))) if m else (0, 0)
        m = g(r'\[틱시작\] (.*)'); ts = m.group(1) if m else ''
        s['bound'] = sum(int(x) for x in re.findall(r'약진:\S+ (\d+)', ts)); s['man'] = sum(int(x) for x in re.findall(r'기동조:\S+ (\d+)', ts))
        m = g(r'돌격 개시 (\d+)회'); s['assault'] = int(m.group(1)) if m else 0
        m = g(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+)'); s['ad'], s['ar'] = (int(m.group(1)), int(m.group(2))) if m else (0, 0)
        m = g(r'\[포병2\] 포-초 (\d+), 표적 보유 (\d+) .*?사격 (\d+) \('); s['gun'], s['gt'], s['gs'] = (int(m.group(1)), int(m.group(2)), int(m.group(3))) if m else (0, 0, 0)
        m = g(r'지휘관 교체 (\d+), 지휘 능력\(중대-초\): 장교 (\d+) / 부사관 대리 (\d+) / 없음 (\d+), 인계 중 (\d+)')
        s['succ'], s['off'], s['nco'], s['none'], s['hov'] = tuple(int(m.group(k)) for k in range(1, 6)) if m else (0,) * 5
        m = g(r'중대 전술 틀 시간: (.*)'); tp = dict((a, int(b)) for a, b in re.findall(r'(\w+) (\d+)', m.group(1))) if m else {}
        s['tplNone'] = tp.get('None', 0); s['tplAll'] = sum(tp.values())
        m = g(r'\[기병\] 돌격 (\d+)회: .*?>=10 (\d+), 보병 돌격 ±15초 동시 (\d+)'); s['cav'], s['cavShoot'], s['cavAs'] = (int(m.group(1)), int(m.group(2)), int(m.group(3))) if m else (0, 0, 0)
        effs = re.findall(r'기병 돌격 #\d+: .*?제압>=0\.25 (\d+)기 -> 8초 안 최대 (\d+)기', sec); s['cavEff'] = effs
        m = g(r'유휴 중대.*?: (\d+) 중대-초'); s['idle'] = int(m.group(1)) if m else 0
        r['sides'][i + 1] = s
    return r
def agg(prefix, sc, side=None):
    rs = [parse(f'{D}/{prefix}{sc}{sd}.txt') for sd in (1, 2, 3) if os.path.exists(f'{D}/{prefix}{sc}{sd}.txt')]
    if not rs: return None
    S = lambda k, sides=(1, 2): sum(r['sides'][x][k] for r in rs for x in sides if x in r['sides'])
    sd = (1, 2) if side is None else (side,)
    o = dict(n=len(rs), win=sum(r['win'] for r in rs), dur=sum(r['dur'] for r in rs) / len(rs), K=sum(r['K'] for r in rs), E=sum(r['E'] for r in rs))
    rn = S('rn', sd); o['range'] = sum(r['sides'][x]['ravg'] * r['sides'][x]['rn'] for r in rs for x in sd) / max(1, rn)
    o['r20'] = 100 * S('r20', sd) / max(1, rn); o['r10'] = 100 * S('r10', sd) / max(1, rn)
    ov = S('ovN', sd); o['ov'] = sum(r['sides'][x]['ov'] * r['sides'][x]['ovN'] for r in rs for x in sd) / max(1, ov); o['ovN'] = ov
    o['lagN'] = S('lagN', sd); o['lag'] = sum(r['sides'][x]['lagMed'] * r['sides'][x]['lagN'] for r in rs for x in sd) / max(1, o['lagN'])
    for k in ('bound', 'man', 'assault', 'ad', 'ar', 'gun', 'gt', 'gs', 'succ', 'off', 'nco', 'none', 'hov', 'cav', 'cavShoot', 'cavAs', 'idle', 'tplNone', 'tplAll'): o[k] = S(k, sd)
    o['eff'] = [e for r in rs for x in sd for e in r['sides'][x]['cavEff']]
    return o
def row(name, o):
    if not o: return f'{name}: (없음)'
    ar = f"{o['ar']}/{o['ad']}"
    return (f"{name:14s} 승{o['win']}/{o['n']} 초{o['dur']:.0f} 사상K{o['K']}/E{o['E']} 사거리{o['range']:.1f}(>=20 {o['r20']:.0f}% <10 {o['r10']:.1f}%) "
            f"엄호{o['ov']:.2f}({o['ovN']}s) 시간차{o['lag']:.0f}s(n{o['lagN']}) 약진{o['bound']} 기동조{o['man']} 돌격{o['assault']} 결과후퇴{ar} "
            f"포 표적{100*o['gt']/max(1,o['gun']):.0f}% 사격{o['gs']} 유휴{o['idle']} 기병{o['cav']}(보병사격{o['cavShoot']},동시{o['cavAs']})")
print('== 조건 1/3/4 (양측 합, 시나리오별 시드 1~3 합) ==')
for sc in 'ABC':
    for p in ('V0_', 'fog_', 'rain_', 'night_', 'open_', 'cav_'):
        if p == 'open_' and sc != 'B': continue
        if p == 'cav_' and sc == 'B': continue
        o = agg(p, sc); print(row(p + sc, o) if o else '', end='\n' if o else '')
print('\n== 조건 2 (한국 세력 1 만) ==')
for sc in 'AB':
    for p in ('V0_', 'killcmd_', 'killall_', 'nco_', 'ncokill_'):
        o = agg(p, sc, 1)
        if o: print(f"{p+sc:14s} 승{o['win']}/{o['n']} 초{o['dur']:.0f} 사상K{o['K']}/E{o['E']} 지휘(중대-초) 장교{o['off']} 부사관{o['nco']} 없음{o['none']} 인계중{o['hov']} 교체{o['succ']} 틀None{100*o['tplNone']/max(1,o['tplAll']):.0f}% 약진{o['bound']} 기동조{o['man']} 돌격{o['assault']} 엄호{o['ov']:.2f} 사거리{o['range']:.1f} 사격표본{0}")
print('\n== 기병 돌격 효과 (반경 6 제압>=0.25 적 기수 기준 -> 8초 안 최대) ==')
for p, sc in (('V0_', 'A'), ('V0_', 'B'), ('V0_', 'C'), ('cav_', 'A'), ('cav_', 'C')):
    o = agg(p, sc)
    if o and o['eff']:
        b = sum(int(e[0]) for e in o['eff']); pk = sum(int(e[1]) for e in o['eff']); print(f"{p+sc}: 돌격 {len(o['eff'])}회 기준합 {b} -> 최대합 {pk} (회당 {(pk-b)/len(o['eff']):+.1f})")
