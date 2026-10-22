# T34a~e 전술 AI 3C/3D 공통 사양 (spec, 2026-10-22, 코드 변경 없음)
근거: TACTICAL_AI_STAGE3_REVIEW_2026-10-19 (§3 S2 S5 S6 S9 S10 S11 S12 S13 S15, §4 묶음, §5-2 합격표). 사용자 보류 해제 2026-10-22 ("전술 AI 3C/3D 와 기병 측면 강도" 해제, 지휘자 전달). 3A/3B/3B2 는 끝(ai3b2 f74a675) -> 이미 있는 것(manReplace, counterOdds, defendRepick, fixPush, 사건 투입)은 다시 만들지 않는다.

## 1. 순서 (전부 `ai3b2` 위 스택, 병합 없음, 각 <=150k 토큰)
T34a 제압 문턱+엄호 철수 -> T34b 틀 선택 -> T34c 제병 협동 -> T34d 기병 측면 강도 -> T34e 유휴 진단+난이도 겹침. 브랜치 `kd/t34a-suppthr` 는 ai3b2 에서, 이후 각자 직전 카드 브랜치에서 분기. T34d 는 T34a 만 선행(독립 가능, 병렬 허용 단 무거운 작업 동시 2개). 3D 대량 시험(S15, 3000기)은 카드 밖 (질문 Q-G3).

## 2. 규칙 = 데이터 키, 기본 OFF, 겹침 이름 (KD_AI_OVERLAY=<이름>, ai_profiles.json "overlays", 3B 방식)
| 카드 | 켜는 겹침 | 개별 겹침 | 끄는 겹침 |
|---|---|---|---|
| T34a | `3c` | `3c_suppthr` `3c_coverwd` | `no3c` |
| T34b | `3c` | `3c_tplflank` `3c_tplassault` | `no3c` |
| T34c | `3c` | `3c_assaultres` `3c_mgflank` | `no3c` |
| T34d | `3c` | `3c_cavflank_lo` `3c_cavflank_hi` | `no3c` |
| T34e | `3d_easy/normal/hard` (S1~S9 키 켜고 끄기 + 판단 간격) | - | - |
기본 프로필은 전부 불변 -> detcheck 골든 불변이어야 함(바뀌면 키가 새고 있는 것). 키는 기존 절 안에 추가(tactics / strategy / cavalry), 새 절 금지.

## 3. 측정 프로토콜 (모든 카드 공통; docs/procedures/measurement.md 가 우선)
- 같은 실행 파일 A/B: `KD_AI_OVERLAY=<겹침>` 대 겹침 없음(또는 `no3c`). BASELINE_CURRENT 의 옛 블록에 의존하지 않음(종료선이 70 -> 50% 로 재기준 중, T31b: `*_obs` 사본 50% 권고, 미결 Q-G1). 시나리오는 A=ai_bn_meeting B=ai_bn_assault C=ai_kr_vs_ft_1000 (필요 시 D=ai_kr_vs_ft_2000) + 카드별 bn_campaign 행. 전차·대전차소총병 제외(사용자 2026-10-19).
- `Program --tactics-obs <id> --seed N --seconds 600`, 시드 1~8 x 시나리오 3 = 팔당 n=24 이상. 승률 주장은 n>=24 에서만, 차이 <20%p(2 SE) 는 "변화 없음"으로 적는다 (n=8 에서 노이즈 17%p, T15). 교환비(K/E)·시간초과·지표 개수는 판 수 명시. 600초 시간 상한·무거운 작업 동시 2개 이하.
- 필수 통과: 빌드 0오류, `--detcheck` 골든 일치, `--snaptest`, 자기 묶음 `KD_SELFTEST_ONLY=<카드 묶음>`, 기존 묶음 s3a ai3b ai412 pursuit2 supp tactical drill cavcmd 회귀. 비용: `--stress ai players=4 n=1000` avg <= 0.038 x 1.3 ms/틱.
- 퇴행 금지(공통): A/B/C 승 합계가 겹침 없음 대비 -20%p 이내, K/E 악화 <=0.05, 시간초과 증가 <=2판. 켠 겹침이 이기면 "켬 권고", 아니면 기본 OFF 유지(사용자 결정 전 기본값 변경 금지).

