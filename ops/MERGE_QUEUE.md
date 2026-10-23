# MERGE_QUEUE - wip-ready branch order + version number reservations (updated 2026-10-22)

Rule: the merger is **always on** — if there is even 1 `wip-ready` card, the conductor launches the merger and it processes in this order serially. The lock (`.git/kd-merge.lock`) is held only by the merger (expires in 30 minutes, docs/procedures/merge-procedure.md). Verification right before merging = build · detcheck · own bundle · snaptest; the full selftest is once at release (T13).
Who writes: merger (state · completed table · number confirmation) · conductor (adds wip-ready rows) · release (one line closing the queue).

## Current main values (code constants; re-read at every merge)
- Snapshot.cs `FormatVersion = 52` · Replay.cs `FormatVersion = 19` (both comms ①). main hash: `git -C C:/gameProject log -1 --format=%h main`.

## Merge completed table (what went into main)
Rule: before starting, investigation·measurement·spec cards grep this table + `docs/legacy/GIT_HISTORY_DIGEST.md` to check whether it is already implemented. 'Snapshot' = included in the first commit a1a35e6.
| Hash | Branch | What went in |
|---|---|---|
| snapshot | assaultinf | assault infantry buff (FV49) |
| snapshot | horseart | horse artillery (tachanka wagon + infantry gun) |
| snapshot | savecompat | save format compatibility policy |
| snapshot | arty3 | artillery·heavy weapons bundle (FV50, Replay18) |
| snapshot | tachanka | Korean army tachanka check + sheet |
| snapshot | newunits | 4 new branches (hunter·bandit shotgunner·draft bandit·militia) |
| snapshot | tutorial | tutorial chapter 0, 0-1 · 0-2 |
| snapshot | lookfix | Mongol/bandit look |
| snapshot | formation | formation relaxation (FV51; choke·skirmish line off) |
| snapshot | comms | communication alpha ① (FV52, Replay19) |
| snapshot | trenchdiag | trench_assault · S1~S3 diagnostic document |
| 8d22ad5 | wip/bigbldg | building size increase (HouseBlocks render) |
| ba696b7 | gate-lanes | heavy_gate 3 lanes (disabled by default) |
| 53712e9 · 4d19119 | - | tool cleanup · decision ledger D01~D22 |
| b5918a8 · a4c79c7 | docs-ops · docs-roles | document overhaul (ops/tasks, charter/roles/procedures) |
| 562e578 | docs-fable | PIPELINE v1 · STATE/QUEUE updated · T13 |
| bfe5641 | fix/n12-forest | fixed 4 stale tests (only 3 files of N12/Forest/GapFill tests; user-approved exception 2026-10-22) |
| a8552f9 | tools/fv-renumber | fv_renumber.ps1 (merge/0.2 A1) · FV 52/19 · detcheck 8aa6448b9690b922 |
| 9b2cbff | tools/monitor-launch | run_game.ps1 (A2) · AGENT_RULES section union · FV 52/19 |
| 5d8149f | kd/crash-log | T41 crash log (A3) · FV 52/19 · detcheck 8aa6448b9690b922 · crashlog PASS · uifuzz 30 0 cases |
| 32180a0 | kd/endline-50 | end line 70→50 mission json (A4) · FV 52/19 · detcheck 8aa6448b9690b922 · missions PASS |
| a0611ad | sfxnew | T21 sound effects (A5) · FV 52/19 · detcheck 8aa6448b9690b922 · sounds.json valid |
| 2231459 · abbe77f | gt-face-proto · t30d-train-art | T22 face mockup · T30d train art (A6, no src change) · FV 52/19 · detcheck 8aa6448b9690b922 |
| ffc4129 | wip/breach2 | T03 breakthrough (B1) · FV 53/20 · detcheck 19501757040f0d62 · breach/snapshot/replay/crossing PASS · snaptest OK · uiicons FAIL 1 case (normal, card cell) |
| d1947b0 | wip/orderq2 | T04 order queue · fire line (B3) · FV 54/21 · detcheck 19501757040f0d62 · orderq/firectl/replay/command/crossing PASS · snaptest OK · uiicons FAIL 1 case (normal) |
| 7d5c124 | ai3b2 | T05 AI blackboard 3B (B4) · FV 55/21 · detcheck 19501757040f0d62 · ai3b/snapshot/crossing PASS · snaptest OK · uiicons FAIL 1 case (normal) |
| 89ca250 | kd/t24-s1s3 | T24 D08 ①②③ (B5) · FV 55/21 · detcheck ccfbd1ddab1c1dfe · cavcmd/command/drill/officer PASS · snaptest OK · crossing FAIL 2 cases (normal before B5b) · uiicons FAIL 1 case (normal) |
| 21085f4 | kd/t24-crossing-fix | crossing stage observer keep=0 (B5b) · FV 55/21 · detcheck ccfbd1ddab1c1dfe unchanged · crossing/tactical/officer/command PASS · uiicons FAIL 1 case (normal) |
| efb0850 | kd/acting-delay-tier | T37 acting NCO delay tier (B6) · FV 55/21 · detcheck 6c96b4d6c1603324 ac9ae47194d72378 (units=86 shots=3454; the golden file stays at HEAD so MISMATCH is normal, not regenerated) · officer/command/replay/crossing PASS |
| 97472a0 | kd/t25-bayonet-all | T25 bayonet for all (B7) · FV 55/21 · detcheck b7c3761c573cad25 477d7cb573a6d943 (units=112 shots=2890; differs from rehearsal bef5ffb0 because T37 is included, record only) · charge/pursuit/crossing PASS |
| 5a64b1f | kd/t31a-regroup | T31a regroup limit (B8, default off) · FV 55/21 · detcheck b7c3761c573cad25 kept · pursuit2/tactical/crossing PASS |
| 4e11ece | kd/t31c-surrender | T31c AI surrender (B9, default off) · FV 55/21 · detcheck b7c3761c573cad25 kept · surr/crossing PASS · **snapshot bundle · snaptest FAIL 1 case: 'all World fields classified — new unclassified field: actingCache' (cause B6 T37, reported)** |

