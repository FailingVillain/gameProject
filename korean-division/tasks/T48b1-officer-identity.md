# T48b1 장교 신원 키
- T48b1 | sim | D: 上 · H: 2
- 선행: t48a-commander-data | 후행: t48b2-assign
- 브랜치 t48b1-officer-identity (워크트리 kd-develop/kd-t48b1) | 상태: wip-ready (fa8dff1 + 호환 샘플 커밋)
## 병합 메모 (지휘자 2026-10-22, 연대장·T49 체인 리뷰 차단 1·2)
- **FV 재번호**: 이 브랜치의 Snapshot.FormatVersion 53 은 breach2(53)와 충돌한다. 병합 때 **67 로 재번호**한다 (ops/FV_RESERVATIONS.md 에 예약됨, 이 카드에서는 그 파일을 수정하지 않음). 관련 시험 기대값(SnapshotSelfTest 등)도 같이 올린다.
- 호환 게이트(T49c): `tests/fixtures/campaign/campaign_v9.json` 추가됨 (스키마 v9, gen_campaign_fixtures.py 로 생성). t49bc 와 합치면 `compatgate` 통과 확인.
- 병합 순서: t42d → t48a → t48c → t49a → t49bc → t48b1 (마지막).
