# ART_STATE - Art sub-conductor status (2026-10-22 ~21:30, #2; updated 2026-10-23)
Update note (conductor directly, 2026-10-22): lines marked '(갱신)' [updated] are the latest facts. Lines marked '이전:' [previous] are stale records and are kept, not deleted. Sheet folders are listed only where the directive named them (only 33_D is C:/works/_review_sheets_2026-10-22/).
Update note (docs agent, 2026-10-23): '(갱신 10-23)' [updated 10-23] marks the latest. Stale 'in progress' became done/merged and the original text is kept as '이전:'.

## Limits
- Art render lanes <=8 (7th onward when RAM >=6GB). Non-art heavy <=3 separate. Build slot 1 left empty. Do not kill others' PIDs. New gate (gate-v2) not installed -> existing heavy_gate, render agents 1 at a time.
- Division (coordinator directive): A1 terrain tiles (kd/a1-terrain-tiles), A2 ground decals (kd/a2-ground-decals), B1 (bandit infantry·thug·dadao·officer·scout 5), B2 (Gando·Mongol/bandit cavalry·horse artillery·tachanka 5), D train (kd/d-train-sprites), C blizzard (sim) = separate agents. Mine: T39·T55·PR2·C1E1 new units·mixed visuals·VIP·mockup cleanup.
- The 5 deferred render lanes (G1~G5) that #2 had launched were terminated (my PIDs only) before render start because they duplicated B1/B2.

## T11 (kd-rerender b324492)
- (갱신) The PC shut down abnormally at 21:15 and rebooted at 21:24 -> many render agents died. However, the old driver PID 22168 (rerender96 --only cavalry-bandit,cavalry-mongol,heavy-horse-artillery,heavy-tachanka, kd-b2-units worktree) is running again as of 21:31.
- (갱신) cavalry-bandit render complete (rc=0, uncommitted). cavalry-mongol rendering (22:06~). heavy-horse-artillery·heavy-tachanka state not recorded.
- Main queue (rerender96 --list) = 0 to render, 37 done. Progress record commit b324492 (wip-ready, awaiting merge. QUEUE not touched).
- 이전: deferred 10 = infantry-bandit, infantry-thug-bandit, infantry-dadao-fengtian, infantry-gando, infantry-scout, officer-bandit, cavalry-mongol, cavalry-bandit, heavy-horse-artillery, heavy-tachanka -> within T11 scope (of 81) but delegated to B1/B2, not rendered.
- (갱신) Of the deferred 10, cavalry-bandit·cavalry-mongol·heavy-horse-artillery·heavy-tachanka are within the scope of PID 22168 above. infantry-bandit·thug-bandit·dadao-fengtian are B1 commit-complete (shard table B1 row). officer-bandit·infantry-scout are in progress with a new B1 agent. For gando see the T39g row.
- Old drivers A/B/C/D/F (wait-only) are still alive: cleanup is the conductor's decision.

