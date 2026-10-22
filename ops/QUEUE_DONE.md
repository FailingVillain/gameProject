# QUEUE_DONE - QUEUE.md 에서 이동한 merged/closed/done 카드 행 (2026-10-23 경량화, 행 원문 보존)

> 현행 대기열 = ops/QUEUE.md. 아래는 이동 시점의 두 표(옛 요약 표 · 옛 '상태 실측' 표) 원문 행이다. 상태가 바뀌면 지휘자가 QUEUE.md 에서 여기로 옮긴다.

## 요약 표 (id | 제목 | 역할 | 등급 | 선행 | 상태)

| id | 제목 | 역할 | 등급 | 선행 | 상태 |
|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP + UI, 저장 트랜잭션 수정 포함) | feature → merger | D中 R2 SM H1 Wlight | 없음 | merged (2026-10-22 is-ancestor 확인: gemini-import 63080df) · 이전: wip-ready (리뷰 수정 커밋 63080df · kd-gemini-import · base main) |
| T02 | wip/bigbldg 병합 (건물 크기 확대) | merger | D中 R2 SM H2 Wlight | 없음 | merged (8d22ad5) |
| T03 | 참호 돌파 (wip-resolved/breach 검증 → 병합) | sim → merger | D上 R3 SM H3 Wheavy | 없음 (D17 기본값 승인) | merged (2026-10-22 is-ancestor 확인: wip/breach2 6ad6c50 (B1 ffc4129)) · 이전: wip-ready (자기 브랜치 참조) |
| T04 | wip/orderq 복원·병합 (사격 통제 · 명령 대기열) | sim → merger | D上 R3 SM H3 Wlight | T03 | merged (2026-10-22 is-ancestor 확인: wip/orderq2 0377f8a (B3 d1947b0)) · 이전: assigned (wip/orderq2 0377f8a · kd-orderq2 · base T03; wip-ready 선언 확인 필요) |
| T05 | wip/ai3b2 복원·병합 (전술 AI 3B) | sim → merger | D上 R3 SM H3 Wheavy | T04 | merged (2026-10-22 is-ancestor 확인: ai3b2 f74a675 (B4 7d5c124)) · 이전: wip-ready (ai3b2 f74a675 · kd-ai3b2 · base T04) |
| T06 | 사상자 속도 조정 (wip/casualty) | sim | D中~上 R3 SM H2 Wheavy | D19 | closed (D19 C 폐기 — 사용자 2026-10-22; wip/casualty2 inert 보존) |
| T07 | 철도 지형 (wip/rail 복원, 장갑열차 제외) | feature → merger | D上 R2 SM H2 Wlight | 없음 (T02 병합됨) | merged (2026-10-22 is-ancestor 확인: rail 9f513e7 (C1 c7929fe)) · 이전: wip-ready (자기 브랜치 참조) |
| T08 | 조작 UI 묶음 + UI 종이 질감 개편 | feature | D中 R1 SM H1 Wlight | T01 | merged (2026-10-22 is-ancestor 확인: t08-ui-bundle 2e9aef5) · 이전: wip-ready (자기 브랜치 참조) |
| T09 | 캠페인 수직 슬라이스 (0-1→0-2→1-1 한 사이클) | feature | D上 R3 SM H2 Wlight | T01(저장 트랜잭션) | merged (2026-10-22 is-ancestor 확인: t09-camp-slice 0c82a47) · 이전: wip-ready (t09-camp-slice 0c82a47 · kd-t09 · base T01; T29b 가 대체) |
| T10 | 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) | sim | D上 R3 SM H3 Wheavy | 없음(D15 A 확정); H3 직렬: T03 wip-ready 뒤 스택 | merged (578ef29, 병합 라인 B10; FV 57/Replay 22 재번호) |
| T10b | 난이도 소비처 (전령 사로잡힘 · 유선 끊김, commsRules 기본 끔) + AI 배수 적용 설계 | sim | D上 R2 S H1 Wlight | T10 위 스택 | merged (kd/t10b-difficulty 4d74ce9 는 main 조상) |
| T11b | 엎드려쏘기 넓은 칸 에셋 가져오기 (보병 25 유닛, frame_wide 176/192x160) | art | D中 R1 S H0 Wheavy | gt-prone-fix 2694c89 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/prone-wide-assets · kd-prone-wide-assets) |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | docs | D下 R1 S H0 Wlight | 없음 | merged (b5918a8 · a4c79c7 · docs-fable) |
| T16 | trench_assault 하락 원인 조사 (D12 A 재측정) | scout→sim | D上 R2 M H2 Wheavy | T03 | merged (2026-10-22 is-ancestor 확인: kd/t16-trench 2cb118c (B12 c907fa5)) · 이전: wip-ready (kd/t16-trench 8b38b29; 채택 기준 미달 — Z1 참고) |
| T17 | 지휘 편의 (미션 중 저장 · 전과 보고(wip/battlereport 복원) · 교전 알림/미니맵) | feature | D中 R2 SM H2 Wlight | T08 | merged (2026-10-22 is-ancestor 확인: t17-cmd-convenience 0eb92e8) · 이전: wip-ready (t17-cmd-convenience 0eb92e8 · kd-t17 · base T08; 확인 필요) |
| T18 | 한국군 타찬카 (기마 포병 horseart 병합됨) | art+feature | D中 R1 SM H1 Wlight | 없음 | closed (이미 main: tachanka 스냅샷 a1a35e6, 2026-10-22 확인) |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 구현 (wip/doctrines 복원 포함) | sim+feature | D上 R3 L (분할) H2 Wlight | T09 | closed (분할됨: T19a~d, spec/t19-split aea476b) |
| T21 | wip/sfxnew 복원 (효과음: sounds.json · SfxDirector · SOUND_POLICY) | feature | D中 R1 SM H1 Wlight | 없음 | merged (2026-10-22 is-ancestor 확인: sfxnew aaba414) · 이전: wip-ready (자기 브랜치 참조) |
| T22 | wip/gt-face-proto 재개 (얼굴 프로토타입, game-tools) | art | D中 R1 S H0 Wlight | 없음 | merged (2026-10-22 is-ancestor 확인: gt-face-proto bc52007) · 이전: wip-ready (자기 브랜치 참조) |
| T23 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md, 코드 없음) | spec | D中 R1 S H0 Wlight | 없음 | merged (2026-10-22 is-ancestor 확인: spec/t23-campaign-home 42fa456) · 이전: wip-ready (자기 브랜치 참조) |
| T24 | D08 수정안 S1~S3 (대리 지휘 범위 · 관측반 전진 한계 · 돌격 제압 확인) | sim | D上 R3 SM H3 Wheavy | T04 위 스택 | merged (2026-10-22 is-ancestor 확인: kd/t24-s1s3 07ac80c (B5 89ca250)) · 이전: wip-ready (kd/t24-s1s3) |
| T25 | 착검 멈춤 범위 = 모든 돌격 (D02 A); 전환 거리 3칸 유지 (D01 B, 코드 변경 없음) | sim | D中 R3 S H3 Wlight | T03 위 스택 | merged (97472a0, 병합 라인 B7) |
| T26 | 레거시 이메일 차단 git 훅 설치 (D20 A) | docs | D下 R1 S H0 Wlight | 없음 | done (훅 설치됨 .git/hooks pre-commit·commit-msg; 2026-10-22) |
| T35 | IsAi 리플레이 머리말·체크섬 복원 (AF1, FV 60 / Replay 24) | sim | D上 R3 S H2 Wlight | 없음 (main) | merged (merge/0.2 E1 122bfff) |
| T32a | 전투공병 유닛 데이터 infantry_pioneer (wireCutter·수류탄2·한국군 전용) | feature | D中 R1 S H1 Wlight | ai3b2 위 스택 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/t32a-pioneer 26cc9fd) |
| T32b | 전술 AI 연막 수류탄 (개활 횡단·사격 아래·참호 돌격 smokeHook, 기본 꺼짐, FV 56→예약 62) | sim | D上 R3 SM H2 Wheavy | ai3b2 위 스택 | merged-in-line1 (b5f1da5, FV 65) |
| T43 | 병종 기호 곡사포·저격 추가 | feature | D下 R1 S H0 Wlight | 없음 (main) | merged (2026-10-22 is-ancestor 확인: kd/t43-branch-symbols d45b18f) · 이전: wip-ready (kd/t43-branch-symbols b9c97a9) |
| T48a | 연대장 데이터 · 스키마 v8 · 로더 · 샘플 (연대장 풀) | feature | D中 R2 S H2 Wlight | T42d 위 스택 (06ee9bd) | merged (2026-10-22 is-ancestor 확인: t48a-commander-data) · 이전: wip-ready (브랜치 t48a-commander-data, 스택 base t42d 06ee9bd) |
| T48b1 | 장교 신원 키 (OfficerKey) · 주입/환수 · 전사/항상부상 · 대대장 주입 · 진급 Key · 종착 | sim | D上 R3 M H3 Wlight | T48a | merged (2026-10-22 is-ancestor 확인: t48b1-officer-identity fa8dff1 (G1)) · 이전: wip-ready (t48b1-officer-identity fa8dff1 · kd-t48b1 · T48a e31406d 위 스택 · FV 53 · 캠페인 v9, 병합 보류) |
| T48b2 | 연대장 기용/해임 · 장교 배정 · 참모 4슬롯 효과 (캠페인 층) · 작전 태그 정산 | feature | D中 H2 | T48b1 T48c | merged (2026-10-22 is-ancestor 확인: t48b2-assign) · 이전: wip-ready (t48b2-assign · kd-t48b2 · t48b1 fa8dff1 + t48c c24c423 스택 · 캠페인 v9 유지, 병합 보류) |
| T48c | 연대장 호감도 + 해임 시 소폭 하락 | feature | D中 R2 S H2 Wlight | T48b | merged (2026-10-22 is-ancestor 확인: t48c-favor c24c423) · 이전: wip-ready (t48c-favor c24c423, T48a 위 스택, 병합 보류) |
| T48d | 연대장·대대장·참모 관리 UI (탭 8/9/12/14) | feature | D中 R1 M H2 Wlight | T48b T48c | merged (2026-10-22 is-ancestor 확인: t48d-commander-ui) · 이전: wip-ready (branch t48d-commander-ui, 스택: t48b2-assign 위) |
| T39c | 하천 렌더(합성 여울 타일)·test_river 지도/미션 | feature | D中 R1 S H0 Wlight | river-r2 위 스택 | merged-in-line1 (post-E2 ac77417) |
| TC-A | 브리핑 규모 분리 + 정보 3단계 (C1E1) | feature | D中 H2 | 없음(base t42d) | merged (2026-10-22 is-ancestor 확인: kd/tca-briefing f7c6dcd) · 이전: wip-ready (kd/tca-briefing f7c6dcd, 사용자 검수 불필요 · kd-tca · base merge/0.2-ui, FV 불변, 스키마 8) |
| TC-B | SK2 경보 상태 (C1E1) | sim | D上 H3 | 없음(base merge/0.2) | merged (post-N2-25 기준 확인) · 이전: assigned (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) |
| TC-C | 이벤트 중대 명령 주입 + 잠금 D15 (C1E1) | sim | D上 H3 | 없음(base merge/0.2) | merged (post-N2-25 기준 확인) · 이전: assigned (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) |
| TC-D | funds · 외인 슬롯 · 통제 한도 · 가용 보급 (C1E1) | feature | D中 H2 | t48b2-assign | merged (2026-10-22 is-ancestor 확인: kd/tcd-funds-slots) · 이전: wip-ready (branch kd/tcd-funds-slots, 스키마 v11) |
| TC-E | favor.* · 변수 레지스트리 · 결정 런타임 (C1E1) | sim | D中~上 H2 | TC-D | merged (2026-10-22 is-ancestor 확인: kd/tce-vars 2dbada7) · 이전: wip-ready (kd/tce-vars 2dbada7) |
| TC-F | 미션 데이터 변형 · SK 플래그 · BT1 이탈 · pow.bandit (C1E1) | feature | D中 H2 | TC-D · TC-E | merged (post-N2-25 기준 확인) · 이전: assigned (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) |
| TC-G | 일본 철도 부속지 진입/사격 금지 (C1E1) | sim | D中~上 H3 | 없음(base merge/0.2) | merged (2026-10-22 is-ancestor 확인: kd/tcg-annex 9490879b) · 이전: wip-ready (9490879) |
| C1E1-m1 | MAP-A 지도 생성 (medium) | feature | D中 H0 | 없음 | merged (2026-10-22 is-ancestor 확인: kd/c1e1-m1-map-a) · 이전: wip-ready (kd/c1e1-m1-map-a) |
| C1E1-g1 | 에피소드 그래프 런타임 | sim | D上 H3 | 없음(base t48b2 80c11fb) | merged (2026-10-22 is-ancestor 확인: kd/c1e1-g1-episode-graph) · 이전: wip-ready (kd/c1e1-g1-episode-graph, 캠페인 스키마 v12, FV 없음) |
| C1E1-g4 | 목표 확장 (항복/N점/losses 변수) | sim | D上 H2 | 없음(base merge/0.2) | merged (post-N2-25 기준 확인) · 이전: assigned (spec/cards-c1e1, 계획 docs/C1E1_IMPLEMENTATION_PLAN.md) |
| C1E1-g5 | 피난민 비전투 유닛 | feature | D下~中 H1 | u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (b37ac32) |
| C1E1-u2 | C1E1 누락 유닛 정의 5종 (봉천 기마·야포, 철도수비대, 피난민, 의용병) | feature | D中 H1 | u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-u2-units 8061b9a, merge/0.2-final 에 병합됨 H5-u2) |
| C1E1-g6 | unlock.* 편성 해금 | feature | D中 H2 | TC-D | merged (post-N2-25 기준 확인) · 이전: wip-ready (7b2bcdf) |
| C1E1-d2 | E1 시작 상태 + 경제 상수 | docs | D下 H1 | TC-D | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-d2-econ 84880d0) |
| M1 | 아군 사기 ↔ 제압 하락 속도 연동 (세력 사기 0~100 기본 50, 배수 0.75+0.5·m/100, FV 임시 +1; DECISIONS BH2) | sim | D上 R3 SM H3 Wheavy | 없음(base merge/0.2) | merged (post-N2-25 기준 확인) · 이전: assigned (kd/m1-morale-suppress, opus) |
| T55i | 민가 42종(66키) 임포트 | art | D中 R1 S H0 Wlight | T55s 등 민가 체인 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd-t55i, 사용자 시트 승인) |
| T56a | 스프라이트 칸 B v2 포맷 도구 + 보병 1종 시범 | art | D中 R1 S H0 Wlight | 없음 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/t56a-pack-sheets, kd-t56a, 사용자 시트 승인) |
| C1E1-s1 | BT1 미션 JSON | feature | D中 H1 | m1 u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s1-bt1 ba97f61; 리뷰 수정 진행 중) |
| C1E1-s2 | SK1 미션 JSON | feature | D中 H1 | m1 u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s2-sk1 9a6760f; 리뷰 수정 진행 중) |
| C1E1-s3 | SK2 미션 JSON | feature | D中 H1 | m1 u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s3-sk2 c5d36aa; 리뷰 수정 진행 중) |
| C1E1-s4 | SK3 미션 JSON | feature | D中 H1 | m2 u1 g5 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s4-sk3 0139f6d; 리뷰 수정 진행 중) |
| C1E1-s5 | SK4 미션 JSON | feature | D中 H1 | m2 u1 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s5-sk4 151c550; 리뷰 수정 진행 중) |
| C1E1-s7 | OP1 포위 미션 JSON | feature | D中 H1 | s6 | merged (post-N2-25 기준 확인) · 이전: wip-ready (kd/c1e1-s7-op1-envelop 4b032dd; 리뷰 수정 진행 중) |
| T49a | 저장 호환 정책 적용 1 (메시지 · 이행 .bak · 목록 회색 표시, ops/SAVE_COMPAT_POLICY.md) | feature | D中 R2 S H1 Wlight | 없음 | merged (2026-10-22 is-ancestor 확인: t49a-save-messages 48961134) · 이전: wip-ready (브랜치 t49a-save-messages, 스택 base t48a e31406d) |
| T49b | 저장 호환 정책 적용 2 (설정 관대 읽기 · 사용자 지도/지휘관 건너뛰기 · 테스트) | feature | D中 R2 S H1 Wlight | T48a | merged-in-line2 (11e7a7b, 병합 커밋 20132b4, merge/0.2-ui) |
| T49c | 저장 호환 출시 게이트 (캠페인 v1..현재 옛 샘플 테스트) | feature | D下 R2 S H1 Wlight | T49a | merged-in-line2 (11e7a7b, 병합 커밋 20132b4, merge/0.2-ui) |
| T38n | 게임이 env KD_MONITOR 를 따름 (창 모드만) | feature | D中 H1 | 없음 | merged (2026-10-22 is-ancestor 확인: kd/t38n-monitor-env a13672a) · 이전: wip-ready (kd/t38n-monitor-env @a13672a · kd-t38n) |
| T55m | 민가 import · 테마 키 · 42종 레지스트리 (housedata) | art/docs → merger | D下 H0 | T55s 필드 정의 | merged (post-N2-25 기준 확인) · 이전: wip-ready (t55m-house-data · kd-develop/kd-t55m-house-data · 무병합) |

