# T25 착검 멈춤 = 모든 돌격 (D02 A) · 전환 거리 3칸 유지 (D01 B)
- id: T25 | 역할: sim | 등급: D中 H3 (선택 메모: R3 S Wlight)
- 선행: T03 (wip 브랜치 위 스택, 병합 없음) | 후행: T12
- 핫스팟: assets/data/balance.json combat.bayonetFixOrderedOnly(현재 true) · src/Core/Bayonet.cs:43 · Snapshot/Replay 번호 · SelfTest(ChargeSelfTest.cs:82) · 골든
- 브랜치: `kd/t25-bayonet-all` (T03 브랜치에서 분기) | 상태: wip-ready | 담당: feature (Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- D02 A: "모든 돌격" 에 착검 멈춤 적용 (사용자 2026-10-22). 지금 = 돌격 명령에만 멈추고 자동 · 행진 · 역돌격은 멈춤 없음.
- D01 B: 행진 사격 -> 돌격 전환 거리 3칸 유지, 변경 안 함 (사용자 2026-10-22). marchChargeRange 현재 값 그대로. "예비 타이머 시작점만 필요 시 카드에서" -> 현재 marchChargeSince "phase" 유지 (변경 요구 없음).
- 일본군 BayonetPreFixed(처음부터 착검)는 그대로 (ROADMAP_LONG 11번).
## 범위 밖
전환 거리/타이머 시작점 변경; 새 명령; 봉천 대도 병종 규칙; 기병 칼 뽑기 수치
## 수용 기준 (측정 가능)
- bayonetFixOrderedOnly=false 로 자동 · 행진 · 역돌격 돌격도 BayonetFixSec(1.0~1.5초) 멈춤 후 돌격 (SelfTest 신규/수정 케이스)
- 한국 소총 자동 돌격 시 Stats.BayonetFixes 증가; 일본군은 멈춤 0
- marchChargeRange=5 · marchChargeSince="phase" 불변 (diff 에 없음)
- detcheck · snaptest · 리플레이; 골든 변경 시 사유 기록; R3 reviewer
## 측정
selftest charge 묶음 + tactics-obs A/B/C 시드 8 (돌격 횟수 · 손실비를 기준선과 비교, 악화 시 보고)
## 열린 질문
1. 값 변경만으로 부족하면(멈춤 중 사격/피격 처리) 어디까지? -> 보고 후 결정
2. 저장 형식 변경 없으면 FormatVersion 불변이어도 되나?
## 결과 (에이전트가 끝에 3~5줄만)
- bayonetFixOrderedOnly false (balance.json + Defs 기본값 + 주석), SelfTest charge 수정(스위치 기본값 · 행진 돌격 착검 멈춤). marchChargeRange 5 / marchChargeSince phase 불변. 카드 문구(3칸)와 실제 값(5) 불일치: 값 유지.
- 검증: charge/replay/combat3 PASS, snaptest OK, detcheck 8변형 동일. 골든 재생성 (행동 변화 정당: 자동 돌격에 착검 멈춤): 19501757040f0d62 d62e14463121dd02 -> 07def638b60429d7 e526368936c60409.
- 미실시: tactics-obs A/B/C 시드 8 측정, R3 reviewer (measure/reviewer 몫). Snapshot/Replay 번호 불변.
