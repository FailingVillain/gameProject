# T42d 일정 · 일지 (탭 14 · 탭 12 확장)
- id: T42d | 역할: feature | 등급: D下~中 H1 (R1 S<=80k Wlight) | 버전 0.2
- 선행: T42c | 후행: -
- 파일: CampaignState.cs(Day, 이벤트 이력 확장) · CampaignEvents.cs(발동 기록) · UI/CampaignHomeReadonly.cs · Strings · Tests
- 브랜치 `t42d-schedule-log` (T42c 위) | 상태: queued
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
