# MERGE_PLAN - Full WIP merge plan (written 2026-10-22, based on main d4de9fb)

## Summary for the user (plain words)
- What happens: all merges are done **locally only** in a separate worktree `merge/0.2`. Nothing is pushed.
- main is raised with `--ff-only` only at the very end. Only after golden regeneration and the final selftest are done, and only after the user says "proceed".
- 3 places needing user approval: ① merge start ② golden regeneration (after checking the new values) ③ final raising of main.
- Rollback: tag `pre-merge-A`~`pre-merge-H` before each group starts, `post-<group>` when it ends. Group-level rollback only after user confirmation.
- Risky steps: ① B2 `t38c-on-ui` (based on t38a so an order exception, §Caution B2) ② B5b·E2 (crossing fix, river FV 63→64) ③ E2b·E3·t34a (FV number reassignment) ④ D1~D2 train conflicts ⑤ F2·F4 campaign conflicts ⑥ G4·G4b (t38h duplicate commit).
- If a conflict is not described or a checksum differs from expectation, stop at that step and report. No arbitrary resolution.

> Basis: user decision AT (2026-10-22 15:0x) "merge policy lifted — start merging in the integration rehearsal order after the crossing failure (T24 suspected) and uiicons (T38c) are resolved. Write the merge plan right before merging and get the user's final confirmation". The two blocker fix branches: crossing = `kd/t24-crossing-fix` 390763a, uiicons = `kd/t38c-on-ui` 85e6658 (on top of t38a-settings-keys 7274c71; the previous `kd/t46-card-overflow` 54e8a54 conflicts with T27 paging (Game.cs) and is **replaced - do not merge**, kept for the record).
> Sources: ops/QUEUE.md · CARD_INDEX.md · FV_RESERVATIONS.md (14:4x correction, T39d memo) · PIPELINE_LOG.md · STATE.md · DECISIONS.md (AT) · rehearsal card `kd-develop/kd-integration/korean-division/tasks/INTEGRATION-REHEARSAL.md` (integration/rehearsal 984c404) · docs/procedures/merge-procedure.md · gate.md · LIMITS.md · `tools/fv-renumber` a9366d4.
> This document contains no design decisions. All choices are already written below, and situations not written (non-mechanical conflicts, unexpected checksums, tip mismatches) **stop at that step and are reported to the conductor** (no arbitrary resolution).
> User rule (2026-10-22): game/editor windows are not opened fullscreen·borderless·maximized. Windowed mode, <=1280x720, only on the secondary monitor via `run_game.ps1`. Do not change Mode in settings.json. Do not open an image viewer (write files only). Merge verification is all headless (selftest/detcheck/snaptest/uifuzz) so there is no need to open a window.

## 0. Preparation (once)
1. Start only after receiving the user's final confirmation (§4-1). No push at any step.
2. Lock: `mkdir C:/gameProject/.git/kd-merge.lock` + `started.txt` (merge-procedure.md). If it exceeds 30 minutes renew it or re-acquire between steps. If the lock is someone else's, report and stop.
3. Working branch·working tree: main of `C:/gameProject/.git` is checked out in C:/gameProject and has unrelated uncommitted changes there (ROADMAP_SHORT etc.). So **do not merge directly into main**.
   After `git -C C:/gameProject worktree add C:/gameProject/kd-develop/kd-merge -b merge/0.2 main`, do all merges in `merge/0.2` of that tree. main is raised only by `git merge --ff-only merge/0.2` after the final confirmation of §4-3 (if there are overlapping uncommitted files, stop·report).
