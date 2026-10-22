# T34c 3C-3 제병 협동 시점 (포 · 기관총 · 보병) (S6)
- id: T34c | 역할: sim | 등급: D上 H3 (R3 SM Wheavy) - 저장 필드 1개
- 선행: T34b | 후행: T34d(선택)
- 핫스팟: TacticalAi.Artillery.cs (T24 observerMinEnemyDist - 관측자 조건 불변) · TacticalAi.Regiment.cs 돌격 예약 부분 · TacticalState.cs (T31a 근처, TacRegiment 필드 1) · Snapshot.cs FV +1 · SnapshotSelfTest.Audit · AiProfile.cs
- 브랜치: kd/t34c-coop (T34b 에서) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 전술 AI 3C/3D 와 기병 측면 강도 보류 해제 (사용자 2026-10-22, 지휘자 전달). 합격 기준: 협동 지표 먼저, 그다음 승률 (사용자 2026-10-19 결정 1). 전차·대전차소총병 시뮬 제외 (2026-10-19). 3C/3D 설계 근거 TACTICAL_AI_STAGE3_REVIEW §3 §5-2. 공통 사양: docs/T34_3CD_SPEC.md (겹침·프로토콜·핫스팟·FV).
- 곡사포는 관측자 (장교·정찰병) 가 본 곳에만 자동 사격, 지도상 '알려진 위치' 자동 사격 없음 (사용자 2026-10-19). 예약은 관측된 표적에만 건다.
- 저장 형식 변경은 3단계에서 한 번에 올린다는 방침 (2026-10-19 결정 3) - 이 카드가 3C 의 유일한 FV (+1).
## 범위 밖
대포병 사격 (Q-G3) · 포 탄막 이동(P10) · 곡사포 관측 규칙 변경 · 지점 사격 명령 변경 · 기본값 변경
## 수용 기준
- `artillery.assaultReserve`(false, 겹침 3c_assaultres): 기동 중대가 돌격 거리 x `reserveDistMul`(2.0) 안에 들면 연대가 포 표적 고정 + 첫 착탄 후 `firstImpactDelaySec` 에 돌격 허가, 근접 위험 거리에서는 연신. 예약 시각 = TacRegiment `AssaultRsvTick`. `tactics.mgFlankFire`(false, 겹침 3c_mgflank): 기관총을 돌격 표적 측면 칸 사격 위치로.
- 곡사포 조준점이 돌격 표적 12칸 안 8% -> >= 30% · 곡사포 살상 108 -> >= 140 (ArtyFx C 5판, B 12판 - 기준선 N21) · 돌격 개시 20초 전~개시 사이 착탄 >= 2발 비율 >= 50% (판 수 명시, 승률 주장 아님이라 n 규칙은 K/E·승 퇴행 확인에만).
- 퇴행 금지, 골든 불변 (필드는 기본 0), snaptest + Audit, `KD_SELFTEST_ONLY=s3c3`.
## 측정
SPEC §3 + `KD_ARTYFX=1` (tools/artyfx_scen, run_n21.sh). FV 번호는 지휘자가 지정 (SPEC §5).
## 결과 (에이전트가 끝에 3~5줄만 기록)
