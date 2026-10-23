"""Compare translated md files with their git HEAD originals: headings, table rows, links, file paths, backticked tokens, CRLF, size. Read-only."""
import subprocess, re, sys
repo = "C:/gameProject"
files = sys.argv[1:]
def orig(p):
    r = subprocess.run(["git", "-C", repo, "show", "HEAD:" + p], capture_output=True)
    return r.stdout.decode("utf-8", "ignore") if r.returncode == 0 else None
def feats(t):
    return dict(
        head=len(re.findall(r"^#{1,6} ", t, re.M)),
        rows=len(re.findall(r"^\|", t, re.M)),
        links=sorted(re.findall(r"\]\(([^)]+)\)", t)),
        paths=sorted(set(re.findall(r"[A-Za-z]:/[^\s`\"')|]+", t))),
        ticks=set(re.findall(r"`([^`\n]+)`", t)),
        crlf=t.count("\r\n"))
for p in files:
    o = orig(p)
    n = open(repo + "/" + p, encoding="utf-8", errors="ignore", newline="").read()
    if o is None: print(p, "no HEAD version"); continue
    a, b = feats(o), feats(n)
    msgs = []
    for k in ("head", "rows"):
        if a[k] != b[k]: msgs.append(f"{k} {a[k]}->{b[k]}")
    if a["links"] != b["links"]: msgs.append(f"links diff: -{set(a['links'])-set(b['links'])} +{set(b['links'])-set(a['links'])}")
    pm = set(a["paths"]) - set(b["paths"])
    if pm: msgs.append(f"paths lost: {sorted(pm)[:5]}")
    tm = a["ticks"] - b["ticks"]
    if tm: msgs.append(f"ticks changed({len(tm)}): {sorted(tm)[:6]}")
    if a["crlf"] != b["crlf"]: msgs.append(f"CRLF {a['crlf']}->{b['crlf']}")
    print(("OK   " if not msgs else "CHECK"), p, "; ".join(msgs))
