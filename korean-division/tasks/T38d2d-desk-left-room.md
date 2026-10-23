# T38d2d Left room (records room): cork board, ledger, calendar
- id T38d2d | role feature | grade D中 H2 (R1 M<=120k Wlight) | status wip-ready (spec)
- blockedBy: T38d2c | after: T38d2e
- hotspots: CampaignHomeReadonly.cs, Schedule/Records draw (CampaignHome*.cs), StringsCampaignHomeRo.cs. Serial.
- branch: kd/t38d2d-desk-left | base: after T38d2c
## Confirmed decisions
- 2026-10-22 AY: "좌=기록실·우=인사실, 기존 탭 배치" (tabs 11, 12, 14 stay in the left room)
- 2026-10-23: cork board / ledger / calendar shapes accepted as-is.
## Do
Spec sec 3.3, 3.5: skin tabs 11 (cork board, empty-state note: no intel data), 12 (ledger from CampaignHomeViews records; casualty roll and medals = empty-state), 14 (event timeline; calendar grid cosmetic only, no date logic, spec Q3 default). Wall/cork background from the T38d2b kit.
## Out of scope
New intel / casualty roll / medal data, date system, saved state.
## Acceptance
1) Each tab renders with data and with an empty save at 3 sizes without clipping. 2) Cosmetic items carry no state. 3) uifuzz 30 --fuzz-campaign (left views) 0 exceptions; uiicons, paperui, i18n PASS; savecompat/detcheck unchanged.
## Tools/baseline
build, KD_CAMPAIGN_VIEW=left:N sheets.
## Result
-
