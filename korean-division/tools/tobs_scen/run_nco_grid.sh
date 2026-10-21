#!/bin/bash
# 11차 부사관 격자: 멍칫 {0.5,0.7,0.85} x 감소 {1.0,1.25,1.5}, 장교 뒤 1칸, 시드 1~10. 사용: bash tools/tobs_scen/run_nco_grid.sh
export SEEDS="1 2 3 4 5 6 7 8 9 10"
for h in 0.5 0.7 0.85; do for d in 1.0 1.25 1.5; do
  bash tools/tobs_scen/run_tobs11.sh N${h}_${d} 1 3 $h $d
done; done
echo grid-done
