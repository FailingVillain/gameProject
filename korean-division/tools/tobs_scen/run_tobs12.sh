#!/bin/bash
# 12차 실행기 (추격 · 기병 측면, 순차). 사용: bash tools/tobs_scen/run_tobs12.sh
# P = pursuit (bin/pursuit), OLD = 같은 실행 파일 KD_OBS_VARIANT=OLD (돌격 금지 · 사격 추격 · 새 기병 AI 끔), M = 병합 전 main (../kd-pursuit-base/bin/pbase).
O=docs/tactics_obs_raw12
for sd in 1 2 3 4 5; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  bin/pursuit/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/P_${sc%%:*}$sd.txt >/dev/null 2>$O/err_P_${sc%%:*}$sd.log
  KD_OBS_VARIANT=OLD bin/pursuit/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/OLD_${sc%%:*}$sd.txt >/dev/null 2>$O/err_OLD_${sc%%:*}$sd.log
  [ -n "$SKIP_M" ] || ../kd-pursuit-base/bin/pbase/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/M_${sc%%:*}$sd.txt >/dev/null 2>$O/err_M_${sc%%:*}$sd.log
done; done
find $O -name 'err_*.log' -empty -delete
echo done
