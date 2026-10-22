# CARD_NAMES - 카드 ID 읽는 이름 사전

기준: 2026-10-22. ID 는 바꾸지 않는다. 이 문서는 ID 에 읽을 수 있는 이름을 붙일 뿐이다.
출처: ops/CARD_REGISTRY.md (ID 183개) · ops/QUEUE.md (main 최신, 상태 우선) · ops/CARD_INDEX.md (브랜치 · 워크트리) · ops/MERGE_QUEUE.md · 설계서 브랜치 korean-division/tasks/ 첫 제목 · 도구·측정·kd 브랜치 카드.
이름은 카드 제목에서 가져왔다. 제목이 없는 항목은 QUEUE 제목을 썼다. 모르는 칸은 '확인 못 함'.

## 이름 읽는 법
| 접두어 | 뜻 | 예 |
|---|---|---|
| T## | 일반 작업 카드 순번 (T01~T57) | T03 참호 돌파 |
| T##a·b·c… | 한 카드를 쪼갠 조각 | T19a 저장 슬롯 |
| T19b2 · T24x · T46 | 쪼갠 조각의 재스택 · 별도 브랜치 · 같은 일의 별칭 (T46 = T38c) | T24x 도하 관측반 수정 |
| TC-A…TC-G | C1E1 캠페인 엔진 카드 | TC-B 경보 상태 |
| C1E1-m/u/g/s/d/t/v/p/q | 안동 전역 콘텐츠: 지도(m) · 유닛(u) · 엔진·그래프(g) · 미션 JSON(s) · 데이터(d) · 문구(t) · 변형(v) · 포트레이트(p) · 검증(q) | C1E1-s6 OP1 강공 미션 |
| M1~M3 | 사기(morale) 카드. 장부에는 M1 만 있고 M2·M3 는 장부 밖 | M1 사기↔제압 연동 |
| TF-A…TF-E | 캠페인 흐름 설계서 | TF-B 진행 연결 |
| TI1…TI6 | 번역(i18n) 설계서 | TI2 문자열 일괄 변환 |
| TW1…TW3 | 날씨 설계서 | TW2 눈보라 |
| RP1~RP3 · RV1~RV3 | RP = 전과 보고(AAR) 설계서 · RV = 리플레이 뷰어 설계서 | RV2 리플레이 커서 |
| PR1~PR5 | 카드 ID 가 아니다. kd/pr1-props-v2 등 브랜치 이름에만 있고 카드 파일·장부에는 없음 | 확인 못 함 |
| A1 · C14 · C15 | 카드 ID 가 아니다 (지휘자 추정과 다름). A1 = 병합 단계 이름(ops/MERGE_PLAN.md A1 = fv-renumber). C14·C15 = tools/c14-nightly · tools/c15-watchdog 브랜치 이름 | — |
| D## | 결정 장부 ops/DECISIONS.md 의 결정 번호 | D19 사상자 측정 |
| FV## | 저장 형식(FormatVersion) 번호 예약. ops/FV_RESERVATIONS.md | FV 53 |
| E1~E3 · F1~F7 · G · H | 병합 라인 단계 이름 (ops/MERGE_PLAN.md) | E2 하천 r1~r4 |
| 한 글자 절 A~Z · 두 글자 절 AA~ | 결정 장부의 절 이름 (ops/DECISIONS.md). BH 절은 M1 카드가 인용하지만 절 본문은 확인 못 함 | — |

상태: **병합됨** · **wip-ready** (완료, 병합은 지휘자 발주 뒤) · **진행 중** (assigned · running) · **대기** (queued · blocked) · **폐기** (closed · 다른 카드로 대체 · 종결). 설계서 카드는 상태 기록이 없어 '대기'로 둔다.

