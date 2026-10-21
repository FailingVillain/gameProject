# Korean Division

캠페인 위주 싱글 플레이 게임. **C# (.NET 8) + raylib (Raylib-cs)**. wargame RTS 엔진 (2026-10-18 분기) 위에 만든다.

- **Korean Division**: 가상 1920년 대체역사 — 러일전쟁 러시아 승리, 한일합병을 면한 대한제국의 **만주 북벌**.
- 미션 1판 1~2시간, 보병 위주, 아군 · 적 각각 수천 기. 자원 · 생산 · 건물 없이 **주어진 유닛만으로 싸운다** (2026-10-19 방향 변경).
- 경제 · 생산 · 건물 · 공병 (wargame 에서 물려받은 부분) 은 2026-10-19 제거됨.

## 실행
`dist\KoreanDivision.exe` 를 그냥 실행하면 **시작 화면** (플레이 · 관전 · 리플레이 · 종료). 아래처럼 인자를 주면 바로 시작한다.
```bash
dotnet run                            # 기본: 10인 (224×224 자동), 다른 세력 AI = standard, 전장의 안개 켬
dotnet run -- --players 2             # 인원 (2 ~ 24). --map 을 안 주면 인원에 맞춰 맵 크기 자동
dotnet run -- --map 160x120           # 맵 크기 직접 지정 (48 ~ 512)
dotnet run -- --seed 42               # 다른 지형
dotnet run -- --ai passive            # 적 AI 선택 (standard / passive / none)
dotnet run -- --nofog                 # 안개 끄기 (테스트)
dotnet run -- --map flat4             # 고정 지도 (assets/maps/*.json) — flat4 = 4인 평지 테스트 맵
dotnet run -- --spectate --players 10 # 관전: 모든 세력이 AI (0~9 시점, Tab 현황판, [ ] 최대 8배속)
dotnet run -- --theme none            # 이미지 세트 끄기 (도형 그림). 기본 ww2
dotnet run -- --demo                  # 시연: 본진 앞 교전
dotnet run -- --replay replays/xxx.kdr.json      # 리플레이 보기 (게임 종료 시 자동 저장)
dotnet run -- --verify-replay replays/xxx.kdr.json   # 창 없이 재현 검증
dotnet run -- --selftest              # 창 없이 로직 검증 + 성능 측정
```

## 배포 빌드
```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1   # → dist\KoreanDivision.exe + dist\assets\ (.NET 설치 불필요)
```
`dist\assets\data\balance.json` 을 고치면 다시 빌드하지 않고 밸런스를 바꿀 수 있다.

## 게임 방향 (2026-10-19 확정)
- **구조**: 스타크래프트식 선형 미션 + 전략 지도 분기 (분기 · 캠페인 계층은 아직 미정).
- **자원 없음**: 화폐 · 연료 · 인구 · 재고를 쓰지 않는다.
- **생산 없음**: 미션마다 주어진 유닛만 전투. 병영 · 공장 등 생산 건물 없음.
- **보급 (탄약)**: 유닛마다 한도가 따로 있다. 교전 중 소모, 비전투 상태 (10초간 사격 · 피격 없음) 에서 한도의 1%/초로 회복, 아군이 점령한 도시 8칸 이내면 ×3.
- **보급 마차**: 비무장 수송 유닛. 주변 6칸 아군의 탄약 회복 ×3 (그만큼 적재량 감소), 아군 도시 근처에서 다시 싣는다.
- **엄폐 · 참호**: 엄폐 중인 유닛은 맞을 확률과 받는 제압이 줄어든다 (참호 명중 ×0.5 · 제압 ×0.5 초기값). 보병 개인 참호 (명령 '참호' D, 8초, 움직이면 해제) 구현, 참호선은 다음 단계.
- 도시 · 점령지는 목표 · 보급 가속용으로 남긴다. 미결 사항은 `docs/ROADMAP.md` §0-1.

## 규칙 (수치는 `assets/data/balance.json`)
- **인원**: 2~16 (기본 10). 기본 4인 맵 `corners4`: 한국(좌상, 기본) · 일본(우상) · 봉천 군벌(좌하) · 러시아 백군(우하), 시작 화면이나 `--faction` 으로 세력 선택.
- **시작 병력**: 지도 파일 `units` 배치가 있으면 그대로, 없으면 `start.force` (세력마다 대도시 앞에 보병 중대 2 · 기관총/대전차 중대 1 · FT 전차 3 · 보급 마차 2).
- **점령 시설** (파괴 불가 — 주변에 전투 유닛을 두면 점령, 적과 함께 있으면 경합): 대도시 (4×4, 40초, 세력마다 하나 들고 시작) · 소도시 (4×4, 20초) · 마을 · 광산 · 유전 (3×3). 도시 (대도시 · 소도시) 근처 8칸 = 아군 탄약 회복 ×3.
- **패배**: 도시 (대도시 · 소도시) 를 하나도 갖지 않거나 유닛이 전멸했을 때. 대도시를 모두 잃으면 30초 뒤 위치가 드러난다. 살아남은 세력이 모두 서로 동맹이면 공동 승리.
- **전장의 안개**: 지형과 중립 시설 위치는 처음부터 보임. 유닛은 시야 안에서만.
- **정적 팀** (외교 없음): 지도 파일 `factions[].team` 이 같은 세력끼리 고정 동맹 (교전 없음 · 시야 공유 · 탄약 회복 · 공동 승리). 팀 정보가 없으면 모두가 서로 적. 실행 중 바뀌지 않는다.

