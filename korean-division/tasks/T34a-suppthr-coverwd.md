# T34a 3C-1 제압 인지 문턱 재조정 + 엄호 철수 (S5 + S9 일부)
- id: T34a | 역할: sim | 등급: D中 H2 (R2 SM Wheavy)
- 선행: T05 (ai3b2 f74a675 위 스택, 병합 없음) | 후행: T34b T34c
- 핫스팟: TacticalAi.Company.cs (호출 한 줄만) · 새 partial TacticalAi.Coop3C.cs · Core/TacticDrill.cs (T24 ③ chargeMinSuppression 겹침 - 같은 키 재사용) · Core/Pursuit.cs (T31a, 읽기만) · tactics.json/ai_profiles.json 키 끝 · AiProfile.cs
- 브랜치: kd/t34a-suppthr (ai3b2 에서) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 전술 AI 3C/3D 와 기병 측면 강도 보류 해제 (사용자 2026-10-22, 지휘자 전달). 합격 기준: 협동 지표 먼저, 그다음 승률 (사용자 2026-10-19 결정 1). 전차·대전차소총병 시뮬 제외 (2026-10-19). 3C/3D 설계 근거 TACTICAL_AI_STAGE3_REVIEW §3 §5-2. 공통 사양: docs/T34_3CD_SPEC.md (겹침·프로토콜·핫스팟·FV).
- T24 ③ (돌격 제압 확인 chargeMinSuppression) 은 기본 0 으로 끔 (사용자 2026-10-22, D08) - 켜려면 겹침으로만.
## 범위 밖
후퇴 뒤 재편·재투입 (T31a 소관) · 새 명령/메커닉 · 수치 보너스 · 제압 시스템 자체 변경 · 기본 프로필 값 변경
## 수용 기준
- 데이터: `tactics.superioritySupp/chargeSuppression/retreatSuppression/boundSuppression` 을 겹침 `3c_suppthr` 로 새 분포 값 (먼저 교전 병사 p50/p90 측정으로 문턱 산출, 값 표를 카드 결과에 기록). 신규 키 `tactics.pinnedTargetWeight` (엎드린 적 중대 표적 가중, 기본 0) · `tactics.ownPronePivot` (아군 엎드림 비율 >= 값이면 약진 대신 화력 기반, 기본 0=끔).
- 엄호 철수 `3c_coverwd`: 신규 `tactics.coverWithdraw`(false) `withdrawStep` `coverSec`(15): 후퇴 결정 시 사격 진지 점수 높은 분대·기관총이 남아 coverSec 사격 후 교대 이탈.
- 측정 지표 (n>=24, 3C 켬 대 끔): 돌격 허가 중 제압 신호 비율 >= 50% · 돌격 40초 내 후퇴 <= 25% (T24 측정 28~43% 로 기준선 흔들림 - 같은 exe 대조로 판정) · 약진 엄호 >= 비약진 · 후퇴 이탈 생존 +20%p (pursuit 대비, 판 수 명시).
- 퇴행 금지 (SPEC §3), 기본값 detcheck 골든 불변, 신규 selftest `KD_SELFTEST_ONLY=s3c1` (키 OFF 시 동작 불변 · 문턱 경계 · 엄호 분대 선정 결정성).
## 측정
SPEC §3 프로토콜. tactics-obs A/B/C 시드 1~8 + 제압 재설계 측정 시나리오 + pursuit 후퇴 시나리오 (docs/supp_raw, pursuit raw). 종료선 70/50 어느 쪽이든 같은 exe A/B (Q-G1).
## 결과 (에이전트가 끝에 3~5줄만 기록)
