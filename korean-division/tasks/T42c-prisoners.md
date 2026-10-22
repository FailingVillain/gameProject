# T42c 포로 장부 · 송환/수용 (탭 7)
- id: T42c | 역할: feature | 등급: D中 H2 (통계 1개는 Sim 접촉; R3 S<=120k Wlight) | 버전 0.2
- 선행: T42b (+ T31c kd/t31c-surrender 의 항복 통계 형식 확인; 없으면 값 0 고정으로 진행) | 후행: T42d
- 파일: World 측정 전용 통계 1개 (GunShots 와 같은 방식, 저장/체크섬 없음) · MissionResults/DebriefReport.PrisonersTaken · Stock.Prisoners · UI 탭 7 · Strings · Tests
- 브랜치 `t42c-prisoners` (T42b 위) | 상태: queued
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
