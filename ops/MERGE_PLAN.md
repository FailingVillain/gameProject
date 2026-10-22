# MERGE_PLAN - WIP 전체 병합 계획서 (작성 2026-10-22, main d4de9fb 기준)

## 사용자용 요약 (쉬운 말)
- 무엇이 일어나나: 모든 병합은 별도 워크트리 `merge/0.2` 에서 **로컬로만** 진행한다. 아무것도 push 하지 않는다.
- main 은 맨 마지막에만 `--ff-only` 로 올린다. 골든 재생성과 최종 selftest 가 끝난 뒤, 사용자가 "진행"이라고 한 뒤에만.
- 사용자 승인이 필요한 곳 3가지: ① 병합 시작 ② 골든 재생성(새 값 확인 후) ③ main 최종 올리기.
- 되돌리기: 그룹마다 시작 전 태그 `pre-merge-A`~`pre-merge-H`, 끝나면 `post-<그룹>`. 그룹 단위 되돌림은 사용자 확인 후에만.
- 위험한 단계: ① B2 `t38c-on-ui` (t38a 기반이라 순서 예외, §주의 B2) ② B5b·E2 (crossing 수정, 하천 FV 63→64) ③ E2b·E3·t34a (FV 번호 재배정) ④ D1~D2 열차 충돌 ⑤ F2·F4 캠페인 충돌 ⑥ G4·G4b (t38h 중복 커밋).
- 충돌이 설명에 없거나 체크섬이 예상과 다르면 그 단계에서 멈추고 보고한다. 임의 해결은 하지 않는다.

> 근거: 사용자 결정 AT(2026-10-22 15:0x) "병합 정책 해제 — crossing 실패(T24 의심)와 uiicons(T38c) 해소 뒤 통합 리허설 순서로 병합 개시. 병합 직전 병합 계획서 작성·사용자 최종 확인". 두 블로커 수정 브랜치: crossing = `kd/t24-crossing-fix` 390763a, uiicons = `kd/t38c-on-ui` 85e6658 (t38a-settings-keys 7274c71 위; 이전 `kd/t46-card-overflow` 54e8a54 는 T27 paging(Game.cs) 과 충돌해 **대체됨 - 병합 금지**, 기록용으로 남김).
> 출처: ops/QUEUE.md · CARD_INDEX.md · FV_RESERVATIONS.md(14:4x 정정, T39d 메모) · PIPELINE_LOG.md · STATE.md · DECISIONS.md(AT) · 리허설 카드 `kd-develop/kd-integration/korean-division/tasks/INTEGRATION-REHEARSAL.md`(integration/rehearsal 984c404) · docs/procedures/merge-procedure.md · gate.md · LIMITS.md · `tools/fv-renumber` a9366d4.
> 이 문서는 설계 결정을 포함하지 않는다. 모든 선택은 아래에 이미 적혀 있고, 적혀 있지 않은 상황(비기계적 충돌, 예상 밖 체크섬, 팁 불일치)은 **그 단계에서 중단하고 지휘자에게 보고**한다 (임의 해결 금지).
> 사용자 규칙(2026-10-22): 게임/편집기 창은 전체화면·테두리없음·최대화로 띄우지 않는다. 창 모드, <=1280x720, `run_game.ps1` 로 보조 모니터에서만. settings.json 의 Mode 를 바꾸지 않는다. 이미지 뷰어를 열지 않는다(파일만 쓴다). 병합 검증은 전부 헤드리스(selftest/detcheck/snaptest/uifuzz)이므로 창을 띄울 일이 없다.

## 0. 준비 (한 번)
1. 사용자 최종 확인(§4-1)을 받은 뒤에만 시작. push 는 어떤 단계에서도 하지 않는다.
2. 잠금: `mkdir C:/gameProject/.git/kd-merge.lock` + `started.txt` (merge-procedure.md). 30분 넘으면 갱신하거나 단계 사이에 재획득. 잠금이 남의 것이면 보고하고 정지.
3. 작업 브랜치·작업 트리: `C:/gameProject/.git` 의 main 은 C:/gameProject 에 체크아웃되어 있고 거기에 무관한 미커밋 변경(ROADMAP_SHORT 등)이 있다. 그래서 **main 에 직접 병합하지 않는다**.
   `git -C C:/gameProject worktree add C:/gameProject/kd-develop/kd-merge -b merge/0.2 main` 후 모든 병합은 그 트리의 `merge/0.2` 에서. main 은 §4-3 의 최종 확인 뒤 `git merge --ff-only merge/0.2` 로만 올린다(겹치는 미커밋 파일이 있으면 중단·보고).
