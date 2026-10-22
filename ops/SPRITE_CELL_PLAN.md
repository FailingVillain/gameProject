# SPRITE_CELL_PLAN — 프레임별 가변 칸 (옵션 B) 설계 · 카드 순서

기준 2026-10-22, main 90af825. 분석 = 읽기 전용 (`git show` / `git diff main...<br>`), 렌더 전용 → **결정성 위험 0** (시뮬 · 체크섬 · 저장 무관).
**사용자 결정 (최종): 옵션 B 채택** — 프레임마다 가변 크기 사각형 + 잘라 담기. 엎드림 폭 문제는 B 자체로 해결 (시트 전체 확대 불필요).

## 0. 현재 상태 (실측)

- **GPU 쪽은 이미 B 의 절반이 있다**: `LodAtlas.Build` (2026-10-19 art-memory) 가 칸마다 알파 경계 상자로 잘라 (1px 테두리 + 1px 틈) 선반 배치,
  `COX/COY` 로 칸 기준 위치를 보관. 투명 여백은 GPU 상주 메모리에 이미 0 바이트. 남은 낭비 = **디스크 → 디코드 → 알파 훑기** (작업자 스레드 CPU · 순간 RAM).
- 칸 크기는 레벨당 하나 (`LodLevel.FrameW/H`, `x0 = CellCol × FrameW`). 반전 = `FrameW − an.X`, `FrameW − COX − CW`.
- 시트별 `"frame"/"anchor"` (make_unit `prone_wide.py` 가 씀) 는 **main 게임이 못 읽는다** → main 의 infantry-bandit `_prone.png` (160×200 칸) 이 128×160 으로 잘못 잘려 읽힘 (현존 버그, 3 레이아웃).
  읽는 코드 (`LodLevel.CellFW/CellFH`, 시트 anchor) 는 **T01 gemini-import 커밋 06424c8** 에 있음 (main 미병합, t38* · tcd · c1e1-g1 · merge/0.2-ui 가 그 위에 쌓임).
- make_unit `frame_wide` (gt-prone-fix 8a1794d) = 레이아웃 전체 칸을 1.375 (176) / 1.5 (192) 배로 → main 코드 그대로 읽힘 (LOD 비율 176/88/44 유지 → `LevelFor` 문턱 불변, 월드 배율 `SCALE 0.25`/px 불변).

### 메모리 (보병 29종 · kd1920, PNG 머리 + 알파 bbox 로 계산 · PIL 일회성 스크립트, 저장소 밖)

| | main (128 칸 + _prone 시트) | kd/prone-wide-assets (176/192 균일, 작업 중 워크트리) | 변화 |
|---|---|---|---|
| 디코드 크기 (전 LOD, RGBA) | 623 MB | 895 MB | **+44 %** (순간 RAM · 훑기 시간, 상주 아님) |
| LOD0 잘린 칸 합 (전 칸, 상한) | 219 MB | 230 MB | **+5 %** (실제 새 픽셀 · 열 4개 추가) |
| 디스크 PNG | 32.2 MB | 35.2 MB | +9 % |

- GPU 상주 = 잘린 칸 중 `Used` + 화면에 나온 종류만. 실측 uifuzz 60: LOD0 최대 211 MB / 예산 768 MB, 보병 1종 LOD0 ≈ 9.5 MB.
- 가옥 (house-brick 예): LOD0 856×432 (2칸) = 1.41 MB 디코드, 채움률 0.70 → 42 기본형 ≈ 78 MB 디코드 / ≈ 55 MB 잘린 칸 (전 LOD). 가옥은 이득이 작다 (여백 30 %).
- 결론: B 의 이득은 **GPU 가 아니라** 로딩 CPU (디코드 · 알파 훑기 픽셀 ≈ −66 %, 위 895 → ≈ 300 MB 수준: LOD0 230 + @64·@32 ≈ 30 %) · 디스크 · **그림 칸 자유** (엎드림 · 기관총 · 깃발이 칸에 안 갇힘, `frame_wide` · `prone_wide` 특례 삭제).

## 1. 옵션 비교 (요약)

| | A 엎드림 전용 둘째 시트 | **B 프레임별 가변 사각형 (채택)** | C 균일 확대 (현재 prone-wide-assets) |
|---|---|---|---|
| 코드 | T01 의 CellFW/CellFH 그대로 (≈0) + prone_wide 유지 | 로더 · 아틀라스 · 그리기 v2 경로 + 패커 | 0 |
| GPU 상주 | = main | = main (이미 잘림) | +5 % |
| 디코드 | ≈ main | ≈ −66 % | +44 % |
| 그림 자유 | 엎드림 3칸만 | 모든 프레임 | 칸 하나 크기로 전부 |
| 텍스처 전환 · 배치 | 불변 | 불변 (LOD0 종류별 페이지, LOD1+ 공유 페이지) | 불변 |

