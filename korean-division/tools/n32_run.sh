#!/bin/bash
# N32 측정: tools/n32_run.sh <bin dir> <variant V0|NoTA> — tactics-obs B=ai_bn_assault T=trench_assault J=ai_kr_vs_jp_trench, 시드 1~5, 600초 (한 번에 하나씩)
X=$1/KoreanDivision.exe; V=$2; O=${O:-docs/n32_raw}; mkdir -p $O
for k in ${SETS:-B:ai_bn_assault T:trench_assault J:ai_kr_vs_jp_trench}; do m=${k#*:}; K=${k%%:*}
 for sd in ${SEEDS:-1 2 3 4 5}; do
  KD_OBS_VARIANT=$V $X --tactics-obs $m --seed $sd --seconds 600 --out $O/${V}_${K}$sd.txt >/dev/null 2>&1
  echo "$V $K$sd $(sed -n 2,4p $O/${V}_${K}$sd.txt | tr '\n' ' ' | cut -c1-170)"
 done; done
