# CARD_INDEX - Task card index (as of 2026-10-22, main f3fe6db · measured snapshot `git branch -v` / `git worktree list`)
> **STALE SNAPSHOT (2026-10-23)**: not maintained. Current state = ops/QUEUE.md, `git branch -v`, `git worktree list`. Do not hand-update; a generated table is planned for the pipeline redesign.

> Purpose: find on one page which branch·worktree·file holds card Tnn. main's korean-division/tasks has only T01~T13, so for the rest the **card branch** is given (`git show <branch>:korean-division/tasks/<file>`).
> Source: ops/PIPELINE_LOG.md (tail 60, 13:3x~15:0x) · ops/QUEUE.md · `git branch -v` · `git worktree list`. Branches were not checked out.
> State values: wip-ready / running / queued / closed / blocked(reason) / hold-balance 0.3 / hold-AI 0.4. Version = ops/VERSIONS.md (0.2 before balance · 0.3 after balance · 0.4 AI improvements). Cell not confirmed = `?`. "needs checking" = commits exist but there is no basis in the log·branch title that records the card state.
> Worktree path = `C:/gameProject/kd-develop/<name>` (gt is `C:/gameProject/gt-develop/<name>`). `-` = no worktree.
> base (stack) = the card directly below that this card sits on (by the log·QUEUE stack chain). main = merged base.
> Update rule: when the state changes, fix only that row of this table. Re-check the branch tip with `git branch -v`.
> Correction (2026-10-22 measured): stale values of the previous table — T02 wip/bigbldg no branch·closed / T04 orderq2 tip 0377f8a / T05 f74a675 / T09 0c82a47 / T10b 4ac07b4→4d74ce9 / T12 measure/t12 e6122ac / T14 closed / T15 queued→measurement complete / T16 8b38b29→2cb118c / T29 replaced / T30b·T36·T37·T24 worktree mismatch corrected / T31b endline 10361a4→476ceec / T32a·T32b·T34a in progress→wip-ready / T33a ef0e39b→c5b30f4 / T35 592392d→5c08a38 / T38a·b·c·f·g wip-ready corrected / T39a·b·c·d·i / T40·T41 wip-ready corrected / T42a~d·T43·T45 new rows.

