# PIPELINE_ROADMAP - Pipeline improvement roadmap (user approved 2026-10-22)

## Done
- 3 tools (branch tools/pipe-tools 7ddd32c): merge_step.ps1 · preflight.ps1 · state_snapshot.ps1. Run with `powershell -ExecutionPolicy Bypass -File`.
- STATE unification rule · review checklist (FV/save fields/snapshot classification) · LESSONS 31.
- Art sub-conductor #1 running (ops/ART_STATE.md). Limits: art render 8 (start 6) + non-art heavy 3 (LIMITS.md).
- Additionally done 2026-10-22 (integrated into branch tools/pipeline-v2, reflected in main at the merge round): parallel processing tools (fv_claim/fv_assign · assemble_notes · queue_guard) · card id/roadmap checks (card_id · roadmap_check) · watchdog/stack_depth · gate v2 (pool separation·build slot·wait log) · self-sufficient publish.
- Documents: LESSONS 12 lines + LESSONS_ARCHIVE, PIPELINE v2.1 (ordering·merge procedure, wave 2 checklist), ROLE_CARD_PATCHES (awaiting application by the conductor).

## Wave 2 introduction (user approved) — reflected (2026-10-22, PIPELINE.md 'sub-conductors' section)
1. **3 sub-conductor areas: art · campaign · merge.**
   - Art: running. Campaign: appointed at the start of wave 2 (C1E1 cards·mission JSON·UI). Merge: merger always on (= merge sub-conductor; in charge of the final merge).
   - Rules: order only within own pool limit · QUEUE/STATE are modified only by the top-level conductor (sub-conductors only their dedicated state files ART_STATE/CAMPAIGN_STATE) · a new agent per wave · final report ≤15 lines · sim (H3) is managed serially by the top level without a sub-conductor.
2. **Supervisor (opus, read-only): default cycle 60 minutes** (changes on a separate user command).
   - Job: detect token usage patterns·leaks, duplicate orders, stalled agents, QUEUE↔actual state mismatches, omissions in determinism·save format·FV·snapshot classification of merged branches.
   - Output: append to ops/WATCH_REPORT.md (numbers mandatory, not recorded without numbers; format per the watch/LEAKS.md rules).
   - Cost control: ≤60k tokens per cycle — read only the state_snapshot output + the diff since the last cycle. Skip idle time with no work. No code modification.
   - First run: start of wave 2 (including 1 precise check right before the final merge).

## Remaining improvements (in order)
- **[Gate v2 code complete (gate-v2 f482e7b, merged into pipeline-v2). Remaining = install in main (merge round) + wrap up the T44 smoke]** Gate: reserve a build-only slot + wait-time log + fix so that render processes that only wait (-Render waiters) cannot hold pool slots. Basis: the T44 agent's heavy_gate stalled for 20+ minutes because of kd-rerender waiting processes (LESSONS 25 recurrence). Finish running the T44 smoke after the gate fix (kd-t44, tools/smoke_missions.ps1).
- Stack depth ≤3 · FV late binding: tools·rules documentation complete (PIPELINE v2.1). Remaining = reflect tools in main + real use on the merge line.
- Stall detection watchdog: implementation complete (c15-watchdog). Remaining = reflect in main.
- Bulk reconciliation of QUEUE states (at the end of wave 1; stale rows confirmed = T38e T38h T49b T49c).
- r2 follow-up: GameMapTerrain.FiringLineBlock (display only) does not pass waterOpen — differs only when waterBlocksFire=false; confirm with the river owner (wave 2 small card)
- Remaining (merge round after the final merge): reflect tool branches (pipe-tools · gate-v2 · c15-watchdog · selftest-parallel · fv-renumber · pipeline-v2) in main, install gate v2, integrate tools/c14-nightly, apply role card patches (conductor), run the wave 2 checklist (PIPELINE v2.1 (i)).
- Undecided (user): concurrency cap · reducing management devices · session replacement rule · always-on supervisor (PIPELINE.md 'undecided' section).
