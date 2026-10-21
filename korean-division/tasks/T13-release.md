# T13 release (병합 큐 끝 1회: 전체 selftest · 골든 · publish · 기록)
- id: T13 | 역할: release | 등급: D中 H1 (선택 메모: R2 SM Wheavy)
- 선행: T12 (= T03 T04 T05 병합 뒤) | 후행: T11 렌더 예약, T20
- 핫스팟/만질 파일: 코드 수정 없음. 문서만: docs/PROJECT_NOTES.md '알려진 실패' 최신 줄, docs/legacy/SELFTEST_HISTORY.md, docs/BASELINE_CURRENT.md, ops/STATE.md 한 줄, ops/MERGE_QUEUE.md 큐 닫음, 카드 상태 closed
- 브랜치: main 직접(문서 전용 커밋, 잠금 규칙은 merge-procedure.md) | 상태: queued | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "병합 직전엔 결정성+해당 시험, 전체 시험은 큐 끝에 한 번" (속도 개선 5안 ①, 사용자 승인 2026-10-22)
- 병합마다 하던 CLAUDE/SELFTEST_HISTORY/BASELINE/HANDOFF 갱신은 release 가 큐 끝에 한 번 (2026-10-22)
## 범위 밖
실패한 시험 수정(보고만; 지휘자가 해당 카드 재개) · 밸런스 값 변경 · push
## 수용 기준
전체 `--selftest` FAIL 0 · detcheck 8변형 동일 · snaptest OK · `dist/KoreanDivision.exe` 재발행(실행 중이면 생략 명기) · 위 기록 5곳 갱신 커밋
## 측정
docs/procedures/gate.md 대로 무거운 단계마다 게이트; 소요 분을 보고에 적는다(LEAKS 주간 숫자용)
## 결과 (에이전트가 끝에 3~5줄만)
-
