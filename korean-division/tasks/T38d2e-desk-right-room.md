# T38d2e Right room (personnel and politics) + final resolution audit
- id T38d2e | role feature | grade D中 H2 (R1 M<=150k Wlight) | status wip-ready (spec)
- blockedBy: T38d2d, kd/t61e-personnel-ui merged | after: -
- hotspots: CampaignHomePersonnel.cs (t61e), CampaignHomeStaff/Promotion/Prisoners.cs, StringsCampaignPersonnel.cs. Serial.
- branch: kd/t38d2e-desk-right | base: after T38d2d
## Confirmed decisions
- 2026-10-22 AY: "좌=기록실·우=인사실, 기존 탭 배치"; tab 17 [인사] lives in the right room (t61e).
- 2026-10-23: baize board, silhouette portraits, sticky notes accepted as-is.
## Do
Spec sec 3.3, 3.5: plates 7/8/9/17 on top; tab 17 = baize board of regiment cards (PersonnelHomeModel unchanged, silhouettes procedural); staff slots from tab 8 data; side sheets and sticky notes data-driven or cosmetic as marked. Keep t61e rule: Esc closes only the open personnel modal. Then run the audit: all rooms x 1024x640, 1280x720, 1920x1080, 2560x1440 sheets.
## Out of scope
New personnel rules, hire/dismiss logic changes, combat HUD, menu/settings sheets (spec Q10).
## Acceptance
1) PersonnelHomeSelfTest unchanged and PASS. 2) Esc/Q/E probes in uifuzz with a personnel modal open: modal first, no room change. 3) uifuzz 30 --fuzz-campaign x3 views 0 exceptions; uiicons, paperui, i18n PASS; savecompat/detcheck unchanged. 4) Audit sheet: no clipping at 4 sizes.
## Tools/baseline
build, KD_CAMPAIGN_VIEW=right:N sheets, run_game.ps1.
## Result
-
