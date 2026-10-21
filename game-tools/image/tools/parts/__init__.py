"""Reusable model parts (primitive lists) for data-driven units (units/<name>.json).

Each module exposes a registry dict  name -> builder:
  headgear.HEADGEAR[name] = dict(fn=fn(Hm, band) -> prims, net=bool)   # net = helmet-net dither post-process
  rifles.WEAPONS[name]    = voxel.Weapon
  packs.GEAR[name]        = fn(Tm) -> prims        (Tm = torso frame; back / hip / front gear)
  uniforms.UNIFORMS[name] = dict(jacket=, trousers=, legging=, boot=, belt=, hand=)   (material names)
"""
