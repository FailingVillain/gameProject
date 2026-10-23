# STATE - one-page status (updated 2026-10-22, at docs-fable merge)

> Sources of truth: state = this file · queue = QUEUE.md · merge = MERGE_QUEUE.md · limits = LIMITS.md · decisions = DECISIONS.md · flow = PIPELINE.md. A new conductor session starts by reading only this file → QUEUE → DECISIONS → PIPELINE (PIPELINE §4).
> Writers: conductor (checks·handoff) + release (one line at queue end). Work agents do not write.
> **Handoff rule (2026-10-22)**: write handoff only in this one STATE.md. Do not write measured values such as hashes·worktrees·locks·heavy counts directly; quote `tools/agent/state_snapshot.ps1` (-Write → ops/STATE_LIVE.md). `ops/PIPELINE_LOG.md` is append-only — do not read it at session start (Grep only when needed).

## Measured values (handoff 2026-10-23)
- Do not write main hash · worktree list · merge lock · running agent count · FV/Replay versions in docs. Command: `powershell -File korean-division/tools/agent/state_snapshot.ps1 -Write` → quote `ops/STATE_LIVE.md`.
- Merge status (2026-10-23): merge line N3c done · N3d in progress · 0.2.1 build scheduled 03:20 · 0.3 promotion awaits user command. (details = end of ops/MERGE_PROGRESS.md, measured = STATE_LIVE.md)
- Stale sections (baseline · unmerged-branch table · in progress · 18:00 handoff §1~§5) = ops/STATE_ARCHIVE_2026-10-22.md. Card status = ops/QUEUE.md (finished rows = ops/QUEUE_DONE.md) · decisions = ops/DECISIONS.md (old decisions = ops/DECISIONS_ARCHIVE_2026-10-22.md).
- Hash·branch values in §6~§10 below were recorded at that time (night of 10-22) and may be stale — before resuming check STATE_LIVE.md and the end of ops/MERGE_PROGRESS.md.

### 6. User decisions (defaults applied · DECISIONS BA–BG · ops/AUTO_DEFAULTS_LOG.md is append-only, not read routinely)
- Korean title BE confirmed: window 'KOREAN DIVISION : MARCH ON MANCHURIA' · menu '대한 사단 : 북벌 1920' · in-story '제17 대한 사단'.
- Defaults applied: ColorBlind setting check (T38m) · concise-mode color-blind shapes · prone-fire cells (prone cells) · save-compat policy (auto default) · CoverSeek clustering = 0.3 balance item.
- User's part: write commander templates (C:/works/commanders_authoring) · trademark check KIPRIS/USPTO (mandatory before release) · U01 GitHub author check · U02 delete old folders · T26 .git/hooks write permission · sheet review (T08/T22/T28/T19c/T11, paper drafts).

### 7. Fixed rules
- Answers in Korean, short. No push (only on user command). Merge only via the merge line (no direct merge to main). main is final `--ff-only`, after user confirmation.
- Balance last (0.3 one-shot), AI improvements 0.4. Web lookups only via Gemini (`hub ask -w`).
- Windowed only, 1280x720 or smaller, no fullscreen/borderless. Secondary monitor = env `KD_MONITOR=secondary` (~/.claude/settings.json; the game reads it after T38n merges).
- Limits: see ops/LIMITS.md (source of truth for values; includes RAM guard). Agent final report ≤12 lines. Do not resume big-context agents; start new ones.
- Read-only reviewers must not git checkout in C:/gameProject (LESSONS 20).
- Questions: auto-apply recommended defaults and ask few at a time. Session replacement once a day. The conductor session context is large (this session 724k, 51% of all tokens) → keep answers·log lines short.

### 8. Known issues
- Gate waits have taken over 1 hour several times (the render lane fills the pool and starves merge builds). Next session investigates first: render lane 6 · RAM budget (LESSONS 25).
- If a checksum differs from expected during merge or a non-mechanical conflict occurs, stop at that step and report (no arbitrary resolution). crossing PASSing after B5b is normal.
- Environment issue where the executable apphost cannot find the .NET runtime → see release record (run with `dotnet bin/main/...`).

### 9. Review materials · reports
- Review sheets `C:/works/_review_sheets_2026-10-22` (items 01–24, paper_ui_v2_tabs/) · roadmap PDF `C:/works/KoreanDivision_roadmap_2026-10-22.pdf`. The user's graphics review is done later in one go.
- Report: ops/REPORT_2026-10-22.md (full report as of 2026-10-22 13:00 · list of roadmap items not yet queued · counter-proposals).

