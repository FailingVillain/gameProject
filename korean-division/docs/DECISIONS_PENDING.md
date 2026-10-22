# 사용자 결정 · 권한 대기 (에이전트가 한 줄씩 추가 → 지휘자가 ops/DECISIONS.md 로 옮기며 ID 부여)
> 형식: `- [작업/에이전트] 결정/권한이 필요한 것 — 선택지/권장`. 옮긴 줄은 지휘자가 지운다.
> 2026-10-22 이전 항목은 전부 ops/DECISIONS.md D01~D22 로 이관됨 (원문 = docs/legacy/DECISIONS_PENDING_ARCHIVE_2026-10-22.md).

- [ai3b2/AI 3B] 결정: 3B 합격 지표 '방어 중대 간 24칸 안 ≥50%' 는 90명 중대 정면 폭(≈30칸)상 거의 불가 (C1/C2 기본 34~42%) — 40칸 안 비율로 바꿀지, 값 유지할지. 또 pincerShare 0.5→0.65 (협공 고정 몫 축소) 는 측면 계획으로 바뀌는 효과라 기본 유지 제안.
- [T34a 3C-1] 결정: 제압 문턱 superioritySupp·boundSuppression·(틀)chargeSuppression 의 엔진 틀(TacticDrill) 쪽은 세력별 겹침으로 못 바꿈(시뮬은 AI 프로필 금지) — 측정에서 '켬 권고'가 나오면 tactics.json 전역 값 변경(골든 바뀜)으로 할지. 겹침 값(0.30/0.6/0.45/0.6, pinned 0.25, pivot 0.4)은 초안, 측정·튜닝 보류(밸런스 후순위).
