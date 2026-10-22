# T39d 하천 4 (선택): Bridge 지형 + 다리 렌더
- id: T39d | 역할: feature | 등급: D中 H1 (R1 S Wlight)
- 선행: T39a T39c | 후행: -   **선택 카드**: 지휘자가 사용자 확인 뒤에만 발주(권장 답은 "우선 흙 덮기 유지, 필요 시 추가" 이므로 기본은 보류/미발주).
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: Terrain.cs(enum 끝 `Bridge`, Ford 다음), Snapshot.FormatVersion(+1, FV 예약 필요 — 이 카드가 만들면 지휘자가 행 추가), MoveMul/TerrainCover(Bridge x1, 엄폐 없음, walkable), 렌더(널빤지 방향 2종: 가로/세로 폭 따라), 시험 지도에 다리 교체
- 브랜치: `river-r4` (base `river-r3` 병합 후 또는 스택) | 상태: queued (선택) | 담당: -
## 확정 결정
- 없음. 다리 = 현행 "흙 덮기" 관례(c0_02_yalu)는 변경하지 않는다.
## 범위 밖
다리 파괴 · 가설 · 철교 · 기존 c0_02_yalu 변경(골든 보존)
## 수용 기준
Bridge 통행 x1 · 사선 불막음 · 저장/로드 일치 · 기존 지도 골든 불변 · 접촉 시트(river_terrain.png 갱신) · FindCrossing 이 Bridge 를 건널 곳으로 잡음(단위 시험).
## 측정
terrain + crossing selftest, detcheck, snaptest.
## 가정한 답
Q2 의 반대 분기(별도 Bridge 필요)가 확정된 경우에만 실행; 아니면 closed 처리.
## 결과
-
