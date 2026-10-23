# VERSIONS - Milestones (user 2026-10-22)

| Version | Content | Condition |
|---|---|---|
| **0.2** | Version **before** the balance patch = feature implementation complete + merge (integration) + baseline·golden regenerated once | Feature card wip-ready → integration rehearsal → merge (user permission) → release |
| **0.3** | Version **after** the balance patch = the balance pass done **in one go** | After the 0.2 merge, started by user decision. Measurement n>=24, 2 seed ranges, end line 50% baseline (kd/endline-50) |
| **0.4** | **AI improvements** | After 0.3. Tactical AI 3C/3D·cavalry flank·AI smoke/barbed wire·expanded surrender·AI difficulty multiplier etc. |

Classification (current cards):
- 0.2 (in progress): combat features (breakthrough·order queue·communication·bayonet·train·**IsAi restoration T35 = infrastructure**), campaign (slots·ammo·home·school), UI (T38), map editor (T33), rivers (T39), re-render (T11)+art follow-ups (T39e~j), T36·T37 fixes, integration rehearsal.
- 0.3 (on hold): all balance items (QUEUE 'balance on hold' section). **AG1 turning on AI surrender (T31c turning on the surrender key) moved to 0.3** (user decision (45), DECISIONS BK; recorded in 0.3 plan Q3, 2026-10-22 night round 3).
- 0.3 (re-report response night of 2026-10-22, added): T61d automatic assignment·company merge UI · officer·equipment handling on merge (Q-F) · artillery barrel 'artillery delay' connection (#31, together with communication alpha piece ③·gun block mapping) · new personnel management UI (T61e candidate).
- 0.3 (art style decision night of 2026-10-22): art style shift to the middle value — comparison sheet → ART_SPEC rules confirmed → saturation·wear expression applied at the final batch re-render. [Confirmed 2026-10-22: mid profile — see ART_SPEC]
  - 0.3 initial: buildings·terrain mid re-render. Confirmed (2026-10-22): includes all buildings·terrain tiles·props·decals (excluding vehicles·trains → unit side, 0.3 late). Time: buildings 1~2 hours · terrain within 1 hour · props within 30 minutes · decals small, total about 3~4 hours of rendering. Prone-firing 4 shards·Gando dress continue now (user decision).
  - 0.3 late: unit re-render
- 0.4 (AI): T34a~e, T32b·T32c (AI smoke·barbed wire), AI difficulty multiplier option A (AE2), AI items after T10b. (T31c turning on the surrender key moved to 0.3 above) **T32b·T34a in progress are finished (default-off key), but the remaining AI cards (T32c, T34b~e) are deferred to 0.4.** (changes if the user corrects)
- 0.4 (sound effects): in-game sound effects overhaul (user night of 2026-10-22).

## Milestone transition procedure (user night of 2026-10-22, round 2)
- Principle: fix bugs at the current level and then move to 0.3. **Only when the user commands** raise the milestone to 0.3 and run the backup procedure. Do not run before the command.
- Procedure (same as the v0.2.1 backup):
  1. Base = merge/0.2-final tip commit (at the time of the command) → annotated tag v0.3.
  2. `git bundle`: tag-only bundle v0.3.bundle + all-branches bundle v0.3-all-branches.bundle → C:\works\_backup\KoreanDivision · D:\KoreanDivisionBackup. The tag-only bundle v0.3.bundle also to G:\내 드라이브\KoreanDivisionBackup.
  3. Update: ops/VERSIONS.md · ops/STATE.md · ops/AUTO_DEFAULTS_LOG.md.
  4. Do not delete·move·rename the backup folders.
- Basis: ops/STATE.md 'milestone v0.2.1 backup complete' (22:30), ops/AUTO_DEFAULTS_LOG.md 2026-10-22 'user command: run 0.2.1 backup'.

### 0.3 baseline scope (user decision night of 2026-10-22, DECISIONS BO)
- Included (only completed·reviewed·test-passed): all of merge N2 · bugfix-1 · bugfix-2 · bugfix-3 · T50c · g3-win-and-delay · ti5-en-draft · b1-import · branch-icons-16 · props-shape-fix · d-train-sprites · ruins-template · ruins-template-ko · t51c · t52b · rv1.
- Excluded (in progress): kd/prone-rerender-* (prone-firing 4 shards) · kd/t39g-gando (Gando dress) · kd/bridge-rework (wooden bridge) · kd/water-foam-fix (shore foam) · ui-lowfix · skill-grade-labels · T51d · T61 · T57 · new ones such as TF-C.
- Excluded (golden change): kd/water-fire-open (waterBlocksFire=false) → merge at '0.3 initial' after the 0.3 tag and regenerate the golden all at once then.
- Promotion timing: after merge N3, full selftest pass + user 'command'. 00:30 is only a target time and there is no rush.

- 0.3 (promoted 2026-10-23 03:29): tag v0.3 = 2531ec16 (merge/0.2-final tip, post-N3f-1 selftest 133 groups PASS + detcheck match, Config 0.2.1, FV Snapshot 74/Replay 27/schema 15). Backup: v0.3.bundle (tag history)·v0.3-all-branches.bundle → C:\works\_backup\KoreanDivision, D:\KoreanDivisionBackup (+ tag only to G:). MANIFEST C:\works\_backup\MANIFEST-v0.3.txt. The promotion script halted at the verify step with a PowerShell error → manual verification done.
