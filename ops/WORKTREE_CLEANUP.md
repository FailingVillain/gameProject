# WORKTREE_CLEANUP - 워크트리 정리 목록 (기준 2026-10-22, main f3fe6db)

> 범위: `C:/gameProject/kd-develop/*` 와 `C:/gameProject/gt-develop/*` 의 등록된 워크트리 (`git worktree list` 기준 93개 중 kd·gt).
> 원칙: **삭제는 사용자 몫.** 아래 명령은 텍스트다. 실행은 `C:/gameProject` 에서 사용자가 한다 (rm -rf 는 권한 규칙으로 거부됨).
> `git worktree remove <path>` 는 브랜치를 지우지 않는다. 브랜치(커밋·카드)는 그대로 남는다. 미커밋 변경(dirty)이 있으면 git 이 거부한다. `--force` 는 그 변경을 버려도 되는지 사용자가 확인한 뒤에만 쓴다.
> dirty = `git status --porcelain` 줄 수 (2026-10-22 스캔 시점). 스캔 뒤 바뀌었을 수 있으니 실행 전 다시 본다.
> 요청에 적힌 "bin/fix assets" · "docs/art_review/t33b/maps_edit" 는 워크트리가 아니다 (목록에 없음). `korean-division/docs/art_review/` 의 미추적 PNG 는 main 의 untracked 파일이라 이 표 밖이다.
> "crossingfix leftovers" 중 워크트리는 `kd-t24-crossing-fix` 한 개뿐이고, 그 브랜치(kd/t24-crossing-fix 390763a)는 미병합 수정이라 **유지**한다.

## A. 지금 제거 가능 (측정 · 임시 · 중복 · 종결 · 카드 전용) — 브랜치 유지

| 워크트리 | 브랜치 @tip | 분류 | dirty | 판단 |
|---|---|---|---|---|
| kd-develop/kd-fvr-throwaway | (detached) a9366d4 | 임시 | 0 | tools/fv-renumber 와 같은 커밋. 제거 가능 |
| kd-develop/kd-meas-t06b-tip | (detached) 97ed026 | 임시·중복 | 0 | kd/t31a-regroup 와 같은 커밋. 제거 가능 |
| kd-develop/kd-meas-t06b-d19c | (detached) 97ed026 | 임시·중복 | 9 | kd-meas-t06b-tip 과 중복. dirty 9 확인 후 |
| kd-develop/kd-meas-t06b | measure/t06b @e241b94 | 측정 (카드만) | 1 | T06 종결. dirty 1 확인 후 |
| kd-develop/kd-meas-t12s | (detached) f74a675 | 임시 사본 | 2 | ai3b2 와 같은 커밋. dirty 2 확인 후 |
| kd-develop/kd-meas-t12 | measure/t12 @e6122ac | 측정 완료 | 2 | 재측정 때 다시 만듦. dirty 2 확인 후 |
| kd-develop/kd-meas-t15 | measure/t15 @a70a308 | 측정 완료 | 2 | 밸런스 보류. dirty 2 확인 후 |
| kd-develop/kd-meas-t31b | measure/t31b @05c228d | 측정 완료 (카드만) | 2 | T31b 종결. dirty 2 확인 후 |
| kd-develop/kd-casualty2 | wip/casualty2 @0722493 | 종결 (T06 폐기, inert) | 0 | 보존 브랜치만 남김 |
| kd-develop/kd-t16b | kd/t16b-rootcause @e724437 | 분석 종결 | 0 | 결과는 카드에 있음 |
| kd-develop/kd-integration | integration/rehearsal @984c404 | 리허설 카드 (코드 없음) | 0 | 결과는 PIPELINE_LOG 에 기록됨 |
| kd-develop/kd-t29 | t29-campaign-economy-json @783e7ce | 대체됨 (T29b 가 대신) | 0 | T29b 는 t09 위에서 다시 쌓음 |
| kd-develop/kd-t19spec | spec/t19-split @aea476b | 카드 전용 | 0 | 카드는 브랜치에 남음 |
| kd-develop/kd-spec-cards | spec/cards-t24-t25-t10-t16 @e75b00d | 카드 전용 | 0 | |
| kd-develop/kd-spec-river | spec/river-design @26e0b88 | 설계 문서 전용 | 0 | |
| kd-develop/kd-spec-t27 | spec/cards-t27-t28 @1a597bd | 카드 전용 | 0 | |
| kd-develop/kd-spec-t30 | spec/cards-t30-train @b36a2bd | 카드 전용 | 0 | |
| kd-develop/kd-spec-t31 | spec/cards-t31 @ccd9e9d | 카드 전용 | 0 | |
| kd-develop/kd-spec-t32 | spec/cards-t32 @726ec29 | 카드 전용 | 0 | |
| kd-develop/kd-spec-t34 | spec/cards-t34 @41799b5 | 카드 전용 | 0 | |
| kd-develop/kd-spec-t39 | spec/cards-t39 @aee2993 | 카드 전용 | 0 | |
| kd-develop/kd-spec-t42 | spec/cards-t42 @817703a | 카드 전용 | 0 | |
| kd-develop/kd-spec-ui | spec/ui-backlog @28605bf | 카드 전용 | 0 | |
| kd-develop/kd-mapeditor-spec | kd/t33-map-editor-design @3b7c994 | 카드 전용 | 0 | T33 카드 원본 |

