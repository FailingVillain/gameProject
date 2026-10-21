# 통신 · 지휘 구조 알파 + 난이도 구조 — 구현 계획 (2026-10-22, 사용자 확정 사항: ROADMAP_LONG '통신 · 지휘 구조' · '통신 확정' · '통신 규칙 확정')

1. **데이터 모델**: `assets/data/difficulty.json` 한 곳 — `default` 키 + `levels.easy|normal|hard` 마다 `comms` (wireMinSec 1 · wireMaxSec 3 · courierMinSec 10 · courierMaxSec 20 · wireCut · courierCapture) 와 `ai` (accuracyMul · ammoMul · reinforceMul). 로더 = `src/Core/Difficulty.cs` (`DifficultyDefs.Current`, props.json 과 같은 방식). AI 배수는 모두 1.0 (값 = 사용자 승인 뒤).
2. **난이도 키**: `World.Difficulty` (문자열, 저장 · 체크섬). 미션 · 캠페인 · 교전 테스트가 `--difficulty easy|normal|hard` · `GameOptions.Difficulty` · `CampaignState.Difficulty` 로 받는다. 리플레이 머리말에도 실어 재현.
3. **명령 흐름**: 중대 · 유닛 명령 = 지금 그대로 즉시 (사용자: 중대 직접 명령 = 즉시 전달). 대대장 · 연대장 앞 명령 = 새 명령 `DispatchOrderCommand(Faction, 받는 대대/연대, 수단, 안의 명령)` → 실행 시 `World.Dispatches` 에 '전달 중' 항목이 생기고, 도착 틱에 안의 명령을 그 제대의 현재 구성원으로 바꿔 실행 (리플레이 = 발송 명령만 기록, 도착은 저장된 상태에서 결정적).
4. **새 상태**: `Dispatch { Id, Faction, Target(대대 Id · 연대 번호), Via(Wire|Courier), SentTick, ArriveTick, State(Transit|Delivered|Lost), LostReason, Order(JSON) }` + `nextDispatchId` + 난이도 키 — ORG_ 끝 `WriteComms` 에 저장 · `HashComms` 체크섬 · `[SnapExt]` 로 Audit. FormatVersion · Replay 버전 = 병합 시점 main+1.
5. **지연**: 유선 = 해시 난수 wireMin~Max 초, 전령 = 조각 ②에서 실제 기마 전령 유닛 이동 (그 전까지 courierMin~Max 초 시간 모형). 연대장 → 대대장 재하달 = 연대 지휘소에서 다시 Dispatch.
6. **분실 · 알림**: 받는 제대 소멸 · 지휘관 없음(인계 중 아님) · 전령 사로잡힘 · 유선 끊김(발송 시점) → State=Lost, 10초 동안 목록에 남김 → UI 가 반드시 알림 (조각 ①은 상태 + `CommsEvents` 목록, 조각 ③에서 화면 알림). 전령이 사로잡히면 내용 노출 = 그 적 세력의 `Exposed` 목록 (AI 가 읽을 수 있음).
7. **기본값 보존**: 기존 명령 · 기존 시험은 Dispatch 를 쓰지 않으므로 어떤 난이도에서도 결과 불변. 기본 난이도 = easy (유선 안 끊김, AI 배수 1.0 = 현재 동작).
8. **조각 ① (이번 병합)**: difficulty.json + 로더 + World.Difficulty + DispatchOrderCommand + 시간 모형 전달 (유선/전령) + 분실 판정(제대 소멸) + 저장 · 체크섬 · 리플레이 + `comms` 시험 묶음 (지연 범위 · 분실 · 저장 왕복 · 리플레이 재현 · 쉬움 = 현재 동작).
9. **조각 ②**: 기마 전령 유닛 (사단 사령부/연대 지휘소 → 받는 지휘관까지 말 이동, 적 근접 시 사로잡힘 → 분실 + 노출) · 유선 (지휘소 사이 전선 칸, 포격 · 적 접촉으로 끊김 — normal/hard 만, 통신병 수리).
10. **조각 ③**: 연대 지휘소 (건물 · 연대 번호) · 대대 간이 지휘 테이블 (대대장이 멈춰 설치) · 연대장 → 대대장 재하달 · '전달 중' 표시 (깃발 위 아이콘 + 남은 시간) · 분실 알림 · 난이도 선택 UI.
11. **조각 ④**: 전술 AI 대대/연대 명령을 Dispatch 로 (같은 규칙), AI 는 노출된 아군 명령 · 적 명령 정보를 블랙보드로만 읽음.
12. **결정성**: DetMath · `Roll(id, salt)` 해시 난수 · 명령으로만 상태 변경 · 목록은 Id 순, 딕셔너리 열거 없음.
13. **핫스팟**: 새 코드는 `src/Core/Comms*.cs` · `Difficulty.cs` · `src/Tests/CommsSelfTest.cs`. 기존 파일은 등록 줄만 (GameCommand JsonDerivedType 1줄 · Execute 분기 1줄 · World.Step 1줄 · Organisation Write/Read/Hash 각 1줄 · Snapshot/Replay 버전 · SelfTest 묶음 1줄 · Program `--difficulty` 1줄).
14. **로드맵만 (범위 밖)**: 무선 모스 · 신호탄 · 전서구 · 수기 · 신호등.
15. **측정**: 기본값에서 detcheck 골든 불변이어야 함 (조각 ①) — 바뀌면 원인 확인 전 병합 안 함.
