# Baseline (main) — append-only, newest last

## 8dc7969 (2026-10-20, F1 rounds 1+2 + all of today's balance)
tactics-obs seeds 1-5 (docs/f2_raw/summary_main.txt): A ai_bn_meeting 3/5 K/E 0.91 timeout 1 · B ai_bn_assault 3/5 K/E 0.98 timeout 1 · C ai_kr_vs_ft_1000 5/5 K/E 0.57 timeout 0

## 2ce0103 (2026-10-20, vs1 vertical slice merge)
sim unchanged: detcheck golden match (8 variants identical), uifuzz 30 clean, selftest 1719 OK. No new balance seeds (UI/campaign only).

## 60dccdd (2026-10-20, supp suppression redesign merged, all 3 switches off)
Switches off = same numbers as main (tactics-obs A 3/5 0.91 · B 3/5 0.98 · C 5/5 0.57). detcheck golden match (8 variants), snaptest OK, selftest 1739 OK, FormatVersion 41.

## bal2 merge (2026-10-20, on top of 60dccdd)
Mission fixes · Maxim 7/5 + minimum wait · raised cavalry charge threshold (minRiders 10 · minOdds 2.0 · flankMinSupp 0.3 · rechargeCooldown 120). Sim change: golden regenerated, 8 variants identical, selftest 1740 OK. Balance verdict by the night soak.

## 8850c48 (2026-10-20, c102 campaign 1-2 merge)
sim unchanged: detcheck golden match, uifuzz 30 clean, selftest 1755 OK. c1_02_tumen --mission-run seeds 1-5 (no carry): 2 wins / 3 losses (60% losses), 300-980 s, enemy 732, 0 timeouts.

## 127b54c (2026-10-20, props F6 merge, Snapshot 42)
generated maps now carry props (golden updated, 8 variants identical); missions only with propScatter. tactics-obs seeds 1-3 (propScatter on vs off, same build): A ai_bn_meeting wins 1/3 vs 2/3, K/E 0.98 vs 1.02 · B ai_bn_assault 1/3 vs 2/3 (timeouts 1 vs 1), K/E 0.98 vs 1.02 · C ai_kr_vs_ft_1000 3/3 vs 3/3, K/E 1.79 vs 1.77. Shots at infantry that got prop cover: A 0.5-0.6%, B 0.0-0.6%, C 0.7-1.6% (props are sparse near the lines; no shell broke a prop in A/B). Plain = sim of main. 3 seeds only: win-count diffs are noise-level, balance verdict stays with the nightly soak.

## b73eefb (2026-10-20, supp2 suppression redesign round 2 — prone 0.35 · cover recovery 1.25 · collapse 0.15 on by default)
Same build, switches off vs on (docs/legacy/supp2_raw, seeds 1-5, tactics-obs 600 s): A ai_bn_meeting off 3/5 K/E 1.16 TO 0 → on 1/5 1.11 TO 2 · B ai_bn_assault off 1/5 1.11 TO 2 → on 4/5 0.93 TO 1 · C ai_kr_vs_ft_1000 off 4/5 0.61 TO 1 → on 4/5 0.49 TO 1. S1 KR charge 40 s retreat (C) 67% → 54%. sim RR (rifle 100 v 100) A wins off 1/5 → on 0/5 (TO 2→3); LT (rifle+lmg attack v trench) off 1/5 (TO 4) → on 2/5 (TO 3). Breaks per game per side 4-12, rallies 2-8. Golden regenerated (8 variants identical), snaptest QUICK OK, selftest 1767 OK. "After" numbers for later agents = the "on" column.

## props2 merge (2026-10-20, on top of 127b54c, all missions propScatter + prop suppression cover)
golden match (detcheck no change), snaptest OK, selftest PASS. tactics-obs seeds 1-3: A ai_bn_meeting 2/3 K/E 1.29 (prop-cover shots 0.1-0.3%) · B ai_bn_assault 2/3 K/E 1.05 TO 1 (0.1-1.4%) · C ai_kr_vs_ft_1000 3/3 K/E 1.59 (0.3-0.5%). mission-run seed 1: all 13 finished without exceptions (c1_01 was an AI defense win at 2700s; main was a loss at 1048s).

## surr merge (2026-10-21, N79b surrender, FormatVersion 43)
golden updated (detcheck 8 variants match), snaptest OK, selftest 1792 OK. tactics-obs seeds 1-3 (A ai_bn_meeting · B ai_bn_assault · C ai_kr_vs_ft_1000): 0 surrenders in all (AI vs AI cannot retreat + 10 s suppression + 80% company wipe-out conditions almost never met = rare). Raw data docs/legacy/surr_raw.

