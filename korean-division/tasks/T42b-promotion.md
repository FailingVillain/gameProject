# T42b 진급 후보 · 적용 (탭 9)
- id: T42b | 역할: feature | 등급: D中 H2 (R2 S<=100k Wlight) | 버전 0.2
- 선행: T42a | 후행: T42c
- 파일: Core/Campaign/(신규 CampaignPromotion.cs) · CampaignEconomyDefs/JSON(문턱 TBD) · UI/CampaignHome*.cs(탭 9) · Strings · Tests
- 브랜치 `t42b-promotion` (T42a 위) | 상태: queued
## 확정 결정
- 설계 정본 = docs/CAMPAIGN_FOLLOWUP_RULES.md (제안, 사용자 확인 대기 · 질문 §7). 훈련 숙련 = 장기 보류 (사용자 Q4, 2026-10-22 "MVP 제외"). 스토리 의존 칸 = TBD (스토리 세션).
- 장교 진급 상한 = 중령 (officers.maxPromotionRank, 사용자 2026-10-19 rankui). 사병 전투 중 진급 제거 유지.
## 범위 밖
군령권 (TBD 스토리: 문구 자리만) · 전투 중 진급 · 계급명 창작 (RANKS_BY_FACTION.txt 사용)
## 수용 기준
1) 후보 산출 단위 테스트(문턱은 JSON 임시, 상한 초과 불가) 2) [진급] 은 작전 사이에만, History 기록, 저장 왕복 3) 이월 UnitLine 계급이 다음 미션 CampaignCarry 에 반영 4) detcheck 불변 5) uifuzz 30 크래시 0
## 측정
build · savecompat · missions · --detcheck · --uifuzz 30
## 질문: Q3 Q7
## 결과
