# GOLDEN BUNDLE PLAN (reviewer, 2026-10-23, static: git merge-tree + diff reading only, no build/test run)
Scope: how to land the golden-changing bundle (G1 water-fire-open + NEW-3 prone-drag + NEW-4 choke-queue) after the MERGE-A chain,
plus the non-golden branches. Merge line = `merge/0.2-final` (worktree C:/gameProject/kd-develop/kd-final), tip when checked = 9d82a61b
(5 of 6 MERGE-A done; `kd/t57f-override-header` be1d5a7f is the remaining one, NOT yet an ancestor). Re-check every tip hash before each step (PIPELINE (e)).

## 0. Facts
- Baseline detcheck on the line: `672804207fc4e486 3c42aea49324248e` (units=29 shots=3237), FV 74 / Replay 27 (per QUEUE_0.3), golden file `assets/data/detcheck.golden` still holds that value.
- None of G1/NEW-3/NEW-4 touches Snapshot.cs / Replay.cs / ops/fv_claims (no FV or Replay bump, no new saved state, no new command). Checked by `git diff --name-only merge/0.2-final...<b>`.
- Each golden branch left the golden file untouched on purpose (commit messages say "not regenerated; bundled"). So after merging any of them `--detcheck` exits 2 (MISMATCH) until the one regeneration.
- Per-branch standalone detcheck (from commit messages; each measured on its own base, NOT on each other):
  - G1 `kd/water-fire-open` 6142cb8c: `terrain.waterBlocksFire` default true -> false (balance.json:29 + Defs.cs default + WaterFireOpen.cs/GameMapWaterLine.cs: no chase/charge across deep water). With switch=true build = golden match. Value false = `cda69bb68bed2b93 a7055757a0577991` (units 29->93, shots 3237->5209). Why goldens change: the detcheck scenario has rivers, firing across water is now allowed. Also changes mission outcomes (yalu_crossing lost->won, c1_01_yalu 633s->739s, c0_02_yalu 329->379s).
  - NEW-3 `kd/new3-prone-drag` 6c8a7ae7: World.Separate (new transient `sepPose`): `4481beae442aa119 cf3d48521804e40b` alone (train_demo.golden unchanged).
  - NEW-4 `kd/new4-choke-queue` a7636dad: OrgFormation.UpdateColumnRows RowTan central difference (affects ALL column marches, choke on or off), Choke.ChokeTailSlack, balance.json `combat.chokeEnabled=true` (user-approved): `0322cbe0f2f711e3 b4d5f80dc861a47d` alone (tangent fix only, choke off = 5b5f250b190bcaf7 48ab58613b77a9c9).
- How goldens are regenerated in this repo (two files):
  1. `assets/data/detcheck.golden`: `KoreanDivision.exe --detcheck --update` (DetCheck.cs:134 writes bin copy AND the source-tree assets/data copy), then `powershell -File tools/detcheck.ps1 -Exe <your exe>` must print `ALL 8 VARIANTS IDENTICAL` and GOLDEN match. `fv_assign.ps1 -Apply -RegenGolden` / `fv_renumber.ps1 -RegenGolden` wrap the same `--detcheck --update` (only when output shape is 2x16 hex and only detcheck.golden/Snapshot.cs/Replay.cs changed) - use them only if FV changes; here simplest is the direct command.
  2. `assets/data/train_demo.golden`: run the traindemo group with env `KD_TRAIN_DEMO_UPDATE=1` once (TrainDemoSelfTest.cs:34), then re-run without it. It was last regenerated at I8 ("train_demo d6ec723e76d8f379 10372"); it may change again only if the bundle alters the demo (NEW-3/4 + water may; check `traindemo` group FAIL after the bundle).
  - Precedent: ops/MERGE_PROGRESS.md I8 line "GOLDEN regenerated once ... (8 variants identical)" - done at the END of the chain, then full `tools/selftest_parallel.ps1 -Jobs 4`, tag `post-golden`. D3 immune-target-fix changed the checksum mid-chain with golden file untouched ("MISMATCH expected") - same pattern is valid here.

