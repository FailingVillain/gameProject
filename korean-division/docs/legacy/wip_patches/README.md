# Unmerged WIP patches (old korean-division / game-tools repos, 2026-10-22)

Each file = `git diff <merge-base main> <branch>` from the old standalone repo (paths relative to the subproject).
Apply from the repo root with `git apply --3way --directory=korean-division <patch>` (game-tools patches: `--directory=game-tools`, in `game-tools/legacy/wip_patches/`).

| branch | branch in gameProject | status at migration |
|---|---|---|
| ai3b2 | wip/ai3b2 | conflict (DECISIONS_PENDING, ENGINE_NOTES, Snapshot.cs) — patch file only |
| battlereport | wip/battlereport | conflict (ENGINE_NOTES) — patch file only |
| bigbldg | wip/bigbldg | applied |
| breach | wip/breach | conflict (detcheck.golden, ENGINE_NOTES, Replay.cs, Snapshot.cs) — patch file only |
| casualty | wip/casualty | applied |
| chokefix | wip/chokefix | applied |
| doctrines | wip/doctrines | applied |
| gemini-import | wip/gemini-import | conflict (Program.cs, CampaignState.cs, MissionSelfTest.cs, CampaignMenu.cs) — patch file only (67 MB) |
| orderq | wip/orderq | conflict (ENGINE_NOTES, GameCommand.cs, Replay.cs, Organisation.cs, Snapshot.cs, World.cs) — patch file only |
| rail | wip/rail | conflict (Snapshot.cs) — patch file only |
| rerender-1 | wip/rerender-1 | applied |
| sfxnew | wip/sfxnew | conflict (DECISIONS_PENDING) — patch file only |
| gt brender | wip/gt-brender | applied |
| gt face-proto | wip/gt-face-proto | applied |
| gt mongol-look | (none) | already identical on main — no branch |
| gt rerender-prep | wip/gt-rerender-prep | applied |