4. 커밋 신원: `git -c user.name=seung -c user.email=seungwoo3656@gmail.com`. 병합 커밋 메시지 끝에 `Co-Authored-By: <실제 모델> <noreply@anthropic.com>` (commit-trailer 규칙: 병합 에이전트의 실제 모델).
5. 팁 검증: 각 단계 시작 때 `git rev-parse --short <branch>` 가 표의 @tip 과 같아야 한다. 다르면 그 단계 건너뛰고 보고(새 커밋 내용은 지휘자가 판단).
6. 충돌 규칙 공통(리허설과 동일): `git merge --no-ff --no-commit <br>` → `ops/*` 충돌은 항상 ours(ops 는 지휘자 소유). `ENGINE_NOTES.md` 충돌 = 절 합집합. 카드(`tasks/*.md`) add/add = ours(구현 브랜치가 먼저 들어온 판). `detcheck.golden` 은 중간에 손대지 않는다(브랜치 golden 이 끼어들면 HEAD 로 되돌림).
   **리허설 해결 재사용**: 표의 "리허 SHA" 는 그 병합 커밋. 충돌 파일마다 `git diff --quiet HEAD <리허SHA>^1 -- <파일>` 이 성공(= 병합 직전 그 파일 내용이 리허설과 동일)이면 `git checkout <리허SHA> -- <파일>` 로 해결본을 가져온다. 다르면 리허설 해결을 참고만 하고 아래 "예상 충돌" 설명대로 손으로 해결, 설명에 없는 충돌이면 중단.
7. FV/Replay 줄(Snapshot.cs `FormatVersion`, Replay.cs `FormatVersion`)은 병합 후 `korean-division/tools/agent/fv_renumber.ps1` (tools/fv-renumber a9366d4 를 §A1 에서 먼저 병합) 로 맞춘다: `-Report` 로 현재값 확인, `-Set <fv> <replay>`. golden 자동 재생성 옵션 `-RegenGolden` 은 **쓰지 않는다**(§4-2).
8. 병합 후 공통 확인(매 단계, 비용 작은 순): ① 빌드(`bin/merge`, heavy_gate 통과 `powershell -ExecutionPolicy Bypass -File C:\gameProject\korean-division\tools\agent\heavy_gate.ps1`) ② `--detcheck` ③ `KD_SELFTEST_ONLY=uiicons` ④ `KD_SELFTEST_ONLY=crossing`(uiicons+crossing 은 **항상**) ⑤ 표의 묶음. 상태 변경 단계는 `--snaptest`(`KD_SNAP_QUICK=1`), UI 단계는 `--uifuzz 30` 추가. 실행은 `dotnet bin/merge/KoreanDivision.dll` (apphost 가 런타임을 못 찾는 환경 이슈, STATE 기록).
   uiicons 는 **B2(t38c-on-ui) 병합 전까지 breach2 이후 단계에서 FAIL 이 정상**(11버튼>9칸), crossing 은 **B5b(crossing-fix) 전까지 FAIL 이 정상** — 그 구간은 해당 FAIL 이 "그 한 건뿐"인지만 확인(다른 FAIL 은 중단).
9. 단계마다 한 줄 기록: `ops/MERGE_QUEUE.md` 병합 완료 표(해시 · 브랜치 · 들어간 것 · FV/Replay · detcheck 첫 16자). 그 밖의 ops 는 쓰지 않는다(release 몫).

## 1. 병합 순서 (그룹 → 단계)
표기: `리허 SHA` = integration/rehearsal 의 병합 커밋(없으면 "미시험" = 리허설에 없던 브랜치, 충돌 예측은 파일 겹침 추정). 번호 = 병합 후 값 `FV/Replay`(변경 없으면 직전 유지). detcheck 열 = 병합 후 기대 체크섬(앞 16자).
**번호가 이 순서에 의존한다**: rail 58 · t30a 59 · t30b 60 · IsAi 61 · AE2 62(미발주, 비워 둠) · river 63 · 다리 64 · 연막 65. 순서를 바꾸면 번호가 달라지므로 바꾸지 않는다.

