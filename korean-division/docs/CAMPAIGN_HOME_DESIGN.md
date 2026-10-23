# Campaign Home Design (v0.4, 2026-10-22 — reflects user answers Q1~Q10: 3 screens 15 tabs · autosave · 9 slots · per-caliber ammo MVP · shell rules)

> A draft gathering user decisions. Original quotes are in `docs/ROADMAP_LONG.md` [1] "campaign" section. All values are drafts — confirm with the user before implementation.
> Campaign story · progression · time/season are being conceived by the user → this document covers only the **home screen and resource rules**.

## 0. Premises
- Player = **'김 참장'** (name undisclosed). Unit = **17th Division '대한'**. Division maximum **10,000 = by organized force**. **Recruit pool max 7,000** (waiting before organization). No approved-strength system — scale is controlled by the **number of recruits** the homeland sends.
- Starting region = **Yalu riverside**. Plan to move the existing 1-1 회령 · 1-2 두만강 to the front part of the Russian route (needs confirmation).
- Organization: squad · company · battalion · regiment (regiments ≤ 9). **Platoon · brigade = nominal only** (no system). Fire support is mainly **company level**.
- Defeat: **large engagement · medium operation = retry**, **small operation = campaign branch** (user 2026-10-21 added medium).
- **Initial release scope = chapters 1 · 2** (user). Retry = **at no cost**, to the state just before combat (user). **Armored train = appears from chapter 2** (user).
- **Map production method** (user): instead of manual production, **auto-generate → the user looks and edits a little**. → generation parameters per mission (river · city · railway · highland · trench line positions) + light map editor (terrain painting · houses/trenches/barbed wire · objective placement) — user decisions: ① per-mission placement is **dictated by the user later** → moved into generation parameters ② **light map editor = roadmap** ③ **add railway terrain** (progress queue). **Includes chapter 0 tutorial** = sweeping the bandit gangs that crossed the Yalu River, **small 1 + medium 1** (user). 0-1 (small) = **small scale, finishes within 5 minutes** (basic controls). 0-2 (medium) = **battalion-level engagement** — to properly learn company-level tactics (user). Approved plan: 0-1 = 1 Korean company vs bandits 20~30, selection · movement · attack-move · prone/cover · fire control K, on-screen guidance text. 0-2 = 1 Korean battalion (3~4 companies + machine guns) vs the bandit main force stronghold, company formations · battalion selection · suppression + flank maneuver · heavy MG emplacement · assault/bayonet · '교전!', guidance by chief of staff 김주현's notes. **Tutorial = focus on learning the systems, the imperial grandson appears from chapter 1** (user). Campaign **9 save slots + autosave (retry possible)** (user 2026-10-22, §15).

## 1. Resources (three layers)
| Layer | Content | Use |
|---|---|---|
| **Manpower** | Recruit pool (homeland support only, replenished every operation) · wounded (awaiting treatment) · reserve (unorganized force) | Organization · replenishment |
| **Supplies** (single value) | Food · medicine · fodder · consumables | Operation deployment · treatment · recruit placement |
| **Equipment stock** | Rifles (by kind) · **rifle/machine gun ammo (5 per-caliber types)** · **shells (by infantry gun · field gun · howitzer)** · heavy weapons · horses | Physically distributed at organization, carried amount at combat start |
- **Funds = for diplomacy · mercenary hiring · research.** Not used for unit management (organization · logistics). **No budget**.
- **Per-caliber ammo only at the campaign level** — not distinguished inside combat. Calibers: Russian (Mosin · Maxim) · Japanese (Arisaka · Type 3) · German (Mauser · 한양 88) · French (Lebel · 쇼샤) · old Japanese (Murata).

### 1-1. Operation deployment cost = Σ branch supply requirement × operation scale multiplier (small 1.0 · large-scale 1.5)
| Branch | Requirement (draft) |
|---|---|
| Infantry · recruit | 1 |
| Assault infantry · sniper | 1.5 |
| Machine gunner | 2 |
| Cavalry | 3 (including fodder) |
| Infantry gun / field gun / howitzer | 5 / 7 / 9 |
| Armored car | 12 |
| Armored train | 40 |
- If supplies are short, no more can be deployed ("deployable force" gauge, deployment locked when exceeded). Example: supplies 3,000 → infantry only 3,000, mixing guns·cavalry ~1,800.
- No upkeep (consumed only when deploying).