## 1. Order
Group P (non-golden; each step must leave detcheck = `672804207fc4e486 3c42aea49324248e`, golden MATCH, FV/Replay unchanged):
| # | Branch (tip) | Notes |
|---|---|---|
| P0 | finish MERGE-A: `kd/t57f-override-header` be1d5a7f | known conflict SnapshotSelfTest.cs:661 (append `delegateOverrideSeconds` to the current exclusion line). No FV (ReplayFile optional field, REVIEW-1). |
| P1 | `kd/tool-assemble` 9157b3bb | tools/docs only (assemble_notes.ps1, AGENT_RULES.md, docs/engine/_inbox.md). |
| P2 | `kd/gate-v3` 3d92cef0 | tools only + game-tools/image/rerender96.py (8 lines). Shipped with REVIEW-3 F1-F3 fixes (3d92cef0). F1 (callers soak.ps1/perf_overnight.ps1/run.py ignoring exit 75) was it fixed? it touched autoplay.ps1, bn_campaign/run.py, nightly_soak.ps1, selftest_parallel.ps1 - not soak.ps1/perf_overnight.ps1: UNCERTAIN, ask before merge. NOTE: the merger runs through the gate; after this lands the NEW gate rules apply to the merger itself (-Priority floor 5 GB, exit 75 = stop and report, no retry loop). Merge it BEFORE the heavy bundle steps only if it passes `heavy_gate_test.ps1`; otherwise defer, it is not required for the bundle. |
| P3 | `kd/ti2-p2` fd5f62b8 (contains ti2-p1 31c6a6ff; one merge) | UI strings only (323+199 consts -> Loc.T, +1426-line fixture tests/fixtures/ui_strings_ko.tsv, UiStringsSnapshotTest). Display only, no FV. Wait for ti2-p3 (currently the same sha as p2, no commits yet) - do not merge p3 until it has its own commit. Run `i18n`/`ti2`/`uiicons`/`paperui`/`campaignhome` groups. |
| P4 | `kd/t61e-personnel-ui` b6b85e9a - ONLY after the B1 Esc fix commit | REVIEW-4: FIX FIRST (Esc closes modal AND leaves campaign, MainMenu.cs:128-136). Fragment add/add conflict (see 2). Registered in savecompat chain: run `personnel`, `savecompat`, `compatgate`, `campaignepisodeui`. |
| P5 | `kd/a3-rerender` 8e404b56 | 248 files, assets only (sprites, layouts, theme.json, 10 res96 units). Run `units`, `uiicons`. Check `git diff --stat -- src` stays empty. |
| P6 | `kd/a5-mid-color-sheet` e10b8636 (1 python file) and `kd/cmd-portraits` e7c5b5de (portrait_maker.py + one PNG) | tools/docs only. `gt/a5-mid-color-sheet` (45305bff) is a duplicate worktree branch of the same sheet script: do not merge both. |
Group G (golden bundle, LAST, in this order, no regeneration between steps):
| G1 | `kd/water-fire-open` 6142cb8c | smallest sim risk, switch kept. Merge, build, detcheck -> expect `cda69bb68bed2b93 a7055757a0577991` (computed on 7d060f5f base; may differ slightly on the new line - record the actual value). |
| G2 | `kd/new3-prone-drag` 6c8a7ae7 | World.cs Separate. Record detcheck. |
| G3 | `kd/new4-choke-queue` a7636dad | OrgFormation + Choke + chokeEnabled=true. Record detcheck (this is the combined value for all three, UNKNOWN beforehand). |
| G4 | single regeneration (detcheck.golden, then traindemo golden if it fails), 8-variant detcheck, full `tools/selftest_parallel.ps1 -Jobs 4 -DetCheck`, tag `post-golden-2`. |
Rationale: art/tool/UI branches first so that every golden-affecting mismatch is attributable to exactly three sim branches; NEW-4 last because the reviewer recommended it (only branch changing sim output on every column march); G1 first because it is a switch (can be bisected with waterBlocksFire=true).

## 2. Pre-computed conflicts (git merge-tree --write-tree chain on 9d82a61b, order P0..G3 above; objects written only, no refs)
- P0 t57f x line: `korean-division/src/Tests/SnapshotSelfTest.cs` (content, line ~661) - known.
- P1 tool-assemble, P2 gate-v3, P3 ti2-p2, P5 a3-rerender, P6 a5/cmd-portraits: CLEAN.
- P4 t61e-personnel-ui: add/add `korean-division/docs/decisions_pending.d/kd__t61e-personnel-ui.md` (spec branch used the same fragment name). Resolve = keep both sections (fragment is just text; spec's Q-P1.. questions + t61e's 3-line note). Code clean.
- G1 water-fire-open: CLEAN (balance.json:29 `waterBlocksFire` - E2d on the line has `true`; auto-merges as the branch changed the same key from the older base, but VERIFY the final value is `false`).
- G2 new3-prone-drag: clean against 9d82a61b alone, but CONFLICTS once t57f is in: SnapshotSelfTest.cs ~660/661 (new3 adds `sepPose` to the work-buffer exclusion list at line 660, t57f changed the adjacent line 661). Resolve = keep both (`... sepOwner sepPose sepHostile ...` and `... delegateOverrideSeconds`). 
- G3 new4-choke-queue: CLEAN. Touches balance.json:112 (chokeEnabled), Choke.cs, OrgFormation.cs, Tests/Choke*.
- Fragment names are all distinct (kd__water-fire-open, kd__new3-prone-drag, kd__new4-choke-queue, kd__ti2-p1, kd__ti2-p2, kd__t57f-override-header, kd__t61e-personnel-ui); only t61e collides (above). After the whole chain run `assemble_notes.ps1 -Write` (and with tool-assemble merged, engine fragments go to docs/engine/<topic>.md; fragments without `topic:` go to `_inbox`).
- Hotspot vs other open branches: `wip/chokefix` (Choke.cs + OrgFormation.cs) overlaps NEW-4 - superseded by NEW-4 (D14 re-judgement); do NOT merge it, tell the conductor. `wip/casualty` touches balance.json (different keys, low risk). No other open kd/ or wip/ branch touches World.cs, WorldCombat.cs, Combat3.cs, Defs.cs, Snapshot.cs or Replay.cs.

