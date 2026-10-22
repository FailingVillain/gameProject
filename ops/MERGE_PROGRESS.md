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
- B6 kd/acting-delay-tier 7cfa0c2 -> efb0850 (ops/QUEUE ours; detcheck.golden restored to HEAD after auto-take of branch golden). detcheck 6c96b4d6c1603324 ac9ae47194d72378 (units=86 shots=3454) = branch's own golden value; golden file stays old -> MISMATCH expected. officer/command/replay/crossing PASS. (snapshot group NOT run at this step - the FAIL below was found later.)
- B7 kd/t25-bayonet-all 29695c3 -> 97472a0 (golden conflict -> HEAD, ops ours). detcheck b7c3761c573cad25 477d7cb573a6d943 (units=112 shots=2890). charge/pursuit/crossing PASS.
- B8 kd/t31a-regroup 97ed026 -> 5a64b1f (ops ours). detcheck unchanged b7c3761c. pursuit2/tactical/crossing PASS.
- B9 kd/t31c-surrender e9cd527 -> 4e11ece (TacticsObs.cs: both env hooks KD_OBS_REJOIN/NOREJOIN + KD_OBS_SURR kept; ops ours). detcheck unchanged b7c3761c. surr PASS.

## STOPPED at B9 (not B10)
FAIL (not in plan): selftest group `snapshot` and `--snaptest` both report `[FAIL] 저장 완전성: World 필드 124개 모두 분류됨 — 분류 안 된 새 필드: actingCache`.
Cause: B6 (T37, kd/acting-delay-tier) adds `readonly Dictionary<Company,(long Tick,bool Has)> actingCache` at src/Core/CommandDelay.cs:64 (partial World); SnapshotSelfTest field classification does not list it (transient cache; cleared in WorldCommands.cs:37). Card owner (T37 / sim) must classify it as transient, on the branch or as a merge-time fix approved by coordinator. Snapshot/snaptest were not run at B6-B8, so first bad point is B6 (efb0850).
Decision for coordinator: (a) fix on kd/acting-delay-tier and re-merge (needs reset of B6-B9, user OK), or (b) add the 1-line classification as a fixup commit here.
Resume point: B10 kd/t10-comms-b @1c91c96 (expected FV 57/22; ENGINE_NOTES, Replay.cs, Snapshot.cs FV lines). Tags: pre-merge-A, post-A, pre-merge-B exist; no post-B yet. HEAD tip see `git log -1 merge/0.2`.
Lock .git/kd-merge.lock released on stop.
- (phase2, merger Sonnet) fixup 4a5d322 classify actingCache transient; snapshot/snaptest/replay/command/officer PASS. NOTE: selftest needs '--selftest' flag with KD_SELFTEST_ONLY (without it the game starts).
- B10 kd/t10-comms-b 1c91c96 -> 578ef29 (Snapshot.cs/Replay.cs from rehearsal be04a5b; ENGINE_NOTES union; ops ours). FV 57/22. detcheck b7c3761c573cad25 unchanged. fieldops/replay/snapshot/savecompat/crossing PASS, snaptest 0 FAIL, uiicons 1 expected FAIL.
- B11 kd/t10b-difficulty 4d74ce9 -> 60be399 (clean). FV 57/22, detcheck b7c3761c unchanged. comms/fieldops/snapshot/crossing PASS, snaptest 0 FAIL, uiicons expected FAIL.
- B12 kd/t16-trench 2cb118c -> c907fa5 (ops/QUEUE ours). FV 57/22, detcheck b7c3761c unchanged. missions/n32/snapshot/crossing PASS, snaptest 0 FAIL.
- B13a kd/t32a-pioneer 26cc9fd -> 6f2d146 (ENGINE_NOTES + SelfTest.cs registration lines union). FV 57/22, detcheck b7c3761c unchanged. pioneer/units/ai3b/tactical/combat4/missions/snapshot/crossing PASS, snaptest 0 FAIL.
- B13b kd/t32d-pioneer-art af357ce (plan said 2118f9b; tip moved) -> 5a9e0bb (ops/QUEUE ours; no src). FV 57/22, detcheck b7c3761c unchanged. pioneer/units/snapshot/crossing PASS, snaptest 0 FAIL. Tag post-B set at 5a9e0bb (before this progress commit). t34a NOT merged (E tail).
- Post-B: uifuzz 30 = 1979 frames, 0 exceptions. Tag post-B placed. pre-merge-C tag = same commit.
- C1 rail 9f513e7 -> c7929fe (Snapshot.cs from rehearsal 16a63bc; ENGINE_NOTES union; ops ours). FV 58/22. detcheck b7c3761c unchanged. terrain/savecompat/snapshot/crossing PASS, snaptest 0 FAIL.
- C2 t28-cover-forest c577c0f -> 6a5f199 (ops ours). FV 58/22, detcheck b7c3761c unchanged. props/forest/snapshot/crossing PASS, snaptest 0 FAIL. Tag post-C = 6a5f199. Group D next (pre-merge-D = post-C).
- D1 t30a-train-move 863f9d1 -> b396991 (GameCommand/Replay/Organisation/Snapshot/ReplaySelfTest from rehearsal 1df3a01; SnapshotSelfTest Audit lines union keeping actingCache fixup; DECISIONS_PENDING union; T30a card theirs (= rehearsal); ops ours). FV 59/23, detcheck b7c3761c unchanged. train/replay/snapshot/crossing PASS, snaptest 0 FAIL.
