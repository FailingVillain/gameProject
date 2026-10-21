# Overnight battle soak (no agent, 0 tokens): alternates
#   (1) every mission headless (--mission-run, all sides AI) with a new seed, and
#   (2) $SimsPerRound random field battles (--balance sim) from an infantry-era pool (no tanks / AT),
#       random map (flat/terrain/city), size, support weapons, sometimes defend/trench.
# Copies bin/main to bin/soak first so rebuilds of bin/main don't disturb it.
# Output docs/soak/<date>/: results.csv (one row per run) + every run's full stdout in runs/ (the data).
param([string]$Until = "07:15", [int]$MissionSec = 1200, [int]$SimSec = 600, [int]$SimsPerRound = 12,
      [int]$GateMax = 3, [int]$RunTimeoutMin = 25)
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bin = Join-Path $root "bin\soak"
if (Test-Path $bin) { Remove-Item -Recurse -Force $bin }
Copy-Item -Recurse (Join-Path $root "bin\main") $bin
$exe = Join-Path $bin "KoreanDivision.exe"
$out = Join-Path $root ("docs\soak\" + (Get-Date -Format "yyyy-MM-dd")); $runs = Join-Path $out "runs"
New-Item -ItemType Directory -Force $runs | Out-Null
$csv = Join-Path $out "results.csv"
if (-not (Test-Path $csv)) { "time,kind,name,seed,exit,sec,status,args" | Out-File -Encoding utf8 $csv }
$missions = Get-ChildItem (Join-Path $root "assets\missions\*.json") | ForEach-Object { $_.BaseName }
$end = [datetime]::ParseExact($Until, "HH:mm", $null); if ($end -lt (Get-Date)) { $end = $end.AddDays(1) }
$gate = Join-Path $PSScriptRoot "heavy_gate.ps1"
$rng = New-Object System.Random 20261020
$lines = @('infantry','infantry_regular','infantry_fengtian','infantry_assault','infantry_dadao_fengtian','infantry_guard','veteran','recruit','infantry_reserve','bandit_rifleman')
$support = @('heavy_machinegunner','heavy_machinegunner_fengtian','heavy_maxim','heavy_infantry_gun','heavy_field_gun','heavy_howitzer','cavalry_default','bandit_horseman','infantry_sniper','scout','officer')
function Army {
  $a = @("$($lines[$rng.Next($lines.Count)]):$(30 * $rng.Next(1, 11))")          # 30..300 line infantry
  for ($i = 0; $i -lt $rng.Next(0, 4); $i++) { $a += "$($support[$rng.Next($support.Count)]):$($rng.Next(2, 13))" }
  ($a -join ',')
}
function Run($kind, $name, $seed, $argList, $envs) {
  & powershell -ExecutionPolicy Bypass -File $gate -Max $GateMax | Out-Null
  $tag = "$kind-$name-$seed" -replace '[^\w\-]', '_'; $log = Join-Path $runs "$tag.log"
  foreach ($k in $envs.Keys) { Set-Item "env:$k" $envs[$k] }
  $t0 = Get-Date
  $p = Start-Process -FilePath $exe -ArgumentList $argList -RedirectStandardOutput $log -RedirectStandardError "$log.err" -NoNewWindow -PassThru
  $null = $p.Handle   # cache the handle, otherwise ExitCode stays empty with -NoNewWindow
  try { $p.PriorityClass = 'BelowNormal' } catch {}
  if (-not $p.WaitForExit($RunTimeoutMin * 60000)) { Stop-Process -Id $p.Id -Force; $code = -999 } else { $code = $p.ExitCode }
  foreach ($k in $envs.Keys) { Remove-Item "env:$k" -ErrorAction SilentlyContinue }
  $sec = [int]((Get-Date) - $t0).TotalSeconds
  $err = Get-Content "$log.err" -Raw -ErrorAction SilentlyContinue
  if ($err) { Add-Content $log "`n--- stderr ---`n$err" }; Remove-Item "$log.err" -ErrorAction SilentlyContinue
  $txt = Get-Content $log -Raw -ErrorAction SilentlyContinue
  if ($null -eq $code -or "$code" -eq "") { $code = "?" }
  $status = if ($code -eq -999) { "TIMEOUT" } elseif (($code -ne "?" -and $code -ne 0) -or $txt -match "Exception|desync|checksum mismatch") { "FAIL" } else { "OK" }
  $envTxt = ($envs.Keys | ForEach-Object { "$_=$($envs[$_])" }) -join ' '
  "$(Get-Date -Format HH:mm),$kind,$name,$seed,$code,$sec,$status,$(("$envTxt $argList") -replace ',', ';' -replace '"', '')" | Out-File -Append -Encoding utf8 $csv
}
$seed = 100
while ((Get-Date) -lt $end) {
  foreach ($m in $missions) {
    if ((Get-Date) -ge $end) { break }
    $seed++; Run "mission" $m $seed "--mission-run $m --seconds $MissionSec --seed $seed" @{}
  }
  for ($i = 0; $i -lt $SimsPerRound -and (Get-Date) -lt $end; $i++) {
    $seed++; $map = @('flat','terrain','city')[$rng.Next(3)]; $A = Army; $B = Army
    $envs = @{}; $r = $rng.Next(10); if ($r -eq 0) { $envs.KD_SIM_DEFEND = '1' } elseif ($r -eq 1) { $envs.KD_SIM_DEFEND = '1'; $envs.KD_SIM_TRENCH = '1' }
    Run "sim" $map $seed "--balance sim `"$A`" `"$B`" $map $seed 1 $SimSec" $envs
  }
}
"SOAKDONE" | Out-File -Append -Encoding utf8 $csv
# LLM-free summary of this soak -> $out\report.md (stdlib python, a few seconds)
& python (Join-Path $root "tools\agent\soak_report.py") $out
