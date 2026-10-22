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
| 06:5x | T25 리뷰 완료(65k, 58초): 차단 0. 골든 변경은 플래그만으로 설명됨, 시험 3개는 약화 아님(오히려 강화 1), marchChargeRange 5 유지 확인(P1 재확인). 주의: tactics-obs 시드 8 측정 미실시(병합 전 measure 필요), golden 은 마지막 병합 뒤 재생성 필요, 카드 문구 '현재 true' 갱신 필요 | 리뷰 5/5 완료(결함 포착 4/5) |
| 06:5x | T19a 리뷰 완료(67k, 65초): 차단 0, 결함 6 — 새 버전/깨진 슬롯 덮어쓰기 가능(요구 위반 소지) · 정산 저장 실패해도 결과 화면 이탈 가능 · 실패 창 클릭 중복 · .bak 덮어쓰기 · 포기 후 Pending 잔존 · fsync 없음(허용). 수정 에이전트 발주(1~4, wip 위 1커밋) | 리뷰 6/6 에서 결함 포착(5건은 실제 수정 필요) |
| 07:0x(실측 06:5x~) | T10 리뷰 완료(79k, 76초): 차단 0. **D15 A 의 난이도 플래그가 sim 에 소비처가 없음 발견**(골든 불변이 검증이 아니었음) · 대포병 탐지 사격 형태 무구분. 교훈: 설정 JSON 플래그 변경 시 소비처 grep 필수 | 질문 Q1·Q2 |
| 07:xx | T05 wip-ready (opus, 101k, 1549초): ai3b2 f74a675 (orderq2 위, FV55). 패치 --reject + 3 hunk 수동. manReplace 기본 꺼짐, s3a·ai3b PASS, GOLDEN 일치. 교훈: wip_patches 는 --directory=korean-division 필수. 스택 breach2→orderq2→ai3b2 완성 → T12 통합 측정 발주(measure, sonnet, 스택 끝 ai3b2 기준) | 완료 20/30 |
| 07:xx | T09+T29b 리뷰 완료(106k, 134초): 차단 0, 비차단 8 — 경제 JSON 범위 검증 없음(0 나눗셈·음수 가능) · 스폰 경로 float(ScaleAmmo) · 지도 탭에서 회령·두만강 재도전 불가 · MissionDef.Load 가 매번 디스크 읽음 · 곡사포 브리핑 불일치. 수정 에이전트 발주(1·2·4). 교훈: Defs.Load 는 범위 검증(common.md 한 줄) | 리뷰 7/7 결함 포착 |
| 실측 ~07:1x | T19a 리뷰 수정 완료(sonnet, 71k, 152초): t19a-save-slots 43e69b1 — 깨진/새 버전 슬롯 저장 거부, 결과 화면 이탈 2단계 확인, 실패 창 클릭 무시, .bak.N 보존. 시험 추가, savecompat·missions PASS, GOLDEN 일치. (2)(3) 은 GUI 미확인 | T19b 는 e511f75 기반 → 43e69b1 체리픽 필요 |
| 실측 ~06:5x | T29b 리뷰 수정 완료(sonnet, 63k, 135초): t29b-on-t09 a89b4d2 — 경제 JSON 범위 보정·예외 폴백·시험 추가, 탄약 정수 천분율(파일 형식 유지), (3) 건드리지 않음, (4) 조건부 로드는 WantsPending 때문에 불가로 보류. 검증 전부 PASS | T19b 재정리 대기(t19a 43e69b1 + t29b a89b4d2 체리픽) |
| 실측 ~07:0x | T19b wip-ready (opus, 211k — 한도 근접, 1067초, 87회): t19b-ammo-shells 622db02. 캠페인 저장 v3, 구경별 탄약·포탄 규칙(Q6), 발사 수는 측정 통계(FV 불변), 값은 campaign_economy.json. CampaignAmmoSelfTest 10, missions·savecompat PASS, GOLDEN 일치. 전체 selftest 5실패 중 4 = n12/forest 선재(fix/n12-forest 로 해소). 발주: T19b2 동기화(t19a 43e69b1 + t29b a89b4d2 체리픽) | 완료 21/31 |
| 실측 ~07:xx | T19b2 동기화 완료(sonnet, 67k, 400초): t19b2-synced 4980e7b (t19a 43e69b1 + t29b a89b4d2 체리픽, 코드 충돌 0; T19b 신규 JSON 키 보정 추가, 시험 +2). 전부 PASS. 발주: T19c 홈 3화면(opus, t19b2 위) | 체인 gemini-import→t09→t19a→t19b→t19b2→t19c |
| 실측 ~07:1x | T12 통합 측정 완료(measure sonnet, 79k, 729초): 스택(ai3b2)과 main 24판 완전 동일 → 회귀 0, 그러나 델타 0은 새 기능이 A/B/C 시나리오에서 켜지지 않았기 때문(의심). 기존 BASELINE(시드 1-5)은 낡음: B 3/5→8/8 은 스택이 아니라 main 변화. 기능별 시나리오(돌파·명령 큐·manReplace) 측정은 별도 필요. T06·T25 는 스택 밖이라 병합 후 재측정 | 완료 22/32 · 후속: T15 + 기능별 측정 |
| 실측 ~07:1x | T24 wip-ready (opus, 194k — 한도 근접, 2545초=42분, 105회): kd/t24-s1s3 12f62cf (orderq2 위). S1·S2·S3 값 후보 3개 구현, 관측반 첫 시도 무효→재시도(정직 보고). **② 목표(적 15칸 안 사망 0) 미달** · ③ 효과 미미. golden 갱신, 20묶음 PASS. 정리 못 한 것: bin/t24v2·v3 삭제 권한 거부(비추적). R3 리뷰 미실시 → 리뷰 발주 | 완료 23/33 |
| 실측 ~07:1x | T24 R3 리뷰 완료(68k, 64초): 차단 0, 병합 전 처리 2 — orderq2 0377f8a 로 rebase + golden 재확인 · ai3b2(T05)와 balance/ai_profiles/golden 겹침(병합 후 golden 재생성). 불확실: 대리 후보에 중화기·포·기병 섞일 수 있음 · 명령 지연 등급 불일치. 수정 에이전트 발주(sonnet) | 리뷰 8/8 결함 포착 |
| 실측 ~07:2x | T24 리뷰 수정 완료(sonnet, 63k, 148초): kd/t24-s1s3 e4fd65b — 대리 후보 보병·비중화기·비정찰로 제한(+시험 2), orderq2 0377f8a 위 rebase 충돌 0·골든 불변(ccfbd1dd…), 지연 등급 불일치는 성능·결정성 위험으로 문서화(U4). 8변형 동일, 묶음 PASS | 리뷰 수정 사이클 5회째 완주 |
| 실측 ~07:2x | T19c wip-ready (opus, 160k, 681초): t19c-home-tabs 5217516 (t19b2 위). 3화면 전환 띠 + MVP 5탭 기존 화면 재사용 + 10탭 빈 틀, 연대 배속 0~9 저장(v3 유지), 병참 미리보기(작전 개시와 같은 함수). 새 UI 는 새 파일 3개로 분리(CampaignMenu 증식 방지). savecompat 9/9, 퍼즈 5회 0예외, GOLDEN 일치. 교훈: 퍼즈는 작은 버튼을 못 누름 → 새 버튼은 selftest 로. 발주: T19d(sonnet, t19c 위) · T19b/T19b2/T19c 통합 리뷰(sonnet) | 완료 24/34 |
| 실측 ~07:3x | T19b/T19b2/T19c 통합 리뷰(sonnet, 111k, 134초): 차단 0. 낮은 위험 5 — 반올림 방식 변경(W1) · Q6 문구 그대로 50% · 미출격 곡사포 포탄 집계 불명 · 테스트 공백 2 · 매 프레임 LINQ. 교훈: 스택 리뷰 전 git log 로 커밋 경계 확인. 시험 보강 에이전트(haiku) 발주 | 리뷰 9/9 |
| 실측 ~07:3x | T19d wip-ready (sonnet, 81k, 160초): t19d-doctrines df329b1 (t19c 위). wip/doctrines 4파일 복원(충돌 0), 참모진 탭(8, 우) 읽기 전용·새 파일. doctrines·savecompat·missions PASS, GOLDEN 일치, 퍼즈 0. **T19 체인(a→b→b2→c→d) 완주** — L 카드 분할이 컨텍스트 한도 문제를 해소 | 완료 25/35 |
| 07:19(실측) | 시험 보강 완료(haiku, 85k, 132초): t19c 9260e36, 시험 +86줄(미리보기=실제 적재 비교 · KD_CAMPAIGN_VIEW 12케이스), 프로덕션 변경 0. ShellBaseLoad 가 미투입 곡사포 포탄도 셈(V5). 자동 점검: harness working 1(이 에이전트) → **T16(opus)·T11(art) 1시간 이상 무보고·무커밋**(T16 마지막 활동 06:03, 'heavy gate 대기' 메시지 후 정지) → SendMessage 로 재개 지시 | 병목 발견: 에이전트가 '게이트 대기'로 정지하면 알림이 오지 않음 |
| 07:2x | T28 리뷰 완료(sonnet, 60k, 46초): 차단 0. 옵션 꺼짐 = 비트 동일(RNG 호출 전부 if(clump) 안), 켠 상태 결정성 OK, Blob 이 물·도로·건물 미침범. 사소: summary 주석 순서, float 사용(지도 생성 단계). I2: 옵션 ON 전환 시 호환성 계획 필요 | 리뷰 10/10 · 이번 건은 결함 거의 없음 |
| 07:2x | UI 스택(T08·T17·T27) 리뷰 완료(sonnet, 85k, 106초): 3개 모두 차단 0. 지적: T17 자동 저장 테스트 없음(중간) · '큰 교전 직전'이 실제론 교전 시작 후(낮음) · MoveStats summary 위치 · T08 Paper.Unload 조건 · T27 작은 해상도 설정 패널 미확인. 병합 순서 제안 T08→T17→T27(충돌은 ENGINE_NOTES 하나). T17 시험 보강 발주 | 리뷰 11/11 |
| 07:2x | T17 보강 완료(sonnet, 57k, 108초): t17 0eb92e8 — 자동 저장 판정을 SaveManager.BattleSaveDue 로 추출(동작 불변)+시험, LossCause 합계 단언, summary 정정. command PASS, GOLDEN 불변 | 리뷰→수정 사이클 6회째 |
| 07:24 | T11 부분 완료(art sonnet, 84k, 5535초=92분): kd-rerender 21cf8f9, 4/70 유닛, 관통 0건, 출력 경로 버그 수정. 드라이버 2개 백그라운드 지속(70개 ≈ 26시간) — **PC 부하 장기화 위험**(heavy 동시 한도 ≠ 백그라운드 프로세스 제어 불가). Y1 결정. 교훈: 대량 렌더는 에이전트 생존 시간과 분리된 별도 잡으로 설계 | 완료 26/36(T11 은 partial) |
| 07:49(실측) | 자동 점검 3회: working 0. **T16 은 6ad6c50 에서 변화 없음(1시간 50분 무진행) — 이전 에이전트 SendMessage 로도 재개 안 됨 → 새 에이전트(sonnet)로 재발주**(교훈 반영: 재개 대신 새 에이전트, 오래 멈춘 에이전트는 포기). 병렬 발주: T06 리뷰(sonnet) · T19d 리뷰(haiku) | 멈춘 에이전트 복구 실패 1건 |
| 07:5x | T19d 리뷰 완료(haiku, 65k, 56초): 차단 0. 지적 — 참모진 탭이 MVP 를 5→6탭으로 확대(주석 stale) · JSON 의 research/figures 가 null 이면 홈 화면 크래시 가능(그리기 경로 try 없음) · 누락 파일 처리는 정상 · 수치 미표시 확인 · 시험 공백. 수정 에이전트 발주(sonnet). 교훈: 자료 JSON 리스트 필드는 Validate 에서 null 검사 | 리뷰 12/12 · haiku 리뷰도 유효 |
| 07:5x | T06 리뷰 완료(sonnet, 62k, 58초): 코드는 정상이나 **수용 기준 미달이라 wip-ready 로 부적합** — ×0.7 은 순수 회귀(평지 175→265초, meeting 시간 초과 1→3/8), bnRetreatCasMul 은 기본 1 이면 무해. 권고: ×0.7 폐기, 키만 기본 1 로 보존 또는 브랜치 폐기(J1 A). main QUEUE T06 행을 '수용 기준 미달 (J1 대기)'로 정정. 병합 충돌: detcheck.golden 4개 브랜치 공통 | 완료 건수 정정 26→25 (T06 제외) |
| 07:5x | T19d 리뷰 수정 완료(sonnet, 59k, 119초): t19d 04a9149 — Validate 가 null 7종을 친절한 메시지로 거부, 그리기 경로 `?? new()` + 학파 0개 가드(Clamp(0,-1) 크래시 방지), 시험 케이스 추가, 주석·카드 정정. 전부 PASS, GOLDEN 일치 | 리뷰→수정 7회째 |
| 07:5x | T16 wip-ready (새 에이전트 sonnet, 66k, 565초, 23회): kd/t16-trench 8b38b29. **'heavy gate' 정체 규명 = tools/agent/heavy_gate.ps1(풀 8 초과 시 대기), 첫 에이전트 정지 원인은 T11 렌더 드라이버의 풀 점유로 추정** — 새 에이전트는 게이트 호출 없이 동시 2판으로 진행. 결과: 20판 측정 6변종 모두 10/20 미달, 'N32 끔 7/10' 이 breach 포함 브랜치에서 재현 안 됨 → 원인 재조사 필요(Z1). 미션 json 변경은 개선 효과 없음 | 완료 26/37 |

