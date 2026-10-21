#!/bin/bash
# usage: run.sh <exe> <outdir>   (repo cwd)
EXE=$1; OUT=$2; mkdir -p "$OUT"
GATE="C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\6e3d9b66-3290-41a9-804a-9b023d3f1119\scratchpad\heavy_gate.ps1"
powershell -ExecutionPolicy Bypass -File "$GATE" >/dev/null
start=$(date +%s)
for s in 1 2 3 4 5; do
  KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs ai_kr_vs_ft_1000 --seed $s --seconds 600 --out "$OUT/obs_c$s.txt" > "$OUT/c_s$s.txt" 2>&1
done
B="infantry:60,officer:2"
declare -A V
V[hw3s4]="infantry:60,officer:2,heavy_howitzer:3,infantry_scout:4"
V[fg3]="infantry:60,officer:2,heavy_field_gun:3"
V[ig3]="infantry:60,officer:2,heavy_infantry_gun:3"
V[ctl]="infantry:60,officer:2"
for v in hw3s4 fg3 ig3 ctl; do
 for s in $(seq 1 10); do
  KD_ARTYFX=1 "$EXE" --balance sim "${V[$v]}" "$B" flat $s 1 300 2>&1 | grep -E "^(ARTYFX|MEAS|결과)" > "$OUT/small_${v}_s$s.txt"
 done
done
echo "wall $(( $(date +%s) - start ))s"

# B 1-12, D 1-3
for s in $(seq 1 12); do KD_ARTYFX=1 KD_OBS_VARIANT=V0 $EXE --tactics-obs ai_bn_assault --seed $s --seconds 600 --out "$OUT/obs_b$s.txt" > "$OUT/b_s$s.txt" 2>&1; done
for s in 1 2 3; do
  KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs tools/tobs_scen/ai_kr_vs_ft_2000.json --seed $s --seconds 600 --out "$OUT/obs_d$s.txt" > "$OUT/d_s$s.txt" 2>&1
done
