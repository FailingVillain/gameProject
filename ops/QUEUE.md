# QUEUE - 승인된 작업 id 순서표 (2026-10-22)

등급: **필수 = D (G/下/中/上, 모델) · H (핫스팟 0~3)**. 선택 메모(카드 참고용, 없어도 됨) = R (R1 국소/R2 모듈 간/R3 결정성·저장·리플레이) · S (S<=60k/M<=150k/L>200k 분할) · W (light/heavy). 아래 표의 R·S·W 는 '선택'.
상태: queued / assigned / running / wip-ready / merged / closed / blocked. 카드 = korean-division/tasks/T-xx-*.md. 정보 불분명 = '확인 필요'.
출처: HANDOFF '승인 대기열'(2026-10-22) + 병합 큐 + 문서 재정비 + 재렌더 + 통합 측정 + release.

| id | 제목 | 역할 | 등급 | 선행 | 상태 |
|---|---|---|---|---|---|
| T01 | wip/gemini-import 병합 (캠페인 WIP + UI, 저장 트랜잭션 수정 포함) | merger | D中 R2 SM H1 Wlight | 없음 | queued |
| T02 | wip/bigbldg 병합 (건물 크기 확대) | merger | D中 R2 SM H2 Wlight | 없음 | queued |
| T03 | wip/breach 병합 (참호 돌파) | merger | D上 R3 SM H3 Wheavy | T01 불필요; D17 승인 | queued |
| T04 | wip/orderq 병합 (사격 통제 · 명령 대기열) | merger | D上 R3 SM H3 Wlight | T03 | queued |
| T05 | wip/ai3b2 병합 (전술 AI 3B, 큐 끝 전체 selftest 1회) | merger | D上 R3 SM H3 Wheavy | T04 | queued |
| T06 | 사상자 속도 조정 (wip/casualty) | sim | D中~上 R3 SM H2 Wheavy | 돌격보병+대형 완화(main 병합됨); D19 | blocked(D19) |
| T07 | 철도 지형 (wip/rail, 장갑열차 제외) | merger → feature | D上 R2 SM H2 Wlight | T02 | queued |
| T08 | 조작 UI 묶음 + UI 종이 질감 개편 | feature | D中 R1 SM H1 Wlight | T01 | queued |
| T09 | 캠페인 수직 슬라이스 (0-1→0-2→1-1 한 사이클) | feature | D上 R3 SM H2 Wlight | T01(저장 트랜잭션) | queued |
| T10 | 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) | sim | D上 R3 SM H3 Wheavy | 알파 ① 병합됨; D15 | blocked(D15) |
| T11 | 전 유닛 재렌더 (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | 병합 큐 종료 뒤 렌더 3칸 예약 | queued |
| T12 | 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 T06 | queued |
| T13 | release (전체 selftest · 골든 · publish · 기록) | release | D中 R2 SM H1 Wheavy | T01~T12 | queued |
| T14 | 문서 재정비 (4층 구조, docs 덮어쓰기 커밋) | docs | D下 R1 S H0 Wlight | 이사 검증 | running(docs-roles) |
| T15 | 밤 soak 갱신 → 전투 문제 9건 재보고 | measure | D中 R1 S H0 Wheavy | T12 | queued |
| T16 | trench_assault 하락 원인 조사 (D12) | scout→sim | D上 R2 M H2 Wheavy | T03 | queued |
| T17 | 지휘 편의 (미션 중 저장 · 전과 보고 · 교전 알림/미니맵) | feature | D中 R2 SM H2 Wlight | T08 | queued |
| T18 | 한국군 타찬카 (기마 포병 병합 뒤) | art+feature | D中 R1 SM H1 Wlight | 기마 포병 병합 확인 필요 | queued |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 구현 | sim+feature | D上 R3 L (분할) H2 Wlight | T09 | queued |
| T20 | 저장소 이동 마무리 (맨 마지막, 모든 에이전트 종료 뒤) | docs | D下 R1 S H0 Wlight | T13 | queued |

보류(이 표 밖): 장갑열차 · 하천 · 전술 AI 3C/3D · 지도 편집기 · 캠페인 순서 연결/챕터 1/엔딩.
