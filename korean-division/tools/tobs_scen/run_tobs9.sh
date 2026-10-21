#!/bin/bash
# 9차 실행기 (결정 4 · 12 + 부사관, 순차 · 한 번에 한 프로세스). 사용: bash tools/tobs_scen/run_tobs9.sh
# V9 = 기본 (전부 적용), NoGate = 돌격 허가 끔, NoNco = 부사관 기본 편성 끔 (분리 시험).
X=bin/tobs9/KoreanDivision.exe; O=docs/tactics_obs_raw9
for v in V9 NoGate NoNco; do for sd in 1 2 3 4 5; do
  for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
    KD_OBS_VARIANT=$v $X --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v}_${sc%%:*}$sd.log
  done; done; done
find $O -name 'err_*.log' -empty -delete
echo done
