# 제압 재설계 집계: python -X utf8 -I tools/supp_agg.py docs/supp_raw BASE SUPP
# tactics-obs A/B/C: 승 · K/E · 시간 초과 · 접적 사격 비율 (f1_agg 와 같은 정의) · S1 한국군 돌격 40초 내 후퇴율 · [제압2] 판당 평균
# --balance sim (<tag>_sim.txt): RR = 소총 대 소총, LT = 소총+경기관총 공격 대 참호 소총 (A 승 / 시간 초과 / 손실 A·B)
import re, sys, os, glob
d = sys.argv[1]
for v in sys.argv[2:]:
    print(f"== {v}")
    for k in ("A", "B", "C"):
        fs = sorted(glob.glob(os.path.join(d, f"{v}_{k}[0-9]*.txt")))
        if not fs: continue
        won = to = 0; L = E = 0; sec = {1: 0.0, 2: 0.0}; fire = {1: 0.0, 2: 0.0}; asd = asr = 0; sup = {}
        for f in fs:
            t = open(f, encoding="utf-8").read()
            won += 1 if re.search(r": won at", t) else 0
            to += 1 if re.search(r"running at", t) else 0
            lost = [int(x) for x in re.findall(r"faction \d+ .*?lost (\d+)", t)]
            if len(lost) >= 2: L += lost[0]; E += lost[1]
            m = re.search(r"\[F1\] (.*)", t)
            if m:
                for fs_, s_, fr_ in re.findall(r"세력(\d) 접적 보병-초 (\d+), 사격 비율 ([\d.]+)", m.group(1)):
                    sec[int(fs_)] += float(s_); fire[int(fs_)] += float(s_) * float(fr_)
            r = re.search(r"\[돌격결과\] 완료 (\d+): 후퇴 (\d+)", t)   # 첫 줄 = 세력 1 (한국군)
            if r: asd += int(r.group(1)); asr += int(r.group(2))
            for f_, rest in re.findall(r"\[제압2\] 세력(\d) (.*)", t):
                for name, val in re.findall(r"([가-힣]+) ([\d.]+)", rest):
                    key = f"{f_}:{name}"; sup[key] = sup.get(key, 0) + float(val)
        n = len(fs)
        fr = " / ".join(f"{fire[f] / sec[f]:.3f}" if sec[f] else "-" for f in (1, 2))
        print(f"{k}: 승 {won}/{n}, K/E {L}/{E} = {L / max(1, E):.2f}, 시간 초과 {to}/{n}, 접적 사격 비율 {fr}, S1 돌격 후퇴 {asr}/{asd} = {100 * asr / max(1, asd):.0f}%")
        if sup: print("   [제압2] " + " · ".join(f"{kk} {vv / n:.0f}" for kk, vv in sup.items()) + " (판당)")
    sf = os.path.join(d, f"{v}_sim.txt")
    if os.path.exists(sf):
        for kind in ("RR", "LT"):
            rows = [l for l in open(sf, encoding="utf-8") if re.search(rf" {kind}\d ", l)]
            wa = sum(1 for l in rows if "winner=1" in l); tt = sum(1 for l in rows if "winner=0" in l)
            la = lb = 0; tsum = 0.0
            for l in rows:
                m = re.search(r"lossEnd=(\d+)/(\d+)", l); la += int(m.group(1)); lb += int(m.group(2)); tsum += float(re.search(r"end_t=(\d+)", l).group(1))
            print(f"sim {kind}: A 승 {wa}/{len(rows)}, 시간 초과 {tt}, 손실 A/B {la}/{lb}, 평균 {tsum / max(1, len(rows)):.0f}초")
