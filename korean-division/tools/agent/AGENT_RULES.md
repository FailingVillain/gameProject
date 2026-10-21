# Korean Division — common agent rules (read this instead of long boilerplate)

Repo: `C:\Users\seung\OneDrive\바탕 화면\coding\korean-division`. Reply/report in Korean, short.

## Workspace
- Worktree: `git worktree add ../kd-develop/kd-<name> -b <name> main` (create ../kd-develop if missing). Never switch the main dir's branch.
- Build: `"/c/Program Files/dotnet/dotnet" build -p:IntermediateOutputPath=obj/<name>/ -p:OutputPath=bin/<name>/`.
- Heavy gate BEFORE every build / selftest / detcheck / sim / render:
  `powershell -ExecutionPolicy Bypass -File "C:\Users\seung\OneDrive\바탕 화면\coding\korean-division\tools\agent\heavy_gate.ps1"` — one heavy process at a time yourself.
- NEVER kill processes by image name (no `taskkill /IM`). Kill only PIDs you started.
- No tanks / AT riflemen in sims.

## Token economy (MANDATORY)
- Do NOT read whole big docs. `docs/legacy/TACTICS_OBS_2026-10-19.md`, `docs/legacy/BATTALION_CAMPAIGN_2026-10-19.md`, `docs/ENGINE_NOTES.md`, `docs/ROADMAP_LONG.md`, `docs/HANDOFF.md` are large: Grep for the section heading you need and Read only that range.
- Find code via Grep on `PROJECT_MAP.md`; read only the functions you change.
- Pipe long test/sim output through `grep`/`tail`; never dump full logs into context.
- Reuse existing measurement tools (`--tactics-obs`, `tools/bn_campaign`, ArtyFx, `tools/n18_*`); don't write new aggregators unless needed.
- Measure only what the task's acceptance needs; respect the sim-time cap in the task. Default 5 seeds; 8 only for stated acceptance checks. If `docs/BASELINE_CURRENT.md` exists for the current main commit, use its numbers as "before" instead of re-running.
- During development run targeted tests (`KD_SELFTEST_ONLY=<group>`); run the FULL `--selftest` only once, right before merging.
- Merge main into your branch ONCE, at the end (not repeatedly while main moves). If main moves again before you merge, merge again only if there are conflicts; otherwise fast re-test with `--detcheck` + your test group.
- Long logs: summarize with the helper instead of reading them: `<cmd> 2>&1 | python C:/works/agentHub/hub.py ask auto "what failed and why? cite lines" -f -` (verify cited lines yourself).
- Final report ≤ 15 lines: what changed, key numbers, decisions needed. No long tables unless asked.

