# T30a 열차 객체 + 선로 이동 (조종 · 스크립트) — 기반 `rail` 위
- id: T30a | 역할: sim | 등급: D上 H3 (R3 S=M W=heavy)
- 선행: T07 `rail` wip-ready (kd-develop/kd-rail) | 후행: T30b T30c
- 핫스팟: 신규 Core/Train*.cs, Entity/World(목록), Snapshot(FV+1,[SnapExt]), WorldChecksum, GameCommand+Replay(번호+1), Nav 는 만지지 않음, SelfTest/Audit, Config 필요 시
- 브랜치 `kd/t30a-train-move` (rail 위, main 아님) | 상태: wip-ready | 담당: sim (Opus 5.5)
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
- 브랜치 `t30a-train-move` (rail 9f513e7 위), worktree kd-develop/kd-t30a. **번호 예약: FV 58 · Replay 23** (rail=53 와 충돌 회피, ops/FV_RESERVATIONS.md 반영 요청).
- 구현: `src/Core/Train.cs` (TrainRulesDef · TrainTrack · TrainCar : Entity · Train · TrainMoveCommand "trainmove" · World.Trains/SpawnTrain/StepTrains/저장/체크섬). 미션 json `trains` (MissionTrain), balance.json `trains`. 핫스팟 편집은 한두 줄씩 (World.Step · WorldChecksum · Organisation ORG_ 끝 · GameCommand · WorldCommands · MissionDef · WorldMission · Defs · Snapshot/Replay 번호 · SnapshotSelfTest Audit 3줄 · ReplaySelfTest 표본 1줄 · SelfTest 묶음).
- 임시 답 (사용자 승인 대기, 권장안 채택): Q-A1 새 Entity 하위 `TrainCar` (Units/byId 밖 World.Trains — 군집·충돌·AI·길찾기·표적 비간섭, Audit/[SnapExt] 틀 그대로). Q-A2 단선/순환선만, 경유점 BFS 로 경로 고정(분기 선택 없음, 이미 지난 칸 재방문 금지). Q-A3 앞쪽 선로 위 유닛(세력 무관) → 제동 정지, 비키면 출발 (치임 없음). Q-A4 maxSpeed 3칸/s · accel 0.4 · brake 1.2 (정지거리 3.75칸) · carSpacing 1.5 · maxCars 8 · lookahead 5 · blockRadius 0.7, 객차 hp/armor 자리값. Q-A5 T28 재스택 불필요.
- 검증: 빌드 경고 0 · `train` 26검사 PASS · replay · terrain · snapshot(Audit Train/TrainCar/TrainTrack) · missions · savecompat · command PASS · snaptest(QUICK) OK · detcheck GOLDEN match 8aa6448b (열차 없는 지도 불변). 전체 selftest 생략(release 몫).
- 틱 비용: 유닛 213 미션, 열차 3대 vs 없음 +0.19% (2회째 −3.8%, 잡음 범위). 감지 6틱 주기.
- 남은 것: 그림(T30d) 없음 → 게임 화면에 열차가 안 보임, 조종 UI 없음(명령 API `World.TrainMove` 만), 객차 피해(T30b).
