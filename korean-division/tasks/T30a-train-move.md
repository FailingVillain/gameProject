# T30a 열차 객체 + 선로 이동 (조종 · 스크립트) — 기반 `rail` 위
- id: T30a | 역할: sim | 등급: D上 H3 (R3 S=M W=heavy)
- 선행: T07 `rail` wip-ready (kd-develop/kd-rail) | 후행: T30b T30c
- 핫스팟: 신규 Core/Train*.cs, Entity/World(목록), Snapshot(FV+1,[SnapExt]), WorldChecksum, GameCommand+Replay(번호+1), Nav 는 만지지 않음, SelfTest/Audit, Config 필요 시
- 브랜치 `kd/t30a-train-move` (rail 위, main 아님) | 상태: queued | 담당: -
## 확정 결정
docs/TRAIN_PLAN.md '확정 결정' 그대로: 조종 + 스크립트 이동 둘 다, 선로 위 전진 · 정지 · 후진, 선로 파괴 없음(선로 = 지형).
## 범위 밖
무기/피해(T30b), 화물 목표(T30c), 그림(T30d), 적 열차 AI, 분기기, 선로 파괴, 탑승.
## 수용 기준
1. 객차 N량 열차가 Rail 타일 경로 위에서만 이동: 전진/정지/후진 명령, 객차 간격 유지, 선로 끝에서 정지(관통 없음).
2. 스크립트: 경유점 열 + 정차 시간으로 같은 경로를 반복 재생, 동일 시드 2회 체크섬 동일.
3. 저장/복원 왕복 일치(snaptest), Audit 위반 0, Replay 재생 일치(새 명령 포함), FV+1 · Replay+1 문서화.
4. 기존 SelfTest · golden 회귀 없음(열차 없는 지도에서 체크섬 불변).
## 측정
SelfTest 전체 + snaptest + replay 시험 + golden. 기준선 docs/BASELINE_CURRENT.md. 열차 1~3대 지도에서 틱 비용 <1% 증가.
## 질문 (DECISIONS_PENDING 에도 기재)
- Q-A1 객차 = Unit 확장(병과 rail) 인가 새 Entity 종류인가? (권장: 새 Entity, 군집/전술 AI 간섭 차단. 에이전트가 코드 본 뒤 제안)
- Q-A2 분기/교차 지도는 1차에서 막는가(지도 시험 오류) 아니면 경유점으로 선택하나? (권장: 경유점)
- Q-A3 열차 위로 아군/적 보병이 있으면? 정지만(권장) / 무시 / 치임(신규 규칙이라 기본 금지).
- Q-A4 속도·간격·최대 객차 수 값(밸런스)? (권장: balance.json 신규 키, 값은 에이전트가 제안).
- Q-A5 철도 소품이 필요하면 T28 위 재스택?
## 결과
-
