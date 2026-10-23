# DISPATCH_TAIL - common tail of order prompts (single source; PIPELINE 1-d points here)
Version: 2026-10-23 v1 (from PIPELINE 1-d v2.1). Do not write limit numbers here - numbers live only in ops/LIMITS.md.

## required fields (append to the end of every order)
1. Base branch and commit hash.
2. FV: if raising, claim with `fv_claim` (temporary value = base+1); if not, write `none`.
3. Hotspot file list.
4. Preflight: paste the last line of `preflight.ps1 <card id>` (`PREFLIGHT OK <id> (n warning(s))`) into the prompt and into the report.

## common bans
- No push. No modifying main or merge lines (merge/*). No modifying ops/ on work branches (run `queue_guard`).
- Shared docs go through fragment files (`docs/decisions_pending.d/<branch>.md`, `docs/engine_notes.d/<branch>.md`).
- Non-art heavy jobs (build, selftest, measurement) run sequentially inside the agent through heavy_gate. Web lookups via `hub ask -w`.

## report
- Within the role card's line count (the conductor's value if set). Written in Korean.
- Last line "임의 결정:" = every value, assumption, interpretation, deletion or test relaxation the card did not decide ("없음" if none).

## inline
(6-line condensed form for generated prompts)
- No push; no ops/ edits on work branches (queue_guard); no main/merge/* edits.
- Fragments via docs/decisions_pending.d and docs/engine_notes.d.
- Logs over 150 lines go through Gemini flash.
- WIP-commit and stop before ~200k tokens.
- Report within the card's line cap, Korean.
- Last report line "임의 결정:" (or "없음").

## role:feature
null/NaN/range validation in JSON Load/Validate and grep consumers when changing a flag; distinguish stale tests from wrong code (no weakening). New button gets a selftest; new mission gets a suite iterating `MissionDef.Available()` (N77, N12, Props, Terrain, missions). n=8 is a behavior check only (no win-rate judgment); heap after forced GC.

## role:sim
Same validation rule as feature. Adding ObjCodec/save fields is FV+1 even if default-off; measurement-only statistics leave FV unchanged.

## role:art
Check cell boundaries of poses with `edge_check`.

## role:reviewer
No checkout. Adding save fields is FV+1 even if default-off.

## role:measure
n=8 is a behavior check only (no win-rate judgment); heap after forced GC.

## role:merger
Use only `merge_step.ps1` (snapshot and `--snaptest` enforced).

## event / campaign cards
When tying an event trigger to a player choice (popup response, etc.), put that choice in as a `GameCommand` (recorded command): the event condition is sim state and every input that changes state must be a recorded command so it re-fires on the same tick in replay. Commands executed directly by the event (injection) are not put in the command log (double execution). The evtcmd test is the evidence.
