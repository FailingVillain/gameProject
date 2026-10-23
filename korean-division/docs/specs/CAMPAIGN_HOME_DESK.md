# Campaign Home "Commander's Desk" - implementation spec (T38d2a-e)
Date 2026-10-23 | author: spec (sonnet) | status: draft for the conductor, no code touched | merge line read: kd-final tip 0dfe2bfc; personnel tab read from kd/t61e-personnel-ui 1bac7fe8.

## 0. Inputs and confirmed decisions (unchangeable)
- 2026-10-21 (T38d card): "사령관 책상은 나쁘지 않다. 일단은 이정도로 하고, 추후 지도 이미지의 개선 및 세부 사항 수정을 들어가자"
- 2026-10-22 AY (DECISIONS_ARCHIVE): "캠페인 홈은 처음부터 사령관 책상 3구역 레이아웃(중앙 책상·좌·우 별도 공간; 이동 방식 등은 기본값(가로 이동, 좌=기록실·우=인사실, 기존 탭 배치)으로 시안부터 진행)"
- 2026-10-22 AZ: "작전 지도 탭: 책상 위 지도와 겹침(탭 = 책상 위 지도 자체) + 지도 확대(줌) 기능 추가"
- 2026-10-23 (conductor message): implement the desk; invented mockup elements (room backgrounds, cork board, ledger/board shapes, nav plates, names/numbers, silhouette portraits) are accepted as-is for now.
- Design source: docs/CAMPAIGN_HOME_DESIGN.md section 5 (paper, 9-slice + noise, Myeongjo titles, typewriter numbers), section 11-1 (centre/left/right tab placement).
- Not finished work: T38d (palette: bright paper + dark ink) is already in the merge line (Palette.cs tokens, Panel/PanelDark/TextOnDark...). T47 map zoom exists (CampaignMapView.cs + MapZoomView). T38f read-only tabs, T42b/c/d, T52e difficulty, T49a slots exist. GIT_HISTORY_DIGEST/MERGE_QUEUE contain no desk-layout work (grep desk/paper/T38d2: none). So this spec covers ONLY the layout/skin/navigation shell, not new tab content.

## 1. Fonts (checked in Ui.cs, kd-final)
Ui.cs `BundledFile` (lines 18-20) = NanumGothic-Regular (Body), NanumMyeongjo-Bold (Heading), NanumGothicCoding-Regular (Numeric); `AllowSystemFonts` (malgun) is only a fallback when the bundle is missing (KD_FONT_NO_SYSTEM=1 disables). Confirmed: the game no longer depends on Windows fonts.
Does it reproduce the mockup look? Mockup = HANBatang (serif title) + Courier New (typewriter). Heading = Myeongjo Bold: yes, close serif look. Body = NanumGothic: mockup body was also a sans gothic: yes. Numbers = Gothic Coding: monospaced and aligned, but NOT a typewriter look (no ink bleed, FONTS.md says so); the paper grain/stamp ink has to carry the feel. Two traps: (a) atlas tiers are Heading 26/40/72, Numeric 16/22, Body 16/26; tiers >= 40 contain only ASCII + title strings (DisplayTier), so Korean Heading text must stay <= 32 px or it renders tofu; (b) Numeric has no 12-13 px tier (scaled from 16, slightly soft).