### 그룹 A - 인프라 · main 기반 단독 (FV 불변, 골든 불변: 기대 detcheck = main 8aa6448b)
태그 `pre-merge-A`. 서로 독립, 순서 고정.
| 단계 | 브랜치 @tip | 대체/비고 | 기반 | 예상 충돌 | 추가 확인 |
|---|---|---|---|---|---|
| A1 | tools/fv-renumber @a9366d4 | fv_renumber.ps1 추가만 | main | 없음 | `fv_renumber.ps1 -Report` 가 52/19 출력 |
| A2 | tools/monitor-launch @75a1712 | 리허 a293ad1 (X3 clean) | main | 없음 | - |
| A3 | kd/crash-log @69320b1 (T41) | 충돌 로그, 코드 일부 | c108343 | Program.cs 가능(미시험) | crashlog 묶음, `--uifuzz 30` |
| A4 | kd/endline-50 @476ceec (AK: 종료선 70→50) | 미션 json 수치만 | 0ca2157 | 없음 예상(미시험) | missions 묶음 |
| A5 | sfxnew @aaba414 (T21) | 리허 1bc839a (DECISIONS_PENDING 합집합) | d2c05d5 | docs/DECISIONS_PENDING.md 합집합 | `sfx`/`audio` 묶음(이름은 `--selftest` 목록에서 확인), sounds.json 유효 |
| A6 | gt-face-proto @bc52007 · t30d-train-art @8e51e5a | game-tools/그림만. t30d 는 리허 6314c28 (TRAIN_PLAN 합집합, T30a/b/c/e 카드 = ours) | - | 카드 add/add | 코드 변경 없음 확인 `git diff --stat` 에 src/ 없음 |
확인 A 끝: 빌드 + detcheck(8aa6448b 유지) + uiicons + crossing PASS(둘 다 main 에서 통과 상태). 태그 `post-A`.

### 그룹 B - 돌파 → 명령 큐 → AI 3B → T24/T25 → 통신 (sim, R3, 핵심)
태그 `pre-merge-B`. 모두 breach2 계열 스택이므로 **아래 순서 필수**.
| 단계 | 브랜치 @tip | 대체/비고 | 기반 | 리허 SHA / 예상 충돌 | FV/Replay | 묶음 | detcheck 기대 |
|---|---|---|---|---|---|---|---|
| B1 | wip/breach2 @6ad6c50 (T03) | wip-resolved/breach 7faccf0·wip/breach 는 breach2 안에 포함 → 병합 금지 | main(8d22ad5) | 87ba391 / ops/QUEUE | 53/20 | breach, snapshot, replay | 19501757 |
| B2 | kd/t38c-on-ui @85e6658 (T38c, **uiicons 블로커 수정**; t38a-settings-keys 7274c71 위). kd/t46-card-overflow 54e8a54 는 T27 paging 과 충돌해 **대체됨 - 병합 금지** | breach2 위 (기반 t38a 체인 포함, §주의 B2) | breach2 | 미시험 / CONTROLS.md, UiIconsSelfTest.cs, ops/QUEUE.md 충돌 예상 → **스태킹 시 수작업 해결**(ops 자동 ours 금지, 이 셋만 예외) | 불변 | uiicons(이제 PASS 필수), command, `--uifuzz 30` | 19501757 |
| B3 | wip/orderq2 @0377f8a (T04) | wip/orderq 는 원본 → 금지 | breach2 | 1ce5e56 / ops/QUEUE | 54/21 | orderq, firectl, replay, command | 19501757 |
| B4 | ai3b2 @f74a675 (T05) | wip/ai3b2 금지 | orderq2 | 5dd80ea / ops/QUEUE | 55/21 | ai3b, snapshot | 19501757 |
| B5 | kd/t24-s1s3 @07ac80c (T24) | | orderq2 위 | 53b7c9a / ENGINE_NOTES 합집합 | 55/21 | cavcmd, command, drill, officer | ccfbd1dd |
| B5b | kd/t24-crossing-fix @390763a (**crossing 블로커 수정**; t24-s1s3 07ac80c 뒤) | t24-s1s3 위 1커밋(도하 단계 observer keep=0) | t24-s1s3 | 미시험 / 없음 예상(TacticalAi.Artillery.cs) | 불변 | **crossing PASS 필수**, tactical, officer, command | ccfbd1dd 불변(브랜치 보고: 골든 불변) |
| B6 | kd/acting-delay-tier @7cfa0c2 (T37, 대리 부사관 지연 등급) | t24-s1s3 위; 팁은 크로싱 수정과 별개 | t24-s1s3 | 미시험 / 없음~소수 | 불변 | officer, command, replay | **변경됨(예상)**: 브랜치 자체 골든 6c96b4d6 → 통합값은 미시험. 새 값을 기록만, 재생성 금지 |
| B7 | kd/t25-bayonet-all @29695c3 (T25) | | ai3b2? (t24 와 무관 체인) | 8b65175 / 없음 | 불변 | charge, pursuit | bef5ffb0 (T37 이 없는 리허설 값; T37 병합 시 달라짐 - 새 값 기록) |
| B8 | kd/t31a-regroup @97ed026 | 기본 꺼짐 | ai3b2 | 0f20308 / ops | 불변 | pursuit2, tactical | 직전 유지 |
| B9 | kd/t31c-surrender @e9cd527 | 기본 꺼짐 | 5c6db29 | 2d2da45 / src/Tests/TacticsObs.cs: 두 env 훅 모두 유지 | 불변 | surr, snapshot | 유지 |
| B10 | kd/t10-comms-b @1c91c96 (T10) | | breach2 | be04a5b / ENGINE_NOTES, Replay.cs, Snapshot.cs 의 FV/Replay 줄 | **57/22** (56 비움, 리허설과 동일) | fieldops, replay, snapshot, savecompat | 유지 |
| B11 | kd/t10b-difficulty @4d74ce9 | t10 위 | t10 | 1ca8904 / 없음 | 57/22 | comms, fieldops | 유지 |
| B12 | kd/t16-trench @2cb118c | 미션 json 만(채택 기준 미달이지만 사용자 Z1 "변경 유지") | breach2 | 67c390d / 없음 | 불변 | missions, n32 | 유지 |
| B13 | kd/t34a-suppress @29c2479 (**이전 3d0f4de 대체**; 브랜치 내 FV 56 → §주의 B13-2 로 이동) · kd/t32a-pioneer @26cc9fd · kd/t32d-pioneer-art @2118f9b (**tip may move** - 그림 수정 진행 중) | 모두 기본 꺼짐/데이터·그림. t32d 는 t32a 위. t34a 는 ai3b2 위 | ai3b2 | 미시험 / TacticalAi*.cs·units json 가능 | 불변(t32a·t32d 의 55 브랜치값 → 57 유지) | ai, units, pioneer, 회귀: ai3b, tactical | 유지 |

