# ART_STATE - 아트 보조 지휘자 현황 (2026-10-22 ~21:30, 2호; 갱신 2026-10-23)
갱신 반영(지휘자 직접, 2026-10-22): '(갱신)' 표시 줄이 최신 사실. '이전:' 줄은 낡은 기록이며 삭제하지 않고 남김. 시트 폴더는 지시문에 명시된 것만 적음(33_D 만 C:/works/_review_sheets_2026-10-22/).
갱신 반영(docs 에이전트, 2026-10-23): '(갱신 10-23)' 표시가 최신. 낡은 '진행 중'은 완료/병합으로 바꾸고 원문은 '이전:' 으로 남김.

## 한도
- 아트 렌더 레인 <=8 (RAM >=6GB 일 때 7번째~). 비아트 heavy <=3 별도. 빌드 slot 1 비움. 남의 PID 종료 금지. 신규 게이트(gate-v2) 미설치 -> 기존 heavy_gate, 렌더 에이전트는 동시 1개.
- 분담(coordinator 지시): A1 지형타일(kd/a1-terrain-tiles), A2 지면데칼(kd/a2-ground-decals), B1(마적 보병·잡병·대도병·장교·척후 5), B2(간도·몽골/마적 기병·마포·tachanka 5), D 열차(kd/d-train-sprites), C 눈보라(sim) = 별도 에이전트. 나는 T39·T55·PR2·C1E1 신규 유닛·혼합 비주얼·VIP·시안정리.
- 2호가 띄웠던 deferred 렌더 레인 5개(G1~G5)는 B1/B2 와 중복이라 아직 렌더 시작 전에 종료함(내 PID 만).

## T11 (kd-rerender b324492)
- (갱신) PC 가 21:15 에 비정상 종료·21:24 재부팅 → 렌더 에이전트 다수 사망. 다만 구 드라이버 PID 22168(rerender96 --only cavalry-bandit,cavalry-mongol,heavy-horse-artillery,heavy-tachanka, kd-b2-units 워크트리)이 21:31 에 다시 돌고 있음.
- (갱신) cavalry-bandit 렌더 완료(rc=0, 미커밋). cavalry-mongol 렌더 중(22:06~). heavy-horse-artillery·heavy-tachanka 상태 미기재.
- 본 큐(rerender96 --list) = 0 to render, 37 done. 진행 기록 커밋 b324492 (wip-ready, 병합 대기. QUEUE 는 건드리지 않음).
- 이전: deferred 10 = infantry-bandit, infantry-thug-bandit, infantry-dadao-fengtian, infantry-gando, infantry-scout, officer-bandit, cavalry-mongol, cavalry-bandit, heavy-horse-artillery, heavy-tachanka -> T11 범위(81 중) 이나 B1/B2 로 위임, 미렌더.
- (갱신) deferred 10 중 cavalry-bandit·cavalry-mongol·heavy-horse-artillery·heavy-tachanka 는 위 PID 22168 범위. infantry-bandit·thug-bandit·dadao-fengtian 은 B1 커밋 완료(샤드 표 B1 행). officer-bandit·infantry-scout 는 B1 새 에이전트 진행 중. gando 는 T39g 행 참조.
- 구 드라이버 A/B/C/D/F(대기 전용) 아직 살아 있음: 정리는 지휘자 결정.

