# PIPELINE - work flow structure
Version: v2 · updated: 2026-10-22 · source of truth: this file is the source of truth for the work flow structure (state·queue·decisions are sourced from STATE/QUEUE/DECISIONS).

**Standard flow (v2)**: card → implementation (wip-ready) → **review** (reviewer, mandatory) → fix cycle → measurement (cards with goals) → integration rehearsal → merge (after user permission).

## 1. Flow by stage
| Stage | Owner | Input → output | Limit (LIMITS) | Bottleneck → response |
|---|---|---|---|---|
| Input | user | instruction → DECISIONS answer / QUEUE row | - | 25 decisions piled up → low-risk ones proceed with defaults (D22) |
| Work definition | spec (or conductor) | QUEUE row → tasks/Tnn card | card ≈1.5KB · L cards split in advance | duplicate investigation → MERGE_QUEUE completion table + mandatory GIT_HISTORY_DIGEST grep |
| Ordering | conductor | card → agent (role card + 3-line card prompt) | current limits see ops/LIMITS.md · wave ≤12 | conductor notification sink → replace session every wave · stale QUEUE rows → cross-check MERGE_QUEUE·main before ordering (T18) |
| Coding | sim/feature/art/docs | card → branch commits | context 200k | hotspot conflicts → serialize H≥2 same file · role misassignment → code cards forbidden for docs (T26) |
| Local verification | same agent | build·detcheck·own suite·snaptest → card result | heavy jobs = see LIMITS.md | gate wait 25 min+ → full selftest only by release |
| wip-ready | same agent | card status wip-ready → order review → MERGE_QUEUE row (conductor) | no lock | lock polling → work agents do not take the lock |
| Review | reviewer (read-only, mandatory) | wip-ready branch diff → list of blocker/high/medium defects | 1 review per branch | missed review → no ordering the next stack without review |
| Fix cycle | author or fix agent | review findings → 1 commit on the same branch → re-verify | 1 cycle = 1 commit | fix to lower stack → SendMessage the upper branch to cherry-pick |
| Measurement | measure | main commit → BASELINE_CURRENT block | non-art heavy = see LIMITS.md | duplicate individual measurements → one integrated measurement (T12) · goal missed → no wip-ready, report honestly (T06·T16) |
| Integration rehearsal | conductor (proposal, user confirms) | merge in merge order into integration/rehearsal → selftest·golden → record in ops/REPORT | 1 lock | conflict·number overlap → renumber via FV_RESERVATIONS then retest |
| Merge | merger (always-on) | rehearsal passed → main, completion table row, card merged | 1 lock, expires 30 min | re-verify on every main move → merge to main once, re-merge only on conflict |
| release record | release | queue end → selftest·golden·publish·record in 5 places, card closed | once per queue | first real run after fix/n12-forest merges (§8) |
| Decision ledger | conductor | DECISIONS_PENDING line → DECISIONS D number | - | asking many times → one ledger, user answers at once |
| Conductor check | conductor | harness /api/state → STATE.md | 20 min (auxiliary when no events) | session 360k → role limits + replacement |

### 1-a Ordering rules (v2)
- Stacked orders: write the base commit hash · the prerequisite card's review-blocker status in the prompt. Do not fix FV/Replay numbers at order time (§Ordering·merge procedure v2.1 (c)). Order prerequisite fix commits to be cherry-picked via SendMessage.
- L cards are split in advance (T19 succeeded). For cards without detailed design, write 'default-off switch + draft numbers' in the acceptance criteria, and name the key and target in decision options (T10·T06).

### 1-b Stall detection (v2)
- Signal: size·mtime of the task output file. 0 bytes or long no change = start failure → reorder with a new agent. Do not judge by unchanged worktree alone.
- If output keeps growing it may be waiting (heavy_gate, measurement). Before judging a stall, check heavy_gate.ps1 pool occupancy (gate v2: holder PID/name in `ops/gate_wait.log`, `heavy_gate.ps1 -DryRun`). Heavy jobs (render·measurement) are decoupled from agent lifetime.
- Before reordering confirm the original agent is alive (T16: completed normally after 5.5 hours, 66k duplicate).

