# T38d2a Desk shell: rooms, Q/E nav, layout function
- id T38d2a | role feature | grade D中 H2 (R1 M<=150k Wlight) | status wip-ready (spec) 
- blockedBy: T38d2b | after: T38d2c
- hotspots: src/UI/CampaignHome.cs, CampaignMenu.cs (DrawHomeFrame/DrawViews/DrawTabColumn/DrawScreenStrip), new src/UI/DeskLayout.cs, new test DeskLayoutSelfTest. Serial with c/d/e. Hud.cs untouched.
- branch: kd/t38d2a-desk-shell | base: merge line tip
## Confirmed decisions
- 2026-10-22 AY: "캠페인 홈은 처음부터 사령관 책상 3구역 레이아웃(중앙 책상·좌·우 별도 공간; 이동 방식 등은 기본값(가로 이동, 좌=기록실·우=인사실, 기존 탭 배치)으로 시안부터 진행)"
- 2026-10-23: desk approved; invented mockup elements accepted as-is.
## Do
Per docs/specs/CAMPAIGN_HOME_DESK.md sec 3.1-3.3: pure DeskLayout.Compute(W,H) (grid 1280x720, s=clamp 0.8..1.5), 3 rooms strip, Q/E, edge arrows, bottom nav plates, 180 ms slide, header, ledger strip, shared ModalOpen predicate (Esc, Q/E, click-through). Tab bodies keep old draw inside a sheet rect.
## Out of scope
New tab content, map art, combat HUD, saved state, FV.
## Acceptance
1) DeskLayoutSelfTest: rects inside screen, no overlaps at 1024x640/1280x720/1920x1080/2560x1440 + 200 random sizes >= 1024x640. 2) uifuzz 30 --fuzz-campaign: 0 exceptions, Q/E/Esc during every modal OK. 3) uiicons, paperui, i18n PASS; savecompat, detcheck GOLDEN unchanged. 4) screenshot sheet 3 rooms x 3 sizes (--size, windowed).
## Tools/baseline
build, groups above, run_game.ps1; baseline = merge-line frame time.
## Result
-
