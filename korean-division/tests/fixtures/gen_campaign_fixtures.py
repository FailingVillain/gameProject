#!/usr/bin/env python3
"""Regenerate tests/fixtures/campaign/campaign_v{N}.json (T49c).

Each historical sample = the populated CURRENT sample (dumped by `KD_SELFTEST_ONLY=compatgate KD_COMPAT_DUMP=<dir>`)
with every JSON key removed that did not exist as a property at the commit that introduced schema N (read from git history of
CampaignState.cs / MissionResults.cs). Run from the repo root: python korean-division/tests/fixtures/gen_campaign_fixtures.py <populated_current.json>
Add the new (schema, commit) pair below when a card raises CampaignState.SchemaVersion, then regenerate. Existing samples never change.
"""
import json, re, subprocess, sys, pathlib

# schema -> commit that introduced it (last entry = current; its sample is the unfiltered dump)
COMMITS = {1: "a1a35e6", 2: "06424c8", 3: "622db02", 4: "ba621fe", 5: "51b92c3", 6: "66e7637", 7: "0d5a676", 8: "e31406d", 9: "fa8dff1", 10: "fa8dff1", 11: "0b98b10", 12: "bd1dc11"}   # 10 = skipped schema (no real v10 format; same shape as 9, gate requires every number)
FILES = ["korean-division/src/Core/Campaign/CampaignState.cs", "korean-division/src/Core/Mission/MissionResults.cs"]
PROP = re.compile(r"public\s+[\w<>\?,\.\[\] ]+?\s+(\w+)\s*\{\s*get;")

def names(commit):
    out = set()
    for f in FILES:
        try:
            txt = subprocess.run(["git", "show", f"{commit}:{f}"], capture_output=True, text=True, encoding="utf-8", check=True).stdout
        except subprocess.CalledProcessError:
            continue
        out |= set(PROP.findall(txt))
    return out

def strip(o, keep, universe):
    if isinstance(o, dict):
        return {k: strip(v, keep, universe) for k, v in o.items() if not (k in universe and k not in keep)}
    if isinstance(o, list):
        return [strip(v, keep, universe) for v in o]
    return o

cur = json.load(open(sys.argv[1], encoding="utf-8"))
per = {v: names(c) for v, c in COMMITS.items()}
universe = set().union(*per.values())
out = pathlib.Path(__file__).parent / "campaign"
out.mkdir(exist_ok=True)
for v, keep in per.items():
    d = strip(cur, keep, universe)
    d["Version"] = v
    (out / f"campaign_v{v}.json").write_text(json.dumps(d, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(v, len(json.dumps(d)), "bytes")
