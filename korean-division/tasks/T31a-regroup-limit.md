# T31a 후퇴한 중대 재투입 제한  (제안 docs/CASUALTY_FOLLOWUP_PROPOSAL.md)
- id: T31a | 역할: sim | 등급: D上 H2 (R3 SM Wlight)
- 선행: 없음 (main 위). T05(ai3b2)와 Company.cs·tactics.json 겹침 -> 스택 필요 시 T05 뒤, 옵션 B 는 FV 충돌이라 금지
- 핫스팟: tactics.json regroup, TacticalAi.Company.cs(BeginRegroup/Regrouping/RejoinOk), 시험 Pursuit/Regroup
- 브랜치: kd/t31a-regroup (ai3b2 f74a675 위 스택) | 상태: wip-ready | 담당: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "3번 중대 단위 후퇴 판단도 적당해보인다. 그렇게 하자" (2026-10-21) — 후퇴 판단 자체는 불변.
- D19 C 의 원거리 명중 x0.7 은 되돌림(J1 A). 재시도 금지.
## 범위 밖
후퇴 문턱·명중률 변경, 새 저장 필드(FV 증가), 항복, 미션 종료 조건
## 수용 기준
- 새 키 전부 기본 = 현행(꺼짐): 키 없음/기본에서 detcheck 골든 불변, regroup 시험 PASS.
- 켠 값(A rejoinStrength 0.7 / C noRejoinBelow)으로 시드 8 대대전 평균 손실이 BASELINE_CURRENT 대비 감소(목표 방향 20~40%), 시간 초과 <= 1/8, 평지 절반 손실 150~200초 유지.
- 새 FV 증가 0 (옵션 C: 기존 Initial·Alive 사용).
## 측정
tactics-obs 시드 8 (meeting·assault), BASELINE_CURRENT, Stats.RegroupRejoins/Readvances 비교표
## 결과
- 구현: A = 기존 키 rejoinStrength (코드 0), C = 새 키 `regroup.noRejoinBelow` (기본 0=끔; RejoinOk 맨 앞에서 g.Alive < Initial×값이면 재전진 안 함 = 그 자리 사수·지원 사격, 접촉 밖·지원·증원 예외도 막음, 증원으로 넘으면 다시 판단). 새 저장 필드 0, FV 그대로 55. 측정용 env KD_OBS_REJOIN / KD_OBS_NOREJOIN (TacticsObs 전용).
- 미결 질문은 카드 권장안 채택: A+C 조합 / B(maxRejoins) 안 함 / C 동작 = 영구 사수(예비 대용, 과제 변경 없음 — 대대 층이 매 초 과제를 덮어써서 무상태로는 Reserve 과제 전환 불가) / 기본값 끔.
- 측정 (ai3b2 위, 시드 1-8, 600초, 손실 = 양측 평균, TO = 600초 미종료): meeting 끔 60.3% TO 1/8 → A 0.7 58.0% TO 3/8 → A+C(0.7/0.5) 57.8% TO 2/8 → A+C(0.7/0.67) 51.1% TO 5/8 · assault 끔 61.1% TO 0 → A 60.2% → A+C 0.5 60.0% TO 0 → A+C 0.67 57.4% TO 1/8. 재전진(양측 합, Readvances) meeting 끔 91 → A+C 0.5 55 (-40%).
- 판정: 목표(20~40%) **미달** — 감소 1~3%p(0.67 은 9%p 지만 meeting TO 5/8 로 기각선 3/8 초과). 원인: 미션이 진 쪽 70% 손실에서 끝나 진 쪽 손실 ≈70% 고정(평균 손실 하한을 정함) → 재투입 제한만으로는 못 내림, T31b(종료선) 몫. 평지 절반 손실은 기본 끔이라 불변(미측정).
- 시험: KD_SELFTEST_ONLY=pursuit2 PASS (새 검사 4: 기본 0, 1.01 막힘, 0.5+병력 충분 그대로, rejoinStrength 1.01), detcheck GOLDEN match(골든 불변), snaptest 생략(상태 변경 없음), 전체 selftest 생략.
