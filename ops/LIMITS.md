# LIMITS - current limits (one place, 2026-10-22; values change only on user instruction)

| Item | Value | Note |
|---|---|---|
| Concurrent agents (max_total) | **12** (opus <= 3) | user instruction 2026-10-23 13:5x (earlier same day: 9; before that 30). Harness env var CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS is changed by the user (currently 30); the conductor session keeps to 12. agentHub/guard_config.json max_total = 12 |
| 上(opus) slots | **3 (from 0.3)** | user instruction 2026-10-23 (was 8). No recycling; stop only on user instruction |
| Art render pool | 8 (7 if free RAM <6GB) | user instruction 2026-10-22. `heavy_gate.ps1 -Render`. Waiting processes do not count as slots; only running make_unit children count |
| Non-art heavy pool (build·selftest·stress) | 3 (1 of them reserved for builds) | Separate pool from art. Builds use `-Build` (up to 3 slots), others up to 2. Art cannot use the last slot of the combined limit |
| Heavy jobs combined | 10 (12 if free RAM >=6GB) | user approval 2026-10-22 15:3x raised to 12; user approval AO1 (2026-10-22): 8→10; gate closes if free RAM <3GB (heavy_gate.ps1 RAM guard). Render lane counts only make_unit children (driver double-count bug fixed 564cc53). Gate v2: wait-time log `ops/gate_wait.log` (start·wait seconds·holder PID/name), never kills another PID |
| Heavy gate v3 (kd/gate-v3 9bb0d0de; installed in the main checkout a9522f84 on 2026-10-23) | budget 20 GB heavy WorkingSet; free-RAM floor build/merge/selftest/measure 7 -> 6.5 -> 6 GB (one stage per day from 10-23, set in `tools/agent/gate_config.json` `floor_heavy_stages`); render floor 8 GB; emergency floor 4 GB (nothing starts below it, also `-Priority`); waiter cap 6 (a 7th+ new non-priority call exits with code 75 'gate overloaded: report to conductor, do other work'); FIFO ticket queue; `-Priority` exempt from the purpose floor only | user 2026-10-23: 20 GB budget is the user's own decision (PC has 32 GB; two freezes on 10-18/10-22), the rest per the recommended plan. Watchdog kill threshold stays 3 GB. To halt the descent edit/delete the later stages in gate_config.json. Daily summary: `tools/agent/gate_stats.ps1`. The 'Heavy jobs combined' row above describes v2 and is superseded where they differ |
| Gemini pro | 3 | concurrent |
| Gemini flash | 3 | concurrent. Daily budget via `hub.py policy` |
| Agents per wave | 12 recommended (24 is the cap) | |
| Merge lock | held only by the merge role (merger, always-on) | `C:/gameProject/.git/kd-merge.lock` directory + `started.txt` (start time). Work agents do not take the lock; they finish with wip-ready. Expires after 30 min (merger checks the holder and cleans up). If the lock exists, report and stop, no wait loop |
| Conductor session | replace every wave (one ordering batch) recommended | Update STATE.md and start a new session before session context reaches 200k. Fable advice (2026-10-22): lower to agents 12 · 上 3 · heavy jobs 6 — user instruction takes precedence, values left as is |
| Agent context | stop before exceeding 200k | S=L cards are split in advance |

Current actual operating state vs target: target = art render 8 + non-art heavy 3 separate pools (after gate-v2 install) · actual = single pool 10/12 (heavy_gate.ps1, gate-v2 not yet in; table values are target/cap).

## 20-minute auxiliary check loop (idle only, when there are no events) (8 lines)
1. Query harness `GET http://localhost:8500/api/state` (context · cache · tokens · slots).
2. Agent with context >=200k → order merge/commit then stop.
3. If limits (agents · 上 · heavy jobs) are exceeded, hold orders.
4. Free slot → next card in QUEUE.md whose prerequisites are resolved (上 slot: R3 first).
5. If at least one wip-ready exists and there is no merger, order a merger (always-on); check MERGE_QUEUE.md order.
6. Update ops/STATE.md (main hash · agents · lock · limits).
7. On finding a waste pattern, record it in watch/LEAKS.md with numbers (no numbers, no record).
8. If a user decision is needed, write it only in ops/DECISIONS.md and notify the user in one line.

## Conductor (coordinator) role limits
Ordering · summarizing · decision ledger · checks only (flow = ops/PIPELINE.md). Does not write code, merge directly, or do long investigations; leaves them to agents (including scouts). Each wave, or when context grows, hand off via STATE.md and replace with a new session (L8).
