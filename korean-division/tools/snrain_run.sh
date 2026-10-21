#!/bin/bash
# usage: snrain_run.sh <tag> <suffix|->  ; suffix '' = assets mission, 'rain' = tools/tobs_scen/<m>_rain.json
X=bin/snrain/KoreanDivision.exe; O=docs/snrain_raw; tag=$1; suf=$2
for m in ai_bn_meeting ai_bn_assault; do for sd in ${SEEDS:-1 2 3 4 5}; do
 if [ "$suf" = "-" ]; then mm=$m; else mm=tools/tobs_scen/${m}_$suf.json; fi
 KD_OBS_VARIANT=V0 $X --tactics-obs $mm --seed $sd --seconds 600 --out $O/${tag}_${m}_$sd.txt >/dev/null 2>&1
 echo "$tag $(sed -n 2,4p $O/${tag}_${m}_$sd.txt | tr '\n' ' ' | sed 's/faction 1 한국군: spawned [0-9]*, //;s/faction 2 봉천 군벌: spawned [0-9]*, //;s/# ai_bn_//')"
done; done
