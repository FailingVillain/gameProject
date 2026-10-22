# T42c 포로 장부 · 송환/수용 (탭 7)
- id: T42c | 역할: feature | 등급: D中 H2 (통계 1개는 Sim 접촉; R3 S<=120k Wlight) | 버전 0.2
- 선행: T42b (+ T31c kd/t31c-surrender 의 항복 통계 형식 확인; 없으면 값 0 고정으로 진행) | 후행: T42d
- 파일: World 측정 전용 통계 1개 (GunShots 와 같은 방식, 저장/체크섬 없음) · MissionResults/DebriefReport.PrisonersTaken · Stock.Prisoners · UI 탭 7 · Strings · Tests
- 브랜치 `t42c-prisoners` (T42b 위) | 상태: wip-ready
## 확정 결정
- 설계 정본 = docs/CAMPAIGN_FOLLOWUP_RULES.md (제안, 사용자 확인 대기 · 질문 §7). 훈련 숙련 = 장기 보류 (사용자 Q4, 2026-10-22 "MVP 제외"). 스토리 의존 칸 = TBD (스토리 세션).
- 항복 켜기(AG1)는 0.4 로 미룸 (사용자 2026-10-22 VERSIONS) -> 이 카드는 발생 빈도를 건드리지 않는다.
## 범위 밖
편입(병력화) · 항복 로직/값 변경 · 민심 연동 · 수치 튜닝
## 수용 기준
1) 통계는 FV·체크섬 불변 (detcheck GOLDEN 불변) 2) 포로 수 -> 정산 장부 가산, 송환/수용 처리 후 감소, v4 왕복 3) 항복 꺼짐 = 0 이어도 크래시 없음 4) uifuzz 30 크래시 0
## 측정
build · savecompat · missions · --detcheck · --snaptest
## 질문: Q4 Q5
## 결과
- 상태 wip-ready (브랜치 t42c-prisoners, 기반 t42b-promotion 51b92c3). 빌드 0 오류 · savecompat PASS (신규 T42c 15건) · missions PASS · detcheck GOLDEN match (8aa6448b, 불변) · uifuzz 30 크래시 0 (단, uifuzz --fuzz-menu 는 캠페인 홈 화면에 도달하지 않음 — 기존 한계, 탭 7 은 --menu-page campaign 스크린샷으로 확인: docs/art_review/t42c_home_prisoners.png · t42c_debrief_prisoners.png). --snaptest 는 상태 변경 없음(World 무변경)이라 생략.
- 가정한 답: Q4 = 편입(병력화) 없음, 송환 · 수용만. Q5 = 발생원은 항복(기본 꺼짐)에 의존, 꺼져 있으면 0 — 통로만 만듦. Q7 = 스토리 칸 없음.
- 발생 통로: 새 World 통계를 만들지 않고 기존 MissionResults 계산(끝났을 때 Surrendered 인 채 살아 있는 유닛, 이미 FactionLine.Captured 로 계산됨)에서 적대 세력 합계 MissionResults.EnemyCaptured 를 추가 (시뮬 읽기만, 저장 · 체크섬 · FV 무접촉). 승리 정산(SettleBattle)에서만 Stock.Prisoners 에 가산 (노획과 같은 조건, 반복 승리 · 패배는 0).
- 처리: 송환 = 무료, 수용 = 1인당 보급 prisonerAcceptSupply (임시 2, campaign_economy.json, 0.3 확정), 작전 사이(Pending 없음)에만, [1명]/[전부] 버튼. 처리하면 장부가 줄고 누계(PrisonersRepatriated/Interned)가 늘어 표시됨. 수용 후 효과(민심 · 편입)는 없음. 새 규칙 켜짐/꺼짐 스위치 없음 (장부는 항상 켜짐, 항복이 꺼진 기본에서는 값이 0이라 동작 변화 없음).
- 스키마: SchemaVersion 5 -> 6 (추가만). Stock.Prisoners · CampaignState.PrisonersRepatriated/PrisonersInterned · DebriefReport.PrisonersTaken. v3~v5 는 0 으로 이행 (시험). T42a/T42b/T19b 시험의 버전 상수 5 -> 6 갱신.
- 코드: Core/Campaign/CampaignPrisoners.cs · UI/CampaignHomePrisoners.cs (탭 7, Tab.Diplomacy) · StringsCampaignPrisoners.cs · 작전 보고 창에 포로 줄 (있을 때만) · Tests/CampaignPrisonersSelfTest.cs.
