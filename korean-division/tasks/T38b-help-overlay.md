# T38b 게임 내 도움말/단축키 오버레이 (Bindings 단일 출처 + CONTROLS 동기 테스트)
- id: T38b | 역할: feature | 등급: D中 H1 (R1 S<=60k Wlight)
- 선행(blockedBy): T27 wip-ready 브랜치(t27-cmd-ui2) | 후행: T38a
- 핫스팟: UI/Bindings.cs · Hud.cs(오버레이 1개) · Game.HandleGlobalKeys(키 1개) · Strings.cs · docs/CONTROLS.md · Tests(CommandSelfTest 또는 uiicons)
- 브랜치: ui/t38b-help-overlay (base = t27-cmd-ui2) | 상태: queued | 담당: -
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
-
