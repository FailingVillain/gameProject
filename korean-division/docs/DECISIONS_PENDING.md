# 사용자 결정 · 권한 대기 (에이전트가 한 줄씩 추가 → 지휘자가 ops/DECISIONS.md 로 옮기며 ID 부여)
> 형식: `- [작업/에이전트] 결정/권한이 필요한 것 — 선택지/권장`. 옮긴 줄은 지휘자가 지운다.
> 2026-10-22 이전 항목은 전부 ops/DECISIONS.md D01~D22 로 이관됨 (원문 = docs/legacy/DECISIONS_PENDING_ARCHIVE_2026-10-22.md).

- [C1E1-g1/sim] SK 실패 확정 시 일수 소모 여부 — 명세에 없음. 기본 0 (`failDays`, 노드별 JSON). 1일로 할지 확인.
- [C1E1-g1/sim] 에피소드 미션 승리 시 CampaignState.Day 는 지금 missionDays 고정으로 오른다 — 노드 days(BT1 1 · OP1 2/4)로 맞추는 연동은 g2/연동 카드에서. 권장: 에피소드 중에는 노드 days 사용.
- [C1E1-g1/d1] D08 이 붙는 SK 미지정 (c1e1.json 에 D08 위치 없음) — d1 이 흐름요약 §7 표대로 decisions 에 추가.
