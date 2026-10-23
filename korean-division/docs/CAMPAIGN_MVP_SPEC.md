# Campaign Home MVP Spec (draft v0.1, 2026-10-21)

> Basis: `docs/CAMPAIGN_HOME_DESIGN.md` (hereafter §) + user approval "building a minimal-feature MVP is also good. Approved." All values·events are **drafts — awaiting user confirmation**. No code.

## 0. What exists now (code check)
- `CampaignState` (`src/Core/Campaign/CampaignState.cs`): `campaign.json` (`%LOCALAPPDATA%/KoreanDivision/campaign/`, `KD_CAMPAIGN_DIR`), `Version=1`, finished missions · `NextMission` · `History` · carried-over enlisted/officers (`Units`/`Officers` = `MissionResults.UnitLine`) · `HowitzerCompany` · `Entry*` (entry force for retries). Mission order = `Chain` table (c1_01_hoeryong → c1_02_tumen → c1_03 no file). A separate save **unrelated** to simulation/snapshot/checksum (temp file → replace method).
- `CampaignCarry.Patch` (called by `MissionDef.Load` for missions with `"carry": true`): writes the surviving riflemen's ranks·officers into the mission text's `b1` company. Force size·company composition is **decided by the mission JSON** (not by the campaign).
- UI: `CampaignMenu` (65 lines: new campaign/progress summary/next mission 'in preparation'), `StringsCampaign.cs`, results-screen save notice. Resources·supply·home screen·deployment selection·events **absent**.

## 1. MVP scope
| Category | Content |
|---|---|
| **IN** | Division status summary · unit management (organization list · deployable indication) · logistics (supplies · stock · treatment · recruit placement) · operations map (next 1~2 operations, list level) · homeland (supreme commander/imperial grandson events 3~5) · pre-operation (briefing → deployed unit selection + supply cost gauge) · post-operation (results report → home) · resource (manpower · supplies · equipment stock) save · loot · 25% of wounded return |
| **OUT (follow-up)** | Organization management · equipment management/improvement · training (proficiency) · diplomacy/mercenaries/prisoners · staff details · promotion·command authority · intelligence (espionage) · unit records (log·medals) · schedule/time·season · research (§4) · public sentiment application · strongholds·railway income · auto organization/replenishment button · supply calculation preview (replaced by the gauge) · staff advice notes (replaced by event dialogue) · difficulty multiplier · paper texture skin (§5) · morale·fatigue · regiment (≤9) speed assignment · defeat branching · multiple save slots (MVP = current 1) · 11th Division liaison · corps (이석) routing details · funds |

## 2. Data model (`CampaignState` extension)
**Version**: `Version` 1 → **2**. When reading a v1 file new fields are filled with defaults (System.Text.Json: missing property = default) + starting resources are the §3 start values. `Roster` is generated once from the mission JSON's organization on entry to the next mission. Load failure = null as now (new campaign).
**Verification rules**: the campaign save is not simulation state, so `Snapshot.FormatVersion` · checksum · `SnapshotSelfTest.Audit` are **not needed** (the CLAUDE.md 'new state field' rule applies only to sim state). However if the mission text changes (Patch extension), the snapshot carries the written text as it does now → replay determinism is kept (confirm `--snaptest`/`--detcheck` at S3). New rules get unit tests in `KD_SELFTEST_ONLY=missions` (or a new `home`), randomness is hash-based on the stored `Seed` — no wall clock.

Added fields:
| Field | Type | Description |
|---|---|---|
| `Manpower` | `{Recruits, Wounded, Reserve}` int | Recruit pool (≤7000) · wounded · unorganized reserve |
| `Supply` | float | Single supply value |
| `Stock` | object | Rifles (dict by kind) · ammo 5 types (`ru`·`jp`·`de`·`fr`·`jpOld`) · shells 3 types (`infGun`·`field`·`howitzer`) · heavy weapons dict · horses |
| `Roster` | list | Deployment unit = **company**: `{Id, Kind(branch), Name, Strength, Wounded, Available}`. Squad and below·individual ranks keep the current `Units`/`Officers` carry-over |
| `Flags` | dict<string,int> | Hidden values: `heir_mood` (imperial grandson's mood 0~100, start 60) · `cmd_favor` (supreme commander's trust, start 50) · list of completed event ids |
| `Seed` | uint | Seed for event/loot randomness |
| `Pending` | object? | Operation in progress `{MissionId, DeployedIds, SupplySpent, AmmoLoaded}` (against abnormal termination) |
**Morale·fatigue: excluded from the MVP** (no consecutive-operation rules so no fields either). No public sentiment field.