## 2. B 설계

### 2.1 디스크 형식 `"format": 2` (레이아웃 json, LOD 마다)
```
{ "format": 2, "frame": [128,160],            // 명목 칸 = LOD 선택 기준 (LevelFor) · UI 맞춤용, 실제 그림 크기와 무관
  "dirs": [...], "columns": [...], "anims": {...}, "mirror": true,
  "sheets": [{"file": "x_p0.png", "team": "x_p0_team.png", "size": [W,H]}],
  "rects": { "<frame>": [[sheet, x, y, w, h, ax, ay], ...dir 행 순] } }   // ax, ay = 기준점 (사각형 왼쪽 위 기준, 소수 가능), w=0 = 빈 칸
```
- 사각형 = 알파 경계 + 1px 투명 테두리 (지금 Build 의 Pad 규칙 그대로) → 래스터 결과 픽셀 동일. 칸 사이 1px 틈 (Gutter).
- 팀 시트 = **같은 사각형** (기본 · 팀 알파의 합집합 bbox). 팀 접기 (알파 254) 는 지금처럼 로드 때 — contact/검토 도구가 _team 을 계속 읽음.
- 사용 안 하는 칸 (`spare`, `go_prone` 등 `Used` 밖) 도 패커가 담는다 (게임 쪽 `Used` 필터 유지 — 이름 목록이 바뀌어도 재패킹 불필요).
- 페이지 ≤ 4096×4096, 시트 분할은 페이지 단위 (`_p0`, `_p1`).

### 2.2 게임 코드 (렌더 전용)
| 파일 / 함수 | 변경 |
|---|---|
| `LodSprite.Load` | `format==2` 이면 `rects` → 레벨에 `CellSheet[ci]`, `SX/SY/SW/SH[ci]` (원본 사각형), `RAX/RAY[ci]` (사각형 기준 기준점). `Anchors` 는 v1 호환용으로 채움. v1 (격자) 경로 그대로 둠 — 차량 · 옛 시트 · 미이주 시트. |
| `LodAtlas.Build` | v2: 시트 디코드 후 사각형을 **훑기 없이** 복사 (`OX/OY` 불필요). 배치 · 업로드 · 공유 페이지 · LOD0 예산/내림 불변. `Raw` 측정 = 시트 크기 (자동으로 줄어듦). |
| `LodDraw.Cell` | 위치 = `feet − (RAX·k, RAY·k)`; 반전 = `feet.X − (CW − RAX)·k`, src 폭 음수 (지금과 같음). v1 은 `COX/COY` + `FrameW` 그대로. 두 경로를 레벨 플래그 하나로 분기. |
| `LodDraw.CellFit` | 사각형 크기 그대로 (지금도 CW/CH 기준) — 변경 없음. |
| `LodAuditSelfTest.Filled` | v2: `SW>0` 이면 채워짐 (PNG 읽기 불필요 → 테스트 빨라짐). v1 경로 유지. 새 검사: v2 사각형이 시트 경계 안 · 겹침 없음. |
| `UnitArt` · `Theme.LoadLod` | 변경 없음 (`FrameW/H` = 명목 칸 → `Size`, `LevelFor` 그대로). |

- **LOD 피라미드**: @64/@32 는 따로 렌더된 그림 → 패커가 LOD 마다 독립으로 자르고 담음. 명목 칸은 128/64/32 비율 유지 (LevelFor · `Scale = scale × L0.FrameW / Li.FrameW` 불변).
  가변 칸이라 LOD 사이 사각형 크기 비율이 달라도 무관 (각 LOD 기준점이 자기 px).
- **반전**: 기준점이 사각형 기준이므로 칸 중심 가정이 사라짐 — make_unit 의 기준점이 칸 가운데가 아닌 시트 (비대칭 칸) 도 정확.
- **배치 · 텍스처 전환**: 페이지 구성 규칙이 같으므로 y 정렬 유닛 그리기의 전환 수 불변 (LOD0 = 종류×진영별 페이지, LOD1+ = 공유 1~2장). 셰이더 진입 (`TeamShader.Enter`) 불변.
- **결정성**: 렌더 전용 — Snapshot · 체크섬 · 저장 무관. detcheck 는 게이트용으로만.

