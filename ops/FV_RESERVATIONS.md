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
| t30a-train-move (T30a) | 58 | 23 | rail | wip-ready (863f9d1) | 열차 객체·궤도 이동 |
| t30b-train-weapons (T30b) | 59 | 23 | t30a | wip-ready (ca1cdb8) | 열차 저장 위치 ORG_ 끝→ENTS 끝, 엔티티 참조 태그 3 |
| t30c-freight-objective (T30c) | 59 유지 | 23 유지 | t30b | wip-ready (f5fa49a) | 새 상태 없음(ObjProgress 재사용) |
| gemini-import / t09 / t29 / T19a~d | 확인 필요 | 확인 필요 | gemini-import | - | 캠페인 저장 스키마는 별도(SchemaVersion 2); sim FV 는 안 올림 |
| sfxnew · rerender · face-proto · t27 · t28 · t17 · t08 | 변경 없음 | - | - | - | 표현/설정/옵션 전용 |

규칙: 합칠 때(사용자 지시 후) 한 줄(Snapshot.cs:83 FormatVersion, Replay.cs:22)을 위 표 순서로 순차 재번호하고 골든 재생성.

메모: t29b-on-t09 가 t09-camp-slice 와 t29-campaign-economy-json 을 대체 (병합 시 단독). 캠페인 저장 스키마는 SchemaVersion 2 로 sim FV 와 무관.

후보(미발주): IsAi 를 리플레이 머리말·스냅샷에 포함(AF1) FV+1/Replay+1 · AI 난이도 배수 안 A(AE2) FV+1/Replay+1 — 발주 시 이 표에 먼저 번호를 적는다.

예약(2026-10-22 13:10, 사용자 승인으로 발주): IsAi 리플레이 머리말·스냅샷 복원(AF1) = FV 60 / Replay 24, 기반 main (병합 순서에서 breach2 계열 뒤). AI 난이도 배수 안 A(AE2)는 AF1 뒤 FV 61 / Replay 25 예정.

예약 후보(카드 가정, 13:1x): T32b AI 연막 FV 62 · T32c AI 철조망 FV 63 (둘 다 새 AI 저장 상태가 필요할 때만; Replay 변경 없음). T35(IsAi) 60/24 뒤, AI 배수 안 A 61/25 앞뒤 재번호 필요.
예약 후보: T34c TacRegiment 필드 1개 = FV 64 (Replay 변경 없음), 기반 ai3b2 스택.
예약 후보: T39a 하천 Ford = rail+1 → 이 브랜치 FV 54 (병합 시 재번호), T39b·c 는 새 상태 없을 때 변경 없음.
river-r1 (T39a) FV 54 / Replay 변경 없음, 기반 rail, wip-ready 5b7b9f1. river-r2·r3 는 새 상태 없을 때 변경 없음.

**정정(2026-10-22 14:4x, 통합 리허설 실측):** 병합 순서대로 합치면 실제 번호는 breach2 53/R20 · orderq2 54/R21 · ai3b2 55 · t10 57/R22 · rail 58 · t30a 59/R23 · t30b 60. 따라서 IsAi 복원(AF1, T35) = **61 / Replay 24**, AI 난이도 배수(AE2) = **62 / Replay 25**, T32b/T32c 는 63/64 이후(필요 시). 병합 직전 tools/fv-renumber 로 재번호 (골든은 FV 만으로는 안 변함, 체크섬 변화는 breach2→t24→t25 세 곳뿐 → 새 골든 bef5ffb0 822cdacd, 사용자 지시 시 재생성).

**T39d 다리(river-r4-bridge 9d4b7be, river-r3 위, 2026-10-22 15:0x):** 지형 `Bridge`(바이트 13) 추가로 브랜치 내 FormatVersion 55(river-r1 54 위), Replay 불변. 병합 시 river 스택(rail→r1→r2→r3→r4)은 rail 58·t30a 59·t30b 60·IsAi 61·AE2 62 뒤로 재번호 필요 → 후보 63. 지도 편집기(T33)의 지형 목록에도 Bridge 추가 필요(T33c/T33d 에 전달).