### 1-2. Treatment · recruits
- **25% of casualties = return as wounded** (can be raised by research). Treatment = supplies 0.5/man. Untreated wounded cannot sortie and are discharged after 3 operations.
- Recruit arrival = free (homeland). **Placement** = supplies 0.5 + 1 rifle (+ ammo). Without rifles placement is impossible.

### 1-3. Sources of income
Homeland regular supply (increases/decreases according to carrying out orders) · **loot** (supplies · rifles · ammo · shells · horses, proportional to enemy losses) · strongholds (captured cities · railway stations, increases when the railway is connected) · public sentiment (later, local procurement).

### 1-4. Combat link
The carried ammo at operation start = filled from the per-caliber stock (if short, less than the default carried amount, e.g. 50%). **Shells the same way (user 2026-10-22 Q6 confirmed)**: at operation start fill the carried amount from the stock per shell type (infantry gun · field gun · howitzer) (if short, 50% of the default carried amount), and after combat deduct the **actual number fired** from the stock. **Looted shells are added to stock in proportion to enemy losses**. No separate rule → one implementation. After combat deduct what was used from stock. Ammo rules inside combat (rifle 60 · slow recovery · wagon) unchanged.

## 2. Tabs
1. **Division status summary** (home first screen = daily report): force · equipment fulfillment rate · supplies · morale · public sentiment · next operation.
2. **Recruits · unit management**: battalion organization · placement, regiment (≤9) assignment.
3. **Organization management**: squad · company organization frameworks (no platoon).
4. **Logistics**: supplies · equipment stock · weapon distribution · treatment.
5. **Equipment management**: equipment improvement · introduction (homeland · cooperating factions).
6. **Training**: raise battalion proficiency (recruit → regular → veteran), cost = time · ammo.
7. **Light diplomacy**: options · civilian aid · mercenary hiring (Cossacks · Gando volunteers etc., funds + weapons) · prisoner handling (repatriation · internment · incorporation).
8. **Staff**: operations staff · regiment commanders · cooperating faction leaders (officer school · personality = long-term roadmap).
9. **Promotion · command authority**.
10. **Operations map**: Manchurian front · cities · railways · next operation candidates (branch choice).
11. **Intelligence (espionage)**: enemy force estimates · reconnaissance reports.
12. **Unit records**: unit log · casualty roll · medals.
13. **Homeland**: orders and demands from the government · imperial court.
14. **Schedule · event record**: until the next operation · order deadlines (time · season rules to be designed later).
15. **Research** (proposal — §4, awaiting user confirmation).
- Common state: **morale · fatigue** (consecutive operation fatigue, morale → sortie eligibility · company retreat threshold modifier). **Public sentiment** (value included, application later).

## 3. Pre/post-operation screens
- Pre-operation: briefing → participating unit selection (supply cost gauge) → placement.
- Post-operation: results report (casualties · wounded · loot · promotion candidates · prisoners) → home.
- **Autosave** (user 2026-10-22): autosave right before the operation starts · right after results settlement, and on save failure **retry** (failure notice + try again). Manual save chooses among 9 slots. Retry = return to the autosave right before the operation start.

## 4. Research (user approved)
- **No research points — it consumes funds + supplies** to proceed (no new currency). One at a time. **Linked to the officer school** (e.g. German school staff → reduced cost/time for infiltration tactics research).
- Three branches, 4~6 each (examples):
  - **Medical · logistics**: field hospital (wounded return 25 → 35%) · rail transport (deployment cost −10%) · ammunition depot (eases the carried ammo shortage penalty).
  - **Tactics · doctrine**: unlock new formations · tactics (skirmish waves · infiltration tactics · creeping barrage cooperation · heavy MG last stop line etc.).
  - **Equipment**: unlock equipment such as light mortars · armored car operation · Type 3 introduction (linked to the equipment management tab).

