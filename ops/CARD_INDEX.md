# CARD_INDEX - 작업 카드 색인 (기준 2026-10-22, main a64169b)

> 목적: 카드 Tnn 이 어느 브랜치·워크트리·파일에 있는지 한 장에 찾는다. main 의 korean-division/tasks 에는 T01~T13 만 있으므로 나머지는 **카드 브랜치**를 적었다 (`git show <branch>:korean-division/tasks/<file>`).
> 출처: ops/PIPELINE_LOG.md · ops/QUEUE.md · ops/STATE.md · ops/FV_RESERVATIONS.md · ops/VERSIONS.md · ops/DECISIONS.md · `git worktree list` · `git branch -v` · `git ls-tree -r --name-only <branch> korean-division/tasks/`. 브랜치는 체크아웃하지 않았다.
> 상태 값: wip-ready / queued / closed / in progress / hold-balance 0.3 / hold-AI 0.4. 버전 = ops/VERSIONS.md (0.2 밸런스 이전 · 0.3 밸런스 이후 · 0.4 AI 개선). 확인 못 한 칸 = `?`.
> 워크트리 경로 = `C:/gameProject/kd-develop/<이름>` (gt 는 `C:/gameProject/gt-develop/<이름>`). `-` = 워크트리 없음.
> 갱신 규칙: 상태가 바뀌면 이 표의 해당 행만 고친다. 브랜치 tip 은 `git branch -v` 로 다시 확인.