## 1. 전투 엔진
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T03 | 참호 돌파 | 참호 돌파 교전 규칙, 연막·철조망 절단 포함 | 병합됨 (6ad6c50c) | wip/breach2 |
| T04 | 명령 대기열 | 사격 통제와 명령 대기열 (T03 위) | 병합됨 (0377f8a0) | wip/orderq2 |
| T05 | 전술 AI 3B | 적 AI 블랙보드 3B (T04 위) | 병합됨 (f74a675c) | ai3b2 |
| T06 | 사상자 속도 | 사상자 발생 속도 조정 (D19 C 폐기) | 폐기 | wip/casualty2 (보존) |
| T10 | 통신 알파 ②~④ | 대포병·야간·행군 피로 전령 | 병합됨 (1c91c965) | kd/t10-comms-b |
| T10b | 난이도 소비처 | 전령 사로잡힘·유선 끊김 난이도 (기본 꺼짐) | 병합됨 (4d74ce9a) | kd/t10b-difficulty |
| T16 | 참호 돌격 하락 원인 | trench_assault 하락 원인 조사 (기준 미달) | 병합됨 (2cb118cc) | kd/t16-trench |
| T16b | 참호 원인 재조사 | 분석 전용 재조사 (분석 종결) | 폐기 | kd/t16b-rootcause |
| T19b | 탄약·포탄 규칙 | 구경별 탄약 5종, 포탄 규칙, 노획 (T19a 위) | 병합됨 (622db02b) | t19b-ammo-shells |
| T24 | 대리 지휘·돌격 제압 | D08 수정안 S1~S3: 대리 지휘 범위, 관측반 전진 한계, 돌격 제압 확인 | 병합됨 (07ac80cb) | kd/t24-s1s3 |
| T24x | 도하 관측반 수정 | 관측반 도하 문제 수정 (T24 위) | 병합됨 (390763a7) | kd/t24-crossing-fix |
| T25 | 착검 = 모든 돌격 | 착검 멈춤을 모든 돌격에 적용 (D02 A) | 병합됨 (29695c34) | kd/t25-bayonet-all |
| T30a | 열차 이동 | 열차 객체와 선로 이동 (FV 58) | 병합됨 (863f9d1e) | t30a-train-move |
| T30b | 장갑열차 무기 | 열차 무기와 객차별 피해 (FV 59) | 병합됨 (ca1cdb84) | t30b-train-weapons |
| T30c | 화물열차 목표 | 화물열차를 미션 목표로 (T30b 위) | 병합됨 (3de60d59) | t30c-freight-objective |
| T30e | 열차 시연 미션 | 장갑열차+화물열차 호송 미션 1개 (T30c 위) | 병합됨 (7f46a4cf) | t30e-demo-mission |
| T31a | 재투입 제한 | 후퇴한 중대 재투입 제한 (기본 꺼짐, 켜지 말 것) | 병합됨 (97ed0267) | kd/t31a-regroup |
| T31c | AI 항복 | AI 항복 개선 (키 전부 기본 끔, 켜기는 0.4) | 병합됨 (e9cd527a) | kd/t31c-surrender |
| T32a | 전투공병 | 보병 공병 유닛 infantry_pioneer (철조망 절단, 수류탄 2) | 병합됨 (26cc9fd9) | kd/t32a-pioneer |
| T32b | AI 연막 수류탄 | AI 연막 투척 (기본 꺼짐) | 병합됨 (b5f1da5) | kd/t32b-ai-smoke |
| T32c | AI 철조망 절단 | AI 철조망 절단 (0.4) | 대기 | — |
| T34a | 제압 문턱·엄호 철수 | AI 제압 인지 문턱과 엄호 철수 | 병합됨 (63a42b4) | kd/t34a-suppress |
| T34b | AI 틀 선택 | 측면·돌격 틀 선택 (0.4) | 대기 | — |
| T34c | 제병 협동 | 포·기관총·보병 협동 시점 (0.4) | 대기 | — |
| T34d | 기병 측면 강도 | 기병 측면 강도 튜닝 (밸런스 보류) | 대기 | — |
| T34e | 유휴 진단 | AI 유휴 진단과 난이도 겹침 (0.4) | 대기 | — |
| T35 | IsAi 복원 | 리플레이 머리말·체크섬 복원 (FV 60 / Replay 24) | 병합됨 (122bfff) | kd/isai-restore |
| T36 | 면역 표적 수정 | 공격 명령 기준 면역 면제 (AH2) | 병합됨 (48bd6ce3) | kd/immune-target-fix |
| T37 | 대리 부사관 지연 | 대리 부사관 지연 등급 (U4) | 병합됨 (7cfa0c25) | kd/acting-delay-tier |
| T40 | 열차 전멸 판정 | 열차 전멸 판정 포함 (FV 59 유지) | 병합됨 (2de288d6) | kd/trains-in-annihilation |
| M1 | 사기↔제압 연동 | 아군 사기에 따라 제압 하락 속도가 달라짐 (DECISIONS BH2) | 병합됨 (d75a2bc0) | kd/m1-morale-suppress |

