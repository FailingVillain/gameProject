# MERGE_PROGRESS - merge/0.2 handoff (merger-1, Sonnet)
Worktree: C:/gameProject/kd-develop/kd-merge (branch merge/0.2, from main f5ebc75). Plan: ops/MERGE_PLAN.md. Lock: .git/kd-merge.lock (held by merger-1 until released).
Checks helper idea: build `dotnet build KoreanDivision.csproj -c Release -o bin/merge`, run `dotnet bin/merge/KoreanDivision.dll`, gate `tools/agent/heavy_gate.ps1 -Priority`.
WARNING: an unknown KD_SELFTEST_ONLY name falls through to the FULL selftest. Verify the name exists in src/SelfTest.cs first.

## Done
- Group A (A1-A6) done, tag pre-merge-A (= main f5ebc75), post-A. detcheck 8aa6448b9690b922 262d0252c35087e2 (ticks=18000 units=20 shots=2530) = golden match. FV 52/Replay 19. uiicons PASS, crossing PASS.
  - A2 conflict: korean-division/tools/agent/AGENT_RULES.md (both added a section) -> union.
  - A3-A6: ops/QUEUE.md conflict -> ours. A5: docs/DECISIONS_PENDING.md -> union (HEAD then sfxnew).
  - A5 has no sfx/audio selftest group (none in SelfTest.cs); only sounds.json validity checked.
