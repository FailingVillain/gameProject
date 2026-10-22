# AUTO_DEFAULTS_LOG — 추천 기본값 자동 적용 기록 (사용자 검토용, append-only)
> 규칙(사용자 2026-10-22): 추천 기본값은 자동 적용하고 여기 **한 줄**만 남긴다. 사용자가 이의를 제기하기 전까지 유효. **토큰 누수 금지**: 이 파일은 세션 시작·카드 작업에서 읽지 않는다(읽기 목록 제외). 에이전트/지휘자는 `echo "..." >> ops/AUTO_DEFAULTS_LOG.md` 로 추가만 하고 파일을 열지 않는다. 사용자가 검토를 요청할 때만 읽는다. 한 줄 ≤160자.
> 형식: `날짜 | 영역 | 적용한 기본값 | 바꾸는 법/근거`

2026-10-22 | UI 3구역 | 구역 이동=가로 이동(가장자리 화살표·Q/E·점 3개), 좌=기록실·우=인사실, 탭 배치 기존(중앙 9/좌 3/우 3) | DECISIONS AY, 시안 승인 시 확정
2026-10-22 | 연대장 | 호감 소비처 3·재기용 쿨다운 1작전·해임 연쇄 끔·잠금 '???'·해임 호감 -2(임시) | COMMANDERS_DESIGN Q-C1~12
2026-10-22 | 튜토리얼 | 지도 탭 [0-1/0-2 연습] 버튼, 결과 미저장, 항상 활성 | T38i 카드
2026-10-22 | 도감/도움말 | 도감(F2)·도움말(F1) 열려도 게임 계속(도감만 틱 정지, Paused 불변) | T38e/T38b 카드
2026-10-22 | FV 번호 | T32b=65·T34a=66 (Replay 불변), AE2 62 비움 | MERGE_PLAN, FV_RESERVATIONS
2026-10-22 | T48b | 분할: T48b1(opus 신원 영속)·T48b2(sonnet 풀/참모); 스냅샷 A안(MissionState 매핑, FV 52→53) | 장교 신원 조사(89b2e5f)
2026-10-22 | 병합 | merge/0.2 에서만 병합, main 은 --ff-only 마지막, 그룹별 태그, 충돌 임의 해결 금지 | MERGE_PLAN
- 2026-10-22 저장 호환 정책: 캠페인=항상 이행(더하기만), 미션 저장·리플레이=옛 형식 거부+한국어 안내, 설정=관대, 사용자 파일=건너뛰기 -> ops/SAVE_COMPAT_POLICY.md (권고 기본값, 자동 적용)
- kd/t33d-rail review: protected-path = korean-division/assets/ or assets/maps|missions; test id _editor_test_<pid>, cleanup at editor exit (not right after launch)
T48a: 연대장 풀 시작상태 = 조건 없음 available/있음 locked; 학파 목록 commander_tags.json; 해임 -2, 쿨다운 1, 호감 -20~100, 재기용 최소 -5 (economy JSON)
T48a: 일정 탭은 Kind mission/event 외 연표 줄 건너뜀; Sync 는 Load()/Apply 에서만(FromJson 이행은 빈 풀); 항목 거부 시 로드 계속
2026-10-22 | 게임 제목 | 'KOREAN DIVISION'/'코리안 디비전' + 부제 '1920 대한 사단'(시안 적용, 사용자 직역체 선호 반영) | 사용자 확정 시 DECISIONS
2026-10-22 | 웹 검색 | 모든 웹 조회·검증은 hub ask gemini-flash -w 로(web-fetch/WebSearch 에이전트 금지) | 사용자 재지시
- 2026-10-22 T48b1: 장교 이름=병종 이름(개인 이름 데이터 없음) · 공적=누적 사살 · 부상 복귀 상수 1작전 · 연표는 대대장 전사/부상만 · 항복 장교=전사 처리
- 2026-10-22 T48b1: OfficerRoster=T48a CampaignOfficers 확장(별도 목록 안 만듦) · 키 주입 미션의 미출전 키 장교 이월 줄 보존 · 참모 배정 장교 미주입
T48c: favor tiers 냉담<10/보통/호의40/신뢰70 x0.5/1.0/1.15/1.3 (scaling on per AY), tagMatchDelta 1, deltas {1,2,3,5}; commander_favor.json
- T49a defaults: migrated .bak=campaign.json.v<n>.bak copy once; rejected rows grey+reason; fuzz-campaign flag absent in stack (plain uifuzz run)
T49bc: settings tolerant read per-key via reflection; compat fixtures v1-v8 key-filtered from git history
2026-10-22 | 성능 | T50a~e 는 순수 리팩터(골든 동일 필수)만 자동 진행, 표적 캐시·탐색 반경 축소는 사용자 결정 전 미발주 | ops/PERF_OPT_PLAN.md §5
T47 campaign map zoom: range 1-8x, wheel x1.18, button x1.5, node pick radius 14px, drag slop 5px (UI-only, per-session)
2026-10-22 | 제목 순서 | 프로젝트=KOREAN DIVISION, 게임명='KOREAN DIVISION : MARCH ON MANCHURIA'(프랜차이즈 먼저; 확장·후속 대비) | 사용자 이의 시 역순 'MARCH ON MANCHURIA : KOREAN DIVISION'
2026-10-22 | 민가 | T55g 생성기 시트는 사용자 승인 대기 없이 T55a/b 자동 착수(시트는 추후 일괄 검수) | 사용자 '그래픽 시안은 추후 한 번에 검토'
