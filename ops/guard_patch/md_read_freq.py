"""Count how often .md files were Read/Edited/Grepped by Claude sessions (tool_use inputs in ~/.claude/projects/**/*.jsonl). Read-only analysis."""
import glob, json, os, re, collections, sys
root = os.path.expanduser("~/.claude/projects")
reads = collections.Counter(); sessions = collections.defaultdict(set); other = collections.Counter()
files = glob.glob(os.path.join(root, "**", "*.jsonl"), recursive=True)
for f in files:
    sid = os.path.basename(f)
    try:
        for line in open(f, encoding="utf-8", errors="ignore"):
            if ".md" not in line: continue
            try: o = json.loads(line)
            except Exception: continue
            msg = o.get("message") or {}
            c = msg.get("content")
            if not isinstance(c, list): continue
            for b in c:
                if not isinstance(b, dict) or b.get("type") != "tool_use": continue
                n = b.get("name"); inp = b.get("input") or {}
                p = inp.get("file_path") or inp.get("path") or ""
                if isinstance(p, str) and p.lower().endswith(".md"):
                    p = p.replace("\\", "/")
                    if n == "Read": reads[p] += 1; sessions[p].add(sid)
                    elif n in ("Edit", "Write"): other[p] += 1
    except Exception as e:
        print("skip", f, e, file=sys.stderr)
print("jsonl files:", len(files))
print("--- top 45 Read counts (reads, sessions, edits) ---")
for p, n in reads.most_common(45):
    print(f"{n:5d} {len(sessions[p]):4d} {other.get(p,0):4d} {p}")
# md files never Read
allmd = []
for pat in ["C:/gameProject/ops/*.md", "C:/gameProject/korean-division/docs/*.md", "C:/gameProject/korean-division/tools/agent/*.md"]:
    allmd += [x.replace("\\", "/") for x in glob.glob(pat)]
norm = {k.lower(): v for k, v in reads.items()}
never = [(os.path.getsize(p) // 1024, p) for p in allmd if p.lower() not in norm]
never.sort(reverse=True)
print("--- never Read (KB, file) top 40 of", len(never), "---")
for k, p in never[:40]: print(f"{k:5d} {p}")
