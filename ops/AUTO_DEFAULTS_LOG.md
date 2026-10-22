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
T50d perf bench: Prof hooks reused, 3 seeds x ACE x 1k/5k/10k, 10% regress threshold, PerfProbe no call sites
- T48b2: hire cost not charged (chargeHireCost=false, JSON; cost unit undefined); regiment-companies/minRegiments checks off (JSON switches)
- T48b2: mission tags (provisional, commander_assign.json): 인명보존 loss<=10%, 속전속결 <=900s, 공세 loss>=30%; only on won missions
- T48b2: staff weight=1000+100*(rank-4)+50*xpTier clamp 500..2000; base supply 5%, intel 2 lines, research 10%, doctrine 5%
- T48b2: wounded officers: no new battalion assignment, staff allowed; staff->battalion return allowed; dead officers free their slot at settlement
- T48b2: supply staff bonus = floor(regular supply * permille/1000) added at first-win settlement; journal = Stock.RecentLog; schema stays v9
2026-10-22 | 엎드려쏴 | 보병 칸 폭 확장(176x160/192x160) 동의 처리 → 27종 import 발주(사용자 '정확한 직각' 요구; C# 변경 없음) | 이의 시 되돌림
- T38k: tiers Body16/26 Heading26/40/72 Numeric16/22; >=40px title-only glyphs; unused 7.4MB fonts kept (propose drop); no merge
2026-10-22 | 글꼴 | 배포 시 미사용 글꼴 3개(Gothic Bold·Myeongjo Regular·Coding Bold, ~7.4MB) 제외 권장(코드가 안 씀) — 개발 중엔 유지 | T38k FONTS.md
T55s: houseStyle absent=legacy 4; tones 255/238-230-218/222-230-240; retry 3; frameMb 2.0; B-variant key suffix .b
2026-10-22 | T55g | 스펙=JSON 파일(house_specs/*.json), 파이썬 표 아님: 생성기 무수정으로 T55a/b 추가 | 카드 요구 housegen_specs.py 대신
2026-10-22 | T55g | 발자국 [w,d] 는 가로형(4x3,5x3) 권장: 용마루가 x축 | 3x4 는 깊이가 커 맞배가 어색
2026-10-22 | T55g | 기존4종 재현 비교 시트 생략, 6샘플+팔레트변형3만 | 요청 범위(샘플 ~6)
2026-10-22 C1E1-u1 audit: 유닛 플래그 기준 표 작성, 외인 의용병 id 는 common_village_militia 후보로 두고 미확정 표기 (보고서만)
- C1E1-m1: 강 폭 26칸(사행 ±10), 여울 폭 14칸, 상류=동, 시작 위치 임시 2개, 가옥 북안10+소초2+내륙10 (스펙 범위 내)
- C1E1-m1: a_escape=북단 중앙 200x12 기본값, 갈대=grass, 밭=dirt, 제방=high 띠, 내륙마을 보조구역 a_inland_village 추가
c1e1-m2: annex NE of city, rubble=timber yard, flank path west edge (spec lacks positions); large=1200 default
T55a1: ko 12 classes (all registry tones=1), footprint 3x4 specified as [4,3] (wider than deep)
- T55b2 (Sonnet): zh giwa footprints written wide-first (4x3 not 3x4); barns 1 tone; renders in gt-house-b2/_out (untracked)
- C1E1-u2: 의용병 id=common_volunteer (신규, 민단 수치); 철도수비대=japanese 전용; 피난민=common 비무장 hp60 spd2.6
- C1E1-u2: 새 유닛 id 규칙 = 기존 접미 패턴(cavalry_fengtian, heavy_field_gun_fengtian, infantry_railway_guard_japanese, civilian_refugee)
T55a2: 9 giwa specs korean_a2_giwa.json set ko_a2, 2 tones via variant a; renders in gt-house-a2/render_a2
T55b1: gt-house-b1 specs manchu_b1_*, tone a=base b=variant; flat/thatch mix per request despite Gemini note flat roofs rare
- 2026-10-22 C1E1-g1: SK 실패 확정 일수 failDays 기본 0 (일수는 승리 시 확정 규칙의 안전한 해석, JSON 노드값으로 변경 가능)
- 2026-10-22 C1E1-g1: OP1 재도전 = IL2 확정 직후 상태 → D11 변형(강공/포위) 선택도 지워 다시 고름
- 2026-10-22 C1E1-g1: 에피소드 일수는 Episode.DaysUsed 별도. CampaignState.Day(missionDays) 연동은 g2/연동 카드로 미룸
- 2026-10-22 C1E1-g1: OP1 미션 id = c1e1_op1_assault / c1e1_op1_siege (Q10 파일 2개, Q18 이름 규칙), D10 결과 키 follow/delay/persuadeOk/persuadeFail
TC-D: base t48b2-assign(v9), schema v11 skip v10; supply gate for episode not enforced yet; fairy/sword price 2 temp; E2 cap via param
- T38m: Overlay default=간결(concise), U key cycles; concise hides colorblind markers/low bars unless selected (user 2026-10-22 messy)
2026-10-22 | 머리 위 표시 | 기본 간결(HP·탄약·색각 도형·분대 아이콘 선택/위험 때만), 색각 도형은 간결에서 선택·호버만 — 색각 사용자는 보통 이상 권장 | T38m, 사용자 확인 대기
