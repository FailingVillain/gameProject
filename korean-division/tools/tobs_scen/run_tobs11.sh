#!/bin/bash
# 11차 실행기 (장교 거리 · 부사관 배수, 순차). 사용: bash tools/tobs_scen/run_tobs11.sh 이름 OFFBACK OFFSPREAD NCOH NCOD
# 시드 1~5 x A/B/C. 결과 docs/tactics_obs_raw11/<이름>_<A|B|C><시드>.txt
O=docs/tactics_obs_raw11; n=$1
export KD_OBS_OFFBACK=$2 KD_OBS_OFFSPREAD=$3 KD_OBS_NCOH=$4 KD_OBS_NCOD=$5
for sd in ${SEEDS:-1 2 3 4 5}; do
  for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
    bin/offnco/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${n}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${n}_${sc%%:*}$sd.log
  done; done
find $O -name 'err_*.log' -empty -delete
