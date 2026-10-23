"""Convert CRLF -> LF for the given repo-relative files (git HEAD versions use LF; translation agents' Write calls introduced CRLF)."""
import sys
repo = "C:/gameProject"
for p in sys.argv[1:]:
    full = repo + "/" + p
    b = open(full, "rb").read()
    n = b.count(b"\r\n")
    if n:
        open(full, "wb").write(b.replace(b"\r\n", b"\n"))
        print("fixed", p, n)
    else:
        print("already LF", p)
