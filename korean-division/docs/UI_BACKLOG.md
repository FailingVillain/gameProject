# UI Improvement Backlog (T38a~g) - 2026-10-22, design/cards only (no code changes)
Basis: ROADMAP_LONG (short-term roadmap 2·3, long-term UI, 'UI check'), code check of branches t08-ui-bundle · t17-cmd-convenience · t27-cmd-ui2 · t19c-home-tabs, docs/CONTROLS.md. Principles: no change to sim/save format (FV unchanged), no adding features the user did not request.

## Branch chain facts
- t08-ui-bundle (paper texture round 1, Paper.cs) <- t17-cmd-convenience (in-mission save · results report), t27-cmd-ui2 (codex · unit list · color-blind) is parallel on top of t08. **Not in main yet (WIP no-merge policy)**: UI cards are stacked on top of t27-cmd-ui2.
- t19c-home-tabs is not a descendant of t08 (no Paper.cs, T19a~b2 stack). The paper skin of the campaign home screen can only be checked after the two are combined -> in T38d the campaign home is split off as a "re-application stage".

## Inventory (present / partial / absent)
| Item | State | Basis | Card |
|---|---|---|---|
| Resolution · window mode · volume settings | present | MainMenu.DrawSettings, UserSettings (settings.json), 5 resolutions | - |
| Color-blind-friendly team colors | present | T27 ⑧ UserSettings.ColorBlind, not applied to minimap/HUD flags | (question Q7) |
| Hotkey rebinding | absent | Commands.Hotkey fixed | T38a |
| In-game help/hotkey overlay | absent | only console help/keys | T38b |
| Bindings = CONTROLS single source | partial | Bindings.All is automatic card keys + manual Extra, no sync test | T38b |
| Command card cell overflow | partial | 3x3 card, without an empty cell rally / barrage type drop out, the combat tab always has rally | T38c |
| Paper texture round 1 (grain + brown tone) | present | t08 Paper.cs, Palette | - |
| Bright paper + dark text | absent | text color specified directly in 334 places (Palette.Text/TextDim/Accent) | T38d |
| Commander's desk background/details | absent | mockup only (ui_paper_mock.png 33eed0f) | T38d |
| Low-resolution layout audit (1024x640) | absent | Config.MinScreen 1024x640, the fuzz only changes size | T38d + T38g |
| Unit list | partial | Hud.DrawBatList exists, no scrolling beyond 18 rows | T38e |
| Codex | partial | CodexMenu only in the main menu, not in combat | T38e |
| Tooltips | partial | present for cards/tabs, no list of missing other HUD elements | T38e |
| Speed control · orders while paused | present | T27 ② closed (Game PlaySpeeds, pending queue) | - |
| In-mission save · results report | present | t17 (BattleAutosave, results report) | - |
| Engagement alerts · minimap display (⑦) | absent | not approved ("deeper discussion later") | no card (Q8) |
| Campaign home 3 screens + MVP 5 tabs | present | t19c | - |
| Campaign home follow-up 10 tabs | absent | placeholders | T38f |
| uifuzz small buttons | limit | T19c result: cannot hit [-][+] | T38g |
| Per-screen screenshot sheets at 3 resolutions | absent | run_game.ps1 only in tools/monitor-launch | T38g |
| Save compatibility policy · crash log | absent | part of the settings/distribution bundle, not UI | out of scope |

## Cards (each <=150k tokens, all sim unchanged)
| Card | Gist | Stack base | Grade |
|---|---|---|---|
| T38b | Help overlay + CONTROLS sync test | t27-cmd-ui2 | 中 S |
| T38a | Hotkey rebinding (settings.json) | T38b | 中 S~M |
| T38c | Command card overflow audit + fix | t27-cmd-ui2 (Hud.cs conflict -> serial after T38b) | 中 S |
| T38e | Unit list scrolling + in-combat codex + tooltips | T38c | 中 S |
| T38d | Paper UI phase 2 (central palette, desk, details, low resolution) | T38e | 中 M (split d1/d2 if large) |
| T38g | UI harness (small button fuzz + sheets) | main + run_game.ps1 | 中 S |
| T38f | Campaign home follow-up tabs read-only | t19c-home-tabs | 中 M |

Proposed execution order: T38g (tool, independent - used for the review of all later cards) -> T38b -> T38a -> T38c -> T38e -> T38d -> (after t19c cleanup) T38f. Since all of them touch Hud.cs, UI cards are not run in parallel with each other (except T38g, T38f).

## Priority proposal
1. T38g - lowers the review cost of later cards. 2. T38c - bug-like because existing features get hidden. 3. T38b -> T38a - required settings before release. 4. T38e. 5. T38d - user approval is given but timing is "after 10-23", and since it is a bulk text color change doing it last minimizes rework. 6. T38f - depends on campaign-side decisions (priority of follow-up tabs).

## Open questions (user decision needed, <=8)
- Q1 Rebinding scope: card keys only? Include camera/global keys? On conflict reject vs swap? Allow entering settings from the Esc menu during combat? (T38a)
- Q2 Help overlay key: F1 conflicts with the area border display - another key (e.g. F2/not H/?)? (T38b)
- Q3 Card overflow solution: rearrange cells / add pages·tabs / move less-used commands by priority? (T38c)
- Q4 Scope of "operations map image improvement" (campaign home map tab only? including art re-creation?) and whether to switch to bright paper on all screens at once? (T38d)
- Q5 Open the codex during combat too (forced pause)? Which key? (T38e, same as the open item of T27)
- Q6 Priority of campaign home follow-up tabs (which first among 3 organization / 5 equipment / 12 records?) (T38f)
- Q7 Apply color-blind markers to the minimap · HUD flags too? Review the marker shape mockup (open item of T27)
- Q8 Whether to approve and card the engagement alerts · minimap display (⑦) (currently not approved -> no card).
