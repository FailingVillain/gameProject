"""units/<name>.json -> renderable unit.

json keys (all optional except base):
  "base": "infantry"                       pose set module in tools/bases/  (pose_for(frame) -> pose dict)
  "anims": "infantry" | {...}              anims/<name>.json or inline: anim -> {"frames": [pose frames], "ms": int|list}
                                           sheet columns = pose frames in order of first appearance (16 for infantry)
  "headgear": "m1_helmet"                  parts/headgear.HEADGEAR key
  "badge": "korean"                        optional cap front badge (parts/badges.BADGES: korean/japanese/fengtian/russian-white/russian-red)
  "uniform": "us_od" | {"jacket": "od", ...}   parts/uniforms.UNIFORMS key or material overrides
  "weapon": "garand" | null                single weapon (parts registries: rifles / sidearms / blades)
  "weapons": {"pistol": "pistol", ...}     several; a pose's `use` key picks the one in hand (others are gear)
  "gear": ["field_pack", "canteen"]        parts/packs.GEAR keys (all attached to the torso frame)
  "team": ["helmet_band", "arm_patch"]     which parts carry team colour
  "belt": true, "belt_pouches": true        built-in belt pouches (false when a gear part draws its own)
  "e_pull_extra": 3.0                      extra E-view pull-back of the weapon toward the body (long rifles)
  "tune": {"parts": {...}, "poses": {...}, "materials": {"ramps": {...}}}   optional per-unit overrides of tune/*.json (deep-merged)
  "outline": {"color": [8,8,8], "px": {"128": 3, "64": 2, "32": 1}}   optional outer-outline override (colour + width per LOD); absent = default   (default is now this strong outline: voxel.OUTLINE_COLOR + Res.outer_px)
                                           make_unit.py --outline-strong applies OUTLINE_STRONG to any unit
  "dirs": "all" | ["s", "e"]               rows to render
  "frames": "all" | ["idle", "walk_1"]     columns to render (others left empty)
"""
import importlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import DIRS, DIRS16, ROOT  # noqa: E402
from voxel import Kit, render_figure, render_prims, RES, Res, LODS  # noqa: E402
import tune  # noqa: E402
from parts.headgear import HEADGEAR  # noqa: E402
from parts.helmets import HELMETS  # noqa: E402
from parts.helmet_adrian import HELMETS as ADRIAN  # noqa: E402
HEADGEAR = {**HEADGEAR, **HELMETS, **ADRIAN}           # extra helmets (parts/helmets.py: stahlhelm, helmet_adrian.py: adrian)
from parts import sniper as _sniper  # noqa: E402
HEADGEAR = {**HEADGEAR, **_sniper.HEADGEAR}
from parts import rifles, sidearms, blades, packs, heavy_weapons, horse, smg, dadao, fengtian, badges, grenade, scout  # noqa: E402
from parts.uniforms import UNIFORMS  # noqa: E402
HEADGEAR = {**HEADGEAR, **fengtian.HEADGEAR}
from parts import bandit  # noqa: E402
HEADGEAR = {**HEADGEAR, **bandit.HEADGEAR}
from parts import caps_ru  # noqa: E402
HEADGEAR = {**HEADGEAR, **caps_ru.HEADGEAR}
UNIFORMS = {**UNIFORMS, **caps_ru.UNIFORMS}
UNIFORMS = {**UNIFORMS, **fengtian.UNIFORMS, **bandit.UNIFORMS}
from parts import kd_new  # noqa: E402
HEADGEAR = {**HEADGEAR, **kd_new.HEADGEAR}
UNIFORMS = {**UNIFORMS, **kd_new.UNIFORMS}
from parts import kd_new2  # noqa: E402
HEADGEAR = {**HEADGEAR, **kd_new2.HEADGEAR}
UNIFORMS = {**UNIFORMS, **kd_new2.UNIFORMS}

WEAPONS = {**rifles.WEAPONS, **sidearms.WEAPONS, **blades.WEAPONS, **heavy_weapons.WEAPONS, **smg.WEAPONS, **grenade.WEAPONS, **dadao.WEAPONS, **_sniper.WEAPONS, **horse.WEAPONS, **scout.WEAPONS, **kd_new.WEAPONS, **kd_new2.WEAPONS}
GEAR = {**packs.GEAR, **sidearms.GEAR, **blades.GEAR, **dadao.GEAR, **_sniper.GEAR, **scout.GEAR, **grenade.GEAR, **kd_new2.GEAR}

