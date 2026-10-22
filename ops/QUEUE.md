# QUEUE - 승인된 작업 id 순서표 (갱신 2026-10-22)

등급: **필수 = D (G/下/中/上, 모델) · H (핫스팟 0~3)**. 선택 메모 = R (R1 국소/R2 모듈 간/R3 결정성·저장·리플레이) · S (S<=60k/M<=150k/L>200k 분할) · W (light/heavy).
상태: queued / assigned / running / wip-ready / merged / closed / blocked(사유). 전이 주체 = ops/PIPELINE.md. 카드 = `korean-division/tasks/Tnn-<slug>.md`. 쓰는 사람: 지휘자(행 추가·assigned) · 작업 에이전트(자기 카드만) · merger(merged) · release(closed).
wip/* 복원 작업(T01 T03 T04 T05 T07): 역할 란의 앞 역할이 main 에서 브랜치를 새로 만들어 패치/브랜치를 복원·검증하고 `wip-ready` 로 끝낸다. merger 는 그 뒤.

(2026-10-23 경량화: merged/closed/done 행은 ops/QUEUE_DONE.md 로 이동. 아래 한 표 = 진행 중·대기 카드 — 옛 위쪽 요약 표 + 아래 '상태 실측' 표를 합침(충돌 시 옛 '상태 실측' 표 값을 상태로 채택). 행별 상세 = ops/CARD_INDEX.md · 워크트리 정리 = ops/WORKTREE_CLEANUP.md · 워크트리 = kd-develop/ 아래.)

| id | 제목 | 역할 | 등급 | 선행 | 상태 | branch @tip | 워크트리 | base(스택) |
|---|---|---|---|---|---|---|---|---|
| T11 | 전 유닛 재렌더 (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | 병합 큐 종료 뒤 렌더 3칸 예약 | running (재렌더 ~60/81, 남은 officer-russian-red 1 + deferred 10) | kd-rerender @467dafc | kd-rerender | main |
| T12 | 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 (T06 은 D19 뒤 재측정) | wip-ready (측정, 보류 0.3) | measure/t12 @e6122ac | kd-meas-t12 | ai3b2 스택 |
| T13 | release (전체 selftest · 골든 · publish · 기록) | release | D中 R2 SM H1 Wheavy | T12 | queued | - | - | T12 |
| T15 | 밤 soak 갱신 → 전투 문제 9건 재보고 | measure | D中 R1 S H0 Wheavy | T12 | wip-ready (측정, 보류 0.3) | measure/t15 @a70a308 | kd-meas-t15 | ai3b2 스택 |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = 사용자 U02) | docs | D下 R1 S H0 Wlight | U02 | blocked (U02) · (옛 표1: blocked(U02)) | - | - | - |
| T48b | 장교 신원 영속 · 진급 종착 · 배정 · 연대장 기용/해임 · 전사/항상부상 | sim | D上 R3 M H2 Wlight | T48a | queued | - | - | - |
| T48e | 2단계 전투 연결 (기본 꺼짐 키 4개, FV 예약 필요) | sim | D上 R3 M H3 Wheavy | T48d · T10 · T24 | queued (blocked) | - | - | - |
| C1E1-m2 | MAP-B 지도 생성 (large) | feature | D中 H0 | 없음 | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-u1 | 유닛/무기/세력 점검 (보고서) | scout | D下 H0 | 없음 | wip-ready (kd/c1e1-u1-unit-audit b13932d) | - | - | - |
| C1E1-g2 | 에피소드 화면 UI | feature | D中 H2 | g1 TC-D TC-E | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-g3 | 미션 이벤트 행동 확장 | sim | D上 H3 | TC-E | assigned (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-d1 | 결정 16개·노드 데이터 JSON | feature | D中 H1 | g1 TC-E | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| M2 | 캠페인 사기·호감·민심 0~100 기본 50 통일 + 전투 후 탈영 + 포 단가 7 (DECISIONS BH2~BH5) | feature | D中 R2 S H2 Wlight | TC-E (base kd/tce-vars) | assigned (kd/m2-morale-camp) | - | - | - |
| C1E1-s6 | OP1 강공 미션 JSON | feature | D中 H1 | m2 u1 | wip-ready (kd/c1e1-s6-op1-assault 3f704c6; 리뷰 수정 진행 중) | - | - | - |
| C1E1-v1 | 변형 데이터 적용 | feature | D中 H1 | s1~s7 TC-F TC-A | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-t1 | 기능 문구 (서사 키만) | docs | D下 H1 | d1 s* | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-p1 | VIP 포트레이트 자리표시 (선택) | art | D下~中 H0 | Q4 | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-q1 | 종단 시험 S1~S4 | measure | D中 H1 | 거의 전부 | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| C1E1-q2 | OP1 성능 스모크 | measure | D中 H0 | s6 | queued (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) | - | - | - |
| NEW-1 | 지도 크기별 과부하 시험 (N13; measure) | measure | D中 | 미기재 | queued (지금 발주됨) | - | - | - |
| NEW-2 | LOD0 그림 누락 버그 조사 | 미기재 | D中 | 미기재 | queued (지금 발주됨) | - | - | - |
| NEW-3 | 제압으로 엎드린 병사가 대열 이동에 끌려가는 현상 | sim | D上 | 미기재 (골든 변경 가능) | queued (0.3 초기 골든 재생성과 함께) | - | - | - |
| NEW-4 | 좁은 길목 우왕좌왕 | sim | D上 | wip/chokefix (D14 보류)와 함께 | queued (0.3 초기) | - | - | - |
| NEW-5 | 일본군 45식 군모·기수 깃발 3:2·보병 겹침 일괄 수정 (collide) | 미기재 | 미기재 | 선행: mid 재렌더 | queued (0.3 유닛 재렌더(mid) 때 함께) | - | - | - |
| T32c | - | - | - | - | queued | - | - | T32b |
| T32d | - | - | - | - | running (총구 수정) | kd/t32d-pioneer-art @2118f9b | kd-t32d | T32a |
| T33c | - | - | - | - | running | kd/t33c-placement @c514821 | kd-t33c-placement | T33b |
| T33d | - | - | - | - | running (확인 필요, 커밋 공유) | kd/t33d-rail @c514821 | kd-t33d-rail | T33c |
| T34b | - | - | - | - | queued | - | - | T34a |
| T34c | - | - | - | - | queued | - | - | ai3b2 |
| T34d | - | - | - | - | queued (보류 0.3) | - | - | T34a |
| T34e | - | - | - | - | queued | - | - | T34a |
| T38c (=T46) | - | - | - | - | wip-ready | kd/t46-card-overflow @54e8a54 | kd-t46 | wip/breach2 |
| T38h | - | - | - | - | wip-ready (f126551, 미커밋 없음) | t38h-fuzz-campaign @e5f76ec | kd-t38h | T38g |
| T39e/f/g/h/j | - | - | - | - | queued (blocked T11) | - | - | T11 |
| T44 | - | - | - | - | 진행 중 (kd/t44-smoke f9dea2d; smoke_missions.ps1 하네스 수정 완료, 게이트 개선 뒤 실행 필요) | kd/t44-smoke @1c4036d | kd-t44 (미커밋 3건) | main |

**정책 (현행, 2026-10-22 AT·AX 로 옛 '병합 금지' 해제 — 옛 문구는 폐기): WIP 는 병합 라인(merge/0.2-final, merger 상시)을 거쳐서만 병합한다. 작업 에이전트는 wip-ready 까지만 하고 병합은 하지 않는다. main 직접 병합 금지, main 은 마지막에 `--ff-only`(사용자 보고·확인 뒤), push 는 사용자 명령 때만.** 카드·QUEUE 행에 '병합 금지' 를 새로 쓰지 않는다(특정 브랜치를 막을 땐 '대체됨/제외: 사유'로 MERGE_PLAN §2 에 적는다).

**실행 순서 (task list)**: 1차 T03 · T07 · T01 (진행) + T18 · T11 · T21 · T22 · T23 → 2차 T04(T03 위) → T05(T04 위) → T08 · T09(T01 위) → T17(T08 위) → T19(T09 위) → T12 · T15 · T16 · T13(WIP 측정은 스택 브랜치 위에서) | 막힘 T06(D19) T10(D15) T20(U02) | choke(D14) 보류.

카드가 있는 것 = T01~T13 (+_TEMPLATE). T15~T20 과 카드 없는 브랜치(wip/chokefix D14 보류 · wip/sfxnew 효과음 · wip/gt-face-proto)는 발주 직전 spec/지휘자가 카드를 쓴다.
보류(이 표 밖): 장갑열차 · 하천 · 전술 AI 3C/3D · 지도 편집기 · 캠페인 순서 연결/챕터 1/엔딩.


## 밸런스 보류 (사용자 2026-10-22: 밸런스 관련 작업은 전부 보류, 최후순위 — 사용자 결정이 있을 때만 진행)
보류 대상: T12 · T15 · T16/T16b 후속 튜닝 · T31a(재투입 제한 튜닝) · T31b/T31b-endline 후속 기준선 재측정 · T31c 항복 값 · T24 ②③ 후속 튜닝 · T34d 기병 측면 강도 튜닝(S4 재확인 포함) · T06(종결) · AH1 열차 밸런스 · AG1 항복 켜기 · U2 목표 미달 보완 · H1 야전포 사거리 · 모든 n>=24 승률 측정 카드. 허용: 기능 구현, 결정성·저장·입력 검증, n=8 동작 확인.
우선순위: ① 재렌더(T11) ② 기능 구현(스택: 전투공병·AI 연막/철조망·지도 편집기·UI 백로그·열차·IsAi·캠페인 후속) ③ 통합 리허설/병합 준비 ④ (보류) 밸런스.

버전 마일스톤(사용자 2026-10-22): 0.2 = 밸런스 패치 이전(기능 완료·병합) → 0.3 = 밸런스 패치 이후(한 번에) → 0.4 = AI 개선. 상세 = ops/VERSIONS.md. 남은 AI 카드(T32c, T34b~e)는 0.4 로 미룸.

## 미병합 실측 2026-10-23

기준: `git merge-base --is-ancestor <branch> HEAD` (kd-develop/kd-final, merge/0.2-final @0384fd9f). 미병합 커밋 수 = `git rev-list --count HEAD..<branch>`.

실제 미병합 (행 · 브랜치 @tip · 미병합 커밋 수):
- T12 · measure/t12 @e6122ac · 1
- T15 · measure/t15 @a70a308 · 1
- T38c (=T46) · kd/t46-card-overflow @54e8a54 · 1
- T38h · t38h-fuzz-campaign @f1265517 · 2 (상태 실측 표의 e5f76ec 는 낡은 값; 실측 tip 은 f1265517)
- C1E1-s6 · kd/c1e1-s6-op1-assault @8c867869 · 1
- C1E1-u1 · kd/c1e1-u1-unit-audit @b13932d0 · 1
- C1E1-g3 · kd/g3-win-and-delay @65ba51e7 · 1 (지시에 따라 상태 건드리지 않음, assigned 유지)

브랜치 없음 (목록만, 상태 유지):
- M2 · kd/m2-morale-camp (assigned) — ref 없음

브랜치 ref 는 없으나 tip 커밋이 조상이라 merged 처리:
- C1E1-d2 · kd/c1e1-d2-econ (tip 84880d0 은 merge/0.2-final 조상)

상태 밖 running (미계산): T11 kd-rerender @467dafc · T32d kd/t32d-pioneer-art @2118f9b · T33c kd/t33c-placement @c514821 · T33d kd/t33d-rail @c514821 (커밋 공유) · T44 kd/t44-smoke @1c4036d

브랜치 없음 (queued, 상태 유지): T13 · T32c · T34b~e · T39e/f/g/h/j (blocked T11) · T48b · T48e · C1E1-m2 · C1E1-g2 · C1E1-d1 · C1E1-t1 · C1E1-p1 · C1E1-q1 · C1E1-q2 · C1E1-v1 · NEW-1~5 (신규 행, 브랜치 미기재)
