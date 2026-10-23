# FV_RESERVATIONS - Save format (FormatVersion)·Replay number reservation table (for the WIP stack, 2026-10-22)

> **2026-10-22 directive (tools/pipe-parallel): this table is no longer updated manually.** Branches claim in `ops/fv_claims/<branch>.txt` (`fv_claim.ps1`), the current reservation status is computed by `fv_assign.ps1 -Report`, and the final number per merge order by `fv_assign.ps1 -Order ...` from the claim files. The table and corrections below remain only as history from before 2026-10-22 (including stale values such as TC-G=68).

> Reason: while WIP is kept as a branch stack without merging, numbers collide (rail·breach2 both 53). A branch that raises the number writes it here first (managed by the conductor). main = FV52 / Replay19.

| Branch | FV | Replay | Base | Status | Memo |
|---|---|---|---|---|---|
| wip/breach2 (T03) | 53 | 20 | main | wip-ready | |
| wip/orderq2 (T04) | 54 | 21 | wip/breach2 | wip-ready | |
| rail (T07) | 53 → renumbered on merge/stack (e.g. 58) | - | main | wip-ready | same-line conflict with breach2; Rail adds a byte at the end of the enum |
| T05 ai3b2 | 55 | - | wip/orderq2 | wip-ready (f74a675) | no new command |
| T24 S1~S3 | 56 or later | 22 or later | wip/orderq2 | in progress | may conflict with Replay 22 of T10 |
| kd/t10-comms-b (T10) | 57 | 22 | wip/breach2 | wip-ready | stacked on breach2 so it skips 54~56 → renumber when merging |
| t30a-train-move (T30a) | 58 | 23 | rail | wip-ready (863f9d1) | train object·track movement |
| t30b-train-weapons (T30b) | 59 | 23 | t30a | wip-ready (ca1cdb8) | train save location ORG_ end→ENTS end, entity reference tag 3 |
| t30c-freight-objective (T30c) | 59 kept | 23 kept | t30b | wip-ready (f5fa49a) | no new state (ObjProgress reused) |
| gemini-import / t09 / t29 / T19a~d | needs checking | needs checking | gemini-import | - | campaign save schema is separate (SchemaVersion 2); sim FV not raised |
| sfxnew · rerender · face-proto · t27 · t28 · t17 · t08 | no change | - | - | - | presentation/settings/options only |

Rule: when merging (after the user's directive) renumber the single lines (Snapshot.cs:83 FormatVersion, Replay.cs:22) sequentially in the table order above and regenerate the golden.

Memo: t29b-on-t09 replaces t09-camp-slice and t29-campaign-economy-json (alone on merge). The campaign save schema is SchemaVersion 2, unrelated to the sim FV.

Candidates (not ordered): including IsAi in the replay header·snapshot (AF1) FV+1/Replay+1 · AI difficulty multiplier option A (AE2) FV+1/Replay+1 — write the number in this table first when ordering.

Reservation (2026-10-22 13:10, ordered with user approval): IsAi replay header·snapshot restoration (AF1) = FV 60 / Replay 24, base main (after the breach2 family in the merge order). AI difficulty multiplier option A (AE2) is planned as FV 61 / Replay 25 after AF1.

Reservation candidates (card assumptions, 13:1x): T32b AI smoke FV 62 · T32c AI barbed wire FV 63 (both only when new AI save state is needed; no Replay change). T35 (IsAi) 60/24, renumbering needed before/after AI multiplier option A 61/25.
Reservation candidate: T34c TacRegiment 1 field = FV 64 (no Replay change), base ai3b2 stack.
Reservation candidate: T39a river Ford = rail+1 → this branch FV 54 (renumbered on merge), T39b·c unchanged when there is no new state.
river-r1 (T39a) FV 54 / no Replay change, base rail, wip-ready 5b7b9f1. river-r2·r3 unchanged when there is no new state.

**Correction (2026-10-22 14:4x, integration rehearsal measured):** merging in the merge order, the actual numbers are breach2 53/R20 · orderq2 54/R21 · ai3b2 55 · t10 57/R22 · rail 58 · t30a 59/R23 · t30b 60. Therefore IsAi restoration (AF1, T35) = **61 / Replay 24**, AI difficulty multiplier (AE2) = **62 / Replay 25**, T32b/T32c after 63/64 (if needed). Renumber with tools/fv-renumber right before merging (the golden does not change from FV alone, checksum changes are only at three places breach2→t24→t25 → new golden bef5ffb0 822cdacd, regenerate on user directive).

**T39d bridge (river-r4-bridge 9d4b7be, on top of river-r3, 2026-10-22 15:0x):** adding terrain `Bridge` (byte 13) gives FormatVersion 55 within the branch (on top of river-r1 54), Replay unchanged. On merge the river stack (rail→r1→r2→r3→r4) needs renumbering after rail 58·t30a 59·t30b 60·IsAi 61·AE2 62 → candidate 63. Bridge must also be added to the terrain list of the map editor (T33) (passed to T33c/T33d).

**T34a (kd/t34a-suppress 29c2479, on top of ai3b2):** adding 7 TacticsProfile fields gives FV 55→56 within the branch (T32b is also 56; the two branches are independent stacks → both renumbered on merge: T32b·T34a both 63 or later after AE2 (62)). Replay unchanged (21).

**C1E1 reservation (2026-10-22 16:2x, episode 1 implementation chain):** campaign schema — the T48 chain uses v8 (T48a)·v9 (T48b1)·v10 (T48b2) if needed, so C1E1 **starts from v11 in chain order**: TC-D=v11, TC-E=v12, TC-F=v13, if g1/TC-A/TC-C use additional fields v14 or later (1 each in card order; numbers finally sorted at merge). sim FormatVersion — after the merge plan numbers (rail 58, t30a 59, t30b 60, IsAi 61, AE2 62, river 63, bridge 64, T32b 65, T34a 66, T48b1 67) **TC-G=68, TC-C=69** (only when needed; Replay unchanged by default) **[Correction 2026-10-22 — stale value]:** the current merge-basis sim FormatVersion is **71** (basis: `git -C C:/gameProject/kd-develop/kd-final show merge/0.2-final:korean-division/src/Core/Snapshot/Snapshot.cs` line 83 `FormatVersion = 71`, commit 14e84417 "L13: T48e … FormatVersion 71"; ops/MERGE_PROGRESS.md L13 "FV 68->71"). Therefore 68·69 are already within the merged number range (used up to 71) and cannot be used for TC-G/TC-C. TC-G/TC-C have no merge record in MERGE_PROGRESS so they are unmerged. The next candidate is 72 or later and the final number awaits D7. Agents write their own number in this table and sort with fv_renumber on merge.

**TC-E (kd/tce-vars, on top of TC-D v11, 2026-10-22 17:xx):** campaign schema **v13** (CampaignState.Decisions added). The reservation table had TC-E=v12 but g1 (kd/c1e1-g1-episode-graph bd1dc11) already uses v12 → TC-E is one slot later at 13 (v12 belongs to g1, finally sorted by order at merge). No sim FormatVersion·Replay change (campaign layer only). TC-F is v14 or later.
