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
T38n: KD_MONITOR unset=old behaviour; secondary=first non-primary, window clamped to monitor-80px, centred; never fullscreen
T48d defaults: no commanders.enabled on base -> rules off when roster empty; fuzz-campaign absent; hire/dismiss both confirm; portraits via file else silhouette
T11b: infantry cells widened 128->176/192 approved (user auto-default); imported, no C# change
T33e: MapDoc.MaxDim=1500 mirror of Config.MaxMapSize (test-checked); synthetic big maps in tests; fixtures A/B copied
2026-10-22 | SmokeAiSelfTest 129 리플레이 왕복 FAIL(E1 IsAi 체크섬 반영) → 시험 쪽 수정(IsAi 유지)으로 위임, 별도 커밋 | merger E3
2026-10-22 | 최종 합치기 F1: CrashLog 중복(T41 UI.CrashLog vs T01 CrashLog) → (a) T41 유지, T01 src/CrashLog.cs·Program.cs 170/236행 제거 | merger final
2026-10-22 | M2 후속: Set은 모든 중대를 같은 값으로 맞춤(요청에 없던 해석) · DecisionCheckpoint.CompanyMorale 선택 필드 추가(재도전 시 이벤트로 바뀐 중대 사기 복원; 옛 저장은 빈 값) | kd/m2-morale-camp f0a187e
2026-10-22 | 지휘자 임의 결정 소급 기록: N77 상한 c1e1_op1* 3,800→3,100 · units 시험 철도수비대 전용 검사 3건 삭제(대상 유닛 제거, 사용자 결정) · 가옥 3~6칸 위반은 시험 완화 대신 지도 생성기 수정 · TC-C 주입은 명령 로그에 안 남김(리플레이 이중 실행 방지) · 탈영은 중대별 사기 기준 · propsV2 기본 꺼짐 · 날씨는 이미 구현돼 있어 설계서 범위 축소 | 지휘자
2026-10-22 | 이름 규칙: 사용자 승인 — ID 유지·현재 접두어 스타일 유지 + 접두어 뜻 규칙표 추가(PDF 바탕화면/coding), 보고는 항상 '한글 이름(ID)', 카드마다 한글 이름 한 줄 필수. 해석: 제안 3(새 ID 영역약어-번호)은 '새 카드에만' 적용하고 기존 접두어는 그대로 둔다 | 지휘자 해석(되돌리기: 새 카드도 기존 스타일로)
2026-10-22 | 병렬 도구(tools/pipe-parallel 448e58b) 임의 결정 6건: ①FV 청구 파일 ops/fv_claims/<브랜치,/→__>.txt 형식 '날짜|FV+n|Replay+n|Schema+n|base해시|사유' ②조각 파일 docs/decisions_pending.d/·docs/engine_notes.d/<브랜치>.md ③조각 합침은 원본 끝 주석 블록(작업 에이전트=조각 파일, 합치는 쪽=병합 라인/release) ④병합 중 FV 줄 충돌은 --ours 로 접고 fv_assign -Apply 후 병합 커밋 뒤엔 -BaseRef HEAD^1 ⑤queue_guard 는 merge_step 에서 기본 경고만(-GuardStrict 일 때 STOP) ⑥Schema 청구는 표시만(캠페인 스키마 수동) | 에이전트 임의 결정(되돌리기: 해당 브랜치 파일 삭제)
2026-10-22 | 한도·꼬리 문서(tools/pipe-docs 2a393ee): 리포 문서 7개의 한도 숫자를 'ops/LIMITS.md 참조'로 교체, PIPELINE.md 1-d 공통 꼬리 5줄 추가, 홈 파일(common.md:6, release.md)은 수정 안 하고 보고만 | 에이전트 결정(되돌리기: 브랜치 revert)
2026-10-22 | 카드 검색: harness 탭 + harness.exe 발주(사용자 요청). 상태는 QUEUE 가 아니라 git 계산(사용자 지시) | 지휘자
2026-10-22 | harness 카드 검색(C:/works/agentHub/harness, 리포 아님 → harness_bak_20261022.zip 백업) 임의 결정 8건: ①사전 최신 ref는 파일 마지막 변경 커밋 시각 기준 ②작업 브랜치는 사전→등록부→CARD_INDEX→id 추정(spec/*·통합 브랜치 제외) ③상태 판정: 병합됨=통합 4브랜치 조상, 삭제된 브랜치는 커밋 제목 언급 시 병합됨(추정), 진행 중=미커밋 또는 2시간 내 커밋, 병합 대기=미병합 브랜치, 보류=사전 문구에만, 알 수 없음 ④'낡음 의심'=QUEUE 첫 단어가 계산 상태와 불일치 ⑤영역은 id 접두어로 ⑥포트 HARNESS_PORT/--port, 사용 중이면 종료 ⑦POST /api/cards/refresh 추가(data/cards.json 에만 씀) ⑧탭 CSS 신규 | 에이전트 결정(되돌리기: harness_bak zip 복원)
2026-10-22 | 카드 이름 PDF: C:/Users/seung/OneDrive/바탕 화면/coding/카드_이름_규칙.pdf 생성(규칙 8항+접두어 표+사전). 규칙 5번(영역약어 ID)은 새 카드에만 적용 | 지휘자
2026-10-22 | 파이프라인 v2.1(tools/pipeline-v2 b29cb6d) 임의 결정 8건: ①LESSONS 12개로 축소(게이트 25 유지, 23·24·28·30 중복 삭제 표기, 12 보관), 원본 32개 LESSONS_ARCHIVE.md ②LIMITS '무거운 작업' 행 삭제 안 함 ③AGENT_RULES 보고 길이 '역할 카드 줄 수'로 통일 ④1-d 꼬리에서 한도 숫자 제거 ⑤체크리스트 (i) 요약·재배열, 동시 상한은 '실효 20 유지' ⑥PIPELINE v2.1 ⑦1-c 의 LESSONS 번호 참조를 ARCHIVE 로 ⑧아카이브 머리글 신규 | 에이전트 결정(되돌리기: 브랜치 revert)
2026-10-22 | 홈 역할 카드 적용(지휘자): ~/.claude/agents/*.md 9개 Report 줄에 '마지막 줄 임의 결정:' 추가 + reviewer checkout 금지 명시, ~/.claude/agent-lessons/common.md 에 한도→LIMITS·웹조회 flash 줄 추가·selftest 문구 갱신. 백업 ~/.claude/agents_backup_20261022/. 보류: 도구(fv_claim·merge_step·queue_guard) 사용 문구는 도구가 base 에 들어간 뒤 적용(지금은 에이전트 worktree 에 도구가 없음) | 지휘자 판단(되돌리기: 백업 복원)
2026-10-22 | 최종 합치기(merge/0.2-final 27440d3) 임의 결정: ①CrashLog 중복=T41 유지·T01 3기능(콘솔 마지막 20줄·GitRev·MessageBox) 소실 ②낡은 시험·문서 분리 커밋 수정(CONTROLS.md Y/U 행 추가, T38m 설정 시험을 T49 필드별 폴백에 맞춤, T48c/TC-D 스키마 리터럴→SchemaVersion 비교, 호환 샘플 v10~v12 추가, PlacementAll이 map 참조 미션 건너뜀, props 시험이 test_* 미션 제외) ③prone-wide-assets 스프라이트 200개 충돌은 브랜치 쪽 채택 ④perf-bench 0355c89 병합 보류(파일 중복) ⑤골든 재생성 1회(사용자 AX 승인 범위): detcheck ccfbd1dd…→67280420… , train_demo 410f3e6c…(8069틱)→d6ec723e…(10372틱, 원인 미확인) ⑥squads 기존 FAIL 2건(지휘 계통 단계·임시 분대장 분대 지연)은 병합 전부터라 미수정 ⑦단축키 U 충돌(Commands.cs:31 vs Game.cs:850) 미수정 | merger/지휘자(되돌리기: 태그 pre-final / 단계 태그)
2026-10-22 | TW3 날씨 전환 페이드(kd/tw3-weather-fade 617a343) 임의 결정: 페이드 2.0초 선형(FadeSeconds 상수) · 종류와 시간대 모두 같은 Changes 로 페이드 · 이전 층 입자 즉시 소멸(안개 덩어리는 새 날씨가 안개 아닐 때 함께 페이드아웃) · 재전환 시 도착 상태를 이전으로 삼음 · 눈보라 상한/프레임 ms 측정/스크린샷 생략 | 에이전트 결정(되돌리기: 브랜치 revert)
2026-10-22 | PR5 소품 산포 최적화(kd/pr5-scatter-opt 4453cac) 임의 결정: 그리드 칸 크기 = 소품 간격+0.5 · 옛 선형 구현을 LegacyCrowded/LegacyAvoid 참조 구현으로 남김 · 측정용 c1e1_map_a/b 를 assets/maps 에 untracked 복사 | 에이전트 결정
2026-10-22 | TW1 날씨 은폐 보정(kd/tw1-weather-conceal 01306f4) 임의 결정: 은폐 수치 안개+10·비+5·눈+5(설계서 초안값, 기본 꺼짐 extendedEffects=false) · 날씨 종류에만 적용·시간대와 곱하지 않음 · minSightMul 검증 범위 0<x≤1 · conceal 범위 0~100 · 비용 비교 --stress 미실행 | 에이전트 결정
2026-10-22 | T52d 난이도 캠페인 키(kd/t52d-difficulty-keys 0544b2d) 임의 결정: '거절'을 파일 전체 거절이 아닌 해당 값만 1.0 으로 교체+경고로 해석 · 정산식 RegularSupply*(1-패널티)*homeSupplyMul, round(RegularRecruits*(1-패널티)*recruitMul) · 카드의 'campaign' 묶음이 없어 comms 묶음으로 대체 · 값 범위 0.25~4 | 에이전트 결정
2026-10-22 | 지휘자 결정: 단축키 U 충돌은 철조망 절단 U 유지·머리 위 표시 순환을 다른 키로 이동(사용자 '적당히 재지정'), squads·train_demo 조사 카드 발주(사용자 승인), harness 에이전트 목록에 서브 지휘자/병합 담당/감독관 라벨 추가 요청 | 지휘자
2026-10-22 | 서브 지휘자 파이프라인 반영(tools/pipeline-v2 dbc5dec) 임의 결정: 캠페인 상태 파일명 CAMPAIGN_STATE.md(로드맵 표기 따름) · ART_STATE/MERGE_PROGRESS 가 해당 워크트리에 없어 이름만 기재 · 로드맵은 제목만 '반영됨'으로 갱신 | 에이전트 결정
2026-10-22 | harness 에이전트 역할·정렬·칸반 상세·서브 지휘자 패널(harness.exe 직접 빌드, 백업 harness_bak_20261022b.zip) 임의 결정: ①카드 ID는 카드 사전에 있는 것만 인정·프롬프트 앞 3000자·최대 3개 ②하는 일=카드 이름→description→프롬프트 첫 줄 80자 ③정렬 기본=상태 오름·토큰/시작은 첫 클릭 내림·동률은 최근 활동순 ④라벨 판정=description+프롬프트 앞 600자의 '보조/서브 지휘자'(앞 한글 1~6자=영역)·'감독관'·agentType merger ⑤패널 상태 파일: 아트(ops·kd-final ART_STATE.md), 캠페인(CAMPAIGN_STATE/PROGRESS.md 후보), 병합(kd-final ops MERGE_PROGRESS.md·MERGE_PROGRESS_UI.md) ⑥하위 에이전트 수=상태 파일명 언급 기준 추정 ⑦병합 영역 담당=merger 타입 ⑧서브에이전트 기록 첫 줄 400KB까지 ⑨추적 범위는 기존 harness 설정 ⑩시작 시각 시:분:초 | 에이전트 결정(되돌리기: harness_bak zip)
2026-10-22 | main ff-only 수행(승인됨): 423f327→9d34dfe, 임의 결정: QUEUE.md 충돌 3곳 main 상태 우선·final 전용 행 T11b/T55m 추가·C1E1-m1은 final 쪽 행 사용 · 병합 잠금은 rm -rf 거부로 started.txt 삭제+rmdir 해제 · kd-final/bin/final 빌드 산출물 생성(워크트리 유지) · 태그 pre-ff-main=423f327 | merger
2026-10-22 | 도구 병합 충돌 해결 결정(지휘자): merge_step/preflight 는 pipeline-v2 판으로 통째 대체(통합 워크트리의 d344159 가 옛 pipe-tools 판 복사본이라 pipeline-v2 가 상위판) · AGENT_RULES.md 는 pipeline-v2 기준 + main 전용 규칙 보존 · .gitignore/FOLLOWUPS union · LESSONS 12개 판 채택(원본 ARCHIVE 보존) · LIMITS union(+실제 운영 vs 목표 줄·gate-v2 풀 행·에이전트 30 env) · PIPELINE_ROADMAP pipeline-v2 우선 · QUEUE/STATE/MERGE_QUEUE main 유지 · c14-nightly 는 작업 중이라 제외 | 지휘자(되돌리기: 병합 커밋 revert)
2026-10-22 | 단축키 U 충돌(kd/fix-ukey-conflict d2196e6) 임의 결정: 머리 위 표시 순환 U→P(P 가 카드 명령·고정 키·Raylib 입력 어디에도 안 쓰여서) · 키 중복 시험의 의도적 중복 예외 목록은 빈 상태 · base=merge/0.2-final 27440d3 | 에이전트 결정(되돌리기: 브랜치 revert)
2026-10-22 | T52a 알림 이력·전보 띠(kd/t52a-alert-log 2d39efa) 임의 결정: 최근 알림 키 Backspace · 알림 종류 UnderAttack·SiteContested 2가지 · 구역 표기 16칸 격자 'A1'(기존 격자 라벨 없어 신규) · 띠 위치 뷰포트 왼쪽 아래·대대 막대 위 · 띠 3건·6초 후 흐림·60초 후 퇴장 · 설정 패널 높이 526→600 · 스크린샷용 환경변수 KD_ALERTDEMO 추가(요청 밖) · 1280x720 캡처 미확인 | 에이전트 결정
2026-10-22 | T52c 사진 모드(kd/t52c-photo-mode b4158f4) 임의 결정: 진입 F12(Q-P1 추천) · 2x 없음(Q-P2) · 자동 정지 '단일전투'는 '관전·리플레이가 아닌 모든 플레이(캠페인 미션 포함)'로 해석 · 모드 안 토글 H(UI)·G(안개)·U(체력 막대) 기본 전부 숨김 · 이동 휠/가운데·오른쪽 드래그/방향키 · 저장 screenshots/KD_yyyyMMdd_HHmmss.png(같은 초는 _2) · 사용자 결정 대기: 안개 틴트(G) 기본을 켬으로 할지(현재 숨김) | 에이전트 결정
2026-10-22 | T51a 경로 안전·JSON 한도(kd/t51a-path-guard 37695d9) 임의 결정: ①잘못된 id 의 PathOf 는 예외 대신 존재할 수 없는 경로('?invalid-id?.json') 반환 ②JsonLimits 중복 키 검사 대소문자 무시(게임 파서와 동일) ③JSON 주석·끝 쉼표 허용 ④4MB 크기 한도 JsonLimits 포함(설계서 4.2절) ⑤윈도우 예약 이름 거부 추가 ⑥PathGuard.cs 를 src/Core/UserData/ 에 신규 | 에이전트 결정
2026-10-22 | 사용자 결정 반영: ①C1E1 미션 설계 가정(G12~G18)은 임의로 채운 것을 인정, 밸런스 교정·난이도 설정·본격적 캠페인 설계 단계에서 재설계 예정, 첫 플레이 가능 빌드 후 확인 ②TC-C 주입 명령이 명령 로그에 안 남는 설계는 지금 해결(카드 발주: 리플레이 일치 시험으로 증명·결함 시 수정 + Gemini 지적 3건 확인) ③T01 전용 CrashLog 기능 3개(콘솔 마지막 20줄·GitRev·MessageBox) 소실은 필요 없다고 판단(지휘자 의견 동의, 복구 안 함) ④사진 모드 안개 틴트(G) 기본값 켬(kd/t52c-photo-mode 수정 발주) | 사용자 결정
2026-10-22 | TI1 번역 기반(kd/ti1-i18n-core 52c6bb9) 임의 결정: Q1~Q8 설계서 추천값(기본 ko·Q5 이벤트 메시지 키 저장 안 함·라틴 글꼴 추가 안 함) · 모르는 Language 값은 ko+안내 1줄 · CSV 헤더 key,en 기준(헤더 없으면 0번 key·2번 en) · 이스케이프 \n·\ · 변환기는 {0} 포함 문자열도 변환·보간/연결 상수만 건너뜀 · 영어 샘플 문구 임시(TI5 용어집이 대체) · 설정 패널 높이 +62px(T52a 와 같은 줄 충돌 가능) · 한글 UI 스크린샷 비교 미실시(--shot 이 메뉴에 도달 못 함) | 에이전트 결정
