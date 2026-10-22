# T34d 3C-4 기병 측면 강도 + 역할 정리 (S11)
- id: T34d | 역할: sim | 등급: D中 H2 (R2 SM Wheavy) - 측정 우선, 코드 최소
- 선행: T34a (T34b/c 와 독립, 병렬 시 무거운 작업 2개 한도) | 후행: T34e
- 핫스팟: TacticalAi.Company.cs CavalryThink(:893)/PickCavalryPlan (T24/T31a 모두 수정 - 새 partial TacticalAi.Cav3C.cs 에서만) · Core/CavReact.cs (T24) · tactics.json "cavalry" 절 · ai_profiles.json
- 브랜치: kd/t34d-cavflank (T34a 에서) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 전술 AI 3C/3D 와 기병 측면 강도 보류 해제 (사용자 2026-10-22, 지휘자 전달). 합격 기준: 협동 지표 먼저, 그다음 승률 (사용자 2026-10-19 결정 1). 전차·대전차소총병 시뮬 제외 (2026-10-19). 3C/3D 설계 근거 TACTICAL_AI_STAGE3_REVIEW §3 §5-2. 공통 사양: docs/T34_3CD_SPEC.md (겹침·프로토콜·핫스팟·FV).
- 기병 돌격은 현재 그대로: 마비 1.5초·쿨다운 10~15초·제압 +300·속도 x1.8 (사용자 2026-10-21, QUEUE 18 B안 취소). 이 값 변경 금지.
- 기병 측면 강도 수치는 미결 (사용자 2026-10-19 결정 4) -> 이 카드는 '측정으로 후보를 제시'까지, 기본값 변경은 사용자 결정 후.
- AI 기병 돌격은 측면이 드러난 보병에게만이 기본, 무능한 장교일수록 정면 돌격 경향 (사용자 2026-10-19 결정 8) - 현재 코드는 strictCharge=false 일 때만 (Q4).
## 범위 밖
돌격 충격·피해 값 · 하마 신규 기능 · D07 최종 결정 (사용자) · 소총 AI 추격 변경(D07 B) · 기본값 변경 · 새 메커닉
## 수용 기준
- '강도' 정의 = SPEC §6 (flankOddsMul · flankMinSupp · flankOffset 기존 키). 겹침 `3c_cavflank_lo` (1.0/0.45) `3c_cavflank_hi` (0.5/0.15) 대 기본 (0.7/0.3), 각 팔 n>=24 (기병 포함 시나리오 CAV_* + n18_cav + tactics-obs).
- 보고할 지표: 기병 생존율 (plan 목표 >= 50%) · 측면 도착 생존 (현 0%) · 기병 대 기관총 교착 (목표 0/20) · 소총 대 기병 시간초과 (D07 현재 9/10) · 기병 킬/로스 · 돌격 수. 코드 변경이 필요하면(예: 겹침 키 읽기) 키 OFF 시 골든 불변.
- 결과로 lo/기본/hi 중 어느 쪽이 목표에 가까운지 표 + D07 선택지 제안 (결정은 사용자). selftest `KD_SELFTEST_ONLY=s3c4` (겹침 키가 문턱 계산에 반영 · OFF 불변).
## 측정
SPEC §3; 도구 tools/n18_cav.py n18_exchange.py, bn_campaign CAV_*, tactics-obs. 열린 질문 Q1~Q8 = SPEC §6, docs/DECISIONS_PENDING.md.
## 결과 (에이전트가 끝에 3~5줄만 기록)
