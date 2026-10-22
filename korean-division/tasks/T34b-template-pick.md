# T34b 3C-2 AI 틀 선택 (측면 · 돌격 틀 사용) (S2)
- id: T34b | 역할: sim | 등급: D中 H3 (R2 SM Wheavy)
- 선행: T34a (문턱 값) | 후행: T34c
- 핫스팟: TacticalAi.Company.cs PickTemplate (:185, T24/T31a 모두 수정 - 호출 한 줄 + 새 partial) · TacticalAi.Manoeuvre.cs (T31a, 기동 제대 지정 읽기만) · CombatFormations.cs enum 읽기 · tactics.json templates · AiProfile.cs
- 브랜치: kd/t34b-tplpick (T34a 에서) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 전술 AI 3C/3D 와 기병 측면 강도 보류 해제 (사용자 2026-10-22, 지휘자 전달). 합격 기준: 협동 지표 먼저, 그다음 승률 (사용자 2026-10-19 결정 1). 전차·대전차소총병 시뮬 제외 (2026-10-19). 3C/3D 설계 근거 TACTICAL_AI_STAGE3_REVIEW §3 §5-2. 공통 사양: docs/T34_3CD_SPEC.md (겹침·프로토콜·핫스팟·FV).
- 전술 AI 는 틀을 기본 사용 (`tactics.templates` true, 사용자 2026-10-19). 부사관 지휘 = 화력-기동, 지휘 없음 = 틀 없음 (TacticAllowed) 유지.
## 범위 밖
새 틀 정의 · 침투(P6, 취소) · 대형 변경 · 부사관/지휘 규칙 변경 · 기본값 변경
## 수용 기준
- 신규 키 (기본 false): `tactics.tplPick.flank` (겹침 3c_tplflank: 연대가 S1 기동 측으로 지정한 대대의 바깥 중대 = FlankLeft/Right, 방향 FlankSign), `tactics.tplPick.assault` (겹침 3c_tplassault: 표적 둘레 제압 >= assaultMinSupp 이고 거리 <= 돌격 거리 x `assaultDistMul` 1.5 이면 Assault 틀). 전환은 DecideInterval 간격, 접촉 중 재배치 금지 (N4 교훈).
- 공격 중대 틀 시간 중 Flank+Assault 비율 0% -> 15~30% · 틀 켠 쪽 교환비 0.98 -> >= 1.1 (N4 시나리오 + C1 F_trench/T_r1.5, 팔당 n>=24 판으로 확인; 6판은 부족).
- 퇴행 금지, 골든 불변, `KD_SELFTEST_ONLY=s3c2` (조건별 선택 · 부사관 제약 · OFF 불변).
## 측정
SPEC §3. N4 시나리오(보병 150+기관총 12+장교 3, 평지·지형) x 시드 8 x 양쪽 바꿔서 + tactics-obs A/B/C 8시드. T24 ③ 를 켠 조합은 측정하지 않음 (끔이 승인 기본).
## 결과 (에이전트가 끝에 3~5줄만 기록)
