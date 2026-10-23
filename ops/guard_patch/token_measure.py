"""Parse a subagent jsonl: context size (input+cache_creation+cache_read) per assistant message; print deltas around each Read."""
import json, sys, glob, os
aid = sys.argv[1]
f = glob.glob(os.path.expanduser(f"~/.claude/projects/C--Users-seung/**/agent-{aid}.jsonl"), recursive=True)[0]
rows = []
for line in open(f, encoding="utf-8"):
    o = json.loads(line); m = o.get("message") or {}
    if m.get("role") != "assistant": continue
    u = m.get("usage") or {}
    ctx = (u.get("input_tokens", 0) or 0) + (u.get("cache_creation_input_tokens", 0) or 0) + (u.get("cache_read_input_tokens", 0) or 0)
    kinds = [b.get("type") + (":" + b.get("name", "") if b.get("type") == "tool_use" else ":" + (b.get("text", "")[:12] if b.get("type") == "text" else "")) for b in (m.get("content") or []) if isinstance(b, dict)]
    rows.append((ctx, u.get("output_tokens", 0), kinds))
for i, (c, o, k) in enumerate(rows): print(i, c, o, k)
print("--- deltas (ctx after read - ctx at the read call) ---")
for i in range(len(rows) - 1):
    if any(x.startswith("tool_use:Read") for x in rows[i][2]):
        print(i, "->", i + 1, rows[i + 1][0] - rows[i][0] - rows[i][1])
