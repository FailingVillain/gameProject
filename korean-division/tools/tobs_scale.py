# 6차: ai_kr_vs_ft_1000 을 가로로 2배 (연대 둘 나란히) 로 키워 tools/tobs_scen/ai_kr_vs_ft_2000.json 생성 (관찰 도구 전용)
import json, copy, sys
src = sys.argv[1]; dst = sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
W = d['mapData']['width']
d['id'] = 'ai_kr_vs_ft_2000'; d['title'] = '한국군 2000 대 봉천군 2000 (관찰용)'
d['mapData']['width'] = W * 2
def sh(p, dx): return [p[0] + dx, p[1]]
forces = d['forces']; new = []
for f in forces:
    g = copy.deepcopy(f); g['id'] += 'b'; g['at'] = sh(f['at'], W)
    if 'battalion' in g: g['battalion'] += 3
    new.append(g)
forces += new
paint = d['mapData']['paint']; np_ = []
for p in paint:
    q = copy.deepcopy(p)
    if 'rect' in q: q['rect'][0] += W
    if 'tiles' in q: q['tiles'] = [sh(t, W) for t in q['tiles']]
    if 'circle' in q: q['circle'][0] += W
    np_.append(q)
paint += np_
d['mapData']['starts'] = [[108, 134], [108, 26]]
for k, a in d['mapData'].get('areas', {}).items():
    if 'rect' in a: a['rect'][2] += W
json.dump(d, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
