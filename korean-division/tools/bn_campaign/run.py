#!/usr/bin/env python3
"""Battalion campaign runner (one command, sequential, resumable).

  python tools/bn_campaign/run.py [--exe bin/bncamp/KoreanDivision.exe] [--out docs/bncamp_raw] [--seeds 1-6]
                                  [--filter REGEX] [--gate path/to/heavy_gate.ps1] [--gate-every 20] [--seconds 600] [--aggregate]

Build the exe first (headless console Release):
  dotnet build -c Release -p:OutputType=Exe -p:IntermediateOutputPath=obj/bncamp/ -p:OutputPath=bin/bncamp/
Runs ONE game process at a time. Output per run: <out>/runs/<config>_s<seed>.json.gz (BnCamp metrics) and .txt.gz (TacticsObs report).
Re-running skips finished runs (delete the file to redo). `--aggregate` calls aggregate.py afterwards.
"""
import argparse, gzip, json, os, re, shutil, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
import gen_scen


def gz(src, dst):
    with open(src, 'rb') as f, gzip.open(dst, 'wb', 6) as g:
        shutil.copyfileobj(f, g)
    os.remove(src)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--exe', default=os.path.join(ROOT, 'bin', 'bncamp', 'KoreanDivision.exe'))
    ap.add_argument('--out', default=os.path.join(ROOT, 'docs', 'bncamp_raw'))
    ap.add_argument('--seeds', default=None, help='e.g. 1-6 or 1,2,3 (default: gen_scen.SEEDS)')
    ap.add_argument('--filter', default=None)
    ap.add_argument('--gate', default=os.environ.get('BN_GATE'))
    ap.add_argument('--gate-every', type=int, default=20)
    ap.add_argument('--seconds', type=int, default=600)
    ap.add_argument('--aggregate', action='store_true')
    ap.add_argument('--gen', default='gen_scen', help='scenario generator module in this folder (gen_scen / gen_scen2)')
    ap.add_argument('--prof1', default=None, help='stage 3A A/B: aiProfile for faction 1 (e.g. korean+stage2)')
    ap.add_argument('--prof2', default=None, help='aiProfile for faction 2 (e.g. fengtian+stage2)')
    a = ap.parse_args()

    import importlib
    gen = importlib.import_module(a.gen)
    seeds = gen.SEEDS
    if a.seeds:
        seeds = []
        for part in a.seeds.split(','):
            if '-' in part:
                x, y = part.split('-'); seeds += list(range(int(x), int(y) + 1))
            else:
                seeds.append(int(part))
    scen = os.path.join(a.out, 'scen')
    runs = os.path.join(a.out, 'runs')
    os.makedirs(runs, exist_ok=True)
    L = gen.cfgs()
    os.makedirs(scen, exist_ok=True)
    import json as _j
    for c in L:
        mi = gen.mission(c)
        for k, pv in ((0, a.prof1), (1, a.prof2)):
            if pv: mi['factions'][k]['aiProfile'] = pv
        _j.dump(mi, open(os.path.join(scen, c['id'] + '.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    _j.dump({'seeds': seeds, 'configs': L}, open(os.path.join(a.out, 'matrix.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
    if a.filter:
        L = [c for c in L if re.search(a.filter, c['id'])]
    try:
        commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip()
        dirty = subprocess.check_output(['git', 'status', '--porcelain', '--', 'src', 'assets'], cwd=ROOT).decode().strip() != ''
    except Exception:
        commit, dirty = 'unknown', False
    json.dump({'commit': commit, 'dirtySrc': dirty, 'seeds': seeds, 'seconds': a.seconds, 'prof1': a.prof1, 'prof2': a.prof2, 'started': time.strftime('%Y-%m-%d %H:%M:%S')},
              open(os.path.join(a.out, 'manifest.json'), 'w'), indent=1)
    todo = [(c, s) for c in L for s in (seeds if a.seeds else (c.get('seeds') or seeds)) if not os.path.exists(os.path.join(runs, f"{c['id']}_s{s}.json.gz"))]
    print(f"{len(L)} configs, {len(todo)} runs to do (commit {commit[:8]}{' +dirty' if dirty else ''})", flush=True)
    t0 = time.time(); log = open(os.path.join(a.out, 'runlog.tsv'), 'a')
    for i, (c, s) in enumerate(todo):
        if a.gate and i % a.gate_every == 0:
            g = subprocess.run(['powershell', '-ExecutionPolicy', 'Bypass', '-File', a.gate], check=False)
            if g.returncode != 0:   # gate overloaded (75) / timeout (1): NOT RUN - never start heavy work past a closed gate; rerun resumes (done cells are skipped)
                print(f'heavy gate exit {g.returncode} (75 = overloaded, 1 = timeout): stopping before run {i + 1}/{len(todo)}; rerun later to resume', flush=True)
                sys.exit(75)
        tag = f"{c['id']}_s{s}"
        j = os.path.join(runs, tag + '.json'); t = os.path.join(runs, tag + '.txt')
        env = dict(os.environ, KD_BN_OUT=j); env.update(c.get('env') or {})
        w0 = time.time()
        r = subprocess.run([a.exe, '--tactics-obs', os.path.join(scen, c['id'] + '.json'), '--seed', str(s), '--seconds', str(a.seconds), '--out', t],
                           env=env, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, cwd=ROOT)
        wall = time.time() - w0
        if r.returncode != 0 or not os.path.exists(j):
            print('FAILED', tag, r.returncode, r.stderr.decode(errors='replace')[:300], flush=True)
            log.write(f"{tag}\tFAIL\t{wall:.1f}\n"); log.flush(); continue
        gz(j, j + '.gz'); gz(t, t + '.gz')
        log.write(f"{tag}\tok\t{wall:.1f}\n"); log.flush()
        if i % 10 == 0:
            print(f"[{i + 1}/{len(todo)}] {tag} {wall:.1f}s elapsed {(time.time() - t0) / 60:.1f} min", flush=True)
    print(f"done in {(time.time() - t0) / 60:.1f} min", flush=True)
    if a.aggregate:
        subprocess.run([sys.executable, os.path.join(HERE, 'aggregate.py'), '--raw', a.out])


if __name__ == '__main__':
    main()
