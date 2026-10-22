# T34a 3C-1 제압 인지 문턱 재조정 + 엄호 철수 (S5 + S9 일부)
- id: T34a | 역할: sim | 등급: D中 H2 (R2 SM Wheavy)
- 선행: T05 (ai3b2 f74a675 위 스택, 병합 없음) | 후행: T34b T34c
- 핫스팟: TacticalAi.Company.cs (호출 한 줄만) · 새 partial TacticalAi.Coop3C.cs · Core/TacticDrill.cs (T24 ③ chargeMinSuppression 겹침 - 같은 키 재사용) · Core/Pursuit.cs (T31a, 읽기만) · tactics.json/ai_profiles.json 키 끝 · AiProfile.cs
- 브랜치: kd/t34a-suppress (ai3b2 f74a675 에서, 사용자 지시 이름) | 상태: wip-ready | 담당: sim (Opus)
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
- 구현: 새 partial TacticalAi.Coop3C.cs + Company.cs 호출 5줄 + AiProfile 키 7 (기본 끔) + 겹침 3c/no3c/3c_suppthr/3c_coverwd. FV 없음 (표지 = FallbackUntil−StateTick·OrderTick). 엔진 틀(TacticDrill) 문턱은 AI 프로필을 못 읽으므로(AF1·리플레이) TacticsObs 측정 거울만. withdrawStep = 엄호로 남는 분대 몫(해석).
- 검증: detcheck GOLDEN match, s3c1 PASS (끔 = KD_AI_OVERLAY=no3c 와 120초 체크섬 같음), s3a ai3b ai412 pursuit2 supp tactical drill cavcmd PASS. snaptest 생략(저장 상태 변경 없음).
- 동작 확인(지휘자 지시: 측정·튜닝 보류): ai_bn_assault 시드1~8, 3c 켬 = 엄호 철수 6~14회/판 · 이탈 4~11 · 엎드린 적 표적 전환 479~1278 · 엎드림 약진 보류 0~27; 끔 = 전부 0. 승 끔 8/8 켬 5/8 (n=8, 노이즈 17%p — 판정 아님). 겹침 값은 초안(p50/p90 산출 미실행) → 기본 OFF 유지.
- 발견: 미션 aiProfile 문자열에 겹침을 붙이면 (korean+norepick 처럼 값 무변 겹침이라도) 같은 시드 결과가 달라짐 → A/B 는 KD_AI_OVERLAY 로만.

- FV 범프 (리뷰 지적): TacticsProfile 필드 7개 추가로 ObjCodec 저장 배치가 바뀌어 Snapshot.FormatVersion 55 -> 56. Replay 머리말에 프로필 없음 -> Replay 21 유지. FV-55 저장 거부 테스트 추가 (SaveCompatSelfTest). 병합 시 번호 재배정 필요.
