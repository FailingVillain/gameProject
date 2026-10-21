#!/bin/bash
# 포병 효과 소규모 쌍대비교 (한 번에 한 프로세스). 사용: tools/artyfx_scen/run_small.sh <exe> <outdir> <seedMax>
EXE=${1:-bin/artyfx/KoreanDivision.exe}; OUT=${2:-docs/artyfx_raw}; SMAX=${3:-20}
B="infantry:60,officer:2"
declare -A V
V[ctl]="infantry:60,officer:2"
V[scout4]="infantry:60,officer:2,infantry_scout:4"
V[ig3]="infantry:60,officer:2,heavy_infantry_gun:3"
V[fg3]="infantry:60,officer:2,heavy_field_gun:3"
V[hw3s4]="infantry:60,officer:2,heavy_howitzer:3,infantry_scout:4"
V[mix]="infantry:60,officer:2,heavy_infantry_gun:1,heavy_field_gun:1,heavy_howitzer:1,infantry_scout:2"
V[ig3eq]="infantry:66,officer:2"
V[fg3eq]="infantry:69,officer:2"
V[hw3s4eq]="infantry:72,officer:2,infantry_scout:4"
V[mixeq]="infantry:69,officer:2,infantry_scout:2"
mkdir -p "$OUT"
for v in ctl scout4 ig3 fg3 hw3s4 mix ig3eq fg3eq hw3s4eq mixeq; do
 for s in $(seq 1 $SMAX); do
  KD_ARTYFX=1 "$EXE" --balance sim "${V[$v]}" "$B" flat $s 1 300 2>&1 | grep -E "^(ARTYFX|MEAS|결과)" > "$OUT/small_${v}_s$s.txt"
 done
done