## 2. 캠페인
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T01 | 캠페인 WIP 복원 | gemini-import 캠페인 WIP·UI, 저장 트랜잭션 수정 포함 | wip-ready | gemini-import |
| T09 | 캠페인 수직 슬라이스 | 0-1→0-2→1-1 한 사이클 (T29b 가 대체) | 병합됨 (0c82a472) | t09-camp-slice |
| T19 | 캠페인 홈 MVP (묶음) | 규칙 1~15·학파 구현. T19a~d 로 쪼갬 | 폐기 | spec/t19-split |
| T19a | 저장 슬롯 | 저장 정리, 자동 저장·재시도, 슬롯 9개 | 병합됨 (43e69b19) | t19a-save-slots |
| T19b2 | 기록 동기화 검증 | 기록이 맞는지 확인하는 검증 (T19b 위) | 병합됨 (4980e7bf) | t19b2-synced |
| T19c | 캠페인 홈 탭 | 캠페인 홈 3화면, MVP 5탭, 연대 배속 UI | 병합됨 (9260e367) | t19c-home-tabs |
| T19d | 학파(교리) | 장교 학파 복원, 참모진 탭 읽기 전용 | 병합됨 (04a91497) | t19d-doctrines |
| T29 | 경제 숫자 JSON | 캠페인 경제 숫자를 JSON 으로 옮기고 첫 승리만 정산 | 폐기 (T29b 대체) | t29-campaign-economy-json |
| T29b | 경제 JSON 재스택 | T29 를 T09 위로 다시 쌓음 | 병합됨 (a89b4d23) | t29b-on-t09 |
| T42a | 사기·피로 계정 | 중대 피로·사기 계정 (스키마 v4) | 병합됨 (ba621feb) | t42a-fatigue-morale |
| T42b | 진급 후보 | 진급 후보 선정과 적용 (탭 9) | 병합됨 (51b92c3d) | t42b-promotion |
| T42c | 포로 장부 | 포로 장부, 송환·수용 (탭 7) | 병합됨 (66e76378) | t42c-prisoners |
| T42d | 일정·일지 | 일정·연표 (스키마 v7, 탭 14) | 병합됨 (06ee9bd1) | t42d-schedule-log |
| T48a | 연대장 데이터 | 연대장 풀 데이터, 스키마 v8, 로더 | 병합됨 (e31406d1) | t48a-commander-data |
| T48b | 장교 신원·배정 | 장교 신원 영속, 진급 종착, 배정 (T48a 위) | 대기 | — |
| T48b1 | 장교 신원 키 | 장교 신원 키(OfficerKey), 주입·환수, 대대장 주입 | 병합됨 (3fcdacf9) | t48b1-officer-identity |
| T48b2 | 연대장 기용·해임 | 기용·해임, 장교 배정, 참모 4슬롯 효과 (장부 밖 ID) | 병합됨 (80c11fba) | t48b2-assign |
| T48c | 연대장 호감도 | 호감도와 해임 시 소폭 하락 | 병합됨 (b543fd40) | t48c-favor |
| T48e | 2단계 전투 연결 | 연대장과 전투 연결 (기본 꺼짐, T48d·T10·T24 뒤) | 대기 | — |
| T49a | 저장 호환 적용 1 | 저장 메시지, 이행 .bak, 목록 회색 표시 | 병합됨 (48961134) | t49a-save-messages |
| T49b | 관대한 읽기 | 설정·사용자 지도·지휘관 JSON 을 관대하게 읽기 | 병합됨 (20132b4) | t49b-tolerant-read |
| T49c | 저장 호환 출시 게이트 | 캠페인 v1~현재 옛 샘플 테스트 | 병합됨 (20132b4) | t49c-compat-gate |
| M2 | 캠페인 사기·호감·민심 | 0~100 통일, 전투 후 탈영, 포 단가 (장부 밖 ID) | 진행 중 | kd/m2-morale-camp |
| TC-A | 브리핑 규모 분리 | 적 규모 분리와 정보 3단계 | 병합됨 (f7c6dcdf) | kd/tca-briefing |
| TC-B | 경보 상태 | SK2 위력 정찰의 경보 상태 (잠입) | 병합됨 (1e566261) | kd/tcb-alert |
| TC-C | 명령 주입·잠금 | 이벤트 중대 명령 주입과 명령 잠금 (D15 황손 독단) | 진행 중 | kd/tcc-inject |
| TC-D | 자금·외인 슬롯 | funds, 외인 슬롯, 통제 한도, 가용 보급 | 병합됨 (0b98b108) | kd/tcd-funds-slots |
| TC-E | 호감도 변수 | favor.* 변수 레지스트리와 결정 런타임 | 병합됨 (2dbada73) | kd/tce-vars |
| TC-F | 미션 변형 | SK 효용 플래그, BT1 이탈, pow.bandit | 병합됨 (43db8a13) | kd/tcf-variants |
| TC-G | 일본 철도 부속지 | 부속지 진입·사격 금지 구역 | 병합됨 (9490879b) | kd/tcg-annex |
| T57a | 연대장 위임 코어 | 위임 상태·명령·AI 구동 필터 | 대기 | spec/regiment-delegation |
| T57b | 위임 성향 매핑 | 학파·특성을 AI 경향 키로 연결 (값 비움) | 대기 | spec/regiment-delegation |
| T57c | 위임 UI | 연대 카드 토글·태도·표식·알림 | 대기 | spec/regiment-delegation |

