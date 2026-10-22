# T38c 명령 카드 칸 넘침 감사 + 수정 (보병 카드 차면 집결 · 포격 종류 빠짐)
- id: T38c | 역할: feature | 등급: D中 H1 (R1 S<=60k Wlight)
- 선행(blockedBy): T27 | 후행: -
- 핫스팟: Game.CommandCard()/BuildUnitCard (Game.cs ~339-400: 3x3=9칸, 빈 칸 없으면 rally/포격 종류 버튼 탈락) · UI/Hud.DrawCommandCard · Core/Commands.cs · Strings.cs. 시뮬 무변경
- 브랜치: t38c-on-ui (base = t38a-settings-keys 7274c71, T27 쪽 넘김 위에 재구현; kd/t46-card-overflow 대체) | 상태: wip-ready | 담당: feature
## 이미 있음
카드 탭 2개(명령/전투) 있음. 전투 탭에는 집결 버튼이 항상 있음. 문제 = 명령 탭에서 칸이 차면 집결(J)/포격 종류(O)가 카드에서 빠짐 (키는 전역 동작).
## 확정 결정
- ROADMAP_LONG 단기 로드맵 2 (2026-10-21): "명령 카드 칸 넘침 (보병 카드 차면 집결 · 포격 종류 빠짐)". 해결 방식(칸 재배치/탭 추가/페이지)은 확인 필요 Q3.
## 범위 밖
새 명령 추가 · 키 변경 · 카드 그림 교체.
## 수용 기준
1) 감사표: 병종 조합(보병/기관총/곡사포/혼합/관측반 등) x 명령 탭/전투 탭 에서 사용 가능 명령 vs 표시 명령 -> 누락 0 (자동 테스트로 열거). 2) 누락이 있던 조합이 수정 후 모두 카드에서 클릭 가능. 3) 핫키 중복 없음. 4) uifuzz 30 예외 0.
## 측정
신규 selftest(카드 열거) · uifuzz · 카드 스크린샷 시트(조합별). 사용자 검수 필요.
## 결과
- 가정(Q3 미답): 쪽 넘김 방식. 명령 탭 버튼이 9 초과면 선호 칸 순으로 쪽당 8개 + 우하단 쪽 버튼 ("1/N", 클릭 또는 `.` 키, 마지막 다음 = 첫 쪽). 9 이하는 옛 배치 그대로. 단축키는 모든 쪽에서 동작. UI 전용: 시뮬 · 기본값 · FormatVersion 무변경.
- 변경: T27 의 고정 2쪽(CardPage 토글, "더 보기"/"처음으로")을 N쪽 `Game.PaginateCard(list,page,out pages)` 로 일반화(static), `CommandCard(allPages)`, `CardPages`; `.` 는 Bindings.Extra + CONTROLS.md §4 · 표 (F1 도움말 자동) 에 고정 키로 등록 - T38a 재지정은 글자 A-Z 만 받으므로 `.` 로 재지정 불가(테스트). Strings: CmdMoreCard/BackCard 제거, CmdCardPage 추가. UiIconsSelfTest: 쪽마다 칸 충돌 0 · 모든 버튼 정확히 한 쪽 · 11개/25개 합성 카드 · `.` 재지정 거부.
- 검증: build OK, KD_SELFTEST_ONLY=uiicons PASS, uifuzz 30 예외 0 (+ --fuzz-menu 예외 0), detcheck GOLDEN match (8aa6448b). 실창 클릭 · 스크린샷은 미검증 (사용자 검수 필요).
- 대체: kd/t46-card-overflow (54e8a54) 는 T27 줄과 Game.cs 충돌 -> 이 브랜치로 대체.
