O=docs/tactics_obs_raw16
for sd in 1 2 3; do for sc in A:ai_bn_meeting B:ai_bn_assault C:ai_kr_vs_ft_1000; do
  for v in LMG:bin/lmg MAIN:../../korean-division/bin/main; do
    powershell -ExecutionPolicy Bypass -File "C:\Users\seung\OneDrive\바탕 화면\coding\korean-division\tools\agent\heavy_gate.ps1" >/dev/null
    ${v#*:}/KoreanDivision.exe --tactics-obs ${sc##*:} --seed $sd --seconds 600 --out $O/${v%%:*}_${sc%%:*}$sd.txt >/dev/null 2>$O/err_${v%%:*}_${sc%%:*}$sd.log
  done
done; done
echo done
