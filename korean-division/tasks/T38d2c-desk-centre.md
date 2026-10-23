# T38d2c Centre desk: map on desk, tab plates, summary sheet, telegram tray
- id T38d2c | role feature | grade D中 H2 (R1 M<=150k Wlight) | status wip-ready (spec)
- blockedBy: T38d2a | after: T38d2d
- hotspots: CampaignMenu.cs (tabs 1/10/13 draw, DrawTabColumn), CampaignMapView.cs, new src/UI/CampaignHomeDesk.cs, StringsCampaignHome*.cs. Serial.
- branch: kd/t38d2c-desk-centre | base: after T38d2a
## Confirmed decisions
- 2026-10-21: "사령관 책상은 나쁘지 않다. 일단은 이정도로 하고, 추후 지도 이미지의 개선 및 세부 사항 수정을 들어가자"
- 2026-10-22 AZ: "작전 지도 탭: 책상 위 지도와 겹침(탭 = 책상 위 지도 자체) + 지도 확대(줌) 기능 추가" (zoom exists since T47: reuse)
## Do
Spec sec 3.2, 3.4, 3.5: map sheet using existing DrawMapView (+zoom), 10 file-index plates (same Tab enum), compact summary sheet (bars from CampaignState; no data -> omit), telegram tray (AvailableEvents), appointment letter + stamps = cosmetic, wide-sheet rule for wide tabs (spec Q1 default).
## Out of scope
Map image art (spec Q4), new data fields, new tabs, equipment/public-sentiment bars.
## Acceptance
1) Every centre tab renders inside the sheet at 1024x640/1280x720/2560x1440 without clipping (sheet). 2) Map zoom/pan/fit and node pick work only on tab 10. 3) uifuzz 30 --fuzz-campaign 0 exceptions, all centre tabs visited. 4) uiicons, paperui, i18n, campaign home selftests PASS; savecompat/detcheck unchanged.
## Tools/baseline
build, KD_CAMPAIGN_VIEW=center:N sheets.
## Result
-
