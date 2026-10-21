#!/bin/bash
# F1 측정: tools/f1_run.sh <bin dir> <tag>  — tactics-obs A=ai_bn_meeting B=ai_bn_assault C=ai_kr_vs_ft_1000, 시드 1~5, 600초 (한 번에 하나씩)
X=$1/KoreanDivision.exe; tag=$2; O=${O:-docs/f1_raw}; mkdir -p $O
for k in ${SETS:-A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000}; do m=${k#*:}; K=${k%%:*}
 for sd in ${SEEDS:-1 2 3 4 5}; do
  KD_OBS_VARIANT=${VAR:-V0} $X --tactics-obs $m --seed $sd --seconds 600 --out $O/${tag}_${K}$sd.txt >/dev/null 2>&1
  echo "$tag $K$sd $(sed -n 2,4p $O/${tag}_${K}$sd.txt | tr '\n' ' ' | cut -c1-160)"
 done; done