## Docs / merge conflict avoidance
- Do NOT edit the selftest count line in `CLAUDE.md` except as the merger (see Self-merge). Report the count.
- If an instruction is ambiguous (value direction/meaning/unit), ask in your report before guessing — or implement the safest reading behind a JSON switch and say so.
- HANDOFF: append ONE line at the end of the N-list using the N number given in your task (don't pick your own).
- ENGINE_NOTES / ROADMAP_SHORT: append a short section/line; don't rewrite others' text.
- Count / number lines in docs (selftest totals, FormatVersion, baseline numbers) are REPLACED with the new value, never appended as a second line.
- Determinism: DetMath, hash RNG, commands only; new state → checksum + Audit + save + FormatVersion bump (check main's current value at merge time).

## Verify + merge
- `--selftest` (grep FAIL), `--detcheck` (`--update` only if sim changed, then `tools/detcheck.ps1` 8 variants), `--snaptest` (KD_SNAP_QUICK=1) if state changed, `--uifuzz 30` if UI changed.
- Commit specific files only: `git -c user.name="seung" -c user.email="seungwoo3656@gmail.com" commit`, message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Merge into main only if `.git/MERGE_HEAD` absent: `--no-ff` (retry once). Re-run detcheck on main after merge; regenerate golden there if needed. Remove worktree + branch. Rebuild `bin/main` if sim/code changed. NEVER push.
- Branches are merged into main on the day they are created; if a measurement is unclear, merge with the feature behind a JSON switch (default off) and decide later.
- **Self-merge (2026-10-20, replaces separate merge agents)**: when your task is done and verified, YOU merge your branch into main unless the task says "do not merge" or a user decision is pending. Lock: `mkdir "<repo>/.git/kd-merge.lock"` (atomic; if it exists, wait 60 s and retry, max 60 min), remove it when done (also on failure). In main: `git merge --no-ff <branch>`; FormatVersion = max(main, yours)+1 if both bumped; golden → regenerate. If main did not move since your last merge-main and your full selftest passed, only re-run `--detcheck` (+ 8 variants if golden changed) — no second full selftest. If main moved: build, detcheck, your test groups + full selftest once. Breaks non-trivially → `git reset --hard ORIG_HEAD`, remove lock, report. Then REPLACE (don't append) the single 'latest' entry on the CLAUDE.md 알려진 실패 line, and append the old entry as one line to docs/legacy/SELFTEST_HISTORY.md (allowed for the merger), `publish.ps1` only if `dist/KoreanDivision.exe` is not running (check PID), remove worktree + branch (no --force). Never `commit -a`; never touch untracked docs/soak/ or `캠페인-초안.txt`.
- **No test dodging**: never change a test's seed/map/values just to get past an invariant failure (e.g. "tank stuck > 5 s"). Fix the cause, or report it and stop.
- **Baseline**: after merging, append your "after" numbers for the new main commit to `docs/BASELINE_CURRENT.md` (one short block: commit, scenario, seeds, wins, K/E, timeouts). Later agents use it as "before" — measure only "after". Balance verdicts come from the nightly soak report (`docs/soak/<date>/report.md`); your own seeds only show the feature works.
- **When done, stop**: after your final report, don't leave background jobs running.
- Merge main into your branch only ONCE, after taking the merge lock (not repeatedly while waiting) — every re-merge costs a full selftest.
- main is updated only once per branch: take the lock, one `git merge --no-ff`, release the lock. No repeated merges into main while waiting.
- Never run --selftest with a timeout under 15 min (a killed run must be repeated from scratch).
- Read selftest output by grepping FAIL/ERROR lines only; long logs go to the Gemini helper (see above).
- Run commands one per tool call — chained lock+merge+build lines get rejected by the permission checker.
- **game-tools: never `git checkout` / switch branches in the shared `coding/game-tools` checkout** (it must stay on `main` — other agents and the coordinator commit there). For a game-tools branch use your own worktree: `git -C <game-tools> worktree add ../gt-<task> -b <branch>`, work and render from that folder, commit there, then `git worktree remove` it (no --force). Commits meant for main go to the shared checkout on main.

## Gemini 위임 (사용자 2026-10-22: flash · pro 적극 활용)
- Antigravity 플랜 경유: `python C:/works/agentHub/agy_delegate.py -m flash|pro -r reader|finder|coder|reviewer [-w] [-f 파일…|-]` (앱이 꺼져 있으면 자동 API 폴백). 가이드 `C:/works/agentHub/ANTIGRAVITY_GUIDE_FOR_CLAUDE.md`.
- 150줄↑ 로그 · 고치지 않을 파일 4개↑ 읽기 · 웹/고증 검색 → flash. 명세가 확정된 데이터(JSON)/UI 문자열/테스트 초안 · 막힌 버그 2차 리뷰 → pro. 결과는 단서/초안이므로 인용 줄 · diff 를 직접 확인한다.
- 결정적 시뮬 핵심 · 저장 형식 · 수정 직전 파일은 위임 금지. 비밀 파일(keys.json 등) 첨부 금지.
- 동시 한도(사용자 2026-10-22 최신): 에이전트 24 (上 ≤5), Gemini pro 3 + flash 3, 무거운 작업 ≤8 (heavy_gate).

## 권한 · 자율 진행 (사용자 2026-10-22: 수동 권한 승인이 필요한 작업은 피하고 속행)
- 폴더/파일 **삭제 · 프로세스 종료(이미지 이름) · 저장소 밖 쓰기 · push** 등 권한 프롬프트/거부가 날 만한 명령은 시도하지 말고, 대안(다른 방식 우회 금지)이 없으면 그 일을 `docs/DECISIONS_PENDING.md` 끝에 `- [에이전트/작업] 필요한 결정/권한: …` 한 줄로 적고 남은 일을 계속한다. 거부된 명령을 다른 도구로 우회하지 않는다.
- 사용자 결정이 필요한 질문도 같은 파일에 한 줄로 모은다(여러 번 묻지 말 것). 보고에는 "결정 N건 DECISIONS_PENDING 에 기록"만 적는다.
- **재렌더 우선**: 유닛 재렌더(`make_unit.py`)는 게이트에서 한 칸을 예약받는다 — 렌더 직전 `heavy_gate.ps1 -Render`, 그 외 모든 빌드 · 시험 · 시뮬은 `-Render` 없이(= 한도 -1).

## 병합 절차 단축 + 잠금 우선 (사용자 2026-10-22, 첫 병합 최우선)
- **병합 잠금(.git/kd-merge.lock)을 잡은 에이전트는 게이트를 `-Priority` 로 호출**한다(한도 +3, 대기열 앞질러 실행). 잠금을 잡았으면 시험·병합을 최단으로 끝내고 즉시 해제. 잠금을 잡기 전 시험은 -Priority 금지.
- **병합 직전 검증**: main 병합 → 빌드 → `--detcheck`(+골든 바뀌면 8변형) → 자기 시험 묶음(`KD_SELFTEST_ONLY=…`) → 병합. **전체 `--selftest` 는 병합 큐의 마지막 병합자(AI 3B)와 코디네이터가 큐 끝에 한 번만** — 그 전 병합자는 전체 selftest 생략(자기 묶음 + detcheck + snaptest 만), 대신 보고에 "전체 selftest 생략"이라 적는다.
- 자기 시험 묶음이 여러 개면 `KD_SELFTEST_ONLY` 묶음을 동시에 2개까지 병렬 실행해도 된다(각각 게이트 통과).

## 토큰 누수 대응 (harness 점검 데이터 기반, 사용자 2026-10-22)
- 데이터: 상급 에이전트 컨텍스트 207~310k, 코디네이터 360k(매 턴 입력), 캐시 재생성 반복(적중률 66~76%, 5분 넘는 대기 뒤 20k+ 재생성), 대기 중 짧은 응답 턴, 같은 파일 3~4회 재독.
- 규칙: ① **컨텍스트 ~200k 넘거나 첫 병합 가능 조각을 병합하면 멈추고 남은 일을 보고** — 남은 일은 코디네이터가 새 에이전트로 이어간다 ② 긴 명령(selftest/detcheck)은 **포어그라운드 timeout 590000 으로 한 번**(또는 백그라운드 1회 후 완료 알림 대기), 폴링 턴 금지 ③ 같은 파일은 필요한 줄 범위만, 읽은 내용은 메모해 재독 금지 ④ 큰 로그 · diff 는 Gemini flash 요약 ⑤ 개별 효과 측정은 `measure` 에이전트/통합 측정으로 모은다.
- 새 에이전트 양식: `measure`(측정만) · `merger`(병합 큐 운영) · `reviewer`(읽기 전용 검토). sim 은 병렬 여러 개 허용(핫스팟 규칙 준수).
