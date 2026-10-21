"""python tools/prone_collide.py <unit> <variant cur|p07|new> : collide scan (all depths) for aim_prone/fire_prone/go_prone, every dir"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import collide, prone_proto as P
u, v = sys.argv[1], sys.argv[2]
uu, _ = P.make(u, v, 64)
for fr, d, c in collide.scan_unit(uu, ["aim_prone", "fire_prone", "go_prone"]):
    print(fr, d, {k: round(x, 1) for k, x in c.items() if x > 0.8})
