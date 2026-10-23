# FOLLOWUPS - Follow-up list (conductor memo)

> 2026-10-22 cleanup: the original two blocks were merged·classified. No items deleted, no duplicates (original: C:/works/_backup/FOLLOWUPS_2026-10-22.md).
> Original block titles: '소후속 목록 (지휘자 메모)' / '코드 변경이 필요해 보류한 후속 (한 줄 메모)'

## 1. Check before merge (verification leads)
- [TC-C kd/tcc-inject 3b391cb · Gemini flash 2nd review 2026-10-22, 'leads' before the conductor verifies the code] (1) Expired CmdLock entries remain in the dictionary and keep being included in the checksum (EventCommand.cs:17-18, WorldMission.cs:699) — deterministic so not a mismatch, but clearing expired ones or hashing only valid locks is recommended. (2) Lock tick = MathF.Ceiling(s / Config.SimDt) floating-point division (EventCommand.cs:33) — integer tick conversion recommended. (3) CmdLockedCompany (the whole company if even 1 is locked) vs Resolve (per-unit filter) asymmetry (EventCommand.cs:20-25, WorldCommands.cs:126-133) — a definition of a partially locked company is needed. → check·fix with a small card before merge.
- [Gemini-flagged lead] Whether the M1 replay header Morale array matches the Faction.Id order (explicit .OrderBy(f=>f.Id) recommended at Replay.cs:71) — check before merge.
- [river bundle] Even after applying kd/fix-river-props, 5 river selftest FAILs (golden mismatch·temporary squad·checksum) — existing problem, possibly the same family as the final merge PRE-EXISTING FAIL (squads).

## 2. Order on hold (waiting for a slot)
- [S7 spec not ordered] Design document: map editor follow-up·mission sharing (spec/mapeditor-share) — on hold due to the concurrent agent limit of 20, order when a slot opens.

## 3. Render·display follow-ups (make into cards if needed)
- [r2 follow-up] GameMapTerrain.FiringLineBlock (display only) does not pass waterOpen (differs only when waterBlocksFire=false).
- [PR4 result] Prop render optimization candidates D (Collect grid)·E (far-zoom LOD)·F (LosMul seenBuf hash set) are render-only/low-risk follow-ups — make into cards when needed. Cleanup targets (deletion is the user's call): worktree kd-develop/kd-pr4-measure, folder kd-develop/pr4-out.

## 4. Code change on hold (test infrastructure)
- src/SelfTest.cs registration line conflict (hotspot of 70 commits outside main, union 11 times in the merge log; supervisor C-3/C-7): make test files register themselves with a `[SelfTestGroup("name")]` attribute or a static registration method and have SelfTest collect them by reflection, removing edits to the registration lines (中, sonnet, test infrastructure only·golden-independent; a test preserving bundle names·execution order is needed). Not implemented because code is forbidden in this tool card (tools/pipe-parallel).
