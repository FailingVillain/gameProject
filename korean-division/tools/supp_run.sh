#!/bin/bash
# 제압 재설계 측정: tools/supp_run.sh <bin dir> <tag>  — tactics-obs A/B/C 시드 1~5 (f1_run.sh) + --balance sim 소총 대 소총 · 소총+경기관총 공격 대 참호 소총 (시드 1~5, 한 번에 하나씩)
X=$1/KoreanDivision.exe; tag=$2; O=${O:-docs/supp_raw}; mkdir -p $O
O=$O bash tools/f1_run.sh $1 $tag
for sd in ${SEEDS:-1 2 3 4 5}; do
  $X --balance sim "infantry:100" "infantry:100" flat $sd 1 400 2>&1 | grep "^MEAS" | sed "s/^/$tag RR$sd /" >> $O/${tag}_sim.txt
  KD_SIM_DEFEND=1 KD_SIM_TRENCH=1 $X --balance sim "infantry:92,heavy_machinegunner:8" "infantry:100" flat $sd 1 400 2>&1 | grep "^MEAS" | sed "s/^/$tag LT$sd /" >> $O/${tag}_sim.txt
done
cat $O/${tag}_sim.txt
