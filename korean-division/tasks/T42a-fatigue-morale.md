# T42a 캠페인 사기 · 피로 계정 (스키마 v4)
- id: T42a | 역할: feature | 등급: D中 H2 (선택: R2 S<=120k Wlight) | 버전 0.2
- 선행: t38f-home-readonly-tabs (= t19d tip 04a9149 + T38f) | 후행: T42b
- 파일: Core/Campaign/CampaignState.cs(RosterCompany.Fatigue/Morale, SchemaVersion 4) · CampaignEconomy.cs(CommitResult 정산) · CampaignEconomyDefs/JSON(임시값) · UI/CampaignHomeViews.cs · StringsCampaignHome*.cs · Tests/SaveCompatSelfTest.cs
- 브랜치 `t42a-fatigue-morale` (t38f 위) | 상태: wip-ready
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
- 상태 wip-ready (브랜치 t42a-fatigue-morale, 기반 t38f b622d1d). 빌드 0 오류 · savecompat PASS (신규 T42a 8건) · missions PASS · detcheck GOLDEN match · uifuzz 30 크래시 0. campaign 별도 묶음 없음 = savecompat 이 캠페인 시험 포함.
- 가정한 답: Q1 = 홈 표시 경고만 (탭 8 편제 막대 + 부대 탭 '지침(피로)' 표시, 잠금 없음, 전투 시뮬 불변) · Q2 = 임시값 (campaign_economy.json "임시": 투입 피로 +20 / 미투입 -15 / 지침 문턱 70 / 사기 승 +5 패 -10 / 손실률 1.0 당 -20 / 미투입 사기 +3) · Q8 = 현재 구현 확인: 출격 중대의 부상은 같은 중대에 남아 같은 중대로 치료 복귀, 미출격 시 병력 풀(Manpower.Wounded) 로 — 규칙 변경 없음(코드 무변경).
- 스키마: SchemaVersion 3 -> 4, RosterCompany.Fatigue (0) / Morale (70, MoraleStart), Sanitize 0..100. v3 파일은 기본값으로 이행 (시험), v4 왕복 (시험). 정산 = CampaignEconomy.SettleFatigueMorale (SettleBattle 2-1 단계, 출격 편성 있을 때만).
- 화면: 탭 1 사단 상태에 '피로 평균 · 사기 평균 · 지침 N개 중대' 한 줄, 탭 3 편제 행에 피로/사기 막대 (읽기 전용), 부대 탭 상태 열 '지침(피로)'.
- 비고: 사기 기본값 70 은 코드 상수 (RosterCompany.MoraleStart). 사기 하한/상한 0..100 고정.
