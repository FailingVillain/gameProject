# T38m 머리 위 표시 정리 (3단계 Overlay)
상태: wip-ready (t38m-overlay-declutter, t38a-settings-keys 위 스택, 병합 안 함)

## 원인 (사용자: 유닛 머리 위 파랑/빨강 표시가 지저분)
교전 테스트 재현: 사용자 settings.json 에 `ColorBlind: true` (T27 ⑧) 이면 모든 유닛 머리 위에 "흰 테두리 + 세력색 도형" (1 = 원 · 2 = 사각 · 3 = 삼각 · 4 = 마름모 · 5 = 십자) 이 상시 찍힌다 (UnitRenderer 6 앞, ColorBlind.Draw). 파랑 = 아군(1), 빨강 = 적(2). 그 밖에 상시 막대(탄약 노랑 · 적재량 파랑, 부족 시 빨강) 와 분대 아이콘도 겹침.
전후 화면: docs/art_review/t38m_before_colorblind_z*.png, t38m_after_{concise,normal,detailed}_z{0.8,1.5}.png, t38m_settings.png (1280x720 창).

## 결과
- `UserSettings.Overlay` (간결/보통/상세, 기본 간결, settings.json "Overlay"), 환경 설정 행 + `U` 키 순환 (Bindings.Extra, CONTROLS.md).
- 규칙 = `src/Render/OverlayRules.cs` 순수 함수. 간결: 체력 = 선택 · 호버 · 피격, 탄약/적재량 = 선택한 아군만, 색각 도형 = 선택 · 호버만, 분대 아이콘 = 선택 분대 또는 위험(고정 · 제압 · 후퇴 · 탄약 없음/부족). 보통 = 예전. 상세 = 아군 체력 · 탄약 · 적재량 상시.
- 색은 Palette 토큰 (AmmoBar · CargoBar · BarLow). 렌더 전용 (시뮬 · 체크섬 · FormatVersion 무관).
- 검증: uiicons 묶음 PASS (T38m 18건), uifuzz 30 + --fuzz-menu 예외 0, detcheck GOLDEN 일치.
- 미구현 (제안): 중대 부대 기호(UnitSymbols) 대대 묶기 · 개수 상한 — 교전 테스트에는 중대가 없어 원인이 아님.
