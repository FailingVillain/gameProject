---
id: Tnn                       # from card_id.ps1 only
title: <one line>
role: feature                 # sim|feature|art|docs|reviewer|measure|spec|scout
tier: mid                     # informational; model derived (up=opus, else sonnet) unless model: is set
grade: D H1                   # D and H required
branch: feat/xyz
base: merge/0.2-final         # hash filled by dispatch.ps1
blockedBy: []
hotspots: [src/File.cs]       # exact repo-relative files
fv: none                      # none | claim
read_first: [src/File.cs:Symbol()]
forbidden: []
report_lines: 8
gates: [build, detcheck]
---
# Tnn <제목>  (파일명 Tnn-<slug>.md, 예 T13-release.md, 카드 ≤1.5KB)
- id | 역할(sim/feature/art/docs/measure/merger/reviewer/release/scout/spec) | 등급: D(G/下/中/上)·H(핫스팟 0~3) 필수, R·S·W 는 선택 메모
- 선행(blockedBy) | 후행
- 핫스팟/만질 파일 (Snapshot · SelfTest · Defs · balance · Combat · Game · Replay 접촉)
- 브랜치/작업 폴더 | 상태(queued/assigned/running/wip-ready/merged/closed) | 담당 에이전트 id
## 확정 결정 (사용자 원문+날짜, 변경 금지)  - 불분명하면 '확인 필요'
## 범위 밖
## 수용 기준 (측정 가능)
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
시작 전: ops/MERGE_QUEUE.md 의 '병합 완료 표'를 grep 해 이미 구현됐는지 확인(조사·측정 카드).