## 3. Test groups per step (all via merge_step.ps1; names exist in SelfTest.cs `KD_SELFTEST_ONLY`)
- Every step: build, detcheck (STOP only on non-allowed change), `snapshot` group + `--snaptest`, `crossing`.
- P0: `delegation`, `replay`, `savecompat`. P1/P6: `-SelfCheck`-style tool tests (`pipe_parallel_test.ps1`, assemble dry run), no game groups. P2: `heavy_gate_test.ps1` (must be ALL PASS). P3: i18n/ti2/uiicons groups. P4: `personnel`, `savecompat`, `compatgate`, `campaignepisodeui`, `uifuzz 30`. P5: `units`, `uiicons`.
- FV/schema-touching ones (compatgate required): P4 only touches a catalog field + UserSettings (no FV), but run `savecompat`+`compatgate` anyway. None of the P group should change FV; if `fv_assign -Report` shows any claim, stop.
- G1: `crossing`, `terrain`, `firectl`, `missions`, `props`, `c1e1maps`, `episode`, `ai`/`tactical` (AI now sees across rivers), `snapshot`, `replay`. Expect mission-result assertions to be stale (yalu_crossing now WON) - fix test per LESSONS 16 (stale test, not weakened), report.
- G2: `pronemove`, `formations`, `squads` (2 pre-existing FAILs known: command chain stage / acting squad delay - not yours), `combat4`, `snapshot`.
- G3: `choke`, `formations`, `crossing` (fords/bridge), `snapshot`, `perf` (StepChokeQueue per-tick cost not measured, REVIEW-2) and `perf` runs alone after buckets.
- G4: full `tools/selftest_parallel.ps1 -Jobs 4 -DetCheck`, `--uifuzz 30`, `traindemo`, `savecompat`. Expect only squads pre-existing FAILs (and nothing else).

## 4. Risks
- NEW-3 x NEW-4 interaction is UNKNOWN: NEW-4 moves column slot positions (RowTan) and keeps companies standing/moving in chokes; NEW-3 changes who is pushed in World.Separate (prone-vs-walking pairs). Prone soldiers in a column slot or at a choke mouth can behave differently with both. The combined detcheck is not known; do not assume `4481...` or `0322...` survive. Record the value after each of G1, G2, G3.
- If a golden-affecting group fails after combining (choke/pronemove/crossing): (1) bisect by temporarily reverting only the last step (reset to the post-G(n-1) tag, no regenerate), (2) check whether it is a stale expectation (retuned numbers in NEW-4 tests were already adjusted on its base; they were not run with NEW-3/G1) vs a real regression, (3) never change seeds/maps/thresholds to pass (AGENT_RULES "no test dodging"), (4) if unresolved after two real attempts: drop the offending branch from the bundle, regenerate golden with the remaining ones (or not at all), report to the conductor. G1 can be disabled by `waterBlocksFire:true` for isolation; NEW-4 by `chokeEnabled:false` (but the RowTan fix is unconditional).
- Do NOT regenerate until all three are in and every non-golden group is green; ONE regeneration (precedent I8). Never `git add` the golden after a partial state.
- G1 float math: `GameMap.LineCrossesWater` uses float/MathF DDA exactly like HasFiringLine (same style as existing code; determinism across the 8 JIT variants is verified by tools/detcheck.ps1 - mandatory). NEW-4 uses Vector2 math. No RNG/dictionary use in any of the three.
- G1 `FiringLineBlock` (display-only) follow-up from E2b noted in MERGE_PROGRESS is unrelated here.
- Mission/campaign effects: G1 turns 3 river missions from lost/long to won; C1E1 placeholders and any campaign test with hard-coded outcome may break - decide per test.
- Gate v3 (if P2 merged first): the merger can see exit 75 / QUEUE waits; waiting is normal, never bypass.

