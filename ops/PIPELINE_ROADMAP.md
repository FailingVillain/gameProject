# PIPELINE_ROADMAP - 파이프라인 개선 로드맵 (사용자 승인 2026-10-22)

## 완료
- 도구 3종 (branch tools/pipe-tools 7ddd32c): merge_step.ps1 · preflight.ps1 · state_snapshot.ps1. 실행은 `powershell -ExecutionPolicy Bypass -File`.
- STATE 단일화 규칙 · 리뷰 체크리스트(FV/저장 필드/snapshot 분류) · LESSONS 31.
- 아트 보조 지휘자 1호 가동 (ops/ART_STATE.md). 한도: 아트 렌더 8(시작 6) + 비아트 heavy 3 (LIMITS.md).

## 웨이브 2 도입 (사용자 승인)
1. **보조 지휘자 3분야: 아트 · 캠페인 · 병합.**
   - 아트: 가동 중. 캠페인: 웨이브 2 시작 때 임명(C1E1 카드·미션 JSON·UI). 병합: merger 상시(= 병합 보조 지휘자; 최종 합치기 담당).
   - 규칙: 자기 풀 한도 안에서만 발주 · QUEUE/STATE 는 최상위 지휘자만 수정(보조는 전용 상태 파일 ART_STATE/CAMPAIGN_STATE 만) · 파도마다 새 에이전트 · 최종 보고 ≤15줄 · sim(H3)은 보조 없이 최상위가 직렬 관리.
2. **감독관(opus, 읽기 전용): 기본 주기 60분** (사용자 별도 명령 시 변경).
   - 일: 토큰 사용 패턴·누수, 중복 발주, 정지 에이전트, QUEUE↔실제 상태 불일치, 병합된 브랜치의 결정성·저장 형식·FV·snapshot 분류 누락 탐지.
   - 산출: ops/WATCH_REPORT.md append (숫자 필수, 숫자 없으면 기록 안 함; 형식은 watch/LEAKS.md 규칙).
   - 비용 통제: 매 주기 ≤60k 토큰 — state_snapshot 출력 + 마지막 주기 이후 diff 만 읽는다. 작업이 없는 유휴 시간엔 건너뜀. 코드 수정 금지.
   - 첫 가동: 웨이브 2 시작 (최종 합치기 직전 정밀 점검 1회 포함).

## 남은 개선안 (순서)
- **[우선·웨이브 2 시작 전, 사용자 승인 2026-10-22]** 게이트: 빌드 전용 슬롯 예약 + 대기 시간 로그 + 대기만 하는 렌더 프로세스(-Render waiters)가 풀 슬롯을 쥐지 못하게 수정. 근거: T44 에이전트가 kd-rerender 대기 프로세스 때문에 heavy_gate 20분+ 정체(LESSONS 25 재발). 게이트 수정 뒤 T44 스모크 실행 마무리(kd-t44, tools/smoke_missions.ps1).
- 스택 깊이 ≤3 · FV 예약 강제 (도구 정착 뒤 PIPELINE.md 규칙화).
- 정지 감지 watchdog (러너 PID · scratchpad · heavy_gate · 워크트리 변화).
- QUEUE 상태 일괄 정합 (웨이브 1 종료 때; 낡은 행 확인 = T38e T38h T49b T49c).
- r2 후속: GameMapTerrain.FiringLineBlock(표시 전용)이 waterOpen 을 안 넘김 — waterBlocksFire=false 일 때만 차이; river 담당 후속 확인(웨이브 2 소카드)