UNITS_DIR = os.path.join(ROOT, "units")
ANIMS_DIR = os.path.join(ROOT, "anims")


def load_anims(spec):
    """'infantry' | dict -> {anim: {"frames": [...], "ms": int|list}}"""
    if isinstance(spec, str):
        with open(os.path.join(ANIMS_DIR, f"{spec}.json"), encoding="utf-8") as fh:
            doc = json.load(fh)
        spec = doc["anims"]
        if doc.get("extends"):                       # "extends": "<anims name>" -> that file's anims first, this file's appended
            spec = {**{k: v for k, v in load_anims(doc["extends"]).items()}, **spec}
    out = {}
    for name, v in spec.items():
        if isinstance(v, list):
            v = {"frames": v}
        out[name] = {"frames": list(v["frames"]), "ms": v.get("ms", 150), "shared_fit": bool(v.get("shared_fit", False))}
    return out


def columns_of(anims):
    cols = []
    for a in anims.values():
        for f in a["frames"]:
            if f not in cols:
                cols.append(f)
    return cols


def _with_weapon_fix(mod, spec):
    """tune/weapon_fix.json[spec.fix_as or name] = {frame: [dx, dy, dz, dpitch, dyaw]}: per-frame weapon offset (torso frame, model units /
    degrees) applied after pose_for - removes weapon/body interpenetration found by collide.py without touching the pose code."""
    try:
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tune", "weapon_fix.json"), encoding="utf-8") as fh:
            table = json.load(fh).get(spec.get("fix_as", spec.get("name", "")), {})
    except OSError:
        table = {}
    if not table:
        return mod

    class _Base:
        def __getattr__(self, k):
            return getattr(mod, k)

        def pose_for(self, frame, d="s", *a):
            pose = mod.pose_for(frame, d, *a)
            f = table.get(frame)
            g = table.get(f"{frame}@{d}")                                          # optional per-direction extra offset
            if g:
                f = [a + b for a, b in zip(f or [0] * 5, g)]
            for sd in ("L", "R"):                                                  # optional elbow-hint override: "<frame>#poleL": [x, y, z]
                pv = table.get(f"{frame}#pole{sd}@{d}") or table.get(f"{frame}#pole{sd}")
                if pv:
                    pose = dict(pose, **{"pole" + sd: tuple(pv)})
            key = "weapon" if isinstance(pose.get("weapon"), dict) else "drop"      # held weapon, or the dropped one (death frames)
            if f and isinstance(pose.get(key), dict):
                w = dict(pose[key]); x, y, z = w["pos"]
                w["pos"] = (x + f[0], y + f[1], z + f[2]); w["pitch"] = w.get("pitch", 0) + f[3]; w["yaw"] = w.get("yaw", 0) + f[4]
                pose = dict(pose, **{key: w})
            return pose
    return _Base()


class WideRes(Res):
    """a standard LOD profile on a wider canvas: same zoom / line widths / anchor height, frame width x kx (anchor stays centred).
    Keeps the LOD file tags (@64 / @32) of the base profile. units/<name>.json "frame_wide": 1.5 (LMG gunner: the level barrel in the
    E-side rows is longer than the 64-unit frame)."""

    def __init__(self, base: Res, kx: float):
        Res.__init__(self, base.fw, base.fh)
        self._base_tag = base.tag
        self._kx = kx
        self.fw = int(round(base.fw * kx / 2)) * 2
        self.ax = self.fw // 2

    def tall(self, k):
        r = Res(self.fw / self._kx, self.fh * k)
        r.fw, r.ax = self.fw, self.ax
        return r

    @property
    def tag(self):
        return self._base_tag


