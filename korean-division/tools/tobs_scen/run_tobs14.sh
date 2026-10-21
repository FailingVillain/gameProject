#!/bin/bash
# 14차 실행기 (N35 장교 교전 회피 · 정찰병 자위 사격, 순차). 사용: bash tools/tobs_scen/run_tobs14.sh [시드...]
# OLD = 같은 실행 파일 + KD_OBS_VARIANT=OFFOLD (combat.officerAvoid · scoutAvoid 끔 = 옛 동작), NEW = 기본 (켬).
O=docs/tactics_obs_raw14; mkdir -p $O
B=bin/officer/KoreanDivision.exe
G="C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\6e3d9b66-3290-41a9-804a-9b023d3f1119\scratchpad\heavy_gate.ps1"
for sd in ${@:-1 2 3 4 5}; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  for v in OLD NEW; do
    [ -f "$G" ] && powershell -ExecutionPolicy Bypass -File "$G" >/dev/null
    if [ $v = OLD ]; then var=OFFOLD; else var=; fi
    KD_OBS_VARIANT=$var $B --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v}_${sc%%:*}$sd.log
  done
done; done
find $O -name 'err_*.log' -empty -delete
echo done
