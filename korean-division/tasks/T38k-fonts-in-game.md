# T38k 게임 글꼴을 OFL 글꼴로 교체 (+ 제목 문자열 목록)
- id T38k | 역할 feature | 등급: D中·H1 (Ui.cs 만, 결정성 무관)
- 선행: ui/fonts-ofl 병합(assets/fonts) | 후행: T38d(종이 UI), 제목 교체 카드
- 만질 파일: src/UI/Ui.cs, (Hud/MainMenu 는 호출부 크기만), 테스트 1개
- 브랜치 ui/fonts-ofl 후속 | 상태 queued | 담당 -

## 확정 결정
- AZ(2026-10-22): 시안의 Windows 시스템 글꼴 문제를 OFL 글꼴로 해결, 저장소에 글꼴+라이선스 포함.
- 제목: 영문 'KOREAN DIVISION', 한글 '코리안 디비전', 작중 부대명(부제) '제17 대한 사단' (2026-10-22 최종, 공백 포함; 앞서의 '1920 대한 사단' 폐기).

## 현재 상태 (조사)
- `src/UI/Ui.cs` `LoadFonts`: `FindFont()` = assets/fonts/font.ttf -> Windows malgun.ttf -> NanumGothic.ttf -> raylib 기본.
  assets/fonts 폴더는 main 에 없었음(font.ttf 동봉 안 됨) -> 배포본이 맑은 고딕(비재배포)에 의존.
- `LoadFontEx(path, 16 | 26, codepoints)` 두 개만: `small`(size<20) / `large`(>=20). 72px 제목은 26px 아틀라스를 확대 -> 흐림. Bilinear.
- 코드포인트: ASCII 32..126 + `S`·`SnapMsg` 상수 문자열 + `defs.AllText()` + `MissionDef.AllText()` (Program.cs:164). 현재 약 1,095자(한글 약 915).
- 역할(제목/본문/숫자) 구분 없음. `Ui.Text(text,x,y,size,color)` 하나.

## 변경 (제안)
1. 역할 enum `FontRole {Heading, Body, Numeric}` + `Ui.Text(..., role = Body)`; 기본 Body 라 기존 호출부 무수정 동작.
   Heading = NanumMyeongjo-Bold, Body = NanumGothic-Regular, Numeric = NanumGothicCoding-Regular.
2. 크기 단계: 역할 x {14, 18, 26, 40, 72} 중 필요한 것만 지연 로드(처음 쓸 때 LoadFontEx). 첫 단계 = Body 16/26, Heading 26/40/72, Numeric 16/22. 글자 간격 0, Bilinear.
3. `FindFont` 를 `assets/fonts/<family>/<file>.ttf` 로. Windows 시스템 글꼴 탐색은 폴백으로만 유지(개발 PC), 최종 폴백 raylib 기본(한글 깨짐 경고를 로그 1줄).
4. 코드포인트 집합은 지금 방식 유지(필요한 글자만). 새 글자가 생기면 사각형이 되므로 셀프테스트로 누락 검사.
5. Gothic Coding 은 한글 = 영문 2칸 고정폭. 표/숫자 열 너비 재조정은 T38d 몫.

## 수용 기준
- [ ] 시작 시 글꼴 3종 로드 성공, 콘솔에 사용 경로 1줄. 시스템 글꼴 탐색을 끈 상태(테스트 스위치)에서도 동일.
- [ ] 셀프테스트 `FontCoverageSelfTest`: 수집된 코드포인트 전부가 3 글꼴 cmap 에 존재(누락 0). 제목 3문자열(KOREAN DIVISION · 코리안 디비전 · 제17 대한 사단) 포함.
- [ ] 메인 메뉴 스크린샷(--screenshot)에서 두부 없음.
- [ ] 아틀라스 로드 합계 <= 300ms (최대 15 아틀라스, 각 ~1.1k 글리프, 텍스처 <= 2048x2048), 프레임 시간 회귀 없음(BASELINE_CURRENT 대비).
- [ ] 렌더 전용: 결정성/세이브 영향 없음, selftest 전체 통과, 골든 해시 불변.
- [ ] dist/assets 에 글꼴 + OFL.txt 3개 포함(publish.ps1 검사 통과), 설정 화면 또는 README 에 OFL 고지 한 줄.

## 범위 밖
종이 UI 레이아웃(T38d), 제목 문자열 교체(아래 목록은 후속 카드용), 글꼴 서브셋.

