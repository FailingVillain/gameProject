# T55s 민가 선택 로직 (양식 · 반전 · 색조)
- id: T55s | 역할: feature | 등급: D中 H1 (BuildingRenderer HouseBlocks LodSprite Theme MissionDef 접촉, 렌더 전용)
- 선행: - (기존 4종으로 스텁) | 후행: T55m
- 만질 파일: src/Render/BuildingRenderer.cs HouseBlocks.cs LodSprite.cs(CellFit mirror/tint) Theme.cs MissionDef(houseStyle 문자열) assets/data/house_styles.json
- 브랜치: kd-develop/kd-t55s | 상태: queued
## 확정 결정
렌더 전용, 결정적 해시, 체크섬/스냅샷/세이브 불변, Snapshot.FormatVersion 불변. houseStyle 생략 = 기존 동작.
## 범위 밖
구역별 혼합 · 새 지형 · 소품
## 수용 기준
- 좌표 해시 + 이웃 반복 회피, 같은 지도 같은 결과 (selftest 한 건: 두 번 계산 일치).
- 반전: CellFit 음수 src 폭, 아틀라스 항목 증가 0.
- 기존 selftest/detcheck 골든 불변. 창 캡처 1장 (run_game.ps1).
## 측정
LodAtlas 메모리·업로드 시간 (새 구역 진입) 보고. 기준선 docs/BASELINE_CURRENT.md
## 결과
