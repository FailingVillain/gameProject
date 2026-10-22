# STATE - 한 장 현황 (갱신 2026-10-22, docs-fable 병합 시점)

> 정본: 상태 = 이 파일 · 대기열 = QUEUE.md · 병합 = MERGE_QUEUE.md · 한도 = LIMITS.md · 결정 = DECISIONS.md · 흐름 = PIPELINE.md. 새 지휘자 세션은 이 파일 → QUEUE → DECISIONS → PIPELINE 만 읽고 시작(PIPELINE §4).
> 쓰는 사람: 지휘자(점검·인계) + release(큐 끝 한 줄). 작업 에이전트는 쓰지 않는다.
> **인계 규칙 (2026-10-22)**: 인계는 이 STATE.md 한 곳에만 쓴다. 해시·워크트리·잠금·heavy 수 같은 실측값은 직접 적지 말고 `tools/agent/state_snapshot.ps1` (-Write → ops/STATE_LIVE.md)를 인용한다. `ops/PIPELINE_LOG.md` 는 append 전용 — 세션 시작 때 읽지 않는다 (필요할 때만 Grep).

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

## 인계 (2026-10-22 18:00 · 지휘자 세션 교체용 — 새 세션은 이 절 → QUEUE → DECISIONS(BG 이후) → LESSONS 순)
> 이 절이 최신이다. 위 '기준'·'지금 진행 중' 절의 main 해시(562e578)·에이전트 수는 낡았다. 실측 명령: `git -C C:/gameProject log -1 --format=%h main` · `git worktree list` · `git branch -v`.
> 이전 인계 원문 16:1x = ops/PIPELINE_LOG.md 끝 "인계 (2026-10-22 16:1x 원문" 절 · 08:50 원문 = "이전 인계 보관" 절.

### 1. 상태 (18:00 실측)
- 실행 중 에이전트 0 · 병합 잠금 없음(.git/kd-merge.lock, kd-merge-b.lock 모두 없음) · 모든 작업은 로컬 브랜치에 커밋 · push 없음.
- main = de2f7cf (ops 문서 커밋만 추가, 코드 불변).

### 2. 병합 라인 1 (sim) — 진행 중, WIP 병합 사용자 승인(AX)
- 워크트리 `C:/gameProject/kd-develop/kd-merge` · 브랜치 `merge/0.2` · HEAD `c8ecf90`.
- 완료 태그: pre-merge-A · post-A · pre-merge-B · post-B(5a9e0bb) · pre-merge-C · post-C(6a5f199) · pre-merge-D · post-D(7611a0d).
- 다음 = 그룹 E (FV 꼬리). 번호 예약 IsAi 61 · river 63 · 다리 64 · 연막(T32b) 65 · T34a 66 (62 비움).
  - E1 kd/isai-restore `5c08a38` (FV 61 / Replay 24; `korean-division/tools/agent/fv_renumber.ps1 -Report` 후 `-Set`)
  - E2 river `r1 5b7b9f1` → `r2 8ada7a9` → `r3 26df7f6` → `r4 9d4b7be`(river-r4-bridge) + balance.json `terrain.waterBlocksFire: true` 1줄 추가 (r2 리뷰 비차단 지적)
  - E3 T32b `33284fa` (FV 65) + T34a `29c2479` (FV 66)
- 기대 detcheck: `672804207fc4e486 3c42aea49324248e` (units=29 shots=3237, D 이후 값).
- 기대 FAIL: uiicons (최종 합치기 전까지 정상) · traindemo (골든 재생성 때 해소). 그 밖의 FAIL 은 멈추고 보고.
- 매 단계 snapshot + `--snaptest` 실행 (LESSONS 28). selftest 는 `--selftest` 와 `KD_SELFTEST_ONLY` 함께 (없으면 게임이 뜬다).
- 진행 기록: kd-merge/ops/MERGE_QUEUE.md 완료 표. kd-merge/ops/MERGE_PROGRESS.md 의 "RESUME POINT (after D6)" 는 낡음 — D6 은 7611a0d 로 완료됨.