| id | 제목 | ver | state | branch @최신 커밋 | 워크트리 | 카드 파일 (branch : path) |
|---|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP+UI, 저장 트랜잭션) | 0.2 | wip-ready | gemini-import @63080df | kd-develop/kd-gemini-import | main : korean-division/tasks/T01-gemini-import.md |
| T02 | 건물 크기 확대 (wip/bigbldg) | ? | closed (merged 8d22ad5) | wip/bigbldg 브랜치 없음 | - | main : korean-division/tasks/T02-bigbldg.md |
| T03 | 참호 돌파 (breach2) | 0.2 | wip-ready | wip/breach2 @6ad6c50 (해결본 wip-resolved/breach @7faccf0) | kd-develop/kd-breach2 | main : korean-division/tasks/T03-breach.md |
| T04 | 명령 대기열·사격 통제 (orderq2) | 0.2 | wip-ready | wip/orderq2 @0377f8a | kd-develop/kd-orderq2 | main : korean-division/tasks/T04-orderq.md |
| T05 | 전술 AI 3B (ai3b2) | 0.2 | wip-ready | ai3b2 @f74a675 | kd-develop/kd-ai3b2 | main : korean-division/tasks/T05-ai3b2.md |
| T06 | 사상자 속도 (D19 C 폐기, casualty2 inert) | 0.3 | closed | wip/casualty2 @0722493 | kd-develop/kd-casualty2 | main : korean-division/tasks/T06-casualty.md |
| T06b | 사상자 재측정 카드 (D19 C) | 0.3 | ? (T06 종결에 따름 추정) | measure/t06b @e241b94 | kd-develop/kd-meas-t06b | measure/t06b : korean-division/tasks/T06b-remeasure.md |
| T07 | 철도 지형 (rail, 장갑열차 제외) | 0.2 | wip-ready | rail @9f513e7 | kd-develop/kd-rail | main : korean-division/tasks/T07-rail.md |
| T08 | 조작 UI 묶음 + 종이 질감 | 0.2 | wip-ready | t08-ui-bundle @2e9aef5 | kd-develop/kd-t08 | main : korean-division/tasks/T08-ui-bundle.md |
| T09 | 캠페인 수직 슬라이스 (T29b 로 대체됨) | 0.2 | wip-ready | t09-camp-slice @0c82a47 | kd-develop/kd-t09 | main : korean-division/tasks/T09-camp-slice.md |
| T10 | 통신 알파 ②~④ (대포병·야간·행군 피로) | 0.2 | wip-ready | kd/t10-comms-b @1c91c96 | kd-develop/kd-t10 | main : korean-division/tasks/T10-comms-b.md |
| T10b | 난이도 소비처 (전령 사로잡힘·유선 끊김) | 0.2 | wip-ready | kd/t10b-difficulty @4d74ce9 | kd-develop/kd-t10b | kd/t10b-difficulty : korean-division/tasks/T10b-difficulty.md |
| T11 | 전 유닛 재렌더 (부분 4/70, 드라이버 가동) | 0.2 | in progress | kd-rerender @467dafc | kd-develop/kd-rerender | main : korean-division/tasks/T11-rerender.md |
| T12 | 통합 밸런스 측정 (A/B/C 시드 8) | 0.3 | hold-balance 0.3 (측정 wip-ready) | measure/t12 @e6122ac | kd-develop/kd-meas-t12 | main : korean-division/tasks/T12-integ-measure.md |
| T13 | release (전체 selftest·골든·publish·기록) | 0.2 | queued | - | - | main : korean-division/tasks/T13-release.md |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | ? | closed (merged b5918a8 · a4c79c7 · docs-fable) | ? | - | ? |
| T15 | 밤 soak 재보고 (측정 a70a308 완료, QUEUE 'queued' 낡음) | 0.3 | hold-balance 0.3 | measure/t15 @a70a308 | kd-develop/kd-meas-t15 | measure/t15 : korean-division/tasks/T15-night-soak.md |
| T16 | trench_assault 원인 조사 (기준 미달, Z1) | 0.3 | hold-balance 0.3 (wip-ready) | kd/t16-trench @2cb118c | kd-develop/kd-t16 | kd/t16-trench : korean-division/tasks/T16-trench-assault.md |
| T16b | trench 원인 재조사 (분석 전용, 종결) | 0.3 | closed (분석 종결, 밸런스 보류) | kd/t16b-rootcause @e724437 | kd-develop/kd-t16b | kd/t16b-rootcause : korean-division/tasks/T16b-rootcause.md |
| T17 | 지휘 편의 (미션 중 저장·전과 보고·교전 알림) | 0.2 | wip-ready | t17-cmd-convenience @0eb92e8 | kd-develop/kd-t17 | t17-cmd-convenience : korean-division/tasks/T17-cmd-convenience.md |
| T18 | 한국군 타찬카 | ? | closed (main 에 이미 반영) | - | - | ? |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 (umbrella, T19a-d 로 분할) | 0.2 | queued (분할됨) | spec/t19-split @aea476b | kd-develop/kd-t19spec | ? (분할 카드 = T19a~d 행 참조) |
| T19a | 저장 슬롯 | 0.2 | wip-ready | t19a-save-slots @43e69b1 | kd-develop/kd-t19a | spec/t19-split : korean-division/tasks/T19a-save-slots.md |
| T19b | 구경별 탄약·포탄 규칙 (STATE 목록 미기재) | 0.2 | ? (wip-ready 추정) | t19b-ammo-shells @622db02 | kd-develop/kd-t19b | spec/t19-split : korean-division/tasks/T19b-ammo-shells.md |
| T19b2 | 기록 동기화 검증 | 0.2 | wip-ready | t19b2-synced @4980e7b | kd-develop/kd-t19b2 | ? |
| T19c | 캠페인 홈 탭 | 0.2 | wip-ready | t19c-home-tabs @9260e36 | kd-develop/kd-t19c | spec/t19-split : korean-division/tasks/T19c-home-tabs.md |
| T19d | 학파(교리) 데이터·탭 | 0.2 | wip-ready | t19d-doctrines @04a9149 | kd-develop/kd-t19d | spec/t19-split : korean-division/tasks/T19d-doctrines.md |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = U02) | ? | queued (blocked U02) | - | - | ? |
| T21 | 효과음 복원 (sfxnew) | 0.2 | wip-ready | sfxnew @aaba414 | kd-develop/kd-sfxnew | sfxnew : korean-division/tasks/T21-sfxnew.md |
| T22 | 얼굴 프로토타입 (gt-face-proto) | ? | wip-ready | gt-face-proto @bc52007 | gt-develop/gt-face-proto | gt-face-proto : korean-division/tasks/T22-face-proto.md |
| T23 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md, 코드 없음) | ? | wip-ready | spec/t23-campaign-home @42fa456 | kd-develop/kd-t23 | ? |
| T24 | D08 수정안 S1~S3 (대리 지휘·관측반·돌격 제압) | 0.2 | wip-ready | kd/t24-s1s3 @07ac80c | kd-develop/kd-t24 (현재 체크아웃 = kd/acting-delay-tier, ?) | kd/t24-s1s3 : korean-division/tasks/T24-s1s3.md |
| T25 | 착검 멈춤 범위 = 모든 돌격 (D02 A) | 0.2 | wip-ready | kd/t25-bayonet-all @29695c3 | kd-develop/kd-t25 | kd/t25-bayonet-all : korean-division/tasks/T25-bayonet-all.md |
| T26 | 레거시 이메일 차단 git 훅 (D20 A) | ? | closed (done) | - | - | ? |
| T27 | 지휘 UI 2 (색맹 기호 마커) | 0.2 | wip-ready | t27-cmd-ui2 @4cf5c45 | kd-develop/kd-t27 | t27-cmd-ui2 : korean-division/tasks/T27-cmd-ui2.md |
| T28 | 덮개 숲 (옵션, 기본 꺼짐) | 0.2 | wip-ready | t28-cover-forest @c577c0f | kd-develop/kd-t28 | t28-cover-forest : korean-division/tasks/T28-cover-forest.md |
| T29 | 캠페인 경제 JSON (T29b 로 대체) | 0.2 | wip-ready (대체됨) | t29-campaign-economy-json @783e7ce | kd-develop/kd-t29 | t29-campaign-economy-json : korean-division/tasks/T29-campaign-economy-json.md |
| T29b | T09 위 재스택 (T09·T29 대체) | 0.2 | wip-ready | t29b-on-t09 @a89b4d2 | kd-develop/kd-t29b | t29b-on-t09 : korean-division/tasks/T29b-restack.md |
| T30a | 열차 객체·궤도 이동 (FV58) | 0.2 | wip-ready | t30a-train-move @863f9d1 | kd-develop/kd-t30a | t30a-train-move : korean-division/tasks/T30a-train-move.md |
| T30b | 열차 무기 (FV59) | 0.2 | wip-ready | t30b-train-weapons @ca1cdb8 | ? (kd-develop/kd-t30b 는 kd/immune-target-fix 체크아웃) | t30b-train-weapons : korean-division/tasks/T30b-train-weapons.md |
| T30c | 화물열차 목표 | 0.2 | wip-ready | t30c-freight-objective @3de60d5 | kd-develop/kd-t30c | t30c-freight-objective : korean-division/tasks/T30c-freight-objective.md |
| T30d | 열차 그림 (블록아웃 5종) | 0.2 | wip-ready | t30d-train-art @8e51e5a | kd-develop/kd-t30d-train-art | t30d-train-art : korean-division/tasks/T30d-train-art-plan.md |
| T30e | 호송 시연 미션 (FV59 유지) | 0.2 | wip-ready | t30e-demo-mission @7f46a4c | kd-develop/kd-t30e | t30e-demo-mission : korean-division/tasks/T30e-train-demo-mission.md |
| T31a | 재투입 제한 (regroup.noRejoinBelow 기본 꺼짐) | 0.3 | hold-balance 0.3 (wip-ready, 켜지 말 것) | kd/t31a-regroup @97ed026 | kd-develop/kd-t31a | kd/t31a-regroup : korean-division/tasks/T31a-regroup-limit.md |
| T31b | 임무 종료선 측정 (종료선 70→50 완결) | 0.3 | closed | measure/t31b @05c228d (카드) / kd/endline-50 @10361a4 (코드) | kd-develop/kd-meas-t31b · kd-develop/kd-endline50 | measure/t31b : korean-division/tasks/T31b-mission-end-review.md · kd/endline-50 : korean-division/tasks/T31b-endline50.md |
| T31c | AI 항복 (키 전부 기본 끔) | 0.4 | wip-ready (켜기 = 0.4) | kd/t31c-surrender @e9cd527 | kd-develop/kd-t31c-surrender | kd/t31c-surrender : korean-division/tasks/T31c-ai-surrender.md |
| T32a | 보병 공병 유닛 (pioneer) | 0.2 | in progress | kd/t32a-pioneer @f74a675 (base 만) | kd-develop/kd-t32a | spec/cards-t32 : korean-division/tasks/T32a-pioneer-unit.md |
| T32b | AI 연막 (키 기본 끔) | 0.4 | in progress | kd/t32b-ai-smoke @f74a675 (base 만) | kd-develop/kd-t32b | spec/cards-t32 : korean-division/tasks/T32b-ai-smoke.md |
| T32c | AI 철조망 절단 | 0.4 | queued | - | - | spec/cards-t32 : korean-division/tasks/T32c-ai-wirecut.md |
| T32d | 공병 그림 | 0.2 | queued | - | - | spec/cards-t32 : korean-division/tasks/T32d-pioneer-art.md |
| T33a | 지도 편집기 문서 모델 (헤드리스) | 0.2 | wip-ready | kd/t33a-map-model @ef0e39b | kd-develop/kd-t33a-map-model | kd/t33a-map-model : korean-division/tasks/T33a-mapedit-model.md |
| T33b | 지도 편집기 UI | 0.2 | in progress (발주됨, 커밋 없음) | kd/t33b-map-ui @ef0e39b (base 만) | kd-develop/kd-t33b-map-ui | kd/t33-map-editor-design : korean-division/tasks/T33b-mapedit-ui.md |
| T33c | 지도 편집기 배치 | 0.2 | queued | kd/t33-map-editor-design @3b7c994 (카드 브랜치) | - | kd/t33-map-editor-design : korean-division/tasks/T33c-mapedit-place.md |
| T33d | 지도 편집기 철도 도구 | 0.2 | queued | kd/t33-map-editor-design @3b7c994 (카드 브랜치) | - | kd/t33-map-editor-design : korean-division/tasks/T33d-mapedit-rail.md |
| T34a | 제압 문턱·엄호 철수 (AI 3C) | 0.4 | in progress | kd/t34a-suppress @f74a675 (base 만) | kd-develop/kd-t34a | spec/cards-t34 : korean-division/tasks/T34a-suppthr-coverwd.md |
| T34b | 틀 선택 (AI 3C) | 0.4 | queued | - | - | spec/cards-t34 : korean-division/tasks/T34b-template-pick.md |
| T34c | 제병 협동 (FV64 예약 후보) | 0.4 | queued | - | - | spec/cards-t34 : korean-division/tasks/T34c-combined-arms.md |
| T34d | 기병 측면 강도 (S4 재확인·튜닝) | 0.4 | hold-balance 0.3 | - | - | spec/cards-t34 : korean-division/tasks/T34d-cav-flank-strength.md |
| T34e | 유휴 진단 | 0.4 | queued | - | - | spec/cards-t34 : korean-division/tasks/T34e-idle-difficulty.md |
| T35 | IsAi 복원 (리플레이 머리말·스냅샷, FV60/Replay24 예약 AF1) | 0.2 | in progress (발주됨) | kd/isai-restore @592392d (docs 커밋만) | kd-develop/kd-isai-restore | ? |
| T36 | 면역 표적 수정 (AH2) | 0.2 | wip-ready | kd/immune-target-fix @c89175d | kd-develop/kd-t30b (체크아웃 주의) | kd/immune-target-fix : korean-division/tasks/T36-immune-target.md |
| T37 | 대리 부사관 지연 등급 (U4) | 0.2 | wip-ready | kd/acting-delay-tier @cd140c9 | kd-develop/kd-t24 (체크아웃 주의) | kd/acting-delay-tier : korean-division/tasks/T37-acting-delay.md |
| T38a | 설정 단축키 재지정 | 0.2 | queued | spec/ui-backlog @b1546db (카드 브랜치) | - | spec/ui-backlog : korean-division/tasks/T38a-settings-keys.md |
| T38b | 도움말 오버레이 | 0.2 | queued | spec/ui-backlog @b1546db (카드 브랜치) | - | spec/ui-backlog : korean-division/tasks/T38b-help-overlay.md |
| T38c | 명령 카드 넘침 | 0.2 | queued | spec/ui-backlog @b1546db (카드 브랜치) | - | spec/ui-backlog : korean-division/tasks/T38c-cmdcard-overflow.md |
| T38d | 종이 UI 2단계 | 0.2 | queued | spec/ui-backlog @b1546db (카드 브랜치) | - | spec/ui-backlog : korean-division/tasks/T38d-paper-ui-phase2.md |
| T38e | 부대 목록·도감·툴팁 | 0.2 | queued | spec/ui-backlog @b1546db (카드 브랜치) | - | spec/ui-backlog : korean-division/tasks/T38e-unitlist-codex-tips.md |
| T38f | 홈 후속 탭 읽기 전용 (t19d 위) | 0.2 | in progress (발주됨, 커밋 없음) | t38f-home-readonly-tabs @04a9149 (base 만) | kd-develop/kd-t38f | spec/ui-backlog : korean-division/tasks/T38f-home-tabs-readonly.md |
| T38g | UI 하네스 (t27 위) | 0.2 | in progress (발주됨, 커밋 없음) | t38g-ui-harness @4cf5c45 (base 만) | kd-develop/kd-t38g | spec/ui-backlog : korean-division/tasks/T38g-ui-harness.md |
| T39a | 하천 Ford 지형 (river-r1, FV54) | 0.2 | wip-ready | river-r1 @5b7b9f1 (river-r2 동일) | kd-develop/kd-river-r1 | spec/cards-t39 : korean-division/tasks/T39a-river-ford.md |
| T39b | 하천 경로 | 0.2 | in progress (발주됨 a64169b) | ? (커밋 없음) | ? | spec/cards-t39 : korean-division/tasks/T39b-river-pathing.md |
| T39c | 하천 렌더 (T39b 와 병행 가능) | 0.2 | queued | - | - | spec/cards-t39 : korean-division/tasks/T39c-river-render.md |
| T39d | 하천 다리 (선택, 기본 미발주) | 0.2 | queued | - | - | spec/cards-t39 : korean-division/tasks/T39d-river-bridge.md |
| T39e | 행군 사격 머즐 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | spec/cards-t39 : korean-division/tasks/T39e-marchfire-muzzle.md |
| T39f | 한·프 색 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | spec/cards-t39 : korean-division/tasks/T39f-kr-ft-colour.md |
| T39g | 간도 복식 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | spec/cards-t39 : korean-division/tasks/T39g-gando-dress.md |
| T39h | 보병 모션 2차 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | spec/cards-t39 : korean-division/tasks/T39h-infantry-motion-r2.md |
| T39i | 가옥 폐허 (코드 부분 완료, 렌더 대기 heavy_gate) | 0.2 | in progress | t39i-ruined-houses @6e561df | kd-develop/kd-t39i | spec/cards-t39 : korean-division/tasks/T39i-ruined-house.md |
| T39j | 엎드려쏴 재확인 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | spec/cards-t39 : korean-division/tasks/T39j-prone-recheck.md |
| T40 | 열차 전멸 판정 포함 (t30e 위, 발주됨) | ? | in progress (발주됨) | ? | - | ? |
| T41 | 충돌 로그 | ? | in progress (발주됨) | ? | - | ? |