## 게임 제목 문자열이 나오는 곳 (git grep, 2026-10-22 ui/fonts-ofl = main + 글꼴)
명령: `git grep -n -E "GameTitle|Config\.Title|KOREAN DIVISION|Korean Division|코리안 디비전|코리안디비전" -- . ':!*.patch' ':!*.png'` (경로는 저장소 루트 기준, 170자로 자름)
코드 요약: 창 제목 = `src/Config.cs:6` Title + `Program.cs:160` InitWindow, 메인 메뉴 = `UI/MainMenu.cs:154-155`(72px),
HUD 좌상단 = `UI/Hud.cs:411-412`, 문자열 = `UI/Strings.cs:16` GameTitle. 별도 '정보(About)' 화면은 없음.
새 문자열 '코리안 디비전' · '제17 대한 사단' 은 현재 게임 소스에 0건(`git grep` 확인) -> 교체 카드에서 신규 추가. 시안 위치: 표지 제목 블록(KOREAN DIVISION / 코리안 디비전 / 제17 대한 사단), 책상·지도 머리글의 부대명, 임명장 문구.
어셈블리/네임스페이스(`KoreanDivision`)는 코드 이름이라 교체 대상 아님.
```
campaign/pdf/C1E1_*_2026-10-22.html:23:<div class="sub">KOREAN DIVISION 캠페인 디자인 v1 · 2026-10-22 · 사용자 승인 사항 기준 (호감도 증감 값은 미지정 — 방향만 표시)</div>
campaign/pdf/C1E1_*_v2.html:22:<div class="sub">KOREAN DIVISION 캠페인 디자인 v1 · 2026-10-22 · 승인 사항 기준 · 호감도 증감 크기는 +1/+2/+3/+5 중 사용자 지정(미지정 = 방향만)</div>
campaign/pdf/C1E1_*_v3.html:23:<div class="sub">KOREAN DIVISION 캠페인 디자인 v1 · 2026-10-22 · 승인 사항 기준 (spec_c1e1 R1~R13) · 호감 · 사기 값은 보류(로드맵 추후)</div>
campaign/spec_c1e1.txt:1:KOREAN DIVISION — C1E1 안동 점령전 · 층 2 명세 (초안 v0.2)
game-tools/audio-sfx/generate.py:1:"""Generate Korean Division default SFX set (procedural, no third-party samples).
game-tools/audio-sfx/generate.py:804:    h = ['<!doctype html><html lang="ko"><meta charset="utf-8"><title>Korean Division SFX audition</title>',
game-tools/audio-sfx/generate.py:808:         '<h1>Korean Division default SFX (procedural)</h1><p>mono / 16-bit / 44.1 kHz. Click to audition.</p>']
game-tools/audio-sfx/generate.py:819:    m = ['# Korean Division default SFX set (procedural)', '',
game-tools/campaign/EPISODES.md:1:# Korean Division — 캠페인 에피소드 설계
game-tools/image/tools/parts/kd_new.py:1:"""Korean Division batch (n63/n68/n72): papakha, volunteer cap + civilian clothes, regimental flag poles (national colours), tach
game-tools/image/tools/parts/kd_new2.py:1:"""Korean Division batch (newunits N68/N71): Manchurian hunter (fur cap + fur collar), village militia (guapi cap, undyed cotton
game-tools/mission-editor/DESIGN.md:1:# 미션/지도 편집기 설계서 (Korean Division Mission Editor)
korean-division/CLAUDE.md:1:# Korean Division - 헌장 (L0, <=2KB)
korean-division/Program.cs:160:Raylib.InitWindow(Config.ScreenWidth, Config.ScreenHeight, $"{Config.Title} {Config.Version}");
korean-division/README.md:1:# Korean Division
korean-division/README.md:5:- **Korean Division**: 가상 1920년 대체역사 — 러일전쟁 러시아 승리, 한일합병을 면한 대한제국의 **만주 북벌**.
korean-division/WORKPLAN.md:1:# Korean Division 작업 계획
korean-division/assets/fonts/FONTS.md:18:- 숫자·타자기 느낌(날짜, 병력수, 표, 'KOREAN DIVISION' 로고 줄): Nanum Gothic Coding. 한글·영문 모두 고정폭(한글 = 영문 2칸).
korean-division/assets/fonts/FONTS.md:23:- 시험 문장 `가나다라마바사 1920대한 사단 KOREAN DIVISION 0123456789 [작전 지도]`, `KOREAN DIVISION`, `코리안 디비전`, `1920 대한 사단` 를
korean-division/docs/PROJECT_NOTES.md:2:> 요약: Korean Division = C# .NET 8 + Raylib-cs 캠페인 게임(wargame 엔진 분기). 빌드 · 플래그 · 검증 · 이름 규칙 · 결정성 규칙은 아래 '빌드 · 실행' · '규칙'. 헌장(CLAUD
korean-division/docs/PROJECT_NOTES.md:24:- **Korean Division** — 캠페인 위주 싱글 플레이 게임. wargame (master `1444d04`, 2026-10-18) 소스에서 분기. C# .NET 8 + Raylib-cs.
korean-division/docs/ROADMAP_LONG.md:94:# Korean Division 로드맵
korean-division/docs/ROADMAP_SHORT.md:1:# Korean Division 단기 로드맵 — 지시 · 진행 순서 · 대기열 (세션마다 읽음, 50줄 이내)
korean-division/docs/korean-division-design.txt:1:Korean Division - 스토리 · 캠페인 지시서 양식
korean-division/docs/legacy/COMPETITIVENESS_2026-10-19.txt:2:Korean Division - Steam RTS 경쟁력 보고서
korean-division/docs/legacy/HANDOFF_ARCHIVE_2026-10-22.md:34:N28. (로드맵만 — 사용자 2026-10-19) 챕터 1 구조 작성 (에피소드 8 × 미션 3~4, 에피소드마다 대형 전투 1, 분기 · 결말 안). 세계관 · 개요 정리본 = `coding/
korean-division/docs/legacy/ROADMAP_DONE.md:1:# Korean Division 로드맵 — 완료 아카이브