## N79a merge (2026-10-21, rally point · barrage types, Snapshot 44 · Replay 16)
golden updated (new state fields enter the checksum — the sim result itself is unchanged, 8 variants identical 8bca15f8…), snaptest QUICK OK, uifuzz 30 exceptions 0, selftest 1782 OK (+13). The new state only changes when a rally point is set or a barrage type is used, so the only change in AI combat numbers is that the AI's own retreat goes to the rally point (= the existing retreat position) — not measured separately (balance verdict by the nightly soak).

## N32 merge (2026-10-21, trench assault doctrine · AI on by default · dash end 12 cells, Snapshot 45 · Replay 17)
golden updated (8 variants identical b1160cd0…), snaptest QUICK OK, uifuzz 30 exceptions 0, selftest 1771 OK. tactics-obs (AI on): B ai_bn_assault 3/5 K/E 0.92 · trench_assault 4/5 K/E 0.89 · ai_kr_vs_jp_trench 4/5 K/E 1.16 (n32 report numbers).

## ai3b merge (2026-10-21, tactical AI 3B on by default, Snapshot 46)
golden unchanged (8 variants identical b1160cd0...), snaptest QUICK OK, selftest 1836 OK. tactics-obs default vs no3b (docs/ai3b_raw): A ai_bn_meeting 2/5 K/E 0.85 · B ai_bn_assault 3/5 K/E 0.98 -> 0.94 · C ai_kr_vs_ft_1000 5/5 K/E 0.66.

## nobreak merge (2026-10-21, collapse off breakChance 0 + tanks without ammo give up, no format change)
golden updated (8 variants identical bf3f2d94...), snaptest QUICK OK, selftest 1836 OK. No sim measurement (the AI win-rate baseline may change with collapse off - check in the nightly soak).

## propdens merge (2026-10-21, all 13 missions propDensity 4 / propCell 6, no format change)
golden unchanged (detcheck bf3f2d94 match), selftest 1838 OK. Cover ratio 0.1-1.4% -> 3.8-8% (c1_01 3.8%, c1_02 0.1% 120 s). Details: ENGINE_NOTES, cover density increase.

## units merge (2026-10-21, N63/N68/N72/N78 new units + Cossack · Gando art fixes, Snapshot 47)
golden updated (8 variants identical 98a52bcb...), snaptest QUICK OK, uifuzz 30 exceptions 0, selftest 1873 OK. No sim measurement (new units not placed in missions).

## pronemove merge (2026-10-21, prone movement · cap 8/3 s · river crossing exempt · bridgehead count fix, Snapshot 48)
golden updated (8 variants identical 916116a7...), snaptest QUICK OK, selftest 1905 OK. crossing single bridge seed 1: bridgehead 211 s (140 men on the far side), crossing complete 323 s · 266 men (main 302 s/302 men). tactical howitzer OK.

## assaultinf merge (2026-10-22, assault infantry buff, Snapshot 49)
golden updated (8 variants identical d7826187...), snaptest QUICK OK, dedicated test OK, full selftest 1914 OK + 1 golden (updated). Rifle vs assault (5 seeds x left/right, balance sim): flat 30:30 exchange ratio 5.36 -> 8.57 (assault losses 5.6 -> 3.5), 30:23 assault wins 0/10 -> 2/10 (exchange ratio 0.28 -> 0.79), forest 30:30 2.96 -> 5.66. Baseline before the formation-relaxation merge - re-measure needed.

## arty3 merge (2026-10-22, ba4033fb, artillery · heavy weapons bundle, Snapshot 50 · Replay 18)
golden updated (8 variants identical 32639a67...), snaptest QUICK OK, full selftest skipped (queue rule). C ai_kr_vs_ft_1000 KD_ARTYFX seeds 1-5 600 s: Korea 5/5 wins, loss ratio (Korea / Fengtian infantry total) 0.47 (previous baseline ai3b K/E 0.66), 222-321 s. Korean guns, shells per game: field gun 253 (48% hit, credit 230) · howitzer 70 (45%, 94) · infantry gun 52 (64%, 41).

## S4 rifle vs cavalry (measurement, a0216911 = main, 2026-10-22, with assault infantry buff · dismounted fire N61 · artillery bundle)
`--balance sim infantry:30 cavalry_default:30 flat` seeds 1-5 x alternating sides, 10 games, 600 s cap, tactical AI on both sides. Rifle wins 1 / cavalry wins 0 / draws (600 s timeout) 9. Total losses: rifle 64 · cavalry 122 (per game 6.4 vs 12.2, exchange ratio about 1.9 in rifle's favor). In timeout games the cavalry charged once (broken by suppression 10-11 times, auto-block 54) then withdrew far and stood off — no decision. Raw data /tmp/s4 (not kept).

## ec9f713 (2026-10-22, first real run of release — after fix/n12-forest bfe5641 merge)
sim unchanged: detcheck golden match (8 variants identical 8aa6448b9690b922), snaptest QUICK OK, full selftest 2008 OK / FAIL 0 (≈12 min; performance unit 200 avg 0.499ms · 1000 avg 1.891ms). No balance measurement. FormatVersion 52 · Replay 19.
