#!/bin/bash
# N22 측정 (한 번에 한 프로세스). 사용: run_n22.sh <exe> <outdir> [parts]   parts = "c ig d small" (기본 전부)
# 환경변수 KD_ARTY2_OVR (ArtyFx 메모리 덮어쓰기, 예 "ai.redeployCooldownSec=20") 는 그대로 자식에 상속.
EXE=$1; OUT=$2; PARTS=${3:-"c ig d small"}; mkdir -p "$OUT"
GATE="C:\Users\seung\AppData\Local\Temp\claude\C--Users-seung\6e3d9b66-3290-41a9-804a-9b023d3f1119\scratchpad\heavy_gate.ps1"
[ -f "$GATE" ] && powershell -ExecutionPolicy Bypass -File "$GATE" >/dev/null
start=$(date +%s)
for p in $PARTS; do
 case $p in
  c) for s in 1 2 3 4 5; do KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs ai_kr_vs_ft_1000 --seed $s --seconds 600 --out "$OUT/obs_c$s.txt" > "$OUT/c_s$s.txt" 2>&1; done ;;
  ig) for s in 1 2 3 4 5; do KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs tools/artyfx_scen/ai_kr_vs_ft_1000_noKig.json --seed $s --seconds 600 > "$OUT/cnoig_s$s.txt" 2>&1; done ;;
  d) for s in 1 2 3; do KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs tools/tobs_scen/ai_kr_vs_ft_2000.json --seed $s --seconds 600 --out "$OUT/obs_d$s.txt" > "$OUT/d_s$s.txt" 2>&1; done ;;
  small)
   B="infantry:60,officer:2"
   declare -A V
   V[hw3s4]="infantry:60,officer:2,heavy_howitzer:3,infantry_scout:4"
   V[fg3]="infantry:60,officer:2,heavy_field_gun:3"
   V[ig3]="infantry:60,officer:2,heavy_infantry_gun:3"
   V[ctl]="infantry:60,officer:2"
   V[ig3eq]="infantry:66,officer:2"
   V[fg3eq]="infantry:69,officer:2"
   V[hw3s4eq]="infantry:72,officer:2,infantry_scout:4"
   for v in ${SMALLV:-hw3s4 fg3 ig3 ctl ig3eq fg3eq hw3s4eq}; do
    for s in $(seq 1 ${SMAX:-10}); do
     KD_ARTYFX=1 "$EXE" --balance sim "${V[$v]}" "$B" flat $s 1 300 2>&1 | grep -E "^(ARTYFX|MEAS|결과)" > "$OUT/small_${v}_s$s.txt"
    done
   done ;;
 esac
done
echo "wall $(( $(date +%s) - start ))s"
