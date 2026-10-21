# 14차 집계 (N35 장교 교전 회피 · 정찰병 자위). 사용: python -X utf8 -I tools/tobs_agg14.py docs/tactics_obs_raw14 OLD NEW
import re, sys, os, glob
d = sys.argv[1]
for v in sys.argv[2:]:
    rows = {}
    for f in sorted(glob.glob(os.path.join(d, f"{v}_*.txt"))):
        t = open(f, encoding="utf-8").read()
        sc = os.path.basename(f)[len(v) + 1:-4]
        won = 1 if re.search(r": won at", t) else 0
        lost = [int(x) for x in re.findall(r"faction \d+ .*?lost (\d+)", t)]
        none = sum(int(x) for x in re.findall(r"없음 (\d+), 인계 중", t))
        m = re.search(r"혼자 돌격 (\d+), 막은 명령 돌격 (\d+)", t)
        ch = sum(int(x) for x in re.findall(r"(?:auto|autoNear|march|order|chargeOnly) (\d+)\(", t.split("[장교교전]")[1].split("\n")[0])) if "[장교교전]" in t else 0
        os_ = re.search(r"\[장교사격\].*합계 (\d+), 자위 밖 (\d+)", t)
        ss = re.search(r"\[정찰사격\].*합계 (\d+), 자위 밖 (\d+)", t)
        pos = re.search(r"\[장교위치\] 시작 장교 (\d+), 전사 (\d+).*평균 ([\d.]+) 중앙 ([\d.]+) p90 ([\d.]+) 최대 ([\d.]+), 8칸 넘음 (\d+)", t)
        rows[sc] = dict(won=won, ke=lost[0] / max(1, lost[1]) if len(lost) >= 2 else 0, none=none, solo=int(m.group(1)), blocked=int(m.group(2)), ch=ch,
                        osh=int(os_.group(1)), obad=int(os_.group(2)), ssh=int(ss.group(1)), sbad=int(ss.group(2)),
                        off=int(pos.group(1)), dead=int(pos.group(2)), avg=float(pos.group(3)), p90=float(pos.group(5)), mx=float(pos.group(6)), far=int(pos.group(7)))
    print(f"== {v} ({len(rows)}판)")
    for k in ("A", "B", "C"):
        r = [x for s, x in rows.items() if s.startswith(k)]
        if not r: continue
        n = len(r); S = lambda key: sum(x[key] for x in r)
        print(f"{k}: 승 {S('won')}/{n}, K/E {S('ke')/n:.2f}, 장교 돌격 {S('ch')} (혼자 {S('solo')}, 막음 {S('blocked')}), 장교 사격/판 {S('osh')/n:.1f} (자위 밖 {S('obad')/n:.1f}), 정찰 사격/판 {S('ssh')/n:.1f} (자위 밖 {S('sbad')/n:.1f}), 장교 전사 {S('dead')}/{S('off')}, 지휘없음 중대-초/판 {S('none')/n:.0f}, 장교-중대 거리 평균 {S('avg')/n:.1f} p90 {S('p90')/n:.1f} 최대 {max(x['mx'] for x in r):.1f}, 8칸 넘음 표본/판 {S('far')/n:.0f}")