## 5. UI
Paper texture (user "as proposed"): home = commander's desk (operations map + file-index tabs, telegrams · notes · appointment-letter seals), combat HUD = field notebook strip · personnel card · telegram strip alerts (weak texture). 9-slice paper texture + noise, Myeongjo titles · typewriter-face numbers (OFL Korean font). First one mockup.

## 6. Conveniences (user approved)
- **Auto organization · replenishment button** (auto-organize by the organization framework, auto-replenish shortfalls, replenishment priority).
- **Supply calculation preview** (shows the remaining amount before deployment · treatment · recruit placement · research).
- **Staff advice** (operations staff notes: supply shortage · fatigue · mood warnings etc.).
- **Difficulty** (homeland supply · recruit count multiplier).
- Unit name · lineage = roadmap.

## 7. Absurdity — imperial grandson deputy division commander · homeland supreme commander (user 2026-10-21, the core axis of the work)
- User's original: "One of the cores of this work is absurdity. The protagonist is tormented mainly by 2 people. One is the imperial grandson appointed deputy division commander (changed from imperial son to imperial grandson), the other is the supreme commander in the homeland. When contacting the homeland go via the supreme commander. The deputy division commander periodically meddles if the protagonist's decision runs against his direction. If his mood is greatly displeased, problems such as fewer recruits or less supply." (original Korean quote kept in the Korean-language history of this document)
- **Worldbuilding change: imperial son 이태 → imperial grandson** (the existing setting 'N26 imperial son 이태 = Gojong's son' needs revision).
- Homeland tab = all requests · reports **go via the supreme commander** (delay · distortion · credit-taking · political orders possible).
- Imperial grandson = **mood** value (Claude proposal: show indirectly through staff notes · expressions · dialogue instead of a direct number). Meddling event for each decision against his 'direction' (preference), low mood reduces recruits · supplies.
- **Confirmed (user 2026-10-21)**: imperial grandson = **face · display · favoritism all three, especially considerable craving for display**, **favors the 시위대**. Response choices · penalty cap · imperial grandson ↔ supreme commander conflict = approved. **Supreme commander General 민** = aggressive, often makes decisions forcing the protagonist to bleed, indifferent to subordinates' deaths.
- **Corps commander imperial son 이석 (deputy)** (user addition): gentle but excessively defensive, militarily incompetent — contrast with the imperial grandson (offense · display). Chain of command = division (김 참장) → corps (이석) → supreme commander (General 민). **Imperial grandson 이태 = 이석's nephew**. **Homeland contact = via the corps** (a case of reporting directly to the supreme commander also exists — the cost/risk of skipping the corps is decided at design). Added **contact with 11th Division commander 남 참장** (a key northern expedition unit together with the 17th Division — in the homeland tab or a separate 'adjacent unit' contact).
- Claude proposal (the rest awaiting confirmation): definition of the preference list · response choices (follow · persuade · compromise · ignore · cede credit) · penalty cap (e.g. recruits/supplies −20~30%, no game over) · gains from placating (imperial gifts · reinforcements) · events where the interests of the two people conflict.

## 8. Characters (user 2026-10-21)
- 김 참장 (protagonist, 17th Division commander) · **division chief of staff 김주현 = the protagonist's own younger brother** (voice of the staff advice notes) · deputy division commander imperial grandson 이태 (이석's nephew) · **guard unit commander (시위대장)** (imperial grandson's aide, ambitious, the practitioner of favoritism toward the 시위대) · corps commander imperial son 이석 (deputy) · supreme commander General 민 · 11th Division commander 남 참장 (close friend, German school, the protagonist's superior in WWI).
- Roadmap (later): 3~4 regiment commanders (various schools · personalities) · homeland quartermaster general · Wonsubu inspector · NCO representative.

