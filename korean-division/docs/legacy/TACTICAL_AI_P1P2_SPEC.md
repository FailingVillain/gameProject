# 전술 AI P1+P2 구현 명세 (opus 실행용, 코드 변경 없음)
근거: STAGE3_PLAN P1·P2, DESIGN §7, `src/AI/Tactical/`. 한 건으로 묶어 구현. 시뮬레이션: 전차·대전차병 제외.

## 0. 현황 (코드 확인)
- `TacticalAi.Company.cs:LineThink` 공격: 접촉 전 Advance → `d>engageD+3` 이면 Approach(`FiringPosition`=TacMap.PickPosition 한 칸까지 AttackMove 일렬) → 교전 거리 Engage(사수) → `boundAfter` 후 조건(better/pinned/strong/dugIn) 충족 시 `StartSquadBound` (짝/홀 분대 교대, `BoundStep`·`ContinueSquadBound`).
- 약진은 **사거리 안 사격선 전진에만** 쓰임. 접근 구간(사거리~×0.8)은 한 줄 걷기 = P2 문제.
- 연대 `Regiment.cs:AssignAttackRoles`: 점수 최고 대대 1개를 BaseOfFire(정지 사격), 나머지 Manoeuvre. 지형 12판 9승(1단계 11승)의 원인 후보.
- 설정은 `assets/data/ai_profiles.json` `tactics` 절 + `AiProfile.cs:TacticsProfile` (balance.json 아님). 빠진 키=default.

## 1. P1 지형 보병전 회복 (Regiment)
**위치**: `Regiment.cs:AssignAttackRoles`, 호출부 `BattalionThink`(L319 Split).
1. BaseOfFire 지정 조건 강화: `best` 점수가 차점(2위) 대비 `baseOfFireMinGain`(0.35) 이상 앞서고, 기동 후보 대대 병력 합 ≥ 화력 대대 병력 × 0.8 일 때만. 아니면 전 대대 `Role=Line, Split=true` (대대 내 분할: 한 대대 안 `BaseGroup` 1중대=화력, 나머지=기동, L319 기존 경로).
2. ScoreAt 에 `LosWeight` 만 보지 말고 **적 쪽 접근 노출도** 감산: `-ApproachExpose*(1-cover(적방향 3칸 샘플 평균))`. 샘플은 기준점→적 직선 8칸 간격 `TacMap.Cover(c)`.
3. 화력 대대는 평지(칸 평균 엄폐 <0.15, 맵 전체 `TacMap` 평균)에서 현행 유지(회귀 방지): 조건 `terrainRoughness`(맵 평균 cover+high) ≥ 0.2 일 때만 1·2 적용. 거친 정도는 `TacMap` 에 `Roughness()` 추가(칸 순회 고정 순서, 지도 해시 캐시 `stamp` 재사용 = 저장 불필요).

## 2. P2 엄폐 홉 + 돌격보병 (Company)
**위치**: `LineThink` 의 `d>engageD+3f && !onLine` 블록(Approach). 신규 함수 `HopStart/HopContinue`(Company.cs, `StartSquadBound` 옆), `TacMap.PickHop`(신규).
### 2.1 홉 선택 `TacMap.PickHop(from, enemy, hopLen, T, grid, threatR)`
- 후보 = 중대 중심에서 적 방향 `hopLen`(= `hopLength`×사거리, 0.4×) ±`lateral` 안 칸 (PickPosition 과 같은 순회·동점 규칙). 점수:
  `S = CoverWeight*cover + HighWeight*high − ThreatWeight*min(1.5, 위협수/20) + LosWeight*(HasFiringLine(bt,enemy)?1:-0.5) − 0.05*|d_enemy−wantDist 어긋남| + ProgressWeight*(전진량/hopLen)`; `ProgressWeight`=0.6 (엄폐만 좇아 제자리 방지). 위협은 적 사거리(`range`) 기준 `grid.Stats(bt, range)`. 상위 4 → 사선검사 → 최고. 현 위치 점수 +0.2 이상 못 넘으면 직진(PickPosition 기준점 규칙 동일).
