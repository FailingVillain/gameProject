# T31b 미션 종료 조건 검토 (제안 + 측정, 엔진 코드 0)  (제안 docs/CASUALTY_FOLLOWUP_PROPOSAL.md)
- id: T31b | 역할: measure → spec | 등급: D中 H0 (R1 S Wlight)
- 선행: T31a 결과가 있으면 그 위에서 비교(없어도 main 에서 가능)
- 핫스팟: assets/missions/ai_bn_meeting|assault|ai_kr_vs_ft_1000.json (측정 사본만), 문서
- 브랜치: spec/t31b-end-review | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 대대전 목표 "10분 20~40% 손실" (ROADMAP_LONG 23, 사용자 2026-10-21)
- 확인 필요: 종료선이 '70%'인 것이 게임 규칙인지 측정 시나리오 설정뿐인지 -> 조사 결과 후자(JSON eliminate/losses percent 70), 캠페인 미션은 불변.
## 범위 밖
캠페인 미션 JSON 수정, 새 종료 종류(rout) 구현 (설계만), 엔진 코드
## 수용 기준
- 옵션 A/B/C 각각 시드 8 수치표(10분 시점 손실, 종료 시각, 승자 분포)를 문서에 채움.
- 코드·캠페인 데이터 diff 0 (측정용 사본은 scratch). 사용자 선택 질문 1개로 정리.
## 측정
tactics-obs 시드 8, percent 70/50/40 사본, BASELINE_CURRENT
## 결과
-