## 9. Advance route · Japan (user 2026-10-21, draft)
- Advance route (user proposal, contemporary names): Yalu River (Andong 安東) → **안산 (鞍山)** → **봉천 (奉天)** → **장춘 (長春)** → **하얼빈 (哈爾濱)**. 안산 → 봉천 = engagement with the Kwantung Army, 장춘 → 하얼빈 = Russian-affiliated forces appear in earnest. 장춘 = boundary of the South Manchuria Railway / Chinese Eastern Railway (Japanese 장춘 station · Russian Kuancheng-tzu station).
- **Route confirmed (user: "work out the Andong->안산 route as you see fit. Yoyang as the intermediate base")**: Yalu River crossing (Andong) → **봉황성 (鳳凰城)** (north along the Andong-Mukden line, bandits · Fengtian local army) → west along the 수암 (岫巖) mountain road → **안산** (South Manchuria Railway main line, local clashes at the Kwantung Army railway zone) → **요양 (遼陽)** (intermediate base) → **봉천** → 장춘 → 하얼빈.
- **Chapter composition (user confirmed 2026-10-21)**: chapter 0 = tutorial (Yalu riverside bandit battle). **Chapter 1 = Yalu River crossing (Andong) → 봉황성 → 수암 → 안산 → to south of 요양** (many steps in the Andong suppression, 안산 = first local clash with the Kwantung Army). **Chapter 2 = 요양 → 봉천 suppression → erasing 장작림's faction** — 장작림 resists in his own way, **second Kwantung Army intervention gives a two-front attack from 장작림 + the Kwantung Army**. Chapter 3 = 장춘 → 하얼빈 (Russian-affiliated). Afterward full war with Japan.
- **Approved (user "approved. We go with the current plan")** + **Kwantung Army intervention = a runaway of the Kwantung Army** (independent action by the local army without Tokyo government approval — user). **Japanese landing on the Korean peninsula = much later** (full-war stage). Details: the second Kwantung Army intervention is a limited intervention on the pretext of 'railway protection' (railway zone garrison · independent garrison, not a regular division) so the foreshadowing of full war is kept · two fronts = northern 장작림 front + the supply line threatened at the southwest railway zone (안산 · 요양) → in the campaign home, battalions are split between two fronts (pressure on the supply limit) · absurdity: homeland 'do not fire first on the Japanese' vs General 민 'occupy 봉천 immediately' vs 이석 'halt' vs imperial grandson 'show off by entering 봉천' · end of chapter 2 = 봉천 falls + 장작림 erased + temporary ceasefire with the Kwantung Army.
- **Chapter 1 = 6 episodes** (user): episode = 1 large engagement + 1~2 medium operations + 2~4 small operations — **at first the lower bound (medium 1 · small 2)**, **reuse maps actively** (medium·small = parts of the large engagement map) (user). **Small operation frameworks** (bandit suppression · escort · reconnaissance · stronghold · railway cutting) + **optional small operations** included (user). Large engagements (draft): ① Yalu River crossing (opposite Andong) ② Andong urban mop-up ③ 구련성 highland ④ 봉황성 assault ⑤ 안산 (first local clash with the Kwantung Army) ⑥ 요양 southern position breakthrough. **The 수암 mountain road is removed from large engagements** (medium·small operation candidate).
- **Erasing 장작림's faction = chapter 2** (user). Characters: 장작림 + **only his senior generals are real people**, below that fictional (user "only high ranks as real people").
- **Existing Tumen missions (c1_01 회령 · c1_02 두만강 crossing) = kept as WIP missions** (user) — removed from the campaign order, files kept.
- **Kwantung Army = local clashes in the early stage** (railway zone boundary disputes). **Full war with Japan = after the northern expedition is complete** (user direction).
- Claude proposal (awaiting confirmation): early-engagement rule constraints (no entering the railway zone · no shelling the railway, Japanese casualties → diplomatic penalty) = the absurdity of the homeland · supreme commander fearing escalation ↔ General 민 forcing bleeding · lay Japan's shadow (bandit · Fengtian support, Kwantung Army provocation · scheming) throughout the northern expedition and use it as foreshadowing of full war after 하얼빈.

