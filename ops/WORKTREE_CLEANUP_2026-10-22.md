# Worktree 정리 2026-10-22 (A류 제거)

- 대상: worktree_classified.tsv 의 class=A 행 82개
- 검증 기준: (a) git status 비어 있음 (b) 브랜치가 main 또는 merge/0.2-final 의 조상 (c) 해당 경로를 쓰는 실행 중 프로세스 없음 (d) 폴더명이 kd-final / kd-merge* / kd-develop 아님
- 통과 82개 / 건너뜀 0개, 통과분 디스크 합계(TSV 추정) 46.02 GB

| 경로 | 브랜치 | 결과 | 사유 | 크기 GB |
|---|---|---|---|---|
| gt-develop/gt-face-proto | gt-face-proto | PASS |  | 0.32 |
| kd-develop/kd-ai3b2 | ai3b2 | PASS |  | 0.70 |
| kd-develop/kd-breach2 | wip/breach2 | PASS |  | 0.70 |
| kd-develop/kd-fvrenumber | tools/fv-renumber | PASS |  | 0.32 |
| kd-develop/kd-gemini-import | gemini-import | PASS |  | 0.63 |
| kd-develop/kd-isai-restore | kd/isai-restore | PASS |  | 0.47 |
| kd-develop/kd-mapeditor-spec | kd/t33-map-editor-design | PASS |  | 0.32 |
| kd-develop/kd-monitor-launch | tools/monitor-launch | PASS |  | 0.54 |
| kd-develop/kd-orderq2 | wip/orderq2 | PASS |  | 0.91 |
| kd-develop/kd-rail | rail | PASS |  | 0.47 |
| kd-develop/kd-river-r1 | river-r1 | PASS |  | 0.54 |
| kd-develop/kd-river-r2 | river-r2 | PASS |  | 0.47 |
| kd-develop/kd-river-r3 | river-r3 | PASS |  | 0.47 |
| kd-develop/kd-river-r4 | river-r4-bridge | PASS |  | 0.55 |
| kd-develop/kd-sfxnew | sfxnew | PASS |  | 0.47 |
| kd-develop/kd-spec-cards | spec/cards-t24-t25-t10-t16 | PASS |  | - |
| kd-develop/kd-spec-river | spec/river-design | PASS |  | 0.32 |
| kd-develop/kd-spec-t27 | spec/cards-t27-t28 | PASS |  | 0.32 |
| kd-develop/kd-spec-t30 | spec/cards-t30-train | PASS |  | 0.32 |
| kd-develop/kd-spec-t31 | spec/cards-t31 | PASS |  | 0.32 |
| kd-develop/kd-spec-t34 | spec/cards-t34 | PASS |  | 0.32 |
| kd-develop/kd-spec-t39 | spec/cards-t39 | PASS |  | 0.32 |
| kd-develop/kd-spec-t42 | spec/cards-t42 | PASS |  | 0.32 |
| kd-develop/kd-spec-ui | spec/ui-backlog | PASS |  | 0.32 |
| kd-develop/kd-t08 | t08-ui-bundle | PASS |  | 0.48 |
| kd-develop/kd-t09 | t09-camp-slice | PASS |  | 0.48 |
| kd-develop/kd-t10 | kd/t10-comms-b | PASS |  | 0.47 |
| kd-develop/kd-t10b | kd/t10b-difficulty | PASS |  | 0.91 |
| kd-develop/kd-t16 | kd/t16-trench | PASS |  | 2.16 |
| kd-develop/kd-t17 | t17-cmd-convenience | PASS |  | 0.71 |
| kd-develop/kd-t19a | t19a-save-slots | PASS |  | 0.55 |
| kd-develop/kd-t19b | t19b-ammo-shells | PASS |  | 0.55 |
| kd-develop/kd-t19b2 | t19b2-synced | PASS |  | 0.55 |
| kd-develop/kd-t19c | t19c-home-tabs | PASS |  | 0.63 |
| kd-develop/kd-t19d | t19d-doctrines | PASS |  | 0.71 |
| kd-develop/kd-t19spec | spec/t19-split | PASS |  | 0.32 |
| kd-develop/kd-t23 | spec/t23-campaign-home | PASS |  | 0.32 |
| kd-develop/kd-t24 | kd/acting-delay-tier | PASS |  | 1.95 |
| kd-develop/kd-t24-crossing-fix | kd/t24-crossing-fix | PASS |  | 0.63 |
| kd-develop/kd-t25 | kd/t25-bayonet-all | PASS |  | 0.54 |
| kd-develop/kd-t27 | t27-cmd-ui2 | PASS |  | 0.48 |
| kd-develop/kd-t28 | t28-cover-forest | PASS |  | 0.47 |
| kd-develop/kd-t29b | t29b-on-t09 | PASS |  | 0.71 |
| kd-develop/kd-t30a | t30a-train-move | PASS |  | 0.70 |
| kd-develop/kd-t30b | kd/immune-target-fix | PASS |  | 0.85 |
| kd-develop/kd-t30c | t30c-freight-objective | PASS |  | 0.70 |
| kd-develop/kd-t30d-train-art | t30d-train-art | PASS |  | 0.32 |
| kd-develop/kd-t30e | t30e-demo-mission | PASS |  | 0.47 |
| kd-develop/kd-t31a | kd/t31a-regroup | PASS |  | 0.47 |
| kd-develop/kd-t31c-surrender | kd/t31c-surrender | PASS |  | 0.47 |
| kd-develop/kd-t32a | kd/t32a-pioneer | PASS |  | 0.56 |
| kd-develop/kd-t32b | kd/t32b-ai-smoke | PASS |  | 0.47 |
| kd-develop/kd-t32d | kd/t32d-pioneer-art | PASS |  | 0.49 |
| kd-develop/kd-t33a-map-model | kd/t33a-map-model | PASS |  | 0.92 |
| kd-develop/kd-t33c-placement | kd/t33c-placement | PASS |  | 0.79 |
| kd-develop/kd-t33d-rail | kd/t33d-rail | PASS |  | 0.79 |
| kd-develop/kd-t33e-map-size | kd/t33e-map-size | PASS |  | 0.79 |
| kd-develop/kd-t34a | kd/t34a-suppress | PASS |  | 0.70 |
| kd-develop/kd-t38a | t38a-settings-keys | PASS |  | 0.49 |
| kd-develop/kd-t38b | t38b-help-overlay | PASS |  | 0.48 |
| kd-develop/kd-t38c-on-ui | t38c-on-ui | PASS |  | 0.49 |
| kd-develop/kd-t38e | t38e-unitlist-codex | PASS |  | 0.49 |
| kd-develop/kd-t38f | t38f-home-readonly-tabs | PASS |  | 0.48 |
| kd-develop/kd-t38g | t38g-ui-harness | PASS |  | 0.56 |
| kd-develop/kd-t38k | t38k-fonts | PASS |  | 0.52 |
| kd-develop/kd-t38m | t38m-overlay-declutter | PASS |  | 0.57 |
| kd-develop/kd-t38n | kd/t38n-monitor-env | PASS |  | 0.47 |
| kd-develop/kd-t42a | t42a-fatigue-morale | PASS |  | 0.48 |
| kd-develop/kd-t42b | t42b-promotion | PASS |  | 0.48 |
| kd-develop/kd-t42c | t42c-prisoners | PASS |  | 0.48 |
| kd-develop/kd-t42d | t42d-schedule-log | PASS |  | 0.71 |
| kd-develop/kd-t43 | kd/t43-branch-symbols | PASS |  | 0.47 |
| kd-develop/kd-t45 | kd/t45-menu-hide | PASS |  | 0.47 |
| kd-develop/kd-t48a | t48a-commander-data | PASS |  | 0.55 |
| kd-develop/kd-t48b1 | t48b1-officer-identity | PASS |  | 0.48 |
| kd-develop/kd-t48b2 | t48b2-assign | PASS |  | 0.48 |
| kd-develop/kd-t48c | t48c-favor | PASS |  | 0.71 |
| kd-develop/kd-t48d | t48d-commander-ui | PASS |  | 0.63 |
| kd-develop/kd-t49a | t49a-save-messages | PASS |  | 0.48 |
| kd-develop/kd-t49bc | t49bc-compat-tests | PASS |  | 0.48 |
| kd-develop/kd-tcd | kd/tcd-funds-slots | PASS |  | 0.55 |
| kd-develop/kd-trains-annih | kd/trains-in-annihilation | PASS |  | 0.47 |

## 제거 결과
- 제거(git worktree remove, --force 없음): 82개 / 실패: 0개 / 건너뜀: 0개
- git worktree prune 실행 완료. 브랜치는 삭제하지 않음.
- C: 여유 공간: 제거 전 206.99 GB -> 제거 후 252.35 GB (+45.36 GB)
- kd-develop 크기 측정은 생략(느림). 통과분 TSV 합계 46.02 GB는 디스크 열이 '-'인 행을 제외한 추정치.
- 세부 결과: scratchpad/remove_results.tsv (C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\1adea170-c177-41f1-b663-68e71e8cf246\scratchpad\remove_results.tsv)
