# 13차 (pursuit2) 집계 (후퇴 사격 · 재정비 · 포병 오사): python -I tools/tobs_agg13p.py docs/tactics_obs_raw13p P1 P2
# 기본 지표 (승 · K/E · 추격 · 기병) 는 tools/tobs_agg12.py 를 같은 폴더 · 변형에 돌린다.
import re, sys, glob, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]


def parse(p):
    t = open(p, encoding='utf-8').read()
    r = {}
    m = re.search(r'\[후퇴사격\] 발수 (\d+) 사살 (\d+) \| \[재정비\] 시작 (\d+)/(\d+) 사수판단 (\d+)/(\d+) 재전진 (\d+)/(\d+) 후퇴 (\d+)/(\d+)', t)
    if m:
        v = list(map(int, m.groups()))
        r.update(rshots=v[0], rkills=v[1], regroups=v[2] + v[3], holds=v[4] + v[5], readv=v[6] + v[7], fallbacks=v[8] + v[9])
    m = re.search(r'\[추격\] .*?후퇴 중 희생 사격 (\d+) 근접 (\d+)', t)
    if m:
        r.update(ffk=int(m.group(1)), fmk=int(m.group(2)))
    ff = re.search(r'\[아군오사\] (.*)', t).group(1)
    art = {'gun_infantry': [0, 0], 'gun_field': [0, 0], 'howitzer_heavy': [0, 0]}
    other = [0, 0]
    for k, h, kl in re.findall(r'(\w+) 명중 (\d+) 사살 (\d+) 피해', ff):
        tgt = art[k] if k in art else other
        tgt[0] += int(h); tgt[1] += int(kl)
    r['art'] = art; r['other'] = other
    return r


def mean(xs):
    xs = list(xs)
    return st.mean(xs) if xs else float('nan')


for v in sys.argv[2:]:
    R = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_[ABC][1-5].txt'))]
    if not R:
        continue
    print(f"{v}: n={len(R)}")
    if 'rshots' in R[0]:
        print(f"   후퇴 사격/판: 발수 {mean(r['rshots'] for r in R):.0f} 사살 {mean(r['rkills'] for r in R):.1f} | 후퇴 중 희생/판 사격 {mean(r['ffk'] for r in R):.1f} 근접 {mean(r['fmk'] for r in R):.1f}")
        print(f"   중대 후퇴 {sum(r['fallbacks'] for r in R)} (15판 합), 재정비 시작 {sum(r['regroups'] for r in R)}, 조건 미달 사수 판단 {sum(r['holds'] for r in R)}, 재전진 {sum(r['readv'] for r in R)}")
    ak = {k: sum(r['art'][k][1] for r in R) for k in R[0]['art']}
    ah = {k: sum(r['art'][k][0] for r in R) for k in R[0]['art']}
    print(f"   아군 오사 (15판 합) 포병 명중/사살: " + ", ".join(f"{k} {ah[k]}/{ak[k]}" for k in ak) + f" | 합 사살 {sum(ak.values())} (판당 {sum(ak.values())/len(R):.1f}), 그 밖 {sum(r['other'][0] for r in R)}/{sum(r['other'][1] for r in R)}")
    for sc in 'ABC':
        S = [parse(f) for f in sorted(glob.glob(f'{d}/{v}_{sc}[1-5].txt'))]
        if not S or 'rshots' not in S[0]:
            continue
        print(f"     {sc}: 후퇴 사격 사살 {sum(r['rkills'] for r in S)} · 후퇴 {sum(r['fallbacks'] for r in S)} · 재전진 {sum(r['readv'] for r in S)} · 포병 아군 사살 {sum(sum(x[1] for x in r['art'].values()) for r in S)}")
