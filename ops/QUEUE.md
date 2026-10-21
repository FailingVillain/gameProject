# QUEUE - 승인된 작업 id 순서표 (갱신 2026-10-22)

등급: **필수 = D (G/下/中/上, 모델) · H (핫스팟 0~3)**. 선택 메모 = R (R1 국소/R2 모듈 간/R3 결정성·저장·리플레이) · S (S<=60k/M<=150k/L>200k 분할) · W (light/heavy).
상태: queued / assigned / running / wip-ready / merged / closed / blocked(사유). 전이 주체 = ops/PIPELINE.md. 카드 = `korean-division/tasks/Tnn-<slug>.md`. 쓰는 사람: 지휘자(행 추가·assigned) · 작업 에이전트(자기 카드만) · merger(merged) · release(closed).
wip/* 복원 작업(T01 T03 T04 T05 T07): 역할 란의 앞 역할이 main 에서 브랜치를 새로 만들어 패치/브랜치를 복원·검증하고 `wip-ready` 로 끝낸다. merger 는 그 뒤.

| id | 제목 | 역할 | 등급 | 선행 | 상태 |
|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP + UI, 저장 트랜잭션 수정 포함) | feature → merger | D中 R2 SM H1 Wlight | 없음 | assigned |
| T02 | wip/bigbldg 병합 (건물 크기 확대) | merger | D中 R2 SM H2 Wlight | 없음 | merged (8d22ad5) |
| T03 | 참호 돌파 (wip-resolved/breach 검증 → 병합) | sim → merger | D上 R3 SM H3 Wheavy | 없음 (D17 기본값 승인) | wip-ready (wip/breach2) |
| T04 | wip/orderq 복원·병합 (사격 통제 · 명령 대기열) | sim → merger | D上 R3 SM H3 Wlight | T03 | queued |
| T05 | wip/ai3b2 복원·병합 (전술 AI 3B) | sim → merger | D上 R3 SM H3 Wheavy | T04 | queued |
| T06 | 사상자 속도 조정 (wip/casualty) | sim | D中~上 R3 SM H2 Wheavy | D19 | blocked(D19) |
| T07 | 철도 지형 (wip/rail 복원, 장갑열차 제외) | feature → merger | D上 R2 SM H2 Wlight | 없음 (T02 병합됨) | assigned |
| T08 | 조작 UI 묶음 + UI 종이 질감 개편 | feature | D中 R1 SM H1 Wlight | T01 | queued |
| T09 | 캠페인 수직 슬라이스 (0-1→0-2→1-1 한 사이클) | feature | D上 R3 SM H2 Wlight | T01(저장 트랜잭션) | queued |
| T10 | 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) | sim | D上 R3 SM H3 Wheavy | D15 | blocked(D15) |
| T11 | 전 유닛 재렌더 (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | 병합 큐 종료 뒤 렌더 3칸 예약 | queued |
| T12 | 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 (T06 은 D19 뒤 재측정) | queued |
| T13 | release (전체 selftest · 골든 · publish · 기록) | release | D中 R2 SM H1 Wheavy | T12 | queued |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | docs | D下 R1 S H0 Wlight | 없음 | merged (b5918a8 · a4c79c7 · docs-fable) |
| T15 | 밤 soak 갱신 → 전투 문제 9건 재보고 | measure | D中 R1 S H0 Wheavy | T12 | queued |
| T16 | trench_assault 하락 원인 조사 (D12 A 재측정) | scout→sim | D上 R2 M H2 Wheavy | T03 | wip-ready |
| T17 | 지휘 편의 (미션 중 저장 · 전과 보고(wip/battlereport 복원) · 교전 알림/미니맵) | feature | D中 R2 SM H2 Wlight | T08 | queued |
| T18 | 한국군 타찬카 (기마 포병 horseart 병합됨) | art+feature | D中 R1 SM H1 Wlight | 없음 | queued |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 구현 (wip/doctrines 복원 포함) | sim+feature | D上 R3 L (분할) H2 Wlight | T09 | queued |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = 사용자 U02) | docs | D下 R1 S H0 Wlight | U02 | blocked(U02) |

카드가 있는 것 = T01~T13 (+_TEMPLATE). T15~T20 과 카드 없는 브랜치(wip/chokefix D14 보류 · wip/sfxnew 효과음 · wip/gt-face-proto)는 발주 직전 spec/지휘자가 카드를 쓴다.
보류(이 표 밖): 장갑열차 · 하천 · 전술 AI 3C/3D · 지도 편집기 · 캠페인 순서 연결/챕터 1/엔딩.
