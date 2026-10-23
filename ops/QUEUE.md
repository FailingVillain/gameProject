# QUEUE - approved work id order table (updated 2026-10-22)

Grade: **required = D (G/下/中/上, model) · H (hotspot 0~3)**. Optional memo = R (R1 local/R2 cross-module/R3 determinism·save·replay) · S (S<=60k/M<=150k/L>200k split) · W (light/heavy).
Status: queued / assigned / running / wip-ready / merged / closed / blocked(reason). Transition owner = ops/PIPELINE.md. Card = `korean-division/tasks/Tnn-<slug>.md`. Writers: conductor (add rows·assigned) · work agent (own card only) · merger (merged) · release (closed).
wip/* restore work (T01 T03 T04 T05 T07): the first role in the role column creates a new branch from main, restores·verifies the patch/branch, and ends with `wip-ready`. The merger comes after.

(2026-10-23 slimming: merged/closed/done rows moved to ops/QUEUE_DONE.md. The single table below = in-progress·waiting cards — merges the old top summary table + the 'measured status' table below (on conflict, the old 'measured status' table value is adopted as status). Per-row detail = ops/CARD_INDEX.md · worktree cleanup = ops/WORKTREE_CLEANUP.md · worktrees = under kd-develop/.)

| id | title | role | grade | prereq | status | branch @tip | worktree | base(stack) |
|---|---|---|---|---|---|---|---|---|
| T11 | Re-render all units (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | after merge queue ends, reserve 3 render slots | running (re-render ~60/81, remaining officer-russian-red 1 + deferred 10) | kd-rerender @467dafc | kd-rerender | main |
| T12 | Integrated balance measurement (A/B/C scenarios seed 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 (T06 re-measured after D19) | wip-ready (measurement, held 0.3) | measure/t12 @e6122ac | kd-meas-t12 | ai3b2 stack |
| T13 | release (full selftest · golden · publish · record) | release | D中 R2 SM H1 Wheavy | T12 | queued | - | - | T12 |
| T15 | Night soak refresh → re-report 9 combat problems | measure | D中 R1 S H0 Wheavy | T12 | wip-ready (measurement, held 0.3) | measure/t15 @a70a308 | kd-meas-t15 | ai3b2 stack |
| T20 | Finish repo move (delete old folders = user U02) | docs | D下 R1 S H0 Wlight | U02 | blocked (U02) · (old table 1: blocked(U02)) | - | - | - |
| T48b | Officer identity persistence · promotion endpoint · assignment · regiment commander appoint/dismiss · KIA/always-wounded | sim | D上 R3 M H2 Wlight | T48a | merged (G1 t48b1 + G2 t48b2) | t48b1-officer-identity @3fcdacf9 · t48b2-assign @80c11fba | - | - |
| T48e | Stage-2 battle link (4 default-off keys, FV reservation needed) | sim | D上 R3 M H3 Wheavy | T48d · T10 · T24 | merged | kd/t48e-battle-link @5303bf99 | - | - |
| C1E1-m2 | MAP-B map generation (large) | feature | D中 H0 | none | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-u1 | Unit/weapon/faction audit (report) | scout | D下 H0 | none | wip-ready (kd/c1e1-u1-unit-audit b13932d) | - | - | - |
| C1E1-g2 | Episode screen UI | feature | D中 H2 | g1 TC-D TC-E | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-g3 | Mission event action expansion | sim | D上 H3 | TC-E | merged (plan docs/C1E1_IMPLEMENTATION_PLAN.md) | kd/c1e1-g3-events @4f059987 | - | - |
| C1E1-d1 | 16 decisions · node data JSON | feature | D中 H1 | g1 TC-E | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| M2 | Unify campaign morale·favor·public sentiment to 0~100 default 50 + post-battle desertion + gun unit price 7 (DECISIONS BH2~BH5) | feature | D中 R2 S H2 Wlight | TC-E (base kd/tce-vars) | merged (kd/m2-morale-camp @1ce9aff1, kd/gun-cost-wire @f0a187ee/ad929bb9; verified 2026-10-23 by M2 agent, code read only) | - | - | - |
| C1E1-s6 | OP1 assault mission JSON | feature | D中 H1 | m2 u1 | wip-ready (kd/c1e1-s6-op1-assault 3f704c6; review fixes in progress) | - | - | - |
| C1E1-v1 | Apply variant data | feature | D中 H1 | s1~s7 TC-F TC-A | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-t1 | Feature text (narrative keys only) | docs | D下 H1 | d1 s* | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-p1 | VIP portrait placeholder (optional) | art | D下~中 H0 | Q4 | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-q1 | End-to-end tests S1~S4 | measure | D中 H1 | nearly all | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-q2 | OP1 performance smoke | measure | D中 H0 | s6 | queued (spec/cards-c1e1, plan docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| NEW-1 | Overload test by map size (N13; measure) | measure | D中 | not stated | done (measured; ops/PERF_MAPSIZE_2026-10-23.md) | - | - | - |
| NEW-2 | Investigate LOD0 missing-drawing bug | not stated | D中 | not stated | done (not reproducible on 0.2-final 33ee9be3; USER_DECISIONS_QUEUE 16) | kd/new2-lod0 @33ee9be3 | - | - |
| NEW-3 | Suppression-prone soldiers dragged along by formation movement | sim | D上 | not stated (golden may change) | queued (together with 0.3 early golden regeneration) | - | - | - |
| NEW-4 | Dithering in narrow chokepoints | sim | D上 | together with wip/chokefix (D14 held) | queued (0.3 early) | - | - | - |
| NEW-5 | Japanese army Type-45 cap · standard-bearer flag 3:2 · infantry overlap batch fix (collide) | not stated | not stated | prereq: mid re-render | queued (together with the 0.3 unit re-render (mid)) | - | - | - |
| T32c | - | - | - | - | queued | - | - | T32b |
| T32d | - | - | - | - | merged (muzzle fix; B13b) | kd/t32d-pioneer-art @af357ce3 | kd-t32d | T32a |
| T33c | - | - | - | - | running | kd/t33c-placement @c514821 | kd-t33c-placement | T33b |
| T33d | - | - | - | - | merged (F5-t33d fdf0a25) | kd/t33d-rail @022647f9 | kd-t33d-rail | T33c |
| T34b | - | - | - | - | queued | - | - | T34a |
| T34c | - | - | - | - | queued | - | - | ai3b2 |
| T34d | - | - | - | - | queued (held 0.3) | - | - | T34a |
| T34e | - | - | - | - | queued | - | - | T34a |
| T38c (=T46) | - | - | - | - | wip-ready | kd/t46-card-overflow @54e8a54 | kd-t46 | wip/breach2 |
| T38h | - | - | - | - | wip-ready (f126551, no uncommitted) | t38h-fuzz-campaign @e5f76ec | kd-t38h | T38g |
| T39e/f/g/h/j | - | - | - | - | queued (blocked T11) | - | - | T11 |
| T44 | - | - | - | - | in progress (kd/t44-smoke f9dea2d; smoke_missions.ps1 harness fix done, needs run after gate improvement) | kd/t44-smoke @1c4036d | kd-t44 (3 uncommitted) | main |

**Policy (current, the old 'no merge' lifted by AT·AX on 2026-10-22 — old wording discarded): WIP is merged only via the merge line (merge/0.2-final, merger always-on). Work agents stop at wip-ready and do not merge. No direct merge to main, main gets a final `--ff-only` (after user report·confirmation), push only on user command.** Do not newly write 'no merge' in cards·QUEUE rows (to block a specific branch, write 'superseded/excluded: reason' in MERGE_PLAN §2).

**Execution order (task list)**: stage 1 T03 · T07 · T01 (in progress) + T18 · T11 · T21 · T22 · T23 → stage 2 T04 (on T03) → T05 (on T04) → T08 · T09 (on T01) → T17 (on T08) → T19 (on T09) → T12 · T15 · T16 · T13 (WIP measurement on the stacked branch) | blocked T06 (D19) T10 (D15) T20 (U02) | choke (D14) held.

Items with cards = T01~T13 (+_TEMPLATE). For T15~T20 and branches without cards (wip/chokefix D14 held · wip/sfxnew sound effects · wip/gt-face-proto), spec/conductor writes the card just before ordering.
Held (outside this table): armored train · river · tactical AI 3C/3D · map editor · campaign order linking/chapter 1/ending.


## Balance hold (user 2026-10-22: all balance-related work held, lowest priority — proceed only on user decision)
Held: T12 · T15 · T16/T16b follow-up tuning · T31a (reinforcement limit tuning) · T31b/T31b-endline follow-up baseline re-measurement · T31c surrender values · T24 ②③ follow-up tuning · T34d cavalry flank strength tuning (including S4 recheck) · T06 (closure) · AH1 train balance · AG1 surrender enable · U2 shortfall remedy · H1 field gun range · all n>=24 win-rate measurement cards. Allowed: feature implementation, determinism·save·input validation, n=8 behavior checks.
Priority: ① re-render (T11) ② feature implementation (stack: combat engineers·AI smoke/barbed wire·map editor·UI backlog·train·IsAi·campaign follow-up) ③ integration rehearsal/merge prep ④ (held) balance.

Version milestones (user 2026-10-22): 0.2 = before balance patch (features complete·merged) → 0.3 = after balance patch (all at once) → 0.4 = AI improvements. Details = ops/VERSIONS.md. Remaining AI cards (T32c, T34b~e) deferred to 0.4.

## Unmerged measurement 2026-10-23

Criterion: `git merge-base --is-ancestor <branch> HEAD` (kd-develop/kd-final, merge/0.2-final @0384fd9f). Unmerged commit count = `git rev-list --count HEAD..<branch>`.

Actually unmerged (row · branch @tip · unmerged commit count):
- T12 · measure/t12 @e6122ac · 1
- T15 · measure/t15 @a70a308 · 1
- T38c (=T46) · kd/t46-card-overflow @54e8a54 · 1
- T38h · t38h-fuzz-campaign @f1265517 · 2 (e5f76ec in the measured-status table is stale; measured tip is f1265517)
- C1E1-s6 · kd/c1e1-s6-op1-assault @8c867869 · 1
- C1E1-u1 · kd/c1e1-u1-unit-audit @b13932d0 · 1
- C1E1-g3 · kd/g3-win-and-delay @65ba51e7 · 1 (status untouched per instruction, stays assigned)

No branch (list only, status kept):
- M2 · kd/m2-morale-camp (assigned) — no ref

No branch ref but tip commit is an ancestor, so treated as merged:
- C1E1-d2 · kd/c1e1-d2-econ (tip 84880d0 is an ancestor of merge/0.2-final)

Running outside status (not counted): T11 kd-rerender @467dafc · T32d kd/t32d-pioneer-art @2118f9b · T33c kd/t33c-placement @c514821 · T33d kd/t33d-rail @c514821 (shared commit) · T44 kd/t44-smoke @1c4036d

No branch (queued, status kept): T13 · T32c · T34b~e · T39e/f/g/h/j (blocked T11) · T48b · T48e · C1E1-m2 · C1E1-g2 · C1E1-d1 · C1E1-t1 · C1E1-p1 · C1E1-q1 · C1E1-q2 · C1E1-v1 · NEW-1~5 (new rows, branch not stated)
