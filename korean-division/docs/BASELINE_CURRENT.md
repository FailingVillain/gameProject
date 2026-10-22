# Baseline (main) — append-only, newest last

## 8dc7969 (2026-10-20, F1 1+2차 + 오늘 밸런스 전부)
tactics-obs seeds 1-5 (docs/f2_raw/summary_main.txt): A ai_bn_meeting 3/5 K/E 0.91 timeout 1 · B ai_bn_assault 3/5 K/E 0.98 timeout 1 · C ai_kr_vs_ft_1000 5/5 K/E 0.57 timeout 0

## 2ce0103 (2026-10-20, vs1 vertical slice merge)
sim unchanged: detcheck golden match (8 variants identical), uifuzz 30 clean, selftest 1719 OK. No new balance seeds (UI/campaign only).

## 60dccdd (2026-10-20, supp 제압 재설계 병합, 스위치 3개 모두 끔)
스위치 꺼짐 = main 과 같은 수치 (tactics-obs A 3/5 0.91 · B 3/5 0.98 · C 5/5 0.57). detcheck golden 일치 (8 variants), snaptest OK, selftest 1739 OK, FormatVersion 41.

## bal2 병합 (2026-10-20, 60dccdd 위)
미션 수정 · 맥심 7/5 + 최소 대기 · 기병 돌격 문턱 상향 (minRiders 10 · minOdds 2.0 · flankMinSupp 0.3 · rechargeCooldown 120). 시뮬 변경: golden 재생성, 8 variants identical, selftest 1740 OK. 균형 판정은 야간 soak.

## 8850c48 (2026-10-20, c102 campaign 1-2 merge)
sim unchanged: detcheck golden match, uifuzz 30 clean, selftest 1755 OK. c1_02_tumen --mission-run seeds 1-5 (no carry): 2 wins / 3 losses (60% losses), 300-980 s, enemy 732, 0 timeouts.

## 127b54c (2026-10-20, props F6 merge, Snapshot 42)
generated maps now carry props (golden updated, 8 variants identical); missions only with propScatter. tactics-obs seeds 1-3 (propScatter on vs off, same build): A ai_bn_meeting wins 1/3 vs 2/3, K/E 0.98 vs 1.02 · B ai_bn_assault 1/3 vs 2/3 (timeouts 1 vs 1), K/E 0.98 vs 1.02 · C ai_kr_vs_ft_1000 3/3 vs 3/3, K/E 1.79 vs 1.77. Shots at infantry that got prop cover: A 0.5-0.6%, B 0.0-0.6%, C 0.7-1.6% (props are sparse near the lines; no shell broke a prop in A/B). Plain = sim of main. 3 seeds only: win-count diffs are noise-level, balance verdict stays with the nightly soak.

## b73eefb (2026-10-20, supp2 제압 재설계 2차 — 엎드림 0.35 · 엄폐 회복 1.25 · 붕괴 0.15 기본 켬)
Same build, switches off vs on (docs/legacy/supp2_raw, seeds 1-5, tactics-obs 600 s): A ai_bn_meeting off 3/5 K/E 1.16 TO 0 → on 1/5 1.11 TO 2 · B ai_bn_assault off 1/5 1.11 TO 2 → on 4/5 0.93 TO 1 · C ai_kr_vs_ft_1000 off 4/5 0.61 TO 1 → on 4/5 0.49 TO 1. S1 KR charge 40 s retreat (C) 67% → 54%. sim RR (rifle 100 v 100) A wins off 1/5 → on 0/5 (TO 2→3); LT (rifle+lmg attack v trench) off 1/5 (TO 4) → on 2/5 (TO 3). Breaks per game per side 4-12, rallies 2-8. Golden regenerated (8 variants identical), snaptest QUICK OK, selftest 1767 OK. "After" numbers for later agents = the "on" column.

## props2 병합 (2026-10-20, 127b54c 위, 미션 전부 propScatter + 소품 제압 엄폐)
golden 일치 (detcheck 변화 없음), snaptest OK, selftest PASS. tactics-obs seeds 1-3: A ai_bn_meeting 2/3 K/E 1.29 (소품 엄폐 사격 0.1-0.3%) · B ai_bn_assault 2/3 K/E 1.05 TO 1 (0.1-1.4%) · C ai_kr_vs_ft_1000 3/3 K/E 1.59 (0.3-0.5%). mission-run seed 1: 13개 모두 예외 없이 끝 (c1_01 은 AI 방어 승 2700s, main 은 1048s 패).

## surr 병합 (2026-10-21, N79b 항복, FormatVersion 43)
golden 갱신 (detcheck 8 변형 일치), snaptest OK, selftest 1792 OK. tactics-obs seeds 1-3 (A ai_bn_meeting · B ai_bn_assault · C ai_kr_vs_ft_1000): 항복 0건 전부 (AI끼리는 퇴각 불가 + 제압 10초 + 중대 80% 소멸 조건이 거의 안 맞음 = 드묾). 원본 docs/legacy/surr_raw.

## N79a 병합 (2026-10-21, 집결 지점 · 포격 종류, Snapshot 44 · Replay 16)
golden 갱신 (새 상태 필드가 체크섬에 들어감 — 시뮬 결과 자체는 그대로, 8 변형 동일 8bca15f8…), snaptest QUICK OK, uifuzz 30 예외 0, selftest 1782 OK (+13). 새 상태는 집결 지점을 지정하거나 포격 종류를 쓸 때만 바뀌므로 AI 전투 수치 변화는 AI 자기 후퇴가 집결 지점 (= 기존 후퇴 위치) 으로 가는 것뿐 — 별도 측정 안 함 (밸런스 판정은 야간 soak).

