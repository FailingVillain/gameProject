# TC-E 호감도 favor.* + 결정(Decision) 런타임
- id: TC-E | 역할: feature | 등급: D中~上 H2
- 선행: TC-D · T09 | 후행: -
- 핫스팟/만질 파일: CampaignState(변수 레지스트리) · 이벤트/선택지 UI · Snapshot(FV 예약)
- 브랜치: kd/tce-vars (base kd/tcd-funds-slots) | 상태: wip-ready | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22: 호감 증감 크기 +1/+2/+3/+5(정수, 값은 사용자 지정). VIP 호감 변수: 황손 · 민 대장(E1), 이석 · 남 참장(E2 해금) · 시위대장(E2 해금). 김주현 제외.
- 결정 객체 7종(CHOICE APPROACH SELECT INMISSION ORDER DEPLOY REPORT), 소비처 필수. 일수는 미션 승리 시 확정(재도전은 일수 미사용). minor 결정도 응답 필수(미션 중 일시정지).
- 원문: 캠페인 디자인 v1.txt §11 · §15.
- 2026-10-22: 호감 값·소비처는 [보류](로드맵 추후). 이 카드는 변수 자리와 결정 런타임만 만들고 증감은 0 으로 시작 가능.
- 2026-10-22 결정 가중치: major 4(D02 D10 D11 D13) · std 8(D01 D03 D05 D06 D07 D09 D15 D16) · minor 4(D04 D08 D12 D14) = 16.
- 2026-10-22 D10 설득: 선택지에 성립 조건(intel.andong)과 실패 결과를 미리 표시, 조건 없어도 선택 가능(비활성 금지). D16 대기: 항상 선택 가능, IL2 에 '대기 시 N일째 / 기한 M일' 과 ⚠ 표시. 일수 판정표 = spec R1.
- 2026-10-22 D09 B 포로 처형: pow.bandit ≥ 1 일 때만 활성(0 이면 '포로 없음' 비활성). pow.bandit = 마적이 항복한 인원 합산(BT1 + SK1), 처형 후 0. spec R8.
- 2026-10-22 협상 점수 0~3: +1 D02 A+C · +1 D07 B · +1 SK4 성공, D15 포위 중 추인 −1. 사건 데이터 소비처는 TC-F.
## 범위 밖
연출 문장(층 3) · 포트레이트.
## 수용 기준 (측정 가능)
- 결정 16개 데이터 로드 시험. 일수 재도전 복원 시험. 저장 호환.
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
selftest.
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 캠페인 스키마 v13 (12 는 g1 몫, 건너뜀; ops/FV_RESERVATIONS.md 에 행 추가). sim FV/Replay 불변 (detcheck golden 일치). 새 필드 CampaignState.Decisions (응답 기록 · 대기 결정 · 미션 복원점).
- 변수 레지스트리: assets/data/campaign_vars.json (18개, 범위 · 초기값 · alias) + CampaignVars (Get/Set/Add/SnapshotAll/RestoreAll). 값은 Flags 에 저장 (favor.hwangson=heir_mood 60, favor.minDaejang=cmd_favor 50 별칭). 증감 크기는 [보류]라 정의 안 함. 초기값 가정: E2 호감 0, rep.local 0 (-100..100), morale.troops 70 (중대 기본 사기), maxCasualtyFrac 50 (%).
- 결정 런타임: assets/data/campaign_decisions.json (16개 major4/std8/minor4, 소비처 필수, 효과는 자금 가격 · 대민 구제 · D07 B intel · D09 B 처형만) + CampaignDecisions (Raise/Answer 원자적 · EnableIf 비활성 · SuccessIf 성립(D10, 비활성 금지) · Negotiation 점수 · BeginMission/RetryMission/CommitMission). 시험: Tests/CampaignDecisionsSelfTest.cs (savecompat 그룹).
- 미결(제안): g1 의 ICampaignVars/FlagsCampaignVars 를 CampaignVars 로 바꿔 끼우기 · g1 체크포인트와 DecisionCheckpoint 통합은 병합 때 (g1 이 먼저 병합되면 이 카드 리베이스에서 스키마 번호/체크포인트 정리). 협상 D15 -1(포위 중 추인) 은 TC-F. D02 의 마적 인원 편성 반영 · 미션 중 결정 팝업은 g2/g3.