- **노출 한계** `maxExposed`: 홉 직선 중 적 사거리 안 & 엄폐 <0.3 인 구간 길이 ≤ `maxExposed`(0.3×사거리). 초과 시 홉 길이를 줄이고, 첫 홉 지점이 사거리 밖(d>range+4)이면 한 홉 더 허용.
### 2.2 상태기계 (`g.SquadBound` 재사용: 0 없음, 1·2 이동 분대 홀짝, 신규 `g.HopMode`=0 약진/1 접근홉)
`Approach(d>range+6)`: 기존 한 줄 AttackMove 유지(안전 거리, 노출 없음) →
`HopStart` (d ≤ range+6 이고 분대 ≥2): 이동 분대(홀 또는 짝) `Move`→홉 지점, 나머지 `Hold`(감시 사격=overwatch, 적 있으면 쏨; `BoundStep` 재사용, `bound=hopLength*range`) →
`HopContinue`(=`ContinueSquadBound` 확장): 이동 분대 도착(2.5칸) 또는 8초 → 교대(`SquadBound=3-…`), 다음 홉 재선택 →
**종료**: (a) d ≤ engageD+1 → Hold·Engage(기존 reached), (b) 제압 supp>0.6 → 현 위치 Hold(엄폐 정지), (c) 이동 분대 손실 >35% 이번 홉 → 홉 중단·`Fallback` 판단은 기존 Retreat 로, (d) 8초 타임아웃, (e) 단일 분대/장교 전용이면 불가 → 기존 접근.
이동 분대는 `marchFire` 허용 안 함(홉 중 정지 사격 불가, 도착 후 사격).
### 2.3 돌격보병 (SMG·착검 역할 = 역할군 기동조)
- 사거리 `smg.range`×`assaultKickoff`(3.0) 이내 도달 전에는 홉(엄폐 연쇄)으로만 접근. 홉 종료 지점이 kickoff 안이면 전원 Assault (`TryAssault` 우회, `NoChargeUntil` 검사). 소총 사수 100 상대 사격 발수 111~379 → 증가 목표.
- 소총 중대 `ChargeRange` 규칙은 불변.
### 2.4 템플릿(N4) 상호작용
- `PickTemplate`: `templates=true` 로 켜는 것은 **P1·P2 측정 통과 뒤 별도 단계**(기본 꺼짐 유지). 켠 상태에서는 공격=화력-기동 틀의 역할군(화력조=BaseOfFire 분대, 사격조=홉 이동/감시 교대, 기동조=최종 돌격)을 홉의 짝/홀 대신 사용: `BoundStep` 의 `i%2` 분산을 `Squad.Role` 기반(사격조는 교대, 화력조는 항상 Hold, 기동조는 측면 홉 후 Assault)으로 교체. 틀 전환 판단 간격은 `World.DecideInterval(c,iv)`, 허용은 `TacticAllowed(CommandLevelOf(c),tactic)` — 부사관 지휘면 홉 없이 기존 접근.
- 틀 OFF 상태에서는 홀짝 분대 방식으로 동작(본 명세 기본).
### 2.5 규칙 준수
돌격 사격(`marchFire`)은 AttackMove 접근만. 중량무기(기관총·맥심)는 보병보다 앞서지 않음: 홉 이동 분대 선두보다 +3칸 앞 금지(`Order` 슬롯 클램프), 화력조는 Hold. 기병 돌격 규칙 불변.

## 3. 데이터 키 (`TacticsProfile` + `ai_profiles.json` default.tactics)
`baseOfFireMinGain` 0.35, `approachExpose` 0.4, `hopEnabled` true, `hopLength` 0.4, `maxExposed` 0.3, `hopProgressWeight` 0.6, `assaultKickoff` 3.0, `hopAbortLoss` 0.35. 프로필별(공격형) hopLength 0.5, (방어형) hopEnabled false. 주석 한 줄씩 파일 머리 설명란에 추가.

## 4. 결정성·저장
- 신규 `TacGroup` 필드: `HopMode`(int), 재선택 캐시는 기존 `PickKind=4`/`PickBase`/`PickResult` 재사용(`TacticalState.cs`). `TacMap.Roughness` 는 지형 순수 함수라 저장 불필요.
- TacticalState 는 스냅샷·체크섬 대상 → 필드 추가 시 직렬화 순서 끝에 추가, **Snapshot 버전 bump**(v23→v24, 사용자 확인 후). 호환 읽기: 없으면 0.
- 난수 금지, 순회 순서 고정, 동점은 낮은 Id. `TestDeterminism`·`TestSnapshotContinue` 통과 필수.

## 5. 측정 (무거운 작업 한 번에 하나, 유닛 ≤4000, 시드 ≤3)
기준선=밸런스 1~5번 적용 main. 모든 비교 `--balance sim` (`KD_SIM_AI`/`KD_SIM_AI_B`), 전후 동일 시드.
1. 지형 보병 200:200 전술 대 옛 battle AI: 12판 = 3시드씩 4회 분할 → 승수·승 시 생존. 평지 12판 동일 방식.
2. 개활지 사수 B 대 AI A 100:100 (3시드): A 손실, B 손실, 소요 시간, A 사격 발수 비.
3. 참호 사수 대 공격 (3시드, 300초): 공격 생존·시간 (회귀 감시, P6 전 단계).
4. 소총 100 대 돌격보병 100 (3시드): 돌격보병 사격 발수, 승부.
5. 미션 승수: yalu_crossing 10시드 = 3+3+3+1 분할 순차.
6. `TacticalSelfTest` 신규: `TestCoverHop`(PickHop 결정성·노출 한계), `TestHopStateMachine`(홀짝 교대·종료 5조건), `TestBaseOfFireGain`. `--stress ai players=4 n=1000` avg ≤ 0.038×1.3 ms/틱.

## 6. 합격 기준
- 지형 12판 승 ≥ 11 (1단계 수준, 현 9), 평지 12승 유지(≥11).
- 개활지 사수 대 A 손실 71~83 → ≤ 55, B 손실 ≥ 3배(2~9 → ≥ 15), A 사격 발수 ≥ 2배.
- 돌격보병 사격 발수 ≥ 2배 또는 소총 대비 승수 개선.
- 참호전·yalu 승수 악화 ≤ 1. 결정성/스냅샷/기존 SelfTest 전부 통과, 비용 +30% 이내.
## 7. 실패 시
홉이 손해면 `hopLength` 0.3·`maxExposed` 0.2 로 줄여 재측정(최대 2회), 그래도 평지 악화면 `hopEnabled` 를 지형(Roughness≥0.2)에서만 켠다. 이전 2단계 실험(사거리 안 교대 약진 1승 11패) 교훈: 이동 분대가 쏘지 못하는 구간을 사거리 밖~경계에 한정.