## 점검 (완료 보고마다 갱신)
| 지표 | v1 이전 | 지금 | 비고 |
|---|---|---|---|
| 중복 조사 | 250k 토큰 | scout 1회 66k | 겹침 조사를 한 번에 |
| 병합 잠금 대기 | 2h+ | 해당 없음(병합 금지 정책) | merger 미사용 |
| 지휘자 토큰 | 360k | 측정 전 | |
| wip-ready 형식 준수 | - | 8/8 준수(T22·T23·T21·T01·T03·T07·T08·T28: 카드·QUEUE·커밋·시트·알려진 흠) | 첫 보고 도착 시 |
| scout(haiku) charter 준수 | - | 준수(표 형식 · 근거 · 열린 질문 포함) | 1건 |
| 완료된 카드 / 발주 | 10/13 병합 | 26/37 (T16 포함; T06 제외; T11 partial 포함; T19d 포함; T19c 포함; T24 포함; T12 포함; T19b 포함; T05 포함; T25 포함; T29b 포함; T19a 포함; T10 포함; T04 포함; T29 포함; T27 포함; T09 포함; T18 종결 · T22 · T23 · T21 · T01 · T03 · T07 · T08 · T28 · T06 wip-ready(T06 은 수용 기준 미달)) | |
| 오류·재작업 | - | 2건(T26 역할 오배정 44k) · 1건(T18 중복 발주, 낭비 56k) → 규칙 추가: 발주 전 QUEUE 행을 MERGE_QUEUE 완료표와 대조 | |
| 08:50 | 자동 점검: working 0, 발주 가능 카드 없음(T15 는 렌더 풀 점유로 보류). ops/STATE.md 에 새 지휘자 세션용 인계 절 작성 | 인계 완료 |
| 09:19 | 09:00 경과 → 자율 가동 종료, 크론 해제. 에이전트 0, T11 렌더 드라이버만 가동 | 종료 |
| 11:30(실측) | 사용자 지시: 13:00 까지 못한 작업 속행 → 새 크론(매시 23·53분) 설정. 발주: T15 + 기능별 측정(measure sonnet, ai3b2 스택 끝, heavy 풀은 T11 렌더가 2 점유 — 10분 이상 대기 시 부분 수치로 마무리 지시) | 자율 가동 2차 시작 |
| 11:3x | **원래 T16 에이전트(opus)가 5.5시간(19,888초) 뒤 완료 보고** — 멈춘 게 아니라 heavy_gate 대기 중이었음. 같은 worktree·브랜치에서 측정해 새 에이전트의 결과와 동일한 수치 확인(독립 재현 6변종 일치), 커밋은 새 에이전트 것(8b38b29)+2cb118c. **재발주는 중복 작업이었음**(약 66k 토큰 낭비) 반면 결과 교차검증 효과. 교훈: 에이전트 정지 판정 전 heavy_gate 대기 여부 확인(작업 트리 무변화만으로 판단 금지), 같은 카드의 이중 발주 방지 | 정지 판정 규칙 수정 |
| 11:4x | 사용자 지시 '에이전트가 놀고 있다, 로드맵 확인해 알아서 채워라' → 권장 기본값(D22)으로 채움: T06 정리(J1 A, sonnet) · Z1 원인 재조사 T16b(opus, bisect main↔breach2) · T10b 난이도 플래그 소비처(opus, 리플레이 안전성 확인 후) · 장갑열차 카드 분할(spec, rail 위) · 구현 여부 확인 scout(공병·경박격포·연막·하천·간도 의용대·가옥 폐허) + 진행 중 T15 | 동시 6, 上 2 |
| 11:4x | 구현 확인 scout 완료(haiku, 77k, 60초): 공병·경박격포·연막 = breach2 에만 구현, 하천 = 코드 없음(설계 필요), 간도 복식·가옥 폐허 = 부분. 완전 미구현+결정 불필요 = 없음 → 하천 설계 제안 문서(spec) 발주 | 동시 6 |
| 11:5x | 장갑열차 카드 분할 완료(spec, 73k, 139초): T30a~e 5장 + TRAIN_PLAN, 기반 rail(t28 제외 이유 명시). 발주: T30a(opus, rail 위, FV58/R23 예약, 권장 답 채택)·T30d(art sonnet, 그림 규격·블록아웃). FV_RESERVATIONS 갱신 | 동시 7, 上 3 |
| 11:4x | 하천 설계 제안서 완료(spec, 88k, 99초): spec/river-design 26e0b88, Ford 1개 추가안·카드 R1~R4·질문 8. 사용자 요구가 명확하지 않아 구현은 보류(메커닉 임의 추가 금지 규칙) | AB1~8 |
| 11:4x | T06 정리 완료(sonnet, 71k, 996초): wip/casualty2 0722493 — accFar·시험·골든 복원, bnRetreatCasMul 기본 1.0 (float 1ulp 차이 방지 위해 mul==1 이면 옛 식 그대로), detcheck = main 골든과 동일. 카드 '목표 미달+후속 필요' 정직 기록. 정리 못 한 것: bin 산출물(rm 거부). 후속 카드 spec 발주(재투입 제한·종료 조건·AI 항복) | T06 은 이제 병합 무해(inert) |
| 11:5x | 후속 카드 T31a·b·c 완료(spec, 84k, 135초): 재정비는 이미 regroup 키로 구현, 70% 종료는 엔진 규칙이 아니라 측정 시나리오 설정, 항복은 구현돼 있으나 조건이 엄해 대대전 0건 → 카드 3장(키 기본 꺼짐). 발주: T31c(sonnet, main 위)·T31a(opus, ai3b2 위). T31b(측정 전용)는 heavy 풀 여유 시 | 동시 8, 上 4 |
| 11:5x | T30d wip-ready (art sonnet, 139k, 1004초): t30d-train-art 8e51e5a, 블록아웃 5종×5방향×평상/파괴, ART_SPEC 열차 절, outroute train-*. 크기 값은 카드에 없어 임의 설정(T30a 확정 후 대조 필요) | 완료 27/42 |
| 11:56(실측) | 자동 점검(2차 1회): 진행 중 T15·T16b·T10b·T30a·T31a·T31c(워크트리 생성 완료, harness working 1·나머지 idle = 시험/측정 대기 추정; 발주 후 10~30분이라 정지 판정 안 함). 신규 발주 없음(T31b 는 heavy 풀 여유 대기) | 대기 |
| 12:0x | T30a wip-ready (opus, 203k — 한도 근접, 1648초, 70회): t30a-train-move 1800d5a (rail 위). 새 파일 Train.cs, TrainCar 는 Units 밖 World.Trains, 정수 호장, 스크립트 이동, FV58/R23, train 26검사·replay·snapshot·savecompat PASS, 열차 없는 지도 골든 불변, 틱 비용 잡음 수준. 발주: T30b(opus, t30a 위, FV59)·T30a 리뷰(sonnet) | 완료 28/43 |
| 12:0x | T30a 리뷰 완료(sonnet, 87k, 91초): 차단 0, train 묶음 PASS 확인. 지적 — PosAt/TrackBlocked 가 Vector2·Normalize 로 Blocked(해시 포함)를 정함(플랫폼 위험) · 방향 전환 뒤 Blocked 가 최대 6틱 잔존 · cars:null/NaN waits 등 입력 방어 구멍·시험 부재 · 해시 누락 필드. 수정 에이전트 발주(sonnet, t30a 위 1커밋; T30b 가 체리픽) | 리뷰 13/13 결함 포착 |
| 12:1x | T10b wip-ready (opus, 117k, 1957초, 53회): kd/t10b-difficulty 4ac07b4. 난이도가 스냅샷·해시·리플레이 머리말에 이미 포함됨을 확인 → commsRules(기본 꺼짐)로 전령 사로잡힘·유선 끊김 구현, 새 상태 없음(FV57/R22 유지), comms 18/18, 골든 불변. AI 배수는 설계 두 안(A 권장). 발견: gapfill 예외는 T10 기반에도 있음(fix/n12-forest 로 해소 확인). 에이전트가 main 폴더 ops/QUEUE.md 를 직접 수정(커밋 안 함) — 경계 위반 소지. 발주: T10b 리뷰 | 완료 29/44 |
| 12:1x | T10b 리뷰 완료(sonnet, 65k, 64초): 차단 0. 난이도의 스냅샷·체크섬·리플레이 머리말 포함을 독립 검증. 발견 — **Faction.IsAi 가 자유전 리플레이에 복원 안 됨(기존 결함, AF1)**: T31c 항복 확대·AI 배수 안 A 의 선행 조건 → 진행 중 T31c 에 SendMessage 로 IsAi 비의존 지시. difficulty.json 검증 부재 → 수정 에이전트(haiku) 발주. FV_RESERVATIONS 에 후보 번호 기재 | 리뷰 14/14 |
| 12:1x | T10b 검증 보강 완료(haiku, 69k, 104초): 270172f — 깨진 JSON → 기본값+경고, commsRules 범위 clamp(반경 1~50·확률 0~1·checkSec≥1·NaN→기본), 시험 2, 유효 파일 경고 0, GOLDEN 일치 | 리뷰→수정 8회째 |
| 12:1x | T30a 리뷰 수정 완료(sonnet, 82k, 237초): t30a 863f9d1 — 정수 PosFix(Blocked 에서 float 제거), 정지 중 Blocked 재계산, 입력 방어 17시험, 해시 필드 추가(FV58 유지), 골든 불변. 진행 중 T30b 에 SendMessage 로 863f9d1 체리픽 지시 | 리뷰→수정 9회째 |
| 12:2x | T31c wip-ready (sonnet, 100k, 1773초): kd/t31c-surrender e9cd527. 키 6개(AI 한정, 기본 끔)·시험 TestRelaxed, GOLDEN 불변, SendMessage 지시(IsAi 비의존/선행 조건 명시) 반영. 켠 값은 수용 기준 미달: 항복을 늘리면 승/종료가 나빠짐(항복자가 생존 집계) → 종료 조건(T31b)과 함께 설계 필요 | 완료 30/45 |
| 12:3x | T30b wip-ready (opus, 237k — 한도 근접, 1131초, 101회): t30b-train-weapons ca1cdb8, FV59. 지휘자가 보낸 863f9d1 을 a2e0f97 로 체리픽 확인. 저장 위치 이동(ORG_ 끝→ENTS 끝)·엔티티 참조 태그 3(표적 지정 상태 저장 파손 방지). trainweapons 19검사 PASS. **밸런스: 열차 8승 0패(보병 전멸)** → AH1. 발주: T30c(opus, t30b 위)·T30b 리뷰(sonnet) | 완료 31/47 |

