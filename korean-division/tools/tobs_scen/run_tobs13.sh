#!/bin/bash
# 13차 실행기 (cavcmd 기병 장교 · 기병 부사관, 순차). 사용: bash tools/tobs_scen/run_tobs13.sh
# VARS="CAV:bin/cavcmd" 로 일부만. CAV1 = officerBack 0 (장교도 함께 돌격, 첫 실행 파일). CAV = cavcmd (bin/cavcmd), B = 같은 obs 도구를 가진 병합 전 main (../kd-cavbase/bin/cavbase).
O=docs/tactics_obs_raw13
G="C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\6e3d9b66-3290-41a9-804a-9b023d3f1119\scratchpad\heavy_gate.ps1"
for sd in 1 2 3; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  for v in ${VARS:-CAV:bin/cavcmd B:../kd-cavbase/bin/cavbase}; do
    [ -f "$G" ] && powershell -ExecutionPolicy Bypass -File "$G" >/dev/null
    ${v#*:}/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v%%:*}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v%%:*}_${sc%%:*}$sd.log
  done
done; done
find $O -name 'err_*.log' -empty -delete
echo done
