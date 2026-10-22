# C1E1-g1 에피소드 그래프 런타임 (노드·SELECT·일수·기한표·재도전 복원)
- id: C1E1-g1 | 역할: sim | 유형: engine-gap | 모델: opus | 등급: D上 H3 (W heavy)
- 선행(blockedBy): 없음 (변수는 CampaignState.Flags 임시 인터페이스, TC-E 가 교체) | 병렬/직렬: 병렬 (새 파일 위주, CampaignState 접촉 최소)
- 핫스팟/만질 파일: src/Core/Campaign/CampaignEpisode*.cs (신규) · CampaignState(스키마 추가만, FV/저장 호환) · docs/C1E1_EPISODE_SCHEMA.md (첫 커밋으로 먼저 올린다 → d1 이 읽는다) · Tests/CampaignEpisodeSelfTest.cs
- 브랜치: kd/c1e1-g1-episode-graph (base: t42d-schedule-log (캠페인 tip, 발주 때 해시 확인)) | 상태: wip-ready | 담당 에이전트: sim (Opus 5.5)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 노드: EV/CH/IL/BT/SK/OP/SELECT/REPORT. 흐름: EV1 → CH1 → IL1 → BT1 → SELECT x2(SK 0~2, 개당 1일) → CH2 → EV2 → IL2 → OP1 → D14 → EV3.
- 2026-10-22 일수 확정 규칙: 일수는 미션 승리 시점에 확정, 재도전은 일수를 쓰지 않음, D16 대기는 IL 선택 확정 시 소모, OP1 재도전은 IL2 선택 이후 상태로 복원. 에피소드 7일.
- 2026-10-22 R1 D10 기한 표 (따름5/지연6/설득성립7/설득실패5; 총일수 8 = 상한 초과 같은 페널티; 기한 초과 = 패배 아님, 승리 후 페널티): 강공 3/4/5(대기 +1), 포위 5/6/7. 대기는 항상 선택 가능.
- 원문: campaign/spec_c1e1.txt (v0.2) · 계획 docs/C1E1_IMPLEMENTATION_PLAN.md. 명세에 없는 것은 만들지 말고 docs/DECISIONS_PENDING.md 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행.
## 범위 밖
결정 UI(TC-E/g2) · 화면(g2) · 변수 레지스트리(TC-E) · 미션 JSON · 서사 · 호감·사기 값(보류) · 서사 문구(층 3) · E2 설계 · 밸런스 조정(보류, 사용자 2026-10-22)
## 수용 기준 (측정 가능)
- 에피소드 정의 JSON 로더(검증 포함: 없는 노드/끊긴 전이 거부) + 노드 전이기 (헤드리스).
- SELECT 규칙 (0~2, 중복 불가, 건너뛰기) 과 일수 계산 · 기한 표 6행 x 4열 x 대기 유/무 전부를 표 시험으로 확인 (R1 표와 동일).
- 재도전 복원: 미션 패배 시 일수/보급/변수 불변, OP1 재도전 = IL2 선택 이후 상태. 저장/불러오기 왕복.
- 새 상태는 체크섬 + Audit + 저장 + FormatVersion 예약(지휘자 배정). 마이그레이션 시험 (SAVE_COMPAT_POLICY).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build · selftest(관련 묶음) · detcheck · snaptest · 기준선 docs/BASELINE_CURRENT.md
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 2026-10-22 base t48b2-assign 80c11fb (지휘자 지시). 스키마 문서 054af31 선커밋. 캠페인 스키마 v12 (10/11 예약, 병합 때 정렬), sim FV 변경 없음 (캠페인 저장은 스냅샷 밖).
- 공개 API: EpisodeDefs.Load/Parse · EpisodeRuntime.Start/Current/Resolve/ConfirmFormation/Select/ChooseVariant/BeginMission/MissionWon/MissionLost/ProjectDays/WouldBeLate · ICampaignVars(FlagsCampaignVars) · CampaignState.Episode.
- 시험 episode 묶음 10줄 OK (깨진 그래프 20종 · R1 48칸 · SELECT · 복원 · 왕복 · v9/v11 이행), savecompat 182 OK, commanders OK, detcheck GOLDEN match 8aa6448b.
- 미연동: CampaignState.Day/CommitResult 와 결합, UI(g2), 결정 내용(d1/TC-E). 기본값 3건 DECISIONS_PENDING.