| id | title | ver | state | branch @latest commit | worktree | base (stack) | card file (branch : path) |
|---|---|---|---|---|---|---|---|
| T01 | wip/gemini-import restoration·merge (campaign WIP+UI, save transaction) | 0.2 | wip-ready (review fix commit 63080df) | gemini-import @63080df | kd-develop/kd-gemini-import | main | main : korean-division/tasks/T01-gemini-import.md |
| T02 | Building size increase (wip/bigbldg) | ? | closed (merged 8d22ad5) | wip/bigbldg no branch | - | main | main : korean-division/tasks/T02-bigbldg.md |
| T03 | Trench breakthrough (breach2) | 0.2 | wip-ready | wip/breach2 @6ad6c50 (resolved version wip-resolved/breach @7faccf0) | kd-develop/kd-breach2 | main (8d22ad5 basis) | main : korean-division/tasks/T03-breach.md |
| T04 | Order queue·fire control (orderq2) | 0.2 | assigned (commit 0377f8a review fix; wip-ready declaration needs checking) | wip/orderq2 @0377f8a | kd-develop/kd-orderq2 | T03 | main : korean-division/tasks/T04-orderq.md |
| T05 | Tactical AI 3B (ai3b2) | 0.2 | wip-ready | ai3b2 @f74a675 | kd-develop/kd-ai3b2 | T04 | main : korean-division/tasks/T05-ai3b2.md |
| T06 | Casualty speed (D19 C discarded, casualty2 inert kept) | 0.3 | closed | wip/casualty2 @0722493 | kd-develop/kd-casualty2 | ? | main : korean-division/tasks/T06-casualty.md |
| T06b | Casualty re-measurement card (D19 C, 24 seeds, card only) | 0.3 | closed (follows T06 closure) | measure/t06b @e241b94 | kd-develop/kd-meas-t06b (+ d19c·tip 2 temporary, 97ed026 detached) | ? | measure/t06b : korean-division/tasks/T06b-remeasure.md |
| T07 | Railway terrain (rail, armored train excluded) | 0.2 | wip-ready | rail @9f513e7 | kd-develop/kd-rail | main | main : korean-division/tasks/T07-rail.md |
| T08 | Controls UI bundle + paper texture | 0.2 | wip-ready | t08-ui-bundle @2e9aef5 | kd-develop/kd-t08 | T01 | main : korean-division/tasks/T08-ui-bundle.md |
| T09 | Campaign vertical slice (replaced by T29b, merge by T29b alone) | 0.2 | wip-ready | t09-camp-slice @0c82a47 | kd-develop/kd-t09 | T01 | main : korean-division/tasks/T09-camp-slice.md |
| T10 | Communication alpha ②~④ (counter-battery·night·march fatigue) | 0.2 | assigned (commit 1c91c96; wip-ready declaration needs checking) | kd/t10-comms-b @1c91c96 | kd-develop/kd-t10 | T03 | main : korean-division/tasks/T10-comms-b.md |
| T10b | Difficulty sinks (messenger capture·wire cut) | 0.2 | wip-ready | kd/t10b-difficulty @4d74ce9 | kd-develop/kd-t10b | T10 | kd/t10b-difficulty : korean-division/tasks/T10b-difficulty.md |
| T11 | All-unit re-render (partial 4/70, driver running) | 0.2 | running | kd-rerender @467dafc | kd-develop/kd-rerender (dirty 2) | main | main : korean-division/tasks/T11-rerender.md |
| T12 | Integrated balance measurement (A/B/C seeds 8) | 0.3 | wip-ready (measurement complete, balance on hold 0.3) | measure/t12 @e6122ac | kd-develop/kd-meas-t12 (+ t12s temporary f74a675, dirty 2) | ai3b2 stack | measure/t12 : korean-division/tasks/T12-integ-measure.md |
| T13 | release (full selftest·golden·publish·record) | 0.2 | queued (a 14:3x release first-real-run record exists — conductor to check state) | - | - | T12 | main : korean-division/tasks/T13-release.md |
| T14 | Document overhaul (4-layer structure + ops + PIPELINE v1) | ? | closed (merged b5918a8 · a4c79c7 · docs-fable) | - | - | main | ? |
| T15 | Night soak re-report (measurement a70a308 complete) | 0.3 | wip-ready (measurement complete, balance on hold 0.3) | measure/t15 @a70a308 | kd-develop/kd-meas-t15 (dirty 2) | ai3b2 stack | measure/t15 : korean-division/tasks/T15-night-soak.md |
| T16 | trench_assault cause investigation (below baseline, Z1) | 0.3 | wip-ready (balance on hold 0.3) | kd/t16-trench @2cb118c | kd-develop/kd-t16 | T03 | kd/t16-trench : korean-division/tasks/T16-trench-assault.md |
| T16b | trench cause re-investigation (analysis only, closed) | 0.3 | closed (analysis closed) | kd/t16b-rootcause @e724437 | kd-develop/kd-t16b | ? | kd/t16b-rootcause : korean-division/tasks/T16b-rootcause.md |
| T17 | Command conveniences (in-mission save·results report·engagement alerts) | 0.2 | wip-ready (per CARD_INDEX; needs checking) | t17-cmd-convenience @0eb92e8 | kd-develop/kd-t17 | T08 | t17-cmd-convenience : korean-division/tasks/T17-cmd-convenience.md |
| T18 | Korean army tachanka | ? | closed (already in main, tachanka a1a35e6) | - | - | main | ? |
| T19 | Campaign home MVP / rules 1~15 / school (umbrella → split into T19a~d) | 0.2 | closed (split: see rows T19a~d) | spec/t19-split @aea476b | kd-develop/kd-t19spec | T09 | ? (split cards = see rows T19a~d) |
| T19a | Save slots | 0.2 | wip-ready | t19a-save-slots @43e69b1 | kd-develop/kd-t19a | T09 | spec/t19-split : korean-division/tasks/T19a-save-slots.md |
| T19b | Per-caliber ammo·shell rules | 0.2 | wip-ready (presumed) | t19b-ammo-shells @622db02 | kd-develop/kd-t19b | T19a | spec/t19-split : korean-division/tasks/T19b-ammo-shells.md |
| T19b2 | Record synchronization verification | 0.2 | wip-ready | t19b2-synced @4980e7b | kd-develop/kd-t19b2 | T19b | ? |
| T19c | Campaign home tabs | 0.2 | wip-ready | t19c-home-tabs @9260e36 | kd-develop/kd-t19c | T19b2 | spec/t19-split : korean-division/tasks/T19c-home-tabs.md |
| T19d | School (doctrine) data·tab | 0.2 | wip-ready | t19d-doctrines @04a9149 | kd-develop/kd-t19d | T19c | spec/t19-split : korean-division/tasks/T19d-doctrines.md |
| T20 | Repository move wrap-up (deleting old folders = U02) | ? | blocked (U02) | - | - | - | ? |
| T21 | Sound effects restoration (sfxnew) | 0.2 | wip-ready | sfxnew @aaba414 | kd-develop/kd-sfxnew | main | sfxnew : korean-division/tasks/T21-sfxnew.md |
| T22 | Face prototype (gt-face-proto) | ? | wip-ready | gt-face-proto @bc52007 | gt-develop/gt-face-proto | main | gt-face-proto : korean-division/tasks/T22-face-proto.md |
| T23 | Campaign home design document (docs/CAMPAIGN_HOME_DESIGN.md, no code) | ? | wip-ready | spec/t23-campaign-home @42fa456 | kd-develop/kd-t23 | main | spec/t23-campaign-home : ? (card filename needs checking) |
| T24 | D08 fix proposal S1~S3 (acting command·observer team·assault suppression) | 0.2 | wip-ready | kd/t24-s1s3 @07ac80c | - (kd-develop/kd-t24 has kd/acting-delay-tier checked out, mismatch) | T04~T05 (ai3b2) | kd/t24-s1s3 : korean-division/tasks/T24-s1s3.md |
| T24x | Crossing observer team fix (crossing fix, on top of T24) | 0.2 | wip-ready (log 15:0x) | kd/t24-crossing-fix @390763a | kd-develop/kd-t24-crossing-fix | T24 (t24-s1s3) | no card (stack after T24 on merge) |
| T25 | Bayonet halt range = all assaults (D02 A) | 0.2 | assigned (commit 29695c3; wip-ready needs checking) | kd/t25-bayonet-all @29695c3 | kd-develop/kd-t25 | ai3b2 stack | kd/t25-bayonet-all : korean-division/tasks/T25-bayonet-all.md |
| T26 | Legacy email blocking git hook (D20 A) | ? | closed (done, .git/hooks installed) | - | - | main | ? |
| T27 | Command UI 2 (color-blind symbol markers) | 0.2 | wip-ready | t27-cmd-ui2 @4cf5c45 | kd-develop/kd-t27 | main | t27-cmd-ui2 : korean-division/tasks/T27-cmd-ui2.md |
| T28 | Cover forest (option, default off) | 0.2 | wip-ready | t28-cover-forest @c577c0f | kd-develop/kd-t28 | T07 (rail) | t28-cover-forest : korean-division/tasks/T28-cover-forest.md |
| T29 | Campaign economy JSON (replaced by T29b) | 0.2 | closed (replaced by T29b) | t29-campaign-economy-json @783e7ce | kd-develop/kd-t29 | T01 | t29-campaign-economy-json : korean-division/tasks/T29-campaign-economy-json.md |
| T29b | Re-stack on top of T09 (replaces T09·T29) | 0.2 | wip-ready | t29b-on-t09 @a89b4d2 | kd-develop/kd-t29b | T09 | t29b-on-t09 : korean-division/tasks/T29b-restack.md |
| T30a | Train object·track movement (FV58) | 0.2 | wip-ready | t30a-train-move @863f9d1 | kd-develop/kd-t30a | T07 (rail) | t30a-train-move : korean-division/tasks/T30a-train-move.md |
| T30b | Train weapons (FV59) | 0.2 | wip-ready | t30b-train-weapons @ca1cdb8 | - (kd-develop/kd-t30b has kd/immune-target-fix checked out, mismatch) | T30a | t30b-train-weapons : korean-division/tasks/T30b-train-weapons.md |
| T30c | Freight train objective | 0.2 | wip-ready | t30c-freight-objective @3de60d5 | kd-develop/kd-t30c | T30b | t30c-freight-objective : korean-division/tasks/T30c-freight-objective.md |
| T30d | Train art (blockout 5 types) | 0.2 | wip-ready | t30d-train-art @8e51e5a | kd-develop/kd-t30d-train-art | T30a (presumed) | t30d-train-art : korean-division/tasks/T30d-train-art-plan.md |
| T30e | Escort demo mission (FV59 kept) | 0.2 | wip-ready | t30e-demo-mission @7f46a4c | kd-develop/kd-t30e | T30c | t30e-demo-mission : korean-division/tasks/T30e-train-demo-mission.md |
| T31a | Rejoin limit (regroup.noRejoinBelow default off) | 0.3 | wip-ready (balance on hold 0.3, do not turn on) | kd/t31a-regroup @97ed026 | kd-develop/kd-t31a | ai3b2 | kd/t31a-regroup : korean-division/tasks/T31a-regroup-limit.md |
| T31b | Mission end line measurement (end line 70→50 complete) | 0.3 | closed | measure/t31b @05c228d (card, dirty 2) / kd/endline-50 @476ceec (code, dirty 1) | kd-develop/kd-meas-t31b · kd-develop/kd-endline50 | main (?) | measure/t31b : korean-division/tasks/T31b-mission-end-review.md · kd/endline-50 : korean-division/tasks/T31b-endline50.md |
| T31c | AI surrender (all keys default off) | 0.4 | wip-ready (turning on = 0.4) | kd/t31c-surrender @e9cd527 | kd-develop/kd-t31c-surrender | ai3b2 | kd/t31c-surrender : korean-division/tasks/T31c-ai-surrender.md |
| T32a | Infantry engineer unit (infantry_pioneer) | 0.2 | wip-ready | kd/t32a-pioneer @26cc9fd | kd-develop/kd-t32a | ai3b2 | spec/cards-t32 : korean-division/tasks/T32a-pioneer-unit.md |
| T32b | AI smoke (key default off) | 0.4 | wip-ready | kd/t32b-ai-smoke @33284fa | kd-develop/kd-t32b | ai3b2 | spec/cards-t32 : korean-division/tasks/T32b-ai-smoke.md |
| T32c | AI barbed wire cutting | 0.4 | queued | - | - | T32b | spec/cards-t32 : korean-division/tasks/T32c-ai-wirecut.md |
| T32d | Engineer art (prone-firing muzzle fix ordered) | 0.2 | running (15:0x muzzle fix re-render) | kd/t32d-pioneer-art @2118f9b | kd-develop/kd-t32d | T32a | spec/cards-t32 : korean-division/tasks/T32d-pioneer-art.md |
| T33a | Map editor document model (headless) | 0.2 | wip-ready (review fix c5b30f4) | kd/t33a-map-model @c5b30f4 | kd-develop/kd-t33a-map-model | main | kd/t33a-map-model : korean-division/tasks/T33a-mapedit-model.md |
| T33b | Map editor UI | 0.2 | wip-ready (log 14:0x) | kd/t33b-map-ui @5207b8f (dirty 1) | kd-develop/kd-t33b-map-ui | T33a | kd/t33-map-editor-design : korean-division/tasks/T33b-mapedit-ui.md |
| T33c | Map editor placement (props·facilities·start points·zones) | 0.2 | running (commit c514821) | kd/t33c-placement @c514821 | kd-develop/kd-t33c-placement | T33b | kd/t33-map-editor-design : korean-division/tasks/T33c-mapedit-place.md |
| T33d | Map editor railway tool | 0.2 | running? (same tip as c514821, no dedicated commit — needs checking) | kd/t33d-rail @c514821 | kd-develop/kd-t33d-rail | T33c | kd/t33-map-editor-design : korean-division/tasks/T33d-mapedit-rail.md |
| T34a | Suppression threshold·cover withdrawal (AI 3C-1) | 0.4 | wip-ready (log 14:3x) | kd/t34a-suppress @3d0f4de | kd-develop/kd-t34a | ai3b2 | spec/cards-t34 : korean-division/tasks/T34a-suppthr-coverwd.md |
| T34b | Framework selection (AI 3C) | 0.4 | queued | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34b-template-pick.md |
| T34c | Combined arms cooperation (FV64 reservation candidate) | 0.4 | queued | - | - | ai3b2 | spec/cards-t34 : korean-division/tasks/T34c-combined-arms.md |
| T34d | Cavalry flank intensity (S4 re-check·tuning) | 0.4 | queued (balance on hold 0.3) | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34d-cav-flank-strength.md |
| T34e | Idle diagnosis | 0.4 | queued | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34e-idle-difficulty.md |
| T35 | IsAi restoration (replay header·checksum, FV60/Replay24 reservation AF1) | 0.2 | wip-ready (log 14:0x, review blocks 0) | kd/isai-restore @5c08a38 | kd-develop/kd-isai-restore | main | ? (card filename needs checking) |
| T36 | Immune target fix (AH2: immunity exemption based on attack orders) | 0.2 | wip-ready | kd/immune-target-fix @48bd6ce | - (kd-develop/kd-t30b has it checked out, mismatch) | ? | kd/immune-target-fix : korean-division/tasks/T36-immune-target.md |
| T37 | Acting NCO delay tier (U4) | 0.2 | wip-ready (commit 7cfa0c2) | kd/acting-delay-tier @7cfa0c2 | - (kd-develop/kd-t24 has it checked out, mismatch) | ? | kd/acting-delay-tier : korean-division/tasks/T37-acting-delay.md |
| T38a | Settings key rebinding | 0.2 | wip-ready (log 14:3x) | t38a-settings-keys @7274c71 | kd-develop/kd-t38a | T38b | spec/ui-backlog : korean-division/tasks/T38a-settings-keys.md |
| T38b | Help overlay | 0.2 | wip-ready (log 14:4x) | t38b-help-overlay @93ce79f | kd-develop/kd-t38b | T38g | spec/ui-backlog : korean-division/tasks/T38b-help-overlay.md |
| T38c | Command card overflow (= kd/t46-card-overflow, same job as card T38c) | 0.2 | wip-ready (log 14:3x) | kd/t46-card-overflow @54e8a54 | kd-develop/kd-t46 | wip/breach2 (T03) | spec/ui-backlog : korean-division/tasks/T38c-cmdcard-overflow.md |
| T38d | Paper UI phase 2 (campaign home + combat HUD) | 0.2 | queued | spec/ui-backlog @28605bf (card branch) | - | main | spec/ui-backlog : korean-division/tasks/T38d-paper-ui-phase2.md |
| T38e | Unit list·codex·tooltips | 0.2 | queued (no commits, 10 uncommitted in worktree) | t38e-unitlist-codex @7274c71 (= T38a tip) | kd-develop/kd-t38e (dirty 10) | T38a | spec/ui-backlog : korean-division/tasks/T38e-unitlist-codex-tips.md |
| T38f | Home follow-up tabs read-only (on top of t19d) | 0.2 | wip-ready (log 14:3x) | t38f-home-readonly-tabs @b622d1d | kd-develop/kd-t38f | T19d | spec/ui-backlog : korean-division/tasks/T38f-home-tabs-readonly.md |
| T38g | UI harness (on top of t27) | 0.2 | wip-ready (log 14:3x) | t38g-ui-harness @f5a76d4 | kd-develop/kd-t38g | T27 | spec/ui-backlog : korean-division/tasks/T38g-ui-harness.md |
| T38h | UI fuzz·campaign screen coverage (uifuzz reaches campaign home, log 42 gap follow-up candidate) | 0.2 | needs checking (no log record; 4 uncommitted in worktree) | t38h-fuzz-campaign @e5f76ec | kd-develop/kd-t38h (dirty 4) | T38g (per commit message) | ? |
| T39a | River Ford terrain (river-r1, FV54) | 0.2 | wip-ready (log 13:3x) | river-r1 @5b7b9f1 | kd-develop/kd-river-r1 | rail | spec/cards-t39 : korean-division/tasks/T39a-river-ford.md |
| T39b | River pathing (waterBlocksFire key) | 0.2 | wip-ready (log 14:0x) | river-r2 @8ada7a9 | kd-develop/kd-river-r2 | T39a | spec/cards-t39 : korean-division/tasks/T39b-river-pathing.md |
| T39c | River render (synthetic ford tile·test_river) | 0.2 | wip-ready (log 14:2x) | river-r3 @26df7f6 | kd-develop/kd-river-r3 | T39b | spec/cards-t39 : korean-division/tasks/T39c-river-render.md |
| T39d | River bridge (Bridge terrain, default off) | 0.2 | wip-ready (log 15:0x; sheet update incomplete) | river-r4-bridge @9d4b7be | kd-develop/kd-river-r4 | T39c | spec/cards-t39 : korean-division/tasks/T39d-river-bridge.md |
| T39e | March-fire muzzle (after T11) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39e-marchfire-muzzle.md |
| T39f | Korea·Fengtian color (after T11) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39f-kr-ft-colour.md |
| T39g | Gando dress (after T11) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39g-gando-dress.md |
| T39h | Infantry motion round 2 (after T11) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39h-infantry-motion-r2.md |
| T39i | House ruins (code complete, render waiting heavy_gate → sheet card wip-ready) | 0.2 | wip-ready | t39i-ruined-houses @66d6b95 | kd-develop/kd-t39i | main | spec/cards-t39 : korean-division/tasks/T39i-ruined-house.md |
| T39j | Prone-firing re-check (after T11) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39j-prone-recheck.md |
| T40 | Include trains in annihilation judgment (AliveCars, FV59 kept) | ? | wip-ready (log 13:5x, 2de288d) | kd/trains-in-annihilation @2de288d | kd-develop/kd-trains-annih | T30e | ? |
| T41 | Crash log | ? | wip-ready (log 14:0x, 69320b1) | kd/crash-log @69320b1 | kd-develop/kd-crash-log | main | ? |
| T42a | Company fatigue·morale account (schema v4) | 0.2 | wip-ready (log 14:3x) | t42a-fatigue-morale @ba621fe | kd-develop/kd-t42a | T38f | spec/cards-t42 : ? (T42a~d filenames need checking) |
| T42b | Promotion candidates·application (schema v5) | 0.2 | wip-ready (log 14:3x) | t42b-promotion @51b92c3 | kd-develop/kd-t42b | T42a | spec/cards-t42 : ? (T42a~d filenames need checking) |
| T42c | Prisoner ledger·repatriation/internment (schema v6) | 0.2 | wip-ready (log 14:4x) | t42c-prisoners @66e7637 | kd-develop/kd-t42c | T42b | spec/cards-t42 : ? (T42a~d filenames need checking) |
| T42d | Schedule·timeline (schema v7; prisoner double-counting fix 06ee9bd) | 0.2 | wip-ready (log 14:4x·15:0x) | t42d-schedule-log @06ee9bd | kd-develop/kd-t42d | T42c | spec/cards-t42 : ? (T42a~d filenames need checking) |
| T48a | Regiment commander data·schema v8·loader | 0.2 | queued | kd/t48a-… (not ordered) | - | T42d | spec/cards-commanders 122c65a : korean-division/tasks/T48a-*.md · design docs/COMMANDERS_DESIGN.md |
| T48b | Officer identity·terminus·assignment·appointment/dismissal | 0.2 | queued | kd/t48b-… (not ordered) | - | T48a | spec/cards-commanders 122c65a : korean-division/tasks/T48b-*.md · design docs/COMMANDERS_DESIGN.md |
| T48c | Regiment commander favor·dismissal decline | 0.2 | queued | kd/t48c-… (not ordered) | - | T48b | spec/cards-commanders 122c65a : korean-division/tasks/T48c-*.md · design docs/COMMANDERS_DESIGN.md |
| T48d | Management UI (tabs 8/9/12/14) | 0.2 | queued | kd/t48d-… (not ordered) | - | T48b·T48c | spec/cards-commanders 122c65a : korean-division/tasks/T48d-*.md · design docs/COMMANDERS_DESIGN.md |
| T48e | Stage 2 combat link (default off) | 0.2 | queued | kd/t48e-… (not ordered) | - | T48d·T10·T24 | spec/cards-commanders 122c65a : korean-division/tasks/T48e-*.md · design docs/COMMANDERS_DESIGN.md |
| T43 | Branch symbols howitzer·sniper added (+ cavalry symbol fix d45b18f) | 0.2 | wip-ready (log 14:1x; T43b commit d45b18f) | kd/t43-branch-symbols @d45b18f | kd-develop/kd-t43 | main | kd/t43-branch-symbols : ? (card filename needs checking) |
| T44 | All-mission operation smoke | 0.2 | running (ordered, dirty 3) | kd/t44-smoke @1c4036d | kd-develop/kd-t44 | main | spec/cards-t42 : ? (card filename needs checking) |
| T45 | Menu hiding (play button behind DevMenu·AI vs AI mission spectate tab) | 0.2 | wip-ready (log 14:0x) | kd/t45-menu-hide @88735b1 | kd-develop/kd-t45 | main | ? |
| T46 | = T38c (command card split, kd/t46-card-overflow) | 0.2 | wip-ready | kd/t46-card-overflow @54e8a54 | kd-develop/kd-t46 | wip/breach2 | = see row T38c |
| TC-A | Briefing scale separation + 3 information levels (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base t42d) | spec/cards-c1e1 : korean-division/tasks/TC-A-*.md |
| TC-B | SK2 alert state (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base merge/0.2) | spec/cards-c1e1 : korean-division/tasks/TC-B-*.md |
| TC-C | Event company order injection + lock D15 (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base merge/0.2) | spec/cards-c1e1 : korean-division/tasks/TC-C-*.md |
| TC-D | funds · foreign slots · control limit · available supplies (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | t42d | spec/cards-c1e1 : korean-division/tasks/TC-D-*.md |
| TC-E | favor.* · variable registry · decision runtime (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | TC-D | spec/cards-c1e1 : korean-division/tasks/TC-E-*.md |
| TC-F | Mission data variants · SK flags · BT1 departure · pow.bandit (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | TC-D · TC-E | spec/cards-c1e1 : korean-division/tasks/TC-F-*.md |
| TC-G | Japanese railway zone entry/fire ban (C1E1) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base merge/0.2) | spec/cards-c1e1 : korean-division/tasks/TC-G-*.md |
| C1E1-m1 | MAP-A map generation (medium) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none | spec/cards-c1e1 : korean-division/tasks/C1E1-m1-*.md |
| C1E1-m2 | MAP-B map generation (large) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none | spec/cards-c1e1 : korean-division/tasks/C1E1-m2-*.md |
| C1E1-u1 | Unit/weapon/faction check (report) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none | spec/cards-c1e1 : korean-division/tasks/C1E1-u1-*.md |
| C1E1-g1 | Episode graph runtime | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base t42d) | spec/cards-c1e1 : korean-division/tasks/C1E1-g1-*.md |
| C1E1-g2 | Episode screen UI | 0.2 | queued | kd/c1e1-… (not ordered) | - | g1 TC-D TC-E | spec/cards-c1e1 : korean-division/tasks/C1E1-g2-*.md |
| C1E1-g3 | Mission event action extension | 0.2 | queued | kd/c1e1-… (not ordered) | - | TC-E | spec/cards-c1e1 : korean-division/tasks/C1E1-g3-*.md |
| C1E1-g4 | Objective extension (surrender/N points/losses variable) | 0.2 | queued | kd/c1e1-… (not ordered) | - | none (base merge/0.2) | spec/cards-c1e1 : korean-division/tasks/C1E1-g4-*.md |
| C1E1-g5 | Refugee non-combat unit | 0.2 | queued | kd/c1e1-… (not ordered) | - | u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-g5-*.md |
| C1E1-g6 | unlock.* organization unlock | 0.2 | queued | kd/c1e1-… (not ordered) | - | TC-D | spec/cards-c1e1 : korean-division/tasks/C1E1-g6-*.md |
| C1E1-d1 | 16 decisions·node data JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | g1 TC-E | spec/cards-c1e1 : korean-division/tasks/C1E1-d1-*.md |
| C1E1-d2 | E1 starting state + economy constants | 0.2 | queued | kd/c1e1-… (not ordered) | - | TC-D | spec/cards-c1e1 : korean-division/tasks/C1E1-d2-*.md |
| C1E1-s1 | BT1 mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m1 u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-s1-*.md |
| C1E1-s2 | SK1 mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m1 u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-s2-*.md |
| C1E1-s3 | SK2 mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m1 u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-s3-*.md |
| C1E1-s4 | SK3 mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m2 u1 g5 | spec/cards-c1e1 : korean-division/tasks/C1E1-s4-*.md |
| C1E1-s5 | SK4 mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m2 u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-s5-*.md |
| C1E1-s6 | OP1 direct-assault mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | m2 u1 | spec/cards-c1e1 : korean-division/tasks/C1E1-s6-*.md |
| C1E1-s7 | OP1 encirclement mission JSON | 0.2 | queued | kd/c1e1-… (not ordered) | - | s6 | spec/cards-c1e1 : korean-division/tasks/C1E1-s7-*.md |
| C1E1-v1 | Variant data application | 0.2 | queued | kd/c1e1-… (not ordered) | - | s1~s7 TC-F TC-A | spec/cards-c1e1 : korean-division/tasks/C1E1-v1-*.md |
| C1E1-t1 | Functional text (narrative keys only) | 0.2 | queued | kd/c1e1-… (not ordered) | - | d1 s* | spec/cards-c1e1 : korean-division/tasks/C1E1-t1-*.md |
| C1E1-p1 | VIP portrait placeholder (optional) | 0.2 | queued | kd/c1e1-… (not ordered) | - | Q4 | spec/cards-c1e1 : korean-division/tasks/C1E1-p1-*.md |
| C1E1-q1 | End-to-end tests S1~S4 | 0.2 | queued | kd/c1e1-… (not ordered) | - | almost all | spec/cards-c1e1 : korean-division/tasks/C1E1-q1-*.md |
| C1E1-q2 | OP1 performance smoke | 0.2 | queued | kd/c1e1-… (not ordered) | - | s6 | spec/cards-c1e1 : korean-division/tasks/C1E1-q2-*.md |

## Stack chains (base → top)

- **gemini-import → t09-camp-slice → t19a → t19b → t19b2 → t19c → t19d → t38f → t42a → t42b → t42c → t42d** (campaign axis). T38f (t38f-home-readonly-tabs, b622d1d) sits on top of t19d and T42a~d on top of that. (log 13:5x~15:0x)
- **gemini-import → t29 (economy JSON) → t29b-on-t09** : T29b replaces T09·T29 on top of t09 (alone on merge).
- **breach2 → orderq2 → ai3b2 → {t24 (kd/t24-s1s3) → t24-crossing-fix, t25, t31a, t31c, t32a → t32d, t32b, t34a, t10 (kd/t10-comms-b) → t10b}**. Reference: breach2 → t16 (kd/t16-trench). breach2 → t46 (T38c, kd/t46-card-overflow).
- **rail → t28** (T28 cover-forest). **rail → river-r1 (T39a) → river-r2 (T39b) → river-r3 (T39c) → river-r4-bridge (T39d)**. Merge order rail → r1 → r2 → r3 (FV 53 rail / 54 r1).
- **rail → t30a → t30b → t30c → t30e → trains-annih (T40)**. T30d (art, 8e51e5a) is shown separately.
- **t27-cmd-ui2 → t38g-ui-harness → t38b-help-overlay → t38a-settings-keys (T38a)**; t38e is at the same tip as t38a. **t38g → t38h-fuzz-campaign (per commit message, needs checking)**.
- **t33a-map-model (c5b30f4) → t33b-map-ui (5207b8f, cherry-pick) → t33c-placement (c514821) → t33d-rail (same tip as c514821)**. Card branch kd/t33-map-editor-design (3b7c994).
- **kd-rerender (T11) → T39e~j** (no editing of make_unit/rerender96 before T11 finishes).
- **ai3b2 → T34c** (FV64 reservation candidate, before ordering).
- **measure/t12, measure/t15, measure/t31b, kd/t16b-rootcause, measure/t06b** : measurement·analysis-only branches (no code changes).
- **integration/rehearsal (984c404)** : integration rehearsal cards·records only (no code changes, worktree kd-integration).

## Memo

- 3 worktree checkout mismatches: `kd-develop/kd-t30b` has kd/immune-target-fix (T36), `kd-develop/kd-t24` has kd/acting-delay-tier (T37). There are no dedicated worktrees for T30b·T24·T36·T37 (existing worktree names and checkouts are misaligned). Cleanup method = ops/WORKTREE_CLEANUP.md.
- The commit hashes in ops/STATE.md (e.g. T10b 4ac07b4, T16 8b38b29, T24 e4fd65b, T36 etc.) were rewritten later and differ from the current tips. This table lists tips based on `git branch -v`.
- Items without a card file: T02 (exists in main), T14·T18·T20·T26 (no card file), T19b2·T23·T40·T41·T45 (card file `?`), T35·T36·T37·T43·T44 (path needs checking).
- Balance on hold (QUEUE): T12 · T15 · T16/T16b · T31a · T31b follow-up · T31c surrender values · T24 ②③ · T34d · T06 — marked hold-balance 0.3 / closed.
- 0.4 (AI improvements): turning on T31c, T32b, T32c, T34a~e, AI items after T10b.
- Cells needing checking: T04·T10·T25 (only commits, no wip-ready declaration), T17 (per CARD_INDEX), T33d (shared tip), T38h (no log record), T13 (release real-run record vs queued mismatch).
