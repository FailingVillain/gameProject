# STATE - 한 장 현황 (2026-10-22, 코디네이터 점검 때 갱신)

> 정본: 상태 = 이 파일 · 대기열 = ops/QUEUE.md · 병합 = ops/MERGE_QUEUE.md · 한도 = ops/LIMITS.md. HANDOFF.md 는 이쪽을 가리킨다.

## 기준
- main: `4d19119` (ops: decision register D01-D22). Snapshot FormatVersion 52 · Replay FormatVersion 19 (comms 병합분까지).
- 실행 중 에이전트: 0 (이 문서 작성 시점; 값은 harness /api/state 로 확인) · 병합 잠금: 없음 (`.git/kd-merge.lock` 없음).
- 한도: 에이전트 24 · 上 5 · 무거운 작업 8 (ops/LIMITS.md).
- 워크트리: kd-develop/docs-roles (docs-roles 브랜치, 다른 문서 작업) · kd-develop/gate-lanes · kd-develop/docs-ops (이 작업).

## wip 브랜치 15개 (전부 main 보다 1커밋 앞선 `WIP snapshot` 스냅샷, 미병합)
"패치 보관" = 충돌로 `wip-patches/<이름>.patch` 파일로만 저장된 스냅샷(코드 브랜치 아님, 병합 전 복원 필요). 내용 설명은 확인된 것만 적음.

| 브랜치 | 스냅샷 | 형태 | 병합 상태 | 내용/메모 |
|---|---|---|---|---|
| wip/gemini-import | 80c6f50 | 패치 보관 | 미병합 | Gemini 캠페인 WIP + UI 가져오기 (패치 64MB). 저장 트랜잭션 수정 선행 필요 |
| wip/bigbldg | a6fdbeb | 브랜치 | 미병합 | 건물 크기 확대 (민가 3x3~3x4, 큰 건물 4x6) |
| wip/breach | 73d173c | 패치 보관 | 미병합 | 참호 돌파 (연막 · 철조망 · 경박격포 · 연막 수류탄). D17 대기 |
| wip/orderq | 35c2297 | 패치 보관 | 미병합 | 사격 통제 / 명령 대기열 / 사선 표시 |
| wip/ai3b2 | c87745f | 패치 보관 | 미병합 | 전술 AI 3B (큐 마지막 병합자가 전체 selftest 1회) |
| wip/rail | e7c9a19 | 패치 보관 | 미병합 | 철도 지형 (장갑열차 제외) |
| wip/casualty | 32eed08 | 브랜치 | 미병합 | 사상자 속도 조정. D19 결정 대기(보류) |
| wip/chokefix | bbf480d | 브랜치 | 미병합 | choke 수정. D14 (choke 끔 유지) |
| wip/battlereport | 1b72242 | 패치 보관 | 미병합 | 전과 보고 추정 - 확인 필요 |
| wip/doctrines | 116c9fa | 브랜치 | 미병합 | 장교 학파/교리 추정 - 확인 필요 |
| wip/sfxnew | 280187e | 패치 보관 | 미병합 | 효과음 추가 추정 - 확인 필요 |
| wip/rerender-1 | 6d927ee | 브랜치 | 미병합 | 전 유닛 재렌더 1차 |
| wip/gt-brender | eb5b94d | 브랜치 | 미병합 | game-tools 렌더 - 확인 필요 |
| wip/gt-face-proto | 6ee1c60 | 브랜치 | 미병합 | game-tools 얼굴 프로토타입 - 확인 필요 |
| wip/gt-rerender-prep | fc6fa1d | 브랜치 | 미병합 | game-tools 재렌더 준비 |

병합 완료 목록(main 에 이미 있음, 참고): arty3 · pronemove · units · assaultinf · formation(대형 완화) · comms 알파 ① (Snapshot 52 에 반영).

## 사용자 결정 대기 (ops/DECISIONS.md)
D01 D02 D07 D08 D12 D15 D19 D20 보류형. 나머지는 "무응답 시" 기본값으로 진행(D22).
