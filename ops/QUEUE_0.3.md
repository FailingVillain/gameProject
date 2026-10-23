# QUEUE 0.3 (draft 2026-10-23 03:20; read by the first session right after promotion)

Limits (user 2026-10-23, from 0.3): **max 9 concurrent agents, max 3 senior (opus)**. No recycling (always a new agent). Stop only on user instruction. Gate: render (art) pool default 2 (can be raised to 3~4 outside merge rounds, KD_GATE_ART_LIMIT), heavy pool 2 + build 1.
Baseline: merge line post-N3f-1 (Config 0.2.1, FV Snapshot 74 / Replay 27 / campaign schema 15, detcheck 672804207fc4e486 3c42aea49324248e). Model grade: 상=opus, 중=sonnet, 하=haiku.

## Wave 1 — right after promotion (max 9 parallel, 3 senior)
| # | Card | Grade | Content | Prereq·note |
|---|---|---|---|---|
| 1 | G1 golden-change bundle | 상 | merge `kd/water-fire-open`(6142cb8c) + regenerate golden once | Gather golden-changing work in this window and regenerate last; keep separate so the same senior agent does not do NEW-3·NEW-4 |
| 2 | G2 NEW-3 prone soldiers dragged along by formation | 상 | split posture/movement (soldiers prone from suppression) | golden may change → included in the G1 window |
| 3 | G3 NEW-4 dithering in narrow chokepoints (together with `wip/chokefix` D14 hold) | 상 | automatic column · pass-through queue | golden may change, D14 judgment needed |
| 4 | F1 follow-up small-fix bundle | 중 | resume `kd/review-followups-small`(WIP 354c02cf): DiskVersion migration, HUD height cap, 4 tests | reorder with a new agent (not a continuation) · wip-ready (not merged): `kd/review-followups-small` @a10c6055 |
| 5 | F2 T57f delegation follow-up | 상(or 중) | put OverrideSeconds into the replay header/defs hash | FU-5 decision (expiry-tick command vs header; recommended header) · wip-ready (not merged): `kd/t57f-override-header` @be1d5a7f |
| 6 | F3 T61g·CKf follow-up | 중 | T61g-1·2, CKf-3·4·5 code/tests | T61g-3·4, CKf-1·2 after user decision · wip-ready (not merged): `kd/f3-followups` @9b2eeed5 |
| 7 | TI2-P0 | 중 | first piece of migrating UI string constants→keys (shared helper · 3 `static readonly` caches) | needs answers to Q1~Q7 of spec/ti2-plan; after N3e docs merge · wip-ready (not merged): `kd/ti2-p0` @217207d0 |
| 8 | N3e merge | 중(merger) | autoplay-summary · soak-allon-r (Program.cs conflict) · pro-pilot-save-timing · snapbench-map · spec/* (followups, ti2-plan, runbook, personnel-ui, prisoners-loot-r, regiment-delegation-r, auto-pause, order-undo, mission-checkpoint) | 3 conflicts to resolve beforehand (soak-allon, prisoners-loot, regiment-delegation) · merged into merge/0.2-final @33ee9be3 |
| 9 | cleanup haiku | 하 | tidy QUEUE stale values (C1E1-g3 etc.), check 17 worktrees B, guidance for cleaning kd-rerender-5·perf-bench | deletion needs user approval |

## Wave 2 — art (render pool 3~4, outside merge rounds)
| # | Card | Grade | Content | Note |
|---|---|---|---|---|
| 10 | A1 태극기 | 중 | merge `gt/taegukgi-detail`(41eab625) + re-render·import Korean standard-bearer | draft approved; commit render scripts (scratch/tg·tg2) to tools/; awaiting legal text check |
| 11 | A2 B2 resume | 중 | check tachanka render result (normal exit · collide/edge pass, before commit), investigate·re-render 75 horse-artillery clipping cases, review sheet of 2 cavalry types | render processes were cleaned up so must run anew |
| 12 | A3 shape-changed unit re-render | 중 ×2~3 | about 10~15 types: Type-45 cap Japanese army, infantry overlap batch fix (collide), 5 non-res96 types in shard 2, scout prone (frame_wide), etc. | 2~15 min per unit (including gate), 2~4 hours |
| 13 | A4 mid buildings·terrain·props·decals | 중 | art-style mid re-render (buildings 1~2h · terrain 1h · props 30 min · decals) + comparison sheet approval | user sheet approval required, vehicles·trains excluded |
| 14 | A5 unit color unification draft | 중 | comparison sheet of 5 infantry types with mid color post-processing applied to the remaining units | batch-apply on approval (no render) |

## Wave 3 — features·measurement
| # | Card | Grade | Content | Note |
|---|---|---|---|---|
| 15 | T61d company merge · merge proposal · auto assignment | 상 | proposal at 40% or below of strength, same regiment·branch·caliber, assign waiting officers (Q-F·Q-K~Q-N) | after the 12 design questions are cleared together with personnel-management UI (T61e) · wip-ready (not merged): `kd/t61d-company-merge` @545dbe84 |
| 16 | T61e1~e4 personnel-management UI | 중 | spec/personnel-ui(b429bfc0); only e4 adds schema +1 (company commander link) | user decisions Q-P1~P12 |
| 17 | Enter 33 regiment commanders | 하·중 | write favor preferences · unique traits · rank · bio then commanders.json | awaiting user writing, update test Defs.Count |
| 18 | Integrated balance pass | 중(measure ×2) | T12/T15, n≥24, 2 seed ranges, end-line 50% baseline | after golden regeneration; includes AG1 surrender enable · T31c decision |
| 19 | Resume soak | 중(measure) | remaining missions of WATER·AIPLAN alone · seeds 3~8 | after tools/soak-allon merges |
| 20 | Connect artillery-barrel 'artillery delay' (no. 31) | 상 | together with comms alpha piece ③ · gun block mapping | design check needed |
| 21 | Small tool cards | 하·中 | hold the agent query tool (recycling abolished), `--snapbench` precision, harness Gemini usage display | low priority |

## Awaiting user decision (nice to tell before a wave starts)
- TI2 Q1~Q7, T57f FU-5, T61g-3 (rule for wounded vanishing in an annihilated company)·T61g-4, CKf-1 (practice-cell prefix)·CKf-2 (delete/hide old global cell).
- 12 personnel-management UI questions (cleared in 0.3), writing regiment commander rank·favor·unique traits, open translation items (judgment split among 189 new English keys, English names for the 8 dispositions, naturalized-person naming, foreign general naming).
- Legal text check (태극기 flag face width = vertical interpretation), D14 chokefix judgment, 12 regiment commanders hireable state (decided to keep during testing).

## After 0.4 (reference)
AI improvements (T34b~e·T32b/c · difficulty multiplier · surrender expansion), in-game sound effect overhaul, delegation UI improvements, performance (top 5% ticks in scene A · fog on big maps), armored vehicles (timing undecided), music (roadmap only), long-term items such as campaign main story·playtest·tutorial (ROADMAP_LONG).

## Operating notes
- Merge round rules: use `git merge-base --is-ancestor` to tell merged status, compatgate is mandatory for FV·schema merge steps, lock only by the merge role, render pool cap during merge is 2.
- When ordering each agent: one line of model grade·reason, turn cap (about 80 for senior), specify the docs·files to read directly, report within 12 lines. Gemini delegation (flash reading·logs·search, pro doc drafts · 1 coding pilot; results limited).

## Sleep mode (operating rules while the user sleeps; user 2026-10-23 03:1x)
Purpose: while the user sleeps, **do not do work that needs an approval button**, **pile up user decisions**, and **save tokens.**
1. Commands needing approval forbidden: do not delete files·folders·worktrees·branches, force-kill processes, modify settings·global files, or write to external drives (G:/D:). If denied, **do not retry**; write one line 'command·reason' in `ops/APPROVAL_NEEDED.md` and move on.
2. Things needing a user decision pile up as before in each branch's decisions_pending.d fragment and `ops/USER_DECISIONS_QUEUE.md` (one-line items: question·recommendation·impact). Self-made decisions go as one line in `ops/AUTO_DEFAULTS_LOG.md` (only those where the recommended value can be applied). Do not decide new mechanics·rule changes·deletions on your own.
3. Token saving: no polling·periodic checks (wait only for completion notices). Handle notices in 3 lines or fewer and do not view big outputs (if needed, `route.py`/flash summary). Agents **4 or fewer concurrent**, senior (opus) **0~1**, state the turn cap (中 about 60, 上 about 80), specify files to read, report within 12 lines. Do not report at length to the user; summarize once after waking (accumulate in `ops/WAKE_REPORT.md`).
4. Work to run during sleep (only items needing no approval·decision): A3 shape-changed unit re-render (render pool 3), B2 resume, 태극기 merge prep, F1 small-fix reorder (including tests), N3e merge (including conflict resolution; the merge lock is released by the conductor), soak resume. **Not run during sleep**: golden-change bundle (G1~G3, senior·high-risk), TI2 (awaiting questions), personnel-management UI·T61d (awaiting design decisions), regiment commander entry, mid re-render (needs user sheet approval — only make the draft sheet and wait), integrated balance (after golden regeneration).
5. Merge rounds do only steps that do not change FV·schema; on a new FAIL stop and record in `WAKE_REPORT.md` (no auto-fix·relaxation).