주의 B13: t32a·t32d 의 Snapshot.cs 줄은 55 이므로 병합 시 충돌이 나면 **현재(57) 유지**.
주의 B13-2: t34a(29c2479) 는 브랜치 내 FV 56 이다. 56 은 리허설·계획 번호와 겹치므로 **B13 에서 바로 병합하지 않고 E 그룹 꼬리(E3 뒤)로 옮긴다**. 번호는 AE2 슬롯(62) **이후의 빈 번호**로 재배정하며, 정확한 번호는 지휘자가 FV_RESERVATIONS 와 대조해 확정한다(임의 지정 금지). t32b(33284fa, 브랜치 내 56) 도 같은 사정 - E3 에서 재배정.
주의 B2: kd/t38c-on-ui 는 t38a-settings-keys 7274c71 위에 있으므로(git 확인: 7274c71 은 85e6658 의 조상) 병합하면 t38a 체인의 내용이 B2 에서 함께 들어간다. 그룹 G 의 G1~G6 와 중복 병합이 되지 않도록 **실행 전 지휘자가 체인 범위를 확인**하고, 순서 예외를 DECISIONS_PENDING 에 올린다. 확인 전에는 B2 를 실행하지 않는다.
**B 끝**: 풀 확인 = 빌드 + detcheck + uiicons + crossing + snaptest + uifuzz 30. FV 57 / Replay 22. 이 시점 golden 이 main(8aa6448b)과 다르다 → **골든 파일은 그대로 두고** 시험은 `--detcheck` 출력이 위 열의 값과 같은지만 본다(불일치 = 골든 FAIL 이 정상). 태그 `post-B`.

### 그룹 C - 철도 · 덮개 숲 (표현·지형, 열차 이전)
태그 `pre-merge-C`.
| 단계 | 브랜치 @tip | 기반 | 리허 SHA / 예상 충돌 | FV | 묶음 | detcheck |
|---|---|---|---|---|---|---|
| C1 | rail @9f513e7 (T07; wip/rail 금지) | 23dba85 | 16a63bc / ENGINE_NOTES(합집합), Snapshot.cs FV 줄(53→**58**), ops | **58**/22 | terrain, snapshot, savecompat | 유지 |
| C2 | t28-cover-forest @c577c0f | rail | 1a2606b / ops 만 | 58 | props, forest | 유지 |
`fv_renumber -Set 58 22` 로 C1 후 확인. 태그 `post-C`.