## 4. 핫스팟 겹침 (kd/t24-s1s3 · kd/t31a-regroup, main 대비 diff 기준)
| 파일 | T24 | T31a | 우리 카드 | 비고 |
|---|---|---|---|---|
| AI/Tactical/TacticalAi.Company.cs | O | O | a b d | 가장 위험. 신규 코드는 별 파일 `TacticalAi.Cav3C.cs` `TacticalAi.Coop3C.cs` partial 로 분리하고 Company.cs 는 호출 한 줄 |
| AI/Tactical/AiProfile.cs | O | O | a b c d e | 키 읽기 줄만 추가, 같은 위치 줄 충돌 예상 -> 병합 시 키 목록 끝에 추가 |
| AI/Tactical/TacticalAi.Artillery.cs | O (observerMinEnemyDist) | - | c | 관측자 조건 건드리지 말 것 |
| AI/Tactical/TacticalAi.Manoeuvre.cs / TacticalState.cs | - | O | b c(필드1) | 기동 제대 지정을 읽기만 |
| Core/TacticDrill.cs | O (chargeMinSuppression 끔 기본0) | - | a b | 돌격 제압 문턱 = T24 ③ 키와 의미 겹침, 같은 키 재사용 |
| Core/CommandRanks.cs | O (actingMinRank) | - | e(읽기) | S12 대행은 T24 ①이 이미 함 -> 카드 없음 (Q-G2) |
| Core/Pursuit.cs | - | O | a | 후퇴 재편은 T31a 소관, a 는 엄호 분대만 |
| assets/data/tactics.json · ai_profiles.json · detcheck.golden | O | O | 전부 | json 키 끝에 추가, golden 은 건드리지 않음 |
| Snapshot.cs FV / Replay.cs | 56+ | - | c 만 FV+1 | 아래 FV |

## 5. FV 필요 목록 (FV_RESERVATIONS.md 는 지휘자 관리 - 여기서 수정 안 함)
- T34c: Snapshot FV +1 (TacRegiment `AssaultRsvTick` 1필드, ObjCodec 자동 저장, SnapshotSelfTest.Audit). Replay 변경 없음. 번호는 T24(56 이후)/T10(57)/rail 뒤 지휘자가 지정 (ai3b2=55 위).
- T34a b d e: FV 없음 (프로필 키 + 기존 State). 새 저장 상태가 필요해지면 중단하고 보고.
- AE2 난이도 배수 (FV+1/Replay+1 후보)는 T34e 범위 밖.

## 6. 기병 측면 강도 (T34d) - "강도"의 정의 제안, 기존 키만
현행 기병 돌격 문턱: 일반 표적 minOdds 2.0, 측면·후방 접근 minOdds x flankOddsMul 0.7 = 1.4, 후퇴 안 하는 묶음은 평균 제압 >= flankMinSupp 0.3, 정면은 frontSuppOk 0.5 / frontWeakOdds 3, 우회 flankOffset 14칸, 재돌격 대기 rechargeCooldownSec 120 (strictCharge), 소규모 minRiders 10 이하는 차장, 프로필 strategy.cavalry (0.15 미만이면 예비). **제안: 측면 강도 = (flankOddsMul, flankMinSupp, flankOffset) 세 값이 정하는 "측면 돌격을 얼마나 쉽게 거는가"; 단계 lo/hi 두 겹침**: lo = flankOddsMul 1.0 · flankMinSupp 0.45 (신중), hi = flankOddsMul 0.5 · flankMinSupp 0.15 (적극). 기본(0.7/0.3) 이 중간. 돌격 충격 값(chargeStun, 쿨다운, 제압 +300, 속도 x1.8)은 사용자 2026-10-21 "현재 그대로" -> 만지지 않음.
### 질문 (<=8, 모두 DECISIONS_PENDING 에 한 줄씩)
Q1 "강도" 를 위 세 키로 정의해도 되는가 (그 밖에 minOdds, minRiders, rechargeCooldownSec 도 강도에 넣을지)? Q2 목표 지표: 기병 생존 >=50%(C2 CAV_*)와 기병 대 기관총 교착 0/20(plan)을 그대로 쓸지, D07(소총 대 기병 10판 중 9 시간초과)용 별도 지표를 둘지. Q3 D07 은 "통합 측정 뒤 결정" 이고 T12/T15 에서 퇴행 없음 -> 이번 측정으로 D07 선택지(A 기병 재돌격 완화 / B 소총 추격 / C 둘)를 닫아도 되는가. Q4 사용자 2026-10-19 결정 8 (무능한 장교일수록 정면 돌격): 코드(RashCavalryFront)는 `strictCharge=false` 일 때만 켜짐 - 기본 strictCharge=true 라 현재 꺼짐인데 의도대로인가. Q5 측면 강도를 AI 프로필(세력별)로 둘지 전역 cavalry 절 값으로만 둘지. Q6 측정 시나리오에 기병 포함 혼성(기병 30 + 소총)을 추가해도 되는가(기존 n18 · CAV_*만으로는 측면 도착 생존 n 부족). Q7 하마 사격(mountRangeFrac, 이미 있음)을 강도 측정에서 끄고 비교할지. Q8 승패 퇴행 허용폭 -20%p 가 기병 포함 시나리오에서도 맞는가.
### 카드 밖 질문 (T34 전체)
Q-G1 측정 종료선: 50% 사본 `*_obs` 로 모두 옮길지(T31b 권고)/70% 유지. Q-G2 S12 지휘 대행(사용자 2026-10-19 결정 13: 엔진 규칙·저장 변경)은 T24 ① 로 일부 충족 - 별도 카드를 만들지. Q-G3 3000기 대량 시험(S15)·대포병 사격(결정 11 3단계 포함, AI 코드에 아직 없음)을 T34f 로 발주할지. Q-G4 난이도 인지 지연(0/2/4초)은 새 상태가 필요할 수 있음, AE2 와 합칠지.
