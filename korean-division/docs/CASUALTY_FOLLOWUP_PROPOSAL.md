# 사상자 속도 후속 제안 (T31a~c, 2026-10-22, spec) — 전부 '제안', 기본 끔, 구현 전 사용자 승인

## 배경 (ops/DECISIONS.md J, 카드 T06)
D19 C(원거리 x0.7 + 후퇴 x0.75)는 수용 기준 미달 -> 되돌림(wip/casualty2 0722493). 측정(D19 현상값): 평지 절반 손실 main 175초(목표 2~3분 이미 충족) / 대대전 손실 47~68% (평균 ~60%, 목표 20~40%) / 항복 0건.
원인 가설: ① 후퇴한 중대가 재정비(regroup) 뒤 재투입 ② 미션이 진 쪽 70% 손실에서 끝남 ③ 항복 조건이 중대 단위로만 너무 엄함.
조사 결과 (이미 있는 것 — 새로 만들지 말 것):
- 재정비: `tactics.json` regroup {enabled, minSec 20, checkSec 5, rejoinStrength 0.5, rejoinSupp 0.2, rejoinOdds 1.0, reinforceFrac 0.15, supportRadius, supportMin} — `TacticalAi.Company.cs` BeginRegroup / Regrouping / RejoinOk (스냅샷 필드 RegroupUntil·RegroupCheck·RegroupAlive 이미 있음, FV35).
- 철수(목표 포기): AiProfile.WithdrawAt / 미션 aiWithdrawAt (`TacticalAi.Strategy.cs`:97~111), d.Withdrawn 후엔 방어 자세.
- 미션 종료: AI 대대전 미션(`assets/missions/ai_bn_meeting|assault|ai_kr_vs_ft_1000.json`)은 objective `eliminate percent 70` + failure `losses percent 70` (`WorldMission.FailureTriggered`, 코드 변경 없이 JSON 만). 즉 '70% 종료'는 측정용 시나리오 데이터.
- 항복: `src/Core/Surrender.cs` (N79b, FV43) + `balance.json` surrender* 키 11개 + 시험 `KD_SELFTEST_ONLY=surr` (SurrenderSelfTest.cs) + TacticsObs `[항복]` 줄. 조건: 중대 사상 80%+ AND 남은 10명 이하 AND 제압 0.85+ 10초 연속 AND 퇴각 불가(사방 포위/3방향 막힘) -> 2초마다 5%. 중대 단위뿐, 대대/세력 단위 없음.

## T31a 재투입 제한 — 옵션 (데이터 키 위치: tactics.json "regroup")
- A 문턱 상향: rejoinStrength 0.5 -> 0.7 (병력 70% 미만이면 재투입 안 함, 현재는 50%). 키만, 코드 0. 한 번 후퇴한 중대는 증원 없이는 사실상 복귀 못 함.
- B 영구 예비: 신규 키 regroup.maxRejoins (기본 -1=무제한). 횟수 저장 = TacGroup 새 필드 = 스냅샷 FV 증가(ai3b2 FV55 예약과 충돌). 비권장, 또는 ai3b2 뒤.
- C 무상태 대용: regroup.noRejoinBelow (기본 0=끔): g.Alive < Initial x 값이면 재정비 대신 Fallback/Reserve 과제로 전환. 새 저장 필드 없음 (Initial·Alive 는 기존). 권장 A+C.
- 측정 지표: 대대전 평균 손실, 후퇴 중대의 재투입 횟수(Stats.RegroupRejoins), 시간 초과율(D19: meeting 1/8 -> 3/8 악화였음, 3/8 초과 시 기각).

## T31b 미션 종료 조건 — 옵션 (제안 문서 + 시나리오 JSON, 엔진 코드 0)
- A 현행 유지 (70%): 지표 20~40% 는 '시나리오 종료선' 이 아니라 '시간 10분 시점 손실' 로 재정의 (BASELINE 에 시점별 손실 표기).
- B 측정 시나리오만 종료선 하향: eliminate/losses percent 70 -> 40~50. 게임 미션(캠페인)은 불변.
- C 새 종료 종류 'rout' (진 쪽 AI 세력이 철수/Withdrawn 인 그룹 비율 >= X 면 승패 결정): 코드 + 미션 스키마 = 새 메커니즘 -> 사용자 승인 필요, 이번 범위는 설계만.
- 산출: 옵션별 시드 8 시나리오 수치표 (측정만, 판단은 사용자).

## T31c AI 항복 — 옵션 (balance.json combat 규칙, 키 이름은 기존 surrender* + 신규)
- A 문턱 완화(키만): surrenderCasualty 0.8 -> 0.6, surrenderMaxLeft 10 -> 20, surrenderChance 0.05 -> 0.15. 중대 단위, 퇴각 불가 조건 유지(AI 퇴각 가능하면 항복 안 함).
- B 'AI 퇴각 불능 조건 완화' 신규 키 surrenderRetreatBlockedMul (기본 1.0=현행): 조건 3 의 막힘 판정 반경/각 완화.
- C 세력 단위 항복(신규 메커니즘, 스냅샷 필요): 세력 전체 병력 <= X 이고 철수 중 -> 전 중대 Surrendered. 비권장(미요청), 질문으로만.
- 안전: 기본값 = 현행(항복 거의 0), 새 키는 전부 기본이 현행과 동일 -> 골든 불변. 켜는 값은 측정 후 사용자가 결정.
- 한계: 항복자는 `Surrendered`(포로)로 집계, 미션 점수에 영향(MissionResults.cs)이므로 변경 시 플레이어 쪽 x0.3 유지.

## 핫스팟 겹침 (WIP 와)
| 파일 | 건드리는 카드 | 겹치는 WIP |
|---|---|---|
| TacticalAi.Company.cs · TacticalState.cs · Snapshot.cs | T31a (B 옵션만 FV 증가) | ai3b2(T05, FV55), orderq(T04) |
| tactics.json | T31a | ai3b2 가 프로필 키 추가 시 |
| Defs.cs · balance.json · Surrender.cs · SurrenderSelfTest.cs | T31c | 겹침 거의 없음 (breach 는 Defs.cs 다른 구역) |
| assets/missions/ai_bn_*.json | T31b | 없음 |