제거 명령 (A, 실행 전 dirty>0 행은 변경 확인):
```
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-fvr-throwaway"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t06b-tip"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t06b-d19c"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t06b"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t12s"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t12"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t15"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-meas-t31b"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-casualty2"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t16b"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-integration"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t29"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t19spec"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-cards"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-river"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t27"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t30"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t31"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t32"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t34"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t39"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-t42"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-spec-ui"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-mapeditor-spec"
```

## B. 병합 후 제거 (해당 브랜치가 main 에 병합된 것을 확인한 뒤)

| 워크트리 | 브랜치 @tip | 조건 | dirty |
|---|---|---|---|
| kd-develop/kd-fvrenumber | tools/fv-renumber @a9366d4 | 병합 후 (A 의 kd-fvr-throwaway 와 같은 커밋) | 0 |
| kd-develop/kd-gemini-import | gemini-import @63080df | T01 병합 후 (캠페인 축의 뿌리) | 0 |
| kd-develop/kd-rail | rail @9f513e7 | T07 병합 후 (river·열차·T28 의 기반) | 0 |
| kd-develop/kd-t08 | t08-ui-bundle @2e9aef5 | T08 병합 후 | 0 |
| kd-develop/kd-t09 | t09-camp-slice @0c82a47 | T09 (T29b 로 대체) 병합 후 | 0 |
| kd-develop/kd-t23 | spec/t23-campaign-home @42fa456 | 설계 문서 병합 후 | 0 |
| kd-develop/kd-roadmap-review2 | docs/roadmap-review2 @af4f7ea | docs 병합 후 | 0 |
| kd-develop/kd-roadmap-sync | docs/roadmap-sync @007d49b | docs 병합 후 | 0 |
| kd-develop/kd-monitor-launch | tools/monitor-launch @75a1712 | tools 병합 후 | 0 |
| kd-develop/kd-endline50 | kd/endline-50 @476ceec | T31b endline 병합 후 | 1 (확인) |
| kd-develop/kd-t39i | t39i-ruined-houses @66d6b95 | T39i 병합 후 | 0 |
| kd-develop/kd-crash-log | kd/crash-log @69320b1 | T41 병합 후 | 0 |
| kd-develop/kd-isai-restore | kd/isai-restore @5c08a38 | T35 병합 후 | 0 |
| kd-develop/kd-t43 | kd/t43-branch-symbols @d45b18f | T43 병합 후 | 0 |
| kd-develop/kd-t45 | kd/t45-menu-hide @88735b1 | T45 병합 후 | 0 |
| kd-develop/kd-t46 | kd/t46-card-overflow @54e8a54 | T38c(=T46) 병합 후 | 0 |
| kd-develop/kd-t27 | t27-cmd-ui2 @4cf5c45 | T27 병합 후 | 0 |
| kd-develop/kd-sfxnew | sfxnew @aaba414 | T21 병합 후 | 0 |
| gt-develop/gt-face-proto | gt-face-proto @bc52007 | T22 병합 후 | 0 |

제거 명령 (B, 병합 확인 후 실행):
```
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-fvrenumber"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-gemini-import"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-rail"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t08"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t09"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t23"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-roadmap-review2"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-roadmap-sync"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-monitor-launch"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-endline50"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t39i"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-crash-log"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-isai-restore"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t43"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t45"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t46"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-t27"
git -C C:/gameProject worktree remove "C:/gameProject/kd-develop/kd-sfxnew"
git -C C:/gameProject worktree remove "C:/gameProject/gt-develop/gt-face-proto"
```

## C. 유지 (작업 중 · 미병합 WIP · 스택 기반 · 밸런스 보류 · 체크아웃 불일치)