## 3. C1E1 미션·콘텐츠 (안동 전역)
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| C1E1-d1 | 결정 데이터 | 결정 16개와 노드(EV/CH/IL) 데이터 JSON | 대기 | spec/cards-c1e1 |
| C1E1-d2 | 시작 상태·경제 상수 | E1 시작 상태와 경제 상수 (R5) | wip-ready | kd/c1e1-d2-econ |
| C1E1-g1 | 에피소드 그래프 | 노드·선택·일수·기한표 런타임 | 병합됨 (bd1dc11f) | kd/c1e1-g1-episode-graph |
| C1E1-g2 | 에피소드 화면 | 선택·결정·편성·기한 경고 화면 | 대기 | spec/cards-c1e1 |
| C1E1-g3 | 미션 이벤트 행동 | 결정 팝업, 변수 쓰기, 사기 증감 | 병합됨 (4f059987) | kd/c1e1-g3-events |
| C1E1-g4 | 목표 확장 | 항복·사기 붕괴 승리, 거점 N점, 협상 타이머 | 병합됨 (d0b3a1ff) | kd/c1e1-g4-objectives |
| C1E1-g5 | 피난민 유닛 | 비전투 피난민 유닛 정의와 호송 목표 점검 | wip-ready | kd/c1e1-g5-refugees |
| C1E1-g6 | 편성 해금 | unlock.* 로 야전포 등 편성 해금 | wip-ready | kd/c1e1-g6-unlock |
| C1E1-m1 | MAP-A 지도 | 압록강 서쪽 어귀 지도 (medium) | 병합됨 (425a2c0d) | kd/c1e1-m1-map-a |
| C1E1-m2 | MAP-B 지도 | 안동 일대 지도 (large) | 대기 | kd/c1e1-m2-map-b |
| C1E1-p1 | VIP 포트레이트 | 주요 인물 초상 자리표시 (선택, 낮은 우선순위) | 대기 | spec/cards-c1e1 |
| C1E1-q1 | 종단 시험 | 시나리오 S1~S4 헤드리스 시험 + selftest 추가 | 대기 | spec/cards-c1e1 |
| C1E1-q2 | OP1 성능 스모크 | 큰 지도와 약 5,500 유닛 성능 점검 (밸런스 아님) | 대기 | spec/cards-c1e1 |
| C1E1-s1 | BT1 교두보 | BT1 미션 JSON (기본형) | wip-ready | kd/c1e1-s1-bt1 |
| C1E1-s2 | SK1 비적 소탕 | SK1 미션 JSON | wip-ready | kd/c1e1-s2-sk1 |
| C1E1-s3 | SK2 위력 정찰 | SK2 미션 JSON (소초 존 진입 경보) | wip-ready | kd/c1e1-s3-sk2 |
| C1E1-s4 | SK3 약탈 금지 | SK3 미션 JSON (호송) | wip-ready | kd/c1e1-s4-sk3 |
| C1E1-s5 | SK4 기병 대 기병 | SK4 미션 JSON | wip-ready | kd/c1e1-s5-sk4 |
| C1E1-s6 | OP1 강공 | OP1 강공 미션 JSON (부속지 구역 포함) | wip-ready | kd/c1e1-s6-op1-assault |
| C1E1-s7 | OP1 포위 | OP1 포위 미션 JSON (차단 2곳, 6분, 돌파 2회, 협상) | wip-ready | kd/c1e1-s7-op1-envelop |
| C1E1-t1 | 기능 문구 | 결정 라벨·설득 표시·기한 표시 (서사는 키만) | 대기 | spec/cards-c1e1 |
| C1E1-u1 | 유닛 점검 | 유닛·무기·세력 사용 가능성 보고서 (보고서만) | wip-ready | kd/c1e1-u1-unit-audit |
| C1E1-u2 | 누락 유닛 5종 | 봉천 기마·야포, 철도수비대, 피난민, 의용병 | 병합됨 (4c309a4f) | kd/c1e1-u2-units |
| C1E1-v1 | 변형 데이터 | BT1 접근 3종, D02 B 이탈, SK 플래그 3종 | 대기 | spec/cards-c1e1 |

