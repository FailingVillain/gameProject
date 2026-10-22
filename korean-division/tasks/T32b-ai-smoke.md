# T32b 전술 AI 연막 수류탄 사용 (기본 꺼짐)
- id: T32b | 역할: sim (AI 규칙 + 시험 + 측정) | 등급: D上 H2 (선택 메모: R3 SM Wheavy)
- 선행: T05 (ai3b2). T32a 와 독립이므로 T32a 와 병행 가능하나 스택은 T32a 뒤에 올림 | 후행: T32c
- 핫스팟/만질 파일: src/AI/Tactical/ (새 TacticalAi.Smoke.cs, AiProfile.cs 키 2개, TacticalAi.cs 호출 한 줄, TacticalAi.TrenchAssault.cs 는 TrenchAssault.cs 의 smokeHook 단계만), src/Core/TrenchAssault.cs (smokeHook 단계), assets/data/tactics.json (새 블록 breachAi + smokeHook), ai_profiles.json (키 + 측정용 겹침), 시험 src/Tests (새 SmokeAiSelfTest), docs/ENGINE_NOTES.md, docs/DECISIONS_PENDING.md 질문 줄. Snapshot 접촉: AI 쿨다운 상태를 새로 저장해야 하면 FV+1 (아래 FV 표).
- 브랜치: `kd/t32b-ai-smoke` (base = ai3b2, T32a 가 먼저 wip-ready 면 그 위; 작업 폴더 kd-develop/kd-t32b) | 상태: wip-ready | 담당: sim (Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 사용자: AI 의 연막·철조망 사용 보류(D18) 해제 (지휘자 전달). D18 원문 "AI 연막·철조망 보류"(ops/DECISIONS.md 2026-10-22 일괄 승인)를 대체.
- 연막 = 원형·크기 고정·지속 후 소멸, 시야·직사 사선 차단, 곡사 무관 (breach 2026-10-19/20 승인). 연막 수류탄은 돌격보병 unit.smokeGrenades(1), 사거리 grenade.range 5.
- 참호 돌격 교리: "AI 역시 이 순서를 사용한다" (2026-10-20 N32); smokeHook 은 이 순서에서 연막이 들어갈 자리.
## 범위 밖
신규 병종·수류탄, 연막탄(박격포 지점 포격 '연막') 의 AI 사용 (질문 Q1), 바람·번짐, 플레이어 자동화, 철조망 절단 AI (T32c), 기본값을 켜는 것 (측정 뒤 사용자 결정), IsAi 의존 코드.
## 설계
- **결정성 원칙 (AF1)**: 시뮬 코드는 Faction.IsAi 를 읽지 않는다. AI 는 기존 `ThrowSmokeCommand`(리플레이 `smokegrenade`)를 CommandLog 로 내고, 단계 자동화는 명령의 `Ai` 플래그(TrenchAssaultCommand 가 이미 가짐)로만 구분 → 자유전 리플레이는 IsAi 복원(FV60/R24) 없이도 일치해야 함. 켜고 끄는 것은 프로필 키 + tactics.json 데이터.
- **키 (모두 기본 꺼짐)**: ai_profiles `tactics.smokeAi: false` · tactics.json `trenchAssault.smokeHook: false`(참호 돌격 순서 안 연막 단계 사용). 측정용 겹침 `"smoke_on": {tactics:{smokeAi:true}}`, `"smoke_hook_on"`.
- **초안 값 (tactics.json `breachAi`)**: smokeMinMen 8(중대에 연막 보유 돌격병 ≥ 이 수) · smokeOpenMinDist 18(무방비 개활 구간 칸) · smokeStandoffMin 8, Max 20(적 사선 앞 투척 목표 지점까지 거리) · smokeSpacing 5(투척점 사이, = 반경 2.5×2) · smokeMaxPerCompany 4(동시 연막 수) · smokeCooldownSec 20 · smokeUnderFireSupp 0.25(중대 평균 제압) · smokeUnderFireLossSec 10 / smokeUnderFireLoss 0.05(최근 10초 사상 비율) · smokeSelfBlockGuard true(아군 사격선이 막히는 투척은 사격 중인 아군 소총수가 ≥ 40% 일 때 안 함).
- **언제 던지나**: (R1 개활 횡단) 공격 과제 중대가 진격 중, 보이는 적 사수(소총 사거리×1.3 안에 사선이 열린 적 ≥ 3) 와 우리 사이 엄폐 칸 없는 구간(TacMap 엄폐 점수 기준)이 smokeOpenMinDist 이상이면 → 연막 보유 돌격병이 적 방향 smokeStandoff 거리 가로줄 smokeSpacing 간격에 투척, 연막이 선 뒤(= SmokeBlocks 확인) 그 뒤를 따라 진격. (R2 사격 아래 돌격) 돌격 상태 직전/중 평균 제압 ≥ smokeUnderFireSupp 또는 최근 사상 ≥ 기준이고 돌격 지점까지 ≤ grenade.range+smokeStandoffMax 이면 투척. (R3 참호 돌격 순서) smokeHook 켜짐 + Ai 플래그일 때 단계 2→3 전환 직전 참호 앞 smokeStandoff 점에 투척. 던진 중대는 smokeCooldownSec 동안 다시 안 던짐, 연막 보유 0 이면 규칙 건너뜀. 후퇴·예비·방어 과제 중대는 던지지 않음.
- 저장: 쿨다운은 가능하면 기존 g.OrderTick/StateTick 류 재사용. 새 필드가 필요하면 TacGroup(ObjCodec) 에 추가하고 FV+1.
## 수용 기준 (측정 가능)
1. 키 모두 꺼짐 = 시뮬 불변: detcheck 골든 일치 (8 변형), snaptest QUICK OK, ai3b·s3a·breach·n32 묶음 PASS.
2. 새 시험 `KD_SELFTEST_ONLY=smokeai`(≥6항목): 개활 횡단 투척 발생 · 엄폐 많은 지형 투척 없음 · 사격 아래 돌격 투척 · 연막 0 이면 무동작 · 쿨다운 · 키 꺼짐 시 CommandLog 에 smokegrenade 없음. 리플레이 왕복(켠 상태 90초 기록 → 재생 → 체크섬 일치, IsAi 를 일부러 false 로 해도 일치).
3. 성능: `--stress ai players=4 n=1000` avg 가 BASELINE 대비 +5% 이내 (키 켠 상태에서도).
4. 켠 상태 측정(아래) 결과가 카드 결과란에 숫자로 기록됨. 개선/악화 판정은 하지 않고 숫자만 (켜는 결정은 사용자).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
`--tactics-obs` 시드 1~3: A ai_bn_meeting · B ai_bn_assault · C ai_kr_vs_ft_1000 (키 끔 vs smoke_on vs smoke_hook_on, 같은 빌드). 보는 값: 승수·K/E·타임아웃, 투척 횟수, 연막 안/뒤에서 받은 피격 대 앞선 같은 구간, 돌격 개시 후 사상 비율(S1 KR charge 지표). 개활 사수 시나리오 "열린 땅 사수 B" 3시드 (A 손실·시간; 계획 P2 기준 A 손실 71~83). 한 번에 무거운 작업 1~2개.
## FV / Replay 필요 (FV_RESERVATIONS.md 에 지휘자가 적을 목록 — 이 카드는 수정 안 함)
- Replay: 변경 없음 (smokegrenade 명령 v20 이미 있음).
- FormatVersion: 새 저장 상태가 없으면 변경 없음. AI 쿨다운 필드가 필요하면 +1 (예약 요청: AF1=60/AE2=61 뒤인 62 로 가정, 기반 = ai3b2 스택 → 병합 때 재번호).
- 골든: 키 끔 = 재생성 불필요. IsAi 복원(FV60/R24) 과 독립 — 단 켜기 전 자유전 리플레이 시험은 복원 뒤에 한 번 더.
## 질문 (≤8)
- Q1 연막탄(박격포/곡사포 지점 포격 '연막') 도 AI 가 쓰나? (이 카드는 수류탄만)
- Q2 연막 수류탄 사거리 5 로는 적 사선 앞 투척이 짧다 — 사거리/연막 반경 상향은 밸런스 결정 (이 카드는 값 그대로).
- Q3 연막이 아군 사격도 막는 부작용 — 허용 수준(smokeSelfBlockGuard 40%)?
- Q4 한국 외 세력 AI 는 연막 보유 병종이 없으므로 사실상 한국군 AI 전용인가?
- Q5 켜는 기본 대상: 모든 프로필 vs 공격형 프로필만?
- Q6 측정 지표 합격선(예: C 시나리오 K/E 하락 허용폭)?
## 결과 (에이전트가 끝에 3~5줄만)
- 구현: TacticalAi.Smoke.cs (R1·R2, ThrowSmokeCommand 만), TrenchAssault.cs TaSmokeHook (R3, TA.Flags 1 만), tactics.json breachAi 16키, 프로필 tactics.smokeAi + 겹침 smoke_on, obs 변형 SMOKE·SMOKEHOOK·SMOKEALL. 새 저장 상태 없음(쿨다운=자기 연막, 최근 사상=생존자 LastDamagedTick 대용); 프로필 키 때문에 **FV 56** (ai3b2 55 위, 병합 때 재번호 — 예약 가정 62). Replay 변경 없음.
- 시험: smokeai 13항목 PASS (개활 3발·엄폐 0·사격아래 3·보유0 무동작·쿨다운·키끔 0·smokeHook AI 4발/플레이어 0/끔 0·리플레이 왕복 IsAi false 일치), n32·breach·replay·ai3b·s3a·snapshot PASS, snaptest QUICK OK, detcheck 8변형 GOLDEN 일치(키 끔).
- 확인 실행 ai_kr_vs_ft_1000 시드 1~8 (900초, 같은 빌드): 끔 승 8/8 · KR 손실 합 3086 · 연막 0 / SMOKE 승 8/8 · KR 손실 합 3081 · AI 투척 4발 (3/8 시드; 개활 1 · 사격아래 3). 투척 없는 5시드는 끔과 결과 동일. 사격 아래 판단은 자기 사격선 가드(쏜 병사 ≥40%)에 자주 막힘 — Q3.
- 측정 보류 (지휘자 2026-10-22 속도 우선): 승률·K/E·trench_assault/yalu_crossing/ai_bn_assault 비교 = deferred (그 세 미션은 AI 쪽 연막 보유 병종이 없어 투척 0 예상). --stress ai 모드는 이 기반에 없어 성능 미측정 (키 끔 비용 = 중대 판단마다 bool 1회).
- 질문 권장안 채택: Q1 수류탄만 · Q2 사거리 5 그대로 · Q3 40% · Q4 사실상 한국군 전용 · Q5 결정 보류(전부 꺼짐) · Q6 숫자만.
