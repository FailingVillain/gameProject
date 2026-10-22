# PIPELINE - 작업 흐름 구조
버전: v2 · 갱신: 2026-10-22 · 정본: 이 파일이 작업 흐름 구조의 정본 (상태·대기열·결정의 정본은 STATE/QUEUE/DECISIONS).

**표준 흐름 (v2)**: 카드 → 구현(wip-ready) → **리뷰**(reviewer, 필수) → 수정 사이클 → 측정(목표가 있는 카드) → 통합 리허설 → 병합(사용자 허가 뒤).

## 1. 단계별 흐름
| 단계 | 담당 | 입력 → 출력 | 한도(LIMITS) | 병목 → 대응 |
|---|---|---|---|---|
| 입력 | 사용자 | 지시 → DECISIONS 답 / QUEUE 행 | - | 결정 25건 적체 → 저위험은 기본값 진행(D22) |
| 작업 정의 | spec(또는 지휘자) | QUEUE 행 → tasks/Tnn 카드 | 카드 ≈1.5KB · L 카드는 사전 분할 | 중복 조사 → MERGE_QUEUE 완료 표 + GIT_HISTORY_DIGEST grep 의무 |
| 발주 | 지휘자 | 카드 → 에이전트(역할 카드 + 카드 3줄 프롬프트) | 에이전트 24 · 上 5 · 파도 ≤12 | 지휘자 알림 싱크 → 파도마다 세션 교체 · QUEUE 행 낡음 → 발주 전 MERGE_QUEUE·main 대조(T18) |
| 코딩 | sim/feature/art/docs | 카드 → 브랜치 커밋 | 컨텍스트 200k | 핫스팟 충돌 → H≥2 같은 파일은 직렬 · 역할 오배정 → 코드 카드는 docs 금지(T26) |
| 로컬 검증 | 같은 에이전트 | 빌드·detcheck·자기 묶음·snaptest → 카드 결과 | 무거운 작업 8 | 게이트 대기 25분+ → 전체 selftest 는 release 만 |
| wip-ready | 같은 에이전트 | 카드 상태 wip-ready → 리뷰 발주 → MERGE_QUEUE 행(지휘자) | 잠금 금지 | 잠금 폴링 → 작업 에이전트는 잠금을 안 잡음 |
| 리뷰 | reviewer(읽기 전용, 필수) | wip-ready 브랜치 diff → 차단/높음/중간 결함 목록 | 브랜치 1건당 리뷰 1 | 리뷰 누락 → 리뷰 없이 다음 스택 발주 금지 |
| 수정 사이클 | 작성자 또는 수정 에이전트 | 리뷰 지적 → 같은 브랜치 1커밋 → 재검증 | 사이클 1회 = 1커밋 | 스택 하위 수정 → 상위 브랜치에 SendMessage 로 체리픽 지시 |
| 측정 | measure | main 커밋 → BASELINE_CURRENT 블록 | heavy 8 | 개별 측정 중복 → 통합 측정 1회(T12) · 목표 미달 → wip-ready 금지, 정직 보고(T06·T16) |
| 통합 리허설 | 지휘자(제안, 사용자 확인) | 병합 순서대로 integration/rehearsal 에 합침 → selftest·golden → ops/REPORT 기록 | 잠금 1개 | 충돌·번호 겹침 → FV_RESERVATIONS 로 재번호 후 재시험 |
| 병합 | merger(상시) | 리허설 통과 → main, 완료 표 행, 카드 merged | 잠금 1개, 만료 30분 | main 이동마다 재검증 → main 1회 병합, 충돌 시만 재병합 |
| release 기록 | release | 큐 끝 → selftest·골든·publish·기록 5곳, 카드 closed | 큐당 1회 | 첫 실 가동은 fix/n12-forest 병합 뒤(§8) |
| 결정 장부 | 지휘자 | DECISIONS_PENDING 줄 → DECISIONS D번호 | - | 여러 번 묻기 → 한 장부, 사용자 한 번에 답 |
| 지휘자 점검 | 지휘자 | harness /api/state → STATE.md | 20분(이벤트 없을 때 보조) | 세션 360k → 역할 한정 + 교체 |

