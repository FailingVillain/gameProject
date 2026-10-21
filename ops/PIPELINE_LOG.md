# PIPELINE_LOG - 파이프라인 가동 기록 (효율 점검용, 최신이 아래)

> 목적: 새 파이프라인(v1)이 정상·효율적으로 도는지 숫자로 남긴다. 쓰는 사람: 지휘자. 항목 = 시각 · 사건 · 숫자. 판단은 맨 아래 '점검' 표.
> 기준선(v1 이전): 에이전트 45 · 병합 10/13 · 잠금 대기 2h+ · 지휘자 360k 토큰 · 중복 조사 250k 토큰 (PIPELINE.md 버전표).

## 2026-10-22 파도 1 (정책: WIP 병합 안 함, wip-ready 까지)
| 시각(대략) | 사건 | 숫자/메모 |
|---|---|---|
| 세션 시작 | 재개: conductor-resume 메모 → release 발주(main selftest) | main 562e578→eb73637 |
| +10분 | 정리: 병합 끝난 워크트리 2·브랜치 3 삭제, QUEUE T01/T03/T07 assigned | main 23dba85 |
| +10분 | 1차 발주 4: T03 sim(上) · T07 sim(上) · T01 feature(中) · scout(下 로드맵×WIP 겹침) | 동시 5(release 포함), 上 2 |
| +15분 | scout 완료: 66k 토큰·58초. 결과 = 겹치지 않아 지금 시작 가능한 로드맵 항목 0건 | 겹침 조사 1회로 종결(중복 조사 없음) |
| +20분 | 사용자 정책: WIP 병합 금지. LIMITS 에이전트 20 · 上 5. QUEUE T21~T23 추가, 실행 순서 기록 | main d2c05d5 |
| +20분 | 2차 발주 5(sonnet): T18 · T11 · T21 · T22 · T23 | 동시 9, 上 2, 무거운 작업 에이전트당 ≤2 |
| +25분 | 남은 WIP(T04 T05 T08 T09 T17 T19)는 선행 wip-ready 뒤 스택 발주. T06 T10 은 결정 대기 | 선행 대기 6 · 결정 대기 3 |
| +30분 | T18 완료 보고: 이미 main 에 있어 아무것도 안 만들고 정지(56k 토큰, 44초). **QUEUE 행이 낡아 있었음** — 발주 전 main 구현 여부 확인(scout) 누락 | 낭비 56k · 원인 = QUEUE T18 상태 미갱신 |
| +40분 | T22 face-proto wip-ready (sonnet, 59k 토큰, 92초): 브랜치 gt-face-proto bc52007, 카드·QUEUE 행을 자기 브랜치 안에서 갱신(규칙 준수), 시트 제출. 흠: 얼굴 51x28px 로 작음 | 병합 안 함 · 사용자 시트 검토 대기 |
| +45분 | T23 wip-ready (sonnet, 90k 토큰, 101초): spec/t23-campaign-home 3cde1f3, 설계 v0.2→v0.3(탭 15개·규칙 대응·WIP 정합·구현 순서), 사용자 질문 Q1~Q10 | 병합 안 함 · 사용자 답 대기 |
| +60분 | 사용자 결정 D01~D20 확정 → T06 발주(opus), T24/T25/T10 스택 대기. T26(훅)을 docs(haiku)에 잘못 배정 → 헌장(코드 금지)대로 13초 만에 거부·정지, feature(sonnet)로 재발주 | 낭비 44k · 원인 = 역할 선택 오류(지휘자). haiku 가 헌장을 지킨 것은 긍정 신호 |
| +65분 | T26 재발주(feature): 훅 파일 작성+임시 클론 시험 Bash 가 권한 시스템에서 거부 → 우회 없이 정지(46k). 사용자 권한 허용 필요 | 대기 = 사용자 |
| +75분 | T21 sfxnew wip-ready (sonnet, 61k, 264초): kd-develop/kd-sfxnew aaba414, 빌드·cleanup·ai PASS, detcheck GOLDEN match. 패치 3-way 충돌 1건(DECISIONS_PENDING) 수동 처리 | 병합 안 함 · 사용자 결정(함성 A/B/C) · 청취 시험 대기 |
| +85분 | T01 gemini-import wip-ready (sonnet, 99k, 407초, 도구 41회): kd-gemini-import 06424c8. 3-way 불가 → --reject 후 4파일 수동 합침. 빌드 0 오류 · GOLDEN match · snaptest · missions · savecompat PASS. 위험: CampaignMenu 즉시 저장이 읽기 실패 상태에서 덮어쓸 수 있음(배너만) | 발주: T08(sonnet)·T09(opus) 를 gemini-import 위 스택으로 (첫 스택 발주) |
| +100분 | 4건 추가 발주(reviewer×2 · spec 카드 4장 · docs 로드맵 정리). T21 리뷰 완료(51k, 54초): 시뮬 불변 확인, 차단 항목 0, 경미 3 | 리뷰 단계 가동 시작 |
| +125분 | T03 wip-ready (opus, 108k, 530초, 43회): wip/breach2 b94be2c+6ad6c50. replay 묶음 실패(명령 표본 2개 누락)를 찾아 고침 — 7faccf0 에서도 있던 결함. detcheck 8설정 일치, breach 16/16, 나머지 묶음 PASS. 교훈: 새 명령 작업은 replay 묶음 필수 | 병목 해소 → T04·T10 즉시 발주(wip/breach2 위 스택), T16·T25·T24 는 카드 도착 후 |
| 참고 | main QUEUE 행이 낡는 문제: 작업자는 자기 브랜치에서만 상태를 바꾸므로 main 에서는 보이지 않음 → 지휘자가 wip-ready 보고를 받을 때 main 행 동기화(이번에 T01·T03·T21·T22·T23 수행) | 규칙화 |
| +130분 | T01 리뷰 완료(88k, 107초): **blocker 1건 + 높음 2 + 중간 2 + 낮음 2** — 작성자 보고에 없던 문제(읽기 실패 저장 덮어쓰기). 리뷰 단계가 실제로 가치 있음 확인. 수정 에이전트 발주(1·4·6·7), 2·3 은 D23·D24 로 사용자 결정 대기. 로드맵 정리(docs) 완료(69k) — 사실 확인으로 '전부 해소' 대신 8개만 기재(정확) | 리뷰가 wip-ready 직후 결함 포착 → 순서: 리뷰 → wip-ready 가 맞음(PIPELINE 표기 유지) |
| +140분 | T07 rail wip-ready (opus, 89k, 566초): kd-rail 423e326+9f513e7, FV53 로 맞춤(breach2 와 FV53 충돌 예상 → 병합 단계 아닌 '스택' 시 재번호 필요), terrain·detcheck·snaptest PASS. spec 카드 4장 완료(75k). 발주: reviewer×2(T03·T07) · T25(sonnet, breach2 위) · T16 scout(haiku) | 병렬도 확대, 동시 약 9 |
| +150분 | 설계서 v0.4 완료(64k, 76초): 15탭 배치안 · T09/T19 범위 구분. 사용자 검토 F1~F4(무응답 시 제안대로) | 사용자 취침 중 → 무응답 기본값으로 진행 |
| +160분 | T08 wip-ready (sonnet, 98k, 186초): t08-ui-bundle 2e9aef5 (gemini-import 위 첫 스택 성공), uiicons PASS · uifuzz 30 크래시 0. T16 scout(haiku, 61k, 24초): 파라미터 위치·실행 명령·breach 무충돌 확인 → T16 sim(opus)·T17 feature(sonnet, t08 위) 발주 | 동시 약 11, 上 5 |
| +165분 | T07 리뷰 완료(65k, 71초): 차단 0. 유일한 충돌 = FormatVersion 줄(rail·breach2·orderq2·t10 모두 53 예약) → 병합은 안 하지만 '스택' 단계에서 번호 정리 필요. 권고: breach2 기준 순서 확정 후 rail 54 | 번호 충돌은 FV 줄 하나, 사전 예측 가능 → FV 예약표 필요 |
| +180분 | 로드맵 결정불필요 조사 완료(sonnet scout, 99k, 96초): 결정 불필요 항목 대부분이 이미 T카드에 있음. 카드 없음·결정 불필요: 공병/경박격포/연막(돌파 묶음, T03 안), AI 항복 개선(T06 뒤), 지휘편의 ②③⑥⑧, 지도 엄폐물 군집화, 가옥 폐허/기수 깃발/행진 화염/한국·봉천 색(T11 때), D23/D24(T01 위). 발주: 구현 여부 확인 scout(haiku) + spec 카드 T27·T28. 지적: T17이 ⑦(교전 알림/미니맵)을 포함 — LONG 은 '추후 심도 논의' | 사용자 결정 필요 15건은 보고 |
| +190분 | **release(main 전체 selftest) 결과: FAIL 4** (n12 gapfill 2 · forest 정찰 2, 단독 재현, 비플레이키). detcheck GOLDEN 일치 · snaptest 통과. 소요 15분(887초). 원인 추정 = formation 병합(그 병합은 n12·forest 묶음을 안 돌림). publish·기록 미실행. → 수정 에이전트 발주(fix/n12-forest, sonnet). T03 리뷰 완료(95k, 124초): 차단 0 · FV53 충돌(rail) · 문자열에 수치 박힘 · 주석 FV51 잔존 | **v1 이전 병합에서 놓친 회귀를 release 단계가 포착 = 단계 분리 가치 입증**. 개선: 병합 때 관련 묶음 외에 풀 selftest 주기 필요 |
| +195분 | 구현 확인 scout(haiku, 86k, 71초): 포병 묶음 대부분 구현(야전포 사거리만 80), 교전!/일본 착검 구현, 엎드려쏴는 T11 범위 밖(별도 카드 필요) → 중복 발주 방지 | 질문 H1 · H2 기록 |
| 06:05 | 사용자 취침 지시: 09:00 까지 작업 속행, 결정 불필요 항목은 에이전트가 비는 대로 큐에 추가. 크론 자동 점검(매시 17·47분) 설정 · 완료 알림이 와도 즉시 처리 | 자율 가동 시작 |
| 06:10 | spec 카드 T27·T28 완료(73k, 114초). 발견: ②③⑥ 은 t08 에 이미 구현 → 틈 메우기 카드로 축소(중복 방지). 발주: T27(t08 위)·T28(rail 위) feature×2, 열린 질문은 안전 기본값(옵션 끔·최소) | 동시 약 11 |
| 06:25 | T28 wip-ready (sonnet, 112k, 581초): t28-cover-forest c577c0f. 원인 특정(사각형=미션 지도 rect 칠하기, 흩어짐=반경3 균등 뿌림), 옵션 2개 기본 꺼짐, 고립 소품 36%→3%, detcheck GOLDEN 일치. **forest 묶음 2건 FAIL이 rail 빌드에서도 재현 → main 의 forest 실패와 같은 선재 결함 확인** | 사용자: I1 기본값 전환 여부, 시트 검수 |
| 06:35 | T01 수정 완료(sonnet, 78k, 888초): gemini-import 63080df — blocker 해소(읽기 실패 저장 차단·.bak), 탄약 정수화, null 거부, 주석. savecompat·missions PASS, GOLDEN 일치. 리뷰→수정→재검증 사이클 1회 완주. 발주: T29(D23 첫 승리만 정산 + D24 경제 JSON, 기본값 A, gemini-import 위) | 리뷰 단계가 실제 결함을 막음 |
| 06:50 | T06 wip-ready지만 **수용 기준 미달** (opus, 151k, 1284초, 74회): wip/casualty2 ef8daab. 결정 D19 C 를 그대로 적용해 측정 → 평지 느려짐·대대전 불변·항복 0. 원인 진단까지 보고(결정 문구 모호: '후퇴 기준 ×0.75' 대상 불명). 시험 23묶음 PASS, golden 갱신. 사용자 결정 J1·J2 | 교훈: 선택지에 키 이름 명시. 에이전트가 '목표 미달'을 숨기지 않고 보고한 점은 긍정 |
| 07:05 | T17 wip-ready (sonnet, 79k, 1009초): t17-cmd-convenience 622a96c+6c47982 (t08 위). 미션 중 저장·자동 저장·전과 보고 구현, ⑦ 교전알림/미니맵은 미승인이라 범위 밖(내가 지적한 불일치를 에이전트가 스스로 정정). command·savecompat·missions PASS, uifuzz 0예외, GOLDEN 일치. Game.cs +22줄(T04와 충돌 가능) | 스택 2단(gemini-import→t08→t17) 성공 |
| 07:20 | T09 wip-ready (opus, 202k 토큰 — 컨텍스트 한도 근접, 1226초, 79회): t09-camp-slice 0c82a47. 1-1 압록강 미션 신설(두만강 복사 초안), 손실→Roster 반영, 재승리 정산 방지(D10/D11). missions·savecompat 새 검사 19, GOLDEN 일치, 리플레이 verify 일치. 수동 UI 한 사이클 확인은 아직. 질문 4건. 위험: 스택 기반(gemini-import)이 이후 63080df 로 바뀜 → 재스택 필요; T29(D23 첫 승리 정산)와 정산 코드 겹침 | 교훈: 스택 발주 시 선행 카드 리뷰 blocker 상태를 프롬프트에 기재. 발주: spec T19 분할 | L 카드는 분할 없이 보내면 한도 위험 |
| 06:19(실측) | 자동 점검 1회: 활성(working) 3(fix/n12-forest · T27 · T19 분할 spec), 대기(idle) T04·T10·T25·T16·T29·T11 은 알림 미도착(긴 시험/무거운 작업 게이트 대기 추정). 발주 가능한 결정 불필요 카드 없음(전부 선행 T04·T09 분할·T11 대기). 주의: 위 표의 '06:35~07:20' 시각은 대략치이며 실제 벽시계는 06:19 — 이후 항목은 실측 시각 사용 | 신규 발주 0 |
| 06:2x | T27 wip-ready (sonnet, 118k, 859초): t27-cmd-ui2 4cf5c45 (t08 위). ②③⑥ 은 이미 구현 확인(전수 대조 시험 추가), ⑧ 색각 도형 토글 신규(기본 꺼짐). uiicons PASS, uifuzz 0, 시트 미제작(사용자 검수용 시안 필요) | 완료 13/23 |
| 06:3x | spec T19 분할 완료(78k, 95초): T19a 저장 슬롯 → T19b 탄약·포탄 → T19c 홈 3화면 → T19d 학파, 전부 스택 체인(spec/t19-split aea476b). 발주: T19a(opus, t09 위). 열린 질문 4: T29 를 t09 위로 재스택할지(정산 코드 겹침) · 후속 탭 포함 여부 · 규칙 9 키 카드 분리 | T19b 는 T29 재스택 결정 뒤 |
| 06:4x | fix/n12-forest 완료(sonnet, 98k, 1205초): 코드 결함 아님, 시험 장면 낡음(formRelax 휴식 정렬). 시험 3파일만 수정, 전 묶음 PASS, GOLDEN 일치. 사용자 판단 M1(main 병합 여부) | main selftest 정상화 대기 → release 재실행 필요 |
| 06:5x | T29 wip-ready (sonnet, 88k, 555초): t29-campaign-economy-json 783e7ce (gemini-import 63080df 위). D23·D24 완료, 경제 숫자 14개 JSON 이전(campaign_economy.json), missions·savecompat PASS, GOLDEN 일치. 발주: T29b 재스택(t09 위, 정산 로직 통합) — L1 기본값 이행 | 완료 14/24 |
| 07:0x | T04 wip-ready (opus, 111k, 1498초): wip/orderq2 79ae768 (breach2 위). patch 3way 불가 → --reject + 11곳 수동 반영. FV54/Replay21, GOLDEN 일치, orderq·firectl·replay·snapshot·command PASS, uifuzz 875동작 0오류. 교훈: 옛 패치는 처음부터 --reject. 발주: T05(opus)·T24(opus)·T04 리뷰(sonnet) | 上 5 가동, 완료 15/27 |
| 07:1x | T10 wip-ready (opus, 220k 토큰 — 한도 초과 근접, 1570초, 83회): kd/t10-comms-b 1c91c96 (breach2 위). 세부 설계 없는 카드라 '기본 끔 스위치 + 초안 수치'로 구현(fieldops.json), fieldops 16/16 + 14묶음 PASS, GOLDEN 일치. FV57/Replay22 예약(54~56 건너뜀). 신규: ops/FV_RESERVATIONS.md 예약표 작성(번호 충돌 사전 관리). 질문 N1~N4 | 개선 반영: 세부 설계 없는 카드 → 안전 기본값 규칙 |
| 07:2x | T04 리뷰 완료(72k, 97초): 차단 0. 지적 2건 — 철조망 절단이 대기열을 취소하지 않음(돌격보병 절단 중 다음 다리 실행) · 대기열 해시에 구성원 수 누락. 후속 수정 에이전트 발주(wip/orderq2 위 1커밋). T10(FV57/R22) 와 orderq2(FV54/R21) 정면 충돌 재확인 → FV_RESERVATIONS 에 기록됨 | 리뷰 단계 3/3 에서 실제 결함 포착(T01 blocker, T03 FV, T04 2건) |
| 07:3x | T19a wip-ready (opus, 153k, 1047초): t19a-save-slots e511f75 (t09 위). T09 에는 자동 저장·슬롯이 없었음을 확인하고 신규 구현: 수동 슬롯 9 + 작전 직전/정산 직후 자동 저장, 실패 시 .tmp 정리·재시도 UI. 63080df 가드 최소 이식(재스택 없이). savecompat 새 시험 5, GOLDEN 일치, uifuzz 852프레임 0예외. 재도전 복귀는 GUI 수동 확인 미실시. T19b 는 T29b 재스택 완료 후 | 완료 17/28 |
| 06:4x | T29b wip-ready (sonnet, 84k, 802초): t29b-on-t09 062dd38 (63080df 82ab015 + T29 ee810be 체리픽, 정산 충돌 통합). 재승리 = RecordRepeatVictory(D10+D23), 첫 승리 = T09 손실→Roster 유지. 이 브랜치가 T09·T29 둘을 대체(병합 시 단독). savecompat·missions PASS, GOLDEN 일치. 발주: T19b(opus, t19a 위 + t29b 체리픽) | 재스택 비용 802초·84k — 스택 간 정산 충돌 해소 성공 |
| 06:5x | orderq2 리뷰 수정 완료(sonnet, 61k, 723초): 0377f8a — CutWireCommand 취소 목록 추가, 연막은 제외(Order 불변), 대기열 해시에 구성원 수 추가, 골든 불변(19501757040f0d62), TestWireSmoke 추가. 리뷰→수정→재검증 2번째 완주. 주의: T05·T24 는 79ae768 기반 → orderq2 0377f8a 와 병합 시 단순 합류 | 완료 변동 없음(수정 커밋) |
| 06:4x | T25 wip-ready (sonnet, 76k, 2571초=43분 — 시험 반복): kd/t25-bayonet-all 29695c3. 플래그 1개 변경, 골든 정당 재생성, 시험 3 수정. **D01 문구 불일치 발견**(사용자 '3칸 유지' vs main 이미 5) → 에이전트가 값 미변경·보고(올바른 판단) → P1 질문 | 완료 19/29, 결정 문구-현실 불일치 1건 |
| 06:49(실측) | 자동 점검 2회: 리뷰 미실시 wip-ready 4건(T10·T09+T29b·T19a·T25) 리뷰어 일괄 발주(sonnet×4, 읽기 전용). 신규 구현 카드 발주 가능분 없음(남은 것: T16·T24·T05·T11·T19b 진행 중, T19c·d 는 T19b 뒤, T12/T15 는 T05 뒤, 엎드려쏴는 T11 뒤) | 리뷰 4건 병렬 |

