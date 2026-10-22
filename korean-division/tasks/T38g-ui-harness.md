# T38g UI 회귀 하네스: uifuzz 작은 버튼 클릭 + 화면x3해상도 스크린샷 시트
- id: T38g | 역할: feature (도구) | 등급: D中 H0 (R1 S<=100k Wlight)
- 선행(blockedBy): 없음(도구만; T38a/T38d 이후 재사용). 병합 전 선행 = tools/monitor-launch 의 run_game.ps1 | 후행: -
- 핫스팟: src/Tests/UiFuzz.cs · tools/agent/run_game.ps1(별도 브랜치 tools/monitor-launch) · tools/ui_sheet.ps1(신규) · 게임 코드 변경 없음(필요 시 --uishot 인자 1개)
- 브랜치: ui/t38g-harness (base = main + tools/monitor-launch 의 run_game.ps1 가져오기; UiFuzz 는 t27 에도 있으나 main 기준 확인 필요) | 상태: queued
## 이미 있음
--uifuzz [초]: 창을 띄워 마우스를 화면 전체에 무작위 이동·클릭. 알려진 한계 = 작은 버튼([-][+] 등)을 거의 못 맞힘(T19c 결과). run_game.ps1 = 보조 모니터로 창 이동(브랜치 tools/monitor-launch).
## 확정 결정
- 보조 모니터 규칙: 게임 창은 보조 모니터에 띄운다 (run_game.ps1 헤더 - 사용자 규칙; 원문 날짜는 ops/LESSONS 에서 확인 필요).
## 범위 밖
렌더 품질 판정 · 시뮬 검증 · CI 연동.
## 수용 기준
1) 퍼즈가 현재 화면의 클릭 가능 사각형(수집 방법은 구현 시 결정 - 코드 확인 필요)을 수집해 작은 것(<=40px)도 비율 예약 클릭(예 30%). 2) 화면 열거(메인/설정/도감/미션/캠페인 홈 15탭/HUD/전과 보고) x {1024x640, 1280x720, 1920x1080} 자동 캡처 -> 한 장 시트(contact sheet). 3) 실패 시 크래시 지점 보고, 예외 0 기준. 4) heavy_gate(W) 준수, 동시 2개 이하.
## 측정
--uifuzz 30 x 해상도 3, 시트 산출물 docs/art_review/ui_sheet_*.png (사용자 검수).
## 결과
-
