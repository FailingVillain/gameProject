# Korean Division - common agent rules (core, <=3KB)
Repo `C:\gameProject\korean-division`. Report in Korean, short. Procedures: `docs/procedures/` (merge-procedure, gate, permissions, measurement). Limits: `ops/LIMITS.md`.

## Workspace
- Worktree: `git worktree add ../kd-develop/kd-<name> -b <name> main`. Never switch the main dir's branch.
- Build: `"/c/Program Files/dotnet/dotnet" build -p:IntermediateOutputPath=obj/<name>/ -p:OutputPath=bin/<name>/`.
- Heavy gate before every build / selftest / detcheck / sim / render (see gate.md). Run commands one per tool call.
- Never kill processes by image name; only PIDs you started. No tanks / AT riflemen in sims.
- game-tools: never switch branches in the shared `C:/gameProject/game-tools` checkout (stays on main). Use `git -C <game-tools> worktree add ../gt-<task> -b <branch>`, remove it after (no --force).

## Token economy (mandatory)
- Big docs (engine notes are split by topic in `docs/engine/*.md` - Grep there; `docs/ENGINE_NOTES.md` is only the index; `ROADMAP_LONG.md`, `HANDOFF.md`, `docs/legacy/*`): Grep the heading, Read only that range. Code: Grep `PROJECT_MAP.md`, read only the functions you change. Same file: read once, keep notes.
- Pipe long output through grep/tail; logs >150 lines or >=4 unmodified files to read -> Gemini flash (`python C:/works/agentHub/agy_delegate.py -m flash -r reader|finder [-w] -f ...`; pro for a stuck bug's 2nd review). Gemini output is a lead: verify cited lines. Never give Gemini deterministic-sim core, save format, files you are about to edit, or secrets.
- Long commands (selftest / detcheck): foreground, timeout 590000, once; no polling turns.
- Context ~200k, or first mergeable slice done: merge / WIP-commit, then stop and report the rest.

## Docs / determinism
- ENGINE_NOTES / DECISIONS_PENDING: do NOT edit those files; write your short section / lines to your own one file `docs/engine_notes.d/<branch>.md` / `docs/decisions_pending.d/<branch>.md` (branch with `/`->`__`; optional first line `date: YYYY-MM-DD HH:mm`); the merge line assembles them (tools/agent/assemble_notes.ps1). Never rewrite others' text. Your task card: write only its 결과/상태 lines. ops/*.md: not yours (owners in ops/PIPELINE.md). Count lines (selftest totals, FormatVersion) are replaced, never appended. Per-merge CLAUDE.md / SELFTEST_HISTORY / BASELINE / HANDOFF updates are NOT yours - the release role does them at queue end.
- Ambiguous instruction (value, direction, unit): ask in the report, or implement the safest reading behind a JSON switch and say so.
- Determinism: DetMath, hash RNG, commands only; new state -> checksum + Audit + save + FormatVersion bump: do not pick the final number - run `tools/agent/fv_claim.ps1 -Fv 1 [-Replay 1] -Reason ...` (writes ops/fv_claims/<branch>.txt) and use base FV+1 as the temporary number; the merge line assigns it (fv_assign.ps1). Never edit ops/QUEUE.md / STATE.md / MERGE_QUEUE.md (queue_guard.ps1 checks).
- ObjCodec (AI/controller serialization) work buffers must not retain world-owned objects (Squad/Company/Battalion etc.) - Clear after use or mark [NonSerialized] (the latter changes the serialization layout, so it bumps FV). The selftest assertion (kd/objcodec-guard) catches world-owned types.
- No test dodging: never change a test's seed/map/values to pass an invariant; fix the cause or report and stop.
- Two real fix attempts failed: WIP-commit, report the diagnosis.

## Verify, merge, finish
- Verify / measure / render: gate.md. Merge: merge-procedure.md (you do NOT take the merge lock: commit the branch, set the task card to `wip-ready`, stop; the merger lands it; self-merge only for small docs-only cases).
- Commit specific files only (`git add <file>`; never `commit -a`; never touch untracked `docs/soak/` or `캠페인-초안.txt`); identity + trailer per CLAUDE.md. NEVER push.
- Permission-prone command -> permissions.md (DECISIONS_PENDING line, continue).
- When done: report in the role card's format, leave no background jobs.

## Window rules (user 2026-10-22)
- Never open game/editor windows fullscreen / borderless fullscreen / maximized. Always windowed, size <=1280x720 (resolution tests: capture with `--size WxH`, never maximize). Do not change `Mode` in `settings.json` to fullscreen/borderless (revert if you did).
- For screenshot-review images, do not open an image viewer; just produce the files (the conductor points to the folder).
- Launch windows on the secondary monitor via `tools/agent/run_game.ps1`.
- (clarification 2026-10-22) `--uifuzz` random window resizing (up to 1920x1080) is **allowed**: it is windowed, on the secondary monitor, not fullscreen/maximized. Forbidden: fullscreen/borderless/maximize and changing settings.json Mode.
- (token saving 2026-10-22) **The final report must not exceed the role card's line count** (usually <=8, reviewer <=12, or the conductor's value if given) (summary + paths + blockers). End the report with an **'임의 결정' (arbitrary decisions) item**: list one line each for every value, assumption, interpretation, deletion, or relaxation you chose yourself because the card did not specify it ('없음' if none; the conductor logs it in AUTO_DEFAULTS_LOG). Put long tables/cause explanations in commit messages or the card file; the report gets only the path. When waiting for background jobs, do not stop with a "waiting" report; block until done (`Monitor` or a polling loop). Do not resume agents with a large context (>150k) via SendMessage (reprocesses the whole transcript) - follow up with a new agent + card/commit handoff.
- Window position: env var KD_MONITOR=secondary is already set (the game follows it after the T38n merge). To run the game directly use `tools/agent/run_game.ps1`.

