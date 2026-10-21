# 11차 집계 (장교 거리 · 부사관 배수): python -I tools/tobs_agg11.py docs/tactics_obs_raw11 OB3 OB1 ...
import re, sys, statistics as st
d = sys.argv[1]
def f(t, p, i=1, default=0.0):
    m = re.search(p, t); return float(m.group(i)) if m else default
def run(name):
    r = dict(win=0, n=0, kd=0, ed=0, dur=0, ob=[], fi=[0, 0], al=[0, 0], cs={k: [0, 0, 0.0] for k in 'ONL'}, off=0, nco=0, none=0, asd=0, asr=0)
    per = {}
    for sc in 'ABC':
        for sd in range(1, int(__import__('os').environ.get('NSEED', '5')) + 1):
            t = open(f'{d}/{name}_{sc}{sd}.txt', encoding='utf-8').read()
            h = re.search(r'# \S+ seed \d+: (\w+) at ([\d.]+)s', t)
            w = 1 if h.group(1) == 'won' else 0
            r['win'] += w; per[sc] = per.get(sc, 0) + w; r['n'] += 1; r['dur'] += float(h.group(2))
            r['kd'] += f(t, r'faction 1 .*?lost (\d+)'); r['ed'] += f(t, r'faction 2 .*?lost (\d+)')
            for sec in t.split('## 세력')[1:]:
                fac = int(sec.split()[0])
                m = re.search(r'장교가 병사 최전열보다 뒤 (-?[\d.]+)칸, 지휘 범위 안 병사 비율 앞줄\(최전열 2칸\) ([\d.]+) \((\d+)\) 전체 ([\d.]+) \((\d+)\)', sec)
                if m and fac == 1:
                    n = int(m.group(3)); a = int(m.group(5)); r['ob'].append(float(m.group(1)))
                    r['fi'][0] += float(m.group(2)) * n; r['fi'][1] += n; r['al'][0] += float(m.group(4)) * a; r['al'][1] += a
                m = re.search(r'지휘 능력\(중대-초\): 장교 (\d+) / 부사관 대리 (\d+) / 없음 (\d+)', sec)
                if m and fac == 1: r['off'] += int(m.group(1)); r['nco'] += int(m.group(2)); r['none'] += int(m.group(3))
                m = re.search(r'\[돌격결과\] 완료 (\d+): 후퇴 (\d+)', sec)
                if m: r['asd'] += int(m.group(1)); r['asr'] += int(m.group(2))
                m = re.search(r'\[지휘별\] (.*)', sec)
                if m:
                    for k, s_, dd, sp in re.findall(r'([ONL]): 병사-초 (\d+) 사망 (\d+) 사망/천병사-초 [\d.]+ 평균제압 ([\d.]+)', m.group(1)):
                        c = r['cs'][k]; c[0] += int(s_); c[1] += int(dd); c[2] += float(sp) * int(s_)
    r['per'] = per; return r
print(f"{'set':8} win(A/B/C) K/E   dur  offBack  frontIn allIn  assault(ret%)  cmd-sec(off/nco/none)  death/1k-sec O N L   supp O N L")
for name in sys.argv[2:]:
    r = run(name); n = r['n']
    cs = r['cs']; dr = {k: (cs[k][1] * 1000 / cs[k][0] if cs[k][0] else 0) for k in 'ONL'}; sp = {k: (cs[k][2] / cs[k][0] if cs[k][0] else 0) for k in 'ONL'}
    print(f"{name:8} {r['win']:2}({r['per']['A']}/{r['per']['B']}/{r['per']['C']}) {r['kd']/r['ed']:.3f} {r['dur']/n:5.0f}  {st.mean(r['ob']):6.2f}  {r['fi'][0]/max(1,r['fi'][1]):.3f}  {r['al'][0]/max(1,r['al'][1]):.3f}  {r['asd']}({100*r['asr']/max(1,r['asd']):.0f})  {r['off']}/{r['nco']}/{r['none']}  {dr['O']:.2f} {dr['N']:.2f} {dr['L']:.2f}  {sp['O']:.4f} {sp['N']:.4f} {sp['L']:.4f}")
