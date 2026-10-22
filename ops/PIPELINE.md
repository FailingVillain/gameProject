# PIPELINE - 작업 흐름 구조
버전: v2 · 갱신: 2026-10-22 · 정본: 이 파일이 작업 흐름 구조의 정본 (상태·대기열·결정의 정본은 STATE/QUEUE/DECISIONS).

**표준 흐름 (v2)**: 카드 → 구현(wip-ready) → **리뷰**(reviewer, 필수) → 수정 사이클 → 측정(목표가 있는 카드) → 통합 리허설 → 병합(사용자 허가 뒤).

## 1. 단계별 흐름
| 단계 | 담당 | 입력 → 출력 | 한도(LIMITS) | 병목 → 대응 |
|---|---|---|---|---|
| 입력 | 사용자 | 지시 → DECISIONS 답 / QUEUE 행 | - | 결정 25건 적체 → 저위험은 기본값 진행(D22) |
| 작업 정의 | spec(또는 지휘자) | QUEUE 행 → tasks/Tnn 카드 | 카드 ≈1.5KB · L 카드는 사전 분할 | 중복 조사 → MERGE_QUEUE 완료 표 + GIT_HISTORY_DIGEST grep 의무 |
| 발주 | 지휘자 | 카드 → 에이전트(역할 카드 + 카드 3줄 프롬프트) | 현재 한도는 ops/LIMITS.md 참조 · 파도 ≤12 | 지휘자 알림 싱크 → 파도마다 세션 교체 · QUEUE 행 낡음 → 발주 전 MERGE_QUEUE·main 대조(T18) |
| 코딩 | sim/feature/art/docs | 카드 → 브랜치 커밋 | 컨텍스트 200k | 핫스팟 충돌 → H≥2 같은 파일은 직렬 · 역할 오배정 → 코드 카드는 docs 금지(T26) |
| 로컬 검증 | 같은 에이전트 | 빌드·detcheck·자기 묶음·snaptest → 카드 결과 | 무거운 작업 = LIMITS.md 참조 | 게이트 대기 25분+ → 전체 selftest 는 release 만 |
| wip-ready | 같은 에이전트 | 카드 상태 wip-ready → 리뷰 발주 → MERGE_QUEUE 행(지휘자) | 잠금 금지 | 잠금 폴링 → 작업 에이전트는 잠금을 안 잡음 |
| 리뷰 | reviewer(읽기 전용, 필수) | wip-ready 브랜치 diff → 차단/높음/중간 결함 목록 | 브랜치 1건당 리뷰 1 | 리뷰 누락 → 리뷰 없이 다음 스택 발주 금지 |
| 수정 사이클 | 작성자 또는 수정 에이전트 | 리뷰 지적 → 같은 브랜치 1커밋 → 재검증 | 사이클 1회 = 1커밋 | 스택 하위 수정 → 상위 브랜치에 SendMessage 로 체리픽 지시 |
| 측정 | measure | main 커밋 → BASELINE_CURRENT 블록 | 비아트 heavy = LIMITS.md 참조 | 개별 측정 중복 → 통합 측정 1회(T12) · 목표 미달 → wip-ready 금지, 정직 보고(T06·T16) |
| 통합 리허설 | 지휘자(제안, 사용자 확인) | 병합 순서대로 integration/rehearsal 에 합침 → selftest·golden → ops/REPORT 기록 | 잠금 1개 | 충돌·번호 겹침 → FV_RESERVATIONS 로 재번호 후 재시험 |
| 병합 | merger(상시) | 리허설 통과 → main, 완료 표 행, 카드 merged | 잠금 1개, 만료 30분 | main 이동마다 재검증 → main 1회 병합, 충돌 시만 재병합 |
| release 기록 | release | 큐 끝 → selftest·골든·publish·기록 5곳, 카드 closed | 큐당 1회 | 첫 실 가동은 fix/n12-forest 병합 뒤(§8) |
| 결정 장부 | 지휘자 | DECISIONS_PENDING 줄 → DECISIONS D번호 | - | 여러 번 묻기 → 한 장부, 사용자 한 번에 답 |
| 지휘자 점검 | 지휘자 | harness /api/state → STATE.md | 20분(이벤트 없을 때 보조) | 세션 360k → 역할 한정 + 교체 |

### 1-a 발주 규칙 (v2)
- 스택 발주: 프롬프트에 기반 커밋 해시 · 선행 카드의 리뷰 blocker 상태를 적는다. FV/Replay 번호는 발주 때 정하지 않는다(§발주·병합 절차 v2.1 (c)). 선행 수정 커밋은 SendMessage 로 체리픽 지시.
- L 카드는 사전 분할(T19 성공). 세부 설계가 없는 카드는 '기본 꺼짐 스위치 + 초안 수치'를 수용 기준에 적고, 결정 선택지에는 키 이름·대상을 명시(T10·T06).