## 5. Open user decisions
- chokeEnabled=true: approved (user). waterBlocksFire=false: user decision BI 15 (2026-10-22) - confirmed in commit message.
- ti2-p3 and t61e Esc fix not finished; ti2 Q1-Q7 answers were taken for P0..P2 only.
- Whether `wip/chokefix` is retired (recommend yes) and whether a3-rerender sprites are the final art (user reviewed? ART_STATE).
- Whether to tag/publish (v0.3 promote) after golden regeneration, and the balance pass (QUEUE_0.3 row 18) which must start AFTER regeneration.
- Open decision: t57f FU-5 header vs expiry command (already coded as header).

## 6. Copy-paste prompt for the merger
```
Task GOLDEN-MERGE (merger, sonnet 中, turn cap ~120). Repo C:/gameProject (korean-division/ subdir), merge worktree C:/gameProject/kd-develop/kd-final on merge/0.2-final. Read first: korean-division/tools/agent/AGENT_RULES.md, ops/PIPELINE.md (merge procedure v2.1), ops/LESSONS.md, ops/GOLDEN_BUNDLE_PLAN.md, ops/MERGE_PROGRESS.md (tail).
Preconditions: MERGE-A finished (tip contains t57f); merge lock held by you only (never touch another's lock; stuck lock -> stop and report); re-check each branch tip hash against the plan before its step (hold and report if it moved). Do not push. No fullscreen windows. No Bash heredocs for docs (kd_guard): write docs with Write/Edit; run build/test as their own plain calls (no redirect/cp/mv mixed with absolute protected paths); if kd_guard blocks a command, do NOT retry or reword - report the exact command.
Steps (one at a time, ONLY via tools/agent/merge_step.ps1, which builds, runs detcheck, snapshot + --snaptest, your groups, tags post-<Step> and appends to ops/MERGE_PROGRESS.md):
 P0 kd/t57f-override-header (SnapshotSelfTest.cs:661 append delegateOverrideSeconds) | P1 kd/tool-assemble | P2 kd/gate-v3 (run heavy_gate_test.ps1 first; if gate v3 breaks your own gating or REVIEW-3 F1 callers unfixed, skip and report) | P3 kd/ti2-p2 (not p3 until it has its own commit) | P4 kd/t61e-personnel-ui ONLY if the Esc-fix commit exists (add/add docs/decisions_pending.d/kd__t61e-personnel-ui.md: keep both) | P5 kd/a3-rerender | P6 kd/a5-mid-color-sheet, kd/cmd-portraits.
 In P-steps detcheck must stay 672804207fc4e486 3c42aea49324248e with golden MATCH, FV 74 / Replay 27 unchanged; any deviation = STOP, abort the merge, report.
 Golden bundle in this order WITHOUT regenerating between steps: G1 kd/water-fire-open, G2 kd/new3-prone-drag (SnapshotSelfTest.cs ~660 keep sepPose AND delegateOverrideSeconds), G3 kd/new4-choke-queue. Use merge_step -AllowFail only for the detcheck golden mismatch (allowed); record the detcheck value after each. Groups: G1 crossing terrain firectl missions props c1e1maps episode ai tactical snapshot replay; G2 pronemove formations squads combat4 snapshot; G3 choke formations crossing perf snapshot. squads has 2 known pre-existing FAILs (command chain stage / acting squad delay) - ignore only those.
 Then ONE regeneration: build own bin dir, `--detcheck --update`, `tools/detcheck.ps1 -Exe <exe>` must say ALL 8 VARIANTS IDENTICAL; if traindemo golden fails run traindemo with KD_TRAIN_DEMO_UPDATE=1 once and re-run. Then `tools/selftest_parallel.ps1 -Jobs 4 -DetCheck`, `--uifuzz 30`; commit golden files only (git add specific files); tag post-golden-2; assemble_notes.ps1 -Write.
 Failure after combining: do not edit tests to pass, no seed/threshold changes; bisect by resetting to the previous post-tag, fix only stale expectations (LESSONS 16, report each) or stop after two failed attempts; if the NEW-3 x NEW-4 combination breaks choke/pronemove, drop the last branch from the bundle and report.
 Gate notes: every heavy job through heavy_gate (v3 if merged: waits are normal, exit 75 = overloaded -> report, no retry loop; -Priority only while holding the lock). Long runs foreground timeout 590000 or run_in_background. Full selftest only at the end.
Report <=12 lines Korean + last line '임의 결정:' (every value/assumption/test change you chose).
```
