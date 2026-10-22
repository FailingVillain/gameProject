# CARD_INDEX - 작업 카드 색인 (기준 2026-10-22, main f3fe6db · 실측 스냅샷 `git branch -v` / `git worktree list`)

> 목적: 카드 Tnn 이 어느 브랜치·워크트리·파일에 있는지 한 장에 찾는다. main 의 korean-division/tasks 에는 T01~T13 만 있으므로 나머지는 **카드 브랜치**를 적었다 (`git show <branch>:korean-division/tasks/<file>`).
> 출처: ops/PIPELINE_LOG.md (tail 60, 13:3x~15:0x) · ops/QUEUE.md · `git branch -v` · `git worktree list`. 브랜치는 체크아웃하지 않았다.
> 상태 값: wip-ready / running / queued / closed / blocked(사유) / hold-balance 0.3 / hold-AI 0.4. 버전 = ops/VERSIONS.md (0.2 밸런스 이전 · 0.3 밸런스 이후 · 0.4 AI 개선). 확인 못 한 칸 = `?`. "확인 필요" = 커밋은 있으나 카드 상태를 기록한 근거가 로그·브랜치 제목에 없음.
> 워크트리 경로 = `C:/gameProject/kd-develop/<이름>` (gt 는 `C:/gameProject/gt-develop/<이름>`). `-` = 워크트리 없음.
> base(스택) = 이 카드가 올라타 있는 바로 아래 카드 (로그·QUEUE 스택 체인 기준). main = 병합된 기반.
> 갱신 규칙: 상태가 바뀌면 이 표의 해당 행만 고친다. 브랜치 tip 은 `git branch -v` 로 다시 확인.
> 정정 (2026-10-22 실측): 이전 표의 stale 값 — T02 wip/bigbldg 브랜치 없음·closed / T04 orderq2 tip 0377f8a / T05 f74a675 / T09 0c82a47 / T10b 4ac07b4→4d74ce9 / T12 measure/t12 e6122ac / T14 closed / T15 queued→측정 완료 / T16 8b38b29→2cb118c / T29 대체됨 / T30b·T36·T37·T24 워크트리 불일치 정정 / T31b endline 10361a4→476ceec / T32a·T32b·T34a in progress→wip-ready / T33a ef0e39b→c5b30f4 / T35 592392d→5c08a38 / T38a·b·c·f·g wip-ready 정정 / T39a·b·c·d·i / T40·T41 wip-ready 정정 / T42a~d·T43·T45 신규 행.