korean-division/docs/legacy/ROADMAP_DONE.md:197:- wargame master `1444d04` 복사 (미검증 브랜치 `wip/ai-v04` 제외). 이름 변경: `KoreanDivision.*`, `KD_*`, 저장 `.kds`/`KDSV`, 리플레이 `.kdr.j
korean-division/src/Config.cs:6:    public const string Title = "Korean Division";
korean-division/src/UI/Hud.cs:411:        Ui.Text(S.GameTitle, 10, y, 16, Palette.Accent);
korean-division/src/UI/Hud.cs:412:        float x = 10 + Ui.Measure(S.GameTitle, 16).X + 20;
korean-division/src/UI/MainMenu.cs:154:        Vector2 m = Ui.Measure(S.GameTitle, 72);
korean-division/src/UI/MainMenu.cs:155:        Ui.Text(S.GameTitle, (screenW - m.X) / 2, y, 72, Palette.Accent);
korean-division/src/UI/Strings.cs:16:    public const string GameTitle = "KOREAN DIVISION";
korean-division/tools/agent/AGENT_RULES.md:1:# Korean Division - common agent rules (core, <=3KB)
ops/DECISIONS.md:195:- **게임 제목**: 영문 'KOREAN DIVISION', 한글 '1920대한 사단' (시안의 가제 '북벌 1920' 폐기). 게임 내 제목 표기 교체 카드 필요.
"\354\272\240\355\216\230\354\235\270 \353\224\224\354\236\220\354\235\270 v1.txt":1:KOREAN DIVISION — 캠페인 디자인 v1 (양식 + 용어 + 초기 흐름도)
```

## 결과 (2026-10-22, wip-ready)
- 역할 3 (Body=Gothic Regular, Heading=Myeongjo Bold, Numeric=GothicCoding Regular), 단계: Body 16/26, Heading 26/40/72, Numeric 16/22. 시작 때 Body 16/26 만 로드, 나머지는 처음 쓸 때. 40px 이상은 제목 전용 글자 집합(ASCII + 제목 3문자열)만 -> 72px 아틀라스 가벼움.
- 폴백: 번들 -> 본문 번들 -> Windows 맑은 고딕/나눔고딕(`KD_FONT_NO_SYSTEM=1` 이면 끔) -> raylib 기본(경고 1줄). 콘솔에 경로 1줄 + 아틀라스 수/ms 1줄. `KD_FONT_TIME=1` = 전 단계 미리 로드 측정.
- 로드 시간: 시작 2개 36 ms, 전 7개 100~107 ms (<= 300). 코드포인트 2,247 (한글 2,139; 카드 조사 때 1,095 에서 증가).
- 셀프테스트 `FontCoverageSelfTest` (KD_SELFTEST_ONLY=fonts, 전체 selftest 에도 연결): 3 글꼴 cmap(자체 파서 FontCmap) 누락 0, 제목 3문자열 포함, OFL.txt 동봉. uiicons OK, uifuzz 30 + --fuzz-menu 1908 프레임 예외 0, detcheck 8 변형 동일 GOLDEN 8aa6448b, ui_sheet 3 해상도 51장 OK (docs/art_review).
- 메인 메뉴 제목만 Heading 으로 전환 (MainMenu.cs 2줄). 숫자 역할(Numeric) 호출부는 아직 없음 -> T38d 몫. dist 크기: 글꼴 14.8 MB, 미사용 7.4 MB 제거 제안은 FONTS.md.

