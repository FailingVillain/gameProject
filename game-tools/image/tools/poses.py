"""Shared pose helpers for the 16-column infantry-type sheets (see common.FRAMES)."""


def parse(name):
    """'walk_3' -> ('walk', 3); 'aim_b' -> ('aim', 'b'); 'idle' -> ('idle', None)."""
    if "_" in name and name.split("_")[0] in ("walk", "aim", "fire"):
        k, v = name.split("_")
        return k, (int(v) if k == "walk" else v)
    return name, None


def walk_legs(i, bend=42):
    """4-phase gait. 1 contact (L front), 2 passing (R leg swings through), 3 contact (R front), 4 passing (L swings).
    returns dict(leg keys, swing = left-arm swing sign, s)."""
    if i == 1:
        d = dict(hipL=17, hipR=-17, kneeL=10, kneeR=bend, twist=-5, sw=-26)
    elif i == 2:
        d = dict(hipL=1, hipR=10, kneeL=6, kneeR=bend + 10, twist=0, sw=0)
    elif i == 3:
        d = dict(hipL=-17, hipR=17, kneeL=bend, kneeR=10, twist=5, sw=26)
    else:
        d = dict(hipL=10, hipR=1, kneeL=bend + 10, kneeR=6, twist=0, sw=0)
    d["lean"] = 3
    d["head_pitch"] = -3
    d["lift"] = 3
    return d


def quick_legs(i):
    """Quick-step (sokbo) gait: walk_legs with a longer stride, higher knee, more torso twist and a forward lean."""
    d = {1: dict(hipL=26, hipR=-24, kneeL=12, kneeR=56, twist=-8, sw=-38),
         2: dict(hipL=2, hipR=16, kneeL=8, kneeR=70, twist=0, sw=0),
         3: dict(hipL=-24, hipR=26, kneeL=56, kneeR=12, twist=8, sw=38),
         4: dict(hipL=16, hipR=2, kneeL=70, kneeR=8, twist=0, sw=0)}[i]
    d["lean"] = 8
    d["head_pitch"] = -6
    d["lift"] = 3.6 if i in (2, 4) else 3
    return d


# leg / torso stances for the three firing variants (a = standing, b = crouch, c = kneeling)
STANCE = {
    "a": dict(hipL=12, hipR=-14, kneeL=8, kneeR=6, lean=6, spread=9),
    "b": dict(hipL=42, hipR=10, kneeL=74, kneeR=58, lean=15, spread=10),
    "c": dict(hipL=68, hipR=-4, kneeL=82, kneeR=100, lean=11, spread=8),
}
