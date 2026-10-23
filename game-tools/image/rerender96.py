"""Full re-render queue (96x120 mode): python rerender96.py [--group A|B|deferred|all] [--only a,b] [--list] [--limit N]
Renders units one at a time (heavy_gate -Render before each) with make_unit.py --res96 into korean-division-image-out/_res96/,
appends ONE line per unit to RERENDER_PROGRESS.txt ("R96 <unit> rc=0 collide_hits=N secs=S"). Units with an R96 line are skipped (resumable).
Group A = voxel-infantry bases (Res-driven, supported now). Group B (cavalry/artillery/crew/wagon/vehicle own Res) = needs per-base 1.5x LODS first.
Deferred = units other agents (lookfix, newunits, horseart) are changing: render last."""
import argparse, os, re, subprocess, sys, time
HERE = os.path.dirname(os.path.abspath(__file__))
GT = os.path.dirname(HERE)
CODING = os.path.dirname(GT) if os.path.basename(GT) == "game-tools" else os.path.join(os.path.dirname(os.path.dirname(GT)), "game-tools")
GATE = os.path.join(os.path.dirname(GT) if os.path.basename(GT) == "game-tools" else os.path.dirname(os.path.dirname(GT)), "korean-division", "tools", "agent", "heavy_gate.ps1")
OUT = os.path.join(CODING, "korean-division-image-out", "_res96")
PROG = os.path.join(HERE, "RERENDER_PROGRESS.txt")      # tracked file in the repo's image/ folder
DEFER = {"infantry-bandit", "infantry-thug-bandit", "infantry-dadao-fengtian", "infantry-gando", "infantry-scout", "officer-bandit"}
SKIP = {"badge-fengtian", "badge-japanese", "badge-korean", "badge-russian-red", "badge-russian-white", "infantry-ww2", "infantry-default-kepi", "infantry-default-darkgrey", "officer-kepi", "officer-darkgrey"}
GROUP_A = ("standard-bearer-japanese standard-bearer infantry-regular infantry-default infantry-guard infantry-reserve infantry-japanese infantry-russian-red infantry-russian-white infantry-fengtian infantry-guard-fengtian "
           "infantry-sniper infantry-sniper-fengtian infantry-sniper-japanese infantry-sniper-russian-red infantry-sniper-russian-white "
           "standard-bearer-fengtian standard-bearer-russian-red standard-bearer-russian-white "
           "officer officer-fengtian officer-japanese officer-russian-red officer-russian-white infantry-assault "
           "heavy-machinegunner heavy-machinegunner-chauchat heavy-machinegunner-chauchat-fengtian heavy-machinegunner-fengtian heavy-machinegunner-japanese heavy-machinegunner-russian-red heavy-machinegunner-russian-white "
           "heavy-at-rifleman heavy-at-rifleman-fengtian heavy-at-rifleman-japanese heavy-at-rifleman-russian-red heavy-at-rifleman-russian-white").split()
GROUP_B = ("cavalry-default cavalry-fengtian cavalry-japanese cavalry-cossack cavalry-russian-red cavalry-russian-white cavalry-scout cavalry-officer cavalry-officer-fengtian cavalry-officer-japanese cavalry-officer-russian-red cavalry-officer-russian-white "
           "heavy-field-gun heavy-field-gun-fengtian heavy-field-gun-japanese heavy-field-gun-russian-red heavy-field-gun-russian-white heavy-infantry-gun heavy-infantry-gun-fengtian heavy-infantry-gun-japanese heavy-infantry-gun-russian-red heavy-infantry-gun-russian-white "
           "heavy-howitzer heavy-howitzer-fengtian heavy-howitzer-japanese heavy-howitzer-russian-red heavy-howitzer-russian-white "
           "heavy-maxim heavy-maxim-fengtian heavy-maxim-japanese heavy-maxim-russian-red heavy-maxim-russian-white heavy-type3-japanese supply-wagon").split()
QUEUE = {"A": GROUP_A, "B": GROUP_B, "deferred": sorted(DEFER) + ["cavalry-mongol", "cavalry-bandit", "heavy-horse-artillery", "heavy-tachanka"]}


def done():
    if not os.path.isfile(PROG):
        return set()
    return {m.group(1) for l in open(PROG, encoding="utf-8") for m in [re.match(r"R96 (\S+) rc=0", l)] if m}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--group", default="A"); ap.add_argument("--only"); ap.add_argument("--list", action="store_true"); ap.add_argument("--limit", type=int, default=999)
    ap.add_argument("--shard", help="K/N: parallel driver; queue = B then A reversed (the plain forward driver eats A from the front), units claimed via _claims/<unit>.claim (O_EXCL), dynamic balance")
    a = ap.parse_args()
    q = a.only.split(",") if a.only else (sum((QUEUE[k] for k in ("A", "B", "deferred")), []) if a.group == "all" else list(QUEUE[a.group]))
    shard = None
    if a.shard:
        shard = tuple(int(x) for x in a.shard.split("/")); q = list(QUEUE["B"]) + list(reversed(QUEUE["A"])) if not a.only and a.group in ("A", "B") else q
        if a.group == "A" and not a.only: q = list(reversed(QUEUE["A"]))
        if a.group == "B" and not a.only: q = list(QUEUE["B"])
        k, n = shard; q = q[k:] + q[:k]      # stagger start so shards do not fight over the first claim
    d = done(); todo = [u for u in q if u not in d][:a.limit]
    print(f"{len(todo)} to render, {len([u for u in q if u in d])} already done"); 
    if a.list:
        print(" ".join(todo)); return
    cdir = os.path.join(OUT, "_claims"); os.makedirs(cdir, exist_ok=True)
    for u in todo:
        if shard:
            if u in done(): continue
            try: os.close(os.open(os.path.join(cdir, u + ".claim"), os.O_CREAT | os.O_EXCL | os.O_WRONLY))
            except FileExistsError: continue
        g = subprocess.run(["powershell", "-ExecutionPolicy", "Bypass", "-File", GATE, "-Render"], check=False)
        if g.returncode != 0:      # gate overloaded (75) / timeout (1): NOT RUN - release the claim, stop; a rerun resumes
            if shard:
                try: os.remove(os.path.join(cdir, u + ".claim"))
                except OSError: pass
            print(f"heavy gate exit {g.returncode} (75 = overloaded, 1 = timeout): render of {u} NOT run, stopping; rerun later")
            sys.exit(75)
        t = time.time()
        p = subprocess.run([sys.executable, "-u", os.path.join(HERE, "make_unit.py"), u, "--res96", "--out", OUT], capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=HERE)
        m = re.search(r"interpenetration check[^:]*: (?:clean|(\d+) frame/dir hits)", p.stdout or "")
        bad = (int(m.group(1) or 0) if m else -1)      # -1 = no check line (unit without collide)
        with open(PROG, "a", encoding="utf-8") as f:
            f.write(f"R96 {u} rc={p.returncode} collide_hits>1.5={bad} secs={int(time.time()-t)} {time.strftime('%m-%d %H:%M')}\n")
        print(u, p.returncode, bad, int(time.time() - t), flush=True)
        if p.returncode: print(p.stderr[-600:])


if __name__ == "__main__":
    main()