### 1-c Review checklist — fixed items (the reviewer explicitly answers these 3 in every review; 2026-10-22)
1. **FV bump** (LESSONS_ARCHIVE 21): did you add·change a save profile (ObjCodec target) or Snapshot/Replay field? Even if default-off, did you raise `FormatVersion` and record it in FV_RESERVATIONS? (measurement-only statistics excluded, LESSONS_ARCHIVE 14)
2. **Save fields**: did the new state enter the checksum · `SnapshotSelfTest.Audit` · save/load · entity reference tags? (precedents: T37 actingCache · T40 AliveCars omitted)
3. **snapshot classification** (LESSONS_ARCHIVE 23·28): did you classify the new field so snapshot and `--snaptest` handle it (saved / derived cache / presentation-only)? Write one line of classification rationale in the review report.

### 1-d Common tail of order prompts (2026-10-22 v2.1; append to the end of every order. Do not write limit numbers — a cause of contradiction, numbers only in ops/LIMITS.md)
Required 3 fields + one line: ① base branch·commit hash ② FV: if raising, claim `fv_claim` (temporary value base+1) / if not, 'none' ③ hotspot file list ④ write the 1-line result of `preflight.ps1 <card id>` in the report.
- Common prohibitions: no push · no modifying main·merge lines (merge/*) · no modifying ops/ on work branches (`queue_guard`) · shared docs via fragment files (`docs/decisions_pending.d/`, `docs/engine_notes.d/`).
- Report: within the role card's line count (the conductor's value if set). At the end an **'임의 결정' item** (list every value·assumption·interpretation·deletion·relaxation the card did not decide; 'none' if none).
- Non-art heavy (build·selftest·measurement) runs sequentially inside the agent (through heavy_gate). Web lookups via `hub ask -w`.
- One line per role (moved from old LESSONS): feature·sim = null·NaN·range validation in JSON Load/Validate + grep consumers when changing a flag, distinguish stale tests from wrong code (no weakening) · sim·reviewer = adding ObjCodec/save fields is FV+1 even if default-off, measurement-only statistics leave FV unchanged · feature = new button gets a selftest, new mission gets a suite iterating `MissionDef.Available()` (N77·N12·Props·Terrain·missions) · measure/feature = n=8 is behavior check only (no win-rate judgment), heap after forced GC · reviewer = no checkout · art = check cell boundaries of poses with `edge_check` · merger = use only `merge_step.ps1` (snapshot + `--snaptest` enforced) · common = no ops/ edits on own branch.

- Event·campaign card one line (user approval 2026-10-22, TC-C proof): when tying an event trigger to a player choice (popup response, etc.), put that choice in as a `GameCommand` (recorded command) — the event condition is sim state, and every input that changes state must be a recorded command so it re-fires on the same tick in replay. Commands executed directly by the event (injection) are not put in the command log (double execution). The evtcmd test is the evidence.

## 2. Document write ownership
| File | Writer | Others |
|---|---|---|
| ops/STATE.md | conductor (checks·handoff), release (one line) | read only |
| ops/QUEUE.md | conductor (rows·assigned), merger (merged), release (closed) | work agents forbidden |
| ops/MERGE_QUEUE.md | merger (status·completion table·numbers), conductor (wip-ready rows) | read only |
| ops/DECISIONS.md | conductor (cleanup), user (answers) | agents only one DECISIONS_PENDING line |
| ops/FV_RESERVATIONS.md | conductor (order after reserving numbers) | work agents forbidden |
| ops/LIMITS.md · PIPELINE.md | conductor, only on user instruction | - |
| tasks/Tnn | spec/conductor (body), assigned agent (result·status only) | - |
| BASELINE_CURRENT · SELFTEST_HISTORY · PROJECT_NOTES latest line | measure (block) · release | work agents forbidden |
| watch/LEAKS.md | conductor (when there are numbers) | - |
| role cards (~/.claude/agents) · L0 charter | docs after user approval | - |

## 3. Status values and transition owners
queued (conductor/spec) → assigned (conductor, at order time) → running (agent starts) → wip-ready (agent finishes) → merged (merger) → closed (release). blocked (reason) is attached and removed by the conductor. Rollback (merger conflict → running): the merger writes the reason on the card.

## 4. Conductor session resume 5 steps
1. `ops/STATE.md`: main hash · agents · lock · worktrees · "next step". 2. `ops/QUEUE.md`: check running/wip-ready rows, pick queued ones whose prerequisites are resolved. 3. `ops/DECISIONS.md`: pending-type IDs and new answers. 4. Confirm roles·ownership with §1~3 of this file. 5. Check the real agent count via harness `/api/state` (§1-b if a stall is suspected) then order; update STATE. (Other docs only when a card points to them.)

## Tools (tools/agent, PowerShell 5.1; added 2026-10-22)
| Tool | When | Use |
|---|---|---|
| `preflight.ps1 <카드id|브랜치>` | **before ordering** conductor | (includes card_id collision=BLOCK · title-keyword roadmap_check summary=WARN) cross-checks QUEUE status · MERGE_QUEUE (already merged/waiting) · worktree/branch duplicates · uncommitted changes in that worktree · FV reservation table. Duplicate·already merged·uncommitted → exit code 1 + reason (BLOCK), FV overlap is WARN. e.g. `preflight.ps1 T38j` (LESSONS 1·24·30) |
| `merge_step.ps1 -Step E1 -Expected "<a> <b>" -AllowFail uiicons,traindemo [-Groups g1,g2] [-Priority]` | at the end of **each merge step**, merger/merge agent | gate+build → detcheck (STOP immediately on mismatch) → `snapshot` suite → `--snaptest` → own suite → STOP on non-allowed FAIL → tag (`post-<Step>`) → append one line to `ops/MERGE_PROGRESS.md` → one final `PASS`/`STOP` line. No option to skip steps (LESSONS_ARCHIVE 23·28). `-DryRun` (+`-Feed <dir>`) prints only the plan, tests the STOP logic with mock input |
| `state_snapshot.ps1 [-Write]` | handoff·session start·checks | main hash · worktree count · merge lock (expiry shown) · HEAD/latest tag/uncommitted count per merge line · FV/Replay · running heavy process count as markdown. `-Write` = overwrite `ops/STATE_LIVE.md` of this checkout |
| `card_id.ps1 -Prefix T -Count 3 [-Series] -Title "..."` / `-Check <id>` / `-Id T58x -Title ...` | **before assigning a card id** conductor·spec agent | gathers used ids from tasks/ of all branches·worktrees + QUEUE · CARD_INDEX + `ops/CARD_REGISTRY.md`, reserves the next free number per prefix (append one line to the ledger, safe for concurrent calls via file lock) and prints the id. Rejects already-used ids (exit 2). `-Series` = a,b,c on one number (T58a~c), without it T58 T59 T60. `-Check` exit 0=free · 3=in use (1 owner) · 1=collision (different card files with the same id). Do not invent numbers yourself (T51a~e·T49a~c collision accident) |
| `roadmap_check.ps1 -Keywords "날씨","weather" [-Card <id>]` | **before adding an item to a roadmap·card·design doc** | finds keywords in ROADMAP_SHORT/LONG · legacy docs · ops docs · docs/specs·tasks of all branches · merge-line code (src, assets/data) and displays <=40 lines by source. On hits exit 1 + conclusion 'implementation may exist / doc plan only / no hits'. Prevents re-planning existing features (weather WeatherFx, T38e·T38h·T49b/c) |
| `fv_claim.ps1 -Fv 1 [-Replay 1] [-Schema 0] -Reason "..."` / `-Show` | **branches that raise save format numbers** (work agents) | do not finalize numbers yourself. Leave one line `날짜 \| FV+1 \| Replay+1 \| Schema+0 \| base <해시> \| 사유` in `ops/fv_claims/<branch, / → __>.txt` (1 file per branch, append-only → 0 conflicts) and include it in the commit. Temporary number in code = **base FV (Replay) + claim total**. Do not write numbers in `ops/FV_RESERVATIONS.md` |
| `fv_assign.ps1 -Report` / `-Order a,b,c [-BaseRef HEAD]` / `-Branch X -Apply [-BaseRef HEAD] [-NoBuild] [-RegenGolden]` | **merge line** (merger), just before/after each branch merge | `-Report` = scan table of claim files of all branches (replaces the manual reservation table). `-Order` = final FV/Replay plan in merge order (base = constants at BaseRef; branches without claims unchanged). `-Apply` = call during `git merge --no-commit X` (HEAD = pre-merge tip): after folding the number-line conflicts in Snapshot.cs/Replay.cs with `git checkout --ours -- <two files>; git add`, run it and it does `fv_renumber -Set` with base + that branch's claim. If the merge commit was already made, `-BaseRef HEAD^1`. Without a claim file, base value as is. Merges that change the golden use `-RegenGolden` (keep the last-once rule). Schema claims are display-only (campaign schema is manual) |
| `assemble_notes.ps1 [-Target decisions\|notes\|all] [-Write]` | **merge line·release** (work agents do not run) | merges the `docs/decisions_pending.d/*.md` · `docs/engine_notes.d/*.md` fragments in date order (fragment first line `date: 2026-10-22 18:30`, file mtime if absent) and prints. `-Write` = replaces only the **generated block at the end** of DECISIONS_PENDING.md / ENGINE_NOTES.md (between `assembled-notes` comments); the original above is unchanged, no duplicates on re-run |
| `queue_guard.ps1 [-Branch ref] [-Base main]` | work agent before commit · preflight · merge_step | checks whether the branch (unspecified=HEAD+uncommitted) changed `ops/QUEUE.md`·`STATE.md`·`MERGE_QUEUE.md`: if so prints file names + exit 1. Integration: `preflight.ps1` (WARN if a same-name branch exists), `merge_step.ps1 -GuardBranch <branch to merge> [-GuardStrict]` (step 0; warns by default, STOP if Strict). No git pre-commit hook is installed |
### Parallel processing rules (supervisor PIPELINE_AUDIT_2026-10-22 C-1·C-2·C-7, top-priority user instruction)
- **FV late binding**: numbers are not fixed at order time. A branch that raises them claims via `fv_claim.ps1` and writes only a temporary number (base+1) in code. The merge line finalizes in order via `fv_assign.ps1`. `ops/FV_RESERVATIONS.md` stops manual updates (file not deleted, stale values not corrected) and the output of `fv_assign.ps1 -Report` is the current reservation table.
- **Shared doc fragment rule**: work agents do not add lines to `docs/DECISIONS_PENDING.md` · `docs/ENGINE_NOTES.md` but write to `docs/decisions_pending.d/<branch, / → __>.md` · `docs/engine_notes.d/<branch>.md` (1 file per branch, 0 conflicts). The merge line/release merges them with `assemble_notes.ps1 -Write`. Editing existing lines is conductor only.
- **Work branches do not modify ops/QUEUE·STATE·MERGE_QUEUE** (table in §2 above) — check with `queue_guard.ps1`.
- **Wave 2 base is `merge/0.2-final` alone**. Branches already standing on merge/0.2 or the tce chain **merge in final once and stop (no restacking)**; afterwards depth ≤3 relative to final (check before ordering with stack_depth.ps1). Execution of this rule on the merge line is by separate instruction, and the tool·doc card (tools/pipe-parallel) merges no branch.
- Test: `korean-division/tools/agent/pipe_parallel_test.ps1` (reproduces the TC-B/TC-C/M1/g3 temporary-67 situation in a temp repo, including sorting to 68~71 / Replay 24,24,25,26).


| `watchdog.ps1 [<워크트리>...] [-StallMin 30] [-RecentHours 6]` | when an agent looks stuck · periodic check conductor | per worktree `OK` / `WAITING_GATE` / `SUSPECT_STALL` (30 min+ no change) + evidence (recent commits·changed files·runner dotnet/KoreanDivision/make_unit/blender CPU change·scratchpad files with the worktree name in the path·heavy_gate wait log `ops/gate_wait.log`). With no args, worktrees active in the last 6 hours. **Does not kill processes (report only)**; exit code 2 if suspicious (LESSONS 8·18·25). `-Fake "commit=45;file=50;scratch=none;runner=0;cpu=0;gate=wait"` mocks the verdict only |
| `stack_depth.ps1 [<브랜치>...] [-Line merge/0.2] [-Max 3]` | **before ordering** (stack cards) · merge queue check | computes the chain depth of prerequisite branches stacked under each branch 'not yet in the merge line', `WARN` (exit code 3) if over 3 levels + list of prerequisites not in the line (lower first). With no args, wip-ready branches of QUEUE.md |
Verification: `watchdog_test.ps1` (3 mock verdicts + temp-repo stack test, all must PASS).
Finish a merge step with this one tool and do not spell out commands directly. Detailed procedure source of truth = docs/procedures/merge-procedure.md.

### Procedure before adding to roadmap/cards (mandatory, user 2026-10-22)
(i) **Duplicate check**: run `roadmap_check.ps1 -Keywords <한글>,<영문 식별자 후보>`. Actually read the hits (including code), and record one line in the card header `중복 점검: <키워드> → <결론 한 줄: 구현 있음/문서 계획과 합침/히트 없음> (roadmap_check 날짜)`. If an implementation exists, do not create the card and narrow the scope to an extension on top of the existing implementation.
(ii) **id reservation**: get card·roadmap ids only via `card_id.ps1` (`-Count`/`-Series`). Use the printed id as is, and call it individually even when writing several design docs in parallel (the lock prevents overlap). Ledger = ops/CARD_REGISTRY.md (append-only, no direct edits).
(iii) **Prompt template**: put the two lines below verbatim into design-doc·spec agent orders.
> 1. Before writing a card/roadmap item, run `tools/agent/roadmap_check.ps1 -Keywords ...`, read the result, and write a '중복 점검' line in each card header (if the feature is already implemented, report instead of a card).
> 2. Reserve card ids only with `tools/agent/card_id.ps1 -Prefix T -Count N -Title "..."` (use `-Series` for a~c on one number). No arbitrary numbers.
Before ordering, `preflight.ps1` checks id collisions (BLOCK) and the keyword summary (WARN) once more. Test: `tools/agent/pipe_ids_test.ps1`.

## Ordering·merge procedure v2.1 (reflects supervisor PIPELINE_AUDIT_2026-10-22 sections C·D, user approval 2026-10-22)
(a) **3 required before ordering**: `preflight.ps1 <card id>` + `card_id.ps1` (card id reservation) + `roadmap_check.ps1` (roadmap·legacy·code duplicate check). Attach the 1-line result to the order. Until tools land in main, use the copy path after the merge round.
(b) **base rule**: after wave 2 every branch base = `merge/0.2-final` (or the integration branch at that time) **alone**. Branches already on merge/0.2 · tce chain merge in once, no restacking. Stack depth ≤3 (check before ordering with `stack_depth.ps1`).
(c) **FV/Replay late binding**: work branches use temporary value (base+1) + `fv_claim`. The merge line (merger) finalizes in merge order with `fv_assign`. Manual updates of `FV_RESERVATIONS.md` stopped.
(d) **Shared doc fragments**: `docs/decisions_pending.d/`, `docs/engine_notes.d/` fragment files (merged by `assemble_notes.ps1`). Work agents must not modify QUEUE·STATE·MERGE_QUEUE (`queue_guard`).
(e) **At the start of a merge round** re-check each branch tip hash against the MERGE_QUEUE row (prevents branches that moved ahead after merge: precedent of m1·m2·u2 modified after merge). If they diverge, hold that branch and report to the conductor.
(f) **Handling agent completion reports**: the conductor immediately appends the report's 'arbitrary/assumption/interpretation/deletion/relaxation' items as one line to `ops/AUTO_DEFAULTS_LOG.md` (standing user rule).
(g) **Naming rule**: reports·conversation use 'Korean name (ID)'. One line of Korean name per card (approved; dictionary = `ops/CARD_NAMES.md`, being written by a separate agent — not yet present).
(h) **Conductor report length**: 3~4 key lines per completion notice (token leak B-2). PIPELINE_LOG entries ≤200 chars per line (time·card·hash·1 number), details in the card file.
(i) **Checklist before starting wave 2** (execution order; mark `[x]` in the done box):
- [ ] 1. Land the tool bundles (pipe-tools · gate-v2 · c15-watchdog · selftest-parallel · fv-renumber · pipeline-v2) in main in a merge round (docs·tools only, `git diff main --stat -- korean-division/src` must be empty). The 4 tool test scripts pass.
- [ ] 2. Clean up limit sources: replace numbers in STATE.md · harness cap · order tail with 'see LIMITS' (the LIMITS values themselves are the user's).
- [ ] 3. Fix wave 2 base = `merge/0.2-final`. The existing 9 (tcb·tcc·tcg·g4·m1·m3·g3·tcf·m2) merge in final once.
- [ ] 4. FV alignment: finalize the 4 increases (TC-B·TC-C·M1·g3) in merge order via `fv_assign -Report` (68~71 / Replay 25~26), TC-G has no increase.
- [ ] 5. QUEUE consistency: update merged rows (T04·T10·C1E1-m1/m2…), move closed/merged rows to the archive file.
- [ ] 6. LESSONS 12 lines finalized (done) · the conductor applies the role card patch proposal `ops/ROLE_CARD_PATCHES_2026-10-22.md`.
- [ ] 7. Order template = §1-d (3 required fields + 1 preflight line). 8. Apply fragment directories·fv_claims from new branches (no retroactive change).
- [ ] 9. Start with a new conductor session, idle checks read only the `state_snapshot.ps1` output. 10. Keep effective concurrency 20 → only after confirming non-art wait median <10 min and free RAM ≥6GB via `ops/gate_wait.log`, move to the LIMITS value (user decision).

## Sub-conductors (wave 2 · user instruction 2026-10-22)
- 3 areas: **art** (in operation, state file `ops/ART_STATE.md`) · **campaign** (appointed in wave 2 when needed; state file `ops/CAMPAIGN_STATE.md`, stated in the roadmap) · **merge** (merger always-on, state file `MERGE_PROGRESS*.md`).
- Rules:
  1. Order only within your own pool limit: art render lane ≤8 + keep a build slot free. Limit numbers see `ops/LIMITS.md`.
  2. `ops/QUEUE.md` · `STATE.md` · `MERGE_QUEUE.md` are modified only by the top-level conductor. Sub-conductors modify only their own state file.
  3. Appoint a new agent every wave (do not keep alive long). Handoff is one state file.
  4. Final report ≤15 lines, including a table of agents ordered.
  5. Before ordering the same `preflight` · `card_id` · `roadmap_check` apply (§Ordering·merge procedure v2.1 (a)).
  6. List arbitrary decisions on the last line of the report `임의 결정:` → the top-level conductor records them in `ops/AUTO_DEFAULTS_LOG.md`.
  7. H3 sim cards are managed serially by the top level without a sub-conductor.
- harness: shown in the agent list with the label `서브 지휘자(영역)` (implementation by a separate agent; this section is docs only).
- Undecided (not applied): making the supervisor always-on · concurrency cap 6 · shrinking management apparatus — see the 'Undecided' section.

## Undecided (user's part — not applied)
- Concurrent agent cap (supervisor C-6: keep effective 20 vs 30; check the difference from memory rule 6/opus 2).
- Shrinking management apparatus (3 sub-conductor areas · whether supervisor is always-on · 60-minute cycle).
- Conductor session replacement rule (once a day vs replace after STATE handoff when context reaches 150k, supervisor B-1).
- Supervisor always-on (always running·whether to keep cost ≤60k/cycle).
- (Reference) the proposal in C:/works/notes/agent-sprawl-concern.md is not applied.

## 5. Version history
| Version | Date | Changes | Evidence numbers | Score |
|---|---|---|---|---|
| v1 | 2026-10-22 | 4-layer docs + ops + LEAKS, 10 roles, merger always-on·lock forbidden·expiry 30 min, release split, decision ledger defaults | agents 45 · merges 10/13 (04:08~04:40) · lock waits 2h+ · conductor 360k · duplicate investigation 250k tokens | Fable independent ≈45 / self 54 → expected after improvement 60~65 (Fable) vs 75 (self) |
| v2 | 2026-10-22 | review stage mandatory · fix cycle · stack rules (base hash·FV reservation table·cherry-pick) · stall detection (output file) · L pre-split · default-off switch · integration rehearsal · release first-run conditions | orders ~70 · wip-ready ~38 · reviews 18 (all caught defects) · fix cycles 11 · acceptance-miss reports 4 · errors 3 (T18 duplicate · T26 role misassignment · T16 duplicate order) | self 72/100 |
| v2.1 | 2026-10-22 | supervisor reflected: 3 required before ordering · single base · FV late binding · fragment docs · tip re-check · arbitrary-decision reports · naming rule · wave 2 checklist (§Ordering·merge procedure v2.1) | supervisor report C-1~C-7·D | - |

## 6. Check questions and cycle (user interim check; number sources)
| Question | Where to pull from |
|---|---|
| minutes per merge | MERGE_QUEUE completion table times / merger report |
| total gate wait minutes | heavy_gate log · agent reports → LEAKS weekly table |
| tokens at agent start · context distribution | harness /api/state (copy to LEAKS weekly table) |
| duplicate investigation count | scout reports "already implemented" count → LEAKS L5 |
| pending decision count | number of DECISIONS 'pending' rows (harness also counts) |
Cycle: once at the end of each wave, one line in the LEAKS weekly table once a week.

## 7. Promotion rule
Rules·tools·roles are added only when LEAKS has numbers (count·tokens·minutes), and then this file's version is raised. Proposals without numbers are only DECISIONS candidates.

## 8. Held·unresolved
- Of the 5 grade axes, R·S·W are optional memos (only D·H required). Not raised to required again.
- Gate 3 lanes: code exists, default disabled. Test if total gate wait exceeds 60 minutes per week.
- harness: stays a read-only status board (stage 3 expansion held).
- Wave size test (≤12 · 上 ≤3 · heavy ≤6) is Fable's advice — limit values are per ops/LIMITS.md (user instruction takes precedence), test with 12 once in the next wave and record the numbers.
- Conductor session replacement cycle (every wave) is finalized after testing.
- release first real run: done 2026-10-22 (selftest 2008 OK).
- Integration rehearsal owner·procedure: confirm with the user before the first merge.

## Policy addition (2026-10-22, user instruction)
[Discarded — lifted by AT·AX on 2026-10-22] old 'no merge' policy. Current: work agents stop at `wip-ready`, merge only by the merger on the merge line (merge/0.2-final) (always ordered), main gets a final `--ff-only` after user report·confirmation. Push only on user command. Do not write 'no merge' in new docs·cards·prompts (branch exclusion via MERGE_PLAN §2 'superseded').
(v2) Merge to main only after user permission and after passing integration rehearsal (integration/rehearsal).

## Auto-defaults log (user 2026-10-22)
When auto-applying recommended defaults, only **append one line** (`echo >>`) to `ops/AUTO_DEFAULTS_LOG.md`. This file is not in the reading list (STATE→QUEUE→DECISIONS→LESSONS) and agent prompts must not make agents read it (token leak prevention). The conductor reads and summarizes it only when the user asks.

- 2026-10-23 (gate): render (art) pool default cap 2 (1 if RAM<6GB), merge role keeps merge_step -Priority. Changed in korean-division/tools/agent/heavy_gate.ps1 (overridable by the KD_GATE_ART_LIMIT environment variable), test heavy_gate_test.ps1 updated·passed, backup C:/works/_backup/heavy_gate*.before_artlimit_2026-10-23. Ordering rule: avoid general-purpose, opus gets card splitting + turn cap (see LESSONS.md).

## Supervisor·auto-check rules v2.2 (user approval 2026-10-23, basis token totals: check cost ≤5% of the cost of the checked target)
- Supervisor ordering model: opus → **sonnet**. Scheduled (60-minute) runs abolished; run **only when a harness anomaly signal fires or the user requests**. (The 'supervisor always-on' undecided item above is superseded by this rule: not made always-on.)
- In the supervisor prompt, specify directly the docs·files to read (no reading whole big docs), and receive the report within **12 lines**.
- Conductor's 'auto check once': extend the cycle to 1 hour or more and read only `ops/STATE_LIVE.md` (output of tools/agent/state_snapshot.ps1 -Write) and the harness `/api/state` summary. Do not read PIPELINE_LOG·big docs, and leave one line if there is no anomaly. Do periodic checks in code (harness·scripts) and call the LLM only on an anomaly signal.

## Agent recycling: not adopted (2026-10-23, user decision)
- Recycling — waking a finished agent to do new work — is not used. Keep the previous rule ('do not resume big-context agents; start new ones') as is, and always order new work with a new agent. Instructions sent to a working agent (deadline·stop·WIP packaging) are not recycling and are allowed.

## Agent stop·cleanup rules (user approval 2026-10-23)
- Do not stop a running agent without user instruction. If a stop is needed, first order WIP packaging (SendMessage).
- Even after a stop, the child processes that agent started (render python·shell chains) do not end. After a stop, clean up **only processes confirmed to belong to that agent by command line (worktree path·unit name)** (do not touch other agents'·merge·test processes) and report. Case 2026-10-23: after stopping the B2 wrap-up, cleaned up 3 render python and 1 shell.
