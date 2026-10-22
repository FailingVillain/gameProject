# PIPELINE_AUDIT 2026-10-22 (웨이브 2 착수 전, 감독관 opus, 읽기 전용)
> 근거: ops/*.md, watch/LEAKS.md, harness /api/state(19:3x 조회), git(브랜치 189개·main 밖 커밋 456개), kd-final/ops/MERGE_PROGRESS*.md. 추정은 '추정'으로 표시. L#n = LESSONS 번호.

## 0. 요약 (숫자)
| 지표 | 값 | 출처 |
|---|---|---|
| 오늘 Claude 토큰(입력+캐시+출력) | 1.637B (캐시 적중 96.2%), 턴 11,921 | harness claude_today |
| 지휘자 세션 2개 | 이전 324M(712턴·ctx 805k) + 현재 32M(138턴·ctx 415k) = **356M** | harness claude_agents |
| 하위 에이전트 73개 합 | 156M (지휘자가 2.3배 많음) | 같은 곳 |
| 로그에 남은 에이전트 보고 | 149건 · 16.7M · 190k 이상 12건(8%), 최대 406k(T33c) | PIPELINE_LOG.md |
| 중복·오배정 발주 낭비 | 336k (T18 56k·T26 44k·T16 66k·T16b 114k·T38j 56k) | PIPELINE_LOG.md:16,19,94,125,202 |
| 게이트 25분 이상 대기 | 8회 이상 (T16 5.5h, T38g·T38f·T38e 각 60분, T41 30분+, T44·T38e 25분+, 렌더 C/D 60분) | PIPELINE_LOG.md:94,132,155,166,172,208,218,254 |
| 웨이브 2 브랜치 FV 충돌 | FV 67 을 5곳이 씀(final·TC-B·TC-C·M1·M3), g3 는 54/R20 | git show <br>:Snapshot.cs |
| 파이프라인 도구 main 반영 | **0/5** (pipe-tools·gate-v2·watchdog·selftest-parallel·fv-renumber 모두 main 밖) | git merge-base |

## A. 교훈 선별 (31 → 핵심 11)
### A-1 지휘자 핵심 규칙으로 남길 것 (11)
| 새 번호 | 원 번호 | 요약 | 유지 이유 |
|---|---|---|---|
| K1 | 1·24·30 | 발주 전 preflight(QUEUE·완료표·워크트리 미커밋·브랜치 중복) | 중복 발주 4건 336k. 도구가 main 에 오면 'preflight 실행' 한 줄로 |
| K2 | 2 | 작업 종류에 맞는 역할(코드≠docs) | 지휘자 판단 몫, 도구로 못 막음 (T26 44k) |
| K3 | 3 | 스택 발주 = 기반 해시·선행 리뷰 상태 명시 | 재스택 3회. C-2 의 base 규칙과 함께 |
| K4 | 4 | 번호 올리는 브랜치는 FV 할당 먼저 | 웨이브 2 에서 실제로 깨짐(C-1). 할당기로 대체 예정 |
| K5 | 5 | L 카드 사전 분할 | 190k 이상 12건 |
| K6 | 6 | 세부 설계 없는 카드 = 기본 꺼짐 + 초안 수치 | 결정 대기 없이 진행하는 핵심 장치 |
| K7 | 7 | 목표 미달은 정직 보고, wip-ready 금지 | T06·T16·T31c |
| K8 | 10 | wip-ready 보고마다 QUEUE·FV 동기화 | 지금도 낡은 행 다수(C-5) — 생성식으로 바뀌면 삭제 |
| K9 | 11 | 리뷰 필수 | 리뷰 29건 전부 결함 포착, 차단 5건 이상 |
| K10 | 8·18 | 재발주 전 정지 확인(러너 PID·scratchpad·게이트) | 이중 발주 2회. watchdog 배포 뒤 'watchdog SUSPECT 만 재발주' 한 줄로 |
| K11 | 29 | 지휘자 컨텍스트 = 최대 누수, 파도마다 교체·로그 짧게 | 356M (B-1) |

### A-2 역할 카드(~/.claude/agents)에 한 줄로 흡수 (지휘자 읽기 목록에서 제외)
| 원 번호 | 흡수 위치 | 한 줄 |
|---|---|---|
| 9 | 공통(CLAUDE.md 헌장) | 자기 브랜치의 ops/ 파일 수정 금지 — 현재 main 밖 커밋 97/456 이 ops/QUEUE.md 를 건드림 |
| 13 | feature·sim | JSON Load/Validate 에 null·NaN·범위 검증, 플래그 바꾸면 소비처 grep |
| 14·21 | sim·reviewer | ObjCodec/저장 대상 필드 추가 = 기본 꺼짐이어도 FV+1, 측정 전용 통계는 FV 불변 |
| 15·31 | feature | 새 버튼 = selftest, 새 미션 = MissionDef.Available 순회 묶음(N77·N12·Props·Terrain·missions) |
| 16 | feature·sim | 낡은 시험과 틀린 코드 구분, 약화 금지 |
| 17·19·22 | measure·feature | n=8 은 동작 확인만, 승률 판정 금지, 힙은 GC 후 |
| 20 | reviewer | checkout 금지 (이미 '읽기 전용'이지만 명시) |
| 26 | 공통 꼬리 | 웹 조회는 hub ask -w |
| 27 | art | 자세는 edge_check 로 칸 경계 확인 |
| 23·28 | merger | merge_step.ps1 만 사용(snapshot+snaptest 강제) |

### A-3 삭제·보관 (도구로 강제되거나 중복·만료)
| 원 번호 | 처리 | 근거 |
|---|---|---|
| 23 | 삭제(28 과 동일) | LESSONS.md:30 = :35 |
| 30 | 삭제(24 와 동일) | LESSONS.md:31 = :37 |
| 8 | 18 에 병합 | 18 이 8 의 정정판 |
| 12 | 보관 | WIP 패치 복원 단계 종료(T01~T07 wip-ready/병합) |
| 25 | 게이트 v2 main 반영 뒤 삭제 | 지금은 **미반영**이라 유지 필요(C-4) |
| 1·4·18·23·24·28 | 도구 main 반영 뒤 K 줄 1개로 축약 | 도구 5종이 아직 main 밖 → '이미 강제됨'은 지금 사실이 아님 |
주의: ~/.claude/agent-lessons/common.md:6 '전체 selftest 10분+'는 selftest_parallel(268초) 병합 뒤 갱신 대상.

## B. 토큰 누수
| # | 패턴 | 증거 | 추정 토큰 | 절감안 | 효과(추정) |
|---|---|---|---|---|---|
| B-1 | 지휘자 컨텍스트 비대 | 이전 세션 평균 455k/턴(324M/712), 현재 232k/턴(32M/138). 지휘자 356M = 오늘 전체의 21.7% | 356M | 150k 에서 STATE 인계 후 교체(하루 1회 규칙과 충돌 → 사용자 결정), 완료 알림은 1줄 요약만 로그 | 이전 세션 기준 ~200M(추정) |
| B-2 | 긴 보고 재서술 | PIPELINE_LOG 314줄·78k자, 항목당 300~900자. 중복 항목: :202(T38j 두 번), :238=:245 계열 재기재. 18:00 이후 기록 0줄(최종 합치기 H5 19:22 까지 2시간 공백) | 로그 1회 전체 읽기 ≈34k | 줄당 ≤200자(시각·카드·해시·숫자 1개), 세부는 카드 파일에 | 로그 크기 1/3 |
| B-3 | 시작 읽기 묶음 | STATE 11.3KB+QUEUE 22.9KB+DECISIONS 41.3KB+PIPELINE 9.1KB+LESSONS 5.1KB = 89.7KB | 세션당 35~45k(추정) | QUEUE 의 closed/merged 행·DECISIONS 답 완료 절을 보관 파일로, LESSONS 11줄 | 세션당 ~20k |
| B-4 | 폴링·짧은 턴 | 크론 점검(:17·:47)+20분 루프, harness '상급 모델 짧은 응답 9/30턴' 경고 | 확인 필요 | 이벤트 구동만(완료 알림), 유휴 점검은 state_snapshot 1회 출력만 | - |
| B-5 | 프롬프트 공통 꼬리 | C1E1_WAVE1_PROMPTS 꼬리 ≈600자가 역할 카드·CLAUDE.md 와 중복. **꼬리의 한도('동시 6·heavy 2')가 LIMITS(30·heavy 12)와 모순** | 149건×~300 ≈45k(작음) | 꼬리 삭제 → '규칙=역할 카드' + 3칸(기반 해시·FV 할당·핫스팟 목록) | 토큰보다 모순 제거가 효과 |
| B-6 | 큰 컨텍스트 에이전트 | 190k 이상 12/149: T33c 406k, T32b 267k, prone 257k, C1E1 계획 spec 226k, T34a 209k, 리허설 202k | 12건 초과분 ≈0.9M(추정) | 200k 넘는 카드는 사전 분할, 재개 대신 새 에이전트(STATE §7 유지) | - |
| B-7 | 중복 발주 | 336k (0 행 참조). 남은 위험: QUEUE 낡은 행(C-5) | 336k | preflight main 배포 + 발주 프롬프트에 preflight 결과 첨부 | 재발 0 목표 |
| B-8 | 리뷰어 과잉 읽기 | 리뷰 29건 2.50M, 평균 86k, 최대 181k(연대장·T49 체인), 122k 3건 | 2.5M | 600줄 넘는 diff 는 Gemini pro 1차(이미 역할 카드에 있음) — 체인 리뷰는 브랜치 단위로 나눔 | 리뷰당 ~30k(추정) |
| B-9 | harness 오표시 | gauges 상한 24/5/8 (LIMITS 30/8/12), kanban 이 낡은 QUEUE 로 T10·T18·T19·T25 를 '대기' 표시 | - | harness 상한을 LIMITS 에서 읽게 | 판단 오류 방지 |

## C. 병렬 처리 (최우선)
### C-1 병합 직렬과 FV 임시 번호 충돌
증거 (git show <branch>:Snapshot.cs / Replay.cs):
| 브랜치 | 기반 | FV/Replay | 자기 증가 | 예약표 값 |
|---|---|---|---|---|
| merge/0.2-final ce99a09 | 두 라인 합침 | 67/24 | t48b1=67 | 67 |
| kd/tcb-alert (TC-B) | merge/0.2 (66) | 67/24 | +1 | 없음 |
| kd/tcc-inject (TC-C) | merge/0.2 | 67/24 | +1 | 69 |
| kd/m1-morale-suppress (M1) | merge/0.2 | 67/25 | +1/+1 | 없음 |
| kd/m3-morale-link | m1+tce+merge/0.2 | 67/25 | 상속 | 없음 |
| kd/c1e1-g3-events | tce 체인(53/19) | 54/20 | +1/+1 | 없음 |
| kd/tcg-annex (TC-G) | merge/0.2 | 66/24 | 없음 | **68 (낡음)** |
| tcd·tce·tcf·g1·g6·m2·d2 | t48b2 체인 | 53/19 (상속) | 없음 | - |
→ 실제 증가 4건(TC-B·TC-C·M1·g3), Replay 증가 2건(M1·g3). 최종 번호는 병합 순서대로 68~71 / R25~26 이어야 함. ops/FV_RESERVATIONS.md:38 의 'TC-G=68, TC-C=69' 는 사실과 다름.
권고: **번호를 발주 때 정하지 않는다(늦은 결합)**. 규칙 = 브랜치는 '기반 FV+1' 만 쓰고 `ops/fv_claims/<branch>.txt`(append-only, 브랜치당 1파일 → 충돌 0)에 '+1 FV / +1 Replay / 사유'를 남김 → 병합 라인이 단계마다 fv_renumber -Set max(HEAD)+1. FV_RESERVATIONS.md 는 스캔 스크립트(위 표를 만든 git show 루프, ~30줄)의 출력으로 대체. 병합 로그상 이미 'Snapshot.cs FV line: HEAD + entry' 로 12회 기계 처리 중이라 규칙화만 하면 됨.
비용: 소(스크립트 30줄 + 카드 문구) · 효과: 예약표 수동 갱신 0, 번호 충돌 판정 자동.

### C-2 스택 깊이·base 선택과 재병합 비용
증거: 브랜치 아래 깔린 브랜치 수 — tcf 13 · g3 13 · tcb 36(merge/0.2 전체) · tca 43(merge/0.2-ui) · m3 51(두 라인 + tce 체인 = 사실상 개인 최종 합치기). 로드맵 규칙 '스택 ≤3'(PIPELINE_ROADMAP.md:20) 대비 4~17배. 단계 비용: 라인 1 2단계 7623초/17단계 ≈7.5분, 최종 13단계 18:29~19:22 ≈4분/단계(merge_step 사용).
권고: 웨이브 2 base 는 **merge/0.2-final 하나**(FV 67/R24·스키마 v12·두 라인 포함). 이미 merge/0.2 또는 tce 체인에 선 9개(tcb·tcc·tcg·g4·m1·m3·g3·tcf·m2)는 final 을 한 번 merge-in 하고 끝(재스택 금지). 이후 깊이는 final 기준 ≤3, stack_depth.ps1(c15-watchdog 브랜치)로 발주 전 확인.
비용: 소~중(브랜치당 1회 merge, 기계 충돌 위주 ≈4분×9 ≈40분 추정) · 효과: 최종 합치기가 다시 3갈래로 갈라지는 것 방지, m3 같은 개인 합치기 0.

### C-3 H3 핫스팟 직렬 규칙의 실제 충돌 데이터
| 파일 | main 밖 커밋(456 중) | 병합 로그 언급(42단계) | 성격 |
|---|---|---|---|
| ops/QUEUE.md | 97 | 31 ('ops ours') | 규칙 위반(PIPELINE.md:37 작업 에이전트 금지) — 순수 낭비 |
| src/SelfTest.cs | 70 | 11 (등록 줄 union) | 등록부 구조 문제 |
| docs/DECISIONS_PENDING.md | 67 | 8 (union) | 9줄→64줄 |
| docs/ENGINE_NOTES.md | 44 | 8 (union, 255KB) | 한 파일 비대 |
| Snapshot.cs / Replay.cs | 35 / 15 | 12 (FV 줄) | 번호 줄만 → C-1 로 해소 |
| balance.json | 33 | - | 섹션 분리 규칙 유지 |
| 골든(detcheck.golden) | - | 20 | 마지막 재생성 1회 규칙 유지 |
| docs/CONTROLS.md | 17 | 3 | Bindings 에서 생성 가능 |
결론: src 실충돌은 FV 줄·등록 줄뿐이고 모두 기계 처리. **비기계 문제는 snapshot 미분류 fixup 2건(B9 actingCache, D6 AliveCars)** — 작성자가 snapshot 묶음을 안 돌린 탓(L#23). H3 직렬 규칙은 sim 로직(Combat*·TacticalAi)에만 남기고, 문서·등록부 충돌은 C-7 구조로 없앤다.

### C-4 게이트·풀 (아트 8 + 비아트 3), RAM·정체
증거: LIMITS.md:7-9 는 '아트 8 + 비아트 3(풀 분리)'과 '무거운 작업 10/12(단일)'를 동시에 적음. 실제 main heavy_gate.ps1 = 단일 풀 10/12 + RAM 14GB. 풀 분리·빌드 슬롯·대기자 미계산 = tools/gate-v2 f482e7b(**main 밖**). ART_STATE.md:10: 대기 전용 -Render 프로세스 4개가 풀을 쥠, 'heavy=8~10 timeout' 반복. 현재 빈 RAM 5.6/31.1GB(<6GB → 상한 10, 렌더 8레인 불가). 정체 8회 이상(0 표).
권고: 웨이브 2 전에 gate-v2 를 main(에이전트들이 절대경로로 부르는 사본)에 반영, gate_wait.log 로 대기 분 기록 → LEAKS 주간 표. LIMITS.md 의 '무거운 작업' 행 삭제(모순 제거).
비용: 소(도구 병합 + 1줄) · 효과: 빌드 굶김 해소(기록된 60분 대기 4회 이상 원인).

### C-5 보조 지휘자 3분야의 경계·정보 흐름
| 문제 | 증거 | 권고 | 비용 | 효과 |
|---|---|---|---|---|
| 상태 파일 중복·분산 | MERGE_PROGRESS 4벌(kd-merge 12.5KB·kd-merge-b 6.2KB·kd-final 19.1KB+UI 6.2KB), STATE.md:57 '낡음' 자인, CAMPAIGN_STATE 없음 | 병합 라인 상태는 merge_step 한 줄 로그(ops/MERGE_PROGRESS.md, 최종 라인 1개)만; state_snapshot.ps1 가 읽어 STATE_LIVE 생성 | 소 | 지휘자 재독 감소 |
| QUEUE 동기화 지연 | QUEUE.md:12 T04 'assigned'(B3 병합), :18 T10 'assigned'(B10 병합), :55-56 C1E1-m1/m2 'queued'(final H3/H4 병합), harness kanban 오표시 | 상태의 정본을 카드 파일(tasks/*, 에이전트가 이미 씀)로, QUEUE 상태 열은 스크립트 생성 | 중 | K8·L#10 삭제 가능, 중복 발주 위험↓ |
| 한도 출처 5곳 | LIMITS 30/8 · STATE.md:10 24/5/8 · STATE.md:87 30/8/12 · harness 24/5/8 · C1E1 꼬리 6/2 · 메모리(6·opus 2) | LIMITS.md 하나만, 나머지는 'LIMITS 참조' | 소 | 보조 지휘자 오판 방지 |
| 로그 공백 | PIPELINE_LOG 18:00 이후 0줄 | 보조 지휘자는 자기 상태 파일에 1줄, 최상위는 파도 끝 요약 1줄 | 소 | 감사 가능성 |

### C-6 동시 에이전트 20→30 상향 위험
| 위험 | 증거·계산 | 판단 |
|---|---|---|
| 사용량 | 16:5x 시점 주간 51%(2.9억/5.8억, PIPELINE_LOG.md:245), 오늘 1.637B | 30 으로 올리면 지휘자 알림·턴도 비례 증가 → B-1 해결 전엔 위험 |
| 검증 대기열 | 오늘 완료 ~111건/약 15시간 ≈7.4건/시. 병합 처리 4~7.5분/단계 = 8~15건/시. 30 이면 ~11건/시(추정) → 라인 1 속도에 근접 | 병합 라인 1개(final)로 모으면 감당, 2라인 재분기 금지 |
| heavy | gate-v2 기준 비아트 3슬롯, 에이전트당 빌드+묶음 5~10분(추정) → 18~36건/시 | 30 에이전트면 대기 발생. RAM 5.6GB 로 상한 10 |
| 충돌 | QUEUE 97·SelfTest 70·DP 67 커밋 — 에이전트 수에 비례 | C-7 전엔 병합 수작업 증가 |
권고: gate-v2 반영 + 빈 RAM ≥6GB + 비아트 대기 중앙값 <10분 확인 전까지 실효 20 유지, 조건 충족 뒤 30(LIMITS 값 자체는 사용자 결정이라 유지).

### C-7 공유 파일 동시 편집 충돌을 구조로 줄이기
| 대상 | 권고 | 비용 | 효과 |
|---|---|---|---|
| DECISIONS_PENDING.md | `docs/decisions_pending/<branch>.md` 브랜치당 1파일(append-only 디렉터리), 지휘자는 glob 로 수집 | 소 | union 8회→0 |
| ENGINE_NOTES.md (255KB) | 새 내용은 `docs/engine_notes.d/<card>.md` 조각, release 가 주제 절로 합침(towncrier 식) | 소 | union 8회→0, Grep 비용↓ |
| ops/QUEUE.md·STATE·FV 표 | 작업 브랜치에서 ops/ 수정 금지를 merge_step 단계 0 에서 검사(diff 에 ops/ 있으면 STOP 대신 자동 'ours' + 경고) | 소 | 'ops ours' 31회 수작업 제거 |
| FV/Replay | C-1 fv_claims 디렉터리 + 병합 때 할당(번호 할당기) | 소 | FV 줄 12회 기계 충돌 자동화 |
| src/SelfTest.cs 등록부 | 시험 파일이 [SelfTestGroup] 특성 또는 partial 등록으로 스스로 등록(리플렉션), 등록 줄 편집 제거 | 중(sonnet, 시험 인프라·골든 무관) | 70커밋 핫스팟 제거 |
| docs/CONTROLS.md | Bindings.All 에서 생성(T38b 동기 시험 이미 있음) | 소 | 3회 충돌 제거 |

## D. 웨이브 2 착수 전 체크리스트 (10)
1. 도구 5종(tools/pipe-tools 7ddd32c · gate-v2 f482e7b · c15-watchdog eb7bcfa · selftest-parallel cfaaa0a · fv-renumber a9366d4)을 main 에 반영(문서·도구 전용, 게임 코드 불변 확인).
2. LIMITS.md:9 '무거운 작업' 행 삭제, STATE.md:10·harness 상한·C1E1 공통 꼬리 한도를 LIMITS 참조로 교체(메모리 6/2 와의 차이는 사용자 확인).
3. 웨이브 2 base = merge/0.2-final ce99a09 로 고정. 기존 9개(tcb·tcc·tcg·g4·m1·m3·g3·tcf·m2)는 final 을 1회 merge-in.
4. FV 정렬: 증가 4건(TC-B·TC-C·M1·g3)을 병합 순서로 68~71 / Replay 25~26 배정, FV_RESERVATIONS.md:38 의 TC-G=68 삭제(증가 없음).
5. QUEUE 정합: T04·T10·C1E1-m1/m2 등 병합된 행 갱신, closed/merged 행을 보관 파일로 이동(시작 읽기 ~20k 절감).
6. LESSONS.md 를 K1~K11 로 교체, A-2 줄은 역할 카드로, A-3 는 보관(사용자 승인 필요 — 역할 카드 변경 규칙 PIPELINE.md:45).
7. 발주 프롬프트 템플릿: 공통 꼬리 삭제, 필수 3칸(기반 해시 · fv_claims 여부 · 핫스팟 파일 목록) + preflight 출력 1줄.
8. 새 브랜치부터 decisions_pending/ · engine_notes.d/ · fv_claims/ 디렉터리 규칙 적용(기존 파일 소급 변경 없음).
9. 지휘자 새 세션으로 시작, 완료 알림은 로그 1줄(≤200자), 유휴 점검은 state_snapshot 출력만 읽기.
10. 동시 수는 실효 20 유지 → gate_wait.log 로 비아트 대기 중앙값 <10분·빈 RAM ≥6GB 확인 뒤 30.
