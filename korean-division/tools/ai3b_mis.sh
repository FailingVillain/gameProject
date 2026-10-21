#!/bin/bash
# 3B 측정: tools/ai3b_mis.sh <bin dir> <tag> — 방어 관련 미션 (village_defense · c1_01_hoeryong · yalu_crossing) 시드 1~3, --mission-run (한 번에 하나씩)
X=$1/KoreanDivision.exe; tag=$2; O=${O:-docs/ai3b_raw}; mkdir -p $O
for m in ${MIS:-village_defense c1_01_hoeryong yalu_crossing}; do
 for sd in ${SEEDS:-1 2 3}; do
  $X --mission-run $m --seed $sd > $O/${tag}_${m}_$sd.txt 2>&1
  echo "$tag $m $sd $(grep -E 'mission-run|  faction' $O/${tag}_${m}_$sd.txt | tr '\n' ' ' | cut -c1-260)"
 done; done