### 1-b 정지 감지 (v2)
- 신호: 태스크 출력 파일의 크기·mtime. 0바이트 또는 장시간 무변화 = 시작 실패 → 새 에이전트로 재발주. 워크트리 무변화만으로 판단하지 않는다.
- 출력이 계속 커지면 대기 중일 수 있음(heavy_gate, 측정). 정지 판정 전 tools/agent/heavy_gate.ps1 풀 점유를 확인(게이트 v2: `ops/gate_wait.log` 의 점유자 PID/이름, `heavy_gate.ps1 -DryRun`). 무거운 잡(렌더·측정)은 에이전트 수명과 분리.
- 재발주 전 원 에이전트가 살아 있는지 확인(T16: 5.5시간 뒤 정상 완료, 66k 중복).

### 1-c 리뷰 체크리스트 — 고정 항목 (reviewer 는 매 리뷰에서 아래 3개를 명시적으로 답한다; 2026-10-22)
1. **FV 상승** (LESSONS_ARCHIVE 21): 저장 프로필(ObjCodec 대상)·Snapshot/Replay 필드를 추가·변경했는가? 기본값이 꺼져 있어도 `FormatVersion` 을 올렸고 FV_RESERVATIONS 에 적었는가? (측정 전용 통계는 제외, LESSONS_ARCHIVE 14)
2. **저장 필드**: 새 상태가 체크섬 · `SnapshotSelfTest.Audit` · 저장/불러오기 · 엔티티 참조 태그에 모두 들어갔는가? (T37 actingCache · T40 AliveCars 누락 전례)
3. **snapshot 분류** (LESSONS_ARCHIVE 23·28): 새 필드를 snapshot 과 `--snaptest` 가 다루도록 분류했는가(저장 대상 / 파생 캐시 / 표현 전용)? 분류 근거 한 줄을 리뷰 보고에 적는다.

