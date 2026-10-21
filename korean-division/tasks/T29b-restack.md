# T29b 재스택: T29(경제 JSON + 첫 승리만 정산) 를 T09(캠페인 슬라이스) 위로
- id: T29b | 역할: feature | 등급: D中 H2 (선택 메모: S)
- 선행: T09 (t09-camp-slice 0c82a47) · T29 (783e7ce) · T01 리뷰 수정 (63080df) | 후행: T19
- 브랜치: t29b-on-t09 (kd-develop/kd-t29b, t09-camp-slice 에서 분기) | 상태: wip-ready | 담당: feature(Sonnet)
## 한 일
1. t09-camp-slice 위에 63080df (loadError 저장 차단 · int 탄약 배율 · null 거부) 체리픽 - 충돌 없음.
2. 783e7ce (T29) 체리픽 - CampaignEconomy.cs · CampaignState.cs 충돌 3곳 + 1곳을 아래처럼 하나로 합침.
## 정산 규칙 (하나로 합친 결과)
- 첫 승리 (`!wasDone`): `SettleBattle` 전체 (T09 의 출격 중대 손실 → Roster, 부상 복귀 25%, 노획, 정기 보급 · 신병, 탄약 환원).
- 반복 승리: T29 의 `RecordRepeatVictory` (결과 요약만, 자원 · 병력 불변, Pending 폐기) → 정기 보급 · 신병(D10) + 노획 · 치료(D23) 모두 없음.
- T09 의 `SettleBattle(firstWin)` 매개변수는 삭제: 반복 승리는 SettleBattle 에 아예 안 들어오므로 죽은 분기. `next_supply_penalty_pct` 도 첫 승리에서만 소모되므로 자연히 보존.
- T09 의 loss→Roster 경로의 25% 도 `CampaignEconomyDefs.WoundReturnRatio` 로 (값 0.25 불변).
- T09 의 olderReplay (지난 미션 재승리 시 진행 · 이월 병력 안 건드림) 는 그대로 유지.
## 결과
검증 결과는 아래 한 줄에 기록.
빌드 OK · savecompat PASS · missions PASS (T09 재승리 D10 + T29 D23 + 값 가드 포함) · detcheck 8변형 동일 GOLDEN match (8aa6448b9690b922) · uifuzz 30 크래시 0. 교체: 이 브랜치가 t09-camp-slice + t29 를 대체 (T09 → T29b 순서로 병합할 때 t29-campaign-economy-json 은 버림).