## 2. Gap table (mockup vs current kd-final)
Current look (docs/art_review/t38d_paper_ui_1280x720.png): one centred 1020x640 bright-paper panel on a dark vertical gradient, left column of text buttons (tabs + [작전 개시]/[캠페인 초기화]/[뒤로]), 3 strip buttons "좌/중앙/우" above the body, top bar = resource line + [저장/불러오기]. No keyboard navigation, panel never scales above 1020x640.
| Element | Mockup (1280x720 grid = sheet x 2/3) | Current | Gap / data |
|---|---|---|---|
| Desk background | wood-plank desk full bleed, dark header band 0-44 | dark gradient (Palette.Void/DeskTop) | new, cosmetic |
| Room backgrounds | left: cork + blue-grey wall; right: green baize + mauve wall | none | new, cosmetic |
| Header | room title (Myeongjo 30) + subtitle left; date + "division / area" right | division name + resource line | date: no calendar exists (CampaignState.Day = int day counter). Show "Day N" data-driven; "1920.10.21 (목)" = cosmetic (Q3) |
| Operations map | paper sheet x73-727, y67-493, tape corners, grid cross, red next-op callout, N compass, scale text | zoomable node map 150-280 px high inside tab 10 only (T47) | reuse CampaignMapView on the desk; contour lines/rivers = cosmetic backdrop; nodes/next-op/zoom = data-driven (already) |
| File-index tabs | 10 brown plates x743-887, numbered 01..15, active plate pulled left | text buttons in a 126 px column | restyle only; same Tab enum / ScreenTabs; plate "책상 위" marker on tab 10 = cosmetic |
| Right sheet (summary) | paper sheet x873-1233, y67-667, title, bars, next op, buttons, "결재필" stamp | tab-1 text report | bars: 병력, 보급품, 사기, 피로 data-driven (CampaignState); 장비 충족률, 민심 (public sentiment) = no field -> omit or cosmetic (Q6); stamp cosmetic |
| Telegram box | tray x70-417, y515-670, stacked slips "No.0470", counts "미결 3통 / 오늘 5통", red lamp | text "본국 전문: N건" in top bar + tab 13 | slips = CampaignEvents.AvailableEvents titles (data-driven); lamp lit when pending > 0 (data-driven); "오늘 N통" no data -> drop |
| Appointment letter | card x465-728, y515-670 "임명장 ... 제17사단장에 임명", red 임명 seal | none | cosmetic flavour from fixed premise (김 참장 / 제17 대한 사단, existing strings). Real appointment letters (regiment hire) appear in right room, data-driven |
| Nav strip + Q/E | bottom y672-720: 3 dots + labels, [Q]/[E] keycaps; edge arrows with vertical labels | 3 mouse strip buttons, no keys | new: Q/E, edge arrows, plates (mouse); labels cosmetic |
| Left room (기록실) | cork board 정보, ledger "부대 기록/전사자 명부", calendar 일정 | 3 plain tabs 11/12/14 (12 + 14 implemented, 11 placeholder) | board/ledger/calendar = skin over existing tabs; 11 has no data -> empty board with note; casualty roll + medals no data; calendar grid = cosmetic (Q3) |
| Right room (인사/정치실) | 3 top plates (07/08/09), baize board with 9 regiment commander cards, side sheets (staff slots, candidates), sticky notes | tabs 7/8/9 + new [인사] tab 17 (t61e, own 500-line screen, text cards) | tab 17 = the baize board (data-driven, t61e models); staff slots = tab 8 data; silhouettes cosmetic; sticky notes "외교 07/진급 09" = summaries of tabs 7/9 counts (data-driven if trivial, else cosmetic) |
| Action buttons | [작전 개시 준비], [일정 보기] on summary sheet; no [뒤로]/[초기화] | left column bottom | [작전 개시] moves into summary sheet + map sheet; [뒤로]/[초기화]/[저장] placement = Q2 |
| Resource line | not in mockup header | always visible top bar | keep a slim ledger strip under the header (Q2) |
| Fonts | HANBatang + Courier New | Nanum set (see 1) | no gap except typewriter feel |
| Menu + settings sheets, combat HUD | mockup sheets 4-5 | T38d palette only | OUT of scope here (separate paper-v2 cards, Q10) |

