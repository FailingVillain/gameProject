# housegen JSON schema (schema 1) - T55g

Content agents (T55a Korean, T55b Manchu-Chinese) add houses ONLY as new `*.json` files in this folder
(e.g. `ko_choga.json`, `zh_brick.json`); never edit `housegen.py`. Files starting with `_` are tables (`_palettes.json`, `_tints.json`), not houses.
`proto.json` = the T55g sample set (6 houses + 3 palette variants); leave it alone.

```
{ "schema": 1, "set": "ko", "houses": [ <house>, ... ] }
```
`set` names the group (used by `--set`). Ids must be unique across ALL files. Name rule: `ko-cho-01`, `ko-giwa-03`, `ko-bar-02`, `ko-inn-01`, `zh-mud-04`, `zh-giwa-02`, `zh-bar-01`.

## house (every key except `id` optional; defaults in `housegen.DEFAULTS`; values are validated, errors name the id)
| key | meaning |
|---|---|
| `id`, `label` | render folder name and Korean label (goes into `_layout.json`) |
| `extends` | id of another house; deep-merged, your keys win (same-id-set or any file) |
| `footprint` | `[w, d]` tiles, w = along screen x, d = depth. Classes 3x3, 3x4 / 4x3, 4x4, 3x5 / 5x3. **Make houses wider than deep** (ridge runs along x): 4x3 not 3x4 |
| `seed` | int; varies ruin pieces / jar counts (deterministic) |
| `wall` | `mat` (mud, mud_dark, earth, earth_pale, plaster, brick_grey, brick_red, brick_dark, wood, stone), `pattern` none/brick/planks/rammed/lattice_band, `line_mat` (pattern lines, default = darker sibling), `height` (7..10, wall height above base), `pillars` (timber posts + tie beam on front), `pillar_mat`, `lower_band` (mat of a 1-unit damp band) |
| `base` | `height` 0..2.4 stone plinth (0 = none), `mat` |
| `roof` | `type` thatch_round / gable_curved / hip / flat_earth / shed / gable_plain; `mat` + `alt_mat` (pattern lines) ; `rise` (ridge height above eave, 1..9); `eave` (overhang 0.5..3); `curve` (0..1.5 eave upturn: gable_curved and hip only); `thickness`; `ridge_cap` bool; `ridge_mat`; `gable_overhang` |
| `chimney` | `type` none/box/jar/flue (flue = free-standing Manchu kang flue + duct), `side` L / R / B (back), `mat` |
| `front` | `bays` list of 1..6 kinds: paper (창호지 door) / plank (널문) / arch / lattice (window) / slit (small hole) / kitchen / blank; `porch` none / front / front_side; `door_mat`, `window_mat` |
| `addons` | list of names or `{"type": ..., params}`: `yard_wall` {mat, height, cover, gate}, `gate` {roof_mat}, `jars` {n, side L/R, mat}, `firewood` {x}, `stakes` {n}, `hang` {n, mat} (corn/chili under the eave), `haystack` {x}, `tree` {x, z, h, r, kind}, `loft_window`. `yard_wall` shrinks the house to the back ~58% of the footprint |
| `palette` | name from `_palettes.json` or inline `{mat: replacement}` applied to the base render |
| `variants` | `{"a": "giwa_blue", "b": {...}}` -> extra baked colour variants rendered as `<id>-a`, `<id>-b` (each is a separate sprite = render time + atlas memory; plan: only identity-colour classes) |

Materials are `bcommon.MATS` names. New ramps are appended at the END of `MATS` (append-only; never reorder). T55g appended: earth, earth_pale, brick_red, kiwa_blue, tile_red, thatch_gold.

## Ruin
`ruin=True` is derived from the same spec (second state `ruined` of the same sheet): walls cut to random heights (one corner stands), half the roof kept, collapsed roof slab + rods + rubble, door bays 1 and 3 removed, soot materials (`RUIN_MAP`) on roof/wood.

## Tints (`_tints.json`)
Runtime multiply tints for the renderer (T55s): per culture three RGB multipliers (level 0 neutral). Not baked; zero atlas memory.

## Render + output (same format as the four existing houses)
```
python make_building.py <id[,id...]> --set <set[,set]> [--lod 0,1,2] [--out DIR]
python make_building.py gen --set ko --shard 0/4        # every house of a set, shard i/n (0-based)
python house_sheet.py OUT.png id1,id2,... [--variants id-a,...]   # review sheet (from rendered files)
```
Output `<out>/buildings/<id>/<id>.png|@64|@32 (+_team) + <id>_layout.json + contact_<id>.png`, identical to `house-thatch` etc.
(two cells per sheet: `intact`, `ruined`; frames 1/2 and 1/4 per LOD). Import into `assets/themes/kd1920/buildings/` and the
theme.json keys are T55m's job (`tools/import_kd1920_buildings.py`: its `ART` table must become a glob over these folders; suggested key
`building_art.house.<set>.<id>`; footprint tiles are in the layout json). Render one house process at a time, `heavy_gate.ps1 -Render` before each.
