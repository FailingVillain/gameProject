#!/bin/bash
# 10차 실행기 (중대 후퇴 30~40%, 순차). 사용: bash tools/tobs_scen/run_tobs10.sh
# V0 = 변경 전 (bin/retreat0), V10 = 변경 후 (bin/retreat). 시드 1~5 x A/B/C.
O=docs/tactics_obs_raw10
for v in V0:bin/retreat0 V10:bin/retreat; do for sd in 1 2 3 4 5; do
  for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
    ${v##*:}/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v%%:*}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v%%:*}_${sc%%:*}$sd.log
  done; done; done
find $O -name 'err_*.log' -empty -delete
echo done