### 그룹 D - 열차 (t30a → t30b → …) + 면역 표적 + 전멸 판정
태그 `pre-merge-D`. 리허설에서 가장 어려웠던 곳(t30a 8파일, t30b 6파일).
| 단계 | 브랜치 @tip | 기반 | 리허 SHA / 예상 충돌과 해결 | FV/Replay | 묶음 | detcheck |
|---|---|---|---|---|---|---|
| D1 | t30a-train-move @863f9d1 (T30a) | rail | 1df3a01 / DECISIONS_PENDING, GameCommand.cs, Replay.cs, Organisation.cs, Snapshot.cs, ReplaySelfTest.cs, SnapshotSelfTest.cs, ops. 해결 = 명령 등록·저장 호출·시험 줄 합집합(순서 HEAD→열차). **리허 SHA 체크아웃 재사용 우선** | 58→**59**/**23** | train, replay, snapshot | 유지 |
| D2 | t30b-train-weapons @ca1cdb8 (T30b) | t30a | 09413f5 / FogOfWar, Organisation, Snapshot, Train.cs, SnapshotSelfTest, ops. 해결: 열차 저장이 ORG_→ENTS 로 이동 → ORG_ 의 WriteTrains/ReadTrains 삭제(OrderQueues 유지), Fog 합집합, Train 해시 theirs, Snapshot Audit 손 병합(OrderQueue 줄 + trainMask + TrainGun) | 59→**60**/23 | trainweapons, train, snapshot, replay | 유지 |
| D3 | kd/immune-target-fix @48bd6ce (T36) | t30b | 미시험 / 없음~소수(자동 표적 코드) | 60 (브랜치 59 → 줄 충돌 시 60 유지) | 그 카드 묶음 + ai, tactical | **변경됨(예상)**: 브랜치 자체 골든 fcf38519 336c39cd. 새 값 기록만 |
| D4 | t30c-freight-objective @3de60d5 | t30b | 7dda62d / ops | 60 | freight, missions | 직전 유지 |
| D5 | t30e-demo-mission @7f46a4c | t30c? (t30b 위) | 4e68859 / ops. traindemo 묶음 FAIL 1 = `train_demo.golden` (끝에서 재생성) | 60 | freight, traindemo(FAIL 1 기대) | 유지 |
| D6 | kd/trains-in-annihilation @2de288d (T40) | t30e | 미시험 / 없음 | 60 | train, missions | 유지(열차 미션 외 불변) |
`t30d-train-art` 는 A6. 태그 `post-D`.

### 그룹 E - FV 꼬리: IsAi · 하천 · AI 연막
태그 `pre-merge-E`. 번호 61/24 → (62/25 비움) → 63 → 64 → 65.
| 단계 | 브랜치 @tip | 기반 | 예상 충돌 | FV/Replay | 묶음 | detcheck |
|---|---|---|---|---|---|---|
| E1 | kd/isai-restore @5c08a38 (T35 / AF1) | main(592392d) | Snapshot.cs·Replay.cs 의 60/24 줄 → **61/24 로 재번호**; Replay.cs 머리말·체크섬 `if (f.IsAi) h.Add(0x4149)`(AI 아닌 세력 불변) 손 병합 가능(미시험) | **61/24** | replay, snapshot, surr(항복 27 대 27), savecompat | 유지 |
| E2 | river-r1 @5b7b9f1 → river-r2 @8ada7a9 → river-r3 @26df7f6 → river-r4-bridge @9d4b7be (T39a~d, 한 단계씩) | rail | 미시험 / Terrain.cs enum(Ford, Bridge 바이트 13), Snapshot FV 줄, MapFile, 렌더. r1 FV 54 → **63**, r4(브랜치 55) → **64** | **63→64**/24 | terrain, crossing, missions, snapshot, savecompat | 유지(브랜치 보고: 골든 불변) |
| E2b | kd/t33d-rail @ceaa067 → 9b72f2f (T33d, **독립 팁**; 이전 "H1 과 같은 팁" 정정) | rail + river r1–r4 (E2 뒤) | 미시험 / Snapshot·Replay FV 줄 없음 예상, 지도 편집기 쪽 파일 - 충돌 시 §보고 | 64 유지(변경 시 기록) | rail, river(crossing), missions | 유지 |
| E3 | kd/t32b-ai-smoke @33284fa (T32b) | ai3b2 | Snapshot FV 줄(브랜치 내 56) → **65**; tactics.smokeAi 저장; TacticalAi*.cs. 같은 56 을 쓰는 t34a(§주의 B13-2)는 E3 뒤 빈 번호로 재배정 | **65**/24 | smokeai, ai3b, snapshot | 유지(끔) |
E2b 주의: T33c 시험 `test_river_crossing` 은 'map' 참조 미션을 **건너뛰도록** 고친 뒤 실행한다(그 외 수정 금지). 
E2 후 비차단 1건: river-r2 의 `terrain.waterBlocksFire` 키가 balance.json 에 없음 → 리뷰 지시대로 **기본값 true 와 같은 1줄만 추가**(키 이름·값 변경 금지). 이 외 하천 수치는 건드리지 않는다.
E 끝: 풀 확인(빌드, detcheck, uiicons, crossing, snaptest, uifuzz 30). `fv_renumber -Report` 가 65/24 이고 FV_RESERVATIONS 표와 중복 없음. 태그 `post-E`.

