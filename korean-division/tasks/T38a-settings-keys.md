# T38a 설정 메뉴 완성: 단축키 재지정 (+ 전투 중 설정 진입)
- id: T38a | 역할: feature | 등급: D中 H1 (R2 S<=100k Wlight)
- 선행(blockedBy): T38b (도움말이 Bindings 단일 출처를 먼저 세운 뒤) | 후행: T38g
- 핫스팟: UI/UserSettings.cs(키 맵 저장) · UI/Bindings.cs · Core/Commands.cs(Hotkey 읽기만) · Game.HandleKeyboard/HandleGlobalKeys · MainMenu.DrawSettings · Strings.cs · CONTROLS.md. 시뮬/저장 형식 무변경(FV 불변)
- 브랜치: ui/t38a-settings-keys (base = ui/t38b-help-overlay) | 상태: queued | 담당: -
## 이미 있음 (코드 확인 t27-cmd-ui2)
해상도 5종 · 창 모드 3종 · 음량 · 색각 대응 = MainMenu.DrawSettings + UserSettings(settings.json). 없는 것 = 단축키 재지정, 전투 중 설정 진입.
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- ROADMAP_LONG 단기 로드맵 3 (2026-10-21 이관): "설정 메뉴 (해상도 · 창 모드 · 음량 · 단축키 재지정)". 재지정 범위·충돌 규칙은 확인 필요 (UI_BACKLOG Q1).
## 범위 밖
해상도/모드/음량 UI 재작업 · 숫자키 연대/대대 키 설계(T08 결정 9) · 저장 형식 호환 정책 · 충돌 로그.
## 수용 기준
1) 카드 키(Commands.UnitCommands)와 Extra 키 중 재지정 대상 목록이 Bindings 한 곳에서 생성. 2) 재지정 -> settings.json 왕복 -> 재시작 후 유지. 3) 키 충돌 거부(표시) + 기본값 복원 버튼. 4) 도움말/카드 라벨/CONTROLS 생성물이 새 키를 반영. 5) build 오류 0 · uifuzz 30 예외 0 · detcheck 불변.
## 측정
KD_SELFTEST_ONLY=<ui/command 그룹> (+키 맵 왕복 테스트 신규), --uifuzz 30, detcheck. 사용자 검수: 설정 화면 스크린샷 시트(1280x720).
## 결과
-