| 워크트리 | 브랜치 @tip | 유지 이유 |
|---|---|---|
| kd-develop/kd-ai3b2 | ai3b2 @f74a675 | 스택 기반 (T04 위, T24·T25·T31·T32·T34 의 뿌리) |
| kd-develop/kd-breach2 | wip/breach2 @6ad6c50 | 스택 기반 (T04·T10·T16·T38c) |
| kd-develop/kd-orderq2 | wip/orderq2 @0377f8a | T04 미완 (확인 필요) |
| kd-develop/kd-t10 | kd/t10-comms-b @1c91c96 | T10 확인 필요 |
| kd-develop/kd-t10b | kd/t10b-difficulty @4d74ce9 | wip-ready, T10 위 스택 |
| kd-develop/kd-t16 | kd/t16-trench @2cb118c | 밸런스 보류 (재개 여지) |
| kd-develop/kd-t17 | t17-cmd-convenience @0eb92e8 | wip-ready 확인 필요 |
| kd-develop/kd-t19a · kd-t19b · kd-t19b2 · kd-t19c · kd-t19d | t19a..t19d @43e69b1 · 622db02 · 4980e7b · 9260e36 · 04a9149 | T19 스택 (wip-ready, 병합 순서 대기) |
| kd-develop/kd-t24 | kd/acting-delay-tier @7cfa0c2 | T37 작업 장소 (이름과 체크아웃 불일치, 이름 정정 필요) |
| kd-develop/kd-t24-crossing-fix | kd/t24-crossing-fix @390763a | 미병합 수정 (도하 관측반), 병합 때 t24-s1s3 뒤 |
| kd-develop/kd-t25 | kd/t25-bayonet-all @29695c3 | 확인 필요 |
| kd-develop/kd-t28 | t28-cover-forest @c577c0f | wip-ready 옵션 기능 |
| kd-develop/kd-t29b | t29b-on-t09 @a89b4d2 | 대체 카드 (T29b) |
| kd-develop/kd-t30a · kd-t30c · kd-t30e | t30a..t30e @863f9d1 · 3de60d5 · 7f46a4c | 열차 체인 (T30b·T40 기반) |
| kd-develop/kd-t30b | kd/immune-target-fix @48bd6ce | T36 작업 장소 (이름과 체크아웃 불일치) |
| kd-develop/kd-t30d-train-art | t30d-train-art @8e51e5a | 열차 그림 WIP |
| kd-develop/kd-trains-annih | kd/trains-in-annihilation @2de288d | T40 wip-ready |
| kd-develop/kd-t31a | kd/t31a-regroup @97ed026 | 밸런스 보류 (켜지 말 것) |
| kd-develop/kd-t31c-surrender | kd/t31c-surrender @e9cd527 | 0.4 AI 항복 |
| kd-develop/kd-t32a · kd-t32b · kd-t32d | kd/t32a-pioneer @26cc9fd · t32b-ai-smoke @33284fa · t32d-pioneer-art @2118f9b | t32d 는 총구 수정 재렌더 진행 중 |
| kd-develop/kd-t33a-map-model · kd-t33b-map-ui · kd-t33c-placement · kd-t33d-rail | kd/t33a..t33d @c5b30f4 · 5207b8f · c514821 · c514821 | 지도 편집기 스택 (t33c 진행 중). t33b·t33c·t33d 는 tip 이 겹침 |
| kd-develop/kd-t34a | kd/t34a-suppress @3d0f4de | wip-ready, AI 0.4 |
| kd-develop/kd-t38a · kd-t38b · kd-t38f · kd-t38g | t38a..t38g @7274c71 · 93ce79f · b622d1d · f5a76d4 | UI 스택 (T42a 기반 = t38f) |
| kd-develop/kd-t38e | t38e-unitlist-codex @7274c71 | 미커밋 10건 (dirty 10). 커밋 전 삭제 금지 |
| kd-develop/kd-t38h | t38h-fuzz-campaign @e5f76ec | 미커밋 4건 (dirty 4), 상태 확인 필요 |
| kd-develop/kd-t42a · kd-t42b · kd-t42c · kd-t42d | t42a..t42d @ba621fe · 51b92c3 · 66e7637 · 06ee9bd | 캠페인 체인 (병합 순서 대기) |
| kd-develop/kd-t44 | kd/t44-smoke @1c4036d | 발주 중, 미커밋 3건 (dirty 3) |
| kd-develop/kd-rerender | kd-rerender @467dafc | T11 진행 중 (dirty 2). T39e~j 의 기반 |
| kd-develop/kd-river-r1 · kd-river-r2 · kd-river-r3 · kd-river-r4 | river-r1..r4 @5b7b9f1 · 8ada7a9 · 26df7f6 · 9d4b7be | 하천 스택 (rail → r1 → r2 → r3 → r4) |
| kd-develop/kd-t33c-placement / kd-t33d-rail | (위 표 참조) | 진행 중 |

## 주의 (정리 전 확인)

- 워크트리 체크아웃 불일치 2건: `kd-t24` 는 kd/acting-delay-tier(T37), `kd-t30b` 는 kd/immune-target-fix(T36). 이름을 맞추는 작업은 이 표 밖(사용자 결정). 지금은 이름 그대로 두고 **유지**한다.
- B 조건에 있는 브랜치를 병합하기 전에 지우면, 그 워크트리에서만 쓰던 임시 파일이 사라진다. 병합 후 실행.
- 워크트리를 지워도 브랜치는 남는다. 브랜치 삭제(`git branch -d`)는 이 표에 없다.
