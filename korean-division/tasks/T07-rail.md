# T07 철도 지형 (wip/rail, 장갑열차 제외)
- id: T07 | 역할: feature(복원·검증) → merger | 등급: D上 H2 (선택 메모: R2 SM Wlight)
- 선행: 없음 (T02 병합됨 8d22ad5) | 후행: 장갑열차(보류)
- 핫스팟/만질 파일: Terrain, MapFile, RailRenderer(신규), BuildingRenderer, Snapshot, TerrainSelfTest
- 브랜치: main 에서 새 브랜치 `rail` 에 `docs/legacy/wip_patches/rail.patch` 복원(wip/rail 은 원본 스냅샷, 그대로 병합 금지) | 상태: queued | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "철도 지형은 추가를 하는걸로 하자" (사용자, 날짜 확인 필요)
- 1차는 선로 = 지형; 선로 파괴는 나중
## 범위 밖
장갑열차·화물열차·하천·선로 파괴
## 수용 기준
빌드·시험 통과; 길찾기/시야 회귀 없음
## 측정
지도 시험 + snaptest
## 결과 (에이전트가 끝에 3~5줄만)
-
