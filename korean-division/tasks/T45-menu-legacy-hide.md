# T45 메뉴 레거시 숨김 (다자간 숨김 · AI 대 AI 미션 -> 관전 탭)
- T45 | feature | D中 H1 (UI/Strings/MissionDef 로더 한 함수; 시뮬 무변경)
- 선행: 없음 (main 29c6ab9) | 후행: AQ2 (교전 테스트/빠른 교전 제거 검토, 나중)
- 만진 파일: UI/MainMenu.cs · UI/MissionMenu.cs · UI/Strings.cs · Core/Mission/MissionDef.cs(AvailableSpectate) · Program.cs(--dev-menu) · Tests/MenuHideSelfTest.cs · SelfTest.cs · tools/agent/run_game.ps1(75a1712 복사)
- 브랜치 kd/t45-menu-hide (kd-develop/kd-t45) | 상태: wip-ready | 담당: feature(sonnet)
## 확정 결정 (사용자 2026-10-22, DECISIONS AQ)
(1) 관전 유지 (2) 다자간 대결 = 숨김만, 삭제 금지, CLI/개발 경로 유지 (3) AI 대 AI 미션은 미션 메뉴 -> 관전 탭 (4) 교전 테스트 · 빠른 교전은 건드리지 않음.
## 범위 밖
교전 테스트/빠른 교전, 시뮬 변경, 새 메카닉, 미션 json 수정(없음).
## 구현
- 개발 플래그 `MainMenu.DevMenu`: 인자 `--dev-menu` / 환경 `KD_DEV_MENU=1` / `--menu-page setup|setup-gen`(자동). 꺼짐(기본)이면 '플레이' 버튼 숨김.
- 관전 설정(DevMenu 꺼짐): 인원 행 숨김, 생성 맵 2인 고정(`SpectateDefaultPlayers`=2), 3인 이상 고정 지도 칩 숨김(corners4 · flat4 는 4인이라 숨음, 2인 terrain_demo 는 남음). 세력/AI/지도 크기/시드/안개는 유지. DevMenu 켜면 종전과 같음.
- 관전 설정 아래 'AI 대 AI 미션 (관전)' 목록(2열 버튼): 클릭 -> `GameOptions{MissionId, FromMenu}` (미션 spectate 플래그가 관전 모드를 켬). MissionMenu 목록에서는 spectate 미션 제외. `--mission`, `--mission-run`, tactics-obs, soak 은 id 직접 접근이라 무영향.
## AI 대 AI 로 분류한 미션 (근거: json `"spectate": true` 필드, 7개) - 사용자 확인 필요 (AQ3)
| id | 제목 | 메모 |
|---|---|---|
| ai_bn_assault | 대대 공격 대 방어선 (AI 대 AI, 전술 관찰) | 이름 ai_*, spectate:true |
| ai_bn_meeting | 대대 조우전 (AI 대 AI, 전술 관찰) | 〃 |
| ai_kr_vs_ft_1000 | 한국군 1000 대 봉천군 1000 (AI 대 AI) | 〃 |
| ai_kr_vs_jp_trench | 참호전 관전 (AI 대 AI) | 〃 |
| armour_test | 장갑 차량 (시험) | 주석/브리핑에 'AI 대 AI 관전 시험 미션' |
| bandit_raid | 마적 습격 (시험) | 〃 |
| units_showcase | 새 유닛 시험 (관전) | 〃 |
나머지 10개(c0_01/02, c1_01/02, forest_ambush, supply_escort, trench_assault, village_defense, yalu_crossing, artillery_spotters)는 spectate 아님 -> 미션 메뉴 유지.
## 수용 기준
빌드 0 경고 오류 / `KD_SELFTEST_ONLY=menuhide`(신규) · `uiicons` · `missions` · `n77` PASS / uifuzz 30 / detcheck GOLDEN 불변 / 스크린샷 docs/art_review/t45_*.png.
## 결과
- 빌드 오류 0; selftest menuhide(신규 12항목) · uiicons · missions · n77 PASS; uifuzz 30 (게임/--fuzz-menu) 오류·크래시 0; detcheck GOLDEN match 8aa6448b.
- 스크린샷 docs/art_review/t45_{main,main_dev,spectate,spectate_dev,missions}.png (run_game.ps1 보조 모니터).
- 판단: 관전 설정 지도 칩에서 4인 지도(corners4/flat4) 숨김 - 관전 시작은 2인 생성 맵 또는 terrain_demo 또는 미션 목록. 7개 분류는 json spectate 필드 근거(AQ3 사용자 확인).
- 상태: wip-ready (kd/t45-menu-hide, main 29c6ab9 위). 병합/푸시 안 함.