### 그룹 F - 캠페인 축 (캠페인 Schema 만, sim FV 불변)
태그 `pre-merge-F`. 의존: gemini-import → t29b → t19a → t19b2 → t19c → t19d → t38f → t42d. 리허설: "같은 수정이 두 벌" 구간이 여기.
| 단계 | 브랜치 @tip | 대체 | 리허 SHA / 예상 충돌과 해결 | 묶음 |
|---|---|---|---|---|
| F1 | gemini-import @63080df (T01, 336 파일) | wip/gemini-import 금지 | bdab8cd / ops/QUEUE 만 | missions, savecompat, lodaudit, snapshot |
| F2 | t29b-on-t09 @a89b4d2 (T09·T29 대체) | **t09-camp-slice, t29-campaign-economy-json 병합 금지** (t29b 가 대체) | ea8edf2 / WorldMission.ScaleAmmo 같은 수정 2벌 → T29b 천분율판 | missions, savecompat |
| F3 | t19a-save-slots @43e69b1 | | ad89c8e / CampaignState.cs, GameMission.cs, CampaignMenu.cs(5곳): T01 도우미(HasNullElements·SaveUnlessLoadFailed·CommitResult) 유지 + T19a BackupBroken·SaveLive·SaveWithAuto | savecompat, missions |
| F4 | t19b2-synced @4980e7b (T19b 포함 - t19b-ammo-shells 단독 병합 금지) | | 45735c5 / campaign_economy.json, CampaignEconomy.cs, CampaignEconomyDefs.cs, CampaignState.cs, WorldMission.cs, MissionSelfTest.cs: T29·T19a 체리픽 중복 → 캠페인 파일 theirs, 무기별 탄약 배율 theirs (Schema 3) | missions, savecompat |
| F5 | t19c-home-tabs @9260e36 | | a698886 / ops | doctrines 전 단계 확인: missions |
| F6 | t19d-doctrines @04a9149 | wip/doctrines 금지 | dbdb2be / ops | doctrines, missions, officer, savecompat |
| F7 | t38f-home-readonly-tabs @b622d1d | t38f 와 t19d 는 같은 팁이던 시기가 있었음; 현재 t19d 위 | 미시험 / 없음~소수(홈 탭) | missions, savecompat, 캠페인 퍼즈 |
| F8 | t42d-schedule-log @06ee9bd (t42a·b·c 포함, **한 번에**; 최종 팁 - 57f5440 리뷰 수정 포함 뒤) | t42a/b/c 단독 병합 금지 | 미시험 / CampaignState.cs(스키마 v4~v7 이행 줄), campaign_economy.json 가능. 스키마는 v7 까지 순서 유지 | savecompat(이행 v3~v6 포함), missions |
F 끝: 캠페인 Schema 7, sim FV 65 불변, detcheck 불변. 태그 `post-F`.

### 그룹 G - UI 체인 · 지휘 편의
태그 `pre-merge-G`. t08 → (t17, t27) → t38g → t38b → t38a. **uiicons · uifuzz 30 필수**.
| 단계 | 브랜치 @tip | 비고 | 리허 SHA / 예상 충돌 | 묶음 |
|---|---|---|---|---|
| G1 | t08-ui-bundle @2e9aef5 | | 7d36407 / ops | uiicons, command |
| G2 | t17-cmd-convenience @0eb92e8 | | d136b32 / ENGINE_NOTES 합집합 | command |
| G3 | t27-cmd-ui2 @4cf5c45 | | 51daad0 / ops | command, uiicons |
| G4 | t38g-ui-harness @f5a76d4 | (t38h 는 아래 G4b. 팁에 새 커밋이 생겼으면 §보고) | 미시험 / Program.cs, Ui.cs 소수 | uifuzz 30 |
| G4b | kd/t38h-fuzz-campaign @f126551 (**e5f76ec 는 f5a76d4 의 중복 체리픽 → 드롭**; f126551 은 실작업) | t38g 병합 뒤. 주의: f126551 은 e5f76ec 를 **조상으로 포함**하므로 그냥 병합하면 중복분이 같이 들어온다 → 지휘자 확인 후 `e5f76ec..f126551` 범위만 적용 | 미시험 / Program.cs, Ui.cs (t38g 와 중복 충돌 가능) | uifuzz 30 |
| G5 | t38b-help-overlay @93ce79f | | 미시험 / 없음~소수 | uifuzz 30, 도움말 시험 |
| G6 | t38a-settings-keys @7274c71 | **t38e-unitlist-codex 는 같은 팁(7274c71) → 별도 병합 없음** | 미시험 / 소수 | uifuzz 30 |
| G7 | kd/t45-menu-hide @88735b1 | 메인 메뉴·미션 목록 | 미시험 / MainMenu.cs, MissionMenu.cs(G4 의 1줄과 근접) | menuhide, uifuzz 30, missions |
| G8 | kd/t43-branch-symbols @d45b18f (**이전 b9c97a9 대체**) | QUEUE 에 T43 행 중복(ops 는 건드리지 않음) | 미시험 / UnitSymbols (t27 마커 근접) | uiicons |
G 끝: 풀 확인. 이미지 뷰어를 열지 않는다(시트 파일만). 태그 `post-G`.
G 보류: t38e-unitlist-codex(작업 중), t38i(작업 중) - 팁이 정해지기 전에는 계획에 넣지 않는다. 팁 확정 시 §보고 후 추가.