## 3. Rules (draft values)
- **Deployment cost** = Σ branch requirement × scale multiplier (small 1.0 · large-scale 1.5). Requirements: infantry·recruit 1 · assault/sniper 1.5 · machine gun 2 · cavalry 3 · infantry gun/field gun/howitzer 5/7/9 · armored car 12 · armored train 40. If supply < cost, deployment is locked (gauge red, [sortie] disabled). No upkeep.
- **Deployment cap**: supply limit + total of the mission JSON b1 force (or `maxDeploy`, question 3). Selection is by company checkbox.
- **Ammo link**: at sortie deduct the default carried amount (rifle 60 etc.) from the per-caliber stock; if stock is short reduce by a ratio (e.g. 50%) and reflect it in the mission text as `ammoScale`. After combat the **remaining amount returns to stock** (deduct only what was used). Shells the same. Ammo rules in combat unchanged.
- **Results settlement**: 25% of casualties = wounded, the rest killed. Treatment = supplies 0.5/man ([treat] button). Untreated wounded cannot sortie and are discharged after 3 operations.
- **Recruits**: arrival = only by regular homeland supply/events (free). Placement = supplies 0.5 + 1 rifle. With 0 rifles placement is impossible.
- **Start values/regular supply (draft)**: supplies 3000 · +1500 after operations · recruit pool 2000 · +1000 (≤7000) · rifles 3000 · ammo 100,000 per type · shells infantry gun/field gun/howitzer 500/300/150.
- **Loot** (after victory): per enemy killed supplies 0.3 · rifle 0.5 (enemy faction's kind) · 20 ammo of that caliber · horses (enemy cavalry losses). Gun loot excluded from the MVP.
- **Imperial grandson/supreme commander effects**: regular supply multiplier = 1 − penalty. `heir_mood`<40 recruits −10%, <20 recruits −20~30%; low `cmd_favor` supply, same structure. **Combined penalty ≤ 30%, no game over.** Placating gives gains (gift supplies +, reinforcements +).

## 4. Screens (MVP = current raylib UI widgets, paper skin later. Left tab + right body layout so only the skin can be swapped)
1. **Home frame**: top bar (supplies · manpower · next operation), left tabs [summary][units][logistics][map][homeland], bottom [start operation]. Extends `CampaignMenu` (keep the new campaign·reset buttons).
2. **Division status summary** (daily report): organized force/10,000 · recruit pool · wounded · supplies · rifle fulfillment rate · next operation · 1 latest note.
3. **Unit management**: Roster table (name · branch · strength · wounded · deployable), recruit placement [+100] button (shows consumption).
4. **Logistics**: table of supplies · rifles · ammo 5 types · shells 3 types · horses, wounded [treat] button (shows cost), 5 recent receipts.
5. **Operations map**: list of the next mission(s) in `Chain` (2 at a branch) + possibly 1 static regional diagram, selection → pre-operation screen.
6. **Homeland**: list of unread events → card (sender · telegram · 2~3 choices + effect preview). Shows routing via the supreme commander.
7. **Pre-operation**: briefing → deployment selection (company checks, **cost gauge = against supplies**, ammo shortage warning) → [sortie].
8. **Post-operation (results report)**: casualties · wounded · loot · ammo return · supply change (promotion candidates · prisoners are follow-up) → [to home]; regular supply arrives + 1 event may occur. Connected after the current results screen (`MissionResults`).
Strings are consts in `StringsCampaign.cs`. Esc = cancel.

## 5. Sample events (**draft — for user review**, "absurdity" axis)
1. **E1 Guard unit placed forward** (imperial grandson 이태 ← guard unit commander): "By His Highness's command. Place 1 company of the 시위대 [Royal Guard] in the vanguard to win merit. Signed, guard unit commander." (a) follow → the next operation must include a 시위대 company (low combat power), `heir_mood` +10, gift supplies +200 / (b) persuade (note from chief of staff 김주현: "Brother, it would be better to turn them to the rear…") → 50% success `heir_mood` +0, failure −10 / (c) ignore → `heir_mood` −15.
2. **E2 Taking credit** (General 민, delayed via corps commander 이석): "The 17th Division's results are reported as achievements under corps command. I will handle the recommendation for decorations. 민." (a) accept → `cmd_favor` +5 / (b) protest → `cmd_favor` −10, next regular supply −10% / (c) inform 11th Division commander 남 참장 of the facts → no immediate effect, flag `nam_ally`=1 (for later branching).
3. **E3 Forced bleeding** (General 민): "Secure it within the deadline of this operation without fail. Losses are no object. No reinforcements." (a) accept → supplies +300, next operation deadline shortened (text only until implemented) / (b) submit circumstances → `cmd_favor` −10, supplies −10% / (c) chief of staff's recommendation → 50% (a), 50% (b). Supply penalty ≤ 20%.
4. **E4 The imperial grandson's meddling** (이태, craving display; if no 시위대 was deployed in the previous operation): "This differs from my policy. The division commander should know about face." (a) apologize → `heir_mood` +10 / (b) compromise: a small number of 시위대 accompany the next operation → +5, supplies −50 / (c) ignore → `heir_mood` −10, recruits −20% if <20.
5. **E5 The corps commander's dissuasion** (imperial son 이석): "Refrain from reckless offensives. Consolidating the defensive line is the best course." (a) accept → supplies +200, `heir_mood` −5 (the imperial grandson prefers offensives) / (b) keep the offensive → `cmd_favor` +0, `heir_mood` +5. Connected to the imperial grandson ↔ supreme commander conflict (right after E1).
All choices: combined penalty cap 30%, no instant death/defeat.

## 6. Implementation stages (each a mergeable branch)
| # | Branch | Content | Code touched | Difficulty |
|---|---|---|---|---|
| S1 | `home-state` | `CampaignState` v2 fields + migration + pure settlement functions (cost · treatment · 25% · loot · regular supply) + unit tests | `CampaignState.cs`, new `CampaignEconomy.cs`, `MissionSelfTest` | 中 |
| S2 | `home-ui` | Home frame + summary/units/logistics/map tabs (treat · placement buttons) | `CampaignMenu.cs`, `StringsCampaign.cs`, `MainMenu` entry | 中 |
| S3 | `home-deploy` | Pre-operation screen (deployment selection · gauge) + reflect selected companies/ammo ratio in the mission text (`Patch` extension, `Pending`) | `CampaignCarry.cs`, `MissionDef.Load`, `--snaptest`/`--detcheck` check | 上 (sim input change) |
| S4 | `home-debrief` | Results report screen + casualty/wounded/loot/ammo return settlement in `Apply` | `CampaignState.Apply`, `MissionResults`, `GameMission` results screen | 中 |
| S5 | `home-events` | Homeland tab + event data (`assets/data/campaign_events.json`) + 5 events + effects/cap | new `CampaignEvents.cs`, `CampaignMenu`, tests | 中 |

## 7. Questions for user confirmation
1. Are the §3 start values/regular supply numbers (supplies 3000 +1500, recruits 2000 +1000, ammo·shell stock) OK?
2. May we start with deployment unit = **company** (squad selection later)?
3. Should the deployment cap be based on the mission JSON b1 force count, or a separate `maxDeploy`?
4. Are the tone · choices · effect direction of the 5 events right? Are forced elements like mandatory inclusion of the 시위대 allowed?
5. Imperial grandson's mood: the MVP proposes hiding the number and using only dialogue hints — OK?
6. Defer the Yalu River start · 1-1/1-2 relocation in the MVP and keep the current `Chain`?

## User answers (2026-10-21)
- Start values · regular supply = **as in the draft** (supplies 3,000 / +1,500 per operation, recruits 2,000 / +1,000).
- Deployment unit = **battalion unit + remove/add companies**.
- Deployment cap = **per-mission `maxDeploy`** (actual deployment = min(supply limit, maxDeploy)).
- Forced conditions (e.g. mandatory inclusion of the 시위대) = **include but refusable** (refusing carries a mood · supply penalty, within the cap).
- Route: Andong → 봉황성 → (수암) → 안산 → 요양 → 봉천 → 장춘 → 하얼빈. Existing c1_01 · c1_02 = WIP missions kept (`CAMPAIGN_HOME_DESIGN.md` §9).
