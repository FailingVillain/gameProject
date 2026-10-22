# T55s 민가 선택 로직 (양식 · 반전 · 색조)
- id: T55s | 역할: feature | 등급: D中 H1 (BuildingRenderer HouseBlocks LodSprite Theme MissionDef 접촉, 렌더 전용)
- 선행: - (기존 4종으로 스텁) | 후행: T55m
- 만질 파일: src/Render/BuildingRenderer.cs HouseBlocks.cs LodSprite.cs(CellFit mirror/tint) Theme.cs MissionDef(houseStyle 문자열) assets/data/house_styles.json
- 브랜치: t55s-house-select (기반 t39i-ruined-houses 66d6b95, NO-MERGE) | 상태: wip-ready
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
구현: HouseStyles.cs(선택/배정/예산) + house_styles.json(8 클래스·42 키 등록·korean/manchu/mixed·색조 3단계) + CellFit/Cell mirror·plain + MapFile/MissionDef houseStyle(ConditionalWeakTable, 스냅샷 밖) + HouseStyleSelfTest(KD_SELFTEST_ONLY=housestyle, terrain 묶음에도 포함). houseStyle 생략 = 예전 4종 경로 그대로.
검증: 빌드 0 오류, housestyle/terrain/props/uiicons PASS, uifuzz 30 오류 0, detcheck GOLDEN match. lodaudit 2건 FAIL(wave_flag UsedAnimNames)은 기반 66d6b95 에서도 동일 (이 카드 무관).
발견: 팀 셰이더는 UV 만 샘플하므로 음수 폭 반전은 정상이나, 불투명 픽셀에 정점 색을 안 곱해 색조가 안 먹는다 -> 민가는 plain(기본 셰이더) 경로로 그림 (민가는 팀색 픽셀 없음).
미측정: LodAtlas 업로드 시간(새 그림 없음, 스텁), 창 캡처 (새 키 그림 미임포트). 예산 함수 추정: 한국 60MB / 만주 72MB / 혼합+폐허 216MB.
한계: 스냅샷 로드한 미션은 미션 houseStyle 을 잃고 지도 값/예전 4종 (렌더 전용이라 의도적).