## 4. UI
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T08 | 조작 UI 묶음 | 조작 UI 묶음과 종이 질감 개편 | 병합됨 (2e9aef58) | t08-ui-bundle |
| T17 | 지휘 편의 | 미션 중 저장, 전과 보고, 교전 알림 | 병합됨 (0eb92e86) | t17-cmd-convenience |
| T27 | 지휘 UI 2 | 속도·일시정지 명령, 부대 목록, 색각 대응 팀색 | 병합됨 (4cf5c45f) | t27-cmd-ui2 |
| T38a | 설정 단축키 | 설정 메뉴에서 단축키 재지정 | 병합됨 (7274c711) | t38a-settings-keys |
| T38b | 도움말 오버레이 | 게임 안 단축키 도움말 | 병합됨 (93ce79f4) | t38b-help-overlay |
| T38c | 명령 카드 넘침 | 명령 카드 칸 넘침 감사와 수정 | wip-ready | kd/t46-card-overflow |
| T46 | 명령 카드 나눔 | T38c 와 같은 일의 별칭 (카드 이름은 T38c 사용) | wip-ready | kd/t46-card-overflow |
| T38d | 종이 UI 2단계 | 밝은 종이 팔레트, 사령관 책상, 소해상도 감사 | 병합됨 (5f601bc3) | kd/t38d-paper-ui |
| T38e | 부대 목록·도감 | 18행 넘는 목록 스크롤, 전투 중 도감, 툴팁 | 병합됨 (e9e1642f) | t38e-unitlist-codex |
| T38f | 홈 읽기 전용 탭 | 캠페인 홈 후속 탭을 읽기 전용 실화면으로 | 병합됨 (b622d1d8) | t38f-home-readonly-tabs |
| T38g | UI 회귀 하네스 | uifuzz 클릭과 해상도별 스크린샷 시트 | 병합됨 (f5a76d42) | t38g-ui-harness |
| T38h | 퍼즈가 캠페인 두드리기 | 퍼즈가 캠페인 홈을 누르는 시험 (--fuzz-campaign) | wip-ready | t38h-fuzz-campaign |
| T38i | 튜토리얼 연결 | 캠페인 홈에 튜토리얼 연결 | 확인 못 함 (병합 전, 커밋 3e414441) | t38i-tutorial-link |
| T38k | 게임 글꼴 | 게임 글꼴을 OFL 글꼴로 교체 | 병합됨 (07cb63f0) | t38k-fonts |
| T38l | 제목 문자열 | 게임 제목 문자열 교체 (창·메인 메뉴·HUD) | 병합됨 (d1158c9a) | t38l-title-strings |
| T38m | 오버레이 정리 | 화면 겹침 정리 (declutter) | 병합됨 (2ca199c6) | t38m-overlay-declutter |
| T38n | 모니터 환경 | 게임이 KD_MONITOR 설정을 따름 (창 모드만) | 병합됨 (a13672af) | kd/t38n-monitor-env |
| T45 | 메뉴 숨김 | 레거시 메뉴 숨김, AI 대 AI 는 관전 탭으로 | 병합됨 (88735b1e) | kd/t45-menu-hide |
| T47 | 작전 지도 확대 | 캠페인 작전 지도 탭 확대 | 병합됨 (체리픽 ead14ae4) | t47-campaign-map-zoom |
| T48d | 연대장·참모 관리 UI | 연대장·대대장·참모 관리 화면 (탭 8/9/12/14) | 병합됨 (b5a10afc) | t48d-commander-ui |
| T52a | 교전 알림 이력 | 교전 알림 이력과 전보 띠 | 병합됨 (c88bb69a) | kd/t52a-alert-log |
| T52b | 미니맵 핑 | 미니맵 다중 핑과 hover 강조 | 확인 못 함 (tip e4829534 병합 전) | kd/t52b-minimap-pings |
| T52c | 사진 모드 | 사진 모드 | 확인 못 함 (병합 전) | kd/t52c-photo-mode |
| T52d | 난이도 캠페인 키 | 난이도 campaign 키와 로더 검증 | 확인 못 함 (병합 전) | kd/t52d-difficulty-keys |
| T52e | 난이도 카드 UI | 난이도 카드 화면 | 확인 못 함 (병합 전) | kd/t52e-difficulty-ui |