## 조작
| 입력 | 동작 |
|---|---|
| 좌클릭 / 드래그 | 선택 / 범위 선택 (Shift: 추가 · 토글), 더블클릭 · Ctrl+클릭: 화면 안 같은 종류 |
| 우클릭 | 이동 · 적이면 공격 |
| M S A P H | 이동 · 정지 · 공격(적 지정 = 공격, 땅 = 어택땅) · 정찰 · 사수 |
| D | (보병) 개인 참호 파기 — 8초 뒤 엄폐, 움직이면 해제 |
| 화면 가장자리 · 방향키 · 휠 드래그 / 휠 | 화면 이동 / 확대 · 축소 |
| Ctrl+1~9 / Shift+1~9 / 1~9 | 부대 지정 (최대 100기, 넘으면 무작위 100) / 부대에 추가 / 선택 (두 번: 화면 이동). 0 은 예약 |
| Enter | 명령어 입력: `show all maps` (지도 공개 치트) · `spawn <유닛> [수]` · `fps` · `zoom <배율>` · `select <유닛>` · `volume 0~100` · `mute` · `help` — 치트는 리플레이에 기록 |
| F3 · [ ] · G · Space · Esc | 일시정지 · 게임 속도 · 격자 · 선택으로 이동 · 취소 |

## 구조
```
Program.cs                 진입점, 명령줄 인자, 다시 시작 루프
assets/data/balance.json   ★ 모든 밸런스 수치 (유닛 · 점령 시설 · 무기 · 장갑 · 시작 병력 · 보급 · 엄폐)
src/Config.cs              화면/맵/조작/시뮬레이션 상수
src/Game.cs                입력 · 선택 · 커맨드 카드 · 그리기 총괄
src/RtsCamera.cs           카메라
src/SelfTest.cs            헤드리스 테스트 (+ src/Tests/)
src/Core/                  게임 로직 (렌더링 비의존, 결정적)
  Defs.cs                  balance.json 로더 · 검증
  Entity.cs                Entity(공통) · Building · Faction
  Unit.cs                  Unit (무기/장갑 슬롯) · MoveGroup
  GameMap.cs               타일 맵 · 대칭 생성 · 건물 점유 · 시야선
  FlowField.cs             흐름장 경로 탐색 (명령 1회당 1번, 그룹 공유)
  World.cs                 엔티티 · 이동 · 공간 해시 충돌 · Step
  WorldCommands.cs         ★ 명령 API (사람과 AI 가 공용)
  WorldCombat.cs           표적 획득 · 추격 · 명중 판정 · 피해
  WorldCapture.cs          점령 · 승패
  Supply.cs · SupplyUnit.cs   탄약 회복 · 보급 마차 · 개인 참호 · 엄폐 배수
  WorldTeams.cs              정적 팀 (지도 factions[].team) — Allied · IsHostile
  Commands/                GameCommand(명령 객체 · JSON) · Replay(리플레이 파일 · 검증)
  FogOfWar.cs              전장의 안개 (세력별 미탐색/탐색/시야, 건물 기억)
  Commands.cs              유닛 커맨드 카드 정의
src/AI/                    IAiController · AiRegistry · Passive/Standard AI
src/Render/                MapRenderer · BuildingRenderer · UnitRenderer · Minimap · FogRenderer
src/UI/                    Hud · Ui(폰트) · Icons · Palette · Strings(모든 고정 한글 문자열)
```

## 확장 가이드
- **새 유닛 / 건물 / 무기 / 장갑**: `balance.json` 에 항목 추가 (hotkey · slot 지정) → 버튼 · 단축키 · 툴팁 자동 생성. 건물의 `produces` 에 유닛 키를 넣으면 생산 가능.
- **새 AI**: `IAiController` 구현 → `AiRegistry.All` 에 등록 → `--ai 이름`. 규칙: `World` 의 명령 메서드로만 조작, 적 정보는 `Fog.CanSee` 로 보이는 것만.
- **그래픽**: 테마 `assets/themes/ww2/theme.json` (건물 · 지형 = `tools/make_ww2_sprites.py`, 다른 항목은 보존). 유닛 = `../game-tools/image` 의 복셀 3D → 쿼터뷰 렌더 → 픽셀 후처리 (`tools/vox_*.py`, 64×80 · 5방향 + 반전 · 서기/걷기 2/조준/발사/대기동작, 칸별 발 위치 `*_anchors.json`), `python export.py` 로 테마에 복사. 같은 그림을 쓰는 유닛은 `{"alias": "unit.infantry"}`. 0.5배 미만으로 축소하면 유닛은 세력 색 점.
- **효과음**: `tools/make_sfx.py` → `assets/sounds/ww2/` (`sounds.json`: 키 → 파일 · 음량 · 동시 재생 수 · 최소 간격).
- **고정 한글 텍스트**: `Strings.cs`(S)에 const 로 추가 (폰트 아틀라스에 포함되는 글자). 데이터 파일의 이름 · 설명 글자는 자동 포함.
