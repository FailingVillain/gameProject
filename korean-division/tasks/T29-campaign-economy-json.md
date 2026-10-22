# T29 캠페인 경제 숫자 JSON 이전 + 첫 승리만 정산 (D23 · D24)
- id: T29 | 역할: feature | 등급: D中 H1 (선택 메모: R1 S Wlight)
- 선행: T01(스택, gemini-import 63080df 위) | 후행: T09 T19 (경제 튜닝)
- 핫스팟/만질 파일: CampaignEconomy.cs · CampaignState.cs(Apply, 곡사포 중대) · MissionSelfTest.cs · 새 CampaignEconomyDefs.cs · assets/data/campaign_economy.json
- 브랜치: t29-campaign-economy-json (kd-develop/kd-t29, gemini-import 에서 분기) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정 (ops/DECISIONS.md 절 E, D22 무응답 기본값 A — 사용자 수면 중)
- D23 A: 정산(보급·신병·노획)은 미션 첫 승리에만. 반복 승리는 결과 기록만(자원 불변), D10 과 일치.
- D24 A: 하드코딩된 CampaignEconomy 밸런스 숫자를 assets/data JSON 으로. 값 불변.
## 범위 밖
새 메카닉 없음. 병종별 투입 비용 표(UnitDeployCost)는 이번에 안 옮김(요청 목록 밖).
## 수용 기준
빌드 · missions · savecompat 묶음 통과 · detcheck GOLDEN match · JSON 값 = 이전 상수.
## 측정
빌드 / KD_SELFTEST_ONLY=missions,savecompat / --detcheck (8aa6448b9690b922, GOLDEN match).
## 결과
- campaign_economy.json 14개 키(보급1500 신병1000 부상복귀0.25 탄약60/200 노획0.3/0.5/20 상한0.3 대규모180/x1.5 치료0.5 신병배치0.5 곡사포40). 파일 없으면 같은 기본값.
- 반복 승리: `RecordRepeatVictory` 가 LastDebrief(사살 · 손실만, 자원 0)를 남기고 Pending 폐기(출격 비용도 안 받음 — 확인 필요 시 DECISIONS 후보).
- 테스트는 CampaignEconomyDefs.Current 를 읽음 + 값 불변 가드 1건 + D23 테스트 1건.