## 상태 실측 표 (id | 상태 | branch @tip | 워크트리 | base(스택))

| id | 상태 | branch @tip | 워크트리 | base(스택) |
|---|---|---|---|---|
| T01 | merged (2026-10-22 is-ancestor 확인: gemini-import 63080df) · 이전: wip-ready | gemini-import @63080df | kd-gemini-import | main |
| T02 | closed (merged 8d22ad5) | - | - | - |
| T03 | merged (2026-10-22 is-ancestor 확인: wip/breach2 6ad6c50 (B1 ffc4129)) · 이전: wip-ready | wip/breach2 @6ad6c50 | kd-breach2 | main |
| T04 | merged (2026-10-22 is-ancestor 확인: wip/orderq2 0377f8a (B3 d1947b0)) · 이전: assigned (확인 필요) | wip/orderq2 @0377f8a | kd-orderq2 | T03 |
| T05 | merged (2026-10-22 is-ancestor 확인: ai3b2 f74a675 (B4 7d5c124)) · 이전: wip-ready | ai3b2 @f74a675 | kd-ai3b2 | T04 |
| T06 | closed (D19 C 폐기) | wip/casualty2 @0722493 | kd-casualty2 | ? |
| T06b | closed | measure/t06b @e241b94 | kd-meas-t06b (+ d19c·tip 임시) | ? |
| T07 | merged (2026-10-22 is-ancestor 확인: rail 9f513e7 (C1 c7929fe)) · 이전: wip-ready | rail @9f513e7 | kd-rail | main |
| T08 | merged (2026-10-22 is-ancestor 확인: t08-ui-bundle 2e9aef5) · 이전: wip-ready | t08-ui-bundle @2e9aef5 | kd-t08 | T01 |
| T09 | merged (2026-10-22 is-ancestor 확인: t09-camp-slice 0c82a47) · 이전: wip-ready | t09-camp-slice @0c82a47 | kd-t09 | T01 |
| T10 | merged (578ef29, 병합 라인 B10; FV 57/Replay 22 재번호) | kd/t10-comms-b @1c91c96 | kd-t10 | T03 |
| T10b | merged (kd/t10b-difficulty 4d74ce9 는 main 조상) | kd/t10b-difficulty @4d74ce9 | kd-t10b | T10 |
| T14 | closed (merged) | - | - | main |
| T16 | merged (2026-10-22 is-ancestor 확인: kd/t16-trench 2cb118c (B12 c907fa5)) · 이전: wip-ready (보류 0.3) | kd/t16-trench @2cb118c | kd-t16 | T03 |
| T16b | closed (분석 종결) | kd/t16b-rootcause @e724437 | kd-t16b | ? |
| T17 | merged (2026-10-22 is-ancestor 확인: t17-cmd-convenience 0eb92e8) · 이전: wip-ready (확인 필요) | t17-cmd-convenience @0eb92e8 | kd-t17 | T08 |
| T18 | closed (main 반영) | - | - | main |
| T19 | closed (분할 T19a~d) | spec/t19-split @aea476b | kd-t19spec | T09 |
| T19a | merged (post-N2-25 기준 확인) · 이전: wip-ready | t19a-save-slots @43e69b1 | kd-t19a | T09 |
| T19b | merged (post-N2-25 기준 확인) · 이전: wip-ready (추정) | t19b-ammo-shells @622db02 | kd-t19b | T19a |
| T19b2 | merged (post-N2-25 기준 확인) · 이전: wip-ready | t19b2-synced @4980e7b | kd-t19b2 | T19b |
| T19c | merged (post-N2-25 기준 확인) · 이전: wip-ready | t19c-home-tabs @9260e36 | kd-t19c | T19b2 |
| T19d | merged (post-N2-25 기준 확인) · 이전: wip-ready | t19d-doctrines @04a9149 | kd-t19d | T19c |
| T21 | merged (2026-10-22 is-ancestor 확인: sfxnew aaba414) · 이전: wip-ready | sfxnew @aaba414 | kd-sfxnew | main |
| T22 | merged (2026-10-22 is-ancestor 확인: gt-face-proto bc52007) · 이전: wip-ready | gt-face-proto @bc52007 | gt-develop/gt-face-proto | main |
| T23 | merged (2026-10-22 is-ancestor 확인: spec/t23-campaign-home 42fa456) · 이전: wip-ready (카드 설계 문서) | spec/t23-campaign-home @42fa456 | kd-t23 | main |
| T24 | merged (2026-10-22 is-ancestor 확인: kd/t24-s1s3 07ac80c (B5 89ca250)) · 이전: wip-ready | kd/t24-s1s3 @07ac80c | 없음 (kd-t24 는 T37 체크아웃) | ai3b2 |
| T24x (crossing) | merged (post-N2-25 기준 확인) · 이전: wip-ready | kd/t24-crossing-fix @390763a | kd-t24-crossing-fix | T24 |
| T25 | merged (97472a0, 병합 라인 B7) | kd/t25-bayonet-all @29695c3 | kd-t25 | ai3b2 |
| T26 | closed (done) | - | - | main |
| T27 | merged (post-N2-25 기준 확인) · 이전: wip-ready | t27-cmd-ui2 @4cf5c45 | kd-t27 | main |
| T28 | merged (2026-10-22 is-ancestor 확인: t28-cover-forest c577c0f (C2 6a5f199)) · 이전: wip-ready | t28-cover-forest @c577c0f | kd-t28 | T07 rail |
| T29 | closed (T29b 대체) | t29-campaign-economy-json @783e7ce | kd-t29 | T01 |
| T29b | merged (post-N2-25 기준 확인) · 이전: wip-ready | t29b-on-t09 @a89b4d2 | kd-t29b | T09 |
| T30a | merged (post-N2-25 기준 확인) · 이전: wip-ready | t30a-train-move @863f9d1 | kd-t30a | rail |
| T30b | merged (post-N2-25 기준 확인) · 이전: wip-ready | t30b-train-weapons @ca1cdb8 | 없음 (kd-t30b 는 T36 체크아웃) | T30a |
| T30c | merged (post-N2-25 기준 확인) · 이전: wip-ready | t30c-freight-objective @3de60d5 | kd-t30c | T30b |
| T30d | merged (post-N2-25 기준 확인) · 이전: wip-ready | t30d-train-art @8e51e5a | kd-t30d-train-art | T30a (추정) |
| T30e | merged (post-N2-25 기준 확인) · 이전: wip-ready | t30e-demo-mission @7f46a4c | kd-t30e | T30c |
| T31a | merged (post-N2-25 기준 확인) · 이전: wip-ready (보류 0.3) | kd/t31a-regroup @97ed026 | kd-t31a | ai3b2 |
| T31b | closed | measure/t31b @05c228d / kd/endline-50 @476ceec | kd-meas-t31b · kd-endline50 | main (?) |
| T31c | merged (post-N2-25 기준 확인) · 이전: wip-ready (켜기 0.4) | kd/t31c-surrender @e9cd527 | kd-t31c-surrender | ai3b2 |
| T32a | merged (post-N2-25 기준 확인) · 이전: wip-ready | kd/t32a-pioneer @26cc9fd | kd-t32a | ai3b2 |
| T32b | merged-in-line1 (b5f1da5, FV 65 · 기본 꺼짐) | kd/t32b-ai-smoke @33284fa | kd-t32b | ai3b2 |
| T33a | merged (post-N2-25 기준 확인) · 이전: wip-ready | kd/t33a-map-model @c5b30f4 | kd-t33a-map-model | main |
| T33b | merged (post-N2-25 기준 확인) · 이전: wip-ready | kd/t33b-map-ui @5207b8f | kd-t33b-map-ui | T33a |
| T34a | merged-in-line1 (63a42b4, FV 66) | kd/t34a-suppress @3d0f4de | kd-t34a | ai3b2 |
| T35 | merged (merge/0.2 E1 122bfff) | kd/isai-restore @5c08a38 | kd-isai-restore | main |
| T36 | merged (2026-10-22 is-ancestor 확인: kd/immune-target-fix 48bd6ce (D3 a51e507)) · 이전: wip-ready | kd/immune-target-fix @48bd6ce | 없음 (kd-t30b 체크아웃) | ? |
| T37 | merged (2026-10-22 is-ancestor 확인: kd/acting-delay-tier 7cfa0c2 (B6 efb0850)) · 이전: wip-ready | kd/acting-delay-tier @7cfa0c2 | 없음 (kd-t24 체크아웃) | ? |
| T38a | merged (2026-10-22 is-ancestor 확인: t38a-settings-keys 7274c71) · 이전: wip-ready | t38a-settings-keys @7274c71 | kd-t38a | T38b |
| T38b | merged (2026-10-22 is-ancestor 확인: t38b-help-overlay 93ce79f) · 이전: wip-ready | t38b-help-overlay @93ce79f | kd-t38b | T38g |
| T38d | merged (2026-10-22 is-ancestor 확인: kd/t38d-paper-ui 5f601bc (I7 ad6120a)) · 이전: wip-ready (사용자 그래픽 승인 2026-10-22) | kd/t38d-paper-ui @5f601bc | kd-t38d | merge/0.2-ui |
| T38e | merged (2026-10-22 is-ancestor 확인: t38e-unitlist-codex e9e1642 (F2 750117e)) · 이전: wip-ready (e9e1642, 완료·미커밋 없음, 스택 T38a 위) | t38e-unitlist-codex @7274c71 | kd-t38e | T38a |
| T38f | merged (2026-10-22 is-ancestor 확인: t38f-home-readonly-tabs b622d1d) · 이전: wip-ready | t38f-home-readonly-tabs @b622d1d | kd-t38f | T19d |
| T38g | merged (2026-10-22 is-ancestor 확인: t38g-ui-harness f5a76d4) · 이전: wip-ready | t38g-ui-harness @f5a76d4 | kd-t38g | T27 |
| T38k | merged (2026-10-22 is-ancestor 확인: t38k-fonts 07cb63f) · 이전: wip-ready | t38k-fonts @07cb63f | kd-t38k | T38a |
| T38m | merged (2026-10-22 is-ancestor 확인: t38m-overlay-declutter 2ca199c (F3 9faf9e8)) · 이전: wip-ready (스택: t38a 위, 병합 안 함) | t38m-overlay-declutter @2ca199c | kd-t38m | T38a |
| T39a | merged-in-line1 (post-E2 ac77417) | river-r1 @5b7b9f1 | kd-river-r1 | rail |
| T39b | merged-in-line1 (post-E2 ac77417) | river-r2 @8ada7a9 | kd-river-r2 | T39a |
| T39c | merged-in-line1 (post-E2 ac77417) | river-r3 @26df7f6 | kd-river-r3 | T39b |
| T39d | merged-in-line1 (post-E2 ac77417) | river-r4-bridge @9d4b7be | kd-river-r4 | T39c |
| T39i | merged (post-N2-25 기준 확인) · 이전: wip-ready | t39i-ruined-houses @66d6b95 | kd-t39i | main |
| T40 | merged (2026-10-22 is-ancestor 확인: kd/trains-in-annihilation 2de288d (D6)) · 이전: wip-ready | kd/trains-in-annihilation @2de288d | kd-trains-annih | T30e |
| T41 | merged (post-N2-25 기준 확인) · 이전: wip-ready | kd/crash-log @69320b1 | kd-crash-log | main |
| T42a | merged (post-N2-25 기준 확인) · 이전: wip-ready | t42a-fatigue-morale @ba621fe | kd-t42a | T38f |
| T42b | merged (post-N2-25 기준 확인) · 이전: wip-ready | t42b-promotion @51b92c3 | kd-t42b | T42a |
| T42c | merged (post-N2-25 기준 확인) · 이전: wip-ready | t42c-prisoners @66e7637 | kd-t42c | T42b |
| T42d | merged (post-N2-25 기준 확인) · 이전: wip-ready | t42d-schedule-log @06ee9bd | kd-t42d | T42c |
| T43 | merged (2026-10-22 is-ancestor 확인: kd/t43-branch-symbols d45b18f) · 이전: wip-ready | kd/t43-branch-symbols @d45b18f | kd-t43 | main |
| T45 | merged (2026-10-22 is-ancestor 확인: kd/t45-menu-hide 88735b1) · 이전: wip-ready | kd/t45-menu-hide @88735b1 | kd-t45 | main |