4. Commit identity: `git -c user.name=seung -c user.email=seungwoo3656@gmail.com`. At the end of the merge commit message `Co-Authored-By: <actual model> <noreply@anthropic.com>` (commit-trailer rule: the merge agent's actual model).
5. Tip verification: at the start of each step `git rev-parse --short <branch>` must equal the table's @tip. If different, skip the step and report (the new commit content is judged by the conductor).
6. Common conflict rules (same as the rehearsal): `git merge --no-ff --no-commit <br>` → `ops/*` conflicts are always ours (ops is owned by the conductor). `ENGINE_NOTES.md` conflict = section union. Card (`tasks/*.md`) add/add = ours (the version where the implementation branch came in first). Do not touch `detcheck.golden` midway (if a branch golden gets in, restore to HEAD).
   **Reuse rehearsal resolutions**: the table's "rehearsal SHA" is that merge commit. For each conflicting file, if `git diff --quiet HEAD <rehearsal SHA>^1 -- <file>` succeeds (= the file content right before the merge is identical to the rehearsal), bring the resolution with `git checkout <rehearsal SHA> -- <file>`. If different, use the rehearsal resolution only as a reference and resolve by hand as the "expected conflicts" below describe; stop if the conflict is not in the description.
7. The FV/Replay lines (Snapshot.cs `FormatVersion`, Replay.cs `FormatVersion`) are set after merging with `korean-division/tools/agent/fv_renumber.ps1` (tools/fv-renumber a9366d4 merged first in §A1): check the current value with `-Report`, `-Set <fv> <replay>`. The golden auto-regeneration option `-RegenGolden` is **not used** (§4-2).
8. Common checks after merging (every step, in order of small cost): ① build (`bin/merge`, pass heavy_gate `powershell -ExecutionPolicy Bypass -File C:\gameProject\korean-division\tools\agent\heavy_gate.ps1`) ② `--detcheck` ③ `KD_SELFTEST_ONLY=uiicons` ④ `KD_SELFTEST_ONLY=crossing` (uiicons+crossing **always**) ⑤ the bundles in the table. State-changing steps add `--snaptest` (`KD_SNAP_QUICK=1`), UI steps add `--uifuzz 30`. Run with `dotnet bin/merge/KoreanDivision.dll` (an environment issue where the apphost cannot find the runtime, recorded in STATE).
   uiicons **is normally FAIL in steps after breach2 until B2 (t38c-on-ui) is merged** (11 buttons > 9 cells), crossing **is normally FAIL until B5b (crossing-fix)** — in that interval only check that the FAIL is "that one case only" (stop for any other FAIL).
9. One line per step: `ops/MERGE_QUEUE.md` merge completed table (hash · branch · what went in · FV/Replay · first 16 chars of detcheck). Do not write other ops (release's job).

## 1. Merge order (group → step)
Notation: `rehearsal SHA` = merge commit in integration/rehearsal (if none, "untested" = a branch not in the rehearsal, conflict prediction is an estimate from file overlap). Number = post-merge value `FV/Replay` (previous kept if unchanged). detcheck column = expected checksum after merging (first 16 chars).
**The numbers depend on this order**: rail 58 · t30a 59 · t30b 60 · IsAi 61 · AE2 62 (not ordered, left empty) · river 63 · bridge 64 · smoke 65. Changing the order changes the numbers, so do not change it.

### Group A - infrastructure · main-based alone (FV unchanged, golden unchanged: expected detcheck = main 8aa6448b)
Tag `pre-merge-A`. Independent of each other, order fixed.
| Step | Branch @tip | Replacement/note | Base | Expected conflict | Extra check |
|---|---|---|---|---|---|
| A1 | tools/fv-renumber @a9366d4 | only adds fv_renumber.ps1 | main | none | `fv_renumber.ps1 -Report` prints 52/19 |
| A2 | tools/monitor-launch @75a1712 | rehearsal a293ad1 (X3 clean) | main | none | - |
| A3 | kd/crash-log @69320b1 (T41) | crash log, part code | c108343 | Program.cs possible (untested) | crashlog bundle, `--uifuzz 30` |
| A4 | kd/endline-50 @476ceec (AK: end line 70→50) | mission json values only | 0ca2157 | none expected (untested) | missions bundle |
| A5 | sfxnew @aaba414 (T21) | rehearsal 1bc839a (DECISIONS_PENDING union) | d2c05d5 | docs/DECISIONS_PENDING.md union | `sfx`/`audio` bundle (check the name in the `--selftest` list), sounds.json valid |
| A6 | gt-face-proto @bc52007 · t30d-train-art @8e51e5a | game-tools/art only. t30d is rehearsal 6314c28 (TRAIN_PLAN union, T30a/b/c/e cards = ours) | - | card add/add | confirm no code change: no src/ in `git diff --stat` |
End of A check: build + detcheck (8aa6448b kept) + uiicons + crossing PASS (both pass on main). Tag `post-A`.

### Group B - breakthrough → order queue → AI 3B → T24/T25 → communication (sim, R3, core)
Tag `pre-merge-B`. All are a breach2-family stack so the **order below is mandatory**.
| Step | Branch @tip | Replacement/note | Base | Rehearsal SHA / expected conflict | FV/Replay | Bundle | detcheck expected |
|---|---|---|---|---|---|---|---|
| B1 | wip/breach2 @6ad6c50 (T03) | wip-resolved/breach 7faccf0·wip/breach are contained in breach2 → do not merge | main(8d22ad5) | 87ba391 / ops/QUEUE | 53/20 | breach, snapshot, replay | 19501757 |
| B2 | kd/t38c-on-ui @85e6658 (T38c, **uiicons blocker fix**; on top of t38a-settings-keys 7274c71). kd/t46-card-overflow 54e8a54 conflicts with T27 paging and is **replaced - do not merge** | on top of breach2 (base includes the t38a chain, §Caution B2) | breach2 | untested / CONTROLS.md, UiIconsSelfTest.cs, ops/QUEUE.md conflicts expected → **manual resolution when stacking** (no automatic ops ours, exception for these three only) | unchanged | uiicons (must PASS now), command, `--uifuzz 30` | 19501757 |
| B3 | wip/orderq2 @0377f8a (T04) | wip/orderq is the original → forbidden | breach2 | 1ce5e56 / ops/QUEUE | 54/21 | orderq, firectl, replay, command | 19501757 |
| B4 | ai3b2 @f74a675 (T05) | wip/ai3b2 forbidden | orderq2 | 5dd80ea / ops/QUEUE | 55/21 | ai3b, snapshot | 19501757 |
| B5 | kd/t24-s1s3 @07ac80c (T24) | | on top of orderq2 | 53b7c9a / ENGINE_NOTES union | 55/21 | cavcmd, command, drill, officer | ccfbd1dd |
| B5b | kd/t24-crossing-fix @390763a (**crossing blocker fix**; after t24-s1s3 07ac80c) | 1 commit on top of t24-s1s3 (crossing stage observer keep=0) | t24-s1s3 | untested / none expected (TacticalAi.Artillery.cs) | unchanged | **crossing PASS mandatory**, tactical, officer, command | ccfbd1dd unchanged (branch report: golden unchanged) |
| B6 | kd/acting-delay-tier @7cfa0c2 (T37, acting NCO delay tier) | on top of t24-s1s3; tip is separate from the crossing fix | t24-s1s3 | untested / none~few | unchanged | officer, command, replay | **changed (expected)**: the branch's own golden 6c96b4d6 → integrated value untested. Record the new value only, no regeneration |
| B7 | kd/t25-bayonet-all @29695c3 (T25) | | ai3b2? (chain unrelated to t24) | 8b65175 / none | unchanged | charge, pursuit | bef5ffb0 (rehearsal value without T37; differs when T37 is merged - record the new value) |
| B8 | kd/t31a-regroup @97ed026 | default off | ai3b2 | 0f20308 / ops | unchanged | pursuit2, tactical | previous kept |
| B9 | kd/t31c-surrender @e9cd527 | default off | 5c6db29 | 2d2da45 / src/Tests/TacticsObs.cs: keep both env hooks | unchanged | surr, snapshot | kept |
| B10 | kd/t10-comms-b @1c91c96 (T10) | | breach2 | be04a5b / FV/Replay lines of ENGINE_NOTES, Replay.cs, Snapshot.cs | **57/22** (56 left empty, same as rehearsal) | fieldops, replay, snapshot, savecompat | kept |
| B11 | kd/t10b-difficulty @4d74ce9 | on top of t10 | t10 | 1ca8904 / none | 57/22 | comms, fieldops | kept |
| B12 | kd/t16-trench @2cb118c | mission json only (below adoption criteria but user Z1 "keep the change") | breach2 | 67c390d / none | unchanged | missions, n32 | kept |
| B13 | kd/t34a-suppress @29c2479 (**replaces previous 3d0f4de**; FV 56 within the branch → moved by §Caution B13-2) · kd/t32a-pioneer @26cc9fd · kd/t32d-pioneer-art @2118f9b (**tip may move** - art fix in progress) | all default off/data·art. t32d is on top of t32a. t34a is on top of ai3b2 | ai3b2 | untested / TacticalAi*.cs·units json possible | unchanged (the 55 branch value of t32a·t32d → 57 kept) | ai, units, pioneer, regression: ai3b, tactical | kept |

Caution B13: the Snapshot.cs line of t32a·t32d is 55, so if a conflict arises on merge **keep the current (57)**.
Caution B13-2: t34a (29c2479) is FV 56 within the branch. 56 overlaps the rehearsal·plan numbers, so **do not merge it right away at B13 but move it to the tail of group E (after E3)**. The number is reassigned to **an empty number after the AE2 slot (62)**, and the exact number is confirmed by the conductor by checking against FV_RESERVATIONS (no arbitrary assignment). t32b (33284fa, 56 within the branch) has the same situation - reassigned at E3.
Caution B2: kd/t38c-on-ui sits on top of t38a-settings-keys 7274c71 (confirmed by git: 7274c71 is an ancestor of 85e6658), so merging brings the content of the t38a chain in together at B2. To avoid duplicate merging with G1~G6 of group G, **the conductor checks the chain range before execution** and puts the order exception in DECISIONS_PENDING. Do not execute B2 before this check.
**End of B**: full check = build + detcheck + uiicons + crossing + snaptest + uifuzz 30. FV 57 / Replay 22. The golden at this point differs from main (8aa6448b) → **leave the golden file as it is** and in the test only check whether the `--detcheck` output equals the value in the column above (mismatch = golden FAIL is normal). Tag `post-B`.

### Group C - railway · cover forest (presentation·terrain, before trains)
Tag `pre-merge-C`.
| Step | Branch @tip | Base | Rehearsal SHA / expected conflict | FV | Bundle | detcheck |
|---|---|---|---|---|---|---|
| C1 | rail @9f513e7 (T07; wip/rail forbidden) | 23dba85 | 16a63bc / ENGINE_NOTES (union), Snapshot.cs FV line (53→**58**), ops | **58**/22 | terrain, snapshot, savecompat | kept |
| C2 | t28-cover-forest @c577c0f | rail | 1a2606b / ops only | 58 | props, forest | kept |
Check after C1 with `fv_renumber -Set 58 22`. Tag `post-C`.

### Group D - trains (t30a → t30b → …) + immune target + annihilation judgment
Tag `pre-merge-D`. The hardest place in the rehearsal (t30a 8 files, t30b 6 files).
| Step | Branch @tip | Base | Rehearsal SHA / expected conflict and resolution | FV/Replay | Bundle | detcheck |
|---|---|---|---|---|---|---|
| D1 | t30a-train-move @863f9d1 (T30a) | rail | 1df3a01 / DECISIONS_PENDING, GameCommand.cs, Replay.cs, Organisation.cs, Snapshot.cs, ReplaySelfTest.cs, SnapshotSelfTest.cs, ops. Resolution = union of command registration·save calls·test lines (order HEAD→train). **Prefer reusing the rehearsal SHA checkout** | 58→**59**/**23** | train, replay, snapshot | kept |
| D2 | t30b-train-weapons @ca1cdb8 (T30b) | t30a | 09413f5 / FogOfWar, Organisation, Snapshot, Train.cs, SnapshotSelfTest, ops. Resolution: train save moved from ORG_ to ENTS → delete WriteTrains/ReadTrains in ORG_ (keep OrderQueues), Fog union, Train hash theirs, Snapshot Audit manual merge (OrderQueue line + trainMask + TrainGun) | 59→**60**/23 | trainweapons, train, snapshot, replay | kept |
| D3 | kd/immune-target-fix @48bd6ce (T36) | t30b | untested / none~few (auto target code) | 60 (branch 59 → keep 60 on a line conflict) | that card's bundle + ai, tactical | **changed (expected)**: the branch's own golden fcf38519 336c39cd. Record the new value only |
| D4 | t30c-freight-objective @3de60d5 | t30b | 7dda62d / ops | 60 | freight, missions | previous kept |
| D5 | t30e-demo-mission @7f46a4c | t30c? (on top of t30b) | 4e68859 / ops. traindemo bundle FAIL 1 = `train_demo.golden` (regenerate at the end) | 60 | freight, traindemo (FAIL 1 expected) | kept |
| D6 | kd/trains-in-annihilation @2de288d (T40) | t30e | untested / none | 60 | train, missions | kept (unchanged except train missions) |
`t30d-train-art` is in A6. Tag `post-D`.

### Group E - FV tail: IsAi · river · AI smoke
Tag `pre-merge-E`. Numbers 61/24 → (62/25 left empty) → 63 → 64 → 65.
| Step | Branch @tip | Base | Expected conflict | FV/Replay | Bundle | detcheck |
|---|---|---|---|---|---|---|
| E1 | kd/isai-restore @5c08a38 (T35 / AF1) | main(592392d) | the 60/24 lines of Snapshot.cs·Replay.cs → **renumber to 61/24**; the Replay.cs header·checksum `if (f.IsAi) h.Add(0x4149)` (unchanged for non-AI factions) may need a manual merge (untested) | **61/24** | replay, snapshot, surr (surrender 27 vs 27), savecompat | kept |
| E2 | river-r1 @5b7b9f1 → river-r2 @8ada7a9 → river-r3 @26df7f6 → river-r4-bridge @9d4b7be (T39a~d, one step at a time) | rail | untested / Terrain.cs enum (Ford, Bridge byte 13), Snapshot FV line, MapFile, render. r1 FV 54 → **63**, r4 (branch 55) → **64** | **63→64**/24 | terrain, crossing, missions, snapshot, savecompat | kept (branch report: golden unchanged) |
| E2b | kd/t33d-rail @ceaa067 → 9b72f2f (T33d, **independent tip**; correction of the earlier "same tip as H1") | rail + river r1–r4 (after E2) | untested / no Snapshot·Replay FV lines expected, map editor side files - on conflict §report | 64 kept (record if it changes) | rail, river (crossing), missions | kept |
| E3 | kd/t32b-ai-smoke @33284fa (T32b) | ai3b2 | Snapshot FV line (56 within the branch) → **65**; tactics.smokeAi save; TacticalAi*.cs. t34a, which uses the same 56 (§Caution B13-2), is reassigned an empty number after E3 | **65**/24 | smokeai, ai3b, snapshot | kept (off) |
E2b caution: fix the T33c test `test_river_crossing` to **skip** missions that reference a 'map' and then run (no other fixes).
1 non-blocking item after E2: the `terrain.waterBlocksFire` key of river-r2 is not in balance.json → as the review directed, **add only 1 line equal to the default value true** (no changes to key name·value). Do not touch other river values.
End of E: full check (build, detcheck, uiicons, crossing, snaptest, uifuzz 30). `fv_renumber -Report` gives 65/24 and no duplicates with the FV_RESERVATIONS table. Tag `post-E`.

### Group F - campaign axis (campaign Schema only, sim FV unchanged)
Tag `pre-merge-F`. Dependencies: gemini-import → t29b → t19a → t19b2 → t19c → t19d → t38f → t42d. Rehearsal: the "same fix in two copies" interval is here.
| Step | Branch @tip | Replacement | Rehearsal SHA / expected conflict and resolution | Bundle |
|---|---|---|---|---|
| F1 | gemini-import @63080df (T01, 336 files) | wip/gemini-import forbidden | bdab8cd / ops/QUEUE only | missions, savecompat, lodaudit, snapshot |
| F2 | t29b-on-t09 @a89b4d2 (replaces T09·T29) | **do not merge t09-camp-slice, t29-campaign-economy-json** (replaced by t29b) | ea8edf2 / same fix in 2 copies such as WorldMission.ScaleAmmo → T29b per-mille version | missions, savecompat |
| F3 | t19a-save-slots @43e69b1 | | ad89c8e / CampaignState.cs, GameMission.cs, CampaignMenu.cs (5 places): keep the T01 helpers (HasNullElements·SaveUnlessLoadFailed·CommitResult) + T19a BackupBroken·SaveLive·SaveWithAuto | savecompat, missions |
| F4 | t19b2-synced @4980e7b (includes T19b - do not merge t19b-ammo-shells alone) | | 45735c5 / campaign_economy.json, CampaignEconomy.cs, CampaignEconomyDefs.cs, CampaignState.cs, WorldMission.cs, MissionSelfTest.cs: T29·T19a cherry-pick duplicates → campaign files theirs, per-weapon ammo multiplier theirs (Schema 3) | missions, savecompat |
| F5 | t19c-home-tabs @9260e36 | | a698886 / ops | check before doctrines: missions |
| F6 | t19d-doctrines @04a9149 | wip/doctrines forbidden | dbdb2be / ops | doctrines, missions, officer, savecompat |
| F7 | t38f-home-readonly-tabs @b622d1d | t38f and t19d once had the same tip; now on top of t19d | untested / none~few (home tabs) | missions, savecompat, campaign fuzz |
| F8 | t42d-schedule-log @06ee9bd (includes t42a·b·c, **in one go**; final tip - after including the 57f5440 review fix) | do not merge t42a/b/c alone | untested / CampaignState.cs (schema v4~v7 migration lines), campaign_economy.json possible. Keep schema order up to v7 | savecompat (including migration v3~v6), missions |
End of F: campaign Schema 7, sim FV 65 unchanged, detcheck unchanged. Tag `post-F`.

### Group G - UI chain · command conveniences
Tag `pre-merge-G`. t08 → (t17, t27) → t38g → t38b → t38a. **uiicons · uifuzz 30 mandatory**.
| Step | Branch @tip | Note | Rehearsal SHA / expected conflict | Bundle |
|---|---|---|---|---|
| G1 | t08-ui-bundle @2e9aef5 | | 7d36407 / ops | uiicons, command |
| G2 | t17-cmd-convenience @0eb92e8 | | d136b32 / ENGINE_NOTES union | command |
| G3 | t27-cmd-ui2 @4cf5c45 | | 51daad0 / ops | command, uiicons |
| G4 | t38g-ui-harness @f5a76d4 | (t38h is G4b below. If a new commit appeared on the tip, §report) | untested / Program.cs, Ui.cs few | uifuzz 30 |
| G4b | kd/t38h-fuzz-campaign @f126551 (**e5f76ec is a duplicate cherry-pick of f5a76d4 → drop**; f126551 is the real work) | after t38g merge. Caution: f126551 **contains e5f76ec as an ancestor**, so a plain merge brings in the duplicate → after conductor confirmation apply only the range `e5f76ec..f126551` | untested / Program.cs, Ui.cs (duplicate conflict with t38g possible) | uifuzz 30 |
| G5 | t38b-help-overlay @93ce79f | | untested / none~few | uifuzz 30, help tests |
| G6 | t38a-settings-keys @7274c71 | **t38e-unitlist-codex is the same tip (7274c71) → no separate merge** | untested / few | uifuzz 30 |
| G7 | kd/t45-menu-hide @88735b1 | main menu·mission list | untested / MainMenu.cs, MissionMenu.cs (close to 1 line of G4) | menuhide, uifuzz 30, missions |
| G8 | kd/t43-branch-symbols @d45b18f (**replaces previous b9c97a9**) | duplicate T43 row in QUEUE (do not touch ops) | untested / UnitSymbols (close to the t27 marker) | uiicons |
End of G: full check. Do not open an image viewer (sheet files only). Tag `post-G`.
G on hold: t38e-unitlist-codex (in progress), t38i (in progress) - not in the plan until the tip is settled. Add after §report once the tip is confirmed.

### Group H - tools · map editor · art · documents
Tag `pre-merge-H`. Most items leave the game code unchanged → golden unchanged.
| Step | Branch @tip | Note |
|---|---|---|
| H1 | kd/t33a-map-model @c5b30f4 → kd/t33b-map-ui @5207b8f → kd/t33c-placement @c514821 (kd/t33d-rail is now an independent tip ceaa067·9b72f2f → §E2b) | separate exe (MapEditor.App), game code unchanged. Editor tests of the `dotnet test` kind (264+) and the `--selftest` editor bundle. **If Bridge is not in the terrain list, do not fix it but report as a question** (DECISIONS_PENDING). |
| H2 | t39i-ruined-houses @66d6b95 | house ruins code+sheet. Do not open the sheet image. props/forest bundle |
| H3 | spec/* card branches (spec/cards-t24-t25-t10-t16, cards-t27-t28, cards-t30-train, cards-t31, cards-t32, cards-t34, cards-t39, cards-t42, spec/ui-backlog, spec/t19-split, spec/t23-campaign-home, spec/river-design, kd/t33-map-editor-design) | documents only (cards·design documents). Conflicts = `tasks/*.md` ours, other documents union, ops ours. Optional step (skipping has no effect on code). If `git diff --stat main..<br>` has src/, skip that branch and report |

## 2. Branches not merged
| Branch | Reason |
|---|---|
| wip/breach, wip-resolved/breach, wip/orderq, wip/ai3b2, wip/rail, wip/gemini-import, wip/sfxnew, wip/doctrines, wip/battlereport, wip/gt-* , wip/rerender-1 | original/old patch branches. Replaced by restored versions (breach2, orderq2, ai3b2, rail, gemini-import, sfxnew…) |
| wip/casualty, wip/casualty2 | D19 C discarded, T06 closed (inert kept) - the rehearsal merged them but the plan excludes them |
| wip/chokefix | D14 on hold |
| t09-camp-slice, t29-campaign-economy-json | replaced by t29b-on-t09 |
| t19b-ammo-shells, t42a, t42b, t42c | included in t19b2 / t42d respectively (duplicates if merged alone) |
| e5f76ec of t38h-fuzz-campaign | duplicate cherry-pick of t38g f5a76d4 → drop (the real work f126551 is included via §G4b) |
| t38e-unitlist-codex, duplicates sharing the same tip as kd/t33c-placement | no separate new commits (kd/t33d-rail is an independent tip so excluded here → §E2b) |
| measure/t06b, t12, t15, t31b · kd/t16b-rootcause | measurement·analysis only (no code) |
| kd-rerender (T11), gt-brender, gt-rerender-prep | re-render in progress (separately after the queue ends) |
| kd/t44-smoke | same as main (ahead 0), meaningless |
| integration/rehearsal | rehearsal only (no merge into main, rehearsal card rule). Reference for reusing resolutions only |
| docs/roadmap-review2, docs/roadmap-sync | document cleanup branches - outside this plan's scope, conductor's judgment |
| kd/t34b~e, t32c, t38c/d/e (spec), after T33d, not-ordered after T42 | no code yet |
| kd/t46-card-overflow @54e8a54 | conflicts with T27 paging (Game.cs) → replaced by t38c-on-ui (85e6658) (§B2) |
| t38e (in progress), t38i (in progress) | tip unconfirmed → excluded from the plan (§G on hold) |

## 3. Rollback · stop rules
- Before each step check `git status` clean + no `.git/MERGE_HEAD`. Stop mid-merge: `git merge --abort`. Verification failure after the merge commit: `git reset --hard ORIG_HEAD` (only the last merge on top of merge/0.2; merge-procedure.md step 5 permits).
- Tags: `pre-merge-<A..H>` before the group starts, `post-<group>` when it ends (`git tag`, lightweight tag). Group-level rollback (`reset --hard pre-merge-X`) **only after user confirmation**.
- Forbidden: any `git push`, `--force`, branch deletion (cleanup of merged branches·worktrees only after everything is done and on user directive - step 6 "removal" of the usual procedure is on hold this time), direct merge into main (§0-3).
- Stop conditions: conflict not in the description · non-mechanical conflict (logic judgment needed) · unexpected checksum change · selftest FAIL not in the table · tip mismatch · build failure → roll back that step and report (with the file/line/FAIL line). Retrying the same step is limited to 1 time.
- Exception: only the "normal FAILs" of this document §0-8 (uiicons before B2, crossing before B5b, D5 traindemo golden) are allowed.
- If different from the checksum expectation, do not roll back but "record then report": do not regenerate the golden.

## 4. Places needing user confirmation
1. **Final confirmation before start (required by AT)**: this whole plan. Until approval here, do not even create the worktree of §0.
2. **Golden regeneration**: the new golden by rehearsal `bef5ffb0 822cdacd` (units 20→159, shots 2530→5359; change points breach2→t24→t25). With T36 (D3)·T37 (B6) added in this plan the **value differs** (both branches change their own golden). After group H, show the final `--detcheck` value to the user and only after OK do `--detcheck --update` → regenerate `tools/detcheck.ps1` 8 variants + `train_demo.golden`. Not at intermediate steps.
3. **Raising main**: confirm right before the final (after golden regeneration·release full selftest pass) `git -C C:/gameProject merge --ff-only merge/0.2`. Stop if there are overlapping uncommitted files.
4. Right before group E (FV numbers exhausted up to 65, AE2 62 left empty) and whether to merge the H3 documents are the conductor's judgment (question list in korean-division/docs/DECISIONS_PENDING.md).
5. The 15 sheet reviews (`C:\works\_review_sheets_2026-10-22`) are independent of merging - merging does not block on them.

## 5. Work division · heavy pool usage
- Heavy jobs = build, selftest bundles, snaptest, uifuzz, detcheck. **Up to 2 concurrent per agent** (user rule heavy-job-concurrency; for the pool limit see ops/LIMITS.md, shared with other agents) - a merge agent defaults to 1 at a time, only 2 bundles in parallel. The gate `-Priority` only while holding the merge lock.
- Because of the agent context 200k rule, the merger (sonnet) is **launched fresh per group**; one agent does not take over.

| Agent | Scope | Estimated tokens | Number of heavy jobs (approx.) |
|---|---|---|---|
| merger-1 | §0 + A + B1~B4 | 150k | 6 builds + 8 bundles |
| merger-2 | B5~B13 | 180k | 9 builds + 14 bundles |
| merger-3 | C + D | 150k (large conflict resolution; rehearsal SHA reuse) | 8 builds + 12 bundles + 4 snaptest |
| merger-4 | E | 120k | 5 builds + 8 bundles |
| merger-5 | F | 180k | 8 builds + 14 bundles |
| merger-6 | G + H | 150k | 11 builds + 8 uifuzz + 12 bundles |
| release | golden (after user OK) + full selftest once (about 12 min) + snaptest + 8 variants + publish + record | 80k | 1 full selftest |
| sim (opus) standby | consult only when stopped at R3 conflicts not in the description (B6/B7, D3, E1~E3) | as needed | - |
| reviewer | optional: at the end of each group `git diff pre-merge-X..post-X` determinism·Audit check | 60k x 3 | none |
- Serial order fixed (stack·FV dependency). No parallel merging within a group (1 lock). Estimated duration: total about 47 builds · about 68 bundles; assuming pool 2, merging alone takes 6~8 hours (in practice depends on gate waits).
