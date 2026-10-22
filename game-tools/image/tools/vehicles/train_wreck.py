"""Generic train debris pile (wreckage left when a destroyed car is removed): burnt chassis, twisted plates, spilled planks,
a derailed wheel. Static: every hull frame is identical. Turret = a stub nobody draws."""
from voxel import ell, rbox, tr, ry, rz
from vehicles.vehcommon import mat_fn, scorch
from vehicles.traincommon import wheel

RING = (0.0, 6.0, 0.0)
TURRETS = ("none",)


def hull(frame):
    m = mat_fn(True)
    p = [rbox(tr(0, 2.6, 0) @ rz(7) @ ry(4), 8.0, 0.8, 26.0, m("veh_steel"), r=0.5)]
    for i, (x, z, a, b) in enumerate([(-4, 14, 20, 30), (5, 2, -35, 5), (-2, -12, 10, -40), (6, -18, -10, 25)]):
        p.append(rbox(tr(x, 4.5 + i * 0.4, z) @ ry(b) @ rz(a), 5.0, 0.4, 7.0, m("veh_od"), r=0.3))
    for j, (x, z) in enumerate([(-7, 6), (3, -6), (8, 12)]):
        p.append(rbox(tr(x, 1.0, z) @ ry(25 * j), 0.5, 0.5, 6.5, m("veh_wood"), r=0.2))
    p.append(wheel(rz(18), 1, -20.0, 0, m, False))
    p.append(wheel(tr(0, 0, 0), -1, -20.0, 30, m, True))
    p.append(scorch(z=0.0, rx_=16.0, rz_=30.0))
    return p, []


def turret(kind, frame):
    return [ell(tr(0, 1.0, 0), 1.5, 1.0, 1.5, mat_fn(True)("veh_steel"))], []
