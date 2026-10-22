"""Muzzle/limb clipping check: lists sprite cells whose opaque pixels touch the frame border (a clipped silhouette).
python tools/edge_check.py <unit_dir> <unit_name> [<tag>]   e.g. ..\korean-division-image-out\_res96\korean\infantry-pioneer infantry-pioneer
Exit code 1 if any cell touches. Reads per-sheet frame size when a layout has one."""
import json, os, sys
import numpy as np
from PIL import Image


def touching(d, name, tag=""):
    L = json.load(open(os.path.join(d, f"{name}{tag}_layout.json"), encoding="utf-8"))
    sheets = {s["file"]: s for s in L["sheets"]}
    imgs = {}
    bad = []
    for fr, c in L["cells"].items():
        s = sheets[c["sheet"]]
        fw, fh = s.get("frame", L["frame"])
        if c["sheet"] not in imgs:
            imgs[c["sheet"]] = np.array(Image.open(os.path.join(d, c["sheet"])))[..., 3]
        for r, dn in enumerate(L["dirs"]):
            cell = imgs[c["sheet"]][r * fh:(r + 1) * fh, c["col"] * fw:(c["col"] + 1) * fw]
            e = "".join(k for k, v in (("L", cell[:, 0]), ("R", cell[:, -1]), ("T", cell[0]), ("B", cell[-1])) if v.any())
            if e:
                bad.append((dn, fr, e))
    return bad


if __name__ == "__main__":
    bad = touching(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "")
    print(f"{len(bad)} cells touch the frame border" + ("" if not bad else ": " + " ".join(f"{d}/{f}({e})" for d, f, e in bad)))
    sys.exit(1 if bad else 0)
