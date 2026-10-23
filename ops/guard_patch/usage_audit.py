"""Token usage audit for Claude Code session logs (read-only). Sums usage by session file and by model for a date prefix, and flags patterns:
cache_creation churn, big tool results, repeated Reads of the same file."""
import glob, json, os, sys, collections, datetime
date = sys.argv[1] if len(sys.argv) > 1 else "2026-10-23"
root = os.path.expanduser("~/.claude/projects/C--Users-seung")
files = glob.glob(os.path.join(root, "**", "*.jsonl"), recursive=True)
tot = collections.Counter(); bymodel = collections.defaultdict(collections.Counter); byfile = {}
big = []; reads = collections.Counter(); toolres = collections.Counter()
for f in files:
    c = collections.Counter(); n = 0
    try:
        for line in open(f, encoding="utf-8", errors="ignore"):
            if date not in line: continue
            try: o = json.loads(line)
            except Exception: continue
            if not str(o.get("timestamp", "")).startswith(date): continue
            msg = o.get("message") or {}
            u = msg.get("usage")
            if u and msg.get("role") == "assistant":
                n += 1
                for k in ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens", "output_tokens"):
                    c[k] += u.get(k, 0) or 0
                    bymodel[msg.get("model", "?")][k] += u.get(k, 0) or 0
            cont = msg.get("content")
            if isinstance(cont, list):
                for b in cont:
                    if not isinstance(b, dict): continue
                    if b.get("type") == "tool_use" and b.get("name") == "Read":
                        reads[(os.path.basename(f), (b.get("input") or {}).get("file_path", ""))] += 1
                    if b.get("type") == "tool_result":
                        t = b.get("content")
                        s = len(t) if isinstance(t, str) else sum(len(x.get("text", "")) for x in t if isinstance(x, dict)) if isinstance(t, list) else 0
                        if s > 20000: big.append((s, os.path.basename(f)))
    except Exception as e:
        pass
    if n:
        byfile[f] = (n, c)
        tot.update(c)
print("date", date, "files with usage:", len(byfile))
print("TOTAL", {k: f"{v:,}" for k, v in tot.items()})
print("by model:")
for m, c in bymodel.items(): print("  ", m, {k: f"{v:,}" for k, v in c.items()})
print("top 12 sessions by (input+cache_create+output):")
rows = sorted(byfile.items(), key=lambda kv: -(kv[1][1]["input_tokens"] + kv[1][1]["cache_creation_input_tokens"] + kv[1][1]["output_tokens"]))[:12]
for f, (n, c) in rows:
    rel = os.path.relpath(f, root)
    print(f"  {n:5d} msgs  in={c['input_tokens']:>10,} cc={c['cache_creation_input_tokens']:>11,} cr={c['cache_read_input_tokens']:>13,} out={c['output_tokens']:>9,}  {rel[:90]}")
print("tool results >20k chars:", len(big), "total chars", sum(s for s, _ in big))
for s, f in sorted(big, reverse=True)[:8]: print("  ", s, f[:40])
print("files Read >=4 times in one session:")
for (f, p), n in reads.most_common(12):
    if n >= 4: print("  ", n, f[:12], p[-80:])