### 3. 병합 라인 2 (캠페인 + UI) — 2단계까지 완료
- 워크트리 `C:/gameProject/kd-develop/kd-merge-b` · 브랜치 `merge/0.2-ui` · HEAD `2093a74` · 태그 `ui-post-phase2`.
- 완료: F1–F8, G 단계 t45 ed4b7f2 · t38k 61041d8 · t38l d1158c9 · t47 ead14ae · t48a fc19995 · t48c 3ef7942 · t49a 43d8397 · t49bc 20132b4 · perf-bench cc967ed (t38e 보류: ops 충돌·'partial'; t39i 건너뜀: 이미지만) · spec 문서 H3 병합(태그 ui-pre-merge-H3).
- 검증: detcheck `8aa6448b9690b922` = main 골든과 일치. 기록 = kd-merge-b/ops/MERGE_PROGRESS_UI.md.
- 남음: T33d 는 최종 합치기 때(라인 1 의 rail·river 지형 필요). 편집기 지형 목록에 Bridge/Ford/Rail 은 강 병합 뒤 들어간다.

### 4. 최종 합치기 (라인 1 의 E 끝난 뒤)
- 순서: merge/0.2-ui → merge/0.2 → t38e `e9e1642` → t38m `2ca199c` → t38n `a13672a`(KD_MONITOR) → T33d `022647f` + T33e `3dfab34` → 연대장 체인 t48b1 `3fcdacf`(FV 53→67, campaign_v9 샘플) · t48b2 `80c11fb` · t48d `b5a10af` → C1E1 tcd `0b98b10` · g1 `bd1dc11` · m1 `638c172` · m2 `fd1108a` · u2 `8061b9a`(캠페인 스키마 v11/v12; 시뮬 라인 불변) → kd/prone-wide-assets `bdd8d9a`(T11b) → 민가 t55m `bb081b6` · t55s `51b2fe8` · gt-house-gen `bd6e885` · a1 `b4a53db` · a2 `14f20e2` · b1 `cdef9f8` · b2 `e5a4db5` (T55i 임포트 카드 아직 없음) → tools/selftest-parallel `cfaaa0a` → tools/perf-bench (cc967ed 로 이미 들어감, 중복 확인만).
- 그 다음: 골든 재생성(T36/T37 등, 사용자 AX 승인분) → 전체 selftest `tools/selftest_parallel.ps1 -Jobs 4` (~268초) → main `--ff-only` 는 **사용자에게 보고 후** 조건 확인 (DECISIONS AX). push 는 절대 없음.

### 5. 다음 카드 (에이전트 없음 · 기능 먼저, 밸런스 최후)
- T38d 종이 UI 전 화면 (게임 전체 UI 종이 질감, Palette 토큰; 시안 `C:/works/_review_sheets_2026-10-22/paper_ui_v2_tabs`). 제목은 BC/BE 확정 (아래 4번 결정 참조).
- C1E1 2차 (wave 2): `spec/cards-c1e1` 의 C1E1_IMPLEMENTATION_PLAN.md · C1E1_WAVE1_PROMPTS.md — 미션·엔진 g2–g6·데이터 d1·텍스트. FV 예약 = ops/FV_RESERVATIONS.md.
- T48e 커맨더 전투 연결 (기본 꺼짐). T50a–c 성능 (병합 뒤; ops/PERF_OPT_PLAN.md, 벤치 tools/perf-bench; 골든 동일 필수. T50d 완료).
- T56a–e 스프라이트 칸 B (ops/SPRITE_CELL_PLAN.md; T56b 는 T01 + T55s 병합 뒤).
- T39e–j 아트 (T11 재렌더 뒤): kd-rerender 브랜치 `e589068` (467dafc 포함, 15:36 시점 48/81). 완료 여부 확인 필요.
- 공병 엎드려쏴 62° 재작업 (임시 회피 kd-t32d af357ce; 근본 수정 gt-prone-fix 는 완료 → 공병 재렌더 후속).
- 교전 테스트 BattlePanel 접기 (상시 개발 패널) · AQ2 제거 검토 · T55i 민가 임포트 · 생성기 v2 희망 목록 · 편집기 증분 렌더러 결정.