| id | 제목 | ver | state | branch @최신 커밋 | 워크트리 | base(스택) | 카드 파일 (branch : path) |
|---|---|---|---|---|---|---|---|
| T01 | wip/gemini-import 복원·병합 (캠페인 WIP+UI, 저장 트랜잭션) | 0.2 | wip-ready (리뷰 수정 커밋 63080df) | gemini-import @63080df | kd-develop/kd-gemini-import | main | main : korean-division/tasks/T01-gemini-import.md |
| T02 | 건물 크기 확대 (wip/bigbldg) | ? | closed (merged 8d22ad5) | wip/bigbldg 브랜치 없음 | - | main | main : korean-division/tasks/T02-bigbldg.md |
| T03 | 참호 돌파 (breach2) | 0.2 | wip-ready | wip/breach2 @6ad6c50 (해결본 wip-resolved/breach @7faccf0) | kd-develop/kd-breach2 | main (8d22ad5 기준) | main : korean-division/tasks/T03-breach.md |
| T04 | 명령 대기열·사격 통제 (orderq2) | 0.2 | assigned (커밋 0377f8a 리뷰 수정; wip-ready 선언 확인 필요) | wip/orderq2 @0377f8a | kd-develop/kd-orderq2 | T03 | main : korean-division/tasks/T04-orderq.md |
| T05 | 전술 AI 3B (ai3b2) | 0.2 | wip-ready | ai3b2 @f74a675 | kd-develop/kd-ai3b2 | T04 | main : korean-division/tasks/T05-ai3b2.md |
| T06 | 사상자 속도 (D19 C 폐기, casualty2 inert 보존) | 0.3 | closed | wip/casualty2 @0722493 | kd-develop/kd-casualty2 | ? | main : korean-division/tasks/T06-casualty.md |
| T06b | 사상자 재측정 카드 (D19 C, 24 시드, 카드만) | 0.3 | closed (T06 종결에 따름) | measure/t06b @e241b94 | kd-develop/kd-meas-t06b (+ d19c·tip 임시 2개, 97ed026 detached) | ? | measure/t06b : korean-division/tasks/T06b-remeasure.md |
| T07 | 철도 지형 (rail, 장갑열차 제외) | 0.2 | wip-ready | rail @9f513e7 | kd-develop/kd-rail | main | main : korean-division/tasks/T07-rail.md |
| T08 | 조작 UI 묶음 + 종이 질감 | 0.2 | wip-ready | t08-ui-bundle @2e9aef5 | kd-develop/kd-t08 | T01 | main : korean-division/tasks/T08-ui-bundle.md |
| T09 | 캠페인 수직 슬라이스 (T29b 가 대체, 병합은 T29b 단독) | 0.2 | wip-ready | t09-camp-slice @0c82a47 | kd-develop/kd-t09 | T01 | main : korean-division/tasks/T09-camp-slice.md |
| T10 | 통신 알파 ②~④ (대포병·야간·행군 피로) | 0.2 | assigned (커밋 1c91c96; wip-ready 선언 확인 필요) | kd/t10-comms-b @1c91c96 | kd-develop/kd-t10 | T03 | main : korean-division/tasks/T10-comms-b.md |
| T10b | 난이도 소비처 (전령 사로잡힘·유선 끊김) | 0.2 | wip-ready | kd/t10b-difficulty @4d74ce9 | kd-develop/kd-t10b | T10 | kd/t10b-difficulty : korean-division/tasks/T10b-difficulty.md |
| T11 | 전 유닛 재렌더 (부분 4/70, 드라이버 가동) | 0.2 | running | kd-rerender @467dafc | kd-develop/kd-rerender (dirty 2) | main | main : korean-division/tasks/T11-rerender.md |
| T12 | 통합 밸런스 측정 (A/B/C 시드 8) | 0.3 | wip-ready (측정 완료, 밸런스 보류 0.3) | measure/t12 @e6122ac | kd-develop/kd-meas-t12 (+ t12s 임시 f74a675, dirty 2) | ai3b2 스택 | measure/t12 : korean-division/tasks/T12-integ-measure.md |
| T13 | release (전체 selftest·골든·publish·기록) | 0.2 | queued (14:3x release 첫 실가동 기록 있음 — 지휘자 상태 확인) | - | - | T12 | main : korean-division/tasks/T13-release.md |
| T14 | 문서 재정비 (4층 구조 + ops + PIPELINE v1) | ? | closed (merged b5918a8 · a4c79c7 · docs-fable) | - | - | main | ? |
| T15 | 밤 soak 재보고 (측정 a70a308 완료) | 0.3 | wip-ready (측정 완료, 밸런스 보류 0.3) | measure/t15 @a70a308 | kd-develop/kd-meas-t15 (dirty 2) | ai3b2 스택 | measure/t15 : korean-division/tasks/T15-night-soak.md |
| T16 | trench_assault 원인 조사 (기준 미달, Z1) | 0.3 | wip-ready (밸런스 보류 0.3) | kd/t16-trench @2cb118c | kd-develop/kd-t16 | T03 | kd/t16-trench : korean-division/tasks/T16-trench-assault.md |
| T16b | trench 원인 재조사 (분석 전용, 종결) | 0.3 | closed (분석 종결) | kd/t16b-rootcause @e724437 | kd-develop/kd-t16b | ? | kd/t16b-rootcause : korean-division/tasks/T16b-rootcause.md |
| T17 | 지휘 편의 (미션 중 저장·전과 보고·교전 알림) | 0.2 | wip-ready (CARD_INDEX 기준; 확인 필요) | t17-cmd-convenience @0eb92e8 | kd-develop/kd-t17 | T08 | t17-cmd-convenience : korean-division/tasks/T17-cmd-convenience.md |
| T18 | 한국군 타찬카 | ? | closed (main 에 이미 반영, tachanka a1a35e6) | - | - | main | ? |
| T19 | 캠페인 홈 MVP / 규칙 1~15 / 학파 (umbrella → T19a~d 분할) | 0.2 | closed (분할됨: T19a~d 행 참조) | spec/t19-split @aea476b | kd-develop/kd-t19spec | T09 | ? (분할 카드 = T19a~d 행 참조) |
| T19a | 저장 슬롯 | 0.2 | wip-ready | t19a-save-slots @43e69b1 | kd-develop/kd-t19a | T09 | spec/t19-split : korean-division/tasks/T19a-save-slots.md |
| T19b | 구경별 탄약·포탄 규칙 | 0.2 | wip-ready (추정) | t19b-ammo-shells @622db02 | kd-develop/kd-t19b | T19a | spec/t19-split : korean-division/tasks/T19b-ammo-shells.md |
| T19b2 | 기록 동기화 검증 | 0.2 | wip-ready | t19b2-synced @4980e7b | kd-develop/kd-t19b2 | T19b | ? |
| T19c | 캠페인 홈 탭 | 0.2 | wip-ready | t19c-home-tabs @9260e36 | kd-develop/kd-t19c | T19b2 | spec/t19-split : korean-division/tasks/T19c-home-tabs.md |
| T19d | 학파(교리) 데이터·탭 | 0.2 | wip-ready | t19d-doctrines @04a9149 | kd-develop/kd-t19d | T19c | spec/t19-split : korean-division/tasks/T19d-doctrines.md |
| T20 | 저장소 이동 마무리 (옛 폴더 삭제 = U02) | ? | blocked (U02) | - | - | - | ? |
| T21 | 효과음 복원 (sfxnew) | 0.2 | wip-ready | sfxnew @aaba414 | kd-develop/kd-sfxnew | main | sfxnew : korean-division/tasks/T21-sfxnew.md |
| T22 | 얼굴 프로토타입 (gt-face-proto) | ? | wip-ready | gt-face-proto @bc52007 | gt-develop/gt-face-proto | main | gt-face-proto : korean-division/tasks/T22-face-proto.md |
| T23 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md, 코드 없음) | ? | wip-ready | spec/t23-campaign-home @42fa456 | kd-develop/kd-t23 | main | spec/t23-campaign-home : ? (카드 파일명 확인 필요) |
| T24 | D08 수정안 S1~S3 (대리 지휘·관측반·돌격 제압) | 0.2 | wip-ready | kd/t24-s1s3 @07ac80c | - (kd-develop/kd-t24 는 kd/acting-delay-tier 체크아웃, 불일치) | T04~T05 (ai3b2) | kd/t24-s1s3 : korean-division/tasks/T24-s1s3.md |
| T24x | 도하 관측반 수정 (crossing fix, T24 위) | 0.2 | wip-ready (로그 15:0x) | kd/t24-crossing-fix @390763a | kd-develop/kd-t24-crossing-fix | T24 (t24-s1s3) | 카드 없음 (병합 때 T24 뒤에 얹기) |
| T25 | 착검 멈춤 범위 = 모든 돌격 (D02 A) | 0.2 | assigned (커밋 29695c3; wip-ready 확인 필요) | kd/t25-bayonet-all @29695c3 | kd-develop/kd-t25 | ai3b2 스택 | kd/t25-bayonet-all : korean-division/tasks/T25-bayonet-all.md |
| T26 | 레거시 이메일 차단 git 훅 (D20 A) | ? | closed (done, .git/hooks 설치) | - | - | main | ? |
| T27 | 지휘 UI 2 (색맹 기호 마커) | 0.2 | wip-ready | t27-cmd-ui2 @4cf5c45 | kd-develop/kd-t27 | main | t27-cmd-ui2 : korean-division/tasks/T27-cmd-ui2.md |
| T28 | 덮개 숲 (옵션, 기본 꺼짐) | 0.2 | wip-ready | t28-cover-forest @c577c0f | kd-develop/kd-t28 | T07 (rail) | t28-cover-forest : korean-division/tasks/T28-cover-forest.md |
| T29 | 캠페인 경제 JSON (T29b 로 대체) | 0.2 | closed (T29b 에 대체) | t29-campaign-economy-json @783e7ce | kd-develop/kd-t29 | T01 | t29-campaign-economy-json : korean-division/tasks/T29-campaign-economy-json.md |
| T29b | T09 위 재스택 (T09·T29 대체) | 0.2 | wip-ready | t29b-on-t09 @a89b4d2 | kd-develop/kd-t29b | T09 | t29b-on-t09 : korean-division/tasks/T29b-restack.md |
| T30a | 열차 객체·궤도 이동 (FV58) | 0.2 | wip-ready | t30a-train-move @863f9d1 | kd-develop/kd-t30a | T07 (rail) | t30a-train-move : korean-division/tasks/T30a-train-move.md |
| T30b | 열차 무기 (FV59) | 0.2 | wip-ready | t30b-train-weapons @ca1cdb8 | - (kd-develop/kd-t30b 는 kd/immune-target-fix 체크아웃, 불일치) | T30a | t30b-train-weapons : korean-division/tasks/T30b-train-weapons.md |
| T30c | 화물열차 목표 | 0.2 | wip-ready | t30c-freight-objective @3de60d5 | kd-develop/kd-t30c | T30b | t30c-freight-objective : korean-division/tasks/T30c-freight-objective.md |
| T30d | 열차 그림 (블록아웃 5종) | 0.2 | wip-ready | t30d-train-art @8e51e5a | kd-develop/kd-t30d-train-art | T30a (추정) | t30d-train-art : korean-division/tasks/T30d-train-art-plan.md |
| T30e | 호송 시연 미션 (FV59 유지) | 0.2 | wip-ready | t30e-demo-mission @7f46a4c | kd-develop/kd-t30e | T30c | t30e-demo-mission : korean-division/tasks/T30e-train-demo-mission.md |
| T31a | 재투입 제한 (regroup.noRejoinBelow 기본 꺼짐) | 0.3 | wip-ready (밸런스 보류 0.3, 켜지 말 것) | kd/t31a-regroup @97ed026 | kd-develop/kd-t31a | ai3b2 | kd/t31a-regroup : korean-division/tasks/T31a-regroup-limit.md |
| T31b | 임무 종료선 측정 (종료선 70→50 완결) | 0.3 | closed | measure/t31b @05c228d (카드, dirty 2) / kd/endline-50 @476ceec (코드, dirty 1) | kd-develop/kd-meas-t31b · kd-develop/kd-endline50 | main (?) | measure/t31b : korean-division/tasks/T31b-mission-end-review.md · kd/endline-50 : korean-division/tasks/T31b-endline50.md |
| T31c | AI 항복 (키 전부 기본 끔) | 0.4 | wip-ready (켜기 = 0.4) | kd/t31c-surrender @e9cd527 | kd-develop/kd-t31c-surrender | ai3b2 | kd/t31c-surrender : korean-division/tasks/T31c-ai-surrender.md |
| T32a | 보병 공병 유닛 (infantry_pioneer) | 0.2 | wip-ready | kd/t32a-pioneer @26cc9fd | kd-develop/kd-t32a | ai3b2 | spec/cards-t32 : korean-division/tasks/T32a-pioneer-unit.md |
| T32b | AI 연막 (키 기본 끔) | 0.4 | wip-ready | kd/t32b-ai-smoke @33284fa | kd-develop/kd-t32b | ai3b2 | spec/cards-t32 : korean-division/tasks/T32b-ai-smoke.md |
| T32c | AI 철조망 절단 | 0.4 | queued | - | - | T32b | spec/cards-t32 : korean-division/tasks/T32c-ai-wirecut.md |
| T32d | 공병 그림 (엎드려쏴 총구 수정 발주 중) | 0.2 | running (15:0x 총구 수정 재렌더) | kd/t32d-pioneer-art @2118f9b | kd-develop/kd-t32d | T32a | spec/cards-t32 : korean-division/tasks/T32d-pioneer-art.md |
| T33a | 지도 편집기 문서 모델 (헤드리스) | 0.2 | wip-ready (리뷰 수정 c5b30f4) | kd/t33a-map-model @c5b30f4 | kd-develop/kd-t33a-map-model | main | kd/t33a-map-model : korean-division/tasks/T33a-mapedit-model.md |
| T33b | 지도 편집기 UI | 0.2 | wip-ready (로그 14:0x) | kd/t33b-map-ui @5207b8f (dirty 1) | kd-develop/kd-t33b-map-ui | T33a | kd/t33-map-editor-design : korean-division/tasks/T33b-mapedit-ui.md |
| T33c | 지도 편집기 배치 (소품·시설·시작점·구역) | 0.2 | running (커밋 c514821) | kd/t33c-placement @c514821 | kd-develop/kd-t33c-placement | T33b | kd/t33-map-editor-design : korean-division/tasks/T33c-mapedit-place.md |
| T33d | 지도 편집기 철도 도구 | 0.2 | running? (c514821 와 같은 tip, 전용 커밋 없음 — 확인 필요) | kd/t33d-rail @c514821 | kd-develop/kd-t33d-rail | T33c | kd/t33-map-editor-design : korean-division/tasks/T33d-mapedit-rail.md |
| T34a | 제압 문턱·엄호 철수 (AI 3C-1) | 0.4 | wip-ready (로그 14:3x) | kd/t34a-suppress @3d0f4de | kd-develop/kd-t34a | ai3b2 | spec/cards-t34 : korean-division/tasks/T34a-suppthr-coverwd.md |
| T34b | 틀 선택 (AI 3C) | 0.4 | queued | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34b-template-pick.md |
| T34c | 제병 협동 (FV64 예약 후보) | 0.4 | queued | - | - | ai3b2 | spec/cards-t34 : korean-division/tasks/T34c-combined-arms.md |
| T34d | 기병 측면 강도 (S4 재확인·튜닝) | 0.4 | queued (밸런스 보류 0.3) | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34d-cav-flank-strength.md |
| T34e | 유휴 진단 | 0.4 | queued | - | - | T34a | spec/cards-t34 : korean-division/tasks/T34e-idle-difficulty.md |
| T35 | IsAi 복원 (리플레이 머리말·체크섬, FV60/Replay24 예약 AF1) | 0.2 | wip-ready (로그 14:0x, 리뷰 차단 0) | kd/isai-restore @5c08a38 | kd-develop/kd-isai-restore | main | ? (카드 파일명 확인 필요) |
| T36 | 면역 표적 수정 (AH2: 공격 명령 기준 면역 면제) | 0.2 | wip-ready | kd/immune-target-fix @48bd6ce | - (kd-develop/kd-t30b 가 체크아웃, 불일치) | ? | kd/immune-target-fix : korean-division/tasks/T36-immune-target.md |
| T37 | 대리 부사관 지연 등급 (U4) | 0.2 | wip-ready (커밋 7cfa0c2) | kd/acting-delay-tier @7cfa0c2 | - (kd-develop/kd-t24 가 체크아웃, 불일치) | ? | kd/acting-delay-tier : korean-division/tasks/T37-acting-delay.md |
| T38a | 설정 단축키 재지정 | 0.2 | wip-ready (로그 14:3x) | t38a-settings-keys @7274c71 | kd-develop/kd-t38a | T38b | spec/ui-backlog : korean-division/tasks/T38a-settings-keys.md |
| T38b | 도움말 오버레이 | 0.2 | wip-ready (로그 14:4x) | t38b-help-overlay @93ce79f | kd-develop/kd-t38b | T38g | spec/ui-backlog : korean-division/tasks/T38b-help-overlay.md |
| T38c | 명령 카드 넘침 (= kd/t46-card-overflow, 카드 T38c 와 같은 일) | 0.2 | wip-ready (로그 14:3x) | kd/t46-card-overflow @54e8a54 | kd-develop/kd-t46 | wip/breach2 (T03) | spec/ui-backlog : korean-division/tasks/T38c-cmdcard-overflow.md |
| T38d | 종이 UI 2단계 (캠페인 홈 + 전투 HUD) | 0.2 | queued | spec/ui-backlog @28605bf (카드 브랜치) | - | main | spec/ui-backlog : korean-division/tasks/T38d-paper-ui-phase2.md |
| T38e | 부대 목록·도감·툴팁 | 0.2 | queued (커밋 없음, 워크트리 미커밋 10건) | t38e-unitlist-codex @7274c71 (= T38a tip) | kd-develop/kd-t38e (dirty 10) | T38a | spec/ui-backlog : korean-division/tasks/T38e-unitlist-codex-tips.md |
| T38f | 홈 후속 탭 읽기 전용 (t19d 위) | 0.2 | wip-ready (로그 14:3x) | t38f-home-readonly-tabs @b622d1d | kd-develop/kd-t38f | T19d | spec/ui-backlog : korean-division/tasks/T38f-home-tabs-readonly.md |
| T38g | UI 하네스 (t27 위) | 0.2 | wip-ready (로그 14:3x) | t38g-ui-harness @f5a76d4 | kd-develop/kd-t38g | T27 | spec/ui-backlog : korean-division/tasks/T38g-ui-harness.md |
| T38h | UI 퍼즈·캠페인 화면 커버 (uifuzz 캠페인 홈 도달, 로그 42 gap 후속 후보) | 0.2 | 확인 필요 (로그 기록 없음; 워크트리 미커밋 4건) | t38h-fuzz-campaign @e5f76ec | kd-develop/kd-t38h (dirty 4) | T38g (커밋 메시지 기준) | ? |
| T39a | 하천 Ford 지형 (river-r1, FV54) | 0.2 | wip-ready (로그 13:3x) | river-r1 @5b7b9f1 | kd-develop/kd-river-r1 | rail | spec/cards-t39 : korean-division/tasks/T39a-river-ford.md |
| T39b | 하천 경로 (waterBlocksFire 키) | 0.2 | wip-ready (로그 14:0x) | river-r2 @8ada7a9 | kd-develop/kd-river-r2 | T39a | spec/cards-t39 : korean-division/tasks/T39b-river-pathing.md |
| T39c | 하천 렌더 (합성 여울 타일·test_river) | 0.2 | wip-ready (로그 14:2x) | river-r3 @26df7f6 | kd-develop/kd-river-r3 | T39b | spec/cards-t39 : korean-division/tasks/T39c-river-render.md |
| T39d | 하천 다리 (Bridge 지형, 기본 꺼짐) | 0.2 | wip-ready (로그 15:0x; 시트 갱신 미완) | river-r4-bridge @9d4b7be | kd-develop/kd-river-r4 | T39c | spec/cards-t39 : korean-division/tasks/T39d-river-bridge.md |
| T39e | 행군 사격 머즐 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39e-marchfire-muzzle.md |
| T39f | 한·프 색 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39f-kr-ft-colour.md |
| T39g | 간도 복식 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39g-gando-dress.md |
| T39h | 보병 모션 2차 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39h-infantry-motion-r2.md |
| T39i | 가옥 폐허 (코드 완료, 렌더 대기 heavy_gate → 시트 카드 wip-ready) | 0.2 | wip-ready | t39i-ruined-houses @66d6b95 | kd-develop/kd-t39i | main | spec/cards-t39 : korean-division/tasks/T39i-ruined-house.md |
| T39j | 엎드려쏴 재확인 (T11 뒤) | 0.2 | queued (blocked T11) | - | - | T11 | spec/cards-t39 : korean-division/tasks/T39j-prone-recheck.md |
| T40 | 열차 전멸 판정 포함 (AliveCars, FV59 유지) | ? | wip-ready (로그 13:5x, 2de288d) | kd/trains-in-annihilation @2de288d | kd-develop/kd-trains-annih | T30e | ? |
| T41 | 충돌 로그 | ? | wip-ready (로그 14:0x, 69320b1) | kd/crash-log @69320b1 | kd-develop/kd-crash-log | main | ? |
| T42a | 중대 피로·사기 계정 (스키마 v4) | 0.2 | wip-ready (로그 14:3x) | t42a-fatigue-morale @ba621fe | kd-develop/kd-t42a | T38f | spec/cards-t42 : ? (T42a~d 파일명 확인 필요) |
| T42b | 진급 후보·적용 (스키마 v5) | 0.2 | wip-ready (로그 14:3x) | t42b-promotion @51b92c3 | kd-develop/kd-t42b | T42a | spec/cards-t42 : ? (T42a~d 파일명 확인 필요) |
| T42c | 포로 장부·송환/수용 (스키마 v6) | 0.2 | wip-ready (로그 14:4x) | t42c-prisoners @66e7637 | kd-develop/kd-t42c | T42b | spec/cards-t42 : ? (T42a~d 파일명 확인 필요) |
| T42d | 일정·연표 (스키마 v7; 포로 이중집계 수정 06ee9bd) | 0.2 | wip-ready (로그 14:4x·15:0x) | t42d-schedule-log @06ee9bd | kd-develop/kd-t42d | T42c | spec/cards-t42 : ? (T42a~d 파일명 확인 필요) |
| T43 | 병종 기호 곡사포·저격 추가 (+ 기병 기호 수정 d45b18f) | 0.2 | wip-ready (로그 14:1x; T43b 커밋 d45b18f) | kd/t43-branch-symbols @d45b18f | kd-develop/kd-t43 | main | kd/t43-branch-symbols : ? (카드 파일명 확인 필요) |
| T44 | 미션 전수 동작 스모크 | 0.2 | running (발주됨, dirty 3) | kd/t44-smoke @1c4036d | kd-develop/kd-t44 | main | spec/cards-t42 : ? (카드 파일명 확인 필요) |
| T45 | 메뉴 숨김 (플레이 버튼 DevMenu 뒤·AI vs AI 미션 관전 탭) | 0.2 | wip-ready (로그 14:0x) | kd/t45-menu-hide @88735b1 | kd-develop/kd-t45 | main | ? |
| T46 | = T38c (명령 카드 쪽 나눔, kd/t46-card-overflow) | 0.2 | wip-ready | kd/t46-card-overflow @54e8a54 | kd-develop/kd-t46 | wip/breach2 | = T38c 행 참조 |

