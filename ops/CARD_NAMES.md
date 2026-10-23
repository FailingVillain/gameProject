# CARD_NAMES - Readable-name dictionary for card IDs

As of 2026-10-22. IDs are not changed. This document only attaches readable names to IDs. (In the name columns below the English name comes first, with the original Korean name in parentheses.)
Sources: ops/CARD_REGISTRY.md (183 IDs) · ops/QUEUE.md (main latest, state first) · ops/CARD_INDEX.md (branch · worktree) · ops/MERGE_QUEUE.md · first title in korean-division/tasks/ of the design-document branches · cards of tool·measurement·kd branches.
Names were taken from card titles. Items without a title use the QUEUE title. Unknown cells are 'unknown'.

## How to read names
| Prefix | Meaning | Example |
|---|---|---|
| T## | General task card sequence number (T01~T57) | T03 trench breakthrough |
| T##a·b·c… | Pieces of a split card | T19a save slots |
| T19b2 · T24x · T46 | Re-stack of a split piece · separate branch · alias for the same job (T46 = T38c) | T24x crossing observer team fix |
| TC-A…TC-G | C1E1 campaign engine cards | TC-B alert state |
| C1E1-m/u/g/s/d/t/v/p/q | Andong campaign content: map (m) · unit (u) · engine·graph (g) · mission JSON (s) · data (d) · text (t) · variant (v) · portrait (p) · verification (q) | C1E1-s6 OP1 direct-assault mission |
| M1~M3 | Morale cards. The ledger has only M1; M2·M3 are outside the ledger | M1 morale↔suppression link |
| TF-A…TF-E | Campaign flow design documents | TF-B progression link |
| TI1…TI6 | Translation (i18n) design documents | TI2 bulk string conversion |
| TW1…TW3 | Weather design documents | TW2 blizzard |
| RP1~RP3 · RV1~RV3 | RP = results report (AAR) design documents · RV = replay viewer design documents | RV2 replay cursor |
| PR1~PR5 | Not card IDs. They exist only in branch names such as kd/pr1-props-v2, not in card files or the ledger | unknown |
| A1 · C14 · C15 | Not card IDs (differs from the conductor's guess). A1 = merge step name (ops/MERGE_PLAN.md A1 = fv-renumber). C14·C15 = branch names tools/c14-nightly · tools/c15-watchdog | — |
| D## | Decision number in the decision ledger ops/DECISIONS.md | D19 casualty measurement |
| FV## | Save format (FormatVersion) number reservation. ops/FV_RESERVATIONS.md | FV 53 |
| E1~E3 · F1~F7 · G · H | Merge line step names (ops/MERGE_PLAN.md) | E2 river r1~r4 |
| One-letter sections A~Z · two-letter sections AA~ | Section names of the decision ledger (ops/DECISIONS.md). Section BH is quoted by the M1 card but the section body is unknown | — |

States: **merged** · **wip-ready** (done, merge after the conductor's order) · **in progress** (assigned · running) · **waiting** (queued · blocked) · **discarded** (closed · replaced by another card · concluded). Design-document cards have no state record so they are put as 'waiting'.

## 1. Combat engine
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T03 | Trench breakthrough (참호 돌파) | Trench breakthrough engagement rules, including smoke·barbed wire cutting | merged (6ad6c50c) | wip/breach2 |
| T04 | Order queue (명령 대기열) | Fire control and order queue (on top of T03) | merged (0377f8a0) | wip/orderq2 |
| T05 | Tactical AI 3B (전술 AI 3B) | Enemy AI blackboard 3B (on top of T04) | merged (f74a675c) | ai3b2 |
| T06 | Casualty speed (사상자 속도) | Casualty rate adjustment (D19 C discarded) | discarded | wip/casualty2 (kept) |
| T10 | Communication alpha ②~④ (통신 알파 ②~④) | Counter-battery·night·march fatigue messengers | merged (1c91c965) | kd/t10-comms-b |
| T10b | Difficulty sinks (난이도 소비처) | Messenger capture·wire cut difficulty (default off) | merged (4d74ce9a) | kd/t10b-difficulty |
| T16 | Trench assault decline cause (참호 돌격 하락 원인) | trench_assault decline cause investigation (below baseline) | merged (2cb118cc) | kd/t16-trench |
| T16b | Trench cause re-investigation (참호 원인 재조사) | Analysis-only re-investigation (analysis closed) | discarded | kd/t16b-rootcause |
| T19b | Ammo·shell rules (탄약·포탄 규칙) | 5 per-caliber ammo types, shell rules, loot (on top of T19a) | merged (622db02b) | t19b-ammo-shells |
| T24 | Acting command·assault suppression (대리 지휘·돌격 제압) | D08 fix proposal S1~S3: acting command range, observer team advance limit, assault suppression check | merged (07ac80cb) | kd/t24-s1s3 |
| T24x | Crossing observer team fix (도하 관측반 수정) | Fix for the observer team crossing problem (on top of T24) | merged (390763a7) | kd/t24-crossing-fix |
| T25 | Bayonet = all assaults (착검 = 모든 돌격) | Apply the bayonet halt to all assaults (D02 A) | merged (29695c34) | kd/t25-bayonet-all |
| T30a | Train movement (열차 이동) | Train object and track movement (FV 58) | merged (863f9d1e) | t30a-train-move |
| T30b | Armored train weapons (장갑열차 무기) | Train weapons and per-car damage (FV 59) | merged (ca1cdb84) | t30b-train-weapons |
| T30c | Freight train objective (화물열차 목표) | Freight train as a mission objective (on top of T30b) | merged (3de60d59) | t30c-freight-objective |
| T30e | Train demo mission (열차 시연 미션) | 1 armored train + freight train escort mission (on top of T30c) | merged (7f46a4cf) | t30e-demo-mission |
| T31a | Rejoin limit (재투입 제한) | Limit re-commitment of retreated companies (default off, do not turn on) | merged (97ed0267) | kd/t31a-regroup |
| T31c | AI surrender (AI 항복) | AI surrender improvement (all keys default off, turning on is 0.4) | merged (e9cd527a) | kd/t31c-surrender |
| T32a | Combat engineers (전투공병) | Infantry engineer unit infantry_pioneer (barbed wire cutting, grenades 2) | merged (26cc9fd9) | kd/t32a-pioneer |
| T32b | AI smoke grenade (AI 연막 수류탄) | AI smoke throwing (default off) | merged (b5f1da5) | kd/t32b-ai-smoke |
| T32c | AI barbed wire cutting (AI 철조망 절단) | AI barbed wire cutting (0.4) | waiting | — |
| T34a | Suppression threshold·cover withdrawal (제압 문턱·엄호 철수) | AI suppression awareness threshold and cover withdrawal | merged (63a42b4) | kd/t34a-suppress |
| T34b | AI framework selection (AI 틀 선택) | Flank·assault framework selection (0.4) | waiting | — |
| T34c | Combined arms cooperation (제병 협동) | Timing of gun·machine gun·infantry cooperation (0.4) | waiting | — |
| T34d | Cavalry flank intensity (기병 측면 강도) | Cavalry flank intensity tuning (balance on hold) | waiting | — |
| T34e | Idle diagnosis (유휴 진단) | AI idle diagnosis and difficulty overlap (0.4) | waiting | — |
| T35 | IsAi restoration (IsAi 복원) | Replay header·checksum restoration (FV 60 / Replay 24) | merged (122bfff) | kd/isai-restore |
| T36 | Immune target fix (면역 표적 수정) | Immunity exemption based on attack orders (AH2) | merged (48bd6ce3) | kd/immune-target-fix |
| T37 | Acting NCO delay (대리 부사관 지연) | Acting NCO delay tier (U4) | merged (7cfa0c25) | kd/acting-delay-tier |
| T40 | Train annihilation judgment (열차 전멸 판정) | Include trains in annihilation judgment (FV 59 kept) | merged (2de288d6) | kd/trains-in-annihilation |
| M1 | Morale↔suppression link (사기↔제압 연동) | The suppression decay rate varies with allied morale (DECISIONS BH2) | merged (d75a2bc0) | kd/m1-morale-suppress |

## 2. Campaign
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T01 | Campaign WIP restoration (캠페인 WIP 복원) | gemini-import campaign WIP·UI, including the save transaction fix | wip-ready | gemini-import |
| T09 | Campaign vertical slice (캠페인 수직 슬라이스) | One cycle 0-1→0-2→1-1 (replaced by T29b) | merged (0c82a472) | t09-camp-slice |
| T19 | Campaign home MVP, bundle (캠페인 홈 MVP (묶음)) | Implementation of rules 1~15·school. Split into T19a~d | discarded | spec/t19-split |
| T19a | Save slots (저장 슬롯) | Save cleanup, autosave·retry, 9 slots | merged (43e69b19) | t19a-save-slots |
| T19b2 | Record sync verification (기록 동기화 검증) | Verification that the records match (on top of T19b) | merged (4980e7bf) | t19b2-synced |
| T19c | Campaign home tabs (캠페인 홈 탭) | 3 campaign home screens, MVP 5 tabs, regiment speed UI | merged (9260e367) | t19c-home-tabs |
| T19d | School (doctrine) (학파(교리)) | Officer school restoration, staff tab read-only | merged (04a91497) | t19d-doctrines |
| T29 | Economy numbers JSON (경제 숫자 JSON) | Move campaign economy numbers to JSON and settle only the first win | discarded (replaced by T29b) | t29-campaign-economy-json |
| T29b | Economy JSON re-stack (경제 JSON 재스택) | Re-stack T29 on top of T09 | merged (a89b4d23) | t29b-on-t09 |
| T42a | Morale·fatigue account (사기·피로 계정) | Company fatigue·morale account (schema v4) | merged (ba621feb) | t42a-fatigue-morale |
| T42b | Promotion candidates (진급 후보) | Promotion candidate selection and application (tab 9) | merged (51b92c3d) | t42b-promotion |
| T42c | Prisoner ledger (포로 장부) | Prisoner ledger, repatriation·internment (tab 7) | merged (66e76378) | t42c-prisoners |
| T42d | Schedule·log (일정·일지) | Schedule·timeline (schema v7, tab 14) | merged (06ee9bd1) | t42d-schedule-log |
| T48a | Regiment commander data (연대장 데이터) | Regiment commander pool data, schema v8, loader | merged (e31406d1) | t48a-commander-data |
| T48b | Officer identity·assignment (장교 신원·배정) | Officer identity persistence, promotion terminus, assignment (on top of T48a) | waiting | — |
| T48b1 | Officer identity key (장교 신원 키) | Officer identity key (OfficerKey), injection·recall, battalion commander injection | merged (3fcdacf9) | t48b1-officer-identity |
| T48b2 | Regiment commander appointment·dismissal (연대장 기용·해임) | Appointment·dismissal, officer assignment, staff 4-slot effects (ID outside the ledger) | merged (80c11fba) | t48b2-assign |
| T48c | Regiment commander favor (연대장 호감도) | Favor and a small decline on dismissal | merged (b543fd40) | t48c-favor |
| T48e | Stage 2 combat link (2단계 전투 연결) | Link regiment commanders with combat (default off, after T48d·T10·T24) | waiting | — |
| T49a | Save compatibility application 1 (저장 호환 적용 1) | Save messages, migration .bak, gray display in the list | merged (48961134) | t49a-save-messages |
| T49b | Tolerant reading (관대한 읽기) | Read settings·user maps·commander JSON tolerantly | merged (20132b4) | t49b-tolerant-read |
| T49c | Save compatibility release gate (저장 호환 출시 게이트) | Old-sample tests from campaign v1 to current | merged (20132b4) | t49c-compat-gate |
| M2 | Campaign morale·favor·public sentiment (캠페인 사기·호감·민심) | Unified 0~100, desertion after combat, gun unit price (ID outside the ledger) | in progress | kd/m2-morale-camp |
| TC-A | Briefing scale separation (브리핑 규모 분리) | Enemy scale separation and 3 information levels | merged (f7c6dcdf) | kd/tca-briefing |
| TC-B | Alert state (경보 상태) | Alert state (infiltration) of SK2 reconnaissance in force | merged (1e566261) | kd/tcb-alert |
| TC-C | Order injection·lock (명령 주입·잠금) | Event company order injection and order lock (D15 imperial grandson's independent action) | in progress | kd/tcc-inject |
| TC-D | Funds·foreign slots (자금·외인 슬롯) | funds, foreign slots, control limit, available supplies | merged (0b98b108) | kd/tcd-funds-slots |
| TC-E | Favor variables (호감도 변수) | favor.* variable registry and decision runtime | merged (2dbada73) | kd/tce-vars |
| TC-F | Mission variants (미션 변형) | SK utility flags, BT1 departure, pow.bandit | merged (43db8a13) | kd/tcf-variants |
| TC-G | Japanese railway zone (일본 철도 부속지) | Zone entry·fire ban area | merged (9490879b) | kd/tcg-annex |
| T57a | Regiment commander delegation core (연대장 위임 코어) | Delegation state·orders·AI drive filter | waiting | spec/regiment-delegation |
| T57b | Delegation disposition mapping (위임 성향 매핑) | Link school·traits to AI tendency keys (values left empty) | waiting | spec/regiment-delegation |
| T57c | Delegation UI (위임 UI) | Regiment card toggle·attitude·marker·alert | waiting | spec/regiment-delegation |

## 3. C1E1 missions·content (Andong campaign)
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| C1E1-d1 | Decision data (결정 데이터) | 16 decisions and node (EV/CH/IL) data JSON | waiting | spec/cards-c1e1 |
| C1E1-d2 | Starting state·economy constants (시작 상태·경제 상수) | E1 starting state and economy constants (R5) | wip-ready | kd/c1e1-d2-econ |
| C1E1-g1 | Episode graph (에피소드 그래프) | Node·choice·days·deadline table runtime | merged (bd1dc11f) | kd/c1e1-g1-episode-graph |
| C1E1-g2 | Episode screen (에피소드 화면) | Choice·decision·organization·deadline warning screen | waiting | spec/cards-c1e1 |
| C1E1-g3 | Mission event actions (미션 이벤트 행동) | Decision popup, variable writes, morale increase/decrease | merged (4f059987) | kd/c1e1-g3-events |
| C1E1-g4 | Objective extension (목표 확장) | Surrender·morale collapse victory, stronghold N points, negotiation timer | merged (d0b3a1ff) | kd/c1e1-g4-objectives |
| C1E1-g5 | Refugee unit (피난민 유닛) | Non-combat refugee unit definition and escort objective check | wip-ready | kd/c1e1-g5-refugees |
| C1E1-g6 | Organization unlock (편성 해금) | Unlock organization such as field guns via unlock.* | wip-ready | kd/c1e1-g6-unlock |
| C1E1-m1 | MAP-A map (MAP-A 지도) | Yalu River west estuary map (medium) | merged (425a2c0d) | kd/c1e1-m1-map-a |
| C1E1-m2 | MAP-B map (MAP-B 지도) | Andong area map (large) | waiting | kd/c1e1-m2-map-b |
| C1E1-p1 | VIP portrait (VIP 포트레이트) | Placeholder portraits of main characters (optional, low priority) | waiting | spec/cards-c1e1 |
| C1E1-q1 | End-to-end tests (종단 시험) | Headless tests of scenarios S1~S4 + selftest additions | waiting | spec/cards-c1e1 |
| C1E1-q2 | OP1 performance smoke (OP1 성능 스모크) | Performance check with a large map and about 5,500 units (not balance) | waiting | spec/cards-c1e1 |
| C1E1-s1 | BT1 bridgehead (BT1 교두보) | BT1 mission JSON (basic form) | wip-ready | kd/c1e1-s1-bt1 |
| C1E1-s2 | SK1 bandit sweep (SK1 비적 소탕) | SK1 mission JSON | wip-ready | kd/c1e1-s2-sk1 |
| C1E1-s3 | SK2 reconnaissance in force (SK2 위력 정찰) | SK2 mission JSON (alert on entering the outpost zone) | wip-ready | kd/c1e1-s3-sk2 |
| C1E1-s4 | SK3 no looting (SK3 약탈 금지) | SK3 mission JSON (escort) | wip-ready | kd/c1e1-s4-sk3 |
| C1E1-s5 | SK4 cavalry vs cavalry (SK4 기병 대 기병) | SK4 mission JSON | wip-ready | kd/c1e1-s5-sk4 |
| C1E1-s6 | OP1 direct assault (OP1 강공) | OP1 direct-assault mission JSON (including the railway zone area) | wip-ready | kd/c1e1-s6-op1-assault |
| C1E1-s7 | OP1 encirclement (OP1 포위) | OP1 encirclement mission JSON (2 blocking points, 6 minutes, 2 breakthroughs, negotiation) | wip-ready | kd/c1e1-s7-op1-envelop |
| C1E1-t1 | Functional text (기능 문구) | Decision labels·persuasion display·deadline display (narrative keys only) | waiting | spec/cards-c1e1 |
| C1E1-u1 | Unit check (유닛 점검) | Unit·weapon·faction usability report (report only) | wip-ready | kd/c1e1-u1-unit-audit |
| C1E1-u2 | 5 missing units (누락 유닛 5종) | Fengtian cavalry·field gun, railway guard, refugees, volunteers | merged (4c309a4f) | kd/c1e1-u2-units |
| C1E1-v1 | Variant data (변형 데이터) | 3 BT1 approaches, D02 B departure, 3 SK flags | waiting | spec/cards-c1e1 |

## 4. UI
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T08 | Controls UI bundle (조작 UI 묶음) | Controls UI bundle and paper texture overhaul | merged (2e9aef58) | t08-ui-bundle |
| T17 | Command conveniences (지휘 편의) | In-mission save, results report, engagement alerts | merged (0eb92e86) | t17-cmd-convenience |
| T27 | Command UI 2 (지휘 UI 2) | Speed·pause commands, unit list, color-blind-friendly team colors | merged (4cf5c45f) | t27-cmd-ui2 |
| T38a | Settings hotkeys (설정 단축키) | Rebind hotkeys in the settings menu | merged (7274c711) | t38a-settings-keys |
| T38b | Help overlay (도움말 오버레이) | In-game hotkey help | merged (93ce79f4) | t38b-help-overlay |
| T38c | Command card overflow (명령 카드 넘침) | Command card cell overflow audit and fix | wip-ready | kd/t46-card-overflow |
| T46 | Command card split (명령 카드 나눔) | Alias for the same job as T38c (the card name uses T38c) | wip-ready | kd/t46-card-overflow |
| T38d | Paper UI phase 2 (종이 UI 2단계) | Bright paper palette, commander's desk, low-resolution audit | merged (5f601bc3) | kd/t38d-paper-ui |
| T38e | Unit list·codex (부대 목록·도감) | Scrolling for lists over 18 rows, in-combat codex, tooltips | merged (e9e1642f) | t38e-unitlist-codex |
| T38f | Home read-only tabs (홈 읽기 전용 탭) | Campaign home follow-up tabs as read-only real screens | merged (b622d1d8) | t38f-home-readonly-tabs |
| T38g | UI regression harness (UI 회귀 하네스) | uifuzz clicks and per-resolution screenshot sheets | merged (f5a76d42) | t38g-ui-harness |
| T38h | Fuzz hitting the campaign (퍼즈가 캠페인 두드리기) | Test where the fuzz presses the campaign home (--fuzz-campaign) | wip-ready | t38h-fuzz-campaign |
| T38i | Tutorial link (튜토리얼 연결) | Link the tutorial to the campaign home | unknown (before merge, commit 3e414441) | t38i-tutorial-link |
| T38k | Game font (게임 글꼴) | Replace the game font with an OFL font | merged (07cb63f0) | t38k-fonts |
| T38l | Title strings (제목 문자열) | Replace the game title strings (window·main menu·HUD) | merged (d1158c9a) | t38l-title-strings |
| T38m | Overlay cleanup (오버레이 정리) | Screen overlap cleanup (declutter) | merged (2ca199c6) | t38m-overlay-declutter |
| T38n | Monitor environment (모니터 환경) | The game follows the KD_MONITOR setting (window mode only) | merged (a13672af) | kd/t38n-monitor-env |
| T45 | Menu hiding (메뉴 숨김) | Hide the legacy menu, AI vs AI goes to the spectate tab | merged (88735b1e) | kd/t45-menu-hide |
| T47 | Operations map zoom (작전 지도 확대) | Zoom of the campaign operations map tab | merged (cherry-pick ead14ae4) | t47-campaign-map-zoom |
| T48d | Regiment commander·staff management UI (연대장·참모 관리 UI) | Regiment commander·battalion commander·staff management screens (tabs 8/9/12/14) | merged (b5a10afc) | t48d-commander-ui |
| T52a | Engagement alert history (교전 알림 이력) | Engagement alert history and telegram strip | merged (c88bb69a) | kd/t52a-alert-log |
| T52b | Minimap ping (미니맵 핑) | Minimap multiple pings and hover highlight | unknown (tip e4829534 before merge) | kd/t52b-minimap-pings |
| T52c | Photo mode (사진 모드) | Photo mode | unknown (before merge) | kd/t52c-photo-mode |
| T52d | Difficulty campaign keys (난이도 캠페인 키) | Difficulty campaign keys and loader validation | unknown (before merge) | kd/t52d-difficulty-keys |
| T52e | Difficulty card UI (난이도 카드 UI) | Difficulty card screen | unknown (before merge) | kd/t52e-difficulty-ui |

## 5. Art·render·sound
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T11 | All-unit re-render (전 유닛 재렌더) | Redraw all unit art (render cells reserved after the end of the merge queue) | in progress | kd-rerender |
| T11b | Wide cell for prone firing (엎드려쏘기 넓은 칸) | Import wide-cell assets for prone firing | merged (bdd8d9a4) | kd/prone-wide-assets |
| T18 | Korean army tachanka (한국군 타찬카) | Korean army tachanka (horse artillery, included in the snapshot) | merged (a1a35e6) | main |
| T21 | Sound effects restoration (효과음 복원) | Connect sfxnew sound effects (sounds.json, SfxDirector) | merged (aaba4147) | sfxnew |
| T22 | Face prototype (얼굴 프로토타입) | Resume the face prototype (game-tools) | merged (bc520079) | gt-face-proto |
| T30d | Train art (열차 그림) | Train art spec and sprite plan (no code) | merged (8e51e5a5) | t30d-train-art |
| T32d | Engineer art (공병 그림) | Engineer art (prone-firing muzzle fix re-render) | merged (af357ce3) | kd/t32d-pioneer-art |
| T39e | March-fire muzzle (행진 사격 총구) | Recompute the muzzle flash position when shooting while walking | waiting (after T11) | — |
| T39f | Korea·Fengtian color (한·프 색) | Check applying Korea ×0.78 · Fengtian ×1.22 colors | waiting (after T11) | — |
| T39g | Gando dress (간도 복식) | Gando volunteer corps dress | waiting (after T11) | — |
| T39h | Infantry motion round 2 (보병 모션 2차) | Crawling, dragging the wounded, assault grenade, trench firing | waiting (after T11) | — |
| T39j | Prone-firing re-check (엎드려쏴 재확인) | Re-check the prone-firing art | waiting (after T11) | — |
| T43 | Branch symbols (병종 기호) | Zoomed-out branch symbol cleanup (faction insignia, howitzer·sniper added) | merged (d45b18f1) | kd/t43-branch-symbols |

## 6. Props·terrain·buildings
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T02 | Building size increase (건물 크기 확대) | Building size increase (HouseBlocks render) | merged (8d22ad5) | — |
| T07 | Railway terrain (철도 지형) | Railway terrain (armored train excluded) | wip-ready | rail |
| T28 | Cover forest (덮개 숲) | Clustering of cover props and irregular forest outlines (default off) | merged (c577c0f4) | t28-cover-forest |
| T39a | River ford terrain (하천 여울 지형) | Ford terrain, movement multiplier, map width | merged (ac77417) | river-r1 |
| T39b | River pathing (하천 경로) | River pathfinding and crossing AI | merged (ac77417) | river-r2 |
| T39c | River render (하천 렌더) | Narrow water strip, ford overlay, river test map | merged (ac77417) | river-r3 |
| T39d | River bridge (하천 다리) | Bridge terrain and bridge render (default off) | merged (ac77417) | river-r4-bridge |
| T39i | House ruins (가옥 폐허) | New art for ruined houses | merged (66d6b953) | t39i-ruined-houses |
| T55 | House variety, bundle (민가 다양화 (묶음)) | Overall coordination of house sprite variety | waiting | spec/house-variety |
| T55a | Korean village houses (한국 마을 민가) | 21 types of Korean village houses | unknown (before merge, 8faffd26) | t55a-house-korean |
| T55b | Manchu houses (만주 민가) | Manchu houses | unknown (before merge, 13610a92) | t55b-house-manchu |
| T55g | House generator (집 생성기) | Parametric house generator | merged (bd6e8859) | unknown |
| T55i | House import (민가 임포트) | Import of 42 house types (66 keys) | wip-ready | kd-t55i |
| T55m | House data (민가 데이터) | House import, theme keys, map data | merged (7730f79d) | t55m-house-data |
| T55s | House selection (민가 선택) | House selection logic (style·flip·tint) | merged (6c8dfb32) | t55s-house-select |

## 7. Tools·measurement·pipeline·documents
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T06b | Casualty re-measurement (사상자 재측정) | D19 C re-measurement card (follows T06 closure) | discarded | measure/t06b |
| T12 | Integrated balance measurement (통합 밸런스 측정) | A/B/C seeds 8 measurement (balance on hold 0.3) | wip-ready | measure/t12 |
| T13 | Release (릴리스) | Full selftest·golden·publish·record (once at the end of the merge queue) | waiting | — |
| T14 | Document overhaul (문서 재정비) | 4-layer structure, ops, PIPELINE v1 | merged (b5918a8) | docs-ops |
| T15 | Night soak re-report (밤 soak 재보고) | Night long-run test re-report (balance on hold 0.3) | wip-ready | measure/t15 |
| T20 | Repository move wrap-up (저장소 이동 마무리) | Delete old folders (user decision U02) | waiting (blocked) | — |
| T26 | Legacy mail blocking hook (레거시 메일 차단 훅) | Install the git hook blocking the old email (D20 A) | merged (installed) | .git/hooks |
| T31b | End line review (종료선 검토) | Mission end condition measurement card (closed) | discarded | measure/t31b |
| T33a | Map edit model (지도 편집 모델) | Headless document model, serialization, undo | merged (c5b30f4b) | kd/t33a-map-model |
| T33b | Map editor UI (지도 편집기 UI) | Editor window and terrain brush | merged (5207b8ff) | kd/t33b-map-ui |
| T33c | Map placement (지도 배치) | Placement of props·facilities·start points·zones | merged (c5148215) | kd/t33c-placement |
| T33d | Map railway tool (지도 철도 도구) | Editor railway tool (optional) | merged (022647f9) | kd/t33d-rail |
| T33e | Large maps (큰 지도) | Map editor large maps (cap 1500) | merged (4de8acd9) | kd/t33e-map-size |
| T41 | Crash log (충돌 로그) | Crash log recording | wip-ready | kd/crash-log |
| T44 | All-mission smoke (전 미션 스모크) | All-mission operation smoke harness | merged (f9dea2de) | kd/t44-smoke |
| T50d | Performance bench (성능 벤치) | Performance bench harness (measurement tool) | merged (ledger) | tools/perf-bench |
| T51a | Path safety (경로 안전) | Path safety fix and JSON limit checker | merged (34dd8d4f) | kd/t51a-path-guard |
| T51b | Mission package (임무 패키지) | MissionPackage open·validate·export (headless) | unknown (before merge, 7b55e0c3) | kd/t51b-mission-package |
| T51c | Editor export·import (편집기 내보내기·가져오기) | Including a pre-share check | waiting | spec/mapeditor-share |
| T51d | Editor gaps (편집기 빈틈) | Fill in incomplete editor features | waiting | spec/mapeditor-share |
| T51e | Incremental renderer (증분 렌더러) | Editor incremental renderer (on hold) | waiting | spec/mapeditor-share |
| T56a | Sprite cell format tool (스프라이트 칸 포맷 도구) | Cell B v2 format tool and a pilot with 1 infantry type | wip-ready | kd/t56a-pack-sheets |

## 8. Design-document cards (spec branches, no code)
| ID | Name | One-line description | State | Branch |
|---|---|---|---|---|
| T23 | Campaign home design (캠페인 홈 설계) | Campaign home design document (docs/CAMPAIGN_HOME_DESIGN.md) | merged (42fa4566) | spec/t23-campaign-home |
| TF-A | Campaign graph (캠페인 그래프) | Campaign graph data·loader·save object | waiting | spec/campaign-flow |
| TF-B | Progression link (진행 연결) | Next node after settlement, UI reads the graph | waiting | spec/campaign-flow |
| TF-C | Ending screen (엔딩 화면) | Ending judgment, ending screen, completion save | waiting | spec/campaign-flow |
| TF-D | Hint engine (힌트 엔진) | Tutorial hint engine and an off setting | waiting | spec/campaign-flow |
| TF-E | Hint content (힌트 내용) | Hint links and first-run guidance | waiting | spec/campaign-flow |
| TI1 | Translation foundation (번역 기반) | Loc foundation, language setting, pilot conversion | waiting | spec/i18n |
| TI2 | Bulk string conversion (문자열 일괄 변환) | Bulk conversion of S class consts to Loc properties | waiting | spec/i18n |
| TI3 | Non-S strings (비(非) S 문자열) | Hardcoded display strings outside S converted to keys | waiting | spec/i18n |
| TI4 | Data text translation (자료 텍스트 번역) | Translate data JSON text in side files | waiting | spec/i18n |
| TI5 | English translation (영문 번역) | Glossary, draft, check | waiting | spec/i18n |
| TI6 | English layout verification (영문 레이아웃 검증) | English layout, font, determinism verification and fixes | waiting | spec/i18n |
| TW1 | Weather concealment (날씨 은폐) | Weather concealment correction and extended-effect switch | waiting | spec/weather |
| TW2 | Blizzard (눈보라) | Add the blizzard weather type | waiting | spec/weather |
| TW3 | Weather transition fade (날씨 전환 페이드) | Weather transition fade and blizzard particle cap | waiting | spec/weather |
| RP1 | Combat timeline (전투 타임라인) | BattleTimeline sampler, MissionResults v2 | waiting | spec/replay-aar |
| RP2 | Results report, 3 pages (전과 보고 3쪽) | Summary·company·graph, 3 pages | waiting | spec/replay-aar |
| RP3 | Graph→replay (그래프→리플레이) | Click on the graph to move the replay viewpoint | waiting | spec/replay-aar |
| RV1 | Replay controls (리플레이 컨트롤) | Speed·skip·result window entry bar | waiting | spec/replay-aar |
| RV2 | Replay cursor (리플레이 커서) | Keyframes and rewind (seek) | waiting | spec/replay-aar |
| RV3 | Viewpoint slider (시점 슬라이더) | Unit tracking, event markers, full view | waiting | spec/replay-aar |

## 9. IDs outside the ledger and caveats
- Cards not in the ledger (ops/CARD_REGISTRY.md): M2 · M3 · T48b2 · T31b-endline50 (a card filename. Not a separate ID). M3 is the card title "campaign morale average -> link to mission-start faction morale" of kd/m3-morale-link, and its state is merged (e1d5404c cherry-pick; original 747cc3b4 is before the merge of kd/m3-morale-link).
- IDs with no card title and only a QUEUE title: T14 · T18 · T19 · T20 · T23 · T26 · T46 · T48b2 · T55i (titles written in the table above).
- PR1~PR5 are not card IDs. If a prop card is needed, the conductor decides before ordering.

## Future naming rules (proposal, not approved)
1. IDs are not changed. Existing IDs (e.g. T03, C1E1-s6) stay as they are.
2. In reports and conversation always write 'Korean name(ID)'. Example: 참호 돌파(T03).
3. The new ID format is `<area abbreviation>-<number>`. Area abbreviations: ENG combat engine · CAM campaign · MIS C1E1/mission · ART art · UI · PIPE tools · DOC documents · I18N translation.
4. Numbers are assigned only with `card_id.ps1` (do not number by hand).
5. This section is a proposal. Not applied before the conductor's approval.
