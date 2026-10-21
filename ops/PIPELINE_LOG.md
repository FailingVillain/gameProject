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

## 점검 (완료 보고마다 갱신)
| 지표 | v1 이전 | 지금 | 비고 |
|---|---|---|---|
| 중복 조사 | 250k 토큰 | scout 1회 66k | 겹침 조사를 한 번에 |
| 병합 잠금 대기 | 2h+ | 해당 없음(병합 금지 정책) | merger 미사용 |
| 지휘자 토큰 | 360k | 측정 전 | |
| wip-ready 형식 준수 | - | 7/7 준수(T22·T23·T21·T01·T03·T07·T08: 카드·QUEUE·커밋·시트·알려진 흠) | 첫 보고 도착 시 |
| scout(haiku) charter 준수 | - | 준수(표 형식 · 근거 · 열린 질문 포함) | 1건 |
| 완료된 카드 / 발주 | 10/13 병합 | 8/19 (T18 종결 · T22 · T23 · T21 · T01 · T03 · T07 · T08 wip-ready) | |
| 오류·재작업 | - | 2건(T26 역할 오배정 44k) · 1건(T18 중복 발주, 낭비 56k) → 규칙 추가: 발주 전 QUEUE 행을 MERGE_QUEUE 완료표와 대조 | |