## 스택 체인 (기반 → 위)

- **gemini-import → t09-camp-slice → t19a → t19b → t19b2 → t19c → t19d → t38f → t42a → t42b → t42c → t42d** (캠페인 축). t19d 위에 T38f(t38f-home-readonly-tabs, b622d1d) 가 있고 그 위가 T42a~d. (로그 13:5x~15:0x)
- **gemini-import → t29 (economy JSON) → t29b-on-t09** : T29b 가 t09 위에서 T09·T29 를 대체 (병합 시 단독).
- **breach2 → orderq2 → ai3b2 → {t24 (kd/t24-s1s3) → t24-crossing-fix, t25, t31a, t31c, t32a → t32d, t32b, t34a, t10 (kd/t10-comms-b) → t10b}**. 참고: breach2 → t16 (kd/t16-trench). breach2 → t46 (T38c, kd/t46-card-overflow).
- **rail → t28** (T28 cover-forest). **rail → river-r1 (T39a) → river-r2 (T39b) → river-r3 (T39c) → river-r4-bridge (T39d)**. 병합 순서 rail → r1 → r2 → r3 (FV 53 rail / 54 r1).
- **rail → t30a → t30b → t30c → t30e → trains-annih (T40)**. T30d(art, 8e51e5a) 는 별도 표시.
- **t27-cmd-ui2 → t38g-ui-harness → t38b-help-overlay → t38a-settings-keys (T38a)**; t38e 는 t38a 와 같은 tip. **t38g → t38h-fuzz-campaign (커밋 메시지 기준, 확인 필요)**.
- **t33a-map-model (c5b30f4) → t33b-map-ui (5207b8f, 체리픽) → t33c-placement (c514821) → t33d-rail (c514821 와 같은 tip)**. 카드 브랜치 kd/t33-map-editor-design (3b7c994).
- **kd-rerender (T11) → T39e~j** (T11 끝나기 전 make_unit/rerender96 편집 금지).
- **ai3b2 → T34c** (FV64 예약 후보, 발주 전).
- **measure/t12, measure/t15, measure/t31b, kd/t16b-rootcause, measure/t06b** : 측정·분석 전용 브랜치 (코드 변경 없음).
- **integration/rehearsal (984c404)** : 통합 리허설 카드·기록 전용 (코드 변경 없음, 워크트리 kd-integration).