## 5. 아트·렌더·음향
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T11 | 전 유닛 재렌더 | 유닛 그림 전체를 다시 그림 (병합 큐 끝 뒤 렌더 칸 예약) | 진행 중 | kd-rerender |
| T11b | 엎드려쏘기 넓은 칸 | 엎드려쏘기용 넓은 칸 에셋 가져오기 | 병합됨 (bdd8d9a4) | kd/prone-wide-assets |
| T18 | 한국군 타찬카 | 한국군 타찬카 (기마 포병, 스냅샷에 포함) | 병합됨 (a1a35e6) | main |
| T21 | 효과음 복원 | sfxnew 효과음 연결 (sounds.json, SfxDirector) | 병합됨 (aaba4147) | sfxnew |
| T22 | 얼굴 프로토타입 | 얼굴 프로토타입 재개 (game-tools) | 병합됨 (bc520079) | gt-face-proto |
| T30d | 열차 그림 | 열차 그림 규격과 스프라이트 계획 (코드 없음) | 병합됨 (8e51e5a5) | t30d-train-art |
| T32d | 공병 그림 | 공병 그림 (엎드려쏴 총구 수정 재렌더) | 병합됨 (af357ce3) | kd/t32d-pioneer-art |
| T39e | 행진 사격 총구 | 걸으며 쏘기 총구 화염 위치 재계산 | 대기 (T11 뒤) | — |
| T39f | 한·프 색 | 한국 ×0.78 · 봉천 ×1.22 색 적용 확인 | 대기 (T11 뒤) | — |
| T39g | 간도 복식 | 간도 의용대 복식 | 대기 (T11 뒤) | — |
| T39h | 보병 모션 2차 | 기어가기, 부상자 끌기, 돌격 수류탄, 참호 사격 | 대기 (T11 뒤) | — |
| T39j | 엎드려쏴 재확인 | 엎드려쏴 그림 재확인 | 대기 (T11 뒤) | — |
| T43 | 병종 기호 | 축소 화면 병종 기호 정비 (세력 휘장, 곡사포·저격 추가) | 병합됨 (d45b18f1) | kd/t43-branch-symbols |

## 6. 소품·지형·건물
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T02 | 건물 크기 확대 | 건물 크기 확대 (HouseBlocks 렌더) | 병합됨 (8d22ad5) | — |
| T07 | 철도 지형 | 철도 지형 (장갑열차 제외) | wip-ready | rail |
| T28 | 덮개 숲 | 엄폐물 군집화와 숲 윤곽 불규칙화 (기본 꺼짐) | 병합됨 (c577c0f4) | t28-cover-forest |
| T39a | 하천 여울 지형 | Ford 지형, 이동 배수, 지도 폭 | 병합됨 (ac77417) | river-r1 |
| T39b | 하천 경로 | 하천 길찾기와 도하 AI | 병합됨 (ac77417) | river-r2 |
| T39c | 하천 렌더 | 좁은 물 띠, 여울 오버레이, 하천 시험 지도 | 병합됨 (ac77417) | river-r3 |
| T39d | 하천 다리 | Bridge 지형과 다리 렌더 (기본 꺼짐) | 병합됨 (ac77417) | river-r4-bridge |
| T39i | 가옥 폐허 | 폐허 가옥 새 그림 | 병합됨 (66d6b953) | t39i-ruined-houses |
| T55 | 민가 다양화 (묶음) | 민가 스프라이트 다양화 총괄 | 대기 | spec/house-variety |
| T55a | 한국 마을 민가 | 한국 마을 민가 21종 | 확인 못 함 (병합 전, 8faffd26) | t55a-house-korean |
| T55b | 만주 민가 | 만주 민가 | 확인 못 함 (병합 전, 13610a92) | t55b-house-manchu |
| T55g | 집 생성기 | 파라메트릭 집 생성기 | 병합됨 (bd6e8859) | 확인 못 함 |
| T55i | 민가 임포트 | 민가 42종(66키) 임포트 | wip-ready | kd-t55i |
| T55m | 민가 데이터 | 민가 import, 테마 키, 지도 데이터 | 병합됨 (7730f79d) | t55m-house-data |
| T55s | 민가 선택 | 민가 선택 로직 (양식·반전·색조) | 병합됨 (6c8dfb32) | t55s-house-select |

