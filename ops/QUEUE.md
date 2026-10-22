# QUEUE - 승인된 작업 id 순서표 (갱신 2026-10-22)

등급: **필수 = D (G/下/中/上, 모델) · H (핫스팟 0~3)**. 선택 메모 = R (R1 국소/R2 모듈 간/R3 결정성·저장·리플레이) · S (S<=60k/M<=150k/L>200k 분할) · W (light/heavy).
상태: queued / assigned / running / wip-ready / merged / closed / blocked(사유). 전이 주체 = ops/PIPELINE.md. 카드 = `korean-division/tasks/Tnn-<slug>.md`. 쓰는 사람: 지휘자(행 추가·assigned) · 작업 에이전트(자기 카드만) · merger(merged) · release(closed).
wip/* 복원 작업(T01 T03 T04 T05 T07): 역할 란의 앞 역할이 main 에서 브랜치를 새로 만들어 패치/브랜치를 복원·검증하고 `wip-ready` 로 끝낸다. merger 는 그 뒤.

| id | 제목 | 역할 | 등급 | 선행 | 상태 |
|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP + UI, 저장 트랜잭션 수정 포함) | feature → merger | D中 R2 SM H1 Wlight | 없음 | wip-ready → 리뷰 수정 중 (blocker 1건) |
| T02 | wip/bigbldg 병합 (건물 크기 확대) | merger | D中 R2 SM H2 Wlight | 없음 | merged (8d22ad5) |
| T03 | 참호 돌파 (wip-resolved/breach 검증 → 병합) | sim → merger | D上 R3 SM H3 Wheavy | 없음 (D17 기본값 승인) | wip-ready (자기 브랜치 참조) |
| T04 | wip/orderq 복원·병합 (사격 통제 · 명령 대기열) | sim → merger | D上 R3 SM H3 Wlight | T03 | assigned |
| T05 | wip/ai3b2 복원·병합 (전술 AI 3B) | sim → merger | D上 R3 SM H3 Wheavy | T04 | queued |
| T06 | 사상자 속도 조정 (wip/casualty) | sim | D中~上 R3 SM H2 Wheavy | D19 | 정리됨 (J1 A 적용: 명중률 변경 제거·키 기본 1.0, 목표 미달 — wip/casualty2 0722493) |
| T07 | 철도 지형 (wip/rail 복원, 장갑열차 제외) | feature → merger | D上 R2 SM H2 Wlight | 없음 (T02 병합됨) | wip-ready (자기 브랜치 참조) |
| T08 | 조작 UI 묶음 + UI 종이 질감 개편 | feature | D中 R1 SM H1 Wlight | T01 | wip-ready (자기 브랜치 참조) |
| T09 | 캠페인 수직 슬라이스 (0-1→0-2→1-1 한 사이클) | feature | D上 R3 SM H2 Wlight | T01(저장 트랜잭션) | assigned |
| T10 | 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) | sim | D上 R3 SM H3 Wheavy | 없음(D15 A 확정); H3 직렬: T03 wip-ready 뒤 스택 | assigned |
| T11 | 전 유닛 재렌더 (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | 병합 큐 종료 뒤 렌더 3칸 예약 | queued |
| T12 | 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 (T06 은 D19 뒤 재측정) | wip-ready (measure/t12 e6122ac) |
| T13 | release (전체 selftest · 골든 · publish · 기록) | release | D中 R2 SM H1 Wheavy | T12 | queued |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | docs | D下 R1 S H0 Wlight | 없음 | merged (b5918a8 · a4c79c7 · docs-fable) |
| T15 | 밤 soak 갱신 → 전투 문제 9건 재보고 | measure | D中 R1 S H0 Wheavy | T12 | queued |
| T16 | trench_assault 하락 원인 조사 (D12 A 재측정) | scout→sim | D上 R2 M H2 Wheavy | T03 | wip-ready (kd/t16-trench 8b38b29; 채택 기준 미달 — Z1 참고) |
| T17 | 지휘 편의 (미션 중 저장 · 전과 보고(wip/battlereport 복원) · 교전 알림/미니맵) | feature | D中 R2 SM H2 Wlight | T08 | assigned |
| T18 | 한국군 타찬카 (기마 포병 horseart 병합됨) | art+feature | D中 R1 SM H1 Wlight | 없음 | closed (이미 main: tachanka 스냅샷 a1a35e6, 2026-10-22 확인) |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 구현 (wip/doctrines 복원 포함) | sim+feature | D上 R3 L (분할) H2 Wlight | T09 | queued |
| T21 | wip/sfxnew 복원 (효과음: sounds.json · SfxDirector · SOUND_POLICY) | feature | D中 R1 SM H1 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T22 | wip/gt-face-proto 재개 (얼굴 프로토타입, game-tools) | art | D中 R1 S H0 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T23 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md, 코드 없음) | spec | D中 R1 S H0 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T24 | D08 수정안 S1~S3 (대리 지휘 범위 · 관측반 전진 한계 · 돌격 제압 확인) | sim | D上 R3 SM H3 Wheavy | T04 위 스택 | wip-ready (kd/t24-s1s3) |
| T25 | 착검 멈춤 범위 = 모든 돌격 (D02 A); 전환 거리 3칸 유지 (D01 B, 코드 변경 없음) | sim | D中 R3 S H3 Wlight | T03 위 스택 | assigned |
| T26 | 레거시 이메일 차단 git 훅 설치 (D20 A) | docs | D下 R1 S H0 Wlight | 없음 | assigned |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = 사용자 U02) | docs | D下 R1 S H0 Wlight | U02 | blocked(U02) |

**정책 (사용자 2026-10-22, 강조): WIP 는 병합하지 않는다. WIP 작업은 재개해서 완료(wip-ready)까지만 한다.** merger 는 발주하지 않는다. 선행이 '병합'인 카드는 선행 카드의 wip 브랜치 위에 쌓아(스택) 진행한다. main 에는 WIP 가 들어가지 않는다(사용자 지시 전까지).

**실행 순서 (task list)**: 1차 T03 · T07 · T01 (진행) + T18 · T11 · T21 · T22 · T23 → 2차 T04(T03 위) → T05(T04 위) → T08 · T09(T01 위) → T17(T08 위) → T19(T09 위) → T12 · T15 · T16 · T13(WIP 측정은 스택 브랜치 위에서) | 막힘 T06(D19) T10(D15) T20(U02) | choke(D14) 보류.

카드가 있는 것 = T01~T13 (+_TEMPLATE). T15~T20 과 카드 없는 브랜치(wip/chokefix D14 보류 · wip/sfxnew 효과음 · wip/gt-face-proto)는 발주 직전 spec/지휘자가 카드를 쓴다.
보류(이 표 밖): 장갑열차 · 하천 · 전술 AI 3C/3D · 지도 편집기 · 캠페인 순서 연결/챕터 1/엔딩.
