# T30c 화물열차 = 미션 목표 + 미션/UI 연결 — T30b 위 스택
- id: T30c | 역할: sim+feature | 등급: D上 H2 (R3 S=M W=light)
- 선행: T30b | 후행: T30e
- 핫스팟: Mission/MissionDef (목표 종류·열차 배치·스크립트 사건), MissionState(FV+1), WorldMission, MissionResults, MissionAi/Manoeuvre(호송 목표 재사용만), UI Strings/목표 패널·미니맵 표식
- 브랜치 `t30c-freight-objective` (t30b-train-weapons ca1cdb8 위, 워크트리 kd-develop/kd-t30c) | 상태: wip-ready | 담당: sim (Opus) | FV 59 그대로 (새 저장 상태 없음) · Replay 23 그대로 (명령 추가 없음) — FV 60 예약 불필요
## 확정 결정
TRAIN_PLAN: 화물열차 = 미션 목표 (호송 · 탈취 · 파괴), 스크립트 이동.
## 범위 밖
적 열차 AI, 화물 적재/하역 연출, 보급·경제 연동, 선로 파괴, 새 승리 조건 외 보상.
## 수용 기준
1. MissionDef 에 열차 배치(세력 · 시작 선로 위치 · 객차 구성 · 경로 스크립트) 필드; 검증 오류 메시지(선로 없는 위치 등).
2. 목표 3종: 호송 = 화물열차가 목적 구역 도착(기존 escort 의 count/area 재사용), 파괴 = 화물 객차 파괴, 탈취 = 화물 객차 점령(조건은 Q-C1 결정대로).
3. 목표 표시: 목표 패널 문구 · 화물 객차 표식(미니맵/월드), 결과 화면에 객차 손실 기록.
4. 저장/복원 · 체크섬 · Audit · 열차 없는 미션 회귀 0.
## 측정
SelfTest 미션 시험 + snaptest + golden. 목표 3종별 소형 시나리오 각 1건 단위 시험.
## 질문
- Q-C1 '탈취'의 정의: 화물 객차 근처 일정 시간 점령(기존 capture 방식)? 점령 후 열차 소유가 바뀌는가? (권장: 소유 변경 없이 목표 완료만)
- Q-C2 화물 객차 HP 0 = 호송 실패 / 파괴 목표 달성 외 다른 결과는 없음으로 해도 되나?
- Q-C3 화물 내용(탄약·식량 등)을 보상으로 연결하나? (권장: 1차 없음)
## 결과
- 권장안 채택 (사용자 확인 대기): Q-C1 탈취 = 살아 있는 화물 객차 radius(기본 3칸) 안을 seconds 초 단독 확보 (기존 capture 규칙: 점령 가능 아군만 · 적 유닛 없음, 적만 있으면 감소), 소유 변경 없음 — 목표 완료만. Q-C2 화물 객차 HP 0 = 호송 실패 · 파괴 목표 진행, 그 밖의 결과 없음 (+ 목표 판정만: 탈취는 화물 객차 전멸 시 실패, 호송은 남은 열차가 모두 기관차 파괴로 멈추면 실패 — 영원히 진행 중 방지). Q-C3 보상 연결 없음 (순수 목표 결과).
- 미션 json: 목표에 `"train": "<열차 tag>"` (escort · capture · eliminate 만) + capture 용 `"radius"`. 화물 객차 = balance.json trains.cars `"freight": true` (화차 wagon). 열차 배치 필드는 T30a `trains[]` (세력 · cars · path · waits · loop · script · start) 재사용. 검증 (InvalidDataException + 메시지): trains null/null 항목, train tag 없음 · 빈 값, 화물 객차 없음, tag 와 동시 사용, 종류 밖(defend), faction 범위, escort area · count 1~화차 수, capture area 금지 · seconds 0~36000 · radius 0.5~32 (NaN/무한 거부), eliminate percent (0,100].
- 구현: `src/Core/Mission/FreightObjective.cs` (검증 · 판정 · FreightCount/Centroid), WorldMission.EvalObjective 1줄 분기, AI ObjectiveFor 호송 → Escort/Intercept · 탈취 → Take/Hold (기준점 = 화물 객차 중심, 적은 보이는 것만). UI: 목표 패널 문구 (StringsMission ObjTrain*) · 월드 화차 표식 (고리 + 탈취 반경 원) · 미니맵 고리 (적 열차는 보일 때만), 결과 화면 + 결과 json `trains` (객차 · 화차 손실). 새 상태 없음: 진행 = ObjProgress (이미 저장 · 체크섬), 정의 = 미션 json 텍스트 (Def.Hash 텍스트 해시 → 열차 없는 미션 불변).
- 시험: KD_SELFTEST_ONLY=freight 21검사 PASS (검증 15종 메시지 · 호송 완료 26초/화차 파괴 실패/기관차 파괴 실패/count 1 · 파괴 100%/50%/기관차≠화차 · 결과 손실 · 탈취 경합 정지/1초/완료/소유 불변/반경 밖/전멸 실패 · AI 과제 · 탈취 중 저장 왕복 바이트 동일 + 이어 돌림 · 체크섬 포함 · AI 전 세력 결정성). train · trainweapons · missions · snapshot(Audit) · savecompat · replay PASS, snaptest 85 OK, uifuzz 30 예외 0, detcheck GOLDEN match.
- 측정: 열차 없는 미션은 판정 경로 불변 (o.Train null 분기 1개) — 틱 비용 측정 생략. 탈취 판정 = 화차 상자 ± 반경 앞 거르기 후 유닛 × 화차 거리 (float 제곱 거리, 기존 capture 구역 판정과 같은 방식).
- 리뷰 후속 (커밋 1건): (1) AI 탈취/차단 기준점이 viewer=0 (전지) 이라 안 보이는 적 화차 위치를 알던 문제 → 호송(아군 열차)만 전지, 탈취 · 차단은 항상 자기 세력 시야 (안개 적용). 안 보이면 기존 규칙대로 해당 목표는 AI 과제 없음(None, 다음 목표/기본 행동). 새 메커니즘 없음. 시험: 안 보이는 화차 = None, 시야 확보 후 = Take.
- 미션 작성 주의: 호송 열차가 기관차를 잃지 않고 구역 밖에서 멈추는 경우 (비순환 선로 끝) 는 실패가 아님 (canMove 는 Stalled 만 봄) — 호송 미션에는 반드시 제한 시간(timeout) 목표/패배 조건을 둘 것.
- 알려진 한계 (이월): 섬멸 판정 (WorldMission.cs ~443, 470) 은 Units 만 센다 — 열차 객차 · 차량은 세지 않음.
