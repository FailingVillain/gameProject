# Overnight / unattended perf run: waits for the heavy gate, then runs the bench matrix and the 1-hour-equivalent live-heap run.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_overnight.ps1 [-Exe bin/perfbench/KoreanDivision.exe] [-Label name] [-OutDir tools/perf/out] [-Seeds 1,2,3] [-Repeats 1] [-Hour] [-Compare tools/perf/baseline_main.json]
# Output: <OutDir>/<label>_<yyyyMMdd_HHmm>.json (+ _hour.json with -Hour, + .log). One heavy job at a time: every stage passes heavy_gate first (never bypassed).
# With -Compare, runs tools/perf_compare.ps1 on the result at the end (exit code 1 on >10% regression).
param([string]$Exe = "bin/perfbench/KoreanDivision.exe", [string]$Label = "run", [string]$OutDir = "tools/perf/out",
      [string]$Seeds = "1,2,3", [int]$Repeats = 1, [string]$Ks = "1000,5000,10000", [int]$Ticks = 600, [switch]$Hour, [string]$Compare = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot; Set-Location $root
$gate = "C:/gameProject/korean-division/tools/agent/heavy_gate.ps1"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmm'
$out = "$OutDir/${Label}_$stamp.json"; $log = "$OutDir/${Label}_$stamp.log"
& powershell -NoProfile -ExecutionPolicy Bypass -File $gate | Tee-Object -FilePath $log -Append
& $Exe --stress bench scenes=ACE "k=$Ks" "seeds=$Seeds" "repeats=$Repeats" "ticks=$Ticks" "out=$out" "label=$Label" | Tee-Object -FilePath $log -Append
$rc = $LASTEXITCODE
if ($Hour) {
  # B 5000, 216000 ticks (1 h of game time); forced-GC live heap every 21600 ticks. cap=0 = no wall-clock limit.
  & powershell -NoProfile -ExecutionPolicy Bypass -File $gate | Tee-Object -FilePath $log -Append
  & $Exe --stress bench scenes=B k=5000 seeds=1 ticks=216000 memevery=21600 cap=0 "out=$OutDir/${Label}_${stamp}_hour.json" "label=${Label}-hour" | Tee-Object -FilePath $log -Append
}
if ($Compare -ne "") { & powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_compare.ps1 $Compare $out; $rc = [Math]::Max($rc, $LASTEXITCODE) }
exit $rc
