# 13차 집계 (cavcmd 기병 장교 · 기병 부사관): python -I tools/tobs_agg13.py docs/tactics_obs_raw13 B CAV
# 12차 집계 (tobs_agg12.parse: 승 · K/E · 기병 돌격 효과 · [기병AI]) + [기병지휘] 줄 (기병 중대 지휘 능력 중대-초) + 전체 지휘 능력 줄.
import os, re, sys, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.argv, _args = sys.argv[:2], sys.argv[2:]          # tobs_agg12 는 import 때 sys.argv[2:] 를 돈다 → 비워 두고 parse 만 쓴다
import tobs_agg12 as A                                 # noqa: E402
d = sys.argv[1]


def parse(p):
    r = A.parse(p)
    t = open(p, encoding='utf-8').read()
    m = re.search(r'\[기병지휘\] 중대-초: 장교 (\d+) / 부사관 대리 (\d+) / 없음 (\d+), 인계 중 (\d+), 지휘관 교체 (\d+)', t)
    r.update(dict(zip(('cOff', 'cNco', 'cNone', 'cHand', 'cSucc'), map(int, m.groups()))) if m else dict(cOff=0, cNco=0, cNone=0, cHand=0, cSucc=0))
    m = re.search(r'지휘 능력\(중대-초\): 장교 (\d+) / 부사관 대리 (\d+) / 없음 (\d+), 인계 중 (\d+)', t)
    r.update(dict(zip(('lOff', 'lNco', 'lNone', 'lHand'), map(int, m.groups()))) if m else {})
    return r


for v in _args:
    for sc in ('ABC', 'A', 'B', 'C'):
        R = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_[{sc}][1-9].txt'))]
        if not R:
            continue
        eff = [x for r in R for x in r['cavEff']]
        S = lambda k: sum(r.get(k, 0) for r in R)
        tag = '전체' if sc == 'ABC' else sc
        print(f"{v} {tag}: n={len(R)} 승 {S('win')} K/E {S('K')}/{S('E')}={S('K')/max(1, S('E')):.2f} 평균 {A.mean(r['dur'] for r in R):.0f}s | "
              f"기병지휘 장교 {S('cOff')} 부사관 {S('cNco')} 없음 {S('cNone')} 인계 {S('cHand')} 교체 {S('cSucc')} | "
              f"소총중대 지휘 장교 {S('lOff')} 부사관 {S('lNco')} 없음 {S('lNone')} | "
              f"기병 돌격 {S('cavN')}회 효과 {A.mean(eff):+.2f} 정면/측면/후방 {S('cfront')}/{S('cflank')}/{S('crear')}")