## 7. 도구·측정·파이프라인·문서
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T06b | 사상자 재측정 | D19 C 재측정 카드 (T06 종결에 따름) | 폐기 | measure/t06b |
| T12 | 통합 밸런스 측정 | A/B/C 시드 8 측정 (밸런스 보류 0.3) | wip-ready | measure/t12 |
| T13 | 릴리스 | 전체 selftest·골든·publish·기록 (병합 큐 끝 1회) | 대기 | — |
| T14 | 문서 재정비 | 4층 구조, ops, PIPELINE v1 | 병합됨 (b5918a8) | docs-ops |
| T15 | 밤 soak 재보고 | 야간 장시간 시험 재보고 (밸런스 보류 0.3) | wip-ready | measure/t15 |
| T20 | 저장소 이동 마무리 | 옛 폴더 삭제 (사용자 결정 U02) | 대기 (blocked) | — |
| T26 | 레거시 메일 차단 훅 | 옛 이메일 차단 git 훅 설치 (D20 A) | 병합됨 (설치 완료) | .git/hooks |
| T31b | 종료선 검토 | 임무 종료 조건 측정 카드 (종결) | 폐기 | measure/t31b |
| T33a | 지도 편집 모델 | 헤드리스 문서 모델, 직렬화, undo | 병합됨 (c5b30f4b) | kd/t33a-map-model |
| T33b | 지도 편집기 UI | 편집기 창과 지형 브러시 | 병합됨 (5207b8ff) | kd/t33b-map-ui |
| T33c | 지도 배치 | 소품·시설·시작점·구역 배치 | 병합됨 (c5148215) | kd/t33c-placement |
| T33d | 지도 철도 도구 | 편집기 철도 도구 (선택) | 병합됨 (022647f9) | kd/t33d-rail |
| T33e | 큰 지도 | 지도 편집기 큰 지도 (상한 1500) | 병합됨 (4de8acd9) | kd/t33e-map-size |
| T41 | 충돌 로그 | 충돌 로그 기록 | wip-ready | kd/crash-log |
| T44 | 전 미션 스모크 | 전 미션 동작 스모크 하네스 | 병합됨 (f9dea2de) | kd/t44-smoke |
| T50d | 성능 벤치 | 성능 벤치 하네스 (측정 도구) | 병합됨 (장부) | tools/perf-bench |
| T51a | 경로 안전 | 경로 안전 수정과 JSON 한도 검사기 | 병합됨 (34dd8d4f) | kd/t51a-path-guard |
| T51b | 임무 패키지 | MissionPackage 열기·검증·내보내기 (헤드리스) | 확인 못 함 (병합 전, 7b55e0c3) | kd/t51b-mission-package |
| T51c | 편집기 내보내기·가져오기 | 공유 전 검사 포함 | 대기 | spec/mapeditor-share |
| T51d | 편집기 빈틈 | 편집기 미완 기능 보완 | 대기 | spec/mapeditor-share |
| T51e | 증분 렌더러 | 편집기 증분 렌더러 (보류) | 대기 | spec/mapeditor-share |
| T56a | 스프라이트 칸 포맷 도구 | 칸 B v2 포맷 도구와 보병 1종 시범 | wip-ready | kd/t56a-pack-sheets |