**Queue closed (release, 2026-10-22)**: fix/n12-forest queue — full selftest 2008 OK/FAIL 0 · detcheck 8 variants identical · snaptest OK · dist published. (T13 of the T03~T05 queue is separate, still queued.)

## Merge order (only those that became wip-ready are processed; planned order)
| Order | Branch | Card | R | Status | Note |
|---|---|---|---|---|---|
| 1 | wip-resolved/breach | T03 | R3 | restored, awaiting verification | FV53/R20 applied version, wip-ready after reviewer |
| 2 | gemini-import (restored) | T01 | R2 | patch kept | save transaction first |
| 3 | orderq (restored) | T04 | R3 | patch kept | after breach |
| 4 | ai3b2 (restored) | T05 | R3 | patch kept | last; the full selftest is at release |
| later | rail (restored) | T07 | R2 | patch kept | no prerequisite |
| later | wip/casualty | T06 | R3 | branch | after D19 |
| later | chokefix · battlereport · doctrines · sfxnew | -/T17/T19/- | - | branch·patch | restored when the card is ordered; chokefix is D14 |
| non-merge | rerender-1 · gt-brender · gt-rerender-prep · gt-face-proto | T11 | R1 | game-tools | 3 render cells, after sheet review |

## Version number reservation table (confirmed by the merger at merge time; on collision the earlier in merge order goes first. Golden regeneration once for the whole queue)
| Branch | Snapshot reservation | Replay reservation | Change | Confirmed |
|---|---|---|---|---|
| breach | 53 | 20 | smoke · barbed wire cutting (applied in wip-resolved/breach) | unconfirmed |
| orderq | 54 | 21 | order queue / fire line | unconfirmed |
| ai3b2 | 55 | - | AI blackboard 3B (46 is the old 3B, re-check) | unconfirmed |
| rail | 56 | - | railway terrain (map serialization) | unconfirmed |
| casualty · gemini-import | needs checking | needs checking | mostly values / campaign schemaVersion is separate | unconfirmed |
