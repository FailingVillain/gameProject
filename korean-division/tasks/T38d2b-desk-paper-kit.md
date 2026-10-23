# T38d2b Desk paper kit (procedural textures and props)
- id T38d2b | role feature | grade D中 H1 (R1 M<=120k Wlight) | status wip-ready (spec)
- blockedBy: none (first of series) | after: T38d2a
- hotspots: src/UI/Paper.cs, Palette.cs (additive tokens), Ui.cs (additive Ui.TextRot), PaperUiSelfTest. No CampaignHome*.cs.
- branch: kd/t38d2b-paper-kit | base: merge line tip
## Confirmed decisions
- 2026-10-22: paper texture direction; fonts = OFL Nanum (T38k).
- 2026-10-23: invented mockup elements (wood/cork/baize backgrounds, plates, silhouettes) accepted as-is.
## Do
Per docs/specs/CAMPAIGN_HOME_DESK.md sec 4: render-only procedural tiles (wood 256, cork/baize/wall 128-256), 9-slice paper sheet + baked shadow, tape/pin/lamp/keycap/tab-plate/stamp helpers, Ui.TextRot (DrawTextPro). Regenerate only on size change; never use sim RNG. Demo page via an env switch for review (KD_DESK_KIT=1) for a contact sheet.
## Out of scope
Layout, rooms, tab content, PNG assets, Hud.cs, 3D renders.
## Acceptance
1) paperui contrast test covers new tokens (>=4.5:1 for text on paper, OnDark tokens on wood). 2) No texture > 1920x1080; total GPU for kit <= 2 MB; frame cost of a full-screen desk draw <= 1 ms at 1280x720 (measured). 3) Headless selftest does not touch Raylib (no window => no-op). 4) Contact sheet PNG in docs/art_review. 5) detcheck unchanged.
## Tools/baseline
build, paperui, uiicons, detcheck; frame timer.
## Result
-
