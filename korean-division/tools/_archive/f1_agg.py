# F1 집계. 사용: python -X utf8 -I tools/f1_agg.py docs/f1_raw BASE F1
# 승 · K/E (한국 손실 / 봉천 손실) · [F1] 접적 사격 비율 · 분당 이동 (세력 1/2, 접적 보병-초 가중) · 슬롯 복귀 · 빈자리 재배치 · 대열정비 병사 (판당)
import re, sys, os, glob
d = sys.argv[1]
for v in sys.argv[2:]:
    print(f"== {v}")
    for k in ("A", "B", "C"):
        fs = sorted(glob.glob(os.path.join(d, f"{v}_{k}[0-9]*.txt")))
        if not fs: continue
        won = 0; L = E = 0; sec = {1: 0.0, 2: 0.0}; fire = {1: 0.0, 2: 0.0}; mv = {1: 0.0, 2: 0.0}; cnt = {}
        for f in fs:
            t = open(f, encoding="utf-8").read()
            won += 1 if re.search(r": won at", t) else 0
            lost = [int(x) for x in re.findall(r"faction \d+ .*?lost (\d+)", t)]
            if len(lost) >= 2: L += lost[0]; E += lost[1]
            m = re.search(r"\[F1\] (.*)", t)
            if not m: continue
            for fs_, s_, fr_, mv_ in re.findall(r"세력(\d) 접적 보병-초 (\d+), 사격 비율 ([\d.]+), 분당 이동 ([\d.]+)칸", m.group(1)):
                f_ = int(fs_); s = float(s_); sec[f_] += s; fire[f_] += s * float(fr_); mv[f_] += s * float(mv_)
            for kk, vv in re.findall(r"([가-힣 ]+?) (\d+)(?:,|$)", m.group(1).split("||")[1]):
                cnt[kk.strip()] = cnt.get(kk.strip(), 0) + int(vv)
        n = len(fs)
        fr = " / ".join(f"{fire[f] / sec[f]:.3f}" if sec[f] else "-" for f in (1, 2))
        mvs = " / ".join(f"{mv[f] / sec[f]:.2f}" if sec[f] else "-" for f in (1, 2))
        print(f"{k}: 승 {won}/{n}, K/E {L}/{E} = {L / max(1, E):.2f}, 접적 사격 비율 {fr}, 분당 이동 {mvs}칸, 접적 보병-초 {sec[1] / n:.0f}/{sec[2] / n:.0f}")
        print("   " + " · ".join(f"{kk} {vv / n:.0f}" for kk, vv in cnt.items()) + " (판당)")
        to = sum(1 for f in fs if re.search(r"running at", open(f, encoding="utf-8").read()))
        cm, ccm = cnt.get("중대분", 0), cnt.get("접적 중대분", 0)
        if cm: print(f"   [F2] 시간 초과 {to}/{n}, 중대-분당 명령 {cnt.get('중대 명령', 0) / cm:.3f}, 접적 중대-분당 명령 {cnt.get('접적 중대 명령', 0) / max(1, ccm):.3f}")
