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
-