## 10. Northern expedition force size (user 2026-10-21)
- **Initial northern expedition force = 4 divisions** (main effort 11th Division 남 참장 · 17th Division protagonist, the other 2 = flank · rear · railway guard — corps commander 이석's deployment judgment). **4 more divisions reinforce later** — **the reinforcement corps advances in a different direction from the protagonist** (user).
- Basis (Gemini web search, estimate): Fengtian army 1920 Three Eastern Provinces about 100,000~150,000 (1922 Zhili-Fengtian War about 120,000 entered the Pass), Kwantung Army 1919~21 about 10,000~14,000 (rotating divisions 7,000~8,500 + 6 independent garrison battalions 4,000~5,500, treaty cap 15,000), divisions of the time nominally 10,000~12,500 · actually 7,000~11,000.
- Expression: Fengtian army = many but shabby (local units · high share of large-blade troops, elite = part of 장작림's direct guard), Kwantung Army = few but strong. The shortage of forces = homeland · General 민 absurdity.
- **Original character settings = `docs/CHARACTERS.md`** (user draft 2026-10-21: 김 참장 · imperial grandson 이태 李鈦 · imperial son 이석 李墿 · General 민 · 김주현). If it differs from this section CHARACTERS.md takes precedence.

---
# 11. Per-tab specification (v0.3 addition — data · screen · connection to existing systems)
> No new features are created. Only items already in §1~10 and `ROADMAP_LONG.md` are placed in tabs. Status: `wip/gemini-import` already has a 5-tab MVP of summary · units · logistics · map · events (homeland) (§13). "Follow-up" = outside the MVP scope (`CAMPAIGN_MVP_SPEC.md` §1 OUT).
| # | Tab | Data (CampaignState) | Screen elements | Connected existing systems | Stage |
|---|---|---|---|---|---|
| 1 | Division status summary (rule 15) | Manpower · Supply · Stock · Roster totals, Flags (mood · trust = hidden values), morale · fatigue (follow-up), next operation | One page in daily report format: force · equipment fulfillment rate · supplies · next operation | `CampaignMenu` Summary | MVP |
| 2 | Recruits · unit management | Roster (battalion → company `RosterCompany`), recruit pool ≤7,000, regiment assignment (≤9) | Battalion list · remove/add companies · recruit placement button | Same regiment numbering as rule 9 (number = regiment, Alt+number = battalion) · `AssignRecruits` | Battalion-level MVP, **regiment assignment UI included in MVP (Q9 approved)** |
| 3 | Organization management | Squad · company organization frameworks (no platoon · brigade) | Framework editing · auto organization button (§6) | Mission JSON company composition | Follow-up |
| 4 | Logistics | Supply · Stock (rifles · ammo 5 types · shells 3 types · heavy weapons · horses) · wounded | Stock table · treatment · supply calculation preview (§6) | `CampaignEconomy.HealWounded` · `BeginOperation` | MVP |
| 5 | Equipment management (rule 12) | Introduction · improvement state (data model undecided) | Equipment improvement · introduction list | Research tab (§4) unlock link | Follow-up |
| 6 | Training (rule 10) | Battalion proficiency level (recruit → regular → veteran), cost = time · ammo | Per-battalion proficiency · training orders | Link to combat effects = **long-term review (excluded from MVP, user 2026-10-22 Q4)** — only proficiency display · record | Follow-up |
| 7 | Light diplomacy | Funds · mercenary hiring · prisoners (rule 13) | Options · civilian aid · mercenaries · prisoner handling (repatriation · internment · incorporation) | Prisoner count of loot settlement | Follow-up |
| 8 | Staff | Operations staff · regiment commanders · cooperating faction leaders + school (`doctrines.json`) | Character cards (school · personality), staff advice notes (김주현) | `wip/doctrines` (§13) | Follow-up |
| 9 | Promotion · command authority | Promotion candidates (computed from the results report), command authority | Promotion candidate list | Carried-over `Units`/`Officers` ranks | Follow-up |
| 10 | Operations map | Cleared missions · next operation candidates (branch) · strongholds | Manchurian front map, candidate selection | `CampaignState` Chain · chapter composition §9 | List MVP, map follow-up |
| 11 | Intelligence (espionage) | Enemy force estimates · reconnaissance reports | Report list | Mission briefing | Follow-up |
| 12 | Unit records | `MissionRecord` · casualty roll · medals | Unit log | `MissionRecord` (in WIP) | Log MVP, roll · medals follow-up |
| 13 | Homeland | Events (`campaign_events.json`), `heir_mood` · `cmd_favor` | Telegrams · notes + choices, routing via supreme commander · corps (§7) | `CampaignEvents` | MVP |
| 14 | Schedule · event record (rule 14) | Event history, order deadlines | Timeline | Only history until time · season rules are confirmed | History MVP |
| 15 | Research (§4) | 1 research in progress, completed list | Three-branch list | School link (§12) | Follow-up |

## 11-1. Placement on 3 screens (user 2026-10-22 Q1: center 9 + left 3 + right 3 = 15 tabs) — placement is a **Claude proposal, awaiting user review**
| Screen | Tabs | Basis |
|---|---|---|
| **Center 9** (operated every operation cycle) | 1 Division status summary · 2 Recruits · unit management · 3 Organization management · 4 Logistics · 5 Equipment management · 6 Training · 10 Operations map · 13 Homeland · 15 Research → in the list above **research (15) and equipment (5) are linked tabs** so they are placed together | Gathers the controls used before/after every operation, such as organization · supply · sortie preparation · homeland orders, in the center |
| **Left 3** (records · information, mostly reading) | 11 Intelligence · 12 Unit records · 14 Schedule · event record | Centered on viewing reports · history so suitable for a secondary screen |
| **Right 3** (people · politics) | 7 Light diplomacy · 8 Staff · 9 Promotion · command authority | Groups tabs related to people · factions |
- The center in number order is 1 · 2 · 3 · 4 · 5 · 6 · 10 · 13 · 15 = exactly 9, left 11 · 12 · 14, right 7 · 8 · 9 (total 15, no duplicates).
- Homeland (13) is political in nature but is an MVP tab where choices come every operation, so it is placed in the center. If there is an objection, swapping homeland ↔ promotion · command authority (9) is the smallest change.
- Implementation stages: the MVP 5 tabs (1 · 2 · 4 · 10 · 13) are all in the center → **the left · right screens can be opened when follow-up tabs are added** (effect on T09/T19 in §14).

# 12. Correspondence table for rule decisions 1~15 (`ROADMAP_LONG.md` "campaign rule decisions", user 2026-10-21)
| Rule | Content | Design location |
|---|---|---|
| 1 | No budget · supply limit | §1, §1-1 (WIP `TotalDeployCost`) |
| 2 | Loot | §1-3 (WIP `DebriefReport.Looted*`) |
| 3 | 25% of casualties return wounded · treatment supplies | §1-2 (WIP `HealWounded`) |
| 4 | Korea · Fengtian color | Unrelated to the campaign home (art work, outside this document's scope) |
| 5 | Division max 10,000 | §0 (organization basis, recruit pool 7,000) |
| 6 | Defeat: large-scale retry / small branch (+ medium retry, §0) | Tab 10 · post-operation screen (§3). Branch save method is §15 (resolved by autosave · 9 slots, branch record structure decided at implementation) |
| 7 | Time · season later | Tab 14 (history only) |
| 8 | Save slots | §0. 9 slots + autosave (user 2026-10-22), slot UI in the home top menu |
| 9 | Regiment number keys, Alt+number = battalion | Tab 2. Implementation is separate work (ROADMAP_SHORT 'regiment selection in combat') |
| 10 | Training | Tab 6 |
| 11 | Morale · fatigue | Common state (§2), excluded from MVP |
| 12 | Equipment improvement · introduction = equipment management tab | Tab 5 |
| 13 | Prisoner handling | Tab 7 |
| 14 | Schedule · event record | Tab 14 |
| 15 | Division status summary home first screen | Tab 1 |
- Additional rules: recruit supply = homeland support only (replenished every operation) · funds = for diplomacy · mercenaries · research only · mercenary-style units (Cossacks · Gando volunteers) are hired in the diplomacy tab.

# 13. Consistency with in-progress branches (read-only check: 2026-10-22, the two branches were not touched)
## 13-1. `wip/gemini-import` (patch `wip-patches/gemini-import.patch`, being restored by T01)
- Already implemented: `CampaignState` v2 (`Manpower` · `Supply` · `Stock` · `Roster` · `Flags` · `Seed` · `Pending` · `LastDebrief` · `MissionRecord`), `CampaignEconomy` (cost table same as §1-1 · treatment 0.5 · recruit placement 0.5 + 1 rifle · operation start · mood/trust penalty combined cap 30%), `CampaignEvents` + `campaign_events.json` (5 events: guard unit forward · taking credit · forced bleeding · the imperial grandson's meddling · corps commander's dissuasion), sortie organization · `ammoScale` reflection of `CampaignCarry.Patch`, `CampaignMenu` 5 tabs (summary · units · logistics · map · events), `CodexMenu`.
- Differences from this design · needs confirmation: (a) ammo loading is based on only one type `ru` — the 5 per-caliber types of §1 are **reflected from the MVP (Q5 approved)** → implemented in T19. (b) shell stock · heavy weapons have only fields and no consumption rules → shells are implemented by the §1-4 rule (Q6). (c) event effect values (mood ±5~15, supplies ±50~300) = the WIP draft **approved (Q7)**, the `cmd_favor` hidden value structure **approved (Q8)**. (d) the tab name `Events` = the 'homeland' tab of this document (§2-13). (e) the campaign save is not sim state but `CampaignCarry.Patch` changes the mission text, so snapshot · determinism confirmation is needed (MVP spec §2) — `--snaptest` · `--detcheck` in T19.
## 13-2. `wip/doctrines` (`assets/data/doctrines.json`, `src/Core/Doctrines.cs`, read-only data · not used by the sim)
- 5 schools (German · French · Russian · Pyongyang · Beijing) + dispositions per enemy faction. Each school has `research` (research link) and `figures` (people) fields → the staff tab (8) reads `figures` and the research tab (15) reads `research` as is. The AI tendency values (`axes`) are null — this design does not create values.
- Rule: the home screen uses schools **only for display · as the basis for research cost/duration adjustment**. Applying to the combat AI is separate work (ROADMAP_LONG 'officer school').
## 13-3. Possible file conflicts
- Both may touch `Defs.cs` · `SelfTest.cs` (doctrines: loader registration + 2 test lines, gemini-import includes a Defs.cs change). T19 assumes stacking in this order (gemini-import → doctrines).

# 14. Proposed implementation order (basis for the T19 split, plan before approval)
1. WIP MVP restoration (T01, summary · units · logistics · map · homeland) → 2. settlement · save verification → 3. school data link (staff tab) → 4. training · equipment · diplomacy · research tabs → 5. paper texture skin (after approval of the mockup `docs/art_review/ui_paper_mock.png`). Each stage is a mergeable unit, tab additions are separated so they touch only the `Tab` enum + `StringsCampaign`.

## 14-1. Scope of the user answers (2026-10-22) for the T09 slice · T19 MVP
| Item | T09 (slice: one cycle 0-1→0-2→1-1) | T19 (campaign home MVP) |
|---|---|---|
| Autosave + retry | **Included** (autosave right before the operation · right after settlement, retry on failure; save atomicity is the T09 acceptance criterion) | Full timing · notification UI |
| 9 save slots | May start with 1 slot (structure compatible with 9 cells) | **9 + slot UI** |
| 3 screens 15 tabs | N/A (tab extension is outside T09 scope) | MVP 5 tabs in the center, left · right with follow-up tabs |
| Per-caliber ammo 5 types (Q5) | The slice may start with one type `ru` if needed to verify one cycle of carried ammo fill · deduction (needs confirmation) | **Included in MVP** |
| Shell rules (Q6) | One set of code same as ammo, not applied for missions without guns | **Included in MVP** (including looted shell addition) |
| Training effect (Q4) | N/A | **Excluded from MVP**, long-term review |
| Event values (Q7) · cmd_favor (Q8) | T01 restored values as is | Used as is |
| Regiment assignment UI (Q9) | N/A | **Included in MVP** |
| Yalu River start · 1-1/1-2 relocation (Q10) | Start point = Yalu River crossing (1-1) | Reflected |

# 15. Remaining questions
None — Q1~Q10 were all answered by the user on 2026-10-22 (`ops/DECISIONS.md` C2 is canonical). Below are **Claude proposals** from the answers (user review if there are objections):
- The 3-screen placement of the 15 tabs in §11-1 (center 9 · left 3 · right 3).
- The autosave timing (right before the operation · right after settlement) and retry method are the proposed values of §3.
- Whether T09 may start per-caliber ammo with one type `ru` (§14-1).
