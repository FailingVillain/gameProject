# STATE - 한 장 현황 (갱신 2026-10-22, docs-fable 병합 시점)

> 정본: 상태 = 이 파일 · 대기열 = QUEUE.md · 병합 = MERGE_QUEUE.md · 한도 = LIMITS.md · 결정 = DECISIONS.md · 흐름 = PIPELINE.md. 새 지휘자 세션은 이 파일 → QUEUE → DECISIONS → PIPELINE 만 읽고 시작(PIPELINE §4).
> 쓰는 사람: 지휘자(점검·인계) + release(큐 끝 한 줄). 작업 에이전트는 쓰지 않는다.

## 기준 (실시간 값은 명령으로 확인)
- main: `562e578` (docs-fable 병합). 확인 `git -C C:/gameProject log -1 --format=%h main`.
- Snapshot `FormatVersion = 52` (src/Core/Snapshot/Snapshot.cs:83) · Replay `19` (src/Core/Commands/Replay.cs:22). 병합 때마다 다시 읽는다.
- 실행 중 에이전트: 0 (작성 시점; 실제는 harness `GET http://localhost:8500/api/state`). 병합 잠금: 없음.
- 한도: 에이전트 24 · 上 5 · 무거운 작업 8 (LIMITS.md), 파도 ≤12 권장.
- 워크트리: kd-develop/crossingfix(8d22ad5, 변경 없음, 용도 확인 필요) · gate-lanes(병합됨, 제거 가능) · docs-fable(이 작업, 병합 후 제거).

## main 에 들어간 것 (상세 = MERGE_QUEUE 병합 완료 표, 옛 이력 = docs/legacy/GIT_HISTORY_DIGEST.md Grep)
스냅샷 a1a35e6 안: 돌격보병 · 기마 포병 · 저장 호환 · 포병 · 타찬카 · 신규 병종 · 튜토리얼 · 몽골/마적 · 대형 완화 · 통신 ① · 진단 문서. 그 뒤: 건물 크기(8d22ad5) · 게이트 레인(기본 비활성) · 도구 정리 · 결정 장부 · 문서 재정비 2회 · docs-fable.

## 미병합 브랜치 = 이어서 작업할 브랜치 (그대로 병합하지 않는다)
모두 스냅샷 `a1a35e6` 위 1커밋 = main 보다 **뒤**. "패치" = `korean-division/docs/legacy/wip_patches/<이름>.patch` 로만 저장(game-tools 는 `game-tools/legacy/wip_patches/`), 복원 = `git apply --3way --directory=korean-division`(README). 순서: 담당 에이전트가 main 에서 새 브랜치 → 복원 → 검증 → 카드 `wip-ready` → merger.

| 브랜치 | 형태 | 카드 | 내용 (패치 파일 목록으로 확인) |
|---|---|---|---|
| wip-resolved/breach 7faccf0 | 브랜치(main 8d22ad5 위 해결본, FV53/R20, 골든 재생성) | T03 | 참호 돌파 — 여기서 이어감(wip/breach 는 원본) |
| wip/gemini-import | 패치(64MB) | T01 | 캠페인 WIP + UI, 저장 트랜잭션 선행 |
| wip/orderq | 패치 | T04 | 명령 대기열 · 사격 통제 · 사선 |
| wip/ai3b2 | 패치 | T05 | 전술 AI 3B |
| wip/rail | 패치 | T07 | 철도 지형 (Terrain · MapFile · RailRenderer) |
| wip/casualty | 브랜치 | T06 | 사상자 속도 — **보류 D19** |
| wip/chokefix | 브랜치 | 없음 | Choke · OrgFormation · Squads — **보류 D14** |
| wip/battlereport | 패치 | T17 | 전과 보고 (MissionResults · SaveManager) |
| wip/doctrines | 브랜치 | T19 | 장교 교리 데이터 + 시험 (D16) |
| wip/sfxnew | 패치 | 없음 | 효과음 (sounds.json · SfxDirector · SOUND_POLICY) |
| wip/rerender-1 · gt-brender · gt-rerender-prep | 브랜치 | T11 | 재렌더 1차 · Blender 렌더 · rerender96 준비 |
| wip/gt-face-proto | 브랜치 | 없음 | 얼굴 프로토타입 (face_proto.py) |
| wip/bigbldg | 브랜치 | T02 | **병합됨**(8d22ad5), 정리 대상 |

## 지금 진행 중 / 다음 한 걸음
- 진행 중: 없음(에이전트 0, wip-ready 0, 병합 큐 비어 있음).
- 다음: 선행 없는 queued 카드 발주 — T03(sim: wip-resolved/breach 에 main 병합·검증 → reviewer → wip-ready), T01(feature: 패치 복원), T07, T18. T08/T09 는 T01 뒤. 보류형 결정 D01 D02 D07 D08 D12 D15 D19 D20 은 사용자 답 대기.