## Shard / order table (worktree kd-develop/<name>)
| Card | Branch @commit | Grade | State | Sheet (review needed) |
|---|---|---|---|---|
| T39h h1 crawling·trench firing | t39h-motion-r2 @16d158a | sonnet | done wip-ready, render 25 min | kd-t39h/korean-division/docs/art_review/motion_r2_crawl.png, motion_r2_trench_fire.png, motion_r2_regress.png |
| T39h h2 dragging·grenade | t39h2-motion-r2b @9b640f8 (on h1) | sonnet | done wip-ready, 620 s | kd-t39h2/.../motion_r2_drag.png, motion_r2_grenade.png (flaws: dragging s/se overlap, rifle omitted when throwing grenade) |
| T39f Korea/Fengtian color | t39f-kr-ft-colour @2b4cf91 | haiku | done: 0 omissions, no fixes | kd-t39f/.../kr_ft_colour_applied.png (+.md). Incomplete re-check: scout, horse-artillery, dadao-fengtian |
| T39e march-fire muzzle | t39e-marchfire-muzzle @c837eb8 | sonnet | measurement·data done. Cause is the 5-direction hardcoding of the game's UnitRenderer.MarchMuzzle, code fix needed -> DECISIONS_PENDING candidate | kd-t39e/.../marchfire_muzzle.png ; table kd-t39e/game-tools/image/muzzle/walkfire_muzzle_table.csv |
| T39j prone-firing re-check (check only) | t39j-prone-recheck @f671be1 | sonnet | done: muzzle·clipping pass, cell margin <2px: all 19 units fail in direction E -> handled by the 26-infantry re-render (gt-prone-fix) | kd-t39j/.../prone_recheck.png |
| T39g Gando dress | t39g-gando-dress | sonnet | (갱신 10-23) merged (N3-17): render·import. 이전: running (render after gate wait, resume directed, agent a69d7f5) | gando_dress.png planned. Note gando render duplication with B2: B2 must reflect the T39g result |
| T55a Korean houses 21 types | t55a-house-korean @8faffd2 (main 486c4d7) | sonnet | done wip-ready (30 sprites, not imported) | kd-t55a/.../t55a_houses_korean.png, t55a_houses_korean_map.png ; render output in a scratchpad temp path (can re-render) |
| T55b Manchu houses | t55b-house-manchu @13610a9 | sonnet | done wip-ready (36 sprites, 5 min) | kd-t55b/.../t55b_houses_manchu.png (labels' Korean garbled) ; render output C:/gameProject/render_out/t55b |
| 이전: C1E1 new units (refugee·volunteer·Fengtian cavalry/field gun) | kd/c1e1-art-units (base kd/c1e1-u2-units 4c309a4) | sonnet | running (resume directed, agent a11b9cb). Railway guards excluded, bandit·Gando delegated | c1e1_new_units.png planned |
| C1E1 new units (refugee·volunteer·Fengtian cavalry/field gun) (갱신) | kd/c1e1-art-units @8b73c78b (base kd/c1e1-u2-units 4c309a4) | not recorded | fixing 1 theme.json conflict. Railway guards excluded, bandit·Gando delegated | c1e1_new_units.png planned |
| Engineer prone-firing root fix | kd/pioneer-prone a69e6ac | sonnet | approved (merge round M) | pioneer_prone_rootfix.png needs checking |
| B1 bandit·thug·dadao·officer·scout (갱신 10-23) | kd-b1-units @9b4d049f | not recorded | (갱신 10-23) 5 bandit types merged (N3). 이전: infantry-bandit·thug-bandit·dadao-fengtian complete commits. officer-bandit·infantry-scout in progress with a new agent | - |
| A1 roads (갱신) | kd/a1-roads @ab9ab733 | not recorded | wip-ready, review complete (render only, no conflicts, merge OK; roads first) | 25_A1_roads_contact.png |
| A1 terrain tiles·water (갱신) | kd/a1-terrain-tiles @9bf4ffa5 | not recorded | wip-ready, review complete (render only, no conflicts, merge OK) | 26_A1_water_contact.png |
| A2 ground decals (갱신) | kd/a2-ground-decals @b63062dc | not recorded | done, awaiting review | not recorded |
| D train T30d (갱신) | kd/d-train-sprites @21a3b158 | not recorded | (갱신 10-23) merged (N3-4). 이전: all 5 car types (locomotive·gun car·MG car·freight car·wreck) complete, wip-ready | C:/works/_review_sheets_2026-10-22/33_D_trains_contact.png |
| Settings panel (갱신) | kd/settings-panel-fit @d5aa4ac9 | not recorded | state not recorded | 27_settings_1024x640.png |
| B2 cavalry·horse artillery·tachanka (갱신 10-23) | not recorded (worktree kd-b2-units) | not recorded | cavalry-bandit·cavalry-mongol render OK (uncommitted). heavy-horse-artillery 75 clipping cases. heavy-tachanka render failed -> follow-up agent in progress | - |
| Bridge rework (갱신 10-23) | kd/bridge-rework | not recorded | merged·approved (user) | bridge sheet 54 |
| Water foam fix (갱신 10-23) | not recorded | not recorded | merged (N3b-3) | - |
| Objective HUD·UI low-grade fixes (갱신 10-23) | not recorded | not recorded | merge state. HUD waits for N3c | objective HUD sheet (no issues, user) |
| Mockup cleanup scout | (read) | haiku | done, table below | - |

## Waiting list
| Item | Prerequisite | Shard plan |
|---|---|---|
| (갱신 10-23) gt-prone-fix 26 infantry prone-firing re-render (+EDGE_MARGIN=2 all directions, import --only, sheet): 1/4 (kd/prone-rerender-1, 7 units) · 2/4 (kd/prone-rerender-2, 7 units) · 3/4 (kd/prone-rerender-3, 7 units; scout has no frame_wide -> at the 0.3 unit re-render) re-rendered · 4/4 in progress. Full-resolution margin 2 cells passed, small LOD (@96/@48) approved on a 1-cell margin basis (user 2026-10-23) | the conductor's merge signal (kd/pioneer-prone -> merge/0.2-final) | 26 units / 4 shards (6~7 units each, assuming ~900 s per unit -> ~100 min per shard) sonnet x4 ; T39j result: the 19 target units confirmed to lack margin in direction E |
| 이전: PR2 prop variants 22 types (added grass_tuft [tuft of grass]·wildflower) | PR3 document (kd/pr3-prop-variants, key rule props/<kind>/var_01..07) + lane headroom | Shard P-A: bush,sorghum,stump,log_pile,log_pile_scattered,rock_outcrop,dirt_mound,grass_tuft,wildflower ; P-B: dead_tree,dead_tree_fallen,willow,willow_fallen,reed,snow_mound ; P-C: rock_s/m/l(+cracked), tree_birch/larch/pine/oak(+fallen) variants. All sonnet, 1 render per agent. grass_tuft/wildflower report if a small card is needed to add props.json keys |
| PR2 progress (갱신 10-23) | P-A (9 types)·P-B·P-C merged. User sheet no issues. 이전: kd/props-keys (props key addition) in progress -> P-A after that | 이전: P-B in progress (kd/props-pb), P-C waiting |
| Art-style mid re-render (0.3, 갱신 10-23) | after 0.3 promotion | unchanged: buildings·terrain·props·decals first, units later (user) |
| Regiment commander portraits (갱신 10-23) | not made now (user decision) | lightly with AI later |
| Bandit+Fengtian mixed team color/marker | B1 (bandit·Fengtian units) complete | small sonnet 1 |
| VIP portrait placeholder | low priority, reuse gt-face-proto | sonnet 1 |
| T39e game connection (MarchMuzzle loader) | user decision | feature card |
| T56b~e | T01+T55s+T56a merge first (previous record as is) | - |

## Deferred units used by C1E1 s1~s7 missions (measured from kd/c1e1-s* mission JSON)
1. Bandit infantry/thug/cavalry/boss: bt1 (thug3·rifle3·horse2), sk1 (5·5·5·leader1), sk3 (6·6·6·leader2) -> infantry-bandit, infantry-thug-bandit, cavalry-bandit, officer-bandit = **top priority (B1/B2)**
2. infantry_scout: op1_assault/op1_siege 27 each, sk2 scouts -> **2nd priority (B1)**
3. Unused (s1~s7): dadao-fengtian, gando, cavalry-mongol, heavy-horse-artillery, heavy-tachanka -> low priority (B2, gando is T39g)

## Mockup cleanup (scout, 2026-10-22)
| Mockup | Classification |
|---|---|
| quickstep walking | waiting (sheet only, no branch basis) |
| 쇼샤·armored car + austin_fix3 | unknown (armored car re-art is in main, austin_fix3·쇼샤 files not found) |
| Mongol cavalry hat·belt | reflected (main lookfix), awaiting user review U03 |
| prone-firing placement | waiting (kd/pioneer-prone unmerged) |
| march-fire flash | waiting (on top of T39e) |
| missing_theme_units / surrender_pose | unknown, discard candidates |
| new_units | reflected, post-hoc review |
Sheets needing review: gt-paper-ui-v2/.../quickstep_walk.png, gt-fonts-ofl/.../{surrender_pose, missing_theme_units, mongol_bandit_look, new_units_n68_n71}.png, the T39/T55 sheets in the table above.

## Expected completion / blocked
- Possible to finish today: T39g, C1E1 new units (few renders), 1~2 PR2 shards (if lanes have headroom). Difficult: 26-infantry re-render (waiting for the merge signal, ~100 min), all of PR2 (22 types x 6~8, textures ~21MB), mixed visuals·VIP.
- Blocked: some render agents ended their turn while waiting at the gate (handled by a resume directive). The 5 old wait drivers may occupy gate slots.
- (갱신) Many render agents died from the abnormal PC shutdown at 21:15 and reboot at 21:24. Old driver PID 22168 running again since 21:31 (see the T11 (갱신) line).
- Parallelization proposal: reducing PR2 variants to 6 per kind cuts render volume by 25%; the 3 PR2 shards in the order above; the 26 infantry in 4 shards; a 7th lane is not possible because spare render RAM is under 6GB (currently 4.9).
- (갱신 10-23) Merged today: B1 5 bandit types (N3), Gando dress T39g (N3-17), PR2 P-A·P-B·P-C, bridge rework (kd/bridge-rework, approved), water foam fix (N3b-3), train D (N3-4). Objective HUD·UI in merge state (HUD waits for N3c). Prone-firing re-render 4/4 in progress, B2 heavy-tachanka re-render follow-up agent in progress.
