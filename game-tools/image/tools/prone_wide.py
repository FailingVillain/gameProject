"""Wide prone cells: render go_prone / aim_prone / fire_prone in a 1.25x cell (160x200 @128, 80x100 @64, 40x50 @32) into their own
sheet <name>[@N]_prone.png (+ _team) and patch <name>[@N]_layout.json (sheet entry with "frame"/"anchor", cells -> new sheet) and the
anchors json in an existing unit folder. Other frames stay 128x160. The game reads per-sheet "frame"/"anchor" (LodSprite).
python tools/prone_wide.py <unit> <unit_dir>        (unit_dir = folder holding <unit>_layout.json etc.; patched in place)"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
from voxel import Res, LODS
from unit_loader import load_unit

WIDE_UNITS = {"infantry-default", "infantry-bandit", "infantry-fengtian", "infantry-gando", "infantry-guard", "infantry-guard-fengtian",
              "infantry-japanese", "infantry-regular", "infantry-reserve", "infantry-russian-red", "infantry-russian-white", "infantry-scout",
              "infantry-sniper", "infantry-sniper-fengtian", "infantry-sniper-japanese", "infantry-sniper-russian-red", "infantry-sniper-russian-white",
              "standard-bearer", "standard-bearer-fengtian", "standard-bearer-japanese", "standard-bearer-russian-red", "standard-bearer-russian-white"}
PRONE = ["go_prone", "aim_prone", "fire_prone"]
KX = KY = 1.25


class CellRes(Res):
    """same zoom / outline as Res(base_w, base_w*5/4), bigger frame; feet anchor stays 9/10 down -> same ground point semantics."""
    def __init__(self, base_w, base_h):
        Res.__init__(self, base_w, base_h)
        self.fw = int(round(base_w * KX)); self.fh = int(round(base_h * KY))
        self.ax, self.ay = self.fw // 2, self.fh * 9 // 10
    @property
    def tag(self): return f"@{self.fw}"


def wide_res(res):
    return CellRes(res.fw, res.fh)


def apply(unit, out, log=print):
    dirs = list(unit.dirs)
    for res in LODS:
        tag = "" if res is LODS[0] else f"@{res.fw}"
        wr = wide_res(res)
        fw, fh = wr.size
        sheet = Image.new("RGBA", (fw * len(PRONE), fh * len(dirs)), (0, 0, 0, 0)); team = sheet.copy()
        anc = {d: [] for d in dirs}
        for r, d in enumerate(dirs):
            for c, fr in enumerate(PRONE):
                f = unit.frame(d, fr, res=wr)
                sheet.paste(f.base, (c * fw, r * fh)); team.paste(f.team, (c * fw, r * fh))
                ax, ay = getattr(f, "anchor", wr.anchor)
                anc[d].append([round(float(ax), 1), round(float(ay), 1)])
        fname = f"{unit.name}{tag}_prone.png"
        sheet.save(os.path.join(out, fname)); team.save(os.path.join(out, fname.replace(".png", "_team.png")))
        lp = os.path.join(out, f"{unit.name}{tag}_layout.json")
        L = json.load(open(lp, encoding="utf-8"))
        L["sheets"] = [s for s in L["sheets"] if s["file"] != fname]
        L["sheets"].append({"file": fname, "team": fname.replace(".png", "_team.png"), "columns": PRONE, "size": list(sheet.size),
                            "frame": [fw, fh], "anchor": list(wr.anchor)})
        for c, fr in enumerate(PRONE): L["cells"][fr] = {"sheet": fname, "col": c}
        json.dump(L, open(lp, "w", encoding="utf-8"), indent=1)
        ap = os.path.join(out, L["anchors"])
        A = json.load(open(ap))
        for d in dirs:
            for c, fr in enumerate(PRONE): A[d][L["columns"].index(fr)] = anc[d][c]
        json.dump(A, open(ap, "w"))
        log(f"{unit.name}{tag}: {fname} {sheet.size} cell {fw}x{fh} anchor {wr.anchor}")


if __name__ == "__main__":
    apply(load_unit(sys.argv[1]), sys.argv[2])
