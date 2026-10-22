# 저장공간·백업 규칙 (tools/storage-guard)

## 한도
- 로컬 프로젝트 폴더 합계(C:/gameProject 전체): 80GB 경고(WARN), 100GB 초과 FULL.
- FULL = 새 카드 발주 중단 + 정리 목록을 사용자에게 보고. 자동 삭제 없음.
- 드라이브 `G:\내 드라이브\KoreanDivisionBackup` 150GB 상한. 초과 시 삭제 "후보"(오래된 daily 번들)만 출력, 삭제는 사용자.

## 명령
- `tools/disk_guard.ps1 [-Fast] [-Json]` 읽기 전용. -Fast=캐시 크기(%LOCALAPPDATA%\kd\disk_guard_cache.json, 첫 실행·캐시 삭제 시 실측 약 1~2분).
  출력: 상태, 상위 10 폴더, 정리 후보 A(main 병합 완료+깨끗한 worktree) / B(병합됐으나 더럽거나 고아·scratch 폴더). 미병합 브랜치는 절대 후보 아님.
  JSON: schema, totalGB, status, stopNewCards, top10[], candidatesA[], candidatesB[]. 지휘자는 발주 전 `-Fast -Json` 의 stopNewCards 확인.
- `tools/day_close.ps1 [-WhatIf] [-Milestone vX.Y.Z] [-UploadDaily]` 하루 마감. 실행 트리거는 사용자의 "상황 저장" 신호(수동). 스케줄러 등록 금지.
  기본: 태그 `daily/YYYY-MM-DD`(main 기준, 이미 있으면 건너뜀) + 로컬 bundle `C:/works/_backup/KoreanDivision/daily-YYYY-MM-DD.bundle`(--all). G: 에는 안 씀.
  `-Milestone`: 사용자 명령 때만. 태그 vX.Y.Z 생성(기존 태그는 이동 안 함) + 번들 + G: 복사. `-UploadDaily`: daily 번들도 G: 복사(명시 시에만).
  끝에 G: 용량 점검(후보 목록만) 과 disk_guard 요약.

## 버전 규칙
- daily/YYYY-MM-DD: 로컬 기본. 마일스톤 vX.Y.Z: 사용자 명령으로만 생성·갱신(자동 금지).

## 복구
- `git clone <bundle> 폴더` 또는 `git fetch <bundle> '+refs/*:refs/*'` 후 `git checkout <tag>`. 검증: `git bundle verify <bundle>`.
- 정리는 수동: `git worktree remove <경로>` ; `git branch -d <브랜치>`(병합된 것만).

## ops 커밋 감축 안 (제안 — 적용 전 지휘자/병합자 검토 필요)
1. ops 기록(QUEUE·STATE·MERGE_QUEUE·AUTO_DEFAULTS_LOG 등)은 카드 단위로 커밋하지 않고, 병합 라운드 끝 또는 하루 마감(상황 저장) 때 한 커밋으로 묶는다.
2. 병합은 가능하면 `--ff-only` 선형(브랜치를 main 에 rebase 후 ff). 병합 커밋 감소.
3. [검토 필요] ops/MERGE_PLAN.md §충돌 규칙은 `--no-ff` 병합 + ops 충돌 ours 를 전제 — 2번과 충돌. rebase 시 ops 충돌 처리·골든 규칙과의 정합은 병합자 결정.
4. [검토 필요] 카드 상태가 ops/QUEUE 에 묶여 있어 커밋을 미루면 상태가 늦게 보임 — queue_guard/STATE 갱신 주기와 조율.