### 그룹 H - 도구 · 지도 편집기 · 그림 · 문서
태그 `pre-merge-H`. 게임 코드 불변 항목이 대부분 → 골든 불변.
| 단계 | 브랜치 @tip | 비고 |
|---|---|---|
| H1 | kd/t33a-map-model @c5b30f4 → kd/t33b-map-ui @5207b8f → kd/t33c-placement @c514821 (kd/t33d-rail 은 이제 독립 팁 ceaa067·9b72f2f → §E2b) | 별도 exe(MapEditor.App), 게임 코드 불변. 편집기 시험 `dotnet test`류(264+) 와 `--selftest` 편집기 묶음. **지형 목록에 Bridge 가 없으면 고치지 말고 질문으로 보고**(DECISIONS_PENDING). |
| H2 | t39i-ruined-houses @66d6b95 | 폐허 가옥 코드+시트. 시트 이미지 열지 않음. props/forest 묶음 |
| H3 | spec/* 카드 브랜치 (spec/cards-t24-t25-t10-t16, cards-t27-t28, cards-t30-train, cards-t31, cards-t32, cards-t34, cards-t39, cards-t42, spec/ui-backlog, spec/t19-split, spec/t23-campaign-home, spec/river-design, kd/t33-map-editor-design) | 문서 전용(카드·설계서). 충돌 = `tasks/*.md` ours, 그 외 문서 합집합, ops ours. 선택 단계(건너뛰어도 코드엔 영향 없음). `git diff --stat main..<br>` 에 src/ 가 있으면 그 브랜치는 건너뛰고 보고 |

## 2. 병합하지 않는 브랜치
| 브랜치 | 이유 |
|---|---|
| wip/breach, wip-resolved/breach, wip/orderq, wip/ai3b2, wip/rail, wip/gemini-import, wip/sfxnew, wip/doctrines, wip/battlereport, wip/gt-* , wip/rerender-1 | 원본/옛 패치 브랜치. 복원본(breach2, orderq2, ai3b2, rail, gemini-import, sfxnew…)이 대체 |
| wip/casualty, wip/casualty2 | D19 C 폐기, T06 closed (inert 보존) - 리허설은 합쳤지만 계획에서는 제외 |
| wip/chokefix | D14 보류 |
| t09-camp-slice, t29-campaign-economy-json | t29b-on-t09 가 대체 |
| t19b-ammo-shells, t42a, t42b, t42c | 각각 t19b2 / t42d 에 포함 (단독 병합 시 중복) |
| t38h-fuzz-campaign 의 e5f76ec | t38g f5a76d4 의 중복 체리픽 → 드롭 (실작업 f126551 은 §G4b 로 포함) |
| t38e-unitlist-codex, kd/t33c-placement 와 같은 팁 중복 | 별도 새 커밋 없음 (kd/t33d-rail 은 독립 팁이라 여기서 제외됨 → §E2b) |
| measure/t06b, t12, t15, t31b · kd/t16b-rootcause | 측정·분석 전용 (코드 없음) |
| kd-rerender (T11), gt-brender, gt-rerender-prep | 재렌더 진행 중 (큐 종료 뒤 별도) |
| kd/t44-smoke | main 과 동일(ahead 0), 의미 없음 |
| integration/rehearsal | 리허설 전용 (main 병합 금지, 리허설 카드 규칙). 해결 재사용 참조로만 |
| docs/roadmap-review2, docs/roadmap-sync | 문서 정리 브랜치 - 이 계획 범위 밖, 지휘자 판단 |
| kd/t34b~e, t32c, t38c/d/e(spec), T33d 이후, T42 이후 미발주 | 아직 코드 없음 |
| kd/t46-card-overflow @54e8a54 | T27 paging(Game.cs) 과 충돌 → t38c-on-ui(85e6658) 로 대체 (§B2) |
| t38e(작업 중), t38i(작업 중) | 팁 미확정 → 계획 제외 (§G 보류) |

## 3. 롤백 · 중단 규칙
- 단계 시작 전 `git status` 깨끗함 + `.git/MERGE_HEAD` 없음 확인. 병합 중 중단: `git merge --abort`. 병합 커밋 후 검증 실패: `git reset --hard ORIG_HEAD` (merge/0.2 위의 마지막 병합 하나만; merge-procedure.md 5단계 허용).
- 태그: 그룹 시작 전 `pre-merge-<A..H>`, 끝나면 `post-<그룹>` (`git tag`, 가벼운 태그). 그룹 단위 되돌림(`reset --hard pre-merge-X`)은 **사용자 확인 후에만**.
- 금지: `git push` 일체, `--force`, 브랜치 삭제(병합된 브랜치·워크트리 정리는 전부 끝나고 사용자 지시 후 - 평소 절차의 6단계 "제거"는 이번에는 보류), main 직접 병합(§0-3).
- 중단 조건: 설명에 없는 충돌 · 비기계적 충돌(로직 판단 필요) · 예상 밖 체크섬 변화 · 표에 없는 selftest FAIL · 팁 불일치 · 빌드 실패 → 그 단계를 되돌리고 보고(어느 파일/줄/FAIL 줄 포함). 같은 단계 재시도는 1회까지.
- 예외: 이 문서 §0-8 의 "정상 FAIL"(B2 전 uiicons, B5b 전 crossing, D5 traindemo golden)만 허용.
- 체크섬 기대와 다르면 되돌리지 않고 "기록 후 보고": 골든 재생성은 하지 않는다.

## 4. 사용자 확인이 필요한 곳
1. **시작 전 최종 확인(AT 가 요구)**: 이 계획서 전체. 여기서 승인받기 전에는 §0 의 worktree 도 만들지 않는다.
2. **골든 재생성**: 리허설 기준 새 골든 `bef5ffb0 822cdacd`(units 20→159, shots 2530→5359; 변화 지점 breach2→t24→t25). 이번 계획에는 T36(D3)·T37(B6)이 더해져 **값이 달라진다**(두 브랜치 모두 자체 골든 변경). 그룹 H 뒤 최종 `--detcheck` 값을 사용자에게 보여주고 OK 를 받은 뒤에만 `--detcheck --update` → `tools/detcheck.ps1` 8변형 + `train_demo.golden` 재생성. 중간 단계에서는 하지 않는다.
3. **main 올리기**: 최종(골든 재생성·release 전체 selftest 통과 후) `git -C C:/gameProject merge --ff-only merge/0.2` 직전에 확인. 겹치는 미커밋 파일이 있으면 중단.
4. 그룹 E 직전(FV 번호 65까지 소진, AE2 62 는 비움)과 H3 문서 병합 여부는 지휘자 판단(질문 목록은 korean-division/docs/DECISIONS_PENDING.md).
5. 시트 검수 15장(`C:\works\_review_sheets_2026-10-22`)은 병합과 독립 - 병합이 막지 않는다.

## 5. 작업 분할 · 무거운 풀 사용
- 무거운 작업 = 빌드, selftest 묶음, snaptest, uifuzz, detcheck. **에이전트당 동시 2개까지**(사용자 규칙 heavy-job-concurrency; 풀 한도는 ops/LIMITS.md 참조, 다른 에이전트와 공유) - 병합 에이전트는 한 번에 1개를 기본, 묶음 2개만 병렬. 병합 잠금 보유 중에만 게이트 `-Priority`.
- 에이전트 컨텍스트 200k 규칙 때문에 merger(sonnet) 를 **그룹별로 새로 띄운다**; 한 에이전트가 이어받지 않는다.

| 에이전트 | 범위 | 예상 토큰 | 무거운 작업 수(대략) |
|---|---|---|---|
| merger-1 | §0 + A + B1~B4 | 150k | 빌드 6 + 묶음 8 |
| merger-2 | B5~B13 | 180k | 빌드 9 + 묶음 14 |
| merger-3 | C + D | 150k (충돌 해소 큼; 리허 SHA 재사용) | 빌드 8 + 묶음 12 + snaptest 4 |
| merger-4 | E | 120k | 빌드 5 + 묶음 8 |
| merger-5 | F | 180k | 빌드 8 + 묶음 14 |
| merger-6 | G + H | 150k | 빌드 11 + uifuzz 8 + 묶음 12 |
| release | 골든(사용자 OK 후) + 전체 selftest 1회(약 12분) + snaptest + 8변형 + publish + 기록 | 80k | 전체 selftest 1 |
| sim(opus) 대기 | 설명에 없는 R3 충돌(B6/B7, D3, E1~E3) 중단 시에만 상담 | 필요 시 | - |
| reviewer | 선택: 각 그룹 끝 `git diff pre-merge-X..post-X` 결정성·Audit 확인 | 60k x 3 | 없음 |
- 직렬 순서 고정(스택·FV 의존). 그룹 안에서 병렬 병합은 하지 않는다(잠금 1개). 소요 예상: 합계 빌드 약 47회 · 묶음 약 68회; 풀 2 로 가정하면 병합 단독 6~8시간(실제는 게이트 대기에 좌우).
