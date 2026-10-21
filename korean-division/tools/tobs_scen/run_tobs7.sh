#!/bin/bash
# 7차 실행기 (순차, 한 번에 한 프로세스). 사용: bash tools/tobs_scen/run_tobs7.sh
X=bin/tobs7/KoreanDivision.exe; O=docs/tactics_obs_raw7; S=tools/tobs_scen
run(){ # tag mission seed [env...]
  tag=$1; m=$2; sd=$3; KD_OBS_VARIANT=V0 $X --tactics-obs $m --seed $sd --seconds 600 --out $O/${tag}$sd.txt >/dev/null 2>$O/err_${tag}$sd.log; }
for sd in 1 2 3; do
  # V0 기준 (새 지표 포함)
  run V0_A ai_bn_meeting $sd; run V0_B ai_bn_assault $sd; run V0_C ai_kr_vs_ft_1000 $sd
  # 조건 1: 날씨 · 시간
  for k in fog rain night; do
    run ${k}_A $S/ai_bn_meeting_$k.json $sd; run ${k}_B $S/ai_bn_assault_$k.json $sd; run ${k}_C $S/ai_kr_vs_ft_1000_$k.json $sd
  done
  # 조건 2: 장교 상실 (한국 세력 1, 첫 교전 + 30초)
  for km in all cmd; do for sc in A:ai_bn_meeting B:ai_bn_assault; do
    KD_OBS_KILL=$km run kill${km}_${sc%%:*} ${sc##*:} $sd; done; done
  # 조건 2b: 부사관 변형 (분대장 부사관화, 장교 전멸)
  for sc in A:ai_bn_meeting B:ai_bn_assault; do KD_OBS_NCO=1 KD_OBS_KILL=all run ncokill_${sc%%:*} ${sc##*:} $sd; done
  for sc in A:ai_bn_meeting B:ai_bn_assault; do KD_OBS_NCO=1 run nco_${sc%%:*} ${sc##*:} $sd; done
  # 조건 3: 참호 없는 B
  run open_B $S/ai_bn_assault_open.json $sd
  # 조건 4: 기병 증강
  run cav_A $S/ai_bn_meeting_cav.json $sd; run cav_C $S/ai_kr_vs_ft_1000_cav.json $sd
done
echo done > $O/_done.tmp