## 8. 설계서 카드 (spec 브랜치, 코드 없음)
| ID | 한글 이름 | 한 줄 설명 | 상태 | 브랜치 |
|---|---|---|---|---|
| T23 | 캠페인 홈 설계 | 캠페인 홈 설계 문서 (docs/CAMPAIGN_HOME_DESIGN.md) | 병합됨 (42fa4566) | spec/t23-campaign-home |
| TF-A | 캠페인 그래프 | 캠페인 그래프 데이터·로더·저장 객체 | 대기 | spec/campaign-flow |
| TF-B | 진행 연결 | 정산 후 다음 노드, UI 가 그래프 읽기 | 대기 | spec/campaign-flow |
| TF-C | 엔딩 화면 | 엔딩 판정, 엔딩 화면, 완료 저장 | 대기 | spec/campaign-flow |
| TF-D | 힌트 엔진 | 튜토리얼 힌트 엔진과 끄기 설정 | 대기 | spec/campaign-flow |
| TF-E | 힌트 내용 | 힌트 연결과 첫 실행 안내 | 대기 | spec/campaign-flow |
| TI1 | 번역 기반 | Loc 기반, 언어 설정, 시범 변환 | 대기 | spec/i18n |
| TI2 | 문자열 일괄 변환 | S 클래스 const 를 Loc 프로퍼티로 일괄 변환 | 대기 | spec/i18n |
| TI3 | 비(非) S 문자열 | S 밖 하드코딩 표시 문자열을 키로 | 대기 | spec/i18n |
| TI4 | 자료 텍스트 번역 | 자료 JSON 텍스트 옆 파일 번역 | 대기 | spec/i18n |
| TI5 | 영문 번역 | 용어집, 초안, 점검 | 대기 | spec/i18n |
| TI6 | 영문 레이아웃 검증 | 영어 레이아웃, 글꼴, 결정성 검증과 수정 | 대기 | spec/i18n |
| TW1 | 날씨 은폐 | 날씨 은폐 보정과 확장 효과 스위치 | 대기 | spec/weather |
| TW2 | 눈보라 | 눈보라 날씨 종류 추가 | 대기 | spec/weather |
| TW3 | 날씨 전환 페이드 | 날씨 전환 페이드와 눈보라 입자 상한 | 대기 | spec/weather |
| RP1 | 전투 타임라인 | BattleTimeline 표본기, MissionResults v2 | 대기 | spec/replay-aar |
| RP2 | 전과 보고 3쪽 | 요약·중대·그래프 3쪽 | 대기 | spec/replay-aar |
| RP3 | 그래프→리플레이 | 그래프 클릭으로 리플레이 시점 이동 | 대기 | spec/replay-aar |
| RV1 | 리플레이 컨트롤 | 속도·건너뛰기·결과 창 진입 바 | 대기 | spec/replay-aar |
| RV2 | 리플레이 커서 | 키프레임과 되감기 (seek) | 대기 | spec/replay-aar |
| RV3 | 시점 슬라이더 | 부대 추적, 사건 표지, 전체 보기 | 대기 | spec/replay-aar |

## 9. 장부 밖 ID 와 유의점
- 장부(ops/CARD_REGISTRY.md)에 없는 카드: M2 · M3 · T48b2 · T31b-endline50(카드 파일명. 별도 ID 아님). M3 는 kd/m3-morale-link 의 카드 제목 "캠페인 사기 평균 -> 미션 시작 세력 사기 연결"이며 상태는 병합됨 (e1d5404c 체리픽; 원본 747cc3b4 는 kd/m3-morale-link 병합 전).
- 카드 제목이 없고 QUEUE 제목만 있는 ID: T14 · T18 · T19 · T20 · T23 · T26 · T46 · T48b2 · T55i (위 표에 제목을 적음).
- PR1~PR5 는 카드 ID 가 아니다. 소품 카드가 필요하면 발주 전에 지휘자가 정한다.

## 앞으로의 이름 규칙 (제안, 미승인)
1. ID 는 바꾸지 않는다. 기존 ID(예: T03, C1E1-s6)는 그대로 둔다.
2. 보고와 대화에서는 항상 '한글 이름(ID)' 으로 쓴다. 예: 참호 돌파(T03).
3. 새 ID 형식은 `<영역약어>-<번호>` 이다. 영역 약어: ENG 전투 엔진 · CAM 캠페인 · MIS C1E1/미션 · ART 아트 · UI · PIPE 도구 · DOC 문서 · I18N 번역.
4. 번호는 `card_id.ps1` 로만 할당한다 (직접 번호를 붙이지 않는다).
5. 이 절은 제안이다. 지휘자 승인 전에는 적용하지 않는다.
