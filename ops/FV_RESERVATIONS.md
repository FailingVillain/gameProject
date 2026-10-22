# FV_RESERVATIONS - 저장 형식(FormatVersion)·Replay 번호 예약표 (WIP 스택용, 2026-10-22)

> 이유: WIP 를 병합하지 않고 브랜치 스택으로 두는 동안 번호가 겹침(rail·breach2 둘 다 53). 번호를 올리는 브랜치는 여기 먼저 적는다(지휘자 관리). main = FV52 / Replay19.

| 브랜치 | FV | Replay | 기반 | 상태 | 메모 |
|---|---|---|---|---|---|
| wip/breach2 (T03) | 53 | 20 | main | wip-ready | |
| wip/orderq2 (T04) | 54 | 21 | wip/breach2 | wip-ready | |
| rail (T07) | 53 → 병합/스택 시 재번호(예: 58) | - | main | wip-ready | breach2 와 같은 줄 충돌; Rail 은 enum 끝 바이트 추가 |
| T05 ai3b2 | 55 | - | wip/orderq2 | wip-ready (f74a675) | 새 명령 없음 |
| T24 S1~S3 | 56 이후 | 22 이후 | wip/orderq2 | 진행 중 | T10 의 Replay 22 와 충돌 가능 |
| kd/t10-comms-b (T10) | 57 | 22 | wip/breach2 | wip-ready | breach2 위 스택이라 54~56 을 건너뜀 → 합칠 때 재번호 |
| t30a-train-move (T30a) | 58 | 23 | rail | 진행 중 | 열차 객체·궤도 이동; T30b·c 는 각 +1(59·60) 예정 |
| gemini-import / t09 / t29 / T19a~d | 확인 필요 | 확인 필요 | gemini-import | - | 캠페인 저장 스키마는 별도(SchemaVersion 2); sim FV 는 안 올림 |
| sfxnew · rerender · face-proto · t27 · t28 · t17 · t08 | 변경 없음 | - | - | - | 표현/설정/옵션 전용 |

규칙: 합칠 때(사용자 지시 후) 한 줄(Snapshot.cs:83 FormatVersion, Replay.cs:22)을 위 표 순서로 순차 재번호하고 골든 재생성.

메모: t29b-on-t09 가 t09-camp-slice 와 t29-campaign-economy-json 을 대체 (병합 시 단독). 캠페인 저장 스키마는 SchemaVersion 2 로 sim FV 와 무관.

후보(미발주): IsAi 를 리플레이 머리말·스냅샷에 포함(AF1) FV+1/Replay+1 · AI 난이도 배수 안 A(AE2) FV+1/Replay+1 — 발주 시 이 표에 먼저 번호를 적는다.
