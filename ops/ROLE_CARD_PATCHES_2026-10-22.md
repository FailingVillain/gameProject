# ROLE_CARD_PATCHES 2026-10-22 (감독관 지적 반영안; 적용은 지휘자. 홈 디렉터리 파일은 이 작업에서 수정하지 않음)
근거: PIPELINE_AUDIT B-5(꼬리 한도 모순) · A-2(교훈 흡수) · 사용자 상시 규칙(임의 결정 보고). 리포 쪽(AGENT_RULES.md · merge-procedure.md)은 이미 pipeline-v2 브랜치에서 수정됨.

## 1. ~/.claude/agent-lessons/common.md (한도 갱신 + 공통 한 줄 추가)
```diff
-- 2026-10-21 all: 전체 selftest 는 10분 넘음 — 처음부터 run_in_background 로 돌리고 until 루프(Monitor/Bash)로 대기 (sleep 직접 대기는 차단됨).
+- 2026-10-22 all: 전체 selftest 는 selftest_parallel(≈268초, 병합 뒤)·그 전엔 10분+ — run_in_background + until 루프(Monitor/Bash)로 대기 (sleep 직접 대기는 차단됨). 전체 selftest 는 release 담당.
+- 2026-10-22 all: 자기 브랜치에서 ops/ (QUEUE·STATE·MERGE_QUEUE 포함)를 수정하지 않는다(queue_guard.ps1). DECISIONS_PENDING·ENGINE_NOTES 는 조각 파일(docs/decisions_pending.d/, docs/engine_notes.d/)에 쓴다.
+- 2026-10-22 all: 웹 조회는 `python C:/works/agentHub/hub.py ask gemini-flash -w` 로만.
+- 2026-10-22 all: 한도 숫자(동시 에이전트·heavy)는 프롬프트의 값을 쓰지 말고 ops/LIMITS.md 를 따른다.
```

## 2. 보고 길이·임의 결정 항목 (feature · sim · art · docs · measure · spec · scout · release 카드 끝줄)
```diff
 feature.md
-Report (Korean, <=8 lines): chosen values + one-line reasons, tests, commits, wip-ready.
+Report (Korean, <=8 lines): chosen values + one-line reasons, tests, commits, wip-ready. Last line "임의 결정:" = every value/assumption/interpretation/deletion/relaxation you chose that the card did not specify (or "없음").
 sim.md
-Report (Korean, <=8 lines): root cause, fix, numbers, tests, branch/commit + wip-ready. Optional "교훈 제안" <=3 lines.
+Report (Korean, <=8 lines): root cause, fix, numbers, tests, branch/commit + wip-ready. Last line "임의 결정:" (same rule, or "없음"). Optional "교훈 제안" <=3 lines.
 reviewer.md
-Report (Korean, <=12 lines): confirmed findings ... verdict (merge OK / fix first). ...
+Report (Korean, <=12 lines): confirmed findings ... verdict (merge OK / fix first). Also answer the 3 fixed checks of PIPELINE §1-c and add "임의 결정:" if you assumed anything. ...
 merger.md
-Report (Korean, <=8 lines): per branch merged hash or reason not merged.
+Report (Korean, <=8 lines): per branch merged hash or reason not merged. Last line "임의 결정:" (conflict resolutions chosen, or "없음").
```
art / docs / measure / spec / scout / release 도 같은 "Last line 임의 결정:" 문구를 각 Report 줄 끝에 추가.

## 3. 도구 사용 (merger.md · feature.md · sim.md)
```diff
 merger.md (Per branch ...)
-Per branch: merge main in (once); hotspot rules (...); build; --d...
+Per branch: use tools/agent/merge_step.ps1 only (build + detcheck + snapshot/--snaptest enforced; do not hand-expand the steps). First check each branch tip hash against the MERGE_QUEUE row (stop and report if it moved). FV/Replay line conflicts: git checkout --ours the two files, then fv_assign.ps1 -Apply (do not pick numbers by hand). Run queue_guard.ps1 on the branch; ops/QUEUE·STATE edits from work branches -> 'ours' + warning.
 feature.md / sim.md (Procedure:)
+Before commit: queue_guard.ps1 (no ops/ edits). If you raise FV/Replay: temp value = base+1 and fv_claim.ps1 (never FV_RESERVATIONS.md). DECISIONS_PENDING / ENGINE_NOTES lines go to docs/decisions_pending.d/<branch>.md and docs/engine_notes.d/<branch>.md.
```

## 4. 한도 모순 (모든 카드·프롬프트 꼬리)
- 카드·꼬리의 "동시 6 · heavy 2" 류 숫자를 삭제하고 "한도 = ops/LIMITS.md" 로 교체 (에이전트가 직접 쓰는 수는 '내 heavy 는 한 번에 1개, heavy_gate 통과'뿐).
- ~/.claude/agents/*.md 에 한도 숫자가 남아 있으면 같은 방식으로 삭제. (현재 grep 결과 해당 숫자는 카드 본문에 없음 — 모순은 C1E1 웨이브 1 프롬프트 꼬리에서 발생.)

## 5. reviewer.md (checkout 금지 명시)
```diff
-Review agent, C:\gameProject\korean-division. Never edit files or merge.
+Review agent, C:\gameProject\korean-division. Never edit files, merge, or `git checkout` (read via git show / git diff only).
```

## 6. 메모리 (지휘자 몫)
"기본 최대 6 / opus ≤2" 메모리와 LIMITS(30/8) 차이는 사용자 확인 필요 — PIPELINE.md '미결정' 절 참조.