## N32 병합 (2026-10-21, 참호 돌격 교리 · AI 기본 켬 · 약진 끝 12칸, Snapshot 45 · Replay 17)
golden 갱신 (8 변형 동일 b1160cd0…), snaptest QUICK OK, uifuzz 30 예외 0, selftest 1771 OK. tactics-obs (AI 켬): B ai_bn_assault 3/5 K/E 0.92 · trench_assault 4/5 K/E 0.89 · ai_kr_vs_jp_trench 4/5 K/E 1.16 (n32 보고 수치).

## ai3b 병합 (2026-10-21, 전술 AI 3B 기본 켬, Snapshot 46)
golden 변화 없음 (8 변형 동일 b1160cd0...), snaptest QUICK OK, selftest 1836 OK. tactics-obs 기본값 대 no3b (docs/ai3b_raw): A ai_bn_meeting 2/5 K/E 0.85 · B ai_bn_assault 3/5 K/E 0.98 -> 0.94 · C ai_kr_vs_ft_1000 5/5 K/E 0.66.

## nobreak 병합 (2026-10-21, 붕괴 끔 breakChance 0 + 탄 없는 전차 포기, 형식 변경 없음)
golden 갱신 (8 변형 동일 bf3f2d94...), snaptest QUICK OK, selftest 1836 OK. 시뮬 측정 없음 (AI 승률 기준은 붕괴 끔 상태로 달라질 수 있음 - 야간 soak 확인).

## propdens 병합 (2026-10-21, 전 미션 13개 propDensity 4 / propCell 6, 형식 변경 없음)
golden 변화 없음 (detcheck bf3f2d94 일치), selftest 1838 OK. 엄폐 비율 0.1-1.4% -> 3.8-8% (c1_01 3.8%, c1_02 0.1% 120초). 상세 ENGINE_NOTES 엄폐물 밀도 상향.

## units 병합 (2026-10-21, N63/N68/N72/N78 새 유닛 + 카자크 · 간도 그림 수정, Snapshot 47)
golden 갱신 (8 변형 동일 98a52bcb...), snaptest QUICK OK, uifuzz 30 예외 0, selftest 1873 OK. 시뮬 측정 없음 (새 유닛은 미션 미배치).

## pronemove 병합 (2026-10-21, 엎드림 이동 · 상한 8/3초 · 도하 면제 · 교두보 집계 수정, Snapshot 48)
golden 갱신 (8 변형 동일 916116a7...), snaptest QUICK OK, selftest 1905 OK. crossing 단일 다리 시드 1: 교두보 211초 (건너편 140명), 도하 완료 323초 · 266명 (main 302초/302명). tactical 곡사포 OK.

## assaultinf 병합 (2026-10-22, 돌격보병 강화, Snapshot 49)
golden 갱신 (8 변형 동일 d7826187...), snaptest QUICK OK, 전용 시험 OK, 전체 selftest 1914 OK + 골든 1건(갱신). 소총 대 돌격 (5시드 x 좌우, balance sim): 평지 30:30 교환비 5.36 -> 8.57 (돌격 손실 5.6 -> 3.5), 30:23 돌격 승 0/10 -> 2/10 (교환비 0.28 -> 0.79), 숲 30:30 2.96 -> 5.66. 대형 완화 병합 전 기준 - 재측정 필요.

## arty3 병합 (2026-10-22, ba4033fb, 포병 · 중화기 묶음, Snapshot 50 · Replay 18)
golden 갱신 (8 변형 동일 32639a67...), snaptest QUICK OK, 전체 selftest 생략 (큐 규칙). C ai_kr_vs_ft_1000 KD_ARTYFX seeds 1-5 600 s: 한국 5/5 승, 손실비 (한국/봉천 보병계) 0.47 (이전 기준 ai3b K/E 0.66), 222-321 s. 한국 포 판당 포탄: 야전포 253 (명중 48%, 크레딧 230) · 곡사포 70 (45%, 94) · 보병포 52 (64%, 41).

## S4 소총 대 기병 (측정, a0216911 = main, 2026-10-22, 돌격보병 강화 · 하마 사격 N61 · 포병 묶음 반영)
`--balance sim infantry:30 cavalry_default:30 flat` 시드 1-5 x 좌우 교대 10판, 600초 상한, 양쪽 전술 AI. 소총 승 1 / 기병 승 0 / 무승부(600초 시간 초과) 9. 손실 합계: 소총 64 · 기병 122 (판당 6.4 대 12.2, 교환비 약 1.9 소총 우세). 시간 초과 판은 기병이 한 번 돌격(제압으로 꺾임 10-11회, 자동 막음 54) 뒤 멀리 물러나 대치 - 승부가 안 남. 원본 /tmp/s4 (보관 안 함).

## ec9f713 (2026-10-22, release 첫 실 가동 — fix/n12-forest bfe5641 병합 뒤)
sim unchanged: detcheck golden 일치 (8 variants identical 8aa6448b9690b922), snaptest QUICK OK, 전체 selftest 2008 OK / FAIL 0 (≈12분; 성능 유닛 200 평균 0.499ms · 1000 평균 1.891ms). 밸런스 측정 없음. FormatVersion 52 · Replay 19.