## 3. Target layout
### 3.1 Scaling rule (all rooms)
Design grid 1280x720. `s = clamp(min(W/1280, H/720), 0.8, 1.5)`; content rectangle = 1280*s x 720*s centred; desk/room background is drawn full-bleed (cover), so 2560x1440 (s = 1.5 -> content 1920x1080) shows extra desk margin instead of blurry upscaled text. 1024x640: s = 0.8 -> content 1024x576, vertical slack 64 px shared by header/nav. Text size px = max(12, round(designSize * s)) snapped to the nearest atlas tier; design sizes >= 14 at the grid so 1024 stays >= 12 px. All rects come from ONE pure function `DeskLayout.Compute(W,H)` (no Raylib), unit-tested at 1024x640, 1280x720, 1920x1080, 2560x1440 and random fuzz sizes (min 1024x640, Config.MinScreen).
### 3.2 Centre desk (grid px, tolerance +-8 against the mockup)
- Header band y0-44; nav strip y672-720 (3 plates, Q/E keycaps).
- Map sheet x73-727, y67-493 (pinned with tape). Tab stack x743-887, y74-583: 10 plates (9 + Episode 16), plate h = clamp(avail/n - 6, 40, 56) so the stack never overflows. Right sheet x873-1233, y67-667.
- Tray (telegram) x70-417, y515-670; letter x465-728, y515-670; red lamp x530-... between.
- Wide tabs (2 Roster, 3, 4 Logistics, 5, 6, 13 Events, 15, 16) need > 360 px: when such a tab is active the right sheet widens leftwards over the map sheet to x73-1233 (map slides under, only its tape corners show) and the telegram/letter dim. Tab 1 keeps the compact sheet; tab 10 = map focus (see 3.4). Recommended; user question Q1.
### 3.3 Rooms and navigation
Three rooms in a horizontal strip: left = 기록실 (tabs 11 정보, 12 부대 기록, 14 일정), centre = 사령관 책상, right = 인사 · 정치실 (7 외교, 8 참모진, 9 진급, 17 인사). Existing `Screen {Left, Center, Right}` and `ScreenTabs` stay the data model (T19c); t61e already placed Personnel(17) in `ScreenTabs[Right]`.
- Q = room to the left, E = room to the right, Left/Right edge arrow plates (x0-36 and x1244-1280, y278-442) and bottom nav plates by mouse. Horizontal slide ~180 ms (ease-out, skippable by pressing again; cosmetic, no sim effect). Remembered tab per room (`lastTabOf` exists).
- Keys inactive while any modal is open (slots, difficulty, debrief, save-failed, personnel confirm/reform/pick) and while a text input has focus. Esc order: topmost modal -> (nothing else in room) -> leaves campaign home (MainMenu Esc handler, MainMenu.cs:128). T61e rule "Esc closes only the open personnel modal" is the template; the shell must not swallow or double-handle Esc (risk R1).
- Number keys 1-9 are NOT bound (not requested).
### 3.4 Desk map = existing CampaignMapView + zoom
No new map renderer. Tab 10 content = `DrawMapView(sheetRect)` on the big map sheet (zoom wheel / +- / fit / drag already exist, session-only MapZoomView) plus the operation list on the right sheet (existing rows, [출격 준비]). On other centre tabs the same map stays on the desk as a read-only-looking backdrop (clicks on nodes still select the next operation only when tab 10 is active, to avoid accidental picks). Map image improvement (contours, rivers, terrain art) is a later pass (Q4); v1 keeps the T47 line/node look plus paper grid and compass.
### 3.5 Data-driven vs cosmetic (no invented gameplay)
| Item | Class | Source |
|---|---|---|
| Tab plates, tab numbers, active state, event badge | data-driven | ScreenTabs, CampaignEvents.AvailableEvents |
| Telegram slips + count + lamp | data-driven | AvailableEvents(state) (titles, count); tab 13 opens on click |
| Summary bars 병력/보급/사기/피로, next operation, [작전 개시] | data-driven | CampaignState (Roster, Supply, Morale, Fatigue, NextMission) |
| 장비 충족률, 민심 bars | omit (no data) / cosmetic only if user wants (Q6) | none |
| Appointment letter + 임명 seal (centre) | cosmetic | fixed premise strings (김 참장, 제17 대한 사단) |
| "결재필" stamp | cosmetic | none |
| Date "1920.10.21 (목)" | cosmetic; real = "Day N" | Day int only |
| Commander cards, favor dots, 해임 불가 stamp, candidate seals | data-driven | t61e PersonnelHomeModel (hire/dismiss rules) |
| Staff slots (보급/정보/연구/학파) | data-driven where tab 8 has data, else locked/empty | CampaignHomeStaff / doctrines |
| Cork board 정보 notes | data-driven if intel data exists; today empty-state note | tab 11 placeholder (no data) |
| Ledger 부대 기록 | data-driven | CampaignHomeViews.Records (MissionRecord) |
| 전사자 명부, 훈장 row, calendar grid | cosmetic/empty-state ("후속") | no data |
| Silhouette portraits, pins, tape, wood, cork, baize, sticky notes, nav labels | cosmetic | accepted as-is |

