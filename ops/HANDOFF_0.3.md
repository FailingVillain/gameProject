# HANDOFF_AUTO - measured 2026-10-23 03:31 (generated; do not hand-edit)

## State (STATE_LIVE age 0 min; refresh: tools/agent/state_snapshot.ps1 -Write)
- main: `82b8f9c3 tools/agent: gate art pool limit 2 (KD_GATE_ART_LIMIT), run_game muted by default, AGENT_RULES ObjCodec rule`
- worktrees: 149 total ((top)=1 gt-develop=5 kd-develop=143)
- merge lock: none
- merge lines:
  - `merge/0.2-final` at kd-final: HEAD `2531ec1` latest tag `v0.3` dirty files 3
- this checkout (gameProject): Snapshot FV 67 / Replay 24
- heavy processes: 1 (validate 0 / measure 0 / render 1)

## Active queue rows (36; merged/closed/done excluded)
- T11 전 유닛 재렌더 (rerender-1, gt-brender, gt-rer :: main
- T12 통합 밸런스 측정 (A/B/C 시나리오 시드 8) → BASELINE_C :: ai3b2 스택
- T13 release (전체 selftest · 골든 · publish · 기록 :: T12
- T15 밤 soak 갱신 → 전투 문제 9건 재보고 :: ai3b2 스택
- T20 저장소 이동 마무리 (옛 폴더 삭제 = 사용자 U02) :: -
- T48b 장교 신원 영속 · 진급 종착 · 배정 · 연대장 기용/해임 · 전사/항 :: -
- T48e 2단계 전투 연결 (기본 꺼짐 키 4개, FV 예약 필요) :: -
- C1E1-m2 MAP-B 지도 생성 (large) :: -
- C1E1-u1 유닛/무기/세력 점검 (보고서) :: -
- C1E1-g2 에피소드 화면 UI :: -
- C1E1-g3 미션 이벤트 행동 확장 :: -
- C1E1-d1 결정 16개·노드 데이터 JSON :: -
- M2 캠페인 사기·호감·민심 0~100 기본 50 통일 + 전투 후 탈영 +  :: -
- C1E1-s6 OP1 강공 미션 JSON :: -
- C1E1-v1 변형 데이터 적용 :: -
- C1E1-t1 기능 문구 (서사 키만) :: -
- C1E1-p1 VIP 포트레이트 자리표시 (선택) :: -
- C1E1-q1 종단 시험 S1~S4 :: -
- C1E1-q2 OP1 성능 스모크 :: -
- NEW-1 지도 크기별 과부하 시험 (N13; measure) :: -
- NEW-2 LOD0 그림 누락 버그 조사 :: -
- NEW-3 제압으로 엎드린 병사가 대열 이동에 끌려가는 현상 :: -
- NEW-4 좁은 길목 우왕좌왕 :: -
- NEW-5 일본군 45식 군모·기수 깃발 3:2·보병 겹침 일괄 수정 (collid :: -
- T32c - :: T32b
- ... 11 more in ops/QUEUE.md

## Merge-line progress (last lines)
- kd-develop\kd-final\ops\MERGE_PROGRESS.md:
  - - 2026-10-23 02:54 step N3d-4 PASS d86804a7 (merge/0.2-final) tag post-N3d-4 | build:ok detcheck=672804207fc4e486 3c42aea49324248e selftest-snapshot:allowedFAIL=0 selftest-replay:allowedFAIL=0 selftest-crashlog:allowedFA
  - - 2026-10-23 03:13 step N3f-1 PASS 2531ec16 (merge/0.2-final) tag post-N3f-1 | build:ok detcheck=672804207fc4e486 3c42aea49324248e selftest-snapshot:allowedFAIL=0 selftest-compatgate:allowedFAIL=0 selftest-savecompat:all
- kd-develop\kd-final\ops\MERGE_PROGRESS_UI.md:
  - - Final combine of merge/0.2 + merge/0.2-ui is by the commander (campaign-side files overlap with sim: Snapshot/Replay FV lines untouched here).
  - Resume point: ui-post-H partial; nothing in progress, working tree clean.

## Agents active now: 0

## Gemini today: 55/300 pts

## Conductor adds by hand (<= 10 lines): user decisions pending, next card order, known problems

## 손으로 쓴 인계 (10줄 이내)
1. 기준: 태그 v0.3 = merge/0.2-final 2531ec16 (Config 0.2.1, FV Snapshot 74 / Replay 27 / 캠페인 스키마 15, selftest 133 그룹 PASS, detcheck 672804207fc4e486 3c42aea49324248e). 0.2.1 zip: C:/works/_dist_out/KoreanDivision-0.2.1-2531ec1622.zip. 백업 MANIFEST: C:/works/_backup/MANIFEST-v0.3.txt. push 금지.
2. 첫 일: ops/QUEUE_0.3.md(웨이브 1~3, 수면 모드 규칙) → ops/USER_DECISIONS_QUEUE.md(결정 대기 14건). 한도: 에이전트 9개·상급 3개, 재활용 없음, 중단은 사용자 지시 때만.
3. 사용자는 0.3 시작 후 취침 예정: 승인 버튼이 필요한 작업 금지, 거부되면 재시도 말고 ops/APPROVAL_NEEDED.md, 결정은 쌓기, 보고는 ops/WAKE_REPORT.md 에 누적.
4. WIP·미병합: kd/review-followups-small(WIP 354c02cf), gt/taegukgi-detail(41eab625, 승인됨), kd-b2-units(미커밋 wagon.py), N3e 브랜치(tools/autoplay-summary·soak-allon(충돌 Program.cs)·pro-pilot-save-timing·snapbench-map, spec/*), water-fire-open(골든 변경).
5. 병합 규칙: 병합 여부는 git merge-base --is-ancestor(rev-list --count 오판), FV·스키마 병합 단계는 compatgate 필수, 병합 잠금(.git/kd-merge.lock)은 병합 담당만 쥐고 지휘자가 해제, 새 FAIL 이면 멈추고 보고, 시험 완화 금지.
6. Gemini: flash(3점,슬롯3)·pro(10점,슬롯2)·flash_lite(1점,슬롯1), 300점/일, API는 웹 검색 전용(폴백 off), argon 미사용; 긴 로그·다중 파일 읽기는 route.py/agy_delegate flash 로, 결과는 단서(인용 줄 확인).
7. 안전 훅 kd_guard: 막히면 재시도·우회 말고 의도 판단 후 보고, 문서는 Write/Edit 로. 승격 스크립트 promote_v03.ps1 verify 단계 버그(LESSONS 참조).
8. 게이트: 렌더 풀 기본 2(KD_GATE_ART_LIMIT 로 병합 밖에서 3~4), B2 중단 뒤 남은 렌더 프로세스는 정리함(03:3x), 이후 STATE_LIVE 로 재확인.
9. 정리 대기(승인 필요): tools/perf-bench 워크트리·브랜치, kd-rerender-5 워크트리, main 의 추적 안 되는 옛 파일 약 2800개. main 에는 로컬 커밋만 있음(aa93ff18, 82b8f9c3 + 이번 인계 커밋).
10. 사용자 주의: 위임 UI 는 현재 수준 확정(개선 0.4), 연대장 12명 테스트 동안 즉시 채용 가능, 5개 유닛 재렌더는 0.3 에서 재시작, 번역은 보류.
