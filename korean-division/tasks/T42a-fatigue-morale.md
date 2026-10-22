# T42a 캠페인 사기 · 피로 계정 (스키마 v4)
- id: T42a | 역할: feature | 등급: D中 H2 (선택: R2 S<=120k Wlight) | 버전 0.2
- 선행: t38f-home-readonly-tabs (= t19d tip 04a9149 + T38f) | 후행: T42b
- 파일: Core/Campaign/CampaignState.cs(RosterCompany.Fatigue/Morale, SchemaVersion 4) · CampaignEconomy.cs(CommitResult 정산) · CampaignEconomyDefs/JSON(임시값) · UI/CampaignHomeViews.cs · StringsCampaignHome*.cs · Tests/SaveCompatSelfTest.cs
- 브랜치 `t42a-fatigue-morale` (t38f 위) | 상태: queued
## 확정 결정
- 설계 정본 = docs/CAMPAIGN_FOLLOWUP_RULES.md (제안, 사용자 확인 대기 · 질문 §7). 훈련 숙련 = 장기 보류 (사용자 Q4, 2026-10-22 "MVP 제외"). 스토리 의존 칸 = TBD (스토리 세션).
- 전투 시뮬 불변 (FV·detcheck GOLDEN 불변). 효과 = 홈 출전 경고/문턱만 (Q1 답 전엔 경고 표시만, 잠금 없음).
## 범위 밖
전투 안 사기/후퇴 문턱 연결 · 수치 튜닝(0.3) · 훈련 효과 · 부상 복귀 규칙 변경(Q8 답 후 배치 규칙만)
## 수용 기준
1) v3 저장 -> v4 자동 이행, v4 왕복 (savecompat) 2) 정산 시 투입 +x / 미투입 -y 단위 테스트(수치는 JSON 임시) 3) 탭 1 평균 한 줄 + 탭 3 막대, 읽기 전용 4) detcheck GOLDEN 불변 5) 빌드 0 · uifuzz 30 크래시 0
## 측정
build · KD_SELFTEST_ONLY=savecompat · missions · --detcheck · --uifuzz 30
## 질문 (DECISIONS_PENDING): Q1 Q2 Q8
## 결과
