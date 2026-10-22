# STATE - 한 장 현황 (갱신 2026-10-22, docs-fable 병합 시점)

> 정본: 상태 = 이 파일 · 대기열 = QUEUE.md · 병합 = MERGE_QUEUE.md · 한도 = LIMITS.md · 결정 = DECISIONS.md · 흐름 = PIPELINE.md. 새 지휘자 세션은 이 파일 → QUEUE → DECISIONS → PIPELINE 만 읽고 시작(PIPELINE §4).
> 쓰는 사람: 지휘자(점검·인계) + release(큐 끝 한 줄). 작업 에이전트는 쓰지 않는다.
> **인계 규칙 (2026-10-22)**: 인계는 이 STATE.md 한 곳에만 쓴다. 해시·워크트리·잠금·heavy 수 같은 실측값은 직접 적지 말고 `tools/agent/state_snapshot.ps1` (-Write → ops/STATE_LIVE.md)를 인용한다. `ops/PIPELINE_LOG.md` 는 append 전용 — 세션 시작 때 읽지 않는다 (필요할 때만 Grep).

## 실측값 (인계 2026-10-23)
- main 해시 · 워크트리 목록 · 병합 잠금 · 실행 중 에이전트 수 · FV/Replay 버전은 문서에 쓰지 않는다. 명령: `powershell -File korean-division/tools/agent/state_snapshot.ps1 -Write` → `ops/STATE_LIVE.md` 를 인용.
- 병합 현황 (2026-10-23): 병합선 N3c 완료 · N3d 진행 · 0.2.1 빌드 예정 03:20 · 0.3 승격은 사용자 명령 대기. (상세 = ops/MERGE_PROGRESS.md 끝, 실측 = STATE_LIVE.md)
- 낡은 절(기준 · 미병합 브랜치 표 · 지금 진행 중 · 18:00 인계 §1~§5) = ops/STATE_ARCHIVE_2026-10-22.md. 카드 상태 = ops/QUEUE.md (끝난 행 = ops/QUEUE_DONE.md) · 결정 = ops/DECISIONS.md (옛 결정 = ops/DECISIONS_ARCHIVE_2026-10-22.md).
- 아래 §6~§10 의 해시·브랜치 값은 해당 시각(10-22 밤) 기록이며 낡았을 수 있다 — 재개 전에 STATE_LIVE.md 와 ops/MERGE_PROGRESS.md 끝을 확인.

### 6. 사용자 결정 (기본값 적용 중 · DECISIONS BA–BG · ops/AUTO_DEFAULTS_LOG.md 는 append-only, routine 로 읽지 않음)
- 한글 제목 BE 확정: 창 'KOREAN DIVISION : MARCH ON MANCHURIA' · 메뉴 '대한 사단 : 북벌 1920' · 극중 '제17 대한 사단'.
- 기본값 적용: ColorBlind 설정 확인 (T38m) · 간결 모드 색각 도형 · 엎드려쏴 칸(prone cells) · save-compat 정책 (자동 기본값) · CoverSeek 군집 = 0.3 밸런스 항목.
- 사용자 몫: 커맨더 템플릿 작성 (C:/works/commanders_authoring) · 상표 검증 KIPRIS/USPTO (출시 전 필수) · U01 GitHub 작성자 확인 · U02 옛 폴더 삭제 · T26 .git/hooks 쓰기 권한 · 시트 검수 (T08/T22/T28/T19c/T11, 종이 시안).

### 7. 고정 규칙
- 답변 한국어, 짧게. push 금지 (사용자 명령 시만). 병합은 병합 라인 경유만 (main 직접 병합 금지). main 은 최종 `--ff-only`, 사용자 확인 뒤.
- 밸런스 최후 (0.3 one-shot), AI 개선 0.4. 웹 조회는 Gemini (`hub ask -w`) 만.
- 창 모드만, 1280x720 이하, 전체화면·테두리없음 금지. 보조 모니터 = env `KD_MONITOR=secondary` (~/.claude/settings.json; T38n 병합 뒤 게임이 읽음).
- 한도: ops/LIMITS.md 참조 (값 정본; RAM 가드 포함). 에이전트 최종 보고 ≤12줄. 큰 컨텍스트 에이전트는 재개하지 않고 새로 띄운다.
- 읽기 전용 리뷰어는 C:/gameProject 에서 git checkout 금지 (LESSONS 20).
- 질문은 추천 기본값을 자동 적용하고 소수씩. 세션 교체는 하루 1회. 지휘자 세션은 컨텍스트가 크다 (이번 세션 724k, 전체 토큰 51%) → 답변·로그 줄을 짧게.

