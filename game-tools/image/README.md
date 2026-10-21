# game-tools/image

## v0.5 (2026-10): 128x160, data-driven units — see `CLAUDE.md` (spec, commands, how to add a variant)
`python make_unit.py <name>` renders `units/<name>.json` (parts in `tools/parts/`, poses in `tools/bases/`, anims in `anims/`)
to `<game-tools>/korean-division-image-out/<name>/` at 128x160 + 64x80 + 32x40 with a labeled contact sheet.
Everything below describes the legacy 64x80 generators (`tools/vox_*.py` -> `out/`); they still run but now output 128x160
through the shared engine and are due for a rework on the new pipeline.

Sprite generators for korean-division (and the on-hold wargame: `python export.py --game wargame`) (sheet spec: see the docstring of `tools/common.py`).

## Voxel pipeline (current unit art)
3D primitive model -> quarter-view render -> pixel-art post-process.

- `tools/voxel.py`  engine: primitives (box / ellipsoid / capsule / cylinder), rig (pelvis, torso, head, 2-bone
  arms with IK to weapon grips, thigh/shin legs), orthographic ray-caster (elevation 36 deg, yaw 0/45/90/135/180
  for s/se/e/ne/n), z-buffer, 4-tone cel shading per material (`MATERIALS`, built from `common.P`),
  3x supersample + MODE downsample (no blur, alpha 0/255), interior depth lines, 1px outline, stray-pixel removal.
  Team layer = material `team` (helmet band, upper-arm patch). Muzzle flash / backblast are overlay prims.
  Pose keys: `hipL/hipR` (swing deg), `kneeL/kneeR`, `lean`, `twist`, `head_pitch/head_yaw`, `weapon` (frame in
  torso space), `handL/handR` (`"grip"` | `("t", xyz)` | `("sw", fwd, bend, abduct)`), `overlay`, `push`
  (shifts aim/fire poses backwards so long weapons + flash stay inside the 64px frame).
- `tools/vox_infantry.py`, `vox_scout.py`, `vox_worker.py`, `vox_at_infantry.py`  unit generators
  (kit + weapon + 6 poses each), all output via `common.save_sheet`.
- `tools/compare.py`  builds `out/compare.png` (5 idle dirs at 1x and 0.5x, 3x nearest, grass bg);
  `python tools/compare.py 4 compare_fire.png` does the same for another column (0..5).

Run (needs Pillow + numpy):

    python tools/vox_infantry.py
    python tools/vox_scout.py
    python tools/vox_worker.py
    python tools/vox_at_infantry.py
    python tools/compare.py

Output: `out/<unit>/<unit>.png`, `<unit>_team.png`, `preview_<unit>.png`, GIFs.
`export.py` currently lists only infantry/scout/worker in `UNITS`; add `at_infantry` (and a theme entry) to use it.

Legacy hand-pixelled generators (`make_infantry.py`, `make_scout.py`, `make_worker.py`) are kept untouched;
`make_portraits.py` still produces the portraits.

## Update: 9-column unit sheets, tank
- Unit sheets now have 9 columns (6 hit, 7 fall, 8 dead; see `common.py`), a `<unit>_<dir>_death.gif`, and anchors json with 9 entries per direction.
  Fall/dead frames anchor at the ground point under the pelvis; the figure is auto-fitted into the frame (anchor moves).
- Rounded shapes (`rbox`, tapered `seg(..., r1=)`), 5-tone ramps, screen-space AO and ground darkening are in `voxel.py`.
- `python tools/vox_tank.py` -> `out/tank/` (hull sheet, turret sheet, anchors, previews, rotate GIF, portrait).
  Legacy `make_*.py` generators only know the first 6 frame names (they will fail with the extended FRAMES).

## Update v0.4: 16-column sheets, tiers, artillery, fx
- Infantry-type sheets (recruit, infantry, veteran, scout, at_infantry, worker) are 1024x400: 0 idle, 1-4 walk (contact/passing/contact/passing),
  5/6 aim/fire A, 7/8 B, 9/10 C (stand / crouch / kneel; worker: chop / side sweep / dig; at_infantry: 3 bazooka stances), 11 idle_alt,
  12 hit, 13 fall, 14 dead, 15 spare (= idle). `tools/poses.py` holds the shared gait/stance helpers. GIFs: `_walk.gif`, `_fire_a|b|c.gif`, `_death.gif`.
- Tiers: `python tools/vox_infantry.py [recruit infantry veteran]` (soft cap / bare M1 / netted M1).
- `python tools/vox_artillery.py` -> out/artillery (112x112, 9 rows, cols idle/move_a/move_b/fire/wreck, ground anchor (54,72), no turret).
- `python tools/vox_fx.py` -> out/fx_explosion (6x96x96, anchor 48,76), out/fx_muzzle_big (3x64x64, anchor 32,44). No team layer.
- `export.py` exports all of the above (UNITS now includes recruit/veteran, artillery, fx.* theme entries).
- python tools/vox_towed.py -> out/towed_artillery (M2A1 + 3 crew, 112x112, 9 rows, cols travel_a/travel_b/idle/fire/wreck/deploy, anchor (58,74)).
- python tools/make_gifs.py [unit ...] -> out/<unit>/gif/<unit>_{rotate,attack,death,idle}.gif (+_walk for infantry types) for all units (team colour, grass bg, 3x). Vehicles: 16-direction rotate; tank = hull + turret composited. fx_explosion is 8 variant rows x 6 frames; fx_muzzle_big 3 rows x 3 frames (theme key variantRows).
