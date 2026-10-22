# MERGE_PROGRESS - merge/0.2 handoff (merger-1, Sonnet)
Worktree: C:/gameProject/kd-develop/kd-merge (branch merge/0.2, from main f5ebc75). Plan: ops/MERGE_PLAN.md. Lock: .git/kd-merge.lock (held by merger-1 until released).
Checks helper idea: build `dotnet build KoreanDivision.csproj -c Release -o bin/merge`, run `dotnet bin/merge/KoreanDivision.dll`, gate `tools/agent/heavy_gate.ps1 -Priority`.
WARNING: an unknown KD_SELFTEST_ONLY name falls through to the FULL selftest. Verify the name exists in src/SelfTest.cs first.

## Done
- Group A (A1-A6) done, tag pre-merge-A (= main f5ebc75), post-A. detcheck 8aa6448b9690b922 262d0252c35087e2 (ticks=18000 units=20 shots=2530) = golden match. FV 52/Replay 19. uiicons PASS, crossing PASS.
  - A2 conflict: korean-division/tools/agent/AGENT_RULES.md (both added a section) -> union.
  - A3-A6: ops/QUEUE.md conflict -> ours. A5: docs/DECISIONS_PENDING.md -> union (HEAD then sfxnew).
  - A5 has no sfx/audio selftest group (none in SelfTest.cs); only sounds.json validity checked.
- B1 wip/breach2 6ad6c50 -> ffc4129 (only ops/QUEUE.md conflict, ours). FV 53/20. detcheck 19501757040f0d62 d62e14463121dd02 (golden file matches because branch golden came along; plan expects this). breach/snapshot/replay/crossing PASS, snaptest OK (81, 0 FAIL). uiicons FAIL x1 ('card slots / hotkey conflict', expected).
- B2 (t38c-on-ui): DEFERRED to final combine (coordinator: UI chain on second line merge/0.2-ui). uiicons FAIL stays until then.
- B3 wip/orderq2 0377f8a -> d1947b0 (ops/QUEUE ours). FV 54/21. detcheck 19501757040f0d62. orderq/firectl/replay/command PASS, snaptest OK.
- B4 ai3b2 f74a675 -> 7d5c124 (ops/QUEUE ours; docs/DECISIONS_PENDING.md union - unplanned but doc-only). FV 55/21. detcheck 19501757040f0d62. ai3b/snapshot PASS, snaptest OK.
- B5 kd/t24-s1s3 07ac80c -> 89ca250 (ENGINE_NOTES.md taken from rehearsal 53b7c9a because HEAD's copy equaled rehearsal parent). FV 55/21. detcheck ccfbd1ddab1c1dfe beefbe5c575e571b (units=156 shots=5318) golden match. cavcmd/command/drill/officer PASS. crossing FAIL x2 (expected until B5b).
- B5b kd/t24-crossing-fix 390763a -> 21085f4 (clean). detcheck unchanged ccfbd1dd. crossing/tactical/officer/command PASS.
- uiicons: single FAIL 'card slots / hotkey conflict (1431)' persists in every step since B1 (expected; fixed only by B2 at final combine).
- Failures seen: none unexpected. Tags: pre-merge-A, post-A, pre-merge-B (before B1).
