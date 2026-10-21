#!/bin/bash
# 13차 (pursuit2) 실행기 (후퇴 사격 · 재정비 · 포병 오사 · 기병 조금 엄격, 순차). 사용: bash tools/tobs_scen/run_tobs13p.sh
# P2 = pursuit2 (bin/pursuit2), P1 = 같은 실행 파일 KD_OBS_VARIANT=P1 (N22 동작: 후퇴 사격 · 재정비 · 포병 오사 끔, 기병 옛 수치). 둘 다 새 미션 배치 (대대 종대).
O=docs/tactics_obs_raw13p
mkdir -p $O
for sd in 1 2 3 4 5; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  bin/pursuit2/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/P2_${sc%%:*}$sd.txt >/dev/null 2>$O/err_P2_${sc%%:*}$sd.log
  KD_OBS_VARIANT=P1 bin/pursuit2/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/P1_${sc%%:*}$sd.txt >/dev/null 2>$O/err_P1_${sc%%:*}$sd.log
done; done
find $O -name 'err_*.log' -empty -delete
echo done