## 파이프라인 v1 중간 점검 요약 (2026-10-22 08:19 실측, 취침 중 자율 가동 ~2시간 반)
| 지표 | v1 이전 | 지금 | 판단 |
|---|---|---|---|
| 병합 잠금 대기 | 2h+ | 0 (병합 금지 정책) | 해당 없음 — 병합 단계는 아직 미검증 |
| 중복 조사/중복 발주 | 250k 토큰 | T18 중복 1건(56k) + T26 오배정 1건(44k) | 여전히 지휘자 실수; 발주 전 QUEUE↔MERGE_QUEUE 대조로 이후 0건 |
| wip-ready 보고 형식 준수 | - | 26/26 | 양호 |
| 리뷰 단계 | 없음 | 12건 중 12건에서 실제 문제 포착(차단급 blocker 1·수용기준 미달 2 포함) | **가장 가치 큰 변경** |
| 스택(브랜치 체인) | - | 최장 6단(gemini-import→t09→t19a→t19b→t19b2→t19c→t19d) | 가능하나 재스택 비용 3회 발생 → FV_RESERVATIONS·체리픽 규칙 필요 |
| 큰 카드 컨텍스트 | 한도 초과 위험 | opus 카드 6건이 190~220k 근접 | L 카드는 분할(T19 성공) |
| 에이전트 정지 | - | 2건(T16 첫 에이전트·T11) — heavy_gate 대기 후 무알림 정지 | v2: heavy 잡은 에이전트 수명과 분리, 정지 감지 점검(작업 트리 무변화 30분) |
| 지휘자 토큰 | 360k | (이 세션 컨텍스트 누적 큼) | 파도 단위 교체 권장 — 09:00 에 STATE 인계 후 /clear |
| 완료(wip-ready) / 발주 | 병합 10/13 | 26 / 37 (T06·T11 은 단서) | 처리량 우수, 품질은 리뷰가 지킴 |

v2 후보(사용자 확인 후 PIPELINE.md 반영): ① 리뷰 단계 표준화 ② FV 예약표 정식화 ③ 정지 감지 ④ L 카드 사전 분할 ⑤ 세부 설계 없는 카드는 '기본 꺼짐 스위치+초안 수치'로 ⑥ 병합 단계(merger)는 사용자 허가 후 첫 가동에서 검증.
