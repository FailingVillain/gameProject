## T52a 교전 알림 이력 + 전보 띠 (UI 전용)
- `src/UI/AlertLog.cs`: Game.Alerts. `Feed(kind,pos,now)` 순수 함수(16칸·8초 병합, 8건, 효과음 전체 8초·공지 종류별 8초), `Poll(world,me,now)` 가 World.LastAttacked · Building.Contested(false->true, 아군 점령 시설)를 읽기만 함. 월드가 바뀌면 이력 초기화. 시뮬·저장·리플레이 불변(detcheck 골든 일치).
- 띠: Hud.DrawAlertBand / AlertAt (클릭 = Camera.CenterOn), Backspace = 최근 알림 이동 (Bindings.Extra + CONTROLS.md). 설정 `AlertBand`(settings.json, 기본 true) + 설정 메뉴 단추.
- 확인용 환경변수 `KD_ALERTDEMO=1` = 가짜 알림 3건 (스크린샷 전용). 시험 = KD_SELFTEST_ONLY=uiicons 의 [교전 알림 이력].