## 4. Asset plan
Principle: procedural Raylib first, zero heavy renders, no 3D. All textures generated once with the existing hash-noise approach of Paper.cs (render-only, never touches sim RNG), regenerated only on window-size change.
| Asset | Method | Est. size / cost | Who |
|---|---|---|---|
| Wood desk plank tile | procedural 256x256 noise stripes, tiled | 256 KB GPU | code (card b) |
| Cork tile, baize tile, wall panel tiles | procedural 128-256 px noise | <= 0.5 MB GPU total | code (b) |
| Paper sheet with torn/soft edge | procedural 96x96 9-slice + existing Grain; baked drop-shadow 9-slice | ~40 KB | code (b) |
| Tape, pin, lamp, keycap, tab plate | Raylib shapes + gradient (cached to a small atlas) | negligible | code (b) |
| Stamps (임명, 결재필, 해임 불가) | ring + rotated Heading text via DrawTextPro (new Ui.TextRot helper) | negligible | code (b) |
| Silhouette portrait | 2 circles/arc procedural | negligible | code (e) |
| Map contours/rivers (later) | PNG 2:1 backdrop 1024x512 painted in 2D by art agent (PIL) | <= 400 KB | art agent, NOT in a-e (Q4) |
Optional PNG polish (paper edge, wood) only if procedural looks flat on review: <= 6 files x 100 KB, art agent 2D, separate card. Window-size cache: no RenderTexture bigger than 1920x1080; tile textures instead of full-screen RT. Budget: desk skin adds <= 1 ms/frame at 1280x720 on the dev PC (assumption, measured with the existing frame timer; Q-free).

## 5. Card split (ids reserved with card_id.ps1: T38d2a-e; the allocator needs ids without a hyphen)
| Card | Content | Tier | Hotspots | blockedBy |
|---|---|---|---|---|
| T38d2b (first) | Desk paper kit: DeskTheme/Paper.cs additions (tiles, 9-slice sheet, tape/pin/plate/stamp/lamp), Ui.TextRot, tokens for desk colours, PaperUi contrast tests | sonnet | Paper.cs, Palette.cs, Ui.cs (additive), tests | - |
| T38d2a | Shell: DeskLayout (pure), room strip, Q/E + edge arrows + nav plates, slide, header, ledger strip, Esc/modal/click-through rules, replace DrawScreenStrip/left column; tabs still draw old body inside a sheet rect | sonnet (opus not needed: no sim) | CampaignHome.cs, CampaignMenu.cs (DrawHomeFrame, DrawViews, DrawTabColumn), new DeskLayout.cs | b |
| T38d2c | Centre desk: map sheet + zoom on desk, tab plates, summary sheet, telegram tray, letter, wide-sheet rule | sonnet | CampaignMenu.cs (tabs 1, 10, 13 draw), CampaignMapView.cs, new CampaignHomeDesk.cs | a |
| T38d2d | Left room: cork board, ledger, calendar skins over tabs 11/12/14 | sonnet | CampaignHomeReadonly.cs, Schedule/Records draw, strings | c |
| T38d2e | Right room: baize board for tab 17, tabs 7/8/9 plates, side sheets, sticky notes + final resolution audit sheet at 4 sizes | sonnet | CampaignHomePersonnel.cs (from t61e), CampaignHomeStaff/Promotion/Prisoners | d, t61e merged |
Serial order b -> a -> c -> d -> e (all share CampaignMenu.cs/CampaignHome.cs; H2 each). Hud.cs is NOT touched (combat HUD paper v2 = Q10). Tests every card: `--uifuzz 30 --fuzz-campaign` (3 views x resolutions), KD_SELFTEST_ONLY uiicons, paperui, i18n (+ en ASCII glossary strings), campaign home selftests, savecompat, detcheck unchanged. New: DeskLayoutSelfTest (a). Expectation: no FV / schema / golden change (UI only, session state not saved); if a card needs a saved field, stop and report.

## 6. Risks
- R1 Input / Esc: MainMenu Esc leaves the campaign; t61e modals own Esc; the shell adds Q/E and arrows. Mitigate with one `ModalOpen` predicate shared by all handlers + uifuzz Esc probe (exists in t61e) extended to Q/E during modals.
- R2 Click-through: overlapping z-ordered elements (edge arrows over sheets, plates over map, tray over letter). Hit-test top-down once per frame and clear `click` after the first consumer; modal opens already set click=false (Draw()).
- R3 1024x640 text fit: wide tabs (Roster/Logistics/Events) were laid out for ~850x500 body; sheet at s=0.8 gives ~930x480 content. Use existing TextFit/WrapText, 12 px floor, screenshot sheet at 3 sizes.
- R4 Tofu: Heading > 32 px with Korean; Numeric 12 px soft. Add DeskLayout selftest that asserts Heading sizes <= 32 for non-ASCII strings.
- R5 Performance: per-frame DrawTextureRec of tiles and many 9-slices. Cache atlas, avoid per-frame image ops, batch by texture, measure frame time before/after (budget above); sliding animation only redraws, no re-bake.
- R6 Merge/hotspot: t61e tab 17 file (CampaignHomePersonnel.cs, 500 lines) must be merged before e; T38d2c-e rewrite the same switch in DrawHomeFrame as a: keep the switch, change only the rect passed.
- R7 Map clarity: map shrunk to 654x426 (was up to ~1000x280 wide); node labels may overlap at zoom 1 -> keep existing zoom/fit, label cull from T47.