## 스택 체인 (기반 → 위)

- **gemini-import → t09-camp-slice → t19a → t19b → t19b2 → t19c → t19d** (캠페인 축). t19d 위에 T38f(t38f-home-readonly-tabs, 같은 tip 04a9149) 가 있다.
- **gemini-import → t29 (economy JSON) → t29b-on-t09** : T29b 가 t09 위에서 T09·T29 를 대체 (병합 시 단독).
- **breach2 → orderq2 → ai3b2 → {t24 (kd/t24-s1s3), t25, t31a, t31c, t32a, t32b, t34a}**. 참고: breach2 → t10 (kd/t10-comms-b), breach2 → t16 (kd/t16-trench).
- **rail → t28** (T28 cover-forest). **rail → river-r1 (T39a)**, T39b·c 는 T39a 위 병행.
- **rail → t30a → t30b → t30c → t30e** (열차 체인). T30d(art) 는 8e51e5a 로 별도 표시.
- **t30e 위 → T40** (열차 전멸 판정, 발주됨).
- **t27-cmd-ui2 → t38g-ui-harness** (같은 tip 4cf5c45).
- **t08-ui-bundle → T17** (QUEUE 선행. 실제 커밋 기반은 ?).
- **kd/t25-bayonet-all ↔ kd/t24-s1s3 ↔ kd/acting-delay-tier (T37)**: T37 기반은 ?.
- **kd-rerender (T11) → T39e~j** (T11 끝나기 전 make_unit/rerender96 편집 금지).
- **ai3b2 → T34c** (FV64 예약 후보, 발주 전).
- **measure/t12, measure/t15, measure/t31b, kd/t16b-rootcause, measure/t06b** : 측정·분석 전용 브랜치 (코드 변경 없음).

## 메모

- 워크트리 체크아웃 불일치 3건: `kd-develop/kd-t30b` 는 kd/immune-target-fix(T36), `kd-develop/kd-t24` 는 kd/acting-delay-tier(T37). T30b·T24 전용 워크트리는 없음.
- ops/STATE.md 의 커밋 해시(예: T10b 4ac07b4, T16 8b38b29, T24 e4fd65b, T36 등) 는 이후 재작성돼 현재 tip 과 다르다. 이 표는 `git branch -v` 기준 tip 을 적었다.
- 카드 파일이 없는 항목: T02 (main 에는 있음), T14·T18·T20·T26 (카드 파일 없음), T19b2·T23 (카드 파일 ?), T35·T40·T41 (카드 파일 ?).
- 밸런스 보류 (QUEUE): T12 · T15 · T16/T16b · T31a · T31b 후속 · T31c 항복 값 · T24 ②③ · T34d · T06 — hold-balance 0.3 / closed 로 표시.
- 0.4 (AI 개선): T31c 켜기, T32b, T32c, T34a~e, T10b 이후 AI 항목.
