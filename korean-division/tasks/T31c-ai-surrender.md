# T31c AI 항복 개선 (키 완화 + 측정)  (제안 docs/CASUALTY_FOLLOWUP_PROPOSAL.md)
- id: T31c | 역할: feature | 등급: D中 H1 (R2 S Wlight)
- 선행: 없음 (main 위, WIP 와 독립). 이미 있음: Surrender.cs N79b, balance surrender* 키, SurrenderSelfTest
- 핫스팟: balance.json(surrender*), Defs.cs(신규 키 기본=현행), Surrender.cs(막힘 판정 배수), SurrenderSelfTest.cs, TacticsObs [항복] 줄
- 브랜치: kd/t31c-ai-surrender | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "AI 끼리 항복하는 것도 개선" (2026-10-21)
- N79b 사양(2026-10-20): 항복 = 아주 까다로운 조건·낮은 확률, AI 는 퇴각 가능하면 항복 안 함, 플레이어 x0.3.
## 범위 밖
세력 단위 항복(옵션 C), 새 저장 필드(FV 증가), 항복 포즈·점수 공식 변경
## 수용 기준
- 신규 키는 기본 = 현행: 기본에서 surr 시험·detcheck 골든 불변.
- 옵션 A 값(surrenderCasualty 0.6, MaxLeft 20, Chance 0.15) 켠 상태 시드 8 대대전 항복 >= 1건/시드 평균, 플레이어 쪽 변화 없음, 승패 이상 없음.
- 시험 추가: 완화 키 켠 장면에서 AI 항복 발생 / 퇴각 가능하면 여전히 안 함.
## 측정
KD_SELFTEST_ONLY=surr, tactics-obs 시드 8 ([항복] 줄), BASELINE_CURRENT
## 결과
구현 (전부 balance.json combat, 기본 = 현행, 골든 불변 확인: detcheck GOLDEN match, 체크섬 8aa6448b9690b922 262d0252c35087e2; surr/ai/tactical PASS)
- 신규 키: surrenderAiCasualty / AiMaxLeft / AiChance / AiSuppAt / AiSuppSec (-1 = 공통 키 그대로), surrenderRetreatBlockedMul (1 = 현행; 막힘 반경·탐침 길이 x값, 포위 임계 0.35 x값 상한 0.9). 전부 AI 세력에만 적용 (플레이어 변화 없음, 옵션 A+B).
- 열린 질문 답: 확장은 AI 한정(카드 '플레이어 쪽 변화 없음'); 세력 단위(옵션 C)·새 저장 필드 없음; 켜는 값은 사용자 결정(기본 끔).
- 경고 (지휘자 통보): Surrender.cs 가 Faction.IsAi 를 읽는데 Replay.cs 는 자유전투에서 IsAi 를 복원하지 않음 -> 키를 켜기 전에 'IsAi 리플레이 복원' 수정이 선행되어야 함 (미션은 WorldMission 이 설정). 기본 끔이라 현재는 안전.
- 시험: SurrenderSelfTest.TestRelaxed (기본=현행, 완화 시 AI 항복, 플레이어 불변, 퇴각 가능하면 항복 안 함, 사상 미달 항복 안 함, aiChance, aiSuppAt/Sec, retreatBlockedMul).
- 측정 (tactics-obs ai_bn_meeting 시드 1-8, KD_OBS_SURR 환경변수 신설(도구 전용) "aiCas,aiMax,aiChance,blockMul,aiSuppAt,aiSuppSec"). 끔: 항복 0/8시드, 승 3/8(s1,6,8), 시간초과 1/8.
  - 카드 옵션 A 값 단독(0.6,20,0.15): 항복 0 (시드1 확인) -> 수용 기준 미달. 병목은 막힘 판정과 제압 지속 조건 (진단: 자격+제압 지속 샘플 9개 중 퇴각 불가 3개).
  - P1 (A + blockMul 2 + suppAt 0.6/5초): 항복 합 3 (0.4/시드), 승 3/8 불변, 시간초과 +1.
  - P2 (0.6,20,chance 0.3,blockMul 3,suppAt 0.5/3초): 항복 합 11 (1.4/시드: s1:2 s3:3 s4:1 s7:4 s8:1), 승 1/8 (s1,s8 승->시간초과 900s), 시간초과 4/8 -> 항복자가 생존 취급이라 종료 지연, 승패 이상(악화). 즉 평균 >=1 은 되나 승패 기준 미달; 판단은 사용자.
