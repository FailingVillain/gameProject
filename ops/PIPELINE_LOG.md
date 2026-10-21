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

## 점검 (완료 보고마다 갱신)
| 지표 | v1 이전 | 지금 | 비고 |
|---|---|---|---|
| 중복 조사 | 250k 토큰 | scout 1회 66k | 겹침 조사를 한 번에 |
| 병합 잠금 대기 | 2h+ | 해당 없음(병합 금지 정책) | merger 미사용 |
| 지휘자 토큰 | 360k | 측정 전 | |
| wip-ready 형식 준수 | - | 4/4 준수(T22·T23·T21·T01: 카드·QUEUE·커밋·시트·알려진 흠) | 첫 보고 도착 시 |
| scout(haiku) charter 준수 | - | 준수(표 형식 · 근거 · 열린 질문 포함) | 1건 |
| 완료된 카드 / 발주 | 10/13 병합 | 5/13 (T18 종결 · T22 · T23 · T21 · T01 wip-ready) | |
| 오류·재작업 | - | 2건(T26 역할 오배정 44k) · 1건(T18 중복 발주, 낭비 56k) → 규칙 추가: 발주 전 QUEUE 행을 MERGE_QUEUE 완료표와 대조 | |
