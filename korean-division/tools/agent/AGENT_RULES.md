# Korean Division - common agent rules (core, <=3KB)
Repo `C:\gameProject\korean-division`. Report in Korean, short. Procedures: `docs/procedures/` (merge-procedure, gate, permissions, measurement). Limits: `ops/LIMITS.md`.

## Workspace
- Worktree: `git worktree add ../kd-develop/kd-<name> -b <name> main`. Never switch the main dir's branch.
- Build: `"/c/Program Files/dotnet/dotnet" build -p:IntermediateOutputPath=obj/<name>/ -p:OutputPath=bin/<name>/`.
- Heavy gate before every build / selftest / detcheck / sim / render (see gate.md). Run commands one per tool call.
- Never kill processes by image name; only PIDs you started. No tanks / AT riflemen in sims.
- game-tools: never switch branches in the shared `C:/gameProject/game-tools` checkout (stays on main). Use `git -C <game-tools> worktree add ../gt-<task> -b <branch>`, remove it after (no --force).

## Token economy (mandatory)
- Big docs (`ENGINE_NOTES.md`, `ROADMAP_LONG.md`, `HANDOFF.md`, `docs/legacy/*`): Grep the heading, Read only that range. Code: Grep `PROJECT_MAP.md`, read only the functions you change. Same file: read once, keep notes.
- Pipe long output through grep/tail; logs >150 lines or >=4 unmodified files to read -> Gemini flash (`python C:/works/agentHub/agy_delegate.py -m flash -r reader|finder [-w] -f ...`; pro for a stuck bug's 2nd review). Gemini output is a lead: verify cited lines. Never give Gemini deterministic-sim core, save format, files you are about to edit, or secrets.
- Long commands (selftest / detcheck): foreground, timeout 590000, once; no polling turns.
- Context ~200k, or first mergeable slice done: merge / WIP-commit, then stop and report the rest.

## Docs / determinism
- ENGINE_NOTES: append a short section, never rewrite others' text. Your task card: write only its 결과/상태 lines. ops/*.md: not yours (owners in ops/PIPELINE.md). Count lines (selftest totals, FormatVersion) are replaced, never appended. Per-merge CLAUDE.md / SELFTEST_HISTORY / BASELINE / HANDOFF updates are NOT yours - the release role does them at queue end.
- Ambiguous instruction (value, direction, unit): ask in the report, or implement the safest reading behind a JSON switch and say so.
- Determinism: DetMath, hash RNG, commands only; new state -> checksum + Audit + save + FormatVersion bump (check main's value at merge time).
- No test dodging: never change a test's seed/map/values to pass an invariant; fix the cause or report and stop.
- Two real fix attempts failed: WIP-commit, report the diagnosis.

## Verify, merge, finish
- Verify / measure / render: gate.md. Merge: merge-procedure.md (you do NOT take the merge lock: commit the branch, set the task card to `wip-ready`, stop; the merger lands it; self-merge only for small docs-only cases).
- Commit specific files only (`git add <file>`; never `commit -a`; never touch untracked `docs/soak/` or `캠페인-초안.txt`); identity + trailer per CLAUDE.md. NEVER push.
- Permission-prone command -> permissions.md (DECISIONS_PENDING line, continue).
- When done: report in the role card's format, leave no background jobs.

## 창 규칙 (사용자 2026-10-22)
- 게임·편집기 창을 **전체화면/테두리 없는 전체화면/최대화로 띄우지 않는다.** 항상 창 모드(windowed), 크기는 1280x720 이하(해상도 시험은 `--size WxH` 로 캡처하되 최대화 금지). `settings.json` 의 `Mode` 를 fullscreen/borderless 로 바꾸지 말 것(바꿨다면 원복).
- 스크린샷 검수용 이미지는 이미지 뷰어를 열지 말고 파일만 만든다(지휘자가 폴더로 안내).
- 창은 `tools/agent/run_game.ps1` 로 보조 모니터에 띄운다.

## Parallel full selftest (release / pre-merge full run)
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/selftest_parallel.ps1 [-Jobs 4] [-Dll bin/<own>/KoreanDivision.dll] [-OutDir dir] [-Only g1,g2] [-DetCheck] [-SelfCheck]` (headless; build into your OWN bin dir first, never shared bin\Debug).
- Groups = every `KD_SELFTEST_ONLY` name in src/SelfTest.cs (incl. `core_*` / `perf` (lodaudit + lmgtable excluded: not in the full run), which cover the inline parts of the full run); unknown `-Only` names fail loudly (the exe would silently run the FULL selftest).
- Buckets: longest-first greedy by `tools/selftest_timings.json` (refreshed after a clean full run). `perf` + `core_fog` (timing thresholds) run alone after the buckets. Each job = one heavy_gate slot (gate closed >15 min -> job NOT RUN, exit 2).
- Each group has its own `KD_TMP` / `KD_CAMPAIGN_DIR`; groups writing shared files go in `$SharedState` (one bucket). A new group needs no registration (parsed from SelfTest.cs); new inline test code outside a group is NOT run in parallel mode - add it to a `core_*` case.
- Output: per-group PASS/FAIL + seconds, last 30 log lines of each failure, logs in `<OutDir>/logs/<group>.log`. Exit 0 pass, 1 fail, 2 partial. `-DetCheck` also runs `--detcheck` and prints the DETCHECK/GOLDEN lines.
- `-SelfCheck` = 3 tiny groups in 2 jobs (~10 s) to prove the harness works.
