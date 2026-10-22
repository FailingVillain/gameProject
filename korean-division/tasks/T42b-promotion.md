# T42b 진급 후보 · 적용 (탭 9)
- id: T42b | 역할: feature | 등급: D中 H2 (R2 S<=100k Wlight) | 버전 0.2
- 선행: T42a | 후행: T42c
- 파일: Core/Campaign/(신규 CampaignPromotion.cs) · CampaignEconomyDefs/JSON(문턱 TBD) · UI/CampaignHome*.cs(탭 9) · Strings · Tests
- 브랜치 `t42b-promotion` (T42a 위) | 상태: wip-ready
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
- 상태 wip-ready (브랜치 t42b-promotion, 기반 t42a-fatigue-morale ba621fe). 빌드 0 오류 · savecompat PASS (신규 T42b 13건) · missions PASS · detcheck GOLDEN match (8aa6448b) · uifuzz 30 크래시 0.
- 가정한 답: Q3 = 사병도 작전 사이 진급 가능, 상한 정교(색인 7 = 상사급) · 후보 문턱 = 마지막 작전 사살 수(계급별 배열) · 장교 문턱 = 경험치(계급별 배열), 상한 중령(balance officers.maxPromotionRank) · 계급별 정원 대신 '작전 사이 합계 정원' 8명. Q7 = 군령권 필드 없이 문구 자리만 (스토리 TBD).
- 기본 꺼짐: campaign_economy.json promotionManual=false 면 이월은 기존 자동 계급 그대로, 탭 9 는 '규칙 꺼짐' 안내만 (후보 · 적용 없음). 켜면 이월 사병 계급은 작전 시작 계급에 머물고 (사살 환산 자동 진급 보류, 사살은 후보 근거로 보존) [진급] 으로만 오른다.
- 임시값 (JSON, 0.3 확정): promoSoldierKills [1,2,3,4,4,5,6,8,0] · promoSoldierMaxRank 7 · promoOfficerXp [0,60,180,420,900,0,0] · promoMaxPerBreak 8.
- 스키마: SchemaVersion 4 -> 5 (추가만). CampaignState.PromotionLog (PromotionRecord: AfterMission/Officer/UnitId/FromRank/ToRank) · PromotionsThisBreak (이월 병력이 새로 정해지면 0). v3 · v4 파일은 빈 목록 · 0 으로 이행 (시험). History(MissionRecord) 대신 별도 PromotionLog 에 기록 (미션 행과 뜻이 달라서).
- 코드: Core/Campaign/CampaignPromotion.cs (Candidates · Promote · BlockOf · CarriedRank), CampaignState.Copy 가 CarriedRank 사용, UI/CampaignHomePromotion.cs + StringsCampaignPromotion.cs (탭 9). 기존 T42a 시험 2곳의 SchemaVersion 상수 4 -> 5 갱신. 전투 시뮬 · 체크섬 · FV 무변경.
