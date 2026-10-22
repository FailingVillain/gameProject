# T31a 후퇴한 중대 재투입 제한  (제안 docs/CASUALTY_FOLLOWUP_PROPOSAL.md)
- id: T31a | 역할: sim | 등급: D上 H2 (R3 SM Wlight)
- 선행: 없음 (main 위). T05(ai3b2)와 Company.cs·tactics.json 겹침 -> 스택 필요 시 T05 뒤, 옵션 B 는 FV 충돌이라 금지
- 핫스팟: tactics.json regroup, TacticalAi.Company.cs(BeginRegroup/Regrouping/RejoinOk), 시험 Pursuit/Regroup
- 브랜치: kd/t31a-regroup-limit | 상태: queued | 담당: -
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
-