### 10. 2026-10-22 night situation save (right before conductor session replacement; resume: this section → ops/BACKLOG_2026-10-22.md → end of ops/MERGE_PROGRESS.md)
- Base: kd-final branch merge/0.2-final = de2a6053 (merge L1~L8 done: selftest-unknown-group, IsAi, storage-guard, t44, j-followup, t51a, t52a, ti1; settings panel height 650px total·needs redesign; detcheck unchanged, snaptest 86 OK). Next: L9 ti4 → L10 tfa → L11 g2 → L12 rp2 → L13 t48e (see end of MERGE_PROGRESS.md). Lock released. main=486c4d79 (no ff, after user confirmation). No push.
- Branches awaiting merge (merge L order · reflects reviews 6/7): fix-selftest-unknown-group b0d4272f · IsAi round-trip e1e7b558 · tools/storage-guard a73f4d20→8c3be52b (includes day_close D: copy) · t44-smoke-run 9d4105dd · j-followup-fixes 2199de69 · t51a-path-guard 34dd8d4f · t52a-alert-log c88bb69a · ti1-i18n-core 9a08cce8 → ti4-data-text 638b680d (must come after ti1) · tfa-campaign-graph bbcee2ab (schema 14 temporary) · c1e1-g2-episode-ui fc7982ba · rp2-aar-pages 7b58d08a (fix test ==24 hardcode) · t48e-battle-link 5303bf99 (FV 68→71 reassigned, register test chain). Then merge M: tfb-campaign-advance adb28d17 (on top of TF-A) · gun-cost-wire 677f2d13 · tw2-blizzard 92e6bf2f (FV temporary 68 reassigned) · t50a-perf 61e05ebe (cache fields reflected in snapshot classification) · t39e-muzzle-code-clean 5d83369a (UnitsSelfTest conflict expected) · a1-terrain ed1d80b3 → a1-roads ab9ab733 · a2-ground-decals b63062dc · d-trains 21a3b158 · c1e1-art-units 8b73c78b · B2 cavalry 4-type render (kd-b2-units, progress file RERENDER_PROGRESS.txt) · B1 (마적/봉천/척후, kd-b1-units or existing worktree).
- Art review sheets (awaiting user check): a1_terrain_before_after · a1_roads_contact · a2_decals_contact · d_trains_contact · c1e1_new_units (each worktree korean-division/docs/art_review/).
- New operating rules: gate v2.1 installed (CPU<85%, free RAM>=8GB, entry interval 20s, 10s watch under 3GB, art pool 4/non-art 3) · do not launch 20 agents at once (PC froze twice) · merger must not git reset --hard (1 accident, ~/.claude/settings.json deny list reinforced) · admin policy (C:\Program Files\ClaudeCode: managed-settings.json + hooks/kd_guard.ps1 + kd_scope.json) installed, applies from next session → first check with harmless tests only, never actually type dangerous commands · writable folders: gameProject, works, desktop coding, downloads (+ supportRoots for tools), outside scope is a click-to-allow prompt · storage: local 100GB (currently about 77GB), D:\KoreanDivisionBackup secondary, G: milestones only 150GB, version = daily/date tag (day_close.ps1, at '상황 저장') + milestones only on user command · today's daily/2026-10-22 tag and bundle done (C:\works\_backup\KoreanDivision, D:).
- Open user decisions: settings panel height redesign (about 650px/1024x640 after merge) · whether to tighten harness stall-suspect judgment · g2 awaiting approval 5 · awaiting user decision 18 (bottom of BACKLOG) · Linux machine (4GB, CPU from around 2008) unsuitable for build/render, backup use only (SSH key not set, username fv2).
- Queue: ops/BACKLOG_2026-10-22.md (48 items, conflict groups marked; take from the top into slots of finished agents). For art orders try subagent_type "art".
- Remaining-work hints: gun unit price · TF-C (after TF-B) · TI2 (after merge) · T50b/c (after T50a) · PR2 variant re-report (the 10-23 01:02 cron may have vanished with session end → re-register) · QUEUE consistency (queue_status -Write, after merge) · worktree cleanup round 2 (needs user approval) · m2 follow-up done (gun-cost-wire).
- (22:25 added) B1 (kd-b1-units, kd/b1-deferred-units-1 9b4d049f): infantry-bandit·thug-bandit·dadao-fengtian rendered (before import, sheet game-tools/korean-division-image-out/_res96/), remaining infantry-scout (output empty, re-render)·officer-bandit — resume: in game-tools/image run `python -u rerender96.py --only infantry-scout,officer-bandit`. B2 (kd-b2-units): the cavalry 4-type render driver may still be running as an OS process → check RERENDER_PROGRESS.txt·b2_render.log, import/commit finished ones. All agents handed over in WIP state (no running agents, user instruction).
- (22:25 added) Admin policy v2 draft (scratchpad policy folder): adds backup folders (D:\KoreanDivisionBackup, G:\내 드라이브\KoreanDivisionBackup, C:\works\_backup) delete·move·rename forbidden (noDelete), mock tests 27/27. Installed v1 lacks this rule → user must re-copy policy\hooks\* into C:\Program Files\ClaudeCode\hooks\ with admin PowerShell (same copy command as before). No tests typing real dangerous commands.
- (22:30) Milestone v0.2.1 backup done: tag v0.2.1 = merge/0.2-final de2a6053. bundle: v0.2.1.bundle (tag history only 365MB; C:/works/_backup/KoreanDivision, D:\KoreanDivisionBackup, G:\내 드라이브\KoreanDivisionBackup) · v0.2.1-all-branches.bundle (all branches 537MB; C:, D:). Restore: git clone <bundle> or git fetch <bundle> 'refs/*:refs/*'.

- (03:3x) Milestone v0.3 promotion·backup done: tag v0.3 = merge/0.2-final 2531ec16. Handoff: ops/QUEUE_0.3.md (waves·sleep-mode rules), bottom of ops/DECISIONS.md, 0.2.1 zip C:/works/_dist_out/KoreanDivision-0.2.1-2531ec1622.zip.