## 샤드 / 발주 표 (워크트리 kd-develop/<이름>)
| 카드 | 브랜치 @커밋 | 등급 | 상태 | 시트 (검수 필요) |
|---|---|---|---|---|
| T39h h1 기어가기·참호사격 | t39h-motion-r2 @16d158a | sonnet | 완료 wip-ready, 렌더 25분 | kd-t39h/korean-division/docs/art_review/motion_r2_crawl.png, motion_r2_trench_fire.png, motion_r2_regress.png |
| T39h h2 끌기·수류탄 | t39h2-motion-r2b @9b640f8 (h1 위) | sonnet | 완료 wip-ready, 620초 | kd-t39h2/.../motion_r2_drag.png, motion_r2_grenade.png (흠: 끌기 s/se 겹침, 수류탄시 소총 생략) |
| T39f 한국/봉천 색 | t39f-kr-ft-colour @2b4cf91 | haiku | 완료: 누락 0, 수정 없음 | kd-t39f/.../kr_ft_colour_applied.png (+.md). 미완 재확인: scout, horse-artillery, dadao-fengtian |
| T39e 행진사격 총구 | t39e-marchfire-muzzle @c837eb8 | sonnet | 측정·데이터 완료. 게임 UnitRenderer.MarchMuzzle 5방향 하드코딩이 원인, 코드 수정 필요 -> DECISIONS_PENDING 후보 | kd-t39e/.../marchfire_muzzle.png ; 표 kd-t39e/game-tools/image/muzzle/walkfire_muzzle_table.csv |
| T39j 엎드려쏴 재확인(확인만) | t39j-prone-recheck @f671be1 | sonnet | 완료: 총구·관통 합격, 칸 여백 <2px 19유닛 전부 E방향 불합격 -> 26보병 재렌더(gt-prone-fix)로 처리 | kd-t39j/.../prone_recheck.png |
| T39g 간도 복식 | t39g-gando-dress | sonnet | (갱신 10-23) 병합 완료 (N3-17): 렌더·import. 이전: 실행 중(게이트 대기 후 렌더, 재개 지시함, agent a69d7f5) | gando_dress.png 예정. B2 와 gando 렌더 중복 주의: B2 는 T39g 결과 반영 필요 |
| T55a 한국 민가 21종 | t55a-house-korean @8faffd2 (main 486c4d7) | sonnet | 완료 wip-ready (스프라이트 30, import 안 함) | kd-t55a/.../t55a_houses_korean.png, t55a_houses_korean_map.png ; 렌더물은 scratchpad 임시경로(재렌더 가능) |
| T55b 만주 민가 | t55b-house-manchu @13610a9 | sonnet | 완료 wip-ready (36 스프라이트, 5분) | kd-t55b/.../t55b_houses_manchu.png (라벨 한글 깨짐) ; 렌더물 C:/gameProject/render_out/t55b |
| 이전: C1E1 신규 유닛(피난민·의용병·봉천기마/야포) | kd/c1e1-art-units (base kd/c1e1-u2-units 4c309a4) | sonnet | 실행 중(재개 지시, agent a11b9cb). 철도수비대 제외, 마적·간도 위임 | c1e1_new_units.png 예정 |
| C1E1 신규 유닛(피난민·의용병·봉천기마/야포) (갱신) | kd/c1e1-art-units @8b73c78b (base kd/c1e1-u2-units 4c309a4) | 미기재 | theme.json 충돌 1건 수정 중. 철도수비대 제외, 마적·간도 위임 | c1e1_new_units.png 예정 |
| 공병 엎드려쏴 근본수정 | kd/pioneer-prone a69e6ac | sonnet | 승인됨(병합 라운드 M) | pioneer_prone_rootfix.png 확인 필요 |
| B1 마적·잡병·대도병·장교·척후 (갱신 10-23) | kd-b1-units @9b4d049f | 미기재 | (갱신 10-23) 마적 5종 병합 완료 (N3). 이전: infantry-bandit·thug-bandit·dadao-fengtian 완료 커밋. officer-bandit·infantry-scout 는 새 에이전트 진행 중 | - |
| A1 도로 (갱신) | kd/a1-roads @ab9ab733 | 미기재 | wip-ready, 리뷰 완료 (렌더 전용, 충돌 없음, 병합 OK; roads 먼저) | 25_A1_roads_contact.png |
| A1 지형타일·물 (갱신) | kd/a1-terrain-tiles @9bf4ffa5 | 미기재 | wip-ready, 리뷰 완료 (렌더 전용, 충돌 없음, 병합 OK) | 26_A1_water_contact.png |
| A2 지면 데칼 (갱신) | kd/a2-ground-decals @b63062dc | 미기재 | 완료, 리뷰 대기 | 미기재 |
| D 열차 T30d (갱신) | kd/d-train-sprites @21a3b158 | 미기재 | (갱신 10-23) 병합 완료 (N3-4). 이전: 객차 5종(기관차·포차·기관총차·화차·잔해) 전부 완료, wip-ready | C:/works/_review_sheets_2026-10-22/33_D_trains_contact.png |
| 설정 패널 (갱신) | kd/settings-panel-fit @d5aa4ac9 | 미기재 | 상태 미기재 | 27_settings_1024x640.png |
| B2 기병·마포·tachanka (갱신 10-23) | 미기재 (워크트리 kd-b2-units) | 미기재 | cavalry-bandit·cavalry-mongol 렌더 정상 (미커밋). heavy-horse-artillery 관통 75건. heavy-tachanka 렌더 실패 → 후속 에이전트 진행 중 | - |
| 다리 재작업 (갱신 10-23) | kd/bridge-rework | 미기재 | 병합 완료·승인 (사용자) | 다리 시트 54번 |
| 물거품 수정 (갱신 10-23) | 미기재 | 미기재 | 병합 완료 (N3b-3) | - |
| 목표 HUD·UI 낮은 등급 수정 (갱신 10-23) | 미기재 | 미기재 | 병합 상태. HUD 는 N3c 대기 | 목표 HUD 시트 (이상 없음, 사용자) |
| 시안 정리 scout | (읽기) | haiku | 완료, 아래 표 | - |

