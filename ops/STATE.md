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

## 인계 (2026-10-22 16:1x 실측 · 지휘자 세션 교체용 — 새 세션은 이 절 → QUEUE 상태 실측 표 → DECISIONS BF → PIPELINE_LOG 마지막 20줄 순)
- 기준: main `6baacd5` (ops: T38l logged). 확인 `git -C C:/gameProject log -1 --format=%h main`. 워크트리 = `git worktree list`. 병합 잠금 보유 중: `.git/kd-merge.lock`(라인 1), `.git/kd-merge-b.lock`(라인 2) — 남의 잠금 지우지 않음.
- 정책: 병합 정책 해제(사용자 AT, 15:0x). WIP 병합은 병합 라인 경유만 (main 직접 병합 금지), main 은 마지막에 `--ff-only`, 계획 = ops/MERGE_PLAN.md.
- 병합 라인 1 (sim): `kd-develop/kd-merge`, branch `merge/0.2` (tip 4a5d322 = actingCache 분류 fixup). A1–A6, B1–B9 완료 (태그 pre-merge-A · post-A · pre-merge-B). B9 snaptest FAIL(T37 actingCache 미분류) → 지휘자 결정 (b) fixup 후 계속. 재개 = B10 kd/t10-comms-b 1c91c96 (FV 57/22 예상). 상세 = `kd-develop/kd-merge/ops/MERGE_PROGRESS.md`.
- 병합 라인 2 (캠페인+UI): `kd-develop/kd-merge-b`, branch `merge/0.2-ui` (base main d1eaf2e). F1–F8 완료 (태그 ui-post-F = e5fba3a). G 계획: G1 t08 2e9aef5 · G2 t17 0eb92e8 · G3 t27 4cf5c45 · G4 t38g f5a76d4 · G4b t38h f126551(체리픽만) · G5 t38b·t38a → t38c-on-ui 85e6658(uiicons 블로커 해소) · H t43 d45b18f · t38i 3e41444 · T33a–c (T33d 는 최종 합치기 때). G 진행 기록 없음 → 확인 필요 (`kd-merge-b/ops/MERGE_PROGRESS_UI.md`).
- 최종 합치기: 라인 1+2 → 골든 재생성(사용자 OK 후 1회; T36/T37 골든 변경) → 전체 selftest · detcheck 8변형 · release → 사용자 "진행" 뒤 main ff-only. FV 재번호 계획: rail 58 · t30a 59 · t30b 60 · IsAi 61 · river 63 · 다리 64 · 연막(T32b) 65 · T34a 66 (AE2 62 비움).
- 한도: 에이전트 30 · opus 8 · heavy gate 12 (빈 RAM ≥6GB 일 때, 기존 10) · heavyRAM 14GB (heavy 프로세스 WorkingSet 합). 렌더 레인 6개(E/F 포함) — 멈춤 시 E·F 먼저 중단.
- 진행 중 흐름 (실측):
  - 공병 엎드려쏴 총구 잘림: 원인 = 192x240 칸 폭(LodAtlas 가 칸 크기 미독해). 임시 회피 kd-t32d af357ce. 근본 수정 = opus art, 넓은 칸 지원 kd/prone-wide-cells (기반 gt-prone-fix 8a1794d; E/W 정확 직각·총구 ≤0.5°). 공병 재렌더 후속.
  - 지휘관(연대장·대대장) T48: 설계 spec/cards-commanders 89b2e5f. wip-ready = T48a e31406d · T48b1 fa8dff1(opus, FV 53) · T48c c24c423 · T49a 4896113 · T49b+c 11e7a7b. T48b2 발주 기록 — 상태 확인. T48d/e 는 T48b2 뒤. 연결 필요: ApplyDismissPenalty · CanRehire · ApplyMissionTags.
  - T38 UI + 종이 UI: wip-ready = T38a 7274c71 · T38b 93ce79f · T38c-on-ui 85e6658 · T38f b622d1d · T38g f5a76d4 · T38i 3e41444 · T38l fa03891. T38e 미커밋 10건(kd-t38e). T38d(종이 스킨, Palette 토큰, 전 화면: a/b/e/f/h/i 포함) queued. 시안 gt-paper-ui-v2 e689cf5 + 활성 탭 3갈래 tabs-a 922b5f7 · tabs-b 4ff6382 · tabs-c 24b5fee (사용자 "추후 한 번에" 검토; 폴더 C:/works/_review_sheets_2026-10-22/paper_ui_v2_tabs). 글꼴 OFL ui/fonts-ofl c4a4006 (T38k).
  - 캠페인: T47 지도 줌 921f715 wip-ready. C1E1 안동 점령전 = 명세 → 카드 분해 spec/cards-c1e1 f31fc42.
  - 민가 다양화 1차 42종 (BF): 계획 spec/house-variety c731b37. 1단계 발주 기록 T55g(생성기) · T55s(66d6b95 위) · T55m(323c2da) — 상태 확인. T55a/b 는 T55g 뒤.
  - 성능 T50: 계획 ops/PERF_OPT_PLAN.md (eac7b55), 기준 ops/PERF_BASELINE_2026-10-22.md. 순서 T50d(벤치) → T50a → T50c → T50b → T50e(조건부), 골든 동일 필수. 표적 캐시·탐색 반경 축소는 사용자 결정 전 미발주.
  - 재렌더 T11 (kd-rerender e589068): 48/81 완료 (15:4x). T39e/f/g/h/j 는 T11 뒤. rerender96 편집 금지(완료 전).
