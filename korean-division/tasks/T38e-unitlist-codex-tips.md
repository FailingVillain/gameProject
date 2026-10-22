# T38e 부대 목록 스크롤(18행 초과) + 전투 중 도감 + 툴팁 보강
- id: T38e | 역할: feature | 등급: D中 H1 (R1 S<=100k Wlight)
- 선행(blockedBy): T27 | 후행: T38d
- 핫스팟: UI/Hud.DrawBatList · UI/CodexMenu.cs(전투에서 재사용 진입) · Game.cs(키 1개) · Strings*.cs. 시뮬 무변경
- 브랜치: ui/t38e-list-codex (base = t27-cmd-ui2; T38c 와 Hud.cs 충돌 주의 - 직렬) | 상태: wip-ready (부분 - 아래 결과)
## 이미 있음 (T27 결과)
부대 목록 패널(연대›대대›중대, 병력·탄약·상태, 클릭 선택, 전멸 표시) 있음, 18행 초과 스크롤만 없음. 도감은 메인 메뉴만(UnitDef 50종 전수 테스트). 툴팁: 카드/탭에 있음.
## 확정 결정
- T27 확인 질문(미결): "도감을 전투 중(F-키 등)에서도 여는가, 메인 메뉴만인가?" -> 확인 필요 Q5. 부대 목록 승인 (2026-10-21): "부대 목록 패널 (연대 › 대대 › 중대 나무 구조, 병력 · 탄약 · 상태 표시, 클릭 선택)".
## 범위 밖
교전 알림/미니맵 표시(⑦ 미승인) · 튜토리얼 연결 · 포트레이트 그림 작업.
## 수용 기준
1) 행 18 초과에서 휠/드래그 스크롤, 선택 행이 보이도록 자동 스크롤. 2) (Q5 승인 시) 전투 중 도감이 일시정지 상태로 열리고 닫으면 복귀, 시뮬 틱 불변. 3) 툴팁 누락 목록(HUD 요소별) 보고 + 승인된 것만 추가. 4) uifuzz 30 예외 0 · detcheck 불변.
## 측정
selftest(uiicons) · uifuzz · 시트(행 40개 상태).
## 결과
브랜치 t38e-unitlist-codex (base t38a-settings-keys 7274c71, NO-MERGE 스택).
가정한 답: Q5 = 도감을 전투 중에도 연다 (F2, 토글; Esc/뒤로/F2 로 닫음). 열려 있는 동안 시뮬 틱만 건너뜀 (Paused 값 불변 -> 닫으면 그대로 복귀, 사용자가 이미 일시정지였으면 계속 정지). 스크롤 = 휠(3행) + 오른쪽 막대 끌기/클릭 + 선택 중대 행 자동 스크롤, 18행 창 그대로. 툴팁: 누락 목록만 보고, 추가 안 함 (승인 전).
구현: Hud 목록 스크롤 (순수 함수 ListClamp/EnsureVisible/ScrollFromFraction), 제목줄 "a-b / 총" 표시, Game F2/ToggleCodex/CodexBox, 콘솔 codexui · listui · listscroll N, Bindings F2 + CONTROLS 표, 퍼즈 키 F2, 퍼즈 겨냥 등록 listrow/listbar. 시뮬 · 기본값 · FormatVersion 무변경.
Palette 우회 그리기 (T38d 예외 목록): 신규 없음 - 모두 Palette 토큰 (Panel/PanelBorder/Accent/TextDim/Void) 사용. 도감 뒤 어둡게 = Palette.Void 알파 (HelpOverlay 와 동일 패턴).
툴팁 누락 후보 (미추가): 상단 버튼(저장/불러오기/격자/아이콘/종료), 대대 목록 행(클릭=선택, Shift=추가), 목록 막대, 미니맵, 정보 패널 부대 상태 아이콘.
검증 (2026-10-22, heavy_gate 대기 후 - 마지막 몇 회는 "gate timeout, proceed carefully" 였음): build 오류 0 (listscroll 콘솔 컴파일 확인), selftest uiicons PASS, uifuzz 30 (1280x720 창): 1192프레임 예외 0 · 크래시 0, uifuzz 30 --fuzz-menu: 1886프레임 예외 0 · 크래시 지점 0, detcheck GOLDEN match (8aa6448b 그대로). 시트: docs/art_review/ui_sheet_1024x640.png · ui_sheet_1280x720.png · ui_sheet_all.png (hud-list40 = 40행 스크롤 막대 · 12행 스크롤 상태, hud-codex = 전투 중 도감 추가). 1920x1080 시트는 사용자 규칙(창 <=1280x720)으로 갱신 안 함 (기존 파일 그대로).
