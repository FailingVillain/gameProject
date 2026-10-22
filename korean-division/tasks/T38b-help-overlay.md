# T38b 게임 내 도움말/단축키 오버레이 (Bindings 단일 출처 + CONTROLS 동기 테스트)
- id: T38b | 역할: feature | 등급: D中 H1 (R1 S<=60k Wlight)
- 선행(blockedBy): T27 wip-ready 브랜치(t27-cmd-ui2) | 후행: T38a
- 핫스팟: UI/Bindings.cs · Hud.cs(오버레이 1개) · Game.HandleGlobalKeys(키 1개) · Strings.cs · docs/CONTROLS.md · Tests(CommandSelfTest 또는 uiicons)
- 브랜치: t38b-help-overlay (base = t38g-ui-harness f5a76d4) | 상태: wip-ready (t38b-help-overlay, NO-MERGE 스택: t38g-ui-harness 위) | 담당: feature(sonnet)
## 이미 있음
콘솔 `help`/`keys` 만 있음 (Game.RunConsoleCommand). 게임 내 도움말 화면 없음. Bindings.All()이 카드 키(UnitCommands)+Extra 를 생성하나 Extra 는 손으로 쓴 문자열.
## 확정 결정
- ROADMAP_LONG 단기 로드맵 2 (2026-10-21 이관): "단축키 정리 (CONTROLS.md + 게임 내 도움말)".
## 범위 밖
튜토리얼 챕터(캠페인 기반) · 키 재지정(T38a) · 숫자=연대 키 변경.
## 수용 기준
1) 오버레이 키(후보 F1 은 구역 테두리와 충돌 - 확인 필요 Q2)로 표시/닫기, 모든 해상도에서 안 잘림(스크롤 가능). 2) 표시 내용 = Bindings.All() 만 사용(복붙 문구 없음). 3) 테스트: CONTROLS.md 의 키 표 vs Bindings.All() 누락/잉여 0 (불일치 시 실패). 4) 일시정지 중에도 열림.
## 측정
build · KD_SELFTEST_ONLY=<command 그룹> · uifuzz 30. 사용자 검수: 오버레이 시트 3해상도.
## 결과
F1 도움말 오버레이 (src/UI/HelpOverlay.cs): 내용 = Bindings.All() 뿐, 한 단/두 단 + 스크롤(휠 · ↑↓ · PgUp/PgDn · Home/End) + 닫기 버튼(퍼즈 등록). 일시정지·종료 화면에서도 열림, 열려 있는 동안 게임 입력 차단.
가정한 답 (카드 Q2 포함): (1) 키 = F1 (코드에 F1 키 용도 없음 - "F1 2차"는 작업 이름이라 충돌 아님). (2) 닫기 = F1 · Esc · 바깥 클릭 · 닫기 버튼. (3) 열어도 시뮬은 안 멈춤 (자동 일시정지 안 함 - 요청 밖 기능). (4) 콘솔 `helpui` 로도 열림 (스크린샷 하네스용), 콘솔 help/keys 는 그대로. (5) Bindings.Extra 에 F1 한 줄 추가. (6) 동기 테스트 = docs/CONTROLS.md §13 의 BINDINGS 표지 사이 표 vs Bindings.All() 줄 단위 비교 (누락/잉여 0), KD_SELFTEST_ONLY=uiicons. (7) UiFuzz 키 목록에 F1 추가. 시뮬/저장/FormatVersion 무변경.
검증: build 0 오류, uiicons PASS, uifuzz 30 예외 0 / --fuzz-menu 예외 0, detcheck 8변형 동일 GOLDEN 일치. 시트 docs/art_review/ui_sheet_help_overlay.png (1024x640 / 1280x720 / 1920x1080).
