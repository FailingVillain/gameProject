# LESSONS - conductor core rules (13, reflects supervisor section A of 2026-10-22; original 31+ = LESSONS_ARCHIVE.md, not read)
> Agent-facing lessons (orig. 9·13~17·19~22·26·27) moved to role cards/AGENT_RULES/PIPELINE §1-d. Once tools land in main, shrink K1·K4·K10 by one line each.

1. **Preflight before ordering** (orig. 1·24·30): attach the result of `preflight.ps1` + `card_id.ps1` + `roadmap_check.ps1` to the prompt in 1 line. QUEUE rows can be stale → cross-check MERGE_QUEUE completion table · main code · worktree `git status` · duplicate branches (4 duplicate orders, 336k).
2. **Role fits the work type** (orig. 2): do not give code work to docs (haiku) (T26). Conductor judgment that tools cannot block.
3. **Stacked orders** (orig. 3): state base commit hash · status of prerequisite review blockers. After wave 2 the base is a single integration branch, stack ≤3 (PIPELINE §1-a).
4. **Branches that bump numbers** (orig. 4): do not fix FV/Replay at order time — temporary value (base+1) + `fv_claim`, the merge line finalizes with `fv_assign`.
5. **Pre-split L cards** (orig. 5): 12 cases at 190k+. Split cards over 200k; new agent instead of resume.
6. **Cards without detailed design** (orig. 6): write 'default-off switch + draft numbers' in the acceptance criteria. For decision options, name the key and target.
7. **Report shortfalls honestly, no wip-ready** (orig. 7): T06·T16·T31c.
8. **Sync QUEUE·FV on every wip-ready report** (orig. 10): stale rows cause duplicate orders. Delete once QUEUE status is generated from card files.
9. **Review is mandatory** (orig. 11): all 29 reviews caught defects, 5+ blockers. Kept even under the no-merge policy.
10. **Confirm stall before reorder** (orig. 8+18 merged): 0-byte output and unchanged worktree alone do not mean stalled — check runner PID · scratchpad · heavy_gate (`watchdog.ps1`, reorder only SUSPECT_STALL). Long renders are decoupled from agent lifetime.
11. **Conductor context = biggest leak** (orig. 29): replace every wave (session rule is a user decision), 3~4 key lines per completion notice, log lines ≤200 chars.
12. **The gate starves builds** (orig. 25, delete after gate v2 lands in main): when the render lane fills the pool, stalls of 25+ min (8+ times). Kept since main does not yet have v2.

13. **A card that changes a rule runs every suite that verifies that rule** (user approval 2026-10-22): running only its own suite misses stale expectations in others — the T37 proxy delay step change broke 2 `squads` tests (FAIL even before merge), 7 new mission JSONs missed `PropsSelfTest` (requires propScatter). Write 'run all related suites (e.g. squads·command·drill·officer / props·n77·n12·terrain·missions)' in the order prompt.

Archive handling: 23=28·24=30 duplicates deleted, 12 (old repo patch) archived as the restore phase ended.

- 2026-10-23 ordering rule (user approval, based on token totals): avoid general-purpose agents; use only specialist roles (feature/sim/reviewer/docs/measure/spec/merger). By totals, subagents sum to more than the conductor session (cache reads 9.5B vs largest session 1.6B), and general-purpose opus is heaviest at about 38M per agent on average.
- 2026-10-23 ordering rule: split opus agent cards finer and write a turn cap in the prompt (e.g. 80 turns). On hitting the cap, stop and report current state · remaining work.

- 2026-10-23 rule (user approval): run command output over 150 lines via `python C:/works/agentHub/route.py -- <command>` and see only the log path and summary (summary is a lead; verify quoted lines in the saved log).
- 2026-10-23 rule (user approval): handoff = `python C:/works/agentHub/handoff_gen.py` output (out/HANDOFF_AUTO.md, measured values only) plus at most 10 hand-written lines. Not written to the repo.

- 2026-10-23 measurement lesson (fact): measured agent startup cost — first call of a do-nothing haiku agent is 36.8k tokens, all cache creation (that model's cache was cold). A warm sonnet agent at start reads about 24.2k cache (shared prefix: system prompt·tools·CLAUDE.md·memory index) + creates about 14k. So starting one agent ≈ 38k tokens, cheap only when that model's prefix is warm. Re-reading the whole context each turn is the same for new and reused agents. Source: usage field of the subagent jsonl (first assistant line). (Source: seung-23 session measurement; reuse rule not yet a user decision)

- 2026-10-23 measurement (fact, seung-23 session): subagent cache lifetime is about 5 minutes. Resuming a haiku probe at 3-minute interval reads cache including the agent-specific part (29,142 read/7,715 created); at 8 minutes it reads only the shared prefix 23,886 and recreates the specific part 12,971. 15/30-minute measurements aborted.

- 2026-10-23 lesson (safety-hook block response, user approval): when the safety hook (kd_guard.ps1) blocks a command, **do not retry or look for a rewording workaround.** First judge the intent — if it is a real destructive action, report to the user and proceed only after approval; if it is a false positive from text in a document, do not touch the hook, report that fact and the evidence to the user, then proceed (write documents with the Write/Edit tools). The hook regex-checks Bash command text, so writing destructive command names into a document via a Bash heredoc is falsely flagged.

- 2026-10-23 lesson (promotion script): the verify step of promote_v03.ps1 received the stderr ("is okay") of `git bundle verify ... 2>&1` as a PowerShell 5.1 NativeCommandError and aborted. Before the next promotion remove `2>&1` or handle ErrorActionPreference. Tag·bundle·copy steps before verify had already completed, and it was finished with manual verification.