class Unit:
    def __init__(self, spec: dict):
        self.spec = spec
        self.tune = spec.get("tune") or {}                  # per-unit overrides (applied by .active())
        self.outline = spec.get("outline")                  # optional outer-outline override (None = default look)
        self.name = spec["name"]
        self.base = _with_weapon_fix(importlib.import_module("bases." + spec.get("base", "infantry")), spec)
        self.anims = load_anims(spec.get("anims", spec.get("base", "infantry")))
        self.columns = columns_of(self.anims)                 # sheet column order
        hg = HEADGEAR[spec.get("headgear", "m1_helmet")]
        self.net = bool(hg["net"])
        uni = spec.get("uniform", "us_od")
        mats = dict(UNIFORMS[uni]) if isinstance(uni, str) else dict(UNIFORMS["us_od"], **uni)
        team = set(spec.get("team", ["helmet_band", "arm_patch"]))
        gear = [GEAR[g] for g in spec.get("gear", [])]
        self.e_pull_extra = float(spec.get("e_pull_extra", 0.0))     # extra E-view weapon pull-back (long rifles)
        self.kit = Kit(**mats, headgear=badges.with_badge(hg["fn"], spec.get("badge"), spec.get("headgear")), belt_on=spec.get("belt", True), belt_pouches=spec.get("belt_pouches", True),
                       team_band="helmet_band" in team, team_patch="arm_patch" in team,
                       back=(lambda T: [p for g in gear for p in g(T)]) if gear else None)
        # "weapon": "garand"  -> single weapon;  "weapons": {"pistol": "pistol", "sabre": "sabre"} -> a pose picks one with use=
        if "weapons" in spec:
            self.weapons = {k: WEAPONS[v] for k, v in spec["weapons"].items()}
            self.weapon = None
        else:
            w = spec.get("weapon", "garand")
            self.weapon = WEAPONS[w] if w else None
            self.weapons = {}
        if spec.get("frame_wide"):
            self.lods = [WideRes(r, float(spec["frame_wide"])) for r in LODS]
        d = spec.get("dirs", "all"); f = spec.get("frames", "all")
        self.dirs = list(DIRS) if d == "all" else list(DIRS16) if d == "16" else list(d)
        self.frames = list(self.columns) if f == "all" else list(f)

    def _outline_kw(self, res):
        """make_frame kwargs for this unit's outline override at this LOD ({} when none)."""
        o = self.outline
        if not o:
            return {}
        if res is None:
            from voxel import RES
            res = RES
        out = {}
        if o.get("color"):
            out["outline_color"] = tuple(int(c) for c in o["color"]) + (255,)
        w = (o.get("px") or {}).get(str(res.fw))
        if w:
            out["outline_width"] = int(w)
        return out

    def frame(self, d, name, res=None, **kw):
        """res = voxel.Res output profile (default: main 128x160). kw (fit=, shift=) passes to render_figure."""
        kw = {**self._outline_kw(res), **kw}
        if hasattr(self.base, "render_unit_frame"):          # composite bases (cavalry: horse + rider) render themselves
            return self.base.render_unit_frame(self, d, name, res=res, **kw)
        with tune.active(self.tune):
            pose = self.base.pose_for(name, d, self.e_pull_extra)    # e_pull_extra only adds to poses that pull in the E view
            weapon = self.weapons.get(pose.pop("use", None), None) if self.weapons else self.weapon
            slung = self.weapons.get(pose.pop("slung_use", None), None) if self.weapons else None   # weapon carried on the back (pose "slung" frame)
            if "second" in pose:                                         # two-figure scene (drag_wounded): bases/infantry_motions.build_scene
                from bases import infantry_motions as _m
                prims, ov = _m.build_scene(pose, self.kit, self.weapon or next(iter(self.weapons.values()), None))
                kw.setdefault("push", pose.get("push", 0.0))
                return render_prims(prims, ov, d, res or RES, kw.pop("fit", True), kw.pop("shift", None), lift=pose.get("lift", 3.0) * (res or RES).pxs, **kw)
            return render_figure(pose, self.kit, d, weapon, slung=slung, res=res, net=self.net, **kw)

    def active(self):
        """context manager: this unit's tune overrides are in force (use around pose_for / build_figure outside .frame())"""
        return tune.active(self.tune)


# --outline-strong (make_unit.py): near-black, one px thicker outer outline at 128 / 64 (interior lines unchanged)
OUTLINE_STRONG = {"color": [8, 8, 8], "px": {"128": 3, "64": 2, "32": 1}}


def load_unit(name: str) -> Unit:
    path = name if name.endswith(".json") else os.path.join(UNITS_DIR, f"{name}.json")
    with open(path, encoding="utf-8") as fh:
        spec = json.load(fh)
    spec.setdefault("name", os.path.splitext(os.path.basename(path))[0])
    if spec.get("kind") == "crew":
        import crew
        return crew.CrewUnit(spec)
    if spec.get("kind") == "artillery":                  # gun + crew (tools/artillery_unit.py)
        from artillery_unit import ArtilleryUnit
        return ArtilleryUnit(spec)
    return Unit(spec)


import res96 as _res96  # noqa: E402
if _res96.ACTIVE:
    import voxel as _voxel
    _res96.install(_voxel, Unit)