## 점검 (완료 보고마다 갱신)
| 지표 | v1 이전 | 지금 | 비고 |
|---|---|---|---|
| 중복 조사 | 250k 토큰 | scout 1회 66k | 겹침 조사를 한 번에 |
| 병합 잠금 대기 | 2h+ | 해당 없음(병합 금지 정책) | merger 미사용 |
| 지휘자 토큰 | 360k | 측정 전 | |
| wip-ready 형식 준수 | - | 8/8 준수(T22·T23·T21·T01·T03·T07·T08·T28: 카드·QUEUE·커밋·시트·알려진 흠) | 첫 보고 도착 시 |
| scout(haiku) charter 준수 | - | 준수(표 형식 · 근거 · 열린 질문 포함) | 1건 |
| 완료된 카드 / 발주 | 10/13 병합 | 19/29 (T25 포함; T29b 포함; T19a 포함; T10 포함; T04 포함; T29 포함; T27 포함; T09 포함; T18 종결 · T22 · T23 · T21 · T01 · T03 · T07 · T08 · T28 · T06 wip-ready(T06 은 수용 기준 미달)) | |
| 오류·재작업 | - | 2건(T26 역할 오배정 44k) · 1건(T18 중복 발주, 낭비 56k) → 규칙 추가: 발주 전 QUEUE 행을 MERGE_QUEUE 완료표와 대조 | |