## 메모

- 워크트리 체크아웃 불일치 3건: `kd-develop/kd-t30b` 는 kd/immune-target-fix(T36), `kd-develop/kd-t24` 는 kd/acting-delay-tier(T37). T30b·T24·T36·T37 전용 워크트리는 없다(기존 워크트리 이름과 체크아웃이 어긋남). 정리 방법 = ops/WORKTREE_CLEANUP.md.
- ops/STATE.md 의 커밋 해시(예: T10b 4ac07b4, T16 8b38b29, T24 e4fd65b, T36 등) 는 이후 재작성돼 현재 tip 과 다르다. 이 표는 `git branch -v` 기준 tip 을 적었다.
- 카드 파일이 없는 항목: T02 (main 에는 있음), T14·T18·T20·T26 (카드 파일 없음), T19b2·T23·T40·T41·T45 (카드 파일 `?`), T35·T36·T37·T43·T44 (경로 확인 필요).
- 밸런스 보류 (QUEUE): T12 · T15 · T16/T16b · T31a · T31b 후속 · T31c 항복 값 · T24 ②③ · T34d · T06 — hold-balance 0.3 / closed 로 표시.
- 0.4 (AI 개선): T31c 켜기, T32b, T32c, T34a~e, T10b 이후 AI 항목.
- 확인 필요 칸: T04·T10·T25 (커밋만 있고 wip-ready 선언 없음), T17 (CARD_INDEX 기준), T33d (tip 공유), T38h (로그 기록 없음), T13 (release 실가동 기록과 queued 불일치).
