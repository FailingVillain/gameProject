# T55m 민가 import · 테마 키 · 지도 데이터
- id: T55m | 역할: art/docs | 등급: D下 H0
- 선행: T55s 필드 정의 | 후행: T55a T55b import
- 만질 파일: tools/import_kd1920_buildings.py(글롭/--only) assets/themes/kd1920/theme.json(키 규칙) assets/missions|maps 샘플 1~2개의 houseStyle
- 브랜치: t55m-house-data (worktree kd-develop/kd-t55m-house-data, main 기준) | 상태: wip-ready
## 범위 밖
렌더 · 선택 로직 코드
## 수용 기준
- 다른 유닛 폴더를 지우지 않음 (--only). import_kd1920.py 뒤 재실행 규칙 유지, 멱등.
- theme.json 키 +66 이 ops/LIMITS.md 한도 안인지 확인.
## 결과
- (2026-10-22 Haiku) importer: `tools/import_kd1920_buildings.py` 에 민가 종 × 변형 import 추가 (`--only <id glob>`, `--dst`, `--standin` 시험 전용; standin 은 --dst 없이 거부). 키 `building_art.house.<style>.<id>.<tone>`, 렌더 폴더 `<id>-<tone>`, 그림 없으면 [없음] 경고 + 키 안 씀. 기존 4종 legacy 키는 유지 (BuildingRenderer 무변경). 시험: standin → 66 키, 레이아웃 파일 전부 존재 (scratch 에서만, 저장소 theme.json 미변경).
- (2026-10-22 Haiku) `assets/data/house_classes.json` 42종 (한국 21: 초가 6/기와 9/헛간 4/주막 2, 변형 30; 만주·중국 21: 토벽·벽돌 10/기와 5/토담 3/헛간 3, 변형 36). 발자국 3x3 16 · 3x4 14 · 4x4 8 · 3x5 4 (종별 배정은 Haiku 의 제안; 계획서엔 분배 수만 있음 → 리뷰 필요).
- (2026-10-22 Haiku) `housedata` selftest (`src/Tests/HouseDataSelfTest.cs`): 종/변형/발자국 수 검사 + orphan 키 = 오류, 그림 없는 변형 = 경고, 레이아웃 파일 없음 = 오류. 현재 결과 PASS (그림 없음 66 경고). orphan 주입 음성 시험 FAIL 확인.
- 검증: heavy_gate 통과 후 build 0 경고 0 오류 · `KD_SELFTEST_ONLY=housedata` PASS · `uiicons` PASS · `--detcheck` GOLDEN match (8aa6448b9690b922 / 262d0252c35087e2).
- 주의: 워크트리에서 importer 를 돌릴 땐 `--src C:/gameProject/game-tools/korean-division-image-out` 를 줘야 한다 (워크트리 안 game-tools 를 먼저 잡음).