### 1-a 발주 규칙 (v2)
- 스택 발주: 프롬프트에 기반 커밋 해시 · 선행 카드의 리뷰 blocker 상태 · FV/Replay 번호를 적는다. 번호를 올리는 브랜치는 FV_RESERVATIONS.md 에 먼저 적는다. 선행 수정 커밋은 SendMessage 로 체리픽 지시.
- L 카드는 사전 분할(T19 성공). 세부 설계가 없는 카드는 '기본 꺼짐 스위치 + 초안 수치'를 수용 기준에 적고, 결정 선택지에는 키 이름·대상을 명시(T10·T06).

### 1-b 정지 감지 (v2)
- 신호: 태스크 출력 파일의 크기·mtime. 0바이트 또는 장시간 무변화 = 시작 실패 → 새 에이전트로 재발주. 워크트리 무변화만으로 판단하지 않는다.
- 출력이 계속 커지면 대기 중일 수 있음(heavy_gate, 측정). 정지 판정 전 tools/agent/heavy_gate.ps1 풀 점유를 확인(게이트 v2: `ops/gate_wait.log` 의 점유자 PID/이름, `heavy_gate.ps1 -DryRun`). 무거운 잡(렌더·측정)은 에이전트 수명과 분리.
- 재발주 전 원 에이전트가 살아 있는지 확인(T16: 5.5시간 뒤 정상 완료, 66k 중복).

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

## 5. 버전 기록
| 버전 | 날짜 | 바뀐 것 | 근거 숫자 | 점수 |
|---|---|---|---|---|
| v1 | 2026-10-22 | 4층 문서 + ops + LEAKS, 역할 10, merger 상시·잠금 금지·만료 30분, release 분리, 결정 장부 기본값 | 에이전트 45 · 병합 10/13(04:08~04:40) · 잠금 대기 2h+ · 지휘자 360k · 중복 조사 25만 토큰 | Fable 독립 ≈45 / 자체 54 → 개선 후 예상 60~65(Fable) vs 75(자체) |
| v2 | 2026-10-22 | 리뷰 단계 필수화 · 수정 사이클 · 스택 규칙(기반 해시·FV 예약표·체리픽) · 정지 감지(출력 파일) · L 사전 분할 · 기본 꺼짐 스위치 · 통합 리허설 · release 첫 실 가동 조건 | 발주 ~70 · wip-ready ~38 · 리뷰 18 (전부 결함 포착) · 수정 사이클 11 · 수용 기준 미달 보고 4 · 오류 3(T18 중복 · T26 역할 오배정 · T16 중복 발주) | 자체 72/100 |

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
- 파도 크기 시험(≤12 · 上 ≤3 · heavy ≤6)은 Fable 권고 — LIMITS 값은 사용자 지시(24/5/8) 유지, 다음 파도에서 12 로 한 번 시험해 숫자 기록.
- 지휘자 세션 교체 주기(파도마다)는 시험 뒤 확정.
- release 첫 실 가동: 2026-10-22 완료 (selftest 2008 OK).
- 통합 리허설 담당·절차: 첫 병합 전 사용자 확인.

## 정책 추가 (2026-10-22, 사용자 지시)
WIP 는 병합하지 않는다 — 재개해 `wip-ready` 까지만. merger 미발주. '선행=병합' 카드는 선행 wip 브랜치 위에 스택. 근거: 사용자 강조 지시.
(v2) 병합은 사용자 허가 뒤, 통합 리허설(integration/rehearsal) 통과 후에만 main 으로.

## 자동 기본값 로그 (사용자 2026-10-22)
추천 기본값을 자동 적용할 때는 `ops/AUTO_DEFAULTS_LOG.md` 에 **한 줄 append**(`echo >>`)만 한다. 이 파일은 읽기 목록(STATE→QUEUE→DECISIONS→LESSONS)에 넣지 않고 에이전트 프롬프트에서도 읽지 않게 한다(토큰 누수 방지). 사용자가 요청할 때만 지휘자가 읽어 요약한다.
