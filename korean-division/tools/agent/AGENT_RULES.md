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
- ENGINE_NOTES / DECISIONS_PENDING: do NOT edit those files; write your short section / lines to your own one file `docs/engine_notes.d/<branch>.md` / `docs/decisions_pending.d/<branch>.md` (branch with `/`->`__`; optional first line `date: YYYY-MM-DD HH:mm`); the merge line assembles them (tools/agent/assemble_notes.ps1). Never rewrite others' text. Your task card: write only its 결과/상태 lines. ops/*.md: not yours (owners in ops/PIPELINE.md). Count lines (selftest totals, FormatVersion) are replaced, never appended. Per-merge CLAUDE.md / SELFTEST_HISTORY / BASELINE / HANDOFF updates are NOT yours - the release role does them at queue end.
- Ambiguous instruction (value, direction, unit): ask in the report, or implement the safest reading behind a JSON switch and say so.
- Determinism: DetMath, hash RNG, commands only; new state -> checksum + Audit + save + FormatVersion bump: do not pick the final number - run `tools/agent/fv_claim.ps1 -Fv 1 [-Replay 1] -Reason ...` (writes ops/fv_claims/<branch>.txt) and use base FV+1 as the temporary number; the merge line assigns it (fv_assign.ps1). Never edit ops/QUEUE.md / STATE.md / MERGE_QUEUE.md (queue_guard.ps1 checks).
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
- (명확화 2026-10-22) `--uifuzz` 의 무작위 창 크기 변경(최대 1920x1080)은 **허용**: 창 모드·보조 모니터이고 전체화면/최대화가 아니기 때문. 금지 대상은 fullscreen/borderless/최대화와 settings.json Mode 변경.
- (토큰 절약 2026-10-22) **최종 보고는 8~12줄 이내**(요약 + 경로 + 막힌 것). 긴 표·원인 설명은 커밋 메시지·카드 파일에 쓰고 보고에는 경로만. 백그라운드 작업이 끝나길 기다릴 때 '대기 중' 보고로 멈추지 말고 블로킹 대기(`Monitor` 또는 폴링 루프)로 끝까지 진행한다. 큰 컨텍스트(>150k) 에이전트에 SendMessage 로 재개시키지 않는다(전체 기록 재처리) — 후속은 새 에이전트 + 카드/커밋 인계.
- 창 위치: 환경변수 KD_MONITOR=secondary 가 이미 설정돼 있다(게임이 T38n 병합 뒤 따른다). 게임을 직접 실행할 땐 `tools/agent/run_game.ps1` 사용.
