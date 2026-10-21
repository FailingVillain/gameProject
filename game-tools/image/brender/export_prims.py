"""Export posed voxel primitives (world matrices, yaw applied) for Blender. usage: python export_prims.py out.json"""
import os, sys, json, math
HERE = os.path.dirname(os.path.abspath(__file__)); T = os.path.join(HERE, "..", "tools")
sys.path.insert(0, T); sys.path.insert(0, os.path.join(HERE, ".."))
import numpy as np
import voxel
from voxel import build_figure, ry, YAWS, MATERIALS, MAT_IDX
from unit_loader import load_unit
u = load_unit("infantry-regular")
out = {"units": [], "frames": {}}
for name in ("idle", "fire_a"):
    for d in ("s", "se", "e", "ne", "n"):
        with u.active():
            pose = u.base.pose_for(name, d, u.e_pull_extra)
            w = u.weapon
            pose.pop("use", None)
            prims, ov = build_figure(pose, u.kit, w, None)
        vy = pose.get("view_yaw", 0.0)
        R = ry(YAWS[d]) @ (ry(vy) if vy else np.eye(4))
        shx = -pose.get("push", 0.0) * math.sin(math.radians(YAWS[d]))
        L = []
        for p in prims:
            M = R @ p.M; M[0, 3] += shx
            m = p.mat
            col = MATERIALS[m][4][:3] if m in MATERIALS else (128, 128, 128)
            L.append(dict(k=p.kind, d=[float(x) for x in p.dims], M=M.flatten().tolist(), c=list(col), team=(m == "team" or any(mm == "team" for _, mm in p.paint)), clip=p.clipy is not None))
        out["frames"][f"{name}_{d}"] = dict(prims=L, lift=float(pose.get("lift", 3.0)))
json.dump(out, open(sys.argv[1], "w"))
print({k: len(v["prims"]) for k, v in out["frames"].items()})
