# C1E1-g3 미션 이벤트 행동 확장
- id: C1E1-g3 | 역할: sim(사용자 지시로 中 수준) | 브랜치: kd/c1e1-g3-events (base kd/tce-vars 2dbada7) | 상태: wip-ready
## 결과
- 새 사건 조건(JSON when): objectives [..] (목표 AND) · delay N초 (나머지 조건이 처음 참 → N초 뒤, 래치) · var/op/value (미션 변수) · decision/choice (미션 중 결정 응답).
- 새 행동(do): end win|lose (사건 승패, 승패 판정이 덮어쓰지 않음) · decision id (팝업 제기, 응답 전 UI 일시정지) · setVar + value|delta. 적 사기 행동은 만들지 않음 (사용자 결정 BH1).
- 상태: MissionState.EventArmed/Vars/StartVars/PendingDecisions/Decisions (체크섬은 쓰일 때만 기여 -> 기존 미션 불변), 명령 MissionDecisionCommand (리플레이 머리말에 StartVars). 저장 FV 54, 리플레이 20 (임시 번호 - 병합 때 fv_renumber 필요).
- UI: Game.DecisionBlocked + 팝업 (GameMission.cs), 결과에 MissionResults.Vars/Decisions. 캠페인 쪽 도우미: CampaignVars.StartVarsFor/ApplyMissionVars, CampaignDecisions.ApplyMissionOutcome (정산 연결은 g1/TC-F).
- maxDeploy 는 base 에 이미 MissionDef.MaxDeploy + CampaignEconomy 연동이 있어 추가 없음.
- 시험: Tests/MissionEventsSelfTest.cs (missions 묶음), SnapshotSelfTest Audit 분류 갱신. detcheck 8변형 golden match.
