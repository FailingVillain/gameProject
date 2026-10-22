# T10 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) + 난이도 값
- id: T10 | 역할: sim | 등급: D上 H3 (선택 메모: R3 SM Wheavy)
- 선행: T03 (wip 브랜치 위 스택, 병합 없음; 알파 ① 은 main 에 있음: difficulty.json · FormatVersion 52) | 후행: T12
- 핫스팟: assets/data/difficulty.json · src/Core/Comms*.cs · Difficulty.cs · Snapshot/Replay · AI 배수 적용 지점 · SelfTest comms
- 브랜치: `kd/t10-comms-b` (T03 브랜치에서 분기) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 알파 뒤: ② 대포병 사격 ③ 야간 전투 보강 (조명탄 · 야간 시야/명중) ④ 전투 중 행군 피로 (ROADMAP_LONG 330행)
- 난이도 슬라이더에 AI 정확도 · 탄약 · 증원 묶음 (사용자 승인 2026-10-22)
- D15 A: 쉬움 전령 사로잡힘 끔 · 보통 AI 배수 1.0 · 어려움은 카드에서 후보 제시 (사용자 2026-10-22)
## 범위 밖
캠페인 화면; 무선 · 신호탄 · 전서구 (COMMS_ALPHA_PLAN 14); 어려움 값 확정 (후보만)
## 수용 기준 (측정 가능)
- difficulty.json: easy.comms.courierCapture=false; normal ai 3배수 모두 1.0; hard = 후보값 문서화만 (아래), 현 1.0 유지
- ②③④ 각각 기본 끄기 상태에서 기존 detcheck 골든 불변; 켜면 신규 SelfTest 케이스
- 난이도 3종 (easy/normal/hard) 시험 통과, 리플레이 재현, 저장 왕복; 번호는 병합 시점 main 에서 재예약
## 측정
통합 측정 T12; comms 묶음 · detcheck · snaptest
## 어려움 후보 (제안만, 승인 전 적용 금지)
accuracyMul / ammoMul / reinforceMul = 질문 1 에서 사용자가 수치 지정 (에이전트가 임의로 정하지 않음)
## 열린 질문
1. 어려움 AI 배수 3값 (후보 미정; 에이전트가 시안 3안 제시해도 되나?)
2. ②③④ 세부 규칙 (대포병 탐지/반응, 조명탄, 피로 수치) 은 LONG 에 한 줄뿐 -> 설계 질문 따로 받기? 또 COMMS_ALPHA_PLAN 조각 ②~④ (기마 전령 · 연대 지휘소 · AI Dispatch) 와 번호가 다름 -> 이 카드 ②~④ 는 ROADMAP_LONG 기준인지 확인
3. 쉬움 전령 사로잡힘 끔은 normal/hard 에서 기존 true 유지로 해석
## 결과 (에이전트가 끝에 3~5줄만)
-
