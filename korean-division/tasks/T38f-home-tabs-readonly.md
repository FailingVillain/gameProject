# T38f 캠페인 홈 후속 탭 -> 읽기 전용 실화면 (순서 제안 + 1차 3탭)
- id: T38f | 역할: spec -> feature | 등급: D中 H1 (R2 M<=150k Wlight)
- 선행(blockedBy): T19c wip-ready(t19c-home-tabs) | 후행: -
- 핫스팟: UI/CampaignHome.cs(Tab 열거형·배치) · StringsCampaignHome.cs · Core/Campaign 읽기만. 저장 형식 무변경 (읽기 전용 뷰)
- 브랜치: ui/t38f-home-tabs (base = t19c-home-tabs; T38d 의 종이 UI 와 CampaignMenu 충돌 -> t19c 병합 정리 뒤 재적용) | 상태: queued
## 이미 있음 (t19c)
3화면 15탭 틀, MVP 5탭(1 요약 · 2 신병/부대 · 4 병참 · 10 작전 지도 목록 · 13 본국) 구현. 나머지 10탭 = 자리표시.
## 확정 결정
- Q1 3개 화면 중앙 9 + 좌 3 + 우 3 = 15탭 (2026-10-22). 3 편제·5 장비·6 훈련·7 외교·9 진급·11 정보·12 기록·14 일정·15 연구 탭 본문 = 후속 (T19c 카드). 설계 정본 docs/CAMPAIGN_HOME_DESIGN.md.
## 범위 밖
쓰기 동작(편제 변경·훈련·연구 실행) · 시간/계절 · 새 규칙 · 종이 스킨.
## 수용 기준
1) 1차: 이미 데이터가 있는 읽기 전용 탭만(후보 12 기록 · 5 장비 재고 · 3 편제 트리). 순서는 확인 필요 Q6. 2) 각 탭 데이터 없음 상태 문구, 예외 0. 3) 작은 [-][+] 버튼이 있다면 T38g 이후 퍼즈 검증. 4) savecompat PASS(스키마 불변) · uifuzz 30 예외 0.
## 측정
build · missions · savecompat · CampaignHomeSelfTest · uifuzz · 탭별 시트.
## 결과
-