## 대기 목록
| 항목 | 선행 | 샤드 계획 |
|---|---|---|
| (갱신 10-23) gt-prone-fix 26 보병 엎드려쏴 재렌더 (+EDGE_MARGIN=2 전방향, import --only, 시트): 1/4 (kd/prone-rerender-1, 7유닛) · 2/4 (kd/prone-rerender-2, 7유닛) · 3/4 (kd/prone-rerender-3, 7유닛; scout 는 frame_wide 없음 → 0.3 유닛 재렌더 때) 재렌더 완료 · 4/4 진행 중. 풀해상도 여백 2칸 통과, 작은 LOD(@96/@48) 는 여백 1칸 기준 승인 (사용자 2026-10-23) | 지휘자의 병합 신호(kd/pioneer-prone -> merge/0.2-final) | 26유닛 / 4샤드(6~7유닛씩, 유닛당 ~900s 가정 -> 샤드당 ~100분) sonnet x4 ; T39j 결과: 대상 19유닛은 E방향 여백 부족 확인됨 |
| 이전: PR2 소품 변형 22종(풀숲 grass_tuft·들꽃 wildflower 추가) | PR3 문서(kd/pr3-prop-variants, 키 규칙 props/<종류>/var_01..07) + 레인 여유 | 샤드 P-A: bush,sorghum,stump,log_pile,log_pile_scattered,rock_outcrop,dirt_mound,grass_tuft,wildflower ; P-B: dead_tree,dead_tree_fallen,willow,willow_fallen,reed,snow_mound ; P-C: rock_s/m/l(+cracked), tree_birch/larch/pine/oak(+fallen) 변형. 모두 sonnet, 에이전트당 렌더 1개. grass_tuft/wildflower 는 props.json 키 추가 소카드 필요시 보고 |
| PR2 진행 (갱신 10-23) | P-A(9종)·P-B·P-C 병합 완료. 사용자 시트 이상 없음. 이전: kd/props-keys(props 키 추가) 진행 -> P-A 는 그 뒤 | 이전: P-B 진행(kd/props-pb), P-C 대기 |
| 화풍 mid 재렌더 (0.3, 갱신 10-23) | 0.3 승격 뒤 | 그대로: 건물·지형·소품·데칼 먼저, 유닛 후반 (사용자) |
| 연대장 초상화 (갱신 10-23) | 지금 만들지 않음 (사용자 결정) | 나중에 AI 로 가볍게 |
| 마적+봉천 혼합 팀색/표식 | B1(마적·봉천 유닛) 완료 | 소규모 sonnet 1 |
| VIP 포트레이트 자리표시 | 후순위, gt-face-proto 재사용 | sonnet 1 |
| T39e 게임 연결(MarchMuzzle 로더) | 사용자 결정 | feature 카드 |
| T56b~e | T01+T55s+T56a 병합 선행 (이전 기록 그대로) | - |

