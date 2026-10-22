# T38l 게임 제목 문자열 교체 (창 · 메인 메뉴 · HUD)
- id: T38l | 역할: feature | 등급: D中 H1 (문자열만, 결정성 무관) | 상태: wip-ready | 담당: feature(Haiku 5.5)
- 브랜치: t38l-title-strings (base = t38a-settings-keys 7274c71) | NO-MERGE 스택 (t38a 위). 병합은 t38a 뒤 merger 가 처리.
- 선행: T38a(7274c71) | 후행: 없음
- 근거: ops/DECISIONS.md BC/BE (2026-10-22 최종). 목록 원본: ui/fonts-ofl 의 T38k 카드 `제목 문자열이 나오는 곳`.

## 확정 문자열 (사용자 결정, 변경 금지)
- 영문 전체 제목: `KOREAN DIVISION : MARCH ON MANCHURIA` (창 제목)
- 짧은 형태 (HUD · 로고 줄): `KOREAN DIVISION`
- 메인 메뉴 큰 제목: `KOREAN DIVISION`, 그 아래 작은 줄 `MARCH ON MANCHURIA`, 그 아래 한글 줄 `대한 사단 : 북벌 1920`
- 작중 부대명: `제17 대한 사단` (S.UnitNameKo 로 추가, 아직 화면 미사용 - 해당 화면 카드에서 사용)

## 변경 (구현)
- `src/Config.cs` Title = 영문 전체 제목 (Program.cs InitWindow 가 `Title + Version` 으로 사용 -> 창 제목).
- `src/UI/Strings.cs` S.GameTitle(짧은 형태, 기존 유지) + S.GameTitleFull · GameTitleSub · GameTitleKo · UnitNameKo 추가.
- `src/UI/MainMenu.cs` DrawTitle: 72px 제목 아래에 26px 부제(Palette.TextDim) · 26px 한글 줄(Palette.Text) 적층 (높이는 Ui.Measure 로 계산). 제목 시작 y 를 screenH*0.30-120 -> -150 으로 올림 (버튼 줄과 겹침 방지).
- `src/UI/Hud.cs` 는 S.GameTitle(짧은 형태) 그대로 - 변경 없음.
- 색 · 팔레트 신규 없음 (기존 토큰 Accent / TextDim / Text).
- dist/publish.ps1 · 빌드 메타데이터: 제목 문자열 없음 (AssemblyName KoreanDivision 유지 - 코드 이름이라 불변).

## 검증
- 빌드 0 오류 (heavy_gate 통과 후).
- `KD_SELFTEST_ONLY=uiicons` PASS.
- `tools/detcheck.ps1 -Exe bin/t38l/KoreanDivision.exe`: 8 변형 동일, GOLDEN match (ticks=18000 units=20 buildings=32 shots=2530).
- 1280x720 메인 메뉴 스크린샷 (`--menu-shot`, 창 모드): docs/art_review/t38l_menu_1280x720.png (미커밋, 이미지 뷰어 미사용 - 겹침 육안 확인은 지휘자 몫).

## 범위 밖
종이 UI 레이아웃, 글꼴 교체(T38k), 설정 화면의 OFL 고지, 부대명 화면 노출.
