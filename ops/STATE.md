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

## 인계 (2026-10-22 08:50, 취침 중 자율 가동 종료 — 새 지휘자 세션은 이 절부터)
정책(사용자 강조): **WIP 는 병합하지 않는다 — wip-ready 까지만.** push 금지·merger 미발주. 결정 장부 = ops/DECISIONS.md (A~Z 절, 아래 요약), 가동 기록·v1 점검 = ops/PIPELINE_LOG.md, 번호 예약 = ops/FV_RESERVATIONS.md.
- main = 문서·ops 커밋만 추가(코드 불변). main selftest 4건 실패(n12·forest)는 **시험 낡음**이며 수정 브랜치 `fix/n12-forest` 36a5a87 (병합 여부 M1 대기) → 병합되면 release 가 전체 selftest·publish 재실행.
- wip-ready (병합 안 함, 각 브랜치 = kd-develop/<워크트리>): T01 gemini-import 63080df · T03 wip/breach2 · T04 wip/orderq2 0377f8a · T05 ai3b2 f74a675 · T07 rail · T08 t08-ui-bundle · T09+T29 → **t29b-on-t09 a89b4d2(T09·T29 대체)** · T10 kd/t10-comms-b · T12 measure/t12(측정) · T16 kd/t16-trench(기준 미달) · T17 t17-cmd-convenience 0eb92e8 · T19a t19a-save-slots 43e69b1 · T19b2 t19b2-synced · T19c t19c-home-tabs 9260e36 · T19d t19d-doctrines 04a9149 · T21 sfxnew · T22 gt-face-proto · T23 spec/t23-campaign-home 42fa456 · T24 kd/t24-s1s3 e4fd65b · T25 kd/t25-bayonet-all · T27 t27-cmd-ui2 · T28 t28-cover-forest · T11 kd-rerender(부분 4/70, 렌더 드라이버 백그라운드 가동 중).
- **수용 기준 미달·비권장**: T06 wip/casualty2 ef8daab(J1) · T16(Z1).
- 스택 체인: gemini-import → t09 → t19a → t19b → t19b2 → t19c → t19d (t29b 는 t09 위 별도, T19b2 가 흡수). breach2 → orderq2 → ai3b2 / kd/t24 · breach2 → t10 / t25 / t16.
- 사용자 결정 대기(무응답 시 괄호 기본값): M1 fix/n12 병합(보류) · J1/J2 T06(보류) · Z1 T16(변경 유지) · P1 marchChargeRange 3 vs 5(5 유지) · Q1 난이도 플래그 소비처(보류)·Q2 · N1~N4 통신 알파(꺼짐) · U1~U4 S1~S3 값(후보 유지, ③은 끔 권장) · R1/R2 · S1/S2 · T1~T4 · V1~V5 · O1/O2 · K1~K4 · L1~L3 · I1/I2 T28 옵션(꺼짐) · H1 야전포 사거리(80 유지) · Y1 T11 렌더 계속(드라이버 유지) · F1~F4 · R01 함성 방침(잠정 B, 재보고).
- 다음 발주 후보: T15(밤 soak, 렌더 풀 해소 뒤) · 기능별 측정(돌파·명령 큐·manReplace) · T10b(Q1) · 엎드려쏴 재렌더(T11 뒤) · T16 원인 재조사(Z1) · AI 항복 개선(J1 뒤) · 병합 단계 첫 가동 검증(사용자 허가 후).
- 사용자 행동: U01 GitHub 작성자 확인 · U02 옛 폴더 삭제 · T26 훅 설치 권한 허용(.git/hooks 쓰기) · T08/T22/T28/T19c/T11 시트 검수.
- 지휘자 세션은 컨텍스트가 큼 → /clear 후 이 STATE → QUEUE → DECISIONS → PIPELINE_LOG(v1 점검 표) 순으로 읽고 시작.

(보고서: ops/REPORT_2026-10-22.md — 2026-10-22 13:00 기준 전체 보고·로드맵 미큐 목록·역제안)
