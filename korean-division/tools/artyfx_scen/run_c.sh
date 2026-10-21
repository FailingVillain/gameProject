#!/bin/bash
# 시나리오 C (1000 대 1000) 포병 제거 변형. 한 번에 한 프로세스. 사용: run_c.sh <exe> <outdir> <seeds...>
EXE=$1; OUT=$2; shift 2
for s in "$@"; do
 for v in ${VARS:-base noart noartK noartF}; do
  if [ $v = base ]; then M=ai_kr_vs_ft_1000; else M=tools/artyfx_scen/ai_kr_vs_ft_1000_$v.json; fi
  KD_ARTYFX=1 KD_OBS_VARIANT=V0 "$EXE" --tactics-obs $M --seed $s --seconds 600 > "$OUT/c_${v}_s$s.txt" 2>&1
 done
done
