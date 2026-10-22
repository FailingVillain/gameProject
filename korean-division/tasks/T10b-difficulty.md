# T10b 난이도 소비처 (전령 사로잡힘 · 유선 끊김) + AI 배수 적용 설계
- id: T10b | 역할: sim | 등급: D上 H1 (선택 메모: R2 S Wlight)
- 선행: T10 (`kd/t10-comms-b` 1c91c96) | 후행: AI 배수 구현 카드 (사용자 결정 뒤)
- 만질 파일: Comms.cs · Difficulty.cs · difficulty.json · CommsSelfTest.cs (핫스팟 Snapshot/SelfTest/Defs/Combat/Replay 안 건드림)
- 브랜치: `kd/t10b-difficulty` (kd/t10-comms-b 위 스택) | 상태: wip-ready | 담당 에이전트: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- D15 A (2026-10-22): 쉬움 전령 사로잡힘 끔 · 보통 AI 1.0 · 어려움 후보는 카드에서, 보류. ops/DECISIONS Q1 → T10b 발주.
## 조사 결과: 난이도는 리플레이 · 스냅샷 안전
- `World.difficulty` = `[SnapExt]` + `WriteComms` 저장 + `HashComms` 체크섬 (기본 easy 면 생략), 리플레이 머리말 `Difficulty` (Replay.cs:135 틱 0 전 적용, 스냅샷 시작이면 스냅샷 값). → 시뮬이 `DiffLevel` 을 결정적으로 읽어도 됨. 규칙 수치 (difficulty.json) 는 balance.json 과 같은 데이터 파일 취급.
## 구현 (소비처)
- `difficulty.json` `commsRules` (기본 `enabled:false` = 기존 동작, 수치 초안 · 사용자 승인 전): captureRadius 8 · captureChance 0.25 · checkSec 1 · cutRadius 15 · cutChance 0.5.
- 전령 (시간 모형): 출발 = 보낸 세력의 첫 살아 있는 사령부 건물, 도착 = 받는 대대 현재 중심, 진행률 선형 보간 위치에서 checkSec 마다 적 근접 + 해시 난수 → Lost(CourierCaptured). `courierCapture=false` (쉬움) 면 판정 안 함. 사령부 없으면 판정 안 함.
- 유선: 발송 순간 대대 중심 cutRadius 안 적 + 해시 난수 → 즉시 Lost(WireCut). `wireCut=false` (쉬움) 면 안 끊김.
- 새 상태 없음 (기존 Dispatch · DispatchLoss 만) → FormatVersion 57 · Replay 22 그대로, 번호 예약 불필요. 사로잡힘 시 '노출 (Exposed)' 목록 · 실제 기마 전령 유닛 · 전선 칸 · 통신병 수리는 조각 ② (미구현).
## AI 배수 적용 설계 (구현 안 함 — 결정 필요)
- 문제: 배수는 "AI 세력" 에만 걸려야 하는데 `Faction.IsAi` 는 Game.cs(자유전) 에서 화면 쪽이 정함 → 자유전 리플레이에 없음 (TrenchAssault 주석: 시뮬이 IsAi 를 보면 안 됨). 단, Surrender.cs:39 가 이미 IsAi 를 읽음 (선례 · 잠재 위험, 별건).
- 안 A★ (추천): 세력별 `DiffScaled` 플래그를 틱 0 전에 정해 FactionSnapshot 저장 · 체크섬 + 리플레이 머리말 `AiFactions` 목록 (FV +1 · Replay +1, 병합 시점 번호). 시뮬은 이 플래그만 읽음: 정확도 = 명중 판정 (Combat*, 핫스팟), 탄약 = 유닛 생성 시 탄약 초기값. 자유전 · 미션 모두 덮음.
- 안 B: 미션 적재 때 배수를 미션 JSON 의 세력 항목에 구워 넣음 (미션 JSON 은 이미 머리말에 있음 → 리플레이 번호 불필요, 스냅샷은 세력 값 저장 필요 = FV +1). 미션만 덮고 자유전 제외.
- 공통: 증원 배수 (reinforceMul) 는 증원이 AI 컨트롤러가 내는 명령이면 컨트롤러 쪽 (시뮬 밖) 에서 곱해도 결정성 안전 → 시뮬 변경 없음. 정확도 · 탄약만 시뮬 플래그가 필요.
## 결과 (에이전트가 끝에 3~5줄만)
- 검증: 빌드 오류 0 (변경 파일 경고 0) · comms 18/18 (새 5: D15 값, 꺼짐=기존, 보통/어려움 사로잡힘 · 쉬움 전달, 보통 유선 끊김 · 쉬움 전달, 저장 왕복 뒤 같은 틱 사로잡힘) · fieldops · replay · snapshot · savecompat PASS · detcheck GOLDEN 일치 19501757040f0d62 (골든 불변: 기본 commsRules 꺼짐 + 골든 게임은 Dispatch 를 안 씀) · snaptest quick FAIL 0 (81 OK).
- 기존 실패 (내 변경 무관): `KD_SELFTEST_ONLY=gapfill` 단독 실행 시 예외 "Sequence contains no elements" — T10 기준 빌드 (1c91c96) 에서도 같음.
- 결정 필요: commsRules 켜기 · 수치 승인, AI 배수 안 A/B.
