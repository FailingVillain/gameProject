#!/bin/bash
# 15차 실행기 (N23 돌격보병 소탕 · N55 장교 경험 성향, 순차). 사용: bash tools/tobs_scen/run_tobs15.sh
# N23 = bin/n23 (브랜치), B = 같은 obs 도구 이전의 브랜치 기준 (bin/n23base = 브랜치 시작 main + 측정 카운터).
O=docs/tactics_obs_raw15
G="C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\6e3d9b66-3290-41a9-804a-9b023d3f1119\scratchpad\heavy_gate.ps1"
for sd in 1 2 3; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  for v in ${VARS:-N23:bin/n23 B:bin/n23base}; do
    [ -f "$G" ] && powershell -ExecutionPolicy Bypass -File "$G" >/dev/null
    ${v#*:}/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v%%:*}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v%%:*}_${sc%%:*}$sd.log
  done
done; done
find $O -name 'err_*.log' -empty -delete
echo done