## 7a. DECIDED by the user (2026-10-23 15:1x; also in ops/DECISIONS.md)
Q1 widen the sheet over the map · Q2 header-right plates [저장·불러오기][뒤로], reset in the slots window footer, resource line = slim strip under the header · Q3 real campaign "Day N", calendar cosmetic · **Q4 painted operations-map image (contours/rivers PNG) postponed to 0.4; 0.3 = existing node map + grid/compass only** · Q5 keep 180 ms slide · Q6 omit 장비 충족률 / 민심 bars · Q7 appointment letter + "결재필" stamp stay cosmetic (add function when data exists; revisit in 0.4) · Q8 cap scale 1.5 + desk margin · Q9 map stays visible but not clickable on other centre tabs · Q10 menu/settings/battle-HUD paper UI = separate later cards T38d3/T38d4.

## 7. Open questions (recommended answer first; answered in 7a above)
1. Wide tabs (Roster, Logistics, Events...) do not fit the 360 px right sheet. Widen the sheet over the map (rec.) / always split map smaller / separate full-screen sheet per tab?
2. Where do [뒤로], [캠페인 초기화], [저장/불러오기] and the resource line go? Rec.: header-right plates [저장·불러오기] [뒤로]; reset moves into the slots window footer; resource line stays as slim strip under header.
3. Date header: show real campaign "Day N" (rec.) or the mockup's "1920.10.21 (목)" as a cosmetic placeholder? Calendar grid in the left room: cosmetic with no date logic (rec.) or hidden until time/season rules exist?
4. Operations map image improvement scope (T38d "Q4"): v1 = existing node map on paper with grid/compass only (rec.); later pass = painted contours/rivers PNG by the art agent, plus terrain-vision relation to tab 10. Confirm scope and timing (0.3?).
5. Room slide animation 180 ms: keep (rec., matches "가로 이동") or instant cut?
6. 장비 충족률 and 민심 bars have no data. Omit (rec.) or show as cosmetic placeholders?
7. Centre appointment letter and "결재필" stamp: cosmetic decoration (rec.) or drop until data (e.g. letter = regiment commander appointments)?
8. Large screens: cap content scale at 1.5 and show extra desk margin (rec.) or stretch further?
9. Tab 10 gets "tab = the map on the desk": other centre tabs keep the map visible as backdrop but not clickable (rec.) or hide it?
10. Menu/settings sheets and combat HUD paper v2 (mockup sheets 4-5): separate later cards T38d3/T38d4 after the desk (rec.) or fold into this series?

## 8. Gemini pro second opinion (2026-10-23, a LEAD: verify before acting; fold into the cards when they are dispatched)
1. Korean headings at scale s=1.5 would exceed the 32 px Hangul atlas limit: hard-clamp `min(32, round(size*s))`; add a 12-13 px numeric atlas tier (or avoid that size); define a vertical compression rule at 1024x640 (wide tabs ~480 px available vs ~500 px before).
2. Procedural paper tiles drawn per frame risk the 1 ms budget (many DrawTextureRec calls): pre-bake static paper into one RenderTexture, unload/recreate it on resolution change (also a VRAM-leak check for T38d2b).
3. A single boolean `ModalOpen` does not cover nested modals or key mashing during the 180 ms slide: use a modal stack (Esc goes to the top modal only) and lock Q/E while sliding.
4. Missing acceptance criteria: T38d2a slide input lock + heading size <= 32 px unit test; T38d2b texture unload on resolution change; T38d2c click-through block when the wide sheet covers map/telegram area at 1024x640; T38d2d/e empty-state rendering for tabs without data (11 info, medals, roll of the dead).
5. Order suggestion: T38d2a (pure layout/input, DeskLayout has no Raylib dependency) before T38d2b so the tile specs are fixed; or a and b in parallel; decouple T38d2e from the t61e merge so a delay does not block the series.