## Running the game window
- Whenever the game window opens (visual check, screenshot, uifuzz, manual run) launch it via `powershell -File tools/agent/run_game.ps1 <KoreanDivision.exe | project dir> [game args]`: it moves the window to the SECONDARY monitor (primary if only one); `KD_MONITOR=primary|secondary|<index>` overrides; exit code is the game's. Headless flags (`--selftest`, `--detcheck`) need no launcher.

## Parallel full selftest (release / pre-merge full run)
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/selftest_parallel.ps1 [-Jobs 4] [-Dll bin/<own>/KoreanDivision.dll] [-OutDir dir] [-Only g1,g2] [-DetCheck] [-SelfCheck]` (headless; build into your OWN bin dir first, never shared bin\Debug).
- Groups = every `KD_SELFTEST_ONLY` name in src/SelfTest.cs (incl. `core_*` / `perf` (lodaudit + lmgtable excluded: not in the full run), which cover the inline parts of the full run); unknown `-Only` names fail loudly (the exe would silently run the FULL selftest).
- Buckets: longest-first greedy by `tools/selftest_timings.json` (refreshed after a clean full run). `perf` + `core_fog` (timing thresholds) run alone after the buckets. Each job = one heavy_gate slot (gate closed >15 min -> job NOT RUN, exit 2).
- Each group has its own `KD_TMP` / `KD_CAMPAIGN_DIR`; groups writing shared files go in `$SharedState` (one bucket). A new group needs no registration (parsed from SelfTest.cs); new inline test code outside a group is NOT run in parallel mode - add it to a `core_*` case.
- Output: per-group PASS/FAIL + seconds, last 30 log lines of each failure, logs in `<OutDir>/logs/<group>.log`. Exit 0 pass, 1 fail, 2 partial. `-DetCheck` also runs `--detcheck` and prints the DETCHECK/GOLDEN lines.
- `-SelfCheck` = 3 tiny groups in 2 jobs (~10 s) to prove the harness works.
- (pipeline v2 2026-10-22) Never modify ops/QUEUE, STATE, MERGE_QUEUE or anything under ops/ on a work branch (`tools/agent/queue_guard.ps1`). When bumping FV/Replay, do not fix the number: use base+1 as a temporary value + `fv_claim.ps1`. Write DECISIONS_PENDING / ENGINE_NOTES into the fragment files under `docs/decisions_pending.d/` / `docs/engine_notes.d/`. Follow `ops/LIMITS.md` for limit numbers, not values in the prompt. Merge steps only via `merge_step.ps1` (including snapshot + `--snaptest`).
