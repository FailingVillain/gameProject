"""Frequency of recurring terms (English tokens/identifiers and Korean words) in the working docs, to pick abbreviation candidates. Read-only."""
import glob, re, collections
files = glob.glob("C:/gameProject/ops/*.md") + glob.glob("C:/gameProject/korean-division/docs/*.md") + glob.glob("C:/gameProject/korean-division/tools/agent/*.md") + glob.glob("C:/Users/seung/.claude/agents/*.md") + glob.glob("C:/Users/seung/.claude/projects/C--Users-seung/memory/*.md")
en = collections.Counter(); ko = collections.Counter(); phr = collections.Counter()
for f in files:
    t = open(f, encoding="utf-8", errors="ignore").read()
    for w in re.findall(r"[A-Za-z][A-Za-z0-9_\-/\.]{3,}", t):
        w = w.strip(".-/")
        if len(w) >= 5 and not w.lower().startswith(("http", "c:/")): en[w.lower()] += 1
    for w in re.findall(r"[가-힣]{2,}", t): ko[w] += 1
    for w in re.findall(r"[가-힣]+ [가-힣]+", t): phr[w] += 1
print("== EN top 60 =="); print(", ".join(f"{w}:{n}" for w, n in en.most_common(60)))
print("== KO top 60 =="); print(", ".join(f"{w}:{n}" for w, n in ko.most_common(60)))
print("== KO 2-word phrases top 30 =="); print(", ".join(f"{w}:{n}" for w, n in phr.most_common(30)))