- 제목 (BC/BE 확정): 창 'KOREAN DIVISION : MARCH ON MANCHURIA' · 메뉴 한글 '대한 사단 : 북벌 1920' · 약칭 KOREAN DIVISION / 코리안 디비전 · 극중 부대 '제17 대한 사단'. 제목 교체 T38l wip-ready.
- 사용자 결정 대기 (DECISIONS A2·B·E~BF): 답 없는 항목 = 괄호 기본값 적용, 자동 기본값 기록 = ops/AUTO_DEFAULTS_LOG.md (이 세션에서 읽지 않음). 주요: M1 fix/n12-forest 병합 여부 · J1/J2 T06 보류 · Q1 난이도 소비처 · N1~N4 통신 알파(꺼짐) · R01 함성 방침 잠정 B(재보고).
- 사용자 행동 (결정 아님): U01 GitHub 작성자 확인 · U02 옛 폴더 삭제 · T26 .git/hooks 쓰기 권한 · 시트 검수(T08/T22/T28/T19c/T11, 종이 시안) · kd-throwaway-t38c 정리(--force, 사용자 몫).
- 사용자 standing rules: push 금지(명령 시만) · WIP 는 병합 라인 경유만 · 밸런스 최후순위 · 웹 검색은 Gemini (`hub ask -w`) · 게임 창 = 창 모드 ≤1280x720, 전체화면·테두리없음 금지, run_game.ps1 로 보조 모니터 · 질문은 추천 기본값 자동 적용·소수씩 · 세션 교체 하루 1회 · 큰 카드는 사전 분할 · 답변 한국어.
- 다음 단계 (순서):
  1. 라인 1: B10 → B11~B13 → 그룹 C(철도·덮개숲) → D(열차) → E(FV 꼬리). 단계마다 snapshot+snaptest. 체크섬 예상 밖·비기계 충돌 = 멈추고 보고.
  2. 라인 2: G1~G5 확인 → H → 라인 2 완료 표 갱신.
  3. 두 라인 합치기 → 골든 재생성(사용자 OK) → selftest·detcheck 8변형 → release 기록 → 사용자 "진행" 시 main ff-only.
  4. 병합 뒤: T38e 미커밋 정리 · T38d 종이 스킨 · T48b2 → T48d/e · T49 연결 · T55 1단계 확인 · C1E1 카드 발주.
  5. 성능 T50d→a→c→b · 재렌더 T11 완료 → T39e/f/g/h/j · 공병 근본 수정 완료 → 공병 재렌더.

(보고서: ops/REPORT_2026-10-22.md — 2026-10-22 13:00 기준 전체 보고·로드맵 미큐 목록·역제안. 이전 인계 절 원문 = ops/PIPELINE_LOG.md 끝 "이전 인계 보관".)