### 2.3 도구
- `korean-division/tools/pack_sheets.py` (신규): 격자 레이아웃 (v1, 시트별 `frame` 포함) + anchors json → v2. **재렌더 없음** (기존 PNG 에서 자름). `--verify` = v2 → 격자로 되살려 원본과 알파>0 픽셀 동일 비교.
- `import_kd1920.py`: 복사 뒤 `pack_sheets` 호출 (`--no-pack` 으로 끔). LOD 고르기 (`frame_w` 1/2·1/4) 는 명목 칸 기준 그대로.
- make_unit: 렌더 칸은 넉넉한 캔버스 (현 `frame_wide` 그대로 써도 됨 — 여백은 패커가 0 으로) · `prone_wide.apply` 특례는 B 이주 후 삭제 (T56c).

## 3. 분기 충돌 (git diff main...<br> --stat, 2026-10-22)

| 브랜치 | 겹치는 파일 | 비고 |
|---|---|---|
| gemini-import (T01, 06424c8) + 그 위 t38a~m · kd/tcd-funds-slots · kd/c1e1-g1 · merge/0.2-ui | LodAtlas · LodSprite (CellFW/CellFH · 시트 anchor · walk_quick) · UnitArt · LodAudit | **T56b 는 T01 병합 뒤 시작** (같은 줄) |
| t55s-house-select | LodSprite (`LodDraw.Cell/CellFit` plain · mirror 인자) | T56b 는 T55s 병합 뒤 |
| kd/t32d-pioneer-art | theme.json · import_kd1920.py | T56c 와 import 스크립트 겹침 → 병합 뒤 |
| gt-prone-fix | make_unit.py (4줄) | T56c 가 이어받음 |
| kd/prone-wide-assets | 자산만 (코드 0) | 충돌 없음 |
| t43 | 없음 | - |

## 4. kd/prone-wide-assets (균일 176/192 가져오기) 처리

**그대로 유지 · 먼저 병합** (재작업 불필요). 이유: main 코드로 바로 읽힘 · GPU +5 % 뿐 · main 의 bandit `_prone` 오독 버그를 없앰 · B 가 오면 T56d 의 재패킹이 여백을 자동 0 으로.
안전 규칙 (그 에이전트의 전후 수치로 판정):
- **go**: uifuzz 60 LOD0 최대 증가 ≤ +10 % 이고 LOD0 최대 ≤ 384 MB (예산의 절반) · 업로드 프레임 최대 ≤ 6 ms · lodaudit 통과.
- **no-go** (하나라도 넘으면): 가져오기 보류, T56a~b 뒤 v2 로 다시 가져온다.
B 자체의 안전 확인 (T56d): LOD0 최대 ≤ main +2 % · 아틀라스 만들기 합계 시간 감소 · `--shot` 줌 1/2/4 픽셀 동일 · lodaudit · uifuzz 내림/물러서 그림 횟수 악화 없음. 넘으면 v1 경로로 되돌림 (자산만 되돌리면 됨).

## 5. 카드 초안 (각 ≤150k 토큰)

| id | 제목 | 역할 · 등급 | 예상 | 선행 | 핫스팟 |
|---|---|---|---|---|---|
| T56a | v2 형식 명세 + `pack_sheets.py` (+ `--verify` 되살림 비교) · 보병 1종 시범 | feature 中 (sonnet) | 70~90k | 없음 | 없음 (새 파일) |
| T56b | 게임 v2 읽기: LodSprite.Load · LodAtlas.Build · LodDraw.Cell 반전 · LodAudit v2 · 시범 1종 픽셀 동일 | sim 上 (opus) | 110~140k | T01 · T55s 병합, T56a | LodSprite · LodAtlas · LodAudit |
| T56c | 파이프라인: import_kd1920 가 패킹 호출, make_unit `prone_wide` 특례 삭제 · frame_wide 는 캔버스 의미만 | feature 中 (sonnet) | 50~70k | T56a, t32d 병합 | import_kd1920 · make_unit |
| T56d | 이주: kd1920 전 유닛 (prone-wide 27종 포함) 재패킹 · 측정 (uifuzz 60 메모리 · 만들기 시간 · 디코드 MB) · 스크린샷 동일 · go/no-go | measure+art 中 (sonnet) | 80~110k | T56b, T56c, prone-wide-assets | 자산만 |
| T56e | 가옥 (T55 계열 42 기본형 · 폐허 칸) v2 가져오기 | art 中 (sonnet) | 30~50k | T56d, T55 병합 | 자산 · import_kd1920_buildings |

순서: T56a ∥ (T01 · T55s · prone-wide-assets 병합) → T56b → T56c → T56d → T56e. 합계 ≈ 350~460k 토큰, 카드 5장.
각 카드 게이트: 빌드 + `KD_SELFTEST_ONLY=lodaudit` + detcheck (렌더 전용 확인) + 시범 스크린샷 비교. snaptest 불필요 (상태 변경 없음).