### 6. 사용자 결정 (기본값 적용 중 · DECISIONS BA–BG · ops/AUTO_DEFAULTS_LOG.md 는 append-only, routine 로 읽지 않음)
- 한글 제목 BE 확정: 창 'KOREAN DIVISION : MARCH ON MANCHURIA' · 메뉴 '대한 사단 : 북벌 1920' · 극중 '제17 대한 사단'.
- 기본값 적용: ColorBlind 설정 확인 (T38m) · 간결 모드 색각 도형 · 엎드려쏴 칸(prone cells) · save-compat 정책 (자동 기본값) · CoverSeek 군집 = 0.3 밸런스 항목.
- 사용자 몫: 커맨더 템플릿 작성 (C:/works/commanders_authoring) · 상표 검증 KIPRIS/USPTO (출시 전 필수) · U01 GitHub 작성자 확인 · U02 옛 폴더 삭제 · T26 .git/hooks 쓰기 권한 · 시트 검수 (T08/T22/T28/T19c/T11, 종이 시안).

### 7. 고정 규칙
- 답변 한국어, 짧게. push 금지 (사용자 명령 시만). 병합은 병합 라인 경유만 (main 직접 병합 금지). main 은 최종 `--ff-only`, 사용자 확인 뒤.
- 밸런스 최후 (0.3 one-shot), AI 개선 0.4. 웹 조회는 Gemini (`hub ask -w`) 만.
- 창 모드만, 1280x720 이하, 전체화면·테두리없음 금지. 보조 모니터 = env `KD_MONITOR=secondary` (~/.claude/settings.json; T38n 병합 뒤 게임이 읽음).
- 한도: 에이전트 ≤30 · opus ≤8 · heavy gate 12 (빈 RAM ≥6GB) · heavyRAM 14GB. 에이전트 최종 보고 ≤12줄. 큰 컨텍스트 에이전트는 재개하지 않고 새로 띄운다.
- 읽기 전용 리뷰어는 C:/gameProject 에서 git checkout 금지 (LESSONS 20).
- 질문은 추천 기본값을 자동 적용하고 소수씩. 세션 교체는 하루 1회. 지휘자 세션은 컨텍스트가 크다 (이번 세션 724k, 전체 토큰 51%) → 답변·로그 줄을 짧게.

### 8. 알려진 문제
- 게이트 대기가 1시간 넘게 걸린 적이 여러 번 (렌더 레인이 풀을 채우면 병합 빌드를 굶긴다). 다음 세션은 먼저 조사: 렌더 lane 6 · RAM 예산 (LESSONS 25).
- 병합 중 체크섬이 예상과 다르거나 비기계적 충돌이 나면 그 단계에서 멈추고 보고 (임의 해결 금지). crossing 은 B5b 이후 PASS 가 정상.
- 실행 파일 apphost 가 .NET 런타임을 못 찾는 환경 이슈 → release 기록 참조 (`dotnet bin/main/...` 로 실행).

### 9. 검수 자료 · 보고서
- 리뷰 시트 `C:/works/_review_sheets_2026-10-22` (항목 01–24, paper_ui_v2_tabs/) · 로드맵 PDF `C:/works/KoreanDivision_roadmap_2026-10-22.pdf`. 사용자의 그래픽 검토는 나중에 한 번에.
- 보고서: ops/REPORT_2026-10-22.md (2026-10-22 13:00 기준 전체 보고·로드맵 미큐 목록·역제안).
