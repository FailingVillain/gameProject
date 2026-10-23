# Engine notes - foundations (early v0.3/a3 notes + original preamble and old TOC) [split from ENGINE_NOTES.md 2026-10-23, body verbatim] (엔진 노트 - foundations)

> English note: the block below (original Korean header + stale table of contents, line numbers no longer valid) is kept verbatim and intentionally untranslated; for current locations see docs/engine/HEADING_MAP.md.

# 엔진 노트 (wargame 에서 물려받은 하위 시스템 상세)

> 필요한 절만 Grep 해서 읽는다. 원본: wargame CLAUDE.md (2026-10-18). 이름은 KoreanDivision / KD_ / .kds / .kdr 로 바뀜.

## 목차 (2026-10-20 · 본문 줄 번호; 재생성: grep -n '^## \|^### ')

  - v0.3 notes (L69)
  - a3 notes (교전 테스트 · 밸런스 도구 · AI 자원 · 세력 언어/보이스) (L77)
  - 저장 / 불러오기 (스냅샷) (L84)
  - 방향 변경 §0-1: 주어진 병력 · 탄약 회복 · 보급 마차 · 엄폐 (2026-10-19, `src/Core.. (L93)
  - 교전 개편 1차 (2026-10-18, ROADMAP §2-5) (L103)
  - 교전 개편 2차 (2026-10-18, ROADMAP §2-4b~g · §2-6) (L111)
  - 장갑 면역 · 신규 유닛 (2026-10-19, ROADMAP §2-4f) (L128)
  - 교전 4차 (2026-10-19, `src/Core/Combat4.cs`, 테스트 `Tests/Combat4Se.. (L132)
  - 부대 편제 · 깃발 대형 (2026-10-18, ROADMAP §2-4b 부대 편제 2단계, `src/Core/.. (L137)
  - 유닛 시트 LOD (2026-10-19, `src/Render/LodSprite.cs`, 테마 `assets/t.. (L144)
  - 정적 팀 (2026-10-19, 외교 시스템 대체, `src/Core/WorldTeams.cs`, 테스트 `Te.. (L163)
  - 지형 · 높이 · 지형 시야 (2026-10-19, ROADMAP ③ '지형 · 높이', `src/Core/Ga.. (L169)
  - 분대 · 부사관 · 계급 · 빈자리 점검 · 지휘 계통 지연 (2026-10-19, ROADMAP §2-7 "④.. (L185)
  - 날씨 · 시간대 (2026-10-19, ROADMAP '날씨 · 시간', `src/Core/Weather.cs`.. (L194)
  - 참호 자동 점유 · 포탄 구덩이 (2026-10-19, `src/Core/Trenches.cs` · `Rende.. (L200)
  - 미션 틀 (2026-10-19, `src/Core/Mission/` · `src/AI/MissionAi.cs` .. (L211)
  - 전술 AI 1단계 (2026-10-19, ROADMAP ⑤, `src/AI/Tactical/` — `Tactic.. (L226)
  - 전술 AI 2단계 (2026-10-18, ROADMAP ⑤, `src/AI/Tactical/` — `Tactic.. (L242)
  - 전술 AI 3단계 A — 기동 계획 · 미션 목표 · 철수 (2026-10-19, `src/AI/Tactical.. (L249)
  - 포탄 비행 시간 (2026-10-19, `src/Core/ShellFlight.cs`, 테스트 `Tests/Sh.. (L258)
  - 방열 사계 (2026-10-19, `src/Core/Combat2.cs` '방열 사계', 테스트 `ShellFl.. (L265)
  - N11 포병 사거리 · 거리별 퍼짐 · 지도 크기 5단계 (2026-10-19) (L273)
  - N21 포병 AI (2026-10-19, 브랜치 `artyai`, `src/AI/Tactical/Tactical.. (L279)
  - N22 포병 기여 · 보병포 · 정찰병 (2026-10-19, 브랜치 `arty2`, 테스트 `KD_SELFTE.. (L290)
  - 사거리 · 시야 · 지도 ×1.5 (2026-10-19, ROADMAP §2-8) (L298)
  - 숲 전투 (2026-10-19, 브랜치 `forest`, 테스트 `KD_SELFTEST_ONLY=forest`) (L306)
  - 정찰병 (2026-10-19, ROADMAP '정찰병 유닛', `src/Core/Scouts.cs`, 테스트 `.. (L310)
  - 충돌 · 대형 재설계 · 중대 행진 사격 (2026-10-18, 브랜치 `collision`, 분석 `docs/.. (L323)
  - 묶음 3 (2026-10-19): 돌격 사격 토글 · 사병 진급 · 비전투 어택땅 · 잔재 정리 (L335)
  - 플레이어 지점 포격 (2026-10-19, `src/Core/GroundFire.cs`, 테스트 `Tests/G.. (L340)
  - 전투 대형 · 역할군 · 중대 전술 틀 1단계 (2026-10-19, 브랜치 `formations`, `src/.. (L347)
  - 경기관총 사격 방향 · 스프라이트 정합 (N2 / HANDOFF 4o, 2026-10-19, 테스트 `Tests.. (L359)
  - 지휘 계급 · 임시 진급 · 승계 · 장교 경험치 (N3, 2026-10-19, `src/Core/Command.. (L365)
  - 전술 틀 2단계 — 역할 행동 · 지휘관 전환 · 중화기 보폭 (N4, 2026-10-19, `src/Core/.. (L383)
  - 공간 해시 · 지형 파생 캐시 (N16, 2026-10-19, `World.RebuildHash` · `Game.. (L397)
  - N12 대형 강박 줄이기 · 장교 위치 · 정찰병 묶음 (2026-10-19 사용자 승인, 브랜치 `n12`, .. (L402)
  - gapfill 대열 정비 — 명령 · 장교 판단 · 병사 숙련 (2026-10-19 사용자 결정, 브랜치 `ga.. (L412)
  - 최대 확대 LOD0 누락 감사 (N19, 2026-10-19, 테스트 `Tests/LodAuditSelfTest.. (L430)
  - ai412 — 돌격 허가 (결정 4) · 대대 역할 (결정 12) · 부사관 기본 편성 (2026-10-19 사.. (L435)
  - 중대 후퇴 임계 30~40% (2026-10-19, 브랜치 `retreat`, 데이터만) (L441)
  - 추격 · 후퇴 속보 · 기병 측면 (2026-10-19 사용자 결정, 브랜치 `pursuit`, `src/Cor.. (L444)
  - cavcmd — 기병 장교 · 기병 부사관 대리 (2026-10-19 사용자 결정, 브랜치 `cavcmd`, 테.. (L452)
  - 후퇴 사격 · 후퇴 뒤 재정비 · 포병만 아군 오사 · 기병 조금 엄격 · 미션 배치 (2026-10-19 사용.. (L459)
  - UI 묶음: 수류탄 X · 아군 오사 계수 · 분대 상태 아이콘 (2026-10-19, 브랜치 `uiicons`) (L467)
  - N21 돌격보병 = 카빈 + 수류탄 2 · 한국군 전용 (2026-10-19 사용자 결정, 브랜치 `carbin.. (L474)
  - 병종 이미지 마커 (2026-10-19 사용자 결정, 브랜치 `rankui`, 표시 전용 — 시뮬 · 저장 · .. (L481)
  - 교전 거리 · 엄폐 (engage, 2026-10-19, `WorldCombat.cs` ReactRange · .. (L483)
- N35 장교 교전 회피 · 정찰병 자위 사격 (2026-10-19, `src/Core/OfficerConduct.. (L495)
  - 세력별 계급 표 (ranks 브랜치, 2026-10-19, 원문 `docs/RANKS_BY_FACTION.txt.. (L499)
- 마적 · 공용 유닛 · 소총 변형 (N26 · N53 · N54, 2026-10-19, 브랜치 `factions`) (L507)
  - tweak1 — N44 전투 조정 묶음 (2026-10-19 사용자 결정, 브랜치 `tweak1`, 테스트 `T.. (L515)
  - 제압 재설계 N25 (2026-10-19 사용자 결정, 브랜치 `supp`, `src/Core/Suppressi.. (L521)
  - 도하 · 병목 돌파 · 역습 교리 (crossing, 2026-10-19 사용자 결정, `src/AI/Tacti.. (L531)
- 돌격보병 소탕 (N23) · 장교 경험 성향 (N55) — 2026-10-19 (L544)
- 경기관총 중대 편입 · 하향 (N64, 2026-10-20, 브랜치 `lmg`) (L549)
  - 탄약 (N76, 브랜치 ammo, 2026-10-20) (L553)
  - 탄약 A안 (N76b, 브랜치 ammo2, 2026-10-20) (L558)
- F1 접적 · 구역 사격 위치 (1차, 2026-10-20, 브랜치 `f1`, `src/Core/Contact... (L562)
- F1 2차 · 회전 · 선까지 전진 · 표시 (2026-10-20, 브랜치 `f2`, `src/Core/Cont.. (L567)
  - 캠페인 최소판 (2026-10-20, vs1) (L575)
  - 제압 재설계 2 (supp, 2026-10-20, `src/Core/Suppression.cs`, 테스트 `KD.. (L580)

### v0.3 notes
- Economy v2: 5 resources (funds, steel, fuel, supplies, manpower), neutral capturable metropolis/city/village/mine/oilfield (metropolis = start base, replaces HQ), supply_plant converts funds to supplies. No shortage penalties yet (user decision).
- Pathfinding: `src/Core/Nav.cs` + `WorldNav.cs` (hierarchical sectors, node-budget slicing). `FlowField` = full-Dijkstra reference only.
- Cities (`city: true`) are walk-on (reserved, not buildable); units on them use collision radius x2.
- Unit art comes from sibling folder `../game-tools/image` (voxel pipeline, `python export.py`). Theme PNGs are included in the repository (테마 PNG 는 저장소에 포함).
- Design doc from user: `wargame 기획서.hwp` (wargame's design document, reference only). Features beyond it need user planning docs first - ask.


### a3 notes: combat test, balance tool, AI resources, faction language/voice (교전 테스트 · 밸런스 도구 · AI 자원 · 세력 언어/보이스)
- **Combat test (교전 테스트)**: start screen [Combat test] or `--battle "infantry:1500,tank:300" --vs "recruit:1800,at_infantry:200" [--battle-map flat|terrain|city] [--battle-distance N] [--battle-ai-vs-ai] [--nofog]`. Spawns units only (`src/Core/Battle.cs` BattleSetup · World.CreateBattleWorld, `World.BattleMode`: no buildings, upkeep or population; the side that is wiped out loses), max 2000 units per side, the opponent is the tactical AI (`TacticalAi`, default - with `--ai battle` the old `BattleAi`). The setup is recorded in the replay header (`ReplayFile.Battle`) - spawns are reproduced deterministically from the setup, not from commands. F5 = same setup again (seed+1). Panel = `src/UI/BattlePanel.cs`. Combat maps = assets/maps/battle_*.json (hidden from the menu map list).
- **Balance tool (밸런스 도구)**: `KoreanDivision.exe --balance [all|matchups|aigames|quick] [--out folder] [--procs N] [--seeds N] [--budgets 500,2000,6000] [--arenas flat,terrain,city] [--ai-seeds N] [--ai-minutes M]`, `--balance report <folder>` (regenerate the report only), `--balance sim "A" "B" [map] [seed] [fog 0|1] [sec]` (one-match combat debug). Results `reports/balance/<timestamp>/report.md` + CSV. Code `src/Tests/BalanceTool.cs` (process-parallel: `--balance-worker`). Cost conversion: funds 1 · steel 2 · fuel 2 · supplies 2 · manpower 4. Balance values are not changed, only reported.
- **(Deleted - direction change §0-1) AI resource management (AI 자원 관리)**: `AiBase.PlanSupplies` (number of plants needed from upkeep · how long the stock lasts) -> StandardAi builds plants before barracks (Urgent) / when needed, `ManagePlants` switches operation by looking at funds · supplies, 2 supply depots at once, conscripts when steel is at the floor, tank production checks steel margin, oilfield first when planning tanks. Building sites keep a 2-tile margin around them (so tanks do not get stuck in gaps).
- **Faction language/voice (세력 언어/보이스)**: map `factions[].lang` (en/ru/de) -> `Faction.Lang` (default en, display only · checksum-irrelevant). `Sfx.PlayVoice` plays `assets/sounds/ww2/voice/<lang>/<event>_<n>.wav` (silent if missing, per-faction cooldown). Events · format in voice/README.txt. Game hooks it to select/move/attack/under attack/produce/death/build/capture.


