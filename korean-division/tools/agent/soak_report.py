"""Summarize a soak folder (docs/soak/<date>/) into report.md (stdlib only, no LLM).

usage: python soak_report.py <soak_dir> [--skip N]
  --skip N  ignore the first N data rows of results.csv (bogus early runs)
Reads results.csv (time,kind,name,seed,exit,sec,status,args) and runs/<kind>-<name>-<seed>.log.
Writes <soak_dir>/report.md and prints the same text (capped at 120 lines).
"""
import os
import re
import sys
from collections import defaultdict

MAX_LINES = 120
MEAS_RE = re.compile(r"MEAS .*?winner=(-?\d+) lossEnd=(-?\d+)/(-?\d+).*?shots=(\d+)")
ROW_RE = re.compile(r"^[ 	]+([0-9]+):(\S+)[ 	]+(\d+)→(\d+)[ 	]+(\d+)", re.M)
MISSION_RE = re.compile(r"\[mission-run\] (\S+) '.*?': (won|lost|running)")


def tag_of(kind, name, seed):
    return re.sub(r"[^\w\-]", "_", f"{kind}-{name}-{seed}")


def read_text(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return f.read()
    except OSError:
        return None


def mode_of(args):
    if "KD_SIM_TRENCH=1" in args:
        return "trench"
    if "KD_SIM_DEFEND=1" in args:
        return "defend"
    return "field"


def load_rows(soak_dir, skip):
    rows = []
    path = os.path.join(soak_dir, "results.csv")
    with open(path, encoding="utf-8-sig", newline="") as f:
        lines = f.read().splitlines()
    # split on the first 7 commas only: args may contain commas on old rows
    for line in lines[1:]:
        parts = line.split(",", 7)
        if len(parts) < 7 or parts[1] not in ("sim", "mission"):
            continue
        rows.append({"kind": parts[1], "name": parts[2], "seed": parts[3],
                     "status": parts[6].strip(), "args": parts[7] if len(parts) > 7 else ""})
    return rows[skip:]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    soak_dir = sys.argv[1]
    skip = int(sys.argv[sys.argv.index("--skip") + 1]) if "--skip" in sys.argv else 0
    rows = load_rows(soak_dir, skip)
    runs_dir = os.path.join(soak_dir, "runs")
    out = [f"# Soak report: {os.path.basename(os.path.normpath(soak_dir))} (first {skip} rows skipped)", ""]

    # --- run counts
    counts = defaultdict(lambda: defaultdict(int))
    for r in rows:
        counts[r["kind"]][r["status"]] += 1
        counts[r["kind"]]["total"] += 1
    out.append("## Runs")
    for kind in ("mission", "sim"):
        c = counts[kind]
        out.append(f"- {kind}: total {c['total']}  OK {c['OK']}  FAIL {c['FAIL']}  TIMEOUT {c['TIMEOUT']}  other {c['total'] - c['OK'] - c['FAIL'] - c['TIMEOUT']}")
    out.append("")

    # --- missions
    mis = defaultdict(lambda: defaultdict(int))
    mis_bad = []
    for r in rows:
        if r["kind"] != "mission":
            continue
        m = mis[r["name"]]
        m["runs"] += 1
        if r["status"] != "OK":
            m["csvfail"] += 1
        text = read_text(os.path.join(runs_dir, tag_of("mission", r["name"], r["seed"]) + ".log"))
        found = MISSION_RE.search(text) if text else None
        m[found.group(2) if found else "none"] += 1
        if found and found.group(2) == "running":
            mis_bad.append(f"{r['name']} seed {r['seed']} still running at end")
    out.append("## Missions (outcome per log; nonOK = csv status not OK: exit 3 = still running at --seconds limit)")
    out.append(f"{'mission':<24}{'runs':>5}{'won':>5}{'lost':>5}{'run':>5}{'none':>5}{'nonOK':>8}  won%")
    for name in sorted(mis):
        m = mis[name]
        decided = m["won"] + m["lost"]
        wp = f"{100 * m['won'] / decided:.0f}" if decided else "-"
        out.append(f"{name:<24}{m['runs']:>5}{m['won']:>5}{m['lost']:>5}{m['running']:>5}{m['none']:>5}{m['csvfail']:>8}  {wp}")
    out.append("")

    # --- sims
    draw_tbl = defaultdict(lambda: [0, 0])  # (map, mode) -> [n, draws]
    unit = defaultdict(lambda: {"n": 0, "win": 0, "draw": 0, "surv": []})
    anomalies = []
    for r in rows:
        if r["kind"] != "sim":
            continue
        tag = tag_of("sim", r["name"], r["seed"])
        text = read_text(os.path.join(runs_dir, tag + ".log"))
        if not text:
            continue
        mp, md = r["name"], mode_of(r["args"])
        meas = MEAS_RE.search(text)
        if meas:
            winner, la, lb, shots = int(meas.group(1)), int(meas.group(2)), int(meas.group(3)), int(meas.group(4))
        else:
            winner, la, lb, shots = None, -1, -1, -1
            anomalies.append((0, f"{tag}: no MEAS line"))
        draw_tbl[(mp, md)][0] += 1
        if winner == 0:
            draw_tbl[(mp, md)][1] += 1
        if shots == 0:
            anomalies.append((0, f"{tag}: 0 shots"))
        if la >= 0 and lb >= 0:
            if (la >= 10 and lb == 0) or (lb >= 10 and la == 0):
                anomalies.append((1, f"{tag}: one-sided losses {la}/{lb}"))
            elif la >= 10 and lb >= 5 and max(la / lb, lb / la) >= 10:
                anomalies.append((2, f"{tag}: loss ratio {la}/{lb} (x{max(la / lb, lb / la):.0f})"))
        for m in ROW_RE.finditer(text):
            side, typ, start, left = m.group(1), m.group(2), int(m.group(3)), int(m.group(4))
            if start <= 0:
                continue
            u = unit[(side, typ)]
            u["n"] += 1
            u["surv"].append(100.0 * left / start)
            if winner is not None and str(winner) == side:
                u["win"] += 1
            if winner == 0:
                u["draw"] += 1

    out.append("## Sims: draw rate by map x mode (draw = MEAS winner=0)")
    out.append(f"{'map':<10}{'mode':<8}{'n':>5}{'draws':>7}{'draw%':>7}")
    for (mp, md) in sorted(draw_tbl):
        n, d = draw_tbl[(mp, md)]
        out.append(f"{mp:<10}{md:<8}{n:>5}{d:>7}{100 * d / n:>6.0f}%")
    out.append("")

    unit_rows = sorted(unit.items(), key=lambda kv: (-kv[1]["n"], kv[0]))
    shown = unit_rows[:30]
    out.append(f"## Units (side:type, sorted by appearances; win% = side won / appearances)  [{len(unit_rows)} types]")
    out.append(f"{'side:type':<30}{'n':>5}{'win%':>6}{'draw%':>7}{'surv%':>7}")
    for (side, typ), u in shown:
        wp = 100 * u["win"] / u["n"]
        dp = 100 * u["draw"] / u["n"]
        sv = sum(u["surv"]) / len(u["surv"])
        out.append(f"{side + ':' + typ:<30}{u['n']:>5}{wp:>5.0f}%{dp:>6.0f}%{sv:>6.0f}%")
    if len(unit_rows) > len(shown):
        out.append(f"... {len(unit_rows) - len(shown)} more types not shown")
    out.append("")

    # --- anomalies
    out.append(f"## Anomalies ({len(anomalies)} sims, {len(mis_bad)} missions)")
    kinds = defaultdict(int)
    for k, _ in anomalies:
        kinds[k] += 1
    out.append(f"- counts: 0-shot/no-MEAS {kinds[0]}, one-sided wipe {kinds[1]}, loss ratio >=10x {kinds[2]}")
    for _, a in sorted(anomalies, key=lambda x: x[0])[:8]:
        out.append(f"- {a}")
    for a in mis_bad[:5]:
        out.append(f"- {a}")
    out.append("")

    if len(out) > MAX_LINES:
        out = out[:MAX_LINES - 1] + [f"... truncated to {MAX_LINES} lines (full list in report.md is the same text)"]
    text = "\n".join(out) + "\n"
    with open(os.path.join(soak_dir, "report.md"), "w", encoding="utf-8") as f:
        f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stdout.write(text)


if __name__ == "__main__":
    main()
