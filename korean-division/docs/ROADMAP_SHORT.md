# Korean Division Short-Term Roadmap — Directives · Progress Order · Queue (read every session, within 50 lines)
> Purpose: order of what to do now · awaiting confirmation · decided short-term items only. Details · long-term · on hold = `docs/ROADMAP_LONG.md` (Grep by item name). Rule: if there is no directive, save here · when over 50 lines, move the oldest item to ROADMAP_LONG.
> State 2026-10-23 ~13:30: version 0.3 reached (tag v0.3 = 2531ec16; 0.4 = AI improvements, ops/VERSIONS.md). Merge line merge/0.2-final tip 6a47f3c8. Verified-finished items moved to LONG 'Done (moved from SHORT 2026-10-23)'.

## Progress order
1. Merge round MERGE-A (running): ui-log-transparent merged (6a47f3c8). Pending, in order: t57f-override-header be1d5a7f (T57→T57c→T57f) · review-followups-small a10c6055 · f3-followups 9b2eeed5 · t61d-company-merge 545dbe84 · ti2-p0 217207d0. compatgate mandatory on FV/schema steps; report results in ops/MERGE_PROGRESS.md
2. Golden bundle (上, one window): G1 kd/water-fire-open 6142cb8c (not started) + NEW-3 kd/new3-prone-drag 6c8a7ae7 + NEW-4 kd/new4-choke-queue a7636dad (chokeEnabled approved 2026-10-23; choke + N33/N51 jitter) → regenerate goldens once (detcheck now 672804207fc4e486 3c42aea49324248e)
3. Personnel line: T61d (in MERGE-A; 12:1x decisions: annihilated-company re-form stays manual · supply-reserve setting for auto recruits · PersonnelLog held) → T61e personnel UI (kd/t61e-personnel-ui 12cc70f1, WIP, stacked on T61d 714da8f2; P1~P12 decided)
4. Art A1~A5: taegukgi (gt/taegukgi-detail 040cfd87) + B2 re-render (kd/b2-deferred-units-2 8340da71) + A3 shape-changed re-render (kd/a3-rerender) → A4 mid buildings·terrain·props·decals (sheet approval) → A5 unit colour draft
5. Integrated balance pass after goldens (T12/T15, n>=24): casualty speed tuning (中~上: long-range hit ×0.5 · rifle close ×0.85 · company retreat 30~40%, #23) · AG1 surrender enable · 9 combat problems re-report (#22; soak resumed 12:50, ops/SOAK_2026-10-23.md)
6. Tactical AI: stage 3A/3B/3B2 merged; mass test = soak above; 3C/3D and the rest = 0.4
7. GitHub batch only on the user's command (see Waiting)

## In progress · awaiting confirmation
- Agents in flight: T61e (kd/t61e-personnel-ui 12cc70f1) · A3 re-render (kd/a3-rerender, tip f979e23f = base, no own commit yet) · B2 re-render (kd/b2-deferred-units-2 8340da71; horse-artillery edge-contact 8 cells fix decided) · taegukgi (gt/taegukgi-detail 040cfd87, v2 approved by the user; merges with the Korean standard-bearer re-render) · soak (measure)
- Awaiting user decisions: ops/USER_DECISIONS_QUEUE.md items 8~16 (33 regiment commanders, translation open items, flag law text, held branches, 0.2.1 play notes, NEW-2 LOD0 not reproducible)
- Quickstep walking motion (quickstep) — awaiting confirmation (sheet only, no branch; unverified)
- armour · 쇼샤 re-render sheet + Austin team-color marker (austin_fix3.png) — (unverified: ART_STATE 'unknown'), then trench width 2x (trenchvis)
- mongol-look sheet 18 — confirm Mongol cavalry pointed hat · waist belt (look merged as lookfix; user review U03 pending)
- Art · UI awaiting confirmation (unverified): missing_theme_units · surrender_pose (discard candidates) · new_units (post-hoc review) · progress bar · march-fire muzzle flash (T39e c837eb87, needs a game-side MarchMuzzle fix = user decision) · 4 UI checks (details: LONG)

## Decided · short-term queue
- Engineers (上): assault troops also take the engineer role — (unverified; pioneers + wire cutting are done)
- Command conveniences ①~⑥ (上) — remaining: speed 0.5~3x · unit list panel · etc. (unverified; ①⑤ and order queue are done) (details: LONG)
- Regiment selection in combat (中~上) — number = regiment, Alt+number = battalion (rule decision 9 option 1), move the existing Alt+number assignment key (unverified, details: LONG)
- Campaign rule decisions 1~15 — supply limit · 25% of casualties return as wounded · defeat retry / branching · save slots (unverified; T19 split T19a~d, details: LONG)
- Non-combat review, 6 bundles (中~下) — controls · info · settings · performance · sound · campaign foundation (unverified, details: LONG)
- Move repository (上) — repo now at C:/gameProject; deleting the old folders is blocked on U02 (ops/QUEUE.md T20) (details: LONG)

## Waiting · on hold · later (decided 2026-10-22)
- [Decided 2026-10-22] Re-report while waiting: assault reserve time (N47) · bayonet-fix halt range (N57) (details: LONG)
- [Decided 2026-10-22] On hold: faction addition candidates (Red Army / Far Eastern Republic)
- [Decided 2026-10-22] Later (user proceeds): unit description input · episode design
- [To do later 2026-10-22] **GitHub batch**: ① get the URL of the new private repository on the seungwoo3656 account → update the game-tools internal bare (`C:/works/_git/game-tools.git`, 1-commit snapshot) to latest, then push ② decide whether to start korean-division fresh in the new repository too (origin push currently blocked) ③ allow/directly install the git hook that rejects commits with legacy addresses ④ clean up GitHub account email list · check Education impact (user). Details = `docs/DECISIONS_PENDING.md`, `docs/HANDOFF.md` 'legacy email handling'
- [Decided 2026-10-22] Later (user proceeds): campaign favor · morale — increment values (+1/+2/+3/+5) and sinks (thresholds · effects) (details: campaign/spec_c1e1.txt R11, 캠페인 디자인 v1.txt §28)
- [Decided 2026-10-23] Held designs: prisoners·loot · auto-pause · order cancel · checkpoint (UNDO-b/c · TPOW-c · CK3) · delegation UI improvement (0.4)
