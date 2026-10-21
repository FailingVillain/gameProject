# 병합 절차 + 병합 담당 계층
> 요약: 작업 에이전트는 잠금을 잡지 않고 브랜치 커밋 + 카드 `wip-ready` 로 종료. 병합은 상시 merger 만. 소규모 예외만 자기 병합.

## 원칙
- **작업 에이전트(sim/feature/art/docs…)는 병합 잠금을 잡지 않는다**: 브랜치를 커밋하고 작업 카드 상태를 `wip-ready` 로 쓴 뒤 종료. 병합은 merger(상시)만. 순서 = `ops/MERGE_QUEUE.md`.
- **예외(자기 병합)**: 카드가 "문서 전용 소규모"라고 적은 경우만, 아래 절차로. 작업이 "do not merge" 이거나 사용자 결정 대기면 병합하지 않는다.
- **잠금 수명**: `mkdir C:/gameProject/.git/kd-merge.lock` 후 그 안에 시작 시각 기록(`started.txt`). 30분 넘으면 만료 - merger 가 보유자(에이전트 id/PID)를 확인한 뒤 정리. 남의 잠금은 merger 외 아무도 지우지 않는다. 잠금이 있으면 대기 루프 없이 보고하고 정지.
- merger 책임: Snapshot/Replay 버전 일괄 재번호(FormatVersion = max(main, branch)+1), 골든 재생성 1회, 정리, 보고. 비기계적 충돌은 abort 후 해당 카드 담당에게 되돌림. 큐 끝 전체 selftest + 문서 기록은 release.

## 병합 단계 (merger, 또는 예외 자기 병합)
1. 잠금 획득(위). 잠금을 쥔 동안만 게이트를 `-Priority` 로 호출.
2. main 을 브랜치로 **한 번만** 병합. main 이 또 움직이면 충돌 시에만 재병합, 아니면 `--detcheck` + 자기 묶음만 재시험.
3. 빌드 -> `--detcheck` (골든 변경 시 `--update` 후 `tools/detcheck.ps1` 8변형) -> 자기 묶음 `KD_SELFTEST_ONLY=<group>` (동시 2개까지) -> 상태 변경 시 `--snaptest` (`KD_SNAP_QUICK=1`), UI 변경 시 `--uifuzz 30`.
4. 전체 `--selftest` 는 하지 않는다 (큐 끝 release). 보고에 "전체 selftest 생략" 명기.
5. main 에서 `.git/MERGE_HEAD` 없음 확인 후 `git merge --no-ff <branch>` (1회 재시도). 병합 후 main detcheck. 깨지면 `git reset --hard ORIG_HEAD`, 잠금 해제, 보고.
6. 잠금 해제(`rmdir`, 실패 시에도), worktree + 브랜치 제거(no --force), 코드 변경 시 `bin/main` 재빌드, `publish.ps1` 은 `dist/KoreanDivision.exe` 미실행일 때만(PID 확인). `ops/MERGE_QUEUE.md` 병합 완료 표에 한 줄 추가 + 카드 상태 `merged`. 그 밖의 문서 갱신(CLAUDE/PROJECT_NOTES/BASELINE/STATE)은 하지 않는다 — release 몫.
7. 측정이 불분명하면 기능을 JSON 스위치 뒤(기본 꺼짐)에 두고 병합, 판단은 나중에.
