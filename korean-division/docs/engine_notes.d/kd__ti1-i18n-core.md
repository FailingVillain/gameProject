## TI1 i18n 기반 (Loc)
- `src/Core/Loc.cs`: `Loc.T(key, 한글원문)`, `Loc.Language` (ko|en, 모르는 값 = ko, 즉시 전환), 표시 전용(시뮬 미사용, 저장 형식 · FV · 골든 불변). ko = 조회 없이 원문. en = `assets/i18n/en/*.csv` (key,ko,en,status,kohash; 헤더 열 이름으로 읽음, BOM · 따옴표 · `\n` 이스케이프). 누락 = 원문 + `KD_I18N_WARN=1` 이면 키당 1회 stderr.
- `Loc.WithKo(f)`: 범위 안에서 ko 강제 (S 프로퍼티의 한국어 원문 수집용). `Loc.AllText()` = en 사전 전체 -> `Ui.CollectCodepoints` 가 const + 프로퍼티(ko 강제) + AllText 를 모두 포함 (언어 전환 때 아틀라스 재로드 없음).
- settings.json `Language` 키 (T49b 관대한 읽기: 모르는 값 -> ko + 안내 1줄). 설정 화면 언어 행 (키 행 위), 즉시 반영.
- `tools/i18n/convert_consts.py <파일...>`: `public const string X = "..."` -> `public static string X => Loc.T("S.X", "...")` (멱등, 보간 · 연결 상수는 건너뛰고 보고, `--check`). 시범: StringsCampaignPrisoners.cs 13개. 나머지는 TI2.
- 시험 `KD_SELFTEST_ONLY=i18n` (SelfTest 전체에도 포함). en 샘플 3행: assets/i18n/en/ui_misc.csv.
