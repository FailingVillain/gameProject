# 사용자 결정 · 권한 대기 (에이전트가 한 줄씩 추가 → 지휘자가 ops/DECISIONS.md 로 옮기며 ID 부여)
> 형식: `- [작업/에이전트] 결정/권한이 필요한 것 — 선택지/권장`. 옮긴 줄은 지휘자가 지운다.
> 2026-10-22 이전 항목은 전부 ops/DECISIONS.md D01~D22 로 이관됨 (원문 = docs/legacy/DECISIONS_PENDING_ARCHIVE_2026-10-22.md).


- [MERGE_PLAN 2026-10-22] 병합 계획서(ops/MERGE_PLAN.md) 확인 요청 — 병합 개시 전 최종 확인(AT). 권장: 승인.
- [MERGE_PLAN] 골든: T36(immune-target-fix)·T37(acting-delay-tier)가 둘 다 자체 골든이 바뀌는 브랜치라 리허설 값 bef5ffb0 822cdacd 와 다른 값이 나올 수 있음 — 최종 값 확인 후 재생성(사용자 OK). 권장: 그룹 H 뒤 1회.
- [MERGE_PLAN] t38h-fuzz-campaign(e5f76ec)은 t38g 커밋의 중복이라 병합 제외 — 캠페인 화면 퍼즈 커버(T42c 보고 갭)는 별도 카드로 남음. 권장: 제외 유지.
- [MERGE_PLAN] AE2(62/25) 번호는 미발주라 비워 둠(river 63 · 다리 64 · 연막 65) — 나중에 AE2 를 발주하면 그때 번호를 새로 정함(저장 형식 호환 영향 없음). 권장: 비워 둔 채 진행.
- [MERGE_PLAN] 병합 대상에서 wip/casualty2(리허설은 합침, T06 폐기)·kd/t16-trench(기준 미달, Z1 변경 유지)의 취급: 계획서는 casualty2 제외, t16 포함. 권장: 그대로.
- [MERGE_PLAN] 지도 편집기 지형 목록에 Bridge 가 없으면(T39d 메모) 병합 때 고치지 않고 T33 후속 카드로 — 권장: 카드로.