## C1E1 s1~s7 미션이 쓰는 deferred 유닛 (kd/c1e1-s* 미션 JSON 실측)
1. 마적 보병/잡병/기마/두목: bt1(thug3·rifle3·horse2), sk1(5·5·5·leader1), sk3(6·6·6·leader2) -> infantry-bandit, infantry-thug-bandit, cavalry-bandit, officer-bandit = **최우선 (B1/B2)**
2. infantry_scout: op1_assault/op1_siege 각 27, sk2 scouts -> **2순위 (B1)**
3. 미사용(s1~s7): dadao-fengtian, gando, cavalry-mongol, heavy-horse-artillery, heavy-tachanka -> 후순위 (B2, gando 는 T39g)

## 시안 정리 (scout, 2026-10-22)
| 시안 | 분류 |
|---|---|
| quickstep 속보 걷기 | 대기 (시트만, 브랜치 근거 없음) |
| 쇼샤·장갑차 + austin_fix3 | 불명 (장갑차 재아트는 main 반영, austin_fix3·쇼샤 파일 못 찾음) |
| 몽골 기병 모자·허리띠 | 반영(main lookfix), 사용자 검수 대기 U03 |
| 엎드려쏴 배치 | 대기 (kd/pioneer-prone 미병합) |
| 행진 사격 화염 | 대기 (T39e 위) |
| missing_theme_units / surrender_pose | 불명, 폐기 후보 |
| new_units | 반영, 사후 검수 |
검수 필요 시트: gt-paper-ui-v2/.../quickstep_walk.png, gt-fonts-ofl/.../{surrender_pose, missing_theme_units, mongol_bandit_look, new_units_n68_n71}.png, 위 표의 T39/T55 시트들.

## 완료 예상 / 막힌 것
- 오늘 안 완료 가능: T39g, C1E1 신규 유닛(렌더 소수), PR2 샤드 1~2개 (레인 여유 시). 어려움: 26보병 재렌더(병합 신호 대기, ~100분), PR2 전체(22종 x 6~8, 텍스처 ~21MB), 혼합 비주얼·VIP.
- 막힘: 렌더 에이전트 일부가 게이트 대기 중 턴을 끝냄(재개 지시로 처리). 구 대기 드라이버 5개가 게이트 슬롯을 점유 가능.
- (갱신) 21:15 PC 비정상 종료·21:24 재부팅으로 렌더 에이전트 다수 사망. 21:31 부터 구 드라이버 PID 22168 재가동 (T11 (갱신) 줄 참조).
- 병렬화 제안: PR2 변형을 종류당 6 으로 줄이면 렌더량 25% 감소; PR2 샤드 3개를 위 순서로; 26 보병은 4샤드; 렌더 RAM 여유가 6GB 미만(현재 4.9)이라 7번째 레인 불가.
- (갱신 10-23) 오늘 병합 완료: B1 마적 5종(N3), 간도 복식 T39g(N3-17), PR2 P-A·P-B·P-C, 다리 재작업(kd/bridge-rework, 승인), 물거품 수정(N3b-3), 열차 D(N3-4). 목표 HUD·UI 는 병합 상태 (HUD 는 N3c 대기). 엎드려쏴 재렌더 4/4 진행 중, B2 heavy-tachanka 재렌더 후속 에이전트 진행 중.