### 1-d 발주 프롬프트 공통 꼬리 (2026-10-22 v2.1; 모든 발주문 끝에 붙인다. 한도 숫자는 쓰지 않는다 — 모순 원인, 숫자는 ops/LIMITS.md 만)
필수 3칸 + 한 줄: ① 기반 브랜치·커밋 해시 ② FV: 올리면 `fv_claim` 청구(임시값 기반+1) / 안 올리면 '없음' ③ 핫스팟 파일 목록 ④ `preflight.ps1 <카드id>` 결과 1줄을 보고에 적는다.
- 공통 금지: push 금지 · main·병합 라인(merge/*) 수정 금지 · 작업 브랜치에서 ops/ 수정 금지(`queue_guard`) · 공유 문서는 조각 파일(`docs/decisions_pending.d/`, `docs/engine_notes.d/`).
- 보고: 역할 카드의 줄 수 이내(지휘자가 정하면 그 값). 끝에 **'임의 결정' 항목**(카드가 안 정한 값·가정·해석·삭제·완화를 전부 나열; 없으면 '없음').
- 비아트 heavy(빌드·selftest·측정)는 에이전트 안에서 순차 (heavy_gate 통과). 웹 조회는 `hub ask -w`.
- 역할별 한 줄 (옛 LESSONS 에서 이전): feature·sim = JSON Load/Validate 에 null·NaN·범위 검증 + 플래그 바꾸면 소비처 grep, 낡은 시험과 틀린 코드를 구분(약화 금지) · sim·reviewer = ObjCodec/저장 필드 추가는 기본 꺼짐이어도 FV+1, 측정 전용 통계는 FV 불변 · feature = 새 버튼은 selftest, 새 미션은 `MissionDef.Available()` 순회 묶음(N77·N12·Props·Terrain·missions) · measure/feature = n=8 은 동작 확인만(승률 판정 금지), 힙은 강제 GC 후 · reviewer = checkout 금지 · art = 자세는 `edge_check` 로 칸 경계 확인 · merger = `merge_step.ps1` 만 사용(snapshot + `--snaptest` 강제) · 공통 = 자기 브랜치에서 ops/ 수정 금지.

- 이벤트·캠페인 카드 한 줄 (사용자 승인 2026-10-22, TC-C 증명): 사건 발동을 플레이어 선택(팝업 응답 등)에 걸 때 그 선택을 `GameCommand`(기록 명령)로 넣는다 — 사건 조건은 sim 상태, 상태를 바꾸는 입력은 모두 기록 명령이어야 리플레이에서 같은 틱에 재발동한다. 사건이 직접 실행한 명령(주입)은 명령 로그에 넣지 않는다(이중 실행). evtcmd 시험이 근거.

## 2. 문서 쓰기 소유권
| 파일 | 쓰는 사람 | 그 외 |
|---|---|---|
| ops/STATE.md | 지휘자(점검·인계), release(한 줄) | 읽기만 |
| ops/QUEUE.md | 지휘자(행·assigned), merger(merged), release(closed) | 작업 에이전트 금지 |
| ops/MERGE_QUEUE.md | merger(상태·완료 표·번호), 지휘자(wip-ready 행) | 읽기만 |
| ops/DECISIONS.md | 지휘자(정리), 사용자(답) | 에이전트는 DECISIONS_PENDING 한 줄만 |
| ops/FV_RESERVATIONS.md | 지휘자(번호 예약 후 발주) | 작업 에이전트 금지 |
| ops/LIMITS.md · PIPELINE.md | 지휘자, 사용자 지시 때만 | - |
| tasks/Tnn | spec/지휘자(본문), 담당 에이전트(결과·상태만) | - |
| BASELINE_CURRENT · SELFTEST_HISTORY · PROJECT_NOTES 최신 줄 | measure(블록) · release | 작업 에이전트 금지 |
| watch/LEAKS.md | 지휘자(숫자 있을 때) | - |
| 역할 카드(~/.claude/agents) · L0 헌장 | 사용자 승인 뒤 docs | - |

## 3. 상태 값과 전이 주체
queued(지휘자/spec) → assigned(지휘자, 발주 시) → running(에이전트 시작) → wip-ready(에이전트 끝) → merged(merger) → closed(release). blocked(사유)는 지휘자가 붙이고 뗀다. 되돌림(merger 충돌 → running)은 merger 가 카드에 사유를 적는다.

## 4. 지휘자 세션 재개 5단계
1. `ops/STATE.md`: main 해시 · 에이전트 · 잠금 · 워크트리 · "다음 한 걸음". 2. `ops/QUEUE.md`: running/wip-ready 행 확인, 선행 해소된 queued 선별. 3. `ops/DECISIONS.md`: 보류형 ID 와 새 답. 4. 이 파일 §1~3 으로 역할·소유권 확인. 5. harness `/api/state` 로 실제 에이전트 수 확인(정지 의심 시 §1-b) 뒤 발주; STATE 갱신. (다른 문서는 카드가 가리킬 때만.)

## 도구 (tools/agent, PowerShell 5.1; 2026-10-22 추가)
| 도구 | 언제 | 사용 |
|---|---|---|
| `preflight.ps1 <카드id|브랜치>` | **발주 전** 지휘자 | (card_id 충돌=BLOCK · 제목 키워드 roadmap_check 요약=WARN 포함) QUEUE 상태 · MERGE_QUEUE(이미 병합/대기) · worktree/branch 중복 · 해당 워크트리 미커밋 · FV 예약표를 대조. 중복·병합완료·미커밋이면 종료코드 1 + 사유(BLOCK), FV 겹침은 WARN. 예: `preflight.ps1 T38j` (LESSONS 1·24·30) |
| `merge_step.ps1 -Step E1 -Expected "<a> <b>" -AllowFail uiicons,traindemo [-Groups g1,g2] [-Priority]` | **병합 한 단계** 끝마다 merger/병합 에이전트 | 게이트+빌드 → detcheck(불일치 즉시 STOP) → `snapshot` 묶음 → `--snaptest` → 자기 묶음 → 허용 외 FAIL 이면 STOP → 태그(`post-<Step>`) → `ops/MERGE_PROGRESS.md` 한 줄 append → 끝에 `PASS`/`STOP` 한 줄. 단계 생략 옵션 없음(LESSONS_ARCHIVE 23·28). `-DryRun`(+`-Feed <dir>`)은 계획만 출력·모의 입력으로 STOP 논리 시험 |
| `state_snapshot.ps1 [-Write]` | 인계·세션 시작·점검 | main 해시 · 워크트리 수 · 병합 잠금(만료 표시) · 병합 라인별 HEAD/최신 태그/미커밋 수 · FV/Replay · 실행 중 heavy 프로세스 수를 마크다운으로. `-Write` = 이 체크아웃의 `ops/STATE_LIVE.md` 덮어쓰기 |
| `card_id.ps1 -Prefix T -Count 3 [-Series] -Title "..."` / `-Check <id>` / `-Id T58x -Title ...` | **카드 id 를 붙이기 전** 지휘자·spec 에이전트 | 모든 브랜치·워크트리의 tasks/ + QUEUE · CARD_INDEX + `ops/CARD_REGISTRY.md` 에서 쓰인 id 를 모아 접두어별 다음 빈 번호를 예약(장부에 한 줄 append, 파일 락으로 동시 호출 안전)하고 id 를 출력. 이미 쓰인 id 는 거부(종료 2). `-Series` = 한 번호에 a,b,c(T58a~c), 없으면 T58 T59 T60. `-Check` 종료 0=비어 있음 · 3=사용 중(주인 1) · 1=충돌(서로 다른 카드 파일이 같은 id). 번호를 직접 짓지 않는다 (T51a~e·T49a~c 충돌 사고) |
| `roadmap_check.ps1 -Keywords "날씨","weather" [-Card <id>]` | **로드맵·카드·설계서에 항목을 넣기 전** | ROADMAP_SHORT/LONG · 레거시 문서 · ops 문서 · 모든 브랜치의 docs/specs·tasks · 병합 라인 코드(src, assets/data)에서 키워드를 찾아 출처별로 <=40줄 표시. 히트가 있으면 종료 1 + 결론 '구현 존재 가능 / 문서 계획만 / 히트 없음'. 이미 있는 기능(날씨 WeatherFx, T38e·T38h·T49b/c)을 또 계획하는 일을 막는다 |
| `fv_claim.ps1 -Fv 1 [-Replay 1] [-Schema 0] -Reason "..."` / `-Show` | **저장 형식 번호를 올리는 브랜치**(작업 에이전트) | 번호를 직접 확정하지 않는다. `ops/fv_claims/<브랜치, / → __>.txt`(브랜치당 1파일, append-only → 충돌 0)에 `날짜 \| FV+1 \| Replay+1 \| Schema+0 \| base <해시> \| 사유` 한 줄을 남기고 커밋에 포함. 코드의 임시 번호 = **기반 FV(Replay)+청구 합계**. `ops/FV_RESERVATIONS.md` 에 번호를 적지 않는다 |
| `fv_assign.ps1 -Report` / `-Order a,b,c [-BaseRef HEAD]` / `-Branch X -Apply [-BaseRef HEAD] [-NoBuild] [-RegenGolden]` | **병합 라인**(merger), 각 브랜치 병합 직전·직후 | `-Report` = 모든 브랜치의 청구 파일 스캔표(수동 예약표 대체). `-Order` = 병합 순서대로 최종 FV/Replay 계획(기반 = BaseRef 의 상수; 청구 없는 브랜치는 불변). `-Apply` = `git merge --no-commit X` 도중(HEAD = 병합 전 tip)에 호출: Snapshot.cs/Replay.cs 의 번호 줄 충돌은 `git checkout --ours -- <두 파일>; git add` 로 접은 뒤 실행하면 기반 + 그 브랜치 청구로 `fv_renumber -Set` 한다. 병합 커밋을 이미 만들었으면 `-BaseRef HEAD^1`. 청구 파일이 없으면 기반 값 그대로. 골든이 바뀌는 병합은 `-RegenGolden`(마지막 1회 규칙 유지). Schema 청구는 표시만(캠페인 스키마는 수동) |
| `assemble_notes.ps1 [-Target decisions\|notes\|all] [-Write]` | **병합 라인·release** (작업 에이전트는 실행 안 함) | `docs/decisions_pending.d/*.md` · `docs/engine_notes.d/*.md` 조각을 날짜순(조각 첫 줄 `date: 2026-10-22 18:30`, 없으면 파일 수정시각)으로 합쳐 출력. `-Write` = DECISIONS_PENDING.md / ENGINE_NOTES.md **끝의 생성 블록**(`assembled-notes` 주석 사이)만 교체; 위쪽 원문은 불변, 재실행해도 중복 없음 |
| `queue_guard.ps1 [-Branch ref] [-Base main]` | 작업 에이전트 커밋 전 · preflight · merge_step | 브랜치(미지정=HEAD+미커밋)가 `ops/QUEUE.md`·`STATE.md`·`MERGE_QUEUE.md` 를 바꿨는지 검사: 있으면 파일명 출력 + 종료 1. 연동: `preflight.ps1`(같은 이름 브랜치가 있으면 WARN), `merge_step.ps1 -GuardBranch <병합할 브랜치> [-GuardStrict]`(0단계; 기본은 경고, Strict 면 STOP). git pre-commit 훅은 설치하지 않는다 |
### 병렬 처리 규칙 (감독관 PIPELINE_AUDIT_2026-10-22 C-1·C-2·C-7, 사용자 최우선 지시)
- **FV 늦은 결합**: 번호는 발주 때 정하지 않는다. 올리는 브랜치는 `fv_claim.ps1` 로 청구하고 임시 번호(기반+1)만 코드에 쓴다. 병합 라인이 순서대로 `fv_assign.ps1` 로 확정한다. `ops/FV_RESERVATIONS.md` 는 수동 갱신을 멈추고(파일은 삭제하지 않음, 낡은 값은 정정 안 함) `fv_assign.ps1 -Report` 출력이 현재 예약표다.
- **공유 문서 조각 규칙**: 작업 에이전트는 `docs/DECISIONS_PENDING.md` · `docs/ENGINE_NOTES.md` 에 줄을 추가하지 않고 `docs/decisions_pending.d/<브랜치, / → __>.md` · `docs/engine_notes.d/<브랜치>.md`(브랜치당 1파일, 충돌 0)에 쓴다. 병합 라인/release 가 `assemble_notes.ps1 -Write` 로 합친다. 기존 줄 편집은 지휘자만.
- **작업 브랜치는 ops/QUEUE·STATE·MERGE_QUEUE 를 수정하지 않는다**(위 §2 표) — `queue_guard.ps1` 로 확인.
- **웨이브 2 base 는 `merge/0.2-final` 하나**. 이미 merge/0.2 또는 tce 체인 위에 선 브랜치는 final 을 **한 번 merge-in 하고 끝(재스택 금지)**; 이후 깊이는 final 기준 ≤3(stack_depth.ps1 로 발주 전 확인). 이 규칙의 병합 라인 실행은 별도 지시로 하며, 도구·문서 카드(tools/pipe-parallel)는 어떤 브랜치도 병합하지 않는다.
- 시험: `korean-division/tools/agent/pipe_parallel_test.ps1` (임시 저장소에서 TC-B/TC-C/M1/g3 임시 67 상황을 재현해 68~71 / Replay 24,24,25,26 정렬 포함).


| `watchdog.ps1 [<워크트리>...] [-StallMin 30] [-RecentHours 6]` | 에이전트가 멈춰 보일 때 · 주기 점검 지휘자 | 워크트리별 `OK` / `WAITING_GATE` / `SUSPECT_STALL`(30분+ 무변화) + 근거(최근 커밋·변경 파일·러너 dotnet/KoreanDivision/make_unit/blender CPU 변화·경로에 워크트리명이 든 스크래치패드 파일·heavy_gate 대기 로그 `ops/gate_wait.log`). 인자 없으면 최근 6시간 활동 워크트리. **프로세스를 종료하지 않는다(보고만)**; 의심이면 종료코드 2 (LESSONS 8·18·25). `-Fake "commit=45;file=50;scratch=none;runner=0;cpu=0;gate=wait"` 로 판정만 모의 |
| `stack_depth.ps1 [<브랜치>...] [-Line merge/0.2] [-Max 3]` | **발주 전**(스택 카드) · 병합 대기열 점검 | 각 브랜치 아래에 쌓인 '병합 라인에 아직 안 들어간' 선행 브랜치 사슬 깊이 계산, 3단 초과면 `WARN`(종료코드 3) + 라인에 안 들어간 선행 목록(낮은 쪽 먼저). 인자 없으면 QUEUE.md 의 wip-ready 브랜치 |
검증: `watchdog_test.ps1` (판정 3종 모의 + 임시 저장소 스택 시험, 전부 PASS 해야 함).
병합 단계는 이 도구 하나로 끝내고, 직접 명령을 풀어 쓰지 않는다. 세부 절차 정본 = docs/procedures/merge-procedure.md.

### 로드맵/카드 추가 전 절차 (필수, 사용자 2026-10-22)
(i) **중복 점검**: `roadmap_check.ps1 -Keywords <한글>,<영문 식별자 후보>` 를 실행한다. 히트(코드 포함)를 실제로 읽고, 카드 머리말에 `중복 점검: <키워드> → <결론 한 줄: 구현 있음/문서 계획과 합침/히트 없음> (roadmap_check 날짜)` 한 줄을 기록한다. 구현이 있으면 카드를 만들지 않고 기존 구현 위 확장으로 범위를 줄인다.
(ii) **id 예약**: 카드·로드맵 id 는 `card_id.ps1` 로만 받는다 (`-Count`/`-Series`). 출력된 id 를 그대로 쓰고, 설계서 여러 개를 병렬로 쓸 때도 각자 호출한다 (락으로 안 겹친다). 장부 = ops/CARD_REGISTRY.md (append-only, 직접 편집 금지).
(iii) **프롬프트 템플릿**: 설계서·spec 에이전트 발주문에 아래 두 줄을 그대로 넣는다.
> 1. 카드/로드맵 항목을 쓰기 전에 `tools/agent/roadmap_check.ps1 -Keywords ...` 를 돌려 결과를 읽고, 각 카드 머리말에 '중복 점검' 줄을 적는다 (이미 구현된 기능이면 카드 대신 보고).
> 2. 카드 id 는 `tools/agent/card_id.ps1 -Prefix T -Count N -Title "..."` (한 번호에 a~c 는 `-Series`) 로만 예약한다. 임의 번호 금지.
발주 전 `preflight.ps1` 이 id 충돌(BLOCK)과 키워드 요약(WARN)을 한 번 더 확인한다. 시험: `tools/agent/pipe_ids_test.ps1`.

## 발주·병합 절차 v2.1 (감독관 PIPELINE_AUDIT_2026-10-22 C·D절 반영, 사용자 승인 2026-10-22)
(a) **발주 전 필수 3종**: `preflight.ps1 <카드id>` + `card_id.ps1`(카드 id 예약) + `roadmap_check.ps1`(로드맵·레거시·코드 중복 점검). 결과 1줄을 발주문에 첨부. 도구가 main 에 반영되기 전에는 병합 라운드 후 사본 경로를 쓴다.
(b) **base 규칙**: 웨이브 2 이후 모든 브랜치 base = `merge/0.2-final`(또는 그 시점의 통합 브랜치) **하나**. 이미 merge/0.2 · tce 체인에 선 브랜치는 1회 merge-in 후 재스택 금지. 스택 깊이 ≤3(`stack_depth.ps1` 로 발주 전 확인).
(c) **FV/Replay 늦은 결합**: 작업 브랜치는 임시값(기반+1) + `fv_claim`. 병합 라인(merger)이 병합 순서대로 `fv_assign` 으로 확정. `FV_RESERVATIONS.md` 수동 갱신 중단.
(d) **공유 문서 조각**: `docs/decisions_pending.d/`, `docs/engine_notes.d/` 조각 파일(`assemble_notes.ps1` 가 합침). 작업 에이전트의 QUEUE·STATE·MERGE_QUEUE 수정 금지(`queue_guard`).
(e) **병합 라운드 시작 때** 각 브랜치 팁 해시를 MERGE_QUEUE 행과 재확인한다(병합 뒤 앞서간 브랜치 방지: m1·m2·u2 가 병합 뒤 수정된 전례). 어긋나면 그 브랜치를 보류하고 지휘자에 보고.
(f) **에이전트 완료 보고 처리**: 지휘자는 보고의 '임의/가정/해석/삭제/완화' 항목을 즉시 `ops/AUTO_DEFAULTS_LOG.md` 에 한 줄 append (사용자 상시 규칙).
(g) **이름 규칙**: 보고·대화는 '한글 이름(ID)'. 카드마다 한글 이름 한 줄(승인됨; 사전 = `ops/CARD_NAMES.md`, 별도 에이전트가 작성 중 — 아직 없음).
(h) **지휘자 보고 길이**: 완료 알림당 핵심 3~4줄(토큰 누수 B-2). PIPELINE_LOG 항목은 줄당 ≤200자(시각·카드·해시·숫자 1개), 세부는 카드 파일.
(i) **웨이브 2 착수 전 체크리스트** (실행 순서; 완료 칸에 `[x]`):
- [ ] 1. 도구 묶음(pipe-tools · gate-v2 · c15-watchdog · selftest-parallel · fv-renumber · pipeline-v2)을 병합 라운드에서 main 에 반영(문서·도구 전용, `git diff main --stat -- korean-division/src` 가 비어야 함). 4개 도구 시험 스크립트 통과.
- [ ] 2. 한도 출처 정리: STATE.md · harness 상한 · 발주 꼬리의 숫자를 'LIMITS 참조'로 교체(LIMITS 값 자체는 사용자 몫).
- [ ] 3. 웨이브 2 base = `merge/0.2-final` 고정. 기존 9개(tcb·tcc·tcg·g4·m1·m3·g3·tcf·m2)는 final 을 1회 merge-in.
- [ ] 4. FV 정렬: `fv_assign -Report` 로 증가 4건(TC-B·TC-C·M1·g3)을 병합 순서대로 확정(68~71 / Replay 25~26), TC-G 는 증가 없음.
- [ ] 5. QUEUE 정합: 병합된 행(T04·T10·C1E1-m1/m2…) 갱신, closed/merged 행을 보관 파일로 이동.
- [ ] 6. LESSONS 12줄 확정(완료) · 역할 카드 수정안 `ops/ROLE_CARD_PATCHES_2026-10-22.md` 를 지휘자가 적용.
- [ ] 7. 발주 템플릿 = §1-d (필수 3칸 + preflight 1줄). 8. 새 브랜치부터 조각 디렉터리·fv_claims 적용(소급 변경 없음).
- [ ] 9. 지휘자 새 세션으로 시작, 유휴 점검은 `state_snapshot.ps1` 출력만 읽기. 10. 동시 수 실효 20 유지 → `ops/gate_wait.log` 로 비아트 대기 중앙값 <10분 · 빈 RAM ≥6GB 확인 뒤에야 LIMITS 값으로(사용자 결정).

## 서브 지휘자 (웨이브 2 · 사용자 지시 2026-10-22)
- 분야 3개: **아트**(운영 중, 상태 파일 `ops/ART_STATE.md`) · **캠페인**(웨이브 2 필요 시 임명; 상태 파일 `ops/CAMPAIGN_STATE.md`, 로드맵 명시) · **병합**(merger 상시, 상태 파일 `MERGE_PROGRESS*.md`).
- 규칙:
  1. 자기 풀 한도 안에서만 발주: 아트 렌더 레인 ≤8 + 빌드 slot 비움. 한도 숫자는 `ops/LIMITS.md` 참조.
  2. `ops/QUEUE.md` · `STATE.md` · `MERGE_QUEUE.md` 는 최상위 지휘자만 수정. 서브 지휘자는 자기 상태 파일만 수정.
  3. 파도마다 새 에이전트로 임명(오래 살려 두지 않음). 인계는 상태 파일 한 개.
  4. 최종 보고 ≤15줄, 발주한 에이전트 표 포함.
  5. 발주 전 `preflight` · `card_id` · `roadmap_check` 동일 적용(§발주·병합 절차 v2.1 (a)).
  6. 임의 결정은 보고 마지막 줄 `임의 결정:` 에 나열 → 최상위 지휘자가 `ops/AUTO_DEFAULTS_LOG.md` 에 기록.
  7. H3 sim 카드는 서브 지휘자 없이 최상위가 직렬 관리.
- harness: 에이전트 목록에 `서브 지휘자(영역)` 라벨로 표시된다 (구현은 별도 에이전트; 이 절은 문서만).
- 미결정(적용하지 않음): 감독관 상시화 · 동시 상한 6개 · 관리 장치 축소 — '미결정' 절 참조.

## 미결정 (사용자 몫 — 적용하지 않음)
- 동시 에이전트 상한(감독관 C-6: 실효 20 유지 vs 30; 메모리 규칙 6/opus 2 와의 차이 확인).
- 관리 장치 축소(보조 지휘자 3분야 · 감독관 상시화 여부 · 60분 주기).
- 지휘자 세션 교체 규칙(하루 1회 vs 컨텍스트 150k 도달 시 STATE 인계 후 교체, 감독관 B-1).
- 감독관 상시화(상시 가동·비용 ≤60k/주기 유지 여부).
- (참고) C:/works/notes/agent-sprawl-concern.md 의 제안은 적용하지 않음.

## 5. 버전 기록
| 버전 | 날짜 | 바뀐 것 | 근거 숫자 | 점수 |
|---|---|---|---|---|
| v1 | 2026-10-22 | 4층 문서 + ops + LEAKS, 역할 10, merger 상시·잠금 금지·만료 30분, release 분리, 결정 장부 기본값 | 에이전트 45 · 병합 10/13(04:08~04:40) · 잠금 대기 2h+ · 지휘자 360k · 중복 조사 25만 토큰 | Fable 독립 ≈45 / 자체 54 → 개선 후 예상 60~65(Fable) vs 75(자체) |
| v2 | 2026-10-22 | 리뷰 단계 필수화 · 수정 사이클 · 스택 규칙(기반 해시·FV 예약표·체리픽) · 정지 감지(출력 파일) · L 사전 분할 · 기본 꺼짐 스위치 · 통합 리허설 · release 첫 실 가동 조건 | 발주 ~70 · wip-ready ~38 · 리뷰 18 (전부 결함 포착) · 수정 사이클 11 · 수용 기준 미달 보고 4 · 오류 3(T18 중복 · T26 역할 오배정 · T16 중복 발주) | 자체 72/100 |
| v2.1 | 2026-10-22 | 감독관 반영: 발주 필수 3종 · base 단일화 · FV 늦은 결합 · 조각 문서 · 팁 재확인 · 임의 결정 보고 · 이름 규칙 · 웨이브 2 체크리스트 (§발주·병합 절차 v2.1) | 감독관 보고 C-1~C-7·D | - |

## 6. 점검 질문과 주기 (사용자 중간 점검; 숫자 출처)
| 질문 | 어디서 뽑나 |
|---|---|
| 병합 1건 소요 분 | MERGE_QUEUE 완료 표 시각 / merger 보고 |
| 게이트 대기 합계 분 | heavy_gate 로그 · 에이전트 보고 → LEAKS 주간 표 |
| 에이전트 시작 시 토큰 · 컨텍스트 분포 | harness /api/state (LEAKS 주간 표에 복사) |
| 중복 조사 건수 | scout 보고 "이미 구현됨" 수 → LEAKS L5 |
| 결정 대기 건수 | DECISIONS '보류' 행 수 (harness 도 센다) |
주기: 파도 끝마다 1회, 주 1회 LEAKS 주간 표 한 줄.

## 7. 승격 규칙
규칙·도구·역할은 LEAKS 에 숫자(횟수·토큰·분)가 있을 때만 추가하고, 그때 이 파일의 버전을 올린다. 숫자 없는 제안은 DECISIONS 후보로만.

## 8. 보류·미해결
- 등급 5축 중 R·S·W 는 선택 메모(D·H 만 필수). 다시 필수로 올리지 않음.
- 게이트 3레인: 코드 있음, 기본 비활성. 게이트 대기 합계가 주 60분을 넘으면 시험.
- harness: 읽기 전용 현황판 그대로(3단계 확장 보류).
- 파도 크기 시험(≤12 · 上 ≤3 · heavy ≤6)은 Fable 권고 — 한도 값은 ops/LIMITS.md 기준(사용자 지시 우선), 다음 파도에서 12 로 한 번 시험해 숫자 기록.
- 지휘자 세션 교체 주기(파도마다)는 시험 뒤 확정.
- release 첫 실 가동: 2026-10-22 완료 (selftest 2008 OK).
- 통합 리허설 담당·절차: 첫 병합 전 사용자 확인.

## 정책 추가 (2026-10-22, 사용자 지시)
[폐기 — 2026-10-22 AT·AX 로 해제됨] 옛 '병합 금지' 정책. 현행: 작업 에이전트는 `wip-ready` 까지만, 병합은 merger 가 병합 라인(merge/0.2-final)에서만(상시 발주), main 은 마지막 `--ff-only` 를 사용자 보고·확인 뒤. push 는 사용자 명령 때만. 새 문서·카드·프롬프트에 '병합 금지' 를 쓰지 않는다(브랜치 제외는 MERGE_PLAN §2 '대체됨' 으로).
(v2) 병합은 사용자 허가 뒤, 통합 리허설(integration/rehearsal) 통과 후에만 main 으로.

## 자동 기본값 로그 (사용자 2026-10-22)
추천 기본값을 자동 적용할 때는 `ops/AUTO_DEFAULTS_LOG.md` 에 **한 줄 append**(`echo >>`)만 한다. 이 파일은 읽기 목록(STATE→QUEUE→DECISIONS→LESSONS)에 넣지 않고 에이전트 프롬프트에서도 읽지 않게 한다(토큰 누수 방지). 사용자가 요청할 때만 지휘자가 읽어 요약한다.

- 2026-10-23 (게이트): 렌더(art) 풀 기본 상한 2(RAM<6GB 이면 1), 병합 담당은 merge_step -Priority 유지. 변경 위치 korean-division/tools/agent/heavy_gate.ps1(KD_GATE_ART_LIMIT 환경 변수로 재정의), 시험 heavy_gate_test.ps1 갱신·통과, 백업 C:/works/_backup/heavy_gate*.before_artlimit_2026-10-23. 발주 규칙: general-purpose 지양, opus 는 카드 분할+턴 상한(LESSONS.md 참조).

## 감독관·자동 점검 규칙 v2.2 (사용자 승인 2026-10-23, 토큰 집계 근거: 점검 비용은 점검 대상 비용의 5% 이하)
- 감독관 발주 모델: opus → **sonnet**. 정기(60분) 실행은 폐지하고 **하니스 이상 신호가 뜰 때 또는 사용자가 요청할 때만** 실행한다. (위 '감독관 상시화' 미결정 항목은 이 규칙으로 대체: 상시화하지 않음.)
- 감독관 프롬프트에는 읽을 문서·파일을 직접 지정하고(대문서 전체 읽기 금지), 보고는 **12줄 이내**로 받는다.
- 지휘자의 '자동 점검 1회': 주기를 1시간 이상으로 늘리고 읽는 것은 `ops/STATE_LIVE.md`(tools/agent/state_snapshot.ps1 -Write 출력)와 하니스 `/api/state` 요약뿐. PIPELINE_LOG·큰 문서는 읽지 않고, 이상이 없으면 한 줄만 남긴다. 정기 점검은 코드(하니스·스크립트)로 하고 LLM 은 이상 신호일 때만 부른다.
