# MERGE_PROGRESS_UI (line 2: merge/0.2-ui in C:/gameProject/kd-develop/kd-merge-b)
Base: main d1eaf2e. Lock: .git/kd-merge-b.lock (own; line 1 holds .git/kd-merge.lock). Build: `dotnet build KoreanDivision.csproj -c Release -o bin/merge-b` in korean-division/. Run: `dotnet bin/merge-b/KoreanDivision.dll`.
Heavy gate: `heavy_gate.ps1 -Max 12 -TimeoutMin 15` (no -Priority: line 1 owns the real lock).
Merge method: `git merge --no-ff --no-commit`, ops/* = ours, rehearsal SHA reuse where file identical to rehearsal parent. MERGE_QUEUE.md not touched on this branch (release/line-1 records; avoid conflict).
WARNING: `--fuzz-menu` / `--fuzz-campaign` do not exist before G4/G4b are merged: unknown args open the GAME WINDOW. Use them only after G4 (fuzz-menu) / G4b (fuzz-campaign).

## Done (all detcheck 8aa6448b9690b922 262d0252c35087e2 GOLDEN match; uiicons PASS; crossing PASS at F1)
| step | branch@tip | merge | notes |
|---|---|---|---|
| F1 | gemini-import 63080df | 088ed02 | ops/QUEUE ours |
| F2 | t29b-on-t09 a89b4d2 | 76f8d96 | WorldMission.ScaleAmmo hunk = theirs (permille) |
| F3 | t19a-save-slots 43e69b1 | eff985b | CampaignState/CampaignMenu from rehearsal ad89c8e; GameMission hunk theirs (SaveWithAuto) |
| F4 | t19b2-synced 4980e7b | 0282004 | campaign_economy.json/CampaignEconomy*/CampaignState from rehearsal 45735c5; WorldMission+MissionSelfTest hunks theirs |
| F5 | t19c-home-tabs 9260e36 | 63485c0 | ops only |
| F6 | t19d-doctrines 04a9149 | cac2c96 | ops only; doctrines/officer PASS |
| F7 | t38f-home-readonly-tabs b622d1d | 1257e42 | clean |
| F8 | t42d-schedule-log 06ee9bd | e5fba3a | ops only; Campaign SchemaVersion 7; snaptest QUICK exit 0; uifuzz 30 0 exceptions |
Tags: ui-pre-merge-F, ui-post-F (e5fba3a).

## Group G plan (user-corrected)
G1 t08-ui-bundle 2e9aef5, G2 t17 0eb92e8, G3 t27 4cf5c45, G4 t38g f5a76d4, G4b cherry-pick f126551 only (t38h branch name = t38h-fuzz-campaign), G5 t38b 93ce79f, t38a-settings-keys 7274c71 (skip if ancestor), then t38c-on-ui 85e6658 (NB branch name has no kd/ prefix). t38c-on-ui contains t27,t38g,t38b,t38a already (ancestry) - expect "already up to date" for several.
Then H: t43 kd/t43-branch-symbols d45b18f, t38i-tutorial-link 3e41444 (check wip-ready), T33a-c editor chain (T33d tip 022647f deferred for final combine).

## Group G + part of H (all: detcheck 8aa6448b9690b922 GOLDEN match, uiicons/command PASS, uifuzz 30 + --fuzz-menu + --fuzz-campaign 0 exceptions)
Flag syntax: `--uifuzz 30 --fuzz-menu` and `--uifuzz 30 --fuzz-campaign` (modifiers of --uifuzz, not standalone).
| step | branch@tip | commit | notes |
|---|---|---|---|
| G1 | t08-ui-bundle 2e9aef5 | c1118bc | ops ours |
| G2 | t17-cmd-convenience 0eb92e8 | e138ec1 | |
| G3 | t27-cmd-ui2 4cf5c45 | a7fb263 | |
| G4 | t38g-ui-harness f5a76d4 | 4ca45a7 | |
| G4b | t38h-fuzz-campaign f126551 (cherry-pick only) | 44e8004 | e5f76ec dropped |
| G5 | t38b-help-overlay 93ce79f | ff6ab92 | |
| G6 | t38a-settings-keys 7274c71 (was NOT an ancestor) | 79f81a1 | |
| -- | t38c-on-ui 85e6658 | 16457df | no CONTROLS/UiIcons conflict; crossing+missions+snaptest(QUICK exit 0) PASS |
| G8 | kd/t43-branch-symbols d45b18f | 060b4c8 | |
| -- | t38i-tutorial-link 3e41444 (cherry-pick only; card says wip-ready) | eea5d65 | missions/savecompat PASS |
| H1 | kd/t33a-map-model c5b30f4 / t33b-map-ui 5207b8f / t33c-placement c514821 | d55a0a0 / 3ab7c21 / fa5805a | game code untouched; `dotnet run --project game-tools/map-editor/MapEditor.Tests -- <korean-division root>` pass 604 fail 0 |
Tags: ui-pre-merge-G, ui-post-G (060b4c8... before t38i), ui-pre-merge-H.

## Phase 2 (operator Sonnet; all steps detcheck 8aa6448b9690b922 GOLDEN match, uiicons/missions PASS, uifuzz 30 --fuzz-menu --fuzz-campaign 0 exceptions)
Helper: heavy_gate -Max 12 -TimeoutMin 15; lock .git/kd-merge-b.lock held during phase 2.
| step | branch@tip | commit | notes |
|---|---|---|---|
| 1 G7 | kd/t45-menu-hide 88735b1 | ed4b7f2 | Program.cs arg list: HEAD list + --dev-menu; ops ours; menuhide PASS |
| 2 | t38k-fonts 07cb63f | (see log) | SelfTest.cs both registrations kept; fonts PASS; assets/fonts 15M |

## Not done / next
- G7 kd/t45-menu-hide 88735b1: not in the order given to line 2 -> NOT merged (plan G7, needs menuhide, uifuzz 30, missions).
- T33d (kd/t33d-rail 022647f): final combine only (needs rail/river terrains from line 1). Editor terrain list has no Bridge/Ford/Rail yet (Terrain.cs) - expected until river merges.
- H2 t39i-ruined-houses, H3 spec/* docs not done.
- Final combine of merge/0.2 + merge/0.2-ui is by the commander (campaign-side files overlap with sim: Snapshot/Replay FV lines untouched here).
Resume point: ui-post-H partial; nothing in progress, working tree clean.
