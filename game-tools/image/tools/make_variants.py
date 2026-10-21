"""Generate the faction variants (copy model, swap uniform / headgear / badge): python tools/make_variants.py
cavalry-default, infantry-sniper, heavy-howitzer/-field-gun/-infantry-gun, heavy-maxim -> <name>-<faction> (japanese: japanese only)."""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import unit_loader as UL
U = os.path.join(os.path.dirname(HERE), "units")
FACTIONS = {"japanese": "japanese_cap", "fengtian": "fengtian_cap", "russian-white": "furazhka", "russian-red": "budenovka"}
UNI = {"japanese": "infantry-japanese", "fengtian": "infantry-fengtian", "russian-white": "infantry-russian-white", "russian-red": "infantry-russian-red"}
TARGETS = {  # base unit -> (new-name pattern, factions, mounted?)
    "cavalry-default": ("cavalry-{f}", list(FACTIONS), True),
    "infantry-sniper": ("infantry-sniper-{f}", list(FACTIONS), False),
    "heavy-howitzer": ("heavy-howitzer-{f}", list(FACTIONS), False),
    "heavy-field-gun": ("heavy-field-gun-{f}", list(FACTIONS), False),
    "heavy-infantry-gun": ("heavy-infantry-gun-{f}", list(FACTIONS), False),
    "heavy-maxim": ("heavy-maxim-{f}", ["russian-white", "russian-red"], False),
}


def load(n):
    return json.load(open(os.path.join(U, n + ".json"), encoding="utf-8"))


for base, (pat, facs, mounted) in TARGETS.items():
    b = load(base)
    for f in facs:
        d = json.loads(json.dumps(b))
        inf = load(UNI[f])
        uni = inf["uniform"]
        uni = dict(UL.UNIFORMS[uni]) if isinstance(uni, str) else dict(uni)
        for k in ("trouser_stripe", "stripe_mat", "piping", "placket", "puttee_seam"):
            uni.pop(k, None)
        if mounted:                                     # riders: riding breeches + boots, no puttees
            uni.update(legging="leather", puttees=False)
        d["name"] = pat.format(f=f)
        d["uniform"] = uni
        d["headgear"] = FACTIONS[f]
        d.pop("badge", None)
        if d.get("kind") not in ("crew", "artillery"):
            d["fix_as"] = d.get("fix_as", base)
        # drop per-unit tunes tied to the old cap (cap_mat / budenovka spire handled by the cap itself)
        if f == "russian-red" and d.get("kind") == "crew":
            pass
        d["_doc"] = f"{base} faction variant ({f}): same model, {FACTIONS[f]} + {f} infantry uniform; cap badge from parts/badges HEAD_BADGE."
        if "tune" in d and "headgear" in d["tune"].get("parts", {}):
            d["tune"]["parts"].pop("headgear")
        if f == "russian-red":
            d.setdefault("tune", {}).setdefault("parts", {}).setdefault("headgear", {})["budenovka"] = {"spire_top": 14.6}
        json.dump(d, open(os.path.join(U, d["name"] + ".json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
        print("wrote", d["name"])