### 8. 알려진 문제
- 게이트 대기가 1시간 넘게 걸린 적이 여러 번 (렌더 레인이 풀을 채우면 병합 빌드를 굶긴다). 다음 세션은 먼저 조사: 렌더 lane 6 · RAM 예산 (LESSONS 25).
- 병합 중 체크섬이 예상과 다르거나 비기계적 충돌이 나면 그 단계에서 멈추고 보고 (임의 해결 금지). crossing 은 B5b 이후 PASS 가 정상.
- 실행 파일 apphost 가 .NET 런타임을 못 찾는 환경 이슈 → release 기록 참조 (`dotnet bin/main/...` 로 실행).

### 9. 검수 자료 · 보고서
- 리뷰 시트 `C:/works/_review_sheets_2026-10-22` (항목 01–24, paper_ui_v2_tabs/) · 로드맵 PDF `C:/works/KoreanDivision_roadmap_2026-10-22.pdf`. 사용자의 그래픽 검토는 나중에 한 번에.
- 보고서: ops/REPORT_2026-10-22.md (2026-10-22 13:00 기준 전체 보고·로드맵 미큐 목록·역제안).

### 10. 2026-10-22 밤 상황 저장 (지휘자 세션 교체 직전; 재개: 이 절 → ops/BACKLOG_2026-10-22.md → ops/MERGE_PROGRESS.md 끝)
- 베이스: kd-final 브랜치 merge/0.2-final = de2a6053 (병합 L1~L8 완료: selftest-unknown-group, IsAi, storage-guard, t44, j-followup, t51a, t52a, ti1; 설정 패널 높이 650px 합산·재설계 필요; detcheck 불변, snaptest 86 OK). 다음: L9 ti4 → L10 tfa → L11 g2 → L12 rp2 → L13 t48e (MERGE_PROGRESS.md 끝 참조). 락 해제됨. main=486c4d79 (ff 금지, 사용자 확인 뒤). push 금지.
- 병합 대기 브랜치(병합 L 순서·리뷰 6/7 반영): fix-selftest-unknown-group b0d4272f · IsAi 왕복 e1e7b558 · tools/storage-guard a73f4d20→8c3be52b(day_close D: 복사 포함) · t44-smoke-run 9d4105dd · j-followup-fixes 2199de69 · t51a-path-guard 34dd8d4f · t52a-alert-log c88bb69a · ti1-i18n-core 9a08cce8 → ti4-data-text 638b680d(반드시 ti1 뒤) · tfa-campaign-graph bbcee2ab(스키마 14 임시) · c1e1-g2-episode-ui fc7982ba · rp2-aar-pages 7b58d08a(시험 ==24 하드코딩 수정) · t48e-battle-link 5303bf99(FV 68→71 재배정, 시험 체인 등록). 이후 병합 M: tfb-campaign-advance adb28d17(TF-A 위) · gun-cost-wire 677f2d13 · tw2-blizzard 92e6bf2f(FV 임시 68 재배정) · t50a-perf 61e05ebe(캐시 필드 snapshot 분류 반영) · t39e-muzzle-code-clean 5d83369a(UnitsSelfTest 충돌 예상) · a1-terrain ed1d80b3 → a1-roads ab9ab733 · a2-ground-decals b63062dc · d-trains 21a3b158 · c1e1-art-units 8b73c78b · B2 cavalry 4종 렌더(kd-b2-units, 진행 파일 RERENDER_PROGRESS.txt) · B1(마적/봉천/척후, kd-b1-units 또는 기존 worktree).
- 아트 검수 시트(사용자 확인 대기): a1_terrain_before_after · a1_roads_contact · a2_decals_contact · d_trains_contact · c1e1_new_units (각 worktree korean-division/docs/art_review/).
- 새 운영 규칙: 게이트 v2.1 설치됨(CPU<85%, 여유RAM>=8GB, 입장간격 20s, 3GB 미만 10s 감시, 아트풀 4/비아트 3) · 에이전트 한꺼번에 20개 띄우기 금지(PC 2회 멈춤) · 병합자 git reset --hard 금지(사고 1회, ~/.claude/settings.json 금지 목록 보강 완료) · 관리자 정책(C:\Program Files\ClaudeCode: managed-settings.json + hooks/kd_guard.ps1 + kd_scope.json) 설치됨, 다음 세션부터 적용 → 첫 확인은 무해한 시험만, 위험 명령 실제 입력 금지 · 수정 가능 폴더: gameProject, works, 바탕화면 coding, downloads (+도구용 supportRoots), 범위 밖은 클릭 허용창 · 저장공간: 로컬 100GB(현 약 77GB), D:\KoreanDivisionBackup 2차, G: 마일스톤만 150GB, 버전=daily/날짜 태그(day_close.ps1, '상황 저장' 때) + 마일스톤은 사용자 명령 때만 · 오늘 daily/2026-10-22 태그와 bundle 완료(C:\works\_backup\KoreanDivision, D:).
- 열린 사용자 결정: 설정 패널 높이 재설계(병합 후 약 650px/1024x640) · harness 정지의심 판정 엄격화 여부 · g2 승인 대기 5건 · 사용자 결정 대기 18건(BACKLOG 하단) · 리눅스 머신(4GB, 2008년경 CPU)은 빌드/렌더 부적합, 백업 용도만 가능(SSH 키 미설정, 사용자명 fv2).
- 큐: ops/BACKLOG_2026-10-22.md (48개, 충돌 그룹 표시; 끝난 에이전트 자리에 위에서부터 꺼내 발주). 아트 발주는 subagent_type "art" 시도.
- 남은 작업 힌트: 포 단가·TF-C(TF-B 후)·TI2(병합 후)·T50b/c(T50a 후)·PR2 변형 재보고(10-23 01:02 크론은 세션 종료로 사라졌을 수 있음 → 다시 등록)·QUEUE 정합(queue_status -Write, 병합 후)·워크트리 정리 2차(사용자 승인 필요)·m2 후속 완료(gun-cost-wire).
- (22:25 추가) B1(kd-b1-units, kd/b1-deferred-units-1 9b4d049f): infantry-bandit·thug-bandit·dadao-fengtian 렌더 완료(import 전, 시트 game-tools/korean-division-image-out/_res96/), 남음 infantry-scout(산출물 비어 재렌더)·officer-bandit — 재개: game-tools/image 에서 `python -u rerender96.py --only infantry-scout,officer-bandit`. B2(kd-b2-units): cavalry 4종 렌더 드라이버가 OS 프로세스로 계속 도는 중일 수 있음 → RERENDER_PROGRESS.txt·b2_render.log 확인 후 완료분 import/커밋. 모든 에이전트는 WIP 상태로 넘김(실행 중 에이전트 없음, 사용자 지시).
- (22:25 추가) 관리자 정책 v2 초안(스크래치패드 policy 폴더): 백업 폴더(D:\KoreanDivisionBackup, G:\내 드라이브\KoreanDivisionBackup, C:\works\_backup) 삭제·이동·이름변경 금지(noDelete) 추가, 모의 시험 27/27. 설치된 v1 은 이 규칙이 없음 → 사용자가 관리자 PowerShell 로 policy\hooks\* 를 C:\Program Files\ClaudeCode\hooks\ 에 다시 복사해야 적용(복사 명령은 이전과 동일). 실제 위험 명령 입력 시험 금지.
- (22:30) 마일스톤 v0.2.1 백업 완료: 태그 v0.2.1 = merge/0.2-final de2a6053. bundle: v0.2.1.bundle(태그 이력만 365MB; C:/works/_backup/KoreanDivision, D:\KoreanDivisionBackup, G:\내 드라이브\KoreanDivisionBackup) · v0.2.1-all-branches.bundle(모든 브랜치 포함 537MB; C:, D:). 복구: git clone <bundle> 또는 git fetch <bundle> 'refs/*:refs/*'.
