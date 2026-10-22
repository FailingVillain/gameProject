# T38g UI 회귀 하네스: uifuzz 작은 버튼 클릭 + 화면x3해상도 스크린샷 시트
- id: T38g | 역할: feature (도구) | 등급: D中 H0 (R1 S<=100k Wlight)
- 선행(blockedBy): 없음(도구만; T38a/T38d 이후 재사용). 병합 전 선행 = tools/monitor-launch 의 run_game.ps1 | 후행: -
- 핫스팟: src/Tests/UiFuzz.cs · tools/agent/run_game.ps1(별도 브랜치 tools/monitor-launch) · tools/ui_sheet.ps1(신규) · 게임 코드 변경 없음(필요 시 --uishot 인자 1개)
- 브랜치: ui/t38g-harness (base = main + tools/monitor-launch 의 run_game.ps1 가져오기; UiFuzz 는 t27 에도 있으나 main 기준 확인 필요) | 상태: wip-ready (NO-MERGE 스택: 브랜치 t38g-ui-harness, base = t27-cmd-ui2 4cf5c45; 병합 시 t27 -> t38g 순)
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
구현: (1) `Ui.RegHit` 등록부 (퍼즈가 켤 때만 기록; 메인/도감/캠페인/미션 Button, 칩, 체크박스, Ui.Button, HUD 카드 칸 · 탭 · 쪽 넘김) -> UiFuzz 가 직전 프레임 사각형을 모아 클릭의 30% 는 작은 것(가로세로 <=40px), 20% 는 아무 등록 버튼의 중앙을 정확히 겨냥. 종료 보고에 서로 다른 버튼 수 · 작은 버튼 목록. (2) `--size WxH` (저장 안 함, 최소 1024x640). (3) `tools/ui_sheet.ps1` + `tools/ui_sheet.py`: 11 메뉴 화면 + HUD 3 x 3해상도 -> `docs/art_review/ui_sheet_<WxH>.png` 3장 + `ui_sheet_all.png`; 실행은 `tools/agent/run_game.ps1` (보조 모니터; tools/monitor-launch 75a1712 에서 복사). 실패한 화면은 `name @ 해상도`로 보고, 종료 코드 3.
검증: 빌드 OK, KD_SELFTEST_ONLY=uiicons PASS, `--uifuzz 30` 예외 0 / `--uifuzz 30 --fuzz-menu` x {1024x640, 1280x720, 1920x1080} 예외 0 (서로 다른 버튼 37/38/46개, 작은 버튼 7~8종 [- + < > ▶ 자동회전 정지 체크박스] 16~31회 클릭), detcheck 8변형 동일 GOLDEN 일치, 시뮬/FormatVersion 무변경.
열린 질문 권장안 채택: Q-a 작음 기준 = max(가로,세로)<=40px / Q-b 예약 비율 = 작은 30% + 아무 20% / Q-c 시트 = 해상도별 1장 + 전체 개관 1장 (4열, 타일 폭 480) / Q-d 크기 지정은 `--size` 인자 1개 (settings.json 불변) / Q-e 목록 = 현재 존재하는 화면만 (캠페인 홈 15탭 · 전과 보고는 t19c / t17 에만 있어 이 베이스엔 없음 -> 병합 뒤 ui_sheet.ps1 의 $menuPages 에 이름 추가).
알려진 한계: 보조 모니터가 1280x720 이라 큰 창은 화면 밖으로 나가지만 프레임버퍼 캡처는 지정 크기 그대로 (시트에서 실제 크기 확인됨). 1024x640 에서 메인 메뉴 하단 버튼 · 교전 테스트 목록이 잘리는 것이 시트로 드러남 (T38d 소해상도 감사 대상).
