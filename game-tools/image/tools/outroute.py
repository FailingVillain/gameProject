"""Output routing: sprite sheets go into a faction subfolder of the output root.

Rule (single source of truth; the importer in korean-division/tools/import_kd1920.py mirrors it):
  name endswith -japanese            -> japanese/
  name endswith -fengtian            -> fengtian/
  name endswith -russian-white       -> russian-white/
  name endswith -russian-red         -> russian-red/
  name startswith tank-  or == supply-wagon -> vehicles/
  name endswith -old  or == infantry-ww2    -> _legacy/
  everything else (no suffix)        -> korean/
Buildings are not routed here: make_building.py writes into <out>/buildings/ itself.
"""
import os

FACTIONS = ("korean", "japanese", "fengtian", "bandit", "russian-white", "russian-red", "vehicles", "buildings", "_legacy")
_SUFFIX_FACTIONS = (("-russian-white", "russian-white"), ("-russian-red", "russian-red"),
                    ("-japanese", "japanese"), ("-fengtian", "fengtian"), ("-bandit", "bandit"))
_LEGACY_NAMES = ("infantry-ww2",)


def faction_of(name):
    """Faction subfolder name for a unit / vehicle name."""
    if name.startswith("tank-") or name in ("supply-wagon", "heavy-tachanka", "heavy-horse-artillery"):
        return "vehicles"
    if name.endswith("-old") or name in _LEGACY_NAMES:
        return "_legacy"
    for suffix, folder in _SUFFIX_FACTIONS:
        if name.endswith(suffix):
            return folder
    return "korean"


def route_out(out_root, name):
    """<out_root>/<faction>/ for a unit/vehicle name (created by the caller with makedirs)."""
    return os.path.join(out_root, faction_of(name))
