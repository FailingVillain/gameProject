# TC-D funds(자금) + 외인 슬롯 + 통제 한도
- id: TC-D | 역할: feature | 등급: D中 H2
- 선행: T01/T09 | 후행: TC-E
- 핫스팟/만질 파일: CampaignEconomy(campaign_economy.json) · CampaignState · 편성 UI · Snapshot(FV 예약)
- 브랜치: kd/tcd-funds-slots (base t48b2-assign 80c11fb) | 상태: wip-ready | 담당 에이전트: sonnet
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22: funds 신설. 표시 단위 냥, 설계 단위 자금 10 = 은 5,000냥(사단장 특별 자금). 의용군 150명 = 자금 6(50명당 2, 2026-10-22 개정), 마적 = 자금 3/100명.
- 통제 한도 = 한국 정규군 E1 3,000(E2 5,000~6,000). 외인(의용군 · 마적 · 의용병) 별도 슬롯 합계 800(2026-10-22 개정, 500→800). 에피소드 시작 보급 4,000. 연대 3개 시작 → 최대 9개.
- 2026-10-22: 보급 = 투입 비용 풀. 편성 UI 에 '가용 보급' 게이지 필수(D02 C 대민 구제 −400, 앞 미션 영구 소모 반영). 소모 상수는 [제안] — spec R5.
- 2026-10-22 보급 모델(승인, spec R5): 투입은 출발 시 예약, 귀환 시 생존분 환원. 영구 소모 = 전사자 보충 비용 + 투입 비용 10%(탄약). D02 C 대민 구제 = 영구 −400. OP1 가용 보급 = 4,000 − C − 앞 미션 영구 소모. 상수는 JSON.
- 2026-10-22 가격(승인): A 의용군 150명 = 자금 6(50명당 2) · B 마적 3/100명 · D09 요정 2 · 보검 2(임시). 외인 슬롯 800, 정규 통제 3,000.
## 범위 밖
외교 · 연구 지출 UI.
## 수용 기준 (측정 가능)
- 한도 초과 편성 불가 시험. 저장 호환(schemaVersion). 수치는 JSON 데이터로(코드 박기 금지, DECISIONS D24).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
selftest.
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 캠페인 스키마 v11 (v10 건너뜀; 베이스 t48b2 = v9, ops/FV_RESERVATIONS.md C1E1 예약). sim FV 불변 (Replay/detcheck golden 일치). 수치는 전부 assets/data/campaign_economy.json (fundsStart 10 · silverPerFund 500 · volunteer 50명당 2 · bandit 100명당 3 · fairy/sword 2 임시 · regularControlCap 3000 · foreignSlotCap 800 · episodeSupplyStart 4000 · permanentDeployFrac 0.10 · casualtyReplaceCost 1 · civilianReliefSupplyLoss 400).
- 공개 API (src/Core/Campaign/CampaignFunds.cs): ToSilver · VolunteerPrice(men) · BanditPrice(men) · TrySpend(s, price, out err) · Gain · ControlUsage(selected) · CheckControl(selected, regularCap?) -> 사유|null · AvailableSupply(s) · AddPermanentLoss(s, amt) · PermanentLoss(deployCost, dead) · SettleReservation(s, dead). 상태: CampaignState.Funds · EpisodePermanentSupplyLoss · RosterCompany.Foreign · PendingOperation.EpisodeSupplyReserved. CampaignEconomy.BeginOperation 은 이제 bool (한도 초과 = false, 상태 불변; 출발 시 투입 비용 예약), SettleBattle 이 전사자 x 보충 + 투입 10% 를 영구 소모에 누적.
- UI: 편성 화면(DrawPreOpView) 게이지 한 곳 - 가용 보급 바 + 자금(냥) + 정규/외인 사용량 한 줄, 한도 초과 시 [출격] 잠금. 시험: Tests/CampaignFundsSelfTest.cs (savecompat 그룹) - R5 OP1 가용 S1 3759(~3760) S2/S3 3377(~3380) S4 3845.
- 미결(제안): 가용 보급 부족 시 출격 차단은 아직 안 함 (기존 Supply 게이트만) - 에피소드 화면(g2)에서 연결. E2 정규 한도 5,000~6,000 은 CheckControl 의 regularCap 인자로 (JSON 별도 키는 E2 카드에서).
