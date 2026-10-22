# QUEUE - 승인된 작업 id 순서표 (갱신 2026-10-22)

등급: **필수 = D (G/下/中/上, 모델) · H (핫스팟 0~3)**. 선택 메모 = R (R1 국소/R2 모듈 간/R3 결정성·저장·리플레이) · S (S<=60k/M<=150k/L>200k 분할) · W (light/heavy).
상태: queued / assigned / running / wip-ready / merged / closed / blocked(사유). 전이 주체 = ops/PIPELINE.md. 카드 = `korean-division/tasks/Tnn-<slug>.md`. 쓰는 사람: 지휘자(행 추가·assigned) · 작업 에이전트(자기 카드만) · merger(merged) · release(closed).
wip/* 복원 작업(T01 T03 T04 T05 T07): 역할 란의 앞 역할이 main 에서 브랜치를 새로 만들어 패치/브랜치를 복원·검증하고 `wip-ready` 로 끝낸다. merger 는 그 뒤.

| id | 제목 | 역할 | 등급 | 선행 | 상태 |
|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP + UI, 저장 트랜잭션 수정 포함) | feature → merger | D中 R2 SM H1 Wlight | 없음 | wip-ready (리뷰 수정 커밋 63080df · kd-gemini-import · base main) |
| T02 | wip/bigbldg 병합 (건물 크기 확대) | merger | D中 R2 SM H2 Wlight | 없음 | merged (8d22ad5) |
| T03 | 참호 돌파 (wip-resolved/breach 검증 → 병합) | sim → merger | D上 R3 SM H3 Wheavy | 없음 (D17 기본값 승인) | wip-ready (자기 브랜치 참조) |
| T04 | wip/orderq 복원·병합 (사격 통제 · 명령 대기열) | sim → merger | D上 R3 SM H3 Wlight | T03 | assigned (wip/orderq2 0377f8a · kd-orderq2 · base T03; wip-ready 선언 확인 필요) |
| T05 | wip/ai3b2 복원·병합 (전술 AI 3B) | sim → merger | D上 R3 SM H3 Wheavy | T04 | wip-ready (ai3b2 f74a675 · kd-ai3b2 · base T04) |
| T06 | 사상자 속도 조정 (wip/casualty) | sim | D中~上 R3 SM H2 Wheavy | D19 | closed (D19 C 폐기 — 사용자 2026-10-22; wip/casualty2 inert 보존) |
| T07 | 철도 지형 (wip/rail 복원, 장갑열차 제외) | feature → merger | D上 R2 SM H2 Wlight | 없음 (T02 병합됨) | wip-ready (자기 브랜치 참조) |
| T08 | 조작 UI 묶음 + UI 종이 질감 개편 | feature | D中 R1 SM H1 Wlight | T01 | wip-ready (자기 브랜치 참조) |
| T09 | 캠페인 수직 슬라이스 (0-1→0-2→1-1 한 사이클) | feature | D上 R3 SM H2 Wlight | T01(저장 트랜잭션) | wip-ready (t09-camp-slice 0c82a47 · kd-t09 · base T01; T29b 가 대체) |
| T10 | 통신 알파 ②~④ (대포병 · 야간 · 행군 피로) | sim | D上 R3 SM H3 Wheavy | 없음(D15 A 확정); H3 직렬: T03 wip-ready 뒤 스택 | assigned |
| T10b | 난이도 소비처 (전령 사로잡힘 · 유선 끊김, commsRules 기본 끔) + AI 배수 적용 설계 | sim | D上 R2 S H1 Wlight | T10 위 스택 | wip-ready (kd/t10b-difficulty 4d74ce9 · kd-t10b · base T10) |
| T11 | 전 유닛 재렌더 (rerender-1, gt-brender, gt-rerender-prep) | art | D中 R1 SM H0 Wheavy | 병합 큐 종료 뒤 렌더 3칸 예약 | running (kd-rerender 467dafc · base main) |
| T12 | 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_CURRENT | measure | D中 R1 S H0 Wheavy | T03 T04 T05 (T06 은 D19 뒤 재측정) | wip-ready (measure/t12 e6122ac) |
| T13 | release (전체 selftest · 골든 · publish · 기록) | release | D中 R2 SM H1 Wheavy | T12 | queued |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | docs | D下 R1 S H0 Wlight | 없음 | merged (b5918a8 · a4c79c7 · docs-fable) |
| T15 | 밤 soak 갱신 → 전투 문제 9건 재보고 | measure | D中 R1 S H0 Wheavy | T12 | wip-ready (측정 a70a308 · kd-meas-t15 · 밸런스 보류 0.3) |
| T16 | trench_assault 하락 원인 조사 (D12 A 재측정) | scout→sim | D上 R2 M H2 Wheavy | T03 | wip-ready (kd/t16-trench 8b38b29; 채택 기준 미달 — Z1 참고) |
| T17 | 지휘 편의 (미션 중 저장 · 전과 보고(wip/battlereport 복원) · 교전 알림/미니맵) | feature | D中 R2 SM H2 Wlight | T08 | wip-ready (t17-cmd-convenience 0eb92e8 · kd-t17 · base T08; 확인 필요) |
| T18 | 한국군 타찬카 (기마 포병 horseart 병합됨) | art+feature | D中 R1 SM H1 Wlight | 없음 | closed (이미 main: tachanka 스냅샷 a1a35e6, 2026-10-22 확인) |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 구현 (wip/doctrines 복원 포함) | sim+feature | D上 R3 L (분할) H2 Wlight | T09 | closed (분할됨: T19a~d, spec/t19-split aea476b) |
| T21 | wip/sfxnew 복원 (효과음: sounds.json · SfxDirector · SOUND_POLICY) | feature | D中 R1 SM H1 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T22 | wip/gt-face-proto 재개 (얼굴 프로토타입, game-tools) | art | D中 R1 S H0 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T23 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md, 코드 없음) | spec | D中 R1 S H0 Wlight | 없음 | wip-ready (자기 브랜치 참조) |
| T24 | D08 수정안 S1~S3 (대리 지휘 범위 · 관측반 전진 한계 · 돌격 제압 확인) | sim | D上 R3 SM H3 Wheavy | T04 위 스택 | wip-ready (kd/t24-s1s3) |
| T25 | 착검 멈춤 범위 = 모든 돌격 (D02 A); 전환 거리 3칸 유지 (D01 B, 코드 변경 없음) | sim | D中 R3 S H3 Wlight | T03 위 스택 | assigned |
| T26 | 레거시 이메일 차단 git 훅 설치 (D20 A) | docs | D下 R1 S H0 Wlight | 없음 | done (훅 설치됨 .git/hooks pre-commit·commit-msg; 2026-10-22) |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = 사용자 U02) | docs | D下 R1 S H0 Wlight | U02 | blocked(U02) |
| T35 | IsAi 리플레이 머리말·체크섬 복원 (AF1, FV 60 / Replay 24) | sim | D上 R3 S H2 Wlight | 없음 (main) | wip-ready (kd/isai-restore 5c08a38) |
| T32a | 전투공병 유닛 데이터 infantry_pioneer (wireCutter·수류탄2·한국군 전용) | feature | D中 R1 S H1 Wlight | ai3b2 위 스택 | wip-ready (kd/t32a-pioneer 26cc9fd) |
| T32b | 전술 AI 연막 수류탄 (개활 횡단·사격 아래·참호 돌격 smokeHook, 기본 꺼짐, FV 56→예약 62) | sim | D上 R3 SM H2 Wheavy | ai3b2 위 스택 | wip-ready (kd/t32b-ai-smoke 33284fa) |
| T43 | 병종 기호 곡사포·저격 추가 | feature | D下 R1 S H0 Wlight | 없음 (main) | wip-ready (kd/t43-branch-symbols b9c97a9) |
| T48a | 연대장 데이터 · 스키마 v8 · 로더 · 샘플 (연대장 풀) | feature | D中 R2 S H2 Wlight | T42d 위 스택 (06ee9bd) | wip-ready (브랜치 t48a-commander-data, 스택 base t42d 06ee9bd) |
| T48b | 장교 신원 영속 · 진급 종착 · 배정 · 연대장 기용/해임 · 전사/항상부상 | sim | D上 R3 M H2 Wlight | T48a | queued |
| T48b1 | 장교 신원 키 (OfficerKey) · 주입/환수 · 전사/항상부상 · 대대장 주입 · 진급 Key · 종착 | sim | D上 R3 M H3 Wlight | T48a | wip-ready (t48b1-officer-identity fa8dff1 · kd-t48b1 · T48a e31406d 위 스택 · FV 53 · 캠페인 v9, 병합 보류) |
| T48c | 연대장 호감도 + 해임 시 소폭 하락 | feature | D中 R2 S H2 Wlight | T48b | wip-ready (t48c-favor c24c423, T48a 위 스택, 병합 보류) |
| T48d | 연대장·대대장·참모 관리 UI (탭 8/9/12/14) | feature | D中 R1 M H2 Wlight | T48b T48c | queued |
| T48e | 2단계 전투 연결 (기본 꺼짐 키 4개, FV 예약 필요) | sim | D上 R3 M H3 Wheavy | T48d · T10 · T24 | queued (blocked) |
| T39c | 하천 렌더(합성 여울 타일)·test_river 지도/미션 | feature | D中 R1 S H0 Wlight | river-r2 위 스택 | wip-ready (river-r3 26df7f6) |

**정책 (사용자 2026-10-22, 강조): WIP 는 병합하지 않는다. WIP 작업은 재개해서 완료(wip-ready)까지만 한다.** merger 는 발주하지 않는다. 선행이 '병합'인 카드는 선행 카드의 wip 브랜치 위에 쌓아(스택) 진행한다. main 에는 WIP 가 들어가지 않는다(사용자 지시 전까지).

**실행 순서 (task list)**: 1차 T03 · T07 · T01 (진행) + T18 · T11 · T21 · T22 · T23 → 2차 T04(T03 위) → T05(T04 위) → T08 · T09(T01 위) → T17(T08 위) → T19(T09 위) → T12 · T15 · T16 · T13(WIP 측정은 스택 브랜치 위에서) | 막힘 T06(D19) T10(D15) T20(U02) | choke(D14) 보류.

카드가 있는 것 = T01~T13 (+_TEMPLATE). T15~T20 과 카드 없는 브랜치(wip/chokefix D14 보류 · wip/sfxnew 효과음 · wip/gt-face-proto)는 발주 직전 spec/지휘자가 카드를 쓴다.
보류(이 표 밖): 장갑열차 · 하천 · 전술 AI 3C/3D · 지도 편집기 · 캠페인 순서 연결/챕터 1/엔딩.

## 상태 실측 (2026-10-22, `git branch -v` · `git worktree list` · 로그 13:3x~15:0x)

위 표의 상태 칸과 충돌하면 이 표가 우선한다. 행별 상세 = ops/CARD_INDEX.md. 워크트리 정리 = ops/WORKTREE_CLEANUP.md. 워크트리 = kd-develop/ 아래 (T38h 는 kd-t38h, T44 는 kd-t44).

| id | 상태 | branch @tip | 워크트리 | base(스택) |
|---|---|---|---|---|
| T01 | wip-ready | gemini-import @63080df | kd-gemini-import | main |
| T02 | closed (merged 8d22ad5) | - | - | - |
| T03 | wip-ready | wip/breach2 @6ad6c50 | kd-breach2 | main |
| T04 | assigned (확인 필요) | wip/orderq2 @0377f8a | kd-orderq2 | T03 |
| T05 | wip-ready | ai3b2 @f74a675 | kd-ai3b2 | T04 |
| T06 | closed (D19 C 폐기) | wip/casualty2 @0722493 | kd-casualty2 | ? |
| T06b | closed | measure/t06b @e241b94 | kd-meas-t06b (+ d19c·tip 임시) | ? |
| T07 | wip-ready | rail @9f513e7 | kd-rail | main |
| T08 | wip-ready | t08-ui-bundle @2e9aef5 | kd-t08 | T01 |
| T09 | wip-ready | t09-camp-slice @0c82a47 | kd-t09 | T01 |
| T10 | assigned (확인 필요) | kd/t10-comms-b @1c91c96 | kd-t10 | T03 |
| T10b | wip-ready | kd/t10b-difficulty @4d74ce9 | kd-t10b | T10 |
| T11 | running | kd-rerender @467dafc | kd-rerender | main |
| T12 | wip-ready (측정, 보류 0.3) | measure/t12 @e6122ac | kd-meas-t12 | ai3b2 스택 |
| T13 | queued | - | - | T12 |
| T14 | closed (merged) | - | - | main |
| T15 | wip-ready (측정, 보류 0.3) | measure/t15 @a70a308 | kd-meas-t15 | ai3b2 스택 |
| T16 | wip-ready (보류 0.3) | kd/t16-trench @2cb118c | kd-t16 | T03 |
| T16b | closed (분석 종결) | kd/t16b-rootcause @e724437 | kd-t16b | ? |
| T17 | wip-ready (확인 필요) | t17-cmd-convenience @0eb92e8 | kd-t17 | T08 |
| T18 | closed (main 반영) | - | - | main |
| T19 | closed (분할 T19a~d) | spec/t19-split @aea476b | kd-t19spec | T09 |
| T19a | wip-ready | t19a-save-slots @43e69b1 | kd-t19a | T09 |
| T19b | wip-ready (추정) | t19b-ammo-shells @622db02 | kd-t19b | T19a |
| T19b2 | wip-ready | t19b2-synced @4980e7b | kd-t19b2 | T19b |
| T19c | wip-ready | t19c-home-tabs @9260e36 | kd-t19c | T19b2 |
| T19d | wip-ready | t19d-doctrines @04a9149 | kd-t19d | T19c |
| T20 | blocked (U02) | - | - | - |
| T21 | wip-ready | sfxnew @aaba414 | kd-sfxnew | main |
| T22 | wip-ready | gt-face-proto @bc52007 | gt-develop/gt-face-proto | main |
| T23 | wip-ready (카드 설계 문서) | spec/t23-campaign-home @42fa456 | kd-t23 | main |
| T24 | wip-ready | kd/t24-s1s3 @07ac80c | 없음 (kd-t24 는 T37 체크아웃) | ai3b2 |
| T24x (crossing) | wip-ready | kd/t24-crossing-fix @390763a | kd-t24-crossing-fix | T24 |
| T25 | assigned (확인 필요) | kd/t25-bayonet-all @29695c3 | kd-t25 | ai3b2 |
| T26 | closed (done) | - | - | main |
| T27 | wip-ready | t27-cmd-ui2 @4cf5c45 | kd-t27 | main |
| T28 | wip-ready | t28-cover-forest @c577c0f | kd-t28 | T07 rail |
| T29 | closed (T29b 대체) | t29-campaign-economy-json @783e7ce | kd-t29 | T01 |
| T29b | wip-ready | t29b-on-t09 @a89b4d2 | kd-t29b | T09 |
| T30a | wip-ready | t30a-train-move @863f9d1 | kd-t30a | rail |
| T30b | wip-ready | t30b-train-weapons @ca1cdb8 | 없음 (kd-t30b 는 T36 체크아웃) | T30a |
| T30c | wip-ready | t30c-freight-objective @3de60d5 | kd-t30c | T30b |
| T30d | wip-ready | t30d-train-art @8e51e5a | kd-t30d-train-art | T30a (추정) |
| T30e | wip-ready | t30e-demo-mission @7f46a4c | kd-t30e | T30c |
| T31a | wip-ready (보류 0.3) | kd/t31a-regroup @97ed026 | kd-t31a | ai3b2 |
| T31b | closed | measure/t31b @05c228d / kd/endline-50 @476ceec | kd-meas-t31b · kd-endline50 | main (?) |
| T31c | wip-ready (켜기 0.4) | kd/t31c-surrender @e9cd527 | kd-t31c-surrender | ai3b2 |
| T32a | wip-ready | kd/t32a-pioneer @26cc9fd | kd-t32a | ai3b2 |
| T32b | wip-ready (0.4, 기본 꺼짐) | kd/t32b-ai-smoke @33284fa | kd-t32b | ai3b2 |
| T32c | queued | - | - | T32b |
| T32d | running (총구 수정) | kd/t32d-pioneer-art @2118f9b | kd-t32d | T32a |
| T33a | wip-ready | kd/t33a-map-model @c5b30f4 | kd-t33a-map-model | main |
| T33b | wip-ready | kd/t33b-map-ui @5207b8f | kd-t33b-map-ui | T33a |
| T33c | running | kd/t33c-placement @c514821 | kd-t33c-placement | T33b |
| T33d | running (확인 필요, 커밋 공유) | kd/t33d-rail @c514821 | kd-t33d-rail | T33c |
| T34a | wip-ready | kd/t34a-suppress @3d0f4de | kd-t34a | ai3b2 |
| T34b | queued | - | - | T34a |
| T34c | queued | - | - | ai3b2 |
| T34d | queued (보류 0.3) | - | - | T34a |
| T34e | queued | - | - | T34a |
| T35 | wip-ready | kd/isai-restore @5c08a38 | kd-isai-restore | main |
| T36 | wip-ready | kd/immune-target-fix @48bd6ce | 없음 (kd-t30b 체크아웃) | ? |
| T37 | wip-ready | kd/acting-delay-tier @7cfa0c2 | 없음 (kd-t24 체크아웃) | ? |
| T38a | wip-ready | t38a-settings-keys @7274c71 | kd-t38a | T38b |
| T38b | wip-ready | t38b-help-overlay @93ce79f | kd-t38b | T38g |
| T38c (=T46) | wip-ready | kd/t46-card-overflow @54e8a54 | kd-t46 | wip/breach2 |
| T38d | queued | spec/ui-backlog @28605bf | 없음 | main |
| T38e | queued (미커밋 10건) | t38e-unitlist-codex @7274c71 | kd-t38e | T38a |
| T38f | wip-ready | t38f-home-readonly-tabs @b622d1d | kd-t38f | T19d |
| T38g | wip-ready | t38g-ui-harness @f5a76d4 | kd-t38g | T27 |
| T38h | 확인 필요 (로그 없음, 미커밋 4건) | t38h-fuzz-campaign @e5f76ec | kd-t38h | T38g |
| T39a | wip-ready | river-r1 @5b7b9f1 | kd-river-r1 | rail |
| T39b | wip-ready | river-r2 @8ada7a9 | kd-river-r2 | T39a |
| T39c | wip-ready | river-r3 @26df7f6 | kd-river-r3 | T39b |
| T39d | wip-ready | river-r4-bridge @9d4b7be | kd-river-r4 | T39c |
| T39e/f/g/h/j | queued (blocked T11) | - | - | T11 |
| T39i | wip-ready | t39i-ruined-houses @66d6b95 | kd-t39i | main |
| T40 | wip-ready | kd/trains-in-annihilation @2de288d | kd-trains-annih | T30e |
| T41 | wip-ready | kd/crash-log @69320b1 | kd-crash-log | main |
| T42a | wip-ready | t42a-fatigue-morale @ba621fe | kd-t42a | T38f |
| T42b | wip-ready | t42b-promotion @51b92c3 | kd-t42b | T42a |
| T42c | wip-ready | t42c-prisoners @66e7637 | kd-t42c | T42b |
| T42d | wip-ready | t42d-schedule-log @06ee9bd | kd-t42d | T42c |
| T43 | wip-ready | kd/t43-branch-symbols @d45b18f | kd-t43 | main |
| T44 | running | kd/t44-smoke @1c4036d | kd-t44 (미커밋 3건) | main |
| T45 | wip-ready | kd/t45-menu-hide @88735b1 | kd-t45 | main |

## 밸런스 보류 (사용자 2026-10-22: 밸런스 관련 작업은 전부 보류, 최후순위 — 사용자 결정이 있을 때만 진행)
보류 대상: T12 · T15 · T16/T16b 후속 튜닝 · T31a(재투입 제한 튜닝) · T31b/T31b-endline 후속 기준선 재측정 · T31c 항복 값 · T24 ②③ 후속 튜닝 · T34d 기병 측면 강도 튜닝(S4 재확인 포함) · T06(종결) · AH1 열차 밸런스 · AG1 항복 켜기 · U2 목표 미달 보완 · H1 야전포 사거리 · 모든 n>=24 승률 측정 카드. 허용: 기능 구현, 결정성·저장·입력 검증, n=8 동작 확인.
우선순위: ① 재렌더(T11) ② 기능 구현(스택: 전투공병·AI 연막/철조망·지도 편집기·UI 백로그·열차·IsAi·캠페인 후속) ③ 통합 리허설/병합 준비 ④ (보류) 밸런스.

버전 마일스톤(사용자 2026-10-22): 0.2 = 밸런스 패치 이전(기능 완료·병합) → 0.3 = 밸런스 패치 이후(한 번에) → 0.4 = AI 개선. 상세 = ops/VERSIONS.md. 남은 AI 카드(T32c, T34b~e)는 0.4 로 미룸.
| T49a | 저장 호환 정책 적용 1 (메시지 · 이행 .bak · 목록 회색 표시, ops/SAVE_COMPAT_POLICY.md) | feature | D中 R2 S H1 Wlight | 없음 | wip-ready (브랜치 t49a-save-messages, 스택 base t48a e31406d) |
| T49b | 저장 호환 정책 적용 2 (설정 관대 읽기 · 사용자 지도/지휘관 건너뛰기 · 테스트) | feature | D中 R2 S H1 Wlight | T48a | queued |
| T49c | 저장 호환 출시 게이트 (캠페인 v1..현재 옛 샘플 테스트) | feature | D下 R2 S H1 Wlight | T49a | queued |
| T55m | 민가 import · 테마 키 · 42종 레지스트리 (housedata) | art/docs → merger | D下 H0 | T55s 필드 정의 | wip-ready (t55m-house-data · kd-develop/kd-t55m-house-data · 무병합) |
