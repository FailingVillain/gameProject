# T42d 일정 · 일지 (탭 14 · 탭 12 확장)
- id: T42d | 역할: feature | 등급: D下~中 H1 (R1 S<=80k Wlight) | 버전 0.2
- 선행: T42c | 후행: -
- 파일: CampaignState.cs(Day, 이벤트 이력 확장) · CampaignEvents.cs(발동 기록) · UI/CampaignHomeReadonly.cs · Strings · Tests
- 브랜치 `t42d-schedule-log` (T42c 위) | 상태: wip-ready
## 확정 결정
- 설계 정본 = docs/CAMPAIGN_FOLLOWUP_RULES.md (제안, 사용자 확인 대기 · 질문 §7). 훈련 숙련 = 장기 보류 (사용자 Q4, 2026-10-22 "MVP 제외"). 스토리 의존 칸 = TBD (스토리 세션).
- 시간·계절 규칙은 추후 (규칙 7, 사용자 2026-10-21): 카운터만, 효과 없음.
## 범위 밖
계절/날씨 연동 · 명령 기한(문구는 TBD 스토리) · 전사자 명부 이름 · 훈장
## 수용 기준
1) Day 증가 후 저장 왕복, v3/v4 이행 2) 탭 14 연표에 이벤트·작전 결과 시간순 표시 3) 탭 12 일지 줄 추가, 읽기 전용 4) detcheck 불변 · uifuzz 30 크래시 0
## 측정
build · savecompat · --detcheck · --uifuzz 30
## 질문: Q6 Q7
## 결과
- 상태 wip-ready (브랜치 t42d-schedule-log, 기반 t42c-prisoners 66e7637). 빌드 0 오류 · savecompat PASS (신규 T42d 10건) · missions PASS · detcheck GOLDEN match (8aa6448b, 불변) · uifuzz 30 크래시 0 (캠페인 홈 미도달 기존 한계, 화면은 스크린샷: docs/art_review/t42d_tab14.png · t42d_tab12.png).
- 가정한 답: Q6 = 첫 승리 정산마다 고정 일수 (campaign_economy.json missionDays, 임시 1, 0 이면 일수 안 늘림; 미션 JSON days 는 안 만듦), 계절 · 시간 효과 없음 (카운터만). Q7 = 스토리 칸(군령권 · 훈장 · 전사자 명부 · 이벤트 문구) 필드 없음 — 이벤트는 id + 선택 번호만 저장, 제목은 기존 이벤트 정의에서 읽기만.
- 스키마 v6 -> v7 (추가만): Day · Timeline(Day/Kind/Id/Title/Choice, 최대 200). v3~v6 = Day 1 + 기록 수 x missionDays, 빈 연표 (시험). 새 상태에서 첫 정산 = 1일째 + missionDays. 반복 승리 · 패배는 일수 · 연표 불변.
- 코드: 정산 끝(CampaignEconomy.SettleBattle)에서 일수 + 연표 "mission", CampaignEvents.ApplyChoice 에서 "event" 기록. 탭 14(Tab.Schedule) 읽기 전용 본문(현재 일수 · 연표 최신 위 14건) = CampaignHomeViews.Schedule + CampaignHomeReadonly.DrawTabSchedule; 탭 12 작전 일지 줄에 "[N일]" 접두. 새 규칙 스위치 없음 (카운터만). T42a~c 시험의 버전 상수 6 -> 7. Tests/CampaignScheduleSelfTest.cs.
- 리뷰 수정 (t42a..d 적층 리뷰): (a) 마지막 미션 재승리 (이미 클리어 + 같은 EntryMission) 는 이월 사병 · 장교 · PromotionsThisBreak 를 건드리지 않음 (기존: 이월 병력 교체 + 정원 0 으로 -> 정원 8 우회 · 적용한 진급 소실). 최소 안전 동작 = 지난 미션 재승리와 동일하게 병력 유지, 재승리의 생존자 사살 수는 반영 안 함. (b) Sanitize: Day 상한 100000 (CampaignState.DayMax), 불러온 연표 200 으로 자름, Day 덧셈 long 으로 넘침 방지. (c) AddTimeline Day = max(Day,1). 시험: CampaignPromotionSelfTest 7b, CampaignScheduleSelfTest 5b.
- 아직 열린 항목 (미수정): 포로 EnemyKills 이중 집계 (사용자 결정 대기) · 죽은 패배 분기 · 장교 XP 미소비 (설계 질문).
